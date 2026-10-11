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

1. 角色/武器确定后 `RewardPool.Initialize(weapon)` 建立候选池（按武器标签过滤，§8.4：只建一次）。
2. 每次奖励 `Roll(build, 3)`：过滤掉已达层数上限、与已持有项互斥的定义，再按权重不重复抽 3 个。
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
| **角色限定** | `RewardDefinition.RequiresSummoner` | 只有召唤控制角色（`PlayerCharacterKind.Summoner`）才会出现 |

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

⚠ 互斥对的**运行时采样**还没跑出来（需要在同一局先拿到跳弹、再看穿透是否消失，靠随机抽到该组合，本轮没碰上）；配置与双向判定逻辑都已就位，下一次游玩时可以顺带观察。

## 武器池语义澄清（用户 2026-01 反馈）

**背景**：策划文档 §6.2 只把「武器池」列为一种 Node 类型，**没有定义它的效果**（文档里 L411/L540/L654 出现的「武器池」指的是"该角色可选武器清单"，不是节点效果）。原先的实现把武器池做成"三选一换武器"，这与用户的设计意图不符。

**澄清后的语义**：

| 节点 | 效果 | 实现 |
|---|---|---|
| **武器池** | **改造武器的攻击方式**（不是换武器） | 走进化池 `RewardKind.Evolution`（穿甲弹 / 跳弹 / 爆裂弹头 / 双管齐下） |
| **强化池** | **纯数值强化** | 走强化池 `RewardKind.Enhancement`（伤害 / 射速 / 弹匣 / 暴击…） |
| 武器本身 | **一旦在 Configure 选定，本局不可更换** | 已移除所有换武器入口（含商店那一项，改为"随机一件进化"） |

实测（同一局先后来到两个节点，武器为手枪 24 伤害 / 12 弹匣）：

```
--- 武器池 界面: 标题=武器池
    [1] 穿甲弹（进化）子弹额外穿透 2 个目标
    [2] 爆裂弹头（进化）子弹命中时产生小范围爆炸
    武器: 伤害=24 弹匣=12      ← 未更换武器 ✓

--- 强化池 界面: 标题=强化池
    [1] 致命打击（强化）暴击伤害 +35%
    [2] 高速扳机（强化）射速 +12%
    武器: 伤害=24 弹匣=12
```
