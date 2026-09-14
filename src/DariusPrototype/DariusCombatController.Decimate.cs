public sealed partial class DariusCombatController : MonoBehaviour
{
    // ==================== Q: Decimate ====================
    private void TryCastQ()
    {
        if (Time.time < _qReadyAt) return;
        _qReadyAt = Time.time + Q_COOLDOWN;
        StartCoroutine(CastQ());
    }

    private IEnumerator CastQ()
    {
        // Important: movement is deliberately NOT locked during windup.
        DariusPrototypeVfx.CreateQTelegraph(hero.transform, Q_INNER_RADIUS, Q_OUTER_RADIUS, Q_WINDUP);
        yield return new WaitForSeconds(Q_WINDUP);

        if (hero == null || hero.IsNullOrInactive())
            yield break;

        // Soul of the skill: exactly ONE overlap query and ONE resolution per target.
        HashSet<Entity> hit = new HashSet<Entity>();
        Collider[] cols = Physics.OverlapSphere(hero.transform.position, Q_OUTER_RADIUS);
        int outerHits = 0;

        for (int i = 0; i < cols.Length; i++)
        {
            Entity target = cols[i].GetComponentInParent<Entity>();
            if (!IsValidEnemy(target) || !hit.Add(target))
                continue;

            float distance = PlanarDistance(hero.transform.position, target.transform.position);
            bool isInner = distance <= Q_INNER_RADIUS;
            bool isOuter = !isInner && distance <= Q_OUTER_RADIUS;
            if (!isInner && !isOuter)
                continue;

            float ad = hero.Status.finalStats.attackDamage;
            float ratio;

            if (isInner)
            {
                ratio = Q_BASE_AD_RATIO;
            }
            else if (_hemorrhageEnabled)
            {
                // Legacy diagnostic path mirrors the formal skill: Hemorrhage removes the outer damage bonus but keeps outer healing.
                ratio = Q_BASE_AD_RATIO;
                ApplyHemorrhage(target, 1);
                outerHits++;
            }
            else
            {
                ratio = Q_NO_BLEED_OUTER_AD_RATIO;
                outerHits++;
            }

            DealPhysical(target, ad * ratio);
            DariusPrototypeVfx.CreateHitFlash(hero, target.transform.position, isOuter);
        }

        if (outerHits > 0)
        {
            float healFraction = Mathf.Min(Q_HEAL_CAP_MAX_HP, Q_HEAL_MAX_HP_PER_TARGET * outerHits);
            HealSelf(hero.Status.maxHealth * healFraction);
        }

        DariusPrototypeVfx.CreateQSwing(hero, hero.transform.position, Q_INNER_RADIUS, Q_OUTER_RADIUS);
    }
}