using System;
using UnityEngine;

public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
    private bool _wArmedPresentation;

    public void SetWArmed(bool armed)
    {
        _wArmedPresentation = armed;
        ApplyWLocomotionOverrides();
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
        DariusLog.DebugInfo("NATIVE-W", "armed=" + _wArmedPresentation +
            " idle=" + (idle != null ? idle.name : "<null>") + " run=" + (run != null ? run.name : "<null>") +
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
