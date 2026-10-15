# 重构前调研报告（角色 / 武器 / 增幅）

> ⚠️ **状态横幅（2026-10-16 P6b 加）**：本文件是 **P0 的只读调研快照**，里面所有"现状 / 待确认"条目
> 都已经被后续阶段处理掉了，**不要再照着它判断现在的代码**。特别地：
> **P6b 已经把"弹匣/装填"整条横向清理完**（用户裁定「没有弹匣的概念了」）——
> 本文提到的 `Weapon` 弹匣/换弹逻辑、`MagazineSize` / `ReloadTime` / `ReloadSpeedMultiplier` /
> `MagazineSizeAdd`、`meta_reload`、`card_magazine` / `card_reload` **全部已删除**。
> 当前状态见 [character-redesign.md](character-redesign.md) §五 / §七 与 [config.md](config.md) 顶部横幅。

> **性质**：只读调研，产出一份文档。除本文件外，**没有改动项目里任何代码 / 配置 / 场景**。
> **权威来源**：`F:\Godot项目\3d肉鸽雨中哈迪斯\角色设计.txt`（243 行，已完整读完）+ 用户后续澄清 7 条。
> **调研对象**：`F:\Godot项目\项目a`（Godot 4.7.2 stable mono / C#，137 个 `.cs`、22307 行，17 张配置表、134 个 `[ext_resource]`）。
> **证据纪律**：本文每条结论都带 `文件:行号`、代码片段或**实测输出**。做不到的事明确写"做不到 + 为什么"。

---

## 0. 结论摘要（先看这 8 行）

| # | 结论 |
|---|---|
| 1 | **动画**：13 个 `.glb` 全是同一套 Kenney Mini Characters 骨架，**各 32 段动画、名字完全相同**。其中有 7 段带攻击/射击语义 + 3 段可借用的动作段。**没有** dodge / roll / cast / block / parry / charge / throw / dash。 |
| 2 | **"复制 + 改名 + 加事件帧"完全可行，已实测跑通**：用 Godot 4.7.2 真的跑了一遍 `duplicate(true)` → `add_track(TYPE_METHOD)` → `track_insert_key({"method","args"})` → 存 `.tres` → 重新加载 → 播放 → 事件在动画时刻精准触发，且 `speed_scale=2.0` 时墙钟时间从 498ms 缩到 262ms（**逻辑帧自动跟着动画走**）。原始输出见 §1.4。 |
| 3 | **硬编码**：自动初筛命中 **555 行候选**；人工逐行确认、与本次重构直接相关的（Group A）**68 条**，其中 **14 条会直接挡住重构**（能力/按键/角色身份/冷却通路/状态叠层/DoT 间隔等）。Group B（音频/UI/世界/遭遇/首领/局外/交互/自测）只做了初筛级扫描，见 §2.4 的诚实说明。 |
| 4 | **差距**：文档可枚举 **102 条内容项**（角色行为 + 62 条增幅）与 **57 项机制**。现有系统**可直接承载 12 项机制**、**需改造 23 项**、**需新建 22 项**。 |
| 5 | **最致命的三处**：① `Ability` 是纯 `StateTime` 计时驱动，**全项目零 `method_tracks` / 零 `speed_scale` 引用**；② 按键按**能力 C# 类名**硬编码，`AbilityConfig` 里连 `InputAction` 列都刻意没有；③ `PlayerCharacterKind` / `WeaponPresetKind` 两个**枚举 + Slot 对齐**，加角色/武器必须改代码。 |
| 6 | **清除的最大风险**：9 个 `Ability` 子类里**只有 2 个**（`DashAbility` / `BurstFireAbility`）是旧角色专属；其余 7 个 + `Weapon.cs` **敌人/首领/召唤物都在用**，一起清会连带打断敌人系统。25 条技能行里**只有 5 条是旧角色的**。 |
| 7 | **`ConfigIndex.tres` 是唯一真源**：134 个 `[ext_resource]` + 12 个索引数组。删行文件不同时删数组项与 `[ext_resource]` → **整份配置加载失败 → 全数值静默回退**（项目已栽过，代码注释里写着）。 |
| 8 | **总工作量：大**。这是"重写战斗驱动层 + 构筑层 + 动画层"，不是加功能。关键路径 = 动画管线 → 技能实现。 |

---

## 1. ① 模型动画普查

### 1.1 项目内模型 = 13 个 `.glb`，全是同一套 32 段动画

`Assets/Models/player/`（3）+ `Assets/Models/enemies/`（10），逐个解析 GLB 的 JSON chunk 得到动画列表（`asset.generator = UnityGLTF version 2.0`）：

| 文件 | 段数 | nodes | skins |
|---|---|---|---|
| `Assets/Models/player/mesh_player_body.glb` | 32 | 10 | 2 |
| `Assets/Models/player/mesh_player_body_heavy.glb` | 32 | 10 | 2 |
| `Assets/Models/player/mesh_player_body_summoner.glb` | 32 | 10 | 2 |
| `Assets/Models/enemies/mesh_enemy_{boss,charger,chaser,exploder,fast_melee,guard,jumping_melee,ranged,screamer,skill_elite}.glb` | 32 ×10 | 10 | 2 |

**13 个文件的动画名列表逐字相同**（实测输出已对齐）。

### 1.2 32 段的确切名字（+ 时长 + 帧数 + 可用性判定）

用 Godot 4.7.2 本体 `--headless` 加载 `.glb` 打印（`AnimationPlayer.GetAnimationList()`，比 JSON 更权威，因为这就是引擎看到的东西）：

| # | 动画名 | 时长(s) | 关键帧 | loop | 本项目的可用性 |
|---|---|---|---|---|---|
| 0 | `static` | 0.100 | 2 | None | 不用 |
| 1 | `idle` | 1.333 | 40 | None | **已在用**（UnitAnimator.cs:42） |
| 2 | `walk` | 0.667 | 20 | None | **已在用**（:45） |
| 3 | `sprint` | 0.500 | 15 | None | **已在用**（:48）；≡ 文档的"奔跑" |
| 4 | `jump` | 0.500 | 15 | None | **已在用**（跳跃）；可作「跃升 / 闪」的源 |
| 5 | `fall` | 0.333 | 10 | None | 可作「滞空 / 下落攻击」的源 |
| 6 | `crouch` | 0.167 | 2 | None | 只有 2 帧（等于一个姿势） |
| 7 | `sit` | 0.167 | 2 | None | 不用 |
| 8 | `drive` | 0.167 | 2 | None | 不用 |
| 9 | `die` | 0.333 | 10 | None | **已在用**（UnitAnimator.cs:51） |
| 10 | `pick-up` | 0.333 | 10 | None | **可借用**（"收回武器"的伸手动作） |
| 11 | `emote-yes` | 0.667 | 20 | None | 不用 |
| 12 | `emote-no` | 0.667 | 20 | None | 不用 |
| 13 | `holding-right` | 0.167 | 2 | None | 蓄力起手姿势（2 帧，太短） |
| 14 | `holding-left` | 0.167 | 2 | None | 同上 |
| 15 | `holding-both` | 0.167 | 2 | None | 同上；可作「架」的源（拉长） |
| 16 | `holding-right-shoot` | 0.200 | 6 | None | **攻击类**：单手射击 |
| 17 | `holding-left-shoot` | 0.200 | 6 | None | **攻击类**：单手射击（另一侧） |
| 18 | `holding-both-shoot` | 0.200 | 6 | None | **攻击类**：双手射击 |
| 19 | `attack-melee-right` | 0.417 | 13 | None | **攻击类**：右手挥砍 |
| 20 | `attack-melee-left` | 0.417 | 13 | None | **攻击类**：左手挥砍 |
| 21 | `attack-kick-right` | 0.533 | 16 | None | **攻击类**：右腿踢/踩 |
| 22 | `attack-kick-left` | 0.533 | 16 | None | **攻击类**：左腿踢/踩 |
| 23 | `interact-right` | 0.667 | 18 | None | **可借用**：右手前伸（拔刀/抛掷） |
| 24 | `interact-left` | 0.667 | 18 | None | **可借用**：左手前伸 |
| 25–31 | `wheelchair-sit` / `-look-left` / `-look-right` / `-move-forward` / `-back` / `-left` / `-right` | 0.167–0.5 | 2–15 | None | **完全不可用**（轮椅，7 段全是废料） |

**结论（有攻击类动画吗）**：

- **有 7 段**：`holding-right-shoot` / `holding-left-shoot` / `holding-both-shoot` / `attack-melee-right` / `attack-melee-left` / `attack-kick-right` / `attack-kick-left`。
- **另有 3 段可借用**：`interact-right` / `interact-left` / `pick-up`。
- **可用素材合计 10 段**（+ `jump`/`fall`/`idle`/`walk`/`sprint`/`die` 作运动与状态）。
- **明确没有**：`dodge`、`roll`、`cast`、`block`、`parry`、`charge`、`throw`、`aim`、`dash`、`swing`、`punch`、`slash`、`bow-draw`、`slam` —— **一个都没有**。
- ⚠️ 文档点名的 `Dodge` / `Dodger` / `Action` / `ATK` / `Skill_1` / `Skill_2` **全部不存在**。按用户"严格要求有相应动画才会执行相应技能"的裁定：**现在这套素材下，文档里的技能一个都执行不了**。

### 1.3 素材库里有没有更好的模型？——**没有**

`F:\Godot项目\3d肉鸽雨中哈迪斯\素材\模型\` 下 8 个 zip，逐个在内存里读取（未解压进项目）：

| zip | 内容 | 动画 |
|---|---|---|
| `LowPolyGemsPack2.zip` | 12 个 `gem*.fbx` + blend + 贴图 | 静态道具 |
| `Voxel Weapons.zip` | `Bow/Pistol/Rifle/Sniper/Sword/Sword2.fbx` + vox + blend | 静态网格（无骨骼、无动画） |
| `VoxelPlatformerPack.zip` | 一堆 `.vox.obj` 方块/蘑菇/金币 | 静态网格 |
| `首领\DRIZZEL/GEKKOU/KASA/KURENAI/SAMIDALE/SHOGUN_asset.zip` | 各 1 个 `.blend` + 1 个 `.fbx` | **有骨骼，但零动画** |

首领 FBX 的实测证据（二进制扫描关键字段）：

```
DRIZZLE_Lowpoly.fbx   AnimStack=1  AnimCurve=0  Deformer=561  LimbNode=138  Skin=4
GEKKOU_lowpoly.fbx    AnimStack=1  AnimCurve=0  Deformer=841  LimbNode=166  Skin=5
KASA_Lowpoly.fbx      AnimStack=1  AnimCurve=0  Deformer=1163 LimbNode=164  Skin=7
KURENAI_lowpoly.fbx   AnimStack=1  AnimCurve=0  Deformer=661  LimbNode=130  Skin=5
SAMIDALE_Lowpoly.fbx  AnimStack=1  AnimCurve=0  Deformer=609  LimbNode=150  Skin=4
SHOGUN_Lowpoly.fbx    AnimStack=1  AnimCurve=0  Deformer=391  LimbNode=128  Skin=3

curve(不分大小写)=0   KeyTime=0   KeyValueFloat=0
'AnimStack' 的上下文 = 头部 GlobalSettings 的属性名 ActiveAnimStackName（不是动画数据）
```

→ **6 个首领模型都是"有蒙皮、无动画"的静态绑定姿态**。`AnimCurve=0` / `KeyTime=0` 是决定性的：**一个关键帧都没有**。
**普查结论：整个素材库里，除 Kenney 这 32 段以外没有任何可用动画。**

### 1.4 复制 + 改名 + 加事件帧：**可行性结论 = 能，且已实测跑通**

> ⚠️ 为拿到硬证据，我在 **`%TEMP%\godot_anim_probe`** 建了一个**独立临时工程**（`C:\Users\huayu\AppData\Local\Temp\godot_anim_probe`），把 `mesh_player_body.glb` **拷出去**做实验。**没有往项目里拷任何东西，项目文件一字未改**。复现步骤见 §附录 A。

#### API 存在性（反射 `Godot_v4.7.2-stable_mono_win64\GodotSharp\Api\Release\GodotSharp.dll`）

| 需要的能力 | Godot 4.7.2 的实际成员 | 结论 |
|---|---|---|
| 复制动画 | `Resource.Duplicate(Boolean deep)`；实测 `Animation` 重载后 11 条轨道全部独立 | ✅ |
| 新建"事件轨道" | `Animation.AddTrack(TrackType type, Int32 atPosition)`；`TrackType.Method = 5` | ✅ |
| 写事件帧 | `Animation.TrackInsertKey(Int32 trackIdx, Double time, Variant key, Single transition)` | ✅ |
| 事件帧的 key 长什么样 | `MethodTrackGetName(...)` → `StringName`、`MethodTrackGetParams(...)` → `Array` ⇒ key 必须是 `Dictionary{"method": StringName, "args": Array}` | ✅ |
| 轨道目标 | `Animation.TrackSetPath(Int32, NodePath)`，**相对 `AnimationMixer.RootNode` 解析** | ✅ |
| 改名 | `AnimationLibrary.RenameAnimation(StringName, StringName)` / `AddAnimation(StringName, Animation)` | ✅ |
| 塞回播放器 | `AnimationMixer.AddAnimationLibrary(StringName, AnimationLibrary)` | ✅ |
| **加速动画释放** | `AnimationPlayer.SpeedScale`（float）—— 文档与实测一致：1=正常、0.5=半速、2=双速、负=倒放、0=不动 | ✅ |
| 事件调用时机 | `AnimationMixer.CallbackModeMethod` = `Deferred(0)` / `Immediate(1)`；**默认 Deferred** | ✅ |
| 轨道间搬运 | `Animation.CopyTrack(Int32, Animation)` | ✅ |
| 序列化 | `ResourceSaver.Save(...)` → `.tres` 可存可读 | ✅ |

#### 实测原始输出（节选，逐字）

**实验 1（`probe.gd`）—— 复制 / 改名 / 加事件帧 / 存盘 / 读回**

```
animation count = 32
  attack-melee-right  len=0.41666665673256  tracks=11  loop=0
--- source anim: attack-melee-right len=0.41666665673256 tracks=11
duplicate(true) type=Animation tracks=11 len=0.41666665673256 path=
add_track(TYPE_METHOD) -> trackIdx=11 type=5
track_insert_key -> keyIdx=0
roundtrip name=anim_event params=["ATK"]
key_time=0.1
after 2nd key: keycount=2
   key[0] t=0.1 name=anim_event params=["ATK"]
   key[1] t=0.3 name=anim_event params=["RELEASE", 3]
new AnimationLibrary.add_animation(Skill_1) err=0 has=true len=0.6
ap.add_animation_library(skills) err=0
ap.has_animation(Skill_1) = false            ← ⚠️ 见下方"坑 1"
all animations now: [..., "skills/Skill_1", ...]
after rename_animation list=[&"Skill_2"]
ResourceSaver.save -> 0
reloaded: type=Animation tracks=12 len=0.6
   track 11 type=5 path=.. keys=2
      key 0 t=0.10000000149012 name=anim_event args=["ATK"]
      key 1 t=0.30000001192093 name=anim_event args=["RELEASE", 3]
speed_scale set -> 2.0
=== PROBE END OK ===
```

**实验 2（`probe2.gd`）—— 事件真的会触发，且跟着 `speed_scale` 走**

```
setup: add_animation_library err=0 anim len=0.5
setup: has_method(anim_event)=true
--- phase 0: speed_scale=1.0 playing skills/Skill_1 (anim len=0.5s) ---
    [CALLED] t_ms=280 name=ATK_0.10 anim_pos=0.1000 extra=<null>
    [CALLED] t_ms=431 name=ATK_0.25 anim_pos=0.2518 extra=<null>
    [CALLED] t_ms=583 name=RELEASE_0.40 anim_pos=0.4015 extra=7
--- phase 0 done: hits=3 wall_ms=498 (anim len 0.5s) ---
--- phase 1: speed_scale=2.0 ---
    [CALLED] t_ms=749 name=ATK_0.10 anim_pos=0.1067 extra=<null>
    [CALLED] t_ms=825 name=ATK_0.25 anim_pos=0.2572 extra=<null>
    [CALLED] t_ms=901 name=RELEASE_0.40 anim_pos=0.4090 extra=7
--- phase 1 done: phase1 hits=3 wall_ms=262 ---
SUMMARY: [["speed_scale=1.0", 3, 498], ["speed_scale=2.0", 3, 262]]
PROBE2 PASS: method tracks fired on animation frames; speed_scale shortens wall time
```

**这两行输出直接证实了用户方案的核心假设**：
1. `anim_pos` = 0.1000 / 0.2518 / 0.4015 —— 事件**在动画的精确时刻**触发（不是固定时间点）。
2. 同一条 0.5s 动画，`speed_scale=1.0` 用 **498ms**、`2.0` 用 **262ms** —— **逻辑帧自动跟着动画走**，"加速动画释放"不会让动画与逻辑对不齐。
3. 参数（`extra=7`）能透传。

#### 三个必须写进方案的坑（都是实测出来的，不是猜的）

**坑 1 · `HasAnimation` 不搜嵌套库。** `ap.add_animation_library(&"skills", lib)` 之后：

```
ap.has_animation(&"Skill_1")  => false          ← 裸名查不到
ap.get_animation_list()       => [..., "skills/Skill_1", ...]   ← 全路径才对
```
这条**直接命中现有代码**：`Scripts/Combat/UnitAnimator.cs:235` 用的是 `_player.HasAnimation(wanted)`，`:240` 用 `GetAnimationList()` 做大小写不敏感匹配。→ 技能动画若放进命名库，**表里必须写 `skills/Skill_1` 全路径**，否则永远回退到 idle（而且只在 warning 里体现，`.cs:249`）。建议：技能动画直接放进**全局库（空名）**，绕开这一坑。

**坑 2 · 方法轨道的路径是相对 `AnimationMixer.RootNode` 的，不是相对 Unit。** 实测：轨道 path 写 `"."` 时调用落在 mixer 的 `root_node` 自己身上；写 `".."` 时落在它的父节点。项目里 `AnimationPlayer` 在 `Model`（`.glb` 实例根）子树内（`Scenes/Player/Player.tscn`：`Model` → `AnimationPlayer`），所以从 mixer 的 `root_node`（= `.glb` 实例根）到 `Unit` 是 **`".."`**。→ **事件帧的 path 应写 `".."`，接收方法是挂在 `Unit` 上的 C# 方法。**

**坑 3 · 事件默认是 Deferred（晚一帧、且在物理之后）。** 实测第一版报的是 `Error calling deferred method: 'Node::anim_event': Method not found` —— 说明默认走延迟队列。战斗要"事件帧当帧生效"就必须显式设 `CallbackModeMethod = Immediate`（`AnimationMixer` 上的属性，枚举 `Deferred=0 / Immediate=1`）。

#### C# 侧能不能被"按名字调用"？——能，项目里已有先例

`GodotObject.Call`/`CallDeferred` 按 StringName 调 C# 方法，走的是 `ScriptInstance.callp` 同一条路。项目里已经在用并在跑：

- `Scripts/Config/ConfigIndex.cs:47` → `CallDeferred(nameof(BuildTableIndex))`
- `Scripts/Combat/CharacterMovement.cs:81` → `CallDeferred(nameof(ApplyJumpConfigDeferred))`

→ 事件帧调用 C# 的 `public void AnimEvent(string name)` **是同一机制**，不需要额外桥接。（注意参数是 `Variant`，所以签名用 `string`/`Variant` 参数最稳。）

#### `@tool` 批量生成：能，且是推荐落地路径

Godot 4.7 的 C# 支持 `[Tool]`（项目里已在用：`Scripts/Config/AbilityConfig.cs:24`、`CharacterConfig.cs:15`、`RewardConfig.cs:15`、`ConfigIndexData.cs:41`）。

**推荐做法（编辑器一次性生成 → 运行期只加载）**：

1. 新建 `Scripts/Editor/SkillAnimationGenerator.cs`，`[Tool] partial class ... : EditorScript`（或 `[Tool] Node` + 菜单项）。
2. 读一张**映射表**（也放在 `Config/Tables/`，一行 = 一条"源片段 → 目标动画名 → 事件帧列表"）。
3. 对每条映射：`GD.Load<PackedScene>(glb)` → `Instantiate()` → `NodeSearch.FindInSubtree<AnimationPlayer>()` → `lib.GetAnimation(源名)` → `Duplicate(true)` → `AddTrack(Animation.TrackType.Method)` → `TrackSetPath(ti, NodePath(".."))` → 每个事件帧 `TrackInsertKey(ti, t, new Godot.Collections.Dictionary { {"method", "anim_event"}, {"args", new Godot.Collections.Array{...}} })` → `Length = 目标时长` → `ResourceSaver.Save(dup, "res://Assets/Animations/<角色>/<动画名>.tres")`。
4. 运行期 `UnitAnimator` 扩展一个 `LoadSkillLibrary()`：把该角色目录下的 `.tres` 全部 `AddAnimationLibrary` 到单位自己的 `AnimationPlayer`（**每个单位实例各自一份**，避免共享资源被改坏）。这正是修掉下面这个既有隐患的机会。

> ⚠️ **既有隐患（必须在新方案里顺手修掉）**：`UnitAnimator.SetLoop()`（`.cs:259-266`）**直接改共享的 `Animation` 资源**（`.glb` 导入的子资源，13 个 `.glb` 全部实例共享）：
> ```csharp
> // UnitAnimator.cs:261-265
> if (_player?.GetAnimation(name) is { } animation)
> {
>     // 同一个 .glb 的所有实例共享这几段 Animation，所以重复设成同一个值是幂等的。
>     animation.LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
> }
> ```
> 现在"幂等"成立是因为所有单位都设成同一个值。一旦技能动画要按单位/角色区分循环与 `speed_scale`，改共享资源就会**串味到所有同类单位**。→ 新方案里 `speed_scale` 设在 **`AnimationPlayer` 上**（每实例独立，实测 ✅），技能动画用 **`Duplicate()` 出来的独立资源**。

### 1.5 完整「技能 → 动画」映射（本次要求产出）

规则（来自文档 + 用户澄清）：**没有对应动画 → 该技能不执行**。所以每个技能都必须先"造"出一段动画。下表 = 从 10 段可用源里选源，复制改名 + 挂事件帧的**完整方案**（`L` 表示源片段的时长；事件帧时刻按源片段比例给，开发期可在 Inspector 里微调）。

#### 角色 A［猎手］（远程；建议主源 = `holding-*-shoot`）

| 文档技能 | 目标动画名 | 源片段 | 事件帧（method=`anim_event`，args） | 备注 |
|---|---|---|---|---|
| 被动[灵动] | 无需新动画 | — | — | 纯数值（空中跳跃次数） |
| 技能1[后撤] | `Skill_1_Backdash` | `jump` (L=0.5) | `["Dodge"]` @0.00、`["Dodger"]` @L | 文档原话"帧 Dodge 到 Dodger 期间无敌"，即**两个事件帧夹一个无敌窗口**；建议同时 `Length=0.35` 缩短 |
| 技能2[跃升] | `Skill_2_Leap` | `jump` (L=0.5) | `["Action"]` @0.15 | 文档原话"帧 Action 时释放效果" |
| A1 武技1[波波] | `ATK_A1_基础` | `holding-right-shoot` (L=0.2) | `["ATK"]` @0.08 | 文档原话"事件帧到 ATK 时发射" |
| A1 武技2[憋个大的] | `ATK_A1_蓄力` | `holding-both-shoot` (L=0.2) | `["ATK"]` @0.10 | 蓄力用 `speed_scale` 拉伸（<1 变慢），完美时机窗口由逻辑给 |
| A2 武技1[射射射] | `ATK_A2_散射` | `holding-right-shoot` (L=0.2) | `["ATK"]` @0.08 | 三发散射在事件帧里一次发完 |
| A2 武技2[大射天下] | `ATK_A2_蓄力散射` | `holding-both-shoot` (L=0.2) | `["ATK"]` @0.12 | 后置蓄力在逻辑里追加发数 |
| A3 武技1[箭在弦上] | `ATK_A3_拉弓` | `holding-both`(L=0.167) 拉长到 0.5 | `["Charge"]` @0.00、`["ATK"]` @0.35 | 32 段里没有拉弓姿势，用"双手持械"拉长代替 |
| A3 武技2[刹那之眼] | `ATK_A3_瞄准` | `holding-left-shoot` (L=0.2) | `["AimOn"]` @0.00、`["Mark"]` @0.10、`["ATK"]` @0.18 | 每 0.5s 施标记由逻辑循环；"蓄力期被击中"在逻辑里判 |

#### 角色 B［武器大师］（近战；主源 = `attack-melee-*` / `attack-kick-*` / `interact-*`）

| 文档技能 | 目标动画名 | 源片段 | 事件帧 | 备注 |
|---|---|---|---|---|
| 被动[落] 下落攻击 | `Passive_B_Slam` | `fall`(0.333) + 落地段用 `attack-kick-left`(0.533) | `["SlamStart"]` @0.00、`["SlamHit"]` @落地 | 建议做成两段：滞空段 + 落地段 |
| 技能1[闪] | `Skill_1_Dash` | `attack-kick-right` (L=0.533) | `["DashStart"]` @0.00、`["DashEnd"]` @0.35 | 全程穿透判定在逻辑里；路径可视化另做 |
| 技能2[架] | `Skill_2_Guard` | `holding-both`(0.167) 拉长到 0.75 | `["GuardOn"]` @0.00、`["GuardOff"]` @0.75 | 文档"架势 0.75 秒"；成功规避时提前发 `["GuardWin"]` |
| B1 武技1[破剑式] 5 段 | `ATK_B1_1_1` … `ATK_B1_1_5` | `attack-melee-right` / `-left` / `-right` / `-left` / `attack-kick-right` | 每段 `["ATK"]` @0.15 | 5 段 CD 0.3/0.4/0.5/0.6/0.6 → 5 段动画，最后一段可 `Length` 拉长 |
| B1 武技2[拔刀式] | `ATK_B1_2_剑气` | `interact-right` (L=0.667) | `["Charge"]` @0.00、`["ATK"]` @0.40 | 基础蓄力 1s → `speed_scale≈0.55` 或 `Length` 拉到 1.0 |
| B1 武技3[落剑式] | 复用 `Passive_B_Slam` | — | 复用 `["SlamHit"]` | 文档明确"不改变原有攻击，就是多一圈判定" |
| B2 武技1[憎恶] 3 段 | `ATK_B2_1_1/2/3` | `attack-melee-right` / `-left` / `attack-kick-right` | `["ATK"]` @0.15 | 前 2 刀半圆、第 3 刀大回旋 → 可用 `Hitbox` 形状不同表达 |
| B2 武技2[无情] 抛/收 | `ATK_B2_2_抛出` / `ATK_B2_2_收回` | `interact-left` / `pick-up` | `["Release"]` @0.30 / `["Catch"]` @0.25 | 文档"再次使用或靠近武器时收回" |
| B3 武技1[横扫千军] 5 段循环 | `ATK_B3_1_1` … `_5` | `attack-melee-right` / `-left` / `attack-kick-right` / `interact-right` / `attack-melee-right` | 每段 `["ATK"]` @0.15 | 挥砍>上挑>下劈>突刺>挥砍，循环 = 逻辑计数器 |
| B3 武技2[长虹贯日] | `ATK_B3_2_掷枪` / `_收回` | `interact-left` / `pick-up` | `["Release"]` @0.35、`["ATK"]` @0.40 / `["Catch"]` @0.25 | 基础蓄力 1s → `speed_scale`；抛物线+落点指示另做 |
| B3 武技3[落日余晖] | 复用 `Passive_B_Slam` | — | 复用 `["SlamHit"]` | 差异在逻辑（有无指向） |

#### 通用 / 增幅新增的动画

| 需求 | 目标动画名 | 源片段 | 事件帧 |
|---|---|---|---|
| 奔跑（文档通用机制） | 直接用 `sprint` | `sprint` | 无（`speed_scale` 随加成变） |
| 空中跳跃 | 直接用 `jump` | `jump` | 可选 `["AirJump"]` |
| 格挡/护盾破碎、燃烧、冰冻 | 无动画（纯特效 + 数值） | — | — |
| 速攻（"加快动画速度"） | 不改动画资源 | — | 在**技能动画上设 `speed_scale > 1`**（这正是坑 3 与实测验证的那条路） |

> **总计需要生成约 26 段技能动画**（A 角色 8 + B 角色 15 + 下落攻击 1 + 收回 2）。全部通过"复制现有 10 段源 + 改名 + 挂事件帧"完成，**零美术依赖**。

---

## 2. ② 硬编码审计

### 2.1 扫描方法与覆盖范围

- 覆盖 `Scripts/**` 全部 137 个 `.cs`（22307 行）。
- 先做机械初筛（数值字面量参与赋值/比较/乘法/实参，排除注释、`[Export]` 行、`using`）→ **555 行候选**。
- 再**逐行读上下文**确认（读了 `Unit.cs`/`Weapon.cs`/`CharacterMovement.cs`/`AttackHitbox.cs`/`Projectile.cs`/`AttackInstance.cs`/`CombatTypes.cs`/`StatusEffect.cs`/`BlockComponent.cs`/`Ability.cs` + 9 个子类/`RewardConfig`/`RewardDefinition`/`RewardPool`/`RewardCatalog`/`RewardEffectRegistry`/`RunController`/`RunBuild`/`Player*`/`Config*` 共 30+ 文件全文）。
- 另做字符串扫描：所有形如 `"skl_*/chr_*/wpn_*/enemy_*/pmod_*/meta_*/card_*/node_*/stg_*"` 的 id 字面量、`Input.IsAction*` 的动作名、`res://` 路径、节点名、组名。

### 2.2 Group A —— **与本次重构直接相关**（68 条，按严重度排序的前 30 条）

**🔴 = 会直接挡住重构（14 条）**

| 文件:行 | 硬编码内容 | 应放哪 | 严重度 |
|---|---|---|---|
| `Scripts/Player/PlayerAbilityInput.cs:26-38, 49-66` | 按键 ↔ **能力 C# 类** 绑定写死：`DashAbility→"dash"`、`BurstFireAbility→"burst"`、`SummonAbility→"summon"`、`AreaAttackAbility→"control"`；`Input.IsActionJustPressed("...")` | 技能表新增 `InputAction` 列 + 一张按键表（技能槽 1/2、武技 1/2、被动） | 🔴 |
| `Scripts/Config/AbilityConfig.cs:38-40` | **注释明确写"刻意没有 InputAction 列"**：`// ⚠️ 刻意**没有** InputAction 列：按键动作名现在由 PlayerAbilityInput 按能力**类**硬编码` | 同上；这条注释就是本次要推翻的设计决定 | 🔴 |
| `Scripts/Player/PlayerWeaponInput.cs:46,84,102,107` | `"fire"`(左键) / `"aim"`(右键) / `"reload"` 写死；**右键被"瞄准"占用** | 文档要求"武技2=右键" → `aim` 必须让位或改绑 | 🔴 |
| `project.godot:74-78,79-83` | `fire` = 鼠标 button 1；`aim` = 鼠标 button 2；`dash`=Q(81)、`burst`=E(69)、`control`=V(86)、`summon`=G(71)、`reload`=R、`jump`=Space、`interact`=F | 无 `sprint`(shift)、无 `ctrl`、无 `skill1/skill2` 语义化动作名 | 🔴 |
| `Scripts/Config/ConfigIndexData.cs:6-24` | `enum WeaponPresetKind { Pistol, Sniper, Rifle }`、`enum PlayerCharacterKind { Gunner, Heavy, Summoner }` | 角色/武器必须能"加一个不改代码" → 枚举本身就是障碍 | 🔴 |
| `Scripts/Rewards/RunController.cs:444,448` | `WeaponRow(kind) => ConfigRows.BySlot<WeaponConfig>((int)kind + 1)`；`CharacterRow` 同理 | **枚举顺序 = 表 Slot**，插入/调序/新增即错位 | 🔴 |
| `Scripts/Rewards/RunController.cs:455-460` | `DefaultCharacterTitle` 按枚举 `switch` 写死三段角色标题 | 角色表（`DisplayName`） | 🔴 |
| `Scripts/Rewards/RunController.cs:262-264` | 三张角色卡文案写死（"远程枪手 — 三把枪、灵活走位"…） | 角色表 `DescriptionTemplate` | 🔴 |
| `Scripts/Rewards/RunController.cs:508-513` | 能力开关矩阵兜底：`nameof(MeleeAttackAbility) => heavy, nameof(SummonAbility) or nameof(AreaAttackAbility) => summoner` | 角色表（已有 `EnabledAbilityIds`），但兜底路径也写死了角色语义 | 🔴 |
| `Scripts/Rewards/RunController.cs:82-83, 565` | `bool heavy = _character == PlayerCharacterKind.Heavy;` / `bool summoner = ...Summoner` / `_pool.SummonerAllowed = _character == Summoner` | 应改为通用的"限定标签"判定 | 🔴 |
| `Scripts/Config/CharacterConfig.cs:33,36` | `EnabledAbilityIds` / `DisabledAbilityIds` **按脚本类名匹配**（`ability.GetType().Name`，见 `RunController.cs:503`） | 应改为技能行 Id / 槽位号 | 🔴 |
| `Scripts/Abilities/Ability.cs:157` | `CooldownRemaining = Cooldown;` —— **全项目没有任何冷却缩减通路** | 需要 `Stats.Get("CooldownMultiplier")`（文档"洞悉:缩短角色技能冷却"、"速攻"） | 🔴 |
| `Scripts/Combat/Unit.cs:403-434` | `ApplyStatus`："同名刷新时间，**不叠加层数**" | 文档"标记：**可叠加**且不限定单个目标的 buff" → 现状结构性不支持 | 🔴 |
| `Scripts/Combat/Unit.cs:369, 371` | `if (status.TickAccumulator >= 1f)` / `-= 1f` —— **DoT 结算间隔写死 1 秒**；`EffectConfig` **没有 `TickSeconds` 列**（枚举注释提到了但字段不存在） | 文档：燃烧 0.5s 一跳、燃烧+ 0.25s 一跳 | 🔴 |

**🟠 = 严重（数值真源重复 / 常用参数不可配）（16 条）**

| 文件:行 | 硬编码内容 | 应放哪 | 严重度 |
|---|---|---|---|
| `Scripts/Rewards/RewardCatalog.cs:21-91` | **整个 19 条兜底目录硬编码**，且 **id 与表不一致**：Fallback 用 `dmg_up`/`pierce_round`，表用 `upg_dmg_up`/`upg_pierce_round` | 兜底目录应删掉或与之同步；id 漂移 → 见下一条 | 🟠 |
| `Scripts/Rewards/RewardCatalog.cs:51` vs `Config/Tables/Upgrades/evo_ricochet.tres` | Fallback：`excludes: new[] { "pierce_round", "evo_ap" }`；表里：`Excludes = PackedStringArray("upg_pierce_round", "evo_ap")` → **兜底路径上"跳弹×贯穿弹头"的互斥静默失效** | 统一 id 命名（前缀一致） | 🟠 |
| `Scripts/Combat/StatusEffect.cs:53-100` | `Fallback` 状态目录硬编码：`Slow ×0.55`、`Weaken ×0.75`、`Burn 6 dps`、`Empowered ×1.3/×1.15`、`Stun ×0.12/×0.5`；**无 Freeze / 无标记** | 效果表（已有 `eff_*`），但需补 Freeze/Mark 行 + 分级（烈阳+/冰晶+） | 🟠 |
| `Scripts/Combat/Unit.cs:186-210` | `SeedStats()` 里 **16 个 StatSheet 键名 + 初值全部写死**（`DamageMultiplier`…`KnockbackMultiplier`） | 新增"冷却缩减/护甲/跳跃高度/DoT 间隔"等键 = 必须改这个函数 | 🟠 |
| `Scripts/Combat/CharacterMovement.cs:21` | `[Export] public int ExtraAirJumps` —— **存在，但 17 张表里没有任何一列会写它**（grep 全项目仅 3 处，全是自身读写） | 文档"灵动""践踏死亡"需要它进表 + 运行期增减 | 🟠 |
| `Scripts/Combat/Unit.cs:124-126` | `CollisionMask = Faction == Player ? CombatLayers.World \| UnitMask : CombatLayers.World;` —— 玩家**会被敌人挡住** | 文档"剑突…突进过程可以穿过敌人"需要穿透开关 | 🟠 |
| `Scripts/Weapons/Weapon.cs:16-42` | 14 个武器数值的 `[Export]` 默认值 = 与武器表**并存的第二份真源**；尤其 `CritChance = 0.15f`（文档"角色基础 5%"）、`CritMultiplier = 2f`、`AutoReloadDelay = 3f`、`AimFov = 55f` | 武器表（已覆盖），但默认值必须与表一致否则静默换挡 | 🟠 |
| `Scripts/Weapons/Weapon.cs:29` | `AttackTags = { "Projectile", "Gun", "Physical" }` 硬编码字符串标签 | 武器表（已有 `AttackTags` 列） | 🟠 |
| `Scripts/Abilities/Ability.cs:32-34` | `Cooldown=1f` / `Windup=0.3f` / `Recover=0.3f` 默认值 | 技能表（已有），同为第二真源 | 🟠 |
| `Scripts/Config/UnitConfig.cs:155` | `private static readonly string[] RangeProperties = { "AttackRange", "TriggerRadius", "MaxRange", "MaxDistance" };` | 应改为技能表显式列（现在是"猜属性名"） | 🟠 |
| `Scripts/Config/UnitConfig.cs:283` + `AbilityConfig.cs:33` | `AbilityConfig.NodeName` = **场景节点名字符串**（`AbilityDash`/`AbilityMelee`…），按名找节点；场景改名即静默失效 | 需要"技能行 ↔ 能力节点"的稳定绑定 | 🟠 |
| `Scripts/Combat/Projectile.cs:174` | `GD.Load<PackedScene>("res://Scenes/Combat/Explosion.tscn")` 场景路径写死 | 效果表/全局配置 | 🟠 |
| `Scripts/Rewards/RewardEffectRegistry.cs:74` | `ParamText(row,"SummonScene","res://Scenes/Summons/Drone.tscn")` 路径写死 | 同上 | 🟠 |
| `Scripts/Rewards/RewardEffectRegistry.cs:46-49,57,65,75` | 7 个处理器参数的**第二份真源**：`0.12f / 14f / 7f / 3f / 0.12f / 3f / 2f` | 全部应只在表里 | 🟠 |
| `Scripts/Rewards/RunController.cs:657` | `_pool.Roll(_build, 40)` 魔数 40 | 表/常量 | 🟠 |
| `Scripts/Abilities/ChargeAttackAbility.cs:35, 80` | 兜底 `ApproachMaxRange : 14f`；`StateTime > 2f` 硬超时 | 技能表 | 🟠 |

**🟡 = 中等（默认值/常量，量多但每条影响小）（38 条，此处列代表性的 20 条）**

| 文件:行 | 硬编码内容 | 应放哪 |
|---|---|---|
| `Scripts/Combat/Unit.cs:16, 30, 42` | `MaxHealth=100f` / `ArmorDamageReduction=0.3f` / `MoveSpeed=4f` | 单位表 |
| `Scripts/Combat/Unit.cs:235, 237` | 总减伤上限 `0.9f`（两处） | 全局/单位表 |
| `Scripts/Combat/Unit.cs:129` | 组名 `"summon_units"/"player_units"/"enemy_units"` + `HasTag("Summon")` 字符串判定 | 常量/标签表 |
| `Scripts/Combat/AttackInstance.cs:19,23,24,27` | `CritMultiplier=2f` / `HitInterval=0.2f` / `Lifetime=3f` / `ProjectileCount=1` | 攻击/技能表 |
| `Scripts/Combat/AttackInstance.cs:14` + `AttackHitbox.cs:107` + `RewardCatalog.cs:203` | 攻击身份字符串 `"WeaponFire"` / `"Hitbox"` / `"ChainLightning"` | 技能行 Id |
| `Scripts/Combat/AttackHitbox.cs:14,15,17,19,25` | `Damage=10f` / `Knockback=3f` / `HitInterval=0.2f` / `CritMultiplier=2f` / `StatusDuration=3f` | 技能表 `AttackParams` |
| `Scripts/Combat/AttackHitbox.cs:96` | `Mathf.Max(0.2f, offset + halfLength)` 下限 | 常量 |
| `Scripts/Combat/Projectile.cs:21,24,167,194` | `HitMask` / `RicochetRange=9f` / 弹射前移 `0.6f` / 上向量阈值 `0.99f` | 投射物表 |
| `Scripts/Combat/CharacterMovement.cs:14-27` | `GravityScale=1.8` / `MaxFallSpeed=60` / `GroundAcceleration=60` / `AirAcceleration=15` / `JumpVelocity=6.5` / `ImpulseDamping=6` / `TurnSharpness=14` | 角色表/单位表 |
| `Scripts/Combat/BlockComponent.cs:14,17` | `BlockDamageMultiplier=0.2f` / `FrontDotThreshold=-0.35f` | 单位表 |
| `Scripts/Combat/CombatTypes.cs:25-28, 58` | 物理层 `1<<0/1/2`；`EventContext.MaxDepth = 4` | 常量（可接受） |
| `Scripts/Abilities/DashAbility.cs:18-20` | `Distance=6f` / `Duration=0.22f` / `InvulnerableTime=0.18f` | 技能表 |
| `Scripts/Abilities/MeleeAttackAbility.cs:18-20` | `AttackRange=2.3f` / `ActiveTime=0.12f` / `DamageScale=1f` | 技能表 |
| `Scripts/Abilities/AreaAttackAbility.cs:16-18` | `ActiveTime=0.15f` / `TriggerRadius=2.5f` | 技能表 |
| `Scripts/Abilities/BurstFireAbility.cs:14,17` | `Duration=2.5f` / `BurstInterval=0.06f` | 技能表 |
| `Scripts/Abilities/LeapAttackAbility.cs:14-17,34` | `JumpVelocity=9f` / `HorizontalSpeed=8f` / `ActiveTime=0.2f` / `MaxAirTime=3f` / 兜底 `8f` | 技能表 |
| `Scripts/Abilities/ProjectileAttackAbility.cs:14-18` | `MinRange=3f` / `MaxRange=18f` / `AimHeight=0.9f` | 技能表 |
| `Scripts/Abilities/SummonAbility.cs:65` | 召唤物落点 `Y=0.6f` | 技能表/召唤表 |
| `Scripts/Weapons/Weapon.cs:98,100` | 攻速下限 `0.05f` / 装填下限 `0.1f` | 常量 |
| `Scripts/Player/PlayerWeaponInput.cs:15-17` | `MaxAimDistance=200f` / `DefaultFov=78f` / `AimLerpSpeed=14f` | 表现配置 |

**其余 18 条**同属上述模式（各 Ability 子类的 `_stage` 常量、`FaceDirection(dir, 1f)` 瞬时转身、`direction.Y = 0f` 平面化、`LengthSquared() < 0.0001f` 等），详见附录 B 的原始扫描文件。

### 2.3 硬编码的字符串清单（身份/路径/按键/组名）

| 类别 | 位置 | 内容 |
|---|---|---|
| **按键动作名（11 个）** | `PlayerController.cs:33,40`、`PlayerAbilityInput.cs:49,54,60,66`、`PlayerWeaponInput.cs:46,84,102,107` | `move_left/move_right/move_forward/move_back/jump/fire/aim/reload/dash/burst/control/summon` |
| **配置 id 字面量** | `ConfigIndex.cs:361`、`EncounterController.cs:577`、`SelfTest.cs:933,944,949`、`AbilityStore.cs:63`、`BossPerk.cs:162-232`、`BossPerkProbe.cs:150-221`、`MetaProgress.cs:39-114` | `enemy_global`、`skl_dash`、`ai_chaser`、`meta_quota`、7 个 `pmod_*`、`boss_final`、`stg_core`、6 个 `meta_*`、**19 条增幅 id** |
| **场景路径** | `Projectile.cs:174`、`RewardEffectRegistry.cs:74`、`RunController.cs:398`、`ConfigIndexData.cs:290`、`BossPerk*`、`SelfTest` | `res://Scenes/Combat/Explosion.tscn`、`res://Scenes/Summons/Drone.tscn`、`res://Scenes/Levels/Base.tscn`、`res://Assets/Effects/fx_combat_hit.tscn`、各 .tscn |
| **节点名（当契约用）** | `Unit.cs:123,351`、`UnitAnimator.cs:35,38`、`AttackHitbox.cs:196`、`Weapon.cs:107`、`NodeSearch` 调用点 | `"Movement"` / `"Block"` / `"Animator"` / `"Model"` / `"Weapon"` / `"Muzzle"` / `"Attack"` / `"Body"` |
| **组名** | `Unit.cs:129`、`Targeting.cs:10`、`Projectile.cs:138`、`RewardCatalog.cs:218`、`StageController.cs:334` | `player_units` / `enemy_units` / `boss_units` / `summon_units` / `weapons` / `projectiles` |
| **标签名** | `Unit.cs:129`、`Weapon.cs:29`、`RewardCatalog.cs:46-52`、`AttackInstance` 构造点 | `"Summon"` / `"Projectile"` / `"Gun"` / `"Physical"` / `"Lightning"` |
| **UI 文案** | `RunController.cs:262-264,457-459`、`StartScreen.cs:46-48` | 角色/武器标题与描述 |

### 2.4 Group B —— 其它（诚实说明覆盖度）

初筛候选数（机械扫描，**未逐行确认**）：`EncounterController.cs` 56、`StageController.cs` 45、`AbilityStore.cs` 31、`ChallengerMark.cs` 21、`NodePlacement.cs` 18、`BossPerk.cs` 35、`Behavior.cs` 14、`MetaProgress.cs` 13、`BossPhaseController.cs` 8、`PerkQualifier.cs` 8，其余 ≤6。

**⚠️ 我明确说明**：Group B（音频 / UI / 设置 / 世界地图 / 遭遇 / 首领 / 局外成长 / 交互 / 自测）我只做了**机械初筛**，抽样确认了 `ChallengerMark.cs`（21 条全是表现参数：半径 1.15 / 6 个三角 / 自转 0.9 等）、`MetaProgress.cs`（13 条是局外成长系数的**兜底默认值**，与 `meta_*` 表并存）、`BossPerk.cs`（7 个 `pmod_*` id 字面量 + 数值）三类。**其余文件我没有逐行确认**，因此**不给 Group B 的确定条数**——给一个未经核实的数字比不给更糟。若需要 Group B 的精确清单，应单独开一轮同类审计。

---

## 3. ③ 现有战斗管线盘点

### 3.1 `Scripts/Combat/` —— 单位与结算

| 模块 | 职责 | 关键类与文件 | 扩展点 | **承载不了的** |
|---|---|---|---|---|
| 单位 | 生命/护甲/护盾/减伤/状态/死亡/击退；统一命中入口 | `Unit.cs`（519 行，`CharacterBody3D`） | `Tags[]`、`Stats`、事件（`HitReceived/Damaged/Died/KillConfirmed/ArmorBroke/ShieldBroke/KnockbackReceived`） | ① 状态**不能叠层**（`:411-418` 同名只刷时间）② DoT 间隔写死 1s ③ 无"标记"这类**多目标** debuff 容器 ④ `SeedStats()` 键表写死，新增属性必须改代码 ⑤ 玩家会被敌人挡住 |
| 属性 | 基础值 + 修正器（Override→Add→Multiply→Cap） | `StatSheet.cs`（83 行，字符串键） | **键名是字符串，加键不需要改这里**（但要在 `Unit.SeedStats` 给基础值） | 无冷却缩减键、无跳跃高度键、无护甲键 |
| 移动 | 物理与朝向，不做决策 | `CharacterMovement.cs`（253 行） | `SetIntent` / `TryJump` / `BeginDash` / `SetVerticalVelocity` / `FaceDirection` / `AddImpulse` / `ExtraAirJumps` | ① **无奔跑状态**（只有 `SetIntent` 的速度）② **无下落速度修改接口**（滞空 -90% 做不了）③ 无"穿过敌人"开关 ④ 无"突进中开碰撞" |
| 命中体 | 近战/AOE/持续/旋转共用的 `Area3D` | `AttackHitbox.cs`（265 行） | `HitPolicy`、`StatusId`、`Activate/Deactivate`、`HitLanded` 事件、几何最近点命中 | ① **不能斩断子弹**（只查 `GetOverlappingBodies()` 里的 `Unit`）② 形状/参数在预制体里，表只能反射式写属性 ③ 无"半圆/大回旋"这类扫描形状驱动 |
| 投射物 | 射线推进 + 命中结算 + 穿透 + 弹射 + 爆炸 | `Projectile.cs`（197 行） | `Speed`/`GravityScale`/`Pierce`/`Ricochet`/`ExplosionRadius`/`HitMask` | ① **无追踪（homing）** ② 无"弹射 N 次后加伤/全程穿透"标记 ③ 无"斩断/被斩断"交互 ④ 无"射速→下坠时机"规则 ⑤ 无抛物线落点指示 |
| 攻击快照 | 攻击侧属性快照 + 暴击判定 + `HitTracker` | `AttackInstance.cs`（109 行） | `Pierce`/`Ricochet`/`Explosion*`/`Status`/`Tags` | 无"无视格挡"标记、无"每穿透加伤"、无"连续发射" |
| 格挡 | 部分格挡 + 正面判定 + 破防阈值 | `BlockComponent.cs`（48 行） | `Blocking`/`BlockDamageMultiplier`/`FrontDotThreshold`/`BreakThreshold` | ① 无**次数**（护卫"奔跑时获得一次格挡"）② 无**破碎事件/CD**（"碎裂 5 秒后才可再获取"）③ 无"架势期间被命中则规避并退出"（架） |
| 状态 | 状态效果 + 代码内置兜底目录 | `StatusEffect.cs`（101 行） | 效果表 `eff_*`（`EffectConfig`） | 只有 `Slow/Weaken/Burn/Empowered/Stun` + 非状态类 `ChainLightning/Explosion/Lifesteal/SummonOnKill`；**无 Freeze、无 Mark、无分级（+）** |
| 表现 | 受击闪色 + 死亡延迟 | `UnitPresentation.cs`（98 行）、`HitEffect.cs`（122 行） | 递归材质覆盖 | 无脚底光环、无准星指示器、无路径可视化 |
| 动画 | **按移速切 4 段** | `UnitAnimator.cs`（301 行） | `ApplyRow(UnitConfig)`（单位表 6 列）；`Ensure()` 自动挂节点；`Resolve()` 大小写不敏感 + 回退 | ① 只认 `idle/walk/sprint/die` ② **完全无技能动画 / 无事件帧 / 无 speed_scale** ③ `SetLoop` 改**共享**资源（隐患） |
| 其它 | 目标选择/瞄准/精英/召唤寿命/事件总线 | `Targeting.cs`、`AimUtil.cs`、`EliteModifier.cs`、`EliteSkillRandomizer.cs`、`SummonLifetime.cs`、`GameplayEvent.cs`、`CombatTypes.cs`、`NodeSearch.cs` | 事件总线 `GameplayEventBus`（构成增幅"触发式"效果的基础） | 事件种类只有 `Hit/Damaged/Died/Kill`；无"暴击""格挡成功""护甲破碎""落地""蓄力完成"等事件 |

### 3.2 `Scripts/Abilities/` —— 技能

**基类 `Ability`（`Ability.cs`，172 行，`Node3D`）的精确接口**：

```csharp
// 可配置（[Export]，也是技能表能写进去的字段）
AimSource Aim;  float Cooldown;  float Windup;  float Recover;  string AbilityId;
bool Enabled;   float ApproachMinRange;  float ApproachMaxRange;

// 状态
Unit? OwnerUnit;  CharacterMovement? Movement;  Unit? Target;
bool IsBusy;  float CooldownRemaining;  bool IsReady;  float StateTime;
event Action<Ability>? Activated;

// 生命周期（.cs:61-97, 149-164）
_PhysicsProcess(dt) → EnsureInitialized(); CooldownRemaining -= dt; if (IsBusy) StateTime += dt; Tick(dt);
TryActivate(target) → 校验 IsReady && CanActivate → IsBusy=true; StateTime=0; OnActivate(target); Activated?.Invoke
Finish()  → IsBusy=false; StateTime=0; CooldownRemaining = Cooldown;   // ← 冷却在这里落地
Cancel()  → IsBusy=false; StateTime=0;

// 子类覆写点
protected virtual void OnReady();
protected virtual bool CanActivate(Unit? target) => true;
protected abstract void OnActivate(Unit? target);
protected virtual void Tick(float delta) {}
protected Vector3 ResolveAimDirection();   // Facing / MovementIntent / Crosshair
protected T? FindInOwner<T>();
```

**回答具体问题**：

1. **怎么被触发**：玩家 → `PlayerAbilityInput._Process` 里按类分支 `TryActivate()`；敌人 → `EnemyBrain` + `Behaviors/UseAbilityBehavior`（读 `ApproachMinRange/MaxRange` 判"舒适圈"）；Boss → `BossPerk`/`BossPhaseController` 直接 `Enable` 能力。
2. **冷却怎么算**：`Ability._PhysicsProcess` 每物理帧 `CooldownRemaining -= dt`；**`Finish()` 里直接赋值 `Cooldown`，没有任何倍率/缩减**。→ 文档"洞悉（缩短技能冷却）"、"闪身步（击杀减 80% CD）"、"招架反击（格挡成功刷新冷却）"、"神降术（CD 变 2 秒）"——**四类全部无通路**。
3. **能力是组件还是资源**：**是 `Node3D` 组件**，挂在 `Unit` 下、名字是场景里的节点名；`AbilityConfig.NodeName` 按名找。
4. **新能力"种类"怎么注册**：`AbilityId` 默认取 `GetType().Name`（`.cs:113-116`）→ **"新技能种类 = 新 C# 类"**。`AbilityConfig.ClassHint` 只在套用时**校验并警告**，不参与实例化。
5. **config id → C# 类** 的映射在哪：**没有映射表**。关系是反向的：场景里先有节点（带脚本），技能表按 `NodeName` 找节点，`Params` 用 `GodotObject.Set(属性名, 值)` 反射套参数。
6. **9 个子类的 `[Export]` 参数**（每个都是"表里没有就得改代码"）：

| 子类 | 文件 | 参数 | 敌人/首领/召唤物在用？ |
|---|---|---|---|
| `DashAbility` | 60 行 | `Distance` / `Duration` / `InvulnerableTime` | ❌ **仅旧角色**（`skl_dash`） |
| `BurstFireAbility` | 35 行 | `Duration` / `BurstInterval`（按 `Weapon.Automatic` 分两模式） | ❌ **仅旧角色**（`skl_burst`） |
| `MeleeAttackAbility` | 84 行 | `AttackRange` / `ActiveTime` / `DamageScale`；`OnReady` 里 `GetNode<AttackHitbox>("Attack")` | ✅ 6 个敌种 + Boss + 召唤物 + 旧角色（7 条技能行） |
| `AreaAttackAbility` | 70 行 | `ActiveTime` / `KillSelf` / `TriggerRadius` | ✅ 4 敌/Boss + 旧角色 |
| `ProjectileAttackAbility` | 61 行 | `MinRange` / `MaxRange` / `AimHeight`；`GetNodeOrNull<Weapon>("Weapon")` | ✅ `enemy_ranged` |
| `ChargeAttackAbility` | 83 行 | `ChargeSpeed` / `MaxDistance` | ✅ `enemy_charger` + Boss |
| `LeapAttackAbility` | 83 行 | `JumpVelocity` / `HorizontalSpeed` / `ActiveTime` / `MaxAirTime` | ✅ `enemy_jumping_melee` |
| `SummonAbility` | 58 行 | `SummonScene` / `Count` / `Radius` / `MaxAlive`；按 `SummonScene.ResourcePath` 查召唤物表 | ✅ Boss + 旧角色 |
| `BuffAlliesAbility` | 48 行 | `Radius` / `BuffDuration` / `StatusId` / `IncludeSelf` | ✅ `enemy_skill_elite` |

**→ 结论：只有 `DashAbility` 与 `BurstFireAbility` 是"旧角色专属"；其余 7 个是通用基础设施（敌人/首领/召唤物都在用），必须留。**

### 3.3 `Scripts/Weapons/Weapon.cs` —— 武器

- **职责**：攻击节奏（`FireInterval` × `AttackSpeedMultiplier`）、弹匣/被动装填（`AutoReloadDelay`）、一次击发多弹丸（`ProjectilesPerShot` + `MultiShotSpreadDegrees`）、暴击、`AttackTags`、升级池 `PoolId`、`ProjectileScene` 生成投射物。
- **谁在用**：玩家（`PlayerWeaponInput` 驱动）+ **敌人远程兵**（`ProjectileAttackAbility` 的 `Weapon` 子节点）+ 整个投射物管线。
- **承载不了**：① **没有"近战武器"概念**（`Enabled=false` 只是"关掉，让左键交给近战能力"，见 `Weapon.cs:49-50`）② 没有弹匣的武器（能量波/剑/镰刀/长枪）必须绕过整套装填逻辑 ③ **武器不能脱离单位**（镰刀抛出/长枪抛出收回做不了）④ 无"剑气/刀光大小 = 攻击盒大小"的渲染通路 ⑤ 三把旧武器共用同一个预制体 `res://Scenes/Weapons/Pistol.tscn`（`wpn_pistol/rifle/sniper.tres` 的 `ScenePath` 全是它）。

### 3.4 `Scripts/Player/` —— 输入 → 技能

| 文件 | 职责 | 现状 |
|---|---|---|
| `PlayerController.cs`（45 行） | 移动意图 + 跳跃 | `Input.GetVector("move_left", ...)`（`:33`）；`Input.IsActionJustPressed("jump")`（`:40`）。**无奔跑、无空中跳跃按键、无 ctrl** |
| `PlayerAbilityInput.cs`（75 行） | 技能输入 | `switch(child)` 按**类型**收集 4 个能力，再逐个 `JustPressed("dash"/"burst"/"summon"/"control")`。**只有 JustPressed，无法做蓄力** |
| `PlayerWeaponInput.cs`（113 行） | 准星射线 + 开火/装填/瞄准 | 屏幕中心射线（`:65-76`）→ `_weapon.TryFire(aimPoint)`；`"aim"` = 右键 → `SetAiming` + 改 FOV；`Weapon.Enabled=false` 时左键转 `MeleeAttackAbility` |

**按键表在哪**：`project.godot:43` 的 `[input]`，共 12 个动作。语义化动作名 **不存在**（没有 `skill_1` / `skill_2` / `weapon_skill_1` / `weapon_skill_2` / `sprint` / `down_attack`）。文档要求的 `Q/E` 已存在（叫 `dash`/`burst`），`左键` 已存在（叫 `fire`），**`右键` 被 `aim` 占用**，`shift` / `ctrl` 完全没有。

### 3.5 `Scripts/Rewards/` —— 增幅怎么生效

```
Config/Tables/Upgrades/*.tres  (RewardConfig，磁盘行，19 条)
        │  ConfigIndex.BuildTableIndex() → RewardTable.Build(Config.Upgrades)
        ▼
RewardEffectRegistry.Compile(row)   ←  handler 字符串 → C# 委托（6 个已注册）
        │        StatSet / ChainLightning / Lifesteal / BurnOnHit / SummonOnKill / AmmoOnKill
        ▼
RewardCatalog.All  (RewardDefinition[]，运行期视图，带 Action<RewardContext>)
        │
        ├── RewardPool.Initialize(weapon)  → AllowsWeapon 三级回退（专属池 → 武器标签 → 通用）
        └── RewardPool.Roll(build, count, kindFilter/kindSet)
                └── 过滤：Kind / RequiresSummoner / Excludes(双向) / ConflictsWith(单向) / MaxStacks
                        ▼
                  WeightedPick.Pick(...)
                        ▼
              RunBuild.Add(def, ctx) → def.Apply(ctx)  （+ 登记 TriggerRule 子节点）
                        ▼
              TriggerRule（GameplayEventBus 订阅 + 冷却 + 每链次数 + 链深度上限）
```

**已有能力（可直接用）**：
- 三分类：`RewardKind { Enhancement, Evolution, Mutation }` ←→ 文档的"强化/进化/异化"。
- 层数：`MaxStacks`（0=不限）、`Weight`、`MaxStacks` 已由 `RewardPool.cs:62-65` 与 `RunBuild.StacksOf` 实现。
- **互斥两个来源都能表达**：`Excludes`（双向，`RewardPool.IsExcluded`）与 `ConflictsWith`（单向，`RewardPool.cs:67-75`）→ 用户的"逻辑冲突"与"为了不太强"两类都能填表。
- **"限定是用来过滤候选列表的"** —— 现在已经是这个语义：不满足的行**根本不进池**（不是"拿了没效果"）。这一点与用户要求一致。
- 参数逃生口：`ConfigRow.Params[]` + `GodotObject.Set(属性名, 值)`（带"属性不存在则警告并跳过"）。

**承载不了的（缺口清单）**：
1. **限定只有两级且不通用**：`ApplicablePoolIds`（武器专属池）+ `RequiredWeaponTags`（武器标签）+ `RequiresSummoner`（**一个布尔，只认"召唤师"**）。文档要的是 `<A限定>`（角色 A，**不限其 3 把武器**）/ `<A1限定>`（角色 A + 武器 1）/ `<B限定>` 的**层级标签体系**。
2. **没有「前置」**：`RewardConfig.cs:56-65` 只有两个**否定**字段（`Excludes` / `ConflictsWith`），没有任何"必须已拥有 X 才出现"的正向字段。文档里 `弹射+`/`穿透+`/`增生+`/`烈阳+`/`冰晶+`/`大刀阔斧+`/`剑突+`/`速攻+`/`专一`/`神降术` 等十余条都依赖它。
3. **handler 是 C# 闭包**：新增一个"改行为"的增幅 = 新增一个 C# handler + 一个 `RewardEffectRegistry` 条目（`RewardEffectRegistry.cs:22-81` 的字典）。**这条本身可接受**（表仍是可校验数据），但意味着**62 条新增幅里的"行为型"部分需要 C# 代码**，不是纯填表。
4. **`TriggerRule` 可用的事件太少**：`GameplayEventKind` 只有 `Hit/Damaged/Died/Kill`（`GameplayEvent.cs`）。文档需要"暴击时"（巡飞弹）、"格挡成功时"（招架反击）、"护甲破碎时"（能量护盾）、"落地时"（落剑式/地爆）、"蓄力完成时"（完美时机）——**都要新增事件种类**。

### 3.6 `Scripts/AbilityCards/` —— 这是**另一个系统**（局外装备层）

- `AbilityStore.cs`（693 行，static）：局外解锁/升级/激活/额度，存 `user://ability_cards.cfg`；`QuotaRowId = "meta_quota"` 硬编码（`:63`）。
- `AbilityEffects.cs`（218 行）：本局开局把激活的卡套到玩家（`ApplyToPlayer`），三种效果 `StatAdd` / `StatMultiply` / `GrantSkill`。
- **与"增幅"的关系**：`AbilityCardConfig.cs:29-43` 的文档注释明确写了"两者是**两层不同的东西**"：卡片是**局外**（跨局，`user://`），技能行是**局内**。唯一接触点是 `AbilityCardConfig.Skill` 这个**资源引用**。
- **对本次重构的意义**：它**不属于**角色的技能/武器/增幅系统，**但会间接受害**——`card_control` 与 `card_summon` 是 `GrantSkill` 型，分别引用 `skl_control` / `skl_summon`；这 5 条旧角色技能行一旦删掉，这 2 张卡的引用悬空（Godot 置 null → `ConfigIndex.cs:289` 告警 → 卡静默无效）。**用户没说清 → 标为待确认（见 §5.4）**。

### 3.7 `Scripts/Config/` —— 配置框架

| 概念 | 实现 | 说明 |
|---|---|---|
| 一行一个文件 | `ConfigRow`（抽象 `Resource`，`Id/Slot/DisplayName/DescriptionTemplate/Note/Params[]`） | `Config/Tables/<分组>/*.tres`，一个文件夹 = 一张表 |
| 表的持有者 | `ConfigIndexData`（`Resource`，`[GlobalClass]`） | 12 个 `XxxConfig[]` 索引数组 + 全局标量 |
| 唯一入口 | `ConfigIndex`（autoload，`Config/ConfigIndex.tres`） | `Settings` 静态属性；`FileLoaded` 标记"表是否真加载" |
| 索引 | `ConfigRows`（`Rebuild` / `ById<T>` / `BySlot<T>` / `ByScenePath<T>` / `SetKind` / `Count<T>`） | Id / Slot / ScenePath 三种查询 |
| 套用 | `ConfigRow.ApplyTo(Node)`（虚方法，各子类实现） | 走 `GodotObject.Set(属性名, 值)`，属性不存在 → 警告并跳过 |
| 回退 | **表是权威、场景/代码默认值是回退**；读不到行就**什么都不做** | 每张表都有"来源：表 / 代码默认值"的日志留痕 |

**承载不了的**：
- **没有按键列**（`AbilityConfig.cs:38-40` 刻意没有）。
- **没有"限定/前置"字段**（`RewardConfig` 只有否定式互斥）。
- **没有"技能 → 动画"字段**（`AbilityConfig` 有 `NodeName`/`ClassHint`，没有一个动画相关的列；`UnitConfig` 只有 `AnimIdle/Walk/Run/Death` 4 个名字 + 2 个阈值）。
- **没有"效果 → 结算间隔"字段**（`EffectConfig` 无 `TickSeconds`）。
- **没有槽位/层数/蓄力/完美时机相关字段**。

---

## 4. ④ 差距对照表（核心产出）

### 4.1 文档条目清点

| 分组 | 条目数 | 明细 |
|---|---|---|
| 通用机制 | **12** | 按键默认表、动画事件帧驱动、参数全入表、跳跃、空中跳跃、奔跑、术语"增幅"、限定、互斥、前置、技能层数、蓄力 |
| 角色 A［猎手］ | **13** | 被动[灵动] 1 + 技能[后撤][跃升] 2 + 武器 3 + 武技 6 + 滞空 1 |
| 角色 B［武器大师］ | **15** | 被动[落] 1 + 技能[闪][架] 2 + 武器 3 + 武技 8 + 剑气 1 |
| 进化池 | **23** | A 侧 11（乱金柝/有备无患/专注/发散/爆裂火球/专一/花心/心流/变奏/心眼/龙之箭）+ B 侧 12（地爆/闪身步/招架反击/金光术/大拔刀术/神降术/怜悯/普渡/喜爱/气贯长虹/金光大道/浮光跃金） |
| 异化池 | **30** | 通用 12 + \<A限定\> 9（弹射/穿透/增生 各 3 级）+ \<B限定\> 9（大刀阔斧/剑突/速攻 各 3 级） |
| 强化池 | **9** | 力量/生命/快速/暴击/爆伤/洞悉/护甲/攀高/坚硬 |
| **合计** | **102** | |

### 4.2 机制能力对照（57 项，逐条给证据）

**判定口径**：`可直接用` = 现有系统零代码改动即可承载；`需改造` = 组件可复用但有结构性缺口；`需新建` = 现有系统完全没有。

| # | 文档要求 | 现有系统能否承载（证据） | 判定 | 工作量 | 风险 |
|---|---|---|---|---|---|
| M1 | 按键映射（技能1=Q/技能2=E/武技1=左键/武技2=右键/额外键由技能定义/没写的=被动） | ❌ 按键按**能力类**硬编码（`PlayerAbilityInput.cs:26-66`）；`AbilityConfig.cs:38-40` 明确"刻意没有 InputAction 列"；右键=`aim`（`PlayerWeaponInput.cs:84`） | 改造 | 中 | 高（与瞄准功能冲突，需要重新分配右键） |
| M2 | 技能 → 动画映射 | ❌ 全项目零处技能动画；`UnitAnimator` 只认 4 个名字（`.cs:42-51`） | **新建** | 大（26 段） | 中（已实测可行） |
| M3 | 动画事件帧驱动效果 | ❌ `grep method_tracks\|Call Method` = **0 命中**；所有 `Ability` 都是 `StateTime` 计时 | **新建** | 大 | 中（API 已实测） |
| M4 | 加速动画释放（`speed_scale`） | ❌ `grep speed_scale\|SpeedScale` → 项目里**没有一处** `AnimationPlayer.SpeedScale` | **新建** | 小 | 低（实测 498→262ms） |
| M5 | 参数全入表 | ✅ 17 张表 + `ConfigRow.Params[]` 逃生口 + 反射套用 | 可直接用 | — | — |
| M6 | 跳跃 | ✅ `CharacterMovement.TryJump()` + 角色表 `JumpVelocity`/`JumpGravityScale`（`CharacterConfig.cs:50,59`） | 可直接用 | — | — |
| M7 | 空中跳跃（次数上限，最多 3） | 🟡 `ExtraAirJumps` 存在（`CharacterMovement.cs:21`），**但 17 张表无此列**，且无运行期增减接口 | 改造 | 小 | 低 |
| M8 | 奔跑（移动中按 shift 进入，放技能/状态即退出） | ❌ 无 `sprint` 动作；无"奔跑状态"；只有动画按速度选 `sprint` | **新建** | 中 | 中 |
| M9 | 技能层数（每次消耗一层，间隔 0.5s） | ❌ `Ability` 只有 `CooldownRemaining`；`RunBuild` 的 stacks 是增幅层数，不是技能层数 | **新建** | 小 | 低 |
| M10 | 蓄力（按住激活 / 抬起释放 / 基础蓄力前抬起不释放） | ❌ 输入只有 `JustPressed`；无蓄力态；无"抬起"事件 | **新建** | 中 | 中 |
| M11 | 完美时机（完成后短暂窗口） | ❌ 无 | **新建** | 小 | 低 |
| M12 | 滞空（空中按住跳跃 → 下降 −90% + 脚底光环） | ❌ `CharacterMovement` **无下落速度修改接口**；无光环节点 | **新建** | 中 | 中 |
| M13 | 标记 buff（可叠加、不限单目标） | ❌ `Unit.ApplyStatus` "同名刷新时间，**不叠加层数**"（`Unit.cs:411-418`）；无 per-target 容器 | 改造（近于重写） | 中 | 高 |
| M14 | 下落攻击（高度越高伤害越高） | ❌ 无落地事件、无"高度→伤害"；`LeapAttackAbility` 是主动扑击不是下砸 | **新建** | 中 | 中 |
| M15 | 近战突进（攻击时移动；高等级可穿过敌人） | 🟡 `BeginDash`/`ChargeAttackAbility` 可复用；但玩家 `CollisionMask` 含 `UnitMask`（`Unit.cs:126`）→ **会被敌人挡住** | 改造 | 中 | 中 |
| M16 | 武器抛出 / 收回（镰刀 / 长枪） | ❌ `Weapon` 是单位子节点，无脱手/回收/旋转周伤 | **新建** | 大 | 高 |
| M17 | 格挡（奔跑获得一次，破碎 5s 后可再获取） | 🟡 `BlockComponent` 有倍率/正面/破防（`BlockComponent.cs:11-20`），**无次数、无破碎事件、无 CD** | 改造 | 中 | 中 |
| M18 | 架势（0.75s 内被命中则规避并退出） | ❌ 无架势状态；`BlockComponent` 只按倍率减伤，**不"规避"也不"退出"** | 改造+新建 | 中 | 中 |
| M19 | 格挡/护甲破碎时对周围造成范围伤害 | 🟡 `Unit.ArmorBroke`/`ShieldBroke` 事件已存在（`Unit.cs:90-91`）但**无人订阅做伤害**；`BlockComponent` 无破碎事件 | 改造 | 小 | 低 |
| M20 | 近战斩断子弹（含友军，但无法斩断能量波） | ❌ `AttackHitbox` 只遍历 `GetOverlappingBodies()` 里的 `Unit`（`.cs:135-137`） | **新建** | 中 | 中 |
| M21 | 子弹弹射 | ✅ `Projectile.TryRicochet()`（`.cs:133-170`）+ `AttackInstance.Ricochet` + `RicochetAdd` | 可直接用 | — | — |
| M22 | 子弹穿透 | ✅ `Projectile.Pierce` + `AttackInstance.Pierce` + `PierceAdd`（`.cs:115-121`） | 可直接用 | — | — |
| M23 | 穿透+（每穿透 +伤害）/ 穿透++（全程穿透）/ 弹射 n 次计数 | ❌ 无"每穿透加伤"、无"全程穿透"标记 | 改造 | 小 | 低 |
| M24 | 增生（每次发射后**额外连续**发射 N 个） | 🟡 `ProjectilesPerShot` 是**同时**多发（`Weapon.cs:206-217`），不是连续发射 | 改造 | 小 | 低 |
| M25 | 短弩：射速决定下坠时机 / 高射速造成击退 | 🟡 `Projectile.GravityScale` 有重力，但无"射速→下坠/击退"规则 | 改造 | 小 | 低 |
| M26 | 能量波穿透格挡 / 剑气可被格挡 | ❌ `AttackInstance`/`HitInstance` 无 bypass 标记 | **新建** | 小 | 低 |
| M27 | 燃烧（每 0.5s 一跳，持续 3s） | 🟡 `Burn` 状态存在（表 `eff_burn.tres` 6 dps / 3s），但**间隔写死 1s**（`Unit.cs:369`），`EffectConfig` 无 `TickSeconds` | 改造 | 小 | 中 |
| M28 | 冰冻（无法动弹 1s，每 10s 最多一次） | ❌ 无 Freeze；`Stun` 只是"移速×0.12 + 攻速×0.5"（`StatusEffect.cs:89-98`），不是"无法动弹"，也无内建 CD | **新建** | 中 | 中 |
| M29 | 烈阳+/冰晶+（前置 + 更强） | ❌ 无前置机制；状态参数无分级 | **新建** | 小 | 低 |
| M30 | 巡飞弹（暴击伤害时发射追踪飞弹，伤害 50） | ❌ 无"暴击"事件（`GameplayEventKind` 只有 Hit/Damaged/Died/Kill）；`Projectile` 无追踪 | **新建** | 中 | 中 |
| M31 | 灵魂收割（击杀概率掉落"灵魂"，接触拾取回血） | ❌ 无掉落物/拾取系统 | **新建** | 中 | 中 |
| M32 | 落日剑（施加上 debuff，头顶悬剑延时落下） | ❌ 无 | **新建** | 中 | 中 |
| M33 | 闪电链（**造成伤害时概率**触发） | 🟡 `ChainLightningRule` 存在，但只在**击杀**时触发（`RewardCatalog.cs:172-174`），且**无概率**参数 | 改造 | 小 | 低 |
| M34 | 准星蓄力指示器（往准星收缩、闭合变色、短暂后恢复） | ❌ | **新建** | 中 | 低（纯表现） |
| M35 | 路径可视化（冲刺直线 / 抛物线 + 落点指示） | ❌ | **新建** | 中 | 低（纯表现） |
| M36 | 剑气（穿透路径敌人 / 飞行短 / 大小等比攻击盒） | 🟡 可用 `Projectile` + `BasePierce`；但"大小等比攻击盒"无通路 | 改造 | 中 | 中 |
| M37 | 大刀阔斧（改 B 角色的**攻击范围**：伤害碰撞 + 显示物，模型不变） | ❌ `UnitConfig.ApplyAttackTo` 只反射写能力的 `AttackRange/TriggerRadius/MaxRange/MaxDistance`（`UnitConfig.cs:155,252-261`），**不改碰撞盒尺寸、不改刀光/武器模型缩放** | 改造 | 中 | 中 |
| M38 | 速攻（缩短 CD **并加快动画速度**） | 🟡 `AttackSpeedMultiplier` 只改间隔（`Weapon.cs:96-98`）；**没有任何动画速度通路** | 改造 | 中 | 中 |
| M39 | 洞悉（缩短角色技能冷却） | ❌ `Ability.Finish()` 直接 `CooldownRemaining = Cooldown`；无 CDR 键 | **新建** | 小 | 中 |
| M40 | 攀高（提升跳跃高度） | 🟡 角色表给**绝对** `JumpVelocity`（`CharacterConfig.cs:50`，注释明确"不做倍率运算"）；无"提升 %"通路 | 改造 | 小 | 低 |
| M41 | 强化"护甲" | 🟡 `Unit.GrantArmor` 存在（`Unit.cs:174-184`）且护甲无上限，但**没有对应 StatSheet 键、没有 Reward handler** | 改造 | 小 | 低 |
| M42 | 暴击基础 5% / 爆伤基础 100 | 🟡 现为 `CritChance = 0.15`（`Weapon.cs:24` + 3 张武器表）与 `CritMultiplier = 2f`（即 +100%，这项**对**） | 改造（改数据） | 小 | 低 |
| M43 | **限定**（角色 / 武器 / 角色+武器 三层标签） | 🟡 只有 `ApplicablePoolIds`（武器池）+ `RequiredWeaponTags` + `RequiresSummoner`（一个布尔）。`<A限定>`（不限武器）**无法表达** | 改造 | 中 | 高（这是候选列表正确性的核心） |
| M44 | **前置**（拿了 X 才可能出现） | ❌ `RewardConfig` 只有 `Excludes` 与 `ConflictsWith` 两个**否定**字段（`.cs:57,65`） | **新建** | 小 | 中 |
| M45 | **互斥**（逻辑冲突 + 平衡限制两个来源） | ✅ `Excludes`（双向，`RewardPool.cs:88-107`）+ `ConflictsWith`（单向，`.cs:67-75`） | 可直接用 | — | ⚠️ 需先修 id 漂移（`RewardCatalog.cs:51` vs 表） |
| M46 | "进化/异化/强化"三分类统称增幅 | ✅ `RewardKind { Enhancement, Evolution, Mutation }` | 可直接用 | — | — |
| M47 | 层数上限 / 抽取概率 | ✅ `MaxStacks` + `Weight` + `WeightedPick`（`RewardPool.cs:62-85`） | 可直接用 | — | — |
| M48 | **加角色/武器不改代码** | ❌ `WeaponPresetKind`/`PlayerCharacterKind` 枚举 + `(int)kind + 1` 对齐 Slot（`RunController.cs:444,448`）+ `StartScreen.cs:46-48` 三张写死的卡 + `DefaultCharacterTitle` switch | 改造（**阻塞**） | 大 | **高** |
| M49 | 技能参数表（冷却/前摇/后摇/子类参数） | ✅ `AbilityConfig` + `Params[]` 反射套用 | 可直接用 | — | — |
| M50 | 技能 ↔ 能力节点绑定 | ✅ `AbilityConfig.NodeName`；⚠️ 但 = **场景节点名契约**，改名即静默失效（有 warning） | 可直接用（脆弱） | — | 中 |
| M51 | 敌人/Boss/召唤物共用同一套单位表 | ✅ `UnitConfig` 是 `EnemyConfig`/`BossConfig`/`SummonConfig`/`CharacterConfig` 的基类 | 可直接用 | — | — |
| M52 | 效果参数表（击退/燃烧/减速/眩晕/爆炸/吸血/召唤） | ✅ `EffectConfig` 单表 + `EffectKind` 11 个取值（`EffectConfig.cs:8-42`） | 可直接用 | — | — |
| M53 | DoT 结算间隔入表 | ❌ 无 `TickSeconds` 列；`Unit.cs:369` 写死 1f | 改造 | 小 | 低 |
| M54 | 状态叠层 | ❌ `ApplyStatus` 只刷新（`Unit.cs:411-418`） | 改造 | 小 | 中 |
| M55 | 系统性子类参数走逃生口 | ✅ `ConfigRow.Params[]`（`AbilityConfig.cs:123-138` 带"属性不存在则警告"） | 可直接用 | — | — |
| M56 | 抛物线弹道（长虹贯日） | 🟡 `Projectile.GravityScale` 提供重力；无抛物线+落点指示 | 改造 | 中 | 中 |
| M57 | "刹那之眼"蓄力期被击中 → 无效化并强制触发**不消耗层数**的后撤 | ❌ 依赖 M9（层数）与 M10（蓄力），两者都没有 | **新建** | 中 | 中 |

### 4.3 结论性数字

| 维度 | 数字 |
|---|---|
| 文档可枚举**内容条目** | **102** 条（角色行为 28 + 增幅 62 + 通用机制 12） |
| 文档可枚举**机制项** | **57** 项（M1–M57） |
| 现有系统**可直接承载** | **12** 项机制（M5, M6, M21, M22, M45, M46, M47, M49, M50, M51, M52, M55）＝ 21% |
| 现有系统**需改造** | **23** 项机制（M1, M7, M13, M15, M17, M18, M19, M23, M24, M25, M27, M33, M36, M37, M38, M40, M41, M42, M43, M48, M53, M54, M56）＝ 40% |
| 现有系统**需新建** | **22** 项机制（M2, M3, M4, M8, M9, M10, M11, M12, M14, M16, M20, M26, M28, M29, M30, M31, M32, M34, M35, M39, M44, M57）＝ 39% |
| 102 条内容里，**机制就位后纯填表的** | 约 **62 条增幅**（全部是表行）+ 部分技能参数；**但**其中"行为型"增幅需新的 C# handler |
| **旧内容可复用的条数** | **0 条**。现有 19 条增幅（4 进化 + 5 异化 + 10 强化）与新设计**没有一条同名同义**；现有 3 角色 / 3 武器 / 25 技能行同样无一条可复用 |

**判定**：**这是一次"重写"，不是"扩展"。** 现有可承载的 21% 主要是基础设施（配置框架、投射物穿透/弹射、互斥、单位表继承），**没有一条是文档要求的玩法机制**。

---

## 5. ⑤ 清除清单（一次性重构）

### 5.1 要删除的表行（**逐条判断"是不是旧角色"**）

| 表 | 总行数 | 删 | 留 | 理由 |
|---|---|---|---|---|
| `Config/Tables/Characters/` | 3 | **3**（`chr_gunner`/`chr_heavy`/`chr_summoner`） | 0 | 全是旧角色。**表的类型 `CharacterConfig` 要留**，换成新的角色 A/B 行 |
| `Config/Tables/Weapons/` | 3 | **3**（`wpn_pistol`/`wpn_rifle`/`wpn_sniper`） | 0 | 全是旧武器（且三行共用 `Pistol.tscn`）。换成 6 把新武器行 |
| `Config/Tables/Skills/` | **25** | **仅 5**：`skl_dash`, `skl_burst`, `skl_melee`, `skl_summon`, `skl_control` | **20** | ⚠️ **见下方分解，这是最容易误删的地方** |
| `Config/Tables/Upgrades/` | 19 | **19** | 0 | 4 进化 + 5 异化 + 10 强化，与新设计无一条对应 |
| `Config/Tables/UpgradePools/` | 3 | **3**（`pool_pistol`/`pool_sniper`/`pool_rifle`） | 0 | 与新武器对应关系全变 |
| `Config/Tables/AbilityCards/` | 8 | **待确认**（见 §5.4） | — | 与"增幅"是两个系统，用户没说清 |
| `Config/Tables/Effects/` | 9 | **建议全留** | 9 | `Burn/Slow/Weaken/Stun/Empowered/Explosion/Lifesteal/SummonOnKill/ChainLightning` 都是通用机制。新增 `Freeze`、`Mark` 行 |
| `Config/Tables/Enemies/` | 10 | **0** | 10 | 敌人系统，**不在本次范围** |
| `Config/Tables/Bosses/` | 1 | **0** | 1 | 同上 |
| `Config/Tables/Summons/` | 1 | **0** | 1 | 同上（`summon_drone`） |
| `Config/Tables/AiProfiles/` | 11 | **0** | 11 | 敌人 AI，不在范围 |
| `Config/Tables/Pressure/` | 7 | **0** | 7 | Boss 压力词条 |
| `Config/Tables/Meta/` | 6 | **1 待确认**（`meta_reload`） | 5 | `meta_reload`（装填速度）对新武器无意义；其余 5 条是通用强化 |
| `Config/Tables/Nodes/`, `ValueTags/`, `Stages/`, `Audio/` | 8/3/3/13 | **0** | 全部 | 关卡/节点/音频，与本次无关 |

**`Skills` 表 25 行分解（务必按此逐行操作）**：

| 分类 | 条数 | id | 谁在用 | 处置 |
|---|---|---|---|---|
| **旧角色技能** | **5** | `skl_dash`, `skl_burst`, `skl_melee`, `skl_summon`, `skl_control` | 只有 `chr_*` 三行（`Skills=[2_skl..6_skl]` 全引这 5 条）+ ⚠️ `card_control`/`card_summon` | **删**（但要先处理 2 张卡） |
| 敌种技能 | 11 | `skl_enemy_charger_charge`, `..._chaser_melee`, `..._elite_buff`, `..._elite_control`, `..._elite_melee`, `..._exploder_blow`, `..._fast_melee_melee`, `..._guard_melee`, `..._jumping_leap`, `..._ranged_shot`, `..._screamer_scream` | 9 个敌种行（一一对应） | **保留** |
| Boss 技能 | 4 | `skl_boss_charge`, `skl_boss_melee`, `skl_boss_scream`, `skl_boss_summon` | `boss_final` | **保留** |
| **Boss 行为词条** | **4** | `bhv_awakened`, `bhv_charging`, `bhv_screaming`, `bhv_summoner` | `BossPerkCatalog.Build(Config.Skills, Config.PressureMods)`（`ConfigIndex.cs:102`）；靠 `ApplyHandler` 非空区分（`AbilityConfig.cs:79-85`） | **保留**（它们**不是**"旧角色"，是 Boss Build） |
| 召唤物技能 | 1 | `skl_drone_melee` | `summon_drone` | **保留** |

> ⚠️ **用户点名的疑问（4 条 `bhv_*` + 16 条敌人技能"是旧角色的吗？"）——明确回答：都不是。**
> `bhv_*` 是 `ApplyHandler` 非空的 **Boss Build 行为词条**（`ConfigIndex.cs:100-102`、`BossPerkCatalog` 编译成 Boss 实际获得的战斗行为）；16 条（11 敌种 + 4 Boss + 1 召唤物）是**敌人/首领/召唤物的技能**。删掉它们 = 一次性清掉整个敌人与 Boss 技能系统。

### 5.2 要删除的代码（**逐个列出"谁在用"**）

| 文件（行数） | 是否旧角色专属 | 谁在用 | 处置 |
|---|---|---|---|
`Scripts/Abilities/DashAbility.cs` (60) | ✅ **是** | 仅 `skl_dash` → `chr_*`；`Scenes/Player/Player.tscn` 的 `AbilityDash` 节点 | **可删**（或改造复用为「闪」/「后撤」） |
`Scripts/Abilities/BurstFireAbility.cs` (35) | ✅ **是** | 仅 `skl_burst` → `chr_*`；`Player.tscn` 的 `AbilityBurst` 节点 | **可删**（"火力倾泻"在新设计里没有对应物） |
`Scripts/Abilities/MeleeAttackAbility.cs` (84) | ❌ 否 | **7 条技能行**：`skl_boss_melee`/`skl_drone_melee`/`skl_enemy_chaser_melee`/`skl_enemy_elite_melee`/`skl_enemy_fast_melee_melee`/`skl_enemy_guard_melee`/`skl_melee` | **必须留**（B 角色的"破剑式/憎恶/横扫千军"很可能基于它改造） |
`Scripts/Abilities/AreaAttackAbility.cs` (70) | ❌ 否 | 4 条：`skl_boss_scream`/`skl_enemy_elite_control`/`skl_enemy_exploder_blow`/`skl_enemy_screamer_scream`（+ 旧 `skl_control`） | **必须留** |
`Scripts/Abilities/ChargeAttackAbility.cs` (83) | ❌ 否 | `skl_boss_charge`/`skl_enemy_charger_charge` | **必须留** |
`Scripts/Abilities/LeapAttackAbility.cs` (83) | ❌ 否 | `skl_enemy_jumping_leap` | **必须留** |
`Scripts/Abilities/ProjectileAttackAbility.cs` (61) | ❌ 否 | `skl_enemy_ranged_shot` | **必须留** |
`Scripts/Abilities/SummonAbility.cs` (58) | ❌ 否 | `skl_boss_summon` + 旧 `skl_summon` | **必须留** |
`Scripts/Abilities/BuffAlliesAbility.cs` (48) | ❌ 否 | `skl_enemy_elite_buff` | **必须留** |
`Scripts/Abilities/Ability.cs` (172) | 基类 | 全部 | **留 + 大改**（加事件帧/蓄力/层数/冷却缩减） |
`Scripts/Weapons/Weapon.cs` (321) | 玩家+敌人共用 | 玩家武器 + `enemy_ranged` 的 `Weapon` 子节点 + 整条投射物管线 | **留**（新武器要复用其发射/投射物部分，"装填/弹匣"部分按武器类型条件化） |
`Scripts/Combat/*`（26 个文件） | 通用 | 玩家+敌人+Boss+召唤物 | **全留**；`Unit.cs`/`UnitAnimator.cs`/`CharacterMovement.cs`/`AttackHitbox.cs`/`Projectile.cs`/`StatusEffect.cs`/`BlockComponent.cs` 需改造 |
`Scripts/Rewards/RewardCatalog.cs` (344) | ✅ 是 | `RewardPool.cs:21`、`RunController.cs:409`、`SelfTest.cs:871` | **兜底目录整段替换**；但 `TriggerRule` 的 5 个实现（`ChainLightningRule`/`LifestealRule`/`BurnOnHitRule`/`AmmoOnKillRule`/`SummonOnKillRule`）是**可复用的基础设施**，建议拆到单独文件保留 |
`Scripts/Rewards/RewardEffectRegistry.cs` (151) | 框架 | — | **留 + 扩**（新增前置/限定判定、新增 handler） |
`Scripts/Player/PlayerAbilityInput.cs` (75) | ✅ 是 | 仅玩家 | **重写**（表驱动按键） |
`Scripts/Player/PlayerWeaponInput.cs` (113) | 部分 | 仅玩家 | **重写**（右键让位给武技2；瞄准改绑） |
`Scripts/Player/PlayerController.cs` (45) | 部分 | 仅玩家 | **改造**（加 shift 奔跑 / 空中跳跃 / ctrl） |
`Scripts/UI/StartScreen.cs` (128) | ✅ 是 | 基地开场选武器（3 张写死的卡） | **重写**（按武器表生成卡片） |
`Scripts/UI/LoadoutPreview.cs` (170) | 部分 | 角色/武器预览 | **改造**（`Apply(PlayerCharacterKind, WeaponPresetKind)` 签名要换） |
`Scripts/Config/ConfigIndexData.cs` (333) | 部分 | 全局 | **改造**：删两个枚举；其余全局标量保留 |
`Scripts/Testing/SelfTest.cs` (1232) | ✅ 强耦合 | 自测 | **必改**：`:871` 断言"共 19 条（10/4/5）/ `All[0].Id == "upg_dmg_up"` / 枪手期望 18 条"；`:944` `ById<AbilityConfig>("skl_dash")`；`:100` `BeginRunWithCharacter(Gunner, Pistol)` → **删旧角色/增幅后这些断言必然全红** |
`Scripts/Testing/_FeelProbe.cs` (693) | 部分 | 手感探针 | 同上（`:663-664` 用旧角色/武器枚举） |

### 5.3 `ConfigIndex.tres` 同步（**最高风险项**）

现状：`Config/ConfigIndex.tres`，**20289 字节、134 个 `[ext_resource]`、12 个索引数组**（行号实测）：

| 行 | 数组 | 现有元素数 | 本次要动 |
|---|---|---|---|
| 155 | `Stages` | 3 | 不动 |
| 156 | `Weapons` | 3 | → 6（全换） |
| 157 | `Characters` | 3 | → 2（全换） |
| 158 | `Enemies` | 10 | 不动 |
| 159 | `Effects` | 9 | → 11（加 Freeze/Mark） |
| 160 | `Nodes` | 8 | 不动 |
| 161 | `ValueTags` | 3 | 不动 |
| 162 | `Upgrades` | 19 | → 62（全换） |
| 163 | `UpgradePools` | 3 | → 新（按新武器） |
| 164 | `MetaUpgrades` | 6 | → 5（去 `meta_reload`，待确认） |
| 165 | `AbilityCards` | 8 | **待确认** |
| 167 | `Audio` | 13 | 不动 |
| 190 | `Skills` | 25 | → 20 + 新玩家技能 |
| 191 | `PressureMods` | 7 | 不动 |
| 192 | `Bosses` | 1 | 不动 |
| 193 | `Summons` | 1 | 不动 |
| 194 | `AiProfiles` | 11 | 不动 |

**⚠️ 项目已经栽过的坑（代码注释原文，`ConfigIndexData.cs:129-132`）**：

> `**不要单独删/改被引用到的行文件** —— 只要 ConfigIndex.tres 引用的任何一个行文件缺失/改名，Godot 会**整份 ConfigIndex.tres 加载失败**（不是只丢那一行），于是所有数值静默回到代码默认值。要下线一行，就把这里的数组项与顶部对应的 `[ext_resource]` **一起**删掉。`

**纪律（必须写进实施规范）**：
1. **先备份** `ConfigIndex.tres`（进 git 或 `.bak`）。
2. 每次改完，**必须查 `ConfigIndex.FileLoaded` 与 `ConfigIndex.cs:125-127` 那条日志**（`配置表：武器 N / 角色 N / … /（ConfigIndex.tres 已加载 / 未加载（全部数值回退到代码默认值））`）——这是唯一能证明"没静默回退"的手段。
3. **推荐重建而非就地编辑**：先删掉所有要被替换的行文件与 `[ext_resource]`，再让 Godot 重新生成引用；或用一个 `[Tool]` 脚本按目录重建索引数组。
4. `Config/Tables/Config.csv` 与 `Config.csv.import` 也在同目录（资源表格视图插件的产物）→ 改表后需确认它不会被当作真源。

### 5.4 ⚠️ **待确认**：`AbilityCards`（8 张）与 `meta_reload`

用户原话是"旧角色都清了，**表和代码也是**"，但**没有提到能力卡**。它的性质是：

- **是两个系统**：能力卡 = **局外**（跨局、存 `user://ability_cards.cfg`、有额度/解锁/升级）；增幅 = **局内**（本局构筑）。`AbilityCardConfig.cs:29-43` 有完整论证。
- **其中 2 张会被动受害**：`card_control`(GrantSkill→`skl_control`)、`card_summon`(GrantSkill→`skl_summon`)。删那 5 条技能行后，`AbilityCardConfig.Skill` 的引用会变 null，`ConfigIndex.cs:287-292` 会告警："能力卡 X 是授予技能型，但没填 Skill（技能行）—— 这张卡激活后不会有任何效果。"
- **另 6 张语义部分失效**：`card_crit`(CritChanceAdd)、`card_damage`(DamageMultiplier)、`card_magazine`(MagazineSizeAdd)、`card_reload`(ReloadSpeedMultiplier)、`card_speed`(MoveSpeedMultiplier)、`card_vitality`(MaxHealthAdd)。其中 **`MagazineSizeAdd` 与 `ReloadSpeedMultiplier` 依赖"弹匣/装填"**，而新设计的 6 把武器里**能量波 / 剑 / 镰刀 / 长枪都没有弹匣** → 这两张卡对新武器无意义（`meta_reload` 同理）。

**→ 需要用户裁定 3 件事**：① 能力卡系统整体留不留？② 若留，`card_control`/`card_summon` 是删卡还是改指新技能？③ `meta_reload` 与"装填"相关条目是否一并下线？

### 5.5 其它必须同步的东西

| 项 | 位置 | 为什么 |
|---|---|---|
| 输入映射 | `project.godot:43-113` | 要加 `sprint`(shift)、`down_attack`(ctrl)、语义化的技能槽动作；右键 `aim` 要重新分配 |
| 玩家场景 | `Scenes/Player/Player.tscn` | 只有 `AbilityDash`/`AbilityBurst`/`AbilityMelee`/`AbilitySummon`/`AbilityControl` 5 个能力节点，且 `Animator` 挂在 `Player` 下、`AnimationPlayer` 在 `Model` 子树内。新技能要新节点 → 节点名是 `AbilityConfig.NodeName` 的契约 |
| 武器场景 | `Scenes/Weapons/Pistol.tscn` | 3 把旧武器共用它。新设计 6 把武器需要各自的预制体/模型（`素材\Voxel Weapons.zip` 里有 Bow/Pistol/Rifle/Sniper/Sword/Sword2 静态网格，**无动画**，可作武器模型但不含动作） |
| 自测与探针 | `Scripts/Testing/SelfTest.cs`、`_FeelProbe.cs`、`Scripts/Boss/BossPerkProbe.cs` | 硬编码断言与旧枚举 |
| 文档 | `docs/` 下 27 个 md | `config-schema.md` / `config-tables-inventory.md` / `rewards.md` / `combat.md` / `ability-cards.md` 等描述的都是旧结构，会与实现脱节 |
| 备份目录 | `Backup/` | 存在的历史备份，确认不要误当活配置 |

---

## 6. ⑥ 工作量与阶段建议

### 6.1 独占 vs 可并行（按**文件**判定，不按功能）

**必须独占的"共用文件"（一次只能一个人动）**：

| 文件 | 为什么共用 |
|---|---|
| `Config/ConfigIndex.tres` | 全项目所有表的唯一持有者；任何数组变动都会影响全局加载 |
| `Scripts/Config/ConfigIndexData.cs` | 上者的 C# 类型 + 12 个索引数组定义 + 全局标量 |
| `Scripts/Combat/Unit.cs` | 玩家+敌人+Boss+召唤物全用它；状态/属性/碰撞层都在这 |
| `Scripts/Combat/UnitAnimator.cs` | 全部单位的表现层入口 |
| `Scripts/Combat/CharacterMovement.cs` | 全部会动的单位 |
| `Scripts/Combat/AttackHitbox.cs` | 全部近战/范围/冲锋 |
| `Scripts/Combat/Projectile.cs` | 全部远程 |
| `Scripts/Combat/StatusEffect.cs` + `Config/EffectConfig.cs` | 全部状态 |
| `Scripts/Abilities/Ability.cs` | **全部**技能的基类 |
| `Scripts/Rewards/RunController.cs` | 开局装配、奖励池、角色/武器选择的总线 |
| `Scripts/Rewards/RewardPool.cs` + `RewardDefinition.cs` + `RewardConfig.cs` | 增幅候选与过滤的公共路径 |
| `Scripts/Rewards/RewardEffectRegistry.cs` | 所有 handler 的注册表 |
| `Scripts/Player/PlayerAbilityInput.cs` + `PlayerWeaponInput.cs` + `PlayerController.cs` | 唯一输入入口 |
| `project.godot` | 输入映射 |

**可并行的（互不重叠的文件/目录）**：

| 工作 | 为什么不冲突 |
|---|---|
| 角色 A 的技能实现 vs 角色 B 的技能实现 | 各自**新增** `Scripts/Abilities/<角色>/*.cs`，文件名不重叠（前提：基类已冻结） |
| 6 把武器各自的武技实现 | 同上；每把武器一个目录 |
| `Config/Tables/` 下**新增**的 `.tres` | 不同分组的目录互不冲突（但**索引数组**是共用文件，见下） |
| `Assets/Animations/<角色>/*.tres` 生成 | 按角色分目录 |
| 新的 Reward handler 类文件 | 各自新增文件；只在 `RewardEffectRegistry.cs` 注册处会撞（**建议注册改成约定扫描或每次只加一行、串行提交**） |
| 增幅条目本身（62 条） | 表行是独立文件，可分给 2–3 人 |

**关键约束**：`ConfigIndex.tres` 是**所有表工作的汇聚点**。并行做表的人必须**串行地**改索引（或用一个脚本/单独一轮统一重建），否则冲突且会静默回退。

### 6.2 建议阶段（每阶段结束都能编译）

| 阶段 | 内容 | 独占性 | 出口验证 |
|---|---|---|---|
| **P0 冻结与备份** | 备份 `ConfigIndex.tres` + 记录基线日志（表加载数、`FileLoaded=true`）；确认 `dotnet build` 通过；记录改动前基线 | 独占 | `build.ps1` 通过 + 日志里 `ConfigIndex.tres 已加载` |
| **P1 表骨架（只加不删）** | 新建 `Characters`(A/B 两行)、`Weapons`(6 行)、`Skeleton` 玩家技能行、新增幅占位行（62 条壳，先用 `Params`）；**保留全部旧行**；一次性重建 `ConfigIndex.tres` | **独占**（改 `ConfigIndex.tres`） | 启动后日志表数与预期一致，`FileLoaded=true`，旧玩法仍可跑 |
| **P2 动画管线（关键路径，地基）** | `[Tool]` 动画生成器 + 动画映射表 + `Assets/Animations/**` 生成 + `AnimationEventDriver`（`AnimationMixer.CallbackModeMethod = Immediate`）+ `UnitAnimator` 扩展（加载技能动画库、每实例 `speed_scale`、修掉改共享资源的 `SetLoop` 隐患） | **独占**（改 `UnitAnimator.cs`、`Unit.cs`、`Config/UnitConfig.cs`） | 探针：播一段技能动画，事件在指定时刻触发；`speed_scale=2` 时总时长减半（照抄 §1.4 实验 2 的验证方式） |
| **P3 技能基类 + 输入** | `Ability` 大改（事件帧驱动 / 蓄力 / 层数 / 冷却缩减 / 动画名与事件表列）；`AbilityConfig` 加 `InputAction` 列；`project.godot` 加 `sprint`/`down_attack`；重写三个输入脚本 | **独占**（P2 完成后才能做；P3 做完才能并行 P4） | 一个"占位技能"能按键触发并按动画事件帧生效 |
| **P4 角色 A 与角色 B 的技能（可并行，2 条线）** | A：后撤/跃升/滞空/能量掌 2 武技/短弩 2 武技/弓 2 武技；B：落/闪/架/灵剑 3 武技/镰刀 2 武技/长枪 3 武技 + 下落攻击 + 剑气 + 武器抛出收回 | **可并行**（各自新增文件） | 每把武器的武技可独立触发；敌人系统**不受影响**（回归旧遭遇战） |
| **P5 增幅系统改造（可并行）** | `RewardConfig` 加 `限定(角色/武器/角色+武器)`、`前置`；`RewardPool` 过滤加前置判定；`RewardEffectRegistry` 新增 handler；新增"暴击/格挡成功/护甲破碎/落地/蓄力完成"事件；新建 62 条增幅表行 | **独占**改 `RewardConfig/RewardDefinition/RewardPool/RewardEffectRegistry`；**可并行**改 62 条表行与各自 handler | 候选列表按限定/前置/互斥三重过滤正确（可用 `RunController.PoolPreview` 逐条核对） |
| **P6 清除旧内容** | 删 5 条旧技能行 + 3 角色行 + 3 武器行 + 19 增幅 + 3 池 + `DashAbility`/`BurstFireAbility`；同步 `ConfigIndex.tres`；处理能力卡（按 P0 的用户裁定）；改 `SelfTest`/`_FeelProbe` | **独占**（改 `ConfigIndex.tres` + 共用文件） | `ConfigIndex.tres 已加载`；自测更新后全绿；敌人/Boss/召唤物仍正常 |
| **P7 验收** | 全流程跑一遍（基地 → 选角色/武器 → 关卡 → 增幅 → Boss）；两个角色 × 各 3 把武器全试；确认无"静默回退" | 独占 | 手测 + 日志核对 |

### 6.3 并行/独占的最终建议

- **P2（动画）是全项目关键路径**，必须在一切技能工作之前完成并冻结 —— 用户"严格要求有动画才执行技能"把这条提前到了第一位。
- **P3（`Ability` 基类 + 输入）必须独占**；它是 P4 并行的前提（P4 的两个角色线只写"新增子类 + 表行"）。
- **表工作必须串行过 `ConfigIndex.tres` 这一个点**；建议专设一轮（或在 P1/P6 各集中一次）统一重建索引，其余时间**只新增行文件、不动索引**。
- `Scripts/Combat/**` 与 `Scripts/Abilities/Ability.cs` 是"改一次影响全部单位"的文件 → **P2/P3 期间不要并行任何战斗改动**。

### 6.4 总工作量判断

**大（Large）。** 具体量级：

| 维度 | 量 |
|---|---|
| 需要**新建**的机制 | 22 项 |
| 需要**改造**的机制 | 23 项 |
| 需要**新建/替换**的内容条目 | 102 条（其中 62 条增幅表行） |
| 需要**生成**的动画 | ~26 段（脚本批量，已实测可行） |
| 需要**新建**的技能 C# 类 | 约 15–20 个（A 侧 8 个武技 + B 侧 9 个武技 + 2 个被动 + 下落攻击/剑气/抛物/收回等） |
| 需要**新建**的增幅 handler | 约 15–20 个（行为型增幅：护卫/能量护盾/巡飞弹/灵魂收割/落日剑/闪电链/冰冻/标记/剑突/速攻/大刀阔斧…） |
| 需要**删除**的旧代码 | 2 个 Ability 子类 + 兜底目录 + 3 个输入脚本重写 + 2 个 UI 重写 + 2 个测试文件修改 |
| 需要**触碰的共用文件** | 18 个（§6.1 表列） |

**性质判断**：这不是"扩展现有系统"，而是**重写"战斗驱动层（动画事件帧）+ 构筑层（限定/前置/增幅）+ 输入层（表驱动按键）+ 角色/武器数据层（去枚举）"**。现有代码里**能直接带走的只有基础设施**（配置框架、`StatSheet`、`GameplayEventBus`、投射物穿透/弹射、`HitPolicy`、单位表继承、敌人/Boss 全套）。
**建议**：按 §6.2 的 8 个阶段推进，P0–P3 是最容易翻车的部分（配置静默回退 + 动画管线 + 基类重构），先做小样验证（一个角色 + 一把武器的"按键 → 动画 → 事件帧 → 命中"最小闭环），跑通后再横向铺开。

---

## 附录 A · 复现我的实测（命令级）

```powershell
# 1. 建临时工程（不要动 项目a）
$tmp="$env:TEMP\godot_anim_probe"; New-Item -ItemType Directory -Path $tmp,"$tmp\models" -Force
Copy-Item "F:\Godot项目\项目a\Assets\Models\player\mesh_player_body.glb" "$tmp\models\probe_body.glb"
# project.godot: config_version=5 / config/features=PackedStringArray("4.7") / config/name="anim_probe"
# 2. 导入
& "F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --headless --path $tmp --import
# 3. 跑脚本（probe.gd = 复制/改名/加事件帧/存盘/读回；probe2.gd = 事件触发 + speed_scale 对照）
& "F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --headless --path $tmp --script "res://probe.gd"
& "F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --headless --path $tmp --script "res://probe2.gd"
```

- 临时工程仍在 `C:\Users\huayu\AppData\Local\Temp\godot_anim_probe`（`probe.gd` / `probe2.gd` / `recv.gd` / `models\probe_body.glb`），可直接重跑。
- `probe2.gd` 的关键点：`AnimationPlayer` 的 `root_node` 默认是 `..`；方法轨道 path 写 `.` → 落在 `root_node` 自己身上；写 `..` → 落在它的父节点。

## 附录 B · 原始扫描产物（非交付物，仅给出处）

- 硬编码候选 555 行清单：`%TEMP%\hardcode_candidates.txt`（由 `Scripts/**` 机械初筛生成，需人工确认）。
- Godot API 反射来源：`F:\Godot项目\Godot_v4.7.2-stable_mono_win64\GodotSharp\Api\Release\GodotSharp.dll`（`GodotSharp, Version=4.7.2.0`）+ 同目录 `GodotSharp.xml`（16MB）。
- 引擎版本实测：`Godot Engine v4.7.2.stable.mono.official.ed1daf0bf`。

## 附录 C · 本次调研**未**做的事（边界声明）

1. **没有改动项目里任何代码 / 配置 / 场景 / 资源**；只新建了本文件 `docs/refactor-survey.md`。
2. **没有把素材 zip 里的任何内容拷进项目**（首领 FBX 与 Voxel Weapons 只在内存里扫描字节）。
3. **Group B 硬编码只做了机械初筛**（见 §2.4 的诚实说明），未逐行确认，因此**未给出 Group B 的确定条数**。
4. **没有运行项目本体的编译或自测**（那会产生 `bin/`、`.godot/mono/` 等写入）；所有 API 结论来自 GodotSharp 反射 + 独立临时工程的实测。
5. **没有裁定** §5.4 的三件事（能力卡去留 / 2 张 GrantSkill 卡如何处置 / `meta_reload`），需要用户确认。
6. **`n` 参数**（用户原话"不知道具体填什么"）在本文中一律标注为"待填最小可跑值"，未擅自编造数值。
