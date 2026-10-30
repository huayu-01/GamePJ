# 架构与扩展契约

本文描述当前模块职责，不按开发批次追加历史。接续顺序见 [当前快照](SESSION-SNAPSHOT.md)，玩家流程见 [关卡](stages.md)。整理前的演进过程与旧阶段说明见 [历史原文](archive/reference-before-2026-10-21/architecture.md)。

新增内容先看[制作操作](content-authoring.md)。2026-10-24已记录效果组件化、本地化和其他扩展边界，按[解耦计划](content-extension-plan.md)逐步迁移；效果资源列表、统一翻译入口及制作期总校验器目前仍为计划，不视为已实现接口。

## 运行入口与模块边界

正式入口为 `Scenes/Levels/Base.tscn`；出击摘要确认后进入 `Scenes/Expedition.tscn`。`Scenes/Main.tscn` 保留测试地形及历史回归。基地复用真实配装、技能、动画事件和命中链路，不创建关卡、奖励或整局结算上下文。

| 职责 | 当前组件 | 边界 |
|---|---|---|
| 配置装载与查表 | `ConfigIndex`、`ConfigRows`、各强类型配置资源 | 索引统一引用生产表；稳定 Id 用于关联，显示名用于界面 |
| 配装 | `PlayerLoadout`、`LoadoutSelection`、`AbilitySlots` | 基地与关卡共用；只允许当前角色／武器声明的技能占槽 |
| 玩家输入 | `PlayerController`、`PlayerAbilityInput`、`PlayerWeaponInput` | 实际按键由 InputMap／设置提供；输入不扫描仓库同键能力 |
| AI 决策 | `EnemyBrain` 与 `Behavior` | 决定何时移动、追逐和请求能力；不复制命中结算 |
| 动作执行 | `Ability`、`UnitAnimator`、动画事件 | 能力控制状态和动作许可，真实 ATK 等事件启动判定 |
| 战斗核心 | `Unit`、`AttackInstance`、`HitInstance`、命中体／弹体 | 攻击快照、敌对关系、命中策略、防御与伤害结果 |
| 移动与空间 | `CharacterMovement`、`MapManager`、`SpawnPlanner` | 身体、重力、技能速度覆盖、导航与有效点检查 |
| 场景交互 | `InteractionComponent`、`Interactable`、`PlayerInteractInput` | 编辑器选类型，运行期复用原业务；Area／半径判定与F输入共用 |
| 单场挑战 | `EncounterController` | 波次／预算、敌人生成与本场完成；游离怪不进入本场清场账；Aborted明确中止 |
| 单关流程 | `StageController` | 节点、挑战次数、隔离次数、首领门、阶段压力与离场；ChallengeFinished区分真清场与撤销 |
| 整局编排 | `RunController` | 配装、奖励、关卡序列、死亡／通关与整局结算 |
| 构筑 | `RewardCatalog`、`RewardPool`、`RunBuild`、规则组件 | 候选过滤、获取记录和效果生命周期；符文为独立携带层 |
| 首领 | `BossPhaseController`、`BossPressure`、`BossPerkCatalog` | 血量阶段、战斗内计时压力、开场技能和节点数值词条分别处理 |
| 界面 | `OverlayScreen`、`UiRouter`、`UiService` | 业务请求语义入口；路由统一管理暂停、鼠标与顶层关闭 |
| 表现 | `CharacterDisplay`、`CombatFeedback`、HUD／世界提示、镜头与音效 | 消费动作或结算后的状态；显示模型与特效不替代真实几何 |

整局累计记录和首领结束后的离场边界已通过本轮自动化流程验证；范围和完整试玩的区别见 [J44交付记录](archive/development-batches/character-design-j44.md)。

## 战斗与动作约定

- `Ability` 使用 `Node3D`，需要三维变换的武器、命中体与 Muzzle 不挂在普通 `Node` 下。
- 释放、取消和换装必须成对归还所持有的无敌、碰撞例外、速度覆盖、事件订阅及临时表现。技能结束只能释放自己的移动覆盖。
- 蓄力松手提交一次释放快照；真实释放动画事件才生成攻击。缺事件或动作中断不能用补发绕过契约。
- 伤害、暴击、来源技能、弹道属性按对应攻击快照冻结；持续伤害、派生伤害和召唤物仍须保留来源归属。
- `GameplayEventBus` 订阅成对退订。会生成新攻击的通用触发规则受冷却、每链次数和深度约束；纯击杀计数、续时等事实观察者按自身契约处理。
- 命中反馈在防御与伤害结算后区分打肉、碰甲、破甲、格挡、招架和护罩。局部停顿使用既有时钟能力，不能靠暂停整棵树制造打击感。

详细实现与依据见 [战斗](combat.md)、[技能](abilities.md)、[动画产线](anim-pipeline.md)、[打击反馈](combat-feel.md)。

## 配装、配置与内容制作

`PlayerLoadout.Apply` 是装备应用入口：清理旧动作／修饰／输入，应用角色与武器、安装当前槽，再应用局外能力卡与表现。当前角色、武器和输入槽各有明确职责；不要从模型名、能力节点名或 `RunSetup` 猜测实际槽位。

- 新敌人优先组合现有 Behavior 与 Ability，机制确有变化时才扩展组件。
- 新数值使用强类型资源、`StatModifier` 和既有编译入口；说明从配置模板和参数读取。
- 数组参数使用 Godot 支持的强类型导出数组或子表资源引用。禁止以整串文本解析、Id 拼路径或字符串 Get／Set 代替可靠关联。
- 新配置行须同步资源引用、索引数组与所需 `load_steps`；校验实际消费者，不能只确认 Inspector 能填。
- 生产索引、历史回归资源和展示场景分开识别。原型、归档和美术源文件不因未进入正式流程而自动判为废弃。

表字段与回退语义见 [配置表](config-schema.md)；装备、模型、骨骼与技能库接入见 [基地换装](base-loadout.md)。

场景物体通过 `InteractionComponent.Definition` 引用交互定义，分别指定后端与模型场景；旧 `Kind` 可选择16种内置定义。组件安装 `Interactable` 并复用原交互契约，自定义后端无需扩充中央枚举。节点行的 `RewardBehavior` 指定收益策略，`NodeRewardContext` 管理单次完成、取消与退款。模型随禁用、消耗、消失与Reset同步；`HostRoot` 指定布点时一起移动的宿主。Stage通过 `Interactable.InSubtree` 收集嵌套后端，不跨入其他Stage；预置出口独立于普通节点配额并跟随正式换关复位。具体层级和配置约束见 [交互组件接入](interaction-authoring.md)。

增幅与符文通过 `Effects` 组合定义，运行状态归独立实例，`RewardEffectHost` 按来源撤销；组合应用／提交失败逆序回滚。技能增强继承 `AbilityRewardTuning`，技能自行消费参数，撤销仅恢复增强字段。特化定义创建各自行为，协调器只管理租约、次序和事件分发。旧字符串处理器保留在兼容层，新行为不加入中央业务字典。制作和验证边界见[内容制作指南](content-authoring.md)。

## 生命周期、资源与空间

1. 子节点 `_Ready` 早于父节点；引用用惰性获取或明确的就绪绑定。不要在 autoload 的初始化里假设场景节点已经存在。
2. 单位表应在 `AddChild` 前套用，基础值捕获后再应用全局／关卡修正，避免基础值重复放大。
3. 运行期磁盘资源与场景切换使用 `Core/RuntimeResources`；不要新增无限强引用缓存。依据见 [资源生命周期](resource-lifetime.md)。
4. 物理运动、跟随变换和传送须尊重物理帧及插值；传送后同步物理状态，不用单帧截图证明碰撞正确。
5. 单位身体共用 UnitBody 阻挡，阵营命中身份独立。弹体查询几何与单帧命中预算独立配置；详见 [碰撞层](collision-layers.md)。
6. 普通敌人、主首领及开场护卫使用导航、连通性、范围和实际体型净空检查；选点失败等待重试，普通敌人成功生成才扣预算。首领技能召唤与压力增援也经严格选点，同批预留位置；压力失败保留数量并重试，首领死亡撤销。其他奖励／特化召唤仍按各自路径检查，不把一条生成路径的结果扩称为全部生成方式通过。

## 验证与维护

构建、Godot 验证、Git 操作分别遵守当前快照中的独占锁与进程规则，保护用户编辑器、存档和并行美术工作。专项通过证明对应机制，不自动证明完整三关体验、最终美术或内容平衡。

批次证据保留在原路径；历史中的空导航告警、旧枪械／装填和旧角色限制只代表记录当时。当前问题、待验收项和本轮状态以 [当前快照](SESSION-SNAPSHOT.md) 为准。
