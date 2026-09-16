using UnityEngine;

public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
    private void OnDisable()
    {
        ResetWLocomotionOverrides();
        StopSequence();
    }

    private void OnDestroy()
    {
        ResetWLocomotionOverrides();
        StopSequence();
    }
}
