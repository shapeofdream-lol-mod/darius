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

public static partial class DariusConstellationReflection
{
    private static readonly HashSet<string> LoggedMissingLevel = new HashSet<string>();

    public static int ReadRuntimeLevel(StarEffect star, int fallbackMax)
    {
        if (star == null) return 1;
        foreach (string name in new[] { "effectiveLevel", "skillLevel", "Network_skillLevel", "_skillLevel", "level", "currentLevel", "starLevel", "_level", "<level>k__BackingField", "<currentLevel>k__BackingField" })
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
}