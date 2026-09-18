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
            AnimationClipWithSpeed idleWithSpeed = ClipWithSpeed(idle, 1f);
            _entityModel.idle = idleWithSpeed;
            _entityModel.lobby = idleWithSpeed;
            // SoD exposes PlayStaggerAnimation() against EntityModel.stagger. Darius has no
            // identified authored stagger clip, so use Darius' own stable Idle as a safe no-op
            // fallback instead of inheriting Vesper's foreign clip or leaving the slot empty.
            _entityModel.stagger = idleWithSpeed;
        }
        else
        {
            _entityModel.stagger = default(AnimationClipWithSpeed);
        }
        if (death != null) _entityModel.death = ClipWithSpeed(death, 1f);

        if (run != null)
        {
            // Darius assets expose one authored Run clip. Populate every directional slot so the
            // inherited locomotion mode can always resolve a native Darius clip.
            _entityModel.runForwardClip = run;
            _entityModel.runBackwardClip = run;
            _entityModel.runLeftClip = run;
            _entityModel.runRightClip = run;
            _entityModel.runForwardLeftClip = run;
            _entityModel.runForwardRightClip = run;
            _entityModel.runBackwardLeftClip = run;
            _entityModel.runBackwardRightClip = run;
        }

        Transform health = FindAnchor(DariusNativeAssetContract.HealthAnchorNames);
        Transform weapon = FindAnchor(DariusNativeAssetContract.WeaponAnchorNames);
        if (health != null) _entityModel.healthBarPosition = health;
        if (weapon != null) _entityModel.weapon = weapon;
    }

    private Transform FindAnchor(string[] names)
    {
        if (names == null) return null;
        for (int i = 0; i < names.Length; i++)
        {
            Transform anchor = GetAnchor(names[i]);
            if (anchor != null) return anchor;
        }
        return null;
    }

    private static AnimationClipWithSpeed ClipWithSpeed(AnimationClip clip, float speed)
    {
        AnimationClipWithSpeed value = new AnimationClipWithSpeed();
        value.clip = clip;
        value.speed = speed;
        return value;
    }
}
