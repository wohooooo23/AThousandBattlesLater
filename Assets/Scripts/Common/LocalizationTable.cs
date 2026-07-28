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
        // ---------- Story: stage 1 opening ----------
        { "Decades have passed... and now I have returned.", "数十年过去了……如今我回来了。" },
        { "Since that day, I have lost count of the battles I have fought.", "自那日起，我已记不清身经了多少场战斗。" },
        { "Even on the quietest nights, my heart has never known a moment's rest.", "纵是最寂静的夜里，我的心也不曾有过片刻安宁。" },
        { "Today, I have finally made my choice—", "今日，我终于做出了抉择——" },
        { "I swear to defend justice and demand the truth for my lord!", "我发誓守护正义，也要为主公讨回真相！" },

        // ---------- Story: stage 1 first encounter ----------
        { "These monsters again... It feels all too familiar.", "又是这些魔物……这一切实在太熟悉了。" },
        { "Time has rusted my blade—and weathered its wielder.", "岁月锈蚀了我的刀——也磨损了持刀之人。" },
        { "I should find equipment worthy of the road ahead.", "我该寻些配得上前路的装备。" },

        // ---------- Story: stage 1 boss introduction ----------
        { "You...?", "是你……？" },
        { "Yes. I have come to demand the truth.", "不错。我是来讨一个真相的。" },
        { "So you finally came. You never could let go of that day.", "你终究还是来了。那一天的事，你始终无法放下。" },
        { "Your precious lord met a truly wretched end.", "你那位敬爱的主公，死得实在凄惨。" },
        { "Do not dare speak of him!", "不许你提起他！" },
        { "Ha! If you would silence me, prove your right in battle!", "哈！想让我闭嘴，就用你的刀来证明吧！" },

        // ---------- Story: stage 1 boss victory ----------
        { "You have grown stronger through all those years of battle...", "经历了这么多年的战斗，你果然变强了……" },
        { "That technique... You were not the one who killed my lord! What happened that day?", "这招式……杀害主公的人不是你！那一天究竟发生了什么？" },
        { "And wiser, too. Age has sharpened your eyes.", "也更明智了。岁月让你的眼睛变得锐利。" },
        { "You are right. It was not me. Take the crimson rune you found—and seek the truth yourself.", "你说得对，并不是我。带上你找到的绯红符文——亲自去追寻真相吧。" },
        { "...", "……" },
        { "Then today, at last, the truth will be revealed.", "那么今日，真相终将大白。" },
        { "Wait... why is the crimson rune glowing?", "等等……绯红符文为什么在发光？" },

        // ---------- Story: stage 2 opening ----------
        { "This rune... it brought me back to—", "这枚符文……把我带回了——" },
        { "The wizard's castle, on the very day my lord died.", "巫师的城堡，而且正是主公遇害的那一天。" },
        { "Then I can finally see it with my own eyes...", "那么，我终于能亲眼看清一切……" },
        { "Whoever killed my lord will repay that blood a hundredfold.", "无论是谁杀了主公，我都要让他百倍血偿。" },

        // ---------- Story: stage 2 boss introduction ----------
        { "My lord!", "主公！" },
        { "I was fighting elsewhere that day. I could not save you.", "那一天我还在别处厮杀，没能赶来救您。" },
        { "This time, I will keep my oath. I will defend you with my life!", "这一次，我定会履行誓言，拼上性命守护您！" },
        { "He is unworthy of your oath.", "他不配得到你的效忠。" },
        { "What? Why are you here—alive?", "什么？你为什么会在这里——而且还活着？" },
        { "First, see the truth for yourself.", "先亲眼看看真相吧。" },
        { "What... my lord was behind it all?", "什么……这一切竟然都是主公安排的？" },
        { "But why should I believe you?", "可我凭什么相信你？" },
        { "You will, once you see your lord with your own eyes.", "等你亲眼看见自己的主公，就会相信了。" },
        { "I forged the crimson and verdant runes. The green rune can return you to the future.", "绯红与翠绿符文都出自我手。绿色符文能送你回到未来。" },
        { "My part is finished. How amusing...", "我的任务完成了。真是有趣……" },
        { "On your next raid, take this village. I will leave enough 'food' for you.", "下一次袭击就去这个村子。我会为你们留下足够的‘食物’。" },
        { "Graaagh!", "吼——！" },
        { "In return, you will bring me the gold we agreed upon.", "作为交换，你们要把约定好的黄金带给我。" },
        { "Wait. Who is there?", "等等。谁在那里？" },
        { "What is this? Why is my lord bargaining with the enemy?", "这是怎么回事？主公为何在与敌人交易？" },
        { "Was the man I followed nothing but a hypocrite?", "难道我一直追随的，只是一个伪君子？" },
        { "What have I done...?", "我究竟都做了些什么……？" },
        { "But it is not too late. I will still honor my oath—", "但现在还不算太迟。我仍会履行自己的誓言——" },
        { "I will defend justice!", "我要守护正义！" },

        // ---------- Story: stage 2 boss victory ----------
        { "To think I killed my own lord to keep the very oath I made to him...", "没想到，为了履行向主公立下的誓言，我竟亲手杀了他……" },
        { "At last, everything I came here to do is done.", "终于，我来到这里要做的一切都结束了。" },
        { "I will return to the future with one vow intact: justice, no matter the cost.", "我会回到未来，并守住唯一不变的誓言：无论代价如何，都要维护正义。" },

        // ---------- Guidance prompts ----------
        { "WASD / Arrow Keys — Move", "WASD / 方向键 —— 移动" },
        { "Press J to attack. Press I to throw a kunai.", "按 J 攻击。按 I 投掷苦无。" },
        { "Press F to open treasure chests.", "按 F 开启宝箱。" },
        { "Press B to open the backpack, N to open the forge.", "按 B 打开背包，按 N 打开锻造。" },
        { "Press Space in midair to double-jump.", "在空中按空格进行二段跳。" },
        { "Press Shift while moving to dash.", "移动时按 Shift 冲刺。" },
        { "The gate has a red rune-shaped recess.", "大门上有一个红色的符文凹槽" },
        { "The gate has a green rune-shaped recess.", "大门上有一个绿色的符文凹槽" },

        // ---------- Start menu ----------
        { "A THOUSAND BATTLES LATER", "武者之誓" },
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

        // ---------- Pause menu ----------
        { "PAUSED", "暂停" },
        { "RESUME", "继续游戏" },
        { "HOW TO PLAY", "操作帮助" },
        { "MAIN MENU", "返回主菜单" },

        // ---------- End-screen overlays ----------
        { "DEFEATED\nPress R to Restart", "败北\n按 R 重新开始" },
        { "VICTORY\nPress Space for Main Menu", "胜利\n按空格返回主菜单" },
        {
            "VICTORY\nPress Space for Main Menu\n\nTEAM CONTRIBUTIONS\n" +
            "Zixuan Lu - ZJU - Map Design / Art / Code\n" +
            "Mincha Lu - ZJU - Assets / Art\n" +
            "Xiangming Meng - SJTU - Code",
            "胜利\n按空格返回主菜单\n\n小组分工\n" +
            "路子轩 · ZJU — 地图设计 / 美工 / 代码\n" +
            "卢敏察 · ZJU — 素材 / 美工\n" +
            "孟祥铭 · SJTU — 代码"
        },

        // ---------- Minimap legend (rich text kept intact) ----------
        {
            "<color=#FFD21A>● CHEST</color>    <color=#FF3030>■ BOSS</color>",
            "<color=#FFD21A>● 宝箱</color>    <color=#FF3030>■ 首领</color>"
        },

        // ---------- Item detail panel ----------
        { "No description available.", "暂无描述。" },
        { "Click for actions", "点击查看操作" },
        { "Click to inspect", "点击查看详情" },
        { "[E] Equip    [Q] Cancel", "[E] 装备    [Q] 取消" },
        { "[E] Unequip    [Q] Cancel", "[E] 卸下    [Q] 取消" },
        { "[E] Use    [Q] Cancel", "[E] 使用    [Q] 取消" },
        { "[Q] Close", "[Q] 关闭" },

        // ---------- Item names ----------
        { "Claymore Sword", "巨剑" },
        { "Plate Shield", "板甲盾" },
        { "Crimson Gem", "绯红宝石" },
        { "Green Rune", "绿色符文" },
        { "Wall Jump Orb", "蹬墙跳光球" },
        { "Double Jump Orb", "二段跳光球" },
        { "Dash Orb", "冲刺光球" },
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
            "A crimson rune that increases movement and jump speed by 10%, and dash speed by 30%. It cannot be forged.",
            "装备后移动与跳跃速度提高 10%，冲刺速度提高 30%。红色符文无法锻造。"
        },
        {
            "Restores 2 HP per second while equipped. Each successful forge level adds another 2 HPS.",
            "装备后每秒恢复 2 点生命。每次成功锻造额外增加 2 点每秒恢复。"
        },
        {
            "Enables wall sliding and a wall jump that launches the hero away from a wall.",
            "允许勇者沿墙滑落，并从墙面反向蹬墙跳。"
        },
        {
            "Grants one additional jump while airborne.",
            "允许勇者在空中额外跳跃一次。"
        },
        {
            "Enables a high-speed ground and air dash with Shift.",
            "允许勇者按 Shift 在地面或空中高速冲刺。"
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
            "Slot 1: Sword    Slot 2: Shield    Slot 3: Red Rune    Slot 4: Green Rune\n" +
            "\n" +
            "FORGING\n" +
            "Select equipment on the left, then press the centre Forge button.\n" +
            "Swords, shields and the Green Rune can be forged. The Red Rune cannot be forged.\n" +
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
            "槽位 1：武器    槽位 2：护盾    槽位 3：红色符文    槽位 4：绿色符文\n" +
            "\n" +
            "锻造\n" +
            "在左侧选择装备，然后按下中间的锻造按钮。\n" +
            "剑、盾与绿色符文可以锻造；红色符文无法锻造。\n" +
            "锻造失败会降低装备的强化等级。"
        },

        // ---------- Forge ----------
        { "EQUIPMENT", "装备" },
        { "WEAPON", "武器" },
        { "ARMOR", "护甲" },
        { "ACCESSORY", "饰品" },
        { "GREEN RUNE", "绿色符文" },
        { "ABILITY", "能力" },
        { "Passive movement ability", "被动移动能力" },
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
