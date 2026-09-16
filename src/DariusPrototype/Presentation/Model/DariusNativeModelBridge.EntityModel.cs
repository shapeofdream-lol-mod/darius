using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
    private void CacheClips()
    {
        _clips.Clear();
        RuntimeAnimatorController controller = _animator.runtimeAnimatorController;
        AnimationClip[] clips = controller != null ? controller.animationClips : Array.Empty<AnimationClip>();
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && !_clips.ContainsKey(clip.name)) _clips.Add(clip.name, clip);
        }
        if (_clips.Count == 0)
            throw new InvalidOperationException("Native Darius AnimatorController exposes no AnimationClips: " + VariantKey);
    }

    private void CacheAnchors()
    {
        _anchors.Clear();
        if (_modelRoot == null) return;
        Transform[] all = _modelRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t != null && !string.IsNullOrEmpty(t.name) && !_anchors.ContainsKey(t.name)) _anchors.Add(t.name, t);
        }
    }

    private void ConfigureEntityModel()
    {
        if (_entityModel == null || _modelRoot == null) return;
        Renderer[] allRenderers = _modelRoot.GetComponentsInChildren<Renderer>(true);
        List<Renderer> visibleRenderers = new List<Renderer>(allRenderers.Length);
        List<Renderer> authoredHidden = IsGodKingSkin ? new List<Renderer>() : null;
        for (int i = 0; i < allRenderers.Length; i++)
        {
            Renderer renderer = allRenderers[i];
            if (renderer == null) continue;
            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null) skinned.updateWhenOffscreen = false;
            bool hidden = IsGodKingSkin && IsHiddenPresentationRenderer(renderer);
            if (hidden) authoredHidden.Add(renderer);
            else visibleRenderers.Add(renderer);
        }

        _godKingWolfRenderers = authoredHidden != null ? authoredHidden.ToArray() : null;
        _entityModel.bodyRenderers = visibleRenderers.ToArray();
        AnimationClip run = FindClip(RunClipName);
        if (run != null) _entityModel.runForwardClip = run;
        AssignClipWithSpeed(_entityModel, "idle", FindClip(IdleClipName), 1f);
        AssignClipWithSpeed(_entityModel, "lobby", FindClip(IdleClipName), 1f);
        AssignClipWithSpeed(_entityModel, "death", FindClip(DeathClipName), 1f);

        Transform health = GetAnchor("C_BuffBone_Glb_Chest_Loc") ?? GetAnchor("Chest") ?? GetAnchor("Spine");
        Transform weapon = GetAnchor("BuffBone_Glb_Weapon_1") ?? GetAnchor("Weapon") ?? GetAnchor("R_Hand");
        if (health != null) _entityModel.healthBarPosition = health;
        if (weapon != null) _entityModel.weapon = weapon;
    }

    private static void AssignClipWithSpeed(EntityModel model, string fieldName, AnimationClip clip, float speed)
    {
        if (model == null || clip == null) return;
        try
        {
            FieldInfo ownerField = typeof(EntityModel).GetField(
                fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (ownerField == null) return;
            object boxed = ownerField.GetValue(model) ?? Activator.CreateInstance(ownerField.FieldType);
            FieldInfo[] fields = ownerField.FieldType.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.FieldType == typeof(AnimationClip)) field.SetValue(boxed, clip);
                else if (field.FieldType == typeof(float) &&
                         field.Name.IndexOf("speed", StringComparison.OrdinalIgnoreCase) >= 0)
                    field.SetValue(boxed, speed);
            }
            ownerField.SetValue(model, boxed);
        }
        catch (Exception e)
        {
            DariusLog.Exception("NATIVE-MODEL", e,
                "Failed binding EntityModel." + fieldName + " to clip=" + clip.name);
        }
    }
}