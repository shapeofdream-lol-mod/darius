using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mirror;
using UnityEngine;

public sealed partial class St_Darius_NoxianGuillotine : SkillTrigger
{
    private Entity FindRecoveryTarget(Hero caster)
    {
        if (caster == null) return null;
        try
        {
            ControlManager cm = ManagerBase<ControlManager>.instance;
            Entity soft = cm != null ? cm.targetEnemy : null;
            if (IsValidCastTarget(caster, soft)) return soft;
        }
        catch { }

        Entity best = null;
        float bestDistSq = float.PositiveInfinity;
        try
        {
            Collider[] colliders = Physics.OverlapSphere(caster.transform.position, MaxResolvedCenterRange);
            for (int i = 0; i < colliders.Length; i++)
            {
                Entity candidate = colliders[i] != null ? colliders[i].GetComponentInParent<Entity>() : null;
                if (!IsValidCastTarget(caster, candidate)) continue;
                Vector3 delta = candidate.transform.position - caster.transform.position;
                delta.y = 0f;
                float distSq = delta.sqrMagnitude;
                if (distSq >= bestDistSq) continue;
                bestDistSq = distSq;
                best = candidate;
            }
        }
        catch { }
        return best;
    }
}