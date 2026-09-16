#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

internal static class DariusModelPrefabBuilder
{
    public static GameObject Build(string assetPath, string prefabPath, string variant)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (source == null)
            throw new System.InvalidOperationException("Missing imported model: " + assetPath);

        GameObject instance = Object.Instantiate(source);
        instance.name = "darius_" + variant.ToLowerInvariant();

        DariusModelMeshProcessor.ProcessPrefab(instance);
        ConfigureAnimator(instance);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);
        AssetDatabase.Refresh();
        return prefab;
    }

    private static void ConfigureAnimator(GameObject root)
    {
        Animator animator = root.GetComponentInChildren<Animator>(true);
        if (animator == null) return;
        animator.applyRootMotion = false;
    }
}
#endif
