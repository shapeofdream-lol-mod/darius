using UnityEngine;

public sealed class Se_Darius_Hemorrhage : StackedStatusEffect
{
    public override string GetCustomTooltipName()
    {
        return "Hemorrhage";
    }

    public override string GetCustomTooltipDescription()
    {
        return "Darius' attacks and damaging abilities apply Hemorrhage. Reaching the current stack cap grants Darius Noxian Might.";
    }
}
