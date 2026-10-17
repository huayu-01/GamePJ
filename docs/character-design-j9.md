# 原始设计纠偏J9：龙之箭（蓄力阈值 → 更大 + 全穿透，按松手冻结、真实释放 ATK 发射）

2026-10-17 执行交接单「下一步顺序」第 3 条。**加了一条强类型资源列、一个 handler、一个技能类的三个字段**，
`Weapon.cs`、7 个共用 `Ability` 子类、敌人·首领·召唤物的数据行与技能行**一个字都没动**；
`ConfigIndex.tres` **没有改**（本批不增删行）。Debug 与 ExportRelease 均 0 警告／0 错误；
专项探针 **42/0**、Main 自测退出码 0、stderr 0 字节；8 条硬指标与改前逐字一致；四存档 SHA256 跑前＝跑后＝交接点；
`selftest.flag` 不存在。[完整输出](character-design-j9-evidence.log)。

## 病根（改前）

`evo_dragon_arrow.tres` 的 `ApplyHandler` 是 **`AbilityOverride`**，参数只有两条：

```
AbilityId         = "skl_a3_w1_1"    →  改哪条技能行
ChargeMaxSeconds  = 3.0              →  蓄力上限 1s → 3s
```

也就是原文（第 94 行）「武技1现在可以蓄力3秒,**且**蓄力2秒后射出的箭会更大且附带全穿透效果」**只接了前半**；
「更大 + 全穿透」在代码里根本不存在 —— 而它**不是纯数据能表达的**：需要在"松手那一刻"按累计蓄力时长切换弹型
（阈值 / 尺寸 / 穿透数三项一起变），这是**新行为**，而且必须**由真实释放 ATK 发射**（J6 立的规矩）。

### ⚠️ 交接单点名的歧义：`ChargeMaxSeconds = 3` 会让"每 0.1 秒加档"一直加到 3 秒

档数上限的公式在 `Ability.ChargeMaxStepsEffective`（`Ability.cs:292-309`）：

```csharp
steps = min(ChargeMaxSteps, floor((ChargeMaxSeconds - ChargeBaseSeconds) / ChargeStepSeconds))
```

弓武技1 的表值是 `ChargeBaseSeconds = 0.5` / `ChargeStepSeconds = 0.1` / `ChargeStepScale = 1.3`，
所以把上限从 1 秒改成 3 秒 **= 档数从 5 档变成 25 档**：

| 松手时刻 | 档数 | 伤害倍率 | 单发伤害（表内 24） | 弹速（220 + 30×档） |
|---|---|---|---|---|
| 1.0s（改前上限） | 5 | ×2.5 | 60 | 370 |
| **2.0s（大箭阈值）** | **15** | **×5.5** | **132** | **670** |
| 3.0s（改后上限） | 25 | ×8.5 | 204 | 970 |

**所以交接单那句担心是真的**：档位并没有在 2 秒停住，它一直加到 3 秒。探针把这条事实打成一条 INFO 读数
（`[DragonArrow] INFO 口径…`），不是靠文档口头解释。

## 我的判断与建议的解释（推荐 A，本批按 A 落地）

原文是两件并列的事：①「现在**可以蓄力 3 秒**」；②「蓄力 **2 秒**后射出的箭更大 + 全穿透」。

- **A（本批默认，已落地）**：3 秒是"**能按多久**"的新上限，档位照旧每 0.1 秒加一档直到 3 秒；
  2 秒是**叠加的离散门槛** —— 松手时蓄到 ≥2 秒，这一发才变成"大箭"。
  理由：若档位在 2 秒就封顶，那"可以蓄力 3 秒"多出来的 1 秒**什么都不发生**，与原文表述直接冲突；
  而基础弓本来就是"蓄越久越强"（每 0.1 秒 ×1.3），把上限抬到 3 秒就是自然延续。
  代价：满蓄 ×8.5（24 → 204 单发），是"要按住 3 秒"换来的。
- **B（若认为 ×8.5 过大）**：档位在 2 秒封顶（`ChargeMaxSteps = 15`），3 秒只是"能多按一会儿"。
  这一档**不需要改 C#**：把 `evo_dragon_arrow.tres` 的 `ChargedShot.MaxSteps` 从 `0` 改成 `15` 即可
  （表里已经预留了这一列，默认 0 = 不限）。改完探针的 INFO 行会自己变成"2 秒后不再加档"。

请用户裁定 A 或 B。默认按 A 已交付。

## 改成了什么

### ① 一条**强类型资源列**（不是字符串反射）

`Scripts/Config/ChargedShotTuning.cs`（**新**）+ `RewardConfig.cs` 加一列：

```csharp
[Export] public ChargedShotTuning? ChargedShot { get; set; }
```

| 字段 | 含义 | 龙之箭这一行 |
|---|---|---|
| `TargetAbility` | 改哪条技能行（**资源引用**） | `skl_a3_w1_1` |
| `MaxChargeSeconds` | 蓄力总时长上限（≤0 = 不动技能自己的值） | `3`（原文「可以蓄力3秒」） |
| `MaxSteps` | 档数上限（0 = 不限） | `0`（= 解释 A；填 15 = 解释 B） |
| `ThresholdSeconds` | 松手蓄力 ≥ 它才算大箭（≤0 = 整条逻辑不启用） | `2` |
| `SizeScale` | 大箭的投射物整体缩放 | `2`（弓的 `ProjectileSizeScale` 是 0 = 1 倍） |
| `PierceCount` | 大箭**额外**可穿透的目标数 | `99`（实际意义的全穿透） |

与 J6 的 `VolleyRampTuning`、J8 的 `BonusAgainstAbility` 同一条纪律：**新参数一律强类型资源／列**。

### ② 一个 handler，走**公开强类型方法**

`RewardEffectRegistry` 加 `["ChargedShot"]`：`Ability.FindByAbilityId(玩家, TargetAbility.Id)` 拿到
`ArrowVolleyAbility`，再调它**新加的公开方法** `ConfigureChargedShot(maxChargeSeconds, maxSteps, thresholdSeconds, sizeScale, pierce)`。
**没有 `Godot.Set("属性名", 值)` 那种字符串反射**（`architecture.md` 第 12 条）。

### ③ `ArrowVolleyAbility` 里的判据：**松手快照**

| 加了什么 | 位置 | 作用 |
|---|---|---|
| `ChargedShotThresholdSeconds` / `ChargedShotSizeScale` / `ChargedShotPierce` | 新 `[ExportGroup]` | 表值落地（不配 = 0） |
| `_releasedChargeSeconds`（私有） | `OnChargeReleased` **写一次** | **松手那一刻的蓄力秒数快照** |
| `chargedShot` 判定 + `shotPierce` / `shotSize` | `FireVolley` | 按快照决定这一发是不是大箭 |
| `LastShotChargedShot` / `LastShotChargeSeconds` | 只读状态 | 自测 / 排障读数 |

- **为什么判据是快照而不是"发射那一刻读 `ChargeTime`"**：释放段要**重播同一段动画**、等它自己的
  **真实 `ATK` 事件帧**才发射（J6 的契约），中间隔一段时间。把"算不算大箭"冻在松手那一刻，
  才不会随等待时间漂移。
- **发射入口一个都没新增**：`FireVolley` 仍然只从 `OnAnimEvent("ATK")`（且 `_releasePending` 为真）进来，
  蓄力期间越过起手 `ATK` 依旧不发射。**没有给生产能力加"计时出弹"兜底。**
- **不配这条增幅时逐位不变**：`ChargedShotThresholdSeconds` 默认 `0` → `chargedShot` 必为 `false`
  → `shotPierce` / `shotSize` 逐字退化成改动前那两个表达式。

### ④ 表行数据

```
ApplyHandler = "ChargedShot"
ChargedShot  = ChargedShotTuning{ TargetAbility=skl_a3_w1_1, MaxChargeSeconds=3, MaxSteps=0,
                                  ThresholdSeconds=2, SizeScale=2, PierceCount=99 }
```

`DescriptionTemplate` 改成「武技1 可以蓄力 3 秒；蓄力 2 秒后射出的箭更大，且附带全穿透。」，
`Note` 里逐项写明四个数字、A/B 口径与"改法不用碰 C#"。

## 走的是真实链路（没有直接 `Execute` / 没有手搓投射物）

| 环节 | 怎么走的 |
|---|---|
| **表 → 机制** | `ConfigRows.ById<RewardConfig>("evo_dragon_arrow")` 读行 → `RewardCatalog.Find` 拿到编译后的定义（handler 已注册）→ **`RunBuild.Add`**（生产里 `RunController.GrantReward` / 抽中后入构筑的**同一条路**）→ 真实 handler → 技能节点 |
| **输入** | 真实按键（`Input.ParseInputEvent(InputEventAction)`）→ `PlayerAbilityInput` → `Ability.OnInputHeld` / `OnInputReleased`（**每帧真实 delta**） |
| **发射** | 松手 → 释放段动画重播 → 该轮**真实 `ATK` 事件帧** → `FireVolley`（本批没有第二发射入口） |
| **命中** | `Projectile` 每物理帧射线 → `Unit.ReceiveHit` → 读数来自**真实的 `Unit.Damaged`**（`FinalDamage`）与**真实 `Projectile` 的 `Scale` / `Pierce`** |

## ⭐ "全穿透"的证据：一箭穿过 3 个靶

三个靶都在同一条直线上（x=20），**间距 18m**；2.05s 松手的大箭弹速 = 220 + 30×15 = **670 m/s**
→ 一个物理帧飞 **11.17m**，所以每个靶必然落在**不同帧**的扫掠里（这也是穿透的真实语义）。

| 命中 # | 靶 | 位置 z | 命中物理帧 | `FinalDamage` | 攻击快照 `BaseDamage` | 备注 |
|---|---|---|---|---|---|---|
| 1 | T1 | 11.4（距枪口 8m） | 390 | **132** | 132 | 大箭第 1 个目标 |
| 2 | T2 | −6.6（与 T1 隔 18m） | 392 | **264** | 132 | 这一下**暴击**（×2 = 264） |
| 3 | T3 | −24.6（与 T2 隔 18m） | 393 | **132** | 132 | 第 3 个目标，箭仍未消失 |

- **同一次射击只生成 1 个投射物**，三次命中**是同一个 `AttackInstance` 实例**（`ReferenceEquals` 断言，
  并且它的 `Pierce == 99`、`Ability == "skl_a3_w1_1"`）—— 所以这是"**一箭**穿三个"，不是三箭。
- 穿透预算实测 = `attack.Pierce` = **99**（`_spawnPierce` 读的是真实 `Projectile.Pierce`）。
- 第三个靶之后箭**还活着**（`MaxLifetime = 3s`，它没有在最后一个靶上消失）。

**对照（阈值没到就不是大箭）**：同样三个靶、同样朝向，
0.6s 松手 → 只中 T1（31.2 = 24×1.3，1 档）、1.9s 松手 → 只中 T1（124.8 = 24×5.2，14 档）；
**1.9s < 2s 所以还是一发小箭**，证明判据是表里的 2 秒，不是"超过改前 1 秒上限就算"。

## ⭐ "不穿 World"的证据：撞墙停下、墙后靶一次没被打到

场景：World 层静态墙（12×5×1 的 Box，层位 `CombatLayers.World = 1`）摆在 z∈[7.5, 8.5]；
墙**前**放一个靶（z=14），墙**后**放一个靶（z=2）；同样一发 2.05s 的大箭（穿透预算 99）。

| 读数 | 值 |
|---|---|
| 命中顺序 | `W_Front`（就 1 个） |
| 墙前靶伤害 | **132** |
| 墙**后**靶生命 | **100000 / 100000**（一点都没掉） |
| 箭的最后位置 | **(20, 1.017, 8.2339)** = **在墙体内**（墙 z∈[7.5,8.5]，墙后靶 z=2） |
| 箭撞墙后 | 已消失（`IsInstanceValid=false`），场上存活投射物 **0** |

原因（**既有行为，本批没改** `Projectile.cs`）：`Projectile.ResolveImpact` 只有在命中**敌对 `Unit`** 时才消耗穿透预算；
撞到墙/地形这类非单位碰撞体直接 `QueueFree()`。所以"全穿透"只对单位生效，**不穿过地形**。

## ⭐ "按松手结果冻结"的证据

| 读数 | 值 |
|---|---|
| ① 0.6s 松手 | 判据（`LastShotChargeSeconds`）= **0.608s**（= 松手后冻结的 `ChargeTime`），小箭 |
| ② 1.9s 松手 | 判据 = **1.91s**，小箭 |
| ③ 2.05s 松手 | 判据 = **2.06s**，大箭 |
| ④ 紧接 ③ 之后 0.6s 松手 | 判据 = **0.607s**，**又回到小箭**（×1 / 穿透 0 / 只中 T1 31.2） |

④ 是这条纪律的"有牙"证据：判定**不锁存**在技能节点上 —— 不是"过了 2 秒就永久变大"，
每一发都按**它自己松手那一刻**的蓄力时长决定。另外每一发都验了
「松手后仍等释放段 `ATK`，不立即出弹」→ 「真实释放 `ATK` 才发射」。

## 验收

- `build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**；`dotnet build -c ExportRelease --no-incremental` → **0 warning / 0 error**。
- 专项探针 `Scenes/Debug/_DragonArrowProbe.tscn`（新）headless → **退出码 0**、**stderr 0 字节**、**通过 42，失败 0**。
- 独立回归 `Scenes/Debug/_ChargeReleaseProbe.tscn`（与 Main 无关的另一套真实输入探针）→ 退出码 0、stderr 0 字节、
  **通过 80，失败 0** —— 与 J6/J7/J8 基线**逐字一致**，证明"没拿龙之箭时"的蓄力/发射行为逐位没变。
- Main：`--headless --quit-after 40000 res://Scenes/Main.tscn -- --selftest` → **退出码 0**，68.9s，
  **stderr 0 字节**，日志到 `=== 自测结束 ===`。
- 8 硬指标逐字一致：配置计数（`（ConfigIndex.tres 已加载）` 在）· 10 节点 2/5/3 · 猎手 **220/20/6.5** ·
  索引 `6/2/10/1/1/39/7/14/8/5/6/11/4` · Pressure **3/3/0** · 白板 boss **315/0** · 奖励 UI **True/Paused True** ·
  最小链路 **1 动画事件→1 弹体→命中**。
- 红色行仍是改前就有的 **5 条**历史读数标记（内容写的是"期望=实际"），**没有新增红灯**；
  P5b 那条「表参数落到机制上」仍 ✅（`变奏 + 龙之箭 → 弓武技1 … 蓄力上限 3s（期望 3）`）。
- 四存档 SHA256 与交接点逐字相同（跑前＝跑后）；`selftest.flag` 跑前不存在、跑后不存在。
- 本批**没有**新增/删除表行 → `ConfigIndex.tres` 不需要三处同改，索引与 `load_steps` 逐位不变。
- Godot 编辑器未被关闭（`.gdmcp/bin/gdmcp doctor` → `editor_connected: true`）；没有 `stop_project`，
  也没有杀别人的进程（只等自己启动的 PID 自行退出）。

## 首轮探针失败记录（探针缺陷，不是产品缺陷）

第一版探针 **32/0 通过、10 条失败**，全部是同一个窗口缺陷：上一发的箭在**下一段**才结算
（大箭 670 m/s 到第 2 个靶要好几帧），而我在"技能收招"就关窗口，于是
①/②/④ 的命中列表互相串（② 里读到 ① 的 31.2、③ 里只看到 T1、④ 里读到 ③ 的 T2/T3）。
机制侧当时的读数就是对的（尺寸 ×2 / 穿透 99 / 撞墙停下 / 墙后 0 伤害全绿）。加 `Settle`（等本发结算完或投射物消失）
之后 **42/0**。改的是探针的取样时机，**没有为了变绿改动任何生产代码**。

## 边界 / 不确定处

1. **3 秒上限 vs 2 秒阈值**：本批按**解释 A**（继续加档到 3 秒，2 秒只是大箭门槛）落地；切 **B** 只要表里
   `MaxSteps = 15`（不改 C#）。⚠️ 请用户裁定。
2. **`PierceCount = 99` 是"实际意义的全穿透"**，不是数学上的无限：同屏目标数远小于 99。
   若将来要真的"不限"，应加一个布尔列 `PierceAll`（本批没加，避免为一个用不上的分支加列）。
3. **⚠️ 既有穿透预算被算两遍**（本批**没改**，改了会动到既有箭/其他武器的行为）：
   `ArrowVolleyAbility.FireProjectile` 写 `arrow.Pierce = attack.Pierce`，而 `Projectile.Launch` 里
   `_pierceLeft = Pierce + attack.Pierce` → 实际预算是 `2 × attack.Pierce`。
   对"命中即消失"（0 → 0）没有区别，对 ≥1 的档位算两倍。探针记录的是表值 `attack.Pierce = 99` 与
   "真的打穿 3 个靶"这个事实。是否修由用户裁定。
4. **一帧一个命中的物理限制**：670 m/s 时一帧飞 11.17m；`ResolveImpact` 穿透后把位置推到本帧**终点**，
   所以**间距小于单帧飞行距离的两个目标，这一箭只会结算最近的那个**。探针把靶间距摆成 18m 就是为了
   让每个靶落在不同帧。⚠️ 这对真实玩法有影响（贴脸的两只怪可能被一箭漏掉一只），
   属于 `Projectile.cs` 的既有行为，**不在本批授权范围**内（交接单没要求修），记在这里供用户裁定。
5. **"更大"目前只是表现 + 表值**：`Projectile` 的命中是**射线**，不按 `Scale` 放大命中体积。
   原文"更大"是否也包含"判定更大"未确认，本批按"外观更大"落地（`Scale = 2`）。
6. **表行 `ApplyHandler` 从 `AbilityOverride` 换成了 `ChargedShot`**（蓄力上限 3 秒挪进强类型资源，
   所以那一行不再需要字符串反射）。P5b 的 SelfTest 覆盖行仍 ✅（它读的还是 `ChargeMaxSeconds == 3`），
   `docs/upgrades.md` 的处理器表已同步（`AbilityOverride` 服务 7 条 → 6 条）。
7. **探针取样的物理粒度**：`Settle` 之外，"箭停在墙里"读的是**撞击前一物理帧**的位置
   （探针在树里排在投射物之前；`Projectile` 撞击前已经被推到墙体内 8.2339）。
   这已经是这一套只读接口能给到的最细读数，没有为此给 `Projectile` 加任何新状态。
8. 本批**没有截图**（证据是文本读数 + 存档哈希；与 J7/J8 同一条口径）。真机观感（大箭外观）留给人手 MCP 复核。
9. `Scripts/Config/ChargedShotTuning.cs.uid` / `Scripts/Testing/DragonArrowProbe.cs.uid` 若被编辑器生成，
   与工作区里其它未跟踪 `.uid` 同一批，不入库（这两个脚本都不被任何 `.tscn` 的 `ext_resource` 直接引用；
   `_DragonArrowProbe.tscn` 引的是 `.cs` 路径）。
