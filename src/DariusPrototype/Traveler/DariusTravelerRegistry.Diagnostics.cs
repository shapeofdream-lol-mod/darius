using System;

public static partial class DariusTravelerRegistry
{
    private static void ValidateNativeCosmeticIconContract(string reason)
    {
        try
        {
            object heroIcon = HeroPrefab != null ? ReadMemberValue(HeroPrefab, "icon") : null;
            object mainColor = HeroPrefab != null ? ReadMemberValue(HeroPrefab, "mainColor") : null;
            int ready = 0;
            for (int i = 0; i < SkinSpecs.Length; i++)
            {
                Skin skin;
                object preview = null;
                if (SkinsByName.TryGetValue(SkinSpecs[i].name, out skin) && skin != null)
                    preview = ReadMemberValue(skin, "previewImage");
                if (preview != null) ready++;
                else DariusLog.Error("SKIN-ICON", "Native previewImage missing skin=" + SkinSpecs[i].name + " reason=" + reason);
            }
            DariusLog.Info("HERO-ICON-CONTRACT", "reason=" + reason + " hero.icon=" + (heroIcon != null) +
                " mainColor=" + (mainColor != null) + " skin.previewImage=" + ready + "/" + SkinSpecs.Length);
        }
        catch (Exception e)
        {
            DariusLog.Exception("HERO-ICON-CONTRACT", e, "Native cosmetic icon validation failed reason=" + reason);
        }
    }
}
