# Final Test

`final test` 是当前 Unity 6（`6000.5.2f1`）工作工程。项目的完整玩法由
`Assets/Scenes/stage1_full.unity` 与 `Assets/Scenes/stage2_full.unity` 串联地图探索、宝箱奖励、装备/背包和两场 Boss 战。

## 完整战役流程管线

正式流程固定为 `StartMenu → stage1_full → stage2_full → Victory`。第一关 Boss 是中间关结算，第二关 Boss 才是整轮游戏的最终结算。

1. **开始入口**：`StartMenuController.targetSceneName` 直接保存为 `stage1_full`；Build Settings 的启用顺序固定为 `StartMenu`、`stage1_full`、`stage2_full`、`Help`，旧 `stage1` 与独立 `stage1 boss` 不再参与正式流程。
2. **中间关 Boss 结算**：第一关 `EnemyHealth.nextStageSceneName` 保存为 `stage2_full`。Boss 死亡后仍播放现有胜利剧情，但调用 `PlayBossVictory(false)`，不会启用最终 Victory 面板。
3. **进度继承**：第一关结束时不调用 `GameProgress.ClearAll`，因此背包堆叠、已装备物品、红/绿符文、能力解锁和锻造等级全部通过现有静态 Run 数据进入第二关。`StoryProgress.PrepareForNextStage` 只清除 Boss Introduction 标记，使第二关 Boss 仍能播放入场对话，其余开场和教学不重复。
4. **黑屏淡出**：第一关 Boss 剧情结束后，`EnemyHealth` 使用场景中已经保存的 `Story Fade Canvas/Black Fade`，按 unscaled time 在 1.15 秒内从透明变为纯黑；即使剧情将 `Time.timeScale` 保持为 0，淡出仍能正常完成。
5. **第二关淡入**：加载 `stage2_full` 后，启用的 `StoryDialogueController` 从全黑开始。开场剧情已经完成时不重复台词，但仍执行 1.35 秒 `FadeFromBlack`，形成连续的“第一关淡出—加载—第二关淡入”。
6. **最终胜利**：第二关 `nextStageSceneName` 为空，因此 Boss 死亡后沿用完整胜利剧情并显示 Victory 面板；按 Space 返回 `StartMenu` 时才通过 `GameProgress.ClearAll` 清除整轮背包、装备、能力、锻造、难度与剧情进度。
7. **保存式组件**：流程直接配置两个场景内已有的 `EnemyHealth`、`StoryDialogueController` 和 `CanvasGroup`，没有在运行时补挂核心组件或创建永久 UI。只有淡入淡出协程属于短生命周期运行逻辑。
8. **可重复构筑与验证**：菜单 `Tools > A Thousand Battles Later > Build Campaign Flow` 执行 `CampaignFlowBuilder.Build`，幂等写入开始目标、两关结算角色、淡出引用和 Build Settings；`Validate Campaign Flow` 检查第一关只通往第二关、第二关只显示最终 Victory、两个 Story System 已保存为启用状态，并验证四个正式场景的顺序。

```text
START
  -> stage1_full
  -> stage1 Boss victory dialogue (no Victory panel)
  -> black fade-out
  -> load stage2_full, preserve RunInventory / RunEquipment / RunProgress
  -> black fade-in
  -> stage2 Boss victory dialogue
  -> Victory panel
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
2. **开门检查**：Hero 进入 Boss 门触发器时先调用 `RunEquipment.Get(requiredRuneSlot)`。对应槽为空时取消传送并由 `PlayerProgression` 显示提示；红、绿门分别显示“大门上有一个红色/绿色的符文凹槽”。装备正确符文后再次接触门，才进入原有的相机、BGM、Boss 激活和剧情流程。
3. **第一关奖励**：保留全部三个宝箱及其编辑器位置。上方 `Supply Treasure Chest` 固定掉落红符文、回复药水和一组飞镖；上方宝箱的小地图圆点改为比普通宝箱大 35% 的红色圆点。左下、右下宝箱及其能力光球不变。
4. **第二关奖励**：删除上方 Supply 宝箱及其小地图标记；左下 `Double Jump Treasure Chest` 改为回复药水和一组飞镖，右下 `Dash Treasure Chest` 只掉落绿色符文。两只下方宝箱的位置和既有能力光球关系不修改。
5. **第二关小地图**：右下绿符文宝箱使用比普通宝箱大 35% 的绿色圆点；Boss 门和其他地图信息仍沿用原小地图相机与 Marker Layer。
6. **重建安全**：`DemoSceneBuilder` 的第一关完整地图生成路径已同步红符文宝箱、红色标记和红符文门禁，重建第一关不会恢复旧掉落。菜单 `Tools > A Thousand Battles Later > Build Rune Boss Gates` 可幂等地把最终规则重新写入两个场景，并刻意不写宝箱坐标；`Validate Rune Boss Gates` 会检查门槽位、提示、掉落顺序、被删除的第二关上方宝箱以及彩色大圆点。

运行路径：

```text
Hero touches Boss gate
  -> BossArenaController checks the scene-authored required slot
  -> rune not equipped: localized HUD hint, remain in stage
  -> matching rune equipped: enter arena (rune remains equipped)

stage1 upper chest -> Red Rune + Health Potion + Kunai -> enlarged red minimap dot
stage2 lower-left  -> Health Potion + Kunai
stage2 lower-right -> Green Rune -> enlarged green minimap dot
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
5. **下劈**：`KingGroundCleavePattern` 使用 Ground 层向下检测地面，将刚刚落地的位置提交为攻击后的新移动锚点，再生成面向侧 245×32 单位的白色巨型矩形，水平长度对应第二关竞技场约一半宽度；这样通用攻击收尾不会把 King 拉回空中位置。
6. **攻击计数换位**：`EnemyAttackController` 在每次完整攻击结算后通知保存于 Boss 根对象的 `BossTeleport`。该组件现已抽象为两种换位模式，但保留原类名和资源 GUID，避免破坏第一关已有引用；`attacksPerRelocation` 是 Inspector 可调计数，默认每完成 3 次攻击触发一次换位，攻击冷却仍在换位结束后完整计算。
7. **国王节点跳跃**：Evil Wizard 继续使用 `Blink`，King 则在 `stage2_full` 中直接保存同一组件的 `Jump` 配置。组件复用法师“排除当前位置最近节点、从其余 `EnemyNavigationNode` 随机选点”的逻辑，但不瞬间改坐标；它以 `Rigidbody2D.MovePosition` 沿正弦高度构成的抛物线运动到目标节点。`jumpDuration`（默认 0.8 秒）和 `jumpHeight`（默认 12）均可调，移动期间攻击状态持续锁定普通寻路，落点后再刷新 A* 路径，避免两个移动器争抢刚体。
8. **保存式组件**：国王的换位组件、模式、计数、跳跃时长、高度和受击闪白引用都直接保存在场景中，不在运行时临时补挂；运行时只创建一次短生命周期的跳跃协程。旧 `WizardVisual`、旧巫师攻击和蓝色占位 Mesh 会被删除；`Entity_VFX.targetRenderer`、`BossStateMachine.animator`、`StoryDialogueController.bossVisualRoot` 都直接保存为 KingVisual 的对应组件。
9. **可重复构筑与验证**：菜单 `Tools > Boss > Replace Stage2 Boss With King` 幂等重建第二关 King，并重新写入默认的 3 次攻击、0.8 秒、12 高度 Jump 配置；`Validate Stage2 Medieval King` 校验七套动画、三招、节点跳跃模式、可调数值、移动/生命/剧情/受击引用，并额外确认第一关仍是 Evil Wizard 的 Blink 模式。`BossRelocationPlayModeTests` 还会实际执行三次计数和一次跳跃，断言中途高度明显上升且最终准确落在目标节点。

运行路径：

```text
Hero enters stage2 arena
  -> original BossArenaController enables the preserved bossRoot
  -> EnemyAttackController selects a range-valid King pattern
  -> BossStateMachine plays the pattern's fixed Attack1 / Attack2 / Attack3 clip
  -> white telegraph -> geometric hit check -> white strike fade
  -> completed attack counter +1
  -> attacks 1/2: full cooldown
  -> attack 3: pick another navigation node -> visible parabolic jump -> reset A* -> full cooldown
  -> EnemyHealth death -> original story / victory / return flow
```

## 小怪血条实现管线

1. **持久图形资源**：血条底槽与当前血量统一引用 `Assets/Resources/AttackHitboxes/AttackSquare.png`。不再把 `SceneArt.SquareSprite` 这种运行时 Texture/Sprite 写入 prefab，避免重载后序列化为 `fileID: 0`。
2. **预制体保存**：Orc、Goblin、Mushroom、FlyingEye、Skeleton 五个 prefab 各保存且只保存一个 `EnemyHealthBar`，包括 `Capacity`、`Fill Anchor/Current`、跟随目标、填充 Transform 以及 `CombatHealth.worldHealthBar` 引用。
3. **现有场景迁移**：构筑器修复 `stage1_full` 与 `stage2_full` 的现有 prefab 实例；如旧关卡残留了额外的场景血条，优先保留 prefab 自带血条并删除同一怪物下的重复项。
4. **后续重建**：`DemoSceneBuilder` 创建场景血条时直接使用持久 Sprite；`EnemyContentBuilder` 重建敌人目录后会重新执行 prefab 血条构筑，因此不会再次丢失。
5. **可重复构筑**：菜单 `Tools > Enemies > Repair Prefab Health Bars` 执行修复；`Validate Prefab Health Bars` 校验五个 prefab 的唯一血条、Sprite、跟随目标及生命组件引用。
