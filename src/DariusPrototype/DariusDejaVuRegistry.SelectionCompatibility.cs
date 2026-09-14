public static partial class DariusDejaVuRegistry
{
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
}