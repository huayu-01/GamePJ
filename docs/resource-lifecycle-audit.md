# 资源与配置生命周期

本页回答“哪些旧东西可以动”。当前功能、优先级见 [开发快照](SESSION-SNAPSHOT.md)，调参见 [config.md](config.md)。2026-10-21 以 J43 和当日工作区做只读引用审计；资源证据和外部美术工作不因年代、文件名或未进入正式主场景而失效。

## 清理规则与复核入口

- `project.godot`、`docs/enemy-roster.md` 及其他对话的已修改文件保护；`ArtSource/`、新增 `Assets/`、新增 `Scenes/`、模型展示脚本和截图保护。未跟踪表示尚未提交，不能作为删除理由。
- `.uid` 是脚本稳定标识；`.import` 可能承载导入参数；授权文件、生成源、唯一失败现场、比较基线和有效存档指纹保留。不能批量清理扩展名。2026-10-24按用户明确要求收拢日志和历史文档，逐字重复的日志通过SHA256映射去重，重复跳转页保留归档全文后删除；详情见[日志维护](logging.md)。
- 静态路径、UID、稳定 Id、目录扫描和插值命名均可能产生依赖。没有 `ExtResource` 不能证明没有使用；生成工具和手工 F6 展示场景也属于有效用途。
- 删除候选需给出替代物、动态约定排除结果及验证范围。不要先删依赖，再把因此失败的验证删掉。

只读工具（Python 标准库，不启动 Godot、不写入工程、不删除文件）：

```powershell
python _tools/audit_resource_references.py
python _tools/audit_resource_references.py --path Scenes/Weapons/Pistol.tscn
python _tools/audit_resource_references.py --path Assets/Models/nodes/mesh_node_evolution.tres
```

`--json` 输出完整证据。工具同时读取 Git 跟踪及未跟踪文件，区分正式／制作、测试／工具、退役依赖和文档来源；检查完整路径、UID、Id／名称、目录与插值模板。**结果是人工审阅队列，不是运行可达性证明或删除清单。** 同名、注释和目录命中可能过度保护；未覆盖任意计算路径、二进制嵌入及编辑器外部配置。审计输出不作为固定计数基线。

## 当前分类

| 分类 | 资源与实际入口 | 处理 |
|---|---|---|
| 正式引用 | `Config/ConfigIndex.tres` 显式数组、行间资源引用；`Scripts/Config/ConfigIndex.cs::BuildTableIndex` 建表 | 按生产资源维护；不能因为数组之外没有路径就删行 |
| 正式引用，旧名保留 | `Scenes/Weapons/Pistol.tscn` 由 `Scenes/Player/Player.tscn` 实例化；六武器 `ScenePath` 和 `WeaponConfig.ScenePath` 仍指向它 | 当前共用 `Weapon` 壳，不是已经下线的可选“手枪”。若以后改名，应另做场景、默认路径和导出依赖迁移 |
| 动态约定 | `Assets/Models/nodes/mesh_node_<kind>.tres` 与 `Assets/Materials/mat_node_<kind>.tres` 仍由旧 `RewardNode.BuildShapes/BuildPalette` 按枚举名小写加载；`Scenes/Interactables/_extract_gem.gd` 负责生成 | 保留给Main／历史专项；J47正式基地／森林已改为类型组件生成新模型，不再沿用旧宝石外观。动态依赖仍在，不能按正式入口未引用直接删除 |
| 动态约定与生成输入 | `Assets/Animations/skills/`、`Assets/Animations/enemies/` 由 `SkillAnimationLibrary.LoadInto/ListAnimationFiles` 扫目录；角色专属库由单位配置指定 | 动画名、目录和事件轨道是契约，不能只查资源路径引用 |
| 制作期引用 | `Config/Tables/SkillAnimations/anim_b1_swordqi.tres` 仍在索引；`GenerateSkillAnimations._Run` 还会扫描整个配方目录，`SkillAnimationGenerator.GenerateOne` 生成旧剑气动画；`Assets/Characters/tools/generate_character_skills.gd` 再扫描技能动画适配角色骨骼 | 现玩法虽已被三才灵剑替代，本轮保留配方及三套 `Skill_B1_SwordQi.tres`。真正退役时必须同时处理目录扫描、索引、生成输出及角色制作输入，不能只删索引引用 |
| 测试／退役依赖 | `Config/Retired/Upgrades/mut_homing_missile.tres` 与 `mut_sunset_sword.tres` 由 `Scripts/Testing/SelfTest.cs` 显式加载、编译和验证 | 不进入生产奖励池，继续保留；与当前雷系／风系特化不是同一资源 |
| 历史证据 | `Config/Retired/Upgrades/mut_chain.tres` 已退出生产索引，当前仅发现 `SpecializationProbe` 检查旧 Id 不在生产目录 | 留在明确的退役目录，不重新放回 `Config/Tables/Upgrades`；与另外两项一起解释历史机制来源 |
| 测试／手工入口 | `Scenes/Debug/_CombatFeedbackLab.tscn`、`Scenes/Enemies/_AnimShowcase.tscn`、`Scenes/TestEnemies.tscn`、`Scenes/TestLab.tscn` | F6、命令行和文档入口不必有场景间引用。仍有验证／调试用途，不能按主场景不可达删除 |
| 正在制作及部分接入 | 未跟踪的森林、GeneratedPilot、敌人模型及演示场景；`ArtSource/` 原始输入和 Blender 工程 | 保护。本轮正式出征组合使用森林环境，未改森林源场景；模型样件仍按各自说明决定生产接入或退役 |

旧武器／角色的生产表现在只有六武器和两角色；`WeaponPresetKind`／`PlayerCharacterKind` 特意保留原槽号。不要为了“整齐”重排枚举与 `Slot`，否则会影响存档和按槽查表。

## 本轮已执行的最小移除集

以下两项已跟踪资源没有发现正式、测试、工具的路径／UID／名称入边，也不落入现有动态命名契约；主代理已核对并移除这两项；共享shader、贴图与有效节点资源保留：

| 候选 | 替代与排除证据 | 最小验证 |
|---|---|---|
| `Assets/Models/mesh_node_reward.tres` | 旧圆柱；当前 `RewardNode.tscn` 引 `nodes/mesh_node_enhancement.tres`，其余类型经枚举加载宝石。仅旧资源规范中的示例仍提及圆柱，`RewardNodeKind` 无 `Reward` 成员 | 删除时同步修正现行资源规范；确认奖励节点形状、描边和传送节点正常 |
| `Assets/Effects/muzzle/mat_muzzle_base.tres` | 没有查到使用入口；实际枪口场景引用 `mat_weapon_muzzle_front/glow/long/side.tres` 等材料。该文件自身引用的共享 shader／贴图仍有其他用途 | 仅移除该材料，不连带删除 shader／贴图；复核 `fx_weapon_muzzle.tscn` 和武器回退路径 |

`Assets/Materials/mat_Model_Toon_3d.tres` 虽也缺静态入边，但与当前 Toon 表现探索及 `ToonPass` 注释有关，先保留给美术复核。未跟踪资源、`.import`、`.uid`、授权文件和上述测试／动态资源不进入最小删除集。

## 字段收敛结果与后续边界

| 项目 | 已证实的旧行为／问题 | 本轮动作或最小方案 |
|---|---|---|
| 六武器 `MagazineSize/ReloadTime/AutoReloadDelay` | `.tres` 共 18 个赋值；`WeaponConfig` 和 `Weapon` 已无对应属性 | 已删除 18 个无效序列化赋值，未改伤害、弹速、冷却、技能或有效武器参数 |
| 灵剑武器总说明 | `wpn_spirit_sword.tres` 仍写“拔刀式／蓄力 1 秒剑气”，与 `skl_b1_w1_2.tres` 冲突 | 已改成三才灵剑的命中追击／归位机制说明；不在武器总说明重复持续、伤害、冷却数值，具体数值仍由技能行说明提供 |
| `StageDefinition.RewardNodeCount` 与 `StageController.ActiveNodeCount` | `ApplyStage` 先读关卡行，`PlaceNodes` 临时选择数量，随后 `EnableByLayer(2,5,3)` 再覆盖；`_Ready/StartRun` 也执行同样两步 | 同批由关卡主线收敛：删除失效导出字段和临时数量分支，固定 10 节点／高 2 中 5 低 3 保留为当前规则；不另造第二套关卡数量开关 |
| `StageDefinition.FreeChallengeQuota` | `ApplyStage` 读行，紧接着 `ResetStage` 以 `Min(FreeChallengeMaxQuota, FreeChallengeBaseQuota + StageNumber - 1)` 覆盖 | 同批由关卡主线去掉行级假开关，保留全局公式唯一入口。HUD 读计算后的关卡状态 |
| `skl_enemy_ranged_shot.AttackParams` 中 `MagazineSize/ReloadTime` | `AbilityConfig.ApplyAttackParams` 退到 `Weapon`，属性缺失时打印警告并跳过，两项没有实际作用 | 已删除两块参数子资源并同步数组，保留真正使用的射击间隔等参数 |
| `EnemySpawnEntry.Weight/MaxConcurrent` | 正式波次路径的随机方式、一次性出整波与参数字面语义不一致 | 本轮流程整合中已将波次类型与单只生成改为加权；MaxConcurrent仅限制非波次逐只生成，整波语义保留 |
| `SummonAbility.MaxAlive` | 原来只在激活前检查一次Summon标签单位，单次请求可能越过剩余名额，普通敌方增援也会漏计 | J45按存活集合去重，补计本技能生成单位，每只生成前检查；死亡／待删除释放名额，0仍不限，见[验证](archive/development-batches/character-design-j45.md) |

运行期默认值并不一概属于“重复字段”：`ConfigIndex`、单位／技能组件在资源缺失时保留可追踪回退，这类路径和验证继续存在。字段可删除与否应看执行链，而不是看默认值是否和表相同。

资源审计子任务仅执行只读扫描和补丁检查；主代理随后持独占锁完成双构建、正式流程、相关回归、Main比对及图形检查，证据见 [J44](archive/development-batches/character-design-j44.md)。五份用户存档指纹前后一致，工作区尚未提交推送。
