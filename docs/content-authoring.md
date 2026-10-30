# 新增内容的当前操作入口

本文描述已经存在的制作流程。生产增幅、符文、23项特化、敌人动作、33项精英特性、交互与节点收益已接入独立定义；文本键、内容校验、表现档案与公共验证设施共同维护；迁移边界见[解耦计划](content-extension-plan.md)。

通用顺序：复制最接近的样板 → 独立保存要改的子资源 → 修改身份与参数 → 绑定组件和素材 → 加入生产索引及投放范围 → 单项验证 → 联动与正式关卡验证。只新增字段或填写描述，不会自动获得机制。

## 新增增幅

2026-10-27起，替换式升级填写`RewardConfig.Replaces`并把同一前级身份放入`Requires`。运行期在新级应用成功后撤销前级实例，历史身份仍满足前置，构筑只展示当前级。可重复基础强化仅力量、生命、快速使用`ShowUpgradeLevel`显示等级；详细规则见[032～038更新](content-update-032-038-20261027.md)。

样板：[力量](../Config/Tables/Upgrades/upg_power.tres)。目录为`Config/Tables/Upgrades/`，类型为`RewardConfig`。

1. 新建稳定英文Id，填写名称、完整描述、分类和正权重；复制后确认没有共用将要修改的参数子资源。
2. 新通用效果填写 `Effects` 列表：纯属性用 `StatRewardEffect.Modifiers`，命中状态用 `StatusOnHitRewardEffect`（直接引用状态资源），吸血用 `LifestealRewardEffect`。一行可以组合多个效果；列表非空时是唯一执行入口，旧顶层 `Modifiers`／`ApplyHandler` 不会再次应用。武器进化用 `AbilityTuningRewardEffect.Tuning` 引用对应强类型参数，不需要登记处理器。全部生产增幅和符文均使用效果列表。体型组合示例见 `reward_1341.tres`（`UnitScaleRewardEffect`＋固定生命属性），逐杀成长示例见 `reward_1342.tres`（`KillHealthGrowthRewardEffect`）。
3. 设置获取次数、前置、互斥和角色／武器限定。唯一内容`MaxStacks=1`；原允许重复的内容继续按设计配置，UI不显示层数不改变实际规则。
4. 把行引用加入`Config/ConfigIndex.tres`的`Upgrades`数组。通过编辑器保存引用；手改文本时还需维护资源声明和`load_steps`。
5. 关卡GM的“可获取增幅”列表检查资格和实际效果，再验证正常随机池。GM强制`give`会跳过部分资格，不能用它证明正常候选正确。

标记加伤使用 `MarkedBonusRewardEffect`：直接引用 `Status`、挂标技能 `MarkAbility` 和吃加伤的技能 `BonusAbility`；后者留空表示玩家的各种伤害。`FlatBonus` 是固定加成，`PerStackBonus` 按本次命中之前已有的标记计算。生产样板为发散与心眼，说明参数读取 `Effects.0.*`，第一击挂标但不吃自己的新标记。`ChainLightningRewardEffect` 可用于击杀连锁效果，配置伤害、范围、跳数和冷却；当前仅保留机制和旧处理器兼容，没有新增奖励行。

持续奔跑使用 `HappyRunnerRewardEffect`，成功起跳使用 `JumpPulseRewardEffect`，每次本人击杀补跳使用 `KillAirJumpRewardEffect`。生产样板分别为快乐小子、杀戮践踏、践踏死亡，说明读取`Effects.0.*`。逐杀补跳仍绕开攻击连锁次数／深度门；撤销效果会停止以后补充，不回滚已经获得的可用跳数，换装继续由原配装流程重置。直接调用规则的`Stop`也会停止其专用总线或起跳事件。

最小验证：未满足前置时不出现；满足后可获得；重复／互斥符合设计；效果与描述一致；换装、死亡、取消冒险和重开无残留。新增会派生攻击的规则还需验证来源、冷却与不递归。

真正新增一种效果时，继承 `RewardEffectDefinition`，实现只读参数校验和 `Create`；创建的 `RewardEffectInstance` 在 `ApplyCore` 应用、`RemoveCore` 精确撤销自己拥有的修改。编译器负责激活、提交、归属和失败回滚。一次性收益在 `CommitCore` 发放，并用 `RollbackCore` 撤回失败事务；普通 `RemoveCore` 不追缴已经获得的护甲、跳数或已发射攻击。需要命中事件时优先复用现有 `TriggerRule`，不要让共享资源保存持有者、计数或冷却，也不要为每个奖励名字注册一个新处理器。

## 武器增强和符文

武器样板为[龙之箭](../Config/Tables/Upgrades/evo_dragon_arrow.tres)、[喜爱](../Config/Tables/Upgrades/evo_affection.tres)、[有备无患](../Config/Tables/Upgrades/evo_spare_dodge.tres)。`Effects` 中放 `AbilityTuningRewardEffect`，`Tuning` 选择已有的 `ChargedShotTuning`、`ChargeBehaviorTuning`、`WeaponSeekTuning`、`WeaponRecallTuning`、`MeleeProjectileTuning`、`VolleyRampTuning`、`SkillEnhancementTuning` 或 `AbilityChargesTuning`。在参数资源中引用 `TargetAbility`，不用填写节点路径或按键。

通用宿主只管理来源。参数资源继承 `AbilityRewardTuning`（同时实现 `IAbilityRewardTuning`），编辑器只允许选择此类资源；各参数自行检查和配置目标；新技能需要消费通用增强时实现 `IAbilityEnhancementConsumer`，不修改奖励注册表中的类型分支。能力用 `CaptureRewardParameters` 保存其专用增强字段，撤销后按原获取顺序重放剩余来源；只恢复参数，不取消动作、重置冷却或收回已经发出的攻击。新增可变参数时必须加入对应能力的快照。

符文仍为 `RuneConfig`，目录 `Config/Tables/Runes/`、索引 `Runes`、一次携带一枚。纯数值复用 `StatRewardEffect`；死铠组合 `GrantArmorRewardEffect` 与 `ArmorOnKillRewardEffect`，生死使用 `DeathAvoidRewardEffect`，狙神使用 `DistanceDamageRewardEffect`，巨人使用 `MaxHealthDamageRewardEffect`，小精灵使用 `CompanionRewardEffect`。不需要修改 `RunController` 或约定节点名前缀。携带来源由 `RewardEffectHost` 统一释放，旧节点名只供诊断。

## 特化运行组件

全部23项特化通过 `Effects` 中的 `SpecializationRewardEffect.Definition` 安装。奖励行的 `Specialization` 是稳定身份，参数、目标技能、弹体场景和转换奖励引用都写在各自的 `SpecializationBehaviorDefinition`。文案可以读取 `Effects.0.Definition.*`；旧 `SpecializationBehavior.*` 是兼容只读入口，不再重复保存顶层参数。

新定义继承 `SpecializationBehaviorDefinition`，声明 `Kind`、`Validate` 和 `CreateCore`；普通C#运行实例继承 `SpecializationBehavior`，在自身实例里保存冻结参数、计时、队列和来源。现有分发接口包括 `OnFact`、`OnPrimaryHit`、`OnDamaged`、`Tick`、`ScaleAgainst`、`TryAvoidDeath` 和状态提示。公共伤害、闪电、弹体与区域服务在 `SpecializationServices`；派生伤害继续使用已有快照和连锁限制。

每次获取有独立来源租约，同类型多个来源共享一个行为；最后来源移除时停止行为，释放自有标记、区域、订阅和临时修正。`RewardEffectHost.ReleaseReward(build,id)` 现在也支持全部特化。已经发射的攻击按自身生命周期结束。死斗先于嗜血更新，雷印／护甲／心雷等按原优先级分发，跨系联动不依赖获取顺序。

## 文本、身份与制作期检查

- 前置和互斥填写稳定 Id，例如 `rune_lei_seal`；显示名可以自由翻译。`LegacyPrerequisiteAliases` 只兼容旧条件，不用于新内容。
- `NameKey`／`DescriptionKey` 是整句文本键。中文原文仍写 `DisplayName`／`DescriptionTemplate`，运行 `python _tools/sync_content_text.py --apply` 给新行补键并更新 `Config/Localization/zh.tres`；默认不带 `--apply` 只预览，`--check` 可检查是否漏同步。翻译其他语言时编辑对应 `ContentLocale.Messages`，并加入 `ConfigIndex.ContentLocales`。
- 参数直接引用实际数据，例如力量的 `{Modifiers.DamageMultiplier:0.##}`、灼烧的 `{Effects.0.Duration:0.##}`。先翻译完整句子，再填参数；译文可以调整参数顺序和格式，不能改参数名。
- `ContentText.SetLocale("en")` 可用于制作调试；当前英文只提供两个示例，缺译回退原文。正式语言目录将 `RequireComplete` 设为 true，统一校验会拒绝缺译。
- 运行 `./_tools/verify_gameplay_fixes.ps1 -Only content` 检查生产索引、资源引用、节点、真实事件、压力和文案；用 `-Only effects,localization,reward_rules` 验证奖励机制，`-Only enemy_actions,roster` 验证动作执行与真实敌人。完整验证日志在 `Logs/evidence/`，临时输出在 `Logs/validation/`。
- 新内容定义可实现`IContentValidatable`，统一校验器会沿已导出的资源引用调用其`Validate`，无需在校验器里增加具体类型判断。效果、敌人动作与特化行为定义已经使用该接口。
- 内容数量基线集中在[content_inventory.tres](../Config/Validation/content_inventory.tres)，覆盖全部21类索引数组和特化总数。新增／删除内容后先核对索引、投放与引用，再明确更新对应数量；校验不会自动接受当前值。奖励分类数量也集中在该文件的 `ExpectedRewardKinds`。Main先检查实际数量，再比较原有8项硬指标，错误数量不能通过归一化混过。命中段数、伤害、槽位等机制断言仍保持独立。
- 起跳／逐杀与身体测试可选`-Only jump_stomp,kill_jumps,kill_facts,body`；特化及联动可选`-Only specialization,synergy`。测试副本修改独立定义，不再修改已被定义接管的旧参数。

运行目录不扫描 `Tables` 自动安装内容：制作行必须明确进入索引。手改 `.tres` 时，自定义属性写在 `script = ExtResource(...)` 之后，并维护 `load_steps`；否则资源文件存在也可能没实际装载。

## 新增敌人

样板：[重击预制](../Scenes/Enemies/HeavySlammer.tscn)、[敌人行](../Config/Tables/Enemies/enemy_heavy_slammer.tres)、[动作计划](../Config/EnemyPlans/heavy_slammer.tres)。

1. 在`Scenes/Enemies/`建立敌人预制，复用`Unit`、`Movement`、`EnemyBrain`、能力和表现组件；身体、攻击与弹体继续遵守既有碰撞层。
2. 复制敌人行，修改Id和`ScenePath`，填写基础数值、正权重、解锁时间、精英概率及物理档案。普通敌人实际生命还乘[全局倍率](../Config/Tables/Enemies/enemy_global.tres)及关卡倍率，不要把最终血量直接重复填入基础值。
3. 组合现有Behavior／Ability，或在`Config/EnemyPlans/`调整动作段。`PressureBehaviors`只计算生成成本，不能安装动作；未知压力键必须先在`Config/EnemyBehaviorPressure.tres`定义。
4. 加入`ConfigIndex.Enemies`，并加入允许投放关卡的`AllowedEnemyIds`。生成器根据敌人表补齐生成项，但还会检查预算、时间、权重、导航连通性和身体净空。
5. 在测试地图单独检查寻路、攻击前摇、事件、预警、碰撞、中断、护甲和死亡，然后验证真实刷怪与资源扣除。

已有动作的新组合通常可配资源；真正新的决策扩展 `Behavior`。准备动作已支持 `EnemyActionStep.Action` 直接引用定义资源，样板为 `Config/EnemyActions/` 的等待、格挡、滞空，以及 `Config/EnemyPlans/spear_guard.tres`。新定义继承 `EnemyActionDefinition`，每次进入动作创建独立 `EnemyActionExecution`，在 `End` 中释放自己持有的移动覆盖或临时节点，不把计时和持有者存进共享资源。

前摇／执行／恢复时序、远近分支、动画事件和整体取消由 `EnemyPlanAbility` 管理。近战／回旋使用 `MeleeEnemyAction`，直线／追踪冲锋使用 `ChargeEnemyAction`；参数在动作定义中编辑，动作段只保留时序和组合。样板为 `blade_spinner`、`combo_fighter`、`tracking_charger`、`spear_guard` 四份计划。进入时生成独立 `EnemyActionState`，预警与执行共读冻结快照，命中体和速度覆盖由执行实例释放。

攻击必须等待真实 ATK；取消后新进范围的目标不能再被残留命中体打中。动作定义提供语义类型，旧 `Kind`／伤害等字段按归属隐藏并停止重复保存。`Action` 留空继续兼容旧路径，重击等由其他会话维护的专用接入保留原入口。

投射物、起跳、俯冲、遁地和后跳也已有独立定义，生产样板为 `burrow_hunter`、`hover_diver`、`arc_thrower`、`arc_bomber`、`bomb_lobber`、`meteor_mage` 六份计划：

| 动作定义 | 制作参数与约束 |
|---|---|
| `ProjectileEnemyAction` | `Mode`选择追踪弹、抛射、直投炸弹、抛物线炸弹、陨石；配置伤害、范围、航程、转向率。抛物线用`FlightSeconds`求初速，直射／陨石使用`Speed`；实际弹速仍受全局上限限制。 |
| `JumpEnemyAction` | `Speed`为起跳垂直速度；在动作段配置起跳持续时间，再接滞空与俯冲。 |
| `DiveEnemyAction` | 飞行速度、伤害与范围在定义中；`LandingSeconds`是落地后独立的伤害窗口，默认0.12秒，落地前不生成命中体。 |
| `BurrowEnemyAction` | `MinDistance`／`MaxDistance`为目标周围出土环，`Depth`仅控制表现根下沉；完成时通过导航、连通性和身体净空选点，取消或无合法点不会强行传送。 |
| `BackHopEnemyAction` | `Speed`与动作段`ActiveSeconds`决定后跳位移；导航检查通过且取得移动所有权后才起跳。 |

`ProjectileScene`可引用根节点使用`Projectile`的弹体场景，留空沿用当前球形样件。自定义场景使用自己的外观；发射模式仍负责追踪、爆炸、扫掠半径等判定参数，换外观后需复核真实碰撞与预警。已经发射的弹体与爆炸预警随弹体生命周期结束，不随施法者收招或死亡提前撤销。

多阶段动作可覆盖执行实例的`ControlsDuration`自行结束；`SuppressWarning`和`WarningVelocity`供预警读取落地事实与实际俯冲速度。控制时序、模型下沉与临时命中体属于实例，不写回定义资源。起跳／滞空预警会读取后续俯冲定义，不能再从旧动作段的默认半径取值。

## 新增精英特性

完整规则与33项行为见[精英特性](elite-traits.md)。现有样板为 [重铸](../Config/EliteTraits/elite_reforge.tres)、[荆棘棋盘](../Config/EliteTraits/elite_thorn_grid.tres)，显式目录为 [catalog.tres](../Config/EliteTraits/catalog.tres)。原冒险和生存模式共用目录，不在两个导演各写一套特性分支。

1. 在 `Scripts/Combat/Elites/Definitions/` 创建继承 `EliteTraitDefinition` 的参数定义，通过 `Create(owner, wave)` 创建独立 `EliteTraitBehavior`。显示名和稳定Id不决定行为。
2. 在 `Config/EliteTraits/` 保存对应资源，填写 `Id`、`Title`、`Description`、`NameKey`／`DescriptionKey` 及该行为使用的参数。`ExtraCost` 是每名承载者的额外生成费用；`MaxCarriers` 是同波整个生命周期的承载上限，不是存活目标、召唤物或危险节点数量。
3. 用 `Requirements` 与 `Supports` 过滤适用原型；有专门限制时覆盖 `Supports`。例如重铸排除固有控制免疫，追踪／多重射击排除无法改造的攻击，首领和精英召唤物不进入普通特性安装。
4. 改变实际体型时使用 `PreSpawnScale`，让导航选点先验证最终碰撞体；不要等行为 `Attach` 才放大。计时、订阅和修正器由行为实例持有，在 `Dispose` 按来源撤销。
5. 将资源加入显式目录的 `Traits`，维护 `ExpectedCount` 和对应专项数量，再运行 `_tools/sync_content_text.py` 同步文本。`_tools/build_elite_traits.py` 会重写其拥有的33个定义、参数行及目录，不是日常调参入口。
6. 使用 `_tools/verify_elite_traits.ps1` 验证防御、危险、机动／投射和两模式接入；用 `-Only EliteDefense` 等选择专项。所有引擎与构建仍串行持有公共验证锁，报告在 `Logs/evidence/elites/`。

当前每只精英最多一个特性、同波只抽一种；12项强特性预设单承载，其余最多2只。生存模式用至多4只原定敌人／20秒的固定批次，不按逐只入场重抽。召唤只出一批有限免费随从，计入存活压力与清场，不产生模式经验和猎令收益。虫洞、筑墙、飞弹减速暂缓；荆棘棋盘是可跳跃躲避的地板效果，不是实体障碍。

防御规则先判断免伤，再在防御前分摊生命链接；各接收者只结算自身防御，转移标记防递归并保留原击杀来源。精英特性危险节点走 `EliteHazardWorld` 的预警、实际几何、跳伤间隔与容量，当前每调度器总上限24个、每来源6个；原型技能／投射物沿原系统限制，跨原型攻击的全场统一额度尚未接入。奖励选择暂停精英危险，清场、取消、结算及场景退出统一清理相关来源。

## 新增或替换模型与动画

样板：[真实重击样件](../Assets/Enemies/StoneSlammer/README.md)。制作源放`ArtSource/`，运行GLB／材质／动画放`Assets/`，组装预制放`Scenes/`。样板展示成功不等于生产敌人已经替换。

1. 固定资产Id，保存来源、许可和Blender工程；整理原点、单位尺寸、朝向和骨架。
2. 骨架模型建立 `UnitVisualProfile`，配置 `ModelScene`、模型局部变换、骨架与播放器的相对路径、动画名和技能库目录。两套生产样板在 `Config/VisualProfiles/lyra.tres` 与 `samurai.tres`。单位行的 `VisualProfile` 引用档案；旧 `ModelPath`／动画字段保留兼容读取，挂档案后在 Inspector 隐藏，保存不会生成第二份旧参数。
3. 真实命中时刻接`ATK`等动画事件；持盾开关、投射物出手等也按能力契约绑定。缺动画事件不能补一个计时伤害来绕过。
4. 玩家档案明确填写 `GripScene`、`GripBone`、`GripSocketPath`，再检查对应技能动画库。`content` 专项会检查真实骨架、播放器、骨骼和挂点；路径填错不能靠递归查找掩盖。敌人骨架也可使用档案；没有档案的旧单位保持既有入口。
5. 只改变外观大小时调整表现子节点；当前敌人行的`ModelScale`会缩放整个单位，包含碰撞体。改变实际体型后复核导航、刷怪净空和狭窄通路。

在真实玩家镜头下检查预警、护甲边缘光、受击、残影、状态图标与模型遮挡，最后再做多敌人战斗。

动作需要影响手持武器外观时，实现只读 `IWeaponPresentationSource`，提供隐藏状态或世界朝向。`CharacterDisplay` 不再识别投掷／下落能力的具体类型；投掷和下落能力已经是生产样板。外观状态不得改变伤害、碰撞和移动，换装会重新收集提供者。

当前大多数模型仍为候选稿，最新确认范围见[美术接入](art-onboarding.md)。`content` 会按与运行期相同的规则检查技能动画轨道的目标骨架和骨骼；骨名不匹配时应重定向动画，不能用关闭检查或跳过事件来“兼容”。档案中的骨骼选择优先于递归找到的第一个骨架。

## 奖励选择流程

`RewardOfferFlow`／`RewardOfferSession` 负责固定候选、单次应用、失败重试、完成和取消；`RunController` 负责把选择结果应用到本局、关闭界面，再完成节点。正常节点、特殊池和商店中的增幅选择共用这个入口。重复信号、同步重入、无效下标和取消后的旧回调不会再次获取。

`RewardScreen` 的按钮绑定打开时的代次，旧按钮的迟到事件不能选中新一轮奖励。节点表的 `RewardBehavior` 指定 `ChoiceNodeReward`、`ResourceNodeReward`、`HealNodeReward` 或 `ShopNodeReward`。`NodeRewardContext` 只提供需要的服务，管理单次完成和取消；商店等待实际领取才完成，候选不足或窗口拒绝时退款，取消待领取购买也退款。关卡继续负责完成后的挑战与结算。

## 新增交互与节点收益

交互使用 `InteractionComponent.Definition` 引用 `InteractionDefinition`，分别指定 `BackendScene` 和可选 `ModelScene`。后端场景根节点继承 `Interactable`，模型场景根节点为 `Node3D`；需要实例覆盖项时由后端实现 `IInteractionConfigurationConsumer`。`VisualStyle` 只在未指定模型时选择现有几何外观。自定义定义允许 `Kind=None`，新增后端不必扩充中央枚举。内置16种资源样板在 `Config/Interactions/`，后端场景在 `Scenes/Interactables/Backends/`。旧场景仍可按 `Kind` 取内置定义。

奖励后端的 `Definition` 可直接引用自己的 `NodeConfig`，其 `RewardBehavior` 决定收益，`ValueTags`／`WaveCount`／`Layer` 决定挑战价值，`BossPressurePoints` 等继续进入原压力公式。新策略继承 `NodeRewardBehavior`，只实现 `Begin(context)` 和 `Validate`；异步回调先检查 `context.IsValid`，完成调用 `Complete`，取消清理由 `Cancel` 管理。不要在资源里保存本局计数或等待回调。

验证使用 `-Only decoupling,interaction,interaction_render,content`，再用正式关卡复验一次领取、挑战开始、取消与首领后的离场。触发器与宿主规则见[交互制作](interaction-authoring.md)。

## 新脚本放在哪里

| 责任 | 当前接入 |
|---|---|
| 敌人何时／对谁／去哪里 | `Scripts/AI/Behaviors/`，继承`Behavior`并由Brain初始化 |
| 前摇、执行、恢复与动作取消 | `Scripts/Abilities/`，继承`Ability`并接真实事件 |
| 敌人准备与攻击步骤 | `Scripts/Config/EnemyActions/`定义，`Scripts/Abilities/EnemyActions/`执行实例 |
| 精英附加特性、同波归属与危险 | `Scripts/Combat/Elites/Definitions/`定义，`Scripts/Combat/Elites/`独立行为，`Config/EliteTraits/`显式目录 |
| 增幅触发与联动 | `Scripts/Config/RewardEffects/`定义，`Scripts/Rewards/Effects/`运行实例；复用规则组件 |
| 节点收益与交互定义 | `Scripts/Config/NodeRewards/`策略，`Scripts/Interaction/`后端；通过资源引用组合 |
| 强类型可调参数 | `Scripts/Config/`资源，并找到实际应用者 |
| 模型、状态和反馈表现 | 对应表现组件，只读动作／战斗事实 |
| 骨架、挂点和动画映射 | `UnitVisualProfile`，通过单位行强类型引用 |

逻辑脚本使用当前项目的C#体系；编译成功后还需挂到场景或由工厂创建、绑定配置和触发入口。共享资源不保存持有者和本局计时；所有订阅、修饰器、移动覆盖和临时表现按自己的来源清理。

技能的通用字段仍由 `AbilityConfig` 应用。能力专用参数可实现 `IAbilityConfigConsumer`：默认值在 `ConfigureBeforeParameters` 消费，需要覆盖通用 `Params` 的权威参数放在 `ConfigureAfterParameters`。新能力无需再给 `AbilityConfig.ApplyTo` 增加具体类型判断。

第一次自行扩展可按“纯数值增幅→现有敌人变体→替换模型与动作→新机制”递进。现有字段见[配置入口](config-authoring.md)，结构边界见[架构](architecture.md)，验证见[测试](testing.md)。

## 模式专属增幅

现有完整例子是[生存试炼十二项](survival-builds.md)。复制 `Config/Tables/Upgrades/surv_scavenger_compass.tres` 时，改稳定Id和文本键，保留 `RequiredModeIds = PackedStringArray("survival_twilight")` 即可限定到生存试炼。模式Id来自 `Config/Modes/twilight_survival.tres`，不使用显示名称或场景文件名判断。

`IconName`填写 `Assets/UI/Icons/` 下SVG文件名（不含扩展名），奖励卡、HUD和构筑面板共用。效果数值放在`Effects`子资源；说明可用`{Effects.0.RadiusScale:0.##}`直接读取该资源的值。复制行还要加入`Config/ConfigIndex.tres`，显式更新`Config/Validation/content_inventory.tres`的数量和类别，再运行文本同步和内容校验。

新增机制从`SurvivalRewardEffect`派生一个参数定义，创建独立`SurvivalAugmentRule`；临时数值、订阅、表现和清理都放在规则实例。不要在公共注册表或界面按增幅Id加行为分支。新增本局详情可实现`IRewardRuntimeDetails`，例如破晓清算；中文显示名改动不影响行为身份。

## 替换武技与补给

构筑2.0的六种替换样板见 `Config/Tables/Upgrades/reward_1347.tres`～`reward_1352.tres`。`AbilityReplacementRewardEffect`引用原技能行与新能力场景，只替换当前槽的实例，保留技能稳定身份和输入槽。新场景独立配置自己的伤害、时间、范围和动作；撤销必须结束旧动作、恢复原槽并清理独立留场物。兼容旧增强时通过能力接口适配，不在输入层按具体增幅判断。表现应读取`CharacterDisplay.ActiveModel`，不能假设原场景的`Model`仍是当前角色。

技能说明复用奖励的整句翻译键，用`{Effects.0.ReplacementScene.Damage}`等命名参数读取场景根节点的已保存参数；这些参数须显式保存在场景。替换时为运行期展示行指定`RuntimeDescriptionSource`，保持中文／英文切换后仍读取同一数值源，不把某种语言提前拼成固定字符串。普通配置行无需指定该运行期来源。

空投样板是`reward_1346.tres`。每种内容用`SupplyOptionDefinition`指定类型、数值、普通／死战权重、状态引用和显示键。临时药剂复用统一状态系统，同类刷新时长、不重复叠幅度，清理仅撤自身来源。即时资源使用本局账本，正常结算兑现一次，取消作废。单位与补给落点复用`MapManager.TryGroundAnchor`和`TryRingPoint`／`TryValidPoint`；投影锚点不能直接当最终落点。完整参数、前置与互斥见[068～070接入说明](content-update-068-070-20261029.md)。
