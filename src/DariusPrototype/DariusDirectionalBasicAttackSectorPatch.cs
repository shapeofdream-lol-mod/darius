// The stock MeleeAttackInstance remains responsible for native damage/crit/on-hit event semantics,
// but every generated hit must lie in the directional sector. Because the stock melee instance is
// an overlap/AOE actor, all enemies inside the sector can pass this filter during the same swing.
[HarmonyPatch]
public static class DariusDirectionalBasicAttackSectorPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo[] methods = typeof(Actor).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo m = methods[i];
            if (m == null || !string.Equals(m.Name, "DoBasicAttackHit", StringComparison.Ordinal)) continue;
            ParameterInfo[] ps = m.GetParameters();
            bool hasEntity = false;
            for (int j = 0; j < ps.Length; j++)
            {
                if (typeof(Entity).IsAssignableFrom(ps[j].ParameterType)) { hasEntity = true; break; }
            }
            if (hasEntity) yield return m;
        }
    }

    private static bool Prefix(Actor __instance, object[] __args)
    {
        Hero_Darius hero = __instance as Hero_Darius;
        if (hero == null) return true;
        Entity target = null;
        if (__args != null)
        {
            for (int i = 0; i < __args.Length; i++)
            {
                target = __args[i] as Entity;
                if (target != null && !ReferenceEquals(target, __instance)) break;
                target = null;
            }
        }
        if (target == null) return true;

        // Network/animation delay can outlive the short directional snapshot. In that case the
        // custom sector no longer has authoritative intent and must not veto a valid native hit.
        DariusDirectionalBasicAttackState captured = hero.GetComponent<DariusDirectionalBasicAttackState>();
        if (captured == null || !captured.HasFreshCapture) return true;

        try
        {
            float dist, angle; Vector3 contact;
            bool inside = DariusDirectionalBasicAttackGeometry.IsInsideAttackSector(hero, target, out dist, out angle, out contact);
            if (inside)
            {
                DariusLog.DebugInfoThrottled("ATK-SECTOR", "pass:" + target.GetInstanceID(),
                    "PASS target=" + DariusLog.EntityLabel(target) + " contactDist=" + dist.ToString("0.###") +
                    " angle=" + angle.ToString("0.##") + " contact=" + DariusLog.Vec(contact), 0.20);
                return true;
            }
            DariusLog.DebugInfoThrottled("ATK-SECTOR", "block:" + target.GetInstanceID(),
                "BLOCK target=" + DariusLog.EntityLabel(target) + " contactDist=" + dist.ToString("0.###") +
                " angle=" + angle.ToString("0.##") + " range=" + At_DariusAxe.AttackRange.ToString("0.###") +
                " halfAngle=" + At_DariusAxe.AttackHalfAngle.ToString("0.#"), 0.20);
            return false;
        }
        catch (Exception e)
        {
            DariusLog.Exception("ATK-SECTOR", e, "Directional sector validation failed; allowing native hit as fail-safe");
            return true;
        }
    }
}
