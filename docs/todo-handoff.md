# 交接文档：待办任务与项目约定

> 给接手工作的代理看。**这份文档是自包含的**，不需要其它对话上下文。
> 项目：`F:\Godot项目\项目a`（Godot 4.7.2 stable mono / C# / net8.0）

---

## 一、铁律（违反会浪费大量时间，都是踩过的坑）

### 1. 改 C# 代码只用这两种方式

| 方式 | 适用 |
|---|---|
| **`edit` 工具**（读文件后精确替换） | **首选**，多行结构改动必须用它 |
| `pwsh` 里的**单行** `.Replace('旧的一整行', '新的')` | 只改一行时可用 |

**绝对不要**用 pwsh 做**多行**文本替换 —— 换行符（`\r\n` vs `\n`）不一致会**静默失败**：命令返回成功、文件没变、行为照旧。本项目已经因此栽了 7 次以上。

如果必须按行块删除方法，用这个可靠套路（已验证）：
```powershell
$lines = [System.Collections.Generic.List[string]](Get-Content $file)
# 从起始行向下找第一个 "\t}" 作为方法结尾；从后往前删除避免行号漂移
$lines.RemoveRange($start, $end - $start + 1)
Set-Content $file -Value ($lines -join "`r`n")
```
删完**立刻编译**，因为范围可能吃多（曾误删过一个方法）。

### 2. 每改一步都要验证

```powershell
# 编译（必须 0 warning / 0 error）
& 'F:\Godot项目\项目a\build.ps1' -v minimal
```
```powershell
# 跑游戏内的自测（验证逻辑）
'1' | Set-Content 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'
$cli = 'F:\Godot项目\项目a\.gdmcp\bin\gdmcp.exe'
& $cli tool-call run_project --args-json '{"allow_window":true,"scene_path":"res://Scenes/Main.tscn"}' --apply
Start-Sleep -Seconds 20
Remove-Item 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'
& $cli tool-call stop_project --args-json '{"allow_window":true}' --apply
# 读日志（自测会把内部状态打进去）
$dir = 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\logs'
$f = Get-ChildItem "$dir\*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Get-Content $f.FullName | Where-Object { $_ -match '波开始|清场完成|端到端' } | Select-Object -Last 8
```

**改完必须把 `selftest.flag` 删掉**，否则玩家自己玩时会被自测接管。

### 3. 其它约定

- 数值一律放 `Config/Tables/<分组>/*.tres`（一行一个文件，索引挂在 `Config/ConfigIndex.tres` 的 `[Export] XxxConfig[]` 数组上）或 `ConfigIndex.tres` 的全局字段里，**不要硬编码在逻辑里**。见 [config.md](config.md) 的"调参去哪张表"。
- **表是权威、代码是回退**：读不到表就用组件/行类自身的 `[Export]` 默认值（`ConfigIndex.FileLoaded` + 每个系统的"来源：表/默认值"日志能证明走的是哪条路）。
- ⚠️ **不要单独删/改 `ConfigIndex.tres` 引用到的行文件** —— 一个行文件缺失会让**整份** `ConfigIndex.tres` 加载失败、所有数值静默回退。要下线一行，把数组项与对应的 `[ext_resource]` 一起删。
- 玩家可见文案改动 → 同步更新 `docs/`。
- 日志用 `ActRoguelike.Core.GameLog.Write("分类", "消息")`（分类：Flow / Node / Reward / Spawn / Combat / Map / **Config**）。
  ⚠️ **`ConfigIndex` 的 `_Ready` 里不能直接写日志** —— `GameLog` 的 autoload 排在它之后，写进去会被静默丢掉。`ConfigIndex` 用 `CallDeferred` 绕过这一点。
- 运行时读数用 CLI 的 `await_runtime_condition`（**不是** `evaluate_debug_expression`，后者只在断点暂停时可用）：
  ```powershell
  $a = @{ expression = 'get_node("/root/Main/Stage").DebugState'; timeout_ms = 1500 } | ConvertTo-Json -Compress
  & $cli tool-call await_runtime_condition --args-json $a --apply --allow-open-world
  ```

---

## 二、项目结构与关键约定

```
Scripts/
  Abilities/ AI/ Boss/ Camera/ Combat/ Config/ Core/ Encounter/
  Interaction/ Player/ Rewards/ Run/ Testing/ UI/ Weapons/ World/
```

**分层**：`Unit`（单位本体）→ `Controller/Brain`（决策）→ `Ability`（技能，必须挂 Node3D）→ `Weapon/Attack/Hit/Damage`（战斗管线）→ `Event/Trigger`（事件与触发器）。
流程层：`RunController`（一局）→ `StageController`（一关）→ `EncounterController`（一场）。

**已抽出的纯逻辑层**（改规则不要碰流程代码）：
`NodePlacement` / `SpawnPlanner` / `RewardService` / `RunSettlement`
（`KillPlane` 已删除，区域杀改为碰撞区域 `Scripts/World/KillZone.cs`）

**统一入口**（新增代码优先用它们，别自己找）：
- `PlayerLocator.Player` —— 玩家引用（缓存，别每帧查组）
- `MapManager.Instance` —— 导航烘焙 + 刷怪点/降临点/节点落点
- `UiService.Instance` —— 开界面（`OpenUpgrades/OpenLoadout/OpenHelp/OpenRewards/OpenChoose/OpenMessage`）
- `UiRouter` —— 界面栈 / Esc / 暂停策略
- `ConfigIndex.Settings` —— 所有数值（**唯一数值门面**；实际数值在 `Config/Tables/<分组>/` 的分表里，见 [config.md](config.md)）
- `ConfigRows.ById<T>()` / `BySlot<T>()` / `ByKind<T,TKind>()` —— 读配置表的唯一入口（读不到 → `null` → 调用方回退到自身 `[Export]` 默认值）

**UI 框架现状**：`OverlayScreen` 已支持分层（`UiLayer.Hud/Panel/Modal`，决定是否暂停）、防误触、开合动画（`FadeSeconds` / `OpenScaleFrom`）。
界面清单与所属层见 [config.md](config.md) 的「界面与弹窗」；**新增界面请走 `UiService` + `UiRouter`，不要自己 new / 找节点路径**
（例：首领状况预览 = `BossPreviewScreen` + `UiService.OpenBossPreview()`，快捷键 B，分层 `Panel`）。

**详细流程**见 [controller-flow.md](controller-flow.md)，架构与坑见 [architecture.md](architecture.md)。

---

## 三、待办任务（本次交接的重点）

### 任务 I：P6b —— 表现层 + UI 尾巴 + 横向清理（**P1→P6 的最后一批**）✅ 已完成（2026-10-16）

> **这一轮把 P1→P6 整条重构线收尾了。** 逐条记录见
> [character-redesign.md](character-redesign.md) §七；本段只列"做完了什么 / 还差什么"。

**① 弹匣/装填横向清理（用户裁定「没有弹匣的概念了」）—— 5 层全清**：
配置表（删 3 个行文件 + `ConfigIndex.tres` 三处同改）· 代码（`Weapon` 整组装填逻辑 + `WeaponConfig`
三列 + `PlayerWeaponInput` 的 reload 键 + `MetaProgress` 的装填成长 + `RunController` 的修饰符 +
`Unit.SeedStats` 两个键 + `GameplayEventKind` 两个成员 + `AmmoOnKillRule`/`AmmoOnKill` handler）·
UI（HUD 弹药显示 + `Hud.tscn` 的 `Root/Ammo` 节点 + 帮助页两行说明 + 三个场景里失效的属性行）·
自测（武器比对那一行的期望串）· `project.godot` 的 **`reload` 输入动作（确认无引用后删）** + 文档。

**② 五条通用异化：做了 2 条**（都是"能复用已有机制"的）：
`mut_stomp` 杀戮践踏（落地范围伤害，轮询 `CharacterMovement.IsGrounded`）×
`mut_homing_missile` 巡飞弹（暴击 → 追踪飞弹，复用 P3 追踪弹通路 + 专用信标状态 `MissileBeacon`）。
**还差 3 条**：护卫（要"奔跑态"，全项目还没有 sprint 输入/状态）· 灵魂收割（要掉落物拾取系统）·
落日剑（命中施加延迟落剑）。两条新行都有**自测行为取证**（真敌人靶子 + 真 `RewardContext`）。

**③ 表现层：做了"准星处的蓄力指示器"**（用户描述最具体的那条；`Scripts/UI/ChargeIndicator.cs`）——
缺口随 `Ability.ChargeProgress` 收缩到闭合，闭合/完美时机变色 + 准星本体染色。
**没做**：开镜特效 / 后撤残影 / 剑气拖尾 / 落圈特效。→ ✅ **4 项已在 P6c 做完，见下面「任务 J」**。

**④ UI 两个尾巴：都做了，并查出两个真缺陷**：
额度进度条配了 `StyleBoxFlat` + 三档变色（正常蓝 / ≥0.8 黄 / 满红）；
**自适应列数原来没在工作** —— ①右栏也在抢宽度、②列数只涨不跌（矩阵尺寸被自己的最小尺寸顶住，
窗口变窄时不发 `Resized`）、③页脚文案不刷新。三个都修了，实测 1920→12 列 / 宽→19、24 列，
卡牌比例恒 0.625，截图在 `docs/screenshots/`。

**⑤ 两个可选都办了**：删掉 `MetaProgress.Buy*` 五个死方法（`Bonus*` 保留 —— 开局还在读）；
`ApplyAbilityMatrix` 的**顺序问题只改注释不改代码**（改顺序会让 `ResetAbilityBindings`
把技能行刚写上的按键绑定清掉，是实打实的回归；真修要拆三步）。

> **索引**：`6/2/10/1/1/39/7/13/8/6/6/11/6`（P6a）→ **`6/2/10/1/1/39/7/14/8/5/6/11/4`**
> （效果 +1 = `eff_missile_beacon`；局外 −1 = `meta_reload`；能力卡 −2 = 两张弹匣卡）。
> **增幅项 57 → 59**（强化 9 / 进化 23 / 异化 25 → 27）。
> **硬指标逐位不变**：节点布置 10(2/5/3) · Pressure=3 pmods=3 bank=0 · 白板首领 315/甲0 ·
> 端到端 奖励界面打开 True · 最小闭环链路 ✓ · 玩家 220/20/6.5 · 日志有 `（ConfigIndex.tres 已加载）`。
> 备份：`_refactor_backup/ConfigIndex.tres.p6bbefore`。

### 任务 J：P6c —— 补齐 4 项表现层特效 ✅ 已完成（2026-10-16）

> ⭐ **本批的纪律：表现层只读运行期状态，战斗逻辑一行没改**
> （唯一例外 = 给 `HawkEyeAbility` 加了一个**只读**属性 `ZoomProgress`）。
> 加特效的完整清单 / 契约 / 踩过的坑见 **[vfx.md](vfx.md)**；逐条接入记录见
> [abilities.md](abilities.md) §9。四张实拍图在 `docs/screenshots/p6c_*.png`（各带一张放大图）。

**① 开镜特效**（弓 武技2「刹那之眼」，设计文档第 87 行「放大视角」）
`Scripts/UI/ScopeOverlay.cs`，挂 `Scenes/UI/Hud.tscn` → `Root/ScopeOverlay`（Root 的**第一个**子节点）。
一张径向渐变贴图（`GradientTexture2D`）= 暗角 + 圆形镜筒细亮环 + 环上 12 条朝内刻度；
**镜筒半径以内一个像素都不画**（中心净空 167.6px）→ 不遮挡瞄准。只读 `HawkEyeAbility.ZoomProgress`
→ 开镜淡入、还原视角淡出，与镜头完全同步。`mouse_filter = Ignore`（双保险）。

**② 后撤残影**（技能1「后撤」，设计文档第 26 行「有无敌帧」）
`Scripts/Combat/AfterimageTrail.cs`，挂 `Scenes/Player/Player.tscn` → `Player/Afterimage`。
无敌帧期间每 0.09s 留一个角色模型的半透明副本（`GhostLifetime` 内淡出）。
只读 `BackdashAbility.InvulnerableOwned`（P3 就有的只读属性）→ 起止与 `Dodge→Dodger` 同起同落。
⚠️ **安全性（做减法 + 三个可数取证量）**：复制出来立刻把子树里**全部 `CollisionObject3D`（含 `Area3D`）
/ `Unit` / `AnimationTree` / `AnimationPlayer`** 摘掉并 `QueueFree`，再把整棵子树**退出所有分组**；
只 `AddChild` 到 `CurrentScene`。实测 4 个残影在场时：`碰撞体=0 单位=0 分组=（无）`，
且 `enemy_units` / `player_units` / `projectiles` 三个分组计数**一位没变**。

**③ 剑气拖尾**（灵剑 武技2「拔刀式」，设计文档第 110/114 行）
`Scripts/Combat/ProjectileTrail.cs`，**挂在剑气本体** `Scenes/Combat/SwordQi.tscn` → `SwordQi/Trail`
（不是另做一个特效实体）。每物理帧记一个采样点，用 `ImmediateMesh` 连成一条**面向镜头**的渐细光带。
只读 `Projectile.VelocityDirection`。采样点用 `ToLocal` 换回局部空间 → 父节点的
`ProjectileSizeScale`（"剑气大小等比攻击盒大小"）自动作用在拖尾宽度上。

**④ 落圈特效**（灵剑 武技3「落剑式」+ 被动「落」，设计文档第 112/99 行）
`Scripts/Combat/LandingShockwave.cs`，挂 `Player/AbilityFallAttack/Shockwave`（订阅公开的
`FallAttackAbility.Landed` 事件）与 `Player/AbilityPassive2/ShockwaveBurst`（轮询公开只读的
`LandingBurstAbility.TriggerCount` 上升沿）。**半径读战斗层真正的那个几何量** ——
`Attack/Shape` 的 `SphereShape3D.Radius`（3.2m，= `PulseDamage.Around` / `AttackHitbox` 用的范围），
读不到才用兜底值。

> **三个实拍里发现并修掉的真问题**（都是截图看出来的，不是"我觉得"）：
> ① 暗角用"一圈套一圈 `DrawArc`"→ 抗锯齿边缘叠出**一圈圈可见的同心细线** → 换 `GradientTexture2D`；
> ② 残影 0.045s/8 个 → **糊成一片光晕**（后撤总共只走 ~1.8m）→ 改 0.09s/4 个 + 饱和一点的青蓝；
> ③ `TorusMesh.Rings` / `RingSegments` **写反了 → 圆画成菱形**；顺带把加法混合默认关
>    （白地板 + `Add` 会冲成一片白）。
>
> **硬指标逐位不变**：节点布置 `10(2/5/3)` · `pressure=3 pmods=3 bank=0` · 白板首领 `315/甲 0` ·
> 端到端 `奖励界面打开=True` · 玩家 `220/20/6.5` · 索引 `6/2/10/1/1/39/7/14/8/5/6/11/4` ·
> 最小闭环链路 ✓ · 自测 `=== 自测结束 ===` **退出码 0**。**`ConfigIndex.tres` 一行没动**
> （表现层不该新增表行）。备份：`_refactor_backup/ConfigIndex.tres.p6cbefore`。

### 📌 P1→P6 整条线现在的状态（给下一位接手的人）

| 阶段 | 内容 | 状态 |
|---|---|---|
| **P0** | 调研（动画普查 + 硬编码审计 + 战斗管线盘点） | ✅ |
| **P1** | 数据层：清旧表 + 建新表（2 角色 / 6 武器 / 39 技能行 / 59 增幅 / 6 升级池） | ✅ |
| **P2** | 机制层：蓄力·完美时机 · 技能层数 · 滞空 · 奔跑/空中跳跃 · 子弹行为 · 标记 · 格挡 · 下落攻击 | ✅（⚠️ **"奔跑"只做了数据语义，没有输入/状态** —— 见 P6b ② 的"护卫"） |
| **P3** | 角色 A（3 把远程武器 + 技能 + 被动） | ✅ |
| **P4** | 角色 B（3 把近战武器 + 技能 + 被动，武技1 统一驱动） | ✅ |
| **P5 / P5b** | 增幅接入 + 池重构（限定/互斥/前置三层判定 + 图鉴数据基础） | ✅ |
| **P6a** | 清旧（旧角色/武器/技能/增幅/池/能力卡）+ 基地 UI 改造 | ✅ |
| **P6b** | 弹匣横向清理 + 2 条异化 + 蓄力指示器 + UI 两个尾巴 + 两个可选清理 | ✅ |
| **P6c** | **4 项表现层特效**（开镜 / 后撤残影 / 剑气拖尾 / 落圈）—— 只读运行期状态，战斗逻辑零改动 | ✅ |

**P6c 之后仍然开着的口子**（都不阻塞玩法）：
1. 通用异化 3 条没做（护卫 / 灵魂收割 / 落日剑）—— 见 [upgrades.md](upgrades.md) §7 第 1 条。
2. 角色 A/B 的**生命 / 护甲 / 移速是预设值**（220/20/6.5 与 220/25/6.8），设计文档没写，**等用户确认**。
3. `ApplyAbilityMatrix` 的时机要拆三步才修得对（见 P6b ⑤）。
4. 3 条占位行 + 8 处"只接了一半"的增幅（[upgrades.md](upgrades.md) §7 第 2、3 条）。
5. `ArmorBreakFx`（破甲特效）的 `TorusMesh.Rings`/`RingSegments` 也是写反的（环是八边形），
   修法 = 两个数字对调；本轮不在边界内，见 [vfx.md](vfx.md) §7。

### 任务 H：P6a —— 清旧 + 基地 UI 改造 + 三个遗留问题 ✅ 已完成（2026-10-15）

> ⚠️ **阶段纪律变了**：**"只加不删"是 P1→P5 的纪律；P6 起解禁**。P1→P5 期间攒下的旧内容
> 已在 P6a 一次清完（见 [character-redesign.md](character-redesign.md) §六）。
>
> **删了**：旧角色 3（`chr_gunner` / `chr_heavy` / `chr_summoner`）· 旧武器 3（`wpn_pistol` / `wpn_sniper` /
> `wpn_rifle`）· 旧技能行 5（`skl_dash` / `skl_burst` / `skl_melee` / `skl_summon` / `skl_control`）·
> 旧增幅 17（强化 10 + 进化 4 + 异化 3）· 旧升级池 3（`pool_pistol` / `pool_sniper` / `pool_rifle`）·
> 能力卡 2（`card_summon` / `card_control`）· 能力类 2（`DashAbility` / `BurstFireAbility`）·
> `Player.tscn` 的 5 个旧角色专属能力节点 · `project.godot` 的 4 条旧按键动作。
>
> **索引 `9/5/10/1/1/44/7/13/8/6/9/11/8` → `6/2/10/1/1/39/7/13/8/6/6/11/6`**（差额只有
> 武器 −3 / 角色 −3 / 技能 −5 / 升级池 −3 / 能力卡 −2）；**增幅项 74 → 57**（强化 9 / 进化 23 / 异化 25）。
> 其余硬指标逐位不变：**节点布置 10（2/5/3）· Pressure=3 pmods=3 bank=0 · 白板首领 315/甲 0 ·
> 端到端 F=True · 最小闭环链路 ✓**。
>
> **⚠️ 两处读数按用户裁定换了基准**（"防回归"的意义是别的东西没被改坏，角色换代后基准本身就该换代）：
> - `配置表比对·玩家` **270/40/7 → 220/20/6.5**（自测基准角色从 `chr_gunner` 换成 `chr_hunter`）
> - `配置表比对·升级池` `pool_pistol / 9 行 → pool_energy_palm / 6 行`
> - ⚠️ **角色 A 的生命/护甲/移速（200/20/6.5）是 P3 代理自拟的预设值** —— 设计文档没写这三项，
>   **等用户确认**；不要当成经过设计的平衡值。
>
> **同时落地了用户两条澄清**：① 删掉基地的「开始探索」按钮（进关卡 = 走到 **出击门**
> `Base.tscn/TerminalGate` 按 F，已核实它真的能进关卡）；② 删掉"出征时按角色行把枪收起来"
> —— "左键归谁"改由**武器行**的 `WeaponConfig.UsesWeaponComponent` 决定（6 把新武器都是武技驱动），
> 基地预览与关卡同源（进关卡**只是切场景**）。基地那页旧的「永久强化」（`BaseScreen`）**整份删除**，
> 局外成长台现在直接开**能力卡界面**（`meta_quota` 与 `MetaProgress.Shards` 一行没动）。
>
> **三个遗留问题的结论**：① `MaxAimDistance` 三条 `已跳过` 警告 → 删参数项（那个类没有瞄准射线）；
> ② `ArmorBroken` / `ShieldBroken` / `Critical` **补上了唯一抛点**（都在 `Unit.ReceiveHit`），
> 顺手把「能量护盾」的"护甲破碎"那一路接上；③ `VFXControllerBB` **复现不出来**（全局类缓存过期，
> 已自愈，不需要改代码）。
>
> **顺手修了一个真 bug**：`践踏死亡` 原来**覆盖**而不是累加额外空中跳跃，会把被动「灵动」的 1 次吃掉。
>
> **留给 P6b（✅ 2026-10-16 已全部办完，见上面「任务 I」）**：① 通用异化还差 5 条（杀戮践踏 / 护卫 / 巡飞弹 / 灵魂收割 / 落日剑 —— 每条都要一个新机制）；
> ② 弹匣/装填那条横向清理（`card_magazine` / `card_reload` / `meta_reload` + `Weapon.cs` 的装填逻辑 ——
> 本批明令不许碰 `Weapon.cs`）；③ `docs/config.md` 等长文档里还有旧角色的读数（**只改会误导的**，没全改）。

### 任务 0：配置表体系阶段 2 —— ConfigIndex 拆成 8 张表 ✅ 已完成（2026-10）

> **做了什么**：`ConfigIndexData` 从 74 字段降到 30 个全局字段；新增 8 张表（武器 3 / 角色 3 / 敌人 1 /
> **效果 10** / 节点 9 / 增幅项 19 / 局外成长 5 / 关卡 3，共 **53 个行文件**），全部"一行一个 `.tres`"，
> 索引挂在 `ConfigIndex.tres` 的 `[Export] XxxConfig[]` 数组上。
> **没有做逐行回退**（保持资源引用数组）—— 但**不要单独删/改行文件**，见下面那条纪律。
> `ConfigIndex.Settings.Xxx` **仍然可用**（渐进迁移，没有大爆炸式重构）。
> 进度 / 实测证据 / 逐项比对 / 两条新坑见 [config-schema.md](config-schema.md) §5 阶段 2 与 §8；调参入口见 [config.md](config.md)。
>
> **留给下一轮的三件事**（按建议顺序）：
> 1. ✅ **AI 行为表（盘点 #15）已做完（2026-10 配置表阶段 5）**：新增 `Config/Tables/AiProfiles/ai_*.tres`
>    **11 行**（一行一个 AI 档案 = 一个单位的整套 AI 参数），行类 `AiProfileConfig`；`UnitConfig` 多一列
>    **`AiProfile`（资源引用）**，套用挂在 `UnitConfig.ApplyTo` 上（**没有碰 `EncounterController`**）。
>    盘到 **25 个 AI 参数**（原先散在 10 个敌人场景的 **53 处**赋值里）。
>    ⚠️ 4 处硬编码避障启发式（`Behavior.AvoidObstacles` 的 2.4 / 0.6 / 1.2s / 0.35）**没有进表**（属"AI 手感"保护值）；
>    ⚠️ 档案只调数值，**不增删行为节点**（拓扑仍在场景里）。
>    见 [config.md](config.md) 的「配置表阶段 5 实测」与 [config-schema.md](config-schema.md) §5 阶段 5。
> 2. ✅ **首领增强的"节点驱动"（T9 最后一步）已做完（2026-10 配置表阶段 6）**：
>    `NodeConfig.BossPressurePoints` / `BossModifierRolls` / `BossModifierTags` 三个字段
>    **已全部接进** `StageController.AddPressureForNode`（不是 `EncounterController.BossModifierRolls` ——
>    改成 `StageController` 排队 + `BossSpawned` 信号在 Boss 出生时套用，避免把关卡逻辑渗进遭遇层）。
>    §7.5 的 Pressure 转换池真的接线了：`PressureModConfig` 新增 `Tags`、`Weight` 真的加权；
>    **两个抽取点拆开**（Boss 开场词条 `Roll()` 只抽行为型，Pressure 转换 `RollPressure()` 只抽数值型）。
>    ⚠️ 因此**开场词条的结果会变**（候选 7 → 4，§7.1 要求的分离）；自测的 `+187.5 → 1` / `+225 → 3` **没漂**。
>    ⚠️ 两处需要平衡测试 / 后续处理：① `BossInitialPressure` 不再叠加节点累积 Pressure 折算出的压力等级
>    （那是 §7.5 的替身）；② HUD 的 `[词条]` 一栏只显示行为型，不显示 Pressure 数值强化。
>    见 [config.md](config.md) 的「配置表阶段 6 实测」与 [boss.md](boss.md)。
>    ⚠️ 关卡核心文档 §7.1 的硬约束仍然成立：**Boss Pressure（数值强化）与 Boss Build（行为/技能）必须分离**，
>    且**不要为 Pressure 设计"可被玩家增幅修改"的接口**（本轮没有加）。
>    ✅ **§8.2 的「免费挑战不加 Pressure」闸门已接（2026-10）** —— 见下面「任务 D」。
> 3. **收尾决定**：`RewardConfig.PoolTags` / `ConflictsWith` 两个死字段（接上或删掉）、
>    `WeaponConfig.ApplicableWeapons` 反向指（武器↔升级池关联）、`Pistol.tscn` 的数值（回退基线）。
>    → ✅ **`PoolTags` / `ConflictsWith` 已在阶段 4 处理**（前者删除、后者接上）；
>    `WeaponConfig.ApplicableWeapons` **全项目从未存在过**，且用户拍板改成**正向指**（武器 → 升级池表），
>    所以不再需要它；`Pistol.tscn` 的数值**仍未动**（它现在是"表读不到时的回退基线"）。
>    见 [config.md](config.md) 的「配置表阶段 4 实测」。
>
> ✅ **阶段 3 已完成（2026-10）**：统一单位表（`UnitConfig` 基类 + 敌人 10 行 / 首领 1 行 / 召唤物 1 行 +
> 角色表改继承）· 技能表 25 行（含 4 条首领行为词条）· 首领数值强化池 3 行 · 白名单改成按 Id ·
> 6 处死字段清理 · 敌人倍率静默换挡修复 · `BarEnemy` 居中修复。
> 详见 [config.md](config.md) 的「配置表阶段 3 实测」与 [config-schema.md](config-schema.md) §5 阶段 3。

### 任务 G：用户第三轮 C1（护甲无上限）+ C2（节点池规则）✅ 已完成（2026-10-12）

> **用户原话**：C1「**有甲了才会显示**，**护甲没有上限额度**，所以**可以删除掉 `MaxArmor`** 的」；
> C2「**进化池里会出什么选项是根据交互的角色持有的攻击方式（武器）**，并没有"**放哪个进化池节点**"的说法……
> 而**池里一开始有什么，应该在进入关卡初始化的时候就判断好的**」。
> 需求原文：[requirements-ability-system.md](requirements-ability-system.md) 的「用户第三轮澄清（2026-10-12）」。
>
> **做了什么**：
> 1. **C1**：`Unit.MaxArmor` / `UnitConfig.MaxArmor` → **`StartingArmor`（开局护甲）**（**改名，值一个没动**）；
>    `MaxArmorMultiplier` / `MaxArmorAdd` 两个 StatSheet 键与 `RefreshStats` 里"抬高上限并补甲"那段**删除**；
>    新增 `Unit.GrantArmor()`（精英怪 `EliteModifier.ArmorAdd = 120` 改走它 —— "纯粹的加甲"）；
>    **显示判据 `MaxArmor > 0` → `Armor > 0`**（3 处 `SetArmor(...)`：`PlayerHud.UpdateArmorBar` /
>    `WorldOverlay.UpdateBar` / `WorldOverlay.CreateBar`）→ **甲打光不再留空壳**；
>    甲条比例的分母换成运行期派生的 `Unit.ArmorPeak`（不来自配置、不参与结算）。
>    ⚠️ `ArmorDamageReduction`（减伤）与 `ArmorBroke`（碎甲，仍是"甲从 >0 掉到 0 那一次"）**一行没动**；
>    `BarBoss.tscn` / 首领没动（0 甲 → 依然天然不显示）。
>    ⚠️ **必然的行为变化**：本来 0 甲的敌种精英化后**真的多出 120 甲**（旧实现只是把上限抬到 120、甲仍是 0）。
> 2. **C2**：候选集**原来在"第一次交互时"才建**（四个惰性分支，其中两个忘了设 `SummonerAllowed`）→
>    改成 **`RunController.InitializePool()`，只在进关卡的 `ApplyBalance()` 里调一次**；
>    交互时只从定好的池里抽（仍然按当前构筑过滤互斥/层数/节点 `Kind`）。
>    **重复的两个进化池节点行合并**：删 `node_weapon.tres`（`Kind = Weapon`，场景无实例、`BossPressurePoints` 是 0），
>    保留 `node_evolution.tres`；`ConfigIndex.tres` 的 `[ext_resource]` 与 `Nodes` 数组项**同一次**删；
>    `NodePlacement.LayerOf` 兜底补 `Weapon => 2`（否则 `Weapon=4` 会掉到 1）；
>    `Config/Tables/UpgradePools/` 三个按攻击方式分的池**保留不动**。**`Id`/类/目录一律没改名。**
> 3. **硬指标**：除 **`配置表比对·索引` 的 `节点 9 → 8`**（合并 = 少一个行文件，必然结果）之外**逐位一致** ——
>    `节点布置 10（高 2/中 5/低 3）`（**它数的是场景实例，不是表行**）、`Pressure=3`、`端到端 F`、`音效 13`、
>    `增幅项 19 条 / 池子 18 条`、`价值标签 → 节点 [Weapon=4 …]` 全部照旧；日志无 `ConfigIndex.tres` 加载失败。
>
> **数据改了哪 18 个文件**：行文件 14（`Enemies/` 9 + `Bosses/` 1 + `Summons/` 1 + `Characters/` 3，
> 含三个角色行的 `DescriptionTemplate` 占位符）+ 场景 4（`Guard` / `Charger` / `SkillElite` / `TrainingDummy`），
> 外加**删除** `Config/Tables/Nodes/node_weapon.tres`。全部 **LF + 无 BOM**。
> **`ConfigIndex.tres` 里本来没有护甲字段**（C1 完全没碰它），C2 才碰了它的 `Nodes` 数组。
> 细节与两侧实测见 [config.md](config.md) 的「护甲无上限 + 进化池合并实测」。

### 任务 F：用户三项修正（护甲只给精英 / 行为型改称技能 / 命中特效落在命中点）✅ 已完成（2026-10）

> **用户原话**：②「**护甲目前就只给精英怪使用就好了，首领不需要**」、
> ④「**行为型的词条其实不太对，这个应该是算是技能**」、
> ⑤「不需要（细分开关），**实际应该在命中的位置发生特效**」。
>
> **做了什么**：
> 1. **`pmod_fortress_armor`「铁壁·护甲」整行下线**（Pressure **8 → 7 条**）：行文件 +
>    `ConfigIndex.tres` 的 `[ext_resource]`/数组项 + **`BossPerkCatalog.FallbackPressure` 里的代码兜底**
>    —— 三处同一次改（只删表行 = 表读不到时首领又吃护甲，静默换挡）。
>    ✅ **精英护甲不受影响**：它来自 `Scripts/Combat/EliteModifier.cs`（`ArmorAdd = 120`），与 Pressure 无关；
>    改完 `MaxArmorAdd` 的生产者全项目只剩它一个。自测期望串同步 `…/25/8/…` → `…/25/7/…`。
> 2. **术语统一**：**行为型 = 技能**（`bhv_*.tres`，本来就是技能表的行）、**数值型 = 词条**。
>    改了 `BossPerkText`（新增 `Summary()`，HUD 那一行变成 `技能 2 · 词条 2 ◆… ◇…`）、`PlayerHud`、
>    `BossPreviewScreen`（拆成【技能】/【词条】两节）、`BossPerkCatalog` 的日志与注释、
>    `AbilityConfig` 的 `[ExportGroup]` 标题与若干注释 + 四份文档。
>    **没有**动任何 `bhv_*` 的文件名 / 目录 / 类名 / 字段名；`◆` / `◇` 两个标记保持不变
>    （它们本来就区分来源，问题在"叫法"）。
> 3. **命中特效落在实际命中点**：`AttackHitbox` 用**目标碰撞体表面上离命中体最近的那一点**
>    （`ResolveHitPoint`：胶囊/球/盒三种形状 + 碰撞体中心兜底），取代"目标原点 + 全局高度偏移"；
>    **`HitEffectHeightOffset` 整列删除**（字段 + `ConfigIndex.tres` 那行）。投射物那条路径**一行没动**。
>    ⚠️ 第一版实现是"打一条射线取接触点"，自测里 7 次跑出 3 次**静默打空**（射线读物理空间在查询那一刻的
>    状态：目标被脚本挪动 / 刚被冻住 / 刚被打死都会打空）→ 改成几何算法，两次连跑数值逐位相同
>    （出生点到碰撞体表面偏差 **0.026 m**，旧做法基线 **0.4 m** = 埋在身体里）。
>
> **证据**：`build.ps1 --no-incremental` **0 warning / 0 error**；自测两次连跑
> （`run_20261012_003652.log` / `run_20261012_003734.log`）全程通过并自动退出、`selftest.flag` 跑完即删；
> 索引行 `Pressure 7`、池明细 7 条无护甲、概率 23.1/23.1/15.4/15.4/7.7×3；探针的合成 `StageIds` 判定仍生效。
> 详见 [config.md](config.md) 的「用户三项修正实测」与 [boss.md](boss.md) 的「Boss 构筑（技能 + 词条）」。
>
> **⚠️ 留给下一轮的两个决定**：① ~~首领**本体的 200 基甲**没动~~ → ✅ **已去掉（2026-10 再一轮，用户明确要求「去掉首领的甲」）**：
> `boss_final.tres` 的 `MaxArmor = 0` + `Boss.tscn` 那份同值导出删掉；**护甲字段全部保留在基类上**
> （用户澄清：护甲是所有单位通用的属性，首领只是"没有获取手段"）。判据/管线走向见
> [config.md](config.md) 的「首领去甲 + 单位动画实测」§一。**遗留**：首领有效血量因此明显下降，
> 要不要补偿调参（`MaxHealth` 或 `BossHealthMultiplier`）是数值决策；
> ② `Scripts/Run/StageController.cs` 里两条日志仍写着"Boss Build **行为词条**"
> （受"只改数据不改算法"的边界约束没动，建议单独一次只改字符串的提交）。
>
> ✅ **同轮还做完了**：**单位动画系统**（`Scripts/Combat/UnitAnimator.cs`）—— 玩家 + 10 敌人 + 首领
> 按移速在 `idle`/`walk`/跑 之间切、死亡播 `die`；参数是 `UnitConfig` 新增的「表现 · 动画」6 列
> （**空 / 0 = 不覆盖**），新敌人不挂节点、不写代码、不填表也能动（`UnitPresentation._Ready` 会 `Ensure()` 补挂）。
> **边界**：`RunController.ApplyBalance` 不调 `ApplyTo`，所以**玩家那 3 行（`chr_*.tres`）的动画列当前不生效**
> —— 一行修法（`character.ApplyAnimationTo(_player);`）与证据见 [art-integration-log.md](art-integration-log.md) §11。

### 任务 E：Pressure 词条"一参数一栏"（3→8 条）+ 技能参数入池 + 限定词刷新判定 ✅ 已完成（2026-10）

> **用户要求**：① 3 条应该拆成 5 条（**每个参数单独占一个词条栏**）；② 把一些**技能的参数**也加入词条；
> ③ 每个 boss 可能都不同，**限定词记得做好刷新判定**。
>
> **做了什么**：
> 1. **数值型 3 → 5 条**（`pmod_berserk` → 伤害/攻速两条，`pmod_fortress` → 生命/护甲两条，`pmod_swift` 不变）+
>    **3 条技能参数词条**（尖啸回响 / 冲锋冲击 / 召唤急速）= **8 条**。权重**继承母行**（3/3/2/2/2）+ 技能型各 1。
> 2. **技能参数进 `Pressure/`（数值型）而不是 `Skills/`**：依据 §7.5 明列「技能冷却等参数」属数值型；
>    `PressureModConfig` 多 `AbilityNode` / `AbilityScales` / `AttackScales`，`Value` 是**倍率**（可叠乘）。
> 3. **限定词 + 刷新判定**：`PerkQualifier`（`BossIds` / `StageIds` / 必须有技能节点）+ `BossPerkCatalog.ApplyWithRefresh`
>    —— 判定在 **Boss 出生时**（`perk.Apply` 里），不成立就**换抽**；`StageController` / `EncounterController` **一行没改**。
>
> **⚠️ 需要知道的两件事**：
> - **每抽的期望强度降到原来的 58.9%**（"一抽只买一个参数"的必然结果，且**调权重补不回来**）——
>   要补只能提高单条数值或降阈值，**这是一轮平衡测试的入口**。
> - 旧行文件 `pmod_berserk.tres` / `pmod_fortress.tres` **已删除**（与 `ConfigIndex.tres` 的数组项 + ext_resource 同一次改动）。
> - 行为型词条（`bhv_*`）**还没有**限定词列（要给 `AbilityConfig` 加列才行，本轮没做）。
>
> 证据：`build.ps1 --no-incremental` 0/0；自测 `+187.5 → 1` / `+225 → 3` **逐位不变**；
> 真实出生实测有"抽到铁壁·护甲 → 出生时判不成立 → 换抽狂暴·伤害 → 护甲停在 200"的完整链条；
> 回退（清空 `PressureMods`）跑满一轮，池明细与表在时逐字相同。
> 详见 [config.md](config.md) 的「配置表阶段 7 实测」与 [config-schema.md](config-schema.md) §5 阶段 7。

### 任务 D：§8.2 免费挑战闸门 + HUD 词条 + 首领状况预览面板 ✅ 已完成（2026-10）

> **做了什么**（三件事，用户一次提的）：
> 1. **§8.2「免费挑战不加 Pressure」闸门接上了**：`StageController.AddPressureForNode` 第一件事读
>    `CurrentChallengeFree`（= 挑战数 ≤ 本关免费额度，额度在 `StartEncounter` 里随挑战数扣，§8.2 第 1–2 步），
>    免费挑战直接返回 —— **不累积 Pressure、也不抽数值强化**（连节点行的"额外抽几次"一起拦）。
>    ⚠️ 顺手修了一个**真 bug**：`StageController._currentNodeKind` 从来没被赋值过，
>    所以**正常游玩时 Pressure 一次都不会累积**（只有自测直接调 `AddPressureForNode` 才有数字）。
>    现在 `StartEncounter` 里写 `_currentNodeKind = node?.Kind`（证据 = 自测端到端那行的 `node=Enhancement`）。
> 2. **HUD 血条栏显示词条名（两套来源都显示）**：新增 `Scripts/UI/BossPerkText.cs` 统一文案，
>    `◆` = 行为型（`EncounterController.BossPerks`）、`◇` = 数值型（`StageController.PressurePerks`）；
>    同名折叠成 `名字×N`、超过上限折叠成 `…+N`（血条栏 5 个 / 顶部状态行 4 个）。
>    节点在 `Scenes/UI/Bars/BarBoss.tscn` 的 `Panel/PerkLabel`。
> 3. **首领状况预览面板**（快捷键 **B**）：`BossPreviewScreen` + `Scenes/UI/BossPreviewScreen.tscn`，
>    走 `OverlayScreen`（分层 `Panel`）/ `UiService.OpenBossPreview()` / `UiRouter`，**没有绕过任何一层**。
>    内容 = 免费额度与闸门状态 / Pressure 阈值·跨阈值次数·余数·转换池候选与权重 / 两类词条逐条（带说明）/ 首领当前数值。
>    正文由静态只读的 `Describe(stage, encounter)` 生成，自测不开界面就能断言。
> 4. **自测**：`SelfTest.ReportPressureGate()` 替换了原来直接调两次 `AddPressureForNode` 的写法 ——
>    先验"免费挑战 = 0"（新增硬指标），再用 `DebugExhaustFreeQuota()` 把额度用掉，之后 `+187.5 / +225` 逐位不变。
>    细节见 [config.md](config.md) 的「免费挑战额度（§8.2）」。
>
> **还没做**：预览面板只有快捷键（用户拍板先这样，后期与词典图鉴合并）；交互时不会自动弹面板，
> 只在 HUD 的交互提示里加了 `[B 首领状况]`。

### 任务 A：血条改事件驱动（用户明确要求）✅ 已完成（2026-10）
> 实现：`Scripts/UI/WorldOverlay.cs`。已订阅 `GameplayEventBus.Raised`（`_Ready` += / `_ExitTree` -= 成对），
> `Damaged` → O(1) 登记并建血条，`Died` → 移除，`_Process` 只更新已登记血条、不再遍历单位组。
> 实测数据见 [config.md](config.md) 的「血条事件驱动」验证条目。
> **注意**：若一次伤害直接致死，`Damaged` 与 `Died` 在同一调用栈内先后触发，血条会「建好又立刻拆掉」，
> 所以用秒级轮询 `Bars` 子节点数是看不到血条的 —— 这是正确行为。

**问题**：`Scripts/UI/WorldOverlay.cs`（195 行）在 `_Process` 里**每帧遍历** `enemy_units` 和 `boss_units` 两个组来画头顶血条。

**要求**：血条应该是**事件通知** —— 扣血后通知，而不是遍历（除非用 ECS，本项目不是）。

**做法**：
1. 在 `_Ready` 订阅 `GameplayEventBus.Raised`，在 `_ExitTree` 退订（**必须配对**，项目里其它订阅都做到了）。
2. `GameplayEventKind.Damaged` → 若目标是敌方单位（`Faction != Faction.Player`），**O(1)** 登记它并创建血条。
3. `GameplayEventKind.Died` → 移除对应血条。
4. 满血的单位移除血条（保留现有的"满血不显示"设计）。
5. `_Process` 只更新**已登记**的那几个血条的位置/血量（通常 0–5 个），**不再遍历组**。
6. 保留现有能力：首领血条由 `PlayerHud` 负责（屏幕上方），`WorldOverlay` 只画普通怪**脚下**血条；`isBoss` 的单位在这里要跳过。

**验收**：
- 构建 0 warning / 0 error
- 跑自测：战斗中日志无异常；用 `await_runtime_condition` 读 `get_node("/root/Main/WorldOverlay").get_child_count()` 这类值确认没有每帧增长
- 视觉：跑起来后敌人受伤会出血条，满血/死亡后消失（可用 `runtime screenshot` 截图确认）
- `docs/architecture.md` 或 `docs/config.md` 里补一句"血条是事件驱动"的说明

### 任务 B：修「移动残影」 ✅ 已完成（2026-01）

**问题**（用户实测）：角色移动、跳跃、滑铲时**整个角色在剧烈缓动**，能看到一层不明显的残影。

**真实原因**（与交接文档给的三个方向**都不同**，靠逐帧探针测出来的）：

1. **主因 —— 物理 60Hz / 渲染 120fps 不同步，且没开物理插值。**
   角色的 `CharacterBody3D` 只在物理帧上移动，而渲染 120fps：实测 301 帧里 **150 帧（49.8%）位移为 0**，另外 150 帧每帧位移 0.114（= 两帧的路程）。肉眼就是「停一下、冲一下」。*跟 `UnitPresentation.cs` 无关*：那个文件只做受击闪色与延迟销毁，**没有任何位置插值**。
2. **帮凶 —— `CameraRig` 的跟随写在 `_Process` 里。**
   镜头每个渲染帧都在平滑移动（σ=0.057，单帧位移在 `0 ↔ 0.1167` 之间交替），而被跟随的角色只有一半的帧在动，两者错拍 → 角色/镜头相对位置每帧抖 ±4cm，角色在画面上「一进一退」，这正是"残影"的来源。
3. `CharacterMovement` 的 `_inputVelocity.MoveToward(...)` 与 `FaceDirection` 的 `LerpAngle` 是**正常的加减速/转身缓动**，不是 bug，保留。

**修复**：
- `project.godot` → `[physics] common/physics_interpolation=true`
- `Scripts/Camera/CameraRig.cs` → 跟随从 `_Process` 移到 `_PhysicsProcess`

**验证数据**（`SelfTest` 表现采样阶段，按住 W 前进 301 帧、120fps）：

| 指标 | 修复前 | 修复后 |
|---|---|---|
| 角色位移为 0 的帧 | 151 / 301（50.2%） | **0 / 301（0%）** |
| 角色单帧位移 标准差 | 0.0579 | **0.0058**（↓10×） |
| 相机单帧位移 标准差 | 0.0572（0↔0.1167 交替） | **0.0092** |
| 角色↔相机相位差 标准差 | 0.0112 | **0.0072** |

截图：`docs/screenshots/ghost_after_1..3.png`（移动中连拍三张，角色轮廓单层、屏幕位置稳定）。
复现方法：`SelfTest` 的表现采样阶段（第 11~12 阶段）会打这些数字；临时去掉 `physics_interpolation` 即可回到修复前的数值。

### 任务 C：清理与文档同步 ✅ 已完成（2026-01）

1. 已删除 `Scripts/Run/KillPlane.cs`（连 `.uid`）—— 确认全项目无引用（`KillPlaneY` 是平衡配置，仍被 `MapManager.EnsureKillZone()` 使用，保留）。
2. `docs/controller-flow.md` 已更新；`docs/config.md` 的区域杀一行也改成「所有单位进入即杀」。
3. `docs/` 与代码不一致处（本次发现并已修）：
   - `stages.md` 还写着「`ActiveNodeCount` 从 3 提到 6 / 实际启用 5 个」—— 实际 `ConfigIndex.tres` 是 `RewardNodeCount = 3/4/4`，自测实测启用 **10 个（2/5/3）**。
   - `stages.md` 的实测表还写着「剩余节点 3 / 免费挑战 2」，与当前「剩余节点 9 / 隔离次数 2」不符。
   - `config.md` 说「怪物血条改到脚下仍缺视觉确认」，实际 `docs/screenshots/healthbar_feet.png` 已有确认。


---

## 四、最近一轮已完成的工作（供参考，不要重复做）

- `PlayerLocator` 统一玩家引用；`Interactable` 交互判定 60Hz → 10Hz；`PlayerHud` 刷新 60Hz → 20Hz
- 区域杀从"每帧遍历 4 个组查 Y"改为**碰撞区域** `KillZone`（进入即杀）
- `OverlayScreen` 加分层（`UiLayer`）与开合动画；`UiRouter` 按层决定暂停
- 抽出 `RewardService` / `RunSettlement`（纯函数）
- `SelfTest` 移到 `Scripts/Testing/` 并加 `#if DEBUG`
- 基地场景 `Scenes/Levels/Base.tscn`（4 个交互台 + 出击门），主场景已切到基地

**已知未解决**：`MapManager` 的**运行时导航烘焙始终得到 0 个多边形**（`TestArena.tscn` 里的 `NavigationRegion3D` 也是空数据）。所有点位供给都有兜底路径，所以不影响玩法，**暂时不管**。

---

## ⚠️ 编辑器构建失败 / MCP 起不来（2026-10-12 实测确认）

**现象**
- 编辑器 Problems 报：`无法解析 SDK "Godot.NET.Sdk/4.7.2"` / `找不到指定的 SDK`
- `run_project` 报：`Play was requested but no debugger session became active` 或
  `The scene likely failed to load — check ProjectSettings application/run/main_scene`
- `runtime info` / `runtime screenshot` 报：`no_active_sessions`

**根因（用户实测结论 —— 不是代码问题、不是 SDK 没装）**

**有子代理在同时运行时就会出现。** 多个 Godot 实例并发会抢编辑器的构建回调：
编辑器 `play_custom_scene` 触发 C#「运行前构建」，而它在 deferred 上下文里建
ProgressDialog 失败：

```
Do not use progress dialog (task) while flushing the message queue
```

→ 构建回调失败 → 没有 debugger session → 运行/截图全都拿不到。

**解法**
1. **重启编辑器**（大概率解决）—— 关掉编辑器进程后重新起：
   ```powershell
   & 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe' `
     -e --path 'F:\Godot项目\项目a' --mcp-port=9080
   ```
   ⚠️ **若当时仍有代理在跑，重启后仍会失败** —— 这是预期的，等代理收工再重启。
2. **任何时候都可靠的绕过：命令行直跑 + 读日志**（不依赖编辑器与 MCP）
   ```powershell
   $godot = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe'
   '1' | Set-Content 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'
   Start-Process $godot -ArgumentList '--path','F:\Godot项目\项目a','res://Scenes/Main.tscn'
   Start-Sleep -Seconds 27
   Remove-Item 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'  # 必须删
   # 日志：C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\logs\
   ```
   ⚠️ **必须带场景参数** —— 主场景是基地，不带会进基地（自测会因为找不到 `Main/Stage` 而输出一堆空值）。

**给并行代理的纪律**
- 跑验证**优先用命令行直跑**，不要依赖 MCP 运行时桥
- **`stop_project` 是全局的**，会杀掉别的代理正在跑的自测实例 —— 不要随便用
- 设置 `selftest.flag` 的窗口要尽量短（它会影响那几秒内**任何**启动的游戏实例）
- 若必须用编辑器：**用完把自己的编辑器实例关掉**

---

## ✅ 编辑器 / MCP 的正确用法（2026-10-15 用户实测确认，**推翻了之前的结论**）

### 真正的根因

之前记录的「编辑器构建失败 / MCP 起不来」，**根因是 Godot 编辑器被关掉了** ——
**不是代码问题、不是 SDK 没装、也不是"子代理并发"**。

**用户实测**：**编辑器开着 → MCP 自动可用**（不需要额外配置）；
编辑器关掉时，代理只能退回"命令行直跑"，于是**每次都要启一个 Godot 进程** →
**慢，而且反复弹控制台窗口**。

### 代理应该怎么做

1. **先查编辑器在不在**：
   ```powershell
   & 'F:\Godot项目\项目a\.gdmcp\bin\gdmcp.exe' doctor     # 看 editor_connected
   ```
2. **不在就自己开一个**（不要干等、也不要一上来就命令行直跑）：
   ```powershell
   & 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe' `
     -e --path 'F:\Godot项目\项目a' --mcp-port=9080
   # 等约 25 秒，再 doctor 确认 editor_connected: true
   ```
3. **然后优先用 MCP**：
   ```powershell
   & $cli tool-call run_project --args-json '{"allow_window":true,"scene_path":"res://Scenes/Main.tscn"}' --apply
   & $cli runtime screenshot --format png --save-path 'user://x.png'
   & $cli runtime info
   ```
   —— **比命令行直跑快得多，也不会反复弹控制台窗口。**
4. **若 `run_project` 仍报 `no debugger session` / 构建回调失败**：
   - 先**再试一次**；仍失败就在编辑器里手动"构建项目"一次，再试
   - **最后**才退回下面的命令行直跑
5. ⚠️ **用完不要关掉编辑器**（别的代理还要用）；⚠️ **也不要 `stop_project`**（它全局生效，会杀掉别人正在跑的自测）

### 命令行直跑（**兜底**，编辑器/MCP 实在不可用时才用）

```powershell
$godot = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe'
'1' | Set-Content 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'
Start-Process $godot -ArgumentList '--path','F:\Godot项目\项目a','res://Scenes/Main.tscn'
Start-Sleep -Seconds 40
Remove-Item 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'   # 必须删
# 日志：C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\logs\
```
⚠️ **必须带场景参数**（主场景是基地；不带会进基地，自测输出一堆空值）。
⚠️ `-- --char=xxx --weapon=yyy` 这类旁路参数只在需要绕过选题界面时用。
