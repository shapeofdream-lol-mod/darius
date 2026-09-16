using UnityEditor;
using UnityEngine;

internal static class DariusModelPrefabBuilder
{
    public static GameObject Build(string assetPath, string prefabPath, string variant)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (source == null)
            return null;

        GameObject instance = Object.Instantiate(source);
        instance.name = "darius_" + variant.ToLowerInvariant();

        DariusModelMeshProcessor.ProcessPrefab(instance);
        ConfigureAnimator(instance);

        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);
        AssetDatabase.Refresh();

        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    private static void ConfigureAnimator(GameObject root)
    {
        Animator animator = root.GetComponentInChildren<Animator>();
        if (animator == null)
            return;

        animator.applyRootMotion = false;
    }
}
