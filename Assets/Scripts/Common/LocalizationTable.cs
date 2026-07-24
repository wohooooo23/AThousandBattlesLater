using System.Collections.Generic;

/// <summary>
/// English source string -> Simplified Chinese. The English text authored by the scene builders is
/// the key, so nothing in the builders or the story data has to change to support a second language.
///
/// Adding a language later means adding another dictionary here. Note that if an English string is
/// reworded, its entry must be updated too, otherwise that line simply falls back to English.
/// </summary>
public static class LocalizationTable
{
    public static bool TryGetChinese(string english, out string chinese)
        => Chinese.TryGetValue(english, out chinese);

    private static readonly Dictionary<string, string> Chinese = new Dictionary<string, string>
    {
        // ---------- Story: opening ----------
        { "Decades have passed... and now I have returned.", "数十年过去了……如今我回来了。" },
        { "Since that day, I have lost count of the battles I have fought.", "自那日起，我已记不清身经了多少场战斗。" },
        { "Even on the quietest nights, I have never known a moment's rest.", "纵是最寂静的夜里，我也不曾有过片刻安宁。" },
        { "Today, I have finally made my choice.", "今日，我终于做出了抉择。" },
        { "I will put the past to rest.", "我要让过往就此安息。" },

        // ---------- Story: first encounter ----------
        { "These monsters again? How familiar.", "又是这些魔物？真是熟悉。" },
        { "Time has rusted my blade—and weathered its wielder.", "岁月锈蚀了我的刀——也磨损了持刀之人。" },
        { "I should find equipment worthy of the road ahead.", "我该寻些配得上前路的装备。" },

        // ---------- Story: boss introduction ----------
        { "You...?", "是你……？" },
        { "Me. I have come to settle an old debt.", "是我。我为了结一笔旧债而来。" },
        { "So you finally came. You never could let go of what happened.", "你终究还是来了。当年之事，你从来放不下。" },
        { "Your lord met a truly wretched end.", "你的主君，死得实在凄惨。" },
        { "Do not dare speak of him!", "不许你提起他！" },
        { "Ha! Whether I have the right is yours to prove in battle!", "哈！我有没有资格，就由你在战斗中证明吧！" },

        // ---------- Story: boss victory ----------
        { "You have grown stronger. All those years of battle...", "你变强了。这些年的厮杀……" },
        { "You did not kill my lord! Who are you?", "杀我主君的不是你！你到底是谁？" },
        { "...and wiser, too. Your eyes have sharpened.", "……也变得更明智了。你的眼睛锐利了许多。" },
        { "You are right. It was not me. Take the crimson rune you found and seek the truth.", "你说得对，不是我。带上你寻得的绯红符文，去追寻真相吧。" },
        { "...", "……" },
        { "Then today, at last, the truth will be revealed.", "那么今日，真相终将大白。" },

        // ---------- Guidance prompts ----------
        { "WASD / Arrow Keys — Move", "WASD / 方向键 —— 移动" },
        { "Press J to attack. Find treasure chests and press F to open them.", "按 J 攻击。找到宝箱后按 F 开启。" },
        { "Press Space in midair to double-jump.", "在空中按空格进行二段跳。" },
        { "Press Shift while moving to dash.", "移动时按 Shift 冲刺。" },

        // ---------- Start menu ----------
        { "A THOUSAND BATTLES LATER", "身经百战" },
        { "START", "开始游戏" },
        { "HELP", "帮助" },
        { "SETTING", "设置" },
        { "CREDIT", "制作名单" },
        { "CREDITS", "制作名单" },
        { "EXIT", "退出游戏" },
        { "BACK", "返回" },
        { "LANGUAGE", "语言" },

        // ---------- End-screen overlays ----------
        { "DEFEATED\nPress R to Restart", "败北\n按 R 重新开始" },
        { "VICTORY\nPress R to Restart", "胜利\n按 R 重新开始" },

        // ---------- Minimap legend (rich text kept intact) ----------
        {
            "<color=#FFD21A>● CHEST</color>    <color=#FF3030>■ BOSS</color>",
            "<color=#FFD21A>● 宝箱</color>    <color=#FF3030>■ 首领</color>"
        },

        // ---------- Item detail panel ----------
        { "No description available.", "暂无描述。" },
        { "Click for actions", "点击查看操作" },
        { "[E] Equip    [Q] Cancel", "[E] 装备    [Q] 取消" },
        { "[E] Unequip    [Q] Cancel", "[E] 卸下    [Q] 取消" },
        { "[E] Use    [Q] Cancel", "[E] 使用    [Q] 取消" },
        { "[Q] Close", "[Q] 关闭" },

        // ---------- Forge ----------
        { "SELECT EQUIPMENT FIRST", "请先选择装备" },
        { "ALREADY MAXED!", "已达最高等级！" },
        { "NOT ENOUGH GOLD!", "金币不足！" },
        { "FORGING... [{0}/10]", "锻造中…… [{0}/10]" },
        { "SUCCESS! +{0}", "成功！+{0}" },
        { "FAILED! -{0}", "失败！-{0}" },
        { "-- ATK", "-- ATK" },
        { "N/A", "无" }
    };
}
