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
- 节点类型：武器池 / 强化池 / 进化池 / 异化池 / 局外资源 / 恢复状态 / 特殊奖励。
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

一个 Stage 场景被复用于所有关卡，差异全部来自 Config/StageDefinition.cs 的数据（策划文档 §6.2：Stage 之间靠敌人强度与进度区分）：

| 字段 | 含义 |
|---|---|
| `DisplayName` | 关卡名，显示在 HUD |
| `CombatBudget` / `MaxConcurrent` / `SpawnInterval` | 本关战斗的 Spawn Budget 与节奏 |
| `EnemyHealthMultiplier` / `EnemyDamageMultiplier` | 本关敌人额外缩放（在全局平衡倍率之上再乘） |
| `EliteChanceScale` | 精英概率附加（加在各生成项自己的 EliteChance 上） |
| `AllowedEnemies` | 本关允许生成的敌人（按生成项 DisplayName 匹配；留空 = 不限制） |
| `RewardNodeCount` / `FreeChallengeQuota` | 本关布置几个节点、免费挑战几次 |
| `BossGuardCount` / `BossHealthMultiplier` / `BossDamageMultiplier` | Boss 护卫数与 Boss 缩放 |
| `IsFinalBoss` | 最终 Boss（打完即通关） |

序列写在 `Balance.tres` 的 `Stages` 数组里（`Config/Balance.tres`），配置为空时使用代码内置的三关兜底。

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
| 走近节点 | HUD 左下显示「按 F 使用：武器池」✓ |
| 按 F | 弹出三选一，标题「武器池」，只出该类型奖励 ✓ |
| 选择后 | 构筑增加（本次拿到「吸血」）、节点变已用、已挑战 +1，立刻开一场 Encounter（场上 3 个敌人）✓ |
| 清场后 | 回到自由探索（剩余节点 2），不再自动推进 ✓ |
| Boss 门 | 按 F 立即出战 Boss + 2 技能精英，初始压力 = max(0, 已挑战 - 免费额度) ✓ |
| Boss 阵亡 | Stage IsVictory = true，弹出「胜利」结算 ✓ |

## 已知限制 / 待确认

- 武器池、局外资源、特殊奖励目前没有独立系统，先回落到通用奖励池或治疗，需要在后续阶段补内容。
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

> 数值以 `Config/Balance.tres` 为准（**2026-01 复核**）：Stage 1/2/3 的 `RewardNodeCount` = **3 / 4 / 4**，`FreeChallengeQuota` = **2 / 2 / 1**。
> `StageController.ActiveNodeCount` 会被 `StageDefinition.RewardNodeCount` 覆盖（`StageController.ApplyStage()` 里赋值），Inspector 上的默认值不生效。

- 场景 `Scenes/Levels/TestArena.tscn` 里手工摆了 10 个 `RewardNode` 实例，`PlaceNodes()` 按 `ActiveNodeCount` 洗牌启用其中若干个
- 每个 Stage **保底**：至少启用 1 个强化池（`RewardNodeKind.Enhancement`）与 1 个材料节点（`RewardNodeKind.Resource`），由 `EnsureNodeEnabled()` 在 `PlaceNodes()` 之后强制打开

实测（自测第一阶段日志）：`节点布置：启用 10 个（高 2 / 中 5 / 低 3）`，最小节点间距 9.74（阈值 8）✓
（早期的「3 个 / 5 个」是节点数改动前的旧记录，已作废。）
