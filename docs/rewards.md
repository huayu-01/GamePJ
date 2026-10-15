# 成长系统（阶段 5）

对应策划文档 §9-10、执行文档 §八。原型阶段只做「强化 / 进化 / 异化 + 三选一」，局外经济与信物符文不在范围内。

## 三层内容的分工

| 类型 | 作用 | 池归属 | 示例 |
|---|---|---|---|
| 强化 Enhancement | 改参数，可叠层 | 通用池 | 增伤弹头、高速扳机、扩容弹匣、稳固站姿、贯穿弹头 |
| 进化 Evolution | 改武器核心行为 | 武器限定池（`RequiredWeaponTags`） | 双管齐下（多一发）、穿甲弹、跳弹、爆裂弹头 |
| 异化 Mutation | 加新规则/新触发 | 通用池 + 角色限定池 | 连锁闪电、吸血、灼烧弹、战地补给 |

内容集中在 `RewardCatalog.All`：**加内容只加一行定义**，池与抽取代码不动。
效果用委托声明，参数走 `StatSheet` 的 Modifier；需要新规则的异化会挂一个 `TriggerRule` 到玩家身上。

## 抽取流程

1. **进关卡初始化时**（`RunController.ApplyBalance` → `InitializePool()`，就是"角色/武器都定了"的那一刻）
   按**当前角色 + 当前武器**建立候选池 `RewardPool.Initialize(weapon)`（§8.4：**只建一次**）：
   武器层走三级回退（专属池 → 武器标签 → 通用），角色层走 `SummonerAllowed`（`RequiresSummoner` 的召唤类）。
   ⚠️ **2026-10-12 用户第三轮 C2 改的就是这一条**：用户原话「池里一开始有什么，应该在**进入关卡初始化的时候**就判断好的」
   —— 之前是**四个交互点里的惰性分支**（谁先被交互谁建池，其中两个还忘了设 `SummonerAllowed`），
   现在只有进关卡时这一条路，交互时只从定好的池里抽。
   留痕：`[Reward] 奖励池按当前角色/武器定下来了（进关卡初始化）：角色 … 武器 …（PoolId …）→ 候选 N 条 …`。
2. 每次奖励 `Roll(build, 3)`：从**定好的候选集**里过滤掉已达层数上限、与已持有项互斥（`Excludes` / `ConflictsWith`）、
   不符合节点类型（`Kind`）与角色限定的定义，再按权重不重复抽 3 个。
   （"依赖你现在已经有什么"的这些过滤**天然必须每次交互算**，但候选项的来源集合不再变。）
3. 玩家在 `RewardScreen` 三选一（点击或按 1/2/3，期间暂停游戏树）。
4. `RunBuild.Add` 应用效果并记录层数；构筑列表显示在 HUD 左下。

## 触发器（Trigger / Condition / Effect）

`TriggerRule` 订阅 `GameplayEventBus`，`Matches` 即条件、`Execute` 即效果，自带：

- 冷却（`Cooldown`）
- 每条事件链的执行次数上限（`MaxTriggersPerChain`）
- 链深度上限（复用 `EventContext.MaxDepth`，防止死亡爆炸互相递归）

已实现：`ChainLightningRule`（击杀 → 附近敌人连锁闪电）、`LifestealRule`（造成伤害 → 回血）、
`BurnOnHitRule`（命中 → 挂 Burn 状态）、`AmmoOnKillRule`（击杀 → 补满弹匣）。

## 新增的属性键

`StatSheet` 里这些键由武器/投射物在射击时读取：

| 键 | 用途 |
|---|---|
| DamageMultiplier / AttackSpeedMultiplier / CritChanceAdd / CritMultiplierAdd | 通用攻击侧 |
| MoveSpeedMultiplier / MaxHealthAdd / DamageReductionAdd | 角色侧 |
| ReloadSpeedMultiplier / MagazineSizeAdd | 弹匣与装填 |
| ProjectileCountAdd / PierceAdd / RicochetAdd | 投射物数量、穿透、弹射 |
| ExplosionRadiusAdd / ExplosionDamageScale | 命中爆炸 |

## 实测（Main 场景）

- 三选一界面正常弹出并暂停游戏；按「1」选中后 `DamageReductionAdd=0.08`、构筑显示「稳固站姿」。
- 增伤弹头 ×3 → `DamageMultiplier=1.5209`（正好 1.15³）。
- 扩容弹匣 → 弹匣 8→11；双管齐下 → `ProjectileCountAdd=1`；爆裂弹头 → `ExplosionRadiusAdd=3.5`。
- 击杀事件链实测：`Kill(src=Player)` → 连锁闪电、战地补给、吸血三条规则全部匹配并执行（吸血是被连锁闪电造成的伤害事件再次触发的，说明链上伤害确实落地）。
- `Died` 事件不会误触发击杀类规则（已分开处理）。

## 已知限制

- 三选一目前用 Tab 手动触发；阶段 7 的 Node 结算会自动发放。
- 没有稀有度分段与「首次必出高价值挑战」机制。
- 互斥只用 `ConflictsWith` 显式声明，还没有按大类自动互斥。
- 异化的触发链上限是每规则独立计数，没有全局事件预算。

## 池子判定（用户需求，2026-01）

奖励池在抽取时会做两类判定，`RewardPool.Roll` 里统一处理：

| 判定 | 字段 | 规则 |
|---|---|---|
| **互斥** | `RewardDefinition.Excludes` | **双向**：候选排斥已拥有的，或已拥有的奖励排斥候选，都不会进池 |
| **单向互斥** | `RewardDefinition.ConflictsWith` | 已拥有列表里任一 Id → 本项不进池（**阶段 4 才接上**：表里现在有 `RewardConfig.ConflictsWith` 列与 `Compile` 填充入口；默认空） |
| **角色限定（旧）** | `RewardDefinition.RequiresSummoner` | 只有召唤控制角色（`PlayerCharacterKind.Summoner`）才会出现 |
| **武器限定（三级回退）** | `RewardDefinition.ApplicablePoolIds` / `RequiredWeaponTags` | ① 选项行填了 `Pool` → 只有 `Weapon.PoolId` 等于它的武器能拿到；② 没填池但填了武器标签 → 按 `weapon.AttackTags` 匹配；③ 两者都空 → 所有武器。**阶段 4 新增第①层**（表：`Config/Tables/UpgradePools/pool_*.tres`，武器行 `Pool` 正向指过去） |
| **🆕 角色限定（P5）** | `RewardDefinition.RequiredCharacterIds` | 当前**角色行 Id**（`chr_hunter` …）不在名单里 → **根本不进池**。空 = 不限角色（既有 19 行逐位不变）。`<A限定>` = 只填这一列；`<A1限定>` = 这一列 **+** `Pool` |
| **🆕 前置（P5）** | `RewardDefinition.Requires` | `Requires` 里的 Id **全部**已拥有才进池（`RewardPool.HasPrerequisites`，抽取时判）。空 = 无前置 |

> 🆕 **P5：三层判定的完整接入契约见 [upgrades.md](upgrades.md)**（哪张表哪一列、哪个文件、判定时机、
> "加一条增幅要做什么"、以及 19 条兜底硬编码为什么被删）。

当前配置：

| 奖励 | 判定 |
|---|---|
| 跳弹 `evo_ricochet` | 排斥 `pierce_round`（贯穿弹头）、`evo_ap`（穿甲弹） |
| 贯穿弹头 `pierce_round` | 排斥 `evo_ricochet` |
| 穿甲弹 `evo_ap` | 排斥 `evo_ricochet` |
| 亡者随从 `mut_summon` | 仅召唤控制角色 |

实测（`RunController.PoolPreview` 返回当前构筑下能抽到的奖励 Id）：

| 角色 | 池子总数 | 是否含 `mut_summon` |
|---|---|---|
| 角色 1 枪手 | 18 | **False** ✓（用户报的「非召唤角色出现击杀召唤」已修） |
| 角色 3 召唤控制 | 19 | **True** ✓ |

（相差正好 1 条 = 亡者随从。）

> 🆕 **阶段 4 复核（2026-10）**：上面这条"枪手 18 / 召唤 19"在加完"武器 → 升级池 → 选项"三层关联之后
> **逐位不变**（自测 `配置表比对·增幅项：… 当前池子=18 条（枪手期望 18 条）`）。
> 原因是 19 条增幅项的 `Pool` 列**全是空的** → 全部落到②③两层，与分表前一致。
> 第一层（专属池）是**留白的新能力**：给某一行填上 `Pool`，那条就只属于指向该池的武器
> （实测：把 `upg_dmg_up` 填成 `pool_sniper` 后，手枪的池子从 18 掉到 17）。

⚠ 互斥对的**运行时采样**还没跑出来（需要在同一局先拿到跳弹、再看穿透是否消失，靠随机抽到该组合，本轮没碰上）；配置与双向判定逻辑都已就位，下一次游玩时可以顺带观察。

## 进化池语义澄清（用户 2026-01 反馈；⚠️ 2026-10-12 术语更新见文末）
> ⚠️ **2026-10-12 术语更新（用户拍板 A3）**：**没有"武器池"这个东西** ——
> 「没有定义武器池，只是一个说法，确切来说是**进化池**，是**武器**（本质是**角色的攻击方式**）的**限定池**，
> 而**进化池算高价值**，因为**攻击方式的改动巨大**」。
> 所以本节下面的"武器池"应全部读作**进化池**：它 = 武器（攻击方式）的限定池，节点价值是**高**（`tag_high` / `Layer = 2` / **4 波**）。
> 代码里的兜底标题也已从 `"武器池"` 改成 `"进化池"`（`RewardService.FallbackTitle`）。
> 细节见 [stages.md](stages.md)「波数按价值标签」与 [config.md](config.md) 的「用户 A2 / A3 + 需求 #6 实测」。
>
> ⚠️ **2026-10-12 第三轮 C2：进化池现在只有一行节点 + 池内容在进关卡时定好**。
> ① 节点行：原 `node_weapon`（`Kind = Weapon`，与 `node_evolution` 逐列相同）已**整行合并进 `node_evolution`** ——
> 用户说得很直接：「**并没有"放哪个进化池节点"的说法**」，即不区分多个进化池节点；`Id` / 类 / 目录一律没改名，
> 节点表从 9 行变 8 行。② 候选集时机：见上面「抽取流程」第 1 条（进关卡初始化时按当前角色/武器定下来）。
> ③ 「按攻击方式分的限定池」= `Config/Tables/UpgradePools/pool_pistol|sniper|rifle.tres` 三行（**保留不动**），
> 武器行的 `Pool` 正向指过去、选项行的 `Pool` / `RequiredWeaponTags` 决定谁能抽到 —— 这就是
> 「进化池里会出什么选项是根据交互的角色持有的攻击方式（武器）」这条链路的落点。
> 实测与两侧逐位比对见 [config.md](config.md) 的「护甲无上限 + 进化池合并实测」。

**背景**：策划文档 §6.2 只把「武器池」列为一种 Node 类型，**没有定义它的效果**（文档里 L411/L540/L654 出现的「武器池」指的是"该角色可选武器清单"，不是节点效果）。原先的实现把武器池做成"三选一换武器"，这与用户的设计意图不符。

**澄清后的语义**：

| 节点 | 效果 | 实现 |
|---|---|---|
| **进化池**（旧称"武器池"） | **改造武器的攻击方式**（不是换武器） | 走进化池 `RewardKind.Evolution`（穿甲弹 / 跳弹 / 爆裂弹头 / 双管齐下） |
| **强化池** | **纯数值强化** | 走强化池 `RewardKind.Enhancement`（伤害 / 射速 / 弹匣 / 暴击…） |
| 武器本身 | **一旦在 Configure 选定，本局不可更换** | 已移除所有换武器入口（含商店那一项，改为"随机一件进化"） |

实测（同一局先后来到两个节点，武器为手枪 24 伤害 / 12 弹匣；记录当时界面标题写作"武器池"）：

```
--- 进化池 界面: 标题=进化池（当时记录为"武器池"）
    [1] 穿甲弹（进化）子弹额外穿透 2 个目标
    [2] 爆裂弹头（进化）子弹命中时产生小范围爆炸
    武器: 伤害=24 弹匣=12      ← 未更换武器 ✓

--- 强化池 界面: 标题=强化池
    [1] 致命打击（强化）暴击伤害 +35%
    [2] 高速扳机（强化）射速 +12%
    武器: 伤害=24 弹匣=12
```
