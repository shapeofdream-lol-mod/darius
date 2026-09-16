using System;
using System.Reflection;
using UnityEngine;

// Thin compile-boundary adapter. The CI reference pack currently omits AssetBundleModule, so this
// isolates late binding to four cold-path Unity calls instead of leaking reflection through runtime code.
internal static class DariusUnityAssetBundleApi
{
    private static Type _bundleType;

    public static object LoadFromFile(string path)
    {
        MethodInfo method = RequireMethod(
            "LoadFromFile", BindingFlags.Public | BindingFlags.Static, new[] { typeof(string) });
        return method.Invoke(null, new object[] { path });
    }

    public static string[] GetAllAssetNames(object bundle)
    {
        if (bundle == null) return Array.Empty<string>();
        MethodInfo method = RequireMethod(
            "GetAllAssetNames", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        return method.Invoke(bundle, null) as string[] ?? Array.Empty<string>();
    }

    public static GameObject LoadGameObject(object bundle, string assetName)
    {
        return LoadAsset(bundle, assetName, typeof(GameObject)) as GameObject;
    }

    public static Mesh LoadMesh(object bundle, string assetName)
    {
        return LoadAsset(bundle, assetName, typeof(Mesh)) as Mesh;
    }

    public static void Unload(object bundle, bool unloadAllLoadedObjects)
    {
        if (bundle == null) return;
        MethodInfo method = RequireMethod(
            "Unload", BindingFlags.Public | BindingFlags.Instance, new[] { typeof(bool) });
        method.Invoke(bundle, new object[] { unloadAllLoadedObjects });
    }

    public static void Reset()
    {
        _bundleType = null;
    }

    private static UnityEngine.Object LoadAsset(object bundle, string assetName, Type assetType)
    {
        if (bundle == null || string.IsNullOrEmpty(assetName) || assetType == null) return null;
        MethodInfo method = RequireMethod(
            "LoadAsset", BindingFlags.Public | BindingFlags.Instance, new[] { typeof(string), typeof(Type) });
        return method.Invoke(bundle, new object[] { assetName, assetType }) as UnityEngine.Object;
    }

    private static MethodInfo RequireMethod(string name, BindingFlags flags, Type[] parameterTypes)
    {
        if (_bundleType == null)
            _bundleType = Type.GetType("UnityEngine.AssetBundle, UnityEngine.AssetBundleModule", false);
        if (_bundleType == null)
            throw new TypeLoadException("UnityEngine.AssetBundle was not found in UnityEngine.AssetBundleModule.");

        MethodInfo method = _bundleType.GetMethod(name, flags, null, parameterTypes, null);
        if (method == null) throw new MissingMethodException(_bundleType.FullName, name);
        return method;
    }
}
