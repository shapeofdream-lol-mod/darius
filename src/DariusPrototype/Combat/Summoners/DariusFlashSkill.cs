using System;
using System.Reflection;
using Mirror;
using UnityEngine;

public sealed class St_Darius_Flash : SkillTrigger
{
    protected override void OnPrepare()
    {
        base.OnPrepare();
        configs = new[] { DariusSummonerBalance.CreateFlashConfig(this), DariusSummonerBalance.CreateFlashConfig(this) };
        for (int i = 0; i < configs.Length; i++) configs[i].triggerIcon = DariusPrototypeIcons.Get("FLASH");
        DariusLog.Info("FLASH-CONFIG", "Prepared flash from native movement template=" + DariusSummonerBalance.NativeSourceName);
    }

    protected override void OnLevelChange(int oldLevel, int newLevel)
    {
        if (newLevel < 1) return;
        if (owner == null) { ClientSkillEvent_OnLevelChange?.Invoke(oldLevel, newLevel); return; }
        base.OnLevelChange(oldLevel, newLevel);
    }

    public override AbilityInstance OnCastComplete(int configIndex, CastInfo info)
    {
        AbilityInstance result = null;
        try { result = base.OnCastComplete(configIndex, info); }
        catch (Exception e) { DariusLog.Exception("FLASH", e, "Stock movement cooldown bookkeeping failed"); }
        try
        {
            Execute(info);
            Hero ownerHero = info.caster as Hero;
            if (ownerHero != null) DariusConstellationRuntime.NotifyMovementSpell(ownerHero, this, "Flash");
        }
        catch (Exception e) { DariusLog.Exception("FLASH", e, "Flash execution failed"); }
        return result;
    }

    private static void Execute(CastInfo info)
    {
        Hero owner = info.caster as Hero;
        if (owner == null) return;
        Vector3 dir = DariusSummonerRuntime.ResolveDirection(owner, info);
        Vector3 start = owner.transform.position;
        Vector3 destination = DariusFlashWarp.ResolveDestination(owner, start, dir, DariusSummonerBalance.FlashDistance);
        DariusPrototypeVfx.CreateSummonerFlash(start, destination);
        if (NetworkServer.active)
        {
            bool forcedPhase = DariusFlashWarp.TeleportThroughUnits(owner, start, destination, dir);
            DariusLog.Info("FLASH", "Teleported " + DariusLog.EntityLabel(owner) + " baseCooldown=" + DariusSummonerBalance.FlashCooldown.ToString("0.###") +
                " distance=" + Vector3.Distance(start, destination).ToString("0.###") + " forcedPhase=" + forcedPhase +
                " from=" + DariusLog.Vec(start) + " requested=" + DariusLog.Vec(destination) + " actual=" + DariusLog.Vec(owner.transform.position));
        }
    }
}

// Flash is a teleport, not a dash. Unit bodies must never shorten its path. We ray-test only
// non-Entity world colliders to keep walls/terrain blocking, then use the game's Teleport for
// network/state bookkeeping. If native collision resolution still clamps the endpoint against a
// unit body, the server force-places the Hero at the already world-validated endpoint.
public static class DariusFlashWarp
{
    private const float ProbeHeight = 0.45f;
    private const float WallBackoff = 0.16f;
    private const float NativeClampTolerance = 0.30f;

    public static Vector3 ResolveDestination(Hero owner, Vector3 start, Vector3 direction, float distance)
    {
        if (owner == null) return start;
        Vector3 dir = direction;
        dir.y = 0f;
        if (dir.sqrMagnitude <= 0.001f) return start;
        dir.Normalize();
        float allowed = Mathf.Max(0f, distance);
        try
        {
            Vector3 origin = start + Vector3.up * ProbeHeight;
            RaycastHit[] hits = Physics.RaycastAll(origin, dir, allowed, ~0, QueryTriggerInteraction.Ignore);
            float nearestWorld = allowed;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                Collider collider = hit.collider;
                if (collider == null || IsUnitCollider(collider, owner)) continue;
                if (hit.distance > 0.01f && hit.distance < nearestWorld) nearestWorld = hit.distance;
            }
            allowed = Mathf.Max(0f, nearestWorld - (nearestWorld < distance ? WallBackoff : 0f));
        }
        catch (Exception e)
        {
            DariusLog.Exception("FLASH-PHASE", e, "World obstruction probe failed; falling back to requested Flash endpoint");
        }
        return start + dir * allowed;
    }

    public static bool TeleportThroughUnits(Hero owner, Vector3 start, Vector3 destination, Vector3 direction)
    {
        if (owner == null) return false;
        Vector3 dir = direction;
        dir.y = 0f;
        if (dir.sqrMagnitude <= 0.001f) dir = destination - start;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f) dir.Normalize();

        owner.Teleport(owner, destination);
        try { Physics.SyncTransforms(); } catch { }

        float intended = Vector3.Distance(start, destination);
        Vector3 afterNative = owner.transform.position;
        float progressed = dir.sqrMagnitude > 0.001f ? Vector3.Dot(afterNative - start, dir) : Vector3.Distance(start, afterNative);
        if (intended <= 0.01f || progressed + NativeClampTolerance >= intended) return false;

        // ResolveDestination already stopped at the first non-unit world collider. A shorter native
        // result here is therefore unit-body collision/overlap resolution and is safe to bypass.
        ForcePosition(owner, destination);
        return true;
    }

    private static bool IsUnitCollider(Collider collider, Hero owner)
    {
        if (collider == null) return false;
        try
        {
            if (owner != null && (collider.transform == owner.transform || collider.transform.IsChildOf(owner.transform))) return true;
            Entity entity = collider.GetComponentInParent<Entity>();
            if (entity != null) return true;
        }
        catch { }
        return false;
    }

    private static void ForcePosition(Hero owner, Vector3 destination)
    {
        try
        {
            owner.transform.position = destination;
            Rigidbody body = owner.GetComponent<Rigidbody>();
            if (body != null) body.position = destination;

            // Keep a NavMesh-backed movement component in sync without taking a compile-time
            // dependency on UnityEngine.AIModule. Only components explicitly named as NavMesh
            // agents are considered, so arbitrary gameplay components with a Warp method are not called.
            Component[] components = owner.GetComponents<Component>();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null) continue;
                Type type = component.GetType();
                string typeName = type.FullName ?? type.Name;
                if (typeName.IndexOf("NavMeshAgent", StringComparison.OrdinalIgnoreCase) < 0) continue;
                MethodInfo warp = type.GetMethod("Warp", flags, null, new[] { typeof(Vector3) }, null);
                if (warp != null)
                {
                    try { warp.Invoke(component, new object[] { destination }); } catch { }
                }
            }
            try { Physics.SyncTransforms(); } catch { }
        }
        catch (Exception e)
        {
            DariusLog.Exception("FLASH-PHASE", e, "Failed to force world-validated Flash endpoint");
        }
    }
}
