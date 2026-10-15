# 配置参数表体系设计（Config Schema）

> ⚠️ **状态横幅（2026-10-16 P6b 加）**：本文件是**设计期的方案稿**，不是现状说明；
> 它描述的分表方案早已落地，而里面出现的 `MagazineSize` / `ReloadTime` / `ReloadSpeedMultiplier` 等列
> **已随用户裁定「没有弹匣的概念了」整条删除**（P6b，见 [character-redesign.md](character-redesign.md) §五）。
> 现在的表清单与字段以下面两处为准：[config.md](config.md)（调参唯一入口）、`Scripts/Config/*.cs`（字段定义）。

> 项目：`F:\Godot项目\项目a`（Godot 4.7.2 stable mono / C# / net8.0）
> 本文回答三件事：**① 现在有哪些可配置项、哪些是硬编码；② 按类型怎么分表（类 / 字段 / `[Export]` / 目录）；③ 怎么分阶段搬过去而不炸。**
> 相关：[todo-handoff.md](todo-handoff.md)（约定与统一入口）、[architecture.md](architecture.md)（分层与踩过的坑）、[controller-flow.md](controller-flow.md)（流程）、[assets.md](assets.md)（目录与命名规范）、[config.md](config.md)（当前数值入口）
>
> ⚠ **本文以设计为主。** 文中的 C# 代码全部按 Godot 4.7.2 实际 API 写成，关键签名已用 `GodotSharp 4.7.2` 反射核对（见 §7）。
> **例外（已落地）**：**阶段 0 / 1 / 2 / 3** 已按本文实施并实测。
> - 阶段 0/1：`Scripts/Config/{ConfigTable,ParamEntry,ConfigText,WeightedPick}.cs`、`Config/Tables/Stages/stg_*.tres`、`RunController.DefaultStages()` 已删除。
> - **阶段 2（2026-10）**：`ConfigIndex` 拆成 **8 张表**（武器 / 角色 / 敌人 / **效果** / 节点 / 增幅项 / 局外成长 / 关卡），
>   新增行类 `ConfigRow`(基类) / `WeaponConfig` / `CharacterConfig` / `EnemyConfig` / **`EffectConfig`** /
>   `NodeConfig` / `RewardConfig` / `MetaUpgradeConfig` / `StatOverride` + 索引器 `ConfigRows` +
>   增幅项处理器注册表 `RewardEffectRegistry` / `RewardTable`。
>   进度、实测证据与逐项比对见 **§5 阶段 2**；YARD 真机冒烟结论见 **§7.2 末尾**。
> - **阶段 3（2026-10，本文的 T2 / T5 / T9 / #24 落地）**：新增**单位表基类 `UnitConfig`** 与
>   **技能表 `AbilityConfig`**，共 **12 张表**（新增 `Enemies` 按敌种 9 行 / `Bosses` 1 行 / `Summons` 1 行 /
>   `Skills` 25 行 / `Pressure` 3 行）；`EnemyConfig` / `BossConfig` / `SummonConfig` / `CharacterConfig`
>   全部改为 `: UnitConfig`；白名单从"按中文显示名"改成"按敌人表 Id"。
>   进度、实测与回退验证见 **§5 阶段 3**。
> - **阶段 4（2026-10，用户点名要的"武器 → 升级池 → 选项"三层关联）**：新增**升级池表
>   `UpgradePoolConfig`**（`Config/Tables/UpgradePools/`，3 行），
>   `WeaponConfig.Pool`（**正向指**）+ `RewardConfig.Pool`（**一个选项占一行 + 一个引用列**）；
>   **三级回退**落在 `RewardDefinition.AllowsWeapon`；顺手**删掉死字段 `PoolTags`、接上 `ConflictsWith`**。
>   ⚠️ 这一轮**推翻了本文 §4.5.2 / §6.9 的"反向指"推荐** —— 用户拍板要正向指，理由与落地形态见
>   **§4.5.0** 与 **§5 阶段 4**。
> - **阶段 5（2026-10，盘点 #15「AI 行为表」）**：新增**AI 行为表 `AiProfileConfig`**
>   （`Config/Tables/AiProfiles/`，11 行 = 一行一个 AI 档案，一共 **14 张表**），
>   `UnitConfig.AiProfile`（**资源引用**，不是字符串 id）+ `AiProfileConfig.ApplyTo` 套到
>   `EnemyBrain` 与各 `Behavior` 节点。盘出 **25 个 AI 参数**（原先散在 10 个敌人场景的 53 处赋值里）。
>   套用挂在 `UnitConfig.ApplyTo` 上，**没有碰 `EncounterController`**。进度、实测与两次回退验证见 **§5 阶段 5**。
> - **阶段 6（2026-10，本文 T7 的后半 + T9 的最后一步）**：**Pressure 转换池接线**。
>   `PressureModConfig` 多一列 `Tags`、`Weight` 真的按权重抽；`NodeConfig` 的三个空壳字段
>   （`BossPressurePoints` / `BossModifierRolls` / `BossModifierTags`）全部接进
>   `StageController.AddPressureForNode`；`StageDefinition.PressureUpgradeTags` 从"半活"变成真消费者；
>   §7.3 的跨阈值改成**显式逐次循环**；**两个抽取点彻底分开**（Boss 开场词条只抽行为型 = Boss Build，
>   Pressure 转换只抽数值型 = §7.5）。进度与实测见 **§5 阶段 6**。
> - **阶段 7（2026-10，用户要求「3 条拆成 5 条 / 每个参数单独占一个词条栏 / 技能参数也进词条 / 限定词要刷新判定」）**：
>   `Pressure/` 从 **3 行 → 8 行**（5 条数值型参数栏 + 3 条技能参数栏）；
>   `PressureModConfig` 多 **5 列**（`AbilityNode` / `AbilityScales` / `AttackScales` = 技能参数，
>   `BossIds` / `StageIds` = 限定词）；新增 `Scripts/Boss/PerkQualifier.cs`（限定词数据 + 判定）与
>   `BossPerkCatalog.ApplyWithRefresh`（**在 Boss 出生时**判限定词、不成立就**换抽**）。
>   **两个既有抽取点（`StageController` / `EncounterController`）一行都没改** —— 判定挂在 `perk.Apply` 里。
>   进度、权重依据、强度换算与实测见 **§5 阶段 7** 与 [config.md](config.md) 的「配置表阶段 7 实测」。
>
> 🔗 **与 [config-tooling.md](config-tooling.md) 的分工**：那份文档回答"**用什么工具**批量编辑这些表"，结论是装 `Edit Resources as Table` 插件、**把文件夹当表、一行一个 `.tres` 文件**。
> **这个形态是本文表格设计的硬约束** —— 本文的行布局（§2.3）与文件粒度取舍（§6.2）已经按它对齐：**每一行 = 独立的 `.tres` 文件，一个文件夹 = 一张表**。

---

## 0. 结论摘要（只看这段也够）

| 问题 | 结论 |
|---|---|
| 盘出多少可配置项 | **632 处磁盘配置实例**（`Scenes/**.tscn` 里 513 处脚本属性赋值 + `Config/ConfigIndex.tres` 里 119 处）+ **305 个 C# 可导出属性槽**（分布在 49 个文件） |
| 多少当前硬编码 | 全项目 **214 处**十进制字面量；剔除测试代码（48）、纯表现/UI 布局（61）、clamp/epsilon/形状数学后，**约 65 处是"可调玩法数值"**（A 类 40 + B 类 25）。其中 **40 处集中在 7 个文件**：**30 处在 6 个纯"目录式"文件**里（`RewardCatalog` 11 / `StatusEffect` 6 / `BossPerk` 4 / `MetaProgress` 4 / `RunSettlement` 3 / `SpawnPlanner` 2），另 10 处在 ~~`RunController.DefaultStages()`~~ —— 这 40 处就是"应该整表外置"的候选。✅ 其中 `RunController` 那 10 处已随阶段 1 删除 |
| 拆几张表 | **11 张**（T1–T11）：武器 / 技能 / 增幅项 / 角色 / 敌人 / 关卡 / 节点 / 状态 / **首领增强（含词条）** / 局外成长（见 §3）。✅ **已落地 14 个表目录**（阶段 1+2+3+4+5）：`Weapons` / `Characters` / `Enemies` / `Bosses` / `Summons` / **`AiProfiles`** / `Skills` / `Pressure` / **`UpgradePools`** / `Effects` / `Nodes` / `Upgrades` / `Meta` / `Stages`；✅ **Pressure 转换池已在阶段 6 接线**（表与抽取点都到位，见 §5 阶段 6） |
| 第一阶段做哪张 | **关卡表（T6）**。理由见 §5 的「阶段 1」小节：类已经存在（`StageDefinition` 15 个字段）、改动只有 2 个调用点、有现成自测可逐项比对、而且顺手消掉一个**已经漂移的真 bug**。✅ **阶段 0 与阶段 1 已完成（2026-10）**，进度与实测证据见 §5；两条新挖出来的坑见 §5 阶段 1 与 §7.2 |
| 行怎么放 | **一行 = 一个 `.tres` 文件，一个文件夹 = 一张表**（[config-tooling.md](config-tooling.md) 推荐的 `Edit Resources as Table` 插件按文件夹分组，这是硬约束）。`ConfigIndex.tres` 持有 `[Export] XxxConfig[]` 数组显式引用这些行 —— **不扫目录**，导出后也不会掉文件 |
| 最需要拍板的 | **效果怎么表达**（`ApplyHandler` 字符串 + C# 处理器注册表，还是允许内嵌脚本）—— 这一条决定整套体系是"可校验的数据"还是"换皮代码"（见 §6.1）。另有两条与你的两个场景直接相关：**武器→升级池的关联方向**（§6.9）与**场景 A 的范围**（§6.10） |

**两个具体使用场景的答案（速查）**

| 用户的原话场景 | 照本设计怎么做 | 落在哪一节 |
|---|---|---|
| 「调**奖励节点用于增强首领的抽奖值和强化参数**」 | 分三处：**抽几次** → `NodeConfig.BossModifierRolls`（按节点累加）；**抽中概率** → `PressureModConfig.Weight`（与增幅项同一套加权抽取，抽成公共的 `WeightedPick`）；**强化参数** → `PressureModConfig.Modifiers : StatOverride[]`。**归到 T9/T11 首领增强表**（不与增幅项混表，理由见 §3 T9）。✅ **2026-10 阶段 6 已全部落地** —— 注意落地名与本文原稿不同（`PressureModConfig`、目录 `Pressure/`，无 `Source`/`MaxLevel`），见 §3 T9 的落地形态表与 §5 阶段 6 | §3 T9 |
| 「调**某个武器的基础参数 + 其对应升级池里的增幅参数**」 | 武器基础参数 → T1 武器表；"对应升级池" → ✅ **阶段 4 已落地**：**武器行 `WeaponConfig.Pool` 正向指向一个新表 `UpgradePoolConfig`（一行一个池）**，池的成员 = 增幅项行里 `RewardConfig.Pool` 指向它的那些行（**一个选项占一行 + 一个引用列**）。三级回退：① 专属池 → ② 武器类别标签（现有 4 条进化）→ ③ 通用池（现有 10 强化 + 5 异化）。~~反向指 `ApplicableWeapons`~~ 已被用户拍板否决，见 §4.5.0 | §3 T3 + §4.5 |

**一句话方案**：把「谁有什么参数」从 C# 委托/三元表达式里搬进 `Config/Tables/<分组>/*.tres`，**一行 = 一个 `.tres` 文件**；每张表在磁盘上是若干独立 `Resource` 行、在运行期**编译成现有类**（`RewardDefinition` / `StatusEffect` / `BossPerk` 等形态不变），所以 `RewardPool` / `RunBuild` / `RunController` / `EncounterController` 这些消费方**一行都不用改**；`ConfigIndex.tres` 通过 `[Export] XxxConfig[]` 数组把这些行**显式引用**起来（不扫目录），因此 `ConfigIndex` 仍是唯一数值入口；读表统一走 `ConfigTable.ById()` / `TryLoad<T>()`，读不到就回退到组件自身的 `[Export]` 默认值——这正是 `ConfigIndex.cs:25` 已经在用的写法。

---

## 1. 第一步：现状盘点

### 1.1 数字总览

| 指标 | 数量 | 来源（可复核） |
|---|---|---|
| 磁盘上的配置实例 | **632** | `Scenes/**.tscn` 里 513 处 PascalCase 属性赋值（22 个场景）+ `Config/ConfigIndex.tres` 里 119 处 |
| C# 里的可导出属性槽 | **305** | `[Export]` 统计，分布在 49 个 `.cs` |
| `ConfigIndexData` 字段 | **74 → 30**（14 个 `[ExportGroup]`） | ✅ **阶段 2 把它们拆成了 8 张表**（25 武器 + 13 角色 + 3 商店价目 = 41 个字段已删除，转移到 `Config/Tables/`；剩 30 个是跨表全局开关）。分表前与 `ConfigIndex.tres` 的 119 处赋值对齐：`74 + 3×15 = 119` |
| `StageDefinition` 字段 | **15** × 3 关 = 45 | `StageDefinition.cs`，作为 `[sub_resource]` 内嵌在 `ConfigIndex.tres:6-58` |
| 增幅项定义 | **19** 条（强化 10 / 进化 4 / 异化 5） | `RewardCatalog.cs:16-86`；与 [rewards.md](rewards.md) 实测的"枪手 18 / 召唤 19"吻合 |
| 敌人生成项 | **8** 条 | `Main.tscn:75-132`（类 `EnemySpawnEntry` 共 7 个 `[Export]`，每条被赋值 2–5 个） |
| 敌人预制体 | 10 个，脚本属性赋值 **299** 处 | `Scenes/Enemies/*.tscn`（Boss 72 / SkillElite 53 / RangedEnemy 31 / Guard & Screamer 23 / Exploder·JumpingMelee·Charger 21 / Chaser·FastMelee 17） |
| 十进制字面量（含测试） | **214** | `Scripts/**/*.cs` 非注释、非 `[Export]` 行 |

### 1.2 现有配置项总表

> 「是否硬编码」= 该数值是**写死在 C# 里**（改它要重新编译），而不是在 `.tres` / `.tscn` / Inspector 里。

#### A. 全局（`Config/ConfigIndex.tres`，74 字段）

| # | 现有配置项 | 当前存放位置 | 是否硬编码 | 建议归属哪张表 |
|---|---|---|---|---|
| A1 | 玩家基础数值（生命/护甲/护盾/移速/减伤）5 项 | `ConfigIndex.tres:61-65`、`ConfigIndexData.cs:35-39` | 否 | **角色表** `character_1`（Gunner 行） |
| A2 | 近战重装数值 5 项 | `ConfigIndex.tres:66-70`、`ConfigIndexData.cs:42-48` | 否 | **角色表** `character_2` |
| A3 | 召唤控制数值 3 项 | `ConfigIndex.tres:71-73`、`ConfigIndexData.cs:51-53` | 否 | **角色表** `character_3` |
| A4 | 敌人全局倍率 4 项 | `ConfigIndex.tres:99-102` | 否 | **保留在 ConfigIndex**（难度总开关，跨表生效） |
| A5 | 节奏倍率 2 项 | `ConfigIndex.tres:103-104` | 否 | 保留在 ConfigIndex |
| A6 | Boss 全局倍率与压力 5 项 | `ConfigIndex.tres:105-109` | 否 | 保留在 ConfigIndex（曲线参数可另立 Boss 表） |
| A7 | 关卡序列 `Stages` | `ConfigIndex.tres:114`（3 个内嵌子资源） | 否（但内嵌） | **关卡表** `Config/Tables/Stages/`（索引用 `ConfigIndex.tres` 上已有的 `Stages` 数组） |
| A8 | 成长/商店 4 项 | `ConfigIndex.tres:110-113` | 否 | 商店价目留在 ConfigIndex；`RewardChoiceCount` 留 ConfigIndex |
| A9 | 表现·伤害跳字 4 项 | `ConfigIndex.tres:115-118` | 否 | 保留（表现，非玩法） |
| A10 | 表现·血条 4 项 + Boss 血条 2 项 | `ConfigIndex.tres:119-123,134` | 否 | 保留 |
| A11 | Pressure 结算 4 项 | `ConfigIndex.tres:128-131`、`ConfigIndexData.cs:141-150` | 否 | 保留（或并入节点表） |
| A12 | 节点/关卡约束 6 项（`MinNodeSpacing` / `KillPlaneY` / `NodeHealRatio` / `EliteBossHealthScale` / `FreeChallengeBaseQuota` / `FreeChallengeMaxQuota`） | `ConfigIndex.tres:124-127,132-133` | 否 | 保留；`KillPlaneY` 归地图表 |

#### B. 武器

| # | 现有配置项 | 当前存放位置 | 是否硬编码 | 建议归属哪张表 |
|---|---|---|---|---|
| B1 | 武器槽位枚举 3 值 | `ConfigIndexData.cs:6-11` `WeaponPresetKind` | **是**（编译期常量，加武器要改 C# 重编译） | **武器表** `Id` + `Slot`（枚举可降级为快捷键壳） |
| B2 | 手枪数值 11 项 | `ConfigIndex.tres:74-84`、`ConfigIndexData.cs:56-68` | 否 | **武器表** `weapon_1` |
| B3 | 狙击数值 7 项 | `ConfigIndex.tres:85-91`、`ConfigIndexData.cs:71-79` | 否 | **武器表** `weapon_2` |
| B4 | 步枪数值 7 项 | `ConfigIndex.tres:92-98`、`ConfigIndexData.cs:82-90` | 否 | **武器表** `weapon_3` |
| B5 | 武器 → 数值的映射 | `RunController.cs:387-414` `EquipWeapon`，11 行三元链 | **是**（且第 408-412 行把 `Pistol*` 字段用在**所有**武器上，狙击/步枪的暴击/弹速/装填延迟实际上没有独立配置项） | 删掉，改查武器表 `WeaponConfig.ApplyTo(weapon)`（§3 T1） |
| B6 | 武器组件自身默认值 21 项 | `Weapon.cs:15-49` | 否（是回退基线） | **保留在组件上** = §4.2 的"自身默认值" |
| B7 | 手枪预制体数值 10 项 | `Scenes/Weapons/Pistol.tscn:16-25` | **是**（与 `ConfigIndex.tres:74-84` **重复且数值不同**，见 §1.4-③） | 与武器表二选一（拍板点 §6.3） |
| B8 | 子弹参数 2+4 项 | `Scenes/Combat/Bullet.tscn:11-12`、`Projectile.cs:13-23` | 否 | 保留（弹速被武器覆盖，见 assets.md §7 红线） |

#### C. 技能 / 能力

| # | 现有配置项 | 当前存放位置 | 是否硬编码 | 建议归属哪张表 |
|---|---|---|---|---|
| C1 | 能力通用参数 8 项（`Aim`/`Cooldown`/`Windup`/`Recover`/`Enabled`/`AbilityId`/`ApproachMin*`/`Max`） | `Ability.cs:30-43` | 否 | **技能表**通用项 |
| C2 | 玩家 5 个技能实例参数 **40 处**（该场景脚本属性赋值共 45 处） | `Scenes/Player/Player.tscn:64-140`（`AbilityDash` 5 / `AbilityBurst` 6 / `AbilityMelee` 5 + 命中体 3 / `AbilitySummon` 8 / `AbilityControl` 8 + 命中体 5） | 否（在场景里） | **技能表** `ability_1..5` + `Params` |
| C3 | 技能子类专属参数 28 项 | `DashAbility.cs:12-14`、`ChargeAttackAbility.cs:12-13`、`LeapAttackAbility.cs:12-15`、`AreaAttackAbility.cs:12-14`、`SummonAbility.cs:12-17`、`BuffAlliesAbility.cs:12-15`、`BurstFireAbility.cs:16-19`、`ProjectileAttackAbility.cs:13-17`、`MeleeAttackAbility.cs:12-14` | 否 | **技能表** `Params` 键值对（子类字段各不相同，穷举会让表爆炸） |
| C4 | 命中体参数 9 项 | `AttackHitbox.cs:13-24` + 各场景 | 否 | 技能表 / 敌人表 `Params` |
| C5 | 召唤物参数 | `Scenes/Summons/Drone.tscn`（19 处）、`SummonLifetime.cs:11`（25 秒） | 部分（`maxSummons=2` 硬编码在 `RewardCatalog.cs:79`） | 技能表 + 召唤物表 |
| C6 | 玩家按键映射 5 个动作 | `project.godot [input]` + `PlayerAbilityInput.cs:49-66` | **是**（动作名字符串） | 设置表（[config.md](config.md):155 已列为待办） |

#### D. 增幅项（用户重点）

| # | 现有配置项 | 当前存放位置 | 是否硬编码 | 建议归属哪张表 |
|---|---|---|---|---|
| D1 | 19 条增幅项定义 | `RewardCatalog.cs:16-86` | **是**（C# 委托 + 字面量） | **增幅项表** `reward_*` |
| D2 | 增幅幅度 13 处（1.15/1.12/1.10/0.08/0.35/1.25/0.08/3.5/0.6/0.12/0.12…） | `RewardCatalog.cs:20,22,24,28,30,32,36,49,50,59,67` | **是** | 增幅项表 `Value` / `Params` |
| D3 | 权重 `Weight` | `RewardDefinition.cs:46` | 字段存在但 **19 条全部没填**，恒为 `1f` → 加权抽取形同虚设 | 增幅项表 `Weight`（真正接上 `RewardPool.Roll` 的权重逻辑） |
| D4 | 互斥 `Excludes` / 标签 / 层数 / 角色限定 | `RewardCatalog.cs:90-130` 三个工厂方法 | 否 | 增幅项表字段 |
| D5 | 互斥 `ConflictsWith` | `RewardDefinition.cs:44` | 字段存在但**从未被任何一条赋值**，`RewardPool.cs:68-75` 的判定是死代码 | 增幅项表字段（接上或删掉，见 §6） |
| D6 | 增幅项说明文案 | `RewardCatalog.cs` 各 `Description` + `RewardDefinition.cs:33` | **是**（拼死在 C#） | 增幅项表 `Note` + `DescriptionTemplate` |
| D7 | 节点类型 → 池过滤 / 界面标题 | `RewardService.cs:16-38`（3 个 switch） | **是** | **节点表** |
| D8 | 商店三项报价 | `ConfigIndex.tres:111-113` | 否 | 保留在 ConfigIndex（或节点表） |

#### E. 角色

| # | 现有配置项 | 当前存放位置 | 是否硬编码 | 建议归属哪张表 |
|---|---|---|---|---|
| E1 | 角色枚举 3 值 | `ConfigIndexData.cs:14-24` `PlayerCharacterKind` | **是** | **角色表** `Id` + `Slot` |
| E2 | 角色 → 数值的映射 | `RunController.cs:78-122` `ApplyBalance`，`heavy ? A : summoner ? B : C` 链 | **是** | 删掉，改查角色表 `CharacterConfig.ApplyTo(...)`（§3 T4） |
| E3 | 角色启用哪些技能 | `RunController.cs:100-122`（`_weapon.Enabled = !heavy`、`melee.Enabled = heavy`、`summon/control.Enabled = summoner`） | **是**（硬编码的角色开关矩阵） | **角色表** `WeaponEnabled` / `EnabledAbilityIds[]` |
| E4 | 角色标签 | `Scenes/Player/Player.tscn:37` `Tags = ["Player","Gun"]` | 否 | 角色表 `Tags` |
| E5 | 角色模型 / 图标 | `Scenes/Player/Player.tscn:20,44`（`mesh_player_body.glb`） | 否 | 角色表 `ModelPath` / `IconPath` |
| E6 | 角色选择界面文案 | `StartScreen.cs:46-48`（拼接了数值） | 半硬编码 | 角色表 `DescriptionTemplate` |

#### F. 敌人

| # | 现有配置项 | 当前存放位置 | 是否硬编码 | 建议归属哪张表 |
|---|---|---|---|---|
| F1 | 生成项 8 条（类 `EnemySpawnEntry` 共 7 个 `[Export]`：`Scene`/`Enabled`/`DisplayName`/`Cost`/`Weight`/`MinElapsedSeconds`/`EliteChance`；每条被赋值 2–5 个） | `Scenes/Main.tscn:75-132` | 否（在场景里） | **敌人表** |
| F2 | 敌人本体数值（生命/护甲/护甲减伤/击退抗性/移速/标签）6 类 × 10 场景 | `Scenes/Enemies/*.tscn` | 否 | 敌人表**可选覆盖** + 场景保留（边界见 §3 T5） |
| F3 | 敌人技能/命中体参数 | 各敌人场景（`Cooldown`/`Damage`/`StatusId`…） | 否 | 敌人表 `Params` / 保留在场景 |
| F4 | 关卡白名单（`AllowedEnemies`） | `StageDefinition.cs:25,42-43`，**按 `DisplayName` 字符串匹配** | 半硬编码（改文案就静默失效） | 敌人表 `Id`（稳定键） |
| F5 | AI 行为参数 ≈25 项 | `AI/Behavior.cs:12-18` + 7 个 Behaviors + `EnemyBrain.cs:14-15` | 否 | 敌人表 `Params` / 保留 |
| F6 | 精英修正 7 项 | `EliteModifier.cs:11-19` | 否 | 精英表（可并入敌人表，低优先） |
| F7 | 技能精英随机技能表 | `Scenes/Enemies/SkillElite.tscn` `Skills = ["AbilityBuff","AbilityControl"]` | 否 | 敌人表 `Params` |

#### G. 关卡 / 遭遇 / 节点

| # | 现有配置项 | 当前存放位置 | 是否硬编码 | 建议归属哪张表 |
|---|---|---|---|---|
| G1 | 3 关 × 15 字段 | `ConfigIndex.tres:6-58`（`[sub_resource]`） | 否（但内嵌在 ConfigIndex 里） | **关卡表** `Config/Tables/Stages/stg_*.tres`（每关一个文件） |
| G2 | 关卡兜底序列（3 关） | ~~`RunController.cs:286-325` `DefaultStages()`~~ **已删除（2026-10）** | **是**，且**已与 `ConfigIndex.tres` 漂移**（见 §1.4-①） | ✅ 已删除；改用 `new StageDefinition()` 的 `[Export]` 默认值 |
| G3 | 遭遇参数 20 项（`TotalBudget`/`BudgetRegen`/`BudgetCap`/`MaxConcurrent`/`SpawnInterval`/`SpawnRadius`/`MinSpawnDistance`/波次…） | `EncounterController.cs:21-63` + `Main.tscn:63-145` | 否 | 关卡表按关覆盖（`StageController.cs:105-119` 已经在做这件事） |
| G4 | 波次规模 4–15 | `SpawnPlanner.cs:17-18`（`const`）**与** `EncounterController.cs:56-57`（`[Export]`）**两份** | **是**（第一份） | 关卡表（删掉 const 那份） |
| G5 | 节点价值层 / 波数 | `NodePlacement.cs:18-26` **与** `StageController.cs:653-661`（**两处实现同一逻辑**） | **是** | **节点表** `Layer` / `WaveCount` |
| G6 | 节点类型 → 池过滤 / 标题 | `RewardService.cs:16-38` | **是** | **节点表** `RewardKind` / `DisplayName` |
| G7 | 节点配色 | `RewardNode.cs:46,105-108` + `Assets/Materials/mat_node_*.tres`（9 个） | 否（走 assets.md 规范） | 节点表 `MaterialPath` |
| G8 | 节点交互参数 3 项 | `Interactable.cs:37-51`、`RewardNode.cs:49-62` | 否 | 保留 / 节点表 |
| G9 | 节点避障启发式（螺旋 0.7 / 1.15 / 球半径 1.3 / 抬升 1.65 / 射线 +2−10） | `NodePlacement.cs:43-69` | **是** | 地图表（可选） |
| G10 | 降临动画 4 项（12 / 0.7 / 0.35 / 0.18） | `StageController.cs:35-38` | **是** | 表现表（可选） |
| G11 | 地图 / 导航烘焙 7 项 | `MapManager.cs:21-33` | 否 | 地图表（可选） |
| G12 | 点位供给启发式（1.2 / 1.5 / 0.3 / 2.2-3.4 / 0.25、区域杀 `-30`） | `MapManager.cs:143,170,191-217` | **是** | 地图表（可选） |
| G13 | 区域杀高度 | `ConfigIndex.tres:124` `KillPlaneY = -8` | 否 | 保留在 ConfigIndex（`MapManager.cs:143` 读它） |

#### H. 状态 / Boss / 局外成长

| # | 现有配置项 | 当前存放位置 | 是否硬编码 | 建议归属哪张表 |
|---|---|---|---|---|
| H1 | 状态预设 5 条（Slow 0.55 / Weaken 0.75 / Burn 6dps / Empowered 1.3·1.15 / Stun 0.12·0.5） | `StatusEffect.cs:26-84`（switch） | **是**（6 处数值） | **状态表** `Config/Tables/Status/sts_*.tres` |
| H2 | Boss 词条 7 条（狂暴 1.25·1.2 / 铁壁 1.4·150 / 疾行 1.35 …） | `BossPerk.cs:33-117` | **是**（4 处十进制 + 1 处整数） | **Boss 词条表** |
| H3 | Boss 阶段 6 项 | `Scenes/Enemies/Boss.tscn`（`PhaseFractions` / `PhaseDamageMultipliers` / `PhaseAttackSpeedMultipliers`）+ `BossPhaseController.cs:13-20` | 否 | Boss 表（可选） |
| H4 | Boss 压力 9 项 | `Scenes/Enemies/Boss.tscn` + `BossPressure.cs:13-22`；与 `ConfigIndex.tres:107-109` **两处重复**（3 个字段） | 否，但**双份真源** | Boss 表（或保留，删掉 ConfigIndex 那一份） |
| H5 | 局外成长 5 项效果 + 1 个价格公式 | `MetaProgress.cs:27-47`（`*10f` / `0.05f` / `0.03f` / `0.03f` / `0.06f` / `CostOf = 10*(level+1)`） | **是** | **局外成长表** |
| H6 | 局外成长界面文案 5 条 | `BaseScreen.cs:70-74` | **是** | 局外成长表 `DescriptionTemplate` |
| H7 | 碎片结算 3 项（10 / 3 / 50） | `RunSettlement.cs:10-16`（`const`） | **是** | 局外成长表（或 ConfigIndex） |
| H8 | 精英修正 / 挑战者标记 / 伤害跳字 / 界面动画 | `EliteModifier.cs`、`ChallengerMark.cs:17-28`、`Interactable.cs:26-27`、`OverlayScreen.cs:36-55`、`WorldOverlay.cs:133`、`PlayerHud.cs:23` | 大部分否 | 表现表（低优先） |
| H9 | 减伤上限 0.9 / 意图阈值 0.04 | `Unit.cs:173-175`、`Ability.cs:141` | **是** | 规则常量，**建议保留**（不是策划可调项） |
| H10 | 镜头 9 项 | `CameraRig.cs:13-34` | 否 | 设置表（跟 [config.md](config.md):155 的设置界面一起做） |

### 1.3 硬编码清单（"应该外置到表里"的候选）

计数规则：**非注释、非 `[Export]` 行上的十进制字面量 + `const` 数值常量**；剔除 `Scripts/Testing/`、`Scripts/UI/`、纯表现几何、以及 clamp/epsilon/形状数学这类保护值。

**A 类 —— 目录式硬编码（40 处，最该整表外置）**

| 文件 | 处数 | 具体位置 | 归属 |
|---|---|---|---|
| `Rewards/RewardCatalog.cs` | 11 | L20,22,24,28,30,32,36,49,50,59,67（另有一批整数也是玩法值：L26 `25`、L33 `3`、L38/L42/L46 `1`、L44 `2`、L61 `14`、L62 `7`、L63 `3`、L71 `3`、L79 `2`） | 增幅项表 |
| `Combat/StatusEffect.cs` | 6 | L43,52,67,68,77,78（+ L59 `DamagePerSecond = 6f`） | 状态表 |
| `Boss/BossPerk.cs` | 4 | L42,43,53,62（+ L54 `150f`）（L132,139 的 `0.01f` 是权重下限保护，归 C 类） | Boss 词条表 |
| `Run/MetaProgress.cs` | 4 | L29,31,34,37（+ L27 `*10f`、L47 `10*(level+1)`） | 局外成长表 |
| `Rewards/RunSettlement.cs` | 3 | L10 `10`、L13 `3`、L16 `50` | 局外成长表 |
| `Encounter/SpawnPlanner.cs` | 2 | L17 `4`、L18 `15` | 关卡表 |
| `Rewards/RunController.cs` | 10 | ~~`DefaultStages()` L303,304,305,307,314,315,316,317,321,322（整段 L286-325 共 ≈30 个值）~~ | ✅ **已删除（2026-10）**（改用 `StageDefinition` 默认值） |

**B 类 —— 流程/几何启发式（25 处，可外置但不是必须）**

`World/MapManager.cs:170,191,196,202,211,217`（7）、`Run/NodePlacement.cs:57,58,61,66,67`（5）、`Encounter/EncounterController.cs:163,286,561,647,684`（5）、`AI/Behavior.cs:70,78,89,96`（4）、`Run/StageController.cs:35-38`（4）。

**C 类 —— 不该外置（保留）**：`Combat/CombatTypes.cs:25-28,58`（碰撞层位、`MaxDepth`）、`Weapons/Weapon.cs:89,91,170,232`（clamp 保护）、`Combat/AttackHitbox.cs:70-73,95`（形状数学）、`Interaction/Interactable.cs:16-27`（节点名常量）、`Scripts/Testing/SelfTest.cs`（48 处，测试代码）。

### 1.4 盘点发现的 7 个既有问题（都是"分表"会顺手修掉的）

> ✅ **2026-10 状态**：① 已修（阶段 1）；② 未修（波次规模两份，`SpawnPlanner` 的 const 与 `EncounterController` 的导出值）；
> ③ 未修（`Pistol.tscn` 的数值仍是回退基线，表是权威）；④ 已修（阶段 2：节点表替掉了重复 switch）；
> ⑤ ✅ **已修（阶段 4）**：`RewardConfig.Weight` 19 条仍全是 1.0（故意不改分布），但
> `PoolTags` **已删除**、`ConflictsWith` **已接上**（有了 `RewardConfig.ConflictsWith` 列与 `Compile` 里的填充）；
> ⑥ ✅ **已修（阶段 4）**：改成用户要的**正向指**（武器 → 升级池表），三级回退零回归，见 §4.5.0；
> ⑦ 未做（节点驱动见 §5 阶段 3 的第 3 步）。
> **阶段 3 新修掉的问题**（本文档 §1.4 之外、由 [config-tables-inventory.md](config-tables-inventory.md) §5 盘出来的）
> 见 §5 阶段 3 的"顺手修掉的既有问题"表：2 个 ConfigIndex 死字段 · `IsFinalBoss` · 效果表 4 列镜像 ·
> `SpawnDisplayName` · 敌人倍率静默换挡 · `BarEnemy` 居中。

**① 关卡数值有两个真源，而且已经漂移。** ✅ **已修（2026-10，配置表阶段 1：删掉 `DefaultStages()` + 拆表，见 §5）**
`Config/ConfigIndex.tres:6-58` 与 `RunController.cs:286-325` 的 `DefaultStages()` 是同一份数据的两个副本：

| 字段 | `ConfigIndex.tres` | `DefaultStages()` | 结果 |
|---|---|---|---|
| Stage1 `BossPerkCount` | `1`（L19） | 未写 → 代码默认 `2`（`StageDefinition.cs:37`） | 兜底路径 Boss 多一个词条 |
| Stage2 `AllowedEnemies` | 6 种（L33） | 未写 → 默认空数组 = **不限制**（`StageDefinition.cs:43`） | 兜底路径第 2 关会放出本不该出现的敌人（守卫 / 尖啸者 / 冲锋兵） |
| Stage2 `EnemyDamageMultiplier` | `1.1`（L31） | 未写 → 默认 `1f` | 兜底路径敌人伤害低 10% |
| Stage2 `BossDamageMultiplier` | `1.1`（L39） | 未写 → 默认 `1f` | 兜底路径 Boss 伤害低 10% |
| Stage3 `BossPerkCount` | `3`（L55） | 未写 → 默认 `2` | 兜底路径最终 Boss 少一个词条 |
| Stage3 `BossGuardCount` | `3`（L54） | `3` | 一致（对照项） |

`RunController.cs:429` 是 `ConfigIndex.Settings.Stages.Length > 0 ? ConfigIndex.Settings.Stages : DefaultStages()` —— 一旦有人把 `ConfigIndex.tres` 的 `Stages` 清空（`ConfigIndexData.cs:111` 的默认值就是空数组），游戏会**静默**换上一套不同的关卡数值。分表（T6）会删掉这个兜底函数，改为"缺文件 → 用 `StageDefinition` 自身默认值"，这正是用户要的回退语义。
✅ **已按此落地（2026-10）**：`DefaultStages()` 删除、3 关独立成 `Config/Tables/Stages/stg_*.tres`、清空数组时实测走"默认值"分支（见 §5 阶段 1）。

**② 波次规模有两个来源。** `SpawnPlanner.cs:17-18` 的 `MinWaveSize = 4 / MaxWaveSize = 15` 与 `EncounterController.cs:56-57` 的 `[Export] WaveMinEnemies = 4 / WaveMaxEnemies = 15` 是同一个数字写了两遍（前者作为 `RollWaveSize` 的默认参数）。

**③ 武器数值有两个来源，且不一致。** `Scenes/Weapons/Pistol.tscn:16-25` 与 `ConfigIndex.tres:74-84` 是同一把枪的两套值：

| 字段 | `Pistol.tscn` | `ConfigIndex.tres` | 谁生效 |
|---|---|---|---|
| `Damage` | 22（L16） | 24（L74） | ConfigIndex（`EquipWeapon` 覆盖） |
| `FireInterval` | 0.32（L17） | 0.26（L75） | ConfigIndex |
| `MagazineSize` | 8（L18） | 12（L76） | ConfigIndex |
| `ReloadTime` | 1.15（L19） | 0.7（L77） | ConfigIndex |
| `ProjectileSpeed` | 75（L20） | 90（L83） | ConfigIndex |
| `MaxLifetime` | 2.2（L21） | 无对应字段 | 保持 2.2 |
| `SpreadDegrees` | 0.6（L22） | 0.5（L79） | ConfigIndex |
| `Knockback` | 2.5（L23） | 2.5（L80） | 一致 |
| `CritChance` | 0.15（L24） | 0.15（L81） | 一致 |
| `AimFov` | 55（L25） | 55（L84） | 一致 |

分表时必须明确"谁是权威"（拍板点 §6.3），否则会继续踩。

**④ 节点价值层与波数在两处重复实现。** `NodePlacement.cs:18-26` 与 `StageController.cs:653-661` 是同一段 `switch`（`Evolution|Mutation → 2`、`Enhancement|Shop → 1`、`_ → 0`；`WavesFor` = `2→3 / 1→2 / _→1`）。改关卡规则要改两处 —— 归入节点表（T7）后只剩表里一列。

**⑤ 三张表的 `Weight` / `ConflictsWith` / `PoolTags` 是死配置。** ✅ **阶段 4 收尾：`PoolTags` 已删、`ConflictsWith` 已接上**（`Weight` 仍故意不动）

| 字段 | 定义处 | 赋值处 | **读取处** | 结论 |
|---|---|---|---|---|
| `RewardDefinition.Weight` | `RewardDefinition.cs:46` | 19 条**全没填**（`RewardCatalog.cs:16-86`） | `RewardPool.cs:89,96`（读了，但值恒为 `1f`） | 加权抽取退化成等概率（**故意不改**，改了抽样分布会变） |
| `RewardDefinition.ConflictsWith` | `RewardDefinition.cs:44` | ~~从未被赋值~~ → ✅ 阶段 4 起由 `RewardConfig.ConflictsWith` 填 | `RewardPool.cs:67-75` | ✅ **已接上**（默认空 = 行为不变） |
| `RewardDefinition.PoolTags` | ~~`RewardDefinition.cs:38`~~ | ~~`RewardCatalog.cs:98,113,126` + 19 行 `.tres`~~ | **从无** | 🗑 **阶段 4 已删除**（连同 19 行 `.tres` 里的那一行）；语义由 `RewardConfig.Pool` + `RequiredWeaponTags` 取代 |
| `BossPerk.Weight` | `BossPerk.cs:15` | 7 条**全没填**（`BossPerk.cs:33-117`） | `BossPerk.cs:132,139` | 同上，等概率 |

分表时把它们接上（`Weight` 接进公共的 `WeightedPick`，见 §3 T9）或**明示删掉** —— 留着不生效的字段比没有更糟。

**⑥ 没有"某把武器 → 它的升级池"这个关联。** ✅ **阶段 4 已修**（用户拍板的**正向指**方案，见 §4.5.0 与 §5 阶段 4）：
新增升级池表 `Config/Tables/UpgradePools/pool_*.tres`，`WeaponConfig.Pool` → 池行 ← `RewardConfig.Pool`（一个选项占一行 + 一个引用列），
`RewardDefinition.AllowsWeapon` 变成三级回退（专属池 → 武器标签 → 通用）。
**原文保留如下（当时的现状）**：`RewardPool.Initialize` 只按 `RequiredWeaponTags` 与 `Weapon.AttackTags` 匹配标签（`RewardPool.cs:18-28` → `RewardDefinition.cs:59-70` → `Weapon.cs:93`），而 4 条进化全部只声明 `"Gun"`（`RewardCatalog.cs:104-116`）、三把武器共用同一个 `Pistol.tscn`（`RunController.cs:383-386` 只改数值不换节点），所以**三把枪共用一个池**。
> ⚠️ 这条**部分仍成立**：三把枪的**标签层②与通用层③**依然相同（4 条进化仍归三把枪共有）；
> 变的是现在**多了一层"专属池"**可以用 —— 19 条现有选项一条都没改，池这一层目前是空的（留白）。

**⑦ "奖励节点 → 增强首领"只实现了三分之一。** ✅ **阶段 6 已补齐**（"确定性的压力点数累积" + "按权重抽取" + "强化参数可配" 三件都在，节点表三列全部接上；见 §5 阶段 6）。
**原文保留如下（当时的现状）**：有"确定性的压力点数累积"（`StageController.AddPressureForNode`，`StageController.cs:334-359`），缺"按权重抽取"、缺"强化参数可配"，而且压力参数在 `ConfigIndex.tres:107-109` 与 `Scenes/Enemies/Boss.tscn` 上**有两份**（前者覆盖后者，`EncounterController.cs:665-667`）。完整分析见 **§3 T9** 的缺口表。

---

## 2. 设计总览

### 2.1 与三条既有约定的关系（不新增全局入口）

| 既有约定（出处） | 本设计如何遵守 |
|---|---|
| **数值集中、`ConfigIndex` 是数值唯一入口**（[todo-handoff.md](todo-handoff.md):52,83；`ConfigIndexData.cs:29`） | 分表**不新增 autoload**。所有表由 `ConfigIndex` 在 `_Ready` 里建立索引并挂到 `ConfigIndexData` 上（新增 `[Export] WeaponConfig[] Weapons` 等 9 个数组字段，见 §2.3 / §3），业务代码继续写 `ConfigIndex.Settings.Xxx`。`ConfigIndex` 从"唯一数值**文件**"变成"唯一数值**门面**"——这是 [architecture.md](architecture.md):134 "加新全局入口前先问：能不能传参" 的同一精神 |
| **`GameplayEventBus` 事件驱动**（[architecture.md](architecture.md):12,23） | 表只提供**数据**，不参与运行时事件。表里声明的机制（增幅项 → `TriggerRule`）仍然由 `RunBuild.Add` 挂到玩家身上、仍旧订阅 `GameplayEventBus`，链路不变 |
| **`MapManager` / `UiService` / `PlayerLocator` 是统一入口**（[todo-handoff.md](todo-handoff.md):79-83） | 表不碰点位/界面/玩家引用。敌人表的生成项仍交给 `SpawnPlanner` → `MapManager`；节点表的标题仍由 `UiService` 开启的界面显示 |

### 2.2 关键决策：磁盘行 `Resource` ↔ 运行期视图（**这是让迁移不扩散的关键**）

不要直接把现有的运行期类改成 `Resource`，而是**加一层"磁盘行"**：

```
Config/Tables/Upgrades/upg_dmg_up.tres   磁盘：RewardConfig : Resource（一行一个文件，Inspector / 插件可编辑）
        │  RewardTable.Definitions()      （读表 → 编译，一次）
        ▼
RewardDefinition（现有类，一字不改）       运行期：带 Action<RewardContext> Apply 委托
        │
        ▼
RewardPool / RunBuild / RunController / RewardScreen   消费方：一行都不用改
```

索引在 `ConfigIndex.tres` 上：`[Export] RewardConfig[] Upgrades`，元素指向上面那些行文件（§6.2）。

**为什么必须这样分两层**：现在的 `RewardDefinition.Apply` 是 `Action<RewardContext>`（`RewardDefinition.cs:57`）——**委托不可能序列化进 `.tres`**。`StatusEffect` 是普通 `sealed class`、`BossPerk` 也是（`BossPerk.cs:10`）——都不是 `Resource`，无法直接存成资源。若强行把它们改成 `Resource`，`RewardPool` / `RunBuild` / `RunController` / `BossPerkCatalog.Roll` / `StatusPresets.Create` 全要跟着改，迁移风险成倍上升。

同理适用于：`RewardConfig : Resource` → `RewardDefinition`、`StatusConfig : Resource` → `StatusEffect`、`BossPerkConfig : Resource` → `BossPerk`。而武器/角色/敌人/关卡/节点这几张表**本来就没有委托**，可以直接用 `Resource`（`StageDefinition` 已经是 `Resource` 了，见 `StageDefinition.cs:10`）。

### 2.3 目录与命名（**一行 = 一个 `.tres` 文件**）

按 [assets.md](assets.md) §1 的精神（**按资源类型分目录、不留空目录、用到才建**）+ §2.5 的理由（小写从根上避免 Windows/Linux 大小写差异）+ [config-tooling.md](config-tooling.md) 的硬约束（**一行一个资源文件、一个文件夹一张表**，插件的分组单位就是文件夹）：

```
Config/
  ConfigIndex.tres                    ← 既有，不动（唯一数值门面）。新增 9 个 [Export] XxxConfig[] 数组当索引
  Tables/                         ← 所有分表（与既有 ConfigIndex.tres 分开，避免混在一起）
    Weapons/                      ← 武器表：一行一个文件
      wpn_pistol.tres
      wpn_sniper.tres
      wpn_rifle.tres
    Skills/
      skl_dash.tres  skl_burst.tres  skl_melee.tres  skl_summon.tres  skl_control.tres
    Upgrades/                     ← 增幅项表（用户重点）
      upg_dmg_up.tres  upg_rate_up.tres  … 共 19 个
    UpgradePools/                 ← ✅ 阶段 4：升级池表（一行一个池）
      pool_pistol.tres  pool_sniper.tres  pool_rifle.tres
    Characters/
      chr_gunner.tres  chr_heavy.tres  chr_summoner.tres
    Enemies/
      enemy_chaser.tres  enemy_fast_melee.tres  … 共 8–10 个
    Stages/
      stg_outer.tres  stg_inner.tres  stg_core.tres
    Nodes/
      node_enhance.tres  node_evolution.tres  … 共 9 个
    Status/
      sts_slow.tres  sts_weaken.tres  sts_burn.tres  sts_empowered.tres  sts_stun.tres
    Perks/
      perk_berserk.tres  … 共 7 个
    Meta/
      meta_health.tres  meta_damage.tres  … 共 5 个
```

**为什么没有"表文件"**：编辑时"文件夹就是表"（插件按文件夹聚合），运行时由 `ConfigIndex.tres` 上的数组当索引 —— 所以**不需要 `weapon_table.tres` 这种中间文件**，少一层间接、少一个能出错的环节。这正是 [config-tooling.md](config-tooling.md) §五"没有任何中间产物，因此没有任何中间产物能出错"的同一条理由。

> ⚠️ **不要把索引数组塞进 `Tables/` 里**：`Tables/` 根目录下如果放了 9 个索引文件，插件打开 `Tables/` 时会把它们当成一张"混合表"。索引在 `ConfigIndex.tres` 上，`Tables/<分组>/` 只放同类的行。

**命名规则**（与 [assets.md](assets.md) §2 的 `<类别>_<分组>_<变体>.tres` 同构，只是类别前缀换成分组前缀）：

| 对象 | 规则 | 例 |
|---|---|---|
| 分组目录 | PascalCase 复数（对齐 `Assets/Materials/`） | `Config/Tables/Weapons/` |
| 行文件 | `<分组前缀>_<语义名>.tres`，全小写 `snake_case`；**文件名与行里的 `Id` 取同一个语义名**，便于人肉对照 | `wpn_pistol.tres` / `upg_dmg_up.tres` / `enemy_chaser.tres` |
| 前缀词表 | `wpn_` 武器 · `skl_` 技能 · `upg_` 增幅项 · `chr_` 角色 · `enemy_` 敌人 · `stg_` 关卡 · `node_` 节点 · `sts_` 状态 · `perk_` Boss 词条 · `meta_` 局外成长 · **`pool_` 升级池（阶段 4）** | — |
| 行 `Id` 字段 | `<分组前缀>_<语义名>`，**一次定好不再改**（会被存档 / 别的表 / `AllowedEnemyIds` 引用）；**可以**与文件名相同以降低认知负担 | `wpn_pistol`、`enemy_chaser`、`upg_dmg_up` |

> **为什么不给行编号（`武器1` / `武器2`）当文件名**：用户说的"武器1/武器2"是**槽位**（`Slot` 字段），槽位会随版本调整（新武器插到中间），文件名和 `Id` 一旦定了就不该跟着动。文件名和 `Id` 用**语义名**，槽位用 `Slot` —— 两者分工见 §4.1。
>
> `Config/ConfigIndex.tres` 是 PascalCase，属历史遗留，**不动**（改名会牵动 `ConfigIndex.cs:12` 的 `ConfigPath` 常量和所有文档）。
> `Config/` 的命名约定在 [assets.md](assets.md) 里没有覆盖（该文自称"资源目录与命名的唯一约定"，但只管 `Assets/`）—— 建议把本节并入 `assets.md`（拍板点 §6.7）。

---

## 3. 分表设计

> 每张表给出：**对应 C# 类 / 字段清单 / `[Export]` 写法 / 目录 / 样板 `.tres`**。
> 所有行类都遵守 [config-tooling.md](config-tooling.md) §五"落地步骤 · 第 0 步"的三条硬性约定：**① 每个类都写 `[GlobalClass]`；② 每个 `[Export]` 都保留 C# 属性初始化器当默认值；③ 一个文件夹 = 一张表。**
> 手写 `.tres` 的格式规矩照 [assets.md](assets.md) §5：`[ext_resource]`/`[sub_resource]` 必须在 `[node]` 之前、`load_steps = ext + sub + 1`、`type` 用基类型、只写 `path=` 不写 `uid=`、UTF-8 无 BOM + LF。
> 下面所有样板都**照抄 `Config/ConfigIndex.tres` 已验证的写法**（`[gd_resource type="Resource" script_class="..." ...]` + `script = ExtResource("...")`）。

### T1 武器表 —— `WeaponConfig : Resource`（一行一个文件）

**目录**：`Config/Tables/Weapons/wpn_*.tres`（每个文件一行）；索引用 `ConfigIndex.tres` 上的 `[Export] WeaponConfig[] Weapons`
**替换**：`WeaponPresetKind` 分支（`RunController.cs:396-412` 的 11 行三元链）+ 手枪/狙击/步枪的 25 个 `ConfigIndex*` 字段（`ConfigIndex.tres:74-98`）

| 字段 | 类型 | 默认值 | 用途 |
|---|---|---|---|
| `Id` | `string` | `"weapon_1"` | 稳定键。存档、别的表引用都用它 |
| `Slot` | `int` | `1` | **槽位号**（用户说的"武器1/武器2"）。1 起；0 = 不进玩家槽位（敌人用武器）。UI 顺序与快捷键 1/2/3 读它 |
| `DisplayName` | `string` | `"新武器"` | 界面标题 / 卡片名 |
| `DescriptionTemplate` | `string` | `""` | 支持 `{Key}` 占位符（§4.4）。`StartScreen.cs:46-48` 的卡片文案搬到这里 |
| `IconPath` | `string` | `""` | `res://Assets/UI/...`；空 = 无图标 |
| `ModelPath` | `string` | `""` | 手上模型 `res://Assets/Models/...`；空 = 用预制体自带 |
| `ScenePath` | `string` | `"res://Scenes/Weapons/Pistol.tscn"` | 武器预制体。**换武器不再重建节点**（`RunController.cs:383-386` 的既有约定），只在需要换模型/投射物时才换预制体 |
| `ProjectileScene` | `PackedScene?` | `null` | 投射物预制体 |
| `AttackTags` | `string[]` | `["Projectile","Gun","Physical"]` | 与 `RewardPool` 的 `RequiredWeaponTags` 配对 |
| ✅ **`Pool`** | `UpgradePoolConfig?` | `null` | **阶段 4 新增（场景 B 的核心字段）**：本武器**正向指向**它的升级池（`Config/Tables/UpgradePools/pool_*.tres`）。`ApplyTo` 时写进 `Weapon.PoolId`，供奖励池做"专属池"匹配。留空 = 只走"标签层 / 通用层"（= 分表前的行为） |
| `Damage` | `float` | `24f` | 单发伤害 |
| `FireInterval` | `float` | `0.26f` | 射击间隔 |
| `MagazineSize` | `int` | `12` | 弹匣 |
| `ReloadTime` | `float` | `0.7f` | 装填秒数 |
| `AutoReloadDelay` | `float` | `3f` | 被动装填延迟 |
| `SpreadDegrees` | `float` | `0.5f` | 腰射散布 |
| `AimSpreadScale` | `float` | `0.35f` | 预瞄时的散布缩放 |
| `MultiShotSpreadDegrees` | `float` | `1.2f` | 多弹丸之间的横向散开 |
| `ProjectilesPerShot` | `int` | `1` | 一次击发弹丸数 |
| `BasePierce` | `int` | `0` | 基础穿透 |
| `Knockback` | `float` | `2.5f` | 击退 |
| `CritChance` | `float` | `0.15f` | 暴击率 |
| `CritMultiplier` | `float` | `2f` | 暴击倍率 |
| `ProjectileSpeed` | `float` | `90f` | 弹速 |
| `MaxLifetime` | `float` | `2.2f` | 投射物存活 |
| `AimFov` | `float` | `55f` | 预瞄 FOV |
| `Automatic` | `bool` | `false` | 按住连发（决定火力倾泻走哪种模式，见 [config.md](config.md):61-68） |
| `Params` | `ParamEntry[]` | `[]` | 以后新武器的新参数（§4.3） |
| `Note` | `string` | `""` | 策划备注 |

**留在 `Weapon.cs` 上、不进表**：`Muzzle`（节点引用，表里给不了）、`Enabled`（运行期由角色表决定，见 E3）、**`ConfigId`**（新增：由 `ApplyTo` 写入的"我对应表里哪一行"，是 §4.5 武器↔升级池关联的必要一环）。

```csharp
// Scripts/Config/WeaponConfig.cs
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 武器表的一行 —— **一个 .tres 文件就是一行**（放在 Config/Tables/Weapons/）。
/// 由 WeaponRegistry / 调用方 ApplyTo() 套到 Weapon 组件上；读不到就是"什么都不做"，
/// 组件保留自己的 [Export] 默认值（这就是"读不到则使用自身的默认值，有则读取"）。
/// </summary>
[GlobalClass]
public partial class WeaponConfig : Resource, IConfigRow
{
    [ExportGroup("身份")]
    [Export] public string Id { get; set; } = "wpn_pistol";
    /// <summary>槽位号（武器1 / 武器2 …）。0 = 不进玩家槽位（敌人用武器）。</summary>
    [Export] public int Slot { get; set; } = 1;
    [Export] public string DisplayName { get; set; } = "新武器";
    [Export] public string DescriptionTemplate { get; set; } = string.Empty;
    [Export] public string IconPath { get; set; } = string.Empty;
    [Export] public string ModelPath { get; set; } = string.Empty;
    [Export] public string ScenePath { get; set; } = "res://Scenes/Weapons/Pistol.tscn";

    [ExportGroup("攻击")]
    [Export] public PackedScene? ProjectileScene { get; set; }
    [Export] public string[] AttackTags { get; set; } = { "Projectile", "Gun", "Physical" };

    [ExportGroup("数值（这里的值就是权威，见拍板点 §6.3）")]
    [Export] public float Damage { get; set; } = 24f;
    [Export] public float FireInterval { get; set; } = 0.26f;
    [Export] public int MagazineSize { get; set; } = 12;
    [Export] public float ReloadTime { get; set; } = 0.7f;
    [Export] public float AutoReloadDelay { get; set; } = 3f;
    [Export(PropertyHint.Range, "0,15,0.1")] public float SpreadDegrees { get; set; } = 0.5f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float AimSpreadScale { get; set; } = 0.35f;
    [Export] public float MultiShotSpreadDegrees { get; set; } = 1.2f;
    [Export] public int ProjectilesPerShot { get; set; } = 1;
    [Export] public int BasePierce { get; set; }
    [Export] public float Knockback { get; set; } = 2.5f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float CritChance { get; set; } = 0.15f;
    [Export] public float CritMultiplier { get; set; } = 2f;
    [Export] public float ProjectileSpeed { get; set; } = 90f;
    [Export] public float MaxLifetime { get; set; } = 2.2f;
    [Export] public float AimFov { get; set; } = 55f;
    [Export] public bool Automatic { get; set; }

    [ExportGroup("扩展")]
    [Export] public ParamEntry[] Params { get; set; } = System.Array.Empty<ParamEntry>();
    [Export(PropertyHint.MultilineText)] public string Note { get; set; } = string.Empty;

    /// <summary>套用到武器组件。读不到表时根本不会调用它 —— 组件保留自己的 [Export] 默认值。</summary>
    public void ApplyTo(Weapons.Weapon weapon)
    {
        weapon.Damage = Damage;
        weapon.FireInterval = FireInterval;
        // …其余字段逐项赋值
        weapon.RefillMagazine();
    }
}
```

```csharp
// Scripts/Config/WeaponRegistry.cs
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 武器表的运行期索引：把 ConfigIndex.Settings.Weapons 里的数组建成按 Id / Slot 查的字典。
/// 表本身没有"表文件" —— 磁盘上是一堆行文件，索引用的是 ConfigIndex.tres 上的 [Export] 数组。
/// </summary>
public static class WeaponRegistry
{
    private static readonly System.Collections.Generic.Dictionary<string, WeaponConfig> ByIdMap = new();
    private static readonly System.Collections.Generic.Dictionary<int, WeaponConfig> BySlotMap = new();
    private static WeaponConfig[] _rows = System.Array.Empty<WeaponConfig>();

    /// <summary>ConfigIndex._Ready 里调一次（配置热重载时再调）。</summary>
    public static void Rebuild(WeaponConfig[]? rows)
    {
        ByIdMap.Clear();
        BySlotMap.Clear();
        _rows = rows ?? System.Array.Empty<WeaponConfig>();

        foreach (WeaponConfig row in _rows)
        {
            if (string.IsNullOrEmpty(row.Id))
            {
                GD.PushWarning($"[WeaponRegistry] {row.ResourcePath} 没有填 Id，已跳过。");
                continue;
            }
            ByIdMap[row.Id] = row;
            if (row.Slot > 0) { BySlotMap[row.Slot] = row; }
        }
    }

    /// <summary>按 Id 取行。读不到返回 null → 调用方回退到组件自身默认值。</summary>
    public static WeaponConfig? Find(string id)
        => !string.IsNullOrEmpty(id) && ByIdMap.TryGetValue(id, out WeaponConfig? row) ? row : null;

    /// <summary>按槽位号取（1 起）。用户说的"武器1 / 武器2"就是这里。</summary>
    public static WeaponConfig? BySlot(int slot)
        => BySlotMap.TryGetValue(slot, out WeaponConfig? row) ? row : null;

    public static int Count => _rows.Length;
}
```

> **为什么是 "一个 `XxxConfig` 行类 + 一个 `XxxRegistry` 静态索引" 而不是 "一个 `XxxTable : Resource` 表类"**：因为 [config-tooling.md](config-tooling.md) 的插件把**文件夹**当表，磁盘上不需要"表文件"。少一个 `XxxTable` 类 = 少一处 `TryLoad`、少一个 `_compiled` 缓存、少一层间接。
> **9 张表要不要写 9 个 `XxxRegistry`**：可以用 §4.1 的 `IConfigRow` + `ConfigTable.ById<T>()` 把 9 份几乎相同的代码压成一份泛型实现。本节的 `WeaponRegistry` 是"想少一层泛型时"的直白版本 —— 两种都行，**别混用**。

**样板 `.tres`**：**每行一个文件**，文件名与 `Id` 取同一个语义名。

`Config/Tables/Weapons/wpn_pistol.tres`（`load_steps = 1 ext + 0 sub + 1 = 2`）：

```ini
[gd_resource type="Resource" script_class="WeaponConfig" load_steps=2 format=3]

[ext_resource type="Script" path="res://Scripts/Config/WeaponConfig.cs" id="1_row"]

[resource]
script = ExtResource("1_row")
Id = "wpn_pistol"
Slot = 1
DisplayName = "手枪"
DescriptionTemplate = "单发高伤、精准，适合中远距离点射（24 伤害 / 12 发弹匣）"
IconPath = "res://Assets/UI/tex_ui_icon_wpn_pistol.png"
ScenePath = "res://Scenes/Weapons/Pistol.tscn"
Damage = 24.0
FireInterval = 0.26
MagazineSize = 12
ReloadTime = 0.7
AttackTags = PackedStringArray("Projectile", "Gun", "Physical")
```

`Config/Tables/Weapons/wpn_sniper.tres`：

```ini
[gd_resource type="Resource" script_class="WeaponConfig" load_steps=2 format=3]

[ext_resource type="Script" path="res://Scripts/Config/WeaponConfig.cs" id="1_row"]

[resource]
script = ExtResource("1_row")
Id = "wpn_sniper"
Slot = 2
DisplayName = "激光狙击枪"
Damage = 60.0
FireInterval = 0.95
MagazineSize = 5
ReloadTime = 1.4
BasePierce = 2
AttackTags = PackedStringArray("Projectile", "Gun", "Physical")
```

`Config/ConfigIndex.tres` 上的**索引**（这一行**不要手写**，在编辑器里往数组里拖 3 个行文件即可，Godot 会写对格式）：

```ini
[ext_resource type="Script" path="res://Scripts/Config/WeaponConfig.cs" id="10_weapon"]
[ext_resource type="Resource" path="res://Config/Tables/Weapons/wpn_pistol.tres" id="11_pistol"]
[ext_resource type="Resource" path="res://Config/Tables/Weapons/wpn_sniper.tres" id="12_sniper"]
[ext_resource type="Resource" path="res://Config/Tables/Weapons/wpn_rifle.tres" id="13_rifle"]

[resource]
script = ExtResource("1_balance")
…
Weapons = Array[Resource]([ExtResource("11_pistol"), ExtResource("12_sniper"), ExtResource("13_rifle")])
```

> **为什么每行一个文件，而不是 `[sub_resource]` 内嵌**：① [config-tooling.md](config-tooling.md) 推荐的 `Edit Resources as Table` 插件**按文件夹分组、一行一个资源文件**，内嵌的行它看不见（虽然它能把数组列"单独打开成另一张表"，但那多一跳、且行仍然不是独立文件）；② 行独立成文件后 `git diff` 一次只动一个文件，`git blame` 能追到"谁把 `wpn_pistol` 的伤害从 24 改成 30"。
> **为什么索引用 `ConfigIndex.tres` 上的数组而不是扫目录**：扫目录（`DirAccess.GetFilesAt("res://Config/Tables/Weapons")`）看着更省事，但① 导出到发布版时"只被目录扫描引用、没有任何 `ExtResource` 指向"的资源有掉出打包范围的风险，② 会让"`ConfigIndex` 是唯一数值入口"这条既有约定失效（[todo-handoff.md](todo-handoff.md):83）。显式数组两边都占。
> **格式风险**：`Weapons = Array[Resource]([ExtResource("11_pistol"), …])` 这种"数组元素指向独立 `ExtResource`"的写法我**没有实测**（项目里现有的 `ConfigIndex.tres:114` 用的是 `SubResource`，即内嵌）。**规避办法就是上面那句：这一行交给编辑器写，不要手抄本文的示例。** 如果编辑器保存出来的格式与本文不同，以编辑器为准。
>
> `IConfigRow` 接口见 §4.1（让 9 张表共用一套 `ById/BySlot`）。若不想引入接口，就照 `WeaponRegistry` 这样每张表写一个 `XxxRegistry`（多约 30 行 × 9 张表）。

### T2 技能表 —— `AbilityConfig : Resource`（一行一个文件）

**目录**：`Config/Tables/Skills/skl_*.tres`；索引用 `ConfigIndex.tres` 上的 `[Export] AbilityConfig[] Skills`
**替换**：`Player.tscn:64-140` 里 5 个技能实例的参数 + `Ability.cs` 的通用项

| 字段 | 类型 | 默认值 | 用途 |
|---|---|---|---|
| `Id` | `string` | `"skl_dash"` | 稳定键（同时写进 `Ability.AbilityId`，`Ability.cs:35`） |
| `Slot` | `int` | `1` | **技能槽位**（用户说的"技能1 / 技能2"） |
| `DisplayName` / `IconPath` / `ModelPath` | `string` | `""` | 名称 / 图标 / 模型（用户明确要"名称/图标/模型/参数等都可以读取参数配置表来寻址"） |
| `ScenePath` | `string` | `""` | 技能预制体（含命中体形状）。空 = 场景里已有的节点 |
| `NodeName` | `string` | `""` | 目标节点名（如 `"AbilityMelee"`、`"AbilitySummon"`）。`Params` 就是套到它上面 |
| `ClassHint` | `string` | `""` | 期望的脚本类名（`"DashAbility"` 等），用于读表时校验，不匹配就警告 |
| `InputAction` | `string` | `""` | 按键动作名（`"dash"` / `"burst"` / `"summon"` / `"control"`）。空 = 无按键（敌人技能） |
| `Aim` | `Ability.AimSource` | `Facing` | 通用项 |
| `Cooldown` / `Windup` / `Recover` | `float` | `1f` / `0.3f` / `0.3f` | 通用项 |
| `Enabled` | `bool` | `true` | 通用项（角色表可覆盖，见 E3） |
| `ApproachMinRange` / `ApproachMaxRange` | `float` | `0` | 通用项 |
| `Params` | `ParamEntry[]` | `[]` | **子类专属参数全放这里**（`Distance`、`Duration`、`ChargeSpeed`、`TriggerRadius`、`Count`、`MaxAlive`…） |
| `Note` | `string` | `""` | 说明 |

**为什么子类专属参数走 `Params` 而不逐字段列举**：`Abilities/*.cs` 里有 **36 个** `[Export]`，分散在 10 个子类上（`DashAbility.cs:12-14`、`ChargeAttackAbility.cs:12-13`、`LeapAttackAbility.cs:12-15`、`AreaAttackAbility.cs:12-14`、`SummonAbility.cs:12-17`、`BuffAlliesAbility.cs:12-15`、`BurstFireAbility.cs:16-19`、`ProjectileAttackAbility.cs:13-17`、`MeleeAttackAbility.cs:12-14`）。穷举进技能表会让表变成"所有技能字段的并集"，每加一个技能类就要改表结构；键值对则是"多数参数项能通用的情况则使用通用项"（用户原话）的自然落地。

**套用方式（不写反射框架，用引擎自带的 `Set`）**：

```csharp
// Scripts/Config/AbilityConfig.cs
using Godot;

namespace ActRoguelike.Config;

[GlobalClass]
public partial class AbilityConfig : Resource
{
    [ExportGroup("身份 / 寻址")]
    [Export] public string Id { get; set; } = "ability_1";
    [Export] public int Slot { get; set; } = 1;
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export] public string IconPath { get; set; } = string.Empty;
    [Export] public string ModelPath { get; set; } = string.Empty;
    [Export] public string ScenePath { get; set; } = string.Empty;
    /// <summary>目标节点名（"AbilityMelee" / "AbilitySummon" …）。Params 套到它身上。</summary>
    [Export] public string NodeName { get; set; } = string.Empty;
    /// <summary>期望的脚本类名，用于读表时校验。</summary>
    [Export] public string ClassHint { get; set; } = string.Empty;
    /// <summary>按键动作名（"dash" / "burst" / "summon" / "control"）。空 = 无按键（敌人技能）。</summary>
    [Export] public string InputAction { get; set; } = string.Empty;

    [ExportGroup("通用项")]
    // 嵌套在 Ability 里的枚举可以直接导出（Ability.cs:30 就是这么写的）。
    [Export] public Abilities.Ability.AimSource Aim { get; set; } = Abilities.Ability.AimSource.Facing;
    [Export] public float Cooldown { get; set; } = 1f;
    [Export] public float Windup { get; set; } = 0.3f;
    [Export] public float Recover { get; set; } = 0.3f;
    [Export] public bool Enabled { get; set; } = true;
    [Export] public float ApproachMinRange { get; set; }
    [Export] public float ApproachMaxRange { get; set; }

    [ExportGroup("子类专属参数（键值对，Key = 属性名）")]
    [Export] public ParamEntry[] Params { get; set; } = System.Array.Empty<ParamEntry>();

    [ExportGroup("说明")]
    [Export(PropertyHint.MultilineText)] public string Note { get; set; } = string.Empty;

    /// <summary>
    /// 把通用项与 Params 套到技能节点上。
    /// Params 的 Key 直接当属性名写 —— 走引擎自带的 GodotObject.Set()，
    /// 所以键名与 Inspector 里的属性名天然一致，不需要维护映射表。
    /// </summary>
    public void ApplyTo(Abilities.Ability ability)
    {
        ability.AbilityId = Id;
        ability.Aim = Aim;
        ability.Cooldown = Cooldown;
        ability.Windup = Windup;
        ability.Recover = Recover;
        ability.Enabled = Enabled;
        ability.ApproachMinRange = ApproachMinRange;
        ability.ApproachMaxRange = ApproachMaxRange;

        foreach (ParamEntry entry in Params)
        {
            if (string.IsNullOrEmpty(entry.Key)) { continue; }

            // 属性不存在时 Get() 返回 Nil —— 用它判定，避免静默写进一个无效属性。
            if (ability.Get(entry.Key).VariantType == Variant.Type.Nil)
            {
                GD.PushWarning($"[AbilityConfig] {Id} 的参数 {entry.Key} 在 {ability.GetType().Name} 上不存在，已跳过。");
                continue;
            }

            ability.Set(entry.Key, entry.Value);
        }
    }
}
```

> `GodotObject.Set(StringName, Variant)` / `Get(StringName) → Variant` / `Variant.VariantType` / `Variant.Type.Nil` 已在 4.7.2 反射核对通过（§7）。参数**只增不改**：`Params` 里没有的字段保留预制体上的值，所以"给某个技能加一个新参数"不需要动表结构。

### T3 增幅项表 —— ★ 用户重点

**目录**：`Config/Tables/Upgrades/upg_*.tres`（一行一个文件）；索引用 `ConfigIndex.tres` 上的 `[Export] RewardConfig[] Upgrades`
**替换**：`RewardCatalog.cs` 整个 static 数组（L16-86）+ 三个工厂方法（L90-130）

**结构完全按用户原话设计**：「增幅项实际就是**包含个键值对的组**和一个**权重值**以及一个**字符串值**（做机制/增幅说明）」，同时保留"多数增幅项的基本是一个参数，也就都通用"的**通用项**。

| 字段 | 类型 | 默认值 | 对应用户原话 |
|---|---|---|---|
| `Id` | `string` | `"reward_1"` | 指向（"都有指向"） |
| `Slot` | `int` | `1` | 序号槽位（"技能也是分技能1技能2"） |
| `DisplayName` | `string` | `"新增幅"` | 名称 |
| `IconPath` | `string` | `""` | 图标 |
| `ModelPath` | `string` | `""` | 模型 |
| **`Weight`** | `float` | `1f` | **权重(概率)** |
| **`TargetStat`** | `string` | `""` | **通用项**：要改的属性键（`"DamageMultiplier"`） |
| **`Operation`** | `ModifierOperation` | `Multiply` | **通用项**：`Add` / `Multiply` / `Override` / `Cap`（`StatSheet.cs:6-12`） |
| **`Value`** | `float` | `1f` | **通用项**：那"一个参数" |
| **`Params`** | `ParamEntry[]` | `[]` | **键值对的组**（需要多个参数时） |
| **`Note`** | `string` | `""` | **字符串值（机制/增幅说明）** |
| `DescriptionTemplate` | `string` | `""` | 文本类参数项（支持 `{Value}` 等占位符，§4.4） |
| `Kind` | `RewardKind` | `Enhancement` | 强化 / 进化 / 异化 |
| **`Pool`** | `UpgradePoolConfig?` | `null` | ✅ **阶段 4 实际采用**：本选项**专属的升级池**（正向关联的"第二半"，第一半是武器行的 `Pool`）。**一个选项占一行 + 这一个引用列** = 池成员关系的唯一真源。留空 = 落到下面的标签层 / 通用层 |
| `RequiredWeaponTags` | `string[]` | `[]` | 武器**类别**限定（`Gun` / `Melee`）。`Pool` 留空时的**第二层** |
| ~~`ApplicableWeapons`~~ | — | — | ❌ **未采用**（§4.5.0 拍板改成"武器 → 池"正向指），字段从未存在 |
| ~~`PoolTags`~~ | — | — | 🗑 **阶段 4 已删除**（死字段，19 行白填了）。语义被 `Pool` + `RequiredWeaponTags` 取代 |
| `Excludes` | `string[]` | `[]` | 双向互斥 |
| ✅ `ConflictsWith` | `string[]` | `[]` | **阶段 4 已接上**：单向互斥（已拥有列表里任一 Id → 本项不进池）。以前表里没有入口所以永远为空，现在 `RewardEffectRegistry.Compile` 会填它（`RewardPool.Roll` 早就在读） |
| `MaxStacks` | `int` | `3` | 层数上限（0 = 不限） |
| `RequiresSummoner` | `bool` | `false` | 角色限定 |
| `ApplyHandler` | `string` | `"StatMultiplier"` | 效果绑定（§6.1 拍板点） |

```csharp
// Scripts/Config/RewardConfig.cs
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 增幅项（强化 / 进化 / 异化）的磁盘行。
/// 运行期由 RewardTable.ToDefinitions() 编译成现有的 Rewards.RewardDefinition，
/// 因此 RewardPool / RunBuild / RunController 不需要任何改动。
/// </summary>
[GlobalClass]
public partial class RewardConfig : Resource
{
    [ExportGroup("身份 / 寻址")]
    [Export] public string Id { get; set; } = "reward_1";
    [Export] public int Slot { get; set; } = 1;
    [Export] public string DisplayName { get; set; } = "新增幅";
    [Export] public string IconPath { get; set; } = string.Empty;
    [Export] public string ModelPath { get; set; } = string.Empty;

    [ExportGroup("权重（概率）")]
    [Export] public float Weight { get; set; } = 1f;

    [ExportGroup("通用项（多数增幅项只填这一组）")]
    /// <summary>StatSheet 的属性键，例如 DamageMultiplier。留空 = 只走 Params / Handler。</summary>
    [Export] public string TargetStat { get; set; } = string.Empty;
    [Export] public Combat.ModifierOperation Operation { get; set; } = Combat.ModifierOperation.Multiply;
    [Export] public float Value { get; set; } = 1f;

    [ExportGroup("键值对的组（需要多个参数时）")]
    [Export] public ParamEntry[] Params { get; set; } = System.Array.Empty<ParamEntry>();

    [ExportGroup("分类与池")]
    [Export] public Rewards.RewardKind Kind { get; set; } = Rewards.RewardKind.Enhancement;

    /// <summary>
    /// 这个选项**专属的升级池**（阶段 4 实际采用；池行是 Config/Tables/UpgradePools/pool_*.tres）。
    /// **一个选项占一行 + 这一个引用列** —— 池行不反过来列选项，成员关系只有一个真源。
    /// 填了 = 只有 Weapon.PoolId 等于它的武器能拿到（不看下面的标签）；
    /// 留空 = 走 RequiredWeaponTags 的类别层；两者都留空 = 通用池（强化类都是这种）。详见 §4.5.0。
    /// </summary>
    [Export] public UpgradePoolConfig? Pool { get; set; }

    /// <summary>武器**类别**标签（Gun / Melee / Launcher）。专属池留空时用它兜底。</summary>
    [Export] public string[] RequiredWeaponTags { get; set; } = System.Array.Empty<string>();

    [Export] public string[] Excludes { get; set; } = System.Array.Empty<string>();

    /// <summary>单向互斥：已拥有这些 Id 里任意一个 → 本项不进池（阶段 4 才在 Compile 里接上）。</summary>
    [Export] public string[] ConflictsWith { get; set; } = System.Array.Empty<string>();
    [Export] public int MaxStacks { get; set; } = 3;
    [Export] public bool RequiresSummoner { get; set; }

    [ExportGroup("说明（字符串值）")]
    /// <summary>机制/增幅说明。给策划看，不进玩家界面。</summary>
    [Export(PropertyHint.MultilineText)] public string Note { get; set; } = string.Empty;
    /// <summary>玩家可见描述模板，支持 {Key} 占位符。留空时由 ConfigText 自动生成。</summary>
    [Export] public string DescriptionTemplate { get; set; } = string.Empty;

    [ExportGroup("效果绑定")]
    /// <summary>处理器 id（RewardEffectRegistry 里注册的字符串）。纯数值增幅不需要新处理器。</summary>
    [Export] public string ApplyHandler { get; set; } = "StatMultiplier";

    /// <summary>按 key 找键值对（找不到返回 fallback）。Handler 里读参数统一走它。</summary>
    public float Param(string key, float fallback)
    {
        foreach (ParamEntry entry in Params)
        {
            if (entry.Key == key) { return entry.AsFloat(fallback); }
        }
        return fallback;
    }

    public string ParamText(string key, string fallback)
    {
        foreach (ParamEntry entry in Params)
        {
            if (entry.Key == key) { return entry.AsText(fallback); }
        }
        return fallback;
    }
}
```

**为什么"通用项 + 键值对组"两级并存** —— 现有 19 条的实际分布证明了这个分层是对的：

| 覆盖情况 | 条数 | 用哪一级 | 例子 |
|---|---|---|---|
| 只改一个属性 | **13**（10 强化 + 3 进化） | 只填 `TargetStat` + `Operation` + `Value` | 增伤弹头 `{DamageMultiplier, Multiply, 1.15}` |
| 一次改多个属性 | 1（`evo_blast`） | `Params` 组 + `ApplyHandler = "StatSet"` | `ExplosionRadiusAdd=3.5`、`ExplosionDamageScale=0.6` |
| 需要真机制 | **5**（全部异化） | `Params` 组 + 专属 `ApplyHandler` | 连锁闪电（4 个参数）、吸血、灼烧、亡者随从、战地补给 |

**即 14/19 条（74%）可以纯数据化，只有 5 条各需要一个 C# 处理器。** 新增一条纯数值增幅 = **加一段 `.tres`，不碰 C#**。

```csharp
// Scripts/Rewards/RewardEffectRegistry.cs
using System;
using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>
/// 「数据描述不了的效果」的处理器。数据能描述的（改 StatSheet）走 StatMultiplier / StatSet，不需要新代码。
/// 新增纯数值增幅只加 .tres；只有真新机制（新的 TriggerRule）才在这里加一行。
/// </summary>
public static class RewardEffectRegistry
{
    public delegate void Handler(RewardContext context, RewardConfig row);

    private static readonly Dictionary<string, Handler> Handlers = new()
    {
        // 通用项：一个属性 + 一个值。
        ["StatMultiplier"] = (context, row) =>
        {
            if (!string.IsNullOrEmpty(row.TargetStat))
            {
                AddStat(context, row.TargetStat, row.Operation, row.Value);
            }
        },

        // 键值对组：每条 Params 都当成「属性键 → 值」（覆盖「爆裂弹头」这种一次改多个属性的）。
        ["StatSet"] = (context, row) =>
        {
            foreach (ParamEntry entry in row.Params)
            {
                if (!string.IsNullOrEmpty(entry.Key))
                {
                    AddStat(context, entry.Key, row.Operation, entry.AsFloat(1f));
                }
            }
        },

        // 下面 5 个是现有异化，参数全部从 Params 组读，改数值不用改这里。
        ["ChainLightning"] = (context, row) => context.Player.AddChild(new ChainLightningRule
        {
            RewardId = row.Id,
            Cooldown = row.Param("Cooldown", 0.12f),
            Damage = row.Param("Damage", 14f),
            Range = row.Param("Range", 7f),
            Jumps = (int)row.Param("Jumps", 3f),
        }),
        ["Lifesteal"] = (context, row) => context.Player.AddChild(new LifestealRule
        {
            RewardId = row.Id,
            Ratio = row.Param("Ratio", 0.12f),
        }),
        ["BurnOnHit"] = (context, row) => context.Player.AddChild(new BurnOnHitRule
        {
            RewardId = row.Id,
            Duration = row.Param("Duration", 3f),
        }),
        ["AmmoOnKill"] = (context, row) => context.Player.AddChild(new AmmoOnKillRule
        {
            RewardId = row.Id,
        }),
        ["SummonOnKill"] = (context, row) => context.Player.AddChild(new SummonOnKillRule
        {
            RewardId = row.Id,
            MaxSummons = (int)row.Param("MaxSummons", 2f),
            // SummonScene 建议在 RewardConfig 上加一个 PackedScene 字段（现在是硬编码路径 RewardCatalog.cs:78）
            SummonScene = row.SummonScene,
        }),
    };

    public static bool TryGet(string handlerId, out Handler handler)
        => Handlers.TryGetValue(handlerId, out handler!);

    private static void AddStat(RewardContext context, string stat, ModifierOperation operation, float value)
    {
        context.Player.Stats.AddModifier(new StatModifier
        {
            Stat = stat,
            Operation = operation,
            Value = value,
            Source = context.Build,
        });
        context.Player.RefreshStats();
    }
}
```

**桥接层（消费方零改动）**：

```csharp
// Scripts/Rewards/RewardTable.cs
using System;
using System.Collections.Generic;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>
/// 增幅项表的**编译器**：把磁盘行编译成运行期的 RewardDefinition（形态与现在完全一致）。
/// 注意它**不是**磁盘上的表文件 —— 行在 Config/Tables/Upgrades/，索引用 ConfigIndex.Settings.Upgrades（§6.2）。
/// </summary>
public static class RewardTable
{
    private static List<RewardDefinition>? _compiled;

    /// <summary>ConfigIndex._Ready 里调一次。rows 为空/为 null 时不建立任何定义，All 会回退到 _fallback。</summary>
    public static void Rebuild(RewardConfig[]? rows)
    {
        _compiled = null;
        if (rows == null || rows.Length == 0)
        {
            GD.PushWarning("[RewardTable] ConfigIndex.Settings.Upgrades 为空，奖励使用代码内置目录。");
            return;
        }

        var built = new List<RewardDefinition>(rows.Length);
        foreach (RewardConfig row in rows)
        {
            if (row == null) { continue; }

            if (!RewardEffectRegistry.TryGet(row.ApplyHandler, out RewardEffectRegistry.Handler handler))
            {
                GD.PushWarning($"[RewardTable] {row.Id} 的 ApplyHandler「{row.ApplyHandler}」未注册，已跳过该条。");
                continue;
            }

            RewardConfig captured = row;   // 闭包捕获（foreach 变量在 C# 5+ 已经是每次新变量，这里显式一点更清楚）
            built.Add(new RewardDefinition
            {
                Id = captured.Id,
                Name = captured.DisplayName,
                Description = ConfigText.Format(captured.DescriptionTemplate, captured, captured.Params),
                Kind = captured.Kind,
                ApplicablePoolIds = captured.Pool == null ? System.Array.Empty<string>() : new[] { captured.Pool.Id },
                RequiredWeaponTags = captured.RequiredWeaponTags,
                Excludes = captured.Excludes,
                ConflictsWith = captured.ConflictsWith,
                Weight = captured.Weight,
                MaxStacks = captured.MaxStacks,
                RequiresSummoner = captured.RequiresSummoner,
                Apply = context => handler(context, captured),
            });
        }

        _compiled = built;
        GD.Print($"[RewardTable] 载入 {built.Count} 条增幅项（来源：表）。");
    }

    /// <summary>编译结果。没读表时返回 null，由 RewardCatalog.All 回退到代码内置目录。</summary>
    public static IReadOnlyList<RewardDefinition>? Definitions => _compiled;
}
```

`RewardCatalog.All` 从 `static readonly RewardDefinition[]` 改成（**消费方 `RewardPool` / `RunBuild` / `RunController` 只认 `IReadOnlyList<RewardDefinition>`，一行都不用改**）：

```csharp
private static readonly RewardDefinition[] _fallback = { /* 现在这份 C# 数组，迁移完成后删除 */ };

public static IReadOnlyList<RewardDefinition> All
    => RewardTable.Definitions ?? _fallback;

public static RewardDefinition? Find(string id)
{
    foreach (RewardDefinition definition in All)
    {
        if (definition.Id == id) { return definition; }
    }
    return null;
}
```

**样板 `.tres`**：**每行一个文件**，文件名与 `Id` 取同一个语义名。

`Config/Tables/Upgrades/upg_dmg_up.tres`（`load_steps = 1 ext + 0 sub + 1 = 2`）—— 只填通用项（"多数增幅项的基本是一个参数"）：

```ini
[gd_resource type="Resource" script_class="RewardConfig" load_steps=2 format=3]

[ext_resource type="Script" path="res://Scripts/Config/RewardConfig.cs" id="1_row"]

[resource]
script = ExtResource("1_row")
Id = "upg_dmg_up"
Slot = 1
DisplayName = "增伤弹头"
IconPath = "res://Assets/UI/tex_ui_icon_upg_dmg.png"
Weight = 1.0
TargetStat = "DamageMultiplier"
Operation = 1
Value = 1.15
MaxStacks = 3
DescriptionTemplate = "伤害 +15%"
Note = "通用项：AttackInstance 生成时读 DamageMultiplier（Weapon.cs:181）"
ApplyHandler = "StatMultiplier"
```

`Config/Tables/Upgrades/upg_evo_blast.tres` —— 一次改**多个**属性，用键值对组 + `StatSet`（`load_steps = 1 ext + 2 sub + 1 = 4`）：

```ini
[gd_resource type="Resource" script_class="RewardConfig" load_steps=4 format=3]

[ext_resource type="Script" path="res://Scripts/Config/RewardConfig.cs" id="1_row"]

[sub_resource type="Resource" id="p_blast_radius"]
script = ExtResource("1_row")
Key = "ExplosionRadiusAdd"
Value = 3.5
Note = "命中爆炸的半径（米）"

[sub_resource type="Resource" id="p_blast_scale"]
script = ExtResource("1_row")
Key = "ExplosionDamageScale"
Value = 0.6
Note = "爆炸伤害相对子弹本体伤害的比例"

[resource]
script = ExtResource("1_row")
Id = "upg_evo_blast"
Slot = 2
DisplayName = "爆裂弹头"
Weight = 1.0
Operation = 2
Kind = 1
RequiredWeaponTags = PackedStringArray("Gun")
MaxStacks = 1
DescriptionTemplate = "子弹命中时产生小范围爆炸"
Note = "多参数：命中后产生 3.5 米爆炸，伤害为本体的 60%"
ApplyHandler = "StatSet"
Params = Array[Resource]([SubResource("p_blast_radius"), SubResource("p_blast_scale")])
```

> ⚠️ `Params` 里的 `ParamEntry` 是**这个行文件内部的 `[sub_resource]`**（不是独立文件）—— 键值对是行的一部分，不构成"一张表"。
> 也因此上例里两个 `[sub_resource]` 的 `script` 都指向 `RewardConfig.cs`：`ParamEntry` 与 `RewardConfig` 仍分居两个 `.cs`（[architecture.md](architecture.md):42 的坑：一个 `.cs` 只能有一个能被挂载的类）。**手写时按上例把 `script` 写成 `RewardConfig` 的 `ExtResource` 即可**；若 Godot 保存成别的 id，以编辑器为准。

`Config/Tables/Upgrades/upg_mut_chain.tres` —— 靠 handler 而非 `StatSheet` 的异化（4 个参数，`load_steps = 2 ext + 4 sub + 1 = 7`）：

```ini
[gd_resource type="Resource" script_class="RewardConfig" load_steps=7 format=3]

[ext_resource type="Script" path="res://Scripts/Config/RewardConfig.cs" id="1_row"]
[ext_resource type="Script" path="res://Scripts/Config/ParamEntry.cs" id="2_param"]

[sub_resource type="Resource" id="p_chain_cd"]
script = ExtResource("2_param")
Key = "Cooldown"
Value = 0.12
Note = "每条事件链之间的最短间隔（秒），防递归"

[sub_resource type="Resource" id="p_chain_damage"]
script = ExtResource("2_param")
Key = "Damage"
Value = 14.0
Note = "每跳伤害"

[sub_resource type="Resource" id="p_chain_range"]
script = ExtResource("2_param")
Key = "Range"
Value = 7.0
Note = "找下一个目标的半径（米）"

[sub_resource type="Resource" id="p_chain_jumps"]
script = ExtResource("2_param")
Key = "Jumps"
Value = 3
Note = "最多跳几次"

[resource]
script = ExtResource("1_row")
Id = "upg_mut_chain"
Slot = 3
DisplayName = "连锁闪电"
Weight = 1.0
Kind = 2
MaxStacks = 1
DescriptionTemplate = "击杀敌人时对附近敌人放电（{Range} 米 / {Jumps} 跳）"
Note = "击杀触发：0.12 秒链间隔 / 14 伤害 / 7 米范围 / 3 跳"
ApplyHandler = "ChainLightning"
Params = Array[Resource]([SubResource("p_chain_cd"), SubResource("p_chain_damage"), SubResource("p_chain_range"), SubResource("p_chain_jumps")])
```

`Config/ConfigIndex.tres` 上的**索引**（这一行**不要手写**，在编辑器里往数组里拖 3 个行文件即可，Godot 会写对格式）：

```ini
[ext_resource type="Resource" path="res://Config/Tables/Upgrades/upg_dmg_up.tres" id="20_upg_dmg"]
[ext_resource type="Resource" path="res://Config/Tables/Upgrades/upg_evo_blast.tres" id="21_upg_blast"]
[ext_resource type="Resource" path="res://Config/Tables/Upgrades/upg_mut_chain.tres" id="22_upg_chain"]

[resource]
script = ExtResource("1_balance")
…
Upgrades = Array[Resource]([ExtResource("20_upg_dmg"), ExtResource("21_upg_blast"), ExtResource("22_upg_chain")])
```

> **为什么 `Params` 用内嵌 `[sub_resource]`、而行用独立文件**：行需要被 `ConfigIndex.tres` 的数组引用（插件也按文件分行），键值对只在行内部被读 —— 两者粒度不同，粘在一起反而让"一行 = 一个语义实体"更清楚。
> **务实的做法**：先照 `upg_dmg_up.tres` 那个最简形态（1 个 `ext_resource` + 1 个 `[resource]`）建 3–5 行跑通，再上带 `Params` 的行。最简形态几乎不可能写错。

> ⚠️ 上面的 `Operation = 1` / `Kind = 1` 是**枚举的整数下标**，不是名字 —— `.tres` 里 Godot 就是这么存枚举的（`Scenes/Main.tscn:150` 的 `Kind = 1` 同理）。写表时务必对照 `ModifierOperation`（`StatSheet.cs:6-12`：`Add=0, Multiply=1, Override=2, Cap=3`）与 `RewardKind`（`RewardDefinition.cs:7-17`：`Enhancement=0, Evolution=1, Mutation=2`）。
> 也可以只写 `DisplayName` 让策划在 Inspector 里点选，避免手数下标出错 —— 建议**表文件都先进编辑器点一遍再手改**。
> `Params` 里 `Value = 1.15` / `Value = 3` 混着浮点与整数：`Variant` 会按字面量推断类型，`ParamEntry.AsFloat()` 两者都能读（§4.3），所以表里写 `3` 还是 `3.0` 都行。

### T4 角色表 —— `CharacterConfig : Resource`（一行一个文件）

**目录**：`Config/Tables/Characters/chr_*.tres`（一行一个文件）；索引用 `ConfigIndex.tres` 上的 `[Export] CharacterConfig[] Characters`
**替换**：`PlayerCharacterKind`（`ConfigIndexData.cs:14-24`）+ `RunController.ApplyBalance()` 的 if/else 链（`RunController.cs:81-122`）

| 字段 | 类型 | 默认值 | 用途 |
|---|---|---|---|
| `Id` / `Slot` | `string` / `int` | `"character_1"` / `1` | 寻址（"角色1"） |
| `DisplayName` / `IconPath` / `ModelPath` / `ScenePath` | `string` | `""` | 名称 / 图标 / 模型（替换占位胶囊体）/ 角色预制体 |
| `Tags` | `string[]` | `["Player","Gun"]` | 对应 `Player.tscn:37` |
| `MaxHealth` / `MaxArmor` / `MaxShield` / `MoveSpeed` | `float` | `250/40/0/7` | 对应 A1-A3 |
| `DamageReduction` | `float` | `0` | 额外减伤 |
| `WeaponEnabled` | `bool` | `true` | 近战角色收起枪械（`RunController.cs:101`） |
| `EnabledAbilityIds` | `string[]` | `[]` | **硬编码的角色开关矩阵**（`RunController.cs:104-122`：`AbilityMelee` / `AbilitySummon` / `AbilityControl`）；按技能表的 `Id` 启用 |
| `Params` | `ParamEntry[]` | `[]` | 角色专属参数（如 `HeavyMeleeDamage`） |
| `DescriptionTemplate` | `string` | `""` | 选择界面文案（`StartScreen.cs:46-48`） |
| `Note` | `string` | `""` | 备注 |

> `HeavyMeleeDamage`（`ConfigIndexData.cs:48`）现在是"角色 2 的近战伤害"，通过 `ApplyBalance` 写进 `AttackHitbox.Damage`（`RunController.cs:104-111`）。归到 `character_2` 的 `Params`（`{Key:"MeleeDamage", Value:32}`）比留在 ConfigIndex 更符合"不同类型有不同的参数表"。

### T5 敌人表 —— `EnemyConfig : Resource`（一行一个文件）

**目录**：`Config/Tables/Enemies/enemy_*.tres`（一行一个文件）；索引用 `ConfigIndex.tres` 上的 `[Export] EnemyConfig[] Enemies`
**替换**：`Scenes/Main.tscn:75-132` 的 8 个 `EnemySpawnEntry` + `StageDefinition.AllowedEnemies` 的字符串匹配

| 字段 | 类型 | 默认值 | 用途 |
|---|---|---|---|
| `Id` | `string` | `"enemy_chaser"` | **稳定键**（取代 `DisplayName` 匹配，见 F4） |
| `DisplayName` | `string` | `""` | 界面显示（对应 `EnemySpawnEntry.DisplayName`） |
| `Scene` | `PackedScene?` | `null` | 敌人预制体 |
| `Enabled` | `bool` | `true` | 本关是否允许生成 |
| `Cost` | `int` | `1` | 占用 Spawn Budget |
| `Weight` | `float` | `1f` | 抽中概率 |
| `MinElapsedSeconds` | `float` | `0` | 开场多少秒后才允许出现 |
| `EliteChance` | `float` | `0` | 精英概率 |
| `Tier` | `int` | `0` | 强度档位（可替代 `AllowedEnemies` 的分组） |
| `Overrides` | `ParamEntry[]` | `[]` | **可选数值覆盖**（见下方边界说明） |
| `Note` | `string` | `""` | 备注 |

**表 vs 场景的边界（重要）**：

| 归表 | 归场景（`Scenes/Enemies/*.tscn`） |
|---|---|
| 这场战斗生成什么、多强：`Cost` / `Weight` / `EliteChance` / `MinElapsedSeconds` / `Enabled` / `Tier` | 这个敌人长什么样、会什么技能：网格 / 材质 / 碰撞形状 / Brain / Behavior / Ability 组合 |
| `Overrides` 里**显式列出**的数值 | 未列出的 `Unit` 基础值（`MaxHealth` / `MaxArmor` / `MoveSpeed`…） |

理由：[architecture.md](architecture.md):97-100 已经把"新敌人 = 复制预制体 + 换模型/数值"定为扩展方式，且"实例覆盖是调参首选"。把 10 个敌人场景的 303 处赋值全部搬进表等于推翻这套工作流。**表只做"可选覆盖"**：`Overrides` 里没写的字段保留场景值 —— 这就是用户要的"读不到则使用自身的默认值，有则读取"。

**一个必须改的点**：`StageDefinition.AllowedEnemies`（`StageDefinition.cs:25,42-43`）现在按 `DisplayName` 匹配：

```csharp
public bool Allows(string displayName)
    => AllowedEnemies.Length == 0 || System.Array.IndexOf(AllowedEnemies, displayName) >= 0;
```

`ConfigIndex.tres:15` 写的是 `PackedStringArray("基础近战", "快速近战", "自爆")`。**只要有人把 `Main.tscn:78` 的 `DisplayName = "基础近战"` 改成别的文案，第 1 关就会静默失去全部敌人。** 改成按 `EnemyConfig.Id` 匹配（`Allows(string id)`），显示名与逻辑键解耦。

### T6 关卡表 —— `StageDefinition : Resource`（**类已存在**，一行一个文件）

**目录**：`Config/Tables/Stages/stg_*.tres`（一行一个文件）；索引用 `ConfigIndex.tres` 上**已有的** `[Export] StageDefinition[] Stages`（形态不变，只是每一关独立成文件）
**替换**：`ConfigIndex.tres` 里 3 个内嵌子资源 + `RunController.DefaultStages()` —— ✅ **两步都已落地（2026-10，见 §5 阶段 1 的实测证据）**

`StageDefinition` 已经是一个 `[GlobalClass] partial class : Resource`，老的 15 个字段**一个都没改**，落地时做了 4 件事：

1. ✅ 从 `ConfigIndex.tres` 的 `[sub_resource]` 变成**每关一个独立文件** `Config/Tables/Stages/stg_outer.tres` / `stg_inner.tres` / `stg_core.tres` —— `Config/Tables/Stages/` 现在就是一张干净的"关卡表"（一行一关），策划可以单独打开一关改。
2. ✅ `ConfigIndex.tres` 的 `Stages` 数组从 `SubResource` 改成 `ExtResource`（指向上面 3 个文件）。**`ConfigIndexData.Stages` 字段本身没动**（仍是 `[Export] StageDefinition[] Stages`），所以 `StageController` 读 `ConfigIndex.Settings.Stages` 的代码一行都没改。
3. ✅ **删掉了 `RunController.DefaultStages()`**。回退路径落成了 `RunController.ResolveStages()`：
   ```csharp
   // 两个调用点都走它（原来 :180 与 :429 各写了一遍三元表达式）
   _stages = ResolveStages();
   // 表为空 → new[] { new StageDefinition() } + GD.PushWarning + 一条 GameLog
   ```
   缺表时日志会写 `关卡表：0 关（来源：默认值）…（ConfigIndex.tres 已加载/未加载，玩家生命 N）`，所以"读不到就用自身默认值"只有**一个**真源（`StageDefinition` 上的 `[Export]` 初值）。
4. ✅ **`SubResource` → `ExtResource` 这一步已经做了**（原计划要等探针）。§7.2 已实测通过，且顺手挖出两条新坑（缺行文件会整份 `ConfigIndex.tres` 失败；Inspector 拖拽填不了 C# 资源数组），操作纪律见 §5 阶段 1。

新增字段（向后兼容，老 `.tres` 缺字段会自动用默认值）—— **本次已落地**：

| 字段 | 类型 | 默认值 | 用途 | 状态 |
|---|---|---|---|---|
| `Id` | `string` | `"stage_1"` | 稳定键（`RunSetup` / 存档 / 分支用） | ✅ 已加；三行分别是 `stg_outer` / `stg_inner` / `stg_core`（与文件名同语义名），开局日志会打出来 |
| `PressureGrowthMultiplier` | `float` | `1f` | Pressure 增长倍率（关卡核心文档 §7.2） | ✅ 已加并接进 `StageController.AddPressureForNode` |
| `PressureThreshold` | `int` | `0`（= 未设置） | Pressure 阈值（§7.3）；`<= 0` → 用 `ConfigIndex.PressurePerPoint` | ✅ 同上 |
| `PressurePerNodeNormal` | `float` | `0f` | 普通挑战基础 Pressure（§7.4）；`<= 0` = 不加（= 分表前的行为） | ✅ 同上 |
| `PressurePerNodeHighValue` | `float` | `0f`（= 未设置） | 高价值挑战基础 Pressure（§7.4）；`<= 0` → 用 `ConfigIndex.PressurePerHighValueNode` | ✅ 同上 |
| `PressureEvolutionBonus` / `PressureMutationBonus` | `float` | `0f`（= 未设置） | 进化/异化额外比例（§7.2/§7.4）；`<= 0` → 用 `ConfigIndex` 上的同名全局值 | ✅ 同上 |
| `PressureUpgradeTags` | `string[]` | `[]` | **数值型**强化池的适用规则（§7.4）；留空 = 不限制 | ✅ **阶段 6 起真的生效**：消费者是 `StageController.PickTags`（节点行 `BossModifierTags` 优先，其次本字段）；在此之前只进日志 |
| `AllowedEnemyIds` | `string[]` | `[]` | 取代 `AllowedEnemies` 的显示名匹配（见 F4） | ⏸ **本次没加**：它的消费者是敌人表（T5，阶段 4）。现在加就是空字段，违反"不留不生效的配置"（§1.4-⑤） |
| `Params` | `ParamEntry[]` | `[]` | 以后的新关卡参数 | ⏸ **本次没加**：同上，等第一个真实参数出现再加（一行的事） |

> ⚠️ **Pressure 两条硬约束（关卡核心文档 §7.1 / §7.5，实现时必须守）**：
> ① **Boss Pressure（数值强化）与 Boss Build（行为/技能）必须分离**，不得合并成一个"来源不明的池" ——
> 所以 `PressureUpgradeTags` 只描述数值型强化，`BossPerkCatalog`（狂暴/召唤师/尖啸…）保持独立；
> ② **不要为 Pressure 设计"可被玩家增幅修改"的扩展接口**（文档明确禁止，除非以后重新决定这项设计）—— 本次没有加任何这类钩子。
>
> **Pressure 数值的取值规则**（只有一条，避免两个真源）：行里填了用行里的；留 0 用 `ConfigIndex` 的全局默认值。
> 阶段 1 时三行**都没填**，所以与分表前**逐位一致**（`+187.5 → 1` / `+225 → 3`）。
> ✅ **阶段 6 之后**：基础点数改成"节点行 → 关卡行按价值层 → ConfigIndex"三层，`node_evolution` / `node_mutation`
> 填了 150（= 文档 §7.2 的固定值），所以那两个数字**仍然逐位不变**，只是来源变成了节点表。
> 完整实测见 [config.md](config.md) 的「配置表阶段 6」。

### T7 节点表 —— `NodeConfig : Resource`（一行一个文件）

**目录**：`Config/Tables/Nodes/node_*.tres`（一行一个文件）；索引用 `ConfigIndex.tres` 上的 `[Export] NodeConfig[] Nodes`
**替换**：`RewardService.cs:16-38` 的 3 个 switch + `NodePlacement.cs:18-26` 与 `StageController.cs:653-661` 的**重复实现**

| 字段 | 类型 | 默认值 | 用途 |
|---|---|---|---|
| `Id` / `Slot` | `string` / `int` | `"node_enhance"` / `1` | 寻址 |
| `Kind` | `RewardNodeKind` | `Enhancement` | 对应 `RewardNode.cs:11-26` |
| `DisplayName` | `string` | `""` | 取代 `RewardService.TitleFor`（L27-38） |
| `RewardKind` | `RewardKind?` | `null` | 取代 `RewardService.FilterFor`（L16-24）。`null` = 不按类型过滤 |
| `Layer` | `int` | `0` | 取代 `NodePlacement.LayerOf` / `StageController.LayerOf`（2 高 / 1 中 / 0 低） |
| `WaveCount` | `int` | `1` | 取代 `WavesFor` 的 `2→3 / 1→2 / _→1` |
| `MaterialPath` | `string` | `""` | 对应 `Assets/Materials/mat_node_*.tres`（9 个） |
| `IconPath` | `string` | `""` | 图标 |
| `SpawnsEncounter` | `bool` | `true` | 对应 `RewardNode.cs:62` |
| **`BossPressurePoints`** | `float` | `0` | **★ 场景 A**：挑战这个节点给 Boss 加多少 **Pressure 基础点数**。取代 `StageController.cs:342-348` 里那个按 `RewardNodeKind` 分支的硬编码（原本是 `ConfigIndex.PressurePerHighValueNode` × `PressureEvolutionBonus`/`PressureMutationBonus`，且只对 `Layer >= 2` 生效）。✅ **阶段 6 已接**（`StageController.ResolveBasePressure`，三层取值的第一层）；`node_evolution` / `node_mutation` 填了 150 |
| **`BossModifierRolls`** | `int` | `0` | **★ 场景 A**：挑战这个节点**额外**给 Boss 抽几次数值强化（与阈值无关；阈值转换的次数由点数与阈值算出来）。✅ **阶段 6 已接**。⚠️ 落地形态与本文原稿不同：**没有**给 `EncounterController` 加 `BossModifierRolls` 字段（那会把"一关怎么攒 Pressure"渗进"一场遭遇"层），改成 `StageController` 自己排队、Boss 出生时用 `BossSpawned` 信号套用 |
| **`BossModifierTags`** | `string[]` | `[]` | **★ 场景 A**：这次抽取的候选标签（对应 `PressureModConfig.Tags`，如 `"Offense"` / `"Defense"` / `"Mobility"`）。留空 → 用关卡行 `PressureUpgradeTags` → 两层都空 = 不限制。✅ **阶段 6 已接**（`StageController.PickTags` → `BossPerkCatalog.RollPressure` / `FilterPressure`） |
| `Params` | `ParamEntry[]` | `[]` | 节点专属参数（`HealRatio`、商店价目等） |
| `Note` | `string` | `""` | 备注 |

**这是收益最高的一张表**：一处交换逻辑（两个文件的重复 `switch`）+ 两个分派 `switch`，共 3 处代码被一张表替掉，而且"节点类型"这个枚举本身也变成了表里的一个字段 —— 以后加节点类型**只加一行 `.tres`**。**它同时是场景 A（增强首领）的入口**：那个"这个节点的挑战该给 Boss 加什么"的映射，落在这里的三列上。

> `RewardNodeKind` 枚举（`RewardNode.cs:11-26`）与 `Main.tscn` 里 10 个节点的 `Kind = N` 保持不变（枚举是 `RewardNode` 的 `[Export]`，改它会牵动 `Scenes/Main.tscn` 的 10 处赋值）。表里的 `Kind` 字段与枚举同类型即可。

### T8 状态表 —— `StatusConfig : Resource`（一行一个文件）

**目录**：`Config/Tables/Status/sts_*.tres`（一行一个文件）；索引用 `ConfigIndex.tres` 上的 `[Export] StatusConfig[] Statuses`
**替换**：`StatusEffect.cs:26-84` 的 `switch`（6 处硬编码数值）

| 字段 | 类型 | 默认值 | 用途 |
|---|---|---|---|
| `Id` | `string` | `""` | 对应 `AttackHitbox.StatusId`（`AttackHitbox.cs:22`）与 `StatusPresets.Create(id, duration)` |
| `DisplayName` / `IconPath` | `string` | `""` | 界面 |
| `Modifiers` | `StatOverride[]` | `[]` | 取代 `StatusEffect.Modifiers`（`StatSheet.cs:15-21` 的 `StatModifier` 不可序列化，需要一个 `Resource` 版：`{Stat, Operation, Value}`） |
| `DamagePerSecond` | `float` | `0` | 对应 `StatusEffect.DamagePerSecond`（`StatusEffect.cs:14`，Burn 6 dps） |
| `Note` | `string` | `""` | 说明 |

`StatusPresets.Create(id, duration)`（`StatusEffect.cs:28`）改为查表 + 回退：

```csharp
public static StatusEffect? Create(string id, float duration)
{
    if (string.IsNullOrEmpty(id)) { return null; }

    StatusConfig? row = Config.ConfigTable.StatusRow(id);   // 读不到 → null
    if (row != null) { return row.ToRuntime(duration); }

    // 读不到表 → 用代码内置的兜底（迁移期保留现在这份 switch）
    return Fallback(id, duration);
}
```

### T9 首领增强表（含词条）—— `BossModifierConfig : Resource`（一行一个文件）

> ✅ **落地形态（2026-10，阶段 3 建表 + 阶段 6 接线）—— 本节以下都是设计原稿，名字与目录与最终落地不同**：
>
> | 原稿 | 实际落地 | 为什么 |
> |---|---|---|
> | 一张表 `BossModifierConfig` + `Source` 枚举（"开局词条 / 节点触发 / 压力触发"三合一） | **两张表**：行为型 = `Config/Tables/Skills/bhv_*.tres`（`AbilityConfig.ApplyHandler` 非空，4 行）；数值型 = **`Config/Tables/Pressure/pmod_*.tres`**（`PressureModConfig`，3 行） | 用户拍板"**首领行为表本质也是一些技能**"—— 行为型直接并进技能表复用通用项 + `Params` + `AttackParams`；两种来源的字段本来就不一样（数值型只需要 `Modifiers`），`Source` 枚举因此**没加** |
> | 目录 `Config/Tables/BossMods/`、索引 `BossModifiers` | 目录 `Pressure/`、索引 `ConfigIndexData.PressureMods` | 与"压力"这个系统同名，少一层翻译 |
> | 数值型字段 `TargetStat` + `Operation` + `Value`（标量） | `StatOverride[] Modifiers`（一行可改多项） | 狂暴要同时改伤害与攻速，标量表达不了；`StatOverride` 已在增幅项表验证过。⚠️ **阶段 7 起一行只放一项**（用户要求"每个参数单独占一个词条栏"），`Modifiers` 现在每行只有 1 条 |
> | `MaxLevel` / `Source` | **没加** | 无消费者（"不留不生效的列"纪律），§5 阶段 6 有说明 |
> | 无 `Tags` | **加了 `Tags`** | §7.4/§7.5 要求"候选内容与适用规则可配"，`StageDefinition.PressureUpgradeTags` / `NodeConfig.BossModifierTags` 靠它生效 |
> | `Weight` 与增幅项共用一套加权抽取 | ✅ 照原稿做了（`WeightedPick`，阶段 0 抽出公共工具） | 阶段 6 起**真的按权重抽**（有 30000 次实测）；阶段 7 起池子 **8 条**、权重 3/3/2/2/2/1/1/1 |
> | 无"技能参数"列 | **阶段 7 加了 3 列**：`AbilityNode` + `AbilityScales` + `AttackScales` | §7.5 明列"技能冷却等参数"属**数值型**，所以技能参数进本表（不进 Boss Build）；`Value` 是**倍率**（乘在能力当前值上），保证"同一条抽两次继续叠乘" |
> | 无"适用条件"字段 | **阶段 7 加了 2 列**：`BossIds` + `StageIds`（+ `AbilityNode` 隐含"必须有这个技能节点"） | 用户要求"限定词记得做好刷新判定"：判定在 **Boss 出生时**（`BossPerkCatalog.ApplyWithRefresh`），抽签时不判、不复用 |
>
> **三个抽取/判定点（阶段 6/7）**：Boss 开场词条 `BossPerkCatalog.Roll()` 只抽**行为型**；Pressure 转换
> `BossPerkCatalog.RollPressure(tags)` 只抽**数值型**（按 `Tags` 过滤、按 `Weight` 加权）；
> **限定词判定** `BossPerkCatalog.ApplyWithRefresh(perk, context)` 在**首领出生时**跑（判不成立就换抽另一条）。
> 见 §5 阶段 6 / §5 阶段 7 与 [config.md](config.md) 的「配置表阶段 6 / 7 实测」。

**目录**：`Config/Tables/BossMods/boss_*.tres`（一行一个文件）；索引用 `ConfigIndex.tres` 上的 `[Export] BossModifierConfig[] BossModifiers`
**替换**：`BossPerk.cs:33-117`（7 条词条 + 4 处十进制）**+** `StageController.AddPressureForNode()`（`StageController.cs:334-359`）里那段硬编码的节点 → 压力映射

#### 先回答："增强项/词条"归哪张表？

| 选项 | 判断 |
|---|---|
| 复用 **T3 增幅项表** | ❌ **不行**。三条硬理由：① **作用对象不同** —— 增幅项作用于玩家（`RewardContext{Player, Weapon, Build}`，`RewardDefinition.cs:20-25`），增强项作用于 Boss 与 `BossPressure`；② **抽取时机不同** —— 增幅项在节点/商店三选一（可反复），增强项在 Boss 出场时一次性抽完（`EncounterController.cs:671-677`）；③ **会污染现有的池子指标** —— `RewardPool.Initialize` 遍历 `RewardCatalog.All`（`RewardPool.cs:18-28`），混表后 [rewards.md](rewards.md):79-86 记录的"枪手 18 / 召唤 19"这个**已验证的回归指标直接失效**，而 `RequiresSummoner` / `MaxStacks` / `Excludes` 这些字段对 Boss 增强项毫无意义 |
| 新开一张与 T9 **平行**的表（保留 `BossPerkTable` 再加 `BossModifierTable`） | ⚠️ 能跑，但**没必要** —— 两张表的字段会一模一样（`Id`/名称/权重/参数/说明/处理器），只有"来源"不同 |
| ⭐ **把 T9 扩成一张"首领增强表"**（本设计采用） | ✅ **推荐**。"词条"与"节点带来的增强"在数据形状上是**同一个东西**，差别只有 `Source`（开局词条 / 节点触发 / 压力等级触发）。一张表 + 一个 `Source` 字段，比两张同构表更好维护，也让"抽奖时按来源筛候选"变成一次普通过滤 |

**共用与不共用**（这条边界很重要）：

| 共用 | 不共用 |
|---|---|
| `WeightedPick`（加权抽取工具，见下）· `ParamEntry`（键值对组）· `ConfigText`（文本模板）· "`ApplyHandler` 字符串 + 处理器注册表"这套**机制** | **处理器注册表实例** —— 增强项的 handler 签名是 `(BossModifierContext, BossModifierConfig)`，能拿到 `Boss` 与 `BossPressure`；与 `RewardEffectRegistry` 的 `(RewardContext, RewardConfig)` 不同（两个各约 20 行的静态类，各自清晰） |

#### 抽奖值：和增幅项的「权重」是同一套机制吗？

**是同一套，而且现在这段加权抽样代码在项目里重复了两遍** —— `RewardPool.Roll`（`RewardPool.cs:84-106`）与 `BossPerkCatalog.Roll`（`BossPerk.cs:122-152`）是逐行相同的"求总权重 → 掷点 → 逐个减"。抽成一个泛型工具，两张表共用：

```csharp
// Scripts/Config/WeightedPick.cs
using System;
using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Config;

/// <summary>按 Weight 加权、不重复地抽 count 个（从 RewardPool.Roll 与 BossPerkCatalog.Roll 抽出，两处逻辑本来就一样）。</summary>
public static class WeightedPick
{
    /// <summary>可被加权抽取的行实现它 —— 与 IConfigRow 分开，因为"可抽"与"可寻址"是两件事。</summary>
    public interface IWeighted
    {
        float Weight { get; }
    }

    public static List<T> Pick<T>(IReadOnlyList<T> source, int count, RandomNumberGenerator rng, Func<T, bool>? filter = null)
        where T : class, IWeighted
    {
        var available = new List<T>();
        foreach (T row in source)
        {
            if (filter == null || filter(row)) { available.Add(row); }
        }

        var picked = new List<T>();
        for (int i = 0; i < count && available.Count > 0; i++)
        {
            float total = 0f;
            foreach (T row in available) { total += Mathf.Max(0.01f, row.Weight); }

            float roll = rng.RandfRange(0f, total);
            int index = available.Count - 1;
            for (int j = 0; j < available.Count; j++)
            {
                roll -= Mathf.Max(0.01f, available[j].Weight);
                if (roll <= 0f) { index = j; break; }
            }

            picked.Add(available[index]);
            available.RemoveAt(index);
        }

        return picked;
    }
}
```

> ⚠️ **行为差异提醒**：`RewardPool.Roll` 用自己的 `RandomNumberGenerator _rng`（`RewardPool.cs:14`），`BossPerkCatalog.Roll` 用静态 `RandomNumberGenerator Rng`（`BossPerk.cs:119`），而 `BossPerkCatalog` 用的是 `Rng.RandfRange`。抽成公共工具时**必须把 rng 当参数传进去**（上面签名已经这么做），否则随机序列会变、自测基线会漂。**不要**改成 `GD.RandRange`。
> 顺手说明为什么这个抽取消掉了：这也让 `RewardPool` 与 `BossPerkCatalog` 的 **`Weight` 字段第一次真的生效**（现在两张表的 `Weight` 都恒为默认值，见 §1.4 与下方缺口表）。

**"抽奖值"在这张表里其实是三个独立旋钮**（用户说的"抽奖值"要拆成这三件事才调得动）：

| 旋钮 | 字段 | 现状 | 归属 |
|---|---|---|---|
| **抽几次** | `NodeConfig.BossModifierRolls`（每个奖励节点贡献几次抽取）→ `StageController` 累加 → `EncounterController.BossModifierRolls` | 只有 `StageDefinition.BossPerkCount`（**按关卡**，不是按节点），且**只有高价值节点**才给压力（`StageController.cs:336-339`） | **T7 节点表**提供，**T11** 消费 |
| **抽中概率** | `BossModifierConfig.Weight` | `BossPerk.Weight` 存在但 **7 条全是默认 `1f`**（`BossPerk.cs:15`）→ 等概率，等于没配 | **T11** |
| **候选范围** | `BossModifierConfig.Source` + `Tags` | 恒为"全部 7 条"（`BossPerk.cs:124`） | **T11** |

#### 强化参数：复用键值对组，还是固定字段？

**两级并存，与 T3 增幅项完全同构**（"多数参数项能通用的情况则使用通用项"的第三次应用）：

| 情况 | 用哪一级 | 现有词条里的例子 |
|---|---|---|
| 只改一个 `StatSheet` 属性 | 通用项 `TargetStat` + `Operation` + `Value` | 疾行（`MoveSpeedMultiplier ×1.35`，`BossPerk.cs:62`） |
| 一次改多个属性 | 键值对组 `Params` + `ApplyHandler = "StatSet"` | 狂暴（伤害 `1.25` + 攻速 `1.2`，L42-43）· 铁壁（生命 `1.4` + 护甲 `+150`，L53-54） |
| 改 `BossPressure` 的字段 | 键值对组 `Params` + 专属 handler | 提前觉醒（`Pressure.InitialLevel += 1`，L73）· 召唤师（`AddsPerLevel += 1`、`AddSpawnLevel -= 1`，L86-87） |
| 启用某个技能节点 | 键值对组 `Params` + 专属 handler | 尖啸强化 / 冲锋强化（`scream.Enabled = true`，L98-115） |

**现有 7 条词条的迁移账**：**3 条纯数据**（狂暴 / 铁壁 / 疾行，其中 2 条走 `StatSet`）· **4 条需要 handler**（提前觉醒 / 召唤师 / 尖啸强化 / 冲锋强化）。

#### 字段清单

| 字段 | 类型 | 默认值 | 用途 |
|---|---|---|---|
| `Id` | `string` | `"boss_berserk"` | 稳定键（HUD 显示、日志） |
| `Slot` | `int` | `1` | 排序 |
| `DisplayName` | `string` | `""` | HUD 上的词条名（`PlayerHud.cs:133-134` 已经在拼 `_bossPerkNames`） |
| `IconPath` | `string` | `""` | 图标 |
| **`Source`** | `BossModifierSource` | `OpeningPerk` | **新增**：`OpeningPerk`（开局词条）/ `FromNode`（奖励节点挑战得来）/ `FromPressure`（压力升级触发）。配合 `Tags` 决定"这次抽取的候选范围" |
| `Tags` | `string[]` | `[]` | 候选过滤标签（如 `"Evolution"` / `"Mutation"` 对应节点类型）。⚠️ 反面教材：`RewardDefinition.PoolTags` 曾经就是这样**填了但从未被读取**的字段（阶段 4 已删，§1.4-⑤）—— 别重犯，这个字段**必须**在抽取时真的用上，并有一条自测输出证明 |
| **`Weight`** | `float` | `1f` | 抽中概率 |
| `TargetStat` / `Operation` / `Value` | `string` / `ModifierOperation` / `float` | `""` / `Multiply` / `1f` | 通用项 |
| `Params` | `ParamEntry[]` | `[]` | 键值对组（多参数 / 改 `BossPressure` 字段 / 启用技能节点） |
| `DescriptionTemplate` | `string` | `""` | 说明模板（`{Key}` 占位符，§4.4） |
| `Note` | `string` | `""` | 机制说明 |
| `ApplyHandler` | `string` | `"StatMultiplier"` | 处理器 id |

#### 改造后的完整链路（"奖励节点 → 增强首领"）—— ✅ **阶段 6 已按此落地（两处按实际实现修正）**

```
玩家挑战奖励节点
  └─ NodeConfig（T7）给出这个节点的贡献：
        BossPressurePoints（压力基础点数，取代 StageController 里那个硬编码 switch）
        BossModifierRolls （额外抽几次增强）
        BossModifierTags  （这次抽取偏向哪一类增强）
  └─ StageController.AddPressureForNode()
        基础点数 = 节点行 →（留 0）关卡行按价值层的 HighValue / Normal →（留 0）ConfigIndex 全局默认值
        本次点数 = 基础点数 × 关卡行 PressureGrowthMultiplier × 进化/异化额外比例
        逐次跨阈值：每满 PressureThreshold 点【立即】BossPerkCatalog.RollPressure(PickTags(...))
        ↓ 抽到的 BossPerk 排队（_pendingPressurePerks）
  └─ OpenBossDoor()
        BossInitialPressure = max(0, ChallengesUsed - FreeChallengeQuota)      ← 修正：不再加 Pressure 折算等级
  └─ EncounterController.SpawnBossNow()
        BossPerkCatalog.Roll(BossPerkCount)     ← Boss Build：只抽行为型（阶段 6 修正）
        最后 EmitSignal(BossSpawned)
  └─ StageController.ApplyPendingPressurePerks()（订阅 BossSpawned）
        perk.Apply(new BossPerkContext{Boss, Source, Pressure}) → StatSheet Modifier
```

> **与原稿的两处修正**：① `BossInitialPressure` **不**叠加 Pressure 折算出来的压力等级
> （转换的产出已经是"抽一条数值强化"，两头都算就是重复）；② 没有给 `EncounterController` 加
> `BossModifierRolls` 字段 —— 改由 `StageController` 排队 + `BossSpawned` 信号套用，
> 理由是"一关怎么攒 Pressure"不该渗进"一场遭遇"那一层。

#### 当时的缺口（2026-10 阶段 6 已全部补齐，原文保留作为依据）

| 缺口 | 证据 | 阶段 6 |
|---|---|---|
| **没有"奖励节点 → 增强什么"的映射** | `AddPressureForNode` 是 3 分支 `switch`，只认 `Evolution` / `Mutation`，且**只有高价值节点计数** | ✅ 节点表三列接管（三层取值 + 额外抽次 + 候选标签） |
| **没有"抽奖"** | 节点带来的只有确定性的压力点数，没有按权重抽取 | ✅ 每次跨阈值立即 `RollPressure`（加权 + 标签过滤） |
| **权重全是 1** | `BossPerk.Weight` 7 条一个都没填 | ✅ 数值型 3 条改成 3/2/2，并有 30000 次实测；行为型仍是等概率（4 条等分，故意不动） |
| **强化参数不可配** | `BossPerk.Apply` 是闭包，数值写在闭包里 | ✅ 数值型搬进 `PressureModConfig.Modifiers : StatOverride[]`（`pmod_*.tres`） |
| **压力参数有两个来源** | `ConfigIndex.tres:107-109` **覆盖**了 `Boss.tscn` 的同名导出值 | ⏸ 未动（本轮范围是 §7；这是 §13 压力曲线那件事） |

> 结论（当时的）：用户说的「奖励节点用于增强首领的抽奖值和强化参数」**当时只实现了三分之一** —— 有"确定性的压力点数累积"，缺"按权重抽取"，缺"强化参数可配"。
> ✅ **2026-10 阶段 6 三件都补齐了**（见上表与 §5 阶段 6）。⚠️ 唯一遗留：**HUD 的词条栏只显示行为型**，看不到 Pressure 数值强化。

### T10 局外成长表 —— `MetaUpgradeConfig : Resource`（一行一个文件）

**目录**：`Config/Tables/Meta/meta_*.tres`（一行一个文件）；索引用 `ConfigIndex.tres` 上的 `[Export] MetaUpgradeConfig[] MetaUpgrades`
**替换**：`MetaProgress.cs:27-47`（5 项效果 + 1 个价格公式）+ `BaseScreen.cs:70-74` 的文案 + `RunSettlement.cs:10-16` 的 3 个 `const`

| 字段 | 类型 | 默认值 | 用途 |
|---|---|---|---|
| `Id` | `string` | `"health"` | 存档键名（**必须与现有 `meta.cfg` 的键一致或做映射**，见下） |
| `DisplayName` | `string` | `""` | 界面 |
| `StatKey` | `string` | `""` | 套用的属性键（`MaxHealthAdd` / `DamageMultiplier` / …） |
| `Operation` | `ModifierOperation` | `Add` | 叠加方式 |
| `PerLevel` | `float` | `0` | 每级效果（现在：血量 `10`、伤害 `0.05`、移速 `0.03`、暴击 `0.03`、装弹 `0.06`） |
| `BaseCost` / `CostGrowth` | `int` | `10` / `10` | 价格（现在 `CostOf = 10 * (level + 1)`，即 `BaseCost=10, CostGrowth=10`） |
| `MaxLevel` | `int` | `0` | 0 = 不限 |
| `DescriptionTemplate` | `string` | `""` | 取代 `BaseScreen.cs:70-74` |
| `Note` | `string` | `""` | 备注 |

⚠️ **存档兼容红线**：`MetaProgress` 用 `ConfigFile` 存 int 等级（`MetaProgress.cs:63-68`），键名是 `health_level` / `damage_level` / …。表的 `Id` **不要**直接当存档键，否则老玩家的进度会归零。做法：`MetaUpgradeConfig` 加一个 `SaveKey` 字段（默认 `"health_level"`），表只管显示名与数值。

**碎片结算**（`RunSettlement.cs:10-16`，3 个 `const`）建议直接搬进 `ConfigIndexData`（已经是"数值唯一入口"），不必单独立表 —— 它们不是"按类型分的行"，而是三个全局常量。

---

## 4. 通用机制

### 4.1 寻址：`武器1/武器2` 怎么映射到具体资源

**建议：字符串 `Id` + 表内注册表（`Find(id)` / `BySlot(n)`），枚举降级为"快捷键壳"（可选）。**

用户说的"武器1/武器2"，在数据上有两种读法，**两个都要支持**：

| 读法 | 字段 | 用在哪 | 为什么需要 |
|---|---|---|---|
| **槽位**（`武器1`） | `Slot = 1,2,3…` | UI 排序、快捷键 1/2/3、手柄方向键 | 槽位是**表现层顺序**，会随版本调整（新武器插到中间） |
| **身份**（`weapon_1`） | `Id = "weapon_1"` | 存档、`AllowedEnemyIds`、`RequiredWeaponTags`、跨表引用 | 身份**一次定好不再改**，改了就断引用 |

**理由（为什么不用枚举做寻址）**：

1. **枚举是编译期常量** —— `WeaponPresetKind`（`ConfigIndexData.cs:6-11`）现在 3 个值写死在 C# 里，加一把武器必须改 C# 并重新编译，与"数据驱动"的目标直接冲突。
2. **枚举名不适合当槽位** —— 用户要的是"武器1/武器2"这种**位置**概念；枚举名是**语义**概念（`Pistol`）。两者混用会导致"把狙击枪挪到槽位 1"这种纯配置操作变成代码改动。
3. **项目里已经有成功先例**：`StageDefinition.AllowedEnemies` 用字符串匹配（`StageDefinition.cs:42-43`）—— 路子可行，但它用了**可变的显示名**（`DisplayName`）当键，这是它的缺陷（见 F4）。新表用**不可变的 `Id`** 即可。
4. **存档/跨场景传递**：`RunSetup.Weapon`（`RunSetup.cs:16`）与 `MetaProgress` 存的是枚举/整数。存字符串 `Id` 才不怕枚举重排（Godot 的 `ConfigFile` 与 `Variant` 都直接支持 string）。
5. **枚举仍有用**：保留 `WeaponPresetKind` 作为"UI 快捷键 1/2/3"的薄壳，实现只做 `Id` 映射（`Pistol → "weapon_1"`），3 行代码，不破坏 `StartScreen.cs:95-97` 与 `SelfTest.cs:88-89`。这是**兼容优先**的做法，愿意改也可以整个删掉（拍板点 §6.4）。

**统一的表接口**（9 张表共用同一套三个方法）：

```csharp
// Scripts/Config/ConfigTable.cs
using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 配置表读取的统一入口。整个体系只有这一个"读文件"的地方。
/// 写法与 ConfigIndex.cs:25 已经验证过的那一行完全一致，只是抽成了公共方法。
/// </summary>
public static class ConfigTable
{
    /// <summary>表文件不存在 / 类型不符 / 加载失败 → 返回 null，由调用方回退到自身默认值。绝不抛异常。</summary>
    public static T? TryLoad<T>(string path) where T : Resource
    {
        if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path))
        {
            GD.PushWarning($"[Config] 未找到配置表 {path}，使用代码默认值。");
            return null;
        }

        // 不要用 CacheMode.Ignore：运行时要吃引擎缓存，否则每局重读磁盘。
        T? loaded = ResourceLoader.Load<T>(path);
        if (loaded == null)
        {
            GD.PushWarning($"[Config] {path} 存在但不是 {typeof(T).Name}，使用代码默认值。");
        }

        return loaded;
    }

    /// <summary>按槽位号取行（1 起）。越界 / 空表 → null。</summary>
    public static T? BySlot<T>(IReadOnlyList<T>? rows, int slot) where T : class
    {
        if (rows == null) { return null; }
        foreach (T row in rows)
        {
            if (row is IConfigRow withId && withId.Slot == slot) { return row; }
        }
        return null;
    }

    /// <summary>按 Id 取行（字符串比较，区分大小写 —— Id 一律小写 snake_case）。</summary>
    public static T? ById<T>(IReadOnlyList<T>? rows, string id) where T : class
    {
        if (rows == null || string.IsNullOrEmpty(id)) { return null; }
        foreach (T row in rows)
        {
            if (row is IConfigRow withId && withId.Id == id) { return row; }
        }
        return null;
    }
}

/// <summary>所有表行的公共接口：Id + Slot。让上面两个泛型方法只需要写一遍。</summary>
public interface IConfigRow
{
    string Id { get; }
    int Slot { get; }
}
```

> 若不想引入 `IConfigRow` 接口（每个 `Resource` 单独实现一遍），也可以让每个表自己写 `Find/BySlot`（T1 的 `WeaponRegistry` 就是这么写的）。**引入接口的收益是 9 张表共用一套寻址，代价是每个行类多两行 `: IConfigRow`** —— 建议引入。
>
> 引入时，各表的行类声明统一写成（`Id` / `Slot` 是 `[Export]` 属性，天然满足接口）：
>
> ```csharp
> [GlobalClass]
> public partial class RewardConfig : Resource, IConfigRow { /* … */ }
> ```
>
> 注意：**接口里的 `Id` / `Slot` 必须是只读属性**（`string Id { get; }`），而表行上的 `Id` / `Slot` 是 `[Export] public … { get; set; }` —— `get; set;` 可以满足 `get;` 的接口要求，C# 允许这样实现。这是安全的，不需要额外写显式实现。

### 4.2 回退语义：「读不到则使用自身的默认值，有则读取」

这是用户需求里最硬的一条，实现分**三个层次**，每层都有明确的落点：

| 层次 | 触发条件 | 实现 | 已有先例 |
|---|---|---|---|
| **① 整行缺失** | 某一行文件（`Config/Tables/Weapons/wpn_pistol.tres`）被删 → `ConfigIndex.tres` 的 `Weapons` 数组里那一项变成 `null`；或者整个数组为空（`ConfigIndex.tres` 读不到时） | 索引里查不到 → 调用方保留组件自身的 `[Export]` 默认值，并打一条 `GD.PushWarning` | **`ConfigIndex.cs:25-32`** —— 项目里已经在用这个模式（缺 `ConfigIndex.tres` 时用代码默认值） |
| **② 表里没这一行** | 数组里有别的行，但没有 `wpn_pistol` | `WeaponRegistry.Find("wpn_pistol")` 返回 `null` → 回退到"槽位 1"或组件默认值（由调用方决定） | — |
| **③ 行里没填这个字段** | 行存在，但某字段没写 | `.tres` 缺字段时 Godot 自动用 `[Export]` 初值（这是 Godot 原生行为，[config.md](config.md):8 已写明） | `ConfigIndex.tres` 现有 74 个字段全部显式写出，但缺了也不会崩 |

**可直接用的 C# 写法**（把这个模式从 `ConfigIndex.cs` 抽出来，成为全项目统一写法）：

```csharp
// 唯一的读取点：查不到就是 null（索引在 ConfigIndex._Ready 里建好，见 §3 T1）
WeaponConfig? row = WeaponRegistry.Find(id);

// 套用：有行就套，没有就什么都不做 —— 组件保留自己 Inspector 里的值
if (row != null && _weapon != null)
{
    row.ApplyTo(_weapon);
}
else
{
    GameLog.Write("Flow", $"武器表没有 {id}，使用 {_weapon?.Name} 自身默认值。");
}

// 调用方原有的默认值继续生效（这就是"自身的默认值"）
// 例：Weapon.cs:15-49 的 [Export] 初值 / Scenes/Weapons/Pistol.tscn 里写的值
```

**替换掉的两段硬编码**（迁移完成后删除）：

```csharp
// 现在（RunController.cs:396-412）：11 行三元链
bool sniper = kind == WeaponPresetKind.Sniper;
bool rifle = kind == WeaponPresetKind.Rifle;
_weapon.Damage = rifle ? config.RifleDamage : sniper ? config.SniperDamage : config.PistolDamage;
// …还有 10 行

// 迁移后：读表 → 套用 → 读不到就什么都不做
if (row != null && _weapon != null) { row.ApplyTo(_weapon); }
```

**三条纪律**（写进代码注释，避免后人再走回头路）：

1. **绝不抛异常、绝不 `GD.PushError` 后继续** —— 配置缺失是**正常情况**（新克隆的仓库、策划删了一行、发布版删掉调试表），一律 `PushWarning` + 回退。
2. **回退值只能有一个来源**：组件/A 类上的 `[Export]` 初值。不要再写第二个"兜底表"（`RunController.DefaultStages()` 就是反面教材，见 §1.4-①）。
3. **回退要留痕**：`GameLog.Write("Flow", "配置表 X 未找到，使用默认值")`，这样自测日志里能一眼看到到底走的是表还是默认值 —— 这是"证明迁移没破坏功能"的关键证据。

### 4.3 键值对组：怎么在 Inspector 里编辑"任意键值对"

**建议：`ParamEntry : Resource` 数组（`ParamEntry[]`），不用泛型字典。**

| 方案 | Inspector 能编辑？ | C# 侧好用？ | 结论 |
|---|---|---|---|
| **`ParamEntry[]`（`ParamEntry : Resource`）** | ✅ **本项目已验证**：`ConfigIndexData.Stages` 就是 `StageDefinition[]`，`ConfigIndex.tres:114` 存成 `Array[Resource]([SubResource(...)])`，Inspector 里是带 `+`/`-` 的数组，每项可展开成 `Key` / `Value` / `Note` 三栏 | 好：`foreach` + `entry.AsFloat()` | **主方案** |
| `Godot.Collections.Array<ParamEntry>` | ✅ 同上（`Godot.Collections.Array<T>` 有 `ctor(T[])`，已验证） | 略啰嗦（`Add`/索引器） | 等价替代 |
| `Godot.Collections.Dictionary<string, Variant>` | ⚠ **需验证**：`Godot.Collections.Dictionary<TKey,TValue>` 的两个泛型参数都标了 `[MustBeVariant]`（已反射确认），结构上是给导出用的；但 4.7 的 C# 源生成器是否为它产出可用的 `PropertyInfo`、Inspector 里的呈现是否好用，我**没有实测**。风险点见 §7 | 中：`dict["Damage"]` 返回 `Variant`，仍要 `AsSingle()` | 备选（想少写一个类时用） |
| `System.Collections.Generic.Dictionary<string, float>` | ❌ **不可导出**（Godot 的 C# 导出不支持 BCL 泛型集合） | — | 排除 |
| `float?` / `int?`（可空） | ❌ **不可导出**（Nullable 不是 Variant 兼容类型） | — | 排除，这就是"能编辑的部分要留字段级开关"的原因 |

```csharp
// Scripts/Config/ParamEntry.cs
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 通用「键值对」条目：一个键 + 一个值 + 一句说明。
/// 用在增幅项的额外参数、技能/敌人的可选参数覆盖、关卡的额外配置。
/// </summary>
[GlobalClass]
public partial class ParamEntry : Resource
{
    /// <summary>参数名。与组件 [Export] 属性名一致时可以被 Set() 直接套用（见 AbilityConfig.ApplyTo）。</summary>
    [Export] public string Key { get; set; } = string.Empty;

    /// <summary>值。用 Variant 才能既放数字又放字符串/资源路径（见下方"保底方案"）。</summary>
    [Export] public Variant Value { get; set; } = 0f;

    /// <summary>这一条是干什么的（给策划看，也可用于 {Key} 文本模板与 Note 自动生成）。</summary>
    [Export] public string Note { get; set; } = string.Empty;

    public float AsFloat(float fallback = 0f)
        => Value.VariantType is Variant.Type.Float or Variant.Type.Int ? Value.AsSingle() : fallback;

    public int AsInt(int fallback = 0)
        => Value.VariantType is Variant.Type.Float or Variant.Type.Int ? Value.AsInt32() : fallback;

    public bool AsBool(bool fallback = false)
        => Value.VariantType == Variant.Type.Bool ? Value.AsBool() : fallback;

    public string AsText(string fallback = "")
        => Value.VariantType is Variant.Type.String or Variant.Type.StringName ? Value.AsString() : fallback;
}
```

**取舍说明**（写给以后想改的人）：

- **为什么用 `Variant` 而不是 `float`**：键值对要能表达"数值"（`1.15`）、"字符串"（`"Burn"`、`"res://Scenes/Summons/Drone.tscn"`）、"布尔"（`true`）。用 `float` 就得为字符串另开一个 `ParamText` 数组，用两张表凑一件事。`Variant` 的读取代价是必须写 `AsFloat()` 这类转换 —— 由 `ParamEntry` 的 4 个访问器兜住，业务代码不会散落 `AsXxx()`。
- **为什么不用 `Godot.Collections.Dictionary`**：字典在 Inspector 里只能编辑 `Variant → Variant`，**没有"这一条是干什么的"说明栏**，而用户明确要"额外一个文本类参数项来对该值进行说明或指向"。数组 + `Note` 字段能表达这件事，字典不能。另外数组能一眼看出顺序（哪一条先套用），字典不能。
- **保底方案（若 `[Export] Variant` 在 4.7 有问题，见 §7）**：`ParamEntry` 改成三个绝对可导出的字段 ——
  ```csharp
  public enum ParamValueKind { Float, Int, Bool, Text, ResourcePath }

  [Export] public ParamValueKind Kind { get; set; } = ParamValueKind.Float;
  [Export] public float Number { get; set; }
  [Export] public string Text { get; set; } = string.Empty;
  ```
  `float` / `int` / `bool` / `string` / 枚举都是 Godot C# 一定支持导出的类型，代价是每个值多占两个字段的空间。

### 4.4 文本说明字段：怎么支持引用参数

用户要的是「额外一个**文本类参数项**来对该值进行说明或指向」。设计成**模板字符串 + `{Key}` 占位符**，键从行自身的字段与 `Params` 里找：

```csharp
// Scripts/Config/ConfigText.cs
using System.Globalization;
using System.Text;
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 配置表里的文本模板：把 "伤害 +{Value}%" 里的 {Key} 换成行里的实际值。
/// 规则：
///   {字段名}      优先取 RewardConfig / WeaponConfig 自己的 [Export] 属性（走 GodotObject.Get）
///   {Params 的键} 再取 Params 里同名 Key 的值
///   都找不到 → 原样保留 {Key} 并打一条警告（不抛异常，不显示空白）
/// 刻意**不做表达式求值**（没有 eval、没有脚本）—— 配置表只放数据，不放逻辑。
/// </summary>
public static class ConfigText
{
    public static string Format(string template, GodotObject source, System.Collections.Generic.IReadOnlyList<ParamEntry>? extra = null)
    {
        if (string.IsNullOrEmpty(template)) { return string.Empty; }

        var builder = new StringBuilder(template.Length + 16);
        int i = 0;
        while (i < template.Length)
        {
            int open = template.IndexOf('{', i);
            if (open < 0)
            {
                builder.Append(template, i, template.Length - i);
                break;
            }

            int close = template.IndexOf('}', open + 1);
            if (close < 0)
            {
                builder.Append(template, i, template.Length - i);
                break;
            }

            builder.Append(template, i, open - i);
            string key = template.Substring(open + 1, close - open - 1);

            if (TryResolve(source, extra, key, out string text))
            {
                builder.Append(text);
            }
            else
            {
                GD.PushWarning($"[ConfigText] 模板里的 {{{key}}} 在配置行里找不到，原样保留。");
                builder.Append('{').Append(key).Append('}');
            }

            i = close + 1;
        }

        return builder.ToString();
    }

    private static bool TryResolve(GodotObject source, System.Collections.Generic.IReadOnlyList<ParamEntry>? extra, string key, out string text)
    {
        // 1) 先找 Params（键值对组里的 Note 也能当文本用）
        if (extra != null)
        {
            foreach (ParamEntry entry in extra)
            {
                if (entry.Key == key)
                {
                    text = entry.AsText(entry.AsFloat().ToString(CultureInfo.InvariantCulture));
                    return true;
                }
            }
        }

        // 2) 再找行自身的 [Export] 属性（引擎自带 Get，不需要维护映射）
        Variant value = source.Get(key);
        if (value.VariantType != Variant.Type.Nil)
        {
            text = value.VariantType switch
            {
                Variant.Type.Float => value.AsSingle().ToString("0.##", CultureInfo.InvariantCulture),
                Variant.Type.Int => value.AsInt32().ToString(CultureInfo.InvariantCulture),
                Variant.Type.Bool => value.AsBool() ? "是" : "否",
                Variant.Type.String or Variant.Type.StringName => value.AsString(),
                _ => value.ToString(),
            };
            return true;
        }

        text = string.Empty;
        return false;
    }
}
```

**用法与约定**：

| 模板示例 | 渲染结果 | 说明 |
|---|---|---|
| `"伤害 +{Value}%"` | `伤害 +1.15%` ❌ | **陷阱**：`Value = 1.15` 是乘数不是百分比。要 `+15%` 得填 `Value = 1.15` 并写模板 `"伤害 +{ValuePercent}%"`，用一个额外的 `Params` 条目 `{Key:"ValuePercent", Value:15}` 来表达"给人看的数" |
| `"伤害 +15%"` | `伤害 +15%` | **推荐**：玩家文案与数值解耦，策划直接写结果，模板里不放占位符 |
| `"移动速度 +{Percent}%"` + `Params={Percent:10}` | `移动速度 +10%` | 数值与文案都在表里，改数值要改两处 —— **不推荐** |
| `"击杀敌人时对附近敌人放电（{Range} 米 / {Jumps} 跳）"` + `Params={Range:7},{Jumps:3}` | `…（7 米 / 3 跳）` | ✅ **推荐**：参数多、文案里要复述参数时用，改 `Params` 文案自动跟着变 |
| `"子弹可多穿透 {Value} 个目标"` | `子弹可多穿透 1 个目标` | ✅ 通用项直接当占位符用，零额外字段 |

**结论与建议**：

1. **占位符只用于"参数多、文案要复述参数"的情况**（连锁闪电、爆裂弹头）。单参数增幅项（13 条）**直接写死文案**（`"伤害 +15%"`），比 `{Value}` 更好读也更好改 —— 反正改数值本来就要动这一行。
2. **`Note` 与 `DescriptionTemplate` 是两个字段，不要合并**：`Note` 是"机制/增幅说明"（给策划和程序看，可以写得很技术，如 `"命中后产生 3.5 米爆炸，伤害为本体的 60%"`），`DescriptionTemplate` 是玩家可见文案。现在 `RewardDefinition.Description`（`RewardDefinition.cs:33`）一个字段兼两用，是策划想写清楚就会污染玩家界面的根源。
3. **不做表达式求值**（没有 `eval`、没有内嵌脚本）。原因见拍板点 §6.1。
4. ⚠️ **玩家可见文案改动要同步更新 `docs/`**（[todo-handoff.md](todo-handoff.md):53 的铁律）。文案搬进表之后，这条约定要补一句："表里的 `DescriptionTemplate` / `DisplayName` 改动同样要同步 `docs/`"，否则 `rewards.md` / `config.md` 会慢慢失真。

#### 4.4.1 ✅ 2026-10 补：`{字段:格式}` 格式说明符（**曾经静默失效，已修**）

**症状**：三个角色行 / 三把武器行写的 `{MaxHealth:0}` / `{FireInterval:0.00}` 这类**带格式说明符**的占位符
在界面上**原样打印**（玩家看到 `{MaxHealth:0} 生命`），而且每处还打一条 `[ConfigText] 模板里的 {MaxHealth:0} 在配置行里找不到` 的 WARNING —— 关卡 Configure 步与基地配置台都会出现。

**原因**：`ConfigText.Format` 把 `{` 与 `}` 之间的整段当**字段名**去 `Get()`，`:0` 成了字段名的一部分，自然找不到。

**修法（选"改代码"而不是"改三条 `.tres` 文案"）**：`Format` 现在会把冒号切开 —— `{字段:格式}` 的**格式串交给 .NET 数字格式化**（`double.ToString(格式, InvariantCulture)`），
且：

- **只对数字字段（Float / Int）生效**；字符串 / 布尔 / 资源字段**忽略**冒号后的部分（照旧原样输出）；
- 格式串写错（`FormatException`）→ **退回默认写法，不抛异常、不多打一条警告**（模板是给人写的，写错格式只该影响这一处显示）；
- 没有格式串时**输出与以前逐字节一致**（同一个 `ToString` 调用）。

> **为什么是 ① 而不是 ②**：`.tres` 里已经有 8 处（角色 3 + 武器 3 + 局外 2）在用格式说明符，改文案只能治这 8 处；
> 以后策划照着 `{Damage:0}` 写新行还会再犯。修 `Format` 是**一处改动让后面所有写法都成立**，也是 §4.4
> "模板字符串"这个设计的应有之义（说明符本来就是格式语法的一部分，不是字段名的一部分）。
>
> 证据（一次性探针，跑完即删）：12 条模板全部正确渲染，例：`{MaxHealth:0}` → `250`、`{FireInterval:0.00}` → `0.12`、
> `{PerLevel:0}` → `10`；日志里 `原样保留` 的 WARNING **0 条**。详见 [config.md](config.md) 的本轮更新记录。

### 4.5 跨表关联：武器 ↔ 它的升级池（**场景 B 的核心**）

> 用户原话：「我要调整**某个武器的基础参数以及其与其对应的升级池里的增幅参数**」——关键要求是**能顺藤摸瓜**，不是在一张大表里用肉眼找。

#### 4.5.0 ✅ 2026-10 拍板并已实施（下面 4.5.2–4.5.4 是**被取代**的推荐方案）

用户明确要求的是**正向指**：「武器的配置表里确实是需要有指向对应池表的字段，而**对应池表则不指向其对应武器**，
而**池表里则是一个选项占用一栏**」。所以最终落地的形态是（实测与回退验证见 §5 阶段 4）：

```
WeaponConfig（武器表 3 行）
  └─ Pool ─────► UpgradePoolConfig（Config/Tables/UpgradePools/pool_*.tres，3 行 = 一行一个池）
                     ▲
RewardConfig（增幅项表 19 行 = 一行一个选项）
  └─ Pool ───────────┘（留空 = 落到"标签匹配 / 通用"两层）
```

| 决策点 | 结论与依据 |
|---|---|
| 关联方向 | **武器 → 池**（正向）。池行**不**反指武器，也**不**列选项 |
| "一个选项占一栏"怎么落 | **一行 = 一个选项**（19 行不动），"属于哪个池"是它上面的**一个引用列**。依据：用户原话在 [config-tooling-yard.md](config-tooling-yard.md) §4.3 被解读为"**一栏一个标量**"，而"**一栏里放多个选项**（`Array`）"恰恰是编辑器拿不到的形态 —— 所以成员关系**不能**写成池行上的数组 |
| 成员关系的唯一真源 | **选项行的 `Pool` 列**（池行不重复列一遍 → 不会出现 §1.4-① 那种双真源漂移） |
| 池行的职责 | **身份锚点**：让"武器→池"是两个 `.tres` 之间的**资源引用**（删/改名时引用置 null 可检测），而不是拼字符串 |
| 三级回退 | 保留 §4.5.3 的**分层**，但第一层从"精确武器"换成"**精确池**"：`RewardDefinition.AllowsWeapon` 里 `ApplicablePoolIds` 非空 → 只认 `weapon.PoolId` |
| 纯增量 | 19 条增幅项的**数值与字段一个都没改**（新列 `Pool` 全空 = 走②③两层），`RewardPool` / `RunBuild` / `RunController` **一行没改** |
| 死字段 `PoolTags` | ✅ **删除**（见下 4.5.6）—— 它的语义被"专属池引用 + 武器标签"两张列取代 |

#### 4.5.1 先说现状：这个关联**现在不存在**（读代码后的结论）

| 环节 | 代码 | 事实 |
|---|---|---|
| 池子怎么建 | `RewardPool.Initialize(Weapon weapon)`（`RewardPool.cs:18-28`）：遍历 `RewardCatalog.All`，保留 `definition.AllowsWeapon(weapon)` | 建一次就缓存（`RunController._poolReady`），本局不再重建 |
| 武器怎么判 | `RewardDefinition.AllowsWeapon`（`RewardDefinition.cs:59-70`）：逐个 `RequiredWeaponTags` 查 `weapon.HasTag(tag)` | **只按标签匹配** |
| 标签从哪来 | `Weapon.HasTag(tag)`（`Weapon.cs:93`）查 `AttackTags`（`Weapon.cs:28`，默认 `["Projectile","Gun","Physical"]`） | 三把武器**共用同一个 `Pistol.tscn`**（`RunController.EquipWeapon` 只改数值、不换节点，`RunController.cs:383-386`），`AttackTags` 也不改 |
| 进化项怎么声明 | `RewardCatalog.Evolution(..., "Gun", ...)` → `RequiredWeaponTags = new[] { "Gun" }`（`RewardCatalog.cs:104-116`） | **4 条进化全部只声明 `"Gun"`** |

**结论（这是用户必须知道的一条）**：

> **现在没有"某把武器的升级池"这个粒度。** 三把枪（手枪 / 狙击 / 步枪）的 `AttackTags` 都含 `"Gun"`，所以它们**共用同一个池**：4 条进化（双管齐下 / 穿甲弹 / 跳弹 / 爆裂弹头）对三把枪同时可用，10 条强化与 5 条异化对**所有**武器可用（它们 `RequiredWeaponTags` 为空 = `AllowsWeapon` 恒真）。
> 也就是说：**"调某武器的升级池"这个动作今天无处可下手** —— 要么动全部武器，要么改代码。

**顺带的第二个发现**：`RewardDefinition.PoolTags`（`RewardDefinition.cs:38`）在 `RewardCatalog.cs:98,113,126` 被赋值，但 **`RewardPool` 里从未读取过它** —— 与 `Weight`、`ConflictsWith` 一样是死字段（§1.4 ⑤⑥）。
> ✅ **阶段 4 已处置**：`PoolTags` 删除、`ConflictsWith` 接上，见 §4.5.6 与 §5 阶段 4。

#### 4.5.2 ❌ 推荐方案（**未采用**）：**反向指 + 资源引用数组**

```
T1 武器表（行）                          T3 增幅项表（行）
wpn_pistol.tres                          upg_evo_ap.tres
  Id = "wpn_pistol"          ◀──────────   ApplicableWeapons = [wpn_pistol, wpn_sniper]
  Damage = 24 …                              Id = "upg_evo_ap"  …
```

`RewardConfig.ApplicableWeapons : WeaponConfig[]` —— **每一条增幅项自己声明它属于哪几把武器**。

**为什么是"反向指"（增幅项 → 武器）而不是"正向指"（武器 → 升级池数组）**：

| 维度 | 正向指（`WeaponConfig.UpgradePool : RewardConfig[]`） | ⭐ 反向指（`RewardConfig.ApplicableWeapons : WeaponConfig[]`） |
|---|---|---|
| 加一条增幅项 | 要**同时**打开武器表、往数组里加引用（两个文件） | **只碰一个文件**（新建 `upg_xxx.tres` 并填一列）—— 与"一行一个 `.tres`"的编辑流完全一致 |
| 一条增幅项适用于多把武器 | 要在 N 个武器行里各维护一次（N 处重复，容易漏） | **一处**写 N 个引用 |
| `Edit Resources as Table` 里的编辑体验 | 数组列要"单独打开成另一张表"才编辑得了（[config-tooling.md](config-tooling.md) 提到的原生能力，但多一跳） | **就是增幅项表里的一列**，可以在该列上多选单元格批量改 |
| "找这把武器的升级池"（顺藤摸瓜） | 打开武器行，直接看到池子 | 在增幅项表里**按 `ApplicableWeapons` 列筛选/排序**一次看全 |
| 悬空引用风险 | 低（引用的行被删 → Godot 自动置 null） | **低**（同样因为是 `Resource` 引用而不是字符串 id）✅ |
| 循环引用风险 | 无（方向单一：增幅项 → 武器） | 无 |

**为什么用 `WeaponConfig[]` 资源引用，而不是 `string[]` 武器 id**：

1. **不会悬空**：武器行文件被删/改名时，Godot 把引用置成 `null`（可检测），而字符串 id 会**静默**指向不存在的武器。
2. **编辑不易错**：Inspector 里是资源选择器（拖/点选），不是手打 id；也能被表插件按"引用"处理。
3. **代价**：`RewardConfig.cs` 需要 `using`/`namespace` 到 `ActRoguelike.Config`（同命名空间，零成本）；且它把"武器"与"增幅项"两个行类耦合在一起 —— **这是可接受的**，因为它们本来就在同一套配置体系里，而且方向单一（`WeaponConfig` 不引用 `RewardConfig`，不会循环）。

#### 4.5.3 三级回退（与"读不到则使用自身的默认值"同源）

| 级别 | 条件 | 语义 | 现有内容归属 |
|---|---|---|---|
| **① 精确武器** | `ApplicableWeapons` 非空且包含当前武器 | "这条只属于这几把" | 以后新加的**武器专属**进化（如"狙击枪专属：过载充能"） |
| **② 武器类别** | `ApplicableWeapons` 为空，但 `RequiredWeaponTags` 命中 `weapon.AttackTags` | "所有枪类都能拿" | **现有 4 条进化**（`RequiredWeaponTags = ["Gun"]`）—— 行为与今天**完全一致**，零回归 |
| **③ 通用池** | 两者都为空 | "所有武器都能拿" | **现有 10 条强化 + 5 条异化**（`RequiredWeaponTags` 为空）—— 行为与今天一致 |

> 这个分层的好处：**迁移是纯增量的**。今天 19 条增幅项一条都不用改字段值（①②③ 自然落到原来的行为），新加的武器专属项才填 `ApplicableWeapons`。这就是"读不到则使用自身的默认值"在**关联**层面的同一套思路。

#### 4.5.4 运行时怎么按当前武器筛（具体改动点）

**障碍**：`Weapon` 组件**不知道自己对应哪一行表**（`Weapon.cs` 里没有任何 id/kind 字段；`RunController._weaponKind` 在 `RunController` 手上）。所以要先补这一环：

```csharp
// ① Weapon.cs 加一个身份字段（表套用时写入；不套表时留空 → 走②③回退）
[Export] public string ConfigId { get; set; } = string.Empty;

// ② WeaponConfig.ApplyTo() 里写进去
public void ApplyTo(Weapons.Weapon weapon)
{
    weapon.ConfigId = Id;      // ← 新增这一行
    weapon.Damage = Damage;
    // …其余字段
}

// ③ RewardDefinition 增加"精确武器"信息（由 RewardTable 编译时灌进去）
public string[] ApplicableWeaponIds { get; init; } = Array.Empty<string>();

// ④ AllowsWeapon 改成三级回退
public bool AllowsWeapon(Weapon weapon)
{
    // ① 精确武器：非空时以它为准（这一条只属于这几把）
    if (ApplicableWeaponIds.Length > 0)
    {
        return !string.IsNullOrEmpty(weapon.ConfigId)
            && Array.IndexOf(ApplicableWeaponIds, weapon.ConfigId) >= 0;
    }

    // ② 武器类别：现有的 RequiredWeaponTags 行为，一字不改
    foreach (string tag in RequiredWeaponTags)
    {
        if (!weapon.HasTag(tag)) { return false; }
    }

    // ③ 通用池：两者都为空时恒真（现状）
    return true;
}
```

`RewardTable.Rebuild` 编译每一行时，把 `ApplicableWeapons` 的 `Id` 收集成 `ApplicableWeaponIds`：

```csharp
var ids = new List<string>(captured.ApplicableWeapons.Length);
foreach (Config.WeaponConfig weaponRow in captured.ApplicableWeapons)
{
    if (weaponRow != null && !string.IsNullOrEmpty(weaponRow.Id)) { ids.Add(weaponRow.Id); }
}
// …ApplicableWeaponIds = ids.ToArray()
```

> **为什么不直接把 `WeaponConfig[]` 传给 `RewardDefinition`**：`RewardDefinition` 在 `ActRoguelike.Rewards`，让它去认识 `Config.WeaponConfig` 会把"运行期视图"重新绑回磁盘类型（§2.2 的分层就白做了）。编译成 `string[]` 是最小耦合；而"悬空引用"的问题在**磁盘侧**已经被 `Resource` 引用解决了（`null` 直接过滤掉）。

**`RewardPool` 不用改**：它只调 `definition.AllowsWeapon(weapon)`（`RewardPool.cs:23`），签名不变。

#### 4.5.5 "顺藤摸瓜"在表格工具里的具体操作

装了 `Edit Resources as Table` 之后，打开 `res://Config/Tables/Upgrades/`：

| 想做的事 | 操作 |
|---|---|
| 看"手枪的升级池有哪些" | 在 `ApplicableWeapons` 列上**筛选**（或按该列排序），所有属于手枪的行聚在一起 |
| 改"手枪升级池里全部增幅的强度" | 筛选出这几行 → 在 `Value` 列上**多选单元格** → 批量乘 / 批量加（插件的原生能力） |
| 一条新进化只给狙击枪 | 新建一行 `.tres`，`ApplicableWeapons` 里放 `wpn_sniper` 一个引用 |
| 查有没有悬空引用 | 见下面的自测输出（`ApplicableWeaponIds` 里出现不存在的 id 时打 warning） |

**必须配一条只读校验**（这是"顺藤摸瓜"不腐烂的保险丝）—— 加进 `SelfTest` 或一个调试方法：

```csharp
/// <summary>仅供自测/排查：打印每把武器 → 它的升级池（三级回退各自的条数）。</summary>
public static string DescribePools()
{
    var sb = new System.Text.StringBuilder();
    foreach (Config.WeaponConfig weapon in ConfigIndex.Settings.Weapons)
    {
        int exact = 0, byTag = 0, generic = 0;
        foreach (RewardDefinition d in RewardCatalog.All)
        {
            if (d.ApplicableWeaponIds.Length > 0)
            {
                if (System.Array.IndexOf(d.ApplicableWeaponIds, weapon.Id) >= 0) { exact++; }
            }
            else if (d.RequiredWeaponTags.Length > 0) { byTag++; }
            else { generic++; }
        }
        sb.AppendLine($"{weapon.Id}({weapon.DisplayName})：专属 {exact} / 类别 {byTag} / 通用 {generic}");
    }
    return sb.ToString();
}
```

迁移完成后，这条输出与 [rewards.md](rewards.md):79-86 的"枪手 18 / 召唤 19"一起，就是"关联没坏"的硬证据。
> ✅ **已按等价形态落地（阶段 4）**：自测里新增 `配置表比对·升级池：武器 PoolId「pool_pistol」… 专属层 0 条（命中 0）· 类别层 4 条（命中 4）· 通用层 15 条`，
> 判据是 `RewardDefinition.ApplicablePoolIds` + `AllowsWeapon`（池版，不再是武器版）。

#### 4.5.6 `PoolTags` 这个死字段怎么处理 —— ✅ **已拍板：删掉（阶段 4 落地）**

**最终做法**：**删除**，并用两张**新列**取代它的语义 —— ① `RewardConfig.Pool`（专属池引用，第一层）、
② 原有的 `RequiredWeaponTags`（武器类别，第二层）。删除范围：`RewardConfig.PoolTags`、
`RewardDefinition.PoolTags`、`RewardEffectRegistry.Compile` 的赋值、`RewardCatalog` 三个工厂的 `tag` 参数
与 15 个调用点、以及 **19 个 `.tres` 行文件里的 `PoolTags = …` 那一行**。

> 下面是当时的三个候选（保留作为决策记录）。**没有采纳第一条**："池"在这个项目里的粒度**不是**自由标签，
> 而是**每把武器一个池**（用户原话），所以用资源引用比用 `string` 标签更贴切、也不会拼错。

| 选项 | 说明 |
|---|---|
| **改名 `UpgradePoolId`（`string`）用起来** | 表达"这条增幅属于哪个**逻辑池**"（如 `"pool_gun_core"` / `"pool_high_risk"`）。可以支持"一次抽奖只从某个池里抽"（`RewardService.FilterFor` 现在只能按 `RewardKind` 过滤，`RewardService.cs:16-24`） |
| **当作标签真的用起来** | 让 `RewardPool.Roll` 接受 `string[] requiredPoolTags` 参数并过滤。⚠️ 那么它和 `ApplicableWeapons` / `RequiredWeaponTags` 就有三重过滤语义，容易搞混 |
| ⭐ **直接删掉**（连同 `RewardCatalog.cs:98,113,126` 的赋值） | **← 实际采用**。死字段会误导后来人以为它在生效；"池"改由 `UpgradePoolConfig` 资源引用表达 |

---

## 5. 迁移路径

**总原则**：一张表一次；每阶段结束时**编译 0 warning / 0 error + 跑一次自测 + 逐项比对日志**；迁移期**保留代码里的旧目录作为兜底**（`RewardTable.Instance == null` 时用 `_fallback`），下个阶段再删。这样任何一步出问题都能立刻回退到"旧目录 + 表未生效"的安全状态。

### 阶段 0：铺地基（不改任何现有行为）✅ **已完成（2026-10）**

| 做什么 | 新增文件 | 状态 |
|---|---|---|
| 加 `ConfigTable`（§4.1）、`ParamEntry`（§4.3）、`ConfigText`（§4.4） | `Scripts/Config/ConfigTable.cs`、`ParamEntry.cs`、`ConfigText.cs` | ✅ 已落地（含 `IConfigRow`） |
| 抽公共 `WeightedPick`（§3 T9），**rng 由调用方传入** | `Scripts/Config/WeightedPick.cs` | ✅ 已落地，并**已接上两处调用方**（`RewardPool.Roll` 传自己的实例 RNG、`BossPerkCatalog.Roll` 传自己的静态 RNG）—— 自测压力/Pressure 基线逐位不变 |
| `ConfigIndexData` 加 4 行 `[Export] ...Table?` 字段（全部默认 `null`） | 改 `ConfigIndexData.cs` | ⏸ **未做**：目前只有关卡表一张，它的索引是**已有的** `Stages` 数组，加空字段等于死配置。等 T1/T3 落地时再加 |
| 写一个 5 行的实验 `.tres`，确认 `[Export] Variant` 与 `ParamEntry[]` 在 4.7 编辑器里能编辑、能存盘 | `Config/_probe.tres`（验证完删掉） | ✅ 探针已做并删除，结论写进 **§7.2**：`ExtResource` 数组**已验证可用**；另外补充了一条设计文档里没写的工作流坑（Inspector 拖拽填充 C# 资源数组无效） |

**验证**（实测，2026-10）：

- `& 'F:\Godot项目\项目a\build.ps1' -v minimal` → **0 error**；本项目自己改动的文件 **0 warning**（唯一的 warning 来自无关的第三方插件 `addons/yard/Registry.cs`）。
- 自测（`selftest.flag` + gdmcp CLI）：行为与改动前逐项一致 —— `节点布置：启用 10 个（高 2 / 中 5 / 低 3）`、`节点间距：最小 9.74`、`Pressure 累积：+187.5 → 1 / +225 → 3`、`端到端：按 F 之后 奖励界面打开 = True`。

**收益**：这一步之后，"读表 + 回退"就是一个 3 行的固定写法了。

### 阶段 1：关卡表（T6）—— **建议先做这张** ✅ **已完成（2026-10）**

| 为什么是它 | 依据 |
|---|---|
| 类**已经存在**，15 个字段一个都不用改 | `StageDefinition.cs` 已经是 `[GlobalClass] partial class : Resource`（本次只**新增**了 §7.4 的 Pressure 字段，老字段一字未动） |
| 改动点只有 **2 处** | `RunController.cs` 的 `_stages = ResolveStages()`（原来两处 `ConfigIndex.Settings.Stages.Length > 0 ? ... : DefaultStages()`） |
| **顺手修掉一个真 bug** | ✅ `DefaultStages()` 已删除，§1.4-① 的 5 处漂移随之消失（实测日志：`stg_inner … 敌伤×1.1 Boss伤×1.1 允许敌人[6]`） |
| 有**现成的自动验证** | ✅ 自测日志新增 `关卡表：N 关（来源：表/默认值）| <每关摘要>` 与 `Stage 表行：<Id>…Pressure 规则…` |
| 不碰任何业务逻辑 | ✅ `StageController` / `EncounterController` 的流程未改，只把 Pressure 的取值从"只读 ConfigIndex"改成"行优先、留 0 回退 ConfigIndex" |
| **不依赖 §7.2 那条未验证的 `ExtResource` 数组写法** | ✅ 已实测通过（见 §7.2），所以第 5 步"每关一个文件"**已经做了** |

**实际落地**（比原计划的 1–4 步多做了第 5 步）：

1. `Config/Tables/Stages/` 下新建 `stg_outer.tres` / `stg_inner.tres` / `stg_core.tres`（**一行一关**，字段值逐条照抄 `ConfigIndex.tres` 原来的 3 个 `sub_resource`）。
2. `ConfigIndex.tres` 里 3 个 `[sub_resource]` 删除，改成 4 个 `[ext_resource]`（ConfigIndexData 脚本 + 3 个行文件），`Stages = [ExtResource(...), ...]`。
   **写法取自引擎自己的输出**（探针里让 Godot 的 `ResourceSaver` 写过一遍再照抄），不是手抄文档示例。
3. `RunController.DefaultStages()` 删除，改为 `ResolveStages()`：表为空 → `new[] { new StageDefinition() }` + 一条 `GD.PushWarning` + 一条 GameLog（把"ConfigIndex.tres 是否加载成功"也打出来）。
4. 日志：开局一行 `关卡表：3 关（来源：表）| stg_outer Stage 1 · 外围 允许敌人[3] 敌伤×1 …`。
5. `StageDefinition` 补 §7.4 的 Pressure 字段（`Id` + 6 个数值 + `PressureUpgradeTags`），`StageController.AddPressureForNode` 改为读行、留 0 回退 ConfigIndex。

**验证（实测证据）**：

| 场景 | 结果 |
|---|---|
| 正常（表在） | `关卡表：3 关（来源：表）`，Stage2 的 5 个漂移字段全部正确；自测全程通过；Pressure 与改动前**逐位一致**（+187.5 → 1、+225 → 3） |
| **主动回退验证：把 `Stages` 清空**（`Stages = []`） | 日志 `关卡表：0 关（来源：默认值）…`；游戏正常跑完自测；Pressure 仍与改动前一致（证明"留 0 → 用 ConfigIndex 全局默认值"这条回退真的生效） |
| **主动回退验证：把 `stg_inner.tres` 改名** | 日志 `关卡表：0 关（来源：默认值）…（ConfigIndex.tres 未加载（全部数值回退到代码默认值），玩家生命 160）`；游戏仍能跑完自测 |

> ⚠️ **最后一行是本阶段挖出来的新风险，比设计文档的假设更严重**：只要 `ConfigIndex.tres` 引用的**任何一个行文件**缺失/改名，
> Godot 的资源加载会**整份 `ConfigIndex.tres` 失败**（不是只丢那一行），于是**所有**数值静默回到代码默认值
> （实测玩家生命 250 → 160）。回退是"安全的"（只有 warning、能正常玩、日志留痕），但比"只影响缺的那一行"粗得多。
> **操作纪律**：不要删/改被 `ConfigIndex.tres` 引用的行文件；确实要下线一行，就把 `[ext_resource]` 与数组里的那一项**一起**删。
> 若以后要让"缺一行"只影响那一行，唯一干净的做法是把索引从"资源引用数组"换成**路径字符串数组** + `ConfigTable.TryLoad` 逐行加载
> —— 代价是 §6.2 里放弃的那两条（编辑器里看不到引用、导出可能漏文件）。**这条需要拍板，本次没有改**。

> ⚠️ **第二条工作流坑（设计文档原先假设它可以，实测不行）**：在编辑器 Inspector 里把若干 `.tres` **拖进** C# 的
> `[Export] StageDefinition[]` 数组槽，**不会写进去**（`Object::set` / `UndoRedo.add_do_property` 对"已落盘的资源"整条赋值静默失效，
> 属性保持原值）。探针里 5 种写法全部复现。**可行且已验证的写法是直接把数组写成文本**：
> `Stages = [ExtResource("2_stg_outer"), …]` + 顶部 `[ext_resource type="Resource" path="…" id="…"]`（只写 `path=`，不写 `uid=`，符合本项目约定）。
> 这也说明：**将来接表插件时，往数组里加引用这一步不能靠 Inspector 拖拽**。

### 阶段 2：**ConfigIndex 拆表**（T1 + T3 + T4 + T5 + T7 + T8改 + T10）✅ **已完成（2026-10）**

> **这一步的实际范围比设计文档原计划大**：用户原话是「ConfigIndex 里面的东西应该也是分出去成多个不同表」，
> 所以本轮**没有**按"一张表一次"的顺序慢慢来，而是把 `ConfigIndexData` 里**所有能成行的字段**一次拆完
> （8 张表 + 42 个行文件），并顺手把用户点名的**效果参数表**做出来。技能表（T2）与首领增强表的
> "节点驱动"仍留给后续阶段 —— 它们不是 `ConfigIndex` 里的字段，不拆不影响这一轮的完整性。

#### 拆表总览（74 字段 → 8 张表 + 保留 30 个全局字段）

| 表 | 行类 | 目录 | 行数 | 取代了 `ConfigIndexData` 的哪些字段 |
|---|---|---|---|---|
| 武器表 | `WeaponConfig` | `Config/Tables/Weapons/` | **3** | `Pistol*` 11 + `Sniper*` 7 + `Rifle*` 7 = **25 个字段**（含 `RifleAutomatic`） |
| 角色表 | `CharacterConfig` | `Config/Tables/Characters/` | **3** | `Player*` 5 + `Heavy*` 5（含 `HeavyMeleeDamage`）+ `Summoner*` 3 = **13 个字段** |
| 敌人表 | `EnemyConfig` | `Config/Tables/Enemies/` | **1**（`enemy_global`） | 表的**索引**已就位；四个全局倍率字段**仍保留在 ConfigIndex** 当回退（它们是"难度总开关"，跨表生效） |
| **效果表** | **`EffectConfig`** | `Config/Tables/Effects/` | **10** | `StatusEffect.cs` 的 6 处硬编码 + `RewardCatalog` 里的效果常量（见下） |
| 节点表 | `NodeConfig` | `Config/Tables/Nodes/` | **9** | `ShopHealCost` / `ShopRewardCost` / `ShopWeaponCost` 3 个字段 + 3 处 switch |
| 增幅项表 | `RewardConfig` | `Config/Tables/Upgrades/` | **19** | 无（`RewardCatalog.All` 本来就是 C# 数组，现在变成"表优先 + C# 兜底"） |
| 局外成长表 | `MetaUpgradeConfig` | `Config/Tables/Meta/` | **5** | `MetaProgress` 的 5 个系数 + 价格公式（`ConfigIndex` 里本来没有，属顺手外置） |
| 关卡表 | `StageDefinition` | `Config/Tables/Stages/` | **3** | ✅ 阶段 1 已做 |

**保留下来的 `ConfigIndexData` 字段（30 个）** —— 都是"跨表生效的全局开关"或"表现层"，不成行：
敌人/Boss 全局倍率（6）· 节奏倍率（2）· Boss 压力曲线（3）· 节点/关卡约束（`MinNodeSpacing` / `KillPlaneY` /
`NodeHealRatio` / `EliteBossHealthScale` / `FreeChallengeBaseQuota` / `FreeChallengeMaxQuota` / 4 个 Pressure 结算值）·
成长（`RewardChoiceCount`）· 表现·伤害跳字（4）· 表现·血条（4 + `BossBarFullRange` / `BossHealthBarScale`）。

#### ⭐ 效果表（T8 的落地形态）—— 用户点名的那张

用户原话：「**甚至一些效果，比如击退值、燃烧值这些也是需要配置参数的**」。
盘点后发现"效果参数"原先散在**三处**，本轮全部收进 `Config/Tables/Effects/eff_*.tres`（10 行）：

| 行 Id | Kind | 原来在哪（硬编码处） | 关键数值 |
|---|---|---|---|
| `Slow` | `Slow` | `StatusEffect.cs:43` | `MoveSpeedMultiplier × 0.55` |
| `Weaken` | `Weaken` | `StatusEffect.cs:52` | `DamageMultiplier × 0.75` |
| **`Burn`** | `DamageOverTime` | `StatusEffect.cs:59` | **`DamagePerSecond = 6`**（用户点名的"燃烧值"） |
| `Empowered` | `Empower` | `StatusEffect.cs:67-68` | `AttackSpeed × 1.3` + `Damage × 1.15` |
| `Stun` | `Stun` | `StatusEffect.cs:77-78` | `MoveSpeed × 0.12` + `AttackSpeed × 0.5` |
| **`Knockback`** | `Knockback` | **无**（原来没有全局击退参数） | `Value = 1.0` 全局缩放 + `Value2 = 1.0` 抗性上限（用户点名的"击退值"） |
| `Explosion` | `Explosion` | `RewardCatalog.cs:49-50` | `Radius 3.5` + 伤害比例 `0.6` |
| `Lifesteal` | `Lifesteal` | `RewardCatalog.cs:67` | `Ratio 0.12` |
| `SummonOnKill` | `SummonOnKill` | `RewardCatalog.cs:79` | `MaxSummons 2` |
| `ChainLightning` | `Other` | `RewardCatalog.cs:58-62` | `Damage 14` / `Range 7` / `Jumps 3` / `Cooldown 0.12` |

**两条诚实交代**：
1. **击退值有两个层次**，别找错地方：① **每个单位实际给多少击退** 在**武器表**的 `Knockback`
   （`WeaponConfig.Knockback`：手枪 2.5 / 狙击 4 / 步枪 1.2）与**命中体** `AttackHitbox.Knockback` 上；
   ② **效果表**的 `eff_knockback` 放的是**全局倍率**（`Value = 1.0` = 原样）与抗性上限。
   本轮**没有**把 `Value` 接进 `Unit.ReceiveHit`（那会改玩法数值），所以它当前是"已就位、待接线"的参数。
   接线点只有一处：`Unit.cs:278` 的 `float scale = Stats.Get("KnockbackMultiplier");`。
2. **`StatusEffect` 类没改形态**，只是数据来源从 switch 换成查表 —— `StatusPresets.Create(id, duration)`
   现在是"先查效果表、读不到才走 `Fallback` 的 switch"，所以老行为逐位保留（自测实测：燃烧 6 dps / 减速 0.55）。

#### 增幅项表（T3）：`ApplyHandler` + 处理器注册表

按 §6.1 的**方案 A** 落地：14/19 条纯数据（`StatSet` 处理器 + `Modifiers` 里的 `StatOverride`），
5 条异化各一个 handler（`ChainLightning` / `Lifesteal` / `BurnOnHit` / `SummonOnKill` / `AmmoOnKill`）。
`RewardEffectRegistry.Compile(row)` 在**读表时**就把"handler 未注册"抓出来并警告，不会静默变成"没有效果的奖励"。

`RewardCatalog.All` 从 `static readonly` 改成 **`static … { get; private set; }` + 表优先**：
- 表在 → `All` = 编译出来的 19 条（`ConfigIndex._Ready` 里 `RewardTable.Build(Config.Upgrades)` 设置）
- 表空 / 未加载 → `All` = `RewardCatalog.Fallback`（就是原来那个 C# 数组，**保留**，没有删）
- **`RewardPool` / `RunBuild` / `RunController` 的消费代码一行都没改** —— 这正是 §2.2 那层"磁盘行 → 运行期视图"的价值。

> ⚠️ **`Weight` 仍是 1f**：19 条行的 `Weight` 都写成 `1.0`，所以加权抽取仍然退化成等概率（与分表前一致）。
> 这是**故意的**：改它会改抽样分布、自测基线会漂（§1.4-⑤ 的问题仍在，但至少现在**改得动了** —— 改一行 `.tres` 即可）。

#### 节点表（T7）：一处替掉三个 switch + 一处重复实现

`RewardService.FilterFor` / `TitleFor` / 商店价目、`NodePlacement.LayerOf` / `WavesFor`、
`StageController.LayerOf`（原来与 `NodePlacement` **各写了一份**）现在全部"优先读 `node_*.tres`、读不到才回退原 switch"。
`StageController.WavesFor` 直接转调 `NodePlacement.WavesFor`，重复实现消失。

#### ⚠️ 阶段 2 挖出来的两条新坑

**(1) `ConfigIndex` 的 autoload 排在 `GameLog` 之前 → `_Ready` 里写的日志被静默丢掉。**
`project.godot` 的 `[autoload]` 顺序是 `MCPRuntimeProbe → ConfigIndex → UiRouter → GameLog → SelfTest → UiService`。
首轮实测：日志里**一行 `[Config]` 都没有**，但表其实已经读进来了。
**修法**：`ConfigIndex._Ready` 把建索引推迟一帧（`CallDeferred(nameof(BuildTableIndex))`），
这时所有 autoload 都已就绪、主场景还没加载，日志能落地且不影响任何读取者。

**(2) 编辑器有资源缓存：手写 `ConfigIndex.tres` 后，`ResourceLoader.load` 会**静默返回旧版本**。**
现象：改完 `ConfigIndex.tres` 的数组引用后立刻读，`Stages=3` 而 `Weapons=0`（明明文本写对了）。
**修法**：`EditorInterface.get_resource_filesystem().update_file(path)` + `CACHE_MODE_REPLACE`，
或干脆在编辑器里重开一次。**这只影响编辑器内验证，不影响游戏启动**（游戏是冷启动读盘）。

#### 阶段 2 的验证（实测证据）

**编译**：`& build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**（每改一步都跑，全程 0/0）。

**自测（`selftest.flag` + gdmcp）逐项比对 —— 数值未变**：

| 项 | 实测 | 期望（分表前） |
|---|---|---|
| 配置表索引 | 武器 3 / 角色 3 / 敌人 1 / 效果 10 / 节点 9 / 局外 5 | 3/3/1/10/9/5 |
| 增幅项目录 | 19 条（强化 10 / 进化 4 / 异化 5），**用了表=True** | 19 = 10/4/5 |
| **池子规模** | **枪手 18 条** | **18**（[rewards.md](rewards.md) 的硬指标） |
| 玩家 | 生命 270（= 250 + 元进度 20）护甲 40 移速 7 减伤 0 | 250/40/7/0 |
| 武器（手枪） | 伤害 24 / 0.26 / 弹匣 12 / 装填 0.7 / 散布 0.5 / **击退 2.5** / 暴击 0.15 / 弹速 90 / FOV 55 / 穿透 0 / 非连发 | 全部一致 |
| 节点表 | 进化层 2/波 3 · 强化层 1/波 2 · 恢复层 0/波 1 · 商店价目 5/12/18 · 标题"异化池" | 全部一致 |
| 效果表 | 燃烧 6 dps · 减速 `MoveSpeedMultiplier=0.55` | 6 / 0.55 |
| 波数映射 | 进化3 异化3 强化2 商店2 材料1 恢复1 特殊1 | 一致 |
| 节点布置 / 间距 | 启用 10 个（高 2 / 中 5 / 低 3）· 最小 9.74 | 一致 |
| Pressure | `+187.5 → 1` / `+225 → 3` | 逐位一致 |
| 端到端 | 按 F 之后奖励界面打开 = True | 一致 |

**回退路径（每阶段必做一次）**：把 `Stages` / `Weapons` / `Characters` / `Enemies` / `Effects` / `Nodes` /
`Upgrades` / `MetaUpgrades` **八个数组全部清空**再跑 ——

| 观察点 | 实测 |
|---|---|
| 日志留痕 | `配置表：武器 0 / 角色 0 / 敌人 0 / 效果 0 / 节点 0 / 局外成长 0（ConfigIndex.tres 已加载）`、`增幅项表：0 条（来源：代码默认值）…使用 RewardCatalog.Fallback 的 19 条`、`关卡表：0 关（来源：默认值）` |
| 武器回退 | 伤害 **22** / 间隔 0.32 / 弹匣 8 / 装填 1.15 / 散布 0.6 / 弹速 75 ← **`Weapon.cs` 组件自身的 `[Export]` 默认值**（不是旧 ConfigIndex 值） |
| 玩家回退 | 生命 140（`Unit.cs` 场景默认）+ 元进度 20 = 160 ← **玩家场景自身的值** |
| 节点/效果/局外/增幅回退 | 波数映射、商店价目 5/12/18、标题、燃烧 6 dps、减速 0.55、池子 18 条 —— **与表在时逐位一致**（因为 C# 兜底就是分表前的那份实现） |
| 玩法可用性 | 自测全程通过、端到端按 F 仍打开奖励界面 |

> 结论：**"读不到表就用自身默认值"这条语义真的生效**，而且每一处都在日志里留了痕。

### 阶段 3：武器表 + 角色表（T1 + T4）✅ **已完成（2026-10，随阶段 2 一起做）**

1. ✅ 建 `Config/Tables/Weapons/wpn_*.tres`（3 行）与 `Config/Tables/Characters/chr_*.tres`（3 行），并在 `ConfigIndex.tres` 上加 `Weapons` / `Characters` 两个数组引用它们。
2. ✅ 删掉 `RunController.EquipWeapon` 的三元链（原 L396-412，改成 `WeaponConfig.ApplyTo`)与 `ApplyBalance` 的 if/else 链（改成 `ConfigRows.BySlot<CharacterConfig>`）；`ConfigIndexData` 里那 **25 + 13 = 38 个字段已删除**。
3. ⏸ `RunSetup.Weapon/Character`（`RunSetup.cs:13-16`）**仍存枚举**；`WeaponPresetKind` / `PlayerCharacterKind` 保留为"槽位壳"（`Slot = (int)kind + 1`）。**没改** —— 改它会牵动 `StartScreen` / `UiService` / `SelfTest` 多处，收益（少两个枚举）不值这个风险（§6.4）。
4. ⏸ `Scenes/Weapons/Pistol.tscn:16-25` 的去留（§6.3）**没动**：它现在的值是 22/0.32/8/1.15/75…，与武器表**不再冲突**（表是权威、每局开局覆盖），
   但它仍是"打开预制体看到 22、实际打 24"的困惑源。**建议下一步把它清空为 `Weapon.cs` 的默认值**。
5. ✅ `WeaponConfig` 里已预留跨武器字段（`AttackTags` / `ScenePath` / `ModelPath` / `IconPath`）。
   → 🆕 **"武器→升级池"的关联已在阶段 4 落地**（正向指，不是这里写的 `ApplicableWeapons` 反向指）：见 §4.5.0 与 §5 阶段 4。

**验证**：见阶段 2 的比对表（手枪 11 项逐项一致、玩家 4 项一致、`PoolPreview` 18 条不变）。
❗ **未做**：三把武器各自开一局验证狙击/步枪的暴击/弹速（分表前它们**共用** `Pistol*` 的值，现在各自有独立字段，默认值仍写成 0.15/2/90/55/3 所以**行为不变**；要按武器区分得改行文件）。

### 阶段 3：统一单位表 + 技能表（T2 + T5 + T9 的一部分 + #24）✅ **已完成（2026-10）**

> 用户要求的结构：「敌人和首领单位都享有一个**单位表**，定义了它们**都通用的属性参数**，
> 比如攻击/生命/移速/持有技能/行为&类」+「首领行为表本质也是一些技能」。

**新增行类**（`Scripts/Config/`）：

| 类 | 继承 | 自己声明的列 | 目录 / 行数 |
|---|---|---|---|
| `UnitConfig` | `ConfigRow`（抽象，不导出） | 生命（生命/护甲/护盾/护甲减伤/额外减伤/击退抗性）· 攻击四项（`AttackDamage` / `AttackInterval` / `AttackRange` / `AttackKnockback`）· 移速 · 行为&类（`Tags`）· **持有技能 `Skills : AbilityConfig[]`**（资源引用）· **AI 档案 `AiProfile`**（资源引用）· 表现（`ModelPath` / `ModelScale`）· **动画 6 列**（`AnimIdle` / `AnimWalk` / `AnimRun` / `AnimDeath` + `AnimWalkMinSpeed` / `AnimRunMinSpeed`；**空 / 0 = 不覆盖** → `UnitAnimator`） | —（基类） |
| `EnemyConfig` | `UnitConfig` | 全局倍率行（`IsGlobal` + 4 个倍率）· 刷怪（`ScenePath` / `Cost` / `Weight` / `MinElapsedSeconds` / `EliteChance`） | `Enemies/` **10** |
| `BossConfig` | `UnitConfig` | `ScenePath` · `EliteCopyHealthScale` | `Bosses/` **1** |
| `SummonConfig` | `UnitConfig` | `ScenePath` · `LifetimeSeconds` | `Summons/` **1** |
| `CharacterConfig` | `UnitConfig` | 武器开关 / 能力开关矩阵 | `Characters/` **3** |
| `AbilityConfig` | `ConfigRow` | 寻址（`NodeName` / `ClassHint`）· 通用项（`Aim` / `Cooldown` / `Windup` / `Recover` / `Enabled` / `Approach*`）· `Params`（子类专属）· `AttackParams`（命中体/武器组件）· `ApplyHandler` + `Weight`（**首领行为词条**用） | `Skills/` **25** |
| `PressureModConfig` | `ConfigRow` | `Weight`（阶段 6 起真的加权）· **`Tags`**（阶段 6：数值型候选的适用规则）· `Modifiers : StatOverride[]`（**只允许数值操作**，阶段 7 起一行只放一项）· **`AbilityNode` + `AbilityScales` + `AttackScales`**（阶段 7：技能参数，`Value` = 倍率）· **`BossIds` + `StageIds`**（阶段 7：限定词，**Boss 出生时**判定） | `Pressure/` **8**（5 数值型参数栏 + 3 技能参数栏） |

> 与 §3 的设计相比，落地时**砍掉了三列没有消费者的**（"不留让人误以为能调的列"）：
> `UnitConfig.IconPath`（敌人/召唤物没有任何图标界面；首领头像是 `BarBoss.tscn` 的固定色块）、
> `AbilityConfig.InputAction`（按键由 `PlayerAbilityInput` 按能力类硬编码 + `project.godot [input]` 决定，
> 改键是「设置表」那件事）、`CharacterConfig.DefaultWeaponId`（阶段 2 起就没人读，开局武器由 `StartScreen` 决定）。
> `ModelPath` / `ModelScale` 保留了，因为它们在 `UnitConfig.ApplyModel` 里有真实代码路径（当前所有行留空 = 用场景模型）。
> **动画那 6 列**（2026-10 新增）与它们同一套纪律：`ApplyAnimationTo(unit)` 只写**填过的**列，一行都不填就什么都不做
> （单位用 `UnitAnimator` 自身的 `[Export]` 默认值），所以新敌人不填这几列也能动。细节见
> [config.md](config.md) 的「首领去甲 + 单位动画实测」。

**三条关键设计（都是为了避免两个真源）**：

1. **攻击四项属于单位行，`AbilityConfig.Cooldown <= 0` = 不覆盖**：套用顺序是"先技能行、后单位行"，
   所以主攻击的伤害/间隔/范围/击退只有一个权威（单位行）。范围按能力子类依次试
   `AttackRange` → `TriggerRadius` → `MaxRange` → `MaxDistance`（用 `GodotObject.Get` 判存在）。
2. **`AttackParams` 与 `Params` 分开**：前者套到**这个技能的命中体**（没有命中体就退到它的武器组件，
   远程兵走这条），后者套到**能力节点自己**。命中体的 `Damage`/`Knockback` 对主攻击由单位行统一给。
3. **白名单按 `Id` 匹配**：`StageDefinition.AllowedEnemies`（中文显示名）→ **`AllowedEnemyIds`**；
   生成项与行的配对改用**预制体路径**（`ConfigRows.ByScenePath<T>()`）。未知 Id 由 `ConfigIndex.BuildTableIndex`
   在开局一次性校验并写警告 —— 这正是 §1.4-F4 与 §5 阶段 4 里"最容易出错的一步"的落地。

**首领强化的两套来源（关卡核心文档 §7.1）**：行为/技能型 = 技能表里 `ApplyHandler` 非空的 4 行
（`bhv_awakened` / `bhv_summoner` / `bhv_screaming` / `bhv_charging`，由 `BossPerkCatalog` 的处理器注册表编译）
—— **界面上叫「技能」**；
数值型 = `Pressure/` 的 **7 行**（阶段 7 是 8 行：`pmod_berserk_dmg` / `pmod_berserk_speed` / `pmod_fortress_hp` /
`pmod_fortress_armor` / `pmod_swift` / `pmod_scream_echo` / `pmod_charge_impact` / `pmod_summon_haste`；
**其中 `pmod_fortress_armor`「铁壁·护甲」已按用户要求整行下线（表行 + `ConfigIndex.tres` + 代码兜底）**）
—— **界面上叫「词条」**。
两张表**分别编译、各自独立回退**（一侧读不到不会让另一侧变空）；`PressureOnly` / `BehaviorOnly`
让两套来源可独立追踪。**没有**为 Pressure 加"可被玩家增幅修改"的接口（§7.2 明令禁止）。
> ⚠️ 术语与护甲下线的完整记录见 [config.md](config.md) 的「用户三项修正实测」（同轮还改了
> 命中特效的位置：`HitEffectHeightOffset` 已删除，改用目标碰撞体表面的实际命中点）。

> ✅ **阶段 6 起两个抽取点也分开了**（本节之前写的是"`All` = 数值型在前 + 行为型在后，开场抽取随机序列不变"的**过渡态**）：
> `All` 现在**只是日志统计用**，不再是任何抽取点的候选集；
> Boss 开场词条 `Roll()` **只抽行为型**（Boss Build），Pressure 转换 `RollPressure(tags)` **只抽数值型**（§7.5，加权 + 标签过滤）。
> 所以**开场词条的结果会变**（候选 7 → 4），这是 §7.1 要求的分离；自测的 `+187.5 → 1` / `+225 → 3` 仍逐位不变。见 §5 阶段 6。
> ✅ **阶段 7 追加第三个点**：`ApplyWithRefresh` 在**首领出生时**判限定词（`BossIds` / `StageIds` / 必须有技能节点），
> 判不成立就换抽 —— 抽签（`RollPressure`）**不判**限定词，因为那时首领还不存在。见 §5 阶段 7。

**顺手修掉的既有问题**（详见 [config-tables-inventory.md](config-tables-inventory.md) §5）：

| 问题 | 处理 |
|---|---|
| `ConfigIndexData.EliteBossHealthScale`（0 消费者） | 搬进 `BossConfig.EliteCopyHealthScale`（行优先、场景导出值兜底）后删除 |
| `ConfigIndexData.BossHealthBarScale`（0 消费者） | 删除（血条尺寸由 `BarBoss.tscn` 决定） |
| `StageDefinition.IsFinalBoss`（0 消费者） | 删除字段 + 3 行关卡里的赋值 |
| `EffectConfig.Value` / `Value2` / `Radius` / `TickSeconds`（0 消费者，是 `Params` 的镜像） | 4 列删除；删完只剩注释的 `eff_knockback.tres` 整行删除（9 行） |
| `EnemyConfig.SpawnDisplayName`（为对接显示名匹配而加） | 删除（身份键改为 `Id`） |
| **静默换挡**：`ConfigIndex.cs` 的 `row?.HealthMultiplier ?? config.EnemyHealthMultiplier`（表 0.75/0.3 vs C# 1f/0.8f） | 两处对齐 + 新增"敌人倍率来源"日志 |
| `BarEnemy.tscn` 的 `Content` 没以 Root 原点居中 | 改成 `(-33,-4.5)~(33,4.5)`（尺寸不变），`WorldDraw` 的警告消失 |

**另一条实测踩到的坑（写进 `Unit` 的注释了）**：进树前套用配置表的值时，
`Unit.RefreshStats()` 会在 `StatSheet` 基础值还没就位（`_Ready` 才 `SeedStats`）时把生命算成
`max(1, 生命 × 0) = 1`，而 `_EnterTree` 又把这个 1 捕获成基础值 —— 一路静默错到底
（实测 Chaser 生命 45 → 1）。修法：`SeedStats()` 提到构造函数里。

**验证**（实测日志与回退验证的逐项比对见 [config.md](config.md) 的「配置表阶段 3 实测」一节）：
`build.ps1 --no-incremental` **0 warning / 0 error**；自测里 `技能表实测（玩家）：滑铲 冷却 1.2 位移 6
AbilityId skl_dash ｜ 近战 伤害 32 间隔 0.75 射程 2.4 击退 5 命中标签 [Melee,Heavy]`、
`单位表实测（FastMelee）：生命 26 …（技能行 Id skl_enemy_fast_melee_melee）`；
把 5 个新数组清空后仍能跑完自测、日志显示"来源：代码兜底 / 场景导出值"且敌人倍率**不再换挡**。

### 阶段 4：武器 → 升级池 → 选项（§4.5 的落地）✅ **已完成（2026-10）**

> **做了什么**（详细实测/回退验证/逐位比对见 [config.md](config.md) 的「配置表阶段 4 实测」）：
>
> | 变化 | 文件 |
> |---|---|
> | 新行类 **`UpgradePoolConfig : ConfigRow`**（只承载身份：Id/Slot/DisplayName/Note） | `Scripts/Config/UpgradePoolConfig.cs` |
> | 新表 **`Config/Tables/UpgradePools/pool_{pistol,sniper,rifle}.tres`**（3 行 = 一行一个池） | 新目录 |
> | 索引数组 `ConfigIndexData.UpgradePools` + `ConfigRows.Rebuild` + **`ReportUpgradePools()` 日志留痕** | `ConfigIndexData.cs` / `ConfigIndex.cs` |
> | **正向指**：`WeaponConfig.Pool` → `ApplyTo` 写 `Weapon.PoolId`（`[Export]`，表读不到时用场景值 = 空） | `WeaponConfig.cs` / `Weapon.cs` |
> | **一个选项占一行 + 一个引用列**：`RewardConfig.Pool` | `RewardConfig.cs` |
> | **三级回退**：`RewardDefinition.ApplicablePoolIds` + `AllowsWeapon` 重写 | `RewardDefinition.cs` |
> | 编译入口：`PoolIdsOf(row)` + `ConflictsWith = row.ConflictsWith` | `RewardEffectRegistry.cs` |
> | 死字段清理：删 `PoolTags`（含 19 行 `.tres`）/ 删工厂的 `tag` 参数与 15 个调用点 | `RewardCatalog.cs` + 19 个行文件 |
> | 自测新增 `配置表比对·升级池`（三层条数 + `PoolId`），索引行加 `升级池 3` | `SelfTest.cs` |
>
> **验收（实测）**：`build.ps1 --no-incremental -v minimal` **0 warning / 0 error**；
> 自测 `当前池子=18 条（枪手期望 18 条）` **逐位不变**，改前/改后配置相关日志逐行 diff 只有新增的那几行；
> 两次临时实验证明第一层会拦人（A：选项指向本武器的池 → 命中 1、池子 18；B：指向别的池 → 命中 0、池子 17）；
> 两次回退验证（清武器行 `Pool` / 清 `UpgradePools` 索引）= 池子仍是 18、玩法数值全不变、
> 后者会打 3 条"池不在索引里"的 warning。
>
> ⚠️ **本轮推翻的设计**：§4.5.2 推荐的"**反向指** `RewardConfig.ApplicableWeapons`"**未采用**（用户要求正向指）。
> `WeaponConfig.ApplicableWeapons` 这个字段**全项目从未存在过**（只在文档里），所以没有代码要删。
> §6.9 的取舍点因此有结论了（见该节）。

### 阶段 5：AI 行为表（盘点 #15）✅ **已完成（2026-10）**

> **做了什么**（详细实测 / 两次回退验证 / 逐行 diff 见 [config.md](config.md) 的「配置表阶段 5 实测」）：
>
> | 变化 | 文件 |
> |---|---|
> | 新行类 **`AiProfileConfig : ConfigRow`**（`[Tool]` + `[GlobalClass]`）：Brain 2 列 + **21 个行为槽列**（按节点名）+ `ConfigRow.Params` 逃生口 | `Scripts/Config/AiProfileConfig.cs` |
> | 新表 **`Config/Tables/AiProfiles/ai_*.tres`**（**11 行** = 一行一个 AI 档案：9 敌种 + 首领 + 无人机） | 新目录 |
> | 新列 **`UnitConfig.AiProfile`**（**资源引用** `AiProfileConfig?`，不是字符串 id）+ 一行调用 `ApplyAiProfileTo(unit)` | `Scripts/Config/UnitConfig.cs` |
> | 索引数组 `ConfigIndexData.AiProfiles` + `ConfigRows.Rebuild` + **`ReportAiProfiles()` 日志留痕**（单位行→档案 + "有预制体但没挂档案的 N 个" + "引用还在索引里查不到"的警告） | `ConfigIndexData.cs` / `ConfigIndex.cs` |
> | 11 个单位行填引用：9 个 `enemy_*.tres` + `boss_final.tres` + `summon_drone.tres` | 11 个行文件 |
> | 自测新增 `配置表比对·AI 档案表` + **"伪造 -1 再 ApplyTo"探针** + 场上敌人的实测行，索引行末尾加 `/AI 档案 11` | `SelfTest.cs` |
> | 顺手修：`enemy_exploder.tres` 的 `Note` 里 4 个 ASCII 引号（`"…"` 会让 Godot 的 VariantParser 在该行报错 → 改成 `「」`） | 1 个行文件 |
>
> **盘到的参数（以代码为准）**：**25 个** —— `EnemyBrain` 2 · `Behavior` 基类 3（`Priority`/`MinRange`/`MaxRange`）
> · `Chase` 2 · `KeepDistance` 5 · `Retreat` 3 · `UseAbility` 1 · `Reposition` 5（**0 场景使用**）
> · `AvoidObstacles` 4 处硬编码启发式（**没有进表**，它们是"AI 手感"保护值，见 §1.3 B 类）。
> 场景侧赋值：敌人 10 个场景 **53 处** + 无人机 5 处 + 训练场 4 处 = **62 处**。
>
> **三个设计取舍**（都写进了类注释）：
> 1. **扁平列而不是 `Array[行为]`** —— 一列 = 一个标量属性才拿得到编辑器的排序/批量改；
>    数组列既拿不到这些，阶段 4 还实测过 `Array[Object]` 的插件报错。这与 §4.3「一个选项占一栏」是同一条理由。
> 2. **按节点名开列而不是按行为类** —— Boss 的 `Brain` 下有 **3 个** `UseAbilityBehavior`
>    （`Scream` P120 / `Charge` P110 / `Summon` P60），按类开列会撞成一列。
> 3. **`AiProfileId`（阶段 3 预留名）→ `AiProfile`（资源引用）** —— 与 `UnitConfig.Skills` /
>    `WeaponConfig.Pool` 同一套写法；行文件被删/改名时 Godot 把引用置 null（可判定、可警告），
>    而字符串 id 拼错只会静默失效（§1.2 里那 11 处字符串外键的老问题）。`Edit Resources as Table`
>    里同样是资源列（与阶段 4 实测的 `Pool(type=24, hint=17)` 同类）。
>
> **两条语义约定**（沿用既有纪律）：**数值列 0 = 不覆盖**（`RetreatHealthThreshold` 用 `-1`，因为 0 = 永不撤退有意义）；
> **`Priority <= 0` = 整个槽位不生效**。**边界**：档案只调数值，**不增删行为节点**（拓扑在场景里）；
> `AbilityNodeName`（身份）与 `MinRange`/`MaxRange` 走 `Params` 逃生口（键 = `节点名.属性名`）。
>
> **验收（实测）**：`build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**；
> 自测走到 `=== 自测结束 ===`，索引 `…/升级池 3/AI 档案 11（期望 …/11）`，
> 探针 `SearchRange 40 / Chase.Priority 10 / StopDistance 1.6 / Attack.Priority 100`（篡改后又被写回 = 真套上了）；
> 与基线日志逐行 diff，**所有既有 `配置表比对·*` 行逐位相同**（索引行只在末尾追加）。
> 两次回退验证：清 `AiProfiles` 索引 → `0 个（来源：代码默认值（表读不到））` + 11 条 warning、**玩法不变**；
> 清单位行的 `AiProfile` → 探针**保留伪造的 -1**（= 真的什么都没写，走组件/场景默认值）。
> ⚠️ 清索引数组**不会**让单位丢掉档案（引用在行文件里）—— 与阶段 3/4 那条边界**同一个坑，第三次遇到**。

### 阶段 6：首领增强的"节点驱动抽奖"（T7 的后半 + T9 的最后一步）✅ **已完成（2026-10）**

> **做了什么**（详细实测 / 回退验证 / 逐条解释见 [config.md](config.md) 的「配置表阶段 6 实测」）：
>
> | 变化 | 文件 |
> |---|---|
> | `PressureModConfig` 新增 **`Tags`** 列（数值型候选的适用规则，§7.4/§7.5） | `Scripts/Config/PressureModConfig.cs` + 3 个行文件 |
> | `BossPerk` 新增 `Tags`；`CompilePressure` 填进去 | `Scripts/Boss/BossPerk.cs` |
> | **拆开两个抽取点**：`Roll(count)` 改成**只抽行为型**（Boss Build）；新增 `RollPressure(tags)` / `FilterPressure(tags)` / `DescribePressurePool(tags)`（只抽数值型，加权 + 标签过滤） | `Scripts/Boss/BossPerk.cs` |
> | `AddPressureForNode` 重写：**节点驱动基础点数**（`ResolveBasePressure` 三层）、**显式逐次跨阈值**（§7.3 第 6 条）、**跨阈值立即抽**、抽到的排进 `_pendingPressurePerks` | `Scripts/Run/StageController.cs` |
> | 应用点：`_Ready` 订阅 `_bossEncounter.BossSpawned` → `ApplyPendingPressurePerks()`（成对退订在 `_ExitTree`）；**没有**改 `EncounterController` | `Scripts/Run/StageController.cs` |
> | `PickTags`：节点行 `BossModifierTags` → 关卡行 `PressureUpgradeTags` → 不限制 | `Scripts/Run/StageController.cs` |
> | 移除 §7.5 的替身：`_bossPressureBonus` → `_pressureRolls`（跨阈值次数），`BossInitialPressure` 不再叠加它 | `Scripts/Run/StageController.cs` |
> | 新增确定性日志：`Pressure 节点驱动：…`（节点表里哪几行配了）与 `Pressure 转换池（§7.5…）：候选 N 条 [权重 → 概率]` | `StageController.ApplyStage` / `BossPerkCatalog.Build` |
> | 行文件填值：`pmod_*` 的 `Weight` 3/2/2 + `Tags`，`node_evolution` / `node_mutation` 的 `BossPressurePoints = 150` | 5 个行文件 |
>
> **验收（实测）**：
> - `build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**（全程）。
> - 自测硬指标 **`+187.5 → 1` / `+225 → 3` 逐位不变**（含义从"首领额外压力等级"变成"跨阈值次数"，
>   数值与原来的整除结果相同）；`DebugState` 的 `pressure=3` 与改前逐字相同（新增 `pmods=3`）。
> - **`Weight` 真的生效**：临时探针用独立固定种子 RNG 抽 30000 次，权重 3/2/2 → 42.0/29.0/28.9%；
>   把表里权重改成 1/1/1 → 32.9/33.3/33.8%；把 `swift` 改成 0 → 0.5%（`WeightedPick.MinWeight` 的 1% 下限）。
> - **`BossModifierTags` 真的生效**：`node_evolution.BossModifierTags = ["Defense"]` → `立即抽取 [铁壁]`（强制定向）。
> - **`BossModifierRolls` 真的生效**：`node_enhance.BossModifierRolls = 2` → `本次跨阈值 0 次 + 节点行额外 2 次`，`pmods` 3 → 5 而 `pressure` 仍为 3。
> - **数值真的落到 Boss 身上**：Boss 出生日志带上套用后的血量/护甲/三个乘区，
>   `[铁壁、疾行、疾行]` → 生命 1260（900×1.4）、护甲 350（200+150）、移速 ×1.914（1.35²×1.05），算术逐项对得上。
> - **回退验证**：把本轮新增的配置全部清空（`Weight`→1、删 `Tags`、删节点行的 `BossPressurePoints`）跑一整轮 ——
>   日志明确写 `节点表 9 行都没配 → 全部走「关卡行 → ConfigIndex 全局默认值」回退`，`+187.5` / `+225` 与表在时**逐字相同**，
>   与 Pressure 相关的 warning/error **0 条**，自测走到 `=== 自测结束 ===`，端到端按 F 仍能打开奖励界面。
>
> ⚠️ **两处必须知道的行为变化**（都不是回归）：
> 1. **Boss 开场词条的候选集从 7 条（数值型 3 + 行为型 4）变成 4 条（只有行为型）** ——
>    §7.1 要求两套来源分离，数值型改由 Pressure 转换产出。所以开场词条的**随机结果会变**。
> 2. **`BossInitialPressure` 不再叠加节点累积 Pressure 折算出的压力等级** —— 那段是 §7.5 没落地时的替身；
>    现在转换的产出是"抽一条数值强化套到 Boss 身上"，两头都算就是重复计。**需要一轮平衡测试**。
>
> ⚠️ **未做（第八章范围，已记录）**：§8.2 的"免费挑战不加 Pressure"闸门没接，免费挑战目前也会累积 Pressure。
> （⏩ 后续补记：**该闸门已在 2026-10 接上**，见 [config.md](config.md) 的「免费挑战额度（§8.2）」一节。）

### 阶段 7：Pressure 词条"一参数一栏" + 技能参数入池 + 限定词刷新判定 ✅ **已完成（2026-10）**

> **需求**（用户原话）：「词条我这里只看到了 3 条，`Pressure/` 里的，不过这三条应该**拆成 5 条**的，
> **每个参数单独占一个词条栏**。如果可以的话**把一些技能的参数也加入词条里**
> （尽管每个 boss 可能都不同，**限定词记得做好刷新判定**）」
> 详细实测 / 权重依据 / 强度换算 / 回退验证见 [config.md](config.md) 的「配置表阶段 7 实测」。
>
> | 变化 | 文件 |
> |---|---|
> | `Pressure/` **3 行 → 8 行**：`pmod_berserk` → `pmod_berserk_dmg` + `pmod_berserk_speed`；`pmod_fortress` → `pmod_fortress_hp` + `pmod_fortress_armor`；`pmod_swift` 不变；新增 3 条技能参数行 | `Config/Tables/Pressure/*.tres`（8 个行文件） |
> | `PressureModConfig` 新增 **5 列**：`AbilityNode`（目标技能节点）· `AbilityScales`（能力节点参数倍率）· `AttackScales`（命中体参数倍率）· `BossIds` / `StageIds`（限定词） | `Scripts/Config/PressureModConfig.cs` |
> | 新增 **`PerkQualifier`**：限定词数据 + `FailedReason(context)`（**每次调用现算，没有任何缓存字段**）+ 首领/关卡身份解析（单位行 Id 优先） | `Scripts/Boss/PerkQualifier.cs`（新文件） |
> | `BossPerk` 拆成 **`Effect`（效果本体）+ `Apply`（入口 = 先判限定词）**；新增 `AsRolled(tags)`（抽签副本，带 `RollTags`）/ `MarkRedrawn()`（把名字改写成 `原→实际生效`） | `Scripts/Boss/BossPerk.cs` |
> | 新增 **`BossPerkCatalog.ApplyWithRefresh`**：**Boss 出生时**判限定词 → 成立就生效、不成立就**换抽**同标签池的另一条（循环上界 = 池大小，不死循环）、全判过就只写日志 | `Scripts/Boss/BossPerk.cs` |
> | `RollPressure(tags)` 改为返回**带标签的副本**（换抽要用同一组标签；也避免改写共享的编译期实例）；新增私有 `RollPressureExcept(tags, excludeIds)` | `Scripts/Boss/BossPerk.cs` |
> | 代码兜底 `FallbackPressure` **3 条 → 8 条**（与表同 Id / 权重 / 限定词 / 效果），照 `ScaleAbility` / `ScaleHitbox` 走同一条效果路径 | `Scripts/Boss/BossPerk.cs` |
> | ⚠️ **后续一轮**：`pmod_fortress_armor`「铁壁·护甲」从表、`ConfigIndex.tres`、**以及上面这份代码兜底**里一起下线（8 → 7 条）；自测期望串同步 `…/25/8/…` → `…/25/7/…`；`StageIds` 探针改用合成限定词 | `Config/Tables/Pressure/` + `Config/ConfigIndex.tres` + `Scripts/Boss/BossPerk.cs` + `Scripts/Boss/BossPerkProbe.cs` + `Scripts/Testing/SelfTest.cs` |
> | 数值池的**行为型护栏**：技能参数想写 `Enabled` / `ApplyHandler` / `NodeName` → 警告并跳过（§7.1 边界） | `Scripts/Boss/BossPerk.cs` |
> | 自测探针（池明细 / 技能参数叠乘 / 同一条词条两次判定 / 换抽实测 / 关卡限定 / 端到端 3 次出生） | `Scripts/Boss/BossPerkProbe.cs`（新文件）+ `SelfTest.ReportTables` 一行调用 |
> | `ConfigIndex.tres`：7 条新行文件的 `[ext_resource]`（id 108–114）+ `PressureMods` 数组改指 8 条；`pmod_berserk.tres` / `pmod_fortress.tres` 与它们的 ext_resource 一起删除 | `Config/ConfigIndex.tres` |
>
> **拆分依据（简述，完整算式见 config.md）**：
> 权重 = **继承母行**（3、3、2、2、2）而不是均分 —— 每个老参数的概率**一律**变成原来的 7/15 = 46.7%，没人被单独放大；
> 技能型每条给 **1**（合计 20% 权重，数值型仍占 80%）。单条数值**一个都没动**（1.25/1.2/1.4/+150/1.35 逐字相同）。
> 代价（**必须知道**）：一抽只买一个参数 → 每抽期望强度 = 原来的 **58.9%**，
> 而且"最强的那一条"仍小于原来的每抽总量 → **调权重补不回来**，要补只能改单条数值或降阈值。
>
> **验收（实测）**：
> - `build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**（每改一步都跑）。
> - 自测硬指标 **`+187.5 → 1` / `+225 → 3` 逐位不变**（只由点数与阈值决定，与池子无关）；`Boss 出生 … 3 条`也不变。
>   变的只有**应该变**的两处：`Pressure 3 → 8`（索引行第 7 位与「首领强化来源」），以及 `立即抽取 [...]` 里的名字。
> - **技能参数真的落到技能上**：`AbilityScream.Cooldown 7 → 5.6 → 4.48`（**叠乘**，不是覆盖）、
>   `AbilityCharge 命中体.Damage 20 → 25`；真实出生里也有 `尖啸回响` 生效的样本。
> - **限定词刷新判定**（真实出生，`run_20261011_233032.log`）：第 1 关抽到 `铁壁·护甲` →
>   出生时判**不成立** → 换抽 `狂暴·伤害` → `套用 3 条 [狂暴·攻速、铁壁·护甲→狂暴·伤害、尖啸回响]`
>   → `Boss 生命 900 护甲 200 伤害×1.485`（**护甲 200 而不是 350** = 那一栏真的没生效；
>   `1.485 = 0.9 × 1.25（换抽到的）× 1.32（压力 4 级）` = 换抽到的效果真的落了地）。
> - **同一条词条两次判定结果不同**（探针）：Boss.tscn → 成立；同一帧换成 Chaser.tscn → 不成立 → 换抽。
> - **回退验证**（清空 `ConfigIndex.tres` 的 `PressureMods` 数组跑一整轮）：日志写 `Pressure 0（ConfigIndex.tres 已加载）`
>   + `数值型 Pressure 8 条（来源：代码兜底）`，池明细与表在时**逐字相同**，技能参数/限定词/换抽全部照常，
>   `+187.5 → 1` / `+225 → 3` 与表在时逐字相同，Pressure 相关 warning/error **0 条**。
> - ⚠️ 已知不准确的一行（**本轮边界内改不了**）：清空索引时 `StageController` 那条日志仍硬编码写
>   `（来源：表 Pressure）`；真实来源看 `BossPerkCatalog` 打的那一行（它写"代码兜底"）。

### 阶段 4（原计划编号）：技能表 + 敌人表 + 节点表（T2 + T5 + T7）

- ✅ **节点表（T7）已在阶段 2 做完**：`Config/Tables/Nodes/node_*.tres` 9 行，替掉 `RewardService` 两个 switch + `NodePlacement`/`StageController` 的重复实现。
  ⏸ **未做**：`SpawnsEncounter` / `MaterialPath` / `IconPath` 三个字段已就位但**还没接进 `RewardNode`**（当前它们仍读各自场景里的值）。
- ✅ **敌人表（T5）已在阶段 3 做完**：`Enemies/` 10 行（1 全局 + 9 敌种），生成项按**预制体路径**配对、
  白名单按 `AllowedEnemyIds` 匹配（`StageDefinition.AllowsEnemy`），生成时"先 ApplyTo 再 AddChild"。
- ✅ **技能表（T2）已在阶段 3 做完**：`Skills/` 25 行（5 玩家 + 16 单位 + 4 首领行为词条），
  通用项 + `Params` + `AttackParams` 三类参数；`AbilityId` 会被写成技能行 Id（矩阵改成按类名匹配，见阶段 3）。

### 阶段 5：首领增强表 + 局外成长表（T9 + T10）+ 删旧代码

- ✅ **局外成长表（T10）已在阶段 2 做完**：`Config/Tables/Meta/meta_*.tres` 5 行，`MetaProgress` 的 5 个系数与价格公式都改成查表（存档键 `SaveKey` 保留 `health_level` 等原键名，**存档兼容**）。
  ⏸ `RunSettlement.cs` 的 3 个 `const`（10 / 3 / 50 碎片）**仍没外置**（建议直接搬进 `ConfigIndexData`，不是"按类型的行"）。
- ✅ **首领增强表（T9）三个阶段全部做完**（阶段 3 搬表 + 阶段 6 接"节点驱动"）：
  1. ✅ 7 条词条已搬进表（4 条行为型 → `Skills/bhv_*.tres`，3 条数值型 → `Pressure/pmod_*.tres`），
     `EncounterController` 走 `BossPerkCatalog.Roll`（内部 `WeightedPick`，rng 照旧是本目录的静态 Rng）。
     ⚠️ 阶段 6 起 `Roll` **只抽行为型**（原先是合并的 7 条）—— 数值型改由 `RollPressure` 负责，见「阶段 6」。
  2. ✅ `WeightedPick` 已就位并使用中（阶段 6 起 `Weight` 真的进了概率分母，有 30000 次实测）。
  3. ✅ **"节点驱动"已接**（阶段 6）：`NodeConfig.BossPressurePoints` / `BossModifierRolls` / `BossModifierTags`
     全部接进 `StageController.AddPressureForNode`；抽取按 `BossPerkCatalog.PressureOnly` / `BehaviorOnly` 分开。
     ⚠️ 落地形态与本文原稿不同：**没有**给 `EncounterController` 加 `BossModifierRolls` 字段 ——
     改成 `StageController` 排队 + `BossSpawned` 信号套用，理由是"一关怎么攒 Pressure"不该渗进"一场遭遇"层。
- ✅ **`_fallback` 旧目录没删**（`RewardCatalog.Fallback` / `StatusPresets.Fallback`）—— 按设计这是回退路径，不是技术债。
- ⏸ 收尾决定 §4.5.6：`PoolTags` 改成 `UpgradePoolId` 用起来、还是删掉。 → ✅ **已拍板：删掉（阶段 4 落地）**，
  并新增 `UpgradePoolConfig` 升级池表 + `WeaponConfig.Pool` 正向指（见上面的「阶段 4」与 §4.5.0）。

**场景 A 的验收**（阶段 6 实测，逐条对应）：

| 原计划的验收动作 | 实测结果 |
|---|---|
| 改节点行的 `BossModifierRolls` 从 `0` → N，确认 Boss 身上的增强数变了 | ✅ 改成 `2` 后：`本次跨阈值 0 次 + 节点行额外 2 次 → 立即抽取 [疾行、狂暴]`，`pmods` 3 → 5（`pressure` 仍 3） |
| 把某条 `Weight` 改成 `100`，确认它几乎必出 | ✅ 等价实验：三行 `1/1/1` → 均匀 33%；`swift` 改 `0` → 0.5%；表里 `3/2/2` → 42.0/29.0/28.9%（各抽 30000 次） |
| 清空索引数组跑一次，确认回退到代码内置目录且只打 warning | ✅ 退一步的等价实验（清空**行里的新配置**而不是 `ConfigIndex.tres` 的数组，避免与并行代理抢 `ConfigIndex.tres`）：日志写 `节点表 9 行都没配 → 全部走「关卡行 → ConfigIndex 全局默认值」回退`，数值逐字不变，0 条相关 warning/error |
| （新增）确认数值真的落到 Boss 身上 | ✅ Boss 出生日志带套用后的血量/护甲/乘区：`[铁壁、疾行、疾行]` → 1260 / 350 / 移速 ×1.914，算术逐项吻合 |

> ⚠️ **HUD 没跟上（已知缺口，本轮未做）**：`EncounterController._bossPerkNames`（HUD 的 `[冲锋强化]` 那一栏）
> 只在 `SpawnBossNow` 里由 **Boss Build 抽取**填充，**不包含** Pressure 转换来的数值强化 ——
> 所以玩家看不到"Boss 因为我的挑战多拿了哪些数值强化"。要补的话得让 `StageController` 把套用的强化名字
> 也交给 HUD（`EncounterController` / `PlayerHud` 不在本轮允许改动的文件里）。

### 每阶段的统一验证清单

```powershell
# 1) 编译（必须 0 warning / 0 error）
& 'F:\Godot项目\项目a\build.ps1' -v minimal

# 2) 自测（改完必须删掉 selftest.flag！）
'1' | Set-Content 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'
$cli = 'F:\Godot项目\项目a\.gdmcp\bin\gdmcp.exe'
& $cli tool-call run_project --args-json '{"allow_window":true,"scene_path":"res://Scenes/Main.tscn"}' --apply
Start-Sleep -Seconds 20
Remove-Item 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'
& $cli tool-call stop_project --args-json '{"allow_window":true}' --apply

# 3) 读日志，比对阶段专属的期望值
$dir = 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\logs'
$f = Get-ChildItem "$dir\*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Get-Content $f.FullName | Where-Object { $_ -match '波开始|清场完成|端到端|关卡表|配置表|使用默认' } | Select-Object -Last 12

# 4) 回退路径的主动验证（每阶段必做一次）：把表文件改名 → 跑 → 确认只有 warning、功能正常、日志显示"使用默认值"
```

---

## 6. 需要你拍板的取舍点

### 6.1 ⭐ 效果怎么表达（最重要的一条）

**现有 19 条增幅项里，效果是 C# 委托**（`RewardDefinition.cs:57` 的 `Action<RewardContext> Apply`），**委托无法序列化进 `.tres`**。三条路：

| 方案 | 数据化程度 | 加一条新增幅要做什么 | 风险 |
|---|---|---|---|
| **A. `ApplyHandler` 字符串 + C# 处理器注册表**（本设计推荐） | 14/19 条纯数据；5 条异化各一个 handler | 纯数值增幅：**只加一段 `.tres`**；新机制：加一行 `Handlers[...] = …` 并重新编译 | 低。表仍是**可校验的数据**：`RewardTable.Definitions()` 能在读表时就把"`ApplyHandler` 未注册"抓出来并警告（§3 T3 已实现） |
| **B. 表里内嵌 GDScript / 表达式** | 19/19 | 只加一段文本 | **高**。表从"数据"变成"逻辑"：Inspector 里编辑不了、没有类型校验、策划能写出运行时才炸的东西、`docs/` 与代码审阅全部失效 |
| **C. 全部保留 C#，只把数值搬进表** | 数值 100% / 结构 0% | 加一条新增幅仍要改 C# | 低但收益小 —— 策划能调数值但不能加内容 |

**我的建议是 A**，理由是它精确匹配用户原话里的分界："多数增幅项的基本是一个参数，也就都通用"——**通用的部分（改 `StatSheet`）完全数据化，不通用的小部分（5 条异化 = 26%）才留一行 C#**。这也和项目既有的"加内容只加一行定义"（[rewards.md](rewards.md):13）一脉相承，只是那一行从 `.cs` 挪到了 `.tres`。

**需要你确认**：是否接受"**新增机制仍然要写一行 C#**"？如果要求"策划不碰代码就能加全新机制"，那就只能走 B，我需要重新设计（并且要接受上面那些代价）。

### 6.2 表文件的粒度

**这一条已经由 [config-tooling.md](config-tooling.md) 定了，本文只是确认与说明理由。**

| 方案 | 结论 |
|---|---|
| ⭐ **一行 = 一个 `.tres` 文件，一个文件夹 = 一张表**（`Config/Tables/Weapons/wpn_pistol.tres` …） | **采用**。这是 `Edit Resources as Table` 插件的分组单位（[config-tooling.md](config-tooling.md) §五"落地步骤 · 第 0 步"）。附带好处：`git diff` 一次只动一个文件，`git blame` 能追到具体某一行数值是谁改的 |
| 一个表一个 `.tres`，行是内嵌 `[sub_resource]`（与 `ConfigIndex.tres:114` 的 `Stages` 同构） | **不采用，但保留为退路**。项目里现有的 `Stages` 就是这个形态、机制 100% 验证过，而且不需要"数组元素指向独立 `ExtResource`"这条未验证的写法（§7.2）。**如果阶段 0 发现"一行一个文件 + 编辑器拖进数组"这条路走不通，就退到这个形态** —— 运行时读表逻辑完全不变，只是插件编辑起来要多一跳（把数组列单独打开成另一张表） |
| 一行一个 `.tres`，但**不设索引**（运行时 `DirAccess` 扫目录） | **不采用**。① 只被目录扫描引用、没有任何 `ExtResource` 指向的资源，在导出到发布版时有掉出打包范围的风险；② 会让"`ConfigIndex` 是唯一数值入口"（[todo-handoff.md](todo-handoff.md):83）这条既有约定失效 |

**索引怎么办**（这是"一行一个文件"唯一需要额外考虑的事）：索引用 `ConfigIndex.tres` 上的 `[Export] XxxConfig[]` 数组，元素指向各行的 `.tres` 文件（`ExtResource`）。这样：

- **编辑时**：插件认 `Config/Tables/Weapons/` 这个文件夹 = 一张表 ✅
- **运行时**：`ConfigIndex.Settings.Weapons` 是唯一入口，数组里的行都被显式引用 → 导出不会掉文件 ✅
- **加一行**：建一个 `.tres` 文件 → 拖进 `ConfigIndex.tres` 的数组里（或在插件里 `Ctrl+D` 复制行、`Ctrl+R` 改名，然后补一次数组引用）
- **少了一行**：`ConfigTable.ById` 返回 null → 回退到组件自身默认值 + 一条 warning ✅

> **为什么不在每个表文件夹里放一个 `weapon_table.tres` 索引文件**：`Config/Tables/` 根下放 9 个索引文件，插件打开 `Tables/` 时会把它们当成一张"混合表"；放进各自的分组目录又会让索引本身出现在该表里（多一行垃圾行）。挂在 `ConfigIndex.tres` 上三边都干净。
> **这条约定要写死在 `docs/` 里**（建议并入 [assets.md](assets.md)，见 §6.7），否则以后会两种风格混用。

**这一条的真实代价（不粉饰）**：行独立成文件后，行数 = 文件数 —— 19 条增幅 + 3 武器 + 5 技能 + 3 角色 + ~9 敌人 + 3 关卡 + 9 节点 + 5 状态 + 7 词条 + 5 局外 = **约 68 个小 `.tres` 文件**。
换来的是：① 插件能按文件夹批量编辑（这是用户要的"集中处理这些表的地方"）；② 一次改动只脏一个文件；③ 单行可以改文件名/挪目录而不影响别人。
**如果你觉得 68 个文件太散**，最省事的缓解办法是在编辑器里用 `ResourceTables` 那个标签页看（它按文件夹聚合，不靠资源管理器翻），**而不是**退回内嵌形态 —— 退回内嵌等于放弃插件。

### 6.3 数值权威在哪：表 vs `.tscn`

`Scenes/Weapons/Pistol.tscn:16-25` 与 `ConfigIndex.tres:74-84` 现在是**两套值**（§1.4-③ 的 10 行对照表）。分表时必须定一条：

| 选项 | 含义 |
|---|---|
| **表是唯一权威**（推荐） | `Pistol.tscn` 的 10 个数值清空（改回 `Weapon.cs:15-49` 的组件默认值）。视觉上 Inspector 里的手枪数值变成"默认值"，真正的值在 `Config/Tables/Weapons/wpn_pistol.tres`。表读不到时用组件默认值 —— 完全符合用户要的回退语义 |
| **场景是唯一权威** | 表只放"哪把武器在哪个槽位、指向哪个预制体"，数值全留在 `.tscn` 里。表变薄，但"武器1/武器2 的参数表"就没内容了，与用户需求不符 |

**我建议第一条**，但这一条会让人"打开 `Pistol.tscn` 看到 22 伤害、实际打出来 24"再次困惑 —— 所以要做两件事：① `Pistol.tscn` 的数值**清空为默认值**（不留旧值）；② 组件上加注释指向表。**需要你确认这个体感**（策划以后改武器数值是打开 `Config/Tables/Weapons/wpn_pistol.tres`（或插件的表格视图）还是打开 `Pistol.tscn`）。

### 6.4 枚举是否保留

| 枚举 | 建议 |
|---|---|
| `WeaponPresetKind`（`ConfigIndexData.cs:6-11`） | **保留为快捷键壳**（`Pistol → "weapon_1"`）。删掉要动 `RunSetup.cs:16`、`StartScreen.cs:95-97`、`UiService.cs:97`、`RunController.cs:36,271,275,387-417`、`SelfTest.cs:88-89` —— 收益（少一个枚举）远小于风险。表成为权威之后，枚举只是"UI 上第几个按钮" |
| `PlayerCharacterKind`（`:14-24`） | 同上 |
| `RewardKind` / `ModifierOperation` / `RewardNodeKind` / `HitPolicy` | **保留**。它们会作为表字段的类型出现在 Inspector 里（下拉框），这是**好事**（比填字符串安全）。`.tres` 里存的是整数下标（`Operation = 1`），手写时务必对照枚举顺序，见 §3 T3 的提醒 |

### 6.5 要不要字段级"部分覆盖"开关

用户要的回退是**整表级**的（"读不到则使用自身的默认值"）。但有时只想覆盖一两个字段（"这把枪只改伤害，其余跟预制体一致"）。可选做法：

```csharp
[Flags]
public enum WeaponFieldMask { None = 0, Damage = 1 << 0, FireInterval = 1 << 1, /* … */ All = ~0 }

[Export(PropertyHint.Flags, "Damage,FireInterval,MagazineSize,...")]
public WeaponFieldMask Overrides { get; set; } = WeaponFieldMask.All;
```

**我建议阶段 1-4 都不做这个**（YAGNI：现在没有"只覆盖一部分"的真实需求，`EquipWeapon` 现在也是全量套用），等真出现"两把枪共用一套数值只差伤害"时再加。列在这里是为了让你知道我考虑过、并有明确的加装方式。

### 6.6 键值对的 `Value` 类型

| 选项 | 优点 | 缺点 |
|---|---|---|
| **`Variant`**（推荐） | 一个字段装下数字/字符串/布尔/路径 | 读取要 `AsFloat()`/`AsText()`；`[Export] Variant` 的编辑器支持**我没实测**（§7） |
| `float Number` + `string Text` + `Kind` 枚举 | 100% 确定可导出 | 每个条目多占两个字段；拼参数时要想"这个值该填哪一栏" |

**我建议先按 `Variant` 设计**，阶段 0 的探针 `.tres` 一验证就知道行不行；不行就切成保底方案（`ParamEntry` 的 4 个访问器已经把差异挡住了，业务代码一行都不用改）。

### 6.7 `Config/` 的命名规范归谁

[assets.md](assets.md):4 自称"本文是资源目录与命名的唯一约定"，但它的 §1 目录树只管 `Assets/`，`Config/ConfigIndex.tres` 并不符合它的 `<类别>_<分组>_<变体>.tres` 规则。**需要你定**：是把 §2.3 的 `Config/` 约定并进 `assets.md`（那里是"资源规范"）、还是单独立一份 `docs/config-schema.md`（本文）为准？**我建议并进 `assets.md`**，理由与它 §2.5 的第 4 条一样：规范只在一处才有人查。

### 6.8 局外成长的价格曲线

`MetaProgress.cs:47` 现在是 `CostOf(level) = 10 * (level + 1)`（线性）。进表之后是 `BaseCost=10` + `CostGrowth=10`（同样线性），**表达力没变**。如果你以后想要指数/分段曲线，表里要么加一个 `Curve` 资源字段，要么把 `CostGrowth` 改成乘数。**这一条不影响现在做不做表，只需要你确认"线性够用"**。

### 6.9 ⭐ 武器 ↔ 升级池：关联方向（场景 B）—— ✅ **已拍板：正向指（2026-10，已实施）**

> **结论**：用户选**正向指** —— 「武器的配置表里确实是需要有指向对应池表的字段，而**对应池表则不指向其对应武器**，
> 而**池表里则是一个选项占用一栏**」。落地形态见 **§4.5.0**，实测见 **§5 阶段 4**。
> 关键点：正向指**没有**引入第二个真源 —— 武器行写"我属于哪个池"，**选项行**写"我属于哪个池"，
> 池行自身不写任何一方；两边都是**资源引用**（不是字符串）。
> 下面的对比表保留为决策记录（"反向指"一栏**未采用**）。

| 选项 | 优点 | 缺点 |
|---|---|---|
| ⭐ **正向指**：`WeaponConfig.Pool : UpgradePoolConfig`（**实际采用**） | 打开武器行**直接看到**它的池（用户要的就是这个）；加池只碰池表；池可被多把武器共用 | 要看"池里有哪些选项"得按**选项行的 `Pool` 列**筛选（不能打开池行就看到列表）—— 这是"一个选项占一栏"的必然结果 |
| ~~**反向指**：`RewardConfig.ApplicableWeapons : WeaponConfig[]`~~ | 加一条增幅项只碰一个文件 | **未采用**：反向视图不符合用户要的"武器 → 池"入口；且用户明确说池表不指武器 |
| **双向指**（两边都写） | 都能直接看到 | **两个真源**，必然漂移 —— §1.4-① 的关卡表就是这么坏的，**不要重犯**（本轮也没有犯：成员关系只在选项行上） |

### 6.10 ⭐ 场景 A 的范围：只做表，还是连"节点驱动"一起做 —— ✅ **A-1 已在阶段 3 做，A-2 已在阶段 6 做**

用户说的「奖励节点用于增强首领」包含两件事，**工作量差很多**：

| 范围 | 内容 | 风险 |
|---|---|---|
| **A-1 只把现有机制搬进表** | 7 条词条 → `bhv_*.tres` + `pmod_*.tres`（`Weight` 真的生效）；压力参数从 `ConfigIndex` 收到关卡表；删掉 `Boss.tscn` 与 `ConfigIndex.tres` 的重复 | 低。行为可控，有 `PlayerHud` 的词条名显示可验证。✅ **阶段 3 已完成（`Weight` 的"真的生效"落在阶段 6）** |
| **A-2 再加上"节点驱动"** | `NodeConfig.BossPressurePoints` / `BossModifierRolls` / `BossModifierTags` + `StageController` 累加 + Boss 出生时套用 | 中。**这是新功能**，会改数值曲线（同一个关卡打不同节点，Boss 强度不同），需要重新调平衡。✅ **阶段 6 已完成** |

**当时的建议是"A-2 单独排一轮并明确标注为新增玩法"** —— 阶段 6 就是那一轮，落地时的取舍：

| 待定问题 | 阶段 6 的答案 |
|---|---|
| `BossModifierRolls` 该不该随节点累加、还是按节点类型分档？ | **按节点类型**：它是**节点行**上的一个值（每个挑战类型各自决定"额外抽几次"），所以"打哪个节点"本身就是策略；不随"打了几个"累加 |
| 数值曲线怎么改？ | 见 [config.md](config.md) 的「一处需要平衡测试的可见变化」：转换的产出从"压力等级"换成"随机数值强化"，**仍需一轮平衡测试** |
| 会不会破坏 §7.1 的分离？ | 不会：两个抽取点各用各的候选集（`BehaviorOnly` / `PressureOnly`），这是本轮的硬约束 |

---

## 7. 附录：API 验证结果 / 未验证项

### 7.1 已用 `GodotSharp 4.7.2` 反射核对通过（可以放心写进代码）

| API | 实际签名 | 用在哪 |
|---|---|---|
| `ResourceLoader.Exists` | `Exists(string path, string typeHint = "")` → `bool` | `ConfigTable.TryLoad` |
| `ResourceLoader.Load<T>` | `Load<T>(string path, string typeHint = "", CacheMode cacheMode = …)` → `T` | `ConfigTable.TryLoad` |
| `GD.Load<T>` | `Load<T>(string path)` → `T` | （备选） |
| `GodotObject.Set` | `Set(StringName property, Variant value)` → `void` | 技能表 `Params` 套用（§3 T2） |
| `GodotObject.Get` | `Get(StringName property)` → `Variant` | `Params` 键名校验、`ConfigText` 取值 |
| `Variant.VariantType` | `Variant.Type VariantType { get; }` | 类型判定 |
| `Variant.Type` | 枚举含 `Nil / Bool / Int / Float / String / StringName / Object / Array / Dictionary` 等 39 个值 | 同上 |
| `Variant.AsSingle/AsInt32/AsString/AsBool` | 均存在 | `ParamEntry` 的访问器 |
| `Variant` 隐式转换 | `op_Implicit(Single) → Variant`、`op_Implicit(String) → Variant`、`op_Implicit(Boolean) → Variant` 等 | `ParamEntry.Value` 直接赋值 |
| `Godot.Collections.Array<T>` | 有 `ctor()`、`ctor(T[])`、`ctor(IEnumerable<T>)` | 泛型数组导出 |
| `Godot.Collections.Dictionary<TKey,TValue>` | 两个泛型参数都标了 `[MustBeVariant]`（结构上是给导出用的） | 备选方案（§4.3） |
| `Godot.ExportAttribute` | 有 `Hint` / `HintString` 属性 | `[Export(PropertyHint.Range, "...")]` 等 |
| `Godot.GlobalClassAttribute` | 存在 | 所有表/行类 |
| 项目内已验证的导出形态 | `ConfigIndexData.Stages` 是 `StageDefinition[]`，`ConfigIndex.tres:114` 存成 `Array[Resource]([SubResource("Stage_1"), ...])`；`119 = 74 + 3×15` 数字完全对齐 | **§4.3 主方案的全部依据**（`ParamEntry[]` 与 `StageDefinition[]` 同构） |

### 7.2 未验证项 → **实测结论（2026-10，阶段 0/1 的探针）**

> 探针怎么做的：`Config/_probe/` 下建 2 个独立行 `.tres` + 1 个持有 `StageDefinition[]` 的 `.tres`，
> 在**运行中的 Godot 4.7.2 编辑器**里（gdmcp `execute_editor_script`）加载/保存/回读，
> 探针文件验证完已删除。**索引那一行是照引擎自己的输出写的**（先让 `ResourceSaver` 写一遍 `probe_ext_resaved.tres`，再照抄语法）。

| 项 | 结论 | 证据 |
|---|---|---|
| ⭐ **数组元素指向独立 `ExtResource`** | ✅ **可以用** —— "一行一个 `.tres` + 索引数组"这套地基成立 | 手写 `Stages = Array[Resource]([ExtResource("2_a"), ExtResource("3_b")])` → 编辑器加载得 **2 个** 元素、类型是 `StageDefinition`、`resource_path` 指向各自文件；引擎自己重存一遍写成 `Stages = [ExtResource("1_lx87h"), ExtResource("2_jdwmd")]`（**4.7 的规范写法是裸 `[...]`**，不再包 `Array[Resource](…)`），说明**存、读、回存三个方向都通** |
| 数组元素的**行文件缺失**时 | ⚠️ **比预想严重**：编辑器侧那一项变成"没有脚本的占位 `Resource`"；而**游戏侧整份宿主资源加载失败**（实测：改名 `stg_inner.tres` → 游戏里 `ConfigIndex.tres` 未加载、玩家生命 250→160、关卡表回退成单关默认） | 见 §5 阶段 1 的验证表与操作纪律 |
| **往 C# 资源数组里赋值**（Inspector 拖拽 / `Object::set` / `UndoRedo.add_do_property`） | ❌ **对"已落盘的资源"静默失效**（属性保持原值，无报错）：`[rs.new()]` 能写进去（1 个），`[ResourceLoader.load(...)]`、`[duplicate()]`、`[ConfigIndex.tres 自己的子资源]` 全部写不进去（0 个） | 5 种写法逐个复现。结论：**索引数组只能靠写文本**（或 C# 代码赋值）。这条直接影响 §6.2 "加一行 → 拖进数组"的日常动作 |
| `[Export] public Variant Value` | ✅ C# 侧可导出（`ParamEntry.Value` 编译通过） | `ParamEntry.cs` 已落地并在构建中验证；编辑器里的**可视化编辑**尚未冒烟（无消费者，等 T3 真正用起来时按 §5 阶段 2 验证） |
| `[Export] ParamEntry[]`（自定义 `Resource` 数组） | ✅ 与 `Stages` 同构，可用 | 同"数组元素指向独立 `ExtResource`"一条 |
| `[Export(PropertyHint.Flags, "...")]` + `[Flags] enum` | ⏸ 未试 | 阶段 1–4 本来就不做字段级覆盖（§6.5） |
| `Ability.AimSource` 这类**嵌套在别的类里的枚举**作为表字段 | ⏸ 未试 | 等 T2 技能表时再验 |
| `Edit Resources as Table` 插件在 4.7.2 上能不能用 | ⏸ 本次**没测**（项目里没装；`addons/` 下只有 `godot_mcp`） | 注意新增的前提：它靠 Inspector/`ResourceSaver` 保存，若它用"拖拽填数组"的方式加索引，会撞上前一条的坑 |

> **注意最后一项的地位**：本设计的**运行时读取路径完全不依赖那个插件** —— 插件只是让"批量改表"更舒服。

### 7.3 ⭐ YARD 真机冒烟结论（2026-10，阶段 2）→ ✅ **出下拉，可以用**

调研文档 [config-tooling-yard.md](config-tooling-yard.md) §1.3 明确声明过它**唯一没验证**的一步是
"生出来的 `PropertyInfo(hint=1024)` 在 Inspector 里**真的会长出下拉框**"（当时本机没装编辑器，只到了"代码生成层实测 + 引擎层强推断"）。
**本次在真机上跑完了这一步，结论是 ✅。**

**探针怎么做**（用完已删干净）：

| 步骤 | 做法 |
|---|---|
| 1. 建临时注册表 | `Config/Registries/_yard_probe.tres`（用 YARD 自己的 `RegistryIO.create_registry_file()` 生成，再手工塞 1 个条目 `yard_probe_registry`） |
| 2. 建临时 C# 行类 | `Scripts/Config/YardProbe.cs`，4 个字段：两个 `[Export(YardRegistryHint, "res://Config/Registries/_yard_probe.tres")]`（`StringName` + `string`）、一个同写法的 `Godot.Collections.Array<StringName>`、一个普通 `[Export] StringName` 当对照 |
| 3. 建临时行资源 | `Config/Tables/_Probe/row_yard_probe.tres` |
| 4. 编译 | `build.ps1 --no-incremental` → **0 warning / 0 error**（`(PropertyHint)1024` 能编译） |
| 5. 看真机 | 编辑器（gdmcp `execute_editor_script` + 截屏）里 `EditorInterface.edit_resource(...)` 打开这一行，遍历 `EditorInterface.get_inspector()` 的控件树 |

**证据（三层，逐层变硬）**：

| 层 | 结论 | 证据原文 |
|---|---|---|
| ① C# 侧 hint 元数据 | ✅ 1024 完整保留 | `ProbeId type=21 hint=1024 hint_string='res://Config/Registries/_yard_probe.tres' usage=4102`（21 = `TYPE_STRING_NAME`）<br>`ProbeIdString type=4 hint=1024 …`（4 = `TYPE_STRING`）<br>对照 `PlainId type=21 hint=0 hint_string=''`（普通文本框） |
| ② 数组列 | ❌ 被源码生成器覆盖（**与调研预测一致**） | `ProbeIds type=28 hint=23 hint_string='21/0:'`（23 = `TypeString`，1024 被丢掉） |
| ③ **真机 Inspector 控件** | ✅ **`OptionButton` 真的出现了** | 控件树（真实节点路径）：<br>`…/@EditorInspector@7861/…/@EditorProperty@61301` = `ProbeId` → 其子节点 `@OptionButton@61300`，**`OPTIONS=["yard_probe_registry"] selected=-1 visible=true`**<br>`…/@EditorProperty@61315` = `ProbeIdString` → `@OptionButton@61314`，**同样的 OPTIONS**<br>对照：`ProbeId` 的 `ProbeIds` 是 `EditorPropertyArray`（无按钮）、`PlainId` 是 `EditorPropertyText` |
| ④ 肉眼 | ✅ 点开下拉能看到条目 | `docs/screenshots/yard_probe_inspector.png`（点 `Probe Id` 右侧箭头后弹出 `yard_probe_registry` 那一项） |

**所以三种结论里属于哪一种：✅ 出下拉。** 之前唯一"没在真机点过"的那一步现在补上了，而且三层证据方向一致。

**顺带实测到的两条（对以后写字段有直接影响）**：

1. **同一个 hint 写法在 `StringName` 与 `string` 上都出下拉**（`type=21` 与 `type=4` 都拿到了 `hint=1024`）。
2. **`Array[StringName]` / `Array[string]` 出不来**（`hint=23`，YARD 的 `_parse_property` 第一句 `if hint != 1024: return false` 就返回了）
   → **一条跨表引用 = 一个标量 `StringName` 字段**，这是 C# 侧唯一可用的形态（想放多个就多写几个标量字段）。
3. **YARD 注册表 `.tres` 由引擎写入时会带 `uid=` 与 `uid://` 脚本引用**，与项目"`ext_resource` 只写 `path=`"的既有约定（[assets.md](assets.md) §5）冲突。
   本轮探针里已实测 `ResourceSaver.save()` 会写进 `[gd_resource … uid="uid://…"]` 与 `[ext_resource … uid="uid://…" path=…]`。
   **结论：如果以后真要用 YARD 注册表，它属于"引擎生成的派生清单"，建议单独放 `Config/Registries/`，不要和 `Config/Tables/` 的行混在一起**（YARD 自己的 Reindex 也会重写它）。
> 所以插件在 4.7.2 上坏了，最多退回到"用 Inspector 按文件改"或"用文本编辑器改 `.tres`"，**不影响分表方案落地**。

---

## 8. 阶段 2 之后的现状速查（给下一个接手的人）

### 8.1 `ConfigIndex` 的最终形态 = **唯一的数值门面 + 唯一的表索引持有者**

| 它是什么 | 说明 |
|---|---|
| ✅ **仍是全项目数值的唯一入口** | 业务代码继续写 `ConfigIndex.Settings.Xxx`（[todo-handoff.md](todo-handoff.md) 的"统一入口"约定**没变**）。`ConfigIndex` 从"唯一数值**文件**"变成"唯一数值**门面**"（§2.1 就是这么设计的） |
| ✅ **同时是 14 张表的索引持有者** | 13 个 `[Export] XxxConfig[]` 数组 + 既有的 `Stages`，元素全部是独立 `ExtResource`：<br>`Stages` / `Weapons` / `Characters` / `Enemies` / **`Bosses`** / **`Summons`** / **`AiProfiles`** / **`Skills`** / **`PressureMods`** / `Effects` / `Nodes` / `Upgrades` / **`UpgradePools`** / `MetaUpgrades`（阶段 3 新增的 4 个、阶段 4 新增的 1 个、**阶段 5 新增的 1 个**加粗） |
| ⏸ **没有**把索引分散到各系统 | **理由**：① 分散会造出 12 个"谁持有哪张表"的新约定，[architecture.md](architecture.md):134「加新全局入口前先问：能不能传参」的精神是**少入口**；② `ConfigRows` 是一个静态索引器，谁来 `Rebuild` 都一样，而现在只有 `ConfigIndex._Ready` 一处；③ **回退语义要能一眼看出"整份配置有没有加载上"** —— `ConfigIndex.FileLoaded` 已经是那个开关，分散后它会失去意义 |
| 顺序依赖 | `ConfigIndex._Ready` → `CallDeferred(BuildTableIndex)`（**必须 defer**，见 §5 阶段 2 的坑 1）→ `ConfigRows.Rebuild(...)` ×13 + `SetKind` ×9 + `RewardTable.Build` + **`ReportUpgradePools()`（阶段 4：武器→池→选项的留痕）** + **`ReportAiProfiles()`（阶段 5：单位行→AI 档案的留痕）** + `BossPerkCatalog.Build(Config.Skills, Config.PressureMods)` + 关卡白名单校验（未知 Id 写警告） |
| `ConfigIndexData` 的标量字段 | **30 个**（标量；另有 **14 个表索引数组**。阶段 3 删掉两个死字段 `EliteBossHealthScale` / `BossHealthBarScale`） |

### 8.2 读表 / 回退的固定写法（新代码照抄）

```csharp
// 1) 按 Slot 取（"武器1 / 武器2 / 角色1"这种表现层顺序）
WeaponConfig? row = ConfigRows.BySlot<WeaponConfig>((int)kind + 1);
if (row != null) { row.ApplyTo(_weapon); return; }   // 表在 → 表是权威
// 2) 表不在 → 什么都不做，目标保留自身 [Export] 默认值（+ 一条 warning / GameLog）
GD.PushWarning($"[Config] 武器表里没有 {kind} 的行，保留武器组件自身的默认值。");

// 按 Id / 按枚举（节点表用 ByKind）
NodeConfig? node = ConfigRows.ByKind<NodeConfig, RewardNodeKind>(kind);
MetaUpgradeConfig? meta = ConfigRows.ById<MetaUpgradeConfig>("meta_health");

// 按"预制体路径"配对（阶段 3：敌人 / 首领 / 召唤物的生成项 ↔ 表行）
EnemyConfig? enemy = ConfigRows.ByScenePath<EnemyConfig>(entry.Scene.ResourcePath);

// ⚠️ 单位表套用的时机：必须在 Unit._EnterTree 之前（Unit 在 _EnterTree 捕获基础值）
var unit = scene.Instantiate<Unit>();
row?.ApplyTo(unit);        // ← 先套用（生命/攻击/技能/标签/表现）
AddChild(unit);            // ← 再进树（触发 _EnterTree → _Ready → 回满生命）
```

**单位表的继承关系**（新表照这个模式加）：

```
ConfigRow（Id / Slot / DisplayName / DescriptionTemplate / Note / Params / ApplyTo）
  └─ UnitConfig（生命 · 攻击四项 · 移速 · Tags · Skills · 表现）
       ├─ EnemyConfig（+ 全局倍率行 + 刷怪五列）
       ├─ BossConfig（+ ScenePath + EliteCopyHealthScale）
       ├─ SummonConfig（+ ScenePath + LifetimeSeconds）
       └─ CharacterConfig（+ 武器开关 / 能力开关矩阵）
```

### 8.3 ⚠️ 操作纪律（这一条必须遵守，否则"整份 ConfigIndex.tres 静默回退"）

**不要单独删/改 `ConfigIndex.tres` 引用到的行文件。**
只要 `ConfigIndex.tres` 引用的**任何一个**行文件缺失 / 改名，Godot 会**整份 `ConfigIndex.tres` 加载失败**（不是只丢那一行），
于是**所有**数值静默回到代码默认值（实测过：玩家生命 250 → 140、武器变成 `Weapon.cs` 的 22/0.32/8/1.15）。
回退是"安全的"（只有 warning、能正常玩、日志留痕），但比"只影响缺的那一行"粗得多。

**要下线一行**：把 `ConfigIndex.tres` 里的数组项与顶部对应的 `[ext_resource]` **一起**删掉，再删文件。
**要重命名一行**：先改 `[ext_resource]` 的 `path=`，再 `git mv` 文件（中间不要留空窗）。

### 8.4 手写 `.tres` 的格式规矩（本轮又一次踩到）

- `[ext_resource]` / `[sub_resource]` **必须在 `[resource]` 之前**；`load_steps = ext + sub + 1`。
- `ext_resource` **只写 `path=`，不写 `uid=`**（项目既有约定）。
  ⚠️ 但**引擎自己保存时会写 uid**：用 `ResourceSaver.save()` 生成过一遍之后，`[gd_resource … uid=…]` 与 `[ext_resource … uid=… path=…]` 都会出现。
  本轮的做法是**让引擎生成（保证数组/子资源写法规范）→ 再用脚本把两处 `uid=` 正则剥掉**。
- UTF-8 无 BOM + **LF**。
- **别手抄文档里的示例**：先让引擎 `ResourceSaver.save()` 写一遍，再照抄它的输出语法（阶段 1 就是这么做的）。
- ⚠️ **编辑器有资源缓存**：改完 `ConfigIndex.tres` 后立刻用 `ResourceLoader.load` 读会拿到**旧版本**（表现为"某个数组是 0"）。
  修法：`EditorInterface.get_resource_filesystem().update_file(path)` + `CACHE_MODE_REPLACE`。**游戏冷启动不受影响**。
