using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class DariusLolVfxRuntime
{
    private static GameObject PlayInternal(Hero owner, string systemName, Vector3 position, Quaternion rotation, Transform parent, bool attached, bool persistent, float authoredTimeOffset = 0f)
    {
        EnsureLoaded();
        if (_systems == null || string.IsNullOrEmpty(systemName)) return null;
        JObject system = _systems[systemName] as JObject;
        if (system == null)
        {
            foreach (JProperty p in _systems.Properties())
            {
                if (string.Equals(p.Name, systemName, StringComparison.OrdinalIgnoreCase)) { system = p.Value as JObject; systemName = p.Name; break; }
            }
        }
        if (system == null)
        {
            DariusLog.Warn("LOL-VFX", "Converted system not found: " + systemName);
            return null;
        }

        if (systemName.IndexOf("_Q_Ring", StringComparison.OrdinalIgnoreCase) >= 0)
            DariusLog.DebugInfo("LOL-VFX-Q-VISIBILITY", "Applying Q ring readability compositor to system=" + systemName + " (no duplicate lifetime-color multiplication; chroma lift enabled; authored alpha preserved).");

        GameObject root = new GameObject("LOL_VFX_" + systemName);
        root.AddComponent<DariusLolVfxLinkedObjects>();
        if (parent != null)
        {
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = rotation;
            // GLB bones already inherit the LoL->SoD model scale. Hero transforms do not.
            float lossy = Mathf.Abs(parent.lossyScale.x);
            root.transform.localScale = lossy > 0.00001f && lossy < 0.10f ? Vector3.one : Vector3.one * _coordinateScale;
        }
        else
        {
            root.transform.position = position;
            root.transform.rotation = rotation;
            root.transform.localScale = Vector3.one * _coordinateScale;
        }

        // Mecha Q exposes the small difference between Riot's authored visual radii and the
        // gameplay 4.25 m hit radius.  With coordinateScale=0.00921 the windup AOE quad lands at
        // ~4.283 m while the dominant impact team-ring mesh lands at ~4.080 m.  Normalize the two
        // complete systems to the same 4.25 m boundary instead of changing gameplay collision.
        if (string.Equals(systemName, "Darius_Skin67_Q_RingWindup", StringComparison.OrdinalIgnoreCase))
            root.transform.localScale *= 0.992376f;
        else if (string.Equals(systemName, "Darius_Skin67_Q_Ring", StringComparison.OrdinalIgnoreCase))
            root.transform.localScale *= 1.041622f;

        JArray emitters = system["emitters"] as JArray;
        int built = 0, skipped = 0, disabled = 0;
        float maxLife = 0.25f;
        if (emitters != null)
        {
            for (int i = 0; i < emitters.Count; i++)
            {
                JObject emitter = emitters[i] as JObject;
                if (emitter == null) continue;
                try
                {
                    float life = BuildEmitter(owner, root.transform, emitter, i, persistent, authoredTimeOffset, systemName);
                    if (float.IsNaN(life)) disabled++;
                    else if (life >= 0f) { built++; maxLife = Mathf.Max(maxLife, life); }
                    else skipped++;
                }
                catch (Exception e)
                {
                    skipped++;
                    DariusLog.Exception("LOL-VFX-EMITTER", e, "system=" + systemName + " emitter=" + ((string)emitter["name"] ?? i.ToString()));
                }
            }
        }
        if (!persistent)
        {
            float destroyAfter = Mathf.Clamp(maxLife + 1.0f, 0.5f, 20f);
            // Skin67 E uses several world-space slash/trail layers. Their authored curves can linger
            // for seconds, but after the 0.28 s hook beat that leaves a stale slash behind Darius
            // pointing along the old cast direction.  Keep the cast readable, then clean the old
            // world-space orientation promptly.
            if (string.Equals(systemName, "Darius_Skin67_E_Cast", StringComparison.OrdinalIgnoreCase)) destroyAfter = Mathf.Min(destroyAfter, 0.58f);
            else if (string.Equals(systemName, "Darius_Skin67_E_ClawMarks", StringComparison.OrdinalIgnoreCase)) destroyAfter = Mathf.Min(destroyAfter, 0.34f);
            else if (string.Equals(systemName, "Darius_Skin67_E_AxegrabCollision", StringComparison.OrdinalIgnoreCase)) destroyAfter = Mathf.Min(destroyAfter, 0.30f);
            else if (string.Equals(systemName, "Darius_Skin67_E_Tar02", StringComparison.OrdinalIgnoreCase)) destroyAfter = Mathf.Min(destroyAfter, 0.48f);
            UnityEngine.Object.Destroy(root, destroyAfter);
        }
        DariusLog.Info("LOL-VFX", "Play system=" + systemName + " authoredEmitters=" + (emitters != null ? emitters.Count : 0) +
            " built=" + built + " disabled=" + disabled + " unsupported=" + skipped + " attached=" + attached + " persistent=" + persistent +
            " authoredOffset=" + authoredTimeOffset.ToString("0.###") + " maxLife=" + maxLife.ToString("0.###"));
        return root;
    }
}