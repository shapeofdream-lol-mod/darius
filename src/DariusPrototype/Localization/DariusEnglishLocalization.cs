using System;
using System.Collections.Generic;

public static class DariusLanguage
{
    private static bool StartsWithAny(string language, params string[] prefixes)
    {
        if (string.IsNullOrEmpty(language)) return false;
        foreach (string prefix in prefixes)
            if (language.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static bool IsJapanese
    {
        get
        {
            try
            {
                string language = DewSave.profileMain != null ? DewSave.profileMain.language : null;
                return StartsWithAny(language, "ja", "jp", "Japanese", "日本");
            }
            catch { return false; }
        }
    }

    public static bool IsEnglish
    {
        get
        {
            try
            {
                string language = DewSave.profileMain != null ? DewSave.profileMain.language : null;
                return StartsWithAny(language, "en", "English");
            }
            catch { return false; }
        }
    }
}

public static class DariusEnglishLocalization
{
    private static readonly Dictionary<string, string> StarNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { DariusConstellationIds.DConqueror, "Conqueror" }, { DariusConstellationIds.DTriumph, "Triumph" },
        { DariusConstellationIds.DAlacrity, "Legend: Alacrity" }, { DariusConstellationIds.DLastStand, "Last Stand" },
        { DariusConstellationIds.DAxiomArcanist, "Axiom Arcanist" }, { DariusConstellationIds.DNoxianArena, "Noxian Arena" },
        { DariusConstellationIds.LSecondWind, "Second Wind" }, { DariusConstellationIds.LOvergrowth, "Overgrowth" },
        { DariusConstellationIds.LRevitalize, "Revitalize" }, { DariusConstellationIds.LConditioning, "Conditioning" },
        { DariusConstellationIds.LUnflinching, "Unflinching" },
        { DariusConstellationIds.ICripplingStrike, "Noxian Armory: Crippling Strike" },
        { DariusConstellationIds.IApprehend, "Noxian Armory: Apprehend" },
        { DariusConstellationIds.IInstantDecimate, "Decisive Swing" }, { DariusConstellationIds.IWarFervor, "Fervor of Battle" },
        { DariusConstellationIds.ILegacyCripplingStrike, "Cut Off Their Retreat" },
        { DariusConstellationIds.ILegacyApprehend, "Iron-Handed Conquest" },
        { DariusConstellationIds.ILegacyGuillotine, "Law of the Guillotine" },
        { DariusConstellationIds.DBloodRush, "Blood Rush" }, { DariusConstellationIds.DNoxianMight, "True Noxian Might" },
        { DariusConstellationIds.DDunkmaster, "Dunkmaster" }, { DariusConstellationIds.LBloodPrice, "Blood for Blood" },
        { DariusConstellationIds.FHandOfNoxus, "The Hand of Noxus" }, { DariusConstellationIds.FNimbusCloak, "Nimbus Cloak" },
        { DariusConstellationIds.FCelerity, "Celerity" }, { DariusConstellationIds.FGatheringStorm, "Gathering Storm" },
        { DariusConstellationIds.FCosmicInsight, "Cosmic Insight" }
    };

    private static readonly Dictionary<string, string> StarDescriptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { DariusConstellationIds.DConqueror, "Direct hits grant 1 Conqueror stack for 5 seconds, up to 6. Each stack grants a flat 1.5/2/2.5/3 Attack Damage." },
        { DariusConstellationIds.DTriumph, "Killing an enemy restores 6%/7%/8%/9% of missing Health. Each kill can trigger this only once." },
        { DariusConstellationIds.DAlacrity, "Every 6/5/4/3 kills grants 1 Alacrity stack. Each stack grants 4% Attack Speed, up to 5 stacks for the run." },
        { DariusConstellationIds.DLastStand, "While below 40% Health, gain a flat 9/11/13/15 Attack Damage." },
        { DariusConstellationIds.DAxiomArcanist, "Noxian Guillotine deals 10%/13%/16%/20% more damage and Noxian Might lasts 1/1.25/1.5/2 seconds longer." },
        { DariusConstellationIds.DNoxianArena, "While at least one enemy is within 10 m, gain a base 3/4/5/6 flat Attack Damage, 0.5/0.75/1/1.25 flat Armor, 8%/10%/12%/15% Attack Speed, and 3%/4%/5%/6% Move Speed. Up to 10 nearby enemies are counted; each missing enemy increases all bonuses by 10%. The total is multiplied by 1.5 near an Elite or 2 near a Boss. Boss encounters are capped at x3 total, which is reached when facing a Boss alone." },
        { DariusConstellationIds.LSecondWind, "After taking damage, gradually restore 4%/5%/6%/7% of current missing Health. Further hits refresh the recoverable amount." },
        { DariusConstellationIds.LOvergrowth, "Gain a flat 25/32/38/45 maximum Health." },
        { DariusConstellationIds.LRevitalize, "Decimate healing is increased by 12%/16%/20%/24%, plus another 6% while below 40% Health." },
        { DariusConstellationIds.LConditioning, "Gain a flat 0.5/0.75/1/1.25 Armor." },
        { DariusConstellationIds.LUnflinching, "While below 40% Health, gain 8%/10%/12%/14% Move Speed." },
        { DariusConstellationIds.ICripplingStrike, "At the start of combat, spawn one Crippling Strike Memory near Darius at this Constellation's level." },
        { DariusConstellationIds.IApprehend, "At the start of combat, spawn one Apprehend Memory near Darius at this Constellation's level." },
        { DariusConstellationIds.IInstantDecimate, "Decimate loses its 0.75-second windup and healing, immediately performs the spin, deals 20%/25%/30%/35% more damage, and treats every hit as an outer-blade hit. It applies a guaranteed Hemorrhage stack only while the Hemorrhage Identity is equipped." },
        { DariusConstellationIds.IWarFervor, "Applying 5 Hemorrhage stacks across any targets within 5/5.5/6/6.5 seconds triggers Noxian Might with 35% Attack Damage. Hemorrhage Memory milestones still improve it." },
        { DariusConstellationIds.ILegacyCripplingStrike, "Crippling Strike slows for an additional 0.25/0.35/0.45/0.55 seconds. On hit, each Hemorrhage stack on the target reduces its remaining cooldown by 0.5/0.6/0.7/0.8 seconds." },
        { DariusConstellationIds.ILegacyApprehend, "Enemies pulled by Apprehend are marked for 3 seconds. Darius's skills and Hemorrhage deal 10%/13%/16%/20% more physical damage to marked enemies." },
        { DariusConstellationIds.ILegacyGuillotine, "Noxian Guillotine deals an additional 15%/20%/25%/30% damage. This multiplier stacks multiplicatively with Axiom Arcanist." },
        { DariusConstellationIds.DBloodRush, "Each bleeding enemy grants 3%/3.5%/4%/4.5% Move Speed, counting up to 5 enemies." },
        { DariusConstellationIds.DNoxianMight, "When Noxian Might triggers, it also grants a flat 9/12/15/18 Attack Damage." },
        { DariusConstellationIds.DDunkmaster, "Killing with Noxian Guillotine grants 15%/20%/25%/30% Move Speed for 3 seconds." },
        { DariusConstellationIds.LBloodPrice, "Killing an enemy that is still bleeding restores 2.5%/3.5%/4.5%/5.5% maximum Health." },
        { DariusConstellationIds.FHandOfNoxus, "Hemorrhage lasts 1/1.5/2 seconds longer." },
        { DariusConstellationIds.FNimbusCloak, "Casting Flash or Ghost grants 15%/20%/25% Move Speed for 2 seconds." },
        { DariusConstellationIds.FCelerity, "Gain 5%/6.5%/8% Move Speed." },
        { DariusConstellationIds.FGatheringStorm, "Every 5 in-run Hero levels grants 1 Gathering Storm stack. Each stack grants a flat 2.5/3/3.5 Attack Damage." },
        { DariusConstellationIds.FCosmicInsight, "Flash and Ghost recover 20%/25%/30% faster." }
    };

    private static readonly Dictionary<string, string> EquipmentNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { DariusEquipmentStarIds.TrinityForce, "Trinity Force" }, { DariusEquipmentStarIds.BlackCleaver, "Black Cleaver" },
        { DariusEquipmentStarIds.SpearOfShojin, "Spear of Shojin" }, { DariusEquipmentStarIds.SteraksGage, "Sterak's Gage" },
        { DariusEquipmentStarIds.DeathsDance, "Death's Dance" }, { DariusEquipmentStarIds.OverlordsBloodmail, "Overlord's Bloodmail" },
        { DariusEquipmentStarIds.SunderedSky, "Sundered Sky" }, { DariusEquipmentStarIds.Stridebreaker, "Stridebreaker" },
        { DariusEquipmentStarIds.DeadMansPlate, "Dead Man's Plate" }, { DariusEquipmentStarIds.YoumuusGhostblade, "Youmuu's Ghostblade" },
        { DariusEquipmentStarIds.Awoo, "AWOO!!!" }
    };

    private static readonly Dictionary<string, string> EquipmentDescriptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { DariusEquipmentStarIds.TrinityForce, "Casting Q/W/E/R grants Spellblade. Your next basic attack within 4 seconds deals 130%/150%/170%/195% Attack Damage as bonus physical damage. Spellblade has a 1.5-second independent cooldown." },
        { DariusEquipmentStarIds.BlackCleaver, "Physical damage from basic attacks, Q, and Hemorrhage applies 1 Carve stack for 6 seconds, up to 5. Each stack reduces the target's Armor by 5%/5.5%/6%/6.5%. Stacks are tracked per enemy." },
        { DariusEquipmentStarIds.SpearOfShojin, "Q or W skill damage grants 1 Dragonforce stack for 6 seconds, up to 4. Each stack increases subsequent Q and W damage by 5%/6%/7%/8.5%. One cast can grant only one stack." },
        { DariusEquipmentStarIds.SteraksGage, "The first time Health falls below 35%, gain a 5-second Lifeline barrier equal to 25%/30%/35%/40% maximum Health. Cooldown: 30/28/26/24 seconds." },
        { DariusEquipmentStarIds.DeathsDance, "Delay 5%/10%/15%/20% of incoming damage, taking it evenly as pure damage over 3 seconds. Killing an enemy clears 10%/20%/30%/40% of outstanding delayed damage and grants no healing." },
        { DariusEquipmentStarIds.OverlordsBloodmail, "Convert maximum Health into flat Attack Damage at 0.010/0.0125/0.015/0.018 per Health, capped at 100 Attack Damage. Missing Health grants up to another 12/15/18/22 flat Attack Damage." },
        { DariusEquipmentStarIds.SunderedSky, "Each enemy has an independent charge. Your next charged basic attack deals 100%/115%/130%/150% Attack Damage as bonus physical damage and restores 5%/6%/7%/8% missing Health." },
        { DariusEquipmentStarIds.Stridebreaker, "Crippling Strike also releases a 3 m shockwave, dealing 60%/70%/80%/95% Attack Damage as physical damage and slowing by 35%/40%/45%/50% for 1.5 seconds." },
        { DariusEquipmentStarIds.DeadMansPlate, "Moving builds up to 100 Momentum. Your next basic attack consumes it to deal up to 50%/60%/70%/85% Attack Damage as bonus physical damage. A fully charged hit also heavily slows." },
        { DariusEquipmentStarIds.YoumuusGhostblade, "After 2 seconds out of combat, gain 15%/18%/21%/25% Move Speed. Dealing or taking damage removes the bonus until you leave combat again." },
        { DariusEquipmentStarIds.Awoo, "At the start of each room, Noxian Guillotine uses a 25% Attack Damage ratio and half cooldown. Each cast starts Animals. An R kill permanently adds +5% ratio for the room and +0.1 playback speed; a miss reduces playback speed by 0.1. Both outcomes restart the track. All changes reset between rooms." }
    };

    public static bool TryStarName(string id, out string value) { return StarNames.TryGetValue(id, out value); }
    public static bool TryStarDescription(string id, out string value) { return StarDescriptions.TryGetValue(id, out value); }
    public static bool TryEquipmentName(string id, out string value) { return EquipmentNames.TryGetValue(id, out value); }
    public static bool TryEquipmentDescription(string id, out string value) { return EquipmentDescriptions.TryGetValue(id, out value); }
}
