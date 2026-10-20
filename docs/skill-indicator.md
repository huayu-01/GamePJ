# 技能指示器（SkillIndicator）接入契约

> **一句话**：**玩家与敌人共用同一个组件** —— 敌人技能示警（红圈 / 红矩形）与玩家长按技能的命中区预览
> （青蓝曲线 / 青蓝圈）都是它。四种样式靠 `[Export] IndicatorStyle Style` + `IsHostile` 区分，
> **几何量 / 颜色 / 透明度全部 `[Export]`**，运行期只读、只画。
>
> 脚本 `Scripts/Combat/SkillIndicator.cs` · 预制体 `Scenes/Combat/SkillIndicator.tscn` ·
> 截图表见 §8。表现层总纪律见 [vfx.md](vfx.md)（本组件是那张表里的一行）。

2026-10-20（J37）已接入敌人攻击：自爆／尖啸、陨石、冲锋、重击、跳跃／俯冲落地、回旋和炸弹。入口为EnemyAttackWarning，能力与已发射弹体各自持有独立预制体实例。此前只有Main下默认隐藏的共享实例，实际技能尚未调用；详见[J37接入与验收](character-design-j37.md)。

---

## 0. 三条纪律（和 `docs/vfx.md` §1 / §3 同源）

| 纪律 | 本组件怎么落实 |
|---|---|
| **不进物理世界** | 预制体子树里**没有** `CollisionObject3D`、**没有** `Unit`（它继承 `CharacterBody3D`）、**不进任何分组**。所以不会被清场（`enemy_units` 全杀）误伤、不会被单位计数、不会吃伤害。`DebugState()` 每次现算这三项（实测恒为 `0 / 0 / 无`，见 §7）。 |
| **只读、只画** | 一个战斗状态都不写。入参（起点 / 初速 / 重力 / 落点 / 方向 / 半径 / 长度）**全由调用方给**；本类不自己推战斗数值，也不读任何 `Ability` 字段。 |
| **不硬编码** | 颜色 / `Alpha` / `Width` / `Length` / `Radius` / `GroundOffset` / 呼吸周期 / `CurveSteps` 全是 `[Export]`；调用方显式传的数值优先，传 `0` 才回落到 `Export`。 |

⚠️ **组件不认"谁在用它"**：它不知道玩家/敌人，只认 `IsHostile`（配色）与调用方给的几何量。
敌人示警与玩家预览的主要区别就是 `IsHostile = true` 加"谁在什么时候调 `ShowXxx`"。

---

## 1. 四种样式的接入契约

| # | `IndicatorStyle` | 谁用 | 入口方法 | 画出来是什么 | 关键参数 |
|---|---|---|---|---|---|
| ① | `CurvePath` | **玩家**（长虹贯日之类抛物/弧线投掷） | `ShowCurvePath(origin, startVelocity, gravity, maxDistance)` | 半透明、**有宽度**的抛物线带 | `Width`（带子宽）、`CurveSteps`（采样段） |
| ② | `AreaPoint` | **敌人**（落点红圈）· **玩家**（准星指哪就是哪） | `ShowAreaPoint(worldPos, radius)` / `ShowAreaPointAtCrosshair(owner, maxDistance, radius)` | 贴地圆圈（环 + 很薄的填充盘） | `Radius`、`RingFlatten`、`FillAlphaScale` |
| ③ | `CurvePathPlusArea` | **玩家限定** | `ShowCurvePathPlusArea(origin, startVelocity, gravity, maxDistance, radius)` | ① + 末端 ② | ① 的参数 + `Radius` |
| ④ | `LineArea` | **敌人**（冲撞 / 大范围直线伤害） | `ShowLineArea(origin, direction, length, width)` | 地面**矩形**（贴地投影，有长度与宽度） | `Length`、`Width`、`EdgeWidth` |

**收尾统一用 `HideAll()`**；探针读 `IsShowing` / `CurrentStyle` / `LastSamplePoints` / `LastRadius` / `LastSource`。

### 1.1 ① 曲线路径（玩家）

```csharp
// 上游已经把"真实弹道"解出来了 → 原样喂进来，画的与飞的是同一条曲线
indicator.ShowCurvePath(chest, startVelocity, gravityMagnitude, maxDistance);
```

- **采样复用** `DashPathIndicator.SampleParabola(origin, velocity, gravityVec, flightTime, steps)` ——
  与 `DashPathIndicator`（冲刺/投掷的路径线）**同一个抛物面公式**，本类**不重写采样**。
- 时间轴由本类解：`flight = min(2·vY/g, maxDistance / 水平速度)`（初速朝下时只用后者），
  所以"落回发射高度"与"飞满射程"谁先到谁截断。
- **重力按大小给**（正的 m/s²，与 `Projectile` 里 `physics/3d/default_gravity` 的正号一致；传负数按绝对值）。
- 半透明来自 `Alpha`，宽度来自 `Width`。
- 带宽**面向镜头**（宽度方向 = `cross(段方向, 段中点→镜头)`，`docs/vfx.md` §4"拖尾做竖直的带"那条坑的做法）。
  ⚠️ 所以站在**正后方**看一条朝正前方的弧线时，它天然会收成一道窄带（路径正好沿着视线）——
  这是透视的必然结果，不是 bug；②/③ 的落点圈就是给"看起来像一条线"补的空间信息（用户要的 ③ 正是这个理由）。

### 1.2 ② 区域指向（敌人落点 / 玩家准星）

```csharp
// 敌人：跳跃落地类技能 —— 落点由上游算好（通常是"落地那一刻的位置"）
indicator.IsHostile = true;
indicator.ShowAreaPoint(landingPoint, radius);

// 玩家：准星指哪就是哪
indicator.IsHostile = false;
indicator.ShowAreaPointAtCrosshair(playerUnit, maxAimDistance, radius);
```

- `ShowAreaPointAtCrosshair` 内部**直接用** `AimUtil.GetCrosshairPoint(viewport, owner, maxDistance, CombatLayers.World | CombatLayers.UnitMask)`
  —— 与 `PlayerWeaponInput` / `EnergyWaveAbility` / `HawkEyeAbility` / 自测**同一个函数**（P3 做的），
  本类不自己拼 `ProjectRayOrigin`。射线打空时那个函数会退回"最大距离处"，圈照样会出来。
- **贴地**：两条路都会先把点压到地面（只撞 `World` 层的一条竖直射线，上方 2m → 下方 60m），
  再加 `GroundOffset`（默认 0.05m）—— 所以"空中的落点"也不会画出悬空的圈。
- 圈 = `TorusMesh` 环 + 很薄的 `CylinderMesh` 盘（盘透明度 = `Alpha × FillAlphaScale`）。

### 1.3 ③ 曲线 + 末端圈（玩家限定）

```csharp
indicator.ShowCurvePathPlusArea(origin, startVelocity, gravity, maxDistance, radius);
```

末端圈画在**曲线最后一点投影到地面**的位置（`SnapToGround`），不是悬在半空。
实现上 ③ 与 ① 共用同一个私有方法（`endRadius > 0` 就是 ③），所以两者的曲线**一模一样**。

### 1.4 ④ 直线区域（敌人冲撞 / 直线范围伤害）

```csharp
indicator.IsHostile = true;
indicator.ShowLineArea(origin, direction, length, width);
```

- **方向先贴地投影**：`direction` 的 Y 分量被丢掉再归一化 —— 上游给"准星射线方向"（带俯角）也行，
  画出来永远是一条**躺在地面上的矩形**，不是悬空的斜盒子。水平投影为 0（方向竖直）时退回 `-Z`。
- 填充 = `BoxMesh(1 × 0.02 × 1)` 缩放成 `width × length`；四条边 = "贴地朝向"的带宽（`EdgeWidth`，抬高 2cm 防 z-fighting）。

---

## 2. 加一种**新样式**要做什么

1. **先问要不要新样式**：能用一个已有的入口 + 参数表达就别加（例："细长曲线"= ① 调 `Width`；"小圈"= ② 调 `Radius`）。
2. `SkillIndicator.IndicatorStyle` 枚举加一项（**加在末尾**，别插队——枚举值会被 `.tscn` 序列化）。
3. 写**一个** `public void ShowXxx(...)`：
   - 开头 `Begin(Style.Xxx)`（它会 `HideAll()` + 复位 `LastRadius/LastSamplePoints` + 记 `CurrentStyle`）；
   - 中间只做几何：需要曲线就 `SampleBallistic(...)`（内部复用 `DashPathIndicator.SampleParabola`），
     需要带子就 `FillBand(...)`，需要圈就 `PlaceArea(...)`，需要贴地就 `SnapToGround(...)`；
   - 结尾 `Complete("一行说明（含入参）")` —— 它负责上色、开呼吸、写日志。
4. `DebugState()` 里的 `StyleName(...)` 加一条中文名字。
5. **预制体**：新形态如果现有网格表达不了，就在 `SkillIndicator.tscn` 里加节点（见 §3 的 `.tscn` 三个坑），
   并在 `_Ready` 里 `GetNodeOrNull` + `OwnMaterial` + `HideAll` 里隐藏它。**别在代码里 `new MeshInstance3D`**（用户要求预制体化）。
6. `_Process` 里**不要加每帧的物理查询**（呼吸只用 `ApplyTint`）。需要"每次 X 就画一次"的接法见
   `docs/vfx.md` §2 第 10 条（读公开只读量的上升沿，别改战斗层）。
7. 自测/拍摄台：`ShowXxx` 的入参**不要给默认值**（`docs/vfx.md` §2 第 7 条：MCP 那条路不认默认值）。
8. 文档：本文件 §1 的表加一行 + 加一张截图 + `docs/vfx.md` §1 的表加/改一行（需要时）。

---

## 3. 加一个**新指示器组件**要做什么（什么时候别再用这个组件）

先按 `docs/vfx.md` §2 走一遍；然后分三种情况：

| 你的需求 | 用什么 | 为什么 |
|---|---|---|
| 又是"给玩家/敌人看一块区域或一条路径"，只是形状不同 | **加样式**（§2），别新做组件 | 同一个组件的配色 / 呼吸 / 贴地 / 世界坐标约定已经解决过了 |
| 一次性爆开、播完自回收的特效（落圈扩散、破甲环） | **`LandingShockwave`**（`PlayOnce(pos, radius)`） | 那是"已经发生的事实"的反馈，`Duration` 后自毁；本组件是"将要发生"的预告，**不自动消失**，由调用方 `HideAll()` |
| 屏幕空间（准星圈、开镜、蓄力形状） | **`Scripts/UI/*`（`Control`）** | 本组件是 `Node3D`，画在世界里；屏幕空间的量走 `ChargeIndicator` / `ScopeOverlay` |

真要新做一个世界空间指示器，照本组件抄这五件事：
1. 挂 `Scripts/Combat/`，`Node3D` 根 + **预制体**；
2. `DrawRoot` 子节点 `TopLevel = true` + 单位变换 → 顶点直接写**世界坐标**（见 §4）；
3. 材质在 `_Ready` 里 `Duplicate()`（预制体的 sub-resource 在同一场景的多个实例间**共享**）；
4. 不显示时 `SetProcess(false)` + `Visible = false`（`DashPathIndicator` / 本组件都这么做）；
5. `DebugState()` 自带三项安全性取证（碰撞体 / 单位 / 分组）。

---

## 4. 预制体结构与"世界坐标"约定

```
SkillIndicator (Node3D, SkillIndicator.cs)
└── DrawRoot (Node3D)          ← _Ready 里设 TopLevel = true + Transform = Identity
    ├── Curve   (MeshInstance3D, ImmediateMesh + mat_band)   ① ③ 的带子（世界坐标顶点）
    ├── Edge    (MeshInstance3D, ImmediateMesh + mat_band)   ④ 的描边（世界坐标顶点）
    ├── Area    (Node3D, visible = false)
    │   ├── Ring (MeshInstance3D, TorusMesh  r=1 / 环粗 12% / Rings=64 RingSegments=6)
    │   └── Fill (MeshInstance3D, CylinderMesh r=1 / 高 0.02)
    └── LineFill (MeshInstance3D, BoxMesh 1×0.02×1)
```

- ⚠️ **`TopLevel` 是为了"挂哪都画在同一个世界位置"**：组件挂在 `Main` / 玩家 / 敌人身上都一样。
  它改的是**子节点的一个开关**，不是自己的 `GlobalPosition` —— `docs/HANDOFF.md` §6 第 7 条
  （`_Ready` 里改自己 `GlobalPosition` 会被生成方覆盖）因此与它无关。
- ⚠️ **`TorusMesh` 的 `Rings` / `RingSegments` 别写反**（反了圆画成菱形）：环 = `Rings 64` / `RingSegments 6`。
- ⚠️ **`.tscn` 的 `parent` 必须引用已定义过的节点**（否则节点被静默丢弃）；本预制体按
  `DrawRoot → Curve/Edge/Area → Ring/Fill → LineFill` 的顺序写，别打乱。
- 材质是**模板**：`_Ready` 给每个实例复制一份（`OwnMaterial`），敌人红圈与玩家青圈同时在场互不染色。

---

## 5. 参数（全 `[Export]`，编辑器里可调）

| 分组 | 参数 | 默认 | 说明 |
|---|---|---|---|
| 样式 / 阵营 | `Style` | `CurvePath` | 实例的默认样式（画完会同步成实际画法） |
| | `IsHostile` | `false` | `true` = 敌人（红）/ `false` = 玩家（青蓝） |
| | `PlayerColor` / `HostileColor` | 青蓝 / 红 | 两套默认色 |
| | `StyleColor` | `(0,0,0,0)` | 最后一道覆盖；`A > 0` 才生效，全透明 = 用阵营默认色 |
| 几何 | `Alpha` | `0.45` | 半透明（带 / 环 / 填充都乘它） |
| | `Width` | `0.35` | ① 带子宽 / ④ 矩形宽（米） |
| | `Length` | `8` | ④ 矩形长（米）；① 没传 `maxDistance` 时的兜底射程 |
| | `Radius` | `2.5` | ② ③ 的圈半径（米） |
| | `GroundOffset` | `0.05` | 贴地偏移（米） |
| | `EdgeWidth` | `0.14` | ④ 描边粗细（米） |
| | `CurveSteps` | `32` | ① 采样段数（传给 `DashPathIndicator.SampleParabola`） |
| 外观 | `RingFlatten` | `0.3` | 环的纵向压扁（薄环从第三人称浅角度看更清） |
| | `FillAlphaScale` | `0.35` | 圈内填充相对 `Alpha` 的透明度 |
| | `EmissionEnergy` | `1.8` | 自发光强度（颜色不看场景光照） |
| 呼吸 | `Pulse` / `PulsePeriod` / `PulseDepth` | `true` / `1.2` / `0.35` | 示警呼吸（`AdditiveBlend` **没做**：白地板会把加法混合冲成一片白，见 `docs/vfx.md` §4） |

---

## 6. 挂在哪

| 场景 | 怎么挂 |
|---|---|
| **历史共享实例**（保留兼容） | `Scenes/Main.tscn` → `Main/SkillIndicator`（世界空间，默认隐藏；不能同时承担多次攻击） |
| 玩家独立实例 | 挂到 `Scenes/Player/Player.tscn`（`Player/SkillIndicator`），`IsHostile = false` |
| 敌人独立实例（J37已接入） | 能力或已发射弹体的`AttackWarning/Indicator`，运行期从同一SkillIndicator预制体创建，`IsHostile = true` |

⚠️ 一个实例同时只有一个样式在画（`ShowXxx` 会先清掉上一次）。
"玩家预览"与"敌人示警"要**同时**在场上，就各用各的实例（一个共享实例不够）。

---

## 7. 安全性论证（实测，2026-10-17）

三项取证是 `DebugState()` 现算的（不是写死的字面量），同一份代码在实拍里读出来是：

```
技能指示器：样式=③ 曲线 + 区域 显示=True｜最近一次=曲线+末端圈：33 点（复用 DashPathIndicator.SampleParabola）
初速 (2, 11, -9) 重力 22 最远 12m 宽 0.35m 末圈半径 2.2m｜参数 阵营=玩家（青蓝） alpha=0.45 …
｜安全性 碰撞体=0（必须 0） 单位=0（必须 0） 子树分组=（无）（必须无） 自身分组=0（必须 0）
```

引擎侧独立取证（`evaluate_runtime_expression`，`/root/Main/SkillIndicator`）：

```
find_children("*","CollisionObject3D",true,false).size()  = 0
find_children("*","CharacterBody3D",true,false).size()    = 0     ← Unit 继承 CharacterBody3D，所以"没有单位"
find_children("*","Area3D",true,false).size()             = 0
get_groups().size()（自身 / DrawRoot）                     = 0 / 0
find_children("*","",true,false).size()                   = 7     ← 全是 Node3D / MeshInstance3D
```

**分组计数前后一位没变**（画着 ③ 的时候 / `HideAll()` 之后）：

```
enemy_units=0  projectiles=0   Curve 面数=1   Curve.visible=true
enemy_units=0  projectiles=0   Curve 面数=0   Curve.visible=false   IsShowing=false
```

**呼吸**（同一个材质间隔 0.3s 采样 4 次，`Alpha=0.45`、`PulseDepth=0.35`）：

```
0.4037 → 0.4251 → 0.2967 → 0.3792      （范围正好是 0.45 ~ 0.45×(1−0.35)=0.2925）
```

**"两个实例互不染色"**（同一帧读两份材质，`screenshots/si_dual.png` 就是这一帧）：

```
玩家实例 Area/Ring.albedo_color = (0.35, 0.88, 1.0, 0.45)     ← 青蓝
敌人实例 Edge.albedo_color      = (1.0, 0.26, 0.2, 0.4251)    ← 红（alpha 是各自呼吸相位）
```

原理：预制体里的 4 份 `StandardMaterial3D` 是**模板**，`_Ready` 里逐个 `Duplicate()` 给本实例
（同一份场景的多个实例默认共享 sub-resource，不复制就会互相染色）。

---

## 8. 实拍（2026-10-17，游戏内 `Main.tscn`，默认参数没改）

| 图 | 是什么 | 怎么让它出现的（MCP，全是**正式 API**，没有额外拍摄台方法） |
|---|---|---|
| `screenshots/si_curve_side.png` | **① 曲线路径**：半透明青蓝抛物线带（宽 0.35m）从玩家胸口抛向右侧 | `ShowCurvePath(Vector3(0,1.4,0), Vector3(13,9,-3), 22.0, 12.0)` |
| `screenshots/si_curve.png` | ① 的**对照组**：同一条曲线朝**正前方**（路径沿着视线 → 透视收成一道窄带） | `ShowCurvePath(Vector3(0,1.4,0), Vector3(0,8,-14), 22.0, 12.0)` |
| `screenshots/si_area_crosshair.png` | **② 区域指向（玩家）**：圈画在**准星**指到的地面点上（`AimUtil.GetCrosshairPoint`） | `ShowAreaPointAtCrosshair(get_node("/root/Main/Player"), 60.0, 2.0)` |
| `screenshots/si_area_red.png` | **② 区域指向（敌人）**：落点**红圈** | `IsHostile=true` + `ShowAreaPoint(Vector3(3.5,0,-8), 2.5)` |
| `screenshots/si_curve_area2.png` | **③ 曲线 + 末端圈**：弧线 + 落点圈 | `ShowCurvePathPlusArea(Vector3(0,1.4,0), Vector3(2,11,-9), 22.0, 12.0, 2.2)` |
| `screenshots/si_line_red.png` | **④ 直线区域**：9m × 2m 的红色冲撞矩形（贴地） | `IsHostile=true` + `ShowLineArea(Vector3(0.4,0,1.5), Vector3(0,0,-1), 9.0, 2.0)` |
| `screenshots/si_dual.png` | **"玩家与敌人共用"的正面证据**：同一帧里**两个实例各画各的** —— 左边青蓝圈（玩家 ②，准星点）、右边红色矩形（敌人 ④）。两份材质各是各的色 | ② 由原实例画；④ 由运行期 `duplicate()` 出来的第二个实例画（`IsHostile=true`），只用来取证，**不是项目里的常驻节点** |

⚠️ **拍摄方法**：不需要调慢任何参数 —— 本组件**不会自动消失**（不像 `LandingShockwave` 有 `Duration`），
所以一次 `ShowXxx` 之后画面会一直保持，随拍随取（`runtime screenshot`）。
`IsHostile` 用 `update_runtime_node_property` 改（`evaluate_runtime_expression` **不支持赋值**）。

---

## 9. 没做 / 边界（照实说）

- **敌人攻击已接入，玩家投掷预览保留旧接口**：`SpearThrowAbility.PreviewPath`仍使用`DashPathIndicator`；本轮不迁移玩家预览。敌人预警几何来自实际攻击参数与世界缩放，取消／死亡清理未释放预警，已释放落弹的圈交由弹体管理。
- **多实例网格隔离**：J37给可变ImmediateMesh启用resource_local_to_scene，避免某个实例Show／Hide清掉其他敌人的描边；材质仍逐实例复制。身体闪色与精英染色跳过整个预警子树。
- **① 的曲线不做逐段地形截断**：只按"落回发射高度 / 飞满射程"截断，弧线穿过矮掩体时不会自动停在
  掩体表面（`SpearThrowAbility` 的真实弹道会逐段打射线，因为它要的是真实落点）。要地形贴合就
  让上游用 `DashPathIndicator.SampleLine` 自己采样点列 → 目前没有"喂点列"的入口（要的话是另一个 `ShowPolyline`）。
- **`AdditiveBlend` 没做**：白地板 + 加法混合会冲成一片白（`docs/vfx.md` §4），所以三份材质都是 `Mix`。
- **`CurvePathPlusArea` / `CurvePath` 没有硬拦"敌人不许用"**：`IsHostile` 只换配色。
  用户的原话是"①③ 是玩家限定"，但拦下来只会让以后想给敌人做同类示警时得改代码；
  **要不要用是调用方的选择**，组件只负责画。
- **圈只有"环 + 薄盘"一种形态**：没有扇形 / 扇形进度环（那是另一类指示器，`ChargeIndicator` 在屏幕空间做的是方形圈）。
- **屏幕空间的指示器不在本组件**：准星处的 2D 圈归 `Scripts/UI/`（`ChargeIndicator` 等）。
