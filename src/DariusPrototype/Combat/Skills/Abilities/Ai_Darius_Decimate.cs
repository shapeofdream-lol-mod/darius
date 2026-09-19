using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

// Darius Q. Core invariant: one delayed resolution, one physics snapshot,
// mutually-exclusive inner/outer classification, one damage event per target.
public sealed partial class Ai_Darius_Decimate : AbilityInstance
{
    public const float Windup = 0.75f;

    public const float InnerRadius = 2.40f;

    public const float OuterRadius = 4.25f;

    // PvE baseline: Q has a real opportunity cost because Darius must deliberately space into the
    // axe edge during a 0.75 s windup. Its sustain therefore needs to repay the damage taken while
    // setting up the outer ring, rather than copying the lower PvP values from League.
    public const float InnerAdRatio = 1.50f;

    public const float OuterNoEssenceAdRatio = 2.25f;

    public const float HealMaxHpPerOuterTarget = 0.10f;

    public const float HealCapMaxHp = 0.50f; // Five outer targets = 50% max Health.

    public const float DamageGrowthPerAdditionalLevel = 0.20f;

    public static float DamageRatioAtLevel(float baseRatio, int level)
    {
        return DariusMemoryScaling.Linear(baseRatio, level, DamageGrowthPerAdditionalLevel);
    }

    // Q already receives continuous damage growth. Sustain is deliberately stable so an infinitely
    // levelled Memory does not become permanent full-healing: spacing into the outer blade is worth
    // 10% per enemy from level 1 onward, capped at five enemies / 50% before constellation modifiers.
    public static float HealPerTargetAtLevel(int level)
    {
        return HealMaxHpPerOuterTarget;
    }

    public static float HealCapAtLevel(int level)
    {
        return HealCapMaxHp;
    }

    protected override void OnCreate()
    {
        base.OnCreate();
        StartCoroutine(RunExecute());
    }

    private IEnumerator RunExecute()
    {
        yield return Execute(info, firstTrigger, this);
        if (NetworkServer.active) Destroy();
    }
}