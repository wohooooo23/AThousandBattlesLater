/// <summary>
/// 所有进度的统一入口。清空进度有三个触发点——打赢 Boss、开始菜单里的清零按钮、以及
/// 将来可能新增的入口——各自逐项去清很容易漏掉一项，所以都走这里。
///
/// 覆盖两层：跨会话保存的剧情进度（StoryProgress，落在 PlayerPrefs），以及只在本次
/// Play 会话内存活的一局进度（RunProgress + 背包 + 装备）。
/// </summary>
public static class GameProgress
{
    /// <summary>Whether anything at all has been progressed. Drives the settings button's styling.</summary>
    public static bool HasAny =>
        StoryProgress.HasAny || RunProgress.HasAny || RunInventory.Stacks.Count > 0 ||
        RunEquipment.Weapon != null || RunEquipment.Armor != null || RunEquipment.Rune != null;

    /// <summary>Wipes story, abilities, forge levels, backpack and worn gear back to a first-ever start.</summary>
    public static void ClearAll()
    {
        StoryProgress.Reset();
        RunProgress.Reset();
        RunInventory.Reset();
        RunEquipment.Reset();
    }
}
