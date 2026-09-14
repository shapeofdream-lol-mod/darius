public static partial class DariusConstellationPersistence
{
    private static void EnsureDiskLoaded()
    {
        if (_diskLoaded) return;
        _diskLoaded = true;
        try
        {
            if (!File.Exists(SavePath)) return;
            _diskState = JsonConvert.DeserializeObject<SavedState>(File.ReadAllText(SavePath));
            if (_diskState != null && _diskState.version != FileVersion) _diskState = null;
        }
        catch (Exception e) { DariusLog.Exception("STAR-PERSIST", e, "Could not load Darius constellation backup"); _diskState = null; }
    }

    private static void SaveToDisk(SavedState state)
    {
        if (state == null) return;
        try
        {
            string path = SavePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(state, Formatting.Indented));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            _diskState = state;
            _diskLoaded = true;
        }
        catch (Exception e) { DariusLog.Exception("STAR-PERSIST", e, "Could not write Darius constellation backup"); }
    }
}