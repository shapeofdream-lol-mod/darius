using System;
using System.Collections;
using UnityEngine;

public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
    private bool _wArmedPresentation;
    private bool _wSwingActive;
    private bool _wIdleInAlt;
    private bool _wDeactivateAlt;

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
        if (!armed) ApplyWLocomotionOverrides();
        BeginAnimatorAction();

        if (armed)
        {
            string activate = FirstExisting(moving ? "Spell2_ActivateRun" : "Spell2_ActivateIdle",
                "Spell2_ActivateIdle", "Spell2_ActivateRun");
            if (FindClip(activate) != null && PlayState(activate, 0.04f, 1f))
                yield return new WaitForSeconds(ClipLength(activate, 0.25f));

            if (!moving && _wArmedPresentation)
            {
                _wIdleInAlt = !_wIdleInAlt;
                string entry = FirstExisting(_wIdleInAlt ? "Spell2_IdleIn2" : "Spell2_IdleIn",
                    "Spell2_IdleIn", "Spell2_IdleIn2");
                if (FindClip(entry) != null && PlayState(entry, 0.04f, 1f))
                    yield return new WaitForSeconds(ClipLength(entry, 0.2f));
            }
        }
        else
        {
            _wDeactivateAlt = !_wDeactivateAlt;
            string deactivate = FirstExisting(
                _wDeactivateAlt ? "Darius_Skin15_Spell2_Deactivate.anm" : "Spell2_Deactivate",
                "Spell2_Deactivate", "Darius_Skin15_Spell2_Deactivate.anm");
            if (FindClip(deactivate) != null && PlayState(deactivate, 0.04f, 1f))
                yield return new WaitForSeconds(ClipLength(deactivate, 0.2f));
        }

        EndAnimatorAction();
        _sequence = null;
        if (armed && _wArmedPresentation) ApplyWLocomotionOverrides();
    }

    private void ApplyWLocomotionOverrides()
    {
        if (_entityAnimation == null) return;
        AnimationClip idle = _wArmedPresentation
            ? (FindClip("Spell2_Idle") ?? FindClip(IdleClipName))
            : FindClip(IdleClipName);
        AnimationClip run = _wArmedPresentation
            ? (FindClip("Spell2_Run") ?? FindClip(RunClipName))
            : FindClip(RunClipName);

        int idleBindings = ReplaceLocomotionFamily("Idle", idle);
        int runBindings = ReplaceLocomotionFamily("Run", run);
        DariusLog.DebugInfo("NATIVE-W", "armed=" + _wArmedPresentation+
            " idle=" + (idle != null ? idle.name : "<null>") + " run=" + (run != null ? run.name : "<null>")+
            " replaceableIdle=" + idleBindings + " replaceableRun=" + runBindings);
    }

    private int ReplaceLocomotionFamily(string token, AnimationClip clip)
    {
        if (_entityAnimation == null || clip == null) return 0;
        Array values = Enum.GetValues(typeof(EntityAnimation.ReplaceableAnimationType));
        int replaced = 0;
        for (int i = 0; i < values.Length; i++)
        {
            EntityAnimation.ReplaceableAnimationType type =
                (EntityAnimation.ReplaceableAnimationType)values.GetValue(i);
            string name = type.ToString();
            if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0) continue;
            _entityAnimation.ReplaceAnimation(type, clip);
            replaced++;
        }
        return replaced;
    }

    private void ResetWLocomotionOverrides()
    {
        if (!_wArmedPresentation) return;
        _wArmedPresentation = false;
        ApplyWLocomotionOverrides();
    }
}
