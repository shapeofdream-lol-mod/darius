using HarmonyLib;

namespace DariusPrototype
{
    /// <summary>
    /// Keeps native animation routing conservative while the EntityAnimation migration is in progress.
    /// A native bridge that is not fully initialized must not become the animation authority.
    /// </summary>
    internal static class DariusNativeAnimationValidationPatches
    {
        [HarmonyPatch(typeof(DariusNativeModelBridge), nameof(DariusNativeModelBridge.BindHero))]
        private static class BindHeroValidationPatch
        {
            private static void Postfix(DariusNativeModelBridge __instance)
            {
                if (__instance == null)
                    return;

                if (!__instance.IsReady)
                {
                    DariusLog.Warn("NATIVE-ANIM", "Native animation bridge rejected after BindHero validation.");
                }
            }
        }
    }
}
