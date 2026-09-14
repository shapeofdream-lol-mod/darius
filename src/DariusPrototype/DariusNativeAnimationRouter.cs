using UnityEngine;

// Thin routing layer. EntityAnimation/native adapter remains the authority;
// legacy animation is only reached through existing fallback paths.
public static class DariusNativeAnimationRouter
{
    public static bool PlayAbility(Hero hero, string clip, bool instant = false)
    {
        return DariusNativeAnimationAdapter.PlayAbility(hero, clip, instant);
    }

    public static bool StopAbility(Hero hero)
    {
        return DariusNativeAnimationAdapter.StopAbility(hero);
    }
}
