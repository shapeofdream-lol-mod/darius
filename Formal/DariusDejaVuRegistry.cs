using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using DewInternal;

// Injects the Darius test Memories/Essence into the game's native Deja Vu start-loadout system.
// The implementation intentionally mirrors the profile/content registration path used by
// current (2026) Shape of Dreams mods, with reflection fallbacks for gem-side collections.
public static class DariusDejaVuRegistry
{
    private static readonly string[] SkillNames =
    {
        "St_Darius_Decimate",
        "St_Darius_CripplingStrike",
        "St_Darius_Apprehend",
        "St_Darius_NoxianGuillotine",
        "St_D_Darius_Hemorrhage"
    };

    private static readonly Type[] SkillTypes =
    {
        typeof(St_Darius_Decimate),
        typeof(St_Darius_CripplingStrike),
        typeof(St_Darius_Apprehend),
        typeof(St_Darius_NoxianGuillotine),
        typeof(St_D_Darius_Hemorrhage)
    };

    private const string LegacyGemName = "Gem_Darius_Hemorrhage";

    private static readonly HashSet<string> CandidateKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "St_Darius_Decimate",
        "St_Darius_CripplingStrike",
        "St_Darius_Apprehend",
        "St_Darius_NoxianGuillotine",
        "St_D_Darius_Hemorrhage",
        "b67dbf56-5817-424d-8c46-16129def0bf2",
        "0a1641d1-1cbe-4a99-9d95-7b315b51420c",
        "a016b459-fe0f-4571-a28c-d3c49a29d14f",
        "fbb73980-8b6f-4623-88b0-946aaa201e73",
        "acdd533c-6e5d-4974-9895-e90a103b5307"
    };

    private static bool _localizationPatched;
    private static bool _selectionCompatibilityPatched;
    private static bool _loggedDiscovery;

    // The Ctrl skill-drag UI calls ConvertDescriptionNodesToText several times per second, and
    // sometimes multiple times in one frame. Cache both equipped trigger lookup and the final
    // level-aware text so ordinary drag/hover refreshes stay allocation-free.
    private static readonly Dictionary<string, SkillTrigger> TooltipSkillCache = new Dictionary<string, SkillTrigger>(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> TooltipTextCache = new Dictionary<string, string>(StringComparer.Ordinal);
    private static int _tooltipCacheHeroId;

    public static void RegisterAll()
    {
        try
        {
            RegisterContentSettings(DewBuildProfile.current != null ? DewBuildProfile.current.content : null);
            RegisterCollectables();
            RegisterProfile(DewSave.profileMain);
            RegisterProfileStats(DewSave.profileStats);
            LogDejaVuDiscoveryOnce();
            DariusLog.Info("DEJAVU", "Native Deja Vu injection pass complete for Q/W/E/R + Hero_Darius Hemorrhage Identity.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("DEJAVU", e, "RegisterAll failed");
        }
    }

    public static void InstallLocalizationPatches(Harmony harmony)
    {
        if (_localizationPatched || harmony == null) return;
        try
        {
            MethodInfo stringPostfix = AccessTools.Method(typeof(DariusDejaVuRegistry), nameof(LocalizationStringPostfix));
            MethodInfo nodesPostfix = AccessTools.Method(typeof(DariusDejaVuRegistry), nameof(LocalizationNodesPostfix));
            MethodInfo convertedDescriptionPostfix = AccessTools.Method(typeof(DariusDejaVuRegistry), nameof(LocalizationConvertedDescriptionPostfix));
            int patched = 0;

            foreach (MethodInfo method in typeof(DewLocalization).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                if (!method.Name.StartsWith("GetSkill", StringComparison.Ordinal) &&
                    !method.Name.StartsWith("GetGem", StringComparison.Ordinal) &&
                    !method.Name.StartsWith("GetStar", StringComparison.Ordinal) &&
                    !method.Name.StartsWith("GetHero", StringComparison.Ordinal) &&
                    !method.Name.StartsWith("GetSkin", StringComparison.Ordinal))
                    continue;

                ParameterInfo[] ps = method.GetParameters();
                if (ps.Length < 1 || ps[0].ParameterType != typeof(string)) continue;

                if (method.ReturnType == typeof(string))
                {
                    harmony.Patch(method, postfix: new HarmonyMethod(stringPostfix));
                    patched++;
                    DariusLog.DebugInfo("DEJAVU-I18N", "Patched string localization method " + MethodLabel(method));
                }
                else if (method.ReturnType == typeof(List<LocaleNode>))
                {
                    harmony.Patch(method, postfix: new HarmonyMethod(nodesPostfix));
                    patched++;
                    DariusLog.DebugInfo("DEJAVU-I18N", "Patched node localization method " + MethodLabel(method));
                }
            }

            int converterPatched = 0;

            // The stock tooltip converts LocaleNodes using DescriptionSettings after GetSkillDescription.
            // Our Darius descriptions are runtime-generated text nodes, so intercept this final conversion
            // only for Darius SkillTrigger contexts and render current/previous Memory levels explicitly.
            foreach (MethodInfo method in typeof(DewLocalization).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                // Do not guess the DescriptionSettings parameter position/type name.  The current
                // game build changed that signature and the old filter silently patched zero
                // converters.  Harmony's object[] __args is signature-agnostic, so patch every
                // string-returning overload and locate settings from the runtime arguments.
                if (method.Name != "ConvertDescriptionNodesToText" || method.ReturnType != typeof(string)) continue;
                harmony.Patch(method, postfix: new HarmonyMethod(convertedDescriptionPostfix));
                patched++;
                converterPatched++;
                DariusLog.DebugInfo("DEJAVU-I18N", "Patched native description conversion " + MethodLabel(method));
            }

            _localizationPatched = true;
            DariusLog.Info("DEJAVU-I18N", "Localization patch discovery complete. patchedMethods=" + patched + " converters=" + converterPatched);
            if (converterPatched == 0)
                DariusLog.Warn("DEJAVU-I18N", "No ConvertDescriptionNodesToText overload was patched; live native-style numeric rendering will not activate.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("DEJAVU-I18N", e, "Dynamic localization patch install failed; raw internal names may be shown.");
        }
    }


    public static void InstallSelectionCompatibilityPatches(Harmony harmony)
    {
        if (_selectionCompatibilityPatched || harmony == null) return;
        try
        {
            MethodInfo cmdPrefix = AccessTools.Method(typeof(DariusDejaVuRegistry), nameof(CmdSetDejavuItemPrefix));
            MethodInfo cmdPostfix = AccessTools.Method(typeof(DariusDejaVuRegistry), nameof(CmdSetDejavuItemPostfix));
            MethodInfo serverPostfix = AccessTools.Method(typeof(DariusDejaVuRegistry), nameof(ServerSetDejavuItemPostfix));
            MethodInfo changedPostfix = AccessTools.Method(typeof(DariusDejaVuRegistry), nameof(DejavuChangedPostfix));
            MethodInfo spawnPrefix = AccessTools.Method(typeof(DariusDejaVuRegistry), nameof(DoDejavuSpawnPrefix));
            MethodInfo spawnPostfix = AccessTools.Method(typeof(DariusDejaVuRegistry), nameof(DoDejavuSpawnPostfix));
            MethodInfo qualityPrefix = AccessTools.Method(typeof(DariusDejaVuRegistry), nameof(HemorrhageQualityPrefix));
            MethodInfo uiClickPostfix = AccessTools.Method(typeof(DariusDejaVuRegistry), nameof(DejaVuWindowItemClickPostfix));
            int patched = 0;

            // IMPORTANT: v0.9.7 patched Dew.IsDejavuFree/GetDejavuCost/GetDejavuMaxWins globally.
            // Those methods are also queried while the native Deja Vu window builds its candidate list,
            // which can hide custom entries before the user can click them. v0.9.8 intentionally leaves
            // all three vanilla methods untouched. Cost is therefore calculated by the game from the
            // resource rarity (Q/W/E = Common; R/Hemorrhage = Rare), preserving the normal non-zero economy.

            MethodInfo cmd = typeof(DewPlayer).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "CmdSetDejavuItem" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
            if (cmd != null)
            {
                harmony.Patch(cmd, prefix: new HarmonyMethod(cmdPrefix), postfix: new HarmonyMethod(cmdPostfix));
                patched++;
                DariusLog.DebugInfo("DEJAVU-SELECT", "Patched command wrapper " + MethodLabel(cmd));
            }

            MethodInfo serverCode = typeof(DewPlayer).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name.StartsWith("UserCode_CmdSetDejavuItem", StringComparison.Ordinal) &&
                                     m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
            if (serverCode != null)
            {
                harmony.Patch(serverCode, postfix: new HarmonyMethod(serverPostfix));
                patched++;
                DariusLog.DebugInfo("DEJAVU-SELECT", "Patched server command body (postfix repair) " + MethodLabel(serverCode));
            }

            MethodInfo changed = typeof(DewPlayer).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "OnDejavuItemChanged" && m.GetParameters().Length == 2 &&
                                     m.GetParameters()[0].ParameterType == typeof(string) && m.GetParameters()[1].ParameterType == typeof(string));
            if (changed != null)
            {
                harmony.Patch(changed, postfix: new HarmonyMethod(changedPostfix));
                patched++;
                DariusLog.DebugInfo("DEJAVU-SELECT", "Patched selection change hook " + MethodLabel(changed));
            }

            MethodInfo doSpawn = typeof(PlayGameManager).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "DoDejavuSpawn" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(DewPlayer));
            if (doSpawn != null)
            {
                harmony.Patch(doSpawn, prefix: new HarmonyMethod(spawnPrefix), postfix: new HarmonyMethod(spawnPostfix));
                patched++;
                DariusLog.DebugInfo("DEJAVU-SELECT", "Patched Deja Vu spawn lifecycle bridge " + MethodLabel(doSpawn));
            }

            MethodInfo qualityChange = typeof(Gem).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "OnQualityChange" && m.GetParameters().Length == 2 &&
                                     m.GetParameters()[0].ParameterType == typeof(int) && m.GetParameters()[1].ParameterType == typeof(int));
            if (qualityChange != null)
            {
                harmony.Patch(qualityChange, prefix: new HarmonyMethod(qualityPrefix));
                patched++;
                DariusLog.DebugInfo("DEJAVU-SELECT", "Patched Hemorrhage quality guard " + MethodLabel(qualityChange));
            }

            // UI_Lobby_DejavuWindow_Item is defined in Dew.UI.dll. v0.9.6 proved the custom
            // resources can be enumerated and inspected, but the native item click did not always
            // forward custom names to DewPlayer.CmdSetDejavuItem. Patch click-like handlers only;
            // this does NOT touch candidate enumeration or Stardust cost calculation.
            Type dejaVuItemType = AccessTools.TypeByName("UI_Lobby_DejavuWindow_Item");
            if (dejaVuItemType != null)
            {
                foreach (MethodInfo clickMethod in dejaVuItemType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (clickMethod.Name != "OnPointerClick" && clickMethod.Name != "OnClick" && clickMethod.Name != "OnClicked") continue;
                    try
                    {
                        harmony.Patch(clickMethod, postfix: new HarmonyMethod(uiClickPostfix));
                        patched++;
                        DariusLog.DebugInfo("DEJAVU-SELECT", "Patched native Deja Vu item click " + MethodLabel(clickMethod));
                    }
                    catch (Exception patchError)
                    {
                        DariusLog.DebugInfo("DEJAVU-SELECT", "Could not patch click method " + MethodLabel(clickMethod) + ": " + patchError.Message);
                    }
                }
            }
            else
            {
                DariusLog.Warn("DEJAVU-SELECT", "UI_Lobby_DejavuWindow_Item type was not found; relying on stock CmdSetDejavuItem path.");
            }

            _selectionCompatibilityPatched = true;
            DariusLog.Info("DEJAVU-SELECT", "Selection compatibility installed without touching vanilla Deja Vu list/cost/free/max-wins queries. patchedMethods=" + patched);
        }
        catch (Exception e)
        {
            DariusLog.Exception("DEJAVU-SELECT", e, "Selection compatibility patch install failed");
        }
    }

    private static bool IsDariusCandidate(object value)
    {
        if (value == null) return false;
        string s = value as string;
        if (s != null) return CandidateKeys.Contains(s);

        Type t = value as Type;
        if (t != null) return CandidateKeys.Contains(t.Name);

        UnityEngine.Object uo = value as UnityEngine.Object;
        if (uo != null)
        {
            if (CandidateKeys.Contains(uo.name)) return true;
            if (CandidateKeys.Contains(uo.GetType().Name)) return true;
        }

        return CandidateKeys.Contains(value.GetType().Name);
    }

    private static void CmdSetDejavuItemPrefix(DewPlayer __instance, string __0)
    {
        if (!IsDariusCandidate(__0)) return;
        DariusLog.Info("DEJAVU-SELECT", "CmdSetDejavuItem requested item=" + __0 + " before=" + GetSelectedDejavuItem(__instance));
    }

    private static void CmdSetDejavuItemPostfix(DewPlayer __instance, string __0)
    {
        if (!IsDariusCandidate(__0)) return;
        DariusLog.Info("DEJAVU-SELECT", "CmdSetDejavuItem wrapper returned item=" + __0 + " selectedNow=" + GetSelectedDejavuItem(__instance));
    }

    private static void ServerSetDejavuItemPostfix(DewPlayer __instance, string __0)
    {
        if (!IsDariusCandidate(__0)) return;
        try
        {
            string afterStock = GetSelectedDejavuItem(__instance);
            if (string.Equals(afterStock, __0, StringComparison.Ordinal))
            {
                DariusLog.Info("DEJAVU-SELECT", "Stock server command accepted Darius selection item=" + __0 +
                    " selectedNow=" + afterStock);
                return;
            }

            MethodInfo setter = typeof(DewPlayer).GetMethod("set_selectedDejavuItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(string) }, null);
            if (setter == null)
            {
                DariusLog.Warn("DEJAVU-SELECT", "Stock server command did not select Darius item=" + __0 +
                    " and selectedDejavuItem setter was not found. selectedNow=" + afterStock);
                return;
            }

            // Important: this is a postfix. The stock server command has already run, so
            // any normal Deja Vu confirmation/payment work is not skipped. We only repair
            // the final SyncVar assignment when custom-resource validation rejected it.
            setter.Invoke(__instance, new object[] { __0 });
            DariusLog.Info("DEJAVU-SELECT", "Post-stock repair selected Darius item=" + __0 +
                " beforeRepair=" + afterStock + " afterRepair=" + GetSelectedDejavuItem(__instance));
        }
        catch (Exception e)
        {
            DariusLog.Exception("DEJAVU-SELECT", e, "Post-stock Darius selection repair failed.");
        }
    }

    private static void DejavuChangedPostfix(DewPlayer __instance, string __0, string __1)
    {
        if (!IsDariusCandidate(__0) && !IsDariusCandidate(__1)) return;
        DariusLog.Info("DEJAVU-SELECT", "OnDejavuItemChanged old=" + (__0 ?? "<null>") + " new=" + (__1 ?? "<null>") +
            " propertyNow=" + GetSelectedDejavuItem(__instance));
    }

    private static void DoDejavuSpawnPrefix(DewPlayer __0)
    {
        string selected = GetSelectedDejavuItem(__0);
        if (!IsDariusCandidate(selected)) return;
        DariusLog.Info("DEJAVU-SPAWN", "PlayGameManager.DoDejavuSpawn starting with selected=" + selected +
            " player=" + (__0 != null ? __0.name : "<null>"));
    }

    // The native Deja Vu routine runs after the player/hero/reward-drop context is ready. Reuse that
    // lifecycle point for the two constellation starter Memories instead of spawning them during
    // early StarEffect reconciliation, where rc4 logs showed hero/reward positions still at (0,0,0).
    private static void DoDejavuSpawnPostfix(DewPlayer __0)
    {
        if (!NetworkServer.active || __0 == null) return;
        try
        {
            DariusConstellationRuntime.NotifyNativeDejaVuSpawnPhase(__0);
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAR-DEJAVU", e, "Failed to forward native Deja Vu spawn phase to Darius constellation runtime");
        }
    }

    private static bool HemorrhageQualityPrefix(Gem __instance, int __0, int __1)
    {
        if (!(__instance is Gem_Darius_Hemorrhage)) return true;
        DariusLog.DebugInfo("HEM-GEM", "Skipping stock Gem.OnQualityChange for Hemorrhage old=" + __0 + " new=" + __1 +
            " because this Essence has no quality-scaled configuration.");
        return false;
    }

    private static void DejaVuWindowItemClickPostfix(object __instance)
    {
        if (__instance == null) return;
        try
        {
            string candidate = ExtractCandidateKeyFromUiItem(__instance);
            if (!IsDariusCandidate(candidate)) return;

            DewPlayer local = DewPlayer.local;
            if (local == null)
            {
                DariusLog.Warn("DEJAVU-SELECT", "Clicked Darius Deja Vu item=" + candidate + " but DewPlayer.local is null.");
                return;
            }

            string before = GetSelectedDejavuItem(local);
            DariusLog.Info("DEJAVU-SELECT", "Native Deja Vu item clicked candidate=" + candidate + " selectedBefore=" + before);
            if (string.Equals(before, candidate, StringComparison.Ordinal)) return;

            MethodInfo cmd = typeof(DewPlayer).GetMethod("CmdSetDejavuItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(string) }, null);
            if (cmd == null)
            {
                DariusLog.Warn("DEJAVU-SELECT", "CmdSetDejavuItem(string) was not found after Darius UI click.");
                return;
            }

            // Selection itself is free to change in the lobby; Stardust is checked/spent by the
            // stock lobby start flow. We only make the custom item use the same native command.
            cmd.Invoke(local, new object[] { candidate });
            DariusLog.Info("DEJAVU-SELECT", "Forwarded Darius UI click to CmdSetDejavuItem candidate=" + candidate +
                " selectedAfterLocalCall=" + GetSelectedDejavuItem(local));
        }
        catch (Exception e)
        {
            DariusLog.Exception("DEJAVU-SELECT", e, "Darius Deja Vu UI click forwarding failed");
        }
    }

    private static string ExtractCandidateKeyFromUiItem(object item)
    {
        Type type = item.GetType();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // Prefer the field name visible in the current Dew.UI.dll.
        FieldInfo exact = type.GetField("dejavuItem", flags);
        if (exact != null)
        {
            string key = CandidateFromUiValue(exact.GetValue(item));
            if (IsDariusCandidate(key)) return key;
        }

        foreach (FieldInfo field in type.GetFields(flags))
        {
            if (field.Name.IndexOf("dejavu", StringComparison.OrdinalIgnoreCase) < 0 &&
                field.Name.IndexOf("item", StringComparison.OrdinalIgnoreCase) < 0) continue;
            string key = CandidateFromUiValue(field.GetValue(item));
            if (IsDariusCandidate(key)) return key;
        }

        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            if (!property.CanRead) continue;
            if (property.Name.IndexOf("dejavu", StringComparison.OrdinalIgnoreCase) < 0 &&
                property.Name.IndexOf("item", StringComparison.OrdinalIgnoreCase) < 0) continue;
            try
            {
                string key = CandidateFromUiValue(property.GetValue(item, null));
                if (IsDariusCandidate(key)) return key;
            }
            catch { }
        }

        return null;
    }

    private static string CandidateFromUiValue(object value)
    {
        if (value == null) return null;
        string text = value as string;
        if (text != null) return text;
        Type t = value as Type;
        if (t != null) return t.Name;
        UnityEngine.Object uo = value as UnityEngine.Object;
        if (uo != null)
        {
            if (IsDariusCandidate(uo.name)) return uo.name;
            if (IsDariusCandidate(uo.GetType().Name)) return uo.GetType().Name;
        }
        return value.GetType().Name;
    }

    private static string GetSelectedDejavuItem(DewPlayer player)
    {
        if (player == null) return "<null-player>";
        try
        {
            PropertyInfo p = typeof(DewPlayer).GetProperty("selectedDejavuItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.CanRead) return p.GetValue(player, null) as string ?? "<null>";
            MethodInfo getter = typeof(DewPlayer).GetMethod("get_selectedDejavuItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            if (getter != null) return getter.Invoke(player, null) as string ?? "<null>";
        }
        catch (Exception e) { return "<read-error:" + e.GetType().Name + ">"; }
        return "<unavailable>";
    }

    private static string CandidateLabel(object value)
    {
        if (value == null) return "<null>";
        string s = value as string;
        if (s != null) return s;
        UnityEngine.Object uo = value as UnityEngine.Object;
        if (uo != null) return uo.name + "/" + uo.GetType().Name;
        return value.GetType().Name;
    }

    public static void RegisterContentSettings(DewGameContentSettings content)
    {
        if (content == null)
        {
            DariusLog.Warn("DEJAVU", "Content settings unavailable; candidate injection deferred.");
            return;
        }

        foreach (string skillName in SkillNames)
        {
            AddStringToMember(content, "_availableSkills", skillName);
            AddStringToMember(content, "availableSkills", skillName);
        }
        DariusLog.DebugInfoThrottled("DEJAVU", "content-settings", "Content settings injected. skills=" + string.Join(",", SkillNames) + " Hemorrhage=IdentityMemory", 20.0);
    }

    public static void RegisterCollectables()
    {
        // Force initialization of the public skill cache, then append our Types to the backing list.
        try { _ = Dew.allSkills; } catch { }
        AddTypesToDewBackingList("_allSkills", SkillTypes);

        DariusLog.Info("DEJAVU", "Collectables type lists injected for 4 combat Memories + 1 Hero_Darius Identity Memory.");
    }

    public static void RegisterProfile(DewProfile profile)
    {
        if (profile == null)
        {
            DariusLog.Warn("DEJAVU", "DewProfile unavailable; profile injection deferred.");
            return;
        }

        foreach (string skillName in SkillNames)
        {
            try
            {
                if (profile.skills != null && !profile.skills.ContainsKey(skillName))
                    profile.skills.Add(skillName, new DewProfile.UnlockData
                    {
                        status = UnlockStatus.Complete,
                        didReadMemory = true,
                        isNewHeroOrHeroSkill = false
                    });
            }
            catch (Exception e) { DariusLog.Exception("DEJAVU", e, "Adding profile skill " + skillName + " failed"); }

            EnsureDejaVuTimestamp(profile, skillName);
        }

        DariusLog.Info("DEJAVU", "Profile unlock injection complete. skillKeys=" + CountDictionaryMember(profile, "skills") +
            " dejavuCostKeys=" + CountDictionaryMember(profile, "dejavuCostReductionPeriodTimestamp") +
            " Hemorrhage=skill/Identity (not gem)");
    }

    public static void RegisterProfileStats(DewProfileStats stats)
    {
        if (stats == null)
        {
            DariusLog.Warn("DEJAVU", "DewProfileStats unavailable; Deja Vu qualification injection deferred.");
            return;
        }

        foreach (string skillName in SkillNames)
        {
            try
            {
                if (stats.skills != null)
                {
                    DewProfileStats.ItemData data;
                    if (!stats.skills.TryGetValue(skillName, out data))
                    {
                        data = new DewProfileStats.ItemData();
                        object boxed = data;
                        QualifyItemData(boxed);
                        data = (DewProfileStats.ItemData)boxed;
                        stats.skills.Add(skillName, data);
                    }
                    else
                    {
                        object boxed = data;
                        QualifyItemData(boxed);
                        stats.skills[skillName] = (DewProfileStats.ItemData)boxed;
                    }
                }
            }
            catch (Exception e) { DariusLog.Exception("DEJAVU", e, "Adding profile stats skill " + skillName + " failed"); }
        }

        DariusLog.Info("DEJAVU", "ProfileStats registration complete. skills=" + CountDictionaryMember(stats, "skills") +
            " (Hemorrhage registered as Identity skill; no fabricated wins/playCount).");
    }

    private static void EnsureDejaVuTimestamp(DewProfile profile, string key)
    {
        try
        {
            if (profile.dejavuCostReductionPeriodTimestamp != null &&
                !profile.dejavuCostReductionPeriodTimestamp.ContainsKey(key))
                profile.dejavuCostReductionPeriodTimestamp.Add(key, 0L);
        }
        catch (Exception e) { DariusLog.DebugInfo("DEJAVU", "dejavu timestamp " + key + ": " + e.Message); }
    }

    private static object MakeUnlockData(Type valueType)
    {
        object data = Activator.CreateInstance(valueType);
        SetMember(data, "status", UnlockStatus.Complete);
        SetMember(data, "didReadMemory", true);
        SetMember(data, "isNewHeroOrHeroSkill", false);
        return data;
    }

    private static void QualifyItemData(object data)
    {
        // Match the working Elemental Summon reference mod: merely having an ItemData
        // entry is enough for the profile systems. Do not fabricate wins/playCount.
        // Vanilla Deja Vu cost reads ItemData.wins, so leaving the natural value (0 for
        // a newly injected item) preserves a normal non-zero rarity-based Stardust cost.
        if (data == null) return;
    }

    private static void AddTypesToDewBackingList(string fieldName, IEnumerable<Type> types)
    {
        try
        {
            FieldInfo field = typeof(Dew).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            if (field == null)
            {
                DariusLog.Warn("DEJAVU", "Dew backing field not found: " + fieldName);
                return;
            }

            IList list = field.GetValue(null) as IList;
            if (list == null)
            {
                DariusLog.Warn("DEJAVU", "Dew backing list unavailable: " + fieldName);
                return;
            }

            Type[] currentTypes = types != null ? types.Where(t => t != null).ToArray() : Array.Empty<Type>();
            HashSet<string> currentNames = new HashSet<string>(currentTypes.Select(t => t.Name), StringComparer.Ordinal);

            // DewMod can hot-reload an assembly in the same process. Types from the previous
            // Darius assembly are not reference-equal to the new Types, so remove stale entries
            // with the same resource names before adding the current assembly's Types.
            int removed = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Type existing = list[i] as Type;
                if (existing == null || !currentNames.Contains(existing.Name)) continue;
                if (!currentTypes.Contains(existing))
                {
                    list.RemoveAt(i);
                    removed++;
                }
            }

            int added = 0;
            foreach (Type type in currentTypes)
            {
                if (!list.Contains(type)) { list.Add(type); added++; }
            }
            DariusLog.DebugInfo("DEJAVU", fieldName + " count=" + list.Count + " added=" + added + " staleRemoved=" + removed);
        }
        catch (Exception e) { DariusLog.Exception("DEJAVU", e, "Adding types to " + fieldName + " failed"); }
    }

    private static void AddStringToMember(object target, string memberName, string value)
    {
        if (target == null) return;
        Type type = target.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo field = type.GetField(memberName, flags);
        PropertyInfo property = field == null ? type.GetProperty(memberName, flags) : null;
        object current = field != null ? field.GetValue(target) : (property != null && property.CanRead ? property.GetValue(target, null) : null);

        if (current is string[] arr)
        {
            if (arr.Contains(value)) return;
            string[] next = new string[arr.Length + 1];
            Array.Copy(arr, next, arr.Length);
            next[arr.Length] = value;
            if (field != null) field.SetValue(target, next);
            else if (property != null && property.CanWrite) property.SetValue(target, next, null);
            DariusLog.DebugInfo("DEJAVU", "Added " + value + " to " + memberName + "[] count=" + next.Length);
            return;
        }

        IList list = current as IList;
        if (list != null)
        {
            if (!list.Contains(value)) list.Add(value);
            DariusLog.DebugInfo("DEJAVU", "Ensured " + value + " in " + memberName + " count=" + list.Count);
            return;
        }

        if (field != null || property != null)
            DariusLog.DebugInfo("DEJAVU", "Member " + memberName + " exists but is null/unsupported type=" + (current != null ? current.GetType().FullName : "<null>"));
    }

    private static void AddDictionaryEntryByMember(object target, string memberName, string key, Func<Type, object> factory, bool updateExisting = false)
    {
        try
        {
            object mapObj = GetMemberValue(target, memberName);
            IDictionary map = mapObj as IDictionary;
            if (map == null)
            {
                DariusLog.DebugInfo("DEJAVU", "Dictionary member unavailable: " + target.GetType().Name + "." + memberName);
                return;
            }

            Type[] args = mapObj.GetType().GetGenericArguments();
            Type valueType = args.Length >= 2 ? args[1] : typeof(object);
            if (!map.Contains(key))
            {
                map.Add(key, factory(valueType));
                DariusLog.DebugInfo("DEJAVU", "Added dictionary key " + memberName + "[" + key + "] valueType=" + valueType.FullName);
            }
            else if (updateExisting)
            {
                object existing = map[key];
                QualifyItemData(existing);
                map[key] = existing;
                DariusLog.DebugInfo("DEJAVU", "Updated qualification for " + memberName + "[" + key + "]");
            }
        }
        catch (Exception e) { DariusLog.Exception("DEJAVU", e, "Dictionary injection failed member=" + memberName + " key=" + key); }
    }

    private static object GetMemberValue(object target, string memberName)
    {
        if (target == null) return null;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo f = target.GetType().GetField(memberName, flags);
        if (f != null) return f.GetValue(target);
        PropertyInfo p = target.GetType().GetProperty(memberName, flags);
        if (p != null && p.CanRead) return p.GetValue(target, null);
        return null;
    }

    private static int CountDictionaryMember(object target, string memberName)
    {
        IDictionary map = GetMemberValue(target, memberName) as IDictionary;
        return map != null ? map.Count : -1;
    }

    private static void SetMember(object target, string name, object value)
    {
        if (target == null) return;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo f = target.GetType().GetField(name, flags);
        if (f != null)
        {
            if (value == null || f.FieldType.IsInstanceOfType(value)) f.SetValue(target, value);
            else if (f.FieldType.IsEnum) f.SetValue(target, Enum.ToObject(f.FieldType, Convert.ToInt32(value)));
            return;
        }
        PropertyInfo p = target.GetType().GetProperty(name, flags);
        if (p != null && p.CanWrite)
        {
            if (value == null || p.PropertyType.IsInstanceOfType(value)) p.SetValue(target, value, null);
            else if (p.PropertyType.IsEnum) p.SetValue(target, Enum.ToObject(p.PropertyType, Convert.ToInt32(value)), null);
        }
    }

    private static void SetNumericAtLeast(object target, string name, long minimum)
    {
        if (target == null) return;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo f = target.GetType().GetField(name, flags);
        if (f != null)
        {
            long current = Convert.ToInt64(f.GetValue(target));
            if (current < minimum) f.SetValue(target, Convert.ChangeType(minimum, f.FieldType));
            return;
        }
        PropertyInfo p = target.GetType().GetProperty(name, flags);
        if (p != null && p.CanRead && p.CanWrite)
        {
            long current = Convert.ToInt64(p.GetValue(target, null));
            if (current < minimum) p.SetValue(target, Convert.ChangeType(minimum, p.PropertyType), null);
        }
    }

    private static void LogDejaVuDiscoveryOnce()
    {
        if (_loggedDiscovery) return;
        _loggedDiscovery = true;
        try
        {
            Assembly asm = typeof(Dew).Assembly;
            string[] hits = asm.GetTypes()
                .Where(t => t.FullName != null && (t.FullName.IndexOf("Deja", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                   t.FullName.IndexOf("Reverie", StringComparison.OrdinalIgnoreCase) >= 0))
                .Select(t => t.FullName)
                .OrderBy(s => s)
                .Take(80)
                .ToArray();
            DariusLog.Info("DEJAVU-DISCOVERY", "Types containing Deja/Reverie in " + asm.GetName().Name + ": " +
                (hits.Length > 0 ? string.Join(" | ", hits) : "<none>"));

            foreach (string keyword in new[] { "deja", "reverie" })
            {
                string[] methods = asm.GetTypes().SelectMany(t =>
                {
                    try { return t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static); }
                    catch { return new MethodInfo[0]; }
                }).Where(m => m.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                  .Select(MethodLabel).Distinct().Take(80).ToArray();
                DariusLog.Info("DEJAVU-DISCOVERY", "Methods containing '" + keyword + "': " +
                    (methods.Length > 0 ? string.Join(" | ", methods) : "<none>"));
            }
        }
        catch (Exception e) { DariusLog.Exception("DEJAVU-DISCOVERY", e, "Reflection discovery failed"); }
    }

    private static string MethodLabel(MethodBase m)
    {
        try { return m.DeclaringType.FullName + "." + m.Name + "(" + string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name).ToArray()) + ")"; }
        catch { return m != null ? m.Name : "<null>"; }
    }

    private static bool LooksLikeNameRequest(MethodBase method, string key)
    {
        string methodName = method != null ? method.Name : string.Empty;
        if (!string.IsNullOrEmpty(methodName) &&
            (methodName.IndexOf("Name", StringComparison.OrdinalIgnoreCase) >= 0 || methodName.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0))
            return true;
        if (string.IsNullOrEmpty(key)) return false;
        return key.EndsWith(".name", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith("_name", StringComparison.OrdinalIgnoreCase) ||
               key.IndexOf(".title", StringComparison.OrdinalIgnoreCase) >= 0 ||
               key.IndexOf("_title", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool LooksLikeDescriptionRequest(MethodBase method, string key)
    {
        string methodName = method != null ? method.Name : string.Empty;
        if (!string.IsNullOrEmpty(methodName) &&
            (methodName.IndexOf("Story", StringComparison.OrdinalIgnoreCase) >= 0 ||
             methodName.IndexOf("Lore", StringComparison.OrdinalIgnoreCase) >= 0 ||
             methodName.IndexOf("Description", StringComparison.OrdinalIgnoreCase) >= 0 ||
             methodName.IndexOf("Desc", StringComparison.OrdinalIgnoreCase) >= 0 ||
             methodName.IndexOf("Short", StringComparison.OrdinalIgnoreCase) >= 0))
            return true;
        if (string.IsNullOrEmpty(key)) return false;
        return key.EndsWith(".lore", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith(".desc", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith(".description", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith("_lore", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith("_desc", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith("_description", StringComparison.OrdinalIgnoreCase) ||
               key.IndexOf(".short", StringComparison.OrdinalIgnoreCase) >= 0 ||
               key.IndexOf("shortdesc", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void LocalizationStringPostfix(MethodBase __originalMethod, string __0, ref string __result)
    {
        string key = __0;
        if (string.IsNullOrEmpty(key) || __originalMethod == null) return;
        string value;
        string starValue;
        bool wantsName = LooksLikeNameRequest(__originalMethod, key);
        bool wantsDescription = LooksLikeDescriptionRequest(__originalMethod, key);
        if (DariusLanguage.IsJapanese && DariusJapaneseLocalization.TryGet(key, wantsDescription, out value)) { __result = value; return; }

        if (key == DariusTravelerRegistry.HeroName || key == DariusTravelerRegistry.HeroGuid)
        {
            if (wantsName)
                __result = DariusLanguage.IsEnglish ? "The Hand of Noxus" : "诺克萨斯之手";
            else if (wantsDescription)
                __result = DariusLanguage.IsEnglish ? "Darius is one of Noxus's most feared and battle-hardened commanders. He tears through enemy lines with his great axe, turning every wound into the prelude to an execution." : "德莱厄斯是诺克萨斯最令人畏惧、久经沙场的统帅之一。他以巨斧和绝不退让的意志撕开敌阵，让每一道伤口都成为下一次处决的前奏。";
            return;
        }
        if (key == DariusTravelerRegistry.DefaultSkinName || key == DariusTravelerRegistry.SkinGuid || key == DariusTravelerRegistry.LegacySkinAlias)
        {
            if (wantsName)
                __result = DariusLanguage.IsEnglish ? "Classic Darius" : "经典德莱厄斯";
            else if (wantsDescription)
                __result = DariusLanguage.IsEnglish ? "The Hand of Noxus in his classic armor, wielding his great axe." : "诺克萨斯之手的经典战甲与巨斧。";
            return;
        }
        if (key == DariusTravelerRegistry.GodKingSkinName || key == DariusTravelerRegistry.GodKingSkinGuid)
        {
            if (wantsName) __result = DariusLanguage.IsEnglish ? "God-King Darius" : "神王 德莱厄斯";
            else if (wantsDescription) __result = DariusLanguage.IsEnglish ? "God-King Darius. Changes presentation only, not gameplay." : "神王德莱厄斯造型。仅改变模型、动画与表现，不改变角色玩法。";
            return;
        }
        if (key == DariusTravelerRegistry.DunkmasterSkinName || key == DariusTravelerRegistry.DunkmasterSkinGuid)
        {
            if (wantsName) __result = DariusLanguage.IsEnglish ? "Dunkmaster Darius" : "灌篮高手 德莱厄斯";
            else if (wantsDescription) __result = DariusLanguage.IsEnglish ? "Dunkmaster Darius, with its own model, rig, and animation profile." : "灌篮高手德莱厄斯造型。使用该皮肤独立的模型、骨骼与动画档案。";
            return;
        }
        if (key == DariusTravelerRegistry.MechaSkinName || key == DariusTravelerRegistry.MechaSkinGuid)
        {
            if (wantsName) __result = DariusLanguage.IsEnglish ? "Mecha Kingdoms Darius" : "机神 德莱厄斯";
            else if (wantsDescription) __result = DariusLanguage.IsEnglish ? "Mecha Kingdoms Darius, with its own model, rig, and animation profile." : "机神德莱厄斯造型。使用该皮肤独立的模型、骨骼与动画档案。";
            return;
        }
        if (DariusConstellationLocalization.TryName(key, out starValue))
        {
            if (wantsName)
                __result = starValue;
            else if (wantsDescription && DariusConstellationLocalization.TryDescription(key, out starValue))
                __result = starValue;
            return;
        }
        if (DariusFormalLocalization.TrySkillName(key, out value))
        {
            if (wantsName)
                __result = value;
            else if (!string.IsNullOrEmpty(__originalMethod.Name) && __originalMethod.Name.IndexOf("Memory", StringComparison.OrdinalIgnoreCase) >= 0)
                __result = DariusLanguage.IsEnglish ? "A combat Memory from Darius, the Hand of Noxus." : "来自诺克萨斯之手·德莱厄斯的战斗记忆。";
            else if (wantsDescription && DariusFormalLocalization.TrySkillDescription(key, out value))
                __result = value;
        }
        else if (DariusFormalLocalization.IsHemorrhageKey(key))
        {
            if (wantsName)
                __result = DariusLanguage.IsEnglish ? "Hemorrhage" : "出血";
            else if (!string.IsNullOrEmpty(__originalMethod.Name) && __originalMethod.Name.IndexOf("Memory", StringComparison.OrdinalIgnoreCase) >= 0)
                __result = DariusLanguage.IsEnglish ? "Axe wounds keep tearing open until spilled blood awakens the conqueror's strength." : "斧刃留下的伤口会不断撕裂，直到鲜血唤起征服者的力量。";
            else if (wantsDescription)
                __result = DariusFormalLocalization.HemorrhageDescription;
        }
    }

    private static void LocalizationConvertedDescriptionPostfix(object[] __args, ref string __result)
    {
        if (__args == null || __args.Length < 2) return;
        try
        {
            object settings = FindDescriptionSettings(__args);
            if (settings == null) return;
            SkillTrigger skill = ResolveDariusTooltipSkill(settings, __result);
            string key = skill != null ? skill.GetType().Name : DetectDariusMemoryKey(__result);
            if (!DariusFormalLocalization.IsDariusMemoryKey(key)) return;

            int liveLevel = skill != null ? DariusMemoryScaling.GetLevel(skill) : 1;
            int? currentFromSettings = ReadNullableIntMember(settings, "currentLevel");
            int? previousFromSettings = ReadNullableIntMember(settings, "previousLevel");
            bool showScaling = ReadBoolMember(settings, "showLevelScaling", false);

            int currentLevel = DariusMemoryScaling.NormalizeLevel(currentFromSettings ?? liveLevel);
            int? previousLevel = previousFromSettings;
            if (showScaling)
            {
                // Some UI paths supply both levels (old -> target); others only set the current
                // level and request showLevelScaling. In the latter case explicitly render the
                // next enhancement so Darius gets the same useful preview in every Memory UI.
                if (!previousLevel.HasValue)
                {
                    previousLevel = liveLevel;
                    if (currentLevel == liveLevel) currentLevel = liveLevel + 1;
                }
            }
            else
            {
                previousLevel = null;
            }

            // Do not cache the final rendered tooltip.  Native descriptions evaluate their
            // expressions against DescriptionSettings every time they are converted; keeping a
            // finished custom string here made AP/AD changes appear frozen.  Skill lookup remains
            // cached, but the numerical text is rebuilt from the live context entity.
            Hero contextHero = ResolveTooltipHero(settings, skill);
            string rebuilt;
            if (DariusFormalLocalization.TryLevelAwareDescription(key, skill, currentLevel, previousLevel, showScaling, contextHero, out rebuilt))
            {
                __result = rebuilt;
                // Ordinary Ctrl edit/drag UI can rebuild multiple tooltips on open/close. Never log
                // those normal renders; only keep diagnostics for the actual level-up preview path.
                if (showScaling)
                {
                    DariusLog.DebugInfoThrottled("TOOLTIP-LEVEL", key + ":scaling",
                        "Rebuilt upgrade preview " + key + " live=" + liveLevel + " current=" + currentLevel +
                        " previous=" + (previousLevel.HasValue ? previousLevel.Value.ToString() : "<none>"), 8.0);
                }
            }
        }
        catch (Exception e)
        {
            DariusLog.DebugInfo("TOOLTIP-LEVEL", "Level-aware description conversion fallback: " + e.GetType().Name + " " + e.Message);
        }
    }

    private static object FindDescriptionSettings(object[] args)
    {
        if (args == null) return null;
        for (int i = 0; i < args.Length; i++)
        {
            object candidate = args[i];
            if (candidate == null) continue;
            Type type = candidate.GetType();
            if (type.Name.IndexOf("DescriptionSettings", StringComparison.OrdinalIgnoreCase) >= 0)
                return candidate;
        }

        // Compatibility fallback for builds where the nested settings type was renamed.
        // Require at least two characteristic members so ordinary arguments cannot be mistaken
        // for the tooltip settings object.
        for (int i = 0; i < args.Length; i++)
        {
            object candidate = args[i];
            if (candidate == null) continue;
            int matches = 0;
            if (HasMember(candidate, "contextObject")) matches++;
            if (HasMember(candidate, "contextEntity")) matches++;
            if (HasMember(candidate, "currentLevel")) matches++;
            if (HasMember(candidate, "showLevelScaling")) matches++;
            if (matches >= 2) return candidate;
        }
        return null;
    }

    private static bool HasMember(object target, string name)
    {
        if (target == null || string.IsNullOrEmpty(name)) return false;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        try
        {
            Type type = target.GetType();
            return type.GetField(name, flags) != null || type.GetProperty(name, flags) != null;
        }
        catch { return false; }
    }

    private static Hero ResolveTooltipHero(object settings, SkillTrigger skill)
    {
        // DescriptionSettings.contextEntity is the authoritative entity for stock tooltip
        // expressions.  Some UI paths only provide contextObject, so follow the same ownership
        // chain before falling back to the local player hero.
        try
        {
            object contextEntity = ReadMember(settings, "contextEntity");
            Hero hero = contextEntity as Hero;
            if (hero != null) return hero;

            if (contextEntity != null)
            {
                hero = ReadMember(contextEntity, "hero") as Hero;
                if (hero == null) hero = ReadMember(contextEntity, "owner") as Hero;
                if (hero == null) hero = ReadMember(contextEntity, "entity") as Hero;
                if (hero != null) return hero;
            }

            object contextObject = ReadMember(settings, "contextObject");
            hero = contextObject as Hero;
            if (hero != null) return hero;

            SkillTrigger contextSkill = contextObject as SkillTrigger;
            if (contextSkill == null)
            {
                GameObject go = contextObject as GameObject;
                if (go != null) contextSkill = go.GetComponent<SkillTrigger>();
            }
            if (contextSkill == null)
            {
                Component component = contextObject as Component;
                if (component != null) contextSkill = component.GetComponent<SkillTrigger>();
            }

            Entity owner = ReadSkillOwner(contextSkill != null ? contextSkill : skill);
            hero = owner as Hero;
            if (hero != null) return hero;
        }
        catch { }

        try { return DewPlayer.local != null ? DewPlayer.local.hero : null; }
        catch { return null; }
    }

    private static SkillTrigger CacheTooltipSkill(string key, SkillTrigger skill)
    {
        if (!string.IsNullOrEmpty(key) && skill != null) TooltipSkillCache[key] = skill;
        return skill;
    }

    private static string BuildTooltipCacheKey(string key, SkillTrigger skill, int currentLevel, int? previousLevel, bool showScaling)
    {
        Hero hero = null;
        try { hero = DewPlayer.local != null ? DewPlayer.local.hero : null; } catch { }
        int heroId = hero != null ? hero.GetInstanceID() : 0;
        if (_tooltipCacheHeroId != heroId)
        {
            _tooltipCacheHeroId = heroId;
            TooltipSkillCache.Clear();
            TooltipTextCache.Clear();
        }

        int ad10 = 0, hp10 = 0, stateA = 0, stateB = 0;
        try
        {
            if (hero != null && hero.Status != null)
            {
                ad10 = Mathf.RoundToInt(hero.Status.finalStats.attackDamage * 10f);
                hp10 = Mathf.RoundToInt(hero.Status.maxHealth * 10f);
            }
            if (hero != null)
            {
                if (key.Contains("Darius_Decimate"))
                {
                    stateA = DariusConstellationRuntime.GetStarLevel(hero, DariusConstellationIds.IInstantDecimate);
                    stateB = DariusConstellationRuntime.GetStarLevel(hero, DariusConstellationIds.LRevitalize);
                }
                else if (key.Contains("Darius_NoxianGuillotine"))
                {
                    DariusHemorrhageRuntime hem = hero.GetComponent<DariusHemorrhageRuntime>();
                    stateA = hem != null ? hem.identityMemoryLevel : 1;
                }
                else if (DariusFormalLocalization.IsHemorrhageKey(key))
                {
                    stateA = DariusConstellationRuntime.GetWarFervorLevel(hero);
                    DariusHemorrhageRuntime hem = hero.GetComponent<DariusHemorrhageRuntime>();
                    stateB = hem != null ? hem.identityMemoryLevel : currentLevel;
                }
            }
        }
        catch { }

        return key + "|" + currentLevel + "|" + (previousLevel.HasValue ? previousLevel.Value : -1) + "|" +
               (showScaling ? 1 : 0) + "|" + heroId + "|" + ad10 + "|" + hp10 + "|" + stateA + "|" + stateB;
    }

    private static SkillTrigger ResolveDariusTooltipSkill(object settings, string renderedText)
    {
        object context = ReadMember(settings, "contextObject");
        SkillTrigger skill = context as SkillTrigger;
        if (skill == null)
        {
            GameObject go = context as GameObject;
            if (go != null) skill = go.GetComponent<SkillTrigger>();
        }
        if (skill == null)
        {
            Component component = context as Component;
            if (component != null) skill = component.GetComponent<SkillTrigger>();
        }
        if (skill != null && DariusFormalLocalization.IsDariusMemoryKey(skill.GetType().Name))
            return CacheTooltipSkill(skill.GetType().Name, skill);

        string key = DetectDariusMemoryKey(renderedText);
        if (!DariusFormalLocalization.IsDariusMemoryKey(key)) return null;

        Hero cacheHero = null;
        try { cacheHero = DewPlayer.local != null ? DewPlayer.local.hero : null; } catch { }
        int cacheHeroId = cacheHero != null ? cacheHero.GetInstanceID() : 0;
        if (_tooltipCacheHeroId != cacheHeroId)
        {
            _tooltipCacheHeroId = cacheHeroId;
            TooltipSkillCache.Clear();
            TooltipTextCache.Clear();
        }
        SkillTrigger cachedSkill;
        // Registered prefabs are intentionally inactive, so inactivity must not invalidate this
        // cache. The level-aware renderer can still use DescriptionSettings.currentLevel with them.
        if (TooltipSkillCache.TryGetValue(key, out cachedSkill) && cachedSkill != null)
            return cachedSkill;

        // Fallback for UI paths where DescriptionSettings.contextObject is absent. rc7 used
        // FindObjectsOfType<SkillTrigger>() here, which scans the entire scene whenever the Ctrl
        // skill-edit UI opens/closes and caused a visible one-frame hitch. Only inspect the local
        // hero's tiny equipped-ability dictionary; if the Memory is not equipped, use the registered
        // prefab and the level supplied by DescriptionSettings.
        Hero localHero = null;
        try { localHero = DewPlayer.local != null ? DewPlayer.local.hero : null; } catch { }
        if (localHero != null && localHero.Ability != null && localHero.Ability.abilities != null)
        {
            try
            {
                foreach (var pair in localHero.Ability.abilities)
                {
                    SkillTrigger candidate = pair.Value as SkillTrigger;
                    if (candidate == null) continue;
                    if (MemoryKeyMatchesSkill(key, candidate))
                    {
                        TooltipSkillCache[key] = candidate;
                        return candidate;
                    }
                }
            }
            catch { }
        }
        return RegisteredSkillForKey(key);
    }

    private static Entity ReadSkillOwner(SkillTrigger skill)
    {
        if (skill == null) return null;
        object value = ReadMember(skill, "owner");
        return value as Entity;
    }

    private static bool MemoryKeyMatchesSkill(string key, SkillTrigger skill)
    {
        if (skill == null || string.IsNullOrEmpty(key)) return false;
        string typeName = skill.GetType().Name;
        if (key.Contains("Darius_Decimate")) return typeName.Contains("Darius_Decimate");
        if (key.Contains("Darius_CripplingStrike")) return typeName.Contains("Darius_CripplingStrike");
        if (key.Contains("Darius_Apprehend")) return typeName.Contains("Darius_Apprehend");
        if (key.Contains("Darius_NoxianGuillotine")) return typeName.Contains("Darius_NoxianGuillotine");
        if (DariusFormalLocalization.IsHemorrhageKey(key)) return DariusFormalLocalization.IsHemorrhageKey(typeName);
        return false;
    }

    private static SkillTrigger RegisteredSkillForKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (key.Contains("Darius_Decimate")) return CacheTooltipSkill(key, DariusFormalRegistry.Decimate);
        if (key.Contains("Darius_CripplingStrike")) return CacheTooltipSkill(key, DariusFormalRegistry.CripplingStrike);
        if (key.Contains("Darius_Apprehend")) return CacheTooltipSkill(key, DariusFormalRegistry.Apprehend);
        if (key.Contains("Darius_NoxianGuillotine")) return CacheTooltipSkill(key, DariusFormalRegistry.NoxianGuillotine);
        if (DariusFormalLocalization.IsHemorrhageKey(key)) return CacheTooltipSkill(key, DariusFormalRegistry.Hemorrhage);
        return null;
    }

    private static string DetectDariusMemoryKey(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        // Use structural phrases rather than numeric literals: native value markup inserts TMP
        // tags between the words and numbers, so old tests such as "蓄力0.75秒" stop matching.
        if (text.IndexOf("斧刃外圈", StringComparison.Ordinal) >= 0 && text.IndexOf("出血", StringComparison.Ordinal) >= 0)
            return "St_Darius_Decimate";
        if (text.IndexOf("下一次普攻", StringComparison.Ordinal) >= 0 && text.IndexOf("减速", StringComparison.Ordinal) >= 0)
            return "St_Darius_CripplingStrike";
        if (text.IndexOf("拉至身前", StringComparison.Ordinal) >= 0 && text.IndexOf("60°", StringComparison.Ordinal) >= 0)
            return "St_Darius_Apprehend";
        if (text.IndexOf("纯粹伤害", StringComparison.Ordinal) >= 0 && text.IndexOf("诺克萨斯之力", StringComparison.Ordinal) >= 0)
            return "St_Darius_NoxianGuillotine";
        if (text.IndexOf("每秒每层造成", StringComparison.Ordinal) >= 0 && text.IndexOf("出血", StringComparison.Ordinal) >= 0)
            return "St_D_Darius_Hemorrhage";
        return null;
    }

    private static object ReadMember(object target, string name)
    {
        if (target == null || string.IsNullOrEmpty(name)) return null;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        try
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, flags);
                if (field != null) return field.GetValue(target);
                PropertyInfo property = type.GetProperty(name, flags);
                if (property != null && property.CanRead) return property.GetValue(target, null);
            }
        }
        catch { }
        return null;
    }

    private static int? ReadNullableIntMember(object target, string name)
    {
        object value = ReadMember(target, name);
        if (value == null) return null;
        try { return Convert.ToInt32(value); } catch { return null; }
    }

    private static bool ReadBoolMember(object target, string name, bool fallback)
    {
        object value = ReadMember(target, name);
        if (value == null) return fallback;
        try { return Convert.ToBoolean(value); } catch { return fallback; }
    }

    private static void LocalizationNodesPostfix(MethodBase __originalMethod, string __0, ref List<LocaleNode> __result)
    {
        string text;
        if (DariusLanguage.IsJapanese && DariusJapaneseLocalization.TryGet(__0, true, out text))
            __result = DariusFormalLocalization.Nodes(text);
        else if (DariusConstellationLocalization.TryDescription(__0, out text))
            __result = DariusFormalLocalization.Nodes(text);
        else if (DariusFormalLocalization.TrySkillDescription(__0, out text))
            __result = DariusFormalLocalization.Nodes(text);
        else if (DariusFormalLocalization.IsHemorrhageKey(__0))
            __result = DariusFormalLocalization.Nodes(DariusFormalLocalization.HemorrhageDescription);
    }
}

[HarmonyPatch]
public static class DariusDejaVuLifecyclePatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(DewProfile), nameof(DewProfile.Validate))]
    private static void DewProfile_Validate_Prefix(DewProfile __instance, out DariusConstellationPersistence.GuardState __state)
    {
        __state = DariusConstellationPersistence.BeforeValidate(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DewProfile), nameof(DewProfile.Validate))]
    private static void DewProfile_Validate_Postfix(DewProfile __instance, DariusConstellationPersistence.GuardState __state)
    {
        DariusLog.DebugInfo("DEJAVU", "DewProfile.Validate postfix -> re-injecting Darius candidates.");
        DariusDejaVuRegistry.RegisterProfile(__instance);
        DariusDejaVuRegistry.RegisterProfileStats(DewSave.profileStats);
        DariusDejaVuRegistry.RegisterCollectables();
        DariusConstellationPersistence.AfterValidate(__instance, __state);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DewGameContentSettings), nameof(DewGameContentSettings.Init))]
    private static void ContentSettings_Init_Postfix(DewGameContentSettings __instance)
    {
        if (__instance == (DewBuildProfile.current != null ? DewBuildProfile.current.content : null))
        {
            DariusLog.DebugInfoThrottled("DEJAVU", "content-init", "DewGameContentSettings.Init postfix -> re-injecting Darius candidate names.", 20.0);
            DariusDejaVuRegistry.RegisterContentSettings(__instance);
        }
    }
}
