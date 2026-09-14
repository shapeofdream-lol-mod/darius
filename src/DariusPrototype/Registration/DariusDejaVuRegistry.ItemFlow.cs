public static partial class DariusDejaVuRegistry
{
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
}