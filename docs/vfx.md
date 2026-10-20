# 表现层（VFX）契约与清单

J38新增[统一命中反馈与局部时钟](combat-feel.md)：`CombatFeedback`只读结算结果并调度视觉／音效／镜头；`ImpactPause`是独立、明确的战斗时序能力，由Ability、UnitAnimator和CharacterMovement共同遵守，不属于纯VFX。敌人预警与全局时间不暂停。

> **原文纠偏J4**：下落风线继续读取真实下冲状态与逆速度方向。`CharacterDisplay`仅在长枪共通落的Crosshair模式且真实IsDiving时，将同源握持模型的枪尖+Y对齐真实方向，点积实测1；临时TopLevel解除骨骼父变换防二次转偏，结束／换装还原局部握持变换。没有复制单位或创建额外碰撞，逻辑不读取模型结果。截图和未完成动画边界见[下落专项](character-design-j4.md)。

> **原文纠偏J2**：`ThrownWeaponDisplay`只读投掷位置／方向／阶段和当前武器模型路径，飞出模型复用手持资源；`CharacterDisplay`只读尚未回手状态隐藏握持物，回手恢复。长枪沿弹道朝向、尖端锚定落点，落地不旋转；镰刀水平旋转。姿态／模型偏移用Export配置；两个显示分支都不含单位或碰撞，逻辑不读取显示结果。

> **后续G专项**：MeleeSwingFx只读真实StrikeCount，RewardBeaconFx只读奖励可用态与价值色；通过PackedScene替换。私有材质必须在Battle进树播放前准备，否则动画轨道可能缓存旧材质。动画结束自毁，禁用/消耗停粒子，无单位/碰撞/分组。接线见followup-g-verification.md。

> **第二轮 C批**：CharacterDisplay切换角色与握持场景，真实UnitAnimator、受击闪色、蓄力身体表现与残影跟随当前可见模型。
> 骨骼轨道只在动画私有副本中重定向，关键帧及事件不变；展示子树不创建单位、碰撞或分组。[接入契约](base-loadout.md)。

> **第二轮 E-2（2026-10-16）状态表现接入**：`UnitPresentation` 在收集模型网格后调用
> `StatusEffectFx.Ensure(unit)`，延后挂载 `Scenes/Combat/StatusEffectFx.tscn`，避免父节点就绪期间新增兄弟失败。
> 它只读 `GetStatusIds()` / `GetStatusStacks()` / `IsDead`，不改 Unit、状态结算、模型材质或动画。
> 效果行的 `VisualStyle`（None / Sigil / Frost / Embers / Orbit）、`VisualIcon:Texture2D`、`VisualColor`、
> `VisualRadius/Height/Size/Speed/Count` 和 `VisualShowStacks` 控制表现；新状态使用既有形态只需填行。
> `Mark` 用金色标记、数字层数及逐层填亮的五枚菱形；冻结用蓝色冰晶与细环，燃烧用升腾火星与亮色火芯。
> 9 条现有状态有表现，索敌用 `MissileBeacon` 按原定义保持隐形。状态过期至多 `PollSeconds=0.08s`
> 后清理，死亡在下一物理帧清理，退树随父节点回收；子树仅含 Node3D/Sprite3D/Label3D/MeshInstance3D，
> 没有物理体、Unit 或分组。图标始终朝向镜头，多状态横排。截图和生命周期验收见 [E 批验收](e-batch-verification.md)。

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
| **状态图标、层数与世界特效（E-2）** | `Scripts/Combat/StatusEffectFx.cs` | `Unit.GetStatusIds()` / `GetStatusStacks()` / `IsDead`；纯表现列来自 `EffectConfig` | `Unit/StatusEffects`，由 `UnitPresentation` 延后挂载 |
| 准星蓄力指示器（P6b；**P7 收窄到能量掌武技2 独有 + 补全时序**） | `Scripts/UI/ChargeIndicator.cs` | `Ability.ChargeProgress` / `IsPerfectWindow` / `ChargeBaseComplete` / `IsCharging`（P2 基类）；**P7：数据源按技能行 Id `AbilityId` 认，不再按输入动作认** | `Scenes/UI/Hud.tscn` → `Root/ChargeIndicator` |
| **蓄力完成反馈**（P7 ③，**所有蓄力技能通用**） | `Scripts/Combat/ChargeBodyFx.cs` | `Ability.IsCharging` / `ChargeBaseComplete`（**上升沿**）/ `IsPerfectWindow`（全是 P2 就有的只读量，**没有新增任何属性**） | `Scenes/Player/Player.tscn` → `Player/ChargeBodyFx` |
| **蓄力时的描边**（P7 ④，**占位版**，与 ③ 同一个组件） | 同上 + `Assets/Effects/charge_rim_placeholder.gdshader` | 同上（只读 `IsCharging`） | 同上（运行期叠加在 `Player/Model` 的每个 `MeshInstance3D.MaterialOverlay` 上，**不改模型资源本身**） |
| **开镜特效**（P6c） | `Scripts/UI/ScopeOverlay.cs` | `HawkEyeAbility.ZoomProgress`（**P6c 新增的只读属性**）/ `IsAiming` / `ZoomApplied` | `Scenes/UI/Hud.tscn` → `Root/ScopeOverlay`（Root 的**第一个**子节点） |
| **后撤残影**（P6c） | `Scripts/Combat/AfterimageTrail.cs` | `BackdashAbility.InvulnerableOwned`（P3 就有的只读属性） | `Scenes/Player/Player.tscn` → `Player/Afterimage` |
| **下落攻击风线**（第二轮 A-5） | `Scripts/Combat/DiveWindFx.cs` | `FallAttackAbility.IsDiving`（本批只新增只读属性）/ `Enabled`、`Unit.Velocity` / `IsDead` | `Player/AbilityFallAttack/DiveWind`；青蓝色细长光带逆速度方向向上掠过，落地后 0.09s 淡出 |
| **剑气拖尾**（P6c） | `Scripts/Combat/ProjectileTrail.cs` | `Projectile.VelocityDirection`（P4 就有的只读属性） | `Scenes/Combat/SwordQi.tscn` → `SwordQi/Trail`（**挂在剑气本体上**） |
| **落圈特效**（P6c） | `Scripts/Combat/LandingShockwave.cs` | `FallAttackAbility.Landed`（公开事件）+ `LandingBurstAbility.TriggerCount`（公开只读计数）+ 命中体 `Attack/Shape` 的 `SphereShape3D.Radius`；**P6d 新增** `PlayOnce(worldPosition, radius)` 外部入口（「落日剑」的落点用它，半径由调用方给） | `Player/AbilityFallAttack/Shockwave` 与 `Player/AbilityPassive2/ShockwaveBurst`；**P6d**：`SunsetSwordMarker`（异化「落日剑」的剑）的子节点 |
| **落日剑的头顶那把剑**（P6d，**不是纯表现** —— 它自己会落下造成伤害） | `Scripts/Rewards/SunsetSwordMarker.cs` | 无（它就是"落日剑"这件事本身）；纯表现的部分是它内嵌的 `LandingShockwave` | 目标敌人子树 → `SunsetSword`（异化「落日剑」施加时按需建） |
| **灵魂收割掉的"灵魂"**（P6d，**不是纯表现** —— 拾取会回血） | `Scripts/Combat/SoulPickup.cs` | 无（掉落物自己就是"事实"）；拾取是**每帧距离轮询** `player_units`，**没有 `Area3D`** | `CurrentScene` → `Soul`（异化「灵魂收割」击杀时掉） |
| 滞空光环（P3，写在机制描述里的那条） | `Scripts/Combat/AirHoverController.cs` 的光环部分 | 滞空状态本身 | 玩家脚下 |
| 破甲特效（更早） | `Scripts/UI/ArmorBreakFx.cs`（静态工具） | `Unit.ArmorBroke` 事件 | 一次性，挂 `CurrentScene` |
| **技能指示器**（曲线路径／范围圈／曲线＋范围／直线区域） | `Scripts/Combat/SkillIndicator.cs`，预制体`Scenes/Combat/SkillIndicator.tscn` | 纯几何入参；EnemyAttackWarning从能力／弹体传入实际范围、世界缩放和预计落点，指示器本身不改变战斗 | J37敌人已接入：自爆、陨石、冲锋、重击、跳扑／俯冲、回旋、炸弹；每次攻击独立实例。Main历史共享实例仍默认隐藏。见[契约](skill-indicator.md)与[J37](character-design-j37.md) |

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
   - ⚠️ **"某个按键上的技能"不是一个身份**：同一个 `weapon_skill_2` 上挂着 6 把武器各自的技能。
     要给**某一个技能**做表现就按**技能行 Id** 找（P7 修的就是这个 bug：方形圈曾经在
     弓武技2 / 短弩武技2 / 长枪武技2 蓄力时也冒出来，用户裁定「方形蓄力圈就是能量掌武技2独有的」）。
     **填了 `AbilityId` 就只认它、找不到就不画**（不要"找不到就回退到按按键找"——那等于把 bug 放回来）。
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
     ⚠️ **P7 补的同一条坑的另一半**：`evaluate_runtime_expression` 里调**带默认值**的 C# 方法，
     **必须把参数填满**（默认值在 MCP 这条路上不生效）——
     实测 `node.DebugPreviewChargeFx(3.0, 1.0)` 报 `Expression execution failed`，
     写成 `node.DebugPreviewChargeFx(3.0, 1.0, false)` 就成功（那个方法签名是
     `(float rim, float flash, bool perfect = false)`）。**结论：探针方法别依赖默认值**，
     要不就把参数写全，要不就别给那个参数默认值。
8. **实拍**：`runtime screenshot --format png --save-path 'user://x.png'`（干净的游戏视口截图，
   1920×1080），拷进 `docs/screenshots/`。桌面截图（`mcp__windows__Screenshot`）在游戏窗口
   不可见时拍不到东西 —— P6b 当时拍得到是因为窗口在前台。
   想要"看清一个 0.2s 的瞬间"就**运行期调慢那几个 `[Export]`**（例如把拖尾存活 0.18s → 0.6s、
   把圈扩散 0.38s → 2.5s），**别改默认值、更别改代码**；截图里要说明哪几个数是拍摄用的。
9. **文档**：本文件 §1 的表加一行 + `docs/abilities.md` 的"接入记录"节写一段。
10. **"每次 X 发生时就给一次反馈"怎么接**（P7 ③ 立的第二条标准）：先去战斗层找
    **有没有一个公开只读量能当"事实的上升沿"**，表现层自己在 `_Process` 里存一份"上一次的值"来判沿
    （`ChargeBodyFx` 接"每次完成基础蓄力"就是读 `Ability.ChargeBaseComplete` 的 false→true，
    **没有**给战斗层加事件、也没有改状态机）。⚠️ 只有**同一帧**发生的两件事（例如"基础蓄力完成"与
    "进入完美窗口"）**不要叠两次反馈** —— 合成一次、用颜色 / 强度 / 尺寸**换档**区分。
11. **音效不许写路径**：`AudioManager.PlaySfx("sfx_xxx")` 的 id 进
    `Config/Tables/Audio/audio_*.tres`（**新加一行要三处同改 `ConfigIndex.tres`**：`load_steps` +
    `[ext_resource]` + 数组项，见 `docs/audio.md`）。P7 ③ 就是这么加的 `sfx_charge_complete`。
12. **要"往身上叠一层东西"就借 `MaterialOverlay`，别动 `MaterialOverride`**：
    `MaterialOverride` 是本项目受击闪色（`UnitPresentation`）/ 精英染色（`EliteModifier`）在用的通道，
    覆盖它会打架；`MaterialOverlay` 是**额外一遍**（不改原材质），用完把原值还回去就行。
    想"蓄力时多一圈描边"就挂一个 `ShaderMaterial` 上去（P7 ④ 的 `charge_rim_placeholder.gdshader`）。

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
| **把"变色多久"挂在战斗层的完美窗口上** | 用户要的是「缩 → 闭合 → 变色 → **0.3s 后**变回原来的颜色但保持闭合」，而战斗层的 `PerfectWindowSeconds` 是 **0.2s**（表值、决定"松手算不算完美一击"、有平衡含义）—— 挂上去就会"表一动、表现跟着变" | 表现层的时长做**独立 `[Export]`**（`ChargeIndicator.PerfectColorSeconds = 0.3`）；战斗层那个只**读出来写进探针**，用来证明两者不是一个量 |
| **表现层按"输入动作"找技能** | 一个 `weapon_skill_2` 上挂着 6 把武器的技能 → 只该给能量掌武技2 画的方形圈，弓/短弩/长枪蓄力时也画了 | 按**技能行 Id** 认（`[Export] string AbilityId`），填了就只认它、找不到就不画（P7 ①） |
| **用 `MaterialOverride` 做"身上多加一层"** | 会顶掉受击闪色 / 精英染色（同一个通道） | 用 **`MaterialOverlay`**（额外一遍，退出时把原值还回去）—— P7 ④ |
| **靠"一次 sleep 再截图"去抓一个 0.3s 的瞬间** | MCP 单次调用本身有 **~0.1–0.4s** 延迟（实测 `call_runtime_node_method` 往返就让采样点漂了 0.2s+），截图必然错过窗口 | 要么**运行期把那几个 `[Export]` 调长**（只为拍摄，默认值不动），要么**轮询一串采样**并同时读一个"真实时钟"（P7 读的是 `Ability.ChargeTime`）当锚点 —— 拿到的是一条**时间线**，比一张赌运气的图可信得多 |

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

## 6.2 实拍（2026-10-16 · P7：准星收敛 / 准星时序 / 蓄力完成反馈 / 描边占位）

**默认值一个都没改**（这一批的 5 张图全是默认参数下拍的；有一次曾想把 `FlashSeconds` 调长，
但那两次 `evaluate_runtime_expression` 写属性失败了 —— 单表达式不支持赋值 —— 所以图里是真实默认值）。

| 图 | 是什么 |
|---|---|
| `screenshots/p7_charge_tinted.png` / `_zoom` | 能量掌武技2 蓄力到 0.5s：方形圈**闭合**（缺口 0px）+ **金色变色 + 光晕 + 准星本体也染色** |
| `screenshots/p7_charge_closed_reverted.png` / `_zoom` | **0.5s + 0.3s 之后**（还按着右键）：颜色回到蓄力色（青）、光晕收起，**形状依旧是闭合的**，缺口还是 0px |
| `screenshots/p7_charge_burst.png` | 「基础蓄力完成」那一瞬间：角色被**金色闪光**罩住（满身加亮）+ 一圈**金色**扩大圈（完美档 ×1.8 半径）+ 音效 `sfx_charge_complete` |
| `screenshots/p7_charge_rim_off.png` / `_zoom` | 对照组：**松手之后**同一个角色 —— 边缘光消失；拿它与上一条的 `_zoom`（`p7_charge_rim_on_zoom.png` = 蓄力中那个角色的裁切）并排看，就是"描边随蓄力开关"的 A/B |
| `screenshots/p7_charge_bow_nocircle.png` / `_zoom` | **反例**：弓 武技2「刹那之眼」蓄力中（`skl_a3_w1_2 蓄力中=true 基础蓄满=true`），**屏幕中心只有一个小点，没有方形圈** —— ① 的判据 |

**③ 的时间线取证**（同一次按住右键，`Ability.ChargeTime` 当锚点；MCP 一次往返 ~0.1–0.2s，
所以是"一串采样"而不是"一帧截图"）：

```
ChargeTime=0.24 | 进度=0.62 闭合=False 变色中=False（还剩 0s）
ChargeTime=0.46 | 进度=1    闭合=True  变色中=True （还剩 0.1s）   ← 闭合 + 变色
ChargeTime=0.82 | 进度=1    闭合=True  变色中=False（还剩 0s）     ← 0.3s 后：原色、仍然闭合
ChargeTime=1.01 | 进度=1    闭合=True  变色中=False（还剩 0s）     ← 一直按着就一直闭合
ChargeTime=1.19 … 2.09 同上
```

**② 的两个量确实不同**（同一行的探针里同时读出来）：
`变色中=True（还剩 0.1s）` 时 `完美窗口=False`（战斗层那 0.2s 已经过了）——
`PerfectColorSeconds = 0.3` 与表里的 `PerfectWindowSeconds = 0.2` 是两个独立的量。

**③ 的通用的证据**（同一次运行里换武器，**代码一行没改**）：

```
[Combat] 蓄力完成反馈：skl_a1_w1_2 基础蓄力完成（同时在完美时机窗口里 → 金色 / ×1.8 档）
         → 角色闪一下 0.22s（强度 1.53）+ 一圈扩大 2.7m（0.45s）+ 音效「sfx_charge_complete」（第 1 次 …）
[Combat] 蓄力完成反馈：skl_a3_w1_2 基础蓄力完成 → 角色闪一下 0.22s（强度 0.85）
         + 一圈扩大 1.5m（0.45s）+ 音效「sfx_charge_complete」（第 3 次 …）      ← 弓 武技2 也吃到反馈（普通档）
[Audio]  首次播放「sfx_charge_complete」→ 总线 SFX / res://Assets/Audio/ui/ui_card_unlock.wav
[Combat] 落圈特效：蓄力完成 落地画一圈 —— 半径 2.7m（调用方给定（蓄力完成）），扩散 0.45s
[Combat] 蓄力身体表现：模型「Model」上挂了 2 份叠加材质（文件 res://Assets/Effects/charge_rim_placeholder.gdshader）
```

**安全性读数**（`ChargeBodyFx.DebugState()` 末尾的固定三项）：
`纯表现 不改 MaterialOverride / 无碰撞体 / 不进任何分组`；那一圈由 `LandingShockwave` 生成在
`CurrentScene` 下的临时节点（**不是**单位子树），`LandingShockwave.DebugState()` 自带
`纯表现 碰撞体=0 分组=0` 的那一行。

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

### 7.1 P7 这一批的边界（照实说）

- **④ 的描边是占位**：`Assets/Effects/charge_rim_placeholder.gdshader` 是一个"菲涅尔边缘光"，
  形态不追求好看（用户明说"这个后期在做，现在可以加一个占位的"）。正式版替换那个 `.gdshader`
  文件即可 —— **C# 一个字都不用改**（路径是 `ChargeBodyFx.RimShaderPath`）。
  读不到文件时会退回代码里的**内联兜底着色器**并写一条警告（`DebugState` 里那行
  `叠加材质 2 份（文件 … / 代码内联兜底）` 就是用来区分走了哪条路的）。
- **③ 的"一圈"不是"角色模型的透明残影副本"**：用户给的是"一圈扩大的透明残影**或**描边"，
  本轮选的是**复用 P6c 的落圈**（`LandingShockwave.PlayOnce`，身体中段高度、无碰撞体）。
  真要做"整个人的透明副本往前扩散"，就照 `AfterimageTrail` 的**做减法**那套（§3）来，
  别复制出碰撞体/单位。
- **③ 的音效素材是临时借的**：`Config/Tables/Audio/audio_sfx_charge_complete.tres` 的 `Stream`
  现在指向项目自制的 `ui_card_unlock.wav`（"能力卡·解锁"那一声，语义最接近"就绪"）。
  正式素材出来之后把 `Stream` 指过去即可（**代码不用改**，调用方只认
  `AudioIds.ChargeComplete = "sfx_charge_complete"`）。总线刻意走 `SFX` 而不是 `UI`。
- **"基础蓄力完成"与"进入完美窗口"是同一帧**（窗口就从那一刻起算），所以 ③ 只发**一次**反馈、
  用颜色/强度/半径换档（金色 / ×1.8 / ×1.35）。如果哪天要给"完美窗口里松手打出的那一击"
  单独的反馈，那是**另一个触发点**（`Ability.LastReleasePerfect`），本轮没做。
- **准星圈只认 `skl_a1_w1_2`**（用户裁定）。要让另一个技能也用这个圈，就把
  `Hud.tscn` 的 `ChargeIndicator.AbilityId` 填成那一行 —— 但那会让"两个技能同时蓄力"时只有一个有圈
  （同一个组件只跟一条技能行），要两个都有圈得挂两个 `ChargeIndicator` 节点。
