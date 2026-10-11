# 数值配置（调参唯一入口）

**所有可调数值都在一个文件里：[`Config/Balance.tres`](../Config/Balance.tres)**

- 游戏里运行时由 autoload `Balance` 读取，`RunController` 在**开局那一帧**套用到玩家、武器与节点节奏；生成的每个敌人/Boss 也会立刻套用全局倍率。
- **改完要重新运行游戏才生效**（数值在开局一次性应用，不热重载）。
- 两种改法都行：Godot 编辑器里双击 `Config/Balance.tres` 用 Inspector 改（有中文分组），或直接用文本编辑器改 `.tres`。
- 字段定义在 [`Scripts/Config/BalanceConfig.cs`](../Scripts/Config/BalanceConfig.cs)，加了中文注释；缺字段会回退到代码默认值。

## 参数分组

### 玩家
| 参数 | 含义 |
|---|---|
| `PlayerMaxHealth` / `PlayerMaxArmor` / `PlayerMaxShield` | 玩家最大生命 / 护甲 / 护盾 |
| `PlayerMoveSpeed` | 玩家基础移速 |
| `PlayerDamageReduction` | 护甲之外的额外减伤（0~0.9） |

### 角色 · 近战重装（角色 2）
| 参数 | 含义 |
|---|---|
| `HeavyMaxHealth` / `HeavyArmor` | 最大生命 380 / 护甲 140 |
| `HeavyMoveSpeed` | 移速 6.2（枪手是 7.0） |
| `HeavyDamageReduction` | 护甲之外的额外减伤 12% |
| `HeavyMeleeDamage` | 左键近战重击伤害 32（范围/冷却在 `Player.tscn` 的 `AbilityMelee` 上） |

近战角色会**关闭枪械**（`Weapon.Enabled = false`），左键改由近战能力接管（`PlayerWeaponInput` 里分流）；
近战需要一个「范围内的目标」才会出招，够不着就不挥空。

### 角色 · 召唤控制（角色 3）
| 参数 | 含义 |
|---|---|
| `SummonerMaxHealth` / `SummonerArmor` / `SummonerMoveSpeed` | 210 生命 / 60 护甲 / 6.8 移速 |
| （召唤本身） | 冷却 5 秒、一次 1 架、上限 3 架 —— 写在 `Player.tscn` 的 `AbilitySummon` 上 |

⚠ **未实跑验证**：角色 3 的属性、界面、构建都已通过，但「按 G 召唤出无人机」这一步在本轮没能确认（MCP 对该节点的属性读取与 `TryActivate` 调用持续返回失败，输入注入也没观察到召唤物）。下一轮需要换一种验证方式（例如让 HUD 显示召唤物数量再肉眼/截图确认，或加一个累计召唤计数器）。

### 武器 · 手枪
| 参数 | 含义 |
|---|---|
| `PistolDamage` | 单发伤害 |
| `PistolFireInterval` | 射击间隔（秒），越小射速越快 |
| `PistolMagazineSize` | 弹匣容量 |
| `PistolReloadTime` | **装填时间（秒）** —— 觉得换弹慢就调这个 |
| `PistolSpreadDegrees` | 腰射散布角度 |
| `PistolKnockback` | 击退力 |
| `PistolCritChance` / `PistolCritMultiplier` | 暴击率 / 暴击倍率 |
| `PistolProjectileSpeed` | 弹速 |
| `PistolAimFov` | 瞄准时的 FOV |

### 武器 · 激光狙击枪（第二把武器，策划文档 §8.1：角色 1 的武器是手枪/步枪/激光狙击枪/弓箭，没有霰弹枪）
| 参数 | 含义 |
|---|---|
| `ShotgunDamage` | 单发弹丸伤害（一次击发多颗） |
| ``SniperPierce`` | **基础穿透数量**（2 个目标） |
| `ShotgunFireInterval` / `ShotgunMagazineSize` / `ShotgunReloadTime` | 射击间隔 / 弹匣 / 装填 |
| `ShotgunSpreadDegrees` / `ShotgunKnockback` | 散布 / 击退 |

两把武器共用同一个 Weapon 节点，只换数值（`RunController.EquipWeapon`），所以不会重建节点、不会弄丢挂在武器下的输入组件。

### 火力倾泻（角色 1 技能，按武器分两种模式）

| 武器 | 行为 |
|---|---|
| 手枪 / 激光狙击枪（非连发） | 无视当前弹药，**直接打出一整个弹匣**（1/12 也能打满 12 发），随后自动装填 |
| 步枪（连发） | **持续 2.5 秒自动连射且不消耗子弹**（弹匣 0/N 也能用），到时停止 |

判定依据是 `Weapon.Automatic`，以后新增连发武器会自动走第二种。
### 武器 · 步枪（第三把武器）
| 参数 | 含义 |
|---|---|
| `RifleDamage` / `RifleFireInterval` | 单发伤害 / 射击间隔（0.12 秒 ≈ 8 发每秒） |
| `RifleMagazineSize` / `RifleReloadTime` | 弹匣 30 / 装填 |
| `RifleSpreadDegrees` / `RifleKnockback` | 散布 / 击退 |
| `RifleAutomatic` | 按住左键持续开火（唯一连发武器） |

三把武器共用同一个 Weapon 节点，只换数值（`RunController.EquipWeapon`）。

### 节点 · 商店（花局外碎片买局内收益）
| 参数 | 含义 |
|---|---|
| `ShopHealCost` | 恢复全部生命与护甲的碎片价格（5） |
| `ShopRewardCost` | 随机一件强化奖励（12） |
| `ShopWeaponCost` | 更换武器（18） |

碎片不足时不会扣款、也不会产生收益，但**依然会触发本场 Encounter**（节点 = 一次收益 + 一次挑战）。

### 敌人 · 全局倍率（改一处影响所有敌人）
| 参数 | 含义 |
|---|---|
| `EnemyHealthMultiplier` | 所有普通敌人生命倍率 |
| `EnemyDamageMultiplier` | **所有普通敌人伤害倍率（难度总开关）** |
| `EnemyMoveSpeedMultiplier` | 所有普通敌人移速倍率 |
| `EliteChanceMultiplier` | 精英出现概率倍率（1 = 按各节点的配置，0 = 不出精英） |

### 节奏 · Stage
| 参数 | 含义 |
|---|---|
| `NodeBudgetMultiplier` | 节点总预算倍率（越大敌人越多、节点越久） |
| `SpawnIntervalMultiplier` | 生成间隔倍率（越大出怪越慢） |

### Boss
| 参数 | 含义 |
|---|---|
| `BossHealthMultiplier` / `BossDamageMultiplier` | Boss 生命 / 伤害倍率 |
| `BossPressureInterval` | 压力升级间隔（秒） |
| `BossPressureDamagePerLevel` | 每级压力增加的伤害比例 |
| `BossPressureAttackSpeedPerLevel` | 每级压力增加的攻速比例 |

### 成长
| `RewardChoiceCount` | 三选一给几个选项（默认 3） |
|---|---|

### 表现 · 伤害跳字
| 参数 | 含义 |
|---|---|
| `ShowDamageNumbers` | 是否显示伤害数字 |
| `DamageNumberLifetime` / `DamageNumberRise` / `DamageNumberFontSize` | 跳字存活时间 / 上飘距离 / 字号 |

### 表现 · 血条
| 参数 | 含义 |
|---|---|
| `ShowEnemyHealthBars` | 是否显示敌人头顶血条（满血的杂兵不显示，Boss 常显） |
| `EnemyHealthBarWidth` / `EnemyHealthBarHeight` | 血条尺寸 |
| `EnemyHealthBarTopOffset` | 血条相对单位原点的抬升高度 |
| `BossHealthBarScale` | Boss 血条的放大倍数 |

## 当前取值（2026-01 原型）

玩家 250 血 / 40 甲 / 7 移速；手枪 24 伤害、0.26 秒间隔、12 发弹匣、**0.7 秒装填**；
普通敌人 **生命 ×0.75、伤害 ×0.3**；节点预算 ×1；Boss 生命 ×1、伤害 ×0.9。

调难度建议：
- 整体太难 → 先调 `EnemyDamageMultiplier`（0.3 → 0.2）
- 打起来太肉 → 调 `EnemyHealthMultiplier` 或 `PistolDamage`
- 一波接一波太密 → 调 `SpawnIntervalMultiplier`（1 → 1.5）
- 关卡太长/太短 → 调 `NodeBudgetMultiplier`

## 按键映射（当前）

| 操作 | 按键 | 定义位置 |
|---|---|---|
| 移动 | W/A/S/D + 方向键 | `project.godot` → `[input]` 的 `move_*` |
| 跳跃 | 空格 | `jump` |
| 射击 / 预瞄 | 鼠标左键 / 右键 | `fire` / `aim` |
| 战术滑铲 | **Q** | `dash` |
| 火力倾泻 | E | `burst` |
| 帮助界面 | H | `HelpScreen.ToggleKey`（导出属性，非 InputMap） |
| 菜单 / 关闭顶层界面 | Esc | `UiRouter`（`ui_cancel` 动作）；有弹窗就关掉最顶层那个，没有弹窗就打开菜单 |
| 装填 | 无按键 | 被动：`PistolAutoReloadDelay` 秒没开火或打空弹匣自动装填 |
| 三选一奖励 | 无按键 | 节点清空后自动弹出 |

> **Esc 语义（已按需求实现）**：只用于「打开菜单 / 关闭当前弹层」，**不再切换鼠标捕获** —— 鼠标由 `UiRouter` 按「有没有弹窗」自动 Captured / Visible。
> 现在改键只能手改 `project.godot` 的 `[input]` 段（注意每个事件的 `device` 必须是 `-1`，否则真键盘匹配不上；已踩过一次）。
> 后续要做的是一个设置界面：读取/写入 `InputMap` + 存到 `user://settings.cfg`，并把 H 这类 `ToggleKey` 导出属性也纳入同一套。


## 相机构图

| 参数（`Scenes/Player/Player.tscn` 的 `CameraRig`） | 含义 |
|---|---|
| `TargetScreenHeight` | 角色出现在屏幕的高度比例。`0.5` = 正中，越小角色越靠下。当前 **0.3**（视野更多留给前方） |
| `TargetOffset` | 相机绕的目标点（角色胸口高度） |
| `MouseSensitivity` / `MinPitchDegrees` / `MaxPitchDegrees` | 灵敏度与俯仰范围 |

## 技能瞄准方向

`Ability` 上有 `Aim` 配置项（不是所有技能都朝准星）：

| 取值 | 含义 |
|---|---|
| `Facing`（0） | 朝角色自身前方（敌人默认） |
| `MovementIntent`（1） | 朝当前移动输入方向 |
| `Crosshair`（2） | 朝玩家准星（屏幕中心射线）；没有相机时自动回落为前方 |

玩家滑铲（`Player.tscn` 的 `AbilityDash`）配的是 **2 = Crosshair**。取方向统一走 `Ability.ResolveAimDirection()`，射线工具在 `Scripts/Combat/AimUtil.cs`。

## 界面与弹窗（后续 UI 框架的基础）

- `OverlayScreen`（基类）：`OpenGuardSeconds`(默认 0.3s) 打开后禁输入防误触；`OpeningDuration` 供以后加缓动 —— 保护期取两者较大值，**加缓动不用改逻辑**。`CloseOnCancel` 控制 Esc 能否关闭（奖励 / 结算界面设为 false，避免流程卡住）。
- `UiRouter`（autoload）：维护界面栈，栈空 → 恢复游戏并捕获鼠标；栈非空 → 暂停并放开鼠标。Esc 先尝试关闭最顶层可关闭界面，没有界面时调用 `OpenMenu`（当前指向帮助界面，等真正的菜单）。

## 开局配置与武器池

- 开局先弹 **Configure 画面**（`Scenes/UI/StartScreen.tscn`）：按 1 选手枪 / 按 2 选霰弹枪 → 进入 Stage 1（主循环的 Configure 步）。
- **武器池节点**：互动后弹出**三选一**（手枪 / 霰弹枪 / 步枪），选定即换装并触发本场 Encounter。用的是通用选择界面 ``ChooseScreen``（给定标题 + 选项文本 + 回调下标），以后商店/事件节点可直接复用。

## 节点自身的数值

每个节点的预算、并发上限、生成间隔写在 `Scenes/Main.tscn` 的 `Stage/Node1`、`Node2`、`Node3Boss` 上（Inspector 里可直接改）。
每个节点的敌人种类与权重写在该节点下的 `SpawnXxx` 子节点上（`Scene` / `Cost` / `Weight` / `MinElapsedSeconds` / `EliteChance`）。

> **成本与预算的整除关系**：节点结束时余额若低于任何可用敌人的成本，会被当作「已花完」直接结算（不会卡住）。所以节点预算最好是成本的整数倍，否则最后会有一点余额被浪费。

## 代码里不写死数值

新增可调项时：在 `BalanceConfig` 里加一个 `[Export]` → 在 `Balance.tres` 里给出默认值 → 在套用它的地方读 `Balance.Settings`。
`EncounterController` 生成敌人后调用 `Balance.ScaleEnemy(unit, this, isBoss)` 套用倍率，`RunController.ApplyBalance()` 套用玩家/武器/节点数值。

## 血条（用户需求，2026-01）

| 项目 | 规则 | 配置 |
|---|---|---|
| 普通怪物血条 | 放在**脚下**（沿 -Up 偏移），满血不显示 | `EnemyHealthBarTopOffset = 0.25`（现在按脚下偏移使用） |
| 触发方式 | **事件驱动**：订阅 `GameplayEventBus.Raised`，收到 `Damaged` 才 O(1) 登记该单位并建血条，`Died` 立即移除；**不再每帧遍历 `enemy_units` / `boss_units` 组** | — |
| 首领血条 | **不再画头顶血条**，改为屏幕**上方**的主血条面板（`WorldOverlay` 按 `HasTag("Boss")` 跳过） | — |
| 近距离 | 与首领距离 ≤ `BossBarFullRange` 时展开完整血条（名称 + 血量 + 关卡） | `BossBarFullRange = 22` |
| 远距离 | 只保留首领头像色块 | 同上 |

代码位置：`Scripts/UI/WorldOverlay.cs`（怪物血条）、`Scripts/UI/PlayerHud.cs`（首领面板，含验证用只读属性 `BossBarExpanded` / `BossDistance`）。

**验证状态**：
- 首领面板：**已通过** —— 无首领时 `BossBarExpanded=False`；首领出场近距离（20.7 米）`True`、面板与名称条都可见；走远到 48.5 米（>22）自动收起为 `False`、只留头像；再靠近又展开为 `True` ✓
  （踩过的坑：`UpdateBossPanel()` 一开始被插在 `PlayerHud._Process` 的**某一个状态分支末尾**，Boss 战走的是另一个分支，所以从不执行；移到所有分支之前才生效。）
- 怪物血条改到脚下：代码已改（`WorldOverlay` 用 `- Vector3.Up * offset`），**仍缺视觉确认**，建议你游玩时看一眼。
- 血条事件驱动：**机制已通过**（2026-10 自测实测日志）——满血敌人不建血条（`满血HP=60 血条数=0`）；扣 5 点（60 → 55）后 `Damaged` 事件立刻建出血条（`登记 Chaser HP=55/60 现有血条=1`），此后逐帧采样 121 帧期间**只登记一次、没有重复建条、单位存活且 HP 实时为 55**；致死 `Died` 事件立刻移除（`移除 ... 剩余血条=0`）。首领按 `HasTag("Boss")` 跳过（`跳过 Boss ... 候选=False`）。
  注意：若一次伤害直接致死，`Damaged` 与 `Died` 在同一调用栈内先后触发，血条会「建好又立刻拆掉」——本作实测几乎每次伤害都是即死，所以用 0.5 秒级轮询 `Bars` 子节点数常常看到 0，这是正确行为，不是丢事件。

## 关卡安全（用户需求，2026-01）

| 项目 | 规则 | 配置 |
|---|---|---|
| 地图下方区域杀 | **所有单位**（玩家/敌人/Boss/召唤物）进入区域即击杀 | `KillPlaneY = -8`（Balance.tres）→ 区域中心取 `KillPlaneY - 30`，由 `MapManager.EnsureKillZone()` 建 `KillZone`（Area3D，进入即杀）；旧的 `KillPlane` 每帧遍历实现已删除 |
| 节点生成避障 | 节点启用/降临时先找脚下地面，再从原点螺旋外扩找一个球形探测不重叠的空位 | `StageController.PlaceSafely()` |

避障实现要点（踩过的坑）：
- 探测球心必须抬到**半径 + 余量**之上，否则球体永远与脚下地面重叠，所有节点都会被判成「找不到空位」。
- 找地面的射线起点只抬高 2 米，否则会打到上层平台/斜坡的顶面，把节点放到半空（实测曾把节点放到 y=2.65）。

实测：

```
[Node] 节点避障：NodeWeapon   从 (9, 0, -7)  → (9, 0.25, -7)
[Node] 节点避障：NodeEnhance  从 (-9, 0, -5) → (-9.95, 1.86, -0.19)   ← 它在斜坡上，贴的是坡顶
[Node] 节点避障：NodeEvolution 从 (7, 0, 10) → (7, 0.25, 10)
[Node] 节点避障：NodeMutation  从 (-8, 0, 9) → (-8, 0.25, 9)
[Node] 节点避障：NodeShop      从 (0, 0, 13) → (0, 0.25, 13)
```

区域杀实测：把玩家 Y 设为 -20 → 生命 260 → **0**、`IsDead = true`，日志记「玩家掉出地图 → 击杀」。

⚠ 已知边界：如果人为把节点塞到地图正中央的地下（约 19 米内都有碰撞体），避障会放弃并保持原位并写日志。正常布置不会遇到。

## Pressure 结算（关卡核心文档 L352/L370）

| 参数 | 含义 |
|---|---|
| `PressurePerHighValueNode` | 完成一次**高价值**节点（进化/异化）挑战增加的 Pressure 点数（150） |
| `PressurePerPoint` | 每满多少点转成 1 点首领压力（100） |

规则：**挑战完成后**才累积（不是随时间涨）；只有高价值节点计数；每满 100 转 1 点，**余数清零**（不满 100 的部分丢弃）。

累积结果进入 `OpenBossDoor()` 的初始压力：

```csharp
_bossEncounter.BossInitialPressure = Mathf.Max(0, ChallengesUsed - FreeChallengeQuota) + _bossPressureBonus;
```

即「超额挑战次数」+「节点累积压力」共同决定 Boss 初始压力。

实测（自测直接调 `AddPressureForNode`）：

```
Pressure 累积：+150 → 余数 0 / 本关首领额外压力 1
Pressure 累积：+150 → 余数 0 / 本关首领额外压力 2
再加一次中价值（强化）→ 压力仍为 2（高价值才计数 ✓）
```

注：两次 150 得到 2 点而不是 3 点，是因为文档要求「余数清零」——每次转换后不足 100 的 50 点被丢弃，实现与文档一致。

## 节点生成约束（关卡核心文档 L105-107）

| 规则 | 实现 |
|---|---|
| 节点位置从地图预设点位中选择 | `Scenes/Main.tscn` 里 10 个 `RewardNode` 实例的位置 |
| 节点之间保持**最小间距** | `MinNodeSpacing = 8`；`EnableLayer()` 挑选时**跨层**判定，过近的候选跳过 |
| 不与障碍物冲突 | `StageController.PlaceSafely()`（射线找地面 + 球形探测螺旋外扩） |
| 挑不够时怎么办 | 放宽补足并写日志（避免因为间距约束导致每关节点数不足） |

实测：

```
节点间距：最小 9.74（NodeShop↔NodeHeal），阈值 8        ← 无需放宽
节点布置：启用 10 个（高 2 / 中 5 / 低 3）
```

为了让间距能满足，10 个节点的预设点位重排成 **间距 10 的网格**（±15 × ±10 范围内），否则原位置最近的一对只有 3.16。

## 白板首领副本（关卡核心文档 L231/L315/L317）

每个 Encounter 的**最终阶段**都要打一个「白板首领副本」——文档里叫"第 8 波"，且明确要求**不能与常规波数共用参数**；同时提醒它**不是** Boss Door 里的最终首领。

| 项目 | 实现 |
|---|---|
| 触发时机 | 常规波全部清空后（`CheckCompleted()` 里，早于"结算完成"） |
| 独立参数 | `EncounterController.EliteBossScene`（**独立于** `BossScene`/`IsBossNode`） |
| 弱化 | `EliteBossHealthScale = 0.35`（白板首领血量只有普通首领的 35%） |
| 不带 Boss 构筑 | 不设置 `BossPerkCount`/`BossInitialPressure`，所以无词条、无压力 |
| 完成条件 | 白板首领**死亡后**才算完成（它在 `_spawned` 里，残留会阻止完成判定） |
| 复位 | `ResetEncounter()` 复位 `_eliteBossSpawned` |

实测：

```
常规波已清空 → 进入白板首领副本
白板首领降临（血量倍率 0.35，无压力 / 无词条）
死亡 Boss(0)
Encounter 清场完成：共生成 4，用时 3s      ← 首领死后才结算
交互 #1：接受=True ... encDone=True
```

**踩坑**：第一版我把这段分支写进了 `if (BossScene != null)` 里，而普通 Encounter 的 `BossScene` 是 null（只有 BossFight 才有），所以永不执行；而且它还在 `IsCompleted = true` 之后，即使执行也拦不住结算。正确位置是 `CheckCompleted()` 开头、`AliveCount > 0` 判断之后、`IsRunning = false` 之前。

### 进化 / 异化对 Pressure 的额外比例（关卡核心文档 L356）

| 参数 | 含义 |
|---|---|
| `PressureEvolutionBonus` | 进化奖励的 Pressure 比例（1.25 = +25%） |
| `PressureMutationBonus` | 异化奖励的 Pressure 比例（1.5 = +50%） |

`AddPressureForNode()` 按奖励类型乘比例后再走「每 100 转 1 点、余数清零」。实测：

```
进化 150×1.25 = 187.5 → +188 → 转 1 点（余数清零）
异化 150×1.50 = 225   → +225 → 转 2 点（累计 3 点）
再加一次中价值（强化）→ 压力仍为 3（只有高价值计数）
```
