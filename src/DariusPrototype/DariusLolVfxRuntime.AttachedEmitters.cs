public static partial class DariusLolVfxRuntime
{
    private static float BuildAttachedMeshEmitter(Hero owner, Transform root, JObject e, string name, bool persistent, float authoredTimeOffset, string systemName)
    {
        JObject pd = e["primitiveData"] as JObject;
        JObject ad = pd != null ? pd["attachedMesh"] as JObject : null;
        JArray hashes = ad != null ? ad["submeshHashes"] as JArray : null;
        int blend = ReadIntToken(e["blendMode"], 0);
        Material baseMaterial = MaterialFor((string)e["texture"], blend);
        if (baseMaterial == null) return -1f;
        Material overlayMaterial = new Material(baseMaterial);
        overlayMaterial.name = baseMaterial.name + "_Attached_" + name;

        bool skin67Attached = !string.IsNullOrEmpty(systemName) && systemName.StartsWith("Darius_Skin67_", StringComparison.OrdinalIgnoreCase);
        Color birthTint;
        Gradient lifetimeGradient;
        if (skin67Attached)
        {
            birthTint = ReadInitialColor(e["birthColor"] as JObject, Color.white);
            lifetimeGradient = BuildGradient(e["color"] as JObject, Color.white);
            ApplyMaterialColor(overlayMaterial, MultiplyColor(birthTint, lifetimeGradient.Evaluate(0f)));
        }
        else
        {
            // Preserve the already visually-approved pre-0.30.4 AttachedMesh behavior on
            // Classic/God-King/Dunkmaster. The richer birthColor contract is enabled only for
            // Skin67 layers that were previously missing altogether.
            Color legacyTint = ReadInitialColor(e["color"] as JObject, Color.white);
            ApplyMaterialColor(overlayMaterial, legacyTint);
            birthTint = Color.white;
            lifetimeGradient = BuildGradient(e["color"] as JObject, legacyTint);
        }

        // Skin67 AttachedMesh layers (passive flash, Q heal screen lines) also animate UVs.
        // They were previously absent altogether; when restored without these authored UV
        // transforms they can look like a static texture pasted over the avatar. Keep this
        // behavior scoped to Mecha so the already-approved other skins remain unchanged.
        if (skin67Attached)
        {
            Vector2 attachedOffset = ReadConstantVector2(e["birthUVOffset"] as JObject, Vector2.zero);
            Vector2 attachedScroll = ReadConstantVector2(e["birthUvScrollRate"] as JObject, Vector2.zero) +
                ReadConstantVector2(e["emitterUvScrollRate"] as JObject, Vector2.zero);
            Vector2 attachedScale = Vector2.one;
            JArray attachedDiv = e["texDiv"] as JArray;
            if (attachedDiv != null && attachedDiv.Count >= 2)
            {
                float dx = Mathf.Abs((float)attachedDiv[0]);
                float dy = Mathf.Abs((float)attachedDiv[1]);
                if (dx > 0.0001f && dy > 0.0001f &&
                    (dx < 0.999f || dy < 0.999f || Mathf.Abs(dx - Mathf.Round(dx)) > 0.001f || Mathf.Abs(dy - Mathf.Round(dy)) > 0.001f))
                    attachedScale = new Vector2(1f / dx, 1f / dy);
            }
            overlayMaterial.mainTextureScale = attachedScale;
            overlayMaterial.mainTextureOffset = attachedOffset;
            if (overlayMaterial.HasProperty("_BaseMap"))
            {
                overlayMaterial.SetTextureScale("_BaseMap", attachedScale);
                overlayMaterial.SetTextureOffset("_BaseMap", attachedOffset);
            }
            if (attachedScroll.sqrMagnitude > 0.0000001f)
            {
                DariusLolVfxMaterialUvDriver uv = root.gameObject.AddComponent<DariusLolVfxMaterialUvDriver>();
                uv.material = overlayMaterial; uv.initialOffset = attachedOffset; uv.scrollRate = attachedScroll;
            }
            if (attachedOffset.sqrMagnitude > 0.0000001f || attachedScroll.sqrMagnitude > 0.0000001f || (attachedScale - Vector2.one).sqrMagnitude > 0.0000001f)
                DariusLog.DebugInfo("LOL-VFX-UV", "Applied Skin67 AttachedMesh UV semantics system=" + systemName +
                    " emitter=" + name + " scale=(" + attachedScale.x.ToString("0.###") + "," + attachedScale.y.ToString("0.###") +
                    ") offset=(" + attachedOffset.x.ToString("0.###") + "," + attachedOffset.y.ToString("0.###") +
                    ") scroll=(" + attachedScroll.x.ToString("0.###") + "," + attachedScroll.y.ToString("0.###") + ")");
        }

        bool any = false;
        int appliedSubmeshes = 0;
        DariusLolVfxLinkedObjects links = root.GetComponent<DariusLolVfxLinkedObjects>();
        if (owner != null && hashes != null && hashes.Count > 0)
        {
            for (int hi = 0; hi < hashes.Count; hi++)
            {
                uint hash;
                if (!uint.TryParse((string)hashes[hi], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out hash)) continue;
                GameObject overlay = DariusSkinAnimationHooks.CreateSubmeshOverlay(owner, hash, overlayMaterial);
                if (overlay != null)
                {
                    any = true; appliedSubmeshes++;
                    if (links != null) links.Add(overlay);
                }
            }
        }
        else if (skin67Attached)
        {
            // Skin67 contains several hashless AttachedMesh nodes whose real LoL binding is
            // shader/submesh driven (AvatarFlash, ScreenspaceLines, Temp_Avatar, pauldrons).
            // Treating an empty hash list as "paint every visible character submesh" produced
            // the full-body green grid reported in-game.  Missing one decorative overlay is
            // preferable to corrupting the entire model, so only explicit submesh hashes are
            // rendered until their exact Riot binding contract is known.
            DariusLog.Warn("LOL-VFX-FIDELITY", "Skipping hashless Skin67 AttachedMesh instead of applying a full-avatar overlay emitter=" + name +
                " system=" + systemName);
        }

        if (!any)
        {
            DariusLog.Warn("LOL-VFX-UNSUPPORTED", "AttachedMesh could not resolve visual target emitter=" + name +
                " hashes=" + (hashes != null ? hashes.Count : 0));
            UnityEngine.Object.Destroy(overlayMaterial);
            return -1f;
        }
        if (links != null) links.Add(overlayMaterial);
        float attachedLife = EstimateEmitterLife(e, persistent, authoredTimeOffset);
        float authoredParticleLife = ReadConstantFloat(e["particleLifetime"] as JObject, -1f);
        bool driveFiniteAttachedColor = skin67Attached
            ? authoredParticleLife > 0f && (!persistent || single)
            : (!persistent && authoredParticleLife > 0f);
        if (driveFiniteAttachedColor)
        {
            DariusLolVfxMaterialColorDriver driver = root.gameObject.AddComponent<DariusLolVfxMaterialColorDriver>();
            driver.material = overlayMaterial;
            driver.gradient = lifetimeGradient;
            driver.multiplier = birthTint;
            driver.duration = Mathf.Max(0.05f, authoredParticleLife);
        }
        DariusLog.Info("LOL-VFX-ATTACHED", "Riot AttachedMesh converted emitter=" + name +
            " hashCount=" + (hashes != null ? hashes.Count : 0) + " overlays=" + appliedSubmeshes +
            " fullAvatar=" + (hashes == null || hashes.Count == 0) +
            " finiteColorDriver=" + driveFiniteAttachedColor);
        return attachedLife;
    }

    private static float BuildTrailEmitter(Transform root, JObject e, string primitive, string name, bool persistent, float authoredTimeOffset, string systemName)
    {
        // Long-lived attachment systems are animation/event driven in League. A permanently
        // emitting Unity trail accumulates idle weapon motion into a large ribbon cloud, so
        // persistent trails use attachment-motion gating below instead of being omitted.
        if (root.parent == null)
        {
            DariusLog.Warn("LOL-VFX-FIDELITY", "Skipping world-static Riot trail until target/endpoint binding is available primitive=" + primitive + " emitter=" + name);
            return -1f;
        }
        // Long-lived attachment trails are graph/event driven in League. Keep them alive with
        // the owning effect root, but gate emission by animated attachment motion so an idle
        // weapon cannot accumulate a multi-second ribbon cloud. This restores the authored W
        // trail instead of dropping it while retaining the anti-blob safeguard.
        return BuildAttachedTrail(root, e, primitive, name, persistent, authoredTimeOffset, systemName);
    }
}