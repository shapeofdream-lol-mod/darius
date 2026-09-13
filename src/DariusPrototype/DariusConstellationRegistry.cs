using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DariusConstellationRegistry
{
    private static readonly List<Type> InjectedTypes = new List<Type>();
    private static readonly Dictionary<Type, string> IconKeyByType = new Dictionary<Type, string>();
    private static bool _registered;

    public static void Register()
    {
        if (_registered) return;

        RegisterStar<Se_Star_Darius_D_Conqueror>(DariusConstellationIds.DConqueror, "894ce37e-073e-5b17-89c8-3d6cd7c57928", StarType.Destruction, 4, 0, "RUNE_CONQUEROR");
        RegisterStar<Se_Star_Darius_D_Triumph>(DariusConstellationIds.DTriumph, "c3fce88d-744b-5ef8-8ca7-f37d7ed90bf5", StarType.Destruction, 4, 3, "RUNE_TRIUMPH");
        RegisterStar<Se_Star_Darius_D_Alacrity>(DariusConstellationIds.DAlacrity, "835c8764-0ec9-5e4f-b6e3-b2db267ff25a", StarType.Destruction, 4, 5, "RUNE_ALACRITY");
        RegisterStar<Se_Star_Darius_D_LastStand>(DariusConstellationIds.DLastStand, "0e878932-c89c-53fe-8b8d-c234c721e3d0", StarType.Destruction, 4, 10, "RUNE_LASTSTAND");
        RegisterStar<Se_Star_Darius_D_AxiomArcanist>(DariusConstellationIds.DAxiomArcanist, "116da196-1efa-5be7-bd37-cd6e17dfaa07", StarType.Flexible, 4, 20, "RUNE_AXIOM");
        RegisterStar<Se_Star_Darius_D_NoxianArena>(DariusConstellationIds.DNoxianArena, "43f38fd7-d6ff-4ee0-be5f-96fa01fc41df", StarType.Destruction, 4, 30, "RUNE_LASTSTAND");

        RegisterStar<Se_Star_Darius_L_SecondWind>(DariusConstellationIds.LSecondWind, "3cb0e5c7-2568-5dc4-9e83-635903c016bb", StarType.Life, 4, 0, "RUNE_SECONDWIND");
        RegisterStar<Se_Star_Darius_L_Overgrowth>(DariusConstellationIds.LOvergrowth, "aa078769-474e-503f-8cea-4a79aef40e94", StarType.Life, 4, 5, "RUNE_OVERGROWTH");
        RegisterStar<Se_Star_Darius_L_Revitalize>(DariusConstellationIds.LRevitalize, "bace7cd6-b89d-5677-b991-cb3451add034", StarType.Flexible, 4, 15, "RUNE_REVITALIZE");
        RegisterStar<Se_Star_Darius_L_Conditioning>(DariusConstellationIds.LConditioning, "b001793e-c937-5343-8d12-bf19ae1b6c80", StarType.Life, 4, 10, "RUNE_CONDITIONING");
        RegisterStar<Se_Star_Darius_L_Unflinching>(DariusConstellationIds.LUnflinching, "4167e69a-c3fb-5e58-a388-8e15d2fb3437", StarType.Life, 4, 15, "RUNE_UNFLINCHING");

        RegisterStar<Se_Star_Darius_I_CripplingStrike>(DariusConstellationIds.ICripplingStrike, "c967018d-e824-5ed9-992c-fffded3be39b", StarType.Flexible, 4, 10, "W");
        RegisterStar<Se_Star_Darius_I_Apprehend>(DariusConstellationIds.IApprehend, "b86b8872-d105-5770-9965-35f6ce53b365", StarType.Flexible, 4, 10, "E");
        RegisterStar<Se_Star_Darius_I_InstantDecimate>(DariusConstellationIds.IInstantDecimate, "28a6288e-c433-5f64-bb8e-b1c5347dd808", StarType.Flexible, 4, 25, "Q");
        RegisterStar<Se_Star_Darius_I_WarFervor>(DariusConstellationIds.IWarFervor, "945d0dde-705b-5d19-ae76-12f186c63b6d", StarType.Flexible, 4, 25, "RUNE_FERVOR");
        RegisterStar<Se_Star_Darius_I_LegacyCripplingStrike>(DariusConstellationIds.ILegacyCripplingStrike, "6b98a930-704c-527a-bec2-be6a2e5b8ffa", StarType.Flexible, 4, 10, "W");
        RegisterStar<Se_Star_Darius_I_LegacyApprehend>(DariusConstellationIds.ILegacyApprehend, "2ecf521e-bc1c-51d5-9d40-9a9bd8653bed", StarType.Flexible, 4, 15, "E");
        RegisterStar<Se_Star_Darius_I_LegacyGuillotine>(DariusConstellationIds.ILegacyGuillotine, "e47dcb4b-fd61-5b21-a642-cfda095f77ab", StarType.Flexible, 4, 25, "R");
        RegisterStar<Se_Star_Darius_D_BloodRush>(DariusConstellationIds.DBloodRush, "8ee908ba-b0e3-5d2b-a5a1-33f967b26585", StarType.Destruction, 4, 5, "RUNE_CELERITY");
        RegisterStar<Se_Star_Darius_D_NoxianMight>(DariusConstellationIds.DNoxianMight, "b2c4a927-911b-59ea-9768-13ae87b20dea", StarType.Destruction, 4, 20, "RUNE_CONQUEROR");
        RegisterStar<Se_Star_Darius_D_Dunkmaster>(DariusConstellationIds.DDunkmaster, "53c567a4-0e95-5017-b044-3c3d238cf999", StarType.Destruction, 4, 15, "R");
        RegisterStar<Se_Star_Darius_L_BloodPrice>(DariusConstellationIds.LBloodPrice, "77aba1d0-22cb-5ed2-a279-155dcf485387", StarType.Life, 4, 10, "RUNE_TRIUMPH");
        RegisterStar<Se_Star_Darius_F_HandOfNoxus>(DariusConstellationIds.FHandOfNoxus, "4a8537b1-2711-59db-8cac-c5bb5e08f287", StarType.Imagination, 3, 15, "H");

        RegisterStar<Se_Star_Darius_F_NimbusCloak>(DariusConstellationIds.FNimbusCloak, "0a1205ba-949f-538b-89de-650e8bf0cfe6", StarType.Imagination, 3, 3, "RUNE_NIMBUS");
        RegisterStar<Se_Star_Darius_F_Celerity>(DariusConstellationIds.FCelerity, "67dc3cdd-77fc-51a5-b556-9cdc4105a610", StarType.Imagination, 3, 5, "RUNE_CELERITY");
        RegisterStar<Se_Star_Darius_F_GatheringStorm>(DariusConstellationIds.FGatheringStorm, "adf3a77f-87a2-55cb-9eda-9a64a73c58b3", StarType.Imagination, 3, 15, "RUNE_GATHERING");
        RegisterStar<Se_Star_Darius_F_CosmicInsight>(DariusConstellationIds.FCosmicInsight, "9d2070b3-dcc1-5260-a19a-ebc37da299da", StarType.Flexible, 3, 10, "RUNE_COSMIC");

        // Pass 2: ten LoL equipment constellations plus the user-designed R-mechanic star.
        // Flexible remains strictly reserved for skill-mechanic rewrites.
        RegisterStar<Se_Star_Darius_D_ItemTrinityForce>(DariusEquipmentStarIds.TrinityForce, "948aaa47-89a7-510f-b903-185af94a63d8", StarType.Destruction, 4, 5, "ITEM_TRINITY");
        RegisterStar<Se_Star_Darius_D_ItemBlackCleaver>(DariusEquipmentStarIds.BlackCleaver, "dc43f66e-ad4d-5287-b766-a39b9937104c", StarType.Destruction, 4, 10, "ITEM_BLACK_CLEAVER");
        RegisterStar<Se_Star_Darius_D_ItemSpearOfShojin>(DariusEquipmentStarIds.SpearOfShojin, "30010cc5-ff6e-5f2c-a238-c838461d9af8", StarType.Destruction, 4, 20, "ITEM_SHOJIN");
        RegisterStar<Se_Star_Darius_L_ItemSteraksGage>(DariusEquipmentStarIds.SteraksGage, "99c89b92-0a78-515c-a33d-468f2d6a630d", StarType.Life, 4, 5, "ITEM_STERAK");
        RegisterStar<Se_Star_Darius_L_ItemDeathsDance>(DariusEquipmentStarIds.DeathsDance, "247a5dbf-e358-5318-938f-61a192e14613", StarType.Life, 4, 15, "ITEM_DEATHS_DANCE");
        RegisterStar<Se_Star_Darius_L_ItemOverlordsBloodmail>(DariusEquipmentStarIds.OverlordsBloodmail, "a65d70c4-9105-541e-ba11-e9c482d69e4f", StarType.Life, 4, 25, "ITEM_BLOODMAIL");
        RegisterStar<Se_Star_Darius_I_ItemSunderedSky>(DariusEquipmentStarIds.SunderedSky, "62de3a6e-8b96-5a3c-9456-7ec3aa1e0279", StarType.Imagination, 4, 10, "ITEM_SUNDERED_SKY");
        RegisterStar<Se_Star_Darius_I_ItemDeadMansPlate>(DariusEquipmentStarIds.DeadMansPlate, "1bfcb1f3-df0f-5991-9240-42ad1a0af969", StarType.Imagination, 4, 10, "ITEM_DEAD_MANS");
        RegisterStar<Se_Star_Darius_I_ItemYoumuusGhostblade>(DariusEquipmentStarIds.YoumuusGhostblade, "25ed20cd-9fde-53ce-8529-b05a14cdb8e3", StarType.Imagination, 4, 10, "ITEM_YOUMUU");
        RegisterStar<Se_Star_Darius_F_ItemStridebreaker>(DariusEquipmentStarIds.Stridebreaker, "cde73341-54dc-5949-bf65-153a4e29d140", StarType.Flexible, 4, 20, "ITEM_STRIDEBREAKER");
        RegisterStar<Se_Star_Darius_F_Awoo>(DariusEquipmentStarIds.Awoo, "88e91c5f-91cc-5f51-9749-86a3e18950f1", StarType.Flexible, 1, 30, "STAR_AWOO");

        _registered = true;
        DariusLog.Info("CONSTELLATION", "Registered 38 native StarEffect resources including Noxian Arena, 10 equipment stars and Awoo R-mechanic star.");
    }

    private static void RegisterStar<T>(string name, string guid, StarType type, int maxLevel, int requiredLevel, string iconKey) where T : DariusStarEffect
    {
        guid = "sod-darius-star-" + guid;
        IconKeyByType[typeof(T)] = iconKey;
        T star = DariusFormalRegistry.RegisterRuntimePrefab<T>(name, guid, s =>
        {
            s.type = type;
            DariusConstellationReflection.ConfigureProgression(s, maxLevel, requiredLevel, iconKey);
        });
        InjectStarType(typeof(T));
        DariusLog.Info("CONSTELLATION-STAR", name + " type=" + type + " maxLevel=" + maxLevel + " requiredLevel=" + requiredLevel + " prefab=" + (star != null));
    }


    public static bool TryGetIconKey(StarEffect star, out string iconKey)
    {
        iconKey = null;
        return star != null && IconKeyByType.TryGetValue(star.GetType(), out iconKey) && !string.IsNullOrEmpty(iconKey);
    }

    public static bool TryGetIconKey(Type starType, out string iconKey)
    {
        iconKey = null;
        return starType != null && IconKeyByType.TryGetValue(starType, out iconKey) && !string.IsNullOrEmpty(iconKey);
    }

    private static bool IsOwnedStarType(Type type)
    {
        return type != null && !string.IsNullOrEmpty(type.Name) && type.Name.StartsWith("Se_Star_Darius_", StringComparison.Ordinal);
    }

    private static bool IsSameSemanticStarType(Type left, Type right)
    {
        if (left == null || right == null) return false;
        if (!IsOwnedStarType(left) || !IsOwnedStarType(right)) return false;
        return string.Equals(left.FullName, right.FullName, StringComparison.Ordinal) ||
               string.Equals(left.Name, right.Name, StringComparison.Ordinal);
    }

    private static void InjectStarType(Type starType)
    {
        if (starType == null) return;
        try
        {
            // Force Dew's lazy cache to initialize, but never manufacture an empty replacement list.
            PropertyInfo lazyProperty = typeof(Dew).GetProperty("allStarTypes", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (lazyProperty != null && lazyProperty.CanRead)
            {
                try { lazyProperty.GetValue(null, null); } catch { }
            }

            FieldInfo field = typeof(Dew).GetField("_allStarTypes", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null)
            {
                DariusLog.Warn("CONSTELLATION", "Dew._allStarTypes was not found; star type cache injection skipped for " + starType.Name);
                return;
            }
            List<Type> list = field.GetValue(null) as List<Type>;
            if (list == null)
            {
                DariusLog.Warn("CONSTELLATION", "Dew._allStarTypes is not initialized yet; deferred " + starType.Name);
                return;
            }

            // Dew hot reload loads a timestamped copy of the DLL. The old CLR Type and the new CLR
            // Type are therefore different objects even though they represent the same star resource.
            // Type-identity dedupe (`List.Contains`) is insufficient and caused one visible duplicate
            // constellation after every unrelated Mod add/remove. Collapse stale semantic duplicates
            // before the current type is added.
            int staleRemoved = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Type existing = list[i];
                if (existing == starType) continue;
                if (!IsSameSemanticStarType(existing, starType)) continue;
                list.RemoveAt(i);
                staleRemoved++;
            }
            if (staleRemoved > 0)
                DariusLog.Warn("CONSTELLATION-RELOAD", "Removed " + staleRemoved + " stale hot-reload Type instance(s) for " + starType.Name + " before re-registration.");

            if (!list.Contains(starType)) list.Add(starType);
            if (!InjectedTypes.Contains(starType)) InjectedTypes.Add(starType);
        }
        catch (Exception e)
        {
            DariusLog.Exception("CONSTELLATION", e, "InjectStarType failed type=" + starType.Name);
        }
    }

    public static void ReassertTypeCache()
    {
        Type[] types =
        {
            typeof(Se_Star_Darius_D_Conqueror), typeof(Se_Star_Darius_D_Triumph), typeof(Se_Star_Darius_D_Alacrity),
            typeof(Se_Star_Darius_D_LastStand), typeof(Se_Star_Darius_D_AxiomArcanist), typeof(Se_Star_Darius_D_NoxianArena),
            typeof(Se_Star_Darius_L_SecondWind), typeof(Se_Star_Darius_L_Overgrowth), typeof(Se_Star_Darius_L_Revitalize),
            typeof(Se_Star_Darius_L_Conditioning), typeof(Se_Star_Darius_L_Unflinching),
            typeof(Se_Star_Darius_I_CripplingStrike), typeof(Se_Star_Darius_I_Apprehend),
            typeof(Se_Star_Darius_I_InstantDecimate), typeof(Se_Star_Darius_I_WarFervor),
            typeof(Se_Star_Darius_I_LegacyCripplingStrike), typeof(Se_Star_Darius_I_LegacyApprehend), typeof(Se_Star_Darius_I_LegacyGuillotine),
            typeof(Se_Star_Darius_D_BloodRush), typeof(Se_Star_Darius_D_NoxianMight), typeof(Se_Star_Darius_D_Dunkmaster),
            typeof(Se_Star_Darius_L_BloodPrice), typeof(Se_Star_Darius_F_HandOfNoxus),
            typeof(Se_Star_Darius_F_NimbusCloak), typeof(Se_Star_Darius_F_Celerity),
            typeof(Se_Star_Darius_F_GatheringStorm), typeof(Se_Star_Darius_F_CosmicInsight),
            typeof(Se_Star_Darius_D_ItemTrinityForce), typeof(Se_Star_Darius_D_ItemBlackCleaver), typeof(Se_Star_Darius_D_ItemSpearOfShojin),
            typeof(Se_Star_Darius_L_ItemSteraksGage), typeof(Se_Star_Darius_L_ItemDeathsDance), typeof(Se_Star_Darius_L_ItemOverlordsBloodmail),
            typeof(Se_Star_Darius_I_ItemSunderedSky), typeof(Se_Star_Darius_I_ItemDeadMansPlate), typeof(Se_Star_Darius_I_ItemYoumuusGhostblade),
            typeof(Se_Star_Darius_F_ItemStridebreaker), typeof(Se_Star_Darius_F_Awoo)
        };
        for (int i = 0; i < types.Length; i++) InjectStarType(types[i]);
    }

    public static void RemoveInjectedTypes()
    {
        try
        {
            FieldInfo field = typeof(Dew).GetField("_allStarTypes", BindingFlags.Static | BindingFlags.NonPublic);
            List<Type> list = field != null ? field.GetValue(null) as List<Type> : null;
            if (list != null)
            {
                int removed = 0;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    Type candidate = list[i];
                    // Remove both current-assembly entries and stale timestamped-assembly entries
                    // owned by this Traveler. Stock/other-Mod StarEffects are never touched.
                    if (InjectedTypes.Contains(candidate) || IsOwnedStarType(candidate))
                    {
                        list.RemoveAt(i);
                        removed++;
                    }
                }
                if (removed > 0) DariusLog.Info("CONSTELLATION-RELOAD", "Unregistered " + removed + " owned/stale star Type cache entries.");
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("CONSTELLATION-RELOAD", e, "RemoveInjectedTypes semantic cleanup failed");
        }
        InjectedTypes.Clear();
        IconKeyByType.Clear();
        _registered = false;
    }
}

public static class DariusConstellationReflection
{
    private static readonly HashSet<string> LoggedMissingLevel = new HashSet<string>();

    public static int ReadRuntimeLevel(StarEffect star, int fallbackMax)
    {
        if (star == null) return 1;
        foreach (string name in new[] { "level", "currentLevel", "starLevel", "_level", "<level>k__BackingField", "<currentLevel>k__BackingField" })
        {
            object value;
            if (TryReadMember(star, name, out value))
            {
                try
                {
                    int level = Convert.ToInt32(value);
                    if (level > 0) return Mathf.Clamp(level, 1, Mathf.Max(1, fallbackMax));
                }
                catch { }
            }
        }
        string typeName = star.GetType().Name;
        if (LoggedMissingLevel.Add(typeName))
            DariusLog.Warn("CONSTELLATION-LEVEL", "Could not discover runtime level member on " + typeName + "; using level 1 fallback. Field scan will be logged next.");
        LogLevelMembers(star);
        return 1;
    }

    public static void ConfigureProgression(StarEffect star, int maxLevel, int requiredLevel, string iconKey)
    {
        if (star == null) return;

        // A blank runtime StarEffect has enough fields to register, but the stock constellation UI
        // also expects native serialized per-level metadata arrays. A minimal runtime star that only
        // supplies maxStarLevel and icon can make UI_Lobby_Constellations_StarItem.Refresh index an empty array after a
        // purchase/drag/save. Hydrate the base StarEffect data contract from a real stock star of the
        // same category before applying Darius-owned progression and visuals.
        HydrateFromNativeStar(star, maxLevel);

        // Character-owned Stars must follow the same progression contract as stock Traveler Stars:
        // they unlock from this Traveler's own Mastery, not account-wide Total Mastery.  Runtime
        // hydration can copy either a global or character template, so always overwrite both fields.
        bool heroTypeSet = false;
        foreach (string name in new[] { "heroType", "_heroType", "<heroType>k__BackingField", "requiredHeroType" })
            heroTypeSet = TrySetMember(star, name, DariusTravelerRegistry.HeroName) || heroTypeSet;
        bool masteryScopeSet = false;
        foreach (string name in new[] { "isRequiredLevelTotalMastery", "_isRequiredLevelTotalMastery", "<isRequiredLevelTotalMastery>k__BackingField" })
            masteryScopeSet = TrySetMember(star, name, false) || masteryScopeSet;

        bool maxSet = TrySetMember(star, "maxStarLevel", maxLevel) || TrySetMember(star, "_maxStarLevel", maxLevel) || TrySetMember(star, "<maxStarLevel>k__BackingField", maxLevel) || TrySetMember(star, "maxLevel", maxLevel) || TrySetMember(star, "_maxLevel", maxLevel);
        bool reqSet = false;
        foreach (string name in new[] { "requiredLevel", "requiredHeroLevel", "unlockLevel", "requiredProfileLevel", "_requiredLevel", "<requiredLevel>k__BackingField" })
            reqSet = TrySetMember(star, name, requiredLevel) || reqSet;

        Sprite icon = DariusPrototypeIcons.Get(iconKey);
        bool iconSet = false;
        int presentationFields = 0;
        if (icon != null)
        {
            foreach (string name in new[] { "icon", "starIcon", "_icon", "<icon>k__BackingField" })
                iconSet = TrySetMember(star, name, icon) || iconSet;
            presentationFields = ApplyIconPresentationFields(star, icon, maxLevel);
            iconSet = iconSet || presentationFields > 0;
        }
        DariusLog.DebugInfo("CONSTELLATION-META", star.name + " maxSet=" + maxSet + " requiredSet=" + reqSet +
            " heroTypeSet=" + heroTypeSet + " perHeroMasterySet=" + masteryScopeSet +
            " iconSet=" + iconSet + " presentationFields=" + presentationFields);
    }



    private static int ApplyIconPresentationFields(StarEffect star, Sprite icon, int maxLevel)
    {
        if (star == null || icon == null) return 0;
        int changed = 0;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type t = star.GetType(); t != null && t != typeof(UnityEngine.Object); t = t.BaseType)
        {
            foreach (FieldInfo field in t.GetFields(flags))
            {
                if (field.IsStatic || field.IsInitOnly) continue;
                try
                {
                    if (field.FieldType == typeof(Sprite))
                    {
                        field.SetValue(star, icon);
                        changed++;
                    }
                    else if (field.FieldType == typeof(Sprite[]))
                    {
                        Sprite[] current = field.GetValue(star) as Sprite[];
                        int len = current != null && current.Length > 0 ? current.Length : Math.Max(1, maxLevel + 1);
                        Sprite[] values = new Sprite[len];
                        for (int i = 0; i < len; i++) values[i] = icon;
                        field.SetValue(star, values);
                        changed++;
                    }
                    else if (typeof(IList<Sprite>).IsAssignableFrom(field.FieldType))
                    {
                        IList<Sprite> list = field.GetValue(star) as IList<Sprite>;
                        if (list != null)
                        {
                            int count = list.Count > 0 ? list.Count : Math.Max(1, maxLevel + 1);
                            list.Clear();
                            for (int i = 0; i < count; i++) list.Add(icon);
                            changed++;
                        }
                    }
                }
                catch { }
            }
        }
        return changed;
    }

    private static void HydrateFromNativeStar(StarEffect target, int maxLevel)
    {
        StarEffect source = FindNativeTemplate(target != null ? target.type : default(StarType), maxLevel);
        if (source == null || target == null)
        {
            DariusLog.Warn("CONSTELLATION-CONTRACT", "No stock StarEffect template found for " + (target != null ? target.name : "<null>"));
            return;
        }

        int copied = 0;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        try
        {
            foreach (FieldInfo field in typeof(StarEffect).GetFields(flags))
            {
                if (field.IsStatic || field.IsInitOnly) continue;
                if (!field.IsPublic && !Attribute.IsDefined(field, typeof(SerializeField))) continue;
                try
                {
                    object value = CloneSerializedValue(field.GetValue(source));
                    field.SetValue(target, value);
                    copied++;
                }
                catch { }
            }

            // Some game builds keep upgrade/cost presentation arrays directly on StarEffect. Make
            // every serialized sequence long enough for both zero-based and one-based level indexing.
            EnsureSerializedSequenceCapacity(target, Mathf.Max(2, maxLevel + 1));
            DariusLog.DebugInfo("CONSTELLATION-CONTRACT", target.name + " hydrated from " + source.GetType().Name +
                " copiedFields=" + copied + " sourceMax=" + source.maxStarLevel + " requestedMax=" + maxLevel);
        }
        catch (Exception e)
        {
            DariusLog.Exception("CONSTELLATION-CONTRACT", e, "Could not hydrate native StarEffect contract for " + target.name);
        }
    }

    private static StarEffect FindNativeTemplate(StarType type, int requestedMax)
    {
        try
        {
            Type nativeType = type == StarType.Destruction ? typeof(Se_Star_D_CritChance) :
                type == StarType.Life ? typeof(Se_Star_L_Armor) :
                type == StarType.Imagination ? typeof(Se_Star_I_AdditionalChaosChance) :
                type == StarType.Flexible ? typeof(Se_Star_Aurena_F_CR_EarnGold) : null;
            return nativeType != null ? DewResources.GetByType<StarEffect>(nativeType) : null;
        }
        catch { return null; }
    }

    private static object CloneSerializedValue(object value)
    {
        if (value == null) return null;
        Array array = value as Array;
        if (array != null) return array.Clone();
        IList list = value as IList;
        if (list != null)
        {
            try
            {
                IList clone = Activator.CreateInstance(value.GetType()) as IList;
                if (clone != null)
                {
                    foreach (object item in list) clone.Add(item);
                    return clone;
                }
            }
            catch { }
        }
        return value;
    }

    private static void EnsureSerializedSequenceCapacity(StarEffect star, int count)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        foreach (FieldInfo field in typeof(StarEffect).GetFields(flags))
        {
            if (field.IsStatic || field.IsInitOnly) continue;
            if (!field.IsPublic && !Attribute.IsDefined(field, typeof(SerializeField))) continue;
            try
            {
                if (field.FieldType.IsArray)
                {
                    Array old = field.GetValue(star) as Array;
                    if (old == null || old.Length == 0 || old.Length >= count) continue;
                    Type element = field.FieldType.GetElementType();
                    Array expanded = Array.CreateInstance(element, count);
                    for (int i = 0; i < count; i++) expanded.SetValue(old.GetValue(Mathf.Min(i, old.Length - 1)), i);
                    field.SetValue(star, expanded);
                }
                else if (typeof(IList).IsAssignableFrom(field.FieldType))
                {
                    IList list = field.GetValue(star) as IList;
                    if (list == null || list.Count == 0 || list.Count >= count) continue;
                    object last = list[list.Count - 1];
                    while (list.Count < count) list.Add(last);
                }
            }
            catch { }
        }
    }

    public static bool SetFloat(object target, float value, params string[] names)
    {
        if (target == null || names == null) return false;
        for (int i = 0; i < names.Length; i++)
            if (TrySetMember(target, names[i], value)) return true;
        return false;
    }

    public static int ReadEntityLevel(object target)
    {
        if (target == null) return 1;
        foreach (string name in new[] { "level", "currentLevel", "_level", "<level>k__BackingField" })
        {
            object value;
            if (TryReadMember(target, name, out value))
            {
                try { return Mathf.Max(1, Convert.ToInt32(value)); } catch { }
            }
        }
        return 1;
    }

    private static bool TryReadMember(object target, string name, out object value)
    {
        value = null;
        if (target == null || string.IsNullOrEmpty(name)) return false;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            try
            {
                FieldInfo f = t.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (f != null) { value = f.GetValue(target); return true; }
                PropertyInfo p = t.GetProperty(name, flags | BindingFlags.DeclaredOnly);
                if (p != null && p.CanRead && p.GetIndexParameters().Length == 0) { value = p.GetValue(target, null); return true; }
            }
            catch { }
        }
        return false;
    }

    private static bool TrySetMember(object target, string name, object value)
    {
        if (target == null || string.IsNullOrEmpty(name)) return false;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            try
            {
                FieldInfo f = t.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (f != null && !f.IsInitOnly)
                {
                    f.SetValue(target, ConvertValue(value, f.FieldType));
                    return true;
                }
                PropertyInfo p = t.GetProperty(name, flags | BindingFlags.DeclaredOnly);
                if (p != null && p.CanWrite && p.GetIndexParameters().Length == 0)
                {
                    p.SetValue(target, ConvertValue(value, p.PropertyType), null);
                    return true;
                }
            }
            catch { }
        }
        return false;
    }

    private static object ConvertValue(object value, Type type)
    {
        if (value == null || type == null) return value;
        if (type.IsInstanceOfType(value)) return value;
        try { return Convert.ChangeType(value, type); } catch { return value; }
    }

    private static void LogLevelMembers(object target)
    {
        if (target == null) return;
        try
        {
            List<string> names = new List<string>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                foreach (FieldInfo f in t.GetFields(flags))
                    if (f.Name.IndexOf("level", StringComparison.OrdinalIgnoreCase) >= 0) names.Add(t.Name + "." + f.Name + ":" + f.FieldType.Name);
                foreach (PropertyInfo p in t.GetProperties(flags))
                    if (p.Name.IndexOf("level", StringComparison.OrdinalIgnoreCase) >= 0) names.Add(t.Name + "." + p.Name + ":" + p.PropertyType.Name);
            }
            DariusLog.DebugInfo("CONSTELLATION-LEVEL", target.GetType().Name + " level members=" + string.Join(",", names.ToArray()));
        }
        catch { }
    }
}
