using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
internal static class DariusNativeAnimationReplacementObserverPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return typeof(EntityAnimation).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => (m.Name == "ReplaceAnimation" || m.Name == "ReplaceAnimationLocal") &&
                        m.GetParameters().Length == 2 &&
                        m.GetParameters()[0].ParameterType == typeof(EntityAnimation.ReplaceableAnimationType));
    }

    [HarmonyPostfix]
    private static void Postfix(EntityAnimation __instance, object[] __args)
    {
        if (__instance == null || __args == null || __args.Length != 2) return;
        Hero_Darius hero = DariusNativeAnimationMode.FindHero(__instance);
        if (hero == null || hero.NativeModelBridge == null) return;
        if (!(__args[0] is EntityAnimation.ReplaceableAnimationType)) return;
        DariusNativeAnimationReplacementLedger.Observe(
            __instance, (EntityAnimation.ReplaceableAnimationType)__args[0], __args[1]);
    }
}

internal static class DariusNativeAnimationReplacementLedger
{
    private sealed class AnimationState
    {
        public readonly Dictionary<EntityAnimation.ReplaceableAnimationType, object> desired =
            new Dictionary<EntityAnimation.ReplaceableAnimationType, object>();
        public readonly Dictionary<EntityAnimation.ReplaceableAnimationType, AnimationClip> active =
            new Dictionary<EntityAnimation.ReplaceableAnimationType, AnimationClip>();
    }

    private static readonly Dictionary<EntityAnimation, AnimationState> States =
        new Dictionary<EntityAnimation, AnimationState>();
    private static int _internalWriteDepth;

    public static void Observe(
        EntityAnimation animation,
        EntityAnimation.ReplaceableAnimationType type,
        object replacementData)
    {
        if (animation == null || _internalWriteDepth != 0) return;
        AnimationState state = GetState(animation);
        state.desired[type] = replacementData;
        AnimationClip active;
        if (state.active.TryGetValue(type, out active)) ApplyLocal(animation, type, active);
    }

    public static void PushOverride(
        EntityAnimation animation,
        EntityAnimation.ReplaceableAnimationType type,
        AnimationClip replacement,
        AnimationClip fallback)
    {
        if (animation == null || replacement == null)
            throw new InvalidOperationException("Native animation override requires animation and replacement clip.");
        AnimationState state = GetState(animation);
        if (!state.desired.ContainsKey(type)) state.desired[type] = fallback;
        state.active[type] = replacement;
        try
        {
            ApplyLocal(animation, type, replacement);
        }
        catch
        {
            state.active.Remove(type);
            throw;
        }
    }

    public static void PopOverride(
        EntityAnimation animation,
        EntityAnimation.ReplaceableAnimationType type)
    {
        AnimationState state;
        AnimationClip active;
        if (animation == null || !States.TryGetValue(animation, out state) ||
            !state.active.TryGetValue(type, out active)) return;
        state.active.Remove(type);
        object desired;
        if (!state.desired.TryGetValue(type, out desired)) return;
        try
        {
            ApplyDesiredLocal(animation, type, desired);
        }
        catch
        {
            state.active[type] = active;
            try { ApplyLocal(animation, type, active); } catch { }
            throw;
        }
    }

    public static void ForceRelease(EntityAnimation animation)
    {
        AnimationState state;
        if (animation == null || !States.TryGetValue(animation, out state)) return;
        EntityAnimation.ReplaceableAnimationType[] active = state.active.Keys.ToArray();
        for (int i = 0; i < active.Length; i++)
        {
            EntityAnimation.ReplaceableAnimationType type = active[i];
            state.active.Remove(type);
            object desired;
            if (!state.desired.TryGetValue(type, out desired)) continue;
            try { ApplyDesiredLocal(animation, type, desired); } catch { }
        }
    }

    public static void Forget(EntityAnimation animation)
    {
        if (animation == null) return;
        ForceRelease(animation);
        States.Remove(animation);
    }

    public static void Clear()
    {
        EntityAnimation[] animations = States.Keys.ToArray();
        for (int i = 0; i < animations.Length; i++) ForceRelease(animations[i]);
        States.Clear();
    }

    private static AnimationState GetState(EntityAnimation animation)
    {
        AnimationState state;
        if (!States.TryGetValue(animation, out state))
        {
            state = new AnimationState();
            States.Add(animation, state);
        }
        return state;
    }

    private static void ApplyLocal(
        EntityAnimation animation,
        EntityAnimation.ReplaceableAnimationType type,
        AnimationClip clip)
    {
        _internalWriteDepth++;
        try { animation.ReplaceAnimationLocal(type, clip); }
        finally { _internalWriteDepth--; }
    }

    private static void ApplyDesiredLocal(
        EntityAnimation animation,
        EntityAnimation.ReplaceableAnimationType type,
        object desired)
    {
        AnimationClip clip = desired as AnimationClip;
        if (clip != null || desired == null)
        {
            ApplyLocal(animation, type, clip);
            return;
        }

        MethodInfo method = typeof(EntityAnimation).GetMethod(
            "ReplaceAnimationLocal", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, new[] { typeof(EntityAnimation.ReplaceableAnimationType), desired.GetType() }, null);
        if (method == null)
            throw new MissingMethodException(typeof(EntityAnimation).FullName, "ReplaceAnimationLocal");
        _internalWriteDepth++;
        try { method.Invoke(animation, new[] { (object)type, desired }); }
        finally { _internalWriteDepth--; }
    }
}
