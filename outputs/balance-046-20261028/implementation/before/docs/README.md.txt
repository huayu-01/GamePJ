# 项目文档入口

先读[当前开发快照](SESSION-SNAPSHOT.md)，再按任务选专题；无需顺序阅读历史批次或迁移长文。

| 任务 | 当前入口 |
|---|---|
| 启动、操作与项目范围 | [根README](../README.md) |
| 自己新增增幅、敌人、模型和脚本 | [新增内容操作](content-authoring.md) |
| 本轮更新与修复、玩家自行追加验收项 | [可编辑验收表说明](acceptance-tracker.md) |
| 技能专用类型、数字ID与技能栏资源规则 | [内容编辑入口更新](content-authoring-update.md) |
| 4K／100敌人的性能实测与待选措施 | [性能方案（等待决断）](performance-proposal.md) |
| 效果组件、内容校验、文本键与解耦验证 | [内容扩展与实施状态](content-extension-plan.md) |
| 日志在哪里、怎样清理与保留 | [日志说明](logging.md)、[统一日志目录](../Logs/README.md) |
| Q键、操控、弹道、构筑、特殊奖励与续波修复 | [十四项修复与验收](gameplay-fixes-20261023.md) |
| 取消整局冒险、携带符文HUD、雷池与组合验证 | [冒险回退与联动](adventure-synergy-20261024.md) |
| 本轮体验目标与地形能力门槛设想 | [玩家体验计划](player-experience-plan.md) |
| 基地、探索、节点、首领与整局结算 | [关卡流程](stages.md) |
| 12分钟手动战斗、经验升级和信标撤离 | [暮光回廊生存试炼](survival-mode.md) |
| 拾荒、守阵、协击与契约四条专属路线 | [生存试炼构筑设计](survival-builds.md) |
| 物体交互类型选择、触发器与机关信号 | [交互组件接入](interaction-authoring.md) |
| 配置怎么改、哪些字段有效 | [配置编辑入口](config-authoring.md) |
| 旧资源能否清理、动态与测试依赖 | [资源生命周期](resource-lifecycle-audit.md) |
| 架构与接入边界 | [架构](architecture.md)、[战斗](combat.md) |
| 奖励、构筑、符文与特化 | [成长](rewards.md)、[符文](rune-system.md)、[23项特化](followup-build-updates.md) |
| 地面敌人、导航、护甲与格挡 | [敌人清单](enemy-roster.md)、[碰撞层](collision-layers.md) |
| 精英单特性波次、免费单批召唤与33项设计 | [精英特性规则](elite-traits.md) |
| 敌人与三叶旅童统一风格，先挑描述再生图 | [敌人美术计划书](enemy-art-plan.md)、[建模参考与细节拆解](../ArtSource/modeling-notes.md) |
| 敌人范围预警和打击反馈 | [预警契约](skill-indicator.md)、[反馈框架](combat-feel.md) |
| 配装、模型、界面 | [基地配装](base-loadout.md)、[UI](ui.md)、[美术接入](art-onboarding.md)、[资源规范](assets.md) |
| 森林资源、展示与制作源 | [120米环境](../Scenes/Levels/NatureArena/README.md)、[美术样板](../Scenes/Levels/NatureBenchmark/README.md)、[ArtSource](../ArtSource/README.md) |
| 三叶旅童、角色A试装与额头施法发光 | [森林战斗试玩](clover-combat-trial.md) |
| 验证、排障与索引 | [测试](testing.md)、[CodeGraph](codegraph-usage.md) |
| 最近交付及证据 | [J48挑战状态与拒绝反馈](archive/development-batches/character-design-j48.md)、[J47替换交互模型](archive/development-batches/character-design-j47.md)、[J46交互类型组件](archive/development-batches/character-design-j46.md)、[J45增援与弹层隔离](archive/development-batches/character-design-j45.md)、[J44整理与玩家流程](archive/development-batches/character-design-j44.md) |
| 最近灵剑机制 | [J43命中追击](archive/development-batches/character-design-j43.md)、[J42持续与续时基础](archive/development-batches/character-design-j42.md) |
| 飞刀名称与右键固定散射 | [飞刀与60°总夹角](throwing-knife-volley.md) |
| 追溯旧设计与迁移 | [本轮归档](archive/reference-before-2026-10-21/README.md)、[更早归档](archive/README.md) |

完整验收与基线集中在`Logs/evidence/`；临时运行输出、旧日志和清理清单分别收进`Logs/`子目录。开发批次及旧方案位于`docs/archive/`，正文入口只描述当前契约。截图、原始需求和模型源文件保持原路径；历史待办和数值不作为当前规则。
