using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DariusConstellationLocalization
{
    private static readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { DariusConstellationIds.DConqueror, "征服者" },
        { DariusConstellationIds.DTriumph, "凯旋" },
        { DariusConstellationIds.DAlacrity, "传说：欢欣" },
        { DariusConstellationIds.DLastStand, "坚毅不倒" },
        { DariusConstellationIds.DAxiomArcanist, "公理秘术" },
        { DariusConstellationIds.DNoxianArena, "诺克萨斯竞技场" },
        { DariusConstellationIds.LSecondWind, "复苏之风" },
        { DariusConstellationIds.LOvergrowth, "过度生长" },
        { DariusConstellationIds.LRevitalize, "复苏" },
        { DariusConstellationIds.LConditioning, "调节" },
        { DariusConstellationIds.LUnflinching, "坚定" },
        { DariusConstellationIds.ICripplingStrike, "诺克萨斯武库：致残打击" },
        { DariusConstellationIds.IApprehend, "诺克萨斯武库：无情铁手" },
        { DariusConstellationIds.IInstantDecimate, "旋斧即决" },
        { DariusConstellationIds.IWarFervor, "战争热诚" },
        { DariusConstellationIds.ILegacyCripplingStrike, "斩断退路" },
        { DariusConstellationIds.ILegacyApprehend, "铁腕征服" },
        { DariusConstellationIds.ILegacyGuillotine, "断头台的律法" },
        { DariusConstellationIds.DBloodRush, "血路疾行" },
        { DariusConstellationIds.DNoxianMight, "真正的诺克萨斯之力" },
        { DariusConstellationIds.DDunkmaster, "扣篮王" },
        { DariusConstellationIds.LBloodPrice, "血债血偿" },
        { DariusConstellationIds.FHandOfNoxus, "诺克萨斯之手" },
        { DariusConstellationIds.FNimbusCloak, "灵光披风" },
        { DariusConstellationIds.FCelerity, "迅捷" },
        { DariusConstellationIds.FGatheringStorm, "风暴聚集" },
        { DariusConstellationIds.FCosmicInsight, "星界洞悉" }
    };

    private static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { DariusConstellationIds.DConqueror, "对敌人造成直接命中会获得1层征服者，持续5秒，最多6层。每层固定提高1.5/2/2.5/3攻击力。" },
        { DariusConstellationIds.DTriumph, "击杀敌人时回复已损失生命值的6%/7%/8%/9%。同一次击杀只会结算一次。" },
        { DariusConstellationIds.DAlacrity, "每6/5/4/3次击杀获得1层欢欣，每层提高4%攻击速度，最多5层，本局持续。" },
        { DariusConstellationIds.DLastStand, "生命值低于40%时，固定提高9/11/13/15攻击力。" },
        { DariusConstellationIds.DAxiomArcanist, "诺克萨斯断头台伤害提高10%/13%/16%/20%，并使诺克萨斯之力持续时间延长1/1.25/1.5/2秒。" },
        { DariusConstellationIds.DNoxianArena, "10米内至少存在1个敌人时进入【诺克萨斯竞技场】：基础固定获得3/4/5/6攻击力与0.5/0.75/1/1.25护甲，并获得8%/10%/12%/15%攻击速度和3%/4%/5%/6%移动速度。附近敌人最多按10个计算，每少1个敌人，全部加成提高10%；存在精英时总效果×1.5，存在Boss时总效果×2（Boss优先），Boss场景最终倍率封顶×3，单独面对Boss时即为×3。" },
        { DariusConstellationIds.LSecondWind, "受到伤害后逐步回复当前已损失生命值的4%/5%/6%/7%，新的受击会刷新可回复量。" },
        { DariusConstellationIds.LOvergrowth, "固定提高25/32/38/45最大生命值。" },
        { DariusConstellationIds.LRevitalize, "大杀四方的治疗量提高12%/16%/20%/24%；生命值低于40%时额外提高6%。" },
        { DariusConstellationIds.LConditioning, "固定提高0.5/0.75/1/1.25护甲。" },
        { DariusConstellationIds.LUnflinching, "生命值低于40%时，移动速度提高8%/10%/12%/14%。" },
        { DariusConstellationIds.ICripplingStrike, "诺克萨斯从不教人给敌人第二次逃跑的机会。开战时，德莱厄斯附近出现1个【致残打击】记忆；记忆等级等于本星座等级。" },
        { DariusConstellationIds.IApprehend, "真正的强者不会等猎物自己走近。开战时，德莱厄斯附近出现1个【无情铁手】记忆；记忆等级等于本星座等级。" },
        { DariusConstellationIds.IInstantDecimate, "斧刃不必蓄势，杀意已经足够。Q【大杀四方】取消0.75秒前摇、蓄力动作与治疗，直接播放旋转攻击并立即结算；伤害提高20%/25%/30%/35%，所有命中均按外圈伤害处理；仅在已拥有【出血】身份时必定叠加1层【出血】。" },
        { DariusConstellationIds.IWarFervor, "战争不在乎鲜血来自谁。只要在5/5.5/6/6.5秒内跨目标累计施加5次【出血】，即可触发【诺克萨斯之力】，获得35%攻击力，并继续享受【出血】记忆每5级带来的成长。" },
        { DariusConstellationIds.ILegacyCripplingStrike, "逃跑只是把死亡拖得更久。W【致残打击】的减速额外延长0.25/0.35/0.45/0.55秒；命中时，目标每有1层【出血】，W剩余冷却额外减少0.5/0.6/0.7/0.8秒。" },
        { DariusConstellationIds.ILegacyApprehend, "被铁腕拖回来的敌人，连铠甲也失去了意义。E【无情铁手】拉中的敌人被标记3秒；期间德莱厄斯的技能与【出血】造成的物理伤害提高10%/13%/16%/20%。" },
        { DariusConstellationIds.ILegacyGuillotine, "断头台只承认力量，不承认侥幸。R【诺克萨斯断头台】伤害额外提高15%/20%/25%/30%，并与【公理秘术】乘算。" },
        { DariusConstellationIds.DBloodRush, "鲜血会替你指出逃兵的方向。每个正在流血的敌人使移动速度提高3%/3.5%/4%/4.5%，最多计算5个目标。" },
        { DariusConstellationIds.DNoxianMight, "君王以世袭的名义让你跪下，而诺克萨斯让你站起来。触发【诺克萨斯之力】时，额外固定获得9/12/15/18攻击力。" },
        { DariusConstellationIds.DDunkmaster, "高高跃起，然后让所有人记住这一斧。用【诺克萨斯断头台】完成击杀后，移动速度提高15%/20%/25%/30%，持续3秒。" },
        { DariusConstellationIds.LBloodPrice, "流下的血总要有人偿还。击杀仍带有【出血】的敌人时，回复最大生命值的2.5%/3.5%/4.5%/5.5%。" },
        { DariusConstellationIds.FHandOfNoxus, "伤口不会轻易闭合，德莱厄斯也不会停止追猎。【出血】持续时间延长1/1.5/2秒。" },
        { DariusConstellationIds.FNimbusCloak, "施放闪现或疾跑后，移动速度提高15%/20%/25%，持续2秒。" },
        { DariusConstellationIds.FCelerity, "移动速度提高5%/6.5%/8%。" },
        { DariusConstellationIds.FGatheringStorm, "每达到5个局内英雄等级获得一层风暴聚集；每层固定提高2.5/3/3.5攻击力。" },
        { DariusConstellationIds.FCosmicInsight, "闪现与疾跑的实际恢复时间缩短20%/25%/30%。" }
    };

    public static bool TryName(string key, out string value)
    {
        value = null;
        string id = Normalize(key);
        if (id != null) return DariusLanguage.IsEnglish
            ? DariusEnglishLocalization.TryStarName(id, out value)
            : Names.TryGetValue(id, out value);
        return DariusEquipmentConstellationLocalization.TryName(key, out value);
    }

    public static bool TryDescription(string key, out string value)
    {
        value = null;
        string id = Normalize(key);
        if (id == null) return DariusEquipmentConstellationLocalization.TryDescription(key, out value);

        // The stock constellation detail widget asks for both a description and a separate lore row.
        // Darius used the same gameplay description for both, producing the duplicated grey line in
        // the lower panel. Keep the gameplay description in its native row and intentionally blank lore.
        if (IsLoreKey(key))
        {
            value = string.Empty;
            return true;
        }
        return DariusLanguage.IsEnglish
            ? DariusEnglishLocalization.TryStarDescription(id, out value)
            : Descriptions.TryGetValue(id, out value);
    }

    private static bool IsLoreKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return key.EndsWith(".lore", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith("_lore", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDariusStarKey(string key) => Normalize(key) != null || DariusEquipmentConstellationLocalization.IsEquipmentKey(key);

    private static string Normalize(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        foreach (string id in Names.Keys)
            if (key.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf(id.Replace("Se_", string.Empty), StringComparison.OrdinalIgnoreCase) >= 0)
                return id;
        return null;
    }
}