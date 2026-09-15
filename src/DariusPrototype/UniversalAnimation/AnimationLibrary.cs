using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DariusModelAnimationRuntime
{
    internal sealed class RetargetProfile
    {
        internal sealed class BoneDef
        {
            public string semantic;
            public string sourceNode;
            public Vector3 sourceAxis;
            public string[] aliases;
        }
        public readonly Dictionary<string, BoneDef> bones = new Dictionary<string, BoneDef>(StringComparer.Ordinal);

        public static RetargetProfile Load(string path)
        {
            JObject root = JObject.Parse(File.ReadAllText(path));
            JObject defs = (JObject)root["bones"];
            JObject aliases = root["targetAliases"] as JObject;
            RetargetProfile profile = new RetargetProfile();
            foreach (var p in defs.Properties())
            {
                JObject b = (JObject)p.Value;
                JArray axis = (JArray)b["primaryAxisLocal"];
                JArray a = aliases != null ? aliases[p.Name] as JArray : null;
                List<string> list = new List<string>();
                if (a != null) foreach (JToken x in a) list.Add((string)x);
                profile.bones[p.Name] = new BoneDef
                {
                    semantic = p.Name,
                    sourceNode = (string)b["sourceNode"],
                    sourceAxis = axis != null && axis.Count >= 3 ? new Vector3((float)axis[0], (float)axis[1], (float)axis[2]).normalized : Vector3.up,
                    aliases = list.ToArray()
                };
            }
            return profile;
        }
    }

    internal sealed class SodAnimationClip
    {
        internal sealed class BoneTrack
        {
            public float[] times;
            public Quaternion[] rotations;
        }
        public string name;
        public float length;
        public bool loopRecommended;
        public readonly Dictionary<string, BoneTrack> bones = new Dictionary<string, BoneTrack>(StringComparer.Ordinal);

        public static SodAnimationClip Load(string path)
        {
            JObject root = JObject.Parse(File.ReadAllText(path));
            SodAnimationClip clip = new SodAnimationClip
            {
                name = (string)root["name"],
                length = (float)(root["length"] ?? 0f),
                loopRecommended = (bool?)(root["loopRecommended"]) ?? false
            };
            JObject bones = root["bones"] as JObject;
            if (bones != null)
            {
                foreach (var p in bones.Properties())
                {
                    JObject t = (JObject)p.Value;
                    JArray jt = (JArray)t["times"];
                    JArray jr = (JArray)t["rotations"];
                    int count = Math.Min(jt != null ? jt.Count : 0, jr != null ? jr.Count : 0);
                    float[] times = new float[count];
                    Quaternion[] rots = new Quaternion[count];
                    for (int i = 0; i < count; i++)
                    {
                        times[i] = (float)jt[i];
                        JArray q = (JArray)jr[i];
                        rots[i] = new Quaternion((float)q[0], (float)q[1], (float)q[2], (float)q[3]);
                    }
                    clip.bones[p.Name] = new BoneTrack { times = times, rotations = rots };
                }
            }
            return clip;
        }

        public Quaternion Sample(string semantic, float time)
        {
            BoneTrack t;
            if (!bones.TryGetValue(semantic, out t) || t.times == null || t.times.Length == 0) return Quaternion.identity;
            if (t.times.Length == 1 || time <= t.times[0]) return t.rotations[0];
            int last = t.times.Length - 1;
            if (time >= t.times[last]) return t.rotations[last];
            int lo = 0, hi = last;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (t.times[mid] <= time) lo = mid; else hi = mid;
            }
            float span = t.times[hi] - t.times[lo];
            float a = span <= 0.000001f ? 0f : Mathf.Clamp01((time - t.times[lo]) / span);
            return Quaternion.Slerp(t.rotations[lo], t.rotations[hi], a);
        }
    }

    internal static class AnimationLibrary
    {
        private static readonly Dictionary<string, SodAnimationClip> Cache = new Dictionary<string, SodAnimationClip>(StringComparer.Ordinal);
        private static readonly List<string> Names = new List<string>();
        private static readonly Dictionary<string, bool> Loops = new Dictionary<string, bool>(StringComparer.Ordinal);
        private static RetargetProfile _profile;

        public static void Initialize()
        {
            string root = RuntimeLog.Root;
            string manifestPath = Path.Combine(root, "assets", "animations", "manifest.json");
            string profilePath = Path.Combine(root, "assets", "retarget", "darius_humanoid_profile.json");
            _profile = RetargetProfile.Load(profilePath);
            JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
            JArray clips = (JArray)manifest["clips"];
            Names.Clear(); Loops.Clear();
            foreach (JObject c in clips)
            {
                string n = (string)c["name"];
                Names.Add(n);
                Loops[n] = (bool?)(c["loopRecommended"]) ?? false;
            }
            RuntimeLog.Info("ASSET", "Animation library ready. clips=" + Names.Count + " profileBones=" + _profile.bones.Count);
        }

        public static RetargetProfile Profile { get { return _profile; } }
        public static string[] GetNames() { return Names.ToArray(); }
        public static bool IsLoopRecommended(string name) { bool v; return Loops.TryGetValue(name, out v) && v; }
        public static SodAnimationClip Get(string name)
        {
            SodAnimationClip clip;
            if (Cache.TryGetValue(name, out clip)) return clip;
            string safe = Sanitize(name) + ".sodanim.json";
            string path = Path.Combine(RuntimeLog.Root, "assets", "animations", safe);
            if (!File.Exists(path)) return null;
            clip = SodAnimationClip.Load(path);
            Cache[name] = clip;
            return clip;
        }
        private static string Sanitize(string name)
        {
            char[] a = name.ToCharArray();
            for (int i = 0; i < a.Length; i++) if (!(char.IsLetterOrDigit(a[i]) || a[i] == '.' || a[i] == '_' || a[i] == '-')) a[i] = '_';
            return new string(a);
        }
    }
}
