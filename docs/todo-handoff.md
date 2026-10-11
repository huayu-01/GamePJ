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

- 数值一律放 `Config/Tables/<分组>/*.tres`（一行一个文件，索引挂在 `Config/Balance.tres` 的 `[Export] XxxConfig[]` 数组上）或 `Balance.tres` 的全局字段里，**不要硬编码在逻辑里**。见 [config.md](config.md) 的"调参去哪张表"。
- **表是权威、代码是回退**：读不到表就用组件/行类自身的 `[Export]` 默认值（`Balance.FileLoaded` + 每个系统的"来源：表/默认值"日志能证明走的是哪条路）。
- ⚠️ **不要单独删/改 `Balance.tres` 引用到的行文件** —— 一个行文件缺失会让**整份** `Balance.tres` 加载失败、所有数值静默回退。要下线一行，把数组项与对应的 `[ext_resource]` 一起删。
- 玩家可见文案改动 → 同步更新 `docs/`。
- 日志用 `ActRoguelike.Core.GameLog.Write("分类", "消息")`（分类：Flow / Node / Reward / Spawn / Combat / Map / **Config**）。
  ⚠️ **`Balance` 的 `_Ready` 里不能直接写日志** —— `GameLog` 的 autoload 排在它之后，写进去会被静默丢掉。`Balance` 用 `CallDeferred` 绕过这一点。
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
- `Balance.Settings` —— 所有数值（**唯一数值门面**；实际数值在 `Config/Tables/<分组>/` 的分表里，见 [config.md](config.md)）
- `ConfigRows.ById<T>()` / `BySlot<T>()` / `ByKind<T,TKind>()` —— 读配置表的唯一入口（读不到 → `null` → 调用方回退到自身 `[Export]` 默认值）

**UI 框架现状**：`OverlayScreen` 已支持分层（`UiLayer.Hud/Panel/Modal`，决定是否暂停）、防误触、开合动画（`FadeSeconds` / `OpenScaleFrom`）。
界面清单与所属层见 [config.md](config.md) 的「界面与弹窗」；**新增界面请走 `UiService` + `UiRouter`，不要自己 new / 找节点路径**
（例：首领状况预览 = `BossPreviewScreen` + `UiService.OpenBossPreview()`，快捷键 B，分层 `Panel`）。

**详细流程**见 [controller-flow.md](controller-flow.md)，架构与坑见 [architecture.md](architecture.md)。

---

## 三、待办任务（本次交接的重点）

### 任务 0：配置表体系阶段 2 —— Balance 拆成 8 张表 ✅ 已完成（2026-10）

> **做了什么**：`BalanceConfig` 从 74 字段降到 30 个全局字段；新增 8 张表（武器 3 / 角色 3 / 敌人 1 /
> **效果 10** / 节点 9 / 增幅项 19 / 局外成长 5 / 关卡 3，共 **53 个行文件**），全部"一行一个 `.tres`"，
> 索引挂在 `Balance.tres` 的 `[Export] XxxConfig[]` 数组上。
> **没有做逐行回退**（保持资源引用数组）—— 但**不要单独删/改行文件**，见下面那条纪律。
> `Balance.Settings.Xxx` **仍然可用**（渐进迁移，没有大爆炸式重构）。
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
   - `stages.md` 还写着「`ActiveNodeCount` 从 3 提到 6 / 实际启用 5 个」—— 实际 `Balance.tres` 是 `RewardNodeCount = 3/4/4`，自测实测启用 **10 个（2/5/3）**。
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
