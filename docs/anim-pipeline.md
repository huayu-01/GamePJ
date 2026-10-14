# 动画管线（技能动画：生成 → 加载 → 事件帧 → 技能）

> **本轮范围**：动画管线地基 + 角色 A［猎手］武器 1「能量掌」的最小闭环。
> **总纲**：`docs/refactor-survey.md`（§1 动画普查 / §5 阶段划分）；**需求权威**：`docs/character-redesign.md`；
> **设计权威**：`F:\Godot项目\3d肉鸽雨中哈迪斯\角色设计.txt`。
>
> 本轮**只加不删**：旧的 3 角色 / 3 武器 / 19 条增幅 / 25 条技能行全部保留，新内容全是**新增行文件**。

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
| `AnimEvent` | 等哪个事件帧才释放效果（`ATK` / `Dodge` / `Release` …） |
| `AnimSpeedScale` | 动画速度倍率（>1 加速释放；蓄力类 <1 拉长。写在**每单位自己的播放器**上） |

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

---

## 6. ⚠️ 未解决（本轮没做完的，必须知道）

### 6.1 自测里"播运行期新加进库的技能动画"会让引擎崩

**现象**：`exit -1073741819`（访问违规），崩在**播放技能动画后的下一帧**。
已经逐帧取证：`技能动画开播` 打印成功 → 下一帧 `UnitAnimator._PhysicsProcess` **一次都没进技能分支**就崩了。

**已排除**：资源本身（同一份 `.tres` 在临时工程里正常）、`Stop()/Seek()`、`Play(blend)`、
`TrackSetEnabled`、`PrepareForRuntime` 的复制、动画源片段（换成 `idle` 也一样崩）、
`AnimationPlayer` 是否早已在跑（在 `_Ready` 里就加载库也一样崩）、命中特效、Jolt/导航。

**因此**：`SelfTest.BeginSkillLoopProbe` 里 `DebugSkipSkillAnimation = true`——
自测跳过"动画 → 事件帧"那一段，让"技能 → 子弹 → 命中"能被验证。
动画管线本身的加载/查名/事件帧解析由 case 24 那一行证明。

### 6.2 自测里"能量波命中结算"也让引擎崩

**现象**：`能量波发射` 打印成功（伤害 10 / 弹速 150 / 无视格挡=True）→ 下一帧崩。
已定位到 `Projectile.ResolveImpact` 的**"是敌人单位"那个分支内部**（`K1 ResolveImpact 进入` 打到了，
后面的 `L1 是敌人单位` 没打到）。`GameplayEventBus` / `HitEffect` 都还没走到。

**两个崩点都没有在真人手测里复现过**（手测时游戏窗口还在，没有崩），所以怀疑是
**headless + 无窗口 + 自测这条特殊路径**触发的；但**没有证据**，不能当成"只影响自测"。

### 6.3 因此本轮**没有**拿到"进关卡 → 按左键 → 看到动画播 → 看到子弹飞出 → 看到敌人掉血"的完整证据

拿到的是：
- ✅ 三个坑的真项目实测（§2）
- ✅ 技能 → 子弹：`能量波发射：skl_a1_w1_1 伤害 10 弹速 150（普通，无视格挡=True，穿透 0，方向 …）`
- ✅ 动画管线：加载 2 段 / 全路径查名 / 事件帧解析出 `ATK@0.08s` / `CallbackModeMethod=Immediate`
- ❌ 动画真的播出来（引擎崩）
- ❌ 子弹真的命中掉血（引擎崩）

### 6.4 下一步建议

1. 在**真人手测**（非 headless、正常窗口）里跑 `--char=chr_hunter --weapon=wpn_energy_palm`，
   确认这两个崩点是否只在自测环境出现；
2. 若手测也崩：用 Godot 的 `--verbose` + 崩溃转储定位到具体引擎调用，
   或改用"播放器只播**全局库**里的裸名动画"绕开命名库这条路；
3. 若手测正常：把 `SelfTest.DebugSkipSkillAnimation` 改回 `false`，并给命中链加一个不经过 `Projectile`
   的直连探针（`target.ReceiveHit(...)`）来验证结算。

---

## 7. 旧硬指标对比（只加不删的验证）

| 指标 | 基线 | 本轮 | 结论 |
|---|---|---|---|
| `配置表：武器 …（ConfigIndex.tres 已加载）` | 武器 3 / 角色 3 / 技能 25 · 已加载 | 武器 **4** / 角色 **4** / 技能 **27** · **已加载** | 只多新增行，**没有静默回退** |
| 节点布置 | 10（2/5/3） | 10（2/5/3） | 逐位一致 |
| Pressure 测试 | pressure=3 pmods=3 bank=0 | pressure=3 pmods=3 bank=0 | 逐位一致 |
| 白板首领实例 | 生命 315 / 护甲 0 | 生命 315 / 护甲 0 | 逐位一致 |
| 配置表比对·玩家 | 生命 270 护甲 40 移速 7 | 同 | 逐位一致 |
| 配置表比对·索引 | 3/3/10/1/1/25/7/9/8/6/3/11/8 | 4/4/10/1/1/27/7/9/8/6/3/11/8 | **只在新增行上变化（预期内）**，自测期望值待同步 |

`ConfigIndex.tres` 同步方式（**三处一起改**）：`load_steps` 123→131、新增 6 个 `[ext_resource]`、
`Weapons`/`Characters`/`Skills`/`SkillAnimations` 四个数组各加项。
每次改完都核对日志里的 `（ConfigIndex.tres 已加载）`。

---

## 8. 备份与基线

| 项 | 位置 |
|---|---|
| `ConfigIndex.tres` 改动前 | `F:\Godot项目\_refactor_backup\ConfigIndex.tres.before`（仓库外） |
| 基线自测日志 | `F:\Godot项目\_refactor_backup\baseline_run.log` / `baseline_godot.log` |
| 基线 `HEAD` | `7182e7a769ccaa042ce044d33231261b3e98d660`（`git rev-list --count` = 54） |
