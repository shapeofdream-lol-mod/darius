// Pass 2 equipment constellation layer.
// Equipment stars import only the recognizable item mechanism; they never grant the item's base
// stat line. All item artwork is Riot's original Data Dragon icon. Flexible is reserved for stars
// that actually rewrite a Darius skill mechanic (Stridebreaker -> W, Awoo -> R).
public static class DariusEquipmentStarIds
{
    public const string TrinityForce = "Se_Star_Darius_D_ItemTrinityForce";
    public const string BlackCleaver = "Se_Star_Darius_D_ItemBlackCleaver";
    public const string SpearOfShojin = "Se_Star_Darius_D_ItemSpearOfShojin";

    public const string SteraksGage = "Se_Star_Darius_L_ItemSteraksGage";
    public const string DeathsDance = "Se_Star_Darius_L_ItemDeathsDance";
    public const string OverlordsBloodmail = "Se_Star_Darius_L_ItemOverlordsBloodmail";
    public const string DeadMansPlate = "Se_Star_Darius_I_ItemDeadMansPlate";

    public const string SunderedSky = "Se_Star_Darius_I_ItemSunderedSky";
    public const string YoumuusGhostblade = "Se_Star_Darius_I_ItemYoumuusGhostblade";

    // Flexible: W becomes an AoE breaking strike instead of merely receiving a standalone proc.
    public const string Stridebreaker = "Se_Star_Darius_F_ItemStridebreaker";
    // Flexible: rewrites R's room-local damage/cooldown/reward loop.
    public const string Awoo = "Se_Star_Darius_F_Awoo";
}

public sealed class Se_Star_Darius_D_ItemTrinityForce : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.TrinityForce; }
public sealed class Se_Star_Darius_D_ItemBlackCleaver : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.BlackCleaver; }
public sealed class Se_Star_Darius_D_ItemSpearOfShojin : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.SpearOfShojin; }
public sealed class Se_Star_Darius_L_ItemSteraksGage : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.SteraksGage; }
public sealed class Se_Star_Darius_L_ItemDeathsDance : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.DeathsDance; }
public sealed class Se_Star_Darius_L_ItemOverlordsBloodmail : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.OverlordsBloodmail; }
public sealed class Se_Star_Darius_I_ItemSunderedSky : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.SunderedSky; }
public sealed class Se_Star_Darius_F_ItemStridebreaker : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.Stridebreaker; }
public sealed class Se_Star_Darius_I_ItemDeadMansPlate : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.DeadMansPlate; }
public sealed class Se_Star_Darius_I_ItemYoumuusGhostblade : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.YoumuusGhostblade; }
public sealed class Se_Star_Darius_F_Awoo : DariusStarEffect
{
    protected override string DariusStarKey => DariusEquipmentStarIds.Awoo;
    protected override int FallbackMaxLevel => 1;
}

public static class DariusEquipmentConstellationLocalization
{
    private static readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { DariusEquipmentStarIds.TrinityForce, "三相之力" },
        { DariusEquipmentStarIds.BlackCleaver, "黑色切割者" },
        { DariusEquipmentStarIds.SpearOfShojin, "朔极之矛" },
        { DariusEquipmentStarIds.SteraksGage, "斯特拉克的挑战护手" },
        { DariusEquipmentStarIds.DeathsDance, "死亡之舞" },
        { DariusEquipmentStarIds.OverlordsBloodmail, "霸王血铠" },
        { DariusEquipmentStarIds.SunderedSky, "焚天" },
        { DariusEquipmentStarIds.Stridebreaker, "挺进破坏者" },
        { DariusEquipmentStarIds.DeadMansPlate, "亡者的板甲" },
        { DariusEquipmentStarIds.YoumuusGhostblade, "幽梦之灵" },
        { DariusEquipmentStarIds.Awoo, "嗷呜！！！" }
    };

    private static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { DariusEquipmentStarIds.TrinityForce, "施放Q/W/E/R后获得【咒刃】，4秒内下一次普攻额外造成130%/150%/170%/195%攻击力的物理伤害。咒刃有1.5秒独立冷却。" },
        { DariusEquipmentStarIds.BlackCleaver, "普攻、Q与【出血】造成物理伤害时，对该敌人叠加1层【切割】，持续6秒，最多5层；每层使其护甲降低5%/5.5%/6%/6.5%。不同敌人独立计算。" },
        { DariusEquipmentStarIds.SpearOfShojin, "Q或W造成技能伤害时获得1层【龙之力】，持续6秒，最多4层；每层使之后的Q与W伤害提高5%/6%/7%/8.5%。同一次技能命中多个敌人只叠1层。" },
        { DariusEquipmentStarIds.SteraksGage, "生命值首次降至35%以下时获得持续5秒的【救主灵刃】：以临时生命屏障承受相当于最大生命值25%/30%/35%/40%的伤害。冷却30/28/26/24秒。" },
        { DariusEquipmentStarIds.DeathsDance, "受到的5%/10%/15%/20%伤害不会立即结算，而是在3秒内以纯粹伤害均匀承受。击杀敌人会清除尚未结算延迟伤害的10%/20%/30%/40%，不额外回复生命值。" },
        { DariusEquipmentStarIds.OverlordsBloodmail, "将最大生命值转化为固定攻击力；每点最大生命按0.010/0.0125/0.015/0.018转化，来自生命值的部分最多提供100攻击力。生命值越低时额外获得至多12/15/18/22攻击力。" },
        { DariusEquipmentStarIds.SunderedSky, "每个敌人拥有独立的【焚天】充能。充能就绪时，对该敌人的下一次普攻额外造成100%/115%/130%/150%攻击力物理伤害，并回复5%/6%/7%/8%已损失生命值。" },
        { DariusEquipmentStarIds.Stridebreaker, "W【致残打击】命中时会额外释放一次【破阵冲击】：对目标周围3米所有敌人造成60%/70%/80%/95%攻击力物理伤害并减速35%/40%/45%/50%，持续1.5秒。" },
        { DariusEquipmentStarIds.DeadMansPlate, "移动会积累【气势】，最多100层。下一次普攻消耗全部气势并按层数造成至多50%/60%/70%/85%攻击力的额外物理伤害；满层攻击额外造成强减速。" },
        { DariusEquipmentStarIds.YoumuusGhostblade, "脱离战斗2秒后进入【幽魂步伐】，移动速度提高15%/18%/21%/25%；造成或受到伤害时退出，重新脱战后再次获得。" },
        { DariusEquipmentStarIds.Awoo, "每个房间开始时，R【诺克萨斯断头台】改为25%攻击力加成且冷却时间减半。每次使用R开始播放Animals；R完成击杀时本房间R攻击力加成永久+5%并将播放倍速+0.1，未完成击杀则倍速-0.1；两种结果都会从头重新播放。房间结束后全部重置。" }
    };

    public static bool IsEquipmentKey(string key)
    {
        return Normalize(key) != null;
    }

    public static bool TryName(string key, out string value)
    {
        value = null;
        string id = Normalize(key);
        return id != null && (DariusLanguage.IsEnglish
            ? DariusEnglishLocalization.TryEquipmentName(id, out value)
            : Names.TryGetValue(id, out value));
    }

    public static bool TryDescription(string key, out string value)
    {
        value = null;
        string id = Normalize(key);
        if (id == null) return false;
        if (!string.IsNullOrEmpty(key) && (key.EndsWith(".lore", StringComparison.OrdinalIgnoreCase) || key.EndsWith("_lore", StringComparison.OrdinalIgnoreCase)))
        {
            value = string.Empty;
            return true;
        }
        return DariusLanguage.IsEnglish
            ? DariusEnglishLocalization.TryEquipmentDescription(id, out value)
            : Descriptions.TryGetValue(id, out value);
    }

    private static string Normalize(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        foreach (string id in Names.Keys)
        {
            if (key.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf(id.Replace("Se_", string.Empty), StringComparison.OrdinalIgnoreCase) >= 0)
                return id;
        }
        return null;
    }
}
