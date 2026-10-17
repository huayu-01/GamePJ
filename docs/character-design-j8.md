# 原始设计纠偏J8：发散／心眼（标记来源过滤 + 按命中前层数加伤）

2026-10-17 执行交接单「下一步顺序」第 2 条。**只加了一个新 handler／一条新表列／一条攻击者侧查询口**，
`Weapon.cs`、7 个共用 `Ability` 子类、敌人·首领·召唤物的数据行与技能行**一个字都没动**；
`ConfigIndex.tres` **没有改**（两条行本来就在表里，本批不增删行）。
Debug 与 ExportRelease 均 0 警告／0 错误；Main 自测退出码 0、stderr 0 字节；8 条硬指标与改前逐字一致；
四存档 SHA256 跑前＝跑后＝交接点；`selftest.flag` 不存在。[完整输出](character-design-j8-evidence.log)。

## 病根（改前）

两条表行的 `ApplyHandler` 都是 **`BurnOnHit`**，参数只有 `StatusId = Mark` + `Duration = 6`：

```
evo_divergence.tres   ApplyHandler = "BurnOnHit"    →  武技1/武技2/后撤/任何命中 都挂标记
evo_mindseye.tres     ApplyHandler = "BurnOnHit"    →  同上，且**完全没有** +50% 那一段
```

`BurnOnHitRule.Matches` 的判据只有三条：

```csharp
Kind == GameplayEventKind.Hit && Source == _owner && Target != null && Target != _owner
```

**这就是"来源过滤尚不完整"**：判据里没有"这一击是哪个技能"，而 `GameplayEvent.Tag`（= `HitInstance.Attack.Ability`）
本来就在事件里 —— 只是没人读。后果是 6 把武器、所有角色技能、连环境 DoT 之外的任何命中都会给目标挂「标记」，
于是「心眼」拿到的标记可能来自后撤、灵剑、能量掌武技 2……；而"按标记加伤"那一半**代码里根本不存在**
（`Unit.ReceiveHit` 只看 `hit.Damage`，没有任何地方按目标状态改伤害）。

## 改成了什么

### ① 一条**攻击者侧**的伤害查询口（新的唯一伤害读点）

| 文件 | 加了什么 |
|---|---|
| `Scripts/Combat/IOutgoingDamageScale.cs`（**新**） | 接口 `float ScaleAgainst(Unit target, AttackInstance attack)` |
| `Scripts/Combat/Unit.cs` | `_outgoingDamageScales` 列表 + `AddOutgoingDamageScale` / `RemoveOutgoingDamageScale` / **`OutgoingDamageScaleAgainst`**（没有登记时恒为 1） |
| `Scripts/Combat/AttackInstance.cs` | `RollHit` 里**在生成 `HitInstance` 之前**问一次攻击者：`Damage = BaseDamage × 暴击 × marked` |

**为什么挂在攻击者身上而不是目标身上**：增幅是**玩家**拿到的，规则节点也挂在玩家下 ——
于是敌人／召唤物／其他单位的攻击天然问不到玩家的规则（"其他单位不吃加成"不是一条特例判断，而是归属的自然结果）。

### ② 一个 handler 服务两条行（差别全在数据里）

`Scripts/Rewards/MarkedBonusRule.cs`（**新**）+ `RewardEffectRegistry` 的 `["MarkedBonus"]`：

```csharp
["MarkedBonus"] = row => context => context.Player.AddChild(new MarkedBonusRule
{
    StatusId        = ParamText(row, "StatusId", "Mark"),
    Duration        = Param(row, "Duration", 6f),
    MarkAbilityId   = row.RequiredEventAbility?.Id ?? string.Empty,   // 哪个技能才加标
    BonusAbilityId  = row.BonusAgainstAbility?.Id ?? string.Empty,    // 加伤只算哪些技能（空 = 各种伤害）
    FlatBonus       = Param(row, "FlatBonus", 0f),                    // 心眼 0.5
    PerStackBonus   = Param(row, "PerStackBonus", 0f),                // 发散 0.1
});
```

- **加标**：订阅 `Hit` 事件，判据 = 来源是自己 + 目标是别人 + **`Tag == MarkAbilityId`**（空 = 不限，那是旧行为）。
- **加伤**：实现 `IOutgoingDamageScale`，`1 + FlatBonus + PerStackBonus × 目标层数`（层数读 `Unit.GetStatusStacks`）。
- 层数不存在规则里，存在**目标自己身上**（`eff_mark.MaxStacks = 5`），所以多目标各记各的、到期自动清。

### ③ 一条**强类型资源列**（不是字符串反射）

`Scripts/Config/RewardConfig.cs` 新增：

```csharp
[Export] public AbilityConfig? BonusAgainstAbility { get; set; }
```

与既有的 `RequiredEventAbility`（J5 立下的资源引用列）分工：前者管"哪一击才加标"，这条管"加伤只算哪些技能"。
**两条都是 `AbilityConfig` 资源引用**，`.tres` 里是 `ExtResource`。本批**没有**新增任何 `Godot.Get/Set` 字符串反射。

### ④ 两条表行的数据

| 行 | `RequiredEventAbility`（加标来源） | `BonusAgainstAbility`（加伤来源） | 加成 |
|---|---|---|---|
| 「发散」`evo_divergence` | `skl_a1_w1_1`（**只有「波波」**） | `skl_a1_w1_1`（**只提高「波波」**） | 每层 `+0.1`（**预设 N**，见下） |
| 「心眼」`evo_mindseye` | `skl_a3_w1_1`（**只有箭**：弓武技1；这张行是 `<A3限定>`，弓的两个武技里只有它会射出箭） | **留空 = 玩家的各种伤害** | 固定 `+0.5`（文档给的就是 50） |

#### ⚠️ 发散的 N：文档没写 → 预设 **10%/层**

原文是 `每层标记额外提升技能1的伤害{N}%`，N 未定。表里填 `PerStackBonus = 0.1`，
表行的 `Note` 与 `ParamEntry.Note` 两处都写明"**文档为 N，此处是预设**"。
层数上限来自效果表 `eff_mark.MaxStacks = 5` → **满层 +50%**。要改就改 `HandlerParams` 里那一项，不动 C#。

## ⭐ "命中前已有标记"的证据（用户 2026-10-17 裁定）

裁定原话：「**按本次命中前已有标记结算，第一次加标这次不加伤**」。

实现顺序（这也是为什么不放在 `Unit.ReceiveHit` 里）：
`Projectile/AttackHitbox → AttackInstance.RollHit`（**这里读层数**）→ `Unit.ReceiveHit`
→ 抛 `Hit` 事件 → `MarkedBonusRule.Execute`（**这里才加标**）。读层数发生在加标之前，所以这一击的标记不可能被算进来。

**同一目标连打的伤害读数**（`Chaser` 靶子，`Unit.Damaged` 的 `FinalDamage`；这一段三次都不是暴击）：

| 第几击 | 命中前层数 | 伤害读数 | 攻击快照 | 实际倍率 | 命中后层数 |
|---|---|---|---|---|---|
| 发散 第 1 击 | 0 | **10** | 10 | **×1** | 1 |
| 发散 第 2 击 | 1 | **11** | 10 | **×1.1** | 2 |
| 发散 第 3 击 | 2 | **12** | 10 | **×1.2** | 3 |
| 心眼 第 1 击 | 0 | 54 | 24（×暴击 2.25） | **×1** | 1 |
| 心眼 第 2 击 | 1 | **36** | 24 | **×1.5** | 2 |

> 判据是"**倍率**"而不是绝对伤害：`倍率 = 结算伤害 ÷（攻击快照 × 暴击倍率）`，
> 暴击是随机量（元进度 `CritMultiplierAdd` + 箭自带 5%），按这一条命中记录到的暴击折算，
> 所以暴击不会把这条判据搞脆。心眼的第 1 击正好是暴击（54 = 24 × 2.25），折算后仍是 ×1。

**这条证据是有牙的**：如果顺序反了（先加标再读层数），第 1 击会读出 ×1.1 / ×1.5，这两行立刻变 🔴。

## ⭐ 边界覆盖（逐项给证据）

| 边界 | 判据 | 本次读数 |
|---|---|---|
| **其他技能** | 发散只由「波波」加标、只提高「波波」 | 能量掌**武技2**（`skl_a1_w1_2`）打已带 1 层标记的靶子 → 伤害 ×1、层数 **1 → 1**（不加标也不加伤） |
| **其他技能** | 心眼只由**箭**加标 | 能量掌武技2 打**没有**标记的目标 → 层数 **0 → 0** |
| **其他技能** | 用户点名"不提高后撤" | 真实按 `skill_1`（后撤）→ 层数 **1 → 1**、新增命中 **0** 次 |
| **其他单位** | 非玩家攻击者的伤害不吃心眼 | 靶子 C 带 2 层标记，由**另一个单位**（`J8OtherUnit`）打 20 点 → 实际结算 **20**（×1，不是 ×1.5）；C 的层数仍是 2（敌方命中也**不加标**） |
| **多个目标** | 各记各的层数 | A 已 3 层、B 0 层 → 打 B 得 ×1（用 B 自己的 0 层）；再打 A（A 3 层 / B 1 层）得 **×1.3** |
| **层数** | `Mark` 有 `MaxStacks` | `eff_mark.MaxStacks = 5`：第 5 层时读数 **×1.5**，再打一发仍是 5 层、仍是 ×1.5 |
| **过期** | 到期后加成消失 | `Unit._PhysicsProcess(6.1s)` 走真实状态倒计时 → 层数 5 → **0** → 这一发 **×1** |
| **两条同时拥有** | 规则按乘法叠加（不是二选一） | 同一位玩家：`发散 1.1 × 心眼 1.5 = ×1.65`，实测 **×1.65** |
| **"各种伤害"** | 心眼不是只对箭生效 | 能量掌**武技2**（不是箭）打带 2 层标记的目标 → **×1.5** |

## 走的是真实链路（没有直接 `Execute` 冒充）

| 环节 | 怎么走的 |
|---|---|
| **奖励获取** | ① 先用**真实候选池**确认两张行在对应池里（`RewardPool.Initialize(weapon, "chr_hunter")` + `Roll`）；② 再用 **`RunController.GrantReward(id)`**（生产代码里"保底/脚本奖励"那条公开入口）→ `RunBuild.Add(definition, new RewardContext{真实 Player/Weapon/Build})` → 表里的 `MarkedBonus` handler → 真实规则节点 |
| **命中** | 真实按键（`Input.ParseInputEvent(InputEventAction)`，与 `ChargeReleaseProbe` 同一套）→ `PlayerAbilityInput` → `Ability.TryActivate` → 真实技能动画的 **`ATK` 事件帧** → `EnergyWaveAbility.FireShot` / `ArrowVolleyAbility` 真发射 → `Projectile` 每物理帧射线 → `Unit.ReceiveHit`；读数来自**真实的 `Unit.Damaged` 事件**（`FinalDamage` / `HealthLost`），不是手搓 `HitInstance` |
| 唯一的例外 | "其他单位"那一条用 `new AttackInstance{ Attacker = 敌方靶子 }` 走**同一条** `RollHit → ReceiveHit`（日志里写明了）；它不可能触发"真实敌人的 AI 出手"，因为探针关掉了 Brain |

## 验收

- `build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**；`dotnet build -c ExportRelease --no-incremental` → **0 warning / 0 error**。
- Main：`--headless --quit-after 40000 res://Scenes/Main.tscn -- --selftest` → **退出码 0**，耗时 68.1s，**stderr 0 字节**，日志到 `=== 自测结束 ===`。
- 8 硬指标逐字一致：配置计数（**`（ConfigIndex.tres 已加载）`** 在）· 10 节点 2/5/3 · 猎手 **220/20/6.5** · 索引 `6/2/10/1/1/39/7/14/8/5/6/11/4` · Pressure **3/3/0** · 白板 boss **315/0** · 奖励 UI **True/Paused True** · 最小链路 **1 动画事件→1 弹体→命中**。
- 红色行仍是改前就有的 **5 条**历史读数标记（内容写的是"期望=实际"），**没有新增红灯**。
- **独立回归**（与 Main 无关的另一套真实输入探针）：`_ChargeReleaseProbe.tscn --headless` → 退出码 0、stderr 0 字节、
  **`通过 80，失败 0`**（与 J6/J7 基线逐字一致）—— 说明 `AttackInstance.RollHit` 那一行改动在"没有规则登记"时确实逐位不变。
- 四存档 SHA256 与交接点逐字相同（跑前＝跑后）；`selftest.flag` 跑前不存在、跑后不存在。
- 本批**没有**新增/删除表行 → `ConfigIndex.tres` 不需要三处同改，索引与 `load_steps` 逐位不变。

## 边界 / 不确定处

1. **发散的 N 是预设值 10%/层**（文档未定）。满层（5 层）+50%。
2. **心眼的"箭"= 弓武技1 `skl_a3_w1_1`**（资源引用）。这张行是 `<A3限定>` = 弓，而弓的两个武技里只有武技1 会射出箭
   （弓行 `UsesWeaponComponent = false`，没有普通射击）。若将来加更多射箭技能/武器，**扩表列**即可，不改 C#；
   届时若要"任何箭类武器都算"，应改成一个"攻击标签列"（`Godot.Collections.Array<string>`，architecture.md 第 12 条），
   本批选择资源引用是因为当前只有一条来源、且它本来就是强类型列。
3. **层数不区分施加者**：目标身上的「标记」是一个状态，谁加的层数都算 ——
   这与文档"你对被标记的敌人造成的伤害提高"一致，但意味着"敌人被打上标记"与"谁打上"无关。
4. **DoT / `ApplyDirectDamage` 不吃加成**：它们不走 `AttackInstance.RollHit`（没有攻击快照），本批未接，也没必要接
   （"你的伤害"指你打出去的那一击）。燃烧类 DoT 的归属本来就没有统一来源。
5. `MarkedBonusRule` 继承 `TriggerRule`（`RunBuild` 用它登记/驱动冷却），`RewardCatalog` 里那条"一个类服务所有同类行"的纪律继续成立 ——
   本批加的是**一个**类、服务**两条**行。
6. 本批**没有截图**（证据是文本读数 + 存档哈希；与 J7 同一条口径）。真机（非无头）观感留给人手 MCP 复核。
7. `Scripts/Rewards/MarkedBonusRule.cs.uid` 与 `Scripts/Combat/IOutgoingDamageScale.cs.uid` 未入库
   （与工作区里另外 6 个未跟踪 `.uid` 同一批；这两个脚本都没有被任何 `.tscn` 引用，`.uid` 不参与运行）。
