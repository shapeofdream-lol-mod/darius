public static partial class DariusTravelerRegistry
{
    private static void RepairHeroCosmeticContract(Hero_Darius hero)
    {
        if (hero == null || DefaultSkin == null) return;
        int rebound = 0;
        Sprite portrait = DariusPrototypeIcons.Get("HERO");
        object skinRef = null;
        try { skinRef = new AssetRef<Skin>(DefaultSkin); } catch { }

        // The generic Hero template contains presentation members that can still point at its stock
        // default cosmetic after the concrete Hero component is replaced. Rebind only skin/portrait
        // members; gameplay, constellation and class data remain inherited from the native Hero graph.
        foreach (string name in new[] { "defaultSkinName", "skinName", "selectedSkinName", "defaultSkinType" })
        {
            if (TryAssignCount(hero, name, DefaultSkinName)) rebound++;
        }
        foreach (string name in new[] { "defaultSkin", "skin", "skinPreset", "defaultSkinPreset", "skinRef", "defaultSkinRef" })
        {
            if (TryAssignCount(hero, name, DefaultSkin)) rebound++;
            if (skinRef != null && TryAssignCount(hero, name, skinRef)) rebound++;
        }
        if (portrait != null && TryAssignCount(hero, "icon", portrait)) rebound++;
        if (TryAssignCount(hero, "mainColor", new Color(0.36f, 0.075f, 0.055f, 1f))) rebound++;
        // Skin thumbnails are not the hero portrait. Rebind each Skin.previewImage to its own
        // packaged League selection portrait so UI_SkinList can read the native field directly.
        for (int si = 0; si < SkinSpecs.Length; si++)
        {
            Skin skin;
            if (!SkinsByName.TryGetValue(SkinSpecs[si].name, out skin) || skin == null) continue;
            Sprite preview = DariusPrototypeIcons.Get(SkinSpecs[si].previewIconKey);
            if (preview != null && TryAssignCount(skin, "previewImage", preview)) rebound++;
        }

        // Scrub inherited serialized skin members by type as well as by common names. In particular,
        // stock Heroes may keep their selectable/default cosmetics in an array, which otherwise leaves
        // the wardrobe presenting the template Hero even though Hero_Darius has its own Skin resource.
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type t = hero.GetType(); t != null && t != typeof(UnityEngine.Object); t = t.BaseType)
        {
            foreach (FieldInfo field in t.GetFields(flags))
            {
                if (field.IsStatic || field.IsInitOnly || field.Name.IndexOf("skin", StringComparison.OrdinalIgnoreCase) < 0) continue;
                try
                {
                    Type ft = field.FieldType;
                    if (ft == typeof(string))
                    {
                        string value = field.GetValue(hero) as string;
                        if (!string.IsNullOrEmpty(value) && value.IndexOf("Vesper", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            field.SetValue(hero, DefaultSkinName);
                            rebound++;
                        }
                    }
                    else if (typeof(Skin).IsAssignableFrom(ft))
                    {
                        field.SetValue(hero, DefaultSkin);
                        rebound++;
                    }
                    else if (skinRef != null && ft.IsInstanceOfType(skinRef))
                    {
                        field.SetValue(hero, skinRef);
                        rebound++;
                    }
                    else if (ft.IsArray)
                    {
                        Type element = ft.GetElementType();
                        if (element == typeof(string))
                        {
                            Array array = Array.CreateInstance(element, 1);
                            array.SetValue(DefaultSkinName, 0);
                            field.SetValue(hero, array);
                            rebound++;
                        }
                        else if (typeof(Skin).IsAssignableFrom(element))
                        {
                            Array array = Array.CreateInstance(element, 1);
                            array.SetValue(DefaultSkin, 0);
                            field.SetValue(hero, array);
                            rebound++;
                        }
                        else if (skinRef != null && element.IsInstanceOfType(skinRef))
                        {
                            Array array = Array.CreateInstance(element, 1);
                            array.SetValue(skinRef, 0);
                            field.SetValue(hero, array);
                            rebound++;
                        }
                    }
                }
                catch { }
            }
        }
        DariusLog.Info("TRAVELER-SKIN", "Hero_Darius cosmetic references rebound to " + DefaultSkinName + " members=" + rebound);
    }

    private static bool TryAssignCount(object obj, string name, object value)
    {
        if (obj == null) return false;
        Type t = obj.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        while (t != null)
        {
            FieldInfo f = t.GetField(name, flags | BindingFlags.DeclaredOnly) ?? t.GetField("<" + name + ">k__BackingField", flags | BindingFlags.DeclaredOnly);
            if (f != null && !f.IsInitOnly && IsCompatible(f.FieldType, value))
            {
                try { f.SetValue(obj, value); return true; } catch { return false; }
            }
            t = t.BaseType;
        }
        try
        {
            PropertyInfo p = obj.GetType().GetProperty(name, flags);
            if (p != null && p.CanWrite && IsCompatible(p.PropertyType, value))
            {
                p.SetValue(obj, value, null);
                return true;
            }
        }
        catch { }
        return false;
    }

    private static void RepairHeroBaseStatContract(Hero source, Hero_Darius target)
    {
        if (source == null || target == null) return;
        int copied = 0;
        int cloned = 0;
        int tuned = 0;
        copied += CopySameNamedMember(source, target, "baseStats");
        EntityStatus sourceStatus = source.GetComponent<EntityStatus>();
        EntityStatus targetStatus = target.GetComponent<EntityStatus>();
        if (sourceStatus != null && targetStatus != null)
            copied += CopySameNamedMember(sourceStatus, targetStatus, "baseStats");

        object heroStats = CloneMemberValue(source, "baseStats");
        if (heroStats != null)
        {
            tuned += ApplyDariusBaseStatTuning(heroStats);
            if (TryAssignCount(target, "baseStats", heroStats)) cloned++;
        }

        if (sourceStatus != null && targetStatus != null)
        {
            object statusStats = CloneMemberValue(sourceStatus, "baseStats") ?? CloneMemberValue(source, "baseStats");
            if (statusStats != null)
            {
                tuned += ApplyDariusBaseStatTuning(statusStats);
                if (TryAssignCount(targetStatus, "baseStats", statusStats)) cloned++;
            }
        }

        DariusLog.Info("STAT-CONTRACT", "Hero_Darius base-stat contract normalized from native Hero template copiedMembers=" + copied +
            " clonedMembers=" + cloned + " tunedFields=" + tuned +
            " profile=juggernaut(+health,+attack,+armor,-speed)");
    }
}