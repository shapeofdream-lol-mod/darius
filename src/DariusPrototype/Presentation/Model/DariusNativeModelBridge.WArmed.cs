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
        _wArmedPresentation = armed;

        if (!IsGodKingSkin)
        {
            ApplyWLocomotionOverrides();
            return;
        }

        if (!armed && _wSwingActive)
        {
            ApplyWLocomotionOverrides();
            return;
        }

        bool moving = IsRunningState();
        StopSequence();
        _sequence = StartCoroutine(PlayGodKingWTransition(armed, moving));
    }

    private IEnumerator PlayGodKingWTransition(bool armed, bool moving)
    {
        DariusNativeSkinProfile profile = DariusNativeSkinProfiles.Find(VariantKey);
        if (profile == null) throw new InvalidOperationException("Native skin profile missing: " + VariantKey);
        if (!armed) ApplyWLocomotionOverrides();
        BeginAnimatorAction();

        if (armed)
        {
            string activate = moving ? profile.WActivateRun : profile.WActivateIdle;
            if (PlayState(activate, 0.04f, 1f))
                yield return new WaitForSeconds(ClipLength(activate, 0.25f));

            if (!moving && _wArmedPresentation)
            {
                _wIdleInAlt = !_wIdleInAlt;
                string entry = _wIdleInAlt ? profile.WIdleInAlt : profile.WIdleIn;
                if (PlayState(entry, 0.04f, 1f))
                    yield return new WaitForSeconds(ClipLength(entry, 0.2f));
            }
        }
        else
        {
            _wDeactivateAlt = !_wDeactivateAlt;
            string deactivate = _wDeactivateAlt ? profile.WDeactivateAlt : profile.WDeactivate;
            if (PlayState(deactivate, 0.04f, 1f))
                yield return new WaitForSeconds(ClipLength(deactivate, 0.2f));
        }

        EndAnimatorAction();
        _sequence = null;
        if (armed && _wArmedPresentation) ApplyWLocomotionOverrides();
    }

    private void ApplyWLocomotionOverrides()
    {
        if (_entityAnimation == null) return;
        EnsureWLocomotionSlots();
        AnimationClip idle = _wArmedPresentation
            ? (FindClip("Spell2_Idle") ?? FindClip(IdleClipName))
            : FindClip(IdleClipName);
        AnimationClip run = _wArmedPresentation
            ? (FindClip("Spell2_Run") ?? FindClip(RunClipName))
            : FindClip(RunClipName);

        int idleBindings = ReplaceLocomotionSlots(_wIdleSlots, idle);
        int runBindings = ReplaceLocomotionSlots(_wRunSlots, run);
        if (idleBindings == 0 || runBindings == 0)
            throw new InvalidOperationException("EntityAnimation W locomotion replacement contract failed skin=" + VariantKey +
                " idle=" + idleBindings + " run=" + runBindings);
        DariusLog.DebugInfo("NATIVE-W", "armed=" + _wArmedPresentation +
            " idle=" + (idle != null ? idle.name : "<null>") + " run=" + (run != null ? run.name : "<null>") +
            " replaceableIdle=" + idleBindings + " replaceableRun=" + runBindings);
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
            if (name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0) idle.Add(type);
            if (name.IndexOf("Run", StringComparison.OrdinalIgnoreCase) >= 0) run.Add(type);
        }
        if (idle.Count == 0 || run.Count == 0)
            throw new InvalidOperationException("EntityAnimation.ReplaceableAnimationType exposes no Idle/Run locomotion slots.");
        _wIdleSlots = idle.ToArray();
        _wRunSlots = run.ToArray();
    }

    private int ReplaceLocomotionSlots(EntityAnimation.ReplaceableAnimationType[] slots, AnimationClip clip)
    {
        if (_entityAnimation == null || clip == null || slots == null) return 0;
        for (int i = 0; i < slots.Length; i++) _entityAnimation.ReplaceAnimation(slots[i], clip);
        return slots.Length;
    }

    private void ResetWLocomotionOverrides()
    {
        if (!_wArmedPresentation) return;
        _wArmedPresentation = false;
        ApplyWLocomotionOverrides();
    }
}
