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
    private static T CreateDariusAttackInstance<T>(MeleeAttackInstance source, string name, string guid, uint assetId) where T : MeleeAttackInstance
    {
        GameObject go = CloneTemplateInactive(source.gameObject, name);
        go.name = name;
        go.hideFlags = HideFlags.HideAndDontSave;
        // Same lifecycle contract as Hero_Darius: the resource root keeps this object
        // inactiveInHierarchy, while activeSelf=true is preserved so a runtime Instantiate() is
        // born active and initializes Actor/MeleeAttackInstance before parentActor is assigned.
        go.transform.SetParent(_resourceRoot.transform, false);
        go.SetActive(true);

        MeleeAttackInstance oldInstance = go.GetComponent<MeleeAttackInstance>();
        if (oldInstance == null)
        {
            UnityEngine.Object.Destroy(go);
            throw new InvalidOperationException("Cloned stock melee instance template has no MeleeAttackInstance: " + name);
        }
        Dictionary<FieldInfo, object> fields = CaptureUnitySerializedFields(oldInstance, typeof(MeleeAttackInstance));
        T instance = go.AddComponent<T>();
        RestoreUnitySerializedFields(instance, fields);
        ReplaceDirectComponentReferences(go, oldInstance, instance);
        int instanceResidualRefs = CountDirectComponentReferences(go, oldInstance, instance);
        if (instanceResidualRefs > 0)
            DariusLog.Warn("ATK-NATIVE-PREFAB", name + " structural template still has " + instanceResidualRefs + " direct references to the old component before destruction.");
        UnityEngine.Object.DestroyImmediate(oldInstance);

        // Gameplay fields are retained; all character-specific presentation is explicitly severed.
        instance.fxHitMain = null;
        instance.fxHitSub = null;
        instance.startEffectNoStop = null;
        instance.startEffect = null;
        instance.endEffect = null;
        instance.name = name;
        StripConstructionTemplatePresentation(go, instance);
        ConfigureDariusDirectionalAttackHitVolume(go, name);
        // Normalize the inherited overlap volume, then let MeleeAttackInstance scale it from the
        // live TriggerConfig.effectiveRange. A separate center-distance guard prevents stale/shared
        // target-range data from producing remote melee hits.
        TryAssignIfCompatible(instance, "scaleRangeWithTriggerRange", false);

        NetworkIdentity identity = go.GetComponent<NetworkIdentity>();
        if (identity == null) identity = go.AddComponent<NetworkIdentity>();
        DariusUnsupportedResourceBridge.ConfigureTemplateIdentity(identity, assetId, name);
        // The bootstrap object is still inactiveInHierarchy because its parent resource root is
        // inactive. Its activeSelf=true is preserved specifically for the spawned clone lifecycle.
        DariusUnsupportedResourceBridge.RebuildNetworkBehaviours(identity, name);

        go.hideFlags = HideFlags.None;
        OwnedObjects.Add(go);
        RegisterTypedResource(instance, go, name, guid, assetId);
        DariusLog.Info("ATK-NATIVE-PREFAB", "Registered " + name + " from native MeleeAttackInstance structure with Darius concrete component; activeSelf=" + go.activeSelf +
            " activeInHierarchy=" + go.activeInHierarchy + " runtimeVesperComponents=" + CountVesperNamedComponents(go));
        return instance;
    }

    private static void ConfigureDariusDirectionalAttackHitVolume(GameObject root, string label)
    {
        if (root == null) return;
        // Broad phase is a caster-centred circle. scaleRangeWithTriggerRange is disabled so no
        // inherited Vesper multiplier can silently expand this volume. DoBasicAttackHit is then
        // narrowed to the exact forward sector by the directional geometry patch.
        float radius = At_DariusAxe.AttackRange;
        float diameter = radius * 2f;
        int sphere = 0, capsule = 0, box = 0, dewFields = 0;
        try
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                SphereCollider sc = colliders[i] as SphereCollider;
                if (sc != null) { sc.radius = radius; sphere++; continue; }
                CapsuleCollider cc = colliders[i] as CapsuleCollider;
                if (cc != null) { cc.radius = radius; cc.height = Mathf.Max(diameter, cc.height); capsule++; continue; }
                BoxCollider bc = colliders[i] as BoxCollider;
                if (bc != null)
                {
                    Vector3 size = bc.size; size.x = diameter; size.z = diameter; bc.size = size; box++; continue;
                }
            }

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component.GetType().Name.IndexOf("DewCollider", StringComparison.OrdinalIgnoreCase) < 0) continue;
                for (Type t = component.GetType(); t != null && t != typeof(Component); t = t.BaseType)
                {
                    FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    for (int f = 0; f < fields.Length; f++)
                    {
                        FieldInfo field = fields[f];
                        if (field.IsStatic || field.IsInitOnly) continue;
                        string n = field.Name.ToLowerInvariant();
                        try
                        {
                            if (field.FieldType == typeof(float) && (n.Contains("radius") || n.Contains("range")))
                            {
                                float old = (float)field.GetValue(component);
                                if (old > 0.001f) { field.SetValue(component, radius); dewFields++; }
                            }
                            else if (field.FieldType == typeof(Vector3) && n.Contains("size"))
                            {
                                Vector3 value = (Vector3)field.GetValue(component);
                                value.x = diameter; value.z = diameter; field.SetValue(component, value); dewFields++;
                            }
                        }
                        catch { }
                    }
                }
            }
            DariusLog.Info("ATK-HITBOX", "Directional broad-phase " + label + " radius=" + radius.ToString("0.###") +
                " scaleWithTriggerRange=false sphere=" + sphere + " capsule=" + capsule + " box=" + box + " dewFields=" + dewFields);
        }
        catch (Exception e)
        {
            DariusLog.Exception("ATK-HITBOX", e, "Failed configuring directional broad-phase for " + label);
        }
    }

    private static GameObject CloneTemplateInactive(GameObject source, string debugName)
    {
        if (source == null) throw new ArgumentNullException("source");
        // Instantiate directly under the inactive DontDestroyOnLoad resource root. This prevents
        // inherited activeSelf=true from ever becoming activeInHierarchy during construction, so
        // DewCollider.OnEnable cannot call DewPhysics before its proxy pool exists. The stock
        // template itself is never toggled or mutated.
        GameObject clone = UnityEngine.Object.Instantiate(source, _resourceRoot.transform, false);
        clone.SetActive(false);
        DariusLog.DebugInfo("TRAVELER-COPY", "Cloned native template under inactive resource root name=" +
            debugName + " source=" + source.name);
        return clone;
    }

    private static void StripConstructionTemplatePresentation(GameObject root, Component keepMain)
    {
        if (root == null) return;
        Component[] components = root.GetComponentsInChildren<Component>(true);
        int removed = 0;
        for (int i = components.Length - 1; i >= 0; i--)
        {
            Component c = components[i];
            if (c == null || c == keepMain || c is Transform || c is NetworkIdentity) continue;
            bool presentation = c is Renderer || c is AudioSource || c is ParticleSystem;
            string typeName = c.GetType().Name;
            bool characterSpecific = typeName.IndexOf("Vesper", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     typeName.IndexOf("Mace", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!presentation && !characterSpecific) continue;
            try { UnityEngine.Object.DestroyImmediate(c); removed++; } catch { }
        }
        DariusLog.DebugInfo("ATK-NATIVE-PREFAB", "Construction-time template scrub root=" + root.name + " removedComponents=" + removed);
    }

    private static int CountVesperNamedComponents(GameObject root)
    {
        if (root == null) return 0;
        int count = 0;
        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (c != null && c.GetType().Name.IndexOf("Vesper", StringComparison.OrdinalIgnoreCase) >= 0) count++;
        }
        return count;
    }
}