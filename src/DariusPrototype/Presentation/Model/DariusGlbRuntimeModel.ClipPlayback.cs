public sealed partial class DariusGlbRuntimeModel
{
    private void ApplyClip(RuntimeClip clip, float time, bool upperBodyOnly, bool locomotionActionMask = false,
        bool lowerBodyOnly = false)
    {
        if (clip == null) return;
        for (int i = 0; i < clip.tracks.Count; i++)
        {
            RuntimeTrack t = clip.tracks[i];
            if (t.node < 0 || t.node >= _nodes.Length || _nodes[t.node] == null || t.times.Length == 0) continue;
            if (lowerBodyOnly && !IsLowerBodyLocomotionNode(t.node)) continue;
            if (upperBodyOnly)
            {
                if (locomotionActionMask)
                {
                    if (!IsLocomotionActionNode(t.node)) continue;
                }
                else if (!IsUpperBodyNode(t.node)) continue;
            }
            int k0, k1;
            float lerp;
            FindKeys(t.times, time, out k0, out k1, out lerp);
            int a = k0 * t.components;
            int b = k1 * t.components;

            if (string.Equals(t.path, "translation", StringComparison.Ordinal))
            {
                // The Darius combat clips animate Root and the separate Weapon node as a matched
                // authored pair. Suppressing Root translation while still applying Weapon is what
                // made the axe fly away from the body. Keep locomotion world movement owned by SoD,
                // but preserve the action clip's internal Root translation so the skeleton and axe
                // stay in the same source coordinate frame.
                string nodeName = _nodes[t.node] != null ? _nodes[t.node].name : null;
                bool isRootNode = string.Equals(nodeName, "Root", StringComparison.OrdinalIgnoreCase);
                if (isRootNode && ((upperBodyOnly && !locomotionActionMask) || !IsDariusCombatActionClip(clip.name))) continue;
                Vector3 v0 = new Vector3(t.values[a], t.values[a + 1], t.values[a + 2]);
                Vector3 v1 = new Vector3(t.values[b], t.values[b + 1], t.values[b + 2]);
                _nodes[t.node].localPosition = Vector3.Lerp(v0, v1, lerp);
            }
            else if (string.Equals(t.path, "scale", StringComparison.Ordinal))
            {
                // LoL exports contain scale tracks for smears, weapons and form-swap helpers.
                // Core humanoid bones must never inherit those tracks in the runtime GLB player:
                // a single bad Root/Pelvis/Spine scale is enough to fold the whole skinned model
                // into the compact "ball" seen in gameplay. Keep body scale at the bind pose.
                if (IsCoreBodyScaleNode(t.node))
                {
                    _nodes[t.node].localScale = _baseScale[t.node];
                    continue;
                }

                Vector3 v0 = new Vector3(t.values[a], t.values[a + 1], t.values[a + 2]);
                Vector3 v1 = new Vector3(t.values[b], t.values[b + 1], t.values[b + 2]);
                Vector3 scale = Vector3.Lerp(v0, v1, lerp);
                if (!IsFinite(scale) || Mathf.Abs(scale.x) > 50f || Mathf.Abs(scale.y) > 50f || Mathf.Abs(scale.z) > 50f)
                    scale = _baseScale[t.node];
                _nodes[t.node].localScale = scale;
            }
            else if (string.Equals(t.path, "rotation", StringComparison.Ordinal))
            {
                Quaternion q0 = new Quaternion(t.values[a], t.values[a + 1], t.values[a + 2], t.values[a + 3]);
                Quaternion q1 = new Quaternion(t.values[b], t.values[b + 1], t.values[b + 2], t.values[b + 3]);
                Quaternion sampled = Quaternion.Slerp(q0, q1, lerp);
                if (lowerBodyOnly && IsPrimaryLegRoot(t.node) && Mathf.Abs(_lowerAppliedYaw) > 0.1f)
                    sampled = Quaternion.AngleAxis(_lowerAppliedYaw * 0.62f, Vector3.up) * sampled;
                _nodes[t.node].localRotation = sampled;
            }
        }
    }

    private bool IsLowerBodyLocomotionNode(int node)
    {
        if (node < 0 || node >= _nodes.Length || _nodes[node] == null) return false;
        string n = (_nodes[node].name ?? string.Empty).ToLowerInvariant();
        if (n.Contains("weapon") || n.Contains("buffbone") || n.Contains("ground_loc") ||
            n.Contains("glb_foot_loc") || n.Contains("snap_") || n.Contains("doll")) return false;
        return n.Contains("hip") || n.Contains("knee") || n.Contains("leg") || n.Contains("thigh") ||
               n.Contains("calf") || n.Contains("foot") || n.Contains("toe");
    }

    private bool IsPrimaryLegRoot(int node)
    {
        if (node < 0 || node >= _nodes.Length || _nodes[node] == null) return false;
        string n = (_nodes[node].name ?? string.Empty).ToLowerInvariant();
        return n == "l_hip" || n == "r_hip" || n == "l_thigh" || n == "r_thigh";
    }

    private bool IsCoreBodyScaleNode(int node)
    {
        if (node < 0 || node >= _nodes.Length || _nodes[node] == null) return false;
        string n = (_nodes[node].name ?? string.Empty).ToLowerInvariant();
        // God-King Spell4 drives the restored beast with Lion_* scale tracks. Those names include
        // head/neck/spine and were accidentally caught by the humanoid anti-collapse guard, which
        // folded the authentic wolf/lion submesh into the red-black clump seen in the test video.
        if (n.StartsWith("lion_") || n.StartsWith("wolf_")) return false;
        if (n == "root" || n == "c_root" || n == "skeleton_root" || n == "doll_root" || n.Contains("pelvis") || n.Contains("spine") ||
            n.Contains("chest") || n.Contains("neck") || n.Contains("head") || n.Contains("clav") ||
            n.Contains("shoulder") || n.Contains("upperarm") || n.Contains("forearm") || n.Contains("hand") ||
            n.Contains("thigh") || n.Contains("knee") || n.Contains("calf") || n.Contains("ankle") ||
            n.Contains("foot") || n.Contains("toe")) return true;
        // Some Riot skeletons use L_Arm/R_Arm and L_Leg/R_Leg rather than upperarm/thigh names.
        if ((n.StartsWith("l_") || n.StartsWith("r_")) && (n.Contains("arm") || n.Contains("leg"))) return true;
        return false;
    }

    private static bool IsFinite(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
               !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }

    private static bool IsDariusCombatActionClip(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.StartsWith("Attack", StringComparison.Ordinal) ||
               name.StartsWith("Crit", StringComparison.Ordinal) ||
               name.StartsWith("Spell", StringComparison.Ordinal) ||
               name.StartsWith("Darius_", StringComparison.Ordinal) ||
               string.Equals(name, "Death", StringComparison.Ordinal) ||
               string.Equals(name, "Channel_Wndup", StringComparison.Ordinal) ||
               string.Equals(name, "Channel", StringComparison.Ordinal);
    }

    private bool IsLocomotionActionNode(int node)
    {
        if (node < 0 || node >= _nodes.Length || _nodes[node] == null) return false;
        string n = _nodes[node].name ?? string.Empty;
        // Keep SoD locomotion on the actual leg chains. Everything else (Root/Pelvis, torso,
        // arms, weapon, cape and skin-specific auxiliary bones) follows the authored action.
        string lower = n.ToLowerInvariant();
        if (lower.Contains("hip") || lower.Contains("knee") || lower.Contains("foot") || lower.Contains("toe")) return false;
        if (lower.Contains("leg")) return false;
        if (lower.Contains("ground_loc")) return false;
        return true;
    }

    private bool IsUpperBodyNode(int node)
    {
        if (node < 0 || node >= _nodes.Length || _nodes[node] == null) return false;
        string n = (_nodes[node].name ?? string.Empty).ToLowerInvariant();
        if (n.Contains("hip") || n.Contains("knee") || n.Contains("foot") || n.Contains("toe") || n.Contains("leg")) return false;
        if (n.Contains("ground_loc")) return false;
        return true;
    }

    private static void FindKeys(float[] times, float time, out int k0, out int k1, out float t)
    {
        if (times.Length <= 1 || time <= times[0]) { k0 = k1 = 0; t = 0f; return; }
        int last = times.Length - 1;
        if (time >= times[last]) { k0 = k1 = last; t = 0f; return; }
        int lo = 0, hi = last;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (times[mid] <= time) lo = mid; else hi = mid;
        }
        k0 = lo; k1 = hi;
        float span = times[k1] - times[k0];
        t = span <= 0.000001f ? 0f : Mathf.Clamp01((time - times[k0]) / span);
    }
}