# ACT Roguelike

基于 Godot 4.7.2 mono 与 C#（目标框架 `net8.0`）的第三人称动作肉鸽原型。实际工程位于 `F:\Godot项目\项目a`，上级目录只是项目容器。程序集、工程文件和根命名空间为 `ActRoguelike`。

先读 [当前开发快照](docs/SESSION-SNAPSHOT.md)，再从 [文档目录](docs/README.md) 选择专题；历史交接与旧阶段说明无需顺序阅读。

## 运行与构建

- 在 Godot 编辑器运行工程：正式主场景为 `res://Scenes/Levels/Base.tscn`，从基地出击门进入关卡。
- `Scenes/Expedition.tscn` 是正式森林出征；`Scenes/Main.tscn` 保留测试地形与既有回归入口。
- 基地左侧新增“暮光回廊 · 生存试炼”入口（F互动），进入 `Scenes/Survival.tscn`。保留手动战斗，12分钟生存、拾取升级、三处可选信标，终局击败首领后主动撤离；见[模式设计与制作入口](docs/survival-mode.md)。
- 生存试炼已接入[四路线十二项专属增幅](docs/survival-builds.md)，等级2起每3级和强敌遗物提供专属选择，前置、互斥和模式限定统一校验。
- 在工程根目录执行 `pwsh ./build.ps1`。完整构建、专项验证、独占锁和存档保护约定见 [测试](docs/testing.md) 与 [当前快照](docs/SESSION-SNAPSHOT.md)。
- 当前使用 Forward+、Jolt Physics；Windows 渲染驱动由 `project.godot` 配置。不要关闭其他人的 Godot 编辑器来跑验证。

## 当前可玩范围

基地选角、配装与练习 → 出击 → 自由探索、先领取节点收益再挑战 → 首领 → 互动离场 → 下一关或整局结算 → 回基地成长并再次出发。当前三关复用正式森林场景，以关卡配置区分敌人、强度和首领技能。最终地形与能力门槛设计另行推进。

| 角色 | 所属武器 | 共通主动技能 |
|---|---|---|
| 猎手 | 能量掌、飞刀、弓 | 后撤、跃升 |
| 武器大师 | 灵剑、镰刀、长枪 | 闪、架、落 |

角色展台直接切人并打开该角色武器页；每个角色记住自己的武器。战斗输入与 HUD 共读 `Unit.AbilitySlots` 的当前装槽对象。具体动作、蓄力和输入预览见 [基地与换装](docs/base-loadout.md)、[技能](docs/abilities.md)。

角色A当前使用[三叶旅童](docs/clover-combat-trial.md)视觉档案，悬浮武器适配无手骨架；角色数值和技能沿用猎手配置。技能动作仍为首版施法适配。

## 默认操作

实际键位以设置中的控制页和当前技能槽为准。

| 操作 | 默认按键 |
|---|---|
| 移动／视角 | W/A/S/D 或方向键／鼠标 |
| 跳跃／奔跑 | 空格／Shift |
| 武技1／武技2 | 鼠标左键／右键；点按、按住及松手行为随当前武技 |
| 技能1／技能2 | Q／E |
| 下落攻击 | Ctrl；要求当前配装具有对应技能与可释放状态 |
| 互动展台、节点、首领门、出击门 | F |
| 奖励选择 | 点击卡片或 1／2／3 |
| 角色持有／首领状况 | C／B |
| 基地指引收起／展开 | F1 |
| 能力矩阵／帮助 | K／H |
| 设置／关闭顶层可关闭界面 | O／Esc |

六种现役武器按技能驱动攻击；旧枪械弹匣、装填和三角色操作已退出当前玩法。跳跃目前可用；初期限制跳跃、构筑解锁地形等想法属于 [后续体验计划](docs/player-experience-plan.md)，尚未实施。

## 开发入口

| 工作 | 入口 |
|---|---|
| 模块边界、生命周期和扩展规则 | [架构](docs/architecture.md) |
| 自己新增增幅、敌人、模型和脚本 | [新增内容操作](docs/content-authoring.md) |
| 降低新增内容的理解和维护成本 | [内容扩展与解耦计划](docs/content-extension-plan.md) |
| 日志与验证证据、历史文件清理 | [日志说明](docs/logging.md)、[Logs目录](Logs/README.md) |
| 命中、伤害、动作与反馈 | [战斗](docs/combat.md)、[打击反馈](docs/combat-feel.md) |
| 节点、三关、结算与局外循环 | [关卡流程](docs/stages.md) |
| 给场景物体选择交互类型、配置触发器 | [交互组件接入](docs/interaction-authoring.md) |
| 奖励、构筑与符文 | [成长与奖励](docs/rewards.md)、[符文](docs/rune-system.md) |
| 首领技能、词条与压力 | [首领](docs/boss.md) |
| 数值配置与表结构 | [配置](docs/config.md)、[表结构](docs/config-schema.md) |
| 界面与素材接入 | [UI](docs/ui.md)、[美术入口](docs/art-onboarding.md) |
| 地面敌人机制与验证 | [敌人清单](docs/enemy-roster.md)、[测试](docs/testing.md) |

`Scenes/TestLab.tscn`、`Scenes/TestEnemies.tscn` 及 `Scenes/Debug/` 内场景用于专项观察。森林、美术与模型样板的接入状态以各自 README 和批次证据为准，展示通过不等于正式主循环已接入。

本入口、专题、配置长文与旧快照共12份原文保存在 [2026-10-21 参考文档归档](docs/archive/reference-before-2026-10-21/README.md)，原始验证日志、失败现场与截图仍在原位置。
