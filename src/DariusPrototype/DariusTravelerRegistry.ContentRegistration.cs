public static partial class DariusTravelerRegistry
{
    private static int CopySameNamedMember(object source, object target, string name)
    {
        if (source == null || target == null) return 0;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo sf = FindFieldRecursive(source.GetType(), name) ?? FindFieldRecursive(source.GetType(), "<" + name + ">k__BackingField");
        FieldInfo tf = FindFieldRecursive(target.GetType(), name) ?? FindFieldRecursive(target.GetType(), "<" + name + ">k__BackingField");
        if (sf != null && tf != null && !tf.IsInitOnly && tf.FieldType.IsAssignableFrom(sf.FieldType))
        {
            try { tf.SetValue(target, sf.GetValue(source)); return 1; } catch { }
        }
        PropertyInfo sp = source.GetType().GetProperty(name, flags);
        PropertyInfo tp = target.GetType().GetProperty(name, flags);
        if (sp != null && sp.CanRead && tp != null && tp.CanWrite && tp.PropertyType.IsAssignableFrom(sp.PropertyType))
        {
            try { tp.SetValue(target, sp.GetValue(source, null), null); return 1; } catch { }
        }
        return 0;
    }

    private static void ClearEntityAbilityRuntimeAttackState(EntityAbility ability)
    {
        if (ability == null) return;
        string[] fields = { "_originalAttackAbility", "_overridenAttackAbility", "_attackAbilityOverrides" };
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo f = FindFieldRecursive(ability.GetType(), fields[i]);
            if (f == null) continue;
            try
            {
                object current = f.GetValue(ability);
                IList list = current as IList;
                if (list != null)
                {
                    list.Clear();
                    continue;
                }
                f.SetValue(ability, f.FieldType.IsValueType ? Activator.CreateInstance(f.FieldType) : null);
            }
            catch { }
        }
    }

    private static void RegisterTypes()
    {
        // Force lazy caches to exist first, then append only our exact types.
        try { var _ = Dew.allHeroes; } catch { }
        try { var _ = Dew.allSkills; } catch { }
        AddUniqueTypeToDew("_allHeroes", typeof(Hero_Darius));
        AddUniqueTypeToDew("_allSkills", typeof(St_Darius_Decimate));
        AddUniqueTypeToDew("_allSkills", typeof(St_Darius_NoxianGuillotine));
        AddUniqueTypeToDew("_allSkills", typeof(St_D_Darius_Hemorrhage));
        AddUniqueTypeToDew("_allSkills", typeof(St_Darius_CripplingStrike));
        AddUniqueTypeToDew("_allSkills", typeof(St_Darius_Apprehend));
        AddUniqueTypeToDew("_allSkills", typeof(St_Darius_Flash));
        AddUniqueTypeToDew("_allSkills", typeof(St_Darius_Ghost));
        AddUniqueTypeToDew("_allHeroSkills", typeof(St_Darius_Decimate));
        AddUniqueTypeToDew("_allHeroSkills", typeof(St_Darius_NoxianGuillotine));
        AddUniqueTypeToDew("_allHeroSkills", typeof(St_D_Darius_Hemorrhage));
        AddUniqueTypeToDew("_allHeroSkills", typeof(St_Darius_Flash));
        AddUniqueTypeToDew("_allHeroSkills", typeof(St_Darius_Ghost));
        DariusConstellationRegistry.ReassertTypeCache();
        DariusLog.DebugInfoThrottled("TRAVELER-TYPE", "type-caches", "Dew type caches appended: Hero_Darius + Q/R/Identity + Flash/Ghost movement + ordinary W/E Memories + Darius constellation StarEffects.", 20.0);
    }

    public static void RegisterContent(DewGameContentSettings content)
    {
        if (content == null) return;
        AddStringMember(content, "_availableHeroes", HeroName);
        AddStringMember(content, "availableHeroes", HeroName);
        for (int i = 0; i < SkinSpecs.Length; i++)
        {
            AddStringMember(content, "_availableSkins", SkinSpecs[i].name);
            AddStringMember(content, "availableSkins", SkinSpecs[i].name);
        }

        string[] skillNames =
        {
            DariusFormalRegistry.Decimate != null ? DariusFormalRegistry.Decimate.name : "St_Darius_Decimate",
            DariusFormalRegistry.NoxianGuillotine != null ? DariusFormalRegistry.NoxianGuillotine.name : "St_Darius_NoxianGuillotine",
            DariusFormalRegistry.Hemorrhage != null ? DariusFormalRegistry.Hemorrhage.name : "St_D_Darius_Hemorrhage",
            DariusFormalRegistry.CripplingStrike != null ? DariusFormalRegistry.CripplingStrike.name : "St_Darius_CripplingStrike",
            DariusFormalRegistry.Apprehend != null ? DariusFormalRegistry.Apprehend.name : "St_Darius_Apprehend",
            DariusFormalRegistry.Flash != null ? DariusFormalRegistry.Flash.name : "St_Darius_Flash",
            DariusFormalRegistry.Ghost != null ? DariusFormalRegistry.Ghost.name : "St_Darius_Ghost"
        };
        foreach (string skill in skillNames)
        {
            AddStringMember(content, "_availableSkills", skill);
            AddStringMember(content, "availableSkills", skill);
        }
        foreach (Type starType in Dew.allStarTypes)
        {
            if (starType == null || !starType.Name.StartsWith("Se_Star_Darius_", StringComparison.Ordinal)) continue;
            AddStringMember(content, "_availableStars", starType.Name);
            AddStringMember(content, "availableStars", starType.Name);
        }
        DariusLog.DebugInfoThrottled("TRAVELER-CONTENT", "content", "Content includes Hero_Darius, " + SkinSpecs.Length + " explicit skins, and seven Darius skill resources including Flash/Ghost movement choices.", 20.0);
    }
}