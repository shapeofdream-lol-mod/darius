using UnityEngine;

internal sealed class DariusNativeOverlayMeshLifetime : MonoBehaviour
{
    public Mesh mesh;

    private void OnDestroy()
    {
        if (mesh == null) return;
        try { Object.Destroy(mesh); } catch { }
        mesh = null;
    }
}
