using UnityEngine;

[DefaultExecutionOrder(-10000)]
internal sealed class DariusNativeModelHost : MonoBehaviour
{
    private void OnEnable()
    {
        if (DariusNativeModelAssets.IsActive(gameObject)) return;
        if (DariusNativeModelAssets.TryActivate(gameObject)) return;

        if (DariusNativeModelAssets.CanUseLegacyFallback(gameObject))
        {
            DariusLog.Warn("NATIVE-MODEL",
                "Native model unavailable; local GLB compatibility fallback is present.");
            return;
        }

        DariusSkinModelBinding binding = GetComponent<DariusSkinModelBinding>();
        DariusLog.Error("NATIVE-MODEL",
            "Native model activation failed and no GLB fallback is packaged. skin=" +
            (binding != null ? binding.variantKey : "<unknown>"));
    }
}
