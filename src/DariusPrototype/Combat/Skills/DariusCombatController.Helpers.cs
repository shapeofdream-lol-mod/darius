public sealed partial class DariusCombatController : MonoBehaviour
{
    // ==================== Helpers ====================
    private bool IsValidEnemy(Entity target)
    {
        if (hero == null || target == null || target == hero) return false;
        if (target.IsNullInactiveDeadOrKnockedOut()) return false;
        try { return hero.GetRelation(target).HasFlag(EntityRelation.Enemy); }
        catch { return target is Monster; }
    }

    private void DealPhysical(Entity target, float amount)
    {
        if (target == null || amount <= 0f) return;
        try { hero.DealDamage(hero.PhysicalDamage(amount, 1f), target); }
        catch
        {
            DamageData data = new DamageData(DamageData.SourceType.Physical, amount, 1f);
            data.SetActor(hero);
            data.Dispatch(target);
        }
    }

    private void DealPure(Entity target, float amount)
    {
        if (target == null || amount <= 0f) return;
        // PureDamage is exposed by the public Actor API.
        try { hero.DealDamage(hero.PureDamage(amount, 1f), target); }
        catch
        {
            // Fallback keeps the prototype functional if the exact PureDamage signature changed.
            DamageData data = hero.CreateDamage(DamageData.SourceType.Physical, amount, 1f);
            data.SetActor(hero);
            data.Dispatch(target);
        }
    }

    private void HealSelf(float amount)
    {
        if (amount <= 0f || hero == null) return;
        HealData heal = new HealData(amount);
        heal.SetActor(hero);
        hero.DoHeal(heal, hero);
    }

    private IEnumerator ApplySlow(Entity target, float strength, float duration)
    {
        if (target == null || target.Status == null) yield break;
        StatBonus slow = new StatBonus();
        slow.movementSpeedPercentage = -Mathf.Abs(strength * 100f);
        try { target.Status.AddStatBonus(slow); } catch { yield break; }
        yield return new WaitForSeconds(duration);
        if (target != null && target.Status != null)
        {
            try { target.Status.RemoveStatBonus(slow); } catch { }
        }
    }

    private Entity FindTargetNearAim(float range)
    {
        Vector3 origin = hero.transform.position;
        Vector3 aim = GetAimPoint();
        Collider[] cols = Physics.OverlapSphere(origin, range);
        Entity best = null;
        float bestScore = float.MaxValue;
        HashSet<Entity> seen = new HashSet<Entity>();

        for (int i = 0; i < cols.Length; i++)
        {
            Entity target = cols[i].GetComponentInParent<Entity>();
            if (!IsValidEnemy(target) || !seen.Add(target)) continue;
            float score = PlanarDistance(target.transform.position, aim);
            if (score < bestScore)
            {
                bestScore = score;
                best = target;
            }
        }
        return best;
    }

    private Vector3 GetAimDirection()
    {
        Vector3 delta = Flatten(GetAimPoint() - hero.transform.position);
        if (delta.sqrMagnitude < 0.001f)
            delta = Flatten(hero.transform.forward);
        return delta.normalized;
    }

    private Vector3 GetAimPoint()
    {
        Camera cam = Camera.main;
        if (cam == null) return hero.transform.position + hero.transform.forward * 4f;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, hero.transform.position);
        float enter;
        if (plane.Raycast(ray, out enter))
            return ray.GetPoint(enter);
        return hero.transform.position + hero.transform.forward * 4f;
    }

    private static Vector3 Flatten(Vector3 v) { v.y = 0f; return v; }

    private static float PlanarDistance(Vector3 a, Vector3 b) { return Flatten(a - b).magnitude; }
}