using System;
using System.Collections.Generic;

public static class DariusJapaneseLocalization
{
    public static bool TryGet(string key, bool description, out string value)
    {
        value = null;
        if (string.IsNullOrEmpty(key)) return false;
        if (key.IndexOf("Cosmetics_Category_Hero_Darius", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = "ダリウス";
            return true;
        }
        if (key.IndexOf("Hero_Darius_Name", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = "ダリウス";
            return true;
        }
        if (key.IndexOf("Hero_Darius_Subtitle", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = "ノクサスの戦斧";
            return true;
        }
        if (key.IndexOf("Hero_Darius_Description", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = "ノクサス屈指の歴戦の司令官。巨大な斧で敵陣を切り裂き、出血を重ねて最後は処刑へとつなげる。";
            return true;
        }
        if (key.IndexOf("Skin_Darius_Default", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "ダリウスのクラシックな鎧と巨大な斧。" : "クラシック ダリウス";
            return true;
        }
        if (key.IndexOf("Skin_Darius_GodKing", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "神なる王ダリウス。見た目のみ変更し、ゲームプレイには影響しない。" : "神なる王ダリウス";
            return true;
        }
        if (key.IndexOf("Skin_Darius_Dunkmaster", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "ダンクマスター ダリウス。専用モデル、リグ、アニメーションを使用する。" : "ダンクマスター ダリウス";
            return true;
        }
        if (key.IndexOf("Skin_Darius_Mecha", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "メカキングダム ダリウス。専用モデル、リグ、アニメーションを使用する。" : "メカキングダム ダリウス";
            return true;
        }
        if (key.IndexOf("Darius_Decimate", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "短い予備動作の後、周囲を斧で薙ぎ払う。外周はより高い物理ダメージを与え、命中時に回復する。【大出血】装備中は外周命中でスタックを付与する。" : "皆殺し";
            return true;
        }
        if (key.IndexOf("Darius_CripplingStrike", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "4秒以内の次の通常攻撃を強化し、追加物理ダメージと強力なスロウを与える。" : "脚削ぎ";
            return true;
        }
        if (key.IndexOf("Darius_Apprehend", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "前方扇状範囲の敵をダリウスの目前へ引き寄せ、スロウを与える。" : "捕縛";
            return true;
        }
        if (key.IndexOf("Darius_NoxianGuillotine", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "近くの敵へ跳びかかり確定ダメージを与える。【大出血】スタックで威力が上昇し、撃破するとクールダウンを即座にリセットして【ノクサスの力】を得る。" : "ノクサスギロチン";
            return true;
        }
        if (key.IndexOf("Darius_Hemorrhage", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "通常攻撃やスキルで敵を出血させる。最大スタックに達すると【ノクサスの力】が発動する。" : "大出血";
            return true;
        }
        if (key.IndexOf("Darius_Flash", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "指定方向へ瞬時にブリンクする。" : "フラッシュ";
            return true;
        }
        if (key.IndexOf("Darius_Ghost", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "一定時間、移動速度が上昇する。" : "ゴースト";
            return true;
        }
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
        if (key.IndexOf("Star_Darius_D_ItemTrinityForce", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "Q/W/E/R使用後にスペルブレードを得る。4秒以内の次の通常攻撃が攻撃力130%/150%/170%/195%分の追加物理ダメージ。独立クールダウン1.5秒。" : "トリニティ フォース";
            return true;
        }
        if (key.IndexOf("Star_Darius_D_ItemBlackCleaver", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "通常攻撃、Q、大出血の物理ダメージで6秒間の切断スタックを付与、最大5。各スタックで対象の物理防御を5%/5.5%/6%/6.5%低下。敵ごとに別管理。" : "ブラック クリーバー";
            return true;
        }
        if (key.IndexOf("Star_Darius_D_ItemSpearOfShojin", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "QまたはWのスキルダメージで6秒間のドラゴンフォースを1スタック、最大4。各スタックで以後のQ/Wダメージが5%/6%/7%/8.5%増加。1回の発動で得られるのは1スタック。" : "ショウジンの矛";
            return true;
        }
        if (key.IndexOf("Star_Darius_L_ItemSteraksGage", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "体力が初めて35%未満になると5秒間、最大体力25%/30%/35%/40%分のライフラインバリアを得る。クールダウン30/28/26/24秒。" : "ステラックの篭手";
            return true;
        }
        if (key.IndexOf("Star_Darius_L_ItemDeathsDance", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "受けるダメージの5%/10%/15%/20%を3秒かけて確定ダメージとして後払いする。敵を倒すと未払い分の10%/20%/30%/40%を消去するが回復はしない。" : "デス ダンス";
            return true;
        }
        if (key.IndexOf("Star_Darius_L_ItemOverlordsBloodmail", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "最大体力1につき攻撃力0.010/0.0125/0.015/0.018へ変換し、最大100攻撃力。減少体力に応じてさらに最大12/15/18/22攻撃力を得る。" : "オーバーロード ブラッドメイル";
            return true;
        }
        if (key.IndexOf("Star_Darius_I_ItemSunderedSky", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "敵ごとに独立したチャージを持つ。次のチャージ済み通常攻撃が攻撃力100%/115%/130%/150%分の追加物理ダメージを与え、減少体力の5%/6%/7%/8%を回復する。" : "サンダード スカイ";
            return true;
        }
        if (key.IndexOf("Star_Darius_F_ItemStridebreaker", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "脚削ぎが3mの衝撃波も放ち、攻撃力60%/70%/80%/95%分の物理ダメージと35%/40%/45%/50%スロウを1.5秒与える。" : "ストライドブレイカー";
            return true;
        }
        if (key.IndexOf("Star_Darius_I_ItemDeadMansPlate", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "移動で最大100のモメンタムを蓄える。次の通常攻撃で消費し、最大で攻撃力50%/60%/70%/85%分の追加物理ダメージ。最大時は強力なスロウも付与。" : "デッドマン プレート";
            return true;
        }
        if (key.IndexOf("Star_Darius_I_ItemYoumuusGhostblade", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "2秒間非戦闘状態になると移動速度+15%/18%/21%/25%。ダメージを与えるか受けると解除され、再び非戦闘になるまで戻らない。" : "妖夢の霊剣";
            return true;
        }
        if (key.IndexOf("Star_Darius_F_Awoo", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = description ? "各部屋開始時、ノクサスギロチンの攻撃力倍率を25%、クールダウンを半分にする。R使用でAnimalsを再生。R撃破でその部屋の倍率+5%と再生速度+0.1、外すと速度-0.1。部屋移動で全てリセット。" : "アオオー！！！";
            return true;
        }
        return false;
    }
}
