# 动画管线（技能动画：生成 → 加载 → 事件帧 → 技能）

> **本轮范围**：动画管线地基 + 角色 A［猎手］武器 1「能量掌」的最小闭环。
> **总纲**：`docs/refactor-survey.md`（§1 动画普查 / §5 阶段划分）；**需求权威**：`docs/character-redesign.md`；
> **设计权威**：`F:\Godot项目\3d肉鸽雨中哈迪斯\角色设计.txt`。
>
> 本轮**只加不删**：旧的 3 角色 / 3 武器 / 19 条增幅 / 25 条技能行全部保留，新内容全是**新增行文件**。
> ⚠️ **2026-10-15 P6a 起解禁**：上面那些旧行**已经清掉**（"只加不删"只是 P1→P5 的阶段纪律）。
> 本文件 §7 的"旧硬指标对比"记的是**当时**的读数，保留作历史；当前值见
> [character-redesign.md](character-redesign.md) §六。

---

## 1. 一条命令的用法（后续 6 把武器照这个做）

**加一段技能动画 = 加一行表 + 跑一次生成器，不改任何 C# 代码。**

```powershell
# ① 在 Config/Tables/SkillAnimations/ 下加一行 .tres（照 anim_a1_atk1.tres 抄）
# ② 在 Config/ConfigIndex.tres 里加 [ext_resource] + SkillAnimations 数组项 + 改 load_steps（三处缺一不可）
# ③ 生成：
& 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' `
  --headless --path 'F:\Godot项目\项目a' --script res://Scripts/Editor/GenerateSkillAnimations.cs
# 或编辑器菜单：项目 → 工具 → 生成技能动画
```

生成产物：`Assets/Animations/skills/<动画名>.tres`（`AnimationLibrary` 的命名库 `skills`）。

### 1.1 一行技能动画表（`SkillAnimationConfig`）填什么

| 列 | 说明 |
|---|---|
| `SourceModelPath` | 源模型 `.glb`（当前所有角色共用 `Assets/Models/player/mesh_player_body.glb`） |
| `SourceAnimation` | 源片段名（32 段之一：`holding-right-shoot` / `attack-melee-right` / `interact-right` / `pick-up` …） |
| `TargetName` | 目标动画名（`Skill_A1_ATK1`） |
| `Library` | 命名库名（当前统一 `skills`）→ 查名要写 **`skills/Skill_A1_ATK1`** |
| `OutputDir` | 生成目录（`res://Assets/Animations/skills/`） |
| `Length` | 目标时长（0 = 用源片段长度）；**拉长会等比缩放事件帧时刻** |
| `EventNames` / `EventTimes` | 事件帧名与时刻（`["ATK"]` / `[0.08]`），下标一一对应 |
| `EventTargetPath` | 方法轨道 path（生成器会按真单位层级**标定**，见 §4 坑 2） |
| `EventMethod` | 事件方法名（`anim_event`） |

### 1.2 一行技能表（`AbilityConfig`）新增的列

| 列 | 说明 |
|---|---|
| `InputAction` | **按键动作名**（`weapon_skill_1` / `weapon_skill_2` / `skill_1` / `skill_2`）——**本轮推翻了"刻意没有 InputAction 列"的旧决定** |
| `HoldToCharge` | 按住式输入（蓄力类技能）：按住每帧回调、松开回调一次 |
| `AnimName` | 这个技能要播的技能动画（**填了就必须存在，否则技能不执行**） |
| `AnimEvent` | 等哪个事件帧才释放效果（`ATK` / `Dodge` / `Release` …）；⚠️ **蓄力技能里它只是"闭合"的动画标记，触发点是松手** |
| `AnimSpeedScale` | 动画速度倍率（>1 加速释放；蓄力类 <1 拉长。写在**每单位自己的播放器**上） |

> **P2 之后这一组还多了 9 列**（冷却缩减 / 层数 / 蓄力 / 完美时机：`CooldownMultiplier`、`MaxCharges`、
> `RechargeSeconds`、`ChargeBaseSeconds`、`ChargeStepSeconds`、`ChargeStepScale`、`ChargeMaxSteps`、
> `ChargeMaxSeconds`、`PerfectWindowSeconds`）—— 语义、默认值与"加一个蓄力技能要做什么"
> **见 [abilities.md](abilities.md)**（本文件只管动画那一段）。

### 1.3 单位的动画库目录（`UnitConfig`）新增的列

| 列 | 说明 |
|---|---|
| `AnimLibraryDir` | 这个单位的技能动画目录（`res://Assets/Animations/skills/`）；**空 = 没有技能动画** |
| `AnimLibraryName` | 命名库名（与目录名一致，当前 `skills`） |

`UnitAnimator` 在 `_Ready` 与首次使用时会把这个目录下的全部 `.tres` 收进本单位 `AnimationPlayer` 的命名库
（**每实例一份**），并设 `CallbackModeMethod = Immediate`。

---

## 2. 三个坑在**真项目里**的验证结果（`refactor-survey.md` §1.4）

自测里有一行专门打印这三条（`SelfTest` case 24 的「动画管线·三个坑」）：

```
动画管线·三个坑（真项目实测）：
① 全路径查名 —— HasAnimation(裸名 Skill_A1_ATK1)=False（期望 False）
              / HasAnimation(全路径 skills/Skill_A1_ATK1)=True（期望 True）；
② 方法轨道 path —— AnimationPlayer.RootNode='..'（解析到 'Model'），轨道 path 写 '..'；
③ 回调模式 —— CallbackModeMethod=Immediate
```

| 坑 | 结论 | 本项目怎么处理 |
|---|---|---|
| **1. `HasAnimation` 不搜嵌套库** | ✅ 复现（裸名 False / 全路径 True）。**既有 `UnitAnimator.cs:235` 确实命中这个坑** | 技能查名统一走 `SkillAnimationLibrary.ResolveExact`（内部先拼 `skills/<名>`）；`UnitAnimator.Resolve`（移动/死亡那几段）仍然查全局库的裸名，两条路互不影响 |
| **2. 方法轨道 path 相对 `AnimationMixer.RootNode`** | ✅ 复现。项目里 `root_node='..'` → 解析到 `Model`，所以轨道 path 再写 `'..'` 才落到 `Unit` | **生成期标定**：生成器实例化 `Player.tscn`，算出"从模型根往上走到事件接收节点要几级"，把结果写进轨道 path（见 §4） |
| **3. 事件默认 Deferred** | ✅ 复现 | `SkillAnimationLibrary.LoadInto` 在加载库时设 `CallbackModeMethod = Immediate` |

**另外**：`UnitAnimator.SetLoop()` 改共享资源的隐患**已修**——只在值真的不同时才写，且技能动画的循环标记在**生成期**写进各自的 `.tres`（每单位独立），不再互相踩踏。

---

## 3. 事件帧怎么驱动（**本轮的重要实现决定**）

授权格式**仍然是 Godot 的方法轨道**（生成器照旧写 `anim_event` + args，`.tres` 里能看到完整关键帧）。

但**运行期不经过引擎的方法轨道回调**，改由 `UnitAnimator` 自己读关键帧、在**播放位置跨过它的那一帧**触发
（`UnitAnimator.DriveSkillEvents`）。

**为什么**（实测，Godot 4.7.2）：

- 技能动画的方法轨道**一次都不会落到目标节点上**，并且会在混合器内部**直接让进程崩掉**
  （`exit -1073741819` = 访问违规）；同一份 `.tres` 在临时工程里能正常触发。
- 用 `MethodTrackGetName / MethodTrackGetParams` 读关键帧也会崩；改用 `Animation.TrackGetKeyValue`
  （取那个 Dictionary）之后就正常了 —— 所以读关键帧**必须走 `TrackGetKeyValue`**。

**语义完全一致**：都是"播放位置到点的那一帧触发"，跟着 `speed_scale` 走，当帧生效。
以后引擎修好了，把 `SkillAnimationLibrary.PrepareForRuntime` 里"禁用方法轨道"那一步去掉、
并让 `DriveSkillEvents` 不再兜底即可。

---

## 4. 生成器（`Scripts/Editor/`）

| 文件 | 职责 |
|---|---|
| `SkillAnimationGenerator.cs` | 核心：`Duplicate(true)` → 改名 → `AddTrack(Method)` → `TrackSetPath` → `TrackInsertKey` → `ResourceSaver.Save` → **回读校验** |
| `GenerateSkillAnimations.cs` | 编辑器菜单 `项目 → 工具 → 生成技能动画` + 命令行入口；跑之前先**标定事件帧路径** |

### 4.1 事件帧路径标定（必读）

方法轨道的 path 是相对 `AnimationMixer.RootNode` 的，而 `RootNode` 解析到的是**模型实例的父节点**
（项目里 `root_node='..'`，从 `AnimationPlayer` 看是 `Model`）。

所以生成器会：
1. 实例化 `Player.tscn`，找到它的 `Model`（模型根）与 `Animator`（事件接收节点）；
2. 数出"从模型根的父级往上走到 `Animator` 要几级" → 写成 `'..'` / `'../..'`；
3. **在 `Free()` 之前**把结论算成一个整数存下来（把节点封进闭包里会变成已释放对象，之后每次解析都抛
   `ObjectDisposedException`，然后**静默退回默认值**——这个坑实测踩到过）。

> ⚠️ **接收方法挂在 `Unit` 上**（`Unit.anim_event`），由它转发给 `UnitAnimator.RaiseAnimationEvent`。

---

## 5. 最小闭环（角色 A + 能量掌）

| 环节 | 位置 | 状态 |
|---|---|---|
| 按键 | `project.godot` 新增 `weapon_skill_1`(左键) / `weapon_skill_2`(右键) / `skill_1`(Q) / `skill_2`(E) | ✅ 旧动作全部保留 |
| 表驱动输入 | `PlayerAbilityInput`（读 `AbilityConfig.InputAction`，**不再按 C# 类名硬编码**） | ✅ |
| 技能 → 动画 | `Ability.TryActivate` 先**全路径查动画**，查不到 → **不执行** + 明确日志 | ✅ |
| 事件帧 → 发射 | `EnergyWaveAbility.OnAnimEvent`（`ATK`） | ✅（见 §6 遗留） |
| 子弹 | `Scenes/Combat/EnergyWave.tscn`：直线、`GravityScale=0`（无下坠） | ✅ |
| 穿透格挡 | `AttackInstance.BypassBlock` → `HitInstance.BypassBlock` → `Unit.ResolveBlock` 直接返回 1.0 | ✅ |
| 能量掌武器行 | `wpn_energy_palm.tres`（新增行，Slot 4） | ✅ |
| 角色 A 行 | `chr_hunter.tres`（新增行，Slot 4，只挂两个武技） | ✅ |
| 武技1 行 | `skl_a1_w1_1.tres`：CD 0.5 / 伤害 10 / 弹速 150 / `AnimName=Skill_A1_ATK1` / `AnimEvent=ATK` | ✅ |
| 武技2 行 | `skl_a1_w1_2.tres`：CD 1 / 蓄力 0.5s 完美时机 / 伤害 30→40 / 弹速 250→400 / `HoldToCharge` / `AnimSpeedScale=0.2` | ✅ |

**怎么直接玩到**（选题界面当前只列旧 3 把武器，所以给了一条命令行入口）：

```powershell
Godot_v4.7.2-stable_mono_win64.exe --path 'F:\Godot项目\项目a' res://Scenes/Main.tscn -- --char=chr_hunter --weapon=wpn_energy_palm
```

按表行 Id 选（不是索引）——这正是"加角色/武器不改代码"该有的样子。

> ✅ **这条命令行入口现在已经可靠了**：以前它会在"基地已经选过一局"时静默失效
> （`RunSetup.HasSelection` 那条分支抢在 `BeginConfigure` 之前 `return`），
> 现在 `TryApplyLoadoutOverride()` 判在它**前面** —— 根因、修法与实测见 §6.4.1。

---

## 6. ✅ 本轮结论（2026-10-15）：**两个"引擎崩"都是误判**，最小闭环已跑通

### 6.1 崩因查清：不是"播动画让引擎崩"，是**取证代码误用引擎 API**

| 上一轮的说法 | 本轮实测结论 |
|---|---|
| ① "播运行期加进库的技能动画会让引擎崩"（`exit -1073741819`） | ❌ **误判**。真正崩的是 `SelfTest.ReportSkillLoop()` 里用 **`Animation.MethodTrackGetParams()`** 读方法轨道关键帧 —— 这个调用本身就让进程崩（调用栈 `MethodTrackGetParams → ReportSkillLoop → _Process`），而它恰好落在 case 27（技能动画刚播完那一帧），于是被当成"播动画把引擎搞崩"。全项目 grep 后改成**唯一安全的读法 `Animation.TrackGetKeyValue()`**（取回 `Dictionary{"method","args"}`，与运行期 `UnitAnimator.ReadSkillEvents` 同一口径）→ **自测全程跑完、退出码 0**。 |
| ② "`Projectile.ResolveImpact` 的『是敌人单位』分支内部也崩" | ❌ **没复现**。修掉 ① 之后自测跑完（退出码 `0`），`ResolveImpact` 走"命中真敌人 → 扣血"这条路也不再崩。上一轮那两行 `K1/L1` 探针现场是被前一个崩溃打断的。 |

**证据**（`--selftest` 日志）：
```
最小闭环·关键帧取证：动画长度=1.333 当前=skills/Skill_A1_ATK1 pos=0.887 播放中=True speed_scale=1｜
  method=anim_event args=[ATK] 启用=False 时间=0.08 path='..'     ← 这行就是 TrackGetKeyValue 读出来的
=== 自测结束 ===                                                   ← 退出码 0
```
崩因面包屑 `user://logs/selftest_phase.txt` **保留**（本轮它就是把崩溃钉在 case 27 的工具）。

### 6.2 第二个真 bug：技能**收不到动画事件帧**（本轮修掉，否则闭环不可能成立）

`DebugSkipSkillAnimation` 改回 `false` 之后，自测立刻暴露：

```
[Animation] 技能动画开播：Player → skills/Skill_A1_ATK1（… 事件帧 1 个：ATK@0.08s）
[Combat]    ⚠ 技能 skl_a1_w1_1 等不到动画事件帧「ATK」，本次施法放弃
```

动画正常播、`UnitAnimator` 也真的发出了 `ATK` 帧（自测计数 `动画事件帧收到=1`），但**技能收不到** → 永远不发射。

**根因**：`Ability.EnsureInitialized()` 里那次订阅**只会发生一次**（`_animEventHookedByMe`），
而它被 `Ability._PhysicsProcess` 在**第一个物理帧**就调用了 —— 那一帧配置表还没套上来，
`AnimEvent` 还是空串，于是这次"唯一的机会"被判为"这个技能不需要事件帧"，**之后永远不再订阅**。
表的套用发生在 `_Process`（idle 帧）里的 `RunController.ApplyBalance → UnitConfig.ApplySkillsTo`，
**晚于第一个物理帧**。

**修法**（`Scripts/Abilities/Ability.cs`，**只加不删**）：把订阅抽成幂等的 `EnsureAnimEventHooked()`，
在 `EnsureInitialized` / `_PhysicsProcess` / `TryActivate` 三处都可以补订阅。

### 6.3 ✅ 最小闭环真的能玩：证据（真人手测 + 截图）

入口：`--char=chr_hunter --weapon=wpn_energy_palm`（MCP 的 `run_project` 传不了 user args，
改用 `RunController.BeginRunWithCharacter(Hunter, EnergyPalm)` 达到同一状态）。
**左键是真人真按**（`mcp__windows__` 的鼠标工具落到游戏窗口上）。

同一条日志窗口里三行连起来就是整条链路：

```
02:05:59 [Animation] 技能动画开播：Player → skills/Skill_A1_ATK1（speed_scale=1，循环=False，事件帧 1 个：ATK@0.08s）
02:05:59 [Combat]    能量波发射：skl_a1_w1_1 伤害 10 弹速 150（普通，无视格挡=True，穿透 0，方向 (0,-0.342,-0.9397)）
02:05:59 [Combat]    伤害 Player(94.8) → Chaser(35) 10        ← 敌人 45 → 35（正好是表里的伤害 10）
```

截图（`docs/screenshots/`）：

| 文件 | 证明什么 |
|---|---|
| `loop_shot_2.png` / `loop_shot_4.png` | **动画真的播出来**：角色处于"能量掌"出招姿态（手臂前伸），身前有能量特效 |
| `loop_shot_hit.png` | **命中掉血**：敌人紧贴身前、血条已掉，头顶飘出伤害数字 **10** |

复现步骤（下一步别人要重跑时照这个走）：见 §9。

### 6.4 ✅ 上一轮那 4 条遗留 + 1 个自测缺口：**本轮全部清掉**（2026-10-15 第二轮）

| # | 上一轮的遗留 | 本轮改法 | 状态 |
|---|---|---|---|
| 1 | `--char/--weapon` 在"基地已选过一局"时静默失效 | `RunController._Process`：`TryApplyLoadoutOverride()` **提到 `RunSetup.HasSelection` 判定之前**（原来它躲在 `BeginConfigure` 里，而 `HasSelection` 那条分支会先 `return`）；顺手补上两处早退漏掉的 `_boot = 2` | ✅ §6.4.1 |
| 2 | 左键会额外开一枪「0 伤害的手枪」 | `chr_hunter.tres` 的 `WeaponEnabled` `true → false`（该行 `Note` 的原意就是"不走 Weapon 组件的开火"） | ✅ §6.4.2 |
| 3 | 能量掌的瞄准口径与既有枪械不一致 | 统一成**"求准星世界点"**：新增 `EnergyWaveAbility.ResolveShotDirection()`，并把 `PlayerWeaponInput` 里那段内联求交**换成同一个函数** `AimUtil.GetCrosshairPoint`。飞行中仍然 `GravityScale = 0` | ✅ §6.4.3 |
| 4 | `SelfTest` 的「配置表比对·索引」期望串过期 | 期望串同步成 `4/4/10/1/1/27/7/9/8/6/3/11/8`（顺带把同类的「技能表 25 → 27」也同步） | ✅ §6.4.4 |
| 5 | 自测里「命中掉血」没覆盖 | 靶子换成**真敌人 `Chaser.tscn` 的实例**（按敌人表 `ScenePath` 取；AI 停掉、**体心压在弹道上**），并加一条**自测射线**独立取证 | ✅ §6.4.5 |

#### 6.4.1 `--char/--weapon` 的静默失效（修法 + 验证）

**根因**（与上一轮判断一致）：`RunSetup.HasSelection` 是**静态**的，从基地出击门进关卡时它已经是 `true`；
旧代码在 `_Process` 的 `_boot` 块里**先**判 `HasSelection` 并 `return`，
于是 `BeginConfigure`（唯一读命令行的地方）永远轮不到。

**改法**：把 `TryApplyLoadoutOverride()` 挪到 `_boot` 块里**最前面**（`MetaProgress.Load()` 之后）：

```
200: if (TryApplyLoadoutOverride())   ← 现在在这
210:     if (RunSetup.HasSelection)  ← 原来它排在前面、会先 return
278:     if (TryApplyLoadoutOverride())   ← BeginConfigure 里原来那一处保留（不冲突）
```

**验证**（真进程，两条分支各一次）：

```
# ① 带参数启动一次（HasSelection 初始为 false）→ 旁路立刻生效、不再走基地界面
12:31:33 [Flow] 命令行指定开局：角色 chr_hunter → Hunter / 武器 wpn_energy_palm → EnergyPalm
12:31:33 [Flow] 选择角色 Hunter / 武器 EnergyPalm

# ② 同一进程里重载 Main.tscn（= 新的 RunController，此时 RunSetup.HasSelection 已经是 true：
#    ① 里那次 TryApplyLoadoutOverride 自己写的）→ **旁路依然赢**
12:31:50 [Flow] 命令行指定开局：角色 chr_hunter → Hunter / 武器 wpn_energy_palm → EnergyPalm
12:31:50 [Flow] 选择角色 Hunter / 武器 EnergyPalm
```

对照：**旧代码**用同样的命令行启动时，`_boot` 块只开基地界面（日志里是 `ui_open`），**一行 `命令行指定开局` 都没有** ——
参数要等基地界面被关掉、`BeginConfigure` 被回调时才会被读到（这正是"基地已经选过一局之后失效"的机制）。

> 🧰 **怎么把"带 user args"和"MCP 可控"同时拿到**（本轮实测的新办法，`run_project` 传不了 user args）：
> 用编辑器同款的启动参数自己起一个进程，debugger 会挂到编辑器的 6007 端口，于是 MCP 认这个会话：
> ```powershell
> & 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe' `
>   --path 'F:/Godot项目/项目a' --remote-debug tcp://127.0.0.1:6007 `
>   res://Scenes/Main.tscn -- --char=chr_hunter --weapon=wpn_energy_palm
> ```
> （`doctor` 的 `runtime_running` 可能仍显示 false，但 `runtime info` 会给出 `debugger_active: true` 与
> `current_scene`，`evaluate_runtime_expression` 能用。）

#### 6.4.2 左键不再多打一发 0 伤害的手枪（修法 + 验证）

`chr_hunter.tres`：`WeaponEnabled = false`（`Note` 里补了原因）。真人按左键取证：

```
12:27:22 [Animation] 技能动画开播：Player → skills/Skill_A1_ATK1（… 事件帧 1 个：ATK@0.08s）
12:27:22 [Combat]    能量波发射：skl_a1_w1_1 伤害 10 弹速 150（普通，无视格挡=True，穿透 0，方向 (0, -0.0798, -0.9968)）
12:27:22 [Combat]    伤害 Player(178.8) → Chaser(35) 10
```

**只有这一条发射** —— 不再有上一轮那种 `伤害 Player(220) → Chaser(45) 0`（0 伤害的手枪弹）；
整份自测日志里 grep `伤害 Player(…) → … 0$` 也是**空**。

#### 6.4.3 瞄准口径统一（修法 + 验证 + **旧武器回归**）

- 新增 `AimUtil.GetCrosshairPoint` 的**唯一调用口径**：`PlayerWeaponInput`（枪械）与
  `EnergyWaveAbility.ResolveShotDirection`（能量掌）**调同一个函数**（mask = `World | UnitMask`、排除自己、
  没打中时取最大距离处 —— 与改前的内联代码逐字一致，只是抽成一处）。
- 能量掌仍然是**直线无下坠**（`GravityScale = 0`）：变的是**方向**（朝准星世界点），不是弹道形状。
  求交距离用表里的 `MaxAimDistance`（两行新增 `Params` 项，值 200，与枪械同一个默认）。

实测（真人左键）：

| 场景 | 上一轮 | 本轮 |
|---|---|---|
| 发射方向（能量掌） | `(0, -0.3420, -0.9397)` = **斜向下 20°**，从胸口射出 ~3m 落地 | `(0, -0.0798, -0.9968)` = **向下 4.6°**（朝准星世界点） |
| 命中距离 | 只有近身（1.5m）取证过 | **8.00m 命中**：`靶子 z=-0.859 / 玩家 z=7.141`（同 x）→ `伤害 Player(24.8) → Chaser(15) 20`（敌人 35 → 15） |
| 敌人正被准星指着时 | 射线原本会打到敌人身上 → 方向朝敌人躯干（实测有一次 `(0, +0.0586, -0.9983)`，略向上，正是"朝准星点"的表现） | 同 |

**旧武器回归**（这是本条唯一有风险的地方，因为动了 `PlayerWeaponInput`）：
选旧角色 + 旧武器（`BeginRunWithCharacter(0, 0)` = Gunner + Pistol）进关卡，把敌人摆在正前方 8m，真人左键：

```
12:30:15 [Combat] 伤害 Player(270) → Chaser(21) 24      ← 手枪：表里伤害 24，8m 命中
```

即**旧枪械照旧能打**（`Weapon.cs` 一个字没动；`PlayerWeaponInput` 的求交参数与改前逐字等价，
只是改成调用 `AimUtil.GetCrosshairPoint`）。

#### 6.4.4 「配置表比对·索引」：**它是"只打印"，不是校验**

`SelfTest.ReportTables` 里那一行是一条 `GameLog.Write`：实际值（`ConfigRows.Count<T>()`）与括号里的期望值
**拼在同一个字符串里**，没有任何 `if`/断言 —— 所以期望串过期**不会**让自测失败，上一轮也**没有**"静默漏过一次校验"
（本来就没有校验）。本轮把期望串同步成实际值（并注明"只打印、不比对"），顺带把同样过期的
`配置表比对·技能表：25 → 27`（`5 玩家 → 7 玩家`）也同步。

#### 6.4.5 自测里「投射物命中真敌人 → 掉血」（本轮补上）

**改法**：`SelfTest.BeginSkillLoopProbe` 不再自造 `Unit` + 手工挂胶囊，改成
**按敌人表 `ScenePath` 实例化真敌人 `Chaser.tscn`**：
1. `ConfigRows.ById<EnemyConfig>("enemy_chaser").ScenePath` → `Instantiate()`（**表是权威**，没有硬编码路径）；
2. 停掉这个实例的 `EnemyBrain`（真敌人会朝玩家跑，跑出弹道就变成"随机能不能中"）——
   只关自测实例的 AI 处理，`EnemyBrain` 的生产行为一个字没改；
3. 摆位用 `PlaceOnShotLine()`：把**碰撞体中心**压在"子弹出生点沿弹道 0.9m"处
   （直接挂在根节点下的那个 `CollisionShape3D`，体心与根节点的差自己算 —— 不写死"脚底往上 0.9m"）；
4. 新增**自测射线** `DescribeShotRay()`：从子弹出生点沿弹道打一发（长度取一个物理帧的位移、
   mask 用投射物自己的 `HitMask`），直接打印"打到的第一个东西"。

**证据**（`--selftest`，`run_20261015_123354.log`，**退出码 0**）：

```
12:34:25 最小闭环·造靶子：完成（真敌人 res://Scenes/Enemies/Chaser.tscn 的实例，AI 已停）｜靶子=Chaser 生命 60 护甲 0
12:34:26 最小闭环·靶子上弹道：出生点=(0.4034828, 0.8525854, -0.37216538) 方向=(0.0012825, -0.0797616, -0.9968132)
           → 靶子=Chaser 体心=(0.4046370, 0.7807999, -1.2692972)（离出生点 0.9m，距玩家 1.69m）
           ｜自测射线（0.9→3.4m，mask=5）打到的第一个东西：Unit 'Chaser' @ (0.4040719, 0.8159452, -0.8300731)
12:34:26 最小闭环·命中：伤害输入 20 → 生命扣 20 护甲扣 0 被格挡=False 无视格挡标记=True 攻击技能=skl_a1_w1_1
12:34:27 ✅ 最小闭环·结果：动画事件帧收到=1 次 投射物生成=1 个｜生命 60 → 40（扣 20）
12:34:27 🔴 最小闭环·链路：按键 weapon_skill_1 → 技能 skl_a1_w1_1 → 动画 1 个事件帧 → 投射物 1 → 命中掉血 ✓
```

> ⚠️ 自测里那一发是 **20** 而不是表里的 10：能量掌的 `BaseDamage = 表里 Damage × 玩家 StatSheet.DamageMultiplier`，
> 自测环境下玩家的 `DamageMultiplier = 2`（真人手测那局是 1，日志 `伤害 10`）。与本轮改动无关
> （那条乘式一个字没动），只是**同一条链路在两种环境下的数值差**；判"掉血"只看 > 0。

#### 6.4.6 同一个场景里换角色时的能力残留 ✅ **已修（2026-10-15 第三轮）**

**现象**（上一轮留的）：在同一个 `Main` 场景里**连续换角色**（`BeginRunWithCharacter` 被调两次以上：自测就是这样，
MCP 调试也会踩到）时，**上一个角色挂的技能会残留在能力节点上** —— 表现是"Gunner + Pistol 的左键除了手枪
还会打出一发能量波"（`12:30:15` 那条日志里同时有手枪 24 与能量波 20）。

**根因（一句话）**：`Ability.InputAction` / `Enabled` / `AbilityId` 是**技能行**写在 `Player.tscn` 那两个能力节点上的
（`AbilityConfig.ApplyTo`），而换角色只套用"新角色 `Skills` 里列的那些行"——**上一套写下的绑定不会自己消失**。
`weapon_skill_1` 与 `fire` 在 `project.godot` 里**都是鼠标左键**（`weapon_skill_2`/`aim` 都是右键），
所以残留直接表现为"左键多打一发"。另外 `ApplyAbilityMatrix` 排在 `ApplySkillsTo` **之前**，它只看
`EnabledAbilityIds`/`DisabledAbilityIds` 两个名单，本来就不负责清理没列到的能力。

**本轮改法**（`Scripts/Rewards/RunController.cs`，+1 字段 +1 方法 +1 调用，**没有动** `PlayerAbilityInput` 的"只读表"那条路）：

- 新增 `_abilityBaseline`：每个能力节点的**场景基线**（`Enabled` / `InputAction` / `AbilityId`），
  **第一次** `ApplyBalance` 时抓 —— 那一刻玩家的能力节点还是 `Player.tscn` 的原始值（这个函数排在 `ApplyBalance`
  里所有"行 → 能力"的写入之前，包括能力卡 `AbilityEffects.ApplyToPlayer`）。
- 新增 `ResetAbilityBindings(abilities)`：每次套用角色/武器**先恢复到基线**，之后由技能行 + 开关矩阵 + 能力卡重新写一遍。
  于是"换回 Gunner"与"一开局就是 Gunner"得到**同一份**状态（幂等）。

**验证**（同一个 `Main` 场景，编辑器 + MCP，真按左键）：Hunter(3,3) → Gunner(0,0)：

| 时刻 | 修前 | 修后 |
|---|---|---|
| Hunter 时 `AbilityWeaponSkill1` | `InputAction=weapon_skill_1` `Enabled=True` | 同（能量掌正常） |
| 切到 Gunner 后同两个字段 | **`InputAction=weapon_skill_1` `Enabled=True`（残留）** | **`InputAction=""` `Enabled=False`** |
| 日志的 `能力开关矩阵（角色 chr_gunner）` | `…、skl_a1_w1_1[weapon_skill_1]=开、skl_a1_w1_2[weapon_skill_2]=开` | `…、EnergyWaveAbility[-]=关、EnergyWaveAbility[-]=关` |
| Gunner 左键（真点游戏窗口） | `能量波发射：skl_a1_w1_1 伤害 10`（手枪 **+** 能量波） | 整局日志 **`能量波` 0 行**；`PelletsFired 0→1`、`sfx_weapon_fire`（只有手枪） |
| 切回 Hunter 再左键 | — | `技能动画开播 Skill_A1_ATK1` → `能量波发射：skl_a1_w1_1 伤害 10 弹速 150`（能量掌照旧正常） |

> 注：`能力开关矩阵` 那一行写在 `ApplySkillsTo` **之前**（历史顺序），所以 Hunter 那几行会先显示 `[-]=关`，
> 之后技能行才把 `weapon_skill_1/2` 绑上 —— 这一行的用处正是"看有没有上一套的残留"。


---

## 7. 旧硬指标对比（只加不删的验证 · 逐轮复核）

下表上半部分是"动画管线那一轮"的复核；**第二轮（2026-10-15，清遗留那轮）复测的结论也在同一张表里**。

| 指标 | 基线 | 本轮 | 结论 |
|---|---|---|---|
| `配置表：武器 …（ConfigIndex.tres 已加载）` | 武器 3 / 角色 3 / 技能 25 · 已加载 | 武器 **4** / 角色 **4** / 技能 **27** · **已加载** | 只多新增行，**没有静默回退** |
| 节点布置 | 10（2/5/3） | 10（2/5/3） | 逐位一致（第二轮复测：**仍然 10（2/5/3）**） |
| Pressure 测试 | pressure=3 pmods=3 bank=0 | pressure=3 pmods=3 bank=0 | 逐位一致（第二轮复测：**仍然 3/3/0**） |
| 白板首领实例 | 生命 315 / 护甲 0 | 生命 315 / 护甲 0 | 逐位一致（第二轮复测：**仍然 315/0**） |
| 配置表比对·玩家 | 生命 270 护甲 40 移速 7 | 生命 270 护甲 40 移速 7 | 逐位一致（第二轮复测：**仍然 270/40/7**） |
| 端到端 F | `奖励界面打开 = True  Paused = True` | 同 | 逐位一致（第二轮复测：**仍然 True/True**） |
| 配置表比对·索引 | 3/3/10/1/1/25/7/9/8/6/3/11/8 | 4/4/10/1/1/27/7/9/8/6/3/11/8 | **只在新增行上变化（预期内）**，第二轮把**自测里的期望串同步成实际值**（§6.4.4；那条比对只打印、不校验） |
| 最小闭环·链路的最后一环 | —（上一轮自测是 `命中掉血 ✗`） | **`命中掉血 ✓`（生命 60 → 40）** | 第二轮把靶子换成真敌人 `Chaser.tscn` 实例（§6.4.5） |

**第三轮（2026-10-15，追查"新生成单位同帧 0 血死亡" + 修 §6.4.6 的能力残留）复测**：
只改了 `Scripts/Rewards/RunController.cs`（+1 字段 +1 方法 +1 调用，**没碰任何表、没碰 `ConfigIndex.tres`**），
全量自测（CLI `--selftest`，**退出码 0**，`run_20261015_131758.log`）逐位复现上表：节点布置 `10（2/5/3）`、
`pressure=3 pmods=3 bank=0`、白板首领 `315/甲 0`、玩家 `270/40/7 减伤 0`、端到端 `打开=True Paused=True`、
索引 `4/4/10/1/1/27/7/9/8/6/3/11/8`、`（ConfigIndex.tres 已加载）`、最小闭环链路 ✓、`=== 自测结束 ===`（无中断、无警告）。

`ConfigIndex.tres` 同步方式（**三处一起改**）：`load_steps` 123→131、新增 6 个 `[ext_resource]`、
`Weapons`/`Characters`/`Skills`/`SkillAnimations` 四个数组各加项。
每次改完都核对日志里的 `（ConfigIndex.tres 已加载）`。**动画管线那一轮没有改 `ConfigIndex.tres`；
第二轮（清遗留）也没有改** —— 两轮都只在已有数组之外动"新增行文件"。第二轮改到的表只有两个新增行的
`Params`（`skl_a1_w1_1/2` 各加一个 `MaxAimDistance`）与 `chr_hunter.tres` 的 `WeaponEnabled`。

---

## 8. 备份与基线

| 项 | 位置 |
|---|---|
| `ConfigIndex.tres` 改动前 | `F:\Godot项目\_refactor_backup\ConfigIndex.tres.before`（仓库外） |
| 基线自测日志 | `F:\Godot项目\_refactor_backup\baseline_run.log` / `baseline_godot.log` |
| 动画管线那一轮的 `HEAD` | `36818fe`（本轮从它继续） |
| 最初的基线 `HEAD` | `7182e7a769ccaa042ce044d33231261b3e98d660`（`git rev-list --count` = 54） |

---

## 9. 怎么复现"最小闭环"（下一轮照这个走）

```powershell
$cli = 'F:\Godot项目\项目a\.gdmcp\bin\gdmcp.exe'

# ① 编辑器必须开着（MCP 才可用）：gdmcp doctor → editor_connected: true
& $cli doctor

# ② 起窗口。两条路：
#    ②-a 只验机制（不带 user args）：MCP 的 run_project
& $cli tool-call run_project --allow-open-world --apply `
  --args-json '{"allow_window":true,"scene_path":"res://Scenes/Main.tscn"}'
#    ②-b 要**带 `--char/--weapon`**（run_project 传不了 user args）：用编辑器同款参数自己起进程，
#        debugger 挂到编辑器的 6007 → MCP 照样能控（见 §6.4.1 的方框）
& 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe' `
  --path 'F:/Godot项目/项目a' --remote-debug tcp://127.0.0.1:6007 `
  res://Scenes/Main.tscn -- --char=chr_hunter --weapon=wpn_energy_palm

# ③ 不带参数时用运行期入口选角色/武器（Hunter=3 EnergyPalm=3；Gunner=0 Pistol=0）
& $cli tool-call evaluate_runtime_expression --allow-open-world --apply `
  --args-json '{"node_path":"/root/Main/RunController","expression":"BeginRunWithCharacter(3, 3)"}'
& $cli tool-call evaluate_runtime_expression --allow-open-world --apply `
  --args-json '{"node_path":"/root","expression":"get_node(\"/root/UiService\").CloseAll()"}'

# ④ 造一个"真敌人"靶子摆到弹道上（自造 Unit 不行，见 §6.4.5）：先起一场战斗，等 30s 的定期刷怪，
#    或直接实例化 Chaser.tscn；然后**停掉它的 AI** 再摆位，否则它会跑过来：
#    get_tree().current_scene.add_child(load("res://Scenes/Enemies/Chaser.tscn").instantiate())
#    get_tree().get_nodes_in_group("enemy_units")[0].get_node("Brain").set_physics_process(false)
#    get_tree().get_nodes_in_group("enemy_units")[0].set("global_position",
#        get_node("/root/Main/Player").global_position + Vector3(0,0,-8))   # 8m 也打得中了

# ⑤ 战斗窗口在前台后，**真按左键**（mcp__windows__ 的 Click，坐标取游戏窗口中心）。

# ⑥ 取证只看三行：技能动画开播 / 能量波发射 / 伤害 Player(...) → <敌人>(…)
#    日志：C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\logs\run_*.log
#    自测：`-- --selftest`（或 user://selftest.flag），跑完**删掉 flag**；退出码应为 0
```

⚠️ 玩家会掉血死亡（一局约 2 分钟），**进关卡后要尽快按左键**。
⚠️ **同一个 `Main` 场景里换角色**曾经会让上一个角色的技能残留（§6.4.6）——**第三轮已修**（能力绑定每次套用前
恢复到场景基线），所以现在不再需要"重开一个进程"来拿干净的旧武器回归测试；§6.4.6 同时补了"同名切换"的验证表。
