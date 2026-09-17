using System;
using System.Collections.Generic;
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
        List<Renderer> wolfRenderers = IsGodKingSkin ? new List<Renderer>() : null;
        for (int i = 0; i < allRenderers.Length; i++)
        {
            Renderer renderer = allRenderers[i];
            if (renderer == null) continue;
            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null) skinned.updateWhenOffscreen = false;
            bool hidden = IsGodKingSkin && IsHiddenPresentationRenderer(renderer);
            if (!hidden)
            {
                visibleRenderers.Add(renderer);
                continue;
            }

            renderer.enabled = false;
            if (IsGodKingWolfRenderer(renderer)) wolfRenderers.Add(renderer);
        }

        _godKingWolfRenderers = wolfRenderers != null ? wolfRenderers.ToArray() : null;
        _entityModel.bodyRenderers = visibleRenderers.ToArray();
        AnimationClip idle = FindClip(IdleClipName);
        AnimationClip death = FindClip(DeathClipName);
        AnimationClip run = FindClip(RunClipName);
        if (idle != null)
        {
            _entityModel.idle = ClipWithSpeed(idle, 1f);
            _entityModel.lobby = ClipWithSpeed(idle, 1f);
        }
        if (death != null) _entityModel.death = ClipWithSpeed(death, 1f);
        if (run != null) _entityModel.runForwardClip = run;

        Transform health = GetAnchor("C_BuffBone_Glb_Chest_Loc") ?? GetAnchor("Chest") ?? GetAnchor("Spine");
        Transform weapon = GetAnchor("BuffBone_Glb_Weapon_1") ?? GetAnchor("Weapon") ?? GetAnchor("R_Hand");
        if (health != null) _entityModel.healthBarPosition = health;
        if (weapon != null) _entityModel.weapon = weapon;
    }

    private static AnimationClipWithSpeed ClipWithSpeed(AnimationClip clip, float speed)
    {
        AnimationClipWithSpeed value = new AnimationClipWithSpeed();
        value.clip = clip;
        value.speed = speed;
        return value;
    }
}
