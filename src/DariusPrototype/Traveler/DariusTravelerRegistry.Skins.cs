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
        GameObject go = new GameObject(spec.name);
        go.transform.SetParent(_resourceRoot.transform, false);
        go.SetActive(true);
        go.hideFlags = HideFlags.None;

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
        model.healthBarPosition = CreateSkinAnchor(go.transform, spec.name + "_HealthBarAnchor", new Vector3(0f, 2.75f, 0f));
        model.weapon = CreateSkinAnchor(go.transform, spec.name + "_WeaponAnchor", new Vector3(0f, 1.25f, 0.45f));
        model.holsteredWeapon = CreateSkinAnchor(go.transform, spec.name + "_HolsteredWeaponAnchor", new Vector3(0f, 1.2f, -0.25f));

        if (go.GetComponent<DariusNativeModelHost>() == null) go.AddComponent<DariusNativeModelHost>();
        if (go.GetComponent<DariusTravelerModelInstance>() == null) go.AddComponent<DariusTravelerModelInstance>();
        OwnedObjects.Add(go);
        RegisterNamedResource(skin, go, spec.name, spec.guid);
        DariusLog.Info("TRAVELER-SKIN", "Created runtime skin=" + spec.name + " guid=" + spec.guid +
            " model=" + profile.GlbFile + " profile=" + profile.Variant + " display=" + spec.displayName);
        return skin;
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
