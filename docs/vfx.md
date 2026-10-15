# 表现层（VFX）契约与清单

> 给接手的人看：**加一个新的特效要做什么**、**表现层凭什么"不改战斗逻辑"**、以及本项目在
> 特效这一层踩过的坑。代码在 `Scripts/UI/`（屏幕/2D）与 `Scripts/Combat/`（世界里的一次性特效），
> 全部按 **P6b 的 `Scripts/UI/ChargeIndicator.cs`** 立的标准写。

---

## 0. 一句话

**战斗层产生"事实"，表现层只读事实、只画东西。**
表现层**永远不写**战斗状态（不改伤害、不改冷却、不改判定、不改位置），唯一允许的例外是
**为了拿到状态而在战斗层新增一个只读属性**（P6c 加过 1 个：`HawkEyeAbility.ZoomProgress`）。

---

## 1. ⭐ 唯一契约：表现层读的是"战斗层的公开只读量"

| 特效 | 脚本 | ⭐ 只读来源（谁提供的） | 挂在哪 |
|---|---|---|---|
| 准星蓄力指示器（P6b） | `Scripts/UI/ChargeIndicator.cs` | `Ability.ChargeProgress` / `IsPerfectWindow` / `ChargeBaseComplete` / `IsCharging`（P2 基类） | `Scenes/UI/Hud.tscn` → `Root/ChargeIndicator` |
| **开镜特效**（P6c） | `Scripts/UI/ScopeOverlay.cs` | `HawkEyeAbility.ZoomProgress`（**P6c 新增的只读属性**）/ `IsAiming` / `ZoomApplied` | `Scenes/UI/Hud.tscn` → `Root/ScopeOverlay`（Root 的**第一个**子节点） |
| **后撤残影**（P6c） | `Scripts/Combat/AfterimageTrail.cs` | `BackdashAbility.InvulnerableOwned`（P3 就有的只读属性） | `Scenes/Player/Player.tscn` → `Player/Afterimage` |
| **剑气拖尾**（P6c） | `Scripts/Combat/ProjectileTrail.cs` | `Projectile.VelocityDirection`（P4 就有的只读属性） | `Scenes/Combat/SwordQi.tscn` → `SwordQi/Trail`（**挂在剑气本体上**） |
| **落圈特效**（P6c） | `Scripts/Combat/LandingShockwave.cs` | `FallAttackAbility.Landed`（公开事件）+ `LandingBurstAbility.TriggerCount`（公开只读计数）+ 命中体 `Attack/Shape` 的 `SphereShape3D.Radius`；**P6d 新增** `PlayOnce(worldPosition, radius)` 外部入口（「落日剑」的落点用它，半径由调用方给） | `Player/AbilityFallAttack/Shockwave` 与 `Player/AbilityPassive2/ShockwaveBurst`；**P6d**：`SunsetSwordMarker`（异化「落日剑」的剑）的子节点 |
| **落日剑的头顶那把剑**（P6d，**不是纯表现** —— 它自己会落下造成伤害） | `Scripts/Rewards/SunsetSwordMarker.cs` | 无（它就是"落日剑"这件事本身）；纯表现的部分是它内嵌的 `LandingShockwave` | 目标敌人子树 → `SunsetSword`（异化「落日剑」施加时按需建） |
| **灵魂收割掉的"灵魂"**（P6d，**不是纯表现** —— 拾取会回血） | `Scripts/Combat/SoulPickup.cs` | 无（掉落物自己就是"事实"）；拾取是**每帧距离轮询** `player_units`，**没有 `Area3D`** | `CurrentScene` → `Soul`（异化「灵魂收割」击杀时掉） |
| 滞空光环（P3，写在机制描述里的那条） | `Scripts/Combat/AirHoverController.cs` 的光环部分 | 滞空状态本身 | 玩家脚下 |
| 破甲特效（更早） | `Scripts/UI/ArmorBreakFx.cs`（静态工具） | `Unit.ArmorBroke` 事件 | 一次性，挂 `CurrentScene` |

**新增表现层时照这张表办**：先问"我要读的那个事实，战斗层有没有公开只读的入口？"
- **有** → 直接用，一行战斗代码都不要碰（`AfterimageTrail` / `ProjectileTrail` 就是这么做的）。
- **没有，但只差一次"把已知的中间量暴露出来"** → **加只读属性**，别改行为
  （`HawkEyeAbility.ZoomProgress` = `(基准FOV − 当前FOV) / 跨度` 的一次除法；FOV 的写入、
  平滑、还原、收招时机**一个字都没改**）。
- **没有，而且需要新状态** → 停下来问用户。**不要**为了做特效去改战斗逻辑。

### 为什么"只读"不是洁癖

`architecture.md` 第 7 条那一类事故（HUD 的 `Control` 吃掉鼠标 → 镜头完全不能转）就是"表现层越界"
的后果。表现层一改战斗状态，"手感"与"数值"就会跟着表现的调整漂移 —— 例如开镜的暗角改深一点，
不该让 `FOV` 的还原速度发生变化。

---

## 2. 加一个特效要做什么（照抄这个清单）

1. **先读设计文档那一句原文**（`F:\Godot项目\3d肉鸽雨中哈迪斯\角色设计.txt`），
   表现层描述散在各技能条目里（例如第 87 行"放大视角"、第 112 行"多一圈攻击判定 + 特效"）。
2. **定"事实"来源**：走 §1 的三步判断；能用现成只读量就别加东西。
3. **选落点**（按"这个特效属于谁"）：
   | 特效属于 | 挂哪 | 例子 |
   |---|---|---|
   | 屏幕 / HUD | `Hud.tscn` 的 `Root` 下（`CanvasLayer` 里） | 蓄力指示器、开镜 |
   | 某个单位身上 | 那个单位的场景（`Player.tscn` / `Enemies/*.tscn`） | 后撤残影 |
   | 某个技能 | 那个技能节点下（子节点） | 落圈（`AbilityFallAttack/Shockwave`） |
   | 某个投射物 | **那个投射物自己的场景**（不要另做一个特效实体） | 剑气拖尾（`SwordQi.tscn`） |
   | 一次性、世界里 | 不需要挂：`AddChild` 到 `GetTree().CurrentScene`，自己 `QueueFree` | `ArmorBreakFx`、落圈、残影 |
4. **写 `[Export]`**：**几何量 / 颜色 / 时长一个都不许硬编码**（用户偏好：能在编辑器里调）。
   命名分组用 `[ExportGroup("...")]`（中文标题，和项目里其它组件一致）。
5. **定"数据源节点"别写节点路径**：用**表里的行 Id**（`Ability.FindByAbilityId(unit, id)`）
   或**类型查找**，`[Export] string AbilityId` 兜住；节点名换过时不会静默失效（找不到会打日志）。
6. **2D 的 `Control` 必须 `mouse_filter = Ignore`**（`architecture.md` 第 7 条）：场景里写一次、
   `_Ready` 里再兜一次。
7. **加探针与拍摄台**（P6b 立的规矩，四张实拍图就是靠它拍的）：
   - `DebugState()`：一行字符串，把"现在画成什么样 + 读到的只读源是什么 + 安全检查值"全打出来。
   - `DebugPreviewXxx(...)` / `DebugClearPreview()`：**不依赖真实按键/相机**地强制摆成某个样子，
     拍摄台与取证用。**判据与画法一个字都不变，只是"谁来触发"**。
   - 自测/探针读数用 MCP：`call_runtime_node_method`（`node_path` + `method_name`）或
     `evaluate_runtime_expression`（**单表达式**）。
     ⚠️ **C# 方法带可选参数时，`call_runtime_node_method` 的 0 参数调用会报
     `Method not found`** —— 用 `evaluate_runtime_expression` 写 `node.TryActivate(null)` 绕过。
8. **实拍**：`runtime screenshot --format png --save-path 'user://x.png'`（干净的游戏视口截图，
   1920×1080），拷进 `docs/screenshots/`。桌面截图（`mcp__windows__Screenshot`）在游戏窗口
   不可见时拍不到东西 —— P6b 当时拍得到是因为窗口在前台。
   想要"看清一个 0.2s 的瞬间"就**运行期调慢那几个 `[Export]`**（例如把拖尾存活 0.18s → 0.6s、
   把圈扩散 0.38s → 2.5s），**别改默认值、更别改代码**；截图里要说明哪几个数是拍摄用的。
9. **文档**：本文件 §1 的表加一行 + `docs/abilities.md` 的"接入记录"节写一段。

---

## 3. ⚠️ 残影 / 一次性特效**绝不能进单位列表**（最容易出错的地方）

用户明确点过这条：**残影不能有碰撞、不能进 `enemy_units`、不能吃伤害**。
项目里"清场"就是遍历 `enemy_units` 全杀掉，混进去会产生莫名其妙的死亡日志。

**做减法，不做减法就会漏**（`AfterimageTrail.Sanitize` 是范本）：

```csharp
// 复制出模型子树之后，把"能参与物理/战斗/逻辑"的东西从树上摘掉再 QueueFree
// ① CollisionObject3D（覆盖 Area3D / CharacterBody3D / StaticBody3D / RigidBody3D）
// ② Unit（残影绝不是一个"单位"）
// ③ AnimationTree（状态机会自己驱动骨骼）
// ④ AnimationPlayer（它会从 autoplay 的第 0 帧重播，残影姿势就和角色对不上）
node.GetParent()?.RemoveChild(node);   // ⚠️ 必须 RemoveChild：立刻离开物理世界
node.QueueFree();                      //    只 QueueFree 的话本帧还在
// ⑤ 再递归退出**所有分组**（RemoveFromGroup）
```

**并且给三个可数的取证量**（`DebugState()` 会打出来，实拍时直接读）：

| 量 | 必须 |
|---|---|
| 残影子树里 `CollisionObject3D` 的数量 | `0` |
| 残影子树里 `Unit` 的数量 | `0` |
| 残影子树残留的分组名 | 空 |

**2026-10-16 实测取证**（后撤残影 4 个在场时）：

```
后撤残影：无敌帧=False 场上残影=4 累计=4（间隔 0.09s 存活 3s 起始 alpha 0.62 上限 12）
｜只读源 技能行=skl_a1_skill_1 无敌帧拥有=False
｜安全性 最近残影 碰撞体=0（必须 0） 单位=0（必须 0） 分组=（无）（必须无） 网格=2 抄骨骼=7

留影前：enemy_units=0  player_units=1  projectiles=0
留影后：enemy_units=0  player_units=1  projectiles=0      ← 三个分组计数一位没变
find_children("Afterimage_*") = 4
Afterimage_0.find_children("*","CollisionObject3D") = 0
Afterimage_0.find_children("*","Unit")              = 0
Afterimage_0.get_groups().size()                    = 0
Afterimage_0.is_in_group("enemy_units")             = false
```

**姿势怎么"定格"**：复制出来的骨架是 bind pose，所以要把源骨架的 `GetBonePose(i)` 逐个
`SetBonePose(i, …)` 抄过去，然后**把 `AnimationPlayer` 摘掉**（否则它从 `idle` 第 0 帧重播）。

---

## 4. 已知坑（都是实拍截图里看出来的，别再踩）

| 坑 | 现象 | 正确做法 |
|---|---|---|
| `TorusMesh.Rings` / `RingSegments` 写反 | 圆画成**菱形**（`Rings` 是**主圈切几段**，`RingSegments` 是**管子横截面**） | 环：`Rings = 64`、`RingSegments = 6` |
| 用"一圈套一圈的 `DrawArc`"画暗角 | 每圈的抗锯齿边缘叠在前一圈实心上 → 屏幕上一圈圈**可见的同心细线** | 一张 `GradientTexture2D`（`Fill = Radial`）+ 一次 `DrawTextureRect` |
| 白地板 + 加法混合（`Add`） | 特效冲成一片白，看不出形状 | 默认用 `Mix`（普通半透明）；深色场景再开 `Add` |
| 拖尾做"竖直的带" | 剑气永远朝**准星**飞 = 沿视线飞 → 那条带正好**侧对镜头**，屏幕上只剩 1px 细线 | **面向镜头**：宽度方向 = `cross(段方向, 到镜头)` |
| 复制模型当残影 | 碰撞体 / 分组 / 脚本一起被复制 | §3 的做减法 + 三个取证量 |
| 在 `.tscn` 里加节点 | `parent` 引用了后面才定义的节点 → **节点被静默丢弃**（不报错、只是不显示）；同名节点冲突 | `parent` 必须引用**已定义过**的节点；改实例已有子节点用 `[node name="X" parent="…" index="0"]`（去掉 `type=`） |
| 纯显示 `Control` | 默认 `mouse_filter = Stop` → 吃掉鼠标 → 镜头不能转 | `Ignore`（场景 + `_Ready` 双保险） |
| "一次采样"的错觉 | 只看一帧截图就以为特效没问题 | 至少拍两张（起始 / 扩散中），并把 `DebugState()` 一起记下来 |
| **在 `_Ready` 里改自己的 `GlobalPosition`** | 生成方的写法是 `AddChild` **之后**才写 `GlobalPosition` —— `_Ready` 里那一句**当场被覆盖**（P6d 实测：灵魂的"地面拾取圈"被压到地板下面 0.1m，**看不见**；代码、日志、自测全绿，只有截图能看出来） | 要么把偏移做进**子节点**的局部坐标（本批的改法：圈的 `Position.y = RingGroundOffset`），要么让生成方在 `AddChild` **之前**设位置 |
| 用 `TorusMesh` 画"地面上的圈" | 默认躺在 XZ 平面，但 `Rings` / `RingSegments` 写反会画成多边形（见上面那条） | 环：`Rings = 64`、`RingSegments = 6` |

---

## 5. 验证方式（本批 4 项是怎么验的）

```powershell
# 编译（0 warning / 0 error）
& 'F:\Godot项目\项目a\build.ps1' --no-incremental -v minimal

# 起游戏（MCP 会话）
& $cli tool-call run_project --args-json '{"allow_window":true,"scene_path":"res://Scenes/Main.tscn"}' --apply --allow-open-world

# 换角色/武器（表行 Slot − 1；Hunter=3 Bow=5 WeaponMaster=4 SpiritSword=6）
& $cli tool-call call_runtime_node_method --args-json '{"node_path":"/root/Main/RunController","method_name":"BeginRunWithCharacter","arguments":[3,5]}' --apply --allow-open-world

# 读取只读状态 / 驱动一次技能
& $cli tool-call evaluate_runtime_expression --args-json '{"expression":"get_node(\"/root/Main/Player/AbilityWeapon3Skill2\").TryActivate(null)"}' --apply --allow-open-world
& $cli tool-call call_runtime_node_method --args-json '{"node_path":"/root/Main/Hud/Root/ScopeOverlay","method_name":"DebugState"}' --apply --allow-open-world

# 截图
& $cli runtime screenshot --format png --save-path 'user://p6c_scope.png'
```

⚠️ **`run_project` 传不了 user args**。这条路（`--char=` / `--weapon=`）只在
**命令行直跑 + `--remote-debug`** 时可用，而那条路要求**编辑器已经绑好 6007**
（编辑器第一次 play 之后才会绑上，见 `docs/anim-pipeline.md` §6.4.1）。
本批用的替代办法：**跑起来之后用 `RunController.BeginRunWithCharacter(角色, 武器)` 换装**
（`RunController` 上那个方法是 public 的），比折腾命令行参数稳。

自测：`user://selftest.flag`（**跑完必须删**）→ `=== 自测结束 ===` + 退出码 `0`。

### 5.1 P6d 补的两条 MCP 手法（**下一个人一定会用到**）

| 想做什么 | 怎么做 | 为什么 |
|---|---|---|
| **真的按一次键**（例如"按住 Shift 奔跑"） | `tool-call simulate_runtime_input_action --args-json '{"action_name":"sprint","pressed":true}'` | `run_project` **传不了 user args**（见 §5），而 `BeginRunWithCharacter` 只能换装、按不了键。这个工具走 `Input.parse_input_event(InputEventAction)`，`Input.IsActionPressed` 当场为真 —— **P6d 的「奔跑 → 护卫」就是靠它做实机取证的**。鼠标同理：`simulate_runtime_input_event` + `{"type":"mouse_button","button_index":1,"pressed":true}` |
| **规则节点怎么寻址** | 增幅的 `TriggerRule` 是运行期 `new` 出来挂在 `Player` 下的，**自动名是 `@Node@N`**（不是类名！）。先用 `evaluate_runtime_expression` 数一遍：`get_node("Player").get_child(i).name`，再 `call_runtime_node_method {"node_path":"/root/Main/Player/@Node@N","method_name":"DebugState"}` | 名字里带 `@`，写死路径会失效；`GetNodeOrNull<GuardOnSprintRule>` 这种按类型的查找在 MCP 侧做不到（`call_runtime_node_method` 只吃路径） |
| **拍摄台（把一瞬间钉住）** | `SunsetSwordRule.DebugPreviewOnNearest()`（给最近的敌人挂剑并**钉在悬挂状态不落**）· `SoulHarvestRule.DebugDropInFront()`（在玩家正前方掉一个灵魂） | 与 P6c 的 `DebugPreviewXxx` 同一条纪律：判据与画法一个字都不变，只是"谁来触发"。**正式流程里没有调用点** |
| **重启游戏** | `stop_project` 要带 `--args-json '{"allow_window":true}'`（否则被 Vibe Coding 闸门拒掉：`This tool would open or control a runtime window`）；⚠️ 它**全局生效**，用之前先确认没有别人在跑自测 | 换了 C# 之后**必须重启**才生效（`.godot/mono/temp/bin` 的 dll 是启动时加载的） |
| **`runtime info` 报的是哪个实例** | 看 `current_scene` + `process_frames`；实例回到基地（`/root/Base`）说明这一局已经结束 | 编辑器 `run_project` 起的实例与命令行 `--remote-debug` 的实例都会连 6007 |

---

## 6. 实拍（2026-10-16 · P6c）

| 图 | 是什么 | 拍摄时的运行期调整（**只是拍摄用，默认值没动**） |
|---|---|---|
| `screenshots/p6c_scope.png` | 弓 武技2「刹那之眼」开镜：镜筒环 + 12 条刻度 + 径向渐变暗角（中心完全净空） | 无（`ZoomProgress` 真的到了 1：`IsAiming=True 视角已放大=True 缩放进度=1`） |
| `screenshots/p6c_afterimage.png` / `_zoom` | 技能1「后撤」无敌帧期间的 4 个青蓝残影 | `GhostLifetime` 0.34 → 3.0（只为把这一瞬间留在画面上） |
| `screenshots/p6c_swordqi.png` / `_zoom` | 灵剑 武技2「拔刀式」的剑气拖尾（面向镜头的一条渐细光带） | `Speed` 60 → 25、`MaxLifetime` → 6、拖尾 `Lifetime` 0.18 → 0.6 |
| `screenshots/p6c_landing.png` / `_zoom` | 落地那一圈（`TorusMesh` 环 + 薄盘）：下落攻击（青）与「落剑式」（金）各一圈，半径 3.2m = 命中体真实半径 | 两圈的 `Duration` 0.38 → 2.5 |

## 6.1 实拍（2026-10-16 · P6d）

| 图 | 是什么 | 拍摄时的手段（**默认值一个都没改**） |
|---|---|---|
| `screenshots/p6d_sword_soul.png` / `_zoom` | 「落日剑」悬在 `FastMelee` 头顶（金色剑刃 + 棕色护手，剑尖朝下）+ 「灵魂收割」的灵魂（青色球 + 地面**圆形**拾取圈） | `SunsetSwordRule.DebugPreviewOnNearest()`（钉住不落才会留在画面上）+ `SoulHarvestRule.DebugDropInFront()`（掉在正前方 3.2m） |
| `screenshots/p6d_soul_ring_before_fix.png` / `_zoom` | **改前**：只剩一颗球，**地面上那圈看不见**（被 `_Ready` 里那句 `GlobalPosition += Up*0.15` 压到地板下面 0.1m，随后被生成方的赋值覆盖） | 同上 |
| `screenshots/p6d_soul_ring_after_fix_zoom.png` | **改后**：圈回到地面上，形状是**正圆**（`Rings = 64` / `RingSegments = 6` 没写反） | 同上 |

实机日志（同一次运行，`BeginRunWithCharacter(4,6)` = 武器大师 + 灵剑 + 三条增幅都 `GrantReward`）：

```
[Config] 奔跑：角色行 chr_hunter → 按键「sprint」（project.godot 里有这个动作）/ 移速倍率 1.5
[Reward] 落日剑：施加于 FastMelee（头顶悬挂，1.5s 后落下；落地半径 2.5m 伤害 40 …）
[Reward] 灵魂收割：掉落灵魂（拾取半径 1.2m / 回复 8 点 / 存活 15s …）
[Reward] 灵魂收割：灵魂被 Player 拾取（回复 8 / 配置 8 点，生命 105.6/240）   ← 真的走过去碰到的
```

⚠️ 「奔跑」那一条在实机里是这样验的：`simulate_runtime_input_action` 按住 `sprint` + `move_forward`
→ `SprintController.DebugState()` = `奔跑中=True 按键「sprint」按住=True 倍率=1.5 累计=1`
→ `GuardOnSprintRule.DebugState()` = `手上有格挡=True 累计获得=1`，并且 `Player` 下**多出一个 `Block` 子节点**
（不选这条增幅就没有它）。

---

## 7. 没做 / 边界

- 「剑气拖尾」的**材质是代码里拼的**（`StandardMaterial3D` + 逐顶点色），没有做 `.tres` 美术资源；
  要换外观请在 `ProjectileTrail` 的 `[Export]`（`HeadColor` / `TailColor` / `WidthHead` / `WidthTail` /
  `WidthCurve` / `EmissionEnergy` / `AdditiveBlend`）上调。
- 残影目前**只给玩家**（挂在 `Player.tscn`）。要给敌人加：把同一个组件挂到敌人场景、
  `AbilityId` 填那个敌人的技能行即可（组件本身不认识"玩家"）。
- 「开镜」的暗角是**屏幕空间**的；如果以后要做"镜筒内视野变窄/边缘虚化"那种真·镜头效果，
  得走 `Camera3D` 的 `environment` 或后处理 —— 那是另一条路（会动到相机，要注意别和
  `HawkEyeAbility` 改的 `Fov` 打架）。
- `ArmorBreakFx`（破甲特效）里的 `TorusMesh` 也把 `Rings` / `RingSegments` 写反了
  （`Rings = 8, RingSegments = 24`），所以那个环看起来是**八边形**。它对 0.42s 的一次性特效
  影响不大，**本轮没动**（不在任务边界内）—— 要修就是两个数字对调。
