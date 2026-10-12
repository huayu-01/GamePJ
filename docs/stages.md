# Stage / Node 流程（阶段 7）

对应《策划文档》§6「游戏流程与关卡节点」、《执行文档》阶段 7。

## 主循环（策划文档 §6.1）

```
Base → Configure → Stage → 自由探索 → 选择 Node → 随机奖励 → Encounter
     → 完成 Encounter → 继续探索 → Boss Door → Boss → Next Stage → Final Boss → Base
```

Vertical Slice 目前覆盖 **Stage 内的完整循环**（自由探索 → Node → 奖励 → Encounter → Boss Door → Boss），
Base / Configure / Next Stage / Final Boss 属于后续阶段。

## Node 是什么（关键，别搞错）

**Node 是放置在地面上、供玩家走近互动的物件**，不是「按顺序自动执行的战斗关卡」。

- 每个 Stage 有多个**候选节点位置**，每局**随机布置**节点，减少路线重复。
- 节点类型：强化池 / 进化池（= 用户口语里的"武器池"，见下「波数按价值标签」）/ 异化池 / 局外资源 / 恢复状态 / 特殊奖励 / 补给站。
  ⚠️ **没有"武器池"这个类型**（用户 2026-10-12 拍板 A3：「没有定义武器池，只是一个说法，确切来说是进化池」）——
  旧文档与界面文案里的"武器池"指的就是**进化池**（武器 = 角色的攻击方式，进化池 = 它的限定池）。
- 节点**生成后固定**，且**通常只能使用一次**（用掉后变暗）。
- 流程是「选择 Node → 随机奖励选择 → Encounter」：互动拿奖励，然后打一场战斗。
- **Boss Door 随时可以打开**，玩家可以放弃剩余普通节点直接挑战 Boss。

对应实现：`Scripts/Interaction/Interactable.cs`（基类）、`RewardNode.cs`（奖励节点）、`BossDoor.cs`（Boss 门）、
`PlayerInteractInput.cs`（寻找最近节点、按 F 互动、给 HUD 提示）。

## Stage 与 Encounter 的关系

- `StageController` 管「关卡」：布置节点、免费挑战额度、Boss 门、胜负。
- `EncounterController` 管「一场战斗」：Spawn Budget（总预算 / 再生 / 上限 / 并发 / 成本 / 时间门槛）。
- Stage 下有一个作为**战斗模板**的 Encounter；玩家互动节点后按模板开一场，打完回到自由探索 —— **不再自动串行推进**。

策划文档 §6.4/§6.5 的约束由 Encounter 侧保证：必须击杀本场全部敌人才能完成；预算耗尽不再生成，但已生成的敌人仍须清光。

## Free Challenge Quota 与 Boss Pressure（§6.3）

- 每个 Stage 有**免费挑战额度**（例如前 2 次免费）。
- 超过额度后**增加 Boss Pressure**（不决定 Stage 是否完成）。
- Boss Pressure 与 Boss Skill Acquisition / Boss Build 是分开的概念。

实现：Stage 记录已挑战次数，超出额度部分作为 **Boss 战的初始压力等级**传给 `BossPressure.InitialLevel`；
Boss 战内的压力仍按时间继续升级。

## Stage 序列与 Final Boss

一个 Stage 场景被复用于所有关卡，差异全部来自**关卡表**的数据（策划文档 §6.2：Stage 之间靠敌人强度与进度区分）。

> **关卡表的位置**（配置表体系阶段 1，见 [config-schema.md](config-schema.md) §3 T6 / §5）：
> **一行 = 一个 `.tres` 文件**，放在 `Config/Tables/Stages/`：`stg_outer.tres` / `stg_inner.tres` / `stg_core.tres`；
> 关卡顺序与索引仍是 `Config/Balance.tres` 上**已有的** `[Export] StageDefinition[] Stages` 数组（元素指向上面三个文件）。
> 改某一关的数值 → 打开那一行的 `.tres`（或整张表一起看）；**不要**再往 `Balance.tres` 里塞内嵌 `sub_resource`。

| 字段 | 含义 |
|---|---|
| `Id` | 关卡稳定键（`stg_outer` / `stg_inner` / `stg_core`），与行文件名同语义名；日志用它证明"这一关读的是哪一行" |
| `DisplayName` | 关卡名，显示在 HUD |
| `CombatBudget` / `MaxConcurrent` / `SpawnInterval` | 本关战斗的 Spawn Budget 与节奏 |
| `EnemyHealthMultiplier` / `EnemyDamageMultiplier` | 本关敌人额外缩放（在全局平衡倍率之上再乘） |
| `EliteChanceScale` | 精英概率附加（加在各生成项自己的 EliteChance 上） |
| `AllowedEnemies` | 本关允许生成的敌人（按生成项 DisplayName 匹配；留空 = 不限制） |
| `RewardNodeCount` / `FreeChallengeQuota` | 本关布置几个节点、免费挑战几次 |
| `BossGuardCount` / `BossHealthMultiplier` / `BossDamageMultiplier` | Boss 护卫数与 Boss 缩放 |
| `BossPerkCount` | Boss 词条数量（Boss **Build**，即行为/技能） |
| `EliteBossMinWave` | **白板首领的波次门槛**（本关累计第几波之后才出；三关都是 8，留 0 = 闸门关掉）。见 [boss.md](boss.md) / [config.md](config.md) |
| `IsFinalBoss`（**已删**） | 旧字段，配置表阶段 3 已下线 |
| `PressureGrowthMultiplier` / `PressureThreshold` | Boss **Pressure**（数值强化）的本关增长倍率与转换阈值（关卡核心文档 §7.2/§7.3） |
| `PressurePerNodeNormal` / `PressurePerNodeHighValue` | 普通 / 高价值挑战完成后增加的基础 Pressure（§7.4） |
| `PressureEvolutionBonus` / `PressureMutationBonus` | 进化 / 异化挑战的额外比例（§7.4） |
| `PressureUpgradeTags` | 本关 Pressure 转换后允许进入的**数值型**强化候选标签（§7.4；留空 = 不限制）。⚠️ 数值强化池本身尚未实现（§7.5），当前只写进日志 |

> **Pressure 字段的取值规则**：行里填了就用行里的；留 0（= 未设置）就用 `Balance.tres` 上的全局默认值
> （`PressurePerHighValueNode` 150 / `PressureEvolutionBonus` 1.25 / `PressureMutationBonus` 1.5 / `PressurePerPoint` 100）。
> 当前三行**都没填**，所以 Pressure 与分表之前逐位一致。
> ⚠️ 关卡核心文档 §7.1 的硬约束：Pressure 只管**数值型**强化，行为/技能型走 Boss Build（`BossPerkCatalog`），两者不得合成一个池。

序列写在 `Balance.tres` 的 `Stages` 数组里（`Config/Balance.tres`），指向 `Config/Tables/Stages/stg_*.tres`。
**读不到 / 数组为空时回退到 `StageDefinition` 自身的 `[Export]` 默认值**（一关，`Stage 1`）；
代码里**不再有**"内置三关兜底"（`RunController.DefaultStages()` 已在配置表阶段 1 删除 —— 它和 `Balance.tres` 漂移过 5 处）。走表还是走默认值，开局日志里有一行明确写出。

**切关流程**：Boss 阵亡 → 还有下一关就 `ApplyStage`（节点重新随机布置、挑战计数清零、门复位、场上清空、玩家回出生点并回满、构筑与生命保留）并弹「本关完成」提示；最后一关的 Boss 阵亡 → 通关结算。

## Base 与局外成长（策划文档 §11）

主循环现在完整覆盖三个阶段：**Base → Configure → Stage**。

- **Base**（`Scenes/UI/BaseScreen.tscn`）：显示碎片，消费碎片买永久强化——**五项**：最大生命 +10 / 伤害 +5% / 移速 +3% / 暴击率 +3% / 装弹速度 +6%，费用都是 10×(等级+1)，按键 1~5 或点击购买，Enter 出征。
- **Configure**（ChooseScreen + StartScreen）：先选**角色**（远程枪手 / 近战重装），枪手再选**武器**（手枪 / 霰弹枪 / 步枪），重装直接用近战重击。
- **Stage**：三关自由探索、节点、战斗、Boss、最终 Boss。
- **结算**：通关或阵亡时按进度结算碎片（`10×关卡序 + 挑战次数×3 + 通关 50`），写入 `user://meta.cfg`；下局在 Base 里消费。
- 「局外资源」节点（策划文档 §6.2 的节点类型之一）现在有内容了：互动直接 +5 碎片。

## Esc / 弹层约定

- Esc：关闭当前最顶层可关闭的弹层；没有弹层时打开菜单（当前用帮助界面占位）。
- 弹层基类 `OverlayScreen` 提供 `OpenGuardSeconds`（默认 0.3 秒防误触）与 `OpeningDuration`（以后加缓动时保护期自动延长）。
- 暂停与鼠标捕获由 `UiRouter` 统一管理（栈空恢复游戏并捕获鼠标）。

## 实测（MCP 实跑，2026-01）

| 检查项 | 结果 |
|---|---|
| 开局状态 | 剩余节点 9 / 隔离次数 2 / 已挑战 0，无敌人（自由探索，不再自动刷怪）✓ |
| 每局随机布置 | 4 个候选位置里随机启用 3 个（本次 NodeEvolution 被禁用、不可见）✓ |
| 走近节点 | HUD 左下显示「按 F 使用：进化池」✓（记录当时写作"武器池"，2026-10-12 用户拍板 A3 后统一叫**进化池**） |
| 按 F | 弹出三选一，标题「进化池」，只出该类型奖励 ✓（同上：旧称"武器池"） |
| 选择后 | 构筑增加（本次拿到「吸血」）、节点变已用、已挑战 +1，立刻开一场 Encounter（场上 3 个敌人）✓ |
| 清场后 | 回到自由探索（剩余节点 2），不再自动推进 ✓ |
| Boss 门 | 按 F 立即出战 Boss + 2 技能精英，初始压力 = max(0, 已挑战 - 免费额度) ✓ |
| Boss 阵亡 | Stage IsVictory = true，弹出「胜利」结算 ✓ |

## 已知限制 / 待确认

- 进化池（旧称"武器池"，见上）、局外资源、特殊奖励目前没有独立系统，先回落到通用奖励池或治疗，需要在后续阶段补内容。
- 只有一条 Stage，没有 Next Stage / Final Boss，也没有 Base 与 Configure。
- 节点的候选位置是场景里手工摆好的，还没有按 Stage 规则程序化生成。
- 免费挑战额度的具体数值与 Boss Pressure 曲线仍属《策划文档》§16 的待确定项。

## 局外成长闭环（实测）

预置存档 `shards=100` 后进游戏：

| 步骤 | 结果 |
|---|---|
| Base 买三项（生命 / 暴击 / 装弹，各 10 碎片） | 存档写成 `shards=70`、`health_level=1`、`crit_level=1`、`reload_level=1` ✓ |
| 进入关卡后 | 玩家生命 **260**（250+10）、`CritChanceAdd` **0.03**、`ReloadSpeedMultiplier` **1.06** ✓ 三项精确生效 |
| 碎片不足时购买 | 被拒绝，等级保持 0 ✓ |

即「碎片 → 购买 → 存档 → 下局加成」这条链已完整跑通。

## 端到端回归（最后一轮，单次连续跑完）

| 环节 | 结果 |
|---|---|
| Base | 打开并暂停 ✓ |
| Configure | 角色选择 → 武器选择 → 进入 Stage 1（剩余节点 3）✓ |
| 节点 → 奖励 | 与强化池互动 → 三选一「强化池」→ 选中后构筑变为「精准准星」，已挑战 1、场上敌人 2 ✓ |
| Encounter 完成 | ForceComplete 后回到自由探索，剩余节点 2 ✓ |
| 三关 | Stage 1 完成 → Stage 2 → Stage 3（最终 Boss）→ 「胜利」，RunEnded = true ✓ |
| 通关结算 | 「通关！三关全部清理，用时 6 秒 / 构筑：精准准星 / 获得碎片 +70（累计 140）」✓ |

即策划文档 §6.1 的主循环（Base → Configure → Stage → 探索 → Node → 奖励 → Encounter → 继续探索 → Boss Door → Boss → 通关 → 回 Base）已端到端跑通。

## 波次刷新（用户需求，2026-01）

Encounter 默认使用**波次模式**（`EncounterController.WaveMode = true`），替代原先的纯预算滴灌：

| 规则 | 实现 |
|---|---|
| 一波一波刷，上一波只剩 ≤1 只才开下一波 | `_waveRemaining <= 0 && AliveCount <= 1` 时 `BeginWave()` |
| 每波 4~10 只 | `WaveMinEnemies = 4` / `WaveMaxEnemies = 10`，每波随机 |
| 每波最多 2 种不同类型 | `WaveMaxTypes = 2`，`BeginWave()` 从可用生成项里随机抽 1~2 种 |
| 上一波残留的那只不计入下一波类型限制 | 类型限制只作用于**本波新生成**的敌人，残留敌人不参与筛选 |
| 并发上限仍然生效 | 本波目标数超过 `MaxConcurrent` 时分批出，出满就等清场 |
| 预算不足时本波提前结束 | 本波类型都买不起时 `_waveRemaining = 0`，等场上清空后结算 |

实测日志：

```
[Spawn] 第 1 波开始：2 种类型（基础近战/快速近战）× 目标 6 只
[Spawn] 生成 快速近战（剩余预算 9，场上 0）
[Spawn] 生成 快速近战（剩余预算 8，场上 1）
[Spawn] 生成 基础近战（剩余预算 6，场上 2）
```

波次参数目前是 `EncounterController` 的导出属性（在 `Scenes/Main.tscn` 的 `Stage/Combat`、`Stage/BossFight` 上可直接改）。
**波数上限**（`WaveCount`）不在这里 —— 它每次开战前由 `StageController.StartEncounter` 按节点的**价值标签**写入
（高 4 / 中 3 / 低 1，见上面「波数按价值标签」）。

## Boss 死亡流程（用户需求，2026-01）

改动：Boss 阵亡后**不再自动进入下一关**，而是：

1. **清空场上还在战斗的怪**（`ClearField()`，并在 0.4 秒后再清一次，防止同一帧排队的生成漏网）
2. 在 Boss 阵亡位置**降临一个可交互节点**（`RewardNodeKind.Portal`，提示「前往下一关」）
3. 玩家与它互动 → 才真正进入下一关（`Victory` → `RunController` 推进）
4. 该节点**不刷怪也不给奖励**（`RewardNode.SpawnsEncounter = false`）—— 这也是「节点可配置是否刷怪」的开关，普通奖励节点默认 `true`

已实测通过的部分（日志）：

```
[Node] Boss 阵亡 → 清空场上敌人并降临可交互节点「前往下一关」
[Node] 互动降临节点 → 进入下一关
[Flow] 进入第 2 关：Stage 2 · 内环
```

**清场实现**：只翻 `IsRunning` 不够（波次系统会在清场后再开新波），改用 `EncounterController.StopSpawning()` —— 同时立起 `IsCompleted`，从源头冻结波次与预算。

实测（Boss 死后残留敌人递减到 0，且不再增长）：

| 时刻 | 场上敌人 |
|---|---|
| Boss 死后 0.5s | 3 只（清场当帧 + 0.4s 延迟补清的过渡） |
| 1.5s | 1 只 |
| 3.5s | **0 只** ✓ 之后保持 0 |

互动降临节点后进入 `Stage 2 · 内环` ✓

遗留（非阻塞）：玩家在普通战斗进行中直接开门时，Boss 战期间普通波次仍会刷怪（实测刷了 3 只），直到 Boss 阵亡才被清空；`OpenBossDoor` 里那处 `StopSpawning` 的插入没匹配上，后续补。

## 隔离次数 与 节点数量（用户需求，2026-01）

### 术语

游戏内显示统一改为「**隔离次数**」（用户指定叫法）。策划文档 §6.3 的原文是「免费挑战额度，例如前两次挑战免费」，文档里**没有**"隔离次数"这个词 —— 这是用户的命名，代码字段 `FreeChallengeQuota` / `FreeQuotaLeft` 保留不变，只改玩家可见文案。

实测 HUD：

```
Stage 1 · 外围   剩余节点 5   隔离次数 2   用时 2s   敌人 0
```

### 节点数量与保底

> 数值以 `Config/Tables/Stages/*.tres` 为准（**2026-10 复核**，配置表阶段 1 之后）：Stage 1/2/3 的 `RewardNodeCount` = **3 / 4 / 4**，`FreeChallengeQuota` = **2 / 2 / 1**（分表时逐字段照抄，数值未变）。
> `StageController.ActiveNodeCount` 会被 `StageDefinition.RewardNodeCount` 覆盖（`StageController.ApplyStage()` 里赋值），Inspector 上的默认值不生效。

- 场景 `Scenes/Levels/TestArena.tscn` 里手工摆了 10 个 `RewardNode` 实例，`PlaceNodes()` 按 `ActiveNodeCount` 洗牌启用其中若干个
- 每个 Stage **保底**：至少启用 1 个强化池（`RewardNodeKind.Enhancement`）与 1 个材料节点（`RewardNodeKind.Resource`），由 `EnsureNodeEnabled()` 在 `PlaceNodes()` 之后强制打开

实测（自测第一阶段日志）：`节点布置：启用 10 个（高 2 / 中 5 / 低 3）`，最小节点间距 9.74（阈值 8）✓
（早期的「3 个 / 5 个」是节点数改动前的旧记录，已作废。）

## 波数按价值标签（用户需求 #7，2026-10 改）

**高 4 / 中 3 / 低 1**（原来是高 3 / 中 2 / 低 1），而且改成**按价值标签驱动**、不再按节点类型枚举硬编码。

| 层 | 节点 | 价值标签 | 波数 |
|---|---|---|---|
| 2 高 | 进化池（含"武器限定"那一份，见下）/ 异化池 | `tag_high` | **4** |
| 1 中 | 强化池 / 补给站 | `tag_mid` | **3** |
| 0 低 | 局外资源 / 恢复状态 / 特殊奖励 / 降临节点 | `tag_low` | **1** |

- **标签表**：`Config/Tables/ValueTags/tag_*.tres`（3 行，索引在 `Balance.tres` 的 `ValueTags` 数组）。
- **节点行**：`NodeConfig.ValueTags`（字符串数组，9 行都填了）。
- **取值链**（只有一处定义：`NodePlacement.WavesFor`）：
  ① 标签行 `WaveCount`（多标签命中取**最大**）→ ② 节点行自己的 `WaveCount` → ③ 按 `Layer` 的代码兜底（2→4 / 1→3 / 其它→1）。
- **为什么用标签**：用户原话「这些不写死，假设后期有彩蛋或其他奖励也会在这些不同的价值标签里」——
  加一种新奖励时只要给它的节点行挂一个已有标签（或加一行标签），**不用改任何代码**。
- **日志留痕**：开局会打 `价值标签（节点波数来源）：3 行 …[tag_high→4 波、tag_mid→3 波、tag_low→1 波]` 与
  `节点波数（高 4 / 中 3 / 低 1）：node_evolution（tag_high）4 波 · …`；
  节点行挂了拼错的标签会 **warning + 日志**（不静默落到下一层）。

> ✅ **`node_weapon` 已改成 Layer 2 / `tag_high` / 4 波（2026-10-12 用户拍板 A3）**：
> 用户原话「**没有定义武器池，只是一个说法**，确切来说是**进化池**，是**武器**（本质是**角色的攻击方式**）
> 的**限定池**，而**进化池算高价值**，因为**攻击方式的改动巨大**」。
> 所以 `node_weapon` 那一行不再是"中价值 3 波"：**标题改成「进化池」**、挂 `tag_high`、`Layer = 2`、`WaveCount = 4`。
> **目录与类名刻意没动**（`Config/Tables/UpgradePools/` 与 `UpgradePoolConfig` 是第三批刚落地的"池"的通用名，
> 而且那里只有身份、没有价值标签 —— 价值标签是**节点行**这一侧的列）。
>
> ⚠️ **「节点布置 10（高 2 / 中 5 / 低 3）」这条硬指标没有漂**，因为 `Main.tscn` 里**没有 `Kind = Weapon` 的节点实例**
> （场景里那 10 个是 4 强化 + 进化 + 异化 + 商店 + 材料 + 恢复 + 特殊），所以改它的 `Layer` 不会改变任何一层的实际数量。
> 变的是这两处：`价值标签表 → 节点 [Weapon=3]` 变成 **`Weapon=4`**，以及自测的高/中/低改成**按价值层**
> （`NodePlacement.LayerOf`）数而不是按 `RewardNodeKind` 枚举数 —— 规则与标签自此一致（以后把哪种奖励挂到
> 高价值标签上，它就数进"高"）。

实测（自测，逐位比对）：

```
波数映射：进化=4（期望4） 异化=4（期望4） 武器限定（=进化池）=4（期望4） 强化=3（期望3） 商店=3（期望3） 材料=1（期望1） 恢复=1（期望1） 特殊=1（期望1）
价值标签表：标签 [tag_high=4波 tag_low=1波 tag_mid=3波]（期望 tag_high=4 tag_low=1 tag_mid=3）
           → 节点 [Weapon=4 Enhancement=3 Evolution=4 Mutation=4 Resource=1 Heal=1 Special=1 Shop=3 Portal=1]
配置表比对·节点表：进化层 2/波 4（期望 2/4） 强化层 1/波 3（期望 1/3） 恢复层 0/波 1（期望 0/1）
节点布置：启用 10 个（高 2 / 中 5 / 低 3）→ 期望 10（2/5/3）（按**价值层**数，见 NodePlacement.LayerOf）
```

（旧值 `Weapon=3`、`进化层 2/波 3`、`强化层 1/波 2` 是改动前的记录，已作废。）

## 关卡内的定期刷怪 + 每 120 秒增强（用户需求 #6，2026-10 新增）

用户原话：「当角色进入关卡时，场景里会**定期在任意有效位置刷出怪物**，**每过 120 秒**（可调整的参数）
就**增强接下来刷出的怪物**（提升强度或更强类型的怪）」。

**它与节点的 Encounter 并存**（不是替代）：Encounter 仍按节点价值的波数开战，这套只是"关卡本身一直在往外冒怪"。

| 项目 | 实现 |
|---|---|
| 谁在驱动 | `StageController.UpdateAmbientSpawns`（`_PhysicsProcess` 里每帧，只有 `Definition != null` 才计时 = "进了关卡"才开始） |
| 节奏与强度参数 | **全局标量**（`Config/Balance.tres` 的 `AmbientSpawnInterval` = 30s / `AmbientSpawnEscalateSeconds` = 120s / `AmbientSpawnStrengthStep` = 0.25 / `AmbientSpawnMaxAlive` = 6），字段定义在 `Scripts/Config/BalanceConfig.cs` 的「节奏 · 关卡内的定期刷怪」组 |
| 刷在哪 | **复用** `EncounterController.SpawnAmbient` → `SpawnPlanner.FindSpawnPoint` → `MapManager.SpawnPoint` → `MapManager.RingPoint`（**没有第二套落点实现**） |
| 增强什么 | 档位 = 本关已过秒数 ÷ 120（切关清零）；每档给**之后**刷出的怪同时乘 生命与伤害（×1.25/档）。已刷出的怪不动 |
| 与 Encounter 的关系 | 游离怪**不扣预算、不进 `_spawned`、不计入 `AliveCount`/`MaxConcurrent`**，所以**不参与本场的清场判定**（否则节点挑战会因为"场外还有别的怪"永远结不了算） |
| 关掉它的开关 | `AmbientSpawnInterval = 0` → 整套机制不参与（行为与接它之前逐位一致） |

> ⚠️ **为什么参数在全局标量而不是关卡表**：这是"所有关卡共用的压力节奏"，与 `NodeBudgetMultiplier` /
> `SpawnIntervalMultiplier` 同类；放进 `StageDefinition` 会让 3 行各存一份相同的数。要按关卡区分时再加一列覆盖即可。

> 🔧 **顺带修的一处"有效位置"**：`MapManager.RingPoint` 在**导航网格不可用时**原来直接
> `return center + Up*0.3` —— 那等于"刷怪点 = 玩家脚下"。本项目实测运行时烘焙**始终得到 0 个多边形**
> （见 [todo-handoff.md](todo-handoff.md) 的「已知未解决」），所以那条兜底路径就是实际生效的那条，
> 于是所有敌人 / 白板首领 / 降临物都贴在玩家身上出现，"任意有效位置"名存实亡。
> 现在兜底改成**随机环点 + 地面射线 + 球形避障**（与 `NodePlacement.PlaceSafely` 同一套探测参数），
> 实测游离怪落在离玩家 **13.9 ~ 21.8 m** 的可行走位置上。

实测（自测第一阶段，真的走正式入口刷两只；日志 `run_20261012_221803.log`）：

```
关卡内定期刷怪（需求 #6）：间隔 30s / 每 120s 增强一档（每档 ×1.25）/ 上限 6 只 / 本关已过 1.5s / 当前档位 0 / 场上 0 只
定期刷怪·第 0 档：FastMelee 生命 26.3（倍率账 生命×0.75 / 伤害×0.3）伤害 7 落点 (-11.4, 0.3, -13.4)（离玩家 17.6m，导航网格就绪=False）
定期刷怪·不碰 Encounter 的账：预算 10 / 存活 0 / 已生成 0 → 现在 预算 10（期望不变）/ 存活 0（期望不变）/ 游离怪 1 只
定期刷怪·自测：把本关时钟拨快 120s → 已过 121.5s / 档位 1
定期刷怪·第 1 档：Chaser 生命 56.3（倍率账 生命×0.9375 / 伤害×0.375）伤害 12 落点 (12.8, 0.3, -2.7)（离玩家 13m，导航网格就绪=False）
定期刷怪·清理：两只已击杀 → 场上游离怪 0 只（期望 0）
```

（第 1 档的 `生命×0.9375 = 0.75 × 1.25`、`伤害×0.375 = 0.3 × 1.25` 就是"每过 120 秒增强一档"的账。）

**自然计时路径也单独验过**（把 `AmbientSpawnInterval` 临时改成 6、`AmbientSpawnEscalateSeconds` 临时改成 10 跑一轮，
日志 `run_20261012_221654.log`；跑完已还原成 30 / 120）：

```
22:17:01 [Spawn] 定期刷怪：… 落在 (-7.3, 0.3, 15.8)（强度档位 1 → ×1.25，场上游离怪 1 只）
22:17:06 [Spawn] 定期刷怪·增强：本关已过 20s → 强度档位 2（之后刷出的怪 生命/伤害 ×1.5）
22:17:08 [Spawn] 定期刷怪：… 落在 (-35.9, 0.3, -2.2)（强度档位 2 → ×1.5）
22:17:14 [Spawn] 定期刷怪：…（强度档位 2 → ×1.5，场上游离怪 2 只）
22:17:17 [Spawn] 定期刷怪·增强：本关已过 30s → 强度档位 3（之后刷出的怪 生命/伤害 ×1.75）
22:17:20 [Spawn] 定期刷怪：Chaser 落在 (4.2, 0.3, 27.2)（强度档位 3 → ×1.75）
```

（每隔 6~7 秒落一只、每 10 秒档位 +1，档位只影响**之后**刷出的那些；那一轮的全部硬指标照样逐位一致、
自测跑完整 —— 这就是"与节点 Encounter **并存**、不把节点波次改坏"的正面证据。）

## 白板首领：只在第 8 波出现（用户需求 #5，2026-10 改）

原来**每场 Encounter 末尾都出**白板首领；现在只有**本关累计波数**到门槛（三关都是 **8**）时才出。

| 项目 | 实现 |
|---|---|
| 门槛 | 关卡表 `EliteBossMinWave`（8）→ `EncounterController.MinWaveForEliteBoss`；留 0 = 关掉闸门 |
| 判定量 | `EncounterController.WavesOpenedBefore`（本场之前本关开过的波数）+ 本场 `WaveIndex` |
| 累计来源 | `StageController._wavesOpenedInStage`：每场清空后 `+= _encounter.WaveIndex`，**每关清零** |
| 门槛没过 | 本场照常正常结算（不再有"白板首领副本"这一阶段） |

> ⚠️ **为什么是"本关累计"**：节点波数只有 1～4，按单场算门槛取 8 永远到不了 ——
> 会从"每场都出"变成"永远不出"。按本关累计算，第 1～7 波都不出，累计到第 8 波清空时才降临。

实测（自测同一轮里两种分支都跑到了）：

```
白板首领跳过：本关累计才第 1 波（门槛 8），本场直接结算
白板首领跳过：本关累计才第 3 波（门槛 8），本场直接结算
常规波已清空 → 进入白板首领副本（本关累计第 1 波，门槛 1）    ← 自测把门槛临时压到 1 做正向验证，跑完还原
白板首领降临（血量倍率 0.35（来源：表 boss_final），无压力 / 无词条 / 本关累计第 1 波）
```

## 打 Boss 后卡在「挑战者状态」（用户需求 #12，真 bug，2026-10 修）

**症状**（用户报的）：直接开门打 Boss → 打完无法进下一关，显示还在挑战中。

**根因**：`ChallengerActive` 原来是 `_encounterRunning || (_bossFight && !IsVictory)`，
而 `IsVictory` **只有互动降临节点才变 true** → Boss 一死就死锁（降临节点被自己的挑战者闸门拒掉）。

**修法**：把 `_bossCleared` 也算成"这一战结束了"：

```csharp
!IsVictory && (_encounterRunning
    || (_bossFight && !_bossCleared && _bossEncounter?.Boss is { } boss && !boss.IsDead))
```

**实测（自测阶段 18–22 = 用户那三步）**：

```
Boss 门：打开=True … 挑战者状态=True（期望 True：Boss 还活着）
🔴 Boss 阵亡 → 挑战者状态=False（期望 False，这就是 bug #12 的修复点）
降临节点：找到=True 提示=[按 F 使用：前往下一关]
🔴 互动降临节点：接受=True（期望 True）→ 本关通关=True 关卡 1→2（期望 +1）挑战者状态=False（期望 False）
```

> ⚠️ 自测那个阶段的坑（都已写进代码注释）：① 必须排在"开 Boss 门"**之前**（开门后 `CurrentNode` 指向 Boss 那条
> Encounter，且普通 Encounter 被 `StopSpawning` 冻住）；② **先把玩家挪远**再让 Boss 出场、并每帧打一次直到它阵亡
> （Boss 与护卫在玩家附近生成，站着不动会被秒 → 整局结束 → 后面阶段全废）；③ 读 `IsVictory` 必须在同一行内读走
> （下一关的 `ApplyStage` 会立刻把它复位）；④ 退出前 `GetTree().Paused = false`（刚切过关，树被 `UiRouter` 暂停着）。
