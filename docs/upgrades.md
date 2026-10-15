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

⚠️ **新增键必须有基础值**（`Unit.SeedStats`）+ 读取端"读到 0 就按中性值算"的双保险 ——
`architecture.md` 第 8 条：少一个基础值不能变成"零冷却 / 打不出伤害 / 跳不起来"。

### 2.3 需要一个**新的行为**时才改 C#

- 加一个 handler：在 `RewardEffectRegistry.Handlers` 字典里加一条（`"名字" = row => context => { … }`），
  数值全部从 `row.HandlerParams` 读（`Param(row, "键", 兜底)` / `ParamText(...)`）。
- 加一个"订阅战斗事件"的规则：写一个 `TriggerRule` 子类（`RunBuild` 会自动登记并驱动它的冷却），
  然后在 handler 里 `context.Player.AddChild(new XxxRule { … })`。**一个类服务所有同类行**（数值全在表里）。

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

⚠️ 造"已拥有 X"的构筑走 `RunBuild.DebugGrantWithoutApplying`（**只记层数、不套效果**）——
用 `Add` 会把 X 的效果真挂到玩家身上，那一局的数值就被探针污染了。

---

## 6. 本批（P5）建了什么 / 没建什么

见 `docs/rewards.md` 末尾的「P5 落地记录」表（分类计数 + 逐条偏差）。

---

## 7. 边界（下一轮接着做）

1. **通用异化池 12 条**（杀戮践踏 / 践踏死亡 / 护卫 / 能量护盾 / 烈阳 / 冰晶 / 烈阳+ / 冰晶+ / 巡飞弹 / 灵魂收割 / 落日剑 / 闪电链）**一行都没建** ——
   它们要新的事件种类（落地 / 格挡成功 / 护甲破碎 / 暴击）与"掉落物拾取"系统（调研报告 M19/M28/M30/M31/M32）。
2. **`<B1>/<B2>/<B3限定>` 的 9 条进化**（金光术 / 大拔刀术 / 神降术 / 怜悯 / 普渡 / 喜爱 / 气贯长虹 / 金光大道 / 浮光跃金）
   **一行都没建** —— 每条都要动各自武技的子系统（`SwordQiAbility` / `LandingBurstAbility` / `WeaponThrowAbility` / `SpearThrowAbility` / `MeleeComboAbility` 的串参数）。
3. **`<A3限定>` 的 3 条**（变奏 / 心眼 / 龙之箭）**没建** —— 需要"连续射击计数""被标记目标额外受伤""蓄力 2 秒后全穿透"三个新通路。
4. **`<A1限定>` 的专注 / 发散**、**`<A2限定>` 花心的"追踪"** —— 需要"授予追踪（字符串参数）"与"标记层数→技能伤害"的处理器。
5. 表现层（准星蓄力指示器 / 剑气拖尾 / 刀光缩放）属 P6。
