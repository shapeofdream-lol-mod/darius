using UnityEngine;

public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
    public Transform GetAnchor(string name)
    {
        Transform result;
        return !string.IsNullOrEmpty(name) && _anchors.TryGetValue(name, out result) ? result : null;
    }

    private void StopSequence()
    {
        if (_sequence != null)
        {
            try { StopCoroutine(_sequence); } catch { }
            _sequence = null;
        }
        _wSwingActive = false;
        if (IsGodKingSkin) SetGodKingWolfVisible(false);
        ResetActionFacing();
        EndAnimatorAction();
    }

    private void OnDisable()
    {
        ResetWLocomotionOverrides();
        StopSequence();
    }

    private void OnDestroy()
    {
        EntityAnimation animation = _entityAnimation;
        ResetWLocomotionOverrides();
        StopSequence();
        DariusNativeAnimationReplacementLedger.Forget(animation);
    }
}
