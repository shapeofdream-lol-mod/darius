using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class DariusTravelerRegistry
{
    private static TriggerConfig CloneTriggerConfig(TriggerConfig source)
    {
        TriggerConfig clone = new TriggerConfig();
        if (source == null) return clone;
        Type t = typeof(TriggerConfig);
        while (t != null && t != typeof(object))
        {
            FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                if (f.IsStatic || f.IsInitOnly) continue;
                try { f.SetValue(clone, f.GetValue(source)); } catch { }
            }
            t = t.BaseType;
        }
        return clone;
    }

    private static CastMethodData CloneCastMethodData(CastMethodData source)
    {
        if (source == null) return null;
        CastMethodData clone = new CastMethodData();
        Type t = source.GetType();
        while (t != null && t != typeof(object))
        {
            FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                if (f.IsStatic || f.IsInitOnly) continue;
                try { f.SetValue(clone, f.GetValue(source)); } catch { }
            }
            t = t.BaseType;
        }
        return clone;
    }

    private static T ResolveResourceByTypeName<T>(string typeName) where T : UnityEngine.Object
    {
        Type resourceType = AccessTools.TypeByName(typeName);
        if (resourceType == null) return null;
        MethodInfo[] methods = typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo m = methods[i];
            if (m.Name != "GetByType" || m.IsGenericMethod) continue;
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length < 1 || ps[0].ParameterType != typeof(Type)) continue;
            object[] args = new object[ps.Length];
            args[0] = resourceType;
            for (int j = 1; j < ps.Length; j++)
                args[j] = ps[j].HasDefaultValue ? ps[j].DefaultValue : (ps[j].ParameterType.IsValueType ? Activator.CreateInstance(ps[j].ParameterType) : null);
            try
            {
                UnityEngine.Object result = m.Invoke(null, args) as UnityEngine.Object;
                T direct = result as T;
                if (direct != null) return direct;
                GameObject resultGo = result as GameObject;
                if (resultGo != null)
                {
                    T component = resultGo.GetComponent(resourceType) as T;
                    if (component != null) return component;
                }
                Component c = result as Component;
                if (c != null)
                {
                    T component = c.GetComponent(resourceType) as T;
                    if (component != null) return component;
                }
            }
            catch { }
        }
        return null;
    }

    private static void CreateAndRegisterSkin()
    {
        Skin source = ResolveGenericResource<Skin>("GetByName", "Skin_Vesper_Default", false);
        if (source == null) throw new InvalidOperationException("Could not resolve stock Skin template contract.");
        EntityModel sourceModel = source.GetComponent<EntityModel>();
        if (sourceModel == null) throw new InvalidOperationException("Stock Skin template has no EntityModel contract.");

        for (int i = 0; i < SkinSpecs.Length; i++)
        {
            DariusSkinSpec spec = SkinSpecs[i];
            Skin existing;
            if (SkinsByName.TryGetValue(spec.name, out existing) && existing != null) continue;
            Skin created = CreateAndRegisterSkinResource(sourceModel, spec);
            SkinsByName[spec.name] = created;
        }
        Skin defaultSkin; SkinsByName.TryGetValue(DefaultSkinName, out defaultSkin); DefaultSkin = defaultSkin;
        Skin godKingSkin; SkinsByName.TryGetValue(GodKingSkinName, out godKingSkin); GodKingSkin = godKingSkin;
        Skin dunkmasterSkin; SkinsByName.TryGetValue(DunkmasterSkinName, out dunkmasterSkin); DunkmasterSkin = dunkmasterSkin;
        Skin mechaSkin; SkinsByName.TryGetValue(MechaSkinName, out mechaSkin); MechaSkin = mechaSkin;
        DariusLog.Info("TRAVELER-SKIN", "Darius skins ready count=" + SkinsByName.Count + ". Shared native profiles are authoritative.");
    }

    private static Skin CreateAndRegisterSkinResource(EntityModel sourceModel, DariusSkinSpec spec)
    {
        DariusNativeSkinProfile profile = spec.native;
        if (profile == null) throw new InvalidOperationException("Darius skin has no native profile: " + spec.name);
        GameObject go = DariusNativeModelAssets.InstantiateFreshEntityModelTemplate(
            profile.Variant,
            _resourceRoot.transform);
        if (go == null)
            throw new InvalidOperationException("Failed creating skin model template: " + spec.name);
        go.name = spec.name;
        go.transform.SetParent(_resourceRoot.transform, false);
        go.SetActive(true);
        go.hideFlags = HideFlags.None;
        DariusOfficialEntityModelMarker marker = go.AddComponent<DariusOfficialEntityModelMarker>();
        marker.variantKey = profile.Variant;

        Skin skin = go.AddComponent<Skin>();
        skin.name = spec.name;
        skin.category = HeroName;
        skin.rarity = SkinRarity.Default;
        skin.requiredLevel = 0;
        skin.generatedFromServer = false;
        ClearArrayMember(skin, "skillVisuals");
        Sprite skinPreview = DariusPrototypeIcons.Get(spec.previewIconKey);
        if (skinPreview != null) TryAssignIfCompatible(skin, "previewImage", skinPreview);
        bool previewAssigned = skinPreview != null && object.ReferenceEquals(ReadMemberValue(skin, "previewImage"), skinPreview);
        if (!previewAssigned) DariusLog.Error("SKIN-ICON", "Failed assigning native Skin.previewImage skin=" + spec.name + " key=" + spec.previewIconKey);
        else DariusLog.Info("SKIN-ICON", "Assigned native Skin.previewImage skin=" + spec.name + " key=" + spec.previewIconKey + " sprite=" + skinPreview.name);

        DariusSkinModelBinding binding = go.AddComponent<DariusSkinModelBinding>();
        binding.skinResourceName = spec.name; binding.modelFile = profile.GlbFile; binding.displayName = spec.displayName;
        binding.variantKey = profile.Variant; binding.isGodKingSkin = profile.GodKing;
        binding.modelScale = profile.Scale; binding.modelYOffset = profile.YOffset; binding.modelYaw = profile.Yaw;
        binding.expectedPrimitives = profile.ExpectedPrimitives; binding.expectedBones = profile.ExpectedBones;
        binding.expectedAnimations = profile.ExpectedAnimations;
        binding.idleClip = profile.Idle; binding.idleVariantClip = profile.IdleVariant; binding.runClip = profile.Run; binding.deathClip = profile.Death;
        binding.attack1Clip = profile.Attack1; binding.attack2Clip = profile.Attack2; binding.critClip = profile.Crit;
        binding.qIntroClip = profile.QIntro; binding.qClip = profile.Q; binding.wClip = profile.W; binding.eClip = profile.E; binding.rClip = profile.R;
        binding.attack1ToIdleClip = profile.Attack1ToIdle; binding.attack2ToIdleClip = profile.Attack2ToIdle; binding.critToIdleClip = profile.CritToIdle;
        binding.eToRunClip = profile.EToRun; binding.eToIdleClip = profile.EToIdle; binding.rToRunClip = profile.RToRun;

        EntityModel model = go.AddComponent<EntityModel>();
        RestoreUnitySerializedFields(model, CaptureUnitySerializedFields(sourceModel, typeof(EntityModel)));
        model.name = spec.name; model.bodyRenderers = new Renderer[0]; model.fxLoop = null; model.fxDeath = null; model.fxTakeDamage = null;
        model.customMappings = new List<EntityModelCustomMapping>();
        ConfigureOfficialFreshEntityModel(go, model, sourceModel, profile);
        OwnedObjects.Add(go);
        RegisterNamedResource(skin, go, spec.name, spec.guid);
        DariusLog.Info("TRAVELER-SKIN", "Created runtime skin=" + spec.name + " guid=" + spec.guid +
            " model=" + profile.GlbFile + " profile=" + profile.Variant + " display=" + spec.displayName);
        return skin;
    }

    private static void ConfigureOfficialFreshEntityModel(
        GameObject root,
        EntityModel model,
        EntityModel sourceModel,
        DariusNativeSkinProfile profile)
    {
        Animator animator = root != null ? root.GetComponentInChildren<Animator>(true) : null;
        if (animator == null || animator.runtimeAnimatorController == null)
            throw new InvalidOperationException("Official EntityModel template has no native Animator/controller.");

        Dictionary<string, AnimationClip> clips =
            new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
        AnimationClip[] controllerClips = animator.runtimeAnimatorController.animationClips;
        DariusOfficialActionRuntime actionRuntime = root.GetComponent<DariusOfficialActionRuntime>();
        if (actionRuntime == null) actionRuntime = root.AddComponent<DariusOfficialActionRuntime>();
        actionRuntime.ConfigureTemplate(animator, controllerClips);

        for (int i = 0; i < controllerClips.Length; i++)
        {
            AnimationClip clip = controllerClips[i];
            if (clip != null && !clips.ContainsKey(clip.name)) clips.Add(clip.name, clip);
        }

        AnimationClip idle = RequireOfficialClip(clips, profile.Idle, "idle");
        AnimationClip run = RequireOfficialClip(clips, profile.Run, "run");
        AnimationClip death = RequireOfficialClip(clips, profile.Death, "death");

        Animator sourceAnimator = sourceModel != null
            ? sourceModel.GetComponentInChildren<Animator>(true)
            : null;
        RuntimeAnimatorController stockController =
            sourceAnimator != null ? sourceAnimator.runtimeAnimatorController : null;
        if (stockController == null)
            throw new InvalidOperationException("Stock EntityModel template has no Animator/controller.");
        RuntimeAnimatorController nativeController = animator.runtimeAnimatorController;
        animator.runtimeAnimatorController = stockController;

        AnimationClipWithSpeed idleWithSpeed = new AnimationClipWithSpeed { clip = idle, speed = 1f };
        model.idle = idleWithSpeed;
        model.lobby = idleWithSpeed;
        model.stagger = idleWithSpeed;
        model.death = new AnimationClipWithSpeed { clip = death, speed = 1f };

        model.runForwardClip = run;
        model.runBackwardClip = run;
        model.runLeftClip = run;
        model.runRightClip = run;
        model.runForwardLeftClip = run;
        model.runForwardRightClip = run;
        model.runBackwardLeftClip = run;
        model.runBackwardRightClip = run;

        // Keep the official fresh EntityModel/EntityAnimation lifecycle, but use the same
        // texture-dominant URP/Unlit material path already proven by the other Darius skins.
        // Dew/Dew Entity keeps a lit response even with its common PBR controls zeroed, which is
        // the remaining Classic-only "plastic" reflection seen in runtime.
        DariusRuntimePerformance.OptimizeSkinnedRenderers(root, true);
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        List<Renderer> visibleRenderers = new List<Renderer>(renderers != null ? renderers.Length : 0);
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;

                // God-King contains authored wolf/throne presentation meshes. Keep them in the
                // prefab hierarchy for future action integration, but never hand them to the stock
                // EntityVisual renderer collection or it may re-enable them globally.
                if (DariusNativeAssetContract.IsHiddenObjectName(renderer.gameObject.name))
                {
                    // EntityVisual may rebuild/enable its internal renderer collection after model
                    // load. Renderer.enabled=false alone is therefore not a stable hidden-prop
                    // contract for God-King. Keep the authored wolf/throne object itself inactive
                    // until a future action-specific presentation path explicitly opts it in.
                    renderer.enabled = false;
                    renderer.gameObject.SetActive(false);
                    continue;
                }

                SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
                if (skinned != null) skinned.updateWhenOffscreen = false;
                visibleRenderers.Add(renderer);
            }
        }
        model.bodyRenderers = visibleRenderers.ToArray();

        Transform health = FindOfficialAnchor(root.transform, DariusNativeAssetContract.HealthAnchorNames);
        Transform weapon = FindOfficialAnchor(root.transform, DariusNativeAssetContract.WeaponAnchorNames);
        if (health == null || weapon == null)
            throw new InvalidOperationException("Official EntityModel template is missing required health/weapon anchors.");
        model.healthBarPosition = health;
        model.weapon = weapon;
        model.holsteredWeapon = weapon;

        DariusLog.Info("OFFICIAL-ENTITYMODEL",
            "Prepared fresh " + profile.Variant + " EntityModel template initialized=" + model.isInitialized +
            " locomotion=" + model.locomotion +
            " walkSpeed=" + model.walkAnimationSpeed.ToString("0.###") +
            " renderers=" + model.bodyRenderers.Length +
            " support4=" + model.support4Directions + " support8=" + model.support8Directions +
            " animator=" + animator.gameObject.name +
            " nativeController=" + (nativeController != null ? nativeController.name : "<null>") +
            " stockController=" + stockController.name +
            " sourceAnimator=" + sourceAnimator.gameObject.name);
        if (model.isInitialized)
            throw new InvalidOperationException("Official EntityModel template was initialized before EntityVisual.LoadModelLocal.");
    }

    private static void ApplyOfficialStockMaterialContract(Renderer[] targetRenderers, EntityModel sourceModel)
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
            throw new InvalidOperationException("Official EntityModel template has no target renderers.");

        Renderer stockRenderer = null;
        if (sourceModel != null && sourceModel.bodyRenderers != null)
        {
            for (int i = 0; i < sourceModel.bodyRenderers.Length; i++)
            {
                Renderer candidate = sourceModel.bodyRenderers[i];
                if (candidate != null && candidate.sharedMaterial != null)
                {
                    stockRenderer = candidate;
                    break;
                }
            }
        }
        if (stockRenderer == null && sourceModel != null)
        {
            Renderer[] sourceRenderers = sourceModel.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < sourceRenderers.Length; i++)
            {
                Renderer candidate = sourceRenderers[i];
                if (candidate != null && candidate.sharedMaterial != null)
                {
                    stockRenderer = candidate;
                    break;
                }
            }
        }
        if (stockRenderer == null || stockRenderer.sharedMaterial == null)
            throw new InvalidOperationException("Stock EntityModel template has no material contract.");

        Material stockTemplate = stockRenderer.sharedMaterial;
        Shader stockShader = stockTemplate.shader;
        if (stockShader == null)
            throw new InvalidOperationException("Stock EntityModel material has no shader contract.");

        for (int ri = 0; ri < targetRenderers.Length; ri++)
        {
            Renderer renderer = targetRenderers[ri];
            if (renderer == null) continue;
            Material[] nativeMaterials = renderer.sharedMaterials;
            if (nativeMaterials == null || nativeMaterials.Length == 0) continue;

            Material[] replacements = new Material[nativeMaterials.Length];
            for (int mi = 0; mi < nativeMaterials.Length; mi++)
            {
                Material native = nativeMaterials[mi];
                Texture texture = native != null ? native.mainTexture : null;
                Vector2 scale = native != null ? native.mainTextureScale : Vector2.one;
                Vector2 offset = native != null ? native.mainTextureOffset : Vector2.zero;

                // Reuse only SoD's Entity shader contract. Cloning the whole Vesper material also
                // copied Vesper-specific normal/metallic/occlusion maps and keywords, which made
                // Darius visible but produced incorrect surface colour and lighting.
                Material replacement = new Material(stockShader);
                replacement.name = (native != null ? native.name : "Darius") + "_SOD";
                replacement.renderQueue = stockTemplate.renderQueue;

                // The pre-native Darius renderer deliberately treated the Riot model as
                // texture-dominant/unlit. The Blender->FBX sidecar currently preserves the
                // base-color texture but not the GLB baseColorFactor/PBR factors, so the imported
                // FBX material colour (0.8 for Classic in runtime diagnostics) is not an
                // authoritative Riot value. Keep SoD's Entity shader contract, but configure it
                // as a neutral matte surface so the authored base texture remains the visual source.
                ConfigureTextureDominantEntitySurface(replacement);

                if (texture != null)
                {
                    replacement.mainTexture = texture;
                    replacement.mainTextureScale = scale;
                    replacement.mainTextureOffset = offset;
                    if (replacement.HasProperty("_MainTex"))
                    {
                        replacement.SetTexture("_MainTex", texture);
                        replacement.SetTextureScale("_MainTex", scale);
                        replacement.SetTextureOffset("_MainTex", offset);
                    }
                    if (replacement.HasProperty("_BaseMap"))
                    {
                        replacement.SetTexture("_BaseMap", texture);
                        replacement.SetTextureScale("_BaseMap", scale);
                        replacement.SetTextureOffset("_BaseMap", offset);
                    }
                }
                replacements[mi] = replacement;
            }
            renderer.sharedMaterials = replacements;
        }

        DariusLog.Info("OFFICIAL-ENTITYMODEL",
            "Applied stock shader contract sourceRenderer=" + stockRenderer.name +
            " sourceMaterial=" + stockTemplate.name +
            " shader=" + stockShader.name +
            " targets=" + targetRenderers.Length);
    }

    private static void ConfigureTextureDominantEntitySurface(Material material)
    {
        if (material == null) return;

        SetMaterialColorIfPresent(material, "_Color", Color.white);
        SetMaterialColorIfPresent(material, "_BaseColor", Color.white);
        SetMaterialColorIfPresent(material, "_SpecColor", Color.black);
        SetMaterialColorIfPresent(material, "_SpecularColor", Color.black);

        // Common lit/PBR controls. Dew/Dew Entity is a custom shader, so every write is guarded by
        // HasProperty; unsupported names are simply ignored.
        SetMaterialFloatIfPresent(material, "_Metallic", 0f);
        SetMaterialFloatIfPresent(material, "_MetallicFactor", 0f);
        SetMaterialFloatIfPresent(material, "_Smoothness", 0f);
        SetMaterialFloatIfPresent(material, "_Glossiness", 0f);
        SetMaterialFloatIfPresent(material, "_GlossMapScale", 0f);
        SetMaterialFloatIfPresent(material, "_Shininess", 0f);
        SetMaterialFloatIfPresent(material, "_SpecularHighlights", 0f);
        SetMaterialFloatIfPresent(material, "_GlossyReflections", 0f);
        SetMaterialFloatIfPresent(material, "_EnvironmentReflections", 0f);
        SetMaterialFloatIfPresent(material, "_Roughness", 1f);
        SetMaterialFloatIfPresent(material, "_RoughnessFactor", 1f);

        ClearMaterialTextureIfPresent(material, "_MetallicGlossMap");
        ClearMaterialTextureIfPresent(material, "_SpecGlossMap");
        ClearMaterialTextureIfPresent(material, "_BumpMap");
        ClearMaterialTextureIfPresent(material, "_NormalMap");
        ClearMaterialTextureIfPresent(material, "_OcclusionMap");
        ClearMaterialTextureIfPresent(material, "_ParallaxMap");
        ClearMaterialTextureIfPresent(material, "_DetailNormalMap");

        material.DisableKeyword("_METALLICSPECGLOSSMAP");
        material.DisableKeyword("_SPECGLOSSMAP");
        material.DisableKeyword("_NORMALMAP");
        material.DisableKeyword("_OCCLUSIONMAP");
        material.DisableKeyword("_PARALLAXMAP");
        material.DisableKeyword("_DETAIL_MULX2");
        material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");

        DariusLog.Info("OFFICIAL-ENTITYMODEL",
            "Configured texture-dominant matte entity surface material=" + material.name +
            " shader=" + (material.shader != null ? material.shader.name : "<null>") +
            " color=" + (material.HasProperty("_Color") ? material.GetColor("_Color").ToString() : "<none>") +
            " metallic=" + MaterialFloatOrMissing(material, "_Metallic") +
            " smoothness=" + MaterialFloatOrMissing(material, "_Smoothness") +
            " glossiness=" + MaterialFloatOrMissing(material, "_Glossiness") +
            " roughness=" + MaterialFloatOrMissing(material, "_Roughness") +
            " specularHighlights=" + MaterialFloatOrMissing(material, "_SpecularHighlights") +
            " environmentReflections=" + MaterialFloatOrMissing(material, "_EnvironmentReflections"));
    }

    private static void SetMaterialFloatIfPresent(Material material, string property, float value)
    {
        if (material != null && material.HasProperty(property))
            material.SetFloat(property, value);
    }

    private static void SetMaterialColorIfPresent(Material material, string property, Color value)
    {
        if (material != null && material.HasProperty(property))
            material.SetColor(property, value);
    }

    private static void ClearMaterialTextureIfPresent(Material material, string property)
    {
        if (material != null && material.HasProperty(property))
            material.SetTexture(property, null);
    }

    private static string MaterialFloatOrMissing(Material material, string property)
    {
        return material != null && material.HasProperty(property)
            ? material.GetFloat(property).ToString("0.###")
            : "<none>";
    }

    private static AnimationClip RequireOfficialClip(
        Dictionary<string, AnimationClip> clips,
        string name,
        string slot)
    {
        AnimationClip clip;
        if (string.IsNullOrEmpty(name) || clips == null || !clips.TryGetValue(name, out clip) || clip == null)
            throw new InvalidOperationException("Official EntityModel template missing " + slot + " clip=" + (name ?? "<null>"));
        return clip;
    }

    private static Transform FindOfficialAnchor(Transform root, string[] names)
    {
        if (root == null || names == null) return null;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int ni = 0; ni < names.Length; ni++)
        {
            string name = names[ni];
            if (string.IsNullOrEmpty(name)) continue;
            for (int ti = 0; ti < all.Length; ti++)
            {
                Transform t = all[ti];
                if (t != null && string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase))
                    return t;
            }
        }
        return null;
    }

    private static Transform CreateSkinAnchor(Transform parent, string name, Vector3 localPosition)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }
}
