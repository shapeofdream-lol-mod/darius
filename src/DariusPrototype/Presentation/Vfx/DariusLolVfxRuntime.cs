// Runtime interpreter for Riot's VfxSystemDefinitionData converted from the user's current
// Darius WAD. This deliberately maps authored LoL emitter parameters instead of redrawing the
// effect from screenshots. Unsupported Riot primitives are logged explicitly rather than being
// replaced by hand-authored imitation VFX.
public static partial class DariusLolVfxRuntime
{
    private const float DefaultCoordinateScale = 0.00921f;

    private static bool _loadAttempted;

    private static JObject _manifest;

    private static JObject _systems;

    private static float _coordinateScale = DefaultCoordinateScale;

    private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

    private static Mesh _quad;

    public static bool Ready { get { EnsureLoaded(); return _systems != null; } }

    private static void EnsureLoaded()
    {
        if (_loadAttempted) return;
        _loadAttempted = true;
        try
        {
            string path = Path.Combine(DariusMedia.Root, "assets", "lol_vfx", "darius_lol_vfx.json");
            if (!File.Exists(path))
            {
                DariusLog.Error("LOL-VFX", "Converted League VFX manifest missing path=" + path);
                return;
            }
            _manifest = JObject.Parse(File.ReadAllText(path));
            _systems = _manifest["systems"] as JObject;
            if (_manifest["coordinateScale"] != null) _coordinateScale = (float)_manifest["coordinateScale"];
            int systemCount = _systems != null ? _systems.Count : 0;
            JObject assets = _manifest["assets"] as JObject;
            int textures = 0, meshes = 0, missing = 0;
            if (assets != null)
            {
                foreach (JProperty p in assets.Properties())
                {
                    JObject a = p.Value as JObject;
                    if (a == null) continue;
                    string kind = (string)a["kind"];
                    bool ok = a["file"] != null && a["file"].Type != JTokenType.Null;
                    if (!ok) { missing++; continue; }
                    if (string.Equals(kind, "texture", StringComparison.OrdinalIgnoreCase)) textures++;
                    else if (string.Equals(kind, "mesh", StringComparison.OrdinalIgnoreCase)) meshes++;
                }
            }
            DariusLog.Info("LOL-VFX", "Converted Riot VFX manifest loaded systems=" + systemCount +
                " textures=" + textures + " meshes=" + meshes + " missingAssets=" + missing +
                " coordinateScale=" + _coordinateScale.ToString("0.#####"));
        }
        catch (Exception e)
        {
            _systems = null;
            DariusLog.Exception("LOL-VFX", e, "Failed loading converted Riot VFX manifest");
        }
    }

    public static GameObject PlayAttached(Hero owner, string systemName, Transform parent)
    {
        Vector3 p = parent != null ? parent.position : (owner != null ? owner.transform.position : Vector3.zero);
        return PlayInternal(owner, systemName, p, Quaternion.identity, parent, true, false);
    }

    // Starts an authored Riot system as though `authoredTimeOffset` seconds of its timeline have
    // already elapsed. This is used by Instant Decimate: the normal Q ring has 0.75-0.85 s delays
    // baked into Riot emitters, but the no-windup constellation must enter the spin phase at once.
    public static GameObject PlayAttachedAtAuthoredTime(Hero owner, string systemName, Transform parent, float authoredTimeOffset)
    {
        Vector3 p = parent != null ? parent.position : (owner != null ? owner.transform.position : Vector3.zero);
        return PlayInternal(owner, systemName, p, Quaternion.identity, parent, true, false, Mathf.Max(0f, authoredTimeOffset));
    }

    public static GameObject PlayAttachedPersistent(Hero owner, string systemName, Transform parent)
    {
        Vector3 p = parent != null ? parent.position : (owner != null ? owner.transform.position : Vector3.zero);
        return PlayInternal(owner, systemName, p, Quaternion.identity, parent, true, true);
    }

    public static GameObject PlayWorld(string systemName, Vector3 position)
    {
        return PlayInternal(null, systemName, position, Quaternion.identity, null, false, false);
    }

    public static GameObject PlayWorld(string systemName, Vector3 position, Quaternion rotation)
    {
        return PlayInternal(null, systemName, position, rotation, null, false, false);
    }

    public static GameObject PlayWorldBetween(Hero owner, string systemName, Vector3 from, Vector3 to, float duration, Quaternion rotation)
    {
        GameObject mover = new GameObject("LOL_VFX_ENDPOINT_" + (string.IsNullOrEmpty(systemName) ? "Unknown" : systemName));
        mover.transform.position = from;
        mover.transform.rotation = Quaternion.identity;
        DariusLolVfxEndpointMover motion = mover.AddComponent<DariusLolVfxEndpointMover>();
        motion.from = from; motion.to = to; motion.duration = Mathf.Max(0.01f, duration); motion.destroyDelay = Mathf.Max(2.5f, duration + 2.0f);
        GameObject root = PlayInternal(owner, systemName, from, rotation, mover.transform, true, false);
        if (root == null) UnityEngine.Object.Destroy(mover);
        else DariusLog.DebugInfo("LOL-VFX-ENDPOINT", "Bound world Riot system to moving endpoint system=" + systemName +
            " from=" + DariusLog.Vec(from) + " to=" + DariusLog.Vec(to) + " duration=" + duration.ToString("0.###"));
        return root;
    }

    public static GameObject Play(string systemName, Vector3 position, Transform parent, bool attached)
    {
        return PlayInternal(null, systemName, position, Quaternion.identity, parent, attached, false);
    }
}