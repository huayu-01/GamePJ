# 增幅系统 · 三层判定（限定 / 互斥 / 前置）—— P5 接入契约

> **2026-10-18 J14更新**：怜悯改用SkillEnhancement指定近战单段（负数选择末段），只乘该技能伤害；普渡沿用TablePulse，新增强类型RequireFinalComboStep＋RequiredEventAbility过滤。攻击／真实Kill事件携带冻结段号，群杀每个击杀各回N，N预设8。见 [J14](character-design-j14.md)。以下旧半接清单以当前快照为准，下一项喜爱。

> **2026-10-18 J13更新**：金光术已迁 `MeleeProjectile`（每段真实ATK出弹）；大拔刀术／神降术迁强类型 `SkillEnhancement`，只修改目标技能自身。`AbilityOverride` 当前仍用的为有备无患／专注／气贯长虹／地爆；下方P5b的占位与半接清单是历史，当前剩余以 [会话快照](SESSION-SNAPSHOT.md) 为准。[J13验收与真实命中](character-design-j13.md)。

> **权威**：`docs/character-redesign.md`（用户对"怎么做"的澄清）+ `F:\Godot项目\3d肉鸽雨中哈迪斯\角色设计.txt`（"做什么"）。
> 本文件是**给下一轮（P6 清旧 / 继续补增幅）看的接入契约**：三层判定分别落在哪张表、哪个文件，
> 以及**"加一条增幅要做什么"**。

---

## 0. 一句话

**池的本质 = 哈迪斯的祝福清单，按「当前角色 + 当前武器」过滤候选。**
**复用现有机制的增幅 = 加行文件＋ConfigIndex引用；新增行为则补C#处理器和强类型参数资源，参数仍进表。** 不用字符串反射或脆弱的间接配置规避必要的代码修改。

---

## 1. 三层判定分别落在哪

| 层 | 判什么 | 表列（磁盘） | 运行期字段 | 判定代码 | 判定时机 |
|---|---|---|---|---|---|
| **① 限定 · 角色** | 当前**角色行 Id** 在名单里吗 | `RewardConfig.RequiredCharacterIds` | `RewardDefinition.RequiredCharacterIds` | `RewardDefinition.AllowsCharacter` ← `RewardPool.Initialize` | **进池时**（一次） |
| **① 限定 · 武器** | ①专属池 → ②武器标签 → ③通用（三级回退） | `RewardConfig.Pool` / `RequiredWeaponTags` | `ApplicablePoolIds` / `RequiredWeaponTags` | `RewardDefinition.AllowsWeapon` ← `RewardPool.Initialize` | **进池时**（一次） |
| **② 互斥** | 双向：候选排斥已拥有 / 已拥有排斥候选 | `RewardConfig.Excludes`（双向）、`ConflictsWith`（单向） | `Excludes` / `ConflictsWith` | `RewardPool.IsExcluded` + `Roll` 里的 `ConflictsWith` 循环 | **每次抽取** |
| **③ 前置** | `Requires` 里的 Id **全部**已拥有 | `RewardConfig.Requires` | `RewardDefinition.Requires` | `RewardPool.HasPrerequisites` ← `RewardPool.Roll` | **每次抽取** |

> ### ⭐ 前置现在有**两个来源**（J11 起）：增幅「已拥有」 + **当前携带的符文**
>
> 用户 2026-10-17 澄清符文（哈迪斯信物）：「携带了这个雷之印等于满足它们的前置要求」。
> 落地方式只有一处改动 —— `RewardPool.HasPrerequisites` 改调
> **`RunBuild.SatisfiesPrerequisite(id)`** = `StacksOf(id) > 0` **或** 该 Id 在当前符文的解锁名单里：
>
> | 来源 | 谁写进去 | 读取点 |
> |---|---|---|
> | ① **已拥有的增幅**（改动前唯一来源） | `RunBuild.Add` / `DebugGrantWithoutApplying` | `StacksOf(id) > 0` |
> | ② **当前携带的符文**（J11） | `RunBuild.SetRuneUnlocks` ← `RunController.ApplyCarriedRune` | `_runeUnlocks.Contains(id)` |
>
> 名单里同时放**符文行 Id**（`rune_lei_seal`）与行上 `UnlocksRequires` 的**派系名**（`雷之印`），
> 所以表里两种写法都能用。⚠️ 符文**不进** `_stacks`：它不影响互斥（`Excludes`）、
> 也不出现在 HUD 的构筑列表里 —— 它只满足**前置**这一层。
> **不携带任何符文时名单为空 → 判定退化成 `StacksOf(id) > 0`，逐位不变**。
> 契约见 [rune-system.md](rune-system.md) §4。

**为什么限定在进池时判、互斥/前置在抽取时判**：用户 C2 的原话是「池里一开始有什么，应该在**进入关卡初始化的时候**就判断好的」——
角色与武器在那一刻就定了，所以候选集合只建一次（`RunController.InitializePool`，调用点唯一）。
而"你现在已经有什么"是每局的，天然必须每次算。

### 1.1 `<A限定>` vs `<A1限定>` —— 到底怎么写（用户 §0 亲自澄清过）

| 标签 | 含义 | 表里怎么写 |
|---|---|---|
| `<A限定>` | 限定**角色 A**、**不限其武器**（A 的 3 把武器都能拿到） | `RequiredCharacterIds = ["chr_hunter"]`，**不填** `Pool` |
| `<A1限定>` | 限定**角色 A + 武器 1** | `RequiredCharacterIds = ["chr_hunter"]` **并且** `Pool` 指向该武器行的 `Pool` |
| `<B限定>` / `<B1限定>` … | 同理 | 同理 |

⚠️ **"限定"是过滤条件，不是另建一套池子**：**一层奖励表 + 每行写清"谁能拿到"**。
⚠️ **武器身份必须用资源引用（`Pool`），不能用标签**：短弩与弓的 `AttackTags` 完全相同
（`Projectile/Arrow/Physical`），靠标签分不开；而池引用改名/删文件时会被 Godot 置 `null`（**能检测**）。

### 1.2 不知道角色是谁 → 限定项**不进池**

`AllowsCharacter(null/"")` 返回 **false**。这正是用户要的语义：
> 「角色 A 拿了角色 B 限定的增幅，那么不会产生任何效果，这并不正确」

正确行为是**根本不进候选列表**（`RewardPool` 在 `Initialize` 里就把它们筛掉了，不是"抽到了没效果"）。

---

## 2. 加一条增幅要做什么（**不改 C#** 的那条路）

### 2.1 三步

1. **加一个行文件** `Config/Tables/Upgrades/<你的 id>.tres`（照抄一个同类的改字段）。
2. **`Config/ConfigIndex.tres` 三处同改**：① `[ext_resource]` 一行、② `load_steps` 加 1、③ `Upgrades` 数组加一项。
3. 跑一次，日志里核对两行：
   - `[Config] 增幅项表：N 条（来源：表）—— 强化 a / 进化 b / 异化 c`
   - `[Config] 配置表：… （ConfigIndex.tres 已加载）` ← **必须有这一句**，否则整份表都没读上（所有数值静默回退）。

### 2.2 效果从哪来

效果 = `ApplyHandler` 字符串 + 参数（`Modifiers` / `HandlerParams`）。
**处理器注册表**在 `Scripts/Rewards/RewardEffectRegistry.cs` —— handler id 拼错会在**读表时**警告并跳过那一行
（不会静默变成"没有效果的奖励"）。

**纯数据就能表达的效果**（不需要新 C#，只要填 `Modifiers` 的 `Stat` / `Operation` / `Value`）：

| Stat 键 | 谁在读 | 默认值 |
|---|---|---|
| `DamageMultiplier` | `Weapon.cs` / `EnergyWaveAbility` / `ArrowVolleyAbility` / `MeleeComboAbility.Strike` | 1 |
| `AttackSpeedMultiplier` | `Weapon.cs` | 1 |
| `MoveSpeedMultiplier` | `CharacterMovement` | 1 |
| `MaxHealthMultiplier` / `MaxHealthAdd` | `Unit.RefreshStats` | 1 / 0 |
| `DamageReductionAdd` | `Unit.CombinedDamageReduction` | 0 |
| `CritChanceAdd` / `CritMultiplierAdd` | 武器与三个攻击技能 | 0 |
| `ProjectileCountAdd` / `PierceAdd` / `RicochetAdd` | 武器 + `EnergyWaveAbility` + `ArrowVolleyAbility` | 0 |
| `ExplosionRadiusAdd` / `ExplosionDamageScale` | `Weapon.cs` +（**P5 新增**）`EnergyWaveAbility` | 0 |
| `CooldownMultiplier` | `Ability.UnitCooldownMultiplier`（角色技能 / 近战武技的 CD） | 1 |
| **`JumpVelocityMultiplier`**（P5 新增） | `CharacterMovement.EffectiveJumpVelocity` | 1 |
| **`AttackRangeMultiplier`**（P5 新增） | `MeleeComboAbility.MeleeRangeMultiplier`（乘在表里的命中盒几何量上） | 1 |
| **`MeleeLungeAdd`**（P5 新增） | `MeleeComboAbility.BeginLunge`（近战突进距离，米） | 0 |
| **`ChargeBaseSecondsOverride` / `ChargeStepSecondsOverride` / `ChargeStepSecondsMultiplier`**（P5 新增） | `Ability.ChargeBaseSecondsEffective` / `ChargeStepSecondsEffective` | 0 / 0 / 1 |
| **`VolleyCountOverride` / `VolleyDamagePerLostArrow`**（P5 新增） | `ArrowVolleyAbility.FireVolley` | 0 / 0 |
| **`AbilityOverride` 处理器**（P5b 新增） | HandlerParams 里除 `AbilityId` 外每一条 = `Ability` 上的一个 `[Export]` 属性名 → 值，用 Godot 的 `Set()` 套用（属性名写错按 `GetPropertyList()` 查出并告警，**不静默**）；行上若带 `Modifiers` 则先按 `StatSet` 的语义套上 | 服务 **6** 条进化（J9 起「龙之箭」改用 `ChargedShot` 强类型资源） |
| **`TablePulse` 处理器 + `TablePulseRule`**（P5b 新增；P6a 支持多事件） | 事件 → 效果：`Trigger`（`Kill` / `Parried` / `Critical` / `ArmorBroken` / `Damaged`，**可以逗号分隔多个，任一命中即出手**）+ 一次性 CDR（`AbilityId` + `CooldownFraction` / `CooldownSeconds` / `RefreshAbility`）/ `Heal` / `HoverSeconds` / `RequireHover` / `ExtraAirJumps` + `JumpCap` / `Radius` + `Damage` | **一个类服务 6 条行** |
| **`StompOnLand` 处理器 + `StompOnLandRule`**（**P6b 新增**，服务「杀戮践踏」） | **落地那一刻**对周围敌人造成一次范围伤害：`Radius` / `Damage` / `MinFallHeight` / `Cooldown`（触发间隔）。⚠️ 它是**轮询落地边沿**而不是订阅事件 —— `CharacterMovement` 是共用战斗类（P6b 不许碰），没有抛起跳/落地事件；规则自己每帧读它**已公开**的 `IsGrounded` / `FallHeight`。范围伤害与「能量护盾」共用 `PulseDamage.Around` | 1 条 |
| **`HomingMissileOnCrit` 处理器 + `HomingMissileOnCritRule`**（**P6b 新增**，服务「巡飞弹」） | 暴击 → 发射一枚**追踪飞弹**：`ProjectileScene` / `Damage` / `Speed` / `SeekRange` / `Lifetime` / `BeaconStatusId` / `BeaconSeconds` / `TurnRateDegrees` / `Cooldown`。复用的是 **P3 的追踪弹通路**（`Projectile.HomingStatusId` + 转向速度）：先给最近的敌人挂**专用信标状态**（`eff_missile_beacon`），再发一枚追这个状态的飞弹 | 1 条 |
| **`BurnOnHit` 的 `Cooldown`**（P5b 新增） | 命中挂状态那条规则自己的触发间隔（「冰晶」的"每 10 秒最多一次"） | 0（= 不拦，改动前行为） |
| **`MarkedBonus` 处理器 + `MarkedBonusRule`**（**J8 新增**，服务「发散」「心眼」两条行） | 命中给目标叠「标记」，并按**这一击命中之前**目标身上已有的层数放大**自己**造成的伤害：`StatusId` / `Duration` / `FlatBonus`（心眼 0.5）/ `PerStackBonus`（发散 0.1 = 预设 N）。来源过滤全在表上：**加标来源** = `RequiredEventAbility`（「发散」=<c>skl_a1_w1_1</c>、「心眼」=<c>skl_a3_w1_1</c>），**加伤来源** = 新列 `BonusAgainstAbility`（「发散」只提高波波；「心眼」留空 = 玩家的各种伤害）。消费者是 **`AttackInstance.RollHit` 里那次 `IOutgoingDamageScale` 查询**（所以"第一次加标这一击不加伤"是顺序保证的，不是特例判断） | 2 条 |
| **`ChargedShot` 处理器 + `ChargedShotTuning` 资源列**（**J9 新增**，服务「龙之箭」） | **松手时蓄力 ≥ 阈值 → 这一发更大 + 额外穿透 N 个**（判定在`ArrowVolleyAbility.OnChargeReleased`**冻结**，`FireVolley` 只读快照；发射仍只由释放段的真实 `ATK` 事件帧触发）。参数全在强类型资源里：`TargetAbility`（资源引用）/ `MaxChargeSeconds` / `MaxSteps`（0=不限，填 15 = "2 秒后不再加档"）/ `ThresholdSeconds` / `SizeScale` / `PierceCount`。接线走公开强类型方法 `ArrowVolleyAbility.ConfigureChargedShot`，**没有 `Godot.Set` 字符串反射** | 1 条 |
| **`RampMaxCount` / `RampWindowSeconds`**（P5b 新增） | `ArrowVolleyAbility.FireVolley`：连续射击第 N 发时发数至少 N（「变奏」） | 0 / 0（= 一行不进） |
| **`RuneEffect` 处理器 + `RuneRules.cs` 的 5 个规则类**（**J11 新增**，服务 5 枚"需要一个行为"的符文行） | 符文（开局携带的信物）**分两半**：`Modifiers` 走与 `StatSet` 同一段代码，`HandlerParams` 里**有哪个键就做哪件事** —— `Armor`（立刻加甲）/ `ArmorPerKills`+`ArmorPerTrigger`（每 N 杀加甲）/ `DeathAvoid`+`DeathAvoidHealFraction`（死亡避免）/ `NearRange`…`FarScale`（按距离加伤）/ `MaxHealthDamageRatio`（按最大生命值加伤）/ `PixieIntervalSeconds`…（轨道小精灵）。⚠️ 纯数值的符文（雷之印/战神印/风之印/行军/大力符/飞翼）**不用它**，直接 `StatSet` | 5 条符文行 |
| **StatSheet 新键 `MaxHealthCap` / `IncomingDamageMultiplier` / `MaxFallSpeedMultiplier` / `HealMultiplier`**（**J11 新增**） | 生命上限**硬夹**（0=不夹，`Unit.RefreshStats`）/ 受到的伤害倍率（1=不变，`Unit.ReceiveHit`）/ 正常下落速度上限（1=不变，`CharacterMovement`）/ 治疗量倍率（1=不变，**0 = 禁疗**，`Unit.Heal`） | 0 / 1 / 1 / 1（= 改动前） |
| **`IOutgoingDamageBonus` + `Unit.OutgoingDamageBonusAgainst`**（**J11 新增**） | 攻击者侧的**扁平加伤**口（「巨人」符文）：`AttackInstance.RollHit` 里在既有的 `IOutgoingDamageScale` 倍率**之后**加一项；走倍率口会被暴击一起放大，所以单开一个口。没有登记时 `+0f`（`x + 0f == x`） | 无实现方 → 恒 0 |
| **`IDeathAvoid` + `Unit.AddDeathAvoid`**（**J11 新增**） | **致命伤无效化**（「生死」符文）：钩子在 `Unit.ReceiveHit` 的 `bool killed` 之后、`result` 构造之前（`Damaged` 事件抛得太晚，拦不住 `Die`） | 无实现方 → 那一段 `if` 不进 |
| **`GameplayEventKind.Parried`**（P5b 新增的事件种类） | 抛点唯一：`GuardStanceAbility.OnOwnerHit`（架住那一刻）。订阅方：招架反击 / 能量护盾 | 没有订阅者时 `Raise` 什么也不做 |

⚠️ **新增键必须有基础值**（`Unit.SeedStats`）+ 读取端"读到 0 就按中性值算"的双保险 ——
`architecture.md` 第 8 条：少一个基础值不能变成"零冷却 / 打不出伤害 / 跳不起来"。

### 2.3 需要一个**新的行为**时才改 C#

- 加一个 handler：在 `RewardEffectRegistry.Handlers` 字典里加一条（`"名字" = row => context => { … }`），
  数值全部从 `row.HandlerParams` 读（`Param(row, "键", 兜底)` / `ParamText(...)`）。
- 加一个"订阅战斗事件"的规则：写一个 `TriggerRule` 子类（`RunBuild` 会自动登记并驱动它的冷却），
  然后在 handler 里 `context.Player.AddChild(new XxxRule { … })`。**一个类服务所有同类行**（数值全在表里）。

**P5b 就是这么做的两个例子**（可以直接照抄）：

| 新东西 | 服务几条行 | 做法 |
|---|---|---|
| `AbilityOverride` 处理器 | **6 条**（有备无患 / 专注 / 气贯长虹 / 地爆 / 大拔刀术 / 神降术；**J9 起「龙之箭」改用 `ChargedShot`**） | 不写特例：HandlerParams 里**除 `AbilityId` 外每一条都是"属性名 = 值"**，用 Godot 的 `Set()` 套到 `Ability.FindByAbilityId` 找到的那个节点上 |
| `TablePulse` 处理器 + `TablePulseRule` | **6 条**（乱金柝 / 闪身步 / 招架反击 / 普渡 / 践踏死亡 / 能量护盾） | 一个类带 `Trigger` + 一组效果参数；差别只是"订哪种事件、出手做什么、数值多少" |

---

## 3. 三条判定**不许**用代码写死的东西

| 不许写死 | 正确写法 |
|---|---|
| 角色名 / 角色 Id | 选项行 `RequiredCharacterIds`；池初始化时从 `CharacterRow(_character).Id` 拿当前角色 Id |
| 武器名 / 武器 Id | 武器行 `Pool` ↔ 选项行 `Pool`（**资源引用**，不是字符串） |
| "谁能和谁共存" | 选项行 `Excludes` / `ConflictsWith`（**双向/单向都在表里**） |
| "要先拿到谁" | 选项行 `Requires` |
| 数值（伤害/半径/时长/层数/权重/n） | `Modifiers` / `HandlerParams` / `MaxStacks` / `Weight` |

文档里填 `n` 的：**先填"能跑起来的最小预设值" + 在行 `Note` 里写明"文档为 n，此处是预设"**（用户 §1.6）。

---

## 4. `RewardCatalog` 的 19 条兜底硬编码 —— **已删**（P5 决策）

调研报告 §2.2 点名了它，并且点出了真后果：

| 位置 | 问题 |
|---|---|
| `Scripts/Rewards/RewardCatalog.cs:21-91` | 整个 19 条兜底目录硬编码，且 **id 与表不一致**（兜底 `dmg_up`/`pierce_round`，表 `upg_dmg_up`/`upg_pierce_round`） |
| `RewardCatalog.cs:51` vs `Config/Tables/Upgrades/evo_ricochet.tres` | 兜底写 `excludes: ["pierce_round","evo_ap"]`，表写 `["upg_pierce_round","evo_ap"]` → **兜底路径上"跳弹 × 贯穿弹头"的互斥静默失效** |

**判定：删（不是"表驱动化"）。** 三条理由：

1. **它是第二份真源**（`config-schema.md` §1.4-①）：表在编辑器里改、C# 数组改不动，必然漂移 ——
   实测已经漂了，漂掉的正好是互斥判定的 Id。
2. **"加内容"这件事本身要求它消失**：P5 一次加了 31 条，如果兜底也要一份就得写两遍，
   与 `character-redesign.md` §1.5「加一条增幅 = 加一行表」直接冲突。
3. **它护不住任何东西**：实测（`RunController.ResolveStages` 的注释）删/改一个被引用的行文件会让
   **整份** `ConfigIndex.tres` 加载失败 —— 那时所有数值一起回退到代码默认值（玩家生命 250→160），
   游戏已经不可玩；"只有增幅表读不到"这个状态在实际路径上到不了。

**回退语义保留**（终点从"一份会漂的名单"改成"空目录 + 大声留痕"）：

- `RewardCatalog.All` 默认空数组；`RewardTable.Build` 在表为空/一行都编译不出来时打明确日志。
- 池子为空时互动奖励节点给不出东西，但**不会崩、不会死锁**：`StageController` 收到 `false` 就只是这次交互不生效。
- **`ChainLightningRule` / `LifestealRule` / `BurnOnHitRule` / `AmmoOnKillRule` / `SummonOnKillRule`
  这 5 个 `TriggerRule` 实现全部保留** —— 它们是可复用的基础设施（handler 用它们，不是兜底目录的一部分）。

---

## 5. 自测怎么取证（`--selftest`，退出码 0）

`SelfTest.ReportP5Layers()` 是 P5 的取证入口，**全程走真实代码路径**（`RewardPool.Initialize` + `Roll`），
只是把"当前角色/武器"换成几个探针值（`new Weapon { PoolId = …, AttackTags = … }`，不进树、用完 `Free()`）：

| 行 | 证什么 |
|---|---|
| `✅ P5·层①限定（角色+武器）` | 猎手+能量掌 **有** `evo_blast_palm`（A1限定）；猎手+弓 **没有** `evo_focus`（它属于 `pool_crossbow`）；武器大师+镰刀 **有** `mut_cleave`（B 限定）、**没有** `mut_ricochet`（A 限定）；枪手+能量掌 **没有** `mut_ricochet`、**有** `upg_power`（通用） |
| `✅ P5·层②前置` | 空构筑里**没有** `mut_ricochet_plus`；拿到 `mut_ricochet` 后**有**它；`mut_ricochet_plus2` 仍然没有（它前置的是 `mut_ricochet_plus`，**跳级拿不到**） |
| `✅ P5·层③互斥（逻辑冲突）` | 弹射 × 穿透：两条同时在池里 → 拿到弹射后穿透消失 → **反向再验一次也拦得住**（双向） |
| `✅ P5·层③互斥（平衡限制）` | 专一 × 花心（`<A2限定>`）：用的是**同一个 `Excludes` 字段** —— 用户 §1.3 说的两类互斥来源在实现上无区别 |
| `✅ P5·实测拿到并生效` | 用**真实玩家 + 真实 `RewardContext`** 套「力量」（`DamageMultiplier` 1 → 1.15）与「攀高」（起跳速度 13 → 14.04 m/s），再按 `Source` 还原 —— **量测不留痕**，后面的旧硬指标仍然逐位可核 |

**P5b 新增的四段取证**（同一条 `SelfTest.ReportP5Layers`，`--selftest` 退出码 0）：

| 行 | 证什么 |
|---|---|
| `✅ P5b·层①限定（新行）` | 猎手+弓 **有** 变奏 / 龙之箭（`<A3限定>`）、**有** 乱金柝（`<A限定>` = 只要角色 A）、**没有** 金光术（`<B1限定>`）；武器大师+灵剑 **有** 金光术 / 地爆（`<B限定>`）/ 冰晶（通用异化）、**没有** 变奏 |
| `✅ P5b·层②前置（新行）` | 空构筑里 **没有** 冰晶+ / 烈阳+；拿到「冰晶」后 **有** 冰晶+、**仍然没有** 烈阳+（它前置的是「烈阳」，两条链互不相干） |
| `✅ P5b·实测表参数落到机制上` | **探测技能节点**（`new` 出来、**不进树**，与探针武器同一类做法）+ 7 条 `AbilityOverride` 行：后撤 `MaxCharges 3` / `Recharge 0.5s`、长枪武技2 `ChargeBaseSeconds 0.5` / 每档 `0.125`、弓武技1 `RampMaxCount 3` / `RampWindow 1.2s` / `ChargeMaxSeconds 3`、拔刀式 `ProjectileSizeScale 2` + 伤害乘区 `1.5`、落剑式 `Knockback 6` / `Cooldown 2s` / `DamageScale 1.6` |
| `✅ P5b·实测异化规则` | **真实玩家 + 真实 `RewardContext`**：冰晶 → `BurnOnHitRule` `Cooldown 10s` + `StatusId Freeze`；能量护盾 → `TablePulseRule` `Trigger Parried` / 半径 6 / 伤害 20；践踏死亡 → 击杀匹配 `True`、**直接调 `Matches`/`Execute` 出手一次**后额外空中跳跃 `0 → 1`（封顶 3），量完把三条规则节点摘掉、跳跃次数还原 |

⚠️ ⑨ 那一段**不往全局事件总线上抛真事件**（抛了会被关卡记账 / `GameLog` / 碎片当真，污染后面的旧硬指标），
所以走的是"同一个 `Matches` / `Execute` 入口"的直接调用 —— 这两条判据写在日志里，读数时看得出来。

⚠️ 造"已拥有 X"的构筑走 `RunBuild.DebugGrantWithoutApplying`（**只记层数、不套效果**）——
用 `Add` 会把 X 的效果真挂到玩家身上，那一局的数值就被探针污染了。

---

## 6. 本批（P5 + P5b）建了什么 / 没建什么

> ⚠️ **2026-10-15 P6a 已清旧**：下面的"现有行"是 P5b 当时的读数；清旧后的实际行数见第三列括号里的注。
> 全表 **74 → 57 行**（强化 9 / 进化 23 / 异化 25）。
>
> ⚠️ **2026-10-16 P6b**：异化 **25 → 27 行**（补了「杀戮践踏」`mut_stomp` 与「巡飞弹」
> `mut_homing_missile`），**全表 59 行 = 强化 9 / 进化 23 / 异化 27**。通用异化还差 3 条（见 §7 第 1 条）。
>
> ⚠️ **2026-10-16 P6d**：最后 3 条通用异化补完 —— 异化 **27 → 30 行**（护卫 `mut_guard` / 落日剑
> `mut_sunset_sword` / 灵魂收割 `mut_soul_harvest`），**全表 62 行 = 强化 9 / 进化 23 / 异化 30**
> （= 设计文档要求的 30 条**全建**）。`ConfigIndex.tres` 的索引**逐位不变**
> （`6/2/10/1/1/39/7/14/8/5/6/11/4`），只动了 `load_steps` 与 `Upgrades` 数组。

| 池 | 设计文档要求 | 现有行 | 状态 |
|---|---|---|---|
| **进化** | 23 条（A 2+3+3+3 / B 3+3+3+3） | **27 行** = 设计文档 23 条**全建** + 4 条旧进化行（双管齐下 / 穿甲弹 / 跳弹 / 爆裂弹头，P6 清旧前保留）→ **P6a 后 23 行** | ✅ 条数齐 |
| **异化** | 文档 30 条（通用 12 + A 9 + B 9） | **28 行** = 通用 8 + A 9 + B 9 + 旧行 3（吸血 / 亡者随从 / 战地补给）→ **P6a 后 25 行 = 通用 7 + A 9 + B 9** → **P6b 后 27 行** → **P6d 后 30 行**（通用 12 + A 9 + B 9） | ✅ 条数齐 |
| **强化** | 文档 9 条（+ 旧 10 条） | 19 行 → **P6a 后 9 行**（旧 10 条全删） | ✅ |

逐条偏差（效果只接了一半、或整条没接的）见 [rewards.md](rewards.md) 的「P5b 落地记录」。

---

## 7. 边界（下一轮接着做）

1. **通用异化：P6b 做了 2 / 5，P6d 把最后 3 条做完**（30 条**全建**）：
   - 实机取证（真实按键 / 真实拾取 / 实拍截图）与**逐条偏差**见 [rewards.md](rewards.md) 的「P6d 实机取证」；
     拍摄台与两个 MCP 手法见 [vfx.md](vfx.md) §5.1 / §6.1。
   - ✅ **杀戮践踏**（`mut_stomp`）—— `StompOnLandRule` 轮询落地边沿 + `PulseDamage.Around`；
     半径 3 米 / 伤害 15 / 最小落高 0 是**预设值**（文档只给了 `n`）。
   - ✅ **巡飞弹**（`mut_homing_missile`）—— `HomingMissileOnCritRule` 复用 P3 追踪弹通路；
     伤害 50 来自文档，弹速 60 / 索敌 18m / 存活 4s / 信标 1.5s / 转向 360°/s 是预设值。
   - ✅ **护卫**（`mut_guard`，**P6d 已做**）—— 触发的"奔跑"是本批**新补的前置机制**（文档第 19 行「所有角色通用的机制」）：
     `project.godot` 新增 `sprint` 动作（Shift）· 角色表新增 `SprintInputAction` / `SprintSpeedMultiplier` 两列
     （预设 1.5）· 新组件 `Scripts/Combat/SprintController.cs`（挂 `Player.tscn` 的 `Sprint` 节点，
     `IsSprinting` / `EffectiveSpeed`）。**`CharacterMovement` 一行没改**（只读它已公开的
     `IntentDirection` / `IsGrounded` / `IsDashing` + `Ability.IsBusy`，与 `StompOnLandRule` 同一条纪律）。
     格挡本身落在 `GuardOnSprintRule`（`Scripts/Rewards/RewardCatalog.cs`）+ **既有**的 `BlockComponent`：
     获得 = `Blocking = true`，破碎 = `Unit.Damaged` 事件的 `DamageResult.Blocked`。
     `Cooldown 5` 来自文档 `{5}`；挡后剩伤 `0`（完全格挡）/ 正面阈值 `1`（不看正面）是**预设值**。
   - ✅ **灵魂收割**（`mut_soul_harvest`，**P6d 已做**）—— `SoulHarvestRule` 订阅 `Kill` → 按概率
     在死者位置掉一个 `SoulPickup`（`Scripts/Combat/SoulPickup.cs`）。**比 P5b 当时登记的方案更省**：
     不建 `.tscn`、不建 `Area3D`，拾取是**每帧距离轮询**（`player_units` 组里有没有单位进到
     `PickupRadius` 内）—— 一点物理世界都不进，所以"不进单位列表"这条纪律天然成立
     （`Unit` 0 个 / 碰撞体 0 个 / 分组 0 个，`DebugState()` 打这三个量）。
     文档只写了"有概率 / 恢复一定血量" → 概率 0.3 / 回血 8 / 拾取半径 1.2m / 存活 15s 都是**预设值**。
   - ✅ **落日剑**（`mut_sunset_sword`，**P6d 已做**）—— 订阅 `Hit` 事件的 `SunsetSwordRule` +
     挂在敌人身上的 `SunsetSwordMarker`（头顶悬挂 → 延迟 → 落下 → 打一圈 + 画一圈）。
     **"武技1"不写死技能 Id**：拿行里的 `TriggerInputAction`（按键动作名）反查技能行的 `InputAction` 列，
     再与这一击的 `Tag`（= `AttackInstance.Ability`）比 —— 6 把武器的武技1 自动适用。
     **"没有落日剑"= 目标身上没有 `SunsetSword` 子节点**（按名查，**没有新增效果表行**）。
     落地范围伤害复用 `PulseDamage.Around`（P6d 加"圆心可指定"重载），落圈复用 P6c 的 `LandingShockwave`
     （P6d 加 `PlayOnce(位置, 半径)`）。⚠️ **没有**复用它"下落攻击"那条技能（共用 `Ability` 子类不许碰）。
     文档只写了"一段时间后 / 造成伤害" → 五个数（1.5s / 0.35s / 2.5m / 40 / 3.2m）都是**预设值**。
2. **3 条占位行没有任何效果**（行 `Note` 里都写了"⚠️ 占位行"）：
   `evo_golden_qi` 金光术（要 `MeleeComboAbility` 每段额外生成一道剑气）、
   `evo_affection` 喜爱（要飞行中的武器每 2 秒索敌转向）、
   `evo_variation` 变奏（只接了"发数按连射序号"，"变得更强"那一半没接）。
3. **各有一半没接**（都在行 `Note` 里逐条写明）：怜悯（"只释放最后一刀"）、气贯长虹（"不再能收回 / 落地立刻收回"）、
   神降术（"范围更大"——半径在命中体上，不在那个能力的 `[Export]` 列里）、浮光跃金（"闪现至目标高度"）、
   闪身步（没校验"这一杀是闪造成的"）、普渡（没校验"是最后一刀杀的"）。
   → ⚠️ **2026-10-17 J8 已把「心眼 / 发散」那一条从本文里划掉**：两条行不再复用 `BurnOnHit`，
   改成新处理器 **`MarkedBonus`**（`MarkedBonusRule`，一个类服务两条行）+ 新增强类型资源列
   `RewardConfig.BonusAgainstAbility`；加标来源与加伤来源都进表（`RequiredEventAbility` / `BonusAgainstAbility`），
   按**命中前**目标层数结算（`AttackInstance.RollHit` → `IOutgoingDamageScale`）。
   发散**预设每层 10%**（文档 `{N}` 未定，行 `Note` 写明）。见 [character-design-j8.md](character-design-j8.md)。
   → ⚠️ **2026-10-17 J9 已把「龙之箭」那一条从本文里划掉（两半都接了）**：处理器 **`ChargedShot`** +
   强类型资源列 `RewardConfig.ChargedShot`（`ChargedShotTuning`）；**松手时蓄力 ≥ 2 秒**这一发才更大（`SizeScale 2`）
   且额外穿透 `PierceCount 99` 个，判定在松手时**冻结**、发射仍由释放段的真实 `ATK` 驱动。
   ⚠️ 口径仍是"**档位继续加到 3 秒**（满蓄 ×8.5）"，2 秒只是大箭门槛；要"2 秒后不再加档"把 `MaxSteps` 填 15（不改 C#）。
   见 [character-design-j9.md](character-design-j9.md)。
4. ✅ **P6a 已修**：`ArmorBroken` / `ShieldBroken` / `Critical` 原来**一个抛点都没有**（只有 `GameLog` 的
   `switch` 认得它们）。现在三者各有**唯一**抛点，都在 `Unit.ReceiveHit`：
   - `ArmorBroken` ← 扣甲后 `Armor <= 0` 那一刻（`ArmorBroke?.Invoke()` 旁边）
   - `ShieldBroken` ← 相邻分支（扣盾后 `Shield <= 0`）
   - `Critical` ← 结算完这次命中、`HitInstance.Critical` 且真的打出了伤害
   顺手把「能量护盾」的"护甲破碎"那一路接上了：`TablePulseRule.Trigger` 现在**支持逗号分隔**
   （任一命中即出手），`mut_energy_shield` 填的是 `Parried,ArmorBroken`。
5. ⚠️ **状态的"升级"语义缺失**：烈阳+ / 冰晶+ 与它们的前置同时持有时会**叠加**（文档语义是"变得更强"）；
   而且 `EffectConfig` 没有跳伤间隔（`TickSeconds`）那一列 → 烈阳+ 现在用 `DamagePerSecond 6 → 12` 表达。
6. **表现层**：✅ **P6b 做了「准星处的蓄力指示器」**（`Scripts/UI/ChargeIndicator.cs` +
   `Scenes/UI/Hud.tscn` 的 `Root/ChargeIndicator` 节点）—— 正方形四条边、每个角留缺口，
   缺口随 `Ability.ChargeProgress` 收到 0 即"闭合"，闭合 / 完美时机时换成完美色并给准星本体染色
   （`Ability.IsPerfectWindow` 是 P2 就有的只读量，表现层只读它、不碰判定）。
   截图：`docs/screenshots/p6b_charge_open_zoom.png` / `p6b_charge_closing_zoom.png` / `p6b_charge_perfect_zoom.png`。
   ⬜ 还没做的表现层：开镜特效（弓武技2 的放大视角已有 FOV 收缩，缺屏幕级特效）、
   后撤残影（技能1「后撤」）、剑气拖尾（灵剑武技2「拔刀式」）、落圈特效（灵剑武技3「落剑式」）。
7. ✅ **P6b 已做**：弹匣/装填那条横向清理（`card_magazine` / `card_reload` / `meta_reload` +
   `Weapon` 的装填逻辑 + HUD 的弹药显示 + `reload` 输入动作 + 自测期望串 + 文档）。
   **清了 5 层**，逐层清单见 [character-redesign.md](character-redesign.md) §五。
8. **P6b 新增的两条异化有自测行为取证**（`SelfTest.DebugP6bMutationProbe()`，跑在**最后一个阶段**）：
   真实玩家 + 真敌人靶子，两条规则各真的出手一次 —— 践踏掉 15 血、巡飞弹 FireCount 1 / 目标挂上信标 /
   场上多一枚投射物，量完全部还原。⚠️ 它直接调 `Execute`（不是按键/真实落地），
   因为跑在最后是为了不污染前面的硬指标。
