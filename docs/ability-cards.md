# 能力卡（局外装备层）：怎么加一张卡、效果怎么挂、矩阵怎么扩

> **给"要加一张能力卡 / 改额度 / 改矩阵布局"的人看。** 读完这一篇你不需要看别的文档。
> 需求来源：[requirements-ability-system.md](requirements-ability-system.md) **§一（能力系统）** 与 **§二（矩阵 UI 与交互）**。
> 相关：[config.md](config.md)（配置表体系与纪律）、[ui.md](ui.md)（界面外观唯一入口，**§10 按键提示栏**）、
> [audio.md](audio.md)（加音效要做什么）。
>
> 一句话：**能力卡 = 局外的卡牌**（可解锁 → 可用局外碎片多次升级 → 可激活/关闭，
> **激活时占用"额度点数"**，玩家总额度有限（**上限本身也能用局外资源扩增**），
> 界面是**满屏矩阵**（默认 6×3 = 18 格，格子尺寸随容器自适应，**卡不够时用占位卡填满、不翻页**）。
> 状态存 `user://ability_cards.cfg`，激活的效果在**本局开局**由 `RunController.ApplyBalance` 套到玩家身上。

---

## 0. 30 秒结论

| 问题 | 答案 |
|---|---|
| 卡表在哪？ | `Config/Tables/AbilityCards/card_*.tres`，**一行 = 一张卡**（现有 **8 张**）。索引挂在 `Config/Balance.tres` 的 `AbilityCards` 数组上。 |
| 行类？ | `Scripts/Config/AbilityCardConfig.cs`（继承 `ConfigRow`，所以每行都有 `Id` / `Slot` / `DisplayName` / `DescriptionTemplate` / `Note` / `Params`）。 |
| 状态存哪？ | `user://ability_cards.cfg`（Godot 原生 `ConfigFile`）。**不是** `res://Config/`（那是表）、也**不是** `user://meta.cfg`（那是碎片与永久强化）。 |
| 花什么资源？ | **碎片**（`MetaProgress`）—— 需求里的"局外资源"就是它，本系统**没有另建货币**。 |
| 额度上限怎么算？ | `Balance.tres → AbilityQuotaBase`（基础 6）+ 局外成长行 **`Config/Tables/Meta/meta_quota.tres`** 的 `PerLevel` × 扩增等级。 |
| 界面在哪？ | `Scenes/UI/AbilityMatrixScreen.tscn` + `Scripts/UI/AbilityMatrixScreen.cs`，**挂 `Scenes/Main.tscn`**，快捷键 **K**（`UiService.OpenAbilityMatrix()` 也能开）。 |
| 空位怎么办？ | **空位也是一张卡**（**占位卡**，用户 A5）：卡表里没有对应行的那一格就是它 —— **没有解锁条件 → 永远无法解锁 / 激活 / 升级**，但点它 / 长按它的**读条、震动、失败音效照旧**（见 §5.6）。矩阵**永远是填满的矩形、不翻页**，格子尺寸随容器自适应。 |
| 元素（基类）在哪？ | `Scenes/UI/AbilityCardTile.tscn` + `Scripts/UI/AbilityCardTile.cs` —— 三状态 + 四种交互 + 读条 + 震动 + 音效**全在它里面**（占位卡走的是**同一条**代码路径）。 |
| 激活后真的生效吗？ | 生效。`AbilityEffects.ApplyToPlayer` 在 `RunController.ApplyBalance` 末尾被调 1 行；实测"最大生命 250 → 290"、"`AbilitySummon.Enabled` False → True"。 |
| 加一张卡要改几个文件？ | **2 个**（加一行 `.tres` + 挂进 `Balance.tres` 的数组）—— **零 C#**。详见 §8。 |
| 表读不到会怎样？ | 矩阵全是**占位卡**、本局没有任何卡效果，**玩法其余部分完全不受影响**（与"表读不到就用自身默认值"同一条纪律）。 |

---

## 1. 先回答那个问题：能力卡与 `Config/Tables/Skills/` 是什么关系

需求包 §一 特意留了一句：「⚠ 注意与现有系统的关系……**不要直接混进 `Skills/`**，先判断两者关系再动手」。
**判断结论：两者是"局外 / 局内"两层不同的东西，所以分成两张表，唯一的接触点是一列资源引用。**

### 1.1 怎么判断的（可复核的证据，不是感觉）

| 查了什么 | 看到了什么 | 结论 |
|---|---|---|
| `Scripts/Config/AbilityConfig.cs` 的字段 | `NodeName`（**单位身上的能力节点名**）/ `ClassHint`（期望的脚本类）/ `Aim` / `Cooldown` / `Windup` / `Recover` / `Enabled` / `ApproachMinRange` / `ApproachMaxRange` / `Params`（子类专属）/ `AttackParams`（**命中体**参数） | 它描述的是"**某个单位身上的某个能力节点怎么配**"，是**战斗管线的一部分** |
| 谁读 `Skills` 表 | `UnitConfig.Skills`（资源引用）→ `CharacterConfig.ApplySkillsTo` / 敌人与首领行；`BossPerkCatalog.Build`（`ApplyHandler` 非空的那 4 行） | 消费点全是**局内**（进关卡时套给单位 / Boss 出生时抽） |
| `Config/Tables/Skills/` 的 25 行 | 5 玩家技能 + 16 单位技能 + 4 首领行为技能；`docs/config.md` 的表里它就叫「技能表：一个技能」 | 它是**一局之内不变**的东西 |
| 需求原文 | 「能力**都可以被解锁**，之后可以通过**局内获取的局外资源来升级**，然后这些能力是**可激活或关闭**，**激活时占用一定的额度点数**」 | 解锁 / 升级 / 开关 / 额度**都是跨局的**，与"一局内怎么打"没有关系 |
| 文档纪律 | `docs/config.md` 开头：「配置表**只加不改**」；需求包：「不要直接混进 `Skills/`」 | 混进同一张表会同时违反两条 |

### 1.2 两层各自负责什么

```
【局外】Config/Tables/AbilityCards/card_*.tres      ← 本轮新增（8 行）
   解锁费 / 每级升级费 / 等级上限 / 占用几点额度 / 图标 / 说明 / 激活后的效果
   │
   │  Effect = GrantSkill 时，用 Skill 这一列（**资源引用**）指过去
   ▼
【局内】Config/Tables/Skills/skl_*.tres             ← 一个字都没改（25 行原样）
   NodeName（能力节点）/ 冷却 / 前摇 / 后摇 / 朝向 / 子类参数 / 命中体参数
```

**两张表的唯一接触点 = `AbilityCardConfig.Skill`**（`card_summon.tres → skl_summon.tres`、`card_control.tres → skl_control.tres`）。
所以：

- 卡说"**你有这个技能**"，技能行说"**这个技能的具体参数**"——**卡不复制任何技能参数**；
- 改技能冷却/命中体参数仍然只改 `Skills/` 一行，**卡的 8 行一个字都不用动**；
- 引用是**资源引用**而不是字符串 id：技能行被删/改名时 Godot 把引用置 null（**可判定、可警告**），
  而且 `Balance.ReportAbilityCards()` 会在开局日志里报"卡指向的技能行不在 `Skills` 索引里"。
  这与 `chr_*.tres → skl_*.tres` / `wpn_*.tres → pool_*.tres` 是**同一套写法**。

---

## 2. 数据层：表结构

### 2.1 `Config/Tables/AbilityCards/card_*.tres`（一行 = 一张卡）

行类：`Scripts/Config/AbilityCardConfig.cs`。列分三组（Inspector 里有中文分组）：

| 组 | 列 | 类型 | 含义 |
|---|---|---|---|
| **卡牌 · 图标与额度** | `IconName` | string | 图标文件名（不含目录/扩展名）→ `Assets/UI/Icons/<它>.svg`。留空 = 只显示名字 |
| | `QuotaCost` | int | **激活时占用的额度点数**（"必须取舍"就是它撑起来的） |
| | `MaxLevel` | int | 等级上限（**≥ 1**；解锁后等级 = 1） |
| **价格（局外碎片）** | `UnlockCost` | int | 解锁价（等级 0 → 1） |
| | `UpgradeBaseCost` | int | 第 1 次升级（1 → 2）的价 |
| | `UpgradeCostGrowth` | int | 每多一级的涨价（线性：`Base + Growth × (当前等级-1)`；与 `MetaUpgradeConfig` 同一套公式） |
| **激活后的效果** | `Effect` | enum | `StatAdd`(0) / `StatMultiply`(1) / `GrantSkill`(2) |
| | `Stat` | string | 改哪个属性键（与 `StatSheet` 一致），前两种效果读它 |
| | `PerLevel` | float | 每级的量；`GrantSkill` 时 = **每级（从 2 级起）减少的冷却秒数** |
| | `Skill` | `AbilityConfig` 引用 | `GrantSkill` 时指 `Skills/skl_*.tres` 的一行 |

两个便利方法（算价与文案，**只有一处实现**，日志/悬停提示共用）：
`UpgradeCostOf(level)`、`RefundFor(level)`（重置时该退多少）、`EffectText(level)`、`GrantedCooldown(level)`。

### 2.2 现有 8 张卡（本轮的最小可用闭环）

| 行 Id | 名字 | 效果 | 额度 | 解锁 | 升级 | 上限 |
|---|---|---|---|---|---|---|
| `card_vitality` | 强化肌体 | `MaxHealthAdd` **+20/级** | 2 | 30 | 20 + 15/级 | 5 |
| `card_damage` | 凶戾印记 | `DamageMultiplier` ×(1+0.06/级) | 3 | 40 | 30 + 20/级 | 5 |
| `card_speed` | 疾风之靴 | `MoveSpeedMultiplier` ×(1+0.05/级) | 2 | 30 | 20 + 15/级 | 5 |
| `card_crit` | 锐利直觉 | `CritChanceAdd` **+0.04/级** | 2 | 35 | 25 + 15/级 | 5 |
| `card_reload` | 快速装填 | `ReloadSpeedMultiplier` ×(1+0.08/级) | 2 | 25 | 15 + 10/级 | 5 |
| `card_magazine` | 扩容弹匣 | `MagazineSizeAdd` **+3/级** | 2 | 30 | 20 + 15/级 | 5 |
| `card_summon` | 无人机协议 | **授予 `skl_summon`**（G 键召唤无人机），每级 -0.5s 冷却 | 4 | 60 | 45 + 30/级 | 3 |
| `card_control` | 控制立场 | **授予 `skl_control`**（范围定身），每级 -0.75s 冷却 | 4 | 60 | 45 + 30/级 | 3 |

6 张数值卡 + 2 张授予技能卡 —— 这是覆盖了三种 `Effect` 的**示范**，不是最终数量。
8 张卡的额度之和是 21 点，而初始上限只有 6 点，**所以必须取舍**（这正是需求要的东西）。

### 2.3 额度上限的扩增行（`Config/Tables/Meta/meta_quota.tres`）

额度上限 = **`Balance.tres → AbilityQuotaBase`（基础 6）** + **扩增等级 × 每级增量**。
扩增这一行是**已有的局外成长表**里的第 6 行（不是新机制）：

```
Id = "meta_quota"  Slot = 6  DisplayName = "容量扩容"
SaveKey = "quota_level"   Stat = "AbilityQuotaAdd"（只是可读文案）   PerLevel = 1.0（每级 +1 点）
BaseCost = 30  CostGrowth = 20      → 1 级 30 / 2 级 50 / 3 级 70 …
```

⚠️ **等级本体存在 `user://ability_cards.cfg` 的 `[quota] quota_level`**，由 `AbilityStore` 读写，
**不在 `user://meta.cfg` 里**（`MetaProgress` 只认它自己那 5 个 `*_level` 键）。
`meta_quota` 这一行只提供**每级增量与价格公式**，所以 `SaveKey` / `Stat` 对它是"给人看的元数据"，
这一点在行文件的 `Note` 里写明了 —— 别照着它去 `MetaProgress` 里找 `quota_level`。

### 2.4 索引与开局日志

- `Scripts/Config/BalanceConfig.cs`：`[Export] AbilityCardConfig[] AbilityCards`（**表索引**）+
  `[Export] int AbilityQuotaBase = 6`（**跨表的全局开关**）。
- `Scripts/Config/Balance.cs` 的 `BuildTableIndex()` 里 `ConfigRows.Rebuild(Config.AbilityCards)`，
  并由 `ReportAbilityCards()` 每条开局写一行日志：

  ```
  [Config] 能力卡（局外装备层，与 Skills 是两层）：8 张（来源：表 AbilityCards）—— [card_vitality（2 点/解锁 30/上限 Lv5）、…]；
           额度上限 = 基础 6 + 扩增行 meta_quota 每级 +1（1 级价格 30，每级 +20）
  ```
- 卡"授予技能"却没填 `Skill`、或指向的技能行不在 `Skills` 索引里 → **warning + 日志**（不静默）。

---

## 3. 状态层：`Scripts/AbilityCards/AbilityStore.cs`

静态类，**唯一一处**判定"能不能解锁 / 激活 / 升级 / 重置 / 扩增额度"。

### 3.1 存盘格式（`user://ability_cards.cfg`）

```ini
[cards]
card_vitality_level=3          # 0 = 未解锁；解锁后从 1 起
card_vitality_active=true      # 只写激活的那几张（没写 = 关闭）

[quota]
quota_level=2                  # 额度扩增等级
```

为什么**分文件**而不是塞进 `user://meta.cfg`：`res://Config/` 是**表**（只读、进仓库），
`user://` 是**玩家进度**；三份进度各自有主人（`meta.cfg` = 碎片与永久强化、`settings.cfg` = 设置、
`ability_cards.cfg` = 卡片与额度），**任何一份坏了都不牵连另外两份**（与 `docs/audio.md` / `docs/ui.md` §12 同一条边界）。

### 3.2 记账与事务

花的/退的都是 `MetaProgress` 的碎片。**任何一步失败都不扣钱、不改状态**，并返回一个明确的
`AbilityCardResult`：

| 方法 | 成功条件 | 失败会返回 |
|---|---|---|
| `TryUnlock(row)` | 未解锁 且 碎片 ≥ `UnlockCost` | `AlreadyUnlocked` / `NotEnoughShards` / `NoCard` |
| `TryUpgrade(row)` | 已解锁 且 等级 < `MaxLevel` 且 碎片 ≥ `UpgradeCostOf(level)` | `Locked` / `MaxLevel` / `NotEnoughShards` |
| `TryToggleActive(row)` | 已解锁；**开启**时还要求 `QuotaCost ≤ QuotaLeft` | `Locked` / `QuotaFull` |
| `TryResetUpgrades(row)` | 已解锁 且 等级 > 1 → 等级回到 1，**全额归还**升级费（`RefundFor`，解锁费不退） | `Locked` / `NothingToReset` |
| `TryBuyQuota()` | 碎片 ≥ `QuotaUpgradeCost` → 扩增等级 +1 | `NotEnoughShards` / `NoCard`（`meta_quota` 读不到） |

额度：
`QuotaCap = AbilityQuotaBase + round(QuotaLevel × QuotaPerLevel)`、
`QuotaUsed = Σ(激活中的卡的 QuotaCost)`、`QuotaLeft = max(0, Cap - Used)`。
**`QuotaUsed` 是按当前激活集合现算的**，不是另存一个计数 —— 少一个会漂的真源。

### 3.3 两个注入点（自检/拍摄台用它换钱包）

```csharp
public static Func<int, bool> Spend  { get; set; } = MetaProgress.TrySpend;   // 付钱
public static Action<int>     Refund { get; set; } = MetaProgress.AddShards;  // 退钱
```

外加 `DebugBeginSandbox(shards, allowSave)` / `DebugEndSandbox()` / `DebugSetState(...)` /
`DebugSetQuotaLevel(...)`：自检与拍摄台**全程用内存钱包 + 不落盘**，
所以"跑一遍完整的解锁→升级→重置→激活→额度扩增"**不会改动玩家的任何存档**（实测：跑完 `ability_cards.cfg` 仍然不存在）。

---

## 4. 生效层：`Scripts/AbilityCards/AbilityEffects.cs`

### 4.1 接线只有一处（1 行）

`RunController.ApplyBalance()` 的**末尾**：

```csharp
AbilityEffects.ApplyToPlayer(_player, this);
```

**位置是刻意的**：它在"角色表数值 → 能力开关矩阵 → 技能表 → 角色攻击组"**之后**。
因为角色的 `DisabledAbilityIds`（枪手关掉 `SummonAbility` / `AreaAttackAbility`）会把召唤/控制关掉，
而一张"授予技能"的卡**应该赢过它** —— 卡是玩家花局外资源买来的额外能力。

### 4.2 三种效果怎么落地

| `Effect` | 做什么 | 落在哪 |
|---|---|---|
| `StatAdd` | `player.Stats.AddModifier(Stat, Op=Add, Value=PerLevel×等级)` | 走已有的 `StatSheet` + `Unit.RefreshStats`（`MaxHealthAdd` → 最大生命） |
| `StatMultiply` | `... Op=Multiply, Value = 1 + PerLevel×等级` | 同上（与局外成长表的 Multiply 语义一致） |
| `GrantSkill` | 找到玩家身上的 `Skill.NodeName` 能力节点 → `Skill.ApplyTo(node)`（冷却/前摇/参数/命中体）→ **`Enabled = true`** → 按等级写 `Cooldown` | 走已有的 `AbilityConfig.ApplyTo` + `Ability` 组件 |

**幂等**：所有修饰都挂在同一个专用 `SourceToken` 上，进函数先 `Stats.RemoveBySource(SourceToken)`，
所以换角色 / 重开一局重复调用**不会叠加**（同一张卡不会 ×2）。
（对比：`RunController` 里给局外成长加的那几个 modifier 用的是 `Source = this`，重复调用会叠加 —— 不是本轮范围，别照抄。）

### 4.3 开局日志（"生效了吗"的第一手证据）

```
[Ability] 能力卡：激活 1/8 张 · 额度 2/6 · 碎片 279 —— 强化肌体 MaxHealthAdd +20
[Ability] 能力卡：激活 0/8 张 · 额度 0/6 · 碎片 279 —— 本局没有激活的卡（全部效果为空）
```

---

## 5. 矩阵元素基类：`AbilityCardTile`（需求 §二 的"做进基类"）

`Scenes/UI/AbilityCardTile.tscn` + `Scripts/UI/AbilityCardTile.cs`。**一格 = 一张卡**。

### 5.1 它负责什么

| 职责 | 实现位置 |
|---|---|
| **三个状态**的视觉 | `RefreshVisual()`（**唯一一处**决定颜色 / 变暗 / 光圈 / 文案） |
| **等级 → 颜色** | `static LevelColor(int)` / `LevelName(int)`（唯一一处） |
| **四种交互**（含长按读条） | `_GuiInput` + `_Process` → `RunAction(action)` |
| **失败震动** | `PlayFailShake()`（绕中心来回摆 + 红闪），**所有**失败路径共用 |
| 解锁 / 升级 / 重置的**缓动** | `PlayUnlockEffect()` / `PlayUpgradeEffect()` / `PlayResetEffect()` / `PlayActivatePulse()` |
| **音效** | `RunAction` 里按结果播（成功按动作分 4 条、失败 1 条） |
| 激活时的**呼吸光圈** | `StartGlowPulse()` / `StopGlowPulse()` |

### 5.2 它与"规则"的分工（宿主接口）

```csharp
public interface IAbilityCardHost
{
    AbilityCardResult Request(AbilityCardTile tile, AbilityCardAction action);
}
```

元素把"玩家按了什么"翻译成 `Unlock` / `ToggleActive` / `Upgrade` / `ResetUpgrades`，
**判定交给宿主**，拿回一个 `AbilityCardResult`，**只按结果决定播哪个动画 + 哪个音**。
好处：① 元素完全不知道存档、碎片、额度的存在，可以整份复制到别的界面；
② 判定只有一处实现（`AbilityStore`），界面不会长出第二套规则；
③ 自检塞一个假宿主就能把四种交互逐条跑一遍（不用真的点鼠标）。

### 5.3 三个状态 / 六档边框色 / 光圈（需求原文逐条对照）

| 需求 | 实现 |
|---|---|
| 未解锁 = **变暗** | `_content.Modulate = (0.40,0.42,0.48)` + 更深的底板 + 等级 0 的**灰**边框 |
| 解锁未激活 = **恢复颜色** | `_content.Modulate = 白`，边框按等级着色 |
| 激活 = **有一圈光圈** | `Glow`（`Panel` + `StyleBoxFlat`，`DrawCenter=false` + `BorderWidth=3` + `ShadowSize=10`）**比 `Frame` 大 9px** → 光圈落在**边框外**；颜色 = 边框色；`Modulate.a` 在 0.55↔1.0 之间呼吸（Tween 循环） |
| 边框颜色表示等级：**灰0 > 白1 > 绿2 > 蓝3 > 紫4 > 红5** | `LevelColor()`（`#6b7080 / #ebf0ff / #59eb6b / #57a3ff / #b86bff / #ff5747`） |
| 光圈颜色与边框对应 | 同一处 `LevelColor(level)` 同时喂给 `Frame` 与 `Glow` |
| **制作时预留足够的间隔** | 矩阵格尺寸随容器自适应（1920×1080 下实测 284×220）+ **14px** 行列间隔（`GridContainer` 的 h/v_separation）；光圈往外扩 9px 也不会互相压住 |

### 5.4 四种交互（逐条对照需求）

| 需求原文 | 实现 | 结果 |
|---|---|---|
| **左键长按**（未解锁）→ **读条** → 结束判定资源 → **不够则震动** → 够则解锁 + 播解锁视觉（放大/闪，缓动） | `_GuiInput` 记下按下时刻 → `_Process` 每帧算 `ChargeProgress`（`LongPressSeconds`，默认 **0.7s**）并显示底部 `Progress` → **进度到 1.0 时**才 `RunAction(Unlock)` | `Ok` → `PlayUnlockEffect()`（scale 1→1.16→1 + 白闪）+ `ui_card_unlock`；其它 → `PlayFailShake()` + `ui_card_fail` |
| **左键**（未激活）→ **激活** | 松手时若**没读完条** → `RunAction(ToggleActive)` | 成功 `ui_click` + 轻弹；`QuotaFull` → 震动 + 失败音 |
| **右键** → **升级**（资源不够 → 震动） | 右键松手若没读完条 → `RunAction(Upgrade)` | 成功 `ui_card_upgrade` + 脉冲；`MaxLevel`/`NotEnoughShards` → 震动 + 失败音 |
| **右键长按**（已升级多次）→ 读条 → **重置升级进度并归还资源** | 右键按住且 `Level > 1` 时读条 → 完成即 `RunAction(ResetUpgrades)` | 成功 `ui_card_reset` + 缩回弹起；`NothingToReset` → 震动 + 失败音 |
| **失效/失败的交互都要震动对应元素** | `PlayFailShake()`（唯一实现）；额度按钮那一处用界面的 `ShakeControl()` | — |
| **震动、点击、升级等行为都要有音效** | 见 §6 音效表 | — |

**松手兜底**：鼠标在元素**外面**松开时 `_GuiInput` 收不到事件，所以 `_Process` 里还轮询
`Input.IsMouseButtonPressed` 补一次结算（否则读条会永远卡住）。
探针用 `DebugBeginCharge` / `DebugReleaseCharge` 读条时走 `_syntheticHold` 跳过这条轮询。

### 5.5 给后续维护者的"契约"

> **加一张卡**：只动表（§8）。`AbilityCardTile.cs` 与 `AbilityMatrixScreen.cs` **一个字都不用改**。
>
> **改一个已经存在的东西长什么样**：`LevelColor`（边框/光圈色）、`LongPressSeconds`（读条时长）、
> `ShakeRadians` / `ShakeSeconds`（震动幅度/时长）都是 `[Export]`，在 `AbilityCardTile.tscn` 上就能改。
>
> **换一个宿主界面**：实例化 `AbilityCardTile.tscn` + 实现 `IAbilityCardHost` 就够了。
>
> **加一种新效果**（比如"每局开始多一次三选一"）：`AbilityCardEffectKind` 加一个值 →
> `AbilityEffects.ApplyCore` 加一个 `case` → 加一行卡。**三步，界面完全不用碰**。

### 5.6 占位卡（用户 A5：「空的位置也有卡牌占着……一直无法解锁」）

**怎么区分**：`AbilityCardTile.Card == null` ⇔ 占位卡（`IsPlaceholder`）。界面建格子时，
`i < 卡表行数` 的格子 `Bind(卡行, host)`，其余 `Bind(null, host)` —— 也就是
**"卡表里没有对应行"就是占位卡**，不需要在表里造一行假卡，也不需要在元素上多一个开关。

| 占位卡的性质 | 实现（**没有第二条分支**） |
|---|---|
| **永远无法解锁 / 激活 / 升级** | 宿主（`AbilityMatrixScreen.Request`）看到 `tile.Card == null` 就返回 `AbilityCardResult.NoCard` → 四种操作全部落到失败分支；`AbilityStore` 一行都没碰过（它只认卡行） |
| **但交互反馈照旧**（点它 / 长按它 → 震动 + 失败音效） | `_GuiInput` 不再排除 `Card == null`；`_Process` 的读条、`FinishHold`、`RunAction` 都走**同一条路**：读到 `NoCard` → `FailCount++` + `PlayFailShake()` + `ui_card_fail`。左键长按照样读满 0.7 秒的条（它的"等级"恒为 0，与"未解锁的真卡"同一条读条分支） |
| **看起来也是一张卡** | 灰边框（比"未解锁"压暗一档，`PlaceholderBorderAlpha = 0.55`）+ 底板 + `ic_ui_card_placeholder.svg`（卡框 + 加号）+ 名字「占位卡」+ 状态「占位卡 · 不可解锁」+ 提示「未设定解锁条件」+ tooltip 说明怎么把它变成真卡 |
| **不影响真卡** | 真正的卡仍进 `_tiles`（`TileCount` / `TileAt(i)` 的语义没变）；占位卡单独进 `_placeholders`（`PlaceholderCount` / `PlaceholderAt(i)`） |

**机器可判定证据**（`AbilityMatrixScreen.DebugRunPlaceholderFeedback()`，拍摄台里跑的就是玩家点它时那条路）：

```
占位卡交互：Unlock → NoCard ｜ ToggleActive → NoCard ｜ Upgrade → NoCard ｜ ResetUpgrades → NoCard
　｜ 反馈：失败（震动 + 失败音效）4 次 / 成功 0 次（期望 4 / 0）
　｜ 失败音效 last=ui_card_fail 缺失 id=[]（期望 ui_card_fail）｜ 状态：（占位卡） Lv0 占位卡 … 失败=4
```

---

## 6. 界面：`AbilityMatrixScreen`（满屏矩阵）

### 6.1 布局

```
AbilityMatrixScreen (CanvasLayer, layer=16, ScreenLayer=Panel → 打开即暂停 + 放开鼠标)
└── Root（挂 ui_theme，铺满）
    ├── Dim（ColorRect 0.94）
    └── Panel（全屏铺满：左右 48 / 上 32 / 下 96 ← 给按键提示栏留位）
        └── VBox
            ├── Title  「能力矩阵 · 局外装备」
            ├── Divider
            ├── Header  ← 碎片 / **额度 used / cap（扩增 Lv）** / 「扩增额度 +1（费用 N）」按钮
            ├── Matrix  ← GridContainer，**铺满剩余空间**（size_flags = Fill+Expand），列 = `Columns`、行 = `RowsFor(卡数)`
            ├── Legend  ← 六档等级色图例（代码生成，颜色取自 `AbilityCardTile.LevelColor`，不是抄一遍）
            └── Footer  ← 三状态说明 + 解锁/激活/张数 + 矩阵格数与占位卡数
```

- **不写死行列**：`Columns = 6`（导出属性）、`Rows = 3`（**最少**行数）。需求点名过 5×5 / 6×6 / 3×6 / 6×4 这类"满屏矩阵"。
- **永远是一个填满的矩形、不翻页**（用户 A5）：

  ```
  行数 = max(Rows, ceil(卡数 / Columns))          ← AbilityMatrixScreen.RowsFor(卡数, 列, 最少行)
  格数 = Columns × 行数                            ← 一定 ≥ 卡数、且一定能整除列数（最后一行不缺角）
  不足的格子 = 占位卡（§5.6）
  ```

  实测（`DebugSlotPlan`，**不动真表**的纯算式）：`8 张 → 6×3 = 18 格（占位卡 10）`、
  `18 张 → 6×3 = 18 格（占位 0）`、`19 张 → 6×4 = 24 格（占位 5）`、`25 张 → 6×5 = 30 格（占位 5）`、
  `60 张 → 6×10 = 60 格（占位 0）` —— 全部满足「填满矩形、不翻页」。
- **格子尺寸自适应**：场景里**不再有 232×214 这种写死的格子尺寸** ——
  `AbilityCardTile` 的 `custom_minimum_size` 只是"能缩到多小"（120×110），
  `size_flags = Fill+Expand`；`Matrix` 容器也是 Fill+Expand，于是
  **格子尺寸 = 矩阵区 ÷ (列, 行)**，由 `GridContainer` 按当前容器（也就是屏幕）分配。

  实测（1920×1080，`DebugMatrixReport`）：`矩阵区 1772×686 → 每格 284×220px`
  —— 正好是 `(1772 − 5×14) / 6 = 283.7` 与 `(686 − 2×14) / 3 = 219.3`。
  （改窗口/分辨率时矩阵区跟着变，格子跟着变；`project.godot` 是 `canvas_items` + `expand`，
  所以逻辑坐标恒为 1920×1080、整体随窗口缩放。）
- ⚠️ **行数长了以后格子会变矮**（这是"填满矩形"的必然代价）：1080p 下 3 行 = 220px/格、5 行 ≈ 126px/格，
  再往上（6 行 ≈ 103px）卡面里的名字/状态/提示会挤在一起。真加到那个数量时，
  把 `Columns`（导出属性）调大、或把卡面文案收短；**不要**回到"写死格子尺寸"那条路（小窗口会溢出）。
- 卡表行数 / 行数变了才**重建**矩阵（`RebuildIfNeeded`），平时点卡片只 `RefreshAll()` 刷状态 —— 不重建节点、不打断缓动。

### 6.2 键盘 / 提示栏（照 `docs/ui.md` §10 抄）

```csharp
SetKeyHints(
    KeyHint.Mouse(MouseButton.Left, "激活 / 长按解锁"),
    KeyHint.Mouse(MouseButton.Right, "升级 / 长按重置升级"),
    KeyHint.Of(QuotaKey, "扩增额度"),          // 默认 M
    KeyHint.Text("鼠标悬停", "看效果与价格"),
    (Key.Escape, "关闭"));
```

- 打开/关闭键 = 界面自己的 `ToggleKey`（默认 **K**，`[Export]`），在 `_Process` 里做**边沿检测**，
  所以**任何场景**按 K 都能开关它（与 `BuildScreen` 的 C、`SettingsScreen` 的 O 同一套）。
- 暗示栏是**基类**建的那一栏（`OverlayScreen.SetKeyHints`）：结构永远存在、ZIndex 100、背景透明、不吃鼠标。
  实测：`结构在树里=true 条目=5 可见=true ZIndex=100 鼠标穿透=true`。
- ⚠️ **基地界面（`BaseScreen`）不要再处理一次 K**：两边都读同一个物理键会变成"按一下开、同一帧又被关掉"。
  `BaseScreen` 只在**提示栏**里写了「K 能力卡」，处理留给矩阵界面。
- 说明文字**只在鼠标悬停**（`TooltipText`：名字 / 等级 / 状态 / 额度占用 / 下一级效果 / 价格）——
  照需求「只有选取界面和鼠标悬停时显示说明」。

### 6.3 音效（表驱动，**零 C# 加音效**）

| id | 触发点 | 素材 |
|---|---|---|
| `ui_card_unlock` | 左键长按读条完成且资源足够 | 项目自制（`_tools/make_ui_card_sfx.ps1`） |
| `ui_card_upgrade` | 右键升级成功（也用于额度扩增成功） | 项目自制 |
| `ui_card_reset` | 右键长按重置成功 | 项目自制 |
| `ui_card_fail` | **任何**失败（碎片不够 / 额度不够 / 未解锁 / 已满级 / 没得重置） | 项目自制 |
| `ui_click` | 左键激活 / 关闭（复用已有的按钮点击音） | Kenney（CC0，已经在表里） |

- 表行：`Config/Tables/Audio/audio_ui_card_{unlock,upgrade,reset,fail}.tres`，
  已加进 `Balance.tres` 的 `Audio` 数组（**不加进去 = 文件在、表在、就是没声音**，见 `docs/audio.md` §五）。
- 代码里的 id 常量在 `Scripts/AbilityCards/AbilityAudioIds.cs`
  （**没有**加进 `Scripts/Audio/AudioIds.cs` —— 那个文件不在本轮可改范围内；纪律完全一致：真源永远是音效表）。
- 这 4 条是**项目自制**：`_tools/make_ui_card_sfx.ps1` 用数学公式生成 16-bit PCM 单声道 22050Hz WAV，
  改音色 = 改脚本里的频率/时长再跑一次；想换成 Kenney 的现成音效就把同名 `.ogg` 放进去、改表里 `Stream` 一列。

### 6.4 图标

8 张手写 SVG：`Assets/UI/Icons/ic_ui_card_{vitality,damage,speed,crit,reload,magazine,summon,control}.svg`
（项目自制、Godot 原生导入、改一个十六进制就是换色）。
映射在 `UiIcons.ForAbilityCard(card)` → `Named(card.IconName)`：**卡行的 `IconName` 列说了算**，
所以加卡**不需要**在 `UiIcons` 里加 switch 分支；读不到图返回 `null`（只显示文字，不报错）。

---

## 7. 验证：怎么跑、期望看到什么

### 7.1 编译

```powershell
& 'F:\Godot项目\项目a\build.ps1' --no-incremental -v minimal     # 必须 --no-incremental；0 warning / 0 error
```

资源导入（新增了 8 张 SVG + 4 个 WAV 才需要）：

```powershell
& 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe' --headless --path 'F:\Godot项目\项目a' --import
```

> ⚠️ 若同时有别的 Godot 实例开着，MCP 插件会报 `Port 9080 is already in use` 并让 `--import` 以 **exit 1** 结束 ——
> **导入本身是成功的**（看输出里有没有 `reimport | ic_ui_card_*.svg`）。别被退出码骗了。

### 7.2 游戏内自测（状态层 + 生效层的机器判定）

```powershell
$ud = 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike'
'1' | Set-Content "$ud\selftest.flag"
Start-Process 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe' `
  -ArgumentList '--path','F:\Godot项目\项目a','res://Scenes/Main.tscn'
Start-Sleep -Seconds 27
Remove-Item "$ud\selftest.flag"        # 必须删
# 日志：$ud\logs\run_*.log
```

`[SelfTest]` 里与本系统有关的行（`AbilityStore.DebugSelfTestLines()` + `AbilityEffects.DebugPreviewLine()`）：

```
配置表比对·索引：… /局外 6/升级池 3/AI 档案 11/能力卡 8（期望 3/3/10/1/1/25/7/9/9/6/3/11/8）
配置表比对·能力卡：8 张（来源：表 AbilityCards） 额度上限 6 = 基础 6 + 扩增 Lv0 × 1（扩增行 meta_quota 费用 30） …
能力卡·冒烟② 解锁「强化肌体」→ Ok（期望 Ok）｜Lv1（期望 1）｜碎片 970（期望 970）｜解锁费 30
能力卡·冒烟⑤ 升到上限（Lv5/上限 5）再升 → MaxLevel（期望 MaxLevel）｜重置应归还 170 碎片
能力卡·冒烟⑥ 长按右键重置 → Ok（期望 Ok）｜Lv1（期望 1）｜碎片 970（期望 970）
能力卡·冒烟⑧ 左键激活 → Ok（期望 Ok）｜额度 0 → 2/6（期望 +2）｜IsActive=True
能力卡·冒烟⑩ 未解锁「凶戾印记」：升级 Locked / 激活 Locked / 重置 Locked（期望全部 Locked）
能力卡·冒烟⑪ 额度不足（上限 6）：装下 2 张后额度 5/6，再激活「疾风之靴」（要 2 点）→ QuotaFull（期望 QuotaFull）
能力卡·冒烟⑭ 扩增额度（容量扩容）→ Ok（期望 Ok）｜上限 6 → 7（期望 +1）｜费用 30｜扩增 Lv1
能力卡·冒烟⑮ 还原后：…（期望与起点一致；内存钱包剩 970，玩家的 user:// 文件未改动）
能力卡实测（激活真的影响局内）：强化肌体（card_vitality Lv2）MaxHealthAdd +40 → 最大生命 270 → 310（+40）
  ｜ 无人机协议（card_summon Lv1）授予 skl_summon → AbilitySummon.Enabled False → True …
```

> ⚠️ **索引那一行的期望值变了**（这是加表的必然结果，不是回归）：
> `局外 5 → 6`（新增额度扩增行 `meta_quota`）+ 末尾多一项 `能力卡 8`。
> 其余 11 个数字**逐位不变**；`节点布置` / `Pressure 测试` / `端到端按 F` 三条硬指标与改动前**逐字一致**（已 diff 确认）。

### 7.3 界面拍摄台（状态图 + 占位卡反馈 + 交互自检）

```powershell
& $godot --path 'F:\Godot项目\项目a' 'res://Scenes/UI/_AbilityShowcase.tscn'
```

输出 `docs/screenshots/ability_01…05*`、`ability_08_placeholder_feedback.png` 与逐条 `[AbilityShot]` 行。
**全程在沙箱里**（内存钱包 + 不落盘），跑完 `user://ability_cards.cfg` **仍然不存在**。
它用 `DebugBeginCharge` / `DebugReleaseCharge` 走**真的读条与结算**（`_Process` → `RunAction`），
所以读条截图里的进度条与解锁动画都是真表现；占位卡那张也是**真的走了一遍失败分支**（图上能看到它正在抖 + 红闪）。

本轮（A5）新增的 3 行证据（模板输出，实际数字随屏幕分辨率变）：

```
满屏矩阵算式：0 张 → 6×3 = 18 格（占位卡 18）✓ ｜ 8 张 → 6×3 = 18 格（占位卡 10）✓ ｜ 18 张 → 6×3 = 18 格（占位卡 0）✓
  ｜ 19 张 → 6×4 = 24 格（占位卡 5）✓ ｜ 25 张 → 6×5 = 30 格（占位卡 5）✓ ｜ 60 张 → 6×10 = 60 格（占位卡 0）✓
  → 全部满足「填满矩形、不翻页」 = True
逐格：列 6 × 行 3 → 18 格（真卡 8 + 占位卡 10）｜视口 1920×1080 → 矩阵区 1772×686 → 每格 284×220px（自适应…）
占位卡交互：Unlock → NoCard ｜ ToggleActive → NoCard ｜ Upgrade → NoCard ｜ ResetUpgrades → NoCard
  ｜ 反馈：失败（震动 + 失败音效）4 次 / 成功 0 次（期望 4 / 0）｜ 失败音效 last=ui_card_fail 缺失 id=[]
```

### 7.4 端到端：激活的卡真的影响下一局

```powershell
# ① 用 real 模式产生一份真的存档（钱是内存钱包，所以不会花掉玩家的碎片）
& $godot --path 'F:\Godot项目\项目a' 'res://Scenes/UI/_AbilityShowcase.tscn' -- --real
# ② 跑一局自测，读日志
#    [Ability] 能力卡：激活 1/8 张 · 额度 2/6 · 碎片 279 —— 强化肌体 MaxHealthAdd +20
#    [SelfTest] 配置表比对·玩家：生命 290（期望 250 + 元进度加成 20） …
#      ↑ 250（角色行）+ 20（局外成长 health_level=2）+ 20（能力卡）= 290 —— 卡真的算进去了
# ③ ⚠️ 用完必须删掉这份存档，否则"配置表比对·玩家"的期望值不再成立
Remove-Item "$env:APPDATA\Godot\app_userdata\ACT Roguelike\ability_cards.cfg"
```

### 7.5 在**真的 Main.tscn** 里验接线 + 按键

```powershell
& $godot --path 'F:\Godot项目\项目a' 'res://Scenes/UI/_AbilityInGame.tscn'
```

```
[AbilityInGame] UiService.OpenAbilityMatrix() = true（true = Main.tscn 里真的挂着这个界面）
[AbilityInGame] 界面 IsOpen=true 矩阵格数=18（真卡 8 + 占位卡 10） 表行数=8
[AbilityInGame] 提示栏：结构在树里=true 条目=5 可见=true ZIndex=100 鼠标穿透=true
[AbilityInGame] Close() 之后 IsOpen=false（期望 False）
[AbilityInGame] 合成按键 K 之后 IsOpen=true（期望 True —— 玩家按 K 走的就是这条 ToggleKey 路）
```

（它用 `Input.parse_input_event` 合成**真的按键事件**，与 `SelfTest` 合成按键是同一套；
直接调 `Open()` 是证明不了 `ToggleKey` 这条路的。）

---

## 8. 加一张卡：完整步骤（**只动 2 个文件**）

1. **（可选）加图标**：写一个 `Assets/UI/Icons/ic_ui_card_<你的名字>.svg`（照现有的抄，64×64 viewBox）。
2. **加一行卡**：新建 `Config/Tables/AbilityCards/card_<你的id>.tres`：

   ```
   [gd_resource type="Resource" script_class="AbilityCardConfig" format=3]

   [ext_resource type="Script" path="res://Scripts/Config/AbilityCardConfig.cs" id="1_row"]
   # 只有 GrantSkill 型的卡才需要这一行：
   [ext_resource type="Resource" path="res://Config/Tables/Skills/skl_xxx.tres" id="2_skill"]

   [resource]
   script = ExtResource("1_row")
   IconName = "ic_ui_card_<你的名字>"
   QuotaCost = 2
   MaxLevel = 5
   UnlockCost = 30
   UpgradeBaseCost = 20
   UpgradeCostGrowth = 15
   Effect = 0                      # 0=StatAdd 1=StatMultiply 2=GrantSkill
   Stat = "MaxHealthAdd"           # StatAdd/StatMultiply 才有意义
   PerLevel = 20.0
   Id = "card_<你的id>"
   Slot = 9                        # 只影响"表里的顺序感"，矩阵顺序由 Balance.tres 的数组说了算
   DisplayName = "中文名"
   DescriptionTemplate = "一句话说明（悬停时显示）"
   Note = "为什么这么配 / 数据来源"
   ```
3. **挂进索引**：`Config/Balance.tres` 顶部加一条
   `[ext_resource type="Resource" path="res://Config/Tables/AbilityCards/card_<你的id>.tres" id="1xx_card_xxx"]`，
   并在 `AbilityCards = [ ... ]` 里**按你想在矩阵里出现的顺序**追加 `ExtResource("1xx_card_xxx")`。
4. **（可选）导入资源**：`Godot --headless --path <项目> --import`（新加了 SVG 才需要）。
5. **（可选）更新自测期望**：`Scripts/Testing/SelfTest.cs` 的 `配置表比对·索引` 里 `能力卡 8` → 新数字
   （`…/能力卡 9（期望 3/3/10/1/1/25/7/9/9/6/3/11/9）`）。**只改这一个数字**。

**不需要改的**：`AbilityCardTile.cs`、`AbilityMatrixScreen.cs`、`AbilityStore.cs`、`AbilityEffects.cs`、
任何 `.tscn`、任何别的表。矩阵会自动多一格（**占位卡自动少一张**），图标按 `IconName` 自动找；
如果加满到超过 `列 × 最少行`，矩阵会**自己多长一行**（永远填满、不翻页）。

**加一种新效果**（第三步，才需要 C#）：
`AbilityCardEffectKind` 加一个 enum 值 → `AbilityEffects.ApplyCore` 加一个 `case` → 加一行卡。

---

## 9. 坑与红线（都是本轮实测）

1. **不要动 `Config/Tables/Skills/` 的行内容**：那是局内的单位技能表（25 行 = 5 玩家 + 16 单位 + 4 首领行为），
   本系统只**引用**它（`AbilityCardConfig.Skill`）。混进去会同时破坏"两层分开"与"表只加不改"两条纪律。
2. **`Balance.tres` 的数组不能只加文件不加项**：行文件"只放在目录里"是**不会生效**的
   （索引只认 `Balance.tres` 的 `AbilityCards` 数组）；反过来说，**删/改被引用的行文件会让整份 `Balance.tres`
   加载失败**、所有数值静默回退 —— 要下线一行就把数组项与 `[ext_resource]` **一起**删。
3. **额度扩增等级不在 `meta.cfg`**：它在 `user://ability_cards.cfg` 的 `[quota] quota_level`。
   `meta_quota` 行只提供"每级几点 / 每级多少钱"。
4. **`BaseScreen` 不要也处理 K**：两边都读同一个物理键 → "按一下开、同一帧又被关掉"。
   提示栏写一条、处理留给矩阵界面。
5. **`OverlayScreen` 有 0.3 秒防误触**（`InputBlocked`）：探针里 `Close()` 调用得太早会被挡掉（实测踩过）。
6. **`await get_tree().process_frame` 数帧 ≠ 数时间**：本机 120fps，30 帧只有 0.25 秒，
   0.7 秒的读条**永远读不完**。拍摄台里一律用 `create_timer(秒, true)`（`process_always = true` 才能穿透暂停）。
7. **GDScript 侧读 C# 的自定义类型**：`AbilityStore` 是**静态类**，GDScript 调不到；
   要用的入口都在 `AbilityMatrixScreen` 上开了实例方法封装（`DebugBeginSandbox` / `DebugSummary` / …），
   返回值一律用 Variant 兼容类型（string / int / bool / Node），见 `docs/architecture.md` 第 6 条。
8. **GDScript 的 `:=` 不能接 `.call(...)` 的返回值**：本项目把 GDScript 警告当错误，
   `var x := node.call(...)` 会因"从 Variant 推导类型"**直接解析失败**，整个拍摄台起不来（实测栽过一次）。
9. **音频表要一起加**：新音效的 `.tres` 不加进 `Balance.tres` 的 `Audio` 数组 = "文件在、表在、就是没声音"。
10. **不要用 `stop_project` / 不要在别的代理跑自测时用 MCP 的 `run_project`**：见 `docs/todo-handoff.md` 末尾。
11. **占位卡不是空框，也别把格子尺寸写死回去**（A5 的两条红线）：
    - 占位卡是**真的 `AbilityCardTile`**（`Card == null`）—— 谁在 `_GuiInput` / `RunAction` 里加一句
      `if (Card == null) return;`，占位卡立刻变成"死格子"（用户明确不要这个）；
      **只有宿主**（`AbilityMatrixScreen.Request`）该对 `Card == null` 返回 `NoCard`。
    - `AbilityCardTile.tscn` 的 `custom_minimum_size` 只是"能缩到多小"，**矩阵靠 Expand 铺满**；
      把它改回 `(232, 214)` 之类的大固定值，格子尺寸就不再随屏幕走了（而且小窗口会溢出）。
12. **占用额度的记账与占位卡无关**：`AbilityStore.QuotaUsed` 只扫**卡表行**，占位卡进不了任何统计
    （`UnlockedCount` / `ActiveCount` / `QuotaUsed` 都不会被它影响）。
