using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
    private bool _wArmedPresentation;
    private bool _wSwingActive;
    private bool _wIdleInAlt;
    private bool _wDeactivateAlt;
    private EntityAnimation.ReplaceableAnimationType[] _wIdleSlots;
    private EntityAnimation.ReplaceableAnimationType[] _wRunSlots;

    public void SetWArmed(bool armed)
    {
        if (_wArmedPresentation == armed) return;
        bool moving = IsRunningState();

        if (IsGodKingSkin && !armed && _wSwingActive)
        {
            ApplyWLocomotionOverrides(false);
            _wArmedPresentation = false;
            return;
        }

        if (IsGodKingSkin) StopSequence();
        ApplyWLocomotionOverrides(armed);
        _wArmedPresentation = armed;

        if (IsGodKingSkin)
            _sequence = StartCoroutine(PlayGodKingWTransition(armed, moving));
    }

    private IEnumerator PlayGodKingWTransition(bool armed, bool moving)
    {
        DariusNativeSkinProfile profile = DariusNativeSkinProfiles.Find(VariantKey);
        if (profile == null) throw new InvalidOperationException("Native skin profile missing: " + VariantKey);
        BeginAnimatorAction();
        try
        {
            if (armed)
            {
                string activate = moving ? profile.WActivateRun : profile.WActivateIdle;
                if (PlayState(activate, 0.04f, 1f))
                    yield return WaitForActionDuration(ClipLength(activate, 0.25f));

                if (!moving && _wArmedPresentation)
                {
                    _wIdleInAlt = !_wIdleInAlt;
                    string entry = _wIdleInAlt ? profile.WIdleInAlt : profile.WIdleIn;
                    if (PlayState(entry, 0.04f, 1f))
                        yield return WaitForActionDuration(ClipLength(entry, 0.2f));
                }
            }
            else
            {
                _wDeactivateAlt = !_wDeactivateAlt;
                string deactivate = _wDeactivateAlt ? profile.WDeactivateAlt : profile.WDeactivate;
                if (PlayState(deactivate, 0.04f, 1f))
                    yield return WaitForActionDuration(ClipLength(deactivate, 0.2f));
            }
        }
        finally
        {
            EndAnimatorAction();
            _sequence = null;
        }
    }

    private void ApplyWLocomotionOverrides(bool armed)
    {
        if (_entityAnimation == null)
            throw new InvalidOperationException("EntityAnimation is unavailable for W locomotion replacement.");
        EnsureWLocomotionSlots();

        AnimationClip baseIdle = FindClip(IdleClipName);
        AnimationClip baseRun = FindClip(RunClipName);
        DariusNativeSkinProfile profile = DariusNativeSkinProfiles.Find(VariantKey);
        AnimationClip armedIdle = profile != null && !string.IsNullOrEmpty(profile.WIdle)
            ? FindClip(profile.WIdle)
            : baseIdle;
        AnimationClip armedRun = profile != null && !string.IsNullOrEmpty(profile.WRun)
            ? FindClip(profile.WRun)
            : baseRun;
        if (baseIdle == null || baseRun == null || armedIdle == null || armedRun == null)
            throw new InvalidOperationException("Native W locomotion clips are incomplete skin=" + VariantKey);

        AnimationClip idle = armed ? armedIdle : baseIdle;
        AnimationClip run = armed ? armedRun : baseRun;
        try
        {
            ApplyWSlots(_wIdleSlots, idle);
            ApplyWSlots(_wRunSlots, run);
        }
        catch
        {
            if (armed)
            {
                try { ApplyWSlots(_wIdleSlots, baseIdle); } catch { }
                try { ApplyWSlots(_wRunSlots, baseRun); } catch { }
            }
            throw;
        }

        DariusLog.DebugInfo("NATIVE-W", "armed=" + armed +
            " idle=" + idle.name + " run=" + run.name +
            " replaceableIdle=" + _wIdleSlots.Length + " replaceableRun=" + _wRunSlots.Length);
    }

    private void EnsureWLocomotionSlots()
    {
        if (_wIdleSlots != null && _wRunSlots != null) return;
        Array values = Enum.GetValues(typeof(EntityAnimation.ReplaceableAnimationType));
        List<EntityAnimation.ReplaceableAnimationType> idle = new List<EntityAnimation.ReplaceableAnimationType>();
        List<EntityAnimation.ReplaceableAnimationType> run = new List<EntityAnimation.ReplaceableAnimationType>();
        for (int i = 0; i < values.Length; i++)
        {
            EntityAnimation.ReplaceableAnimationType type =
                (EntityAnimation.ReplaceableAnimationType)values.GetValue(i);
            string name = type.ToString();
            if (IsLocomotionSlot(name, "Idle")) idle.Add(type);
            if (IsLocomotionSlot(name, "Run")) run.Add(type);
        }
        if (idle.Count == 0 || run.Count == 0)
            throw new InvalidOperationException("EntityAnimation.ReplaceableAnimationType exposes no Idle/Run locomotion slots.");
        _wIdleSlots = idle.ToArray();
        _wRunSlots = run.ToArray();
    }

    private static bool IsLocomotionSlot(string name, string prefix)
    {
        return !string.IsNullOrEmpty(name) &&
               name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyWSlots(EntityAnimation.ReplaceableAnimationType[] slots, AnimationClip clip)
    {
        for (int i = 0; i < slots.Length; i++)
            _entityAnimation.ReplaceAnimationLocal(slots[i], clip);
    }

    private void ResetWLocomotionOverrides()
    {
        if (_entityAnimation == null)
        {
            _wArmedPresentation = false;
            return;
        }
        try
        {
            if (_wArmedPresentation) ApplyWLocomotionOverrides(false);
        }
        catch (Exception e)
        {
            DariusLog.Exception("NATIVE-W", e, "Failed restoring W locomotion overrides skin=" + VariantKey);
        }
        _wArmedPresentation = false;
    }
}
