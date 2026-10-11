# 配置表全盘点（哪些东西应该成表）

> 项目：`F:\Godot项目\项目a`（Godot 4.7.2 stable mono / C# / net8.0）
> 本文是**只读代码后的独立盘点**：把「所有能成表的东西」列出来，包括 `docs/config-schema.md` 里没写的。
> 相关：[config-schema.md](config-schema.md)（T1–T11 分表设计与迁移路径）、[config.md](config.md)、[rewards.md](rewards.md)、[boss.md](boss.md)、[stages.md](stages.md)、[summons.md](summons.md)、[config-tooling.md](config-tooling.md)
>
> **性质**：盘点文档。**本文不改任何代码 / 场景 / `.tres`**，只描述现状与建议。
> 依据规则：每条结论都给 `文件:行号`；读不到或没核实的写「**未确认**」，不编造。
>
> ---
>
> ## ⚠️ 2026-10 更新：配置表阶段 3 / 4 / 5 / 6 已落地，本文多条结论已过时
>
> **阶段 5（AI 行为表 = 本文 #15，§2.7）**：✅ **已做**。新增 `Config/Tables/AiProfiles/ai_*.tres`
> **11 行**（一行一个 AI 档案 = 一个单位的整套 AI 参数：9 敌种 + 首领 + 无人机），行类 `AiProfileConfig`；
> `UnitConfig` 新增一列 **`AiProfile`（资源引用）** + 在 `ApplyTo` 里一行调用（**没有碰 `EncounterController`**）。
> **盘到 25 个 AI 参数**（原先散在 10 个敌人场景的 **53 处**赋值里）。
> ⚠️ 本文 §2.7 建议的"`Behaviors: ParamEntry[]` 数组"**没有被采纳** —— 改成了**扁平静态列**
> （列名 = 节点名 + 参数名），理由与阶段 4 的"一个选项占一栏"完全相同：**一列 = 一个标量属性**才拿得到
> 编辑器的排序/批量改；而且 Boss 的 `Brain` 下有 3 个 `UseAbilityBehavior`，按行为类开列会撞成一列。
> 4 处硬编码避障启发式（§2.7 末尾）**没有进表**（属于"AI 手感"保护值）。
> 实测与两次回退验证见 [config.md](config.md) 的「配置表阶段 5 实测」、
> 设计记录见 [config-schema.md](config-schema.md) §5 阶段 5。
>
> **阶段 6（首领增强的"节点驱动抽奖"）**：`NodeConfig` 的三个空壳字段与 `StageDefinition.PressureUpgradeTags`
> **全部接进玩法**；`PressureModConfig` 新增 `Tags` 列、`Weight` 真的加权；**两个抽取点拆开**
> （Boss 开场词条只抽行为型 = Boss Build，Pressure 转换只抽数值型 = §7.5）。
> 对应本文的 **#11**（§2.3 / §5-② 的那几行）与 §4 的"最该先做的三张"。
> 实测见 [config.md](config.md) 的「配置表阶段 6 实测」、设计记录见 [config-schema.md](config-schema.md) §5 阶段 6。
>
> 阶段 3（统一单位表 + 技能表）做完之后，下面这些盘点结论**已经处理**，读本文时请对照本节：
>
> | 本文条目 | 阶段 3 的处理 |
> |---|---|
> | #9 技能/能力表（⏳ 未做，优先级高） | ✅ **已做**：`Config/Tables/Skills/` **25 行**（5 玩家 + 16 单位 + 4 首领行为词条），`AbilityConfig` 含通用项 + `Params` + `AttackParams`；由 `UnitConfig.Skills`（**资源引用**）指向 |
> | #10 敌人按敌种表（⏳ 未做，优先级高） | ✅ **已做**：`Config/Tables/Enemies/` 10 行（1 全局 + 9 敌种），`EnemyConfig : UnitConfig`，生成时"先 ApplyTo 再 AddChild" |
> | #12 首领行为/技能表（🔴 新发现） | ✅ **已做**：4 条行为型词条并入**技能表**（`bhv_awakened` / `bhv_summoner` / `bhv_screaming` / `bhv_charging`，`ApplyHandler` 非空），`BossPerkCatalog` 改成表驱动 + 处理器注册表 |
> | #11 首领增强表（数值型 Pressure，⏳ 未做） | ✅ **阶段 6 已做完**：`Config/Tables/Pressure/` 3 行（`pmod_berserk` / `pmod_fortress` / `pmod_swift`），`Weight` 真的加权、新增 `Tags` 列；**抽取点已与行为型拆开**（Boss 开场词条只抽行为型，Pressure 转换只抽数值型）；`NodeConfig` 的三个字段全部接进 `StageController.AddPressureForNode` |
> | #24 单位本体表（建议并入 #10） | ✅ **已做**：抽成 **`UnitConfig` 基类**（生命 / 攻击四项 / 移速 / Tags / 持有技能 / 表现），`EnemyConfig` / `BossConfig` / `SummonConfig` / `CharacterConfig` 全部继承它 |
> | #19 召唤物表（🔴 优先级中） | ✅ **已做**：`Config/Tables/Summons/summon_drone.tres`（含 `LifetimeSeconds = 25`） |
> | §2.2 的 `AllowedEnemies` 按中文显示名匹配（"这里必须一起修"） | ✅ **已修**：改成 `StageDefinition.AllowedEnemyIds`（按敌人表 Id），生成项与行按**预制体路径**配对；未知 Id 开局写警告 |
> | §5-④ 敌人全局倍率**静默换挡**（0.75→1f / 0.3→0.8f 且无日志） | ✅ **已修**：`BalanceConfig` 的回退值对齐到 0.75 / 0.3，并新增"敌人倍率来源"日志 |
> | §5-② 死字段：`BalanceConfig.BossHealthBarScale` / `EliteBossHealthScale` / `StageDefinition.IsFinalBoss` / `EffectConfig.Value·Value2·Radius·TickSeconds` | ✅ **全部处理**：前两个删掉（`EliteBossHealthScale` 的职责搬进 `BossConfig.EliteCopyHealthScale`）、`IsFinalBoss` 删掉、效果表 4 列删掉（删完只剩注释的 `eff_knockback` 整行删除） |
> | §5-② `EnemyConfig.SpawnDisplayName` | ✅ 已删（身份键改成 `Id`） |
> | 「单位/敌种/AI」相关的其余条目（#13 首领阶段 / #14 精英表 / **#15 AI 行为表 ✅阶段5** / #16 刷怪生成项表 / #17 波次 / #18 投射物 / #20 角色↔技能矩阵 / #23 命中体 / #25 移动手感 …） | ⏸ **仍未做**（**#15 已在阶段 5 做完**，见文首更新节），其余本文结论继续有效 |
>
> 阶段 3 的完整证据（实测日志、逐项比对、回退验证）见 [config.md](config.md) 的「配置表阶段 3 实测」
> 与 [config-schema.md](config-schema.md) §5 阶段 3。
>
> ---
>
> ## ⚠️ 2026-10 更新 2：配置表阶段 4 已落地（本文再增 3 处过时结论）
>
> 阶段 4（武器 → 升级池 → 选项 的三层关联）做完之后，下面这些盘点结论**已经处理**：
>
> | 本文条目 | 阶段 4 的处理 |
> |---|---|
> | #28 武器 ↔ 升级池关联（🔴 新发现） | ✅ **已做，但形态与本文建议相反**：**新开了一张池表** `Config/Tables/UpgradePools/pool_*.tres`（3 行 = 一行一个池），由 `WeaponConfig.Pool` **正向**指过来（用户拍板；本文当时建议"照设计文档的反向指做、不新开表"）。池的成员由**选项行**的 `RewardConfig.Pool` 声明 —— **一个选项占一行 + 一个引用列**。详见 §2.19 的更新块 |
> | §5-② `RewardDefinition.PoolTags`（纯死字段） | 🗑 **已删除**（含 19 行 `.tres` 里的 `PoolTags = …`、`RewardCatalog` 三个工厂的 `tag` 参数与 15 个调用点）；语义由 `RewardConfig.Pool` + `RequiredWeaponTags` 取代 |
> | §5-② `RewardDefinition.ConflictsWith`（死代码） | ✅ **已接上**：`RewardConfig` 新增 `ConflictsWith` 列，`RewardEffectRegistry.Compile` 填充（`RewardPool.Roll` 早就在读） |
> | §2.20 `Weight` 加权抽取退化 | ⏸ **仍是老样子**（19 条全 1.0，**故意不动** —— 改了抽样分布会变） |
> | §6.5「与 config-schema.md 的差异」里关于武器↔池的两行 | ⚠️ **两边的建议都没被采纳**，以阶段 4 的实现为准 |
>
> 阶段 4 的完整证据（实测日志、逐位比对、第一层拦截实验、两次回退验证）见 [config.md](config.md) 的「配置表阶段 4 实测」。

>
> 计数口径：**一个文件夹 = 一张表**（[config-tooling.md](config-tooling.md) 的硬约束），
> 所以「表」= 一组「一行一个 `.tres`」的同构数据；只有 1 个常量、且不随内容变化的**不算表**（见 §3）。

---

## 0. 结论摘要

| 问题 | 结论 |
|---|---|
| 一共列出多少张表 | **47 张** |
| 已落地 | **8 张**（⚠️ 阶段 3 之后是 **12 张**，阶段 4 之后是 **13 张**，**阶段 5 之后是 14 张** —— 新增 `UpgradePools/pool_*.tres` 3 行与 `AiProfiles/ai_*.tres` 11 行，见 §2.19 / §2.7 与文首的更新节） |
| 设计里有但没做 | ~~**3 张**（技能表 T2 / 敌人按敌种表 T5 扩展 / 首领增强表 T9）~~ → ✅ **全部做完**：T2 与 T5 在阶段 3、T9（含"节点驱动"）在阶段 6 |
| **本文新发现** | **36 张**（`config-schema.md` 未列为独立表的）—— ⚠️ 其中 **#28 武器↔升级池已在阶段 4 落地**（形态与本文建议不同，见 §2.19）、**#15 AI 行为表已在阶段 5 落地**（形态同样与本文建议不同，见 §2.7） |
| 最该先做的三张 | ① **敌人按敌种表** ② **技能/能力表** ③ **AI 行为表** —— ✅ **三张全部做完**（① ② 在阶段 3、③ 在阶段 5） |
| 盘点发现的主要问题 | 见 §5：**6 处死字段**（2 个 Balance 字段 ✅阶段3 + `IsFinalBoss` + 效果表 4 列）、**1 处双真源静默换挡**（敌人全局倍率 ✅阶段3）、**3 处加权抽取退化**（`RewardConfig.Weight` / `BossPerk.Weight` / `ConflictsWith` ✅阶段4接上其一）、**3 处数值只靠 C# 默认值撑着**（`Pistol.tscn` / `PlayerInputConfig` / `EnemyConfig.MoveSpeedMultiplier`）、**1 处疑似未生效的列**（`Weapon.ProjectileSpeed`）、**11 处该改成资源引用的字符串外键**。🆕 阶段 4 另**删除**了 `PoolTags` 这处死字段（原表里没单列，混在 `RewardDefinition` 那一行里） |

**「已落地 8 张」的现状**（`Config/Tables/` 下 53 个行文件，索引全挂在 `Config/Balance.tres` 的 `[Export] XxxConfig[]` 数组上）：

> 「自身列数」= 该类自己声明的 `[Export]`；另有 `ConfigRow` 基类的 **6 列公共列**
> （`Id` / `Slot` / `DisplayName` / `DescriptionTemplate` / `Note` / `Params`，`ConfigRow.cs:16-34`）
> 被前 7 张表继承。`StageDefinition` **不继承** `ConfigRow`（它是 `Resource` 直系，`StageDefinition.cs:10`），
> 所以它没有 `Slot` / `Params`。

| 表 | 行数 | 行类 | 自身列数 | 含基类共 | 索引字段（`BalanceConfig.cs`） |
|---|---|---|---|---|---|
| `Weapons/` | 3 | `WeaponConfig` | 21 → **22**（阶段 4 加 `Pool`） | 28 | `Weapons`（:84） |
| `Characters/` | 3 | `CharacterConfig` | 10 | 16 | `Characters`（:88） |
| `Enemies/` | 1 | `EnemyConfig` | 10 | 16 | `Enemies`（:92） |
| `Effects/` | 10 | `EffectConfig` | 11 | 17 | `Effects`（:96） |
| `Nodes/` | 9 | `NodeConfig` | 15 | 21 | `Nodes`（:100） |
| `Upgrades/` | 19 | `RewardConfig` | 10 → **11**（阶段 4：−`PoolTags` +`Pool` +`ConflictsWith`） | 17 | `Upgrades`（:104） |
| `Meta/` | 5 | `MetaUpgradeConfig` | 7 | 13 | `MetaUpgrades`（:108） |
| `Stages/` | 3 | `StageDefinition` | 23 | 23 | `Stages`（:63） |
| 🆕 `UpgradePools/` | 3 | `UpgradePoolConfig` | 0（只有身份） | 6 | `UpgradePools`（阶段 4 新增） |

---

## 1. 总表：所有「能成表」的东西

图例：**✅已落地** · **⏳设计里有但没做** · **🔴新发现** · **⛔建议排除**（保留在 §3 说明理由）

| # | 表名（建议） | 职责 | 当前状态 | 当前数据在哪 | 字段数 | 消费者 | 优先级 |
|---|---|---|---|---|---|---|---|
| 1 | `WeaponConfig` | 武器基础数值 | ✅已落地 | `Config/Tables/Weapons/wpn_*.tres`（3 行） | 21(+6) | `RunController.EquipWeapon`（`RunController.cs:404-408`）、`StartScreen`（:60） | — |
| 2 | `CharacterConfig` | 角色数值与开关矩阵 | ✅已落地 | `Config/Tables/Characters/chr_*.tres`（3 行） | 10(+6) | `RunController.ApplyBalance`（:85-119） | — |
| 3 | `EnemyConfig` | 敌人（当前只有全局倍率一行） | ✅已落地（仅全局行） | `Config/Tables/Enemies/enemy_global.tres` | 10(+6) | `Balance.ScaleEnemy`（`Balance.cs:86-93`）、`EncounterController.cs:418` | — |
| 4 | `EffectConfig` | 效果与状态参数 | ✅已落地（**但 4 列无消费者**，见 §5-②） | `Config/Tables/Effects/eff_*.tres`（10 行） | 11(+6)（其中 `Value/Value2/Radius/TickSeconds` 死） | `StatusPresets.Create`（`StatusEffect.cs:43`）、`buff`/`burn` 等 | — |
| 5 | `NodeConfig` | 节点类型、分层、波数、商店价目、**Boss Pressure 三列** | ✅已落地 | `Config/Tables/Nodes/node_*.tres`（9 行） | 15(+6)（`BossPressurePoints/BossModifierRolls/BossModifierTags` **阶段 6 已全部接进玩法**） | `RewardService`（:21,33,53）、`NodePlacement`（:22,39）、**`StageController.AddPressureForNode`** | — |
| 6 | `RewardConfig` | 增幅项（强化/进化/异化） | ✅已落地 | `Config/Tables/Upgrades/*.tres`（19 行） | 11(+6)（`Weight` 仍无实际效果；`PoolTags` **阶段 4 已删**；`Pool` / `ConflictsWith` **阶段 4 新增**） | `RewardTable.Build` → `RewardPool.Roll` | — |
| 7 | `MetaUpgradeConfig` | 局外成长条目 | ✅已落地 | `Config/Tables/Meta/meta_*.tres`（5 行） | 7(+6) | `MetaProgress`（:33,37） | — |
| 8 | `StageDefinition` | 关卡（含 Pressure 规则） | ✅已落地 | `Config/Tables/Stages/stg_*.tres`（3 行） | 23（**不含**基类公共列）（其中 `IsFinalBoss` 是死字段） | `RunController.ResolveStages`、`StageController.ApplyStage` | — |
| 9 | `AbilityConfig`（T2） | 技能/能力的通用参数与专属参数 | ⏳设计里有但没做 | `Scenes/Player/Player.tscn:64-140`（**40 处**） + 9 个子类各自的 `[Export]` | ~50 | `Ability` 及 9 个子类、`PlayerAbilityInput`、`EnemyBrain` | **高** |
| 10 | `EnemyUnitConfig`（T5 扩展） | **按敌种的敌人本体数值** | ⏳设计里有但没做 | `Scenes/Enemies/*.tscn` 的 `Unit` 节点（10 个场景） | 6 公共 + 各 0–3 | `EncounterController.Spawn`（:413-439） | **高** |
| 11 | `PressureModConfig`（T9/T11） | 首领**数值型**强化池（Pressure，§7.5） | ✅**阶段 6 已落地** | `Config/Tables/Pressure/` **3 行**（`pmod_*`，含 `Weight` / `Tags` / `Modifiers`） | 4(+6) | `BossPerkCatalog.RollPressure`（由 `StageController.AddPressureForNode` 在**跨阈值时**调用） | — |
| 12 | `BossPerkConfig` | 首领**行为/技能型**构筑词条（Boss Build） | 🔴新发现 | `BossPerk.cs:41-125` 硬编码 7 条 | 7 | `BossPerkCatalog.Roll`（:130）、`EncounterController.cs:676` | **高** |
| 13 | `BossPhaseConfig` | 首领阶段阈值 / 各阶段数值与解锁技能 | 🔴新发现 | `BossPhaseController.cs:13-20` 默认值 + `Scenes/Enemies/Boss.tscn:167-171` | 6 | `BossPhaseController.EnterPhase`（:59-92） | 中 |
| 14 | `EliteConfig` | 精英修正（普通敌人精英化） | 🔴新发现 | `EliteModifier.cs:11-19` 默认值；`SkillElite.tscn:114-120` 全是 1.0（等于没配） | 7 | `EncounterController.cs:423` `new EliteModifier()` | 中 |
| 15 | `AiProfileConfig` | AI 决策参数（索敌半径/行为优先级/追击距离/舒适圈/撤退阈值…） | ✅**已落地（2026-10 阶段 5）** | `Config/Tables/AiProfiles/ai_*.tres`（**11 行** = 一行一个 AI 档案）；`UnitConfig.AiProfile`（资源引用）指过来 | 2 自有列 + **21 个行为槽列** + 6 继承列 | `AiProfileConfig.ApplyTo` → `EnemyBrain` + `Behavior`（由 `UnitConfig.ApplyTo` 调用） | ✅ 已完成 |
| 16 | `SpawnEntryConfig` | 每种敌人的刷怪成本 / 权重 / 解锁时间门 | 🔴新发现 | `Scenes/Main.tscn:75-132`（8 条 `EnemySpawnEntry`） | 7 | `EncounterController.PickEntry`（:296）/ `PickWaveEntry`（:379）/ `Spawn`（:403） | **高** |
| 17 | `WaveConfig` | 每波规模 / 类型数 / 波数 / 节奏 | 🔴新发现 | `EncounterController.cs:56-63` 默认值 + `Main.tscn:67-73`、`Scenes/Main.tscn:139-140`；`SpawnPlanner.cs:17-18` **const 重复一份** | 6 | `EncounterController.BeginWave`（:335-390） | 中 |
| 18 | `ProjectileConfig` | 投射物/弹道参数 | 🔴新发现 | `Scenes/Combat/Bullet.tscn:10-11`、`Scripts/Combat/Projectile.cs:13-23` | 6 | `Projectile._PhysicsProcess`（:46-85） | 中 |
| 19 | `SummonConfig` | 召唤物数值与存活时间 | 🔴新发现 | `Scenes/Summons/Drone.tscn:34-35,67-79,88` | 8 | `SummonAbility.OnActivate`（:46-65）、`SummonLifetime`（:11） | 中 |
| 20 | `CharacterAbilityMatrix` | 角色 ↔ 技能的启用/禁用矩阵 | 🔴新发现（表1 的一列，但建议独立成关系表） | `CharacterConfig.EnabledAbilityIds/DisabledAbilityIds`（`CharacterConfig.cs:41,44`） | 2 列 × 角色数 | `RunController.ApplyAbilityMatrix`（:452-486） | 中 |
| 21 | `CharacterArchetypeConfig` | 职业专属参数（目前只有近战伤害一行） | 🔴新发现（可并入表2） | `CharacterConfig.MeleeDamage`（`CharacterConfig.cs:48`） | 1 | `RunController.ApplyBalance`（:111-119） | 低 |
| 22 | `SpawnSceneConfig` | 生成项 ↔ 敌人预制体的绑定 | 🔴新发现（表16 的一列） | `Main.tscn:77,84,91,98,106,113,120,127` | 1 列 | `EncounterController.Spawn`（:413） | 低（并入 16） |
| 23 | `HitboxConfig` | 命中体的伤害 / 击退 / 策略 / 挂状态 | 🔴新发现 | `AttackHitbox.cs:13-24` 默认值；`Player.tscn:101-103,133-137`、各敌人场景、`Drone.tscn:77-79` | 9 | `AttackHitbox.Activate`（:99-119）、`MeleeAttackAbility`（:62-74） | 中 |
| 24 | `UnitConfig` | 单位本体数值（生命/护甲/护盾/减伤/击退抗性/标签） | 🔴新发现 | `Unit.cs:14-33` 默认值；各 `.tscn` 各一份 | 9 | `Unit._Ready`/`RefreshStats` | 中 |
| 25 | `CharacterMovementConfig` | 移动物理手感（重力/加速度/跳跃/击退衰减/转身锐度） | 🔴新发现 | `CharacterMovement.cs:12-25` 默认值；`Player.tscn:52`、各敌人场景 | 8 | `CharacterMovement._PhysicsProcess`（:127-177） | 中 |
| 26 | `WeaponSceneConfig` | 武器预制体自身的数值（与武器表重复） | 🔴新发现 | `Scenes/Weapons/Pistol.tscn:16-27` | 11 | `Weapon` 组件（表读不到时的回退基线） | **该删不该建**（见 §5-③） |
| 27 | `PlayerInputConfig` | 玩家操作手感（瞄准射线长度 / 默认 FOV / ADS 插值速度） | 🔴新发现 | `PlayerWeaponInput.cs:15-17`（**全是默认值，场景里没覆盖**） | 3 | `PlayerWeaponInput._Process`（:68,89-90） | 中 |
| 28 | `UpgradePoolLink` | 武器 ↔ 它的升级池（正向关联） | ✅**已落地（2026-10 阶段 4）** —— 形态与本文当时的建议不同：不是"给 `RewardConfig` 加一列"，而是**新开一张池表** `Config/Tables/UpgradePools/pool_*.tres`（3 行，一行一个池），由 `WeaponConfig.Pool` **正向**指过来 | `Config/Tables/UpgradePools/pool_*.tres`（3 行）+ `WeaponConfig.Pool` + `RewardConfig.Pool` | `UpgradePoolConfig` 0 自有列（只有身份）+ 两侧各 1 个引用列 | `RewardDefinition.AllowsWeapon`（三级回退） | ✅ 已完成 |
| 29 | `RewardPoolFilter` | 奖励池的抽取规则参数 | 🔴新发现 | `RewardPool.cs`（无导出字段）；`RewardChoiceCount` 在 `BalanceConfig.cs:111` | 3 | `RewardPool.Roll`（:33-86） | 中 |
| 30 | `TriggerRuleConfig` | 触发规则的冷却与每链次数上限 | 🔴新发现 | `RunBuild.cs:78-80`（`Cooldown` / `MaxTriggersPerChain=4`） | 2 | `TriggerRule.OnRaised`（:104-132） | 低 |
| 31 | `MapManagerConfig` | 导航烘焙 / 点位供给 / 落点容差 | 🔴新发现 | `MapManager.cs:21-33` 默认值（**Main.tscn 的 MapManager 节点一处都没覆盖**） + 硬编码 `2f/10f/28/0.7/1.15/1.3/1.65`（`NodePlacement.cs:63-91`）、`3/12/1.5/6/0.3`（`MapManager.cs:179-202`） | 4+11 | `MapManager.Bake`（:73-127）、各点位方法 | 中 |
| 32 | `KillZoneConfig` | 区域杀尺寸 | 🔴新发现 | `KillZone.cs:16-22` 默认值；`MapManager.cs:143` 用 `KillPlaneY - 30f` 算 `CenterY` | 3 | `KillZone._Ready`（:24-42） | 低 |
| 33 | `ShardSettlementConfig` | 碎片结算公式 | 🔴新发现（设计 §1.2-H7 提过，未列为独立表） | `RunSettlement.cs:10,13,16`（`const 10/3/50`） | 3 | `RunSettlement.ShardsFor`（:19） | 中 |
| 34 | `MetaCostCurve` | 局外成长的价格曲线 | 🔴新发现（表7 的一列，但值得单独看） | `MetaUpgradeConfig.BaseCost/CostGrowth`（:31,34）+ `MetaProgress.cs:37` 的 `?? (10 * (level + 1))` | 3 | `MetaProgress.CostOf`（:36） | 低 |
| 35 | `MetaProgressConfig` | 局外成长的**软件设置**（存档路径等） | 🔴新发现 | `MetaProgress.cs:12`（`user://meta.cfg`） | 1 | `MetaProgress.Load/Save` | **低 / 见 §3** |
| 36 | `NodePlacementConfig` | 节点摆放启发式（螺旋外扩参数） | 🔴新发现 | `NodePlacement.cs:63-93`（`2f/10f/28/0.7/1.15/0.25/1.3/1.65` 全硬编码） | 8 | `NodePlacement.PlaceSafely` | 低 |
| 37 | `RewardNodeConfig` | 奖励节点的交互与配色 | 🔴新发现 | `Interactable.cs:26-27,48,51` + `RewardNode.cs:110`（材质路径拼接） | 6 | `Interactable.Appear/Disappear`、`RewardNode.BuildPalette` | 低 |
| 38 | `OverlayScreenConfig` | 弹窗开合手感（保护期/缓动/起始缩放） | 🔴新发现 | `OverlayScreen.cs:36-55` 默认值（场景里没覆盖） | 5 | `OverlayScreen.BeginOpen/PlayFade` | 低 |
| 39 | `UiLayerConfig` | 每个界面的层级（HUD/Panel/Modal） | 🔴新发现 | `OverlayScreen.ScreenLayer`（:36）+ `UiService.cs:130,143,157`（运行时写死 Modal） | 1 列 | `UiRouter.Sync`（:87-102） | 低 |
| 40 | `CameraFeelConfig` | 相机手感 | ⛔建议排除（见 §3-①） | `CameraRig.cs:15-34` + `CameraRig.tscn:10-15`（`spring_length=8.0`、`margin=0.2`、`fov=78.0`） | 9 | `CameraRig._Input/_PhysicsProcess` | **低** |
| 41 | `DamageTextConfig` | 伤害跳字表现 | ⛔建议排除（见 §3-②） | `BalanceConfig.cs:114-117`（**已在 Balance**） | 4 | `WorldOverlay.cs:188-215` | **低** |
| 42 | `HealthBarConfig` | 血条表现 | ⛔建议排除（见 §3-②） | `BalanceConfig.cs:120-124,157-158`（**已在 Balance**） | 6 | `WorldOverlay.CreateBar`（:319-339）、`PlayerHud.cs:227` | **低** |
| 43 | `HudTextConfig` | HUD 文案模板与配色 | ⛔建议排除（见 §3-②） | `PlayerHud.cs:95-153,211` | 多 | `PlayerHud._Process` | **低** |
| 44 | `InputBindingConfig` | 按键映射 | ⛔建议排除（见 §3-④） | `project.godot [input]`（13 个动作，L44-112） | 13 | 各 Input 脚本 | **低** |
| 45 | `FxReferenceConfig` | 特效/音效资源引用 | ⛔建议排除（见 §3-⑤） | `Pistol.tscn:5,35`（枪口火光）、`Bullet.tscn:5-6`（拖尾）、`Explosion.tscn:4`（网格）、`Projectile.cs:166`（爆炸场景路径硬编码） | 4 | 各预制体 | **低** |
| 46 | `CoreRulesConfig` | 结算顺序/上限/链深这类规则常量 | ⛔建议排除（见 §3-③） | `Unit.cs:21,173-175`（减伤上限 0.9）、`CombatTypes.cs:25-28,58`（碰撞层位、`MaxDepth=4`）、`WeightedPick.cs:21` | 5 | `Unit.ReceiveHit`、`EventContext.Child` | **低** |
| 47 | `GameLogConfig` | 日志刷盘间隔与开关 | ⛔建议排除（见 §3-⑥） | `GameLog.cs:67`（`_flushTimer >= 1f`） | 1 | `GameLog` | **低** |

---

## 2. 逐张表展开（未落地 + 新发现的）

> 只展开 **#9–#39**（未落地 / 新发现）。已落地的 8 张见 `Config/Tables/`，表结构见 [config-schema.md](config-schema.md) §3。

### 2.1 【#9 · T2】技能 / 能力表 —— `AbilityConfig`

**为什么是表**：`Ability` 有 9 个子类，每个子类的 `[Export]` 字段都不一样（`DashAbility.Distance` 与
`AreaAttackAbility.KillSelf` 毫无关系），但**「哪个单位装了哪个技能、参数是多少」完全是数据** ——
现在这份数据分散在 `Player.tscn` 的 40 处赋值 + 10 个敌人场景里，加一个技能要动 `.tscn`。

**字段清单（通用项，`Ability.cs`）**

| 字段 | 类型 | 当前值（`Player.tscn`） | 用途 |
|---|---|---|---|
| `Aim` | `Ability.AimSource` | `AbilityDash=0`(Facing), `AbilityBurst=2`(Crosshair) | 释放方向来源（`Ability.cs:132-145`） |
| `Cooldown` | `float` | Dash 1.2 / Burst 10.0 / Melee 0.75 / Summon 5.0 / Control 8.0 | 冷却（`Ability.cs:32`） |
| `Windup` | `float` | Burst 0.0 / Melee 0.18 / Summon 0.25 / Control 0.25 | 前摇（:33） |
| `Recover` | `float` | Burst 0.1 / Melee 0.25 / Summon 0.25 / Control 0.4 | 后摇（:34） |
| `AbilityId` | `string` | **全都没填**（靠 `Ability.cs:113-116` 用类名兜底） | 矩阵与表寻址的键 |
| `Enabled` | `bool` | Dash/Burst `true`；Melee/Summon/Control `false` | 默认开关（:38） |
| `ApproachMinRange` / `ApproachMaxRange` | `float` | 只有 Control 填了（0 / 8.0） | `UseAbilityBehavior` 的决策提示（:41-43） |

**字段清单（子类专属，走 `Params` 键值对）**

| 子类 | 字段 | 当前值（玩家 / Boss / 敌人） |
|---|---|---|
| `DashAbility`（:12-14） | `Distance` / `Duration` / `InvulnerableTime` | 6.0 / 0.22 / 0.18（`Player.tscn:68-70`） |
| `BurstFireAbility`（:16,19） | `Duration` / `BurstInterval` | 2.5 / 0.06（`Player.tscn:75-76`） |
| `MeleeAttackAbility`（:12-14） | `AttackRange` / `ActiveTime` / `DamageScale` | 2.4 / 0.12 / 1.0（`Player.tscn:94`）；Boss 3.4（`Boss.tscn:93`） |
| `SummonAbility`（:12-17） | `SummonScene` / `Count` / `Radius` / `MaxAlive` | Drone / 1 / 2.5 / 3（`Player.tscn:113-116`）；Boss 3 / 6.0 / 不限（`Boss.tscn:161-163`） |
| `AreaAttackAbility`（:12-14） | `ActiveTime` / `KillSelf` / `TriggerRadius` | 0.15 / false / 8.0（`Player.tscn:123-124`）；自爆兵 0.12 / **true** / 2.4（`Exploder.tscn:65-67`） |
| `ChargeAttackAbility`（:12-13） | `ChargeSpeed` / `MaxDistance` | 冲锋兵 16.0 / 14.0（`Charger.tscn:66-67`）；Boss 18.0 / 18.0（`Boss.tscn:138-139`） |
| `LeapAttackAbility`（:12-15） | `JumpVelocity` / `HorizontalSpeed` / `ActiveTime` / `MaxAirTime` | 跳兵 9.0 / 8.5 / 0.22 / 3.0（`JumpingMelee.tscn:65-67`） |
| `ProjectileAttackAbility`（:13-17） | `MinRange` / `MaxRange` / `AimHeight` | 远程兵 3.0 / 18.0 / 0.9（`RangedEnemy.tscn:71-73`） |
| `BuffAlliesAbility`（:12-15） | `Radius` / `BuffDuration` / `StatusId` / `IncludeSelf` | 技能精英 11.0 / 5.0 / "Empowered" / true（`SkillElite.tscn:107-110`） |

**落地建议**：与 [config-schema.md](config-schema.md) §3 T2 一致 —— 通用项做成固定列，
子类专属走 `Params`（`ConfigRow.Params` 已存在，`ParamEntry` 已落地并在 `mut_chain.tres` 上验证过）。
**关键前置**：`Set()` 的键名必须与 `Ability` 子类属性名完全一致（`Ability.cs:35` 的 `AbilityId` 是寻址键，
而它现在是空的 —— 要么给每个场景补 `AbilityId`，要么把 `AbilityId` 也搬进表里并由表回填）。

### 2.2 【#10 · T5 扩展】敌人按敌种表 —— `EnemyUnitConfig`

**为什么是表**：10 个敌人场景各自的 `Unit` 节点就是「一行」的雏形。**公共列高度一致**（下表第一段），
正是「每行一份」的形状。

**公共列（10/10 个场景都有，`Scenes/Enemies/*.tscn`）**

| 字段 | 类型 | 各敌种当前值 |
|---|---|---|
| `MaxHealth` | `float` | Chaser 60 / FastMelee 35 / Exploder 25 / JumpingMelee 50 / Guard 130 / Screamer 70 / Charger 90 / RangedEnemy 45 / SkillElite 200 / Boss 900 |
| `MoveSpeed` | `float` | 4.2 / 6.5 / 5.5 / 3.6 / 2.6 / 3.0 / 3.2 / 3.4 / 2.9 / 3.6 |
| `Tags` | `string[]` | Chaser `Melee,Chaser` / Guard `Melee,Guard,Tank` / Boss `Boss,Melee` … |
| `Faction` | `Faction` | 全部 `1`(Enemy) |
| `ArmorDamageReduction` | `float` | 默认 0.3；Guard/SkillElite 0.35；Boss 0.3 |
| `KnockbackResistance` | `float` | 默认 0；Guard 0.85 / SkillElite 0.9 / Boss 1.0 |

**非公共列（只有部分敌种有）**

| 字段 | 出现在 | 值 |
|---|---|---|
| `MaxArmor` | 4/10（Boss 200 / SkillElite 90 / Guard 60 / Charger 30） | — |
| `MaxShield` | **0/10**（`Unit.cs:18` 默认 0，没人写） | — |
| `DamageReduction` | **0/10**（`Unit.cs:24` 默认 0，没人写） | — |

**引用列（现在是「场景内的路径」，该变成「表的外键」）**

| 字段 | 类型 | 用途 |
|---|---|---|
| `ScenePath` | `string` | 敌人预制体（现在写死在 `Main.tscn` 的生成项上） |
| `SpawnEntryId` | `string` | 与 #16 `SpawnEntryConfig` 对接（取代 `DisplayName` 字符串匹配） |
| `AiConfigId` | `string` | 与 #15 `EnemyAiConfig` 对接 —— ⚠️ **实际落地成了资源引用**：`UnitConfig.AiProfile`（`AiProfileConfig?`），**不是字符串 id**（阶段 5） |

**⚠️ 这里有一处必须一起修的**：`StageDefinition.AllowedEnemies` 按**显示名**匹配
（`StageDefinition.cs:31,94-95` → `EncounterController.ApplyEntryFilter` :589）。
配置文件 `stg_outer.tres` 里写的是 `PackedStringArray("基础近战","快速近战","自爆")` ——
改一个中文显示名就**静默放行/屏蔽**敌人。做 #10 时应当同时换成 `Id` 匹配
（[config-schema.md](config-schema.md) §3 T6 的 `AllowedEnemyIds` 就是为此预留的，当时因为「消费者是敌人表」而没加）。

**边界建议**：`Unit.MaxHealth/MaxArmor/MoveSpeed/Tags` 这类「一行一份」的搬进表；
`CollisionShape3D` 尺寸、网格、材质**不要搬**（属美术，见 §3-⑤）。

### 2.3 【#11 · T9/T11】首领增强表（数值型）—— `BossModifierConfig` ✅ **阶段 6 已落地为 `PressureModConfig`**

**⚠️ 与「首领行为表」（#12）必须分离**：`BossPerk.cs` 的注释把这条写成硬约束
（《关卡核心文档》§7.1）：**数值型强化（Pressure）不允许与行为/技能型词条合并成一个无法区分来源的池**。

> ✅ **2026-10 阶段 6 已全部落地**（本节以下是当时的盘点原文，保留作为"为什么这么设计"的依据）：
> 表名定为 **`PressureModConfig`**（`Config/Tables/Pressure/pmod_*.tres`，3 行），字段为
> `Id` / `DisplayName` / `DescriptionTemplate` / **`Weight`** / **`Tags`** / `Modifiers : StatOverride[]` / `Note`。
> 与本节建议字段清单的差异：**没有** `TargetStat` / `Operation` / `Value` / `MaxLevel` / `Source` ——
> 因为一行要改多项（狂暴同时改伤害与攻速），标量字段表达不了，改用**与增幅项表共用的 `StatOverride[]`**
> （`{Stat, Operation, Value}`，已在 `upg_evo_blast` 上验证过）；`MaxLevel` 与 `Source` 当时没有消费者，
> 按"不留不生效的列"纪律**没加**（多次抽到同一条会叠加，这是 §7.3"每次转换各抽一次"的自然结果）。
> 三个「壳」也全部接上了（见下表的"阶段 6"列），`StageDefinition.PressureUpgradeTags` 同理。

**原盘点（改前状态）：这张表根本不存在。** 已有的三个「壳」是空字段：

| 壳 | 位置 | 改前的消费者 | **阶段 6 之后** |
|---|---|---|---|
| `NodeConfig.BossPressurePoints` | `NodeConfig.cs:55` | **无**（`StageController.AddPressureForNode` 读的是 `StageDefinition` + `Balance`） | `StageController.ResolveBasePressure`（三层取值的第一层）；`node_evolution` / `node_mutation` 填 150 |
| `NodeConfig.BossModifierRolls` | `NodeConfig.cs:58` | **无** | `AddPressureForNode` 的「额外 N 次」段（与阈值无关） |
| `NodeConfig.BossModifierTags` | `NodeConfig.cs:61` | 只在日志里打印 | `StageController.PickTags` → `BossPerkCatalog.FilterPressure`（**真的过滤候选**） |
| `StageDefinition.PressureUpgradeTags` | `StageDefinition.cs:92` | 只进日志 | 同上的第二层（节点行留空时才用它） |

**当时的建议字段清单**（对照上面 ✅ 框里的实际落地）：

| 字段 | 类型 | 用途 |
|---|---|---|
| `Id` | `string` | 行键（`bmp_hp` / `bmp_dmg` / `bmp_aspd` …）→ 实际用 `pmod_berserk` / `pmod_fortress` / `pmod_swift` |
| `TargetStat` | `string` | 目标属性键 → 实际并入 `Modifiers` 的 `StatOverride.Stat` |
| `Operation` | `ModifierOperation` | `Multiply` / `Add` → 实际并入 `StatOverride.Operation` |
| `Value` | `float` | 每级强化量 → 实际并入 `StatOverride.Value` |
| `MaxLevel` | `int` | 该条最多叠几级（0 = 不限）→ **没加**（无消费者） |
| `Weight` | `float` | 抽中权重（接 `WeightedPick`）→ ✅ 落地，现有 3/2/2 |
| `Tags` | `string[]` | 与 `StageDefinition.PressureUpgradeTags` 对接 → ✅ 落地（`Offense` / `Defense` / `Mobility`） |
| `Source` | `enum` | `Pressure` → **没加**（两套来源已经靠"两张表 + 两个抽取点"分开，不需要行内枚举） |

**落地顺序建议**：先做「数值池」（本表）→ 再把 `NodeConfig.BossPressurePoints` / `BossModifierRolls`
接进 `StageController.AddPressureForNode` → 最后才放宽 `PressureUpgradeTags` 的语义。
✅ **两步都在阶段 6 一起做完了**（顺序与建议一致：先让池能抽，再接节点驱动）。

### 2.4 【#12】**新发现** 首领行为/技能表（Boss Build）—— `BossPerkConfig`

**为什么是表**：7 条词条现在是一个 C# 数组（`BossPerk.cs:41-125`），每条 = `Id` + `Name` + `Description` +
`Weight` + 一个 `Action<BossPerkContext>` 闭包。**闭包不能序列化**，所以照
[config-schema.md](config-schema.md) §2.2 的两层结构走：磁盘行 → 运行期 `BossPerk`。

**字段清单**

| 字段 | 类型 | 当前值（`BossPerk.cs:41-125`） | 用途 |
|---|---|---|---|
| `Id` | `string` | `berserk` / `fortress` / `swift` / `awakened` / `summoner` / `screaming` / `charging` | 行键 |
| `Name` | `string` | 狂暴 / 铁壁 / 疾行 / 提前觉醒 / 召唤师 / 尖啸强化 / 冲锋强化 | HUD（`PlayerHud.cs:133-135`） |
| `Description` | `string` | 「伤害 +25%，攻速 +20%」… | 卡片文案 |
| `Weight` | `float` | **7 条全是默认 `1f`**（`:15`）→ 等概率 | 加权抽取 |
| `ApplyHandler` | `string` | 需新增：`StatSet` / `EnableAbility` / `PressureMod` | 处理器 id |
| `Modifiers` | `StatOverride[]` | 狂暴 `DamageMultiplier×1.25` + `AttackSpeedMultiplier×1.2`（:50-51）；铁壁 `MaxHealthMultiplier×1.4` + `MaxArmorAdd+150`（:61-62）；疾行 `MoveSpeedMultiplier×1.35`（:70） | 数值型词条 |
| `HandlerParams` | `ParamEntry[]` | 尖啸/冲锋要「EnableAbility = AbilityScream / AbilityCharge」（:106,119）；提前觉醒 `InitialLevel += 1`（:81）；召唤师 `AddsPerLevel += 1` + `AddSpawnLevel -= 1`（:94-95） | 行为型词条 |

**落地建议**：`StatSet` 处理器可直接复用 `RewardEffectRegistry` 的写法（已落地，`RewardEffectRegistry.cs:25-39`）；
「按节点名启用技能」需要一个新处理器（3 行）。⚠️ **不要与 #11 合表**。

### 2.5 【#13】**新发现** 首领阶段表 —— `BossPhaseConfig`

| 字段 | 类型 | 当前值 | 位置 |
|---|---|---|---|
| `PhaseFractions` | `float[]` | `{1.0, 0.65, 0.33}` | `BossPhaseController.cs:13`；`Boss.tscn:167` |
| `Phase1Abilities` | `string[]` | 空（阶段 0 只靠场景里的 `Enabled`） | `:15` |
| `Phase2Abilities` | `string[]` | `AbilityScream, AbilityCharge` | `:16`；`Boss.tscn:168` |
| `Phase3Abilities` | `string[]` | `AbilitySummon` | `:17`；`Boss.tscn:169` |
| `PhaseDamageMultipliers` | `float[]` | `{1, 1.2, 1.5}` | `:19`；`Boss.tscn:170` |
| `PhaseAttackSpeedMultipliers` | `float[]` | `{1, 1, 1.2}` | `:20`；`Boss.tscn:171` |

**判断**：够成一张表（6 个字段、3 阶段、只服务 Boss），但**只有 1 个 Boss**，做表的收益是
「加第二个 Boss 时不用改代码」。**优先级：中**。建议做成 `Config/Tables/Boss/phases_*.tres`，
一行 = 一个 Boss 的阶段方案。

### 2.6 【#14】**新发现** 精英表 —— `EliteConfig`

`EliteModifier.cs:11-19` 默认值：`HealthMultiplier=2.5` / `ArmorAdd=120` / `DamageMultiplier=1.4` /
`MoveSpeedMultiplier=0.9` / `KnockbackResistance=0.6` / `ModelScale=1.35` / `Tint=(0.85,0.3,0.9)`。

**问题**：`EncounterController.cs:423` 是 `enemy.AddChild(new EliteModifier())` —— **用代码默认值**，
所以「精英有多强」现在只能改 C# 重编译。而 `SkillElite.tscn:114-120` 自己配了一份**全是 1.0/0.0**
的（等于关掉），说明「按敌种/按档次配多套精英」是真实需求。**建议成表**（7 字段），
并允许 `SpawnEntryConfig` 指一个 `EliteConfigId`。**优先级：中**。

### 2.7 【#15】**新发现** AI 行为表 —— ✅ **已落地（2026-10 配置表阶段 5），行类叫 `AiProfileConfig`**

> **本节结论已落地，形态与下面的"落地建议"不同，保留原文作历史依据。**
>
> | 项 | 实际实现 |
> |---|---|
> | 表目录 / 行数 | `Config/Tables/AiProfiles/ai_*.tres` —— **11 行**（9 敌种 + 首领 `ai_boss` + 召唤物 `ai_drone`），一行一个 AI 档案 |
> | 行类 | `Scripts/Config/AiProfileConfig.cs`（`[Tool]` + `[GlobalClass]`，继承 `ConfigRow`） |
> | 关联方式 | `UnitConfig.AiProfile`（**资源引用**，阶段 3 预留的名字 `AiProfileId` 改成了资源引用 —— 与 `Skills` 同一套） |
> | 套用点 | `UnitConfig.ApplyTo` 里一行 `ApplyAiProfileTo(unit)` → `AiProfileConfig.ApplyTo` 找 `EnemyBrain` 并写 Brain + 各 `Behavior`。**没有碰 `EncounterController`** |
> | 字段形态 | **扁平静态列**（列名 = `节点名 + 参数名`，如 `ChaseStopDistance`），**不是**本文建议的 `Behaviors: ParamEntry[]` 数组。理由：① 一列 = 一个标量属性才拿得到编辑器的排序/批量改，数组列拿不到（阶段 4 还实测过 `Array[Object]` 的插件报错）；② Boss 的 `Brain` 下有 **3 个** `UseAbilityBehavior`，按行为类开列会撞成一列 |
> | 逃生口 | 基类的 `Params`，键 = `节点名.属性名`（`MinRange` / `MaxRange` / `AbilityNodeName` 没有独立列，走这里） |
> | 4 处硬编码避障启发式 | ⏸ **没有进表**（探针 2.4 / 抬高 0.6 / 翻转 1.2s / 前向权重 0.35）—— 它们是"AI 手感"保护值，与 §1.3 的 B 类同性质 |
> | 盘到的参数 | **25 个**；场景侧赋值：敌人 10 个场景 **53 处** + 无人机 5 处 + 训练场 4 处 = **62 处** |
> | 语义 | **数值列 0 = 不覆盖**（`RetreatHealthThreshold` 用 `-1`）；**`Priority <= 0` = 整个槽位不生效**；**不增删行为节点**（拓扑仍在场景里） |
>
> 实测（含"伪造 -1 再 ApplyTo"的硬证据探针）与两次回退验证见
> [config.md](config.md) 的「配置表阶段 5 实测」；设计记录见 [config-schema.md](config-schema.md) §5 阶段 5。
>
> **⚠️ 原文（当时的盘点与建议）保留如下**：

**为什么是表**：AI 参数现在**每个敌人场景各配一份**，而它们描述的是「这个敌种怎么打」——
正是「每行一份」的形状。10 个场景里的 AI 参数：

| 敌种 | `EnemyBrain` | 行为节点（`Priority` / 参数） | 能力参数 |
|---|---|---|---|
| Chaser | `SearchRange=40`（:49） | Melee `100` / Chase `10` `StopDistance=1.6`（:53,58） | 冷却 1.3 / 前摇 0.4 / 后摇 0.3 / 射程 2.3（:62-65） |
| FastMelee | `40`（:49） | Melee `100` / Chase `10` `StopDistance=1.5`（:53,58） | 0.8 / 0.22 / 0.2 / 2.1（:62-65） |
| Exploder | `40`（:49） | UseAbility `100` / Chase `10` `StopDistance=1.0`（:53,58） | 30.0 / 0.35 / 0.1 / `TriggerRadius=2.4` / `KillSelf=true`（:62-67） |
| JumpingMelee | `40`（:49） | UseAbility `100` / Chase `10` `StopDistance=1.6`（:53,58） | 2.2 / 0.15 / 0.5 / 跳跃 9.0 / 水平 8.5（:62-67） |
| Guard | `30`（:59） | Melee `100` / Chase `10` `StopDistance=2.0`（:63,68） | 1.6 / 0.5 / 0.4 / 2.4（:72-75） |
| Screamer | `30`（:49） | UseAbility `100` / KeepDistance `20` `PreferredDistance=6.0` `Tolerance=1.5`（:53,58-59） | 4.0 / 0.6 / 0.8（:63-65） |
| Charger | `45`（:50） | UseAbility `100` / Chase `10` `StopDistance=1.9`（:54,59） | 2.6 / 0.7 / 0.6 / `MaxDistance=14.0`（:63-67） |
| RangedEnemy | `32`（:48） | **Retreat `90` `HealthThreshold=0.3` `KeepDistance=14.0`**（:52-54）/ Ranged `100`（:58）/ KeepDistance `20` `PreferredDistance=9.0` `Tolerance=2.0`（:62-64） | 1.8 / 0.45 / 0.35 / `MinRange=3.0` `MaxRange=18.0`（:68-72） |
| SkillElite | `45`（:67） | Melee `100` / UseAbility `90` `AbilityNodeName=AbilityBuff` / Chase `10` `StopDistance=2.1`（:71,76,81） | 1.6 / 0.45 / 0.35 / 2.5（:85-88） |
| Boss | `60`（:62） | UseAbility×3 `120/110/60`（`AbilityScream`/`AbilityCharge`/`AbilitySummon`）/ Melee `100` / Chase `10` `StopDistance=2.4`（:66-86） | 见 §2.1 |
| Drone（召唤物） | `SearchRange=22` `TargetRefreshInterval=0.3`（`Drone.tscn:53-54`） | Attack `100` / Chase `10` `StopDistance=1.6`（:58,63） | 0.9 / 0.15 / 0.2 / 1.8（:67-70） |

**公共列（`Behavior.cs:12-18`）**：`Priority` / `MinRange` / `MaxRange` —— **10/10 个敌人都配了 `Priority`**。

**行为专属列**：

| 行为类 | 字段 | 含义 |
|---|---|---|
| `ChaseBehavior`（:9-10） | `StopDistance` / `SpeedScale` | 追到多近停 |
| `KeepDistanceBehavior`（:10-14） | `PreferredDistance` / `Tolerance` / `SpeedScale` / `Strafe` / `StrafeScale` | 舒适圈 |
| `RepositionBehavior`（:9-13） | `MinDistance` / `MaxDistance` / `ArriveTolerance` / `RestartDelay` / `SpeedScale` | **只有 Drone 之外的场景没人用**（未确认是否有敌人用它） |
| `RetreatBehavior`（:10-12） | `HealthThreshold` / `SpeedScale` / `KeepDistance` | 撤退阈值 |
| `UseAbilityBehavior`（:14） | `AbilityNodeName` | 绑定哪个能力节点 |
| `MeleeAttackBehavior` / `RangedAttackBehavior` | 无 | 靠能力自己的射程 |

**还有 4 处硬编码的避障启发式**（`Behavior.cs:70,78,89,96`）：
`probeDistance=2.4` / 射线起点抬高 `0.6` / 侧移翻转周期 `1.2s` / 前向权重 `0.35`。
这些属于「AI 手感」，**建议一起进表**（否则表只覆盖一半）。

**落地建议**：一行 = 一个敌种的 AI 配置（`ai_chaser.tres`），字段含 `Behaviors: ParamEntry[]`
（`Priority` + 该行为的参数）与 `Brain: SearchRange/TargetRefreshInterval`。
或者保守做法：一行 = 一个行为实例（粒度更细，但与 `.tscn` 的节点树对应关系变复杂）。
**优先级：高**（10 个敌人 × 每个 8–15 个参数 = 100+ 处，且都是纯玩法数值）。

### 2.8 【#16】**新发现** 刷怪生成项表 —— `SpawnEntryConfig`

**为什么是表**：`EnemySpawnEntry` 是 `Node` 而不是 `Resource`，8 条挂在 `Main.tscn:75-132` 上。
它**天然有 7 个字段**（`EnemySpawnEntry.cs:11-27`），每条被赋值 2–5 个 —— 这就是「一行」的形状。

| 生成项 | 场景 | `DisplayName` | `Cost` | `Weight` | `MinElapsedSeconds` | `EliteChance` | 行号 |
|---|---|---|---|---|---|---|---|
| `SpawnChaser` | Chaser | 基础近战 | 2 | 3.0 | — | — | `Main.tscn:75-80` |
| `SpawnFast` | FastMelee | 快速近战 | 1 | 2.5 | — | — | :82-87 |
| `SpawnExploder` | Exploder | 自爆 | 1 | 1.5 | — | — | :89-94 |
| `SpawnRanged` | RangedEnemy | 远程 | 2 | 1.6 | **8.0** | — | :96-102 |
| `SpawnJump` | JumpingMelee | 跳跃近战 | 2 | 2.0 | — | — | :104-109 |
| `SpawnCharger` | Charger | 冲锋兵 | 3 | 1.6 | — | **0.25** | :111-117 |
| `SpawnScreamer` | Screamer | 尖啸者 | 2 | 1.2 | — | — | :119-124 |
| `SpawnGuard` | Guard | 守卫 | 3 | 1.2 | — | **0.2** | :126-132 |

**「这个数值在表里和文档里不一致」**：`Config/Tables/Enemies/enemy_global.tres` 写的是
`HealthMultiplier = 0.75` / `DamageMultiplier = 0.3`，而 `Config/Balance.tres:60-61` 写的是
`EnemyHealthMultiplier = 0.75` / `EnemyDamageMultiplier = 0.3` —— 一致；但 `BalanceConfig.cs:45-46` 的
**C# 默认值是 `1f` / `0.8f`**，与 `Balance.tres` 的 `0.75` / `0.3` 不同。
⚠️ 见 §5-④：**表读不到时会静默换成另一套数值**。

**建议**：`Config/Tables/Spawns/spawn_*.tres`，一行一个生成项，`ScenePath` 用文件引用而不是 `PackedScene` 内联。

### 2.9 【#17】**新发现** 波次/预算表 —— `WaveConfig`

| 字段 | 类型 | 当前值 | 位置 |
|---|---|---|---|
| `TotalBudget` | `float` | 普通 10.0 / Boss 0.0 | `Main.tscn:67,136` |
| `BudgetRegenPerSecond` | `float` | 1.4 / 0.0 | `Main.tscn:68,137` |
| `BudgetCap` | `float` | 4.0 / 0.0 | `Main.tscn:69,138` |
| `MaxConcurrent` | `int` | 3 / 4 | `Main.tscn:70,139` |
| `SpawnInterval` | `float` | 2.0 / 1.0 | `Main.tscn:71,140` |
| `SpawnRadius` / `MinSpawnDistance` | `float` | 22.0 / 12.0 | `Main.tscn:72-73` |
| `WaveMode` | `bool` | `true` | `EncounterController.cs:55` |
| `WaveMinEnemies` / `WaveMaxEnemies` | `int` | 4 / 15 | `:56-57`（**与 `SpawnPlanner.cs:17-18` 的 `const 4/15` 重复**，见 §5-②） |
| `WaveCount` | `int` | 0（运行时由 `NodePlacement.WavesFor` 写，`StageController.cs:331`） | `:60` |
| `WaveMaxTypes` | `int` | 3 | `:63` |
| `BossGuardCount` / `BossSpawnDelay` / `BossSpawnDistance` | — | 2 / 1.5 / 16.0 | `Main.tscn:143-145` |

**判断**：**部分已被关卡表覆盖**（`StageDefinition.CombatBudget/MaxConcurrent/SpawnInterval`，
`StageController.cs:111-113` 每关写一次），但**波次规模、类型数、Boss 出场参数没有被覆盖**。
建议：把 `WaveMinEnemies/WaveMaxEnemies/WaveMaxTypes` 并入 **#8 关卡表**（它们本来就是「每关不同」的），
**不新开一张表** —— 这条与 `config-schema.md` §1.4-② 的结论一致。

### 2.10 【#18】**新发现** 投射物/弹道表 —— `ProjectileConfig`

| 字段 | 类型 | 当前值 | 位置 | 谁覆盖它 |
|---|---|---|---|---|
| `Speed` | `float` | 75.0 | `Bullet.tscn:10` | 武器表 `ProjectileSpeed`（只用于计算 `_velocity`，**不写回 `Speed`**，见 `Weapon.cs:206` + `Projectile.Launch` :37-44）⚠️ **未确认**：实际弹速到底用哪个 |
| `MaxLifetime` | `float` | 2.2 | `Bullet.tscn:11` | `AttackInstance.Lifetime`（`Weapon.cs:187`）→ `_attack.Lifetime`（`Projectile.cs:55`）⚠️ 同上 |
| `GravityScale` | `float` | 0（默认） | `Projectile.cs:14` | 无 |
| `Pierce` | `int` | 0（默认） | `:18` | `attack.Pierce`（:41） |
| `HitMask` | `uint` | `World\|UnitMask` | `:20` | 无 |
| `RicochetRange` | `float` | 9.0 | `:23` | 无 |

**判断**：够成一行（6 字段），但**只有一种投射物**（`Bullet.tscn`）。
建议优先把 `Speed`/`MaxLifetime` 的**双来源**理清（`Projectile.Launch` 用的是
`direction.Normalized() * Speed`（本节点 `Speed=75`），而 `Weapon` 传的是 `attack`，`Speed` 并不在 `AttackInstance` 里
—— 所以**弹速实际上是 `Bullet.tscn` 的 75，武器表的 `ProjectileSpeed=90` 可能没有生效**。⚠️ **必须实测确认**）。
这条如果属实，是**一个隐性 bug**：`wpn_pistol.tres` 的 `ProjectileSpeed = 90.0` 与
`WeaponConfig.ApplyTo` 会写进 `weapon.ProjectileSpeed`（`WeaponConfig.cs:98`），但 `Weapon.TryFire` 从没用过 `ProjectileSpeed`。

### 2.11 【#19】**新发现** 召唤物表 —— `SummonConfig`

`Scenes/Summons/Drone.tscn` 的数值：

| 字段 | 值 | 行号 |
|---|---|---|
| `MaxHealth` | 40.0 | :34 |
| `MoveSpeed` | 6.0 | :35 |
| `Tags` | `Summon` | :36 |
| `Brain.SearchRange` | 22.0 | :53 |
| `Brain.TargetRefreshInterval` | 0.3 | :54 |
| `Attack.Priority` / `Chase.Priority` | 100 / 10 | :58,62 |
| `Chase.StopDistance` | 1.6 | :63 |
| `AbilityMelee.Cooldown/Windup/Recover/AttackRange` | 0.9 / 0.15 / 0.2 / 1.8 | :67-70 |
| `Attack.Damage` / `Knockback` | 6.0 / 1.5 | :77-78 |
| `Lifetime` | 25.0 | :88 |
| 碰撞体半径/身高 | 0.3 / 1.0 | :14-15 |

**⚠️ 召唤上限有两个真源**：`SummonAbility.MaxAlive = 3`（`Player.tscn:115`）与
`SummonOnKillRule.MaxSummons = 2`（`RewardCatalog.cs:84` / `mut_summon.tres` 的 `HandlerParams`）。
两者语义不同（一个是技能上限、一个是击杀召唤上限），但**玩家侧都表现为「最多几架无人机」**，
`RunController.cs:252` 的卡片文案写的是「最多 3 架」。**建议在召唤物表里显式各给一列**，并在文案里区分。

**优先级：中**（只有 1 个召唤物，但它是「角色 3 的核心机制」）。

### 2.12 【#20】**新发现** 角色 ↔ 技能矩阵（关系表）

`CharacterConfig.EnabledAbilityIds` / `DisabledAbilityIds`（`CharacterConfig.cs:41,44`）已经是表列，
但它是**字符串数组**（`MeleeAttackAbility` / `SummonAbility` / `AreaAttackAbility`），
而**没有外键约束** —— 拼错一个技能名就静默失效（`RunController.cs:477-484` 的判定是
`Array.IndexOf(...) >= 0`，找不到就跳过）。

**建议**：要么在 #9 技能表做完后改成 `AbilityConfig[]` 引用数组（有类型检查），
要么在 `Balance.BuildTableIndex` 里加一条**校验**：`EnabledAbilityIds` 里每个 id 都能在场景里找到对应 `Ability`
（现在没这条校验）。**判断：值得做一张 2 列的稀疏关系表**，但优先级是中。

### 2.13 【#21】**新发现** 职业专属参数

现在只有 `CharacterConfig.MeleeDamage = 32f`（`CharacterConfig.cs:48`）。
**字段数 = 1，不构成独立表** —— 建议**并入 #2 角色表**（已经在里面了）。
放在总表里是为了说明「这里没有第二张表」，不是建议新建。

### 2.14 【#23】**新发现** 命中体表 —— `HitboxConfig`

`AttackHitbox.cs:13-24` 有 9 个 `[Export]`，而**每个攻击都各配一份**：

| 字段 | 默认值 | Player.tscn 近战 | Player.tscn 控制 | Drone | Chaser | Guard | Boss 挥击 | Boss 尖啸 | Boss 冲锋 | Screamer | Exploder |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `Damage` | 10 | 32.0 | 4.0 | 6.0 | 12.0 | 14.0 | 24.0 | 14.0 | 20.0 | 6.0 | 22.0 |
| `Knockback` | 3 | 5.0 | 0.0 | 1.5 | 3.0 | 3.0 | 6.0 | 8.0 | 10.0 | 6.0 | 5.0 |
| `Policy` | `OncePerTarget` | — | — | — | — | — | — | — | — | — | — |
| `HitInterval` | 0.2 | — | — | — | — | — | — | — | — | — | — |
| `CritChance` | 0 | — | — | — | — | — | — | — | — | — | — |
| `CritMultiplier` | 2 | — | — | — | — | — | — | — | — | — | — |
| `AttackTags` | `[]` | `Melee,Heavy` | `Control,AoE` | `Melee` | `Melee` | `Melee` | `Melee,Heavy` | `AoE,Scream` | `Melee,Charge` | `AoE,Scream` | `AoE,Explosion` |
| `StatusId` | `""` | — | `Stun` | — | — | — | — | `Weaken` | — | `Slow` | — |
| `StatusDuration` | 3.0 | — | **2.0** | — | — | — | — | **4.0** | — | **3.0** | — |

**判断**：`Damage` / `Knockback` / `StatusId` / `StatusDuration` 有明显的「每行一份」形状。
但这些数**已经能通过 `AbilityConfig.Params`（#9）表达**（`MeleeAttackAbility` 已经会读
`_hitbox.Damage * DamageScale`，`MeleeAttackAbility.cs:66`）。**建议：不新开表，走 #9 的 `Params`。**
放在总表里是为了说明「这里有 9 个字段，但它们不该独立成表」。
⚠️ 注意 `AttackTags` 是**玩法**（决定吃哪些进化），只有 `Melee`/`Control`/`AoE` 三类，建议**留枚举/常量**。

### 2.15 【#24】**新发现** 单位本体表（`UnitConfig`）

`Unit.cs:14-33` 的 9 个 `[Export]`。与 #10「敌人按敌种表」**高度重叠**。
**建议：不新开表，并进 #10**（玩家/召唤物也各是一行，可以共用一张 `unit_*.tres`，
或者在 #2 角色表 + #19 召唤物表里各留一份）。放在总表里说明「这是 #10 的字段来源」。

### 2.16 【#25】**新发现** 移动手感表 —— `CharacterMovementConfig`

`CharacterMovement.cs:12-25` 的 8 个 `[Export]`，**目前无人覆盖**（除 `Player.tscn:52` 的
`TurnSharpness = 16.0` 与各敌人场景的 `FaceMovementDirection = false` + `TurnSharpness`）。
默认值：`GravityScale=1.8` / `MaxFallSpeed=60` / `GroundAcceleration=60` / `AirAcceleration=15` /
`JumpVelocity=6.5` / `ExtraAirJumps=0` / `ImpulseDamping=6` / `FaceMovementDirection=true` / `TurnSharpness=14`。

**判断**：**够成一行但暂不值得独立表** —— 它是「所有单位共用」的物理参数，
建议**留 `.tscn` / 或并入 #10**。优先级：中（改它会同时影响所有单位，属于「全局手感」）。
⚠️ 这里有一个**玩家与敌人共用**的问题：玩家 `TurnSharpness=16`、Chaser 10、Guard 5、Boss 5
—— 改玩家手感不会影响敌人，但改默认值会影响所有没覆盖的场景。

### 2.17 【#26】**新发现** 武器预制体数值 —— **建议删除，不建表**

`Scenes/Weapons/Pistol.tscn:16-25` 的 10 个赋值与 `wpn_pistol.tres` **是同一把枪的两套值**：

| 字段 | `Pistol.tscn` | `wpn_pistol.tres` | 差异 |
|---|---|---|---|
| `Damage` | 22.0（:16） | 24.0 | **不同** |
| `FireInterval` | 0.32（:17） | 0.26 | **不同** |
| `MagazineSize` | 8（:18） | 12 | **不同** |
| `ReloadTime` | 1.15（:19） | 0.7 | **不同** |
| `ProjectileSpeed` | 75.0（:20） | 90.0 | **不同** |
| `MaxLifetime` | 2.2（:21） | 2.2 | 一致 |
| `SpreadDegrees` | 0.6（:22） | 0.5 | **不同** |
| `Knockback` | 2.5（:23） | 2.5 | 一致 |
| `CritChance` | 0.15（:24） | 0.15 | 一致 |
| `AimFov` | 55.0（:25） | 55.0 | 一致 |

**结论**：表是权威（`RunController.EquipWeapon` :404-408 调 `row.ApplyTo`），
`Pistol.tscn` 的那份**只在表加载失败时生效**。这符合「读不到则用自身默认值」的约定，
但 5 个字段不同意味着**回退路径会静默换一套手感**。
**建议**：不建新表；把这 5 个值在 `Pistol.tscn` 上改成与表一致（或在 `Weapon.cs` 注释里写明「本预制体的数值只是回退基线，权威在 `wpn_*.tres`」）。
**这条与 `config-schema.md` §1.4-③ 是同一个问题，至今未修。**

### 2.18 【#27】**新发现** 玩家操作手感表 —— `PlayerInputConfig`

`PlayerWeaponInput.cs:15-17`：`MaxAimDistance = 200f` / `DefaultFov = 78f` / `AimLerpSpeed = 14f`。
**场景里一个都没覆盖**（`Player.tscn:61-62` 只挂了脚本）。
**判断**：3 个字段，**「手感」而非「玩法」**，但 `DefaultFov` 与 `CameraRig.tscn:15` 的 `fov = 78.0`
**是同一个数的两份** —— 改一个不改另一个会出现「不瞄准时 FOV 跳变」。
**建议**：不建表，但**合并到一个地方**（让 `DefaultFov` 从相机读，或删掉 `PlayerWeaponInput.DefaultFov`）。
**优先级：中**（这是「同一个数值在多处重复」的又一处，见 §5-①）。

### 2.19 【#28】✅ **已落地（2026-10 配置表阶段 4）** 武器 ↔ 升级池关联

> **本节结论已过时，保留作历史**：下面写的"**建议按设计文档的反向指做（少一张表），不新开**"**没有被采纳**。
> 用户拍板要的是**正向指**（「武器的配置表里确实是需要有指向对应池表的字段，而对应池表则不指向其对应武器，
> 而池表里则是一个选项占用一栏」），所以最终**新开了一张池表**：`Config/Tables/UpgradePools/pool_*.tres`
> （3 行 = 一行一个池），`WeaponConfig.Pool` 正向指向它，池的成员由**选项行**（`RewardConfig.Pool`）声明
> —— 一个选项占一行 + 一个引用列。实测与回退验证见 [config.md](config.md) 的「配置表阶段 4 实测」。
> ⚠️ 本文当时说"反向指做不了 O(1) 查询"这条**是对的**，而正向指正好补上了它。

`config-schema.md` §4.5 论证过：现在**只有「升级项要求武器有某个 tag」的反向指**
（`RewardConfig.RequiredWeaponTags`，`RewardConfig.cs:39`），4 条进化全都只声明 `"Gun"`
（`evo_*.tres` 的 `RequiredWeaponTags = PackedStringArray("Gun")`），
而三把武器共用同一个 `Pistol.tscn` + 同一个 `AttackTags = ("Projectile","Gun","Physical")`
（`wpn_pistol.tres` / `wpn_sniper.tres` / `wpn_rifle.tres` 三行**完全一样**）
→ **三把枪共用一个升级池**。

**判断**：`config-schema.md` 的方案是「在 `RewardConfig` 上加 `ApplicableWeapons : WeaponConfig[]`」
（反向指）—— 这**不新开表**，是给 #6 加一列。
**但**用户要的「某个武器 → 它对应升级池」这个**正向**视图，用反向指做不了 O(1) 查询。
**建议**：按设计文档的反向指做（少一张表），**不新开**。放在总表里是为了标记「这是设计文档已有的方案，本文不重复造」。

### 2.20 【#29】**新发现** 奖励池过滤参数 —— `RewardPoolFilter`

| 字段 | 当前值 | 位置 | 说明 |
|---|---|---|---|
| `RewardChoiceCount` | 3 | `BalanceConfig.cs:111`（**已在 Balance**） | 三选一的选项数 |
| `SummonerAllowed` | 运行时设 | `RewardPool.cs:31`、`RunController.cs:544,590` | 角色限定开关 |
| `Weight` | 19 条**全没填** | `RewardConfig.Weight`（:25）默认 `1f`；`.tres` 里一行都没有 `Weight =` | ⚠️ 加权抽取退化成等概率（`config-schema.md` §1.4-⑤ 的老问题，**分表后仍未修**） |

**判断**：**不新开表** —— `RewardChoiceCount` 留在 Balance，`Weight` 是 #6 的一列。
**但 `Weight` 这条必须处理**：要么给 19 行填上不同的权重，要么在游戏里显式声明「等概率」。

### 2.21 【#30】**新发现** 触发规则参数 —— `TriggerRuleConfig`

`RunBuild.cs:78-80`：`RewardId` / `Cooldown` / `MaxTriggersPerChain = 4`。
`Cooldown` 现在由 `RewardEffectRegistry` 从 `HandlerParams` 里读（`RewardEffectRegistry.cs:46`）。
`MaxTriggersPerChain` 是**全局常量 4**（`RunBuild.cs:80`），`EventContext.MaxDepth = 4`（`CombatTypes.cs:58`）是两层防递归。
**判断**：**不新开表**（2 个字段，且 `Cooldown` 已在 #6 的 `HandlerParams` 里）。
⚠️ 但 `MaxTriggersPerChain = 4` 与 `MaxDepth = 4` 是**两个不同的 4**，且都不可调 —— 若要做「连环爆炸」类玩法需要放开。

### 2.22 【#31】**新发现** 地图表 —— `MapManagerConfig`

`MapManager.cs:21-33` 的 4 个烘焙参数（`AgentRadius=0.6` / `AgentHeight=1.8` / `AgentMaxSlope=30` / `CellSize=0.3`）
+ 3 个行为参数（`GeometryGroupName="nav_geometry"` / `GeometryMask=1` / `BakeDelaySeconds=0.3`），
**`Main.tscn:32-33` 的 MapManager 节点一处都没覆盖**（只有 `script = ExtResource`）。
外加**硬编码的点位启发式**（`MapManager.cs:179-217`、`NodePlacement.cs:63-93`，见 #36）。

**判断**：`AgentRadius/Height/MaxSlope` 是**引擎烘焙参数**（放 `NavigationMesh` 资源上更合适），
`BakeDelaySeconds` 是**软件参数**。**建议：不新开表**，把点位启发式（#36）当成一张可选的「地图手感表」。
**优先级：中**（`stg_*.tres` 已经覆盖了关卡节奏，地图参数是下一层）。
⚠️ `MapManager.cs:143` 的 `KillPlaneY - 30f` 里的 `30f` 是魔法数（区域杀中心比 KillPlaneY 低 30 米），
与 `KillZone.cs:16` 的 `Thickness = 20f` 是**两个不同的数**，逻辑上应该 `CenterY = KillPlaneY - Thickness`。

### 2.23 【#32】**新发现** 区域杀表 —— `KillZoneConfig`

`KillZone.cs:16-22`：`Thickness = 20f` / `Extent = 300f` / `CenterY = -60f`（运行时被 `MapManager.cs:143` 覆盖）。
**字段数 3，且 `Extent`/`Thickness` 是纯几何**。**建议：不建表**（见 §3-③）。
⚠️ 但 `CenterY = KillPlaneY - 30f` 的推导关系应该显式化（见 §2.22）。

### 2.24 【#33】**新发现** 碎片结算表 —— `ShardSettlementConfig`

`RunSettlement.cs:10,13,16`：`ShardsPerStage = 10` / `ShardsPerChallenge = 3` / `ShardsForVictory = 50`。
**这是纯数值、纯玩法、且会被反复平衡的**（它决定局外成长的节奏）。
**判断**：够成一行（3 字段），建议**并入 #7 局外成长表所在目录**，做成
`Config/Tables/Meta/meta_settlement.tres` 一行，或直接进 #8 关卡表。
**优先级：中**（3 个 const，改它要重编译；但它是「局外经济」的唯一旋钮，值得可调）。

### 2.25 【#34】**新发现** 局外价格曲线 —— `MetaCostCurve`

`MetaUpgradeConfig.BaseCost = 10` / `CostGrowth = 10`（:31,34），`CostOf(level) = BaseCost + CostGrowth × level`（:40）。
**已经是表的列**（在 #7 里）。**建议：不新开表。**
`MetaProgress.cs:37` 的 `?? (10 * (level + 1))` 是**兜底公式**，与表的默认值等价（10/10）。

### 2.26 【#35】**新发现** 局外成长的软件参数

`MetaProgress.cs:12`：`SavePath = "user://meta.cfg"`。
**1 个常量、且不该由策划调**。**建议：不建表**（见 §3-③）。

### 2.27 【#36】**新发现** 节点摆放启发式表 —— `NodePlacementConfig`

`NodePlacement.cs:63-93` 全硬编码：

| 值 | 位置 | 含义 |
|---|---|---|
| `2f` | :63 | 地面射线起点抬高 |
| `10f` | :63 | 地面射线向下长度 |
| `1` | :63,88 | 碰撞层（World） |
| `28` | :75 | 螺旋最大步数 |
| `0.7f` | :77 | 每步距离 |
| `1.15f` | :78 | 每步角度（弧度，逼近黄金角） |
| `0.25f` | :81 | 离地高度 |
| `1.3f` | :86 | 探测球半径 |
| `1.65f` | :87 | 探测球抬高 |
| `0.05f` | :93 | 「挪动了」的日志阈值 |

**判断**：8 个启发式数值，**不是策划可调项**（见 §3-③）。**建议：不建表。**
⚠️ 但 `28 × 0.7 = 19.6` 米的最大搜索半径与 `Balance.MinNodeSpacing = 8f`（`BalanceConfig.cs:130`）
之间的关系**没有任何注释说明**，读代码的人会困惑。

### 2.28 【#37】**新发现** 奖励节点交互表 —— `RewardNodeConfig`

| 字段 | 值 | 位置 |
|---|---|---|
| `InteractRadius` | 奖励节点 3.2 / Boss 门 4.5 | `RewardNode.tscn:9`、`BossDoor.tscn:9`；默认 3.2（`Interactable.cs:48`） |
| `SingleUse` | `true`（关卡）/ `false`（基地） | `Interactable.cs:51`、`BaseTerminal.cs:40` |
| `AppearSeconds` / `DisappearSeconds` | 0.35 / 0.45 | `Interactable.cs:26-27`（`const`） |
| 出现缩放起点 / 消失缩放终点 | 0.2 / 0.05 | `:159,192` |
| 交互检测频率 | 0.1s（10Hz） | `:92` |
| 材质路径模板 | `res://Assets/Materials/mat_node_{kind}.tres` | `RewardNode.cs:110`（字符串拼接！） |
| `Prompt` | 每位节点的显示名 | `RewardNode.tscn:8`；`BossDoor.tscn:8`；基地由 `BaseTerminal.cs:41-47` 按 Kind 生成 |

**判断**：`InteractRadius` / `SingleUse` / `Prompt` 值得成列（**但 `Prompt` 已经可由 #5 节点表的 `Title` 推导**
—— `RewardNode.OnNameChanged` :87-97 就是这么做的，`RewardNode.tscn:8` 的 `Prompt` 是重复的）。
`Prompt` 在 `Main.tscn:149,154,159,...` 一共 10 处重复了节点表的 `Title`（「强化池」「进化池」「异化池」「补给站」…）。
**建议**：不新开表；**把 10 处 `Prompt` 删掉**（节点表已经是权威）。
**优先级：低**。

### 2.29 【#38】**新发现** 弹窗开合手感 —— `OverlayScreenConfig`

`OverlayScreen.cs:36-55`：`ScreenLayer` / `OpenGuardSeconds = 0.3` / `FadeSeconds = 0.16` /
`OpenScaleFrom = 0.94` / `CloseOnCancel = true`。**场景里一个都没覆盖**（只设了 `layer`）。
**判断**：**纯表现**，4 个字段。**建议：不建表**（见 §3-②）。

### 2.30 【#39】**新发现** 界面层级表 —— `UiLayerConfig`

`OverlayScreen.ScreenLayer`（`OverlayScreen.cs:36`，默认 `Panel`），
但 `UiService.cs:130,143,157` 在**运行时**把 `RewardScreen` / `ChooseScreen` 改成 `Modal`，
`PlayerHud` 是 `Hud`（`WorldOverlay.cs:151` 的 `Layer = 5` 是 CanvasLayer 层，**不是** `UiLayer`）。
**判断**：**1 列**，但它是「这个界面会不会暂停游戏」的开关 —— **值得成列，不值得成表**。
**建议**：在 `Balance` 或一张 `ui_screens.tres` 里给每个界面一行（`ScreenType` → `UiLayer`），
或者更简单：**把 `UiService` 里那 3 处运行时赋值改成场景里的 `ScreenLayer`**（一行改一处）。
**优先级：低**。

---

## 3. 明确排除的东西及理由

> 这一步是「能判断的清单」的关键：下面每一项都**读过代码**，说明为什么**不建议成表**。

### ① 相机手感（`CameraRig`）—— 排除，但**建议做成「设置」**

**排除理由**：`CameraRig.cs:15-34` 的 9 个参数（`MouseSensitivity=0.25` / `MinPitchDegrees=-70` /
`MaxPitchDegrees=70` / `PitchDegrees=20` / `TargetOffset=(0,1.5,0)` / `TargetScreenHeight=0.3` /
`FollowSharpness=12` / `CaptureMouseOnStart=true` / `Target`）**是玩家偏好，不是策划数值**。
把它做成策划表会让「每个玩家一套灵敏度」这件事无处安放。

**⚠️ 但是有一条例外必须处理**：`PlayerWeaponInput.DefaultFov = 78f`（`PlayerWeaponInput.cs:16`）
与 `CameraRig.tscn:15` 的 `fov = 78.0` 与 `CameraRig.cs:108` 的 `?? 75f`
是**同一个数的三份**（78 / 78 / 75）。这属于 §5-① 的重复问题。
**建议**：把 `DefaultFov` 从 `PlayerWeaponInput` 删掉，改成从相机读初值。

### ② 纯表现参数（伤害跳字 / 血条 / HUD 文案 / 弹窗动画）—— 排除，**它们已经在 Balance 里了**

**排除理由（这是最重要的判断）**：
`BalanceConfig.cs:113-124,157-158` 已经把这 10 个参数集中起来了：

| 参数 | 值 | 消费者 |
|---|---|---|
| `ShowDamageNumbers` | `true` | `WorldOverlay.cs:189` |
| `DamageNumberLifetime` | 0.8 | `:213-214` |
| `DamageNumberRise` | 70 | `:213` |
| `DamageNumberFontSize` | 24 | `:206` |
| `ShowEnemyHealthBars` | `true` | `:220,285` |
| `EnemyHealthBarWidth` / `Height` | 64 / 7 | `:301` |
| `EnemyHealthBarTopOffset` | 0.25 | `:262` |
| `BossBarFullRange` | 22 | `PlayerHud.cs:227` |
| `BossHealthBarScale` | 4.0 | **无消费者（死字段，见 §5-④）** |

**为什么不拆成「伤害跳字表」「血条表」**：它们**不随内容变化**（不会「每个敌人一套跳字颜色」），
拆表只会把「一次表现调参」从 1 个文件变成 2 个文件 + 1 个索引数组。
**判断：留在 Balance 就是正确的形态。**

**⚠️ 但有一条要补**：`WorldOverlay.cs:204` 的两种颜色（玩家伤害 `(1,0.85,0.35)` / 敌人伤害 `(1,0.4,0.35)`）
与 `:207-208` 的描边色 + `outline_size=5`、`:194` 的 `+1.5f` 抬高、:281 的 `HealthColor` 三段配色，
**全是硬编码且美术一定会改**。如果想让美术不碰代码，**这才值得一张表**（但不是策划表，是「美术表现表」）。
**优先级：低。**

### ③ 规则常量 / 引擎数学保护值 —— 排除

| 项 | 位置 | 为什么排除 |
|---|---|---|
| 减伤上限 `0.9` | `Unit.cs:173,175` | 战斗规则的硬上限（`config-schema.md` §1.2-H9 结论相同） |
| 碰撞层位 `World=1<<0` / `Player=1<<1` / `Enemy=1<<2` | `CombatTypes.cs:25-28` | 引擎约定，改了会静默坏碰撞 |
| `EventContext.MaxDepth = 4` | `CombatTypes.cs:58` | 防无限递归的保护值 |
| `WeightedPick.MinWeight = 0.01f` | `WeightedPick.cs:21` | 防除零 |
| clamp / epsilon（`0.0001f` / `0.04f` / `0.0001` 长度平方阈值） | `Ability.cs:141`、`Weapon.cs:170,303`、多处 | 数值稳定性保护 |
| 形状数学（`AttackHitbox.ComputeReach` 的 `0.5f`、`Mathf.Max(0.2f, …)`） | `AttackHitbox.cs:70-95` | 由碰撞体形状推出，不该手配 |
| `KillZone.Extent = 300f` / `Thickness = 20f` | `KillZone.cs:16-19` | 纯几何「够大就行」 |
| `NodePlacement` 螺旋外扩 8 个启发式 | `NodePlacement.cs:63-93` | 见 §2.27 |
| `MetaProgress.SavePath` | `MetaProgress.cs:12` | 软件常量 |
| `GameLog` 刷盘 1 秒 | `GameLog.cs:67` | 软件常量 |
| `SelfTest` 全部数值 | `Scripts/Testing/SelfTest.cs`（67 处） | 测试代码 |
| `CombatLayers` / 节点名常量（`"Mesh"` / `"Label"` / `"Movement"` / `"Block"` / `"Pressure"` / `"Phases"` …） | `Interactable.cs:16-23`、`Unit.cs:85,289`、`EncounterController.cs:665`、`PlayerHud.cs:131-132` | 结构性约定，改名会静默失效，属于「该加断言」而不是「该加配置」 |

### ④ 引擎配置（`project.godot`）—— 排除

- `[input]` 13 个动作（`project.godot:44-112`）：`move_forward/back/left/right` / `jump` / `fire` / `aim` /
  `reload` / `dash` / `burst` / `control` / `summon` / `interact`。
  **这是引擎级设置，Godot 自己就提供了编辑器 UI 与运行时重绑**。做成 `.tres` 表会和 `InputMap` 打架。
  `config.md:155` 把它列为「设置表」待办 —— 那应该是**设置界面**（写回 `InputMap` + 存 `user://`），
  **不是策划配置表**。
- `[autoload]` 6 个（`project.godot:20-26`）：`MCPRuntimeProbe` / `Balance` / `UiRouter` / `GameLog` /
  `SelfTest` / `UiService`。**引擎配置**。
- `[display]` / `[physics]` / `[rendering]`：引擎配置。

### ⑤ 特效 / 音效 / 材质 / 模型资源引用 —— 排除（但**发现一处该修的**）

**排除理由**：`Pistol.tscn:5,35`（枪口火光 `fx_weapon_muzzle.tscn`）、`Bullet.tscn:4-6`（弹道网格 + 拖尾粒子）、
`Explosion.tscn:4`（爆炸网格）、`Drone.tscn:17-26`（材质 + 胶囊网格）、各敌人的 `albedo_color`
**都是「一个资源路径」**，且**已经在 `.tscn` 里可视化编辑**（这也是 [assets.md](assets.md) 的规范地盘）。
做成「特效表」= 把资源引用从「能预览的场景」搬到「看不见的表格」，**是退步**。
**音效**：全项目 `grep` **没有发现任何音频资源引用**（未确认 —— 只确认了 `Scenes/` 与 `Scripts/` 下没有 `AudioStream`，
`Assets/` 下未逐目录核对）。**如果将来加音效**，正确形态是「每个单位/武器在自己的预制体上挂 `AudioStreamPlayer3D`」，
不是一张全局音效表。

**🔴 但这里发现一处该修的硬编码**：`Projectile.cs:166`
```csharp
var scene = GD.Load<PackedScene>("res://Scenes/Combat/Explosion.tscn");
```
**爆炸场景路径写死在代码里**。而 `Explosion.cs:12` 的 `Lifetime = 0.12f` 也只能改代码。
把路径搬到武器表的 `ExplosionScenePath`（表里已经有 `ExplosionRadiusAdd` / `ExplosionDamageScale`
两个爆炸相关键，说明这条路本来就有意图）更合适。

### ⑥ 日志开关 —— 排除

`GameLog.cs:67` 的 `_flushTimer >= 1f`（1 秒刷盘）与 `GameLog` 的分组字符串（`"Config"` / `"Flow"` /
`"Node"` / `"Spawn"` / `"Combat"` / `"Reward"` / `"Map"` / `"SelfTest"`）是**开发工具参数**。
见 [logging.md](logging.md)。**不该由策划调。**

---

## 4. 最该先做的三张（及理由）

### 🥇 第一：敌人按敌种表（#10）—— 从「扩展已有表」开始，不新建索引

**理由**：
1. **基础已经铺好**：`EnemyConfig` 类**已经有** `ScenePath` / `SpawnDisplayName` / `Weight` /
   `MinElapsedSeconds` / `EliteChance` 五个「按敌种的行」字段（`EnemyConfig.cs:30-44`），
   注释里写明了「以后要按敌种调，就再加行」（:11-12）。**这是半成品，不是新设计。**
2. **改动面最小**：`Balance.ScaleEnemy`（`Balance.cs:83-104`）已经在按 `enemy_global` 的 id 查表，
   加「按敌种行」只需要在 `EncounterController.Spawn`（:413-439）里多一次 `ById(rowId)` 调用。
3. **它顺手修掉一个真 bug**：`StageDefinition.AllowedEnemies` 现在按**中文显示名**匹配
   （`stg_outer.tres` 写的是 `"基础近战"` 等），改文案就静默失效。做这张表时换成 `Id` 匹配，
   就是 `config-schema.md` §3 T6 预留的 `AllowedEnemyIds`。
4. **收益立刻可见**：10 个敌种的 6 个公共数值（生命/护甲/减伤/击退抗性/移速/标签）从
   「改 `.tscn` 要一个个开」变成「一个文件夹里一眼看全」。

**风险**：`Unit._EnterTree` 捕获基础值的顺序（`Unit.cs:75-80`）—— 表必须在 `_EnterTree` 之前套用，
否则「回满生命」会算错。`EnemyConfig` 的注释已经点到这点（「生成时立刻套用，会回满生命」，:20）。

### 🥈 第二：技能/能力表（#9 / T2）

**理由**：
1. **它是最大的单块未落地数据**：`Player.tscn:64-140` 有 **40 处**能力参数赋值，加上 10 个敌人场景里的能力参数，
   全项目能力参数 **100+ 处**。
2. **它决定「加一个新技能」的成本**：现在加技能 = 改场景 + 改 C# 子类；有了表 = 加一行 `.tres`。
   这正是用户要的「能成表的东西」里价值最高的一类。
3. **前置件已全部就绪**：`ConfigRow.Params` + `ParamEntry` 已落地并在 `mut_chain.tres` 上验证；
   `GodotObject.Set(StringName, Variant)` 已用反射核对过（`config-schema.md` §7.1）。
4. **它是 #15（AI 行为表）的前置**：`UseAbilityBehavior` 靠 `ApproachMinRange/MaxRange`
   做决策（`UseAbilityBehavior.cs:32-34`），这两列在技能表里 —— 两张表要一起设计。

**唯一硬前置**：`AbilityId` 现在**全空**（靠 `Ability.cs:113-116` 用类名兜底），
而 `CharacterConfig.EnabledAbilityIds` 用的就是类名。做表时要么补 `AbilityId`，要么接受「用类名寻址」。

### 🥉 第三：AI 行为表（#15）—— ✅ **已完成（2026-10 配置表阶段 5）**

> **三张"最该先做"的表全部做完了**（① 敌人按敌种表 + ② 技能表在阶段 3，③ 本表在阶段 5）。
> 实际落地形态（**扁平静态列**而不是 `Behaviors` 数组）、盘到的 25 个参数、实测与两次回退验证见 §2.7 的更新块。

**理由**：
1. **参数密度最高**：10 个敌种 × 每个 8–15 个 AI/能力参数（见 §2.7 的表），
   且**每个都在 `.tscn` 里各配一份** —— 这是典型的「该是列，现在是场景属性」。
2. **它现在完全不可批量调**：想「让所有敌人更激进」，得开 10 个场景改 30 处 `StopDistance`。
3. **它必须与 #10 一起做才划算**：两张表都以「敌种」为行键，共用 `Id`。
   单独做 #10 只搬走 6 个数值，AI 参数仍然散着 —— 收益减半。

**⚠️ 与 #11（首领增强表）的取舍**：用户特别强调了《关卡核心文档》第七章的
「Pressure（数值强化）与 Boss Build（行为/技能）必须分离」。
本文把它拆成 **#11 数值池** + **#12 行为表** 两张表。
✅ **两张表现在都落地了**（阶段 3 建表、阶段 6 把抽取点拆开并接上节点驱动）：
#11 = `Pressure/pmod_*.tres`（3 行）、#12 = `Skills/bhv_*.tres`（4 行），
且 #12 的 7 条词条里有 **3 条是数值型**（狂暴/铁壁/疾行）、**4 条是行为型**（提前觉醒/召唤师/尖啸/冲锋）
—— **就是按「来源」拆到两张表的**。
**原建议：这两张表与 #15 分开排期**（它们服务 Boss，不服务小怪）。

---

## 5. 盘点发现的问题

### ① 同一个数值在多处重复

| 数值 | 出现处 1 | 出现处 2 | 出现处 3 | 后果 |
|---|---|---|---|---|
| **默认 FOV 78** | `CameraRig.tscn:15`（`fov = 78.0`） | `PlayerWeaponInput.cs:16`（`DefaultFov = 78f`） | `CameraRig.cs:108`（`?? 75f` 兜底） | 三处不同步时「不瞄准 FOV 跳变」 |
| 波次规模 4/15 | `SpawnPlanner.cs:17-18`（`const`） | `EncounterController.cs:56-57`（`[Export]`） | `Main.tscn` 上没覆盖 | 改一个不改另一个，`RollWaveSize()` 的默认参数会不同步（`config-schema.md` §1.4-② 的老问题） |
| 节点 `Prompt` | `Main.tscn:149,154,159,164,169,174,179,184,189,194`（10 处） | `Config/Tables/Nodes/node_*.tres` 的 `Title`（9 行） | `RewardNode.tscn:8`（默认「强化池」） | 节点表是权威但场景里的值仍在，改表不改场景时**头顶名牌与菜单标题会不一致**（`RewardNode.cs:94-96` 走的是 `RewardService.TitleFor` → 表，所以场景里的 `Prompt` **只在节点表的 `Title` 为空时才显示**；`Interactable.CurrentPrompt` :64 用的是 `Prompt` → 屏幕提示会显示旧值） |
| 近战重击伤害 32 | `Player.tscn:101`（`Attack.Damage = 32.0`） | `chr_gunner/…/chr_summoner`?（`CharacterConfig.MeleeDamage = 32f`，:48） | `RunController.cs:117`（`hit.Damage = character.MeleeDamage` 覆盖） | 表读不到时会用场景的 32，一致；但改表不改场景会让两者漂移 |
| 弹速 75 / 90 | `Bullet.tscn:10`（`Speed = 75.0`） | `wpn_*.tres`（`ProjectileSpeed = 90.0`） | `Weapon.cs` 的 `ProjectileSpeed` **从未被 `TryFire` 使用** | ⚠️ **疑似表里的弹速根本没生效**（见 §2.10，**必须实测**） |
| `EliteBossHealthScale` 0.35 | `BalanceConfig.cs:127` + `Balance.tres:92` | `EncounterController.cs:100` + `Main.tscn` 上**没覆盖** | — | Balance 那份**无消费者**（见 ④） |
| 爆炸 `Lifetime` | `Explosion.cs:12`（0.12） | — | — | 只在代码里 |
| 强化 19 条的两套定义 | `Config/Tables/Upgrades/*.tres`（19 行，生效） | `RewardCatalog.Fallback`（`RewardCatalog.cs:21-91`，兜底） | — | 符合回退约定，但**两套数值必须手工保持同步**（例如 `mut_chain` 的 `Damage=14` 在 `.tres` 与 `RewardCatalog.cs:65` 各一份） |
| 敌人全局倍率 | `Config/Tables/Enemies/enemy_global.tres`（0.75 / 0.3，生效） | `Balance.tres:60-61`（0.75 / 0.3，被表覆盖） | `BalanceConfig.cs:45-46` 的 **C# 默认值（1f / 0.8f）** | 三套值，**回退路径会静默换一套难度**（见表读不到的分支 `Balance.cs:89,92`） |
| **眩晕时长** | `eff_stun.tres`（`Duration = 1.0`） | `Player.tscn:136`（`StatusDuration = 2.0`，玩家控制技） | `Boss.tscn:127`（`StatusDuration = 4.0`，尖啸挂 Weaken）；`SkillElite.tscn:143`（`2.0`） | 表值被**三处调用方覆盖**（`EffectConfig.ToStatus` :113 的 `duration > 0f ? duration : Duration`）→ 表这一列形同虚设 |
| **连锁闪电 / 爆炸 / 吸血 / 召唤的数值** | `Config/Tables/Upgrades/mut_*.tres` 的 `HandlerParams`（**生效**） | `Config/Tables/Effects/eff_chain_lightning / eff_explosion / eff_lifesteal / eff_summon_on_kill`（**同值镜像，无消费者**） | `RewardCatalog.cs:61-90` 的 Fallback | 同一组数在**表里存了两份**，改效果表不改增幅表就出现「表里的数 ≠ 实际打的伤害」 |

**🔴 最严重的一条**：上面最后一行。`Balance.cs:88-93` 的取值链是
`isBoss ? config.BossHealthMultiplier : (row?.HealthMultiplier ?? config.EnemyHealthMultiplier)`。
如果 `enemy_global` 行读不到（表空 / `Balance.tres` 整体加载失败），
敌人血量倍率会从 **0.75 跳到 1f**（+33%），伤害从 **0.3 跳到 0.8f**（+167%）。
**这是一条没有日志提示的静默换挡**（`Balance.ScaleEnemy` 里没有任何 `GameLog.Write`）。

### ② 死字段 / 没有消费者的字段

> ✅ **2026-10 配置表阶段 4 已收尾其中两个**（本表保留原文，改动用 ✅/🗑 标出）：
> `RewardDefinition.PoolTags` **已删除**、`RewardDefinition.ConflictsWith` **已接上**
> （`RewardConfig` 新增 `ConflictsWith` 列 + `RewardEffectRegistry.Compile` 填充）。
> 其余各行（`Weight` / `BossPerk.Weight` / `IsFinalBoss` / 效果表镜像列 …）**状态不变**。
> 阶段 3 已修的 6 处（两个 Balance 死字段 / `IsFinalBoss` 之外的…）见 [config.md](config.md) 的「配置表阶段 3」。

| 字段 | 定义处 | 赋值处 | 读取处 | 结论 |
|---|---|---|---|---|
| **`BalanceConfig.BossHealthBarScale`** | `BalanceConfig.cs:158` | `Balance.tres:99`（`= 4.0`） | **全项目 0 处** | 🔴 **纯死字段**。BOSS 血条的尺寸实际由 `BarBoss.tscn:20-23`（`offset_left=-240` / `right=240` / `top=14` / `bottom=92`）决定（`PlayerHud.cs:198-207` 只改比例，注释自己写了「不碰预制体给的宽度/位置」）。**建议删除，或接进 `PlayerHud.UpdateBossPanel`** ✅ **阶段 3 已删除** |
| **`BalanceConfig.EliteBossHealthScale`** | `BalanceConfig.cs:127` | `Balance.tres:92`（`= 0.35`） | **全项目 0 处** | 🔴 **死字段**。生效的是 `EncounterController.EliteBossHealthScale`（`EncounterController.cs:100`，默认也是 0.35），`Main.tscn:63-65` 的 `Combat` 节点**没覆盖它**，所以现在恰好也是 0.35 —— **看起来对，但改 Balance 不起作用**。**建议删掉 Balance 那份**（或让 `EncounterController` 读它） ✅ **阶段 3 已搬进 `boss_final.tres` 的 `EliteCopyHealthScale`** |
| `RewardConfig.Weight` | `RewardConfig.cs:25` | **19 行 `.tres` 全没写** | `RewardPool.Roll`（:85，读了但恒为 1f） | 加权抽取退化成等概率（老问题 #5，**分表后未修，阶段 4 也没动** —— 改它会变抽样分布） |
| `RewardDefinition.ConflictsWith` | `RewardDefinition.cs:44` | ~~**从未被赋值**~~ → ✅ **阶段 4 起由 `RewardConfig.ConflictsWith` 填** | `RewardPool.cs:68-75` | ✅ **已接上**（老问题 #5 的一半）。默认空 = 行为不变；语义是**单向**：已拥有列表里任一 Id → 本项不进池 |
| `RewardDefinition.PoolTags` | ~~`RewardDefinition.cs:38`~~ | ~~`RewardCatalog.cs:98,113,126` + 19 行 `.tres` 的 `PoolTags`~~ | **从无** | 🗑 **阶段 4 已删除**（连同 19 行 `.tres` 里的那一行、`RewardCatalog` 三个工厂的 `tag` 参数与 15 个调用点）。语义由 `RewardConfig.Pool`（专属池）+ `RequiredWeaponTags`（标签层）取代 |
| `BossPerk.Weight` | `BossPerk.cs:16` | 行为型 4 条仍全默认 `1f`（数值型 3 条**阶段 6 起是 3/2/2**） | `BossPerkCatalog.Roll`（行为型，等概率）/ `RollPressure`（数值型，**真加权**） | 行为型仍是等概率（4 条等分）；**数值型的加权已经在阶段 6 接上**，有 3 万次实测（3/2/2 → 42.0/29.0/28.9%） |
| `NodeConfig.BossPressurePoints` / `BossModifierRolls` / `BossModifierTags` | `NodeConfig.cs:55,58,61` | `node_evolution` / `node_mutation` 填了 `BossPressurePoints = 150`；另两列仍全空 | ✅ **阶段 6 起三处都有消费者**：`StageController.ResolveBasePressure` / 「额外 N 次」段 / `PickTags` | ✅ **空壳已消除**。回退路径：三个字段都留空时明确走「关卡行 → Balance 全局默认值」，且写了日志 |
| `StageDefinition.PressureUpgradeTags` | `StageDefinition.cs:92` | **3 行都没写** | ✅ **阶段 6 起是 `StageController.PickTags` 的第二层**（节点行留空时才用它） | ✅ **空壳已消除**（留空 = 不限制，是合法状态不是错误） |
| **`StageDefinition.IsFinalBoss`** | `StageDefinition.cs:46` | **3 行全写了**（`stg_outer/inner`=`false`、`stg_core`=`true`） | **全项目 0 处**（`grep IsFinalBoss` 只命中定义行本身） | 🔴 **确认是死字段**。3 个 `.tres` 都认真填了它，但没有任何代码读。**建议删掉字段 + 删 3 行赋值**，或接进「打完最终 Boss 的胜利文案」（`RunController.cs:350` 现在写死「通关！三关全部清理」） |
| `EnemyConfig.MoveSpeedMultiplier` | `EnemyConfig.cs:25` | `enemy_global.tres` **没写** | `Balance.cs:93,99` | **实际值是 C# 默认 `1f`**，表里那行没显式给出来（可读性差）；`enemy_global.tres` 只显式给了 `HealthMultiplier` / `DamageMultiplier` |
| **`EffectConfig.Value` / `Value2` / `Radius` / `TickSeconds`** | `EffectConfig.cs:68-80` | 各行都填了（`eff_chain_lightning` 的 `Value=14/TickSeconds=0.12/Value2=7/Radius=7`、`eff_explosion` 的 `Value=0.6/Radius=3.5` 等） | **全项目 0 处读取**（`grep` 只命中 `EffectConfig.cs` 自己的声明与注释） | 🔴 **确认这 4 列没有消费者** —— `StatusPresets.Create` 只走 `Modifiers` 与 `DamagePerSecond`（`StatusEffect.cs:43-47` → `EffectConfig.ToStatus` :102-117）。这些值目前只是**「把 `RewardConfig.HandlerParams` 里的数抄一份供人看」的镜像**（各行 `Note` 自己承认：「数值来自异化『连锁闪电』（Damage 14 / Range 7 / Jumps 3 / Cooldown 0.12）」，`eff_chain_lightning.tres`）。**真正生效的仍是 `mut_chain.tres` 的 `HandlerParams`** —— 两处改一处不改就会出现「表里的数」与「实际打的伤害」不一致。**建议：要么接上（`RewardEffectRegistry` 改成读效果表），要么删掉这 4 列** |
| `EffectConfig.Duration` | `EffectConfig.cs:71` | **不对称**：`eff_slow/weaken/burn/empowered/stun` 写了 `3.0/3.0/3.0/5.0/1.0`；`eff_knockback/explosion/lifesteal/summon_on_kill/chain_lightning` 没写 | `EffectConfig.ToStatus`（:113） | ⚠️ 而且它是**第二优先**的：`Duration = duration > 0f ? duration : Duration` —— 调用方（`AttackHitbox.StatusDuration`）给了值就用调用方的。所以 `eff_stun.tres` 的 `Duration=1.0` 实际上被 `Player.tscn:136` 的 `StatusDuration = 2.0` 覆盖（Boss 尖啸则是 `Boss.tscn:127` 的 `4.0`）。**同一份表值在三个场景给出三种时长** —— 表这列基本被架空了 |
| `ConfigRow.Note` | `ConfigRow.cs:28` | 部分行写了 | `ConfigText` 可当占位符来源 | 半死（仅作文档） |

### ③ 「表的外键」做成字符串 / 场景属性，而不是资源引用

| 现状 | 位置 | 问题 |
|---|---|---|
| `StageDefinition.AllowedEnemies` 按**中文显示名**匹配 | `StageDefinition.cs:31,94-95`；`stg_outer.tres` 写 `("基础近战","快速近战","自爆")` | 改文案 → 静默放行/屏蔽敌人 |
| `EnemyConfig.SpawnDisplayName` | `EnemyConfig.cs:35` | 同上（为对接现有生成项而加的中转列） |
| `EncounterController.ApplyEntryFilter` 靠 `entry.DisplayName` | `EncounterController.cs:589` | 同上 |
| `CharacterConfig.EnabledAbilityIds` / `DisabledAbilityIds` 按类名 | `CharacterConfig.cs:41,44`；`RunController.cs:477-484` | 拼错 → 静默失效（无校验） |
| `BossPerk` 的 `screaming` / `charging` 按节点名 `GetNodeOrNull<Ability>("AbilityScream")` | `BossPerk.cs:106,119` | 节点改名 → 静默失效 |
| `EliteSkillRandomizer.Skills` 按节点名 | `SkillElite.tscn:151` → `EliteSkillRandomizer.cs:30` | 同上 |
| `UseAbilityBehavior.AbilityNodeName` 按节点名 | `UseAbilityBehavior.cs:14,22` | 同上 |
| `BossPhaseController.Phase*Abilities` 按节点名 | `BossPhaseController.cs:98` | 同上 |
| `RewardNode` 材质路径**字符串拼接** | `RewardNode.cs:110`（`$"res://Assets/Materials/mat_node_{kind.ToString().ToLowerInvariant()}.tres"`） | 枚举改名 → 静默退回默认色（有日志，`GameLog.Write` :117 —— 这条做得对） |
| `Projectile.cs` 爆炸场景路径硬编码 | `Projectile.cs:166` | 见 §3-⑤ |
| `PlayerHud.cs` / `WorldOverlay.cs` **节点名**硬编码 | `PlayerHud.cs:17,49-55,186-188`；`WorldOverlay.cs:19-23` | 注释自己承认「节点名改了要同步这里，别让它静默失效」（`PlayerHud.cs:185`） |

**建议的统一做法**：能引用 `Resource` 的就不写字符串（`ConfigRow` 已经支持 `[Export] XxxConfig[]`）；
确实要写字符串的，**在 `Balance.BuildTableIndex` 里加一次启动校验**并把失败写进 `GameLog`
（现在的做法是「到用的时候才静默跳过」）。

### ④ 表里有一列，但回退路径会静默换数值

**这是「分表」的经典风险，项目里已经有一处**：

```
Balance.cs:89   row?.HealthMultiplier ?? config.EnemyHealthMultiplier
                 ↑ 0.75（表）           ↑ 1f（C# 默认值）
Balance.cs:92   row?.DamageMultiplier ?? config.EnemyDamageMultiplier
                 ↑ 0.3（表）             ↑ 0.8f（C# 默认值）
```

`Balance.tres:60-61` 写的是 0.75 / 0.3，`BalanceConfig.cs:45-46` 的**属性初始化器**是 1f / 0.8f。
只要 `Balance.tres` 加载成功，`config.EnemyHealthMultiplier` 就是 0.75（与表一致）；
但只要 `enemy_global` **单独读不到**（数组里空元素、`Id` 被改），就会掉到 1f / 0.8f。

**为什么这次不是「和分表前逐位一致」**：分表前的唯一真源就是 `Balance.tres` 的 0.75/0.3，
而现在的兜底是**再下一层**的 C# 默认值 1f/0.8f —— **两层回退的最后一层与第一层不同**。
`Balance.ScaleEnemy`（`Balance.cs:83-104`）**没有任何日志**，所以这次换挡是静默的。
**建议**：`ScaleEnemy` 里加一条「来源：表 / 代码默认值」的 `GameLog.Write`
（这是项目自己在 `Balance.cs:41-43` 与 `RunController.cs:298-301` 已经建立的纪律）。

### ⑤ 文件系统/场景层面的「该改没改」

| 项 | 现状 | 建议 |
|---|---|---|
| `BalanceConfig` 的 `[Export]` 数 | **54 个 `[Export]` 行**（其中 8 个是表索引数组：`Stages` + `Weapons/Characters/Enemies/Effects/Nodes/Upgrades/MetaUpgrades`），**真·标量数值字段 46 个** | 任务描述里的「约 40 个」接近但不精确 —— **实际 46 个标量字段 + 8 个表索引**（`BalanceConfig.cs` 全文 159 行，含 14 个 `[ExportGroup]`） |
| 已落地 8 张表的列数 | 参见 §0 的「自身列数 / 含基类共」两列：`WeaponConfig` 21(+6) / `CharacterConfig` 10(+6) / `EnemyConfig` 10(+6) / `EffectConfig` 11(+6) / `NodeConfig` 15(+6) / `RewardConfig` 10(+6) / `MetaUpgradeConfig` 7(+6) / `StageDefinition` 23（不继承 `ConfigRow`） | ⚠️ **`config-schema.md` 里引用的字段数（13 个角色字段等）是设计时的规划值，与落地后的实际列数不同** —— 落地时砍掉了没消费者的列。这是**好事**（符合「不留不生效的配置」），但引用时要按实际列数 |
| `EnemySpawnEntry` 是 `Node` 不是 `Resource` | `EnemySpawnEntry.cs:9` | 变成 `Resource` 才能「一行一个 `.tres`」（设计文档点名要迁移） |
| `RewardCatalog.Fallback` 与 19 行 `.tres` 双份 | `RewardCatalog.cs:21-91` | 保留（符合回退约定），但**加一条自测**：逐字段比对表与 Fallback，不一致就警告（`SelfTest.cs:563` 现在只比了条数 19 与首条 id） |
| `SelfTest` 的期望值写死 | `SelfTest.cs:522-563`（「期望 250」「期望 24」「期望 0.26」…） | 分表后这些期望值就是**表的回归基线** —— 好；但改表时**必须同步改 `SelfTest`**，否则自测会误报。**建议在 `config-schema.md` 的验证清单里显式写上这一点** |
| 10 个敌人的 AI 参数没有任何「公共默认」 | 各 `.tscn` | 见 #15 |

### ⑥ 文档与代码不一致（供交叉核对）

| 文档说法 | 代码实际 | 位置 |
|---|---|---|
| `boss.md:31`：「Boss 压力曲线…全部在 Inspector 可调」 | 只能调一部分：`Interval` / `DamagePerLevel` / `AttackSpeedPerLevel` 被 **`EncounterController.SpawnBossNow` 用 Balance 的值覆盖**（`EncounterController.cs:668-670`）；`MaxLevel` / `MoveSpeedPerLevel` / `AddSpawnLevel` / `AddsPerLevel` / `AddSpawnRadius` **不覆盖**（保留 `Boss.tscn:176-182` 的值） | 即「Boss 压力」实际上**有一半的旋钮在 Balance，一半在场景**，改错地方不生效 |
| `boss.md:60`：「词条数量由每个 Stage 配置：Stage 1 = 1 个、Stage 2 = 2 个、Stage 3 = 3 个」 | ✅ **一致**：`stg_outer.tres` = 1 / `stg_inner.tres` = 2 / `stg_core.tres` = 3 | — |
| `config-schema.md:28`：「已落地 8 张（阶段 1+2）」 | 一致 ✅ | — |
| `config-schema.md:162`：状态预设「Slow 0.55 / Weaken 0.75 / Burn 6dps / Empowered 1.3·1.15 / Stun 0.12·0.5」 | `StatusEffect.cs:53-100` 的 Fallback 逐位一致 ✅；且 `eff_slow/weaken/burn/empowered/stun` 五行 `.tres` 也在 ✅ | — |
| `config-schema.md:55`：「敌人生成项 8 条」 | `Main.tscn` 里确实是 8 个 `EnemySpawnEntry` ✅ | — |
| `config-schema.md:56`：「敌人预制体 10 个，脚本属性赋值 299 处」 | 场景文件确实 10 个 ✅（赋值处数**未逐个数**） | — |

---

## 6. 统计与附录

### 6.1 按优先级 / 建议分布

| 建议 | 张数 | 表 |
|---|---|---|
| **高**（该先做） | 6 | **✅ #9 技能表（阶段 3）· #10 敌人按敌种表（阶段 3）· #11 首领数值强化表（阶段 3+6）· #12 首领行为词条表（阶段 3）· #15 AI 行为表（阶段 5）** · ⏸ #16 刷怪生成项表 |
| 中 | 14 | #13 · #14 · #17 · #18 · #19 · #20 · #23 · #25 · #27 · #28 · #29 · #31 · #33 · #37 |
| 低 | 7 | #30 · #32 · #34 · #38 · #39 · #36（或并入 #31） · #40（或做成「设置」而非表） |
| **并入别处**（不新建表） | 6 | #21 → #2 · #22 → #16 · #24 → #10 · #26 → 应删除 · #28 → #6 加一列 · #29 → #6 + Balance |
| ⛔**明确排除** | 14 | §3 的 6 大类：#35 存档路径 · #36 摆放启发式 · #40 相机手感 · #41 伤害跳字 · #42 血条 · #43 HUD 文案 · #44 按键映射 · #45 特效/音效引用 · #46 规则常量 · #47 日志开关（其中 #41/#42 已在 Balance，**保留现状**） |
| **合计** | **47** | **8 已落地 / 3 设计未做 / 36 新发现** |

> **怎么读这张表**：47 项 = **8 已落地** + **3 设计未做** + **36 新发现**。
> 36 项新发现里：**27 项建议新建**（6+14+7）+ **6 项建议并入已有表** + **3 项建议删除/保留为设置**（#26 删、#36 并入 #31、#40 做成设置）。
> 任务要的是「**能不能成表**」的判断清单，所以把「不该成表」的也列出来并写清理由，比直接丢掉更有用。

### 6.2 按「数据现在在哪」分布

| 数据位置 | 表数 | 代表 |
|---|---|---|
| 已在 `Config/Tables/`（8 张已落地） | 8 | `WeaponConfig` 等 8 张 |
| 在 `Balance.tres` / Balance 的展示字段（**建议保留现状**） | 3 | #41 伤害跳字 · #42 血条 · #43 HUD 文案（#47 日志开关属软件参数，不是 Balance） |
| **只存在于 `.tscn` 脚本属性** | **10** | #9 · #10 · #13 · #14 · #15 · #16 · #17 · #18 · #19 · #25 |
| **只存在于 C# 字面量 / `[Export]` 默认值** | **14** | #12 · #23 · #24 · #26 · #27 · #30 · #31 · #32 · #33 · #34 · #36 · #38 · #39 · #47 |
| 不存在（表里的空壳字段，数据尚无家） | ~~1~~ → **0** | ✅ #11 **阶段 6 已落地**（`Pressure/pmod_*.tres` 3 行），空壳字段全部接上 |
| 不存在（关联关系无载体） | 1 | #20 角色↔技能矩阵（现在是 `CharacterConfig` 的字符串数组） |
| 不存在（无法新建，应并入已有表） | 2 | #21 职业参数（已在角色表） · #29 奖励池过滤（`RewardChoiceCount` 已在 Balance） |
| 不存在（应改设计而非建表） | 3 | #22（并入 #16） · #28（并入 #6） · #37（`Prompt` 与节点表 `Title` 重复，应删除） |
| 引擎配置（`project.godot`，不建议成表） | 3 | #40 相机（建议做设置） · #44 按键映射 · #45 特效引用 |
| 规则常量 / 数学保护值（不建议成表） | 2 | #35 存档路径 · #46 结算顺序等 |
| **合计** | **47** | — |

### 6.3 未确认项清单（需要进一步核实）

1. **`Weapon.ProjectileSpeed` 是否生效** —— `Weapon.TryFire`（`Weapon.cs:150-227`）构造的
   `AttackInstance` 里**没有 Speed 字段**，`Projectile.Launch` 用的是自己节点上的 `Speed`
   （`Projectile.cs:40`，来自 `Bullet.tscn:10` = 75）。而武器表写的是 90。
   **→ 如果属实，`wpn_*.tres` 的 `ProjectileSpeed` 与 `MaxLifetime` 都是「填了但没用」的列。**
   （`MaxLifetime` 走了 `AttackInstance.Lifetime` → `Projectile._attack.Lifetime`，:55，**这条是生效的**。）
2. **`StageDefinition.IsFinalBoss` 是否有消费者** —— ✅ **已确认：没有**（`grep IsFinalBoss` 在 `Scripts/` 全域只命中 `StageDefinition.cs:46` 的定义行本身）。而 3 个 stage 行都填了它 → **确认是死字段**。

### 6.4 复核命令（给下一个接手的人）

```powershell
# BalanceConfig 到底有多少 [Export]
Select-String -Path Scripts\Config\BalanceConfig.cs -Pattern '^\s*\[Export' | Measure-Object

# 每张表当前有多少行
Get-ChildItem Config\Tables -Directory | ForEach-Object {
  "{0}: {1}" -f $_.Name, (Get-ChildItem $_.FullName -Filter *.tres).Count
}

# 每张已落地表的列数（自身 / 含 ConfigRow 基类 6 列）
Select-String -Path Scripts\Config\*Config.cs -Pattern '^\s*\[Export' |
  Group-Object Filename | ForEach-Object { "{0}: {1}" -f $_.Name, $_.Count }

# 哪些字段是死的（示例：BossHealthBarScale）
Select-String -Path Scripts\**\*.cs -Pattern 'BossHealthBarScale' -Recurse

# 每个敌人场景的 Unit 数值一览
Get-ChildItem Scenes\Enemies\*.tscn | ForEach-Object {
  "=== $($_.BaseName) ==="; Select-String -Path $_.FullName -Pattern '^(MaxHealth|MaxArmor|MoveSpeed|Tags|KnockbackResistance|ArmorDamageReduction) ='
}

# 某列到底有没有消费者（示例：EffectConfig.Value）
Select-String -Path Scripts\**\*.cs -Pattern '\.Value\b' -Recurse | Select-String -NotMatch 'EffectConfig.cs'
```

### 6.5 与 `config-schema.md` 的差异（本文相对它的增量）

| 项 | `config-schema.md` | 本文 |
|---|---|---|
| 表数 | 11 张（T1–T11，其中 T8 落地为「效果表」、T9/T11 实为同一张） | **47 项**（含 27 项建议新建） |
| 技能表 T2 | 列为设计（§3 T2） | 落实为 #9，并**补上了 `Player.tscn` 之外的 10 个敌人场景的能力参数**（Boss/SkillElite/冲锋兵/跳兵/自爆兵/远程兵/尖啸者/护卫/Chaser/FastMelee —— 每个都有一份） |
| 敌人表 T5 | 「敌人表**可选覆盖** + 场景保留」 | 明确 **10 个场景的公共列高度一致（6 列 10/10 都有）→ 该按敌种成行**，并列出每个敌种的具体值 |
| 首领增强 | 只有 T9/T11 一张（含词条） | **拆成 #11 数值型（不存在）+ #12 行为/技能型（C# 数组）两张**，因为《关卡核心文档》§7.1 要求分离 |
| AI 行为 | §1.2-F5 提过「≈25 项」，未列表 | 列为 #15，**逐敌种列出 10 组参数** + 4 处硬编码的避障启发式 |
| 波次/预算 | §1.2-G4 提过「两份」，归入关卡表 | 列为 #17，**结论同为「并入关卡表」**，但列出了 `WaveMode/WaveMinEnemies/WaveMaxEnemies/WaveMaxTypes` 等**关卡表当前没有的列** |
| 投射物/召唤物 | §1.2-C5/B8 提过，未列表 | 列为 #18 / #19 |
| 表现参数（跳字/血条/相机/UI） | §1.2-A9/A10/H10 提过，未给结论 | **§3-①② 明确给出「不建表」的理由**（不随内容变化 / 是玩家设置） |
| 死字段 | 列了 5 个（`Weight` / `ConflictsWith` / `PoolTags` / `BossPerk.Weight`） | **新增 6 处**：`BalanceConfig.BossHealthBarScale` / `BalanceConfig.EliteBossHealthScale` / `StageDefinition.IsFinalBoss` / `EffectConfig.Value·Value2·Radius·TickSeconds` / `EffectConfig.Duration`（被调用方架空）/ `NodeConfig.BossPressurePoints·BossModifierRolls·BossModifierTags`。<br>🆕 **2026-10 阶段 4**：这 5 个里 `PoolTags` 已删、`ConflictsWith` 已接上；两个 Balance 字段已在阶段 3 删除/搬家 |
| 武器 ↔ 升级池 | §4.5 推荐**反向指**（`RewardConfig.ApplicableWeapons`） | 本文 §2.19 当时建议"按设计文档的反向指做、不新开表"。🆕 **实际落地是正向指 + 新开池表**（用户拍板，2026-10 阶段 4），两条建议都**没有**被采纳 —— 见 §2.19 的更新块与 [config.md](config.md) 的「配置表阶段 4 实测」 |
| 静默换挡 | §1.4-① 讲过关卡表的漂移（已修） | **新增一处**：`Balance.ScaleEnemy` 的敌人全局倍率（0.75 → 1f / 0.3 → 0.8f，且无日志） |
| 疑似未生效的列 | 无 | **新增**：`Weapon.ProjectileSpeed`（`Weapon.TryFire` 从不用它，见 §6.3-1） |
| 已落地表的列数 | 文中引用的是设计时的规划值（如「角色表 13 字段」） | **按实际代码数**：`CharacterConfig` 落地为 **10** 列（+6 基类），砍掉了没消费者的列 |

---

*本文由只读盘点生成；未修改任何代码、场景、`.tres`，也未修改 `docs/config-schema.md`。*
*⚠️ 盘点期间 Godot 编辑器（`Godot_v4.7.2-stable_mono_win64`，PID 8952，13:31 启动）一直开着；
它自身的资源重导入会刷新 `Config/Tables/**/*.tres` 与 `Scripts/**/*.cs.uid` 的修改时间 ——
那些时间戳变化不是本文造成的。本文的全部写操作只有本文件本身。*
