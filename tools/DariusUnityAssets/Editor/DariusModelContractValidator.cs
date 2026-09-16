#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

internal static class DariusModelContractValidator
{
    public static void Validate(GameObject source, AnimationClip[] clips, DariusNativeSkinProfile profile)
    {
        if (source == null || profile == null) throw new InvalidOperationException("Native model contract received null input.");
        ValidateAnimations(clips, profile);
        ValidateGeometry(source, profile);
        ValidateAnchors(source, profile);
        if (profile.GodKing) ValidateGodKingMaterials(source, profile);
    }

    private static void ValidateAnimations(AnimationClip[] clips, DariusNativeSkinProfile profile)
    {
        int count = clips != null ? clips.Length : 0;
        if (profile.ExpectedAnimations > 0 && count != profile.ExpectedAnimations)
            throw new InvalidOperationException("Animation count mismatch skin=" + profile.Variant +
                " expected=" + profile.ExpectedAnimations + " actual=" + count);

        HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (clips != null)
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] != null && !string.IsNullOrEmpty(clips[i].name)) names.Add(clips[i].name);

        string[] required = profile.RequiredClips;
        for (int i = 0; i < required.Length; i++)
            if (!string.IsNullOrEmpty(required[i]) && !names.Contains(required[i]))
                throw new InvalidOperationException("Required animation missing skin=" + profile.Variant + " clip=" + required[i]);
    }

    private static void ValidateGeometry(GameObject source, DariusNativeSkinProfile profile)
    {
        SkinnedMeshRenderer[] renderers = source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        HashSet<Transform> bones = new HashSet<Transform>();
        int primitives = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = renderers[i];
            if (renderer == null || renderer.sharedMesh == null) continue;
            primitives += renderer.sharedMesh.subMeshCount;
            Transform[] rendererBones = renderer.bones;
            if (rendererBones == null) continue;
            for (int b = 0; b < rendererBones.Length; b++)
                if (rendererBones[b] != null) bones.Add(rendererBones[b]);
        }

        if (profile.ExpectedPrimitives > 0 && primitives != profile.ExpectedPrimitives)
            throw new InvalidOperationException("Primitive count mismatch skin=" + profile.Variant +
                " expected=" + profile.ExpectedPrimitives + " actual=" + primitives);
        if (profile.ExpectedBones > 0 && bones.Count != profile.ExpectedBones)
            throw new InvalidOperationException("Bone count mismatch skin=" + profile.Variant +
                " expected=" + profile.ExpectedBones + " actual=" + bones.Count);
    }

    private static void ValidateAnchors(GameObject source, DariusNativeSkinProfile profile)
    {
        Transform[] transforms = source.GetComponentsInChildren<Transform>(true);
        bool health = HasAny(transforms, "C_BuffBone_Glb_Chest_Loc", "Chest", "Spine");
        bool weapon = HasAny(transforms, "BuffBone_Glb_Weapon_1", "Weapon", "R_Hand");
        if (!health) throw new InvalidOperationException("Health anchor missing skin=" + profile.Variant);
        if (!weapon) throw new InvalidOperationException("Weapon anchor missing skin=" + profile.Variant);
    }

    private static void ValidateGodKingMaterials(GameObject source, DariusNativeSkinProfile profile)
    {
        Renderer[] renderers = source.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materials = renderers[i] != null ? renderers[i].sharedMaterials : null;
            if (materials == null) continue;
            for (int m = 0; m < materials.Length; m++)
            {
                string name = materials[m] != null ? materials[m].name : null;
                if (!string.IsNullOrEmpty(name) && name.IndexOf("Wolf_Mat", StringComparison.OrdinalIgnoreCase) >= 0) return;
            }
        }
        throw new InvalidOperationException("God-King wolf material missing skin=" + profile.Variant);
    }

    private static bool HasAny(Transform[] transforms, params string[] names)
    {
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            if (t == null) continue;
            for (int n = 0; n < names.Length; n++)
                if (string.Equals(t.name, names[n], StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
#endif