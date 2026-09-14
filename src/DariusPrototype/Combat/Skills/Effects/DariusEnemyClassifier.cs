public enum DariusEnemyClass
{
    Common,
    Elite,
    Boss
}

public static partial class DariusEnemyClassifier
{
    private static readonly string[] BossTokens = { "boss", "guardian", "finalboss", "final_boss", "dreamer", "nightmare" };

    private static readonly string[] StrongBossTokens = { "boss", "finalboss", "final_boss" };

    private static readonly string[] EliteTokens = { "elite", "champion", "miniboss", "mini_boss", "mini-boss", "mini boss", "midboss", "subboss", "rare", "special" };

    private static readonly string[] StrongEliteTokens = { "elite", "champion", "miniboss", "mini_boss", "mini-boss", "mini boss", "midboss", "subboss" };

    private static readonly string[] BossBoolNames = { "isBoss", "IsBoss", "boss", "isMajorBoss", "IsMajorBoss", "isFinalBoss", "IsFinalBoss" };

    private static readonly string[] EliteBoolNames = {
        "isElite", "IsElite", "elite", "isChampion", "IsChampion", "champion",
        "isMiniBoss", "IsMiniBoss", "isMiniboss", "IsMiniboss", "miniBoss", "miniboss",
        "isRare", "IsRare", "rare"
    };

    private static readonly string[] RankMemberTokens = { "rank", "tier", "grade", "rarity", "class", "elite", "boss", "champion", "mini" };

    private static readonly string[] ModifierCollectionTokens = { "status", "effect", "buff", "modifier", "affix", "trait" };

    // Shape of Dreams can promote an ordinary monster prefab into a mini-boss at runtime.
    // The Entity name therefore remains a normal Mon_* name while one or more mini-boss
    // modifiers are attached. These are confirmed public modifier names / mini-boss archetypes
    // plus generic internal naming forms so Noxian Arena does not depend on one exact game build.
    private static readonly string[] MiniBossModifierTokens = {
        "minibossmodifier", "mini_boss_modifier", "minibossaffix", "mini_boss_affix",
        "bloodthorn", "blood_thorn", "auraofcold", "aura_of_cold", "arrowstorm", "arrow_storm"
    };

    private static readonly string[] KnownMiniBossArchetypeTokens = {
        "redgiant", "red_giant", "phasebug", "phase_bug", "spinningarrow", "spinning_arrow"
    };

    public static DariusEnemyClass Classify(Entity entity)
    {
        string reason;
        return Classify(entity, out reason);
    }

    // Elite/miniboss status in Shape of Dreams is not guaranteed to live on the concrete monster
    // class itself. Some encounters promote an ordinary monster prefab at runtime, so looking only
    // at entity.name / entity.GetType() can classify a miniboss as Common. Inspect the full runtime
    // entity/component/status graph when Noxian Arena evaluates nearby enemies. The arena runtime throttles
    // this classification pass to a 0.25-second cadence rather than running it every frame.
    public static DariusEnemyClass Classify(Entity entity, out string reason)
    {
        reason = "null/common";
        if (entity == null) return DariusEnemyClass.Common;

        string member;
        if (ReadBoolRecursive(entity, BossBoolNames, out member))
        {
            reason = "entity-bool:" + member;
            return DariusEnemyClass.Boss;
        }
        if (ReadBoolRecursive(entity, EliteBoolNames, out member))
        {
            reason = "entity-bool:" + member;
            return DariusEnemyClass.Elite;
        }

        DariusEnemyClass rankKind;
        if (TryReadRankMarker(entity, out rankKind, out member))
        {
            reason = "entity-rank:" + member;
            return rankKind;
        }

        // The critical v0.1.7 gap: elite/miniboss flags can be stored on EntityStatus, EntityAI,
        // encounter modifiers, health-bar/rank components, or a child runtime component rather than
        // on Entity. Scan those components before falling back to prefab naming.
        Component[] components = null;
        try { components = entity.GetComponentsInChildren<Component>(true); } catch { }
        if (components != null)
        {
            for (int i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                if (c == null || ReferenceEquals(c, entity)) continue;

                if (ReadBoolRecursive(c, BossBoolNames, out member))
                {
                    reason = "component-bool:" + c.GetType().Name + "." + member;
                    return DariusEnemyClass.Boss;
                }
                if (ReadBoolRecursive(c, EliteBoolNames, out member))
                {
                    reason = "component-bool:" + c.GetType().Name + "." + member;
                    return DariusEnemyClass.Elite;
                }
                if (TryReadRankMarker(c, out rankKind, out member))
                {
                    reason = "component-rank:" + c.GetType().Name + "." + member;
                    return rankKind;
                }

                string componentLabel = SafeLabel(c);
                if (ContainsAny(componentLabel, StrongBossTokens))
                {
                    reason = "component-name:" + c.GetType().Name;
                    return DariusEnemyClass.Boss;
                }
                if (ContainsAny(componentLabel, StrongEliteTokens))
                {
                    reason = "component-name:" + c.GetType().Name;
                    return DariusEnemyClass.Elite;
                }

                if (TryScanModifierCollections(c, out rankKind, out member))
                {
                    reason = "component-modifier:" + c.GetType().Name + "." + member;
                    return rankKind;
                }
            }
        }

        // Also scan collections directly exposed by Entity in case StatusEffects/Affixes are actor
        // objects rather than MonoBehaviours in the hierarchy.
        if (TryScanModifierCollections(entity, out rankKind, out member))
        {
            reason = "entity-modifier:" + member;
            return rankKind;
        }

        string label = SafeLabel(entity);
        if (ContainsAny(label, KnownMiniBossArchetypeTokens))
        {
            reason = "known-miniboss-archetype:" + label;
            return DariusEnemyClass.Elite;
        }
        if (ContainsAny(label, BossTokens))
        {
            reason = "entity-name:" + label;
            return DariusEnemyClass.Boss;
        }
        if (ContainsAny(label, EliteTokens))
        {
            reason = "entity-name:" + label;
            return DariusEnemyClass.Elite;
        }

        // Late native-behaviour fallback. Mini-boss/empowered monsters use the same native
        // crowd-control-immunity gate that blocks standard KnockUp/Knockback. Only use this
        // after every explicit Boss/Elite marker above, so a named Boss never gets demoted.
        try
        {
            if (entity.Status != null && entity.Status.hasCrowdControlImmunity)
            {
                reason = "native-cc-immunity";
                return DariusEnemyClass.Elite;
            }
        }
        catch { }

        reason = "no-runtime-rank-marker";
        return DariusEnemyClass.Common;
    }
}