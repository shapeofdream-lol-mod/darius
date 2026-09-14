public static partial class DariusJapaneseLocalization
{
    private static bool TryGetHeroSkinSkillText(string key, bool description, out string value)
    {
        value = null;
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
        return false;
    }
}