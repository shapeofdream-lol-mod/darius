using UnityEngine;

public sealed class DariusDirectionalBasicAttackState : MonoBehaviour
{
    public Vector3 Direction { get; private set; }
    public float CapturedAt { get; private set; }
    public int ConfigIndex { get; private set; }
    public int Serial { get; private set; }

    public void Capture(Vector3 direction, int configIndex)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
        Direction = direction.normalized;
        ConfigIndex = configIndex;
        CapturedAt = Time.time;
        Serial++;
    }

    public Vector3 GetDirection(Vector3 fallback)
    {
        // A native melee instance resolves very shortly after the swing starts. If some unrelated
        // Actor.DoBasicAttackHit path fires much later, do not reuse a stale attack direction.
        if (Time.time - CapturedAt <= 1.25f && Direction.sqrMagnitude > 0.0001f) return Direction;
        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
    }

    public bool HasFreshCapture => Serial > 0 && Time.time - CapturedAt <= 1.25f;
}

public static class DariusDirectionalBasicAttackGeometry
{
    public static Vector3 ResolveAimDirection(Hero hero, CastInfo info)
    {
        Vector3 direction = Vector3.zero;
        try { Entity target = info.target; if (target != null && target != hero) { direction = target.transform.position - hero.transform.position; direction.y = 0f; } } catch { direction = Vector3.zero; }
        if (direction.sqrMagnitude <= 0.001f)
        {
            try
            {
                direction = info.point - hero.transform.position;
                direction.y = 0f;
            }
            catch { direction = Vector3.zero; }
        }
        if (direction.sqrMagnitude <= 0.001f)
        {
            try { direction = info.forward; direction.y = 0f; } catch { direction = Vector3.zero; }
        }
        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = hero.transform.forward;
            direction.y = 0f;
        }
        if (direction.sqrMagnitude <= 0.001f) direction = Vector3.forward;
        return direction.normalized;
    }

    public static void AnchorNativeMeleeInstance(MeleeAttackInstance instance, CastInfo info, bool critical)
    {
        if (instance == null) return;
        Hero owner = null;
        try { owner = info.caster as Hero; } catch { }
        if (owner == null) return;
        Vector3 direction = ResolveAimDirection(owner, info);
        try
        {
            DariusDirectionalBasicAttackState state = owner.GetComponent<DariusDirectionalBasicAttackState>();
            if (state != null) direction = state.GetDirection(direction);
            instance.transform.position = owner.transform.position;
            if (direction.sqrMagnitude > 0.0001f)
                instance.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            DariusLog.DebugInfo("ATK-INSTANCE", "Anchored " + instance.name + " to caster=" + DariusLog.EntityLabel(owner) +
                " pos=" + DariusLog.Vec(instance.transform.position) + " dir=" + DariusLog.Vec(direction) +
                " crit=" + critical);
        }
        catch (System.Exception e) { DariusLog.Exception("ATK-INSTANCE", e, "Failed to anchor directional native melee instance"); }
    }

    public static bool IsInsideAttackSector(Hero_Darius hero, Entity target, out float contactDistance, out float angle, out Vector3 contactPoint)
    {
        contactDistance = float.PositiveInfinity;
        angle = 180f;
        contactPoint = target != null ? target.transform.position : Vector3.zero;
        if (hero == null || target == null || target == hero) return false;

        Vector3 origin = hero.transform.position;
        contactPoint = ClosestContactPoint(target, origin);
        Vector3 delta = contactPoint - origin;
        delta.y = 0f;
        contactDistance = delta.magnitude;
        if (contactDistance <= 0.001f) { angle = 0f; return true; }
        if (contactDistance > At_DariusAxe.AttackRange + At_DariusAxe.ContactTolerance) return false;

        Vector3 fallback = hero.transform.forward;
        DariusDirectionalBasicAttackState state = hero.GetComponent<DariusDirectionalBasicAttackState>();
        Vector3 direction = state != null ? state.GetDirection(fallback) : fallback;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f) direction = Vector3.forward;
        direction.Normalize();
        angle = Vector3.Angle(direction, delta / contactDistance);
        return angle <= At_DariusAxe.AttackHalfAngle + 0.5f;
    }

    private static Vector3 ClosestContactPoint(Entity target, Vector3 origin)
    {
        Vector3 best = target.transform.position;
        float bestSqr = HorizontalSqr(best - origin);
        try
        {
            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider c = colliders[i];
                if (c == null || !c.enabled) continue;
                Vector3 p = c.ClosestPoint(origin);
                float sqr = HorizontalSqr(p - origin);
                if (sqr < bestSqr) { bestSqr = sqr; best = p; }
            }
        }
        catch { }
        return best;
    }

    private static float HorizontalSqr(Vector3 v) { return v.x * v.x + v.z * v.z; }
}
