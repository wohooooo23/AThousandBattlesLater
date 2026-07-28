# Final Test

## 两章剧情、英文对白与四格漫画管线

正式流程现在用三张带英文人物对白框的像素风四格漫画和两组独立的章节台词，完整交代武士对国王的敬仰、国王牺牲村落牟利的真相，以及“未来武士刺杀国王—原时间线武士因此复仇”的时间闭环。英文是场景中保存的规范正文；运行时继续通过现有语言切换系统，把全部 48 句正文映射为润色后的简体中文。

1. **序章漫画**：`Assets/Resources/Story/Comic_Prologue.png` 复用 Hero、Medieval King、Evil Wizard 和 Orc 的轮廓与配色，并改为参考图所示的大颗粒、低内部像素分辨率画风。四个严格正方形分格依次表现国王在重伤武士与平民前迎战怪物、武士宣誓效忠、尸横遍野的战场上传来国王遇刺消息、老年武士来到巫师城堡坚守誓言；每格带一条简短英文对白。
2. **真相漫画**：`Assets/Resources/Story/Comic_Betrayal.png` 的四个正方形分格表现国王把领地划给巫师与怪物、国王守着金币而远处村民受难、未来武士亲手斩杀国王、原时间线武士跪在国王身旁而未来武士携绿色符文光藏在阴影里。具体分镜以仓库根目录的 `comic.txt` 为创作来源。
3. **结尾漫画**：`Assets/Resources/Story/Comic_Epilogue.png` 从国王倒下开始，依次表现未来武士意识到自己正是历史中的刺客、绿色符文开启时间裂隙、原时间线武士收到死讯并立誓复仇，以及多年后他再次来到巫师城堡。最后一格的 `A THOUSAND BATTLES LATER...` 将第二关结局重新扣回序章和标题。
4. **逐格播放**：三张素材都是正方形的 2×2 漫画纹理，每个象限也是严格正方形。场景保存的 `StoryComicPanel` 使用 `RawImage.uvRect` 按“左上—右上—左下—右下”依次裁出四格；每按一次 Enter 才进入下一格。单格以 900×900 原比例显示，不会被横向拉伸或压缩到难以阅读。
5. **保存式 UI 与资源引用**：`Assets/Prefab/StoryComicPanel.prefab` 保存 Screen Space Overlay Canvas、黑色遮罩、漫画 RawImage、白色描边和 `Press Enter to continue` 提示。两关各保存一个 prefab 实例，并由 `StoryDialogueController.comicPanel` 直接引用；Stage 2 的 `endingComic` 也直接保存为 `Comic_Epilogue`，运行时不临时创建任何核心 Canvas 或 Component。
6. **第一关插入点**：首次进入 `stage1_full` 时，序章漫画显示在黑幕之上；四格播放完成后才淡入地图，再播放五句武士独白。遭遇怪物、进入巫师 Boss 房和击败巫师后的台词均按新大纲重写。最后一句 `Wait... why is the crimson rune glowing?` 在第一关切换黑幕开始时保持可见，直到第二关加载。
7. **第二关插入点**：`StoryBeat.Stage2Opening` 与第一关 `Opening` 分开记录，因此转场后会播放四句穿越独白，死亡重载时又不会重复。进入国王房后先显示国王；第 4 句前显示只有 `Idle_0` 的巫师演员并把巫师台词挂到他头顶，巫师说完 `First, see the truth for yourself.` 后逐格播放真相漫画；第 12 句前隐藏巫师、显示国王身边的完整 Orc，并继续国王、怪物和武士的现场对话。国王死亡后的三句收尾对白结束后，黑幕用 unscaled time 渐出，再逐格播放结尾漫画。
8. **说话者路由**：`StoryDialogueController` 在原 Hero/Boss 两个气泡之外保存 `additionalSpeakerBubbles`，将 `EvilWizard` 与 `Monster` 分别路由到巫师和 Orc 自己的世界空间气泡。四名演员的台词不再共享国王气泡；漫画内的对白框仍烘焙进图片，不依赖运行时排版。
9. **中英双语**：场景只存一份英文键，`LocalizationTable` 为当前两章 48 句逐句保存中文翻译，并保存最终 Victory/团队分工界面的整块中文映射。对话框与结算界面都走开始菜单已有的语言切换，不另建第二套组件。
10. **进度兼容**：`Stage2Opening` 追加在 `StoryBeat` 枚举末尾，避免改变旧教学触发器已经保存的枚举数值。`PrepareForNextStage` 仍只重置 Boss Introduction，第二关开场由自己的进度位控制。
11. **重建与验证**：`Tools > Narrative & Audio > Build Two-Chapter Story` 配置三张纹理为 Point Filter、无 mipmap、无压缩，重建漫画 prefab，并幂等写入两关台词、图片引用、插入索引与 Stage 2 团队分工文本。`Validate Two-Chapter Story` 检查台词数量、章节进度位、三张漫画引用、结尾淡出时长与保存式 Canvas；`StoryChapterPlayModeTests` 进一步验证关键英文台词、四个 UV 裁切区域、结尾资源和中英分工文本。

```text
Start -> stage1 black screen
  -> prologue comic panels 1..4 (Enter)
  -> fade in -> stage1 opening / encounter / wizard story
  -> final crimson-rune line remains visible -> black fade
  -> stage2 time-travel opening
  -> king-room dialogue lines 1..6
  -> betrayal comic panels 1..4 (Enter)
  -> king bargain revelation -> king battle -> victory dialogue
  -> black fade-out -> epilogue comic panels 1..4 (Enter)
  -> final Victory / team contributions
```

`final test` 是当前 Unity 6（`6000.5.2f1`）工作工程。项目的完整玩法由
`Assets/Scenes/stage1_full.unity` 与 `Assets/Scenes/stage2_full.unity` 串联地图探索、宝箱奖励、装备/背包和两场 Boss 战。

## 完整战役流程管线

正式流程固定为 `StartMenu → stage1_full → stage2_full → Victory`。第一关 Boss 是中间关结算，第二关 Boss 才是整轮游戏的最终结算。

1. **开始入口**：`StartMenuController.targetSceneName` 直接保存为 `stage1_full`；Build Settings 的启用顺序固定为 `StartMenu`、`stage1_full`、`stage2_full`、`Help`，旧 `stage1` 与独立 `stage1 boss` 不再参与正式流程。
2. **中间关 Boss 结算**：第一关 `EnemyHealth.nextStageSceneName` 保存为 `stage2_full`。Boss 死亡后仍播放现有胜利剧情，但调用 `PlayBossVictory(false)`，不会启用最终 Victory 面板。
3. **进度继承**：第一关结束时不调用 `GameProgress.ClearAll`，因此背包堆叠、已装备物品、红/绿符文、能力解锁和锻造等级全部通过现有静态 Run 数据进入第二关。`StoryProgress.PrepareForNextStage` 只清除 Boss Introduction 标记，使第二关 Boss 仍能播放入场对话，其余开场和教学不重复。
4. **黑屏淡出**：第一关 Boss 剧情结束后，`EnemyHealth` 使用场景中已经保存的 `Story Fade Canvas/Black Fade`，按 unscaled time 在 1.15 秒内从透明变为纯黑；即使剧情将 `Time.timeScale` 保持为 0，淡出仍能正常完成。
5. **第二关淡入**：加载 `stage2_full` 后，启用的 `StoryDialogueController` 从全黑开始。开场剧情已经完成时不重复台词，但仍执行 1.35 秒 `FadeFromBlack`，形成连续的“第一关淡出—加载—第二关淡入”。
6. **最终胜利**：第二关 `nextStageSceneName` 为空。国王死亡后先播放完整胜利剧情，再使用同一个保存式黑幕在 1.15 秒内淡出，逐格播放 `Comic_Epilogue`；漫画完成后淡入最终 Victory 面板。面板同时显示路子轩、卢敏察、孟祥铭的院校与分工，按 Space 返回 `StartMenu` 时才通过 `GameProgress.ClearAll` 清除整轮背包、装备、能力、锻造、难度与剧情进度。
7. **第二关暂退续关**：`StartMenuController` 在场景中分别保存 `targetSceneName = stage1_full` 与 `resumeStageSceneName = stage2_full`。从第二关 Boss 房通过 ESC 返回主菜单时，本轮进度和 `Stage2Opening` 章节标记仍然存在，START 因此解析为第二关；最终胜利或手动清档会统一清除章节标记，下一次 START 重新从第一关开始。
8. **保存式组件**：流程直接配置两个场景内已有的 `EnemyHealth`、`StoryDialogueController` 和 `CanvasGroup`，没有在运行时补挂核心组件或创建永久 UI。只有淡入淡出协程属于短生命周期运行逻辑。
9. **可重复构筑与验证**：菜单 `Tools > A Thousand Battles Later > Build Campaign Flow` 执行 `CampaignFlowBuilder.Build`，幂等写入开始目标、两关结算角色、淡出引用和 Build Settings；`Validate Campaign Flow` 检查第一关只通往第二关、第二关只显示最终 Victory、两个 Story System 已保存为启用状态，并验证四个正式场景的顺序。

```text
START
  -> stage1_full
  -> stage1 Boss victory dialogue (no Victory panel)
  -> black fade-out
  -> load stage2_full, preserve RunInventory / RunEquipment / RunProgress
  -> black fade-in
  -> stage2 Boss victory dialogue
  -> black fade-out -> Comic_Epilogue panels 1..4 (Enter)
  -> fade-in -> Victory / team contributions panel
  -> Space: clear run and return to StartMenu
```

## Hero 受击闪白管线

Hero 与敌人共用 `Entity_VFX` 的材质切换效果，核心组件和引用直接保存在 `Hero.prefab`，运行时不会临时补挂。

1. **伤害入口**：近战、子弹、毒气和尖刺最终都通过 `IDamageable.ApplyDamage -> CombatHealth.ApplyDamage`，只有实际扣除生命时才调用 `HeroHealth.OnDamaged`。
2. **受击反馈**：`HeroHealth.Awake` 缓存同一 Hero 根对象上的 `Entity_VFX`；`OnDamaged` 依次刷新 HP、播放血条反馈、调用 `PlayOnDamageVfx`，最后保留原有击退与跳落状态切换。
3. **材质恢复**：`Entity_VFX` 在 Awake 保存动画 SpriteRenderer 的原材质，受击时切换到 `OnDamage_Material` 0.2 秒后恢复；连续受击会重启同一协程，不会叠加多个闪白计时器。
4. **保存式引用**：`Hero.prefab` 已保存 Hero 根对象的 `Entity_VFX`，其 `targetRenderer` 指向动画模型，`onDamageMaterial` 指向 `Assets/Material/OnDamage_Material.mat`；`HeroHealth.Awake` 会要求该保存式组件存在，避免静默退化成无反馈状态。

```text
Enemy hit / projectile / poison / spike
  -> CombatHealth.ApplyDamage
  -> HeroHealth.OnDamaged
  -> HP bar feedback + Entity_VFX.PlayOnDamageVfx
  -> white material (0.2 s)
  -> original animated sprite material
```

## 红色 / 绿色符文效果管线

红色与绿色符文占用各自独立的装备槽，可以同时生效；红色符文提供固定机动强化且不可锻造，绿色符文提供可锻造的持续生命恢复。

1. **美术资源**：`rune_green.svg` 复用红色符文的几何轮廓并使用绿色五级色板；
   `rune_green.png` 是供 Unity UI、世界掉落共同使用的 256×256 Point-filter Sprite。
2. **物品定义**：`ItemType.GreenRune` 追加在枚举末尾，避免改变旧 ItemData 的序列化数值；`ItemData.IsForgeable` 只允许武器、防具和绿色符文进入锻造，红色符文仍可穿戴但明确返回不可锻造。
3. **运行时装备**：`RunEquipment.GreenRune` 独立于红色符文的 `Rune` 字段，因此两枚符文可以
   同时装备；装备、卸下、详情面板和跨场景静态状态继续走现有 `RunInventory` / `RunEquipment` 事件链。
4. **红色符文**：`Role` 在 Awake 保存场景中原始的 `speed`、`jumpForce`、`dashspeed`，并订阅 `RunEquipment.Changed`。装备红色符文时分别从原值计算为 130%、130%、150%，卸下后恢复原值，因此反复装备不会叠乘。
5. **绿色符文**：`HeroHealth` 装备绿色符文时每帧通过统一的 `CombatHealth.RestoreHealth` 恢复生命。基础恢复为 2 HPS；实际恢复为 `2 + ForgeGreenRuneLevel × 2`，死亡时不复活，满血时不重复刷新 UI。
6. **锻造与跨场景状态**：锻造面板第三格现在映射 `RunEquipment.GreenRune`，不再映射红色符文；右侧显示 HPS 的锻造前后数值。`RunProgress.ForgeGreenRuneLevel` 与武器、防具等级一起跨场景保存、失败降级并在新一局统一清零。
7. **背包界面**：纸娃娃左列从上到下固定为武器、防具、红色符文、绿色符文；第四格
   `EquipSlot_L3` 直接保存 `EquipmentSlotUI(ItemType.GreenRune)`，不会在运行时创建组件。
8. **关卡投放**：`GreenRunePickup.prefab` 复用既有重力、弹出和 1 秒拾取保护逻辑；绿色符文现在由
   第二关右下角宝箱提供，第一关上方宝箱则提供红色符文、药水与飞镖。构筑过程只改掉落引用，
   不写入宝箱 Transform，因此不会覆盖在编辑器中保存的位置。
9. **可重复构筑**：菜单 `Tools > Inventory > Build Green Rune` 执行
   `VerdantRuneBuilder.Build`，只补齐图标导入设置、物品/掉落预制体、第四装备槽和宝箱引用；
   `Validate Green Rune` 还会校验绿色符文可锻造、红色符文不可锻造，并断言红色符文的 1.3/1.3/1.5 倍率、绿色符文 2 HPS、每级 +2 HPS 的数值公式，以及第二关右下角绿符文掉落。

运行路径：

```text
Supply Treasure Chest
  -> GreenRunePickup (ItemPickup)
  -> RunInventory
  -> ItemDetailPanel: E 装备 / Q 取消
  -> RunEquipment.GreenRune
  -> HeroHealth: (2 + forge level * 2) HP/s

Rune_Crimson equipped
  -> RunEquipment.Changed
  -> Role restores from authored base values
  -> movement 130% / jump 130% / dash 150%
```

## 双关卡符文门钥匙管线

红、绿符文同时承担装备和 Boss 房钥匙职责。门只检查对应符文是否正在装备，不消耗物品；这样玩家必须在背包详情中明确装备钥匙，进入后仍保留原有符文能力。

1. **场景保存式配置**：两关现有的 `BossArenaController` 增加 `requiresEquippedRune`、`requiredRuneSlot` 和 `missingRuneMessage` 三个序列化字段。`stage1_full` 保存为 `Accessory`（红符文），`stage2_full` 保存为 `GreenRune`；不在运行时创建门禁组件。
2. **开门检查**：Hero 进入 Boss 门触发器时先调用 `RunEquipment.Get(requiredRuneSlot)`。对应槽为空时取消传送并由 `PlayerProgression` 显示明确提示；第一关显示“需要装备红色符文”，第二关显示“需要装备绿色符文”，英文分别为 `You need to equip the Red/Green Rune.`。装备正确符文后再次接触门，才进入原有的相机、BGM、Boss 激活和剧情流程。
3. **第一关奖励**：保留全部三个宝箱及其编辑器位置。上方 `Supply Treasure Chest` 固定掉落红符文、回复药水和一组飞镖；上方宝箱的小地图圆点改为比普通宝箱大 35% 的红色圆点。左下、右下宝箱及其能力光球不变。
4. **第二关奖励**：删除上方 Supply 宝箱及其小地图标记；左下 `Double Jump Treasure Chest` 提供回复药水、一组飞镖、遗漏的剑与遗漏的二段跳，右下 `Dash Treasure Chest` 提供绿色符文、遗漏的盾与遗漏的冲刺。两只下方宝箱的位置不修改，唯一装备和能力均按跨场景进度去重。
5. **第二关小地图**：右下绿符文宝箱使用比普通宝箱大 35% 的绿色圆点；Boss 门和其他地图信息仍沿用原小地图相机与 Marker Layer。
6. **重建安全**：`DemoSceneBuilder` 的第一关完整地图生成路径已同步红符文宝箱、红色标记和红符文门禁，重建第一关不会恢复旧掉落。菜单 `Tools > A Thousand Battles Later > Build Rune Boss Gates` 可幂等地把最终规则重新写入两个场景，并刻意不写宝箱坐标；`Validate Rune Boss Gates` 会检查门槽位、提示、掉落顺序、被删除的第二关上方宝箱以及彩色大圆点。

运行路径：

```text
Hero touches Boss gate
  -> BossArenaController checks the scene-authored required slot
  -> rune not equipped: localized HUD hint, remain in stage
  -> matching rune equipped: enter arena (rune remains equipped)

stage1 upper chest -> Red Rune + Health Potion + Kunai -> enlarged red minimap dot
stage2 lower-left  -> Health Potion + Kunai + missing Sword / Double Jump
stage2 lower-right -> Green Rune + missing Shield / Dash -> enlarged green minimap dot
```

## 第二关缺失奖励补领与飞镖 HUD 管线

第二关的两只下方宝箱现在同时承担第一关遗漏奖励的补领职责；判断依据是跨场景保存的背包、装备栏和能力进度，因此正常取得过的奖励不会复制，遗漏的奖励则一定还有第二次获取机会。

1. **左下宝箱**：`Double Jump Treasure Chest` 直接保存回复药水、16 枚飞镖和 Claymore Sword 三个掉落 prefab；其上方原有 `AbilityUnlockOrb2D(DoubleJump)` 继续保存为该宝箱的能力奖励。药水和飞镖维持可重复补给，剑只补领一次。
2. **右下宝箱**：`Dash Treasure Chest` 直接保存 Green Rune 和 Plate Shield 两个掉落 prefab；其上方原有 `AbilityUnlockOrb2D(Dash)` 继续绑定该宝箱。绿色符文和盾均为唯一装备，取得后不再重复生成。
3. **装备去重**：`TreasureChest2D.RemainingDrops` 在开箱前逐项读取 `ItemPickup.itemData`。对于剑、盾、符文等 `IsEquippable` 物品，同时检查 `RunInventory.Count(item)` 与 `RunEquipment.Get(item.type)`；物品无论放在背包还是已经穿戴，都会从本次生成列表排除。普通消耗品和弹药不参与唯一物品去重。
4. **能力去重**：二段跳和冲刺光球在 `Awake` 中读取 `RunProgress.IsUnlocked`。第一关已经取得对应能力时，第二关光球直接进入 `collected` 状态并保持隐藏；尚未取得时，打开绑定宝箱才显示光球，触碰后继续走统一的 `RunProgress.Unlock` 与 Hero 能力刷新流程。
5. **保存式场景配置**：宝箱 prefab 数组、两个能力光球及 `sourceChest` 引用全部直接保存在 `stage2_full`，不在运行时创建核心奖励 Component，也不修改已在编辑器中确定的宝箱坐标。
6. **飞镖 HUD 遮挡修复**：`Canvas.prefab` 的 `UIManager.kunaiHud` 直接引用现有 `Kunai Count` 对象。背包、锻造或暂停界面进入 `openPanels/IsPauseOpen` 状态时，`UpdatePauseState` 隐藏飞镖 HUD；所有面板关闭后再恢复，从而不依赖透明背景或 Canvas 绘制顺序。
7. **重建与验证**：`Tools > A Thousand Battles Later > Build Rune Boss Gates` 会恢复第二关的五个物品掉落和两个能力光球绑定；对应 Validate 同时检查左右宝箱顺序及能力来源。`Tools > HUD > Build Kunai Count` 会保存 UIManager 的 HUD 引用。`Stage2RecoveryRewardsPlayModeTests` 验证背包内的剑、身上装备的盾均不再掉落，已解锁能力不会再次出现，并验证面板打开/关闭时飞镖 HUD 的隐藏与恢复。

```text
stage2 left chest
  -> missing sword only + repeatable potion/kunai
  -> Double Jump orb only when RunProgress.DoubleJumpUnlocked == false

stage2 right chest
  -> missing Green Rune / shield only
  -> Dash orb only when RunProgress.DashUnlocked == false

inventory / forge / pause opens -> UIManager hides Kunai Count
all panels close                -> UIManager restores Kunai Count
```

## 能力光球装备实现管线

三个移动能力会显示在背包装备栏右半部分，从上到下依次为蹬墙跳、二段跳、冲刺。它们是进度展示槽，不是普通可穿戴物品。

1. **能力数据**：`Ability_WallJump.asset`、`Ability_DoubleJump.asset`、`Ability_Dash.asset` 使用 `ItemType.Ability` 保存名称、说明和圆形图标；该类型不属于 `IsEquippable`，不会进入普通装备或锻造流程。
2. **自动装备状态**：`RunProgress` 是唯一数据源。新一轮开始时蹬墙跳槽自动激活；拾取二段跳或冲刺光球时，原有 `AbilityUnlockOrb2D -> RunProgress.Unlock` 路径触发 `Changed` 事件，相应槽位立即亮起。静态进度跨场景保留，统一重置时清空。
3. **保存式界面**：`Canvas.prefab` 的 `EquipSlot_R0`、`R1`、`R2` 直接保存 `AbilityEquipmentSlotUI` 组件和图标引用；`R3` 保留为空。运行时只刷新显示，不创建组件。
4. **只读交互**：能力槽只实现悬停、移动与左键点击接口，没有拖放接口，也不调用 `RunEquipment.Unequip`。点击后详情面板仅显示 `[Q] Close`，按 E 不产生效果。
5. **可重复构筑**：菜单 `Tools > Inventory > Build Ability Equipment` 生成/更新三个像素圆形图标、能力数据和右侧槽位；`Validate Ability Equipment` 校验槽位映射、不可拖放属性及自动解锁链。`AlphaUiBuilder` 重建背包时也会恢复相同配置。

运行路径：

```text
Run starts ----------------------------> Wall Jump slot active
Double Jump / Dash world orb collected
  -> AbilityUnlockOrb2D
  -> RunProgress.Unlock + Changed
  -> AbilityEquipmentSlotUI refreshes
  -> Right-side icon appears
  -> Hover / click -> ItemDetailPanel (read-only, Q closes)
```

## 第二关 Mushroom / Skeleton 战斗管线

`stage2_full` 使用统一的 `MobStateMachine` 负责索敌、追逐、停步、冷却与死亡取消；具体招式通过 `MobAttackBehaviour` 分离，使飞行眼球、Mushroom 和 Skeleton 共享状态切换而保留不同攻击设计。

1. **统一攻击接口**：`MobAttackBehaviour` 公开攻击范围、理想停步距离、冷却状态、开始与取消操作。`MobStateMachine` 不再依赖飞行眼球的具体脚本，进入 `Attack` 状态后调用当前预制体保存的攻击组件。
2. **Mushroom**：目标进入 5 单位范围后播放一次 `AttackOne`。预警完全复用既有环形斩击的视觉语言：深红完整范围常驻，亮红圆从中心逐渐扩散到外沿；结算圆形斩击后在相同区域生成持续 1 秒的半透明绿色毒气。毒气生成的瞬间攻击即结束，动画恢复 Idle，Mushroom 在攻击冷却期间明确回到 Patrol 并自由移动；毒气由独立协程继续存在和判定，同一次毒气只会命中同一目标一次。
3. **Skeleton**：目标进入最大攻击范围后连续执行半径 3.5、5、6.5 的三段 105° 扇形斩击。每段完全复用 Orc 的填充扇形预警：深红扇形标明最终范围，亮红扇形从原点按比例向外扩散；每段均从第一帧播放 `AttackOne`，并按该动画的实际帧数等待白色斩击特效完整结束。每刀之后明确切回 Idle，前两刀再经过 Idle 间隔后进入下一刀，第三刀不会被提前切掉末帧。
4. **预制体保存**：`Mob_Mushroom.prefab` 和 `Mob_Skeleton.prefab` 直接保存攻击组件、动画引用及状态机引用；运行时只创建短暂的预警和攻击特效，不动态补挂核心组件。
5. **第二关替换**：`Stage2MobCombatBuilder` 仅在编辑器替换时按房间分别打乱并交替分配，将 Orc 数量平均分给 Mushroom 与 Skeleton（奇数时 Mushroom 多一只）。结果以具体预制体实例保存在场景中，运行时不再随机；原位置、旋转、层级、血条和外部敌人引用都会保留。重复执行不会改变已经替换的结果。
6. **可重复构筑**：菜单 `Tools > Stage 2 > Build Mushroom and Skeleton Combat` 写入预制体并完成关卡替换；对应 Validate 菜单检查场景中 Orc 为零、两种怪物数量差不超过一，且所有攻击组件均已连接状态机。统一敌人目录构筑器也包含这两套配置，日后重建敌人资源不会将攻击清除。

运行路径：

```text
Hero enters detection range
  -> MobStateMachine: Chase
  -> target enters AttackRange
  -> MobStateMachine: Attack
  -> MushroomPoisonAttack: warning -> circular slash -> 1 s poison
     or SkeletonTripleSlashAttack: small -> medium -> large sector slash
  -> cooldown
  -> Chase / Idle
```

## 第二关 Medieval King Boss 实现管线

`stage2_full` 的竞技场继续复用第一只 Boss 的入场、血条、剧情、胜利、相机与节点寻路系统，但将场景中原 Evil Wizard 实例的内部视觉和攻击组件就地替换为 Medieval King；`stage1 boss` 与 `Boss_EvilWizard.prefab` 不会被修改。

1. **场景引用保持**：`KingBossBuilder` 解包第二关的 Boss prefab 实例但保留原 Boss GameObject 和 `EnemyHealth`，因此 `BossArenaController.bossRoot`、`BossHealthBarController.bossHealth`、胜利界面及剧情引用不会断开。
2. **动画驱动**：`BossSpriteAnimator` 新增 `Attack3` Clip；KingVisual 直接保存 Idle、Run、Attack1、Attack2、Attack3、Take Hit、Death 的 sliced Sprite 数组。三招在技能充能阶段逐帧推进到 releaseFrame，结算时播放剩余动作，分别固定使用 Attack1、Attack2、Attack3；针对资源的左下角 Pivot，朝向翻转时还会补偿水平位移，避免模型左右跳动。
3. **横斩**：`KingHorizontalSlashPattern` 使用 `heroY + Rigidbody2D.linearVelocity.y × warningDuration` 计算竖直预测位置。前摇的前 50% 持续读取玩家当前位置和速度、更新预测矩形；进入后 50% 时锁定最后一次预测结果，直到攻击结算都不再移动。
4. **上捞**：`KingUppercutArcPattern` 以 King 为圆心生成 240° 白色优弧扇形。Boss 在进入 Cast 前锁定朝向，扇形主体朝面向侧、120° 缺口固定在背后，前摇期间不再跟随玩家；结算通过半径与夹角直接判断伤害。
5. **下劈**：`KingGroundCleavePattern` 使用 Ground 层向下检测地面，将刚刚落地的位置提交为攻击后的新移动锚点，再生成面向侧 245×32 单位的白色巨型矩形。矩形的近侧边中点严格落在 King 中心，竖直范围以 King 的 Y 坐标向上、向下各覆盖一半，不再只偏向角色上方；水平长度对应第二关竞技场约一半宽度。
6. **逐招换位**：`EnemyAttackController` 在每次完整攻击结算后通知保存于 Boss 根对象的 `BossTeleport`。该组件现已抽象为两种换位模式，但保留原类名和资源 GUID，避免破坏第一关已有引用；第二关 King 把 `attacksPerRelocation` 保存为 1，因此每出完一招都会跳向远离 Hero 的节点，攻击冷却仍在换位结束后完整计算。
7. **复用跳跃追逐**：Evil Wizard 继续使用 `Blink`，King 则在 `stage2_full` 中直接保存同一换位组件的 `Jump` 配置。Jump 不再自行复制曲线或随机挑选落点，而是调用 `EnemyPlatformNavigator.RetreatHopRoutine`，共享原追逐系统的节点收集、连边限制、A*、`navigationSpeed`、`minimumHopDuration`、`jumpHeight` 与 `Rigidbody2D.MovePosition` 抛物线实现。
8. **远离 Hero 选点与加速**：国王以当前位置最近节点为 A* 起点，遍历所有可达节点并选择距 Hero 最远者，只执行该撤离路径的下一段，因此不会跳过墙体或跨越不相连的平台。`jumpSpeedMultiplier` 保存为 2.5，Navigator 的 `navigationSpeed / jumpHeight / minimumHopDuration` 保存为 `60 / 10 / 0.26`；持续时间和最短跳跃时间都会按倍率缩短。落地后重建普通追逐路径，移动期间攻击状态阻止 `FixedUpdate` 与显式跳跃争抢刚体。
9. **保存式组件**：国王的换位组件、Jump 模式、攻击计数、速度倍率和受击闪白引用都直接保存在场景中，不在运行时临时补挂；运行时只创建一次短生命周期的跳跃协程。旧 `WizardVisual`、旧巫师攻击和蓝色占位 Mesh 会被删除；`Entity_VFX.targetRenderer`、`BossStateMachine.animator`、`StoryDialogueController.bossVisualRoot` 都直接保存为 KingVisual 的对应组件。
10. **国王强化与可重复构筑**：场景保存最大生命 3000、单次伤害 18、攻击间隔 1.4、索敌距离 75、受击硬直 0.18，并把重新寻路距离提高到 10。菜单 `Tools > Boss > Replace Stage2 Boss With King` 会幂等写回这些平衡值和逐招 2.5 倍 Jump；`Validate Stage2 Medieval King` 同时校验动画、四招、剑气、节点跳跃、移动/生命/攻击/剧情/受击引用，并确认第一关 Evil Wizard 的 Blink 不受影响。

运行路径：

```text
Hero enters stage2 arena
  -> original BossArenaController enables the preserved bossRoot
  -> EnemyAttackController selects a range-valid King pattern
  -> BossStateMachine plays the pattern's fixed Attack1 / Attack2 / Attack3 clip
  -> white telegraph -> geometric hit check -> white strike fade
  -> completed attack counter +1
  -> every attack: find farthest reachable node from Hero -> take next A* step
                   -> 2.5x navigator parabolic hop -> reset pursuit A* -> full cooldown
  -> story companion Orc disappears when the introduction ends
  -> King is defeated -> original story / victory / return flow
```

## 小怪血条实现管线

1. **持久图形资源**：血条底槽与当前血量统一引用 `Assets/Resources/AttackHitboxes/AttackSquare.png`。不再把 `SceneArt.SquareSprite` 这种运行时 Texture/Sprite 写入 prefab，避免重载后序列化为 `fileID: 0`。
2. **预制体保存**：Orc、Goblin、Mushroom、FlyingEye、Skeleton 五个 prefab 各保存且只保存一个 `EnemyHealthBar`，包括 `Capacity`、`Fill Anchor/Current`、跟随目标、填充 Transform 以及 `CombatHealth.worldHealthBar` 引用。
3. **现有场景迁移**：构筑器修复 `stage1_full` 与 `stage2_full` 的现有 prefab 实例；如旧关卡残留了额外的场景血条，优先保留 prefab 自带血条并删除同一怪物下的重复项。
4. **后续重建**：`DemoSceneBuilder` 创建场景血条时直接使用持久 Sprite；`EnemyContentBuilder` 重建敌人目录后会重新执行 prefab 血条构筑，因此不会再次丢失。
5. **可重复构筑**：菜单 `Tools > Enemies > Repair Prefab Health Bars` 执行修复；`Validate Prefab Health Bars` 校验五个 prefab 的唯一血条、Sprite、跟随目标及生命组件引用。

## 国王圆斩、旋转剑气与攻击反馈管线

第二关国王现在拥有第四种独立攻击模式：以自身为圆心的大范围圆斩，并向十二个等分方向发射旋转、持续加速的白色剑气。该招固定使用 `Attack2` 动作；音效按动画动作而不是技能数量分槽，因此圆斩剑气与原上捞共同使用 Attack2 音效槽。

1. **招式选择与动作**：`KingRadialBladeBurstPattern` 与横斩、上捞、半场下劈一起作为保存于 `stage2_full` 国王根对象上的第四个 `EnemyAttackPattern`。有效距离为 0–250、默认权重 1.05，`castAnimation` 固定为 `Attack2`，会继续参与现有的距离过滤、加权随机、避免连续重复以及每次攻击后的跳跃换位流程。
2. **圆斩预警与伤害**：前摇持续 1.15 秒，白色圆形填充从中心扩张至 28 单位半径；结算时按 Hero 与国王中心的距离进行一次范围伤害，并生成内外两道白色圆环作为短暂斩击残影。预警会在国王前摇期间保持以攻击锚点为中心，不追踪 Hero。
3. **十二向高速螺旋散射**：结算帧把 360° 等分为十二个初始角度，每枚剑气以攻击释放点为固定圆心、以圆斩边缘 28 为初始半径生成。径向初速 18、径向加速度 42、绕圆心角速度 360°/s、寿命 7 秒，参数直接保存在攻击组件中并可由 Inspector 调节。每帧同时增大半径与极角，从而形成更快且覆盖更远的外扩螺旋；剑气图形只对齐螺旋路径的瞬时速度，不围绕自身自转，也不会追踪移动后的国王或玩家。
4. **纯白尖锐弧形**：`KingBladeWave_White.mat` 是独立保存的纯白 `Sprites/Default` 材质。`KingBladeWaveProjectile` 在实例初始化时生成一条弯曲带状网格：中心线使用抛物弧，宽度使用 `sin(πt)` 从中央向两端收缩到零，形成“一段白色圆环、两端尖锐”的轮廓；端点直接写入零宽度，内部采样先 `Clamp01` 再做非整数幂，避免浮点负零产生 NaN 和 `abnormal mesh bounds`。相同有限轮廓同时写入 `PolygonCollider2D`，因此画面和命中形状一致。
5. **剑气碰撞与穿墙**：剑气 prefab 直接保存 `MeshFilter`、`MeshRenderer`、触发式 `PolygonCollider2D`、Kinematic `Rigidbody2D` 和 `KingBladeWaveProjectile`。命中 Player 后通过 `EnemyAttackContext.HitHero` 走国王统一伤害计算并销毁；墙体、Tilemap Collider 和其他场景几何不会消耗剑气，只有命中 Hero 或 7 秒寿命结束才会销毁。
6. **三动作音效加载器**：国王根对象直接保存 `KingAttackAudio` 与 `AudioSource`，三个 Inspector 槽依次对应 Attack1、Attack2、Attack3。当前三个 AudioClip 刻意保持为空，后续只需把音频拖入槽位；所有国王技能在实际结算帧统一调用 `FireFeedback`，由当前技能的 `CastAnim` 选择并 `PlayOneShot`，不会因第四个技能再增加重复音频槽。
7. **屏幕抖动**：探索相机与 Boss Arena Camera 都继续使用场景中已保存的 `CameraShake2D`。攻击结算不再只使用国王启动时缓存的探索相机，而是在每次出手时从当前 `Camera.main` 获取抖动组件；因此进入 Boss 房切换相机后，横斩、上捞、下劈和圆斩剑气都会抖动实际正在显示的镜头。
8. **可重复构筑与验证**：菜单 `Tools > Boss > Replace Stage2 Boss With King` 会生成/更新白色材质与剑气 prefab，重建四个攻击组件，保留将来已经填入的三个音效 Clip，并把音频加载器引用保存给 `EnemyAttackController`。`Validate Stage2 Medieval King` 会检查四招及动作映射、十二发数量、圆斩尺寸、剑气 prefab/白材质、两个相机的抖动组件、音频加载器和原有跳跃换位，同时确认第一关 Evil Wizard 未被改动。

运行路径：

```text
EnemyAttackController selects King Radial Blade Burst
  -> BossStateMachine channels Attack2
  -> expanding circular warning (1.15 s)
  -> radius-28 melee hit + white double-ring slash
  -> 12 saved KingBladeWave prefab instances
       -> tapered white arc mesh + matching polygon collider
       -> angular orbit around release origin + outward radial acceleration
       -> ignore walls; hit Hero / lifetime -> destroy
  -> active MainCamera shake + Attack2 audio slot
  -> every-attack retreat hop / normal cooldown
```

## 第二关多演员剧情与 Boss 单目标胜利管线

国王房的剧情演员直接保存在 `stage2_full`。巫师与 Orc 都只承担入场演出；剧情结束、正式开战时 Orc 会消失，最终胜利只要求击败国王。

1. **保存式演员**：场景根对象 `Boss Introduction Cast` 保存 `Story Evil Wizard Idle_0` 和完整 `Mob_Orc.prefab` 实例。两名演员初始均为 inactive；核心组件不会在运行时补挂。
2. **巫师仅作演出**：Builder 从 `Boss_EvilWizard.prefab` 的 `BossSpriteAnimator.idle.frames[0]` 取得准确的 `Idle_0`，场景演员只保存 `SpriteRenderer`，没有 Collider、生命、AI 或攻击组件，因此不会与国王视觉重叠，也不会被误判为战斗目标。
3. **Orc 仅作演出**：第 12 句开始前隐藏巫师并激活 `Boss Companion Orc`，让怪物台词仍显示在 Orc 自己头顶；最后一句结束后，`actorsHiddenAfterBossIntroduction` 同时关闭巫师和 Orc。跳过已经看过的剧情时也会直接应用相同状态，不会让 Orc 短暂进入战斗。
4. **逐句演员提示**：`bossIntroductionActorCues` 在显示指定行之前执行。当前 cue 为第 4 句显示巫师、第 12 句隐藏巫师并显示 Orc；剧情收尾统一隐藏两名临时演员，只留下 Hero 与 King。
5. **独立气泡**：`Wizard Story Dialogue` 与 `Orc Story Dialogue` 都是保存于 `Dialogue Bubbles` 下的 `WorldDialogueBubble.prefab` 实例，分别跟随各自演员。Samurai、King、EvilWizard、Monster 四类 speaker 均解析到不同气泡，剧情结束时统一隐藏。
6. **单一胜利目标**：第二关国王的 `EnemyHealth.victoryObjective` 为空，场景不再挂载 `BossEncounterObjective`。国王死亡后直接进入现有双语胜利剧情、黑幕、结尾漫画与 Victory/团队分工界面，剧情 Orc 的生命状态完全不参与结算。
7. **中英存储**：两关英文台词仍由 `StoryChapterBuilder` 写入场景，简体中文逐句写在 `LocalizationTable`；`ValidateTranslations` 会枚举两章全部 48 句，拒绝缺失、空白或仍与英文相同的翻译。
8. **幂等构筑**：菜单 `Tools > Narrative & Audio > Build Stage2 Boss Cast and Localization` 会删除旧 `Boss Introduction Cast` 后重建演员、气泡和 cue，并清除旧联合目标引用，但保留场景里当前 Hero、King、地图和宝箱位置。随后验证 Idle_0、完整 Orc、四气泡、三个 cue、开战隐藏状态、King 单目标结算与 48 句本地化。
9. **自动测试**：`KingRadialStoryObjectivePlayModeTests` 验证剑气网格全部顶点与 Bounds 有限、剑气确实向外加速并旋转、半场攻击上下对称、四名说话者路由正确、中文翻译生效、开战时 Orc 消失，以及只击败 King 即可结束战斗。
10. **第二关漫画显示**：`Story Comic Panel` 作为 active 的 Screen Space Overlay Canvas 保存于场景，隐藏只通过 `CanvasGroup.alpha` 完成，避免 inactive Canvas 无法重新渲染。第一关与第二关 Boss 入场分别使用 `BossIntroduction`、`Stage2BossIntroduction` 两个 `StoryBeat`；第一关的历史进度不再跳过第二关背叛漫画，完整流程进入第二关时会清理第二关入场标记，而第二关死亡重试仍不会重复播放已经看过的剧情。

运行路径：

```text
Hero enters the stage2 boss arena
  -> King appears
  -> line 4 cue: show Wizard Idle_0 -> Wizard bubble speaks
  -> betrayal comic
  -> line 12 cues: hide Wizard + enable full Orc -> Orc bubble speaks
  -> dialogue ends -> Wizard and Orc disappear
  -> King fights alone
  -> King EnemyHealth is defeated
  -> bilingual boss-victory dialogue -> black fade
  -> Comic_Epilogue panels 1..4 (Enter)
  -> final Victory / team contributions screen
```

## WebGL 开始菜单中文字体管线

开始菜单的 Legacy UI 不再依赖 Unity 内置 `LegacyRuntime.ttf`。该字体在 Windows 编辑器中可能借用操作系统的中文字体回退，但 WebGL 浏览器构建无法访问相同的系统字形，因此 `START`、`HELP` 翻译为中文后曾显示为空白。

1. **随包字体**：英文界面使用 `Assets/Resources/Fonts/BoldPixels.ttf`，中文界面使用 `Assets/Resources/Fonts/ZCOOLXiaoWei-Regular.ttf`；两份字体数据的 `includeFontData` 均保持启用，因此 WebGL Player 会把实际字形来源包含在构建中。
2. **保存式场景配置**：`Start Label`、`Help Label`、标题、开发者名称及设置/制作名单面板文本的字体引用直接保存在 `StartMenu.unity`，不会在运行时临时创建或替换组件。
3. **语言切换**：`LocalizedText` 在把英文 key 变换成 `LocalizationTable` 中文本的同时切换字体：英文选择 BoldPixels，中文选择站酷小薇体。`START -> 开始游戏`、`HELP -> 帮助` 与已有的 `SETTING / CREDIT` 走同一条路径。
4. **幂等构筑**：`Tools > Start Menu > Build Settings And Credits` 会扫描开始菜单根对象下所有 active/inactive Legacy Text，并统一写入随包字体；以后重建或新增按钮也不会重新引入依赖系统字体的引用。
5. **验证**：`Validate Start Menu` 会检查字体资源存在、字体数据会进入构建、所有菜单 Text 都引用该资源，并确认“开始游戏帮助设置制作名单”所需字形齐全；PlayMode 测试额外检查 Start/Help 的实际字体引用。

```text
StartMenu scene loads
  -> LocalizedText reads English key
  -> LocalizationTable returns Chinese text
  -> UiFont selects BoldPixels or ZCOOL XiaoWei
  -> identical result in Editor, Windows Player and WebGL
```

## WebGL 全流程本地化与漫画首帧管线

WebGL Player 无法像 Windows 编辑器一样从操作系统借用中文字形，因此只修复开始菜单不足以覆盖 Help、剧情气泡、关卡提示、背包和锻造界面。本地化渲染现在统一经过以下管线：

1. **Legacy Text**：`LocalizedText` 在处理每个 `UnityEngine.UI.Text` 时读取 `UiFont.Current`。英文绑定随包的 BoldPixels，中文绑定随包的站酷小薇体；旧场景即使仍保存 `LegacyRuntime.ttf`，首帧也会切换到正确字体。
2. **TextMeshPro**：两份源 TTF 分别生成 `BoldPixels SDF.asset` 与 `ZCOOLXiaoWei SDF.asset`。它们使用 Dynamic Atlas，并保留源 TTF 于 Resources；英文 TMP 还把中文 TMP 设为 fallback，以覆盖夹杂中文的文本。
3. **Help 场景**：标题、正文和返回按钮的 BoldPixels 引用直接保存在 `Help.unity`；切换中文后由 `LocalizedText` 同步替换文字与字体。Help 正文的英文源文本与 `LocalizationTable` 使用完全一致的折叠空白 key，因此整页可一次性翻译，返回按钮显示“返回”。
4. **构筑与检查**：`Tools > Localization > Build Dual Language Fonts` 从下载目录导入两份源字体及许可证，创建 TMP 资产，并把英文字体保存到 StartMenu、Help 和 UI Prefab；`Validate Dual Language Fonts` 检查 TTF 数据、动态 TMP 字体、fallback 和保存式引用；`Build WebGL Localization Smoke Player` 会使用 Build Settings 中的完整场景列表在 `Builds/CodexWebGLLocalization` 生成 Development WebGL Player。
5. **漫画首帧**：`StoryComicPanel.ShowPanel` 会重新启用 `RawImage`、标记 Canvas 为 dirty 并强制刷新。剧情协程在每一格显示后等待一个 `WaitForEndOfFrame`，保证 WebGL 完成大纹理上传和至少一次绘制后才开始监听 Enter，避免第一格逻辑存在但视觉为空。

```text
Language changes to Chinese
  -> LocalizedText selects bundled legacy/TMP font
  -> LocalizationTable translates the stable English source
  -> WebGL renders shipped CJK glyphs

Comic panel 0 is assigned
  -> RawImage enabled + Canvas dirtied
  -> Canvas forced to update
  -> one end-of-frame render completes
  -> Enter may advance to panel 1
```

## 符文平衡、锻造显示与国王落地管线

本轮把角色数值、成长数据显示和第二关 Boss 的物理位置统一到各自的唯一数据源，避免 Inspector、背包详情和战斗实际效果彼此不同步。

1. **红色符文平衡**：`Role` 统一保存红色符文倍率，装备后移动速度与跳跃速度乘以 `1.1`，冲刺速度乘以 `1.3`。`Rune_Crimson.asset`、中英文详情文本以及相关构筑器同步描述为“移动/跳跃 +10%，冲刺 +30%”；编辑器验证器也使用同一组目标值，重复构筑不会恢复旧数值。
2. **锻造等级作为唯一进度**：`RunProgress` 保存武器、盾牌和绿色符文的锻造等级。等级真正变化时触发 `Changed`，背包、已装备栏和锻造界面都订阅该事件并立即重绘，不要求玩家关闭后重新打开面板。
3. **统一装备展示计算**：`ItemDisplay` 根据 `ItemData` 与 `RunProgress` 生成本地化名称和实际数值。未锻造时显示基础名称；锻造后自动显示 `+N`，例如基础 10 ATK 的巨剑锻造一次显示 `Claymore Sword+1` 与 `20 ATK`。武器每级增加 10 ATK、盾牌每级增加 2 DEF、绿色符文每级增加 2 HPS，详情面板、装备槽和锻造槽不再各自复制一套计算。
   盾牌的基础值直接来自 `Armor_Plate.asset`（6 DEF），所以 +1 为 8 DEF；绿色符文基础为 2 HPS，所以 +1 为 4 HPS。锻造面板启用及 `RunProgress.Changed` 触发时直接重读三种等级，不再依赖 `PlayerProgression.Awake` 的执行顺序，因此跨场景打开面板也能立即显示正确的 `+N` 与数值。
4. **国王的两阶段刚体控制**：国王平时保持 `Dynamic Rigidbody2D`、连续碰撞、冻结旋转及可调的下落重力。节点跳跃期间暂时把重力设为零并由抛物线位移控制；到达目标节点后立刻恢复重力，并等待地面碰撞，使节点存在少量高度误差时也能自然落到平台。
5. **攻击不再回写旧锚点**：动画驱动的国王在攻击结束时只清理动画状态，不再把根对象恢复到攻击开始前缓存的位置。这样攻击期间的重力落地结果和上一招后的节点换位都会保留，Idle 与 Attack 之间不会发生浮空、落地或瞬移跳变。
6. **换位与攻击互斥**：每招后的显式节点跳跃会标记为独立换位流程。攻击控制器处于忙碌状态时，普通追逐跳跃会被取消，但显式换位不会被 `FixedUpdate` 提前恢复重力或打断；完成弧线后再交还给动态刚体与普通寻路。

```text
Forge completes
  -> RunProgress level changes
  -> Changed event
  -> ItemDisplay recalculates localized name + effective stats
  -> inventory / equipment / forge panels redraw

King finishes an attack
  -> explicit retreat hop (gravity temporarily 0)
  -> reaches selected navigation node
  -> Dynamic gravity restored
  -> collider contacts Ground
  -> Idle / next Attack keeps the landed transform
```

## 漫画、物品详情、Boss 落地与死亡清场修复管线

本轮将四个表面上互不相关的问题收束到“资源准备—唯一数值源—运行时对象所有权—物理落点”四条明确管线中，避免依靠场景帧序或固定模型尺寸碰运气。

1. **漫画首格预热**：`StoryComicPanel.Prepare` 在画面仍为透明时绑定完整漫画纹理、提交左上象限 UV、显式取得原生纹理句柄并刷新 Canvas。`StoryDialogueController` 先让隐藏画面完成一次帧末上传，再显示第一格；每格显示后下一帧重新提交一次纹理并等待帧末绘制，之后才开始接收 Enter。这样 WebGL 不会出现“逻辑停在第一格、纹理却要到第二格才出现”的情况。
2. **物品名称显示**：`ItemDisplay.LocalizedName` 以 `itemName`、资源名和 `Unnamed Item` 依次兜底，本地化表的空结果不能再擦除名称。`ItemDetailPanel` 每次绘制都会重新绑定当前语言对应的 BoldPixels / 站酷小薇体、请求当前文字字形并标记文字网格为 dirty；标题允许纵向溢出，避免两种字体度量差异在 WebGL 中裁掉整行。中英文名称与 `+N` 锻造后缀走同一接口。
3. **巨剑唯一数值源**：`Weapon_Claymore.asset` 与 `EquipmentBuilder` 的基础攻击统一为 10 ATK；`RunProgress` 每锻造一级增加 10，因此 +1 必须同时在背包、装备栏、锻造界面和实际伤害中得到 20 ATK。构筑器与持久化资源不再分别保留 18 和 10 两套初始值。
4. **Boss 导航点贴地**：`EnemyPlatformNavigator.RefreshNodes` 收集场景中保存的导航点后，从每个点向 Ground 层探测实际平台表面，再按当前 Boss 碰撞体底部到根节点的真实距离修正 Y 坐标。Evil Wizard 与四倍尺寸的 Medieval King 可以复用同一套节点拓扑，同时保证脚底刚好落在平台上；跳跃结束后仍恢复动态重力完成最终接触。
5. **攻击特效所有权**：所有 `EnemyAttackPattern` 统一登记其预警、命中框、扇形 Mesh、激光、弹幕容器与国王剑气。Boss 死亡时 `EnemyHealth` 先清除这些受管对象，再禁用攻击模式并停止控制器协程；组件被禁用或销毁时也执行相同兜底，因此第一关巫师和第二关国王都不会把攻击残影留到 Victory 画面。
6. **验证**：`InventoryItemDetailPlayModeTests` 检查中英文标题确实生成文字网格、巨剑 10→20 ATK；`StoryChapterPlayModeTests` 检查首格纹理与左上 UV 已提交；`KingRadialStoryObjectivePlayModeTests` 检查国王节点按碰撞体贴地，并在两关分别模拟 Boss 死亡验证预警与弹体清除。

```text
Comic / item UI
  -> prewarm bundled texture or font glyphs
  -> bind saved UI component
  -> rebuild Canvas geometry
  -> reveal to player

Boss navigation / death
  -> saved navigation topology -> raycast platform -> collider-correct landing Y
  -> pattern spawns tracked effect -> Boss death -> clear effects -> stop attacks -> victory flow
```

## 国王攻击音效与最高层角色对话框管线

本轮把音效“已经绑定但首次攻击无声”和世界空间对话框会被 HUD 覆盖的问题，分别收束到资源预加载与屏幕空间坐标转换两条稳定管线中。

1. **三动作音效映射**：`KingAttackAudio` 仍由场景直接保存 `Attack1 / Attack2 / Attack3` 三个音频引用，`EnemyAttackController.FireFeedback` 按当前攻击模式的 `CastAnimation` 选择对应音频，不在运行时创建加载器或 `AudioSource`。
2. **WebGL 音频准备**：三个 Boss WAV 的导入设置启用 `preloadAudioData` 并关闭 3D 音效；组件 `Awake` 再主动请求音频数据。攻击发生时若浏览器还在解码，协程最多等待 3 秒并在数据进入 `Loaded` 后播放，避免首招的 `PlayOneShot` 被静默丢弃。
3. **固定 2D 播放源**：国王的场景 `AudioSource` 被统一配置为非循环、非静音、`spatialBlend = 0`、忽略 Listener 暂停，并在 Boss 被禁用或死亡时停止声音与等待协程。
4. **Overlay 对话结构**：`WorldDialogueBubble.prefab` 的根节点是 `Screen Space Overlay` Canvas，排序值固定为 Unity 可用的最高值 `32767`；其下保存一个固定尺寸的 `Bubble Root`，白底文本和 Enter 提示都是预制体内已有组件。
5. **跟随人物**：`WorldDialogueBubble.LateUpdate` 通过当前 `Camera.main.WorldToScreenPoint` 把人物头顶的世界坐标转换到 Overlay Canvas 本地坐标，并把气泡限制在屏幕边缘以内。切换普通镜头和 Boss 镜头后不需要替换气泡或额外创建组件。
6. **构筑与验证**：菜单 `Tools > Narrative & Audio > Rebuild Dialogue Prefab Only` 只重建对话预制体，不改写关卡；`KingRadialStoryObjectivePlayModeTests` 分别验证三段音频都能到达 `AudioSource`，以及四名剧情角色的对话 Canvas 均高于其他 UI。

```text
King attack begins
  -> CastAnimation selects saved WAV
  -> already loaded: PlayOneShot immediately
  -> still decoding: wait with unscaled time -> PlayOneShot

Dialogue line begins
  -> character world position + head offset
  -> active main camera converts to screen position
  -> clamp Bubble Root inside viewport
  -> Overlay Canvas order 32767 draws above every HUD panel
```

## Stage2 三层视差背景管线

第二关使用独立的 `Stage2Background.prefab`，直接保存于 `stage2_full` 场景中。它复用第一关的摄像机位移驱动方式，但按新素材的三张分层图建立真正的远、中、近景视差。

1. **素材导入**：`Assets/Textures/Background/Stage2/1.png`、`2.png`、`3.png` 分别作为天空、远山云层和近景云层。构筑器把它们统一导入为 32 PPU、Point Filter、Full Rect、无 Mipmap 的单张 Sprite，透明中前景不会产生黑边。
2. **保存式预制体**：每个图层由中心、左侧、右侧三个 `SpriteRenderer` 构成，全部保存在 `Stage2Background.prefab` 内，不在运行时临时创建。三层缩放均为 12，在保持原图宽高比的同时，为探索镜头的纵向移动和更高的 Boss 镜头预留足够 overscan；排序值依次为 `-120 / -110 / -100`。九个 Renderer 全部固定在 `background` Sorting Layer，避免中心天空遮住远离原点时应当出现的左右副本。
3. **跟随与景深**：天空使用水平/垂直 `1.0 / 1.0`，始终铺满镜头；中景使用 `0.92 / 0.98`，前景使用 `0.78 / 0.95`，角色移动时产生克制的横向视差，同时纵向不会轻易露出空白。
4. **无限横向覆盖**：`ParallaxLayer` 根据 Sprite 的世界宽度循环移动三联画根节点。循环判断以摄像机中心越过“中心图块边界”为时机，而不是等中心图完全离开视口后才补图，因此即使 Boss 镜头比单张背景更宽，也不会在三联画末端短暂露出空白；循环使用 `while` 校正，传送跨越多个图宽同样有效。左右副本在预制体中水平镜像，使并非为平铺设计的山云素材以相同边缘相接，消除硬接缝。
5. **纵向防穿帮**：各层照常按独立纵向倍率制造景深；一旦摄像机上沿或下沿将越过图层，`KeepVerticalCoverage` 只补偿会露边的部分。这样保留绝大多数视差行程，又不会在地图最高点、最低点或切入 Boss 房时看到背景边界。
6. **镜头切换**：`ParallaxBackground` 每帧重新识别当前 `Camera.main`。普通探索镜头切换到 `Boss Arena Camera` 后，位移计算、横向循环和纵向覆盖检查一起交给 Boss 镜头。
7. **构筑与验证**：菜单 `Tools > Background > Build Stage2 Parallax Background` 负责导入素材、重建预制体并只替换 `stage2_full` 的背景；`Rebuild Stage2 Parallax Prefab Only` 可在保留场景手工修改的情况下只刷新 prefab。对应 PlayMode 测试验证三张素材、九个 Renderer、三组视差倍率、Boss 镜头切换及完整视口覆盖。

```text
Active camera moves
  -> ParallaxBackground reads camera displacement
  -> sky moves 100%
  -> middle layer moves 92% / 98%
  -> foreground moves 78% / 95%
  -> horizontal triplets wrap until camera is covered
  -> vertical edge guard adds only the correction needed to keep the viewport covered

Boss arena camera becomes Camera.main
  -> tracker changes camera reference
  -> same displacement pipeline continues
```

## 黑蓝石板菜单按钮管线

开始菜单、帮助页返回按钮与游戏内暂停菜单现在共用一套日式武士 / 西方暗黑奇幻风格的黑蓝石板按钮。按钮外框使用锻铁转角与铆钉，石板表面保留刀痕；文字仍由原有本地化系统独立渲染，因此更换按钮美术不会破坏中英文切换。

1. **四态素材**：`Assets/Textures/UI/MenuButtons` 保存 `Normal / Hover / Pressed / Disabled` 四张 410×100 透明 Sprite；悬停状态点亮冷蓝刀痕，按下状态使用内陷阴影，禁用状态降低对比度。所有素材均使用 Point Filter、关闭 Mipmap 并启用透明边缘。
2. **保存式交互**：每个 `Button` 的 `Transition` 保存为 `SpriteSwap`，普通图写入目标 `Image`，另外三态写入 `SpriteState`。所有引用都直接保存在 `StartMenu.unity`、`Help.unity` 与 `Canvas.prefab`，运行时不创建按钮或补挂组件。
3. **开始菜单覆盖范围**：`Start / Help / Setting / Credit` 四个入口、难度选择、设置和制作名单页面中的语言、清档及全部返回按钮统一套用石板皮肤。难度中的困难选项与可执行的清档操作只把文字染成浅红，不再给整张石板染色。
4. **帮助与暂停菜单**：`Help.unity` 的返回按钮使用相同四态皮肤；共享 `Canvas.prefab` 中的 `Resume / How To Play / Main Menu / Help Back` 同样保存完整 SpriteSwap 引用，所以两关使用同一个暂停菜单外观。
5. **本地化兼容**：按钮图不烘焙文字。Legacy `Text` 的英文使用 BoldPixels，中文使用站酷小薇体，并由 `LocalizedText` 同步更新内容与字体；皮肤构筑只统一字体颜色和粗体，不修改翻译 key 或点击事件。
6. **可重复构筑**：菜单 `Tools > UI > Apply Dark Slate Button Skin` 会幂等地刷新两个场景和共享 Canvas；`StartMenuSettingsBuilder` 与 `PauseMenuBuilder` 也会在各自重建末尾重新套用皮肤，避免后续构筑恢复白色占位按钮。
7. **验证**：`Tools > UI > Validate Dark Slate Button Skin` 检查四张 Sprite 的导入设置，以及每个目标按钮的 Normal、Hover、Pressed、Selected、Disabled 引用。开始菜单与暂停菜单原有验证器也会执行同一检查。

```text
Button sprites imported
  -> MenuButtonSkin writes Image + SpriteState into saved UI
  -> EventSystem changes Selectable state
  -> SpriteSwap selects Normal / Hover / Pressed / Disabled
  -> separate localized Text renders English or Chinese above the sprite
```

## 双语像素标题管线

开始界面的标准字体标题已替换为一张直接保存于场景的双语像素 Logo。英文主标题分成 `A THOUSAND` 与 `BATTLES LATER` 两行，使用略微前倾、带弧线和刀锋收笔的银灰锻铁字；较小的中文副标题“武者之誓”位于下方，并由弯曲刀锋承托。整体继续沿用按钮的黑蓝石板、冷色金属与刀痕语言。

1. **透明标题素材**：`Assets/Textures/UI/Title/Title_AThousandBattlesLater.png` 为 1200×380 RGBA 图，使用 Point Filter、关闭 Mipmap、Clamp 与无压缩 Sprite 导入设置；标题外部保持透明，不遮挡月夜城堡背景。
2. **场景保存式组件**：`StartMenu.unity` 中原 `Game Title` 对象保留，但标准 `Text` 被替换为 `Image`，Sprite、1200×380 尺寸与 `(0, 250)` 位置全部直接保存在场景中。运行时不创建标题，也不依赖系统字体或 WebGL 字形回退。
3. **重建安全**：`Tools > Start Menu > Apply Bilingual Pixel Title` 可幂等地重新导入并部署标题；完整的 `DemoSceneBuilder.BuildStartMenuScene` 在保存新场景前也会执行同一转换，后续重建不会恢复旧字体标题。
4. **验证**：`Validate Bilingual Pixel Title` 检查场景中只存在标题 `Image`、没有旧 `Text`，并验证 Sprite 引用、透明导入设置、尺寸和位置；完整开始菜单验证器也复用该检查。

```text
Generated bilingual RGBA title
  -> Point-filter Sprite import
  -> StartMenuTitleBuilder replaces saved Text with saved Image
  -> CanvasScaler positions 1200x380 logo at (0, 250)
  -> Editor / Windows / WebGL render the same baked title artwork
```

## 项目文件结构与整理管线

当前工程采用“正式运行资源、开发期产物、编辑器构筑工具”分层的目录结构。整理过程通过 Unity `AssetDatabase` 移动资源并保留每个 `.meta` GUID，因此场景、Prefab、材质与脚本引用不会因为文件换位而失效。`Resources`、角色/敌人 Prefab 和地图 Tile 仍保留原位：这些目录分别受运行时加载路径、场景引用和 Tilemap 工作流约束，没有为了外观整齐而进行高风险迁移。

```text
Assets/
├─ Animations/                 # 正式动画与 Animator Controller
├─ Audio/                      # BGM 与 SFX
├─ Development/               # 构筑器生成、可重复生成的开发资源
│  ├─ GeneratedAttackDemo/
│  └─ GeneratedUI/
├─ Editor/Tools/               # 只在 Unity Editor 中运行的构筑与验证工具
│  ├─ Combat/                  # 敌人、Boss、攻击与血条构筑
│  ├─ Flow/                    # 场景、关卡流程与数值流程
│  ├─ Inventory/               # 物品、装备、符文与背包
│  ├─ Narrative/               # 剧情、漫画与音频
│  ├─ UI/                      # 菜单、HUD、锻造与本地化 UI
│  └─ World/                   # 地图、背景、摄像机、宝箱与门
├─ Enemy/                      # Mob 与 Boss 的正式资源
├─ Material/                   # 正式材质
├─ Prefab/                     # 场景共用正式 Prefab
├─ Resources/                  # 保持运行时 Resources.Load 路径稳定
├─ Scenes/
│  ├─ StartMenu.unity          # 正式流程入口
│  ├─ stage1_full.unity        # 第一关
│  ├─ stage2_full.unity        # 第二关
│  ├─ Help.unity               # 操作说明
│  └─ Legacy/                  # 不进入正式 Build 的旧原型场景
├─ Scripts/                    # 运行时代码，按系统职责分组
├─ Tests/                      # EditMode / PlayMode 验证
└─ Textures/
   └─ Background/
      ├─ Stage1/               # 第一关城堡与菜单背景素材
      └─ Stage2/               # 第二关三层视差背景素材
```

整理与维护流程如下：

1. **边界审计**：先读取 Build Settings 和代码中的硬编码路径，确认 `StartMenu → stage1_full → stage2_full → Help` 是正式场景集合；旧 `stage1`、`stage1 boss` 与 `New Scene` 只作为历史原型保留。
2. **GUID 安全移动**：菜单 `Tools > Project > Organize Asset Structure` 调用 `ProjectStructureBuilder.Build`。脚本先创建全部目标目录并同步刷新 Asset Database，再移动生成资源、背景素材、旧场景和 Editor 工具；背景目录按内容迁移，以规避 Unity 6 在批处理模式中直接移动非空目录的刷新时序问题。
3. **清理确定无效内容**：删除 Unity 自动生成在 `Assets` 根目录的 `InitTestScene*.unity` 恢复场景，以及确认无内容的 `Assets/Sprites`、`Assets/Animations/Enemy`。正式场景、旧原型、导入素材和用户正在修改的资源均不做推断式删除。
4. **同步代码路径**：所有构筑器、验证器与测试中的生成资源、Legacy Scene、Stage1/Stage2 Background 路径同步到新位置。移动 Editor 脚本不会改变类型名或菜单入口，现有构筑命令仍可继续使用。
5. **结构验证**：菜单 `Tools > Project > Validate Asset Structure` 检查正式目录、Legacy Scene、两套背景和六类 Editor 工具是否齐全，并确认旧顶层目录和恢复场景已经清除；缺失任一资源会直接抛出错误，避免以不完整结构继续构筑。
6. **后续新增约定**：正式可运行资源放入对应系统目录；构筑器生成且可重建的产物放入 `Assets/Development`；废弃但仍有参考价值的场景放入 `Assets/Scenes/Legacy`；新编辑器工具按职责放入 `Editor/Tools` 的六个分类之一。

```text
Audit build/runtime paths
  -> create destination folders
  -> AssetDatabase move (preserve GUIDs)
  -> update builder/test paths
  -> remove confirmed recovery/empty folders
  -> compile
  -> Validate Asset Structure
```

## BoldPixels / 站酷小薇体双字体管线

项目不再让一份通用字体同时承担中英文美术表现。英文界面统一使用粗颗粒像素字体 BoldPixels，中文界面统一使用更适合剧情与菜单阅读的站酷小薇体；语言切换同时更新文本内容与字体资产。

1. **源字体与许可**：`BoldPixels.ttf`、`ZCOOLXiaoWei-Regular.ttf` 及各自许可证保存在 `Assets/Resources/Fonts`。TTF 的 `includeFontData` 必须开启，以保证 Windows 与 WebGL 均使用仓库内同一份字形，而不是操作系统回退字体。
2. **两套 UI 入口**：`UiFont.English / Chinese` 提供 Legacy `Text` 字体，`UiFont.TmpEnglish / TmpChinese` 提供 TMP 字体；`Current / TmpCurrent` 根据 `Localization.Current` 返回当前语言的字体。原有 `Regular` 只作为 Editor 构筑器的英文 authoring 入口保留。
3. **运行时同步切换**：`LocalizedText.Apply` 先调用 `ApplyBundledFont`，再翻译文字。这样语言事件触发时，同一帧内完成 BoldPixels 与站酷小薇体切换，不会出现中文内容配英文字体、或英文切回后仍保留中文字形度量的状态。
4. **TMP 动态字形**：构筑器从两份 TTF 分别生成 2048×2048、允许多图集的 Dynamic SDF。BoldPixels TMP 把站酷小薇体 TMP 注册为 fallback；剧情中混合的按键名、英文专名和中文句子因此不需要临时创建第三套字体组件。
5. **保存式 UI**：`StartMenu.unity`、`Help.unity`、暂停/背包/剧情对话等 UI Prefab 直接保存 BoldPixels 引用。关卡内文本由已存在的 `LocalizedText` 在加载时切换；核心字体和 UI Component 均不是运行时临时创建。
6. **可重复构筑与验证**：菜单 `Tools > Localization > Build Dual Language Fonts` 负责导入、生成 TMP、部署和 fallback 配置；`Validate Dual Language Fonts` 检查源字体随包数据、TMP 源字体与 Dynamic 模式、Start/Help 及 UI Prefab 的保存式英文引用。WebGL 冒烟构建继续使用 `Build WebGL Localization Smoke Player`。
7. **语言选择器例外**：设置页的“中文”和“English”是语言名称，不是随当前语言翻译的普通正文。两个保存于场景的标签分别挂载 `FixedLanguageFont`，固定使用站酷小薇体与 BoldPixels；`LocalizedText` 自动扫描时跳过这类标签。这样英文界面仍能显示“中文”，中文界面也不会把 “English” 换成中文字形度量，且不依赖组件事件的执行顺序。
8. **ESC 帮助页同步**：`Canvas.prefab` 中保存的 `Pause Help Body` 与独立 `Help.unity` 共用完全相同的英文源键，包含四个装备栏、绿色符文锻造以及红色符文不可锻造说明。标题、正文、返回按钮都直接在 Prefab 中保存 `LocalizedText`，打开暂停帮助时用同一组中文词条并切换到站酷小薇体，不依赖运行时临时补组件；`PauseMenuBuilder.Validate` 会同时比较 Prefab 正文、构筑常量、保存式组件与中文词条，防止旧说明静默退回英文。
9. **结算名单双语键**：第二关 Victory 的英文源文本只使用 BoldPixels 确定覆盖的 ASCII 字符，成员名保存为 `Zixuan Lu / Mincha Lu / Xiangming Meng`，院校和分工同样使用英文；`LocalizationTable` 以整块英文为键，在中文模式替换为中文姓名与中文分工并切换站酷小薇体。`StoryChapterBuilder`、`stage2_full` 与测试共用同一文本，避免场景仍保存中文姓名时英文像素字体把姓名渲染为空白。

```text
Downloaded TTF + license
  -> copy into Resources/Fonts
  -> enable includeFontData
  -> generate English / Chinese Dynamic TMP assets
  -> save BoldPixels into authored scenes and prefabs
  -> ordinary label: language changes
       -> translate English key -> UiFont selects current face -> rebuild glyph geometry
  -> language-choice label: saved FixedLanguageFont
       -> 中文 always uses ZCOOL XiaoWei / English always uses BoldPixels
  -> validate Editor / Windows / WebGL font sources
```
