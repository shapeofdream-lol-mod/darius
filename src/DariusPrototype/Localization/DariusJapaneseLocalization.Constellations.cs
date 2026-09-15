using System;
using System.Collections.Generic;

public static partial class DariusJapaneseLocalization
{
    private static bool TryGetConstellationText(string key, bool description, out string value)
    {
        value = null;
        if (key.IndexOf("Star_Darius_D_Conqueror", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "直撃で5秒間の征服者スタックを1つ獲得し、最大6。各スタックで攻撃力が1.5/2/2.5/3増加する。" : "征服者";
            return true;
        }
        if (key.IndexOf("Star_Darius_D_Triumph", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "敵を倒すと減少体力の6%/7%/8%/9%を回復する。各撃破につき1回のみ発動する。" : "凱旋";
            return true;
        }
        if (key.IndexOf("Star_Darius_D_Alacrity", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "6/5/4/3体倒すごとに迅速スタックを1つ獲得。各スタックで攻撃速度+4%、ラン中最大5スタック。" : "レジェンド：迅速";
            return true;
        }
        if (key.IndexOf("Star_Darius_D_LastStand", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "体力40%未満の間、攻撃力が9/11/13/15増加する。" : "背水の陣";
            return true;
        }
        if (key.IndexOf("Star_Darius_D_AxiomArcanist", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "ノクサスギロチンのダメージが10%/13%/16%/20%増加し、ノクサスの力の持続時間が1/1.25/1.5/2秒延長される。" : "アクシオム アルカニスト";
            return true;
        }
        if (key.IndexOf("Star_Darius_D_NoxianArena", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "10m以内に敵がいる間、攻撃力3/4/5/6、物理防御0.5/0.75/1/1.25、攻撃速度8%/10%/12%/15%、移動速度3%/4%/5%/6%を得る。最大10体を数え、敵が少ないほど各ボーナスが10%ずつ増幅。エリート付近で1.5倍、ボス付近で2倍、ボス戦では最大3倍。" : "ノクサスの闘技場";
            return true;
        }
        if (key.IndexOf("Star_Darius_L_SecondWind", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "ダメージを受けた後、現在の減少体力の4%/5%/6%/7%を徐々に回復する。追加被弾で回復可能量を更新する。" : "息継ぎ";
            return true;
        }
        if (key.IndexOf("Star_Darius_L_Overgrowth", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "最大体力が25/32/38/45増加する。" : "超成長";
            return true;
        }
        if (key.IndexOf("Star_Darius_L_Revitalize", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "皆殺しの回復量が12%/16%/20%/24%増加し、体力40%未満ではさらに6%増加する。" : "生気付与";
            return true;
        }
        if (key.IndexOf("Star_Darius_L_Conditioning", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "物理防御が0.5/0.75/1/1.25増加する。" : "心身調整";
            return true;
        }
        if (key.IndexOf("Star_Darius_L_Unflinching", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "体力40%未満の間、移動速度が8%/10%/12%/14%増加する。" : "気迫";
            return true;
        }
        if (key.IndexOf("Star_Darius_I_CripplingStrike", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "戦闘開始時、この星座レベルの【脚削ぎ】メモリーをダリウスの近くに1つ生成する。" : "ノクサス武装：脚削ぎ";
            return true;
        }
        if (key.IndexOf("Star_Darius_I_Apprehend", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "戦闘開始時、この星座レベルの【捕縛】メモリーをダリウスの近くに1つ生成する。" : "ノクサス武装：捕縛";
            return true;
        }
        if (key.IndexOf("Star_Darius_I_InstantDecimate", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "皆殺しの0.75秒の予備動作と回復を失う代わりに即座に回転し、ダメージが20%/25%/30%/35%増加、全命中を外周扱いにする。【大出血】装備中のみ確定で1スタック付与する。" : "決然たる一振り";
            return true;
        }
        if (key.IndexOf("Star_Darius_I_WarFervor", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "5/5.5/6/6.5秒以内に任意の対象へ合計5スタックの大出血を付与すると、攻撃力35%相当のノクサスの力を発動する。大出血メモリーの強化も適用される。" : "戦いの熱情";
            return true;
        }
        if (key.IndexOf("Star_Darius_I_LegacyCripplingStrike", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "脚削ぎのスロウが0.25/0.35/0.45/0.55秒延長。命中時、対象の大出血1スタックにつき残りクールダウンを0.5/0.6/0.7/0.8秒短縮する。" : "退路を断て";
            return true;
        }
        if (key.IndexOf("Star_Darius_I_LegacyApprehend", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "捕縛で引き寄せた敵を3秒間マーク。マーク対象に対するダリウスのスキルと大出血の物理ダメージが10%/13%/16%/20%増加する。" : "鉄腕征服";
            return true;
        }
        if (key.IndexOf("Star_Darius_I_LegacyGuillotine", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "ノクサスギロチンのダメージが15%/20%/25%/30%増加する。アクシオム アルカニストとは乗算で重なる。" : "ギロチンの法";
            return true;
        }
        if (key.IndexOf("Star_Darius_D_BloodRush", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "出血中の敵1体につき移動速度+3%/3.5%/4%/4.5%。最大5体まで。" : "血の疾走";
            return true;
        }
        if (key.IndexOf("Star_Darius_D_NoxianMight", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "ノクサスの力発動時、攻撃力をさらに9/12/15/18得る。" : "真なるノクサスの力";
            return true;
        }
        if (key.IndexOf("Star_Darius_D_Dunkmaster", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "ノクサスギロチンで撃破すると3秒間、移動速度が15%/20%/25%/30%増加する。" : "ダンクマスター";
            return true;
        }
        if (key.IndexOf("Star_Darius_L_BloodPrice", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "出血中の敵を倒すと最大体力の2.5%/3.5%/4.5%/5.5%を回復する。" : "血には血を";
            return true;
        }
        if (key.IndexOf("Star_Darius_F_HandOfNoxus", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "大出血の持続時間が1/1.5/2秒延長される。" : "ノクサスの戦斧";
            return true;
        }
        if (key.IndexOf("Star_Darius_F_NimbusCloak", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "フラッシュまたはゴースト使用時、2秒間移動速度が15%/20%/25%増加する。" : "ニンバスクローク";
            return true;
        }
        if (key.IndexOf("Star_Darius_F_Celerity", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "移動速度が5%/6.5%/8%増加する。" : "追い風";
            return true;
        }
        if (key.IndexOf("Star_Darius_F_GatheringStorm", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "ラン中のヒーローレベルが5上がるごとに嵐スタックを1つ獲得。各スタックで攻撃力+2.5/3/3.5。" : "強まる嵐";
            return true;
        }
        if (key.IndexOf("Star_Darius_F_CosmicInsight", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "フラッシュとゴーストの回復速度が20%/25%/30%上昇する。" : "宇宙の英知";
            return true;
        }
        return false;
    }
}