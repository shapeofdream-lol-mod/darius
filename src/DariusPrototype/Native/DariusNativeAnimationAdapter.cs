// Thin adapter between Darius presentation hooks and Shape of Dreams animation runtime.
// This intentionally does not own Animator state. EntityAnimation remains the authority.
internal static class DariusNativeAnimationAdapter
{
    public static bool PlayAbility(Hero hero, string animationName, bool loop = false)
    {
        if (hero == null || string.IsNullOrEmpty(animationName)) return false;
        EntityAnimation animation = hero.GetComponent<EntityAnimation>();
        if (animation == null) return false;

        try
        {
            animation.PlayAbilityAnimation(animationName, loop);
            return true;
        }
        catch (Exception e)
        {
            DariusLog.Exception("NATIVE-ANIM", e, "EntityAnimation.PlayAbilityAnimation failed: " + animationName);
            return false;
        }
    }

    public static bool StopAbility(Hero hero)
    {
        if (hero == null) return false;
        EntityAnimation animation = hero.GetComponent<EntityAnimation>();
        if (animation == null) return false;

        try
        {
            animation.StopAbilityAnimation();
            return true;
        }
        catch (Exception e)
        {
            DariusLog.Exception("NATIVE-ANIM", e, "EntityAnimation.StopAbilityAnimation failed");
            return false;
        }
    }

    public static bool Replace(Hero hero, string key, UnityEngine.AnimationClip clip)
    {
        if (hero == null || clip == null || string.IsNullOrEmpty(key)) return false;
        EntityAnimation animation = hero.GetComponent<EntityAnimation>();
        if (animation == null) return false;

        try
        {
            animation.ReplaceAnimation(key, clip);
            return true;
        }
        catch (Exception e)
        {
            DariusLog.Exception("NATIVE-ANIM", e, "EntityAnimation.ReplaceAnimation failed: " + key);
            return false;
        }
    }
}
