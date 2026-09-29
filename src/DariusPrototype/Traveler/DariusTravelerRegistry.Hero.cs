using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class DariusTravelerRegistry
{
    private static void ClearArrayMember(object owner, string memberName)
    {
        if (owner == null || string.IsNullOrEmpty(memberName)) return;
        Type t = owner.GetType();
        FieldInfo field = FindFieldRecursive(t, memberName);
        if (field != null && field.FieldType.IsArray)
        {
            try { field.SetValue(owner, Array.CreateInstance(field.FieldType.GetElementType(), 0)); } catch { }
            return;
        }
        PropertyInfo property = t.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite && property.PropertyType.IsArray)
        {
            try { property.SetValue(owner, Array.CreateInstance(property.PropertyType.GetElementType(), 0), null); } catch { }
        }
    }

    private static void CreateAndRegisterHero()
    {
        Hero source = DewResources.GetByShortTypeName<Hero>("Hero_Vesper");
        if (source == null) throw new InvalidOperationException("Could not resolve stock Hero_Vesper prefab.");

        GameObject go = UnityEngine.Object.Instantiate(source.gameObject, _resourceRoot.transform, false);
        // The parent resourceRoot is inactive; activeSelf remains true for future spawned clones.
        go.SetActive(true);
        go.name = HeroName;
        go.hideFlags = HideFlags.None;

        Hero oldHero = go.GetComponent<Hero>();
        if (oldHero == null) throw new InvalidOperationException("Cloned Hero_Vesper has no Hero component.");
        Dictionary<FieldInfo, object> serializedHeroFields = CaptureUnitySerializedFields(oldHero, typeof(Hero));

        // Rewire the generic component graph BEFORE destroying the template Hero component.
        // Destroying first turns oldHero into Unity-null and made the previous reference rebinder a
        // no-op, leaving EntityStatus/EntityAbility/etc. with stale MissingReference owners. This
        // manifested as a dead basic-attack path and nonsensical 500-valued detail-panel entries.
        Hero_Darius hero = go.AddComponent<Hero_Darius>();
        RestoreUnitySerializedFields(hero, serializedHeroFields);
        ReplaceDirectComponentReferences(go, oldHero, hero);
        int heroResidualRefs = CountDirectComponentReferences(go, oldHero, hero);
        if (heroResidualRefs > 0)
            DariusLog.Warn("TRAVELER-COPY", "Hero component graph still has " + heroResidualRefs + " direct references to old Hero_Vesper before destruction.");
        UnityEngine.Object.DestroyImmediate(oldHero);
        hero.name = HeroName;
        // Phase 1 still uses a stock melee Hero GameObject only as a serialized generic component graph.
        // The Hero_Vesper component itself has already been removed/replaced above; Skin, attack,
        // Identity and movement loadout references are then rebound to Darius-owned resources.
        EntityAbility entityAbility = go.GetComponent<EntityAbility>();
        if (entityAbility == null) throw new InvalidOperationException("Cloned Hero prefab has no EntityAbility component.");
        if (AttackPrefab == null) throw new InvalidOperationException("At_DariusAxe must be registered before Hero_Darius.");
        entityAbility.attackAbilityPreset = new AssetRef<AttackTrigger>(AttackPrefab);
        ClearEntityAbilityRuntimeAttackState(entityAbility);
        DariusNativeAttackBinder binder = go.GetComponent<DariusNativeAttackBinder>();
        if (binder == null) binder = go.AddComponent<DariusNativeAttackBinder>();
        binder.EnsureBound("Hero_Darius prefab construction");
        RepairHeroBaseStatContract(source, hero);
        RepairHeroGrowthStatContract(source, hero);
        DariusLog.Info("ATK-NATIVE", "Hero_Darius EntityAbility.attackAbilityPreset -> At_DariusAxe with a per-Hero native binder. Vesper attack trigger is no longer the runtime attack preset.");

        // Reuse the stock generic HeroSkill component, but replace the actual loadout arrays.
        HeroSkill heroSkill = go.GetComponent<HeroSkill>();
        if (heroSkill == null) throw new InvalidOperationException("Cloned Hero prefab has no HeroSkill component.");
        // These must be the real serialized arrays: both the native UI and server-side
        // HeroLoadoutData.Validate_Imp read these exact arrays. AssetRef<T> is a public
        // Dew.Core value type and the skill resources are already registered at this point.
        heroSkill.loadoutQ = new[] { new AssetRef<SkillTrigger>(DariusFormalRegistry.Decimate) };
        heroSkill.loadoutR = new[] { new AssetRef<SkillTrigger>(DariusFormalRegistry.NoxianGuillotine) };
        heroSkill.loadoutTrait = new[] { new AssetRef<SkillTrigger>(DariusFormalRegistry.Hemorrhage) };
        heroSkill.loadoutMovement = new[]
        {
            new AssetRef<SkillTrigger>(DariusFormalRegistry.Flash),
            new AssetRef<SkillTrigger>(DariusFormalRegistry.Ghost)
        };
        DariusLog.Info("TRAVELER-LOADOUT", "Native AssetRef arrays configured Q/R/Identity; Movement replaced with selectable Flash/Ghost.");

        // Runtime container exists on every Hero_Darius instance before native constellation effects
        // are created. StarEffect.OnStartServer then only has to populate the equipped star levels.
        if (go.GetComponent<DariusConstellationRuntime>() == null)
            go.AddComponent<DariusConstellationRuntime>();

        hero.cDestruction.defaultCount = 4; hero.cDestruction.maxCount = 6; hero.cDestruction.angleOffset = 0f;
        hero.cLife.defaultCount = 2; hero.cLife.maxCount = 4; hero.cLife.angleOffset = 15f;
        hero.cImagination.defaultCount = 1; hero.cImagination.maxCount = 4; hero.cImagination.angleOffset = -15f;
        hero.cFlexible.defaultCount = 2; hero.cFlexible.maxCount = 4; hero.cFlexible.angleOffset = 30f;
        try
        {
            DariusLog.Info("CONSTELLATION-SETTINGS", "Darius Noxian offense slot profile " +
                "D=" + hero.cDestruction.defaultCount + "/" + hero.cDestruction.maxCount +
                " L=" + hero.cLife.defaultCount + "/" + hero.cLife.maxCount +
                " I=" + hero.cImagination.defaultCount + "/" + hero.cImagination.maxCount +
                " F=" + hero.cFlexible.defaultCount + "/" + hero.cFlexible.maxCount);
        }
        catch (Exception e)
        {
            DariusLog.Exception("CONSTELLATION-SETTINGS", e, "Could not log inherited constellation slot settings; values were left untouched.");
        }

        // Bind the public-facing hero presentation to Darius-owned cosmetic resources.
        Sprite heroPortrait = DariusPrototypeIcons.Get("HERO");
        // Native hero icon contract: UI_HeroIcon / UI_InGame_HeroInfoBar read Hero.icon and
        // Hero.mainColor. Set those exact resource fields and let stock UI render them.
        TryAssignIfCompatible(hero, "icon", heroPortrait);
        TryAssignIfCompatible(hero, "mainColor", new Color(0.36f, 0.075f, 0.055f, 1f));
        RepairHeroCosmeticContract(hero);

        NetworkIdentity identity = go.GetComponent<NetworkIdentity>();
        if (identity == null) throw new InvalidOperationException("Cloned Hero prefab has no NetworkIdentity.");
        DariusUnsupportedResourceBridge.ConfigureTemplateIdentity(identity, HeroAssetId, HeroName);
        DariusUnsupportedResourceBridge.RebuildNetworkBehaviours(identity, HeroName);

        HeroPrefab = hero;
        ValidateNativeCosmeticIconContract("hero-created");
        OwnedObjects.Add(go);
        RegisterTypedResource(hero, go, HeroName, HeroGuid, HeroAssetId);
        DariusLog.Info("TRAVELER-HERO", "Created Hero_Darius from generic stock entity component graph; Darius Skin + At_DariusAxe are independent runtime resources; movement choices=Flash/Ghost.");
    }
}