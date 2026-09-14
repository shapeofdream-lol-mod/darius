public sealed class Gem_Darius_Hemorrhage : Gem
{
    public override void OnEquipGem(Hero newOwner)
    {
        base.OnEquipGem(newOwner);
        if (newOwner == null)
        {
            DariusLog.Warn("HEM-GEM", "OnEquipGem received null owner.");
            return;
        }

        try
        {
            DariusHemorrhageRuntime state = newOwner.GetComponent<DariusHemorrhageRuntime>();
            if (state == null)
            {
                state = newOwner.gameObject.AddComponent<DariusHemorrhageRuntime>();
                DariusLog.Info("HEM-GEM", "Created runtime on owner=" + DariusLog.EntityLabel(newOwner));
            }
            state.AddLegacyEssenceSource(newOwner);
            DariusLog.Info("HEM-GEM", "Equipped on owner=" + DariusLog.EntityLabel(newOwner) + " sources=" + state.sourceCount);
        }
        catch (Exception e)
        {
            DariusLog.Exception("HEM-GEM", e, "OnEquipGem failed");
        }
    }

    public override void OnUnequipGem(Hero oldOwner)
    {
        base.OnUnequipGem(oldOwner);
        if (oldOwner == null)
        {
            DariusLog.Warn("HEM-GEM", "OnUnequipGem received null owner.");
            return;
        }

        try
        {
            DariusHemorrhageRuntime state = oldOwner.GetComponent<DariusHemorrhageRuntime>();
            if (state != null)
            {
                state.RemoveLegacyEssenceSource();
                DariusLog.Info("HEM-GEM", "Unequipped from owner=" + DariusLog.EntityLabel(oldOwner) + " sources=" + state.sourceCount);
            }
            else
            {
                DariusLog.Warn("HEM-GEM", "Unequip could not find DariusHemorrhageRuntime on owner=" + oldOwner.name);
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("HEM-GEM", e, "OnUnequipGem failed");
        }
    }
}
