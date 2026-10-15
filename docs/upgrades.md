# 增幅系统 · 三层判定（限定 / 互斥 / 前置）—— P5 接入契约

> **权威**：`docs/character-redesign.md`（用户对"怎么做"的澄清）+ `F:\Godot项目\3d肉鸽雨中哈迪斯\角色设计.txt`（"做什么"）。
> 本文件是**给下一轮（P6 清旧 / 继续补增幅）看的接入契约**：三层判定分别落在哪张表、哪个文件，
> 以及**"加一条增幅要做什么"**。

---

## 0. 一句话

**池的本质 = 哈迪斯的祝福清单，按「当前角色 + 当前武器」过滤候选。**
**加一条增幅 = 加一个行文件（`Config/Tables/Upgrades/*.tres`）+ 在 `ConfigIndex.tres` 里挂上它 —— 不改 C#。**

---

## 1. 三层判定分别落在哪

| 层 | 判什么 | 表列（磁盘） | 运行期字段 | 判定代码 | 判定时机 |
|---|---|---|---|---|---|
| **① 限定 · 角色** | 当前**角色行 Id** 在名单里吗 | `RewardConfig.RequiredCharacterIds` | `RewardDefinition.RequiredCharacterIds` | `RewardDefinition.AllowsCharacter` ← `RewardPool.Initialize` | **进池时**（一次） |
| **① 限定 · 武器** | ①专属池 → ②武器标签 → ③通用（三级回退） | `RewardConfig.Pool` / `RequiredWeaponTags` | `ApplicablePoolIds` / `RequiredWeaponTags` | `RewardDefinition.AllowsWeapon` ← `RewardPool.Initialize` | **进池时**（一次） |
| **② 互斥** | 双向：候选排斥已拥有 / 已拥有排斥候选 | `RewardConfig.Excludes`（双向）、`ConflictsWith`（单向） | `Excludes` / `ConflictsWith` | `RewardPool.IsExcluded` + `Roll` 里的 `ConflictsWith` 循环 | **每次抽取** |
| **③ 前置** | `Requires` 里的 Id **全部**已拥有 | `RewardConfig.Requires` | `RewardDefinition.Requires` | `RewardPool.HasPrerequisites` ← `RewardPool.Roll` | **每次抽取** |

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
| `AttackSpeedMultiplier` / `ReloadSpeedMultiplier` / `MagazineSizeAdd` | `Weapon.cs` | 1 / 1 / 0 |
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
| **`AbilityOverride` 处理器**（P5b 新增） | HandlerParams 里除 `AbilityId` 外每一条 = `Ability` 上的一个 `[Export]` 属性名 → 值，用 Godot 的 `Set()` 套用（属性名写错按 `GetPropertyList()` 查出并告警，**不静默**）；行上若带 `Modifiers` 则先按 `StatSet` 的语义套上 | 服务 7 条进化 |
| **`TablePulse` 处理器 + `TablePulseRule`**（P5b 新增） | 事件 → 效果：`Trigger`（`Kill` / `Parried` / `Critical` / `ArmorBroken` / `Damaged`）+ 一次性 CDR（`AbilityId` + `CooldownFraction` / `CooldownSeconds` / `RefreshAbility`）/ `Heal` / `HoverSeconds` / `RequireHover` / `ExtraAirJumps` + `JumpCap` / `Radius` + `Damage` | **一个类服务 6 条行** |
| **`BurnOnHit` 的 `Cooldown`**（P5b 新增） | 命中挂状态那条规则自己的触发间隔（「冰晶」的"每 10 秒最多一次"） | 0（= 不拦，改动前行为） |
| **`RampMaxCount` / `RampWindowSeconds`**（P5b 新增） | `ArrowVolleyAbility.FireVolley`：连续射击第 N 发时发数至少 N（「变奏」） | 0 / 0（= 一行不进） |
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
| `AbilityOverride` 处理器 | **7 条**（有备无患 / 专注 / 龙之箭 / 气贯长虹 / 地爆 / 大拔刀术 / 神降术） | 不写 7 个特例：HandlerParams 里**除 `AbilityId` 外每一条都是"属性名 = 值"**，用 Godot 的 `Set()` 套到 `Ability.FindByAbilityId` 找到的那个节点上 |
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

| 池 | 设计文档要求 | 现有行 | 状态 |
|---|---|---|---|
| **进化** | 23 条（A 2+3+3+3 / B 3+3+3+3） | **27 行** = 设计文档 23 条**全建** + 4 条旧进化行（双管齐下 / 穿甲弹 / 跳弹 / 爆裂弹头，P6 清旧前保留） | ✅ 条数齐 |
| **异化** | 文档 30 条（通用 12 + A 9 + B 9） | **28 行** = 通用 8 + A 9 + B 9 + 旧行 2（吸血 / 亡者随从 / 战地补给里通用那几条） | 🟡 通用还差 5 条 |
| **强化** | 文档 9 条（+ 旧 10 条） | 19 行 | ✅ |

逐条偏差（效果只接了一半、或整条没接的）见 [rewards.md](rewards.md) 的「P5b 落地记录」。

---

## 7. 边界（下一轮接着做）

1. **通用异化还差 5 条**：杀戮践踏（要"跳跃事件"，现在没有）/ 护卫（奔跑态 + 格挡授予）/
   巡飞弹（暴击事件**枚举有、全项目没有抛点**）/ 灵魂收割（要掉落物拾取系统）/ 落日剑（延迟落剑的新机制）。
2. **3 条占位行没有任何效果**（行 `Note` 里都写了"⚠️ 占位行"）：
   `evo_golden_qi` 金光术（要 `MeleeComboAbility` 每段额外生成一道剑气）、
   `evo_affection` 喜爱（要飞行中的武器每 2 秒索敌转向）、
   `evo_variation` 变奏（只接了"发数按连射序号"，"变得更强"那一半没接）。
3. **各有一半没接**（都在行 `Note` 里逐条写明）：心眼 / 发散（按**目标身上的标记层数**加伤）、
   龙之箭（蓄力 2 秒后箭变大 + 全穿透）、怜悯（"只释放最后一刀"）、气贯长虹（"不再能收回 / 落地立刻收回"）、
   神降术（"范围更大"——半径在命中体上，不在那个能力的 `[Export]` 列里）、浮光跃金（"闪现至目标高度"）、
   闪身步（没校验"这一杀是闪造成的"）、普渡（没校验"是最后一刀杀的"）。
4. ⚠️ **`ArmorBroken` / `ShieldBroken` / `Critical` 三个枚举值全项目一个抛点都没有**（只有 `GameLog` 的
   `switch` 认得它们）—— 凡是要订这三类事件的增幅（能量护盾的"护甲破碎"那一路、巡飞弹）**都得先补抛点**。
5. ⚠️ **状态的"升级"语义缺失**：烈阳+ / 冰晶+ 与它们的前置同时持有时会**叠加**（文档语义是"变得更强"）；
   而且 `EffectConfig` 没有跳伤间隔（`TickSeconds`）那一列 → 烈阳+ 现在用 `DamagePerSecond 6 → 12` 表达。
6. 表现层（准星蓄力指示器 / 剑气拖尾 / 刀光缩放）属 P6。
