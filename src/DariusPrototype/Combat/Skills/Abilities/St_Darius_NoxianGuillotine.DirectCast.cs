using System;

public sealed partial class St_Darius_NoxianGuillotine : SkillTrigger
{
    // Awoo is a direct R mechanic rewrite, so it owns the R recharge configuration rather
    // than multiplying a generic cooldown stat. The value is reapplied on Prepare and
    // star equip/unequip so native charge recovery and the tooltip stay on one source value.
    public void ApplyAwooCooldownOverride(bool active)
    {
        float cooldown = active ? 12.0f : 24.0f;
        try
        {
            if (configs != null)
            {
                for (int i = 0; i < configs.Length; i++)
                {
                    if (configs[i] == null) continue;
                    DariusTriggerConfigRuntimeEditor.SetCooldownTime(configs[i], cooldown);
                }
            }
            DariusLog.DebugInfo("STAR-AWOO", "Applied Guillotine recharge override active=" + active + " cooldown=" + cooldown.ToString("0.##"));
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAR-AWOO", e, "Failed applying Guillotine cooldown override active=" + active);
        }
    }

    public static void ApplyAwooCooldownOverrideToHero(Hero hero, bool active)
    {
        if (hero == null) return;
        try
        {
            EntityAbility ability = hero.Ability;
            if (ability != null && ability.abilities != null)
            {
                foreach (var pair in ability.abilities)
                {
                    St_Darius_NoxianGuillotine trigger = pair.Value as St_Darius_NoxianGuillotine;
                    if (trigger == null) continue;
                    trigger.ApplyAwooCooldownOverride(active);
                }
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAR-AWOO", e, "Could not locate live Guillotine trigger for cooldown override");
        }
    }

}