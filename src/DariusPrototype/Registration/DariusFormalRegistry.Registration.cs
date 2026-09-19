using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class DariusFormalRegistry
{
    public static void Register()
    {
        if (_registered) return;
        if (DewResources.database == null)
        {
            DariusLog.Warn("REG", "DewResources database is not ready; formal resources were not registered yet.");
            return;
        }

        NoxianMightStatus = RegisterPrefab<Se_Darius_NoxianMight>("Se_Darius_NoxianMight", GuidNoxianMightStatus, status =>
        {
            status.showIcon = true;
            status.icon = DariusPrototypeIcons.Get("H");
            status.isBeneficialBuff = true;
            status.hideOnWorldHealthBar = true;
            status.isCleansable = false;
            status.isKilledByCrowdControlImmunity = false;
            status.scaleDurationByTenacity = false;
            status.topColor = new Color(0.92f, 0.08f, 0.10f, 1f);
            status.bottomColor = new Color(0.28f, 0.02f, 0.03f, 1f);
        });

        // Register clean, warm-initialized AbilityInstance prefabs first. The engineering audit confirmed
        // that Gem/Essence compatibility depends on the stock SkillTrigger -> AbilityInstance ->
        // PrepareAndSpawn chain. These templates live activeSelf=true under an inactive resource root,
        // so cloned Actors have already run Awake before Dew assigns parentActor, without ever becoming
        // live scene actors themselves.
        DecimateAbility = RegisterPrefab<Ai_Darius_Decimate>("Ai_Darius_Decimate", GuidAiQ, null);
        CripplingStrikeAbility = RegisterPrefab<Ai_Darius_CripplingStrike>("Ai_Darius_CripplingStrike", GuidAiW, null);
        ApprehendAbility = RegisterPrefab<Ai_Darius_Apprehend>("Ai_Darius_Apprehend", GuidAiE, null);
        NoxianGuillotineAbility = RegisterPrefab<Ai_Darius_NoxianGuillotine>("Ai_Darius_NoxianGuillotine", GuidAiR, null);

        Decimate = RegisterPrefab<St_Darius_Decimate>("St_Darius_Decimate", GuidQ, skill =>
        {
            ConfigureSkillPrefab(skill, Rarity.Common, 6.0f, "Q", "Q", DecimateAbility, cfg =>
            {
                cfg.alwaysCastImmediately = true;
                cfg.castMethod.type = CastMethodType.None;
                cfg.castMethod._radius = 4.25f;
            });
        });
        CripplingStrike = RegisterPrefab<St_Darius_CripplingStrike>("St_Darius_CripplingStrike", GuidW, skill =>
        {
            ConfigureSkillPrefab(skill, Rarity.Common, 5.0f, "W", "W", CripplingStrikeAbility, cfg =>
            {
                cfg.alwaysCastImmediately = true;
                cfg.castMethod.type = CastMethodType.None;
                cfg.castMethod._radius = 0f;
            });
        });
        Apprehend = RegisterPrefab<St_Darius_Apprehend>("St_Darius_Apprehend", GuidE, skill =>
        {
            ConfigureSkillPrefab(skill, Rarity.Common, 10.0f, "E", "E", ApprehendAbility, cfg =>
            {
                cfg.alwaysCastImmediately = true;
                cfg.faceForward = true;
                cfg.castMethod.type = CastMethodType.Cone;
                cfg.castMethod._radius = 5.35f;
                cfg.castMethod._angle = 60.0f;
            });
        });
        NoxianGuillotine = RegisterPrefab<St_Darius_NoxianGuillotine>("St_Darius_NoxianGuillotine", GuidR, skill =>
        {
            ConfigureSkillPrefab(skill, Rarity.Rare, 24.0f, "R", "R", NoxianGuillotineAbility, cfg =>
            {
                cfg.faceForward = true;
                // Point input + Darius-side target resolver avoids intermittent native Target-mode
                // input rejection before St_Darius_NoxianGuillotine.OnCastComplete is reached.
                cfg.castMethod.type = CastMethodType.Point;
                cfg.castMethod._range = St_Darius_NoxianGuillotine.CastRange;
                cfg.castMethod._radius = St_Darius_NoxianGuillotine.AimPointAssistRadius;
                cfg.castMethod._isClamping = true;
                cfg.alwaysCastImmediately = false;
                cfg.targetValidator = cfg.targetValidator ?? new AbilityTargetValidator();
                cfg.targetValidator.targets = EntityRelation.Enemy;
            });
            skill.type = SkillType.Ultimate;
        });

        // The new summoner-spell movement slot inherits the live Vesper dodge economy.
        // St_M_Charge is the stock Vesper movement trigger in the current game build; if a future
        // build renames it we still register using conservative fallbacks and log the mismatch.
        SkillTrigger nativeMovement = TryResolveNativeSkill("St_M_Charge", "SUMMONER-CONFIG");
        DariusSummonerBalance.InitializeFromNativeMovement(nativeMovement);
        Flash = RegisterPrefab<St_Darius_Flash>("St_Darius_Flash", GuidFlash, skill =>
        {
            ConfigureSummonerSkillPrefab(skill, "FLASH", nativeMovement);
        });
        Ghost = RegisterPrefab<St_Darius_Ghost>("St_Darius_Ghost", GuidGhost, skill =>
        {
            ConfigureSummonerSkillPrefab(skill, "GHOST", nativeMovement);
        });

        Hemorrhage = RegisterPrefab<St_D_Darius_Hemorrhage>("St_D_Darius_Hemorrhage", GuidIdentity, identity =>
        {
            ConfigureIdentityPrefab(identity);
        });

        // Keep the old v0.11 Gem resource loadable for hot-reload/save compatibility, but never
        // put it into normal pools or Deja Vu again. New gameplay uses the Hero_Darius Identity Memory.
        LegacyHemorrhage = RegisterPrefab<Gem_Darius_Hemorrhage>("Gem_Darius_Hemorrhage", GuidGem, gem =>
        {
            gem.rarity = Rarity.Rare;
            gem.excludeFromPool = true;
            gem.isCooldownEnabled = false;
            gem.enableStatBonus = false;
            gem.icon = DariusPrototypeIcons.Get("H");
        });

        // Register Hero_Darius constellation StarEffect resources through the same runtime database/network path.
        // This keeps stars native to the game's constellation/profile system instead of maintaining a parallel save.
        DariusConstellationRegistry.Register();

        // Runtime maps are updated precisely by RegisterObject; do not rebuild the global database here.

        _registered = true;
        DariusLog.Info("REG", "Formal Darius resources registered: Q/W/E/R + Flash/Ghost + Hemorrhage + Noxian Might status + 38 native constellation/equipment stars + legacy Essence + 4 AbilityInstances.");
    }

    public static void DropTestPack(DewPlayer player, bool force)
    {
        if (!_registered) Register();
        if (!_registered || player == null || player.hero == null) return;

        if (!NetworkServer.active)
        {
            DariusLog.Warn("DROP", "Test pack can only be spawned by the host/server.");
            return;
        }

        int heroId = player.hero.GetInstanceID();
        if (!force && _lastDroppedHeroInstanceId == heroId) return;
        _lastDroppedHeroInstanceId = heroId;

        Vector3 center = player.hero.agentPosition;
        try
        {
            Vector3 qPos = GoodDropPosition(center, 2.0f);
            Vector3 wPos = GoodDropPosition(center, 2.2f);
            Vector3 ePos = GoodDropPosition(center, 2.4f);
            Vector3 rPos = GoodDropPosition(center, 2.6f);
            Vector3 hPos = GoodDropPosition(center, 2.8f);
            DariusLog.Info("DROP", "Spawning test pack center=" + DariusLog.Vec(center) +
                " Q=" + DariusLog.Vec(qPos) + " W=" + DariusLog.Vec(wPos) + " E=" + DariusLog.Vec(ePos) +
                " R=" + DariusLog.Vec(rPos) + " H=" + DariusLog.Vec(hPos));
            LogSkillPrefabState("Q", Decimate);
            LogSkillPrefabState("W", CripplingStrike);
            LogSkillPrefabState("E", Apprehend);
            LogSkillPrefabState("R", NoxianGuillotine);

            TrySpawnSkill("Q", Decimate, qPos, player);
            TrySpawnSkill("W", CripplingStrike, wPos, player);
            TrySpawnSkill("E", Apprehend, ePos, player);
            TrySpawnSkill("R", NoxianGuillotine, rPos, player);
            TrySpawnSkill("H-IDENTITY", Hemorrhage, hPos, player);
            DariusLog.Info("DROP", "Internal memory spawn diagnostic completed.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("DROP", e, "Failed to spawn formal test pack");
        }
    }
}