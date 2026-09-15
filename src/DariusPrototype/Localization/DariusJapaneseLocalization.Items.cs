using System;
using System.Collections.Generic;

public static partial class DariusJapaneseLocalization
{
    private static bool TryGetItemText(string key, bool description, out string value)
    {
        value = null;
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