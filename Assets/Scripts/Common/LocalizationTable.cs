using System.Collections.Generic;
using System.Text;

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
    {
        if (Chinese.TryGetValue(english, out chinese))
            return true;

        // Long blocks baked into a scene come back from YAML with their line breaks folded and any
        // column-aligning spaces before a break stripped, so the runtime string is not character
        // for character what was authored here. Matching on collapsed whitespace lets a multi-line
        // key (the help page's control list) be written readably without tracking how Unity wrapped
        // it. Short labels always hit the exact lookup above, so this only runs for the long ones.
        return FoldedIndex.TryGetValue(Fold(english), out chinese);
    }

    private static string Fold(string value)
    {
        StringBuilder folded = new StringBuilder(value.Length);
        bool pendingSpace = false;
        foreach (char character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = folded.Length > 0;
                continue;
            }
            if (pendingSpace)
            {
                folded.Append(' ');
                pendingSpace = false;
            }
            folded.Append(character);
        }
        return folded.ToString();
    }

    private static Dictionary<string, string> folded;

    // Built on first use rather than in a field initialiser, which would run before the table below
    // it has been assigned.
    private static Dictionary<string, string> FoldedIndex
    {
        get
        {
            if (folded != null)
                return folded;
            folded = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> entry in Chinese)
                folded[Fold(entry.Key)] = entry.Value;
            return folded;
        }
    }

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
        { "Press J to attack. Press I to throw a kunai.", "按 J 攻击。按 I 投掷苦无。" },
        { "Press F to open treasure chests.", "按 F 开启宝箱。" },
        { "Press B to open the backpack, N to open the forge.", "按 B 打开背包，按 N 打开锻造。" },
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
        { "CLEAR PROGRESS", "清零进度" },
        { "SELECT DIFFICULTY", "选择难度" },
        { "NORMAL", "普通" },
        { "HARD", "困难" },

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

        // ---------- Item names ----------
        { "Claymore Sword", "巨剑" },
        { "Plate Shield", "板甲盾" },
        { "Crimson Gem", "绯红宝石" },
        { "Health Potion", "治疗药水" },
        { "Kunai", "苦无" },
        { "Gold Coin", "金币" },
        { "Demo Cube", "示例方块" },

        // ---------- Item descriptions ----------
        {
            "A heavy two-handed sword. Equip it to replace the hero's unarmed attack power.",
            "一柄沉重的双手大剑。装备后取代主角赤手空拳的攻击力。"
        },
        {
            "A sturdy plate shield that reduces the damage received from every enemy hit.",
            "一面坚固的板甲盾，可减少每次受到的敌人伤害。"
        },
        {
            "A crimson gem prepared for future accessory effects. It can already be equipped and forged.",
            "一枚为日后的饰品效果备下的绯红宝石。目前已可装备与锻造。"
        },
        {
            "A single-use red potion. Select it in the backpack and press E to restore HP to full.",
            "一次性的红色药水。在背包中选中并按 E 可将生命值恢复至满。"
        },
        {
            "Stackable throwing ammunition. Ranged attacks will consume one kunai per shot.",
            "可堆叠的投掷弹药。每次远程攻击消耗一枚苦无。"
        },
        {
            "Currency collected from defeated enemies and spent at the forge.",
            "从击败的敌人身上获得的货币，可在锻造处消费。"
        },
        {
            "A simple material used to demonstrate inventory stacking and rearrangement.",
            "用于演示背包堆叠与整理的简单材料。"
        },

        // ---------- Credits ----------
        // The URLs are identical in both languages; only the asset headings are translated.
        {
            "Health Bar & Backpack UI\n" +
            "https://byandrox.itch.io/pixel-art-rpg-gui\n" +
            "\n" +
            "Map Tileset\n" +
            "https://brullov.itch.io/2d-platformer-asset-pack-castle-of-despair\n" +
            "\n" +
            "Flying Enemy\n" +
            "https://assetstore.unity.com/packages/2d/characters/monsters-creatures-fantasy-167949\n" +
            "\n" +
            "Boss\n" +
            "https://assetstore.unity.com/packages/2d/characters/evil-wizard-2-284501\n" +
            "\n" +
            "Forge, Coins, Kunai, Cover Art\n" +
            "https://gemini.google.com/\n" +
            "\n" +
            "Player Character\n" +
            "https://xzany.itch.io/samurai-2d-pixel-art\n" +
            "\n" +
            "Ground Enemies\n" +
            "https://zerie.itch.io/tiny-rpg-character-asset-pack",

            "血条与背包界面\n" +
            "https://byandrox.itch.io/pixel-art-rpg-gui\n" +
            "\n" +
            "地图图块\n" +
            "https://brullov.itch.io/2d-platformer-asset-pack-castle-of-despair\n" +
            "\n" +
            "飞行怪\n" +
            "https://assetstore.unity.com/packages/2d/characters/monsters-creatures-fantasy-167949\n" +
            "\n" +
            "Boss\n" +
            "https://assetstore.unity.com/packages/2d/characters/evil-wizard-2-284501\n" +
            "\n" +
            "锻造台、金币、飞镖、封面\n" +
            "https://gemini.google.com/\n" +
            "\n" +
            "主角\n" +
            "https://xzany.itch.io/samurai-2d-pixel-art\n" +
            "\n" +
            "地面怪物\n" +
            "https://zerie.itch.io/tiny-rpg-character-asset-pack"
        },

        // ---------- Help page ----------
        { "CONTROLS", "操作说明" },
        {
            "A                         Move Left\n" +
            "D                         Move Right\n" +
            "SPACE / W                 Jump\n" +
            "B                         Open Backpack\n" +
            "N                         Open Forge\n" +
            "ENTER                     Advance Dialogue\n" +
            "I                         Throw Kunai\n" +
            "\n" +
            "BACKPACK EQUIPMENT\n" +
            "Slot 1: Sword    Slot 2: Shield    Slot 3: Red Rune\n" +
            "\n" +
            "FORGING\n" +
            "Select equipment on the left, then press the centre Forge button.\n" +
            "Only swords and shields can currently be forged.\n" +
            "A failed attempt lowers the equipment's upgrade level.",

            "A                        向左移动\n" +
            "D                        向右移动\n" +
            "空格 / W                 跳跃\n" +
            "B                        打开背包\n" +
            "N                        打开锻造\n" +
            "回车                     推进对话\n" +
            "I                        投掷苦无\n" +
            "\n" +
            "背包装备栏\n" +
            "槽位 1：武器    槽位 2：护盾    槽位 3：红色符文\n" +
            "\n" +
            "锻造\n" +
            "在左侧选择装备，然后按下中间的锻造按钮。\n" +
            "目前只有剑与盾可以锻造。\n" +
            "锻造失败会降低装备的强化等级。"
        },

        // ---------- Forge ----------
        { "EQUIPMENT", "装备" },
        { "WEAPON", "武器" },
        { "ARMOR", "护甲" },
        { "ACCESSORY", "饰品" },
        { "ANCIENT FORGE", "远古锻炉" },
        { "STATS", "属性" },
        { "STAT BOOST", "属性提升" },
        { "SUCCESS RATE", "成功率" },
        { "GOLD REQUIRED", "所需金币" },
        { "Empty", "空" },
        { "MAXED", "已满级" },
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
