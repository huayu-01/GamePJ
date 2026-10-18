# 符文系统 · 契约（加一枚符文 / 加一个派系要做什么）

> **2026-10-18 J27**：飞翼已补下落攻击加速；新增原表DiveSpeedMultiplier，默认预设1.3，真实三武器34→44.2m/s，副本倍率2的长枪斜向总速68m/s。原有起跳与正常下落上限保持，正常下落仍60→48m/s。效果与说明共读三个Modifiers.Value，复用StatSet。[反例与真实物理验收](character-design-j27.md)。

> **2026-10-18 J26**：死铠的每杀计数独立于攻击连锁门，同链十杀与合法末层击杀均记录；初始甲／加甲间隔／每次加甲量仍由原表配置，说明共读参数，禁疗保持。[反例与验收](character-design-j26.md)。

> **权威来源**：`F:\Godot项目\3d肉鸽雨中哈迪斯\后续构筑.txt`（"做什么"）+ 用户 2026-10-17 的原话（"怎么做"）。
> 本文件是**给下一轮看的接入契约**：符文落在哪张表、哪个处理器、前置判定在哪一行，
> 以及"加一枚符文 / 加一个派系"到底是几件事。
> 交付记录与实测读数见 [character-design-j11.md](character-design-j11.md)。

---

## 0. 一句话

**符文 = 哈迪斯信物：开局携带一件 → ① 一个效果（多为负面）② 解锁某些增幅的前置条件。**
**加一枚符文 = 加一个行文件（`Config/Tables/Runes/rune_*.tres`）+ 在 `ConfigIndex.tres` 里挂上它 —— 不改 C#。**

用户原话（这是整个系统的定义，不要重新解释）：

> 「就是哈迪斯那样的，原文想表达的意思就是携带了这个东西之后，会产生些效果，比如雷之印，
> 这个效果就是最大生命值受限，仅此而已，然后新增的增幅里，有一些它是有前置，要求雷之印，
> 所以携带了这个雷之印等于满足它们的前置要求」

---

## 1. 一张表、两个字段、两条通路

| 层 | 在哪 | 是什么 |
|---|---|---|
| **表** | `Config/Tables/Runes/rune_*.tres`，一行一个文件 | `RuneConfig`（**继承 `RewardConfig`**，见 §5） |
| **索引** | `Config/ConfigIndex.tres` → `Runes` 数组（`ConfigIndexData.Runes`） | 与其它表同一条纪律：`ext_resource` + 数组项 + `load_steps` 三处同改 |
| **① 效果** | 行上的 `ApplyHandler` + `Modifiers` + `HandlerParams` | 走**既有的** `RewardEffectRegistry` 处理器注册表（与增幅项**同构**） |
| **② 前置解锁** | 行上的 `UnlocksRequires`（`Godot.Collections.Array<string>`） | 携带时写进 `RunBuild.RuneUnlocks`，判定点在 `RewardPool.HasPrerequisites` |

**不携带任何符文时**：`RunBuild.RuneUnlocks` 为空 → `HasPrerequisites` 退化成改动前的
`build.StacksOf(id) > 0`；玩家身上没有任何符文修正器与规则节点。**逐位不变**（`_RuneProbe` 第 ②/⑧ 段可证）。

---

## 2. 加一枚符文要做什么（三步）

1. **加一个行文件** `Config/Tables/Runes/rune_<你的id>.tres`（照抄一枚同类的改字段）。
   最少要填：`Id` / `DisplayName` / `DescriptionTemplate` / `Note` / `Slot`，
   以及 §3 里的**效果**（`ApplyHandler` 或 `Modifiers`）与**前置解锁**（`UnlocksRequires`，可以为空）。
2. **`Config/ConfigIndex.tres` 三处同改**：① 顶部一条 `[ext_resource]`（id 取**现有最大值之后**）、
   ② `load_steps` 加 1、③ `Runes` 数组加一项。
3. 跑一次，日志里核对两行：
   - `[Config] 符文表（开局携带一件的信物，**不进奖励池**）：N 枚（来源：表 Runes）—— […]`
     ← 每枚符文会打出它的 handler、关键列，以及**"它会解锁哪几条增幅的前置"**；
   - `[Config] 配置表：… （ConfigIndex.tres 已加载）` ← 必须有这一句，否则整份表都没读上。

⚠️ **`Note` 里必须写清"文档为 `{n}`"的那几处 + 你的预设值 + 理由**（用户 §1.6 的纪律）。

---

## 3. 效果从哪来（**能复用就复用**）

### 3.1 纯数值：`StatSet`（**一条 C# 都不用加**）

现成可用的 StatSheet 键（`Unit.SeedStats` 里都有基础值）：

| 键 | 含义 | 默认 | 消费者 |
|---|---|---|---|
| `DamageMultiplier` | 造成的伤害 | 1 | `Weapon` 开火 / `EnergyWaveAbility` / `ArrowVolleyAbility` / `MeleeComboAbility.Strike` |
| `MoveSpeedMultiplier` | 移速 | 1 | `CharacterMovement` |
| `CooldownMultiplier` | 技能冷却**倍率**（>1 = 更慢） | 1 | `Ability.UnitCooldownMultiplier` → `EffectiveCooldown` |
| `CritChanceAdd` | 暴击率 | 0 | 武器与三个攻击技能 |
| `MaxHealthMultiplier` / `MaxHealthAdd` | 生命上限（乘 / 加） | 1 / 0 | `Unit.RefreshStats` |
| `DamageReductionAdd` | 减伤 | 0 | `Unit.CombinedDamageReduction` |
| `JumpVelocityMultiplier` | 起跳速度 | 1 | `CharacterMovement.EffectiveJumpVelocity` |
| **`MaxHealthCap`**（J11 新增） | **生命上限硬夹**（0 = 不夹） | 0 | `Unit.RefreshStats` |
| **`IncomingDamageMultiplier`**（J11 新增） | **受到的伤害倍率**（4 = 承受 4 倍） | 1 | `Unit.ReceiveHit` |
| **`MaxFallSpeedMultiplier`**（J11 新增） | 正常下落速度上限 | 1 | `CharacterMovement` 的下落夹速 |
| **`HealMultiplier`**（J11 新增） | 治疗量倍率（**0 = 完全禁疗**） | 1 | `Unit.Heal` |

> ⚠️ `HealMultiplier` 的 **0 是有意义的值**（禁疗），所以它不是"读到 0 就按 1 算"那一类；
> 它的基础值必须在构造函数里就位（`Unit()` 无条件跑一次 `SeedStats`），改读取端时别套那条兜底。

### 3.2 需要一个行为：`RuneEffect` 处理器（**一个类服务所有这类行**）

`HandlerParams` 里**有哪个键就做哪件事**，缺的键一律不做（所以一行只写它自己要的）：

| 键 | 效果 | 实现 |
|---|---|---|
| `Armor` | 立刻加甲 | 既有 `Unit.GrantArmor` |
| `ArmorPerKills` + `ArmorPerTrigger` | 每 N 杀 +甲 | `RuneArmorOnKillRule`（订阅 `Kill`） |
| `DeathAvoid` + `DeathAvoidHealFraction` | N 次死亡避免 + 恢复比例 | `RuneDeathAvoidRule` + `IDeathAvoid`（`Unit.ReceiveHit` 的致命判定处） |
| `NearRange`/`NearScale`/`FarRange`/`FarScale` | 按距离加伤 | `RuneDistanceDamageRule`（`IOutgoingDamageScale`，**复用 J8 的钩子**） |
| `MaxHealthDamageRatio` | 每次攻击额外造成最大生命值 × ratio | `RuneMaxHealthDamageRule`（`IOutgoingDamageBonus`） |
| `PixieIntervalSeconds`/`PixieRadius`/`PixieDamage`/`PixieOrbitRadius`/`PixieOrbitSpeed` | 轨道小精灵 | `PixieCompanion`（`Node3D`，不进物理世界、不进任何单位分组） |

`Modifiers` 在 `RuneEffect` 里**也会先被套上**（与 `StatSet` 共用同一段 `ApplyModifiers`），
所以"死铠 = 加甲 + 禁疗 + 每 N 杀加甲"这种组合只需要一行 + 一个 handler。

### 3.3 需要在 `Unit` 里多一个口子时

照 J11 的两个例子做（都满足"只加不删、没有登记时行为逐位不变"）：

- `IDeathAvoid`：登记在 `Unit._deathAvoids`，读取点是 `ReceiveHit` 里 `bool killed` **之后**、
  `result` 构造**之前**的那一小段（`if (killed && _deathAvoids.Count > 0)`）；
- `IOutgoingDamageBonus`：登记在 `Unit._outgoingDamageBonuses`，读取点是 `AttackInstance.RollHit`，
  **加**在既有的 `OutgoingDamageScaleAgainst` 之后（`x + 0f == x`，所以没登记时逐位不变）。

⚠️ **不要用 `Godot.Get/Set("属性名", …)` 字符串反射**（`architecture.md` 第 12 条）。

---

## 4. 加一个"派系"要做什么（符文 → 增幅前置）

**派系 = 一个前置字符串**，不是一个枚举、不是一个池、不是新代码。

1. 符文行上填 `UnlocksRequires`（`Array<string>`）：一枚符文可以填**多个**派系名，
   例如将来的"雷之印 + 雷系特化"两档也可以写成两条字符串；
2. 想让增幅吃这个前置，就在**那条增幅行**的 `Requires` 里写同一个字符串；
   两种写法都认：**派系名**（`雷之印`，文档里的写法）或**符文行 Id**（`rune_lei_seal`，表里更稳的写法）。
   携带时 `RunBuild.SetRuneUnlocks` 会把**两者都放进去**，所以表里怎么填都行；
3. **不需要**改 `RewardPool` / `RewardDefinition` / `ConfigIndex`：前置判定只有一个入口
   （`RunBuild.SatisfiesPrerequisite`），符文只是它的**第二个来源**。

⚠️ **符文不进奖励池**：它不在 `Upgrades` 数组里，所以不会出现在"增幅项 62 条"那个读数、
也不会被 `RewardPool.Roll` 抽到。判定"符文带了没带"只有一个地方：`RunController.CarriedRuneId`。

### 4.1 现在有哪些派系可用

| 派系字符串 | 哪枚符文解锁 | 目前表里有几条增幅吃它 |
|---|---|---|
| `雷之印` | `rune_lei_seal` | **0 条**（`后续构筑.txt` 里那些雷系特化增幅属于后续批次，本批不建） |
| `战神刻印` | `rune_war_seal` | **0 条**（同上） |
| `风之印` | `rune_wind_seal` | **0 条**（同上） |

⚠️ 这是**如实登记**，不是"已经能抽到"：本批只交付**机制**与**表**，
2026-10-18按最新文档重清：原清单漏计雷池，新增B2风之领域，待建共23项；其中22项有明确符文前置，暴风未写前置，不自行补。完整限定／前置与源文件SHA256见 [更新接收](followup-build-updates.md)。这些生产行仍未建，不能称已能抽到。
机制侧的证明用的是 `_RuneProbe` 第 ⑥ 段：临时插两条 `Requires = [雷之印]` / `[战神刻印]` 的**探针行**，
走**真实** `RewardPool.Initialize` + `Roll` —— 不带符文时两条都不进池，携带对应符文时**正好多 1 条**、
且派系不串。见 [character-design-j11.md](character-design-j11.md) §6。

---

## 5. 为什么 `RuneConfig` 继承 `RewardConfig`

用户的要求是「**负面效果走既有的 handler 通路**、能复用就复用」。处理器注册表的签名是
`Handler(RewardConfig row)`，继承之后 `RewardEffectRegistry.Compile(runeRow)` 可以直接编译符文行 ——
`StatSet` / `GrantArmor` / `RuneEffect` 全部对符文可用，**没有第二套效果系统、没有第二个注册表**。

代价（已接受）：符文行会顺带拥有 `RewardKind` / `MaxStacks` / `Weight` / `Pool` / `Excludes` 这些列，
它们对符文**没有意义**（符文不进池），表行里一律留默认值。`ConfigRows` 按 `typeof(T).Name` 分桶，
所以 `Runes` 与 `Upgrades` 的 `Count`/`ById` 互不串台。

---

## 6. 携带入口（本批只到 C#，**没有 UI**）

| 层 | 在哪 | 说明 |
|---|---|---|
| 局外存档 | `RuneCarry`（`Scripts/Run/RuneCarry.cs`）→ `user://rune.cfg` | `ConfigFile`，section `rune`，key `carried`。**单独一个文件**，不动四个既有存档（`meta` 的 SHA256 基线因此保持不变） |
| 本局字段 | `RunController.CarriedRuneId` / `CarriedRune` | 开局在 `_Process` 的 `_boot` 段读 `RuneCarry.Current` |
| 写入口 | `RunController.SetCarriedRune(string? id)` | 记字段 + 落盘 + **立刻**重套（换符文时先摘旧的） |
| 应用点 | `RunController.ApplyCarriedRune()`（在 `ApplyBalance` 里、`_loadout.Apply` **之后**） | 顺序很重要：生命回满之后才夹 30 |

⚠️ **本批没做**：符文选择 UI（基地交互物 / 面板 / 图标）、`快乐小子` 的"奔跑穿透单位 + 角色虚化"。
下一批做 UI 时只需要调 `SetCarriedRune`，判定与效果代码一行都不用动。

---

## 7. 自测怎么取证

`Scenes/Debug/_RuneProbe.tscn`（`Scripts/Testing/RuneProbe.cs`）**83/0**，八段全走真实路径：
表 → 索引（含 `Array<string>` 列逐项核对）· 默认逐位不变 · 雷之印 220→30 · 战神印 ×4 ·
风/行军/大力/飞翼的乘区 · **前置解锁（真实 `RewardPool.Roll`）** · 五枚带行为的符文 ·
摘下后回到基线。

```powershell
& 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' `
  --headless --path 'F:\Godot项目\项目a' --quit-after 20000 res://Scenes/Debug/_RuneProbe.tscn
```

⚠️ 探针里 `RuneCarry.UseInMemoryStoreForSelfTest()` 会把携带项恒置为空且**不写盘** ——
Main 自测的 8 条硬指标必须在"没带符文"的状态下跑。
