# 美术资源接入说明

> 给"要给交互物 / 特效 / 粒子 / UI 上模型或做替换"的人看。
> 规范细节见 [assets.md](assets.md)，逐元素的当前实现见 [visual-inventory.md](visual-inventory.md)。

---

## 一、30 秒现状

- **项目已经不是零美术资源了**（本文写于"零资源"时期，现状见文末的**更新记录**，冲突时以更新记录为准）：
  天空盒 ✅、竞技场 10 个 GLB ✅、玩家 + 10 个敌人模型 ✅、枪口火光 / 命中 / 爆炸特效 ✅、
  **UI 主题 + 贴图 + 准星 + 中文字体（系统字体占位）✅**、**界面说明见 [ui.md](ui.md)**。
  仍然程序化的：基地 4 个交互台、木桩、无人机、手枪、子弹/爆炸网格的部分材质。
- **规范已立**：资源目录 `Assets/`，命名 `<类别>_<分组>_<变体>`。
- **已有样板**：交互物（奖励节点、Boss 门）与特效（子弹、爆炸）这 4 个已经**从场景内联改成独立资源文件**，照它们的做法抄就行。
- **转换进度**：4 个场景已外置，**还剩 19 个**内联（9 个敌人 + Base / TestArena / Player / Drone / Training×3 / Pistol）。

## 二、东西放哪、叫什么

```
Assets/
  Models/       mesh_*.tres   （网格：几何形状与尺寸）+ <分组>/mesh_*.glb|fbx（源模型）
  Materials/    mat_*.tres    （材质：颜色、发光、透明）
  Textures/     tex_*.png     （以后加）
  Effects/      fx_*.tscn     （以后加：特效场景）
  Particles/    ps_*.tscn     （以后加：粒子场景）
  UI/           ui_*.png      （以后加：界面素材）
  Audio/        sfx_*.ogg / ui_*.ogg / music_*.ogg
```

**命名**：`<类别>_<分组>_<变体>`，全小写。
类别 `mat_` / `mesh_` / `tex_` / `fx_` / `ps_` / `ui_` / `sfx_`；分组 `node`（交互物）/ `combat`（战斗）/ `enemy` / `player` / `weapon` / `level` / `ui`。

例：`mesh_node_reward.tres`（奖励节点的网格）、`mat_enemy_chaser.tres`（追猎者的材质）、`fx_levelup.tscn`（升级特效）。

> **为什么这样命名**：前缀让同类在文件列表里自动聚拢（搜 `mat_enemy_` 一次看齐）；文件名回答"**给谁用**"，而不是重复文件里已经有的类型信息；全小写避免 Windows/Linux 大小写差异退化成两个文件。

**关键约定：材质挂在网格上，不挂在场景上。**
场景里只引用网格（`mesh = ExtResource(...)`），网格的 `.tres` 再去引用材质。这样**美术改颜色不用碰场景**，换模型只改场景里的一行。

## 三、动手路径（按类别）

### 1. 换某个交互物的模型（奖励节点 / Boss 门）

> ✅ **2026-10 第七轮换过一次**（奖励节点 = `LowPolyGemsPack2` 的 `gem1`），
> ✅ **2026-10 第八轮改成"9 种类型 9 颗不同形状"**（用户纠正：「我的期望是使用**不同形状**的模型
> 而不是**一个模型去换色**」）+ 加了**周身光圈**（光圈颜色 = 价值程度）。
> 实测数据与证据见文末「第七轮 / 第八轮」更新记录，逐颗宝石的量见 §三.1.1.1。

现在的节点结构（`Scenes/Interactables/RewardNode.tscn`）：

```
RewardNode（Node3D，挂 RewardNode.cs）
  ├─ Float（Node3D）              ← 上下浮动 + 自转做在它身上
  │    └─ Mesh（MeshInstance3D）  ← mesh = Assets/Models/nodes/mesh_node_<kind>.tres（**按类型换形状**）；scale 1.35；position.y 0.55
  ├─ Animation（AnimationPlayer） ← idle / active / dead 三条状态动画（见 §三.1.2）
  ├─ Presentation（Node3D）       ← InteractablePresentation（描边 + 按键图标），IconGap = 1.15
  └─ Aura（Node3D）               ← **周身光圈**（见 §三.1.3），三个环：
       ├─ Ring（MeshInstance3D）   ← 色带：光圈颜色 = 价值档位色，Unshaded + HDR 亮度，会吃到 glow
       ├─ Rim（MeshInstance3D）    ← 外圈暗描边（"亮色 + 黑描边"里的黑，负责在近白地板上画出边界）
       └─ Glow（MeshInstance3D）   ← 外发光（同色半透明一大圈）
```

**三条铁律**：

1. 节点名 **`Mesh` 不能改** —— `Interactable.VisualMesh` 按名字取它，"按类型上色 / 用尽淡出 / 失败抖动 / 描边 /
   按键图标高度"**全部**读它，改名**静默失效**（不报错、颜色没了、抖动也没了）。
   **但允许嵌套**（第七轮起 `VisualMesh` 先找直接子节点、找不到再**递归**找）：上面把它挂在 `Float` 下面是**故意的** ——
   浮动动画要写 `Mesh.position`，而失败抖动补间也写 `Mesh.position`，两个作者同一个属性会互相覆盖；
   所以浮动放在父节点 `Float` 上，抖动仍动 `Mesh`，两者叠加。
2. `Mesh` 必须是 **MeshInstance3D**。把整个 FBX/GLB 作为**场景实例**挂在下面（根是 `Node3D`）会 `VisualMesh == null`
   → 静默失去上色与淡出。要换模型就换它的 `mesh`（一个独立的 `mesh_*.tres`）。
3. **材质挂在网格上，不挂在场景上**（§二）：网格的 `.tres` 里写 `material`，场景只引用网格。
   ⚠️ **例外：光圈**（`Aura/*`）的材质**必须按实例覆盖**（`SetSurfaceOverrideMaterial`），
   因为它的颜色是**每档不同**的配置数据（§三.1.3）；那三份材质由代码按颜色缓存，**共用的资源从不被修改**。
4. **交互半径、`Prompt`、`SingleUse` 是玩法参数，不是美术参数**（红线见 §六）。

#### 1.1 奖励节点新模型：**实测**出来的轴心 / 朝向 / 缩放（不是猜的）

> ⚠️ 下表是 **第七轮量 `gem1`** 的那一份（12 颗**逐颗**的实测量在 **§三.1.1.1**，别只看这一张）。

| 量什么 | 怎么量 | 实测结果 |
|---|---|---|
| **轴心** | 实例化后算 `MeshInstance3D.get_aabb()`，看 `min.y` | **`min.y = 0.0000`** → 轴心在**底部**（脚底）。所以 `Mesh.position.y` 就是"离地多高"，**不需要再补半个身高** |
| **高度 / 占地** | 同上 `size` | `1.2 × 1.0 × 1.2 m`（1 米高）→ 想要 1.35 米高就 **`scale = 1.35`**（等比，一个数搞定） |
| **朝向** | 单独渲染观察 | 宝石是**多面体、没有正反面**，所以**这次不需要绕 Y 转 180°**（那条规则是给"有脸的单位"用的，见 §三.2） |
| **顶点绕序** | 有向体积 `V = Σ a·(b×c)/6`（散度定理） | **`V = -0.427193` → 反了！** 这个包的 FBX 绕序是**顺时针**，Godot 默认剔背面 → 画面上会看到宝石"内壁"（空心 / 破面观感）。**修正 = 每个三角形交换后两个索引**，修完 `V = +0.427193` |
| **法线** | 顶点相对形心的方向 · 法线，取平均 | `+0.6759` → **法线本来就是朝外的**（不需要取负）。这两件事**独立**，必须分别量 |
| **源文件里的摆放偏移** | 看 FBX 根下的子节点 `transform` | 每个宝石的 `MeshInstance3D` 带着 Blender 场景里的 X 偏移（`gem1` = +5、`gem2` = +7 … 每 2 米一个）。**只有把网格抽成独立资源才会消失**（抽出来之后网格是居中的） |

**怎么抽出 `mesh_node_<kind>.tres`**（工具已入库，**改一个 `MAP` 就能换一组宝石 / 换映射**）：

```powershell
# 1) 把要用的 .fbx 拷进 Assets/Models/nodes/（保持包内原名 gemN.fbx；它引用的两张 png 本项目不读）
# 2) 让 Godot 生成 .import：
& '<Godot 引擎路径>\Godot_v4.7.2-stable_mono_win64.exe' --headless --path '<项目>' --import
# 3) 跑抽取工具（**逐颗打印 V / 法线外向度 / 轴心 / 尺寸**，并按需修正绕序与法线；顺带出两张 12 颗对比图）：
& '<Godot 引擎路径>\Godot_v4.7.2-stable_mono_win64.exe' --path '<项目>' res://Scenes/Interactables/_ExtractGem.tscn
# 4) 材质**不用手工补**：工具会把 `mat_node_<kind>.tres` 写进网格的 `_surfaces` 里（`surface_set_material`）
```

> ⚠️ **不要**在 `.tscn` 里直接引用 `xxx.fbx::ArrayMesh_1p084` 这种**子资源路径**：
> 后缀是导入器现生成的（12 颗宝石各不相同），重新导入后可能变 → 引用**静默失效**（节点变成看不见的空壳）。
> 抽成 `mesh_*.tres` 之后几何数据独立于 FBX 的子资源命名。
>
> ⚠️ `ArrayMesh` **没有** `material` 属性（那是 `PrimitiveMesh` 才有的）——写 `material = ExtResource(...)`
> 会被当成无效属性忽略掉、网格变默认材质。要写进 `_surfaces` 字典里的 `"material"` 键
> （`surface_set_material` 的序列化形式）。工具用的是 `array_mesh.surface_set_material(0, mat)`，等价且不会写错。

#### 1.1.1 12 颗宝石的**逐颗实测量** + 9 种类型用哪一颗（第八轮）

素材：`…\素材\模型\LowPolyGemsPack2.zip`（0.4 MB，**12 颗宝石** `Meshes/gem1..12.fbx` + 2 张贴图 + blend）。
进仓库的是 **12 个 `.fbx`（约 230 KB，包内原名 `gemN.fbx`）+ 已抽出的 9 个 `mesh_node_<kind>.tres`**；
**zip / blend / Unity shadergraph 一律不入库**。两张 png（`PalleteTex.png` / `ReflectionTex.png`）虽然也拷了，
**但游戏里不读**：FBX 里写的是作者机器的绝对路径（`D:\Librarys\Desktop\Low Poly gems pack 2\…`），
导入时解析不到，而且按类型上色的材质会整体覆盖表面材质。

**逐颗实测量**（`_ExtractGem.tscn` 一次跑完打印的，单位 = 米；`V` 是散度定理算的有向体积）：

| 宝石 | 顶点/三角 | `V`（源 → 修正后） | 法线外向度 | 轴心 `min.y` | 尺寸 x×y×z | 形状（看 §四 的两张对比图） |
|---|---|---|---|---|---|---|
| `gem1` | 40 / 18 | −0.427193 → **+0.427193** | +0.6759 | 0.0000 | 1.20 × 1.00 × 1.20 | 经典多面钻石（宽冠 + 收窄亭部），**12 颗里最好认** |
| `gem2` | 260 / 276 | −0.139036 → **+0.139036** | +0.0064 | 0.0000 | 1.00 × 1.00 × 0.20 | 圆角方块（**薄片**，倒角最细） |
| `gem3` | 112 / 96 | −0.210958 → **+0.210958** | +0.5266 | −0.0000 | 1.00 × 1.00 × 0.50 | 圆角方块（比 gem2 厚） |
| `gem4` | 72 / 24 | −0.111111 → **+0.111111** | +0.6655 | −0.0000 | 1.00 × 1.00 × 1.00 | **四角星** |
| `gem5` | 72 / 44 | −0.376543 → **+0.376543** | +0.8031 | 0.0000 | 1.00 × 1.00 × 1.00 | **厚实盾形**（下尖），**体积最大**、法线最外 |
| `gem6` | 64 / 32 | −0.089658 → **+0.089658** | +0.5742 | 0.0000 | 1.00 × 1.00 × 0.25 | 倒五边形（本垒板状，形心偏高） |
| `gem7` | 84 / 40 | −0.051552 → **+0.051552** | +0.6253 | 0.0000 | 0.50 × 1.00 × 0.25 | 细长六棱晶（**最窄**） |
| `gem8` | 82 / 42 | −0.067130 → **+0.067130** | +0.6498 | **+0.0097** | 0.75 × 1.00 × 0.25 | 水滴 / 长五边形（形心 0.338，**唯一底部离原点 1 cm 的**） |
| `gem9` | 96 / 52 | −0.159175 → **+0.159175** | +0.5033 | 0.0000 | 1.00 × 1.00 × 0.25 | 八边形圆块 |
| `gem10` | 36 / 12 | −0.125000 → **+0.125000** | +0.5660 | 0.0000 | 1.00 × 1.00 × 0.50 | 六边形晶块（面数最少） |
| `gem11` | 24 / 8 | −0.166667 → **+0.166667** | +0.5090 | 0.0000 | 1.00 × 1.00 × 0.50 | 正方形板块 |
| `gem12` | 64 / 32 | −0.054687 → **+0.054687** | +0.6250 | −0.0000 | 0.50 × 1.00 × 0.25 | 细长尖晶（与 gem7 同族） |

**从这张表能读出来的四件事**（第八轮实测，上一轮只有 1 颗的样本时看不出来）：

1. **12 颗的绕序全都是反的**（`V` 全为负）→ **逐颗都要修**，不能因为修过 `gem1` 就以为都一样。
   修完 `V` 全部为正（`|V|` 与修正前相同，只是符号）。
2. **12 颗的法线本来就都对**（外向度全为正，最小 `gem2` 的 +0.0064 —— 薄片的平面法线几乎垂直于"相对形心方向"，
   所以这个指标接近 0 是**正常**的，不是"要取负"的信号）。→ **绕序与法线是两件独立的事，分别量、分别修**。
3. **12 颗的高度都正好 1.00 m**（宽度 0.50~1.20、厚度 0.20~1.20）→ **等比 `scale = 1.35` 让所有类型一样高（1.35 m）**，
   不用逐个调；`min.y = 0.0000`（gem8 差 1 cm）→ 轴心都在脚底，`Mesh.position.y = 0.55` 就是"离地 0.55 m"。
4. ⚠️ **12 颗里有 8 颗是"薄片"**（厚度 0.20~0.50）：它们的辨识度靠**正面剪影**，转到侧面会变成一条窄边。
   所以本轮的做法是"**形状（认类型）+ 材质色（认类型）+ 光圈色（认价值）**"三重编码，
   而不是只靠形状 —— 见 §三.1.3。

**9 种类型用哪一颗**（`_extract_gem.gd` 的 `MAP`；挑的判据 = **形状辨识度**，高价值节点优先给最好认的形状）：

| `RewardNodeKind` | 宝石 | 形状（远处一眼认出的依据） | 材质色（`mat_node_<kind>`） | 价值标签 → 光圈色 |
|---|---|---|---|---|
| `Weapon`(0) | `gem7` | 细长六棱晶（刀锋感） | 橙 (0.90, 0.50, 0.05) | ⚠️ 节点表里**没有这一行**（A3 已并入进化池）→ 光圈中性白 |
| `Enhancement`(1) | `gem5` | 厚实盾形（**立体**，体积最大） | 青蓝 (0.35, 0.80, 1.00) | `tag_mid` → **青蓝** |
| `Evolution`(2) | `gem1` | **经典多面钻石**（最好认） | 紫 (0.62, 0.34, 0.98) | `tag_high` → **金** |
| `Mutation`(3) | `gem4` | **四角星**（异变爆发） | 品红 (0.95, 0.18, 0.72) | `tag_high` → **金** |
| `Resource`(4) | `gem10` | 六边形晶块（矿） | 金黄 (0.98, 0.87, 0.28) | `tag_low` → **绿** |
| `Heal`(5) | `gem8` | **水滴 / 长五边形**（药水） | 绿 (0.30, 0.90, 0.45) | `tag_low` → **绿** |
| `Special`(6) | `gem6` | 倒五边形（本垒板状，形状少见） | 近白 (0.95, 0.96, 1.00) | `tag_low` → **绿** |
| `Shop`(7) | `gem9` | 八边形圆块（币） | 红 (0.95, 0.25, 0.35) | `tag_mid` → **青蓝** |
| `Portal`(8) | `gem11` | 正方形板块（门 / 传送口） | 青绿 (0.10, 1.00, 0.75) | `tag_low` → **绿** |

**留下备用的 3 颗**（加新节点类型时从这里挑，别再从头选 12 颗）：

| 宝石 | 形状 | 留给什么用途 |
|---|---|---|
| `gem2` | 圆角方块，**最薄**（0.20 深），倒角最细（260 顶点） | 再加一个"**块状资源**"类节点（宝箱 / 材料堆）时的首选 —— 它有 12 颗里最精细的倒角 |
| `gem3` | 圆角方块，厚一档（0.50 深） | 同上，但要**比 gem2 更厚实**的那种（例如"重铸台"） |
| `gem12` | 细长尖晶（0.50 宽） | 再加一个"**尖晶 / 碎片**"类节点（例如"诅咒 / 祭坛"）—— 与 `gem7`（Weapon）同族、比它更细 |

> 加新类型时**不用改代码**：放一份 `Assets/Materials/mat_node_<新枚举名小写>.tres` +
> 往 `_extract_gem.gd` 的 `MAP` 里加一行 + 重跑工具生成 `mesh_node_<新枚举名小写>.tres` 即可
> （`RewardNode.BuildShapes/BuildPalette` 都是按枚举名自动映射的）。

#### 1.2 节点的三种状态动画：**待机 / 激活 / 失效**

需求原话：「这些应该是**悬浮的**，且**有待机/激活/失效的状态动画**」。
三种状态**没有另造一套**，全部由 `Interactable` **已有的**交互状态推导（`Scripts/Interaction/Interactable.cs`
的 `RefreshStateAnimation()`，只读状态、只播动画）：

| 动画名（`Animation` 里的 AnimationPlayer） | 对应**已有**状态 | 表现 |
|---|---|---|
| `idle` **待机** | 可用、玩家不在范围（`!CanInteract && Enabled`） | 慢速上下浮动（0 → 0.18 m，3 秒循环）+ 慢速自转（3 秒一圈）+ 不透明 + **光圈正常**（`Aura:scale` 1 ↔ 1.06 呼吸） |
| `active` **激活** | 玩家在范围内、这一刻真能按 F（`CanInteract`） | 浮得更高更快（0 → 0.28 m，1.2 秒）+ 自转快 2.5 倍 + 微微胀缩（1.35 ↔ 1.5，脉动）+ **光圈胀得更快更大**（1 ↔ 1.16，1.2 秒） |
| `dead` **失效** | `!Enabled`（本关没抽中的节点 / **降临物下落中**）或用完（`Used` / `Vanished`） | 下沉（0.04 m）+ 缩小（1.08）+ **半透明 0.72**（"熄灭"）+ **光圈缩到 0.55 并几乎消失**（`Ring/Rim/Glow` 的 `transparency` 0.82 / 0.90 / 0.95） |

**约束（照抄就能加新节点的状态动画）**：

- **不要动根节点的 `scale`**：那是 `Appear()` / `Disappear()` 补间的地盘（互相打断）。
  动 `Float`（浮动 / 自转）、`Mesh`（缩放 / 透明度）与 `Aura`（光圈缩放 / 透明度）就够。
- **不要动共享材质**：一局 10 个节点共用同一份 `mat_node_<kind>.tres` **与同一份光圈材质**，
  动它会把同类 / 同档位的节点一起改掉（"半透明"用的是 `MeshInstance3D.transparency`，它是**每实例**的，安全；
  光圈也**只动 `transparency` 这个每实例属性**，绝不写材质）。
- `appear` / `disappear` 这两条名字**优先级更高**：状态机会在它们播放期间**不抢播放器**
  （`Interactable.RefreshStateAnimation` 里那条 guard）。没做状态动画的交互物（Boss 门、基地交互台）
  只是**什么都不播**，一次查表之后就不再查（零开销）。
- 拍摄台：`res://Scenes/Interactables/_NodeShowcase.tscn`（三种状态并排 + 三档光圈 + 9 种类型 + 失效对比 +
  逐项读数 + 出图，用法见 §四）。

#### 1.3 周身**光圈**：颜色 = **价值程度**（第八轮）

需求原话：「后面其实是考虑**加入一些周身特效来做信息展示的**，比如**光圈**，**光圈颜色来表示价值程度**」。

**做成了什么**：悬浮的宝石**脚下一圈贴地平放的光环**（`RewardNode.tscn` 的 `Aura` 下三个环，见 §三.1 的结构图）：

| 子节点 | 网格 | 材质 | 作用 |
|---|---|---|---|
| `Ring` | `Assets/Models/nodes/mesh_node_aura_ring.tres`（`TorusMesh` 内 0.45 / 外 0.85，压扁到 12% 厚） | **价值档位色**，`Unshaded` + albedo ×2.2（HDR） | 主色带。`Unshaded` = 不参与光照 → **亮地板与暗天空下颜色一致**；×2.2 超过场地 glow 阈值 2.0 → 自带光晕 |
| `Rim` | `mesh_node_aura_rim.tres`（内 0.85 / 外 0.97） | 固定暗色 (0.04, 0.04, 0.05) ×0.1 | **外圈黑描边**（准星那套"亮色 + 黑描边"：亮色负责暗背景、描边负责近白地板） |
| `Glow` | 复用 `Ring` 的网格，缩放到 1.42 倍 | 同色 ×1.5、alpha **0.32** | 外发光（"周身"这件事要看得出来，又不抢色带的色相） |

- **颜色从配置读，代码里一个档位色都没写死**：`Config/Tables/ValueTags/tag_*.tres` 新增一列
  **`AuraColor`**（`Scripts/Config/ValueTagConfig.cs`）。取值链与"这个节点值几波"**完全同一条**：
  节点行的 `ValueTags` → 标签行；多个标签命中时取 `WaveCount` **最大**的那一行。

| 价值标签 | 波数 | `AuraColor` | 谁挂它 |
|---|---|---|---|
| `tag_high` | 4 | **金** `(1, 0.75, 0.15)` | 进化池 / 异化池 |
| `tag_mid` | 3 | **青蓝** `(0.15, 0.72, 1)` | 强化池 / 补给站 |
| `tag_low` | 1 | **绿** `(0.4, 0.86, 0.45)` | 局外资源 / 恢复 / 特殊奖励 / 降临节点 |

- **三档只靠色相区分**（金 / 青蓝 / 绿），不靠明度 —— 这样在近白地板和紫色星空下都不会混。
- **失效节点不发光**：`dead` 动画把光圈缩到 0.55 并把三个环的 `transparency` 拉到 0.82~0.95
  （"已经用掉的节点"在 `Disappear()` 里整个缩掉并隐藏）。
- ⚠️ **实现上的一个真坑（自测抓到的）**：光圈颜色要读 `ConfigIndex` 的**表索引**，而那张索引是
  `ConfigIndex.CallDeferred(BuildTableIndex)` 建的 —— `Main.tscn` 里节点的 `_Ready` **早于**那次 defer，
  于是第一次读到"空索引"，颜色被当成"读不到"**缓存**住了 → **整局光圈都是中性白**（截图看不出来，日志里有）。
  修法两条：① `RewardNode.ApplyIdleVisual()` 里 **`CallDeferred` 再跑一次** `ApplyAuraColor`（defer 队列先进先出，
  `ConfigIndex` 排在主场景之前，所以这一跑索引必然已就绪）；② **只有真读到才写缓存**，读不到不落缓存。
- ⚠️ **`Kind = Weapon` 光圈是中性白**：它**没有节点表行**（A3 之后并入了进化池），所以没有价值标签可读 ——
  这是刻意的，不是漏配（日志里也**不**报 warning：有行却解析不出标签才报）。

### 2. 换单位模型（玩家 / 9 种敌人 / Boss / 召唤物）

每种单位的模型都在各自场景里的 `Model` 节点（`Scenes/Enemies/Chaser.tscn` 等）。

**⚠️ 三个必踩的坑**：

| 坑 | 说明 |
|---|---|
| **轴心** | 单位自身原点在**脚底**。现在 `Model` 节点的 `transform.y = 0.9`（玩家半身高），因为当前的胶囊网格**轴心在中心**。换成新模型时：**新模型轴心在脚底 → `Model.y = 0`**；**轴心在中心 → `Model.y = 高度/2`**。判据用"量 AABB"的办法实测（见下方脚本），不要凭猜 |
| **朝向** | `CharacterMovement` 只绕 Y 轴旋转 → 模型在 Godot 里必须**朝 -Z**。⚠️ 但 glTF 2.0 规范规定**模型正面朝 +Z**，而 Godot 用 **-Z 作为前方** → **正确导出的 glTF 角色导进来是背对镜头的，要绕 Y 转 180°**（逐包仍需实测） |
| **相机高度** | 相机胸口高 `1.5` 写死在 `CameraRig.cs`，`Player.tscn` 没覆盖 → 换高个子模型要么改代码，要么在 `Player.tscn` 里覆盖 `TargetOffset` |

#### ✅ 已实测结论（2026-10，Kenney Space Station Kit / Mini Characters，逐包量过）

> **上面三条"待实测"现在有答案了**，`Player.tscn` 就是照这个改的。细节与证据见
> [art-integration-log.md](art-integration-log.md) §2（含"转向前 / 转向后"两张对比截图）。

1. **轴心 = 脚底**：本批 **11 个 `.glb` 的 `min.y` 全是 `0.000`**，没有一个在几何中心。
   → **`Model.transform.y = 0`**，`y` 分量只用来放**缩放**，**不要再写 `0.9`**。
2. **朝向 = 正面朝 +Z**：实测不转的时候角色的**正脸**对着镜头 → **必须绕 Y 转 180°**。
   写法：**basis 的 x、z 轴取负，y 轴保持正** —— `Transform3D(-s,0,0, 0,s,0, 0,0,-s, 0,0,0)`。
   判据是**行列式 > 0**（两个轴取负 = 绕 Y 转 π，不是镜像；`det < 0` 会让法线/背面剔除出错）。
3. **模型原生高度不一定是 1.8 m**：Kenney Mini Characters 只有 **0.6713 m**（chibi 比例），
   而碰撞胶囊是 1.8 m → **要缩放 `s = 1.8 / 0.6713 = 2.6814`**。
   **`Body`（碰撞胶囊）一个字节都不要动**，让视觉与碰撞同高同底（都从 `y=0` 到 `y=1.8`）即可。
4. **枪的位置要跟着新模型调**：`WeaponMount` 原在 `(0.36, 1.15, -0.25)` —— 对 chibi 角色来说
   **1.15 m 已经在他脑袋里**（该模型头占 `y ∈ [0.92, 1.8]`）。已改到 `(0.45, 0.70, -0.34)`。
   **换任何新模型都要重新对一次手部高度。**
5. **`.glb` 自带动画可以零 C# 先跑起来**：在场景里给实例化子节点加属性覆盖
   `[node name="AnimationPlayer" parent="Model" index="1"]` + `autoplay = "idle"`。
   ⚠️ **`index` 是兄弟节点序号**（GLB 根下 `character-male-a`=0、`AnimationPlayer`=1），写错会**静默不生效**。
   导入的 `idle` 是 `LOOP_NONE`，所以是"播一遍停在最后一帧"；要真正循环/切 walk 得写代码。

**好消息**：`Model` 这个名字**没有任何代码引用，可以随便改**。

### 3. 加特效 / 粒子（当前完全空白）

项目里**没有任何粒子**，枪口火光、命中火花、尘土、升级特效都要从零加。做法：

1. 新建 `Assets/Effects/fx_xxx.tscn`（或 `Assets/Particles/ps_xxx.tscn`）
2. 在需要的地方 `preload` 并实例化；已有的特效样板是 `Scenes/Combat/Explosion.tscn`（现在由 `Explosion.tscn` + `Assets/Models/mesh_combat_explosion.tres` 组成）
3. **⚠️ 注意 `Explosion.tscn` 里的 `SphereShape3D radius=1` 是玩法基准**（伤害判定用它，`Detonate()` 靠 `Scale` 缩放）—— 那是碰撞体，**不是美术资源**，别顺手改

### 4. 换 UI 素材

> ✅ **这一节已经在 2026-10「第五轮」落地了：完整操作说明搬到了 [ui.md](ui.md)（新建，UI 外观的唯一入口）**。
> 下面这些是**仍然成立**的约束，细节（主题怎么组织、怎么加新样式、字体怎么换、准星怎么换）看 ui.md。

`Scenes/UI/` 下每个界面。**⚠️ 换之前先看这几条**：

| 坑 | 说明 |
|---|---|
| **主题是唯一的样式入口** | `Assets/UI/Themes/ui_theme.tres` 管字体 / 字号 / 颜色 / 面板 / 按钮；挂在各界面根 `Control` 的 `theme` 上。**换皮只改主题，不碰场景** |
| 一批控件是**按名字取的** | 改名必须先改代码（见下面第五节） |
| 血条的**填充条 / 残影条 / 文字**按名字取 | 见下面的预制体说明；改 `Fill` / `DelayBar` / `Label` 这三个名字要同步 `PlayerHud.cs` / `WorldOverlay.cs` 里的常量 |
| 血条配色仍由**代码**按血量写（绿 → 黄 → 红） | 美术在预制体里设的颜色会被覆盖；要换配色改 `HealthColor()`（`PlayerHud.cs`、`WorldOverlay.cs` 各一份） |

**三条血条已经是预制体**（`Scenes/UI/Bars/`），改外观不用碰 C#：

| 预制体 | 用在哪 | 谁实例化 |
|---|---|---|
| `BarEnemy.tscn` | 敌人脚下血条（每个受伤的怪一份） | `WorldOverlay.cs`（尺寸取 `ConfigIndex.tres`） |
| `BarBoss.tscn` | 界面顶部首领主血条 | `PlayerHud.cs`（挂到 HUD 层，节点名 `BossPanel`） |
| `BarPlayer.tscn` | 左上角玩家血条 | `Hud.tscn`（实例节点名 `PlayerBar`） |

- 三者结构一致：`Fill`（填充）、`DelayBar`（延迟条 / 受伤残影，受伤后缓慢跟随，可改颜色）、`Animation`（空 `AnimationPlayer` 占位）、可选 `Bg`（底色 / 边框）与 `Label`（文字占位）。
- **控件类型随意换**：代码只按 `Control` 用它们（`size` / `modulate`），`ColorRect` / `TextureRect` / `NinePatchRect` 都行，`Fill` 已经是 `TextureRect`（贴图条）。
- **尺寸**：`BarPlayer` 的宽高**由场景说了算**（代码只改填充宽度，不再每帧覆盖）；`BarEnemy` 的尺寸由 `ConfigIndex.tres` 的 `EnemyHealthBarWidth/Height` 决定，预制体按同比例整体缩放。
- ⚠️ `BarEnemy` 里可缩放的整条内容都放在 `Content` 下、且**以 Root 原点为中心**（现在是 `Content` 摆在 `(-1,-1)`、尺寸 +2）—— 代码把 `Root.Position` 当血条中心，改布局要保持这个中心约定（偏了会打警告）。
- 首领血条的 `Bar` 是 `ProgressBar`（不是 `Control` 填充条），填充色在预制体的 `theme_override_styles/fill` 里。


### 5. 换颜色（最快的收益）

如果只想让玩家**区分池子类型**（现在 10 个奖励节点全是一种浅蓝，玩家分不清强化/进化/异化/商店），最小改动是在 `RewardNode` 里按 `Kind` 用 **Surface Material Override** 上色 —— 注意这一局 10 个节点**共享同一个 mesh/material**，直接改共享材质会把 10 个一起改掉。

## 四、改完必须验证

```powershell
# 1) 编译（必须 0 warning / 0 error）
& 'F:\Godot项目\项目a\build.ps1' --no-incremental -v minimal

# 2) 跑游戏内自测（会走完：进关 → 交互 → 奖励 → 战斗 → 结算）
'1' | Set-Content 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'
$godot = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe'
Start-Process $godot -ArgumentList '--path','F:\Godot项目\项目a','res://Scenes/Main.tscn'   # 必须带场景参数
Start-Sleep -Seconds 27
Remove-Item 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'   # 必须删！

# 3) 看日志有没有加载错误
$dir = 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\logs'
$f = Get-ChildItem "$dir\run_*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Get-Content $f.FullName | Select-String -Pattern 'ERROR|Exception|清场完成|端到端'
```

**⚠️ 编辑器会缓存场景**：用编辑器脚本 `load()` 验证时拿到的是**改动前的副本**，必须用 `ResourceLoader.CACHE_MODE_IGNORE` 强制读盘，否则会看到假失败。

**⚠️ 2026-10 起：并发代理环境下 MCP 的 `run_project` 不可靠**（多开 Godot 会抢编辑器的构建回调），
**优先用上面的命令行直跑**；`stop_project` 是全局的，会杀掉别的代理的自测，**不要用**
（详见 [todo-handoff.md](todo-handoff.md) 末节）。

### 奖励节点模型的拍摄台（换模型 / 调状态动画 / 调光圈时用它出图）

```powershell
# 三种状态并排 + 三档光圈 + 失效对比 + 9 种类型总览 + 逐项读数 + 8 张图
& '<Godot>\Godot_v4.7.2-stable_mono_win64.exe' --path 'F:\Godot项目\项目a' res://Scenes/Interactables/_NodeShowcase.tscn
# 出图：docs/screenshots/node_40_states.png（三态全景）/ 41 idle / 42 active / 43 dead
#       44_kinds9（9 种类型总览：形状 + 光圈色）/ 45_aura_tiers（三档光圈，玩家视角）/ 
#       46_aura_dead（失效 vs 可用并排）/ 47_kinds9_far（26 米外：远处还能不能认）
# 读数：stdout / user://logs/godot.log 里搜 [NodeShot]
```

**挑宝石形状的对比图**（一次性工具 `_ExtractGem.tscn` 顺带出的，12 颗一样大、正交投影）：

```powershell
& '<Godot>\Godot_v4.7.2-stable_mono_win64.exe' --path 'F:\Godot项目\项目a' res://Scenes/Interactables/_ExtractGem.tscn
# 出图：docs/screenshots/gem_00_sheet.png（正交正视：看剪影）/ gem_01_sheet_tilt.png（3/4 视角：看厚度，认薄片）
```

## 五、改名前必须先改代码的节点

这些节点名被 C# 按字符串取用，**改名会静默失效**（不报错，功能没了）：

| 节点名 | 谁在用 | 影响的场景 |
|---|---|---|
| `Mesh` | `Interactable`（"用尽变暗" / 按类型上色 / 淡出）、`InteractablePresentation`（描边 / 抖动 / 图标高度） | 奖励节点、Boss 门、基地 4 个交互台（**名字不能改；2026-10 第七轮起允许嵌套在子节点下**） |
| `Float` | `RewardNode` 的**状态动画轨道**（`Float:position` / `Float:rotation` / `Float/Mesh:scale` / `Float/Mesh:transparency`） | 奖励节点（改这个名字要同步改 `RewardNode.tscn` 的动画轨道路径） |
| `Aura`、`Aura/{Ring,Rim,Glow}` | `RewardNode`（`AuraNodeName` / `AuraRingName` / `AuraRimName` / `AuraGlowName`：按价值档位上光圈颜色）、**状态动画轨道**（`Aura:scale`、`Aura/*:transparency`） | 奖励节点（2026-10 第八轮新增；改名要同步改 `RewardNode.cs` 的常量与 `RewardNode.tscn` 的动画轨道） |
| `Muzzle` | `Weapon`（枪口位置） | 各武器 |
| `Hud/Root/PlayerBar`、`BarPlayer/Fill`、`BarPlayer/DelayBar` | `PlayerHud`（`_Ready` 按名字抓） | HUD |
| `Hud/BossPanel`、`Panel/{Icon,NameLabel,Bar,StateLabel}` | `PlayerHud`（`BuildBossPanel`，`BossPanel` 是代码挂上去时的名字） | `BarBoss.tscn` |
| `Bars/*/Content/Fill|DelayBar|Label` | `WorldOverlay`（每个敌人血条实例） | `BarEnemy.tscn` |
| `Model` / `Body` | **无引用，可自由改名** | 所有单位 |
| ~~`CrosshairH` / `CrosshairV` / `CrosshairDot`~~ | **已删除**（第四轮换成 `Hud.tscn` 的 `Root/Crosshair` 贴图准星）；这三个名字**本来就没有任何代码引用** | HUD |

完整清单（15 组共 26 个节点名）见 [visual-inventory.md](visual-inventory.md) 第 8 节。

## 六、红线（这些不是美术资源，别改）

1. `Explosion.tscn` 的 **`SphereShape3D`** 是伤害判定基准
2. `Interactable.OnUsed()` 要求材质是 **`StandardMaterial3D`** —— 换成 `ShaderMaterial` 会让"已用变暗"**静默失效**
3. 交互物的**交互半径、提示文字**是玩法参数，不是表现
4. `ConfigIndex.tres` 里的 `EnemyHealthBar*` 等是**数值配置**，不要当美术参数调

## 七、还没做的（可以接着推）

| 项 | 说明 |
|---|---|
| 19 个场景仍是内联网格/材质 | 9 个敌人 + Base / TestArena / Player / Drone / Training×3 / Pistol，模式与外置的 4 个一样 |
| 视觉由**代码生成**的还剩 5 处 | `ChallengerMark` 的三角环（`ArrayMesh`）、受击闪色、精英染色、伤害跳字、"用尽变暗" —— 换这些要改 C#。**三条血条已是预制体**（见第三节第 4 条） |
| 血条配色仍是代码写死 | 绿→黄→红在 `HealthColor()` 里；要按美术主题色走需要改成读预制体颜色 |
| 血条的动画还没做 | 预制体上有空的 `AnimationPlayer` 与"数值更新 / 表现更新"两个方法（`SetRatio` / `AdvanceDelay`），插入动画的接口已留好 |
| 奖励节点按类型上色 | 现存可玩性问题，见第三节第 5 条（**已做**，见文末更新记录） |
| 粒子 / 动画 / 骨骼 | 枪口火光、命中、爆炸粒子已接；模型动画已由 `UnitAnimator` 接管（见 [art-integration-log.md](art-integration-log.md) 后续轮次） |
| **UI 只剩"铺满"的体力活** | 主题 + 贴图 + 字体 + 准星已就位（**见 [ui.md](ui.md)**）；剩下的是把 7 个弹窗逐个精修、血条换贴图、中文自带字体（发行前必做） |

---

## 附：本次已完成的（作为抄写样板）

| 已有样板 | 路径 |
|---|---|
| 交互物网格 + 材质 | `Assets/Models/mesh_node_reward.tres`、`Assets/Materials/mat_node_reward.tres` |
| Boss 门 | `Assets/Models/mesh_node_boss_door.tres` |
| 子弹 | `Assets/Models/mesh_combat_bullet.tres` |
| 爆炸 | `Assets/Models/mesh_combat_explosion.tres` |
| 完整规范 | [assets.md](assets.md)（目录、命名、依赖方向、替换步骤、格式规矩、验证、红线） |
| 逐元素清单 | [visual-inventory.md](visual-inventory.md)（88 个视觉元素 + 难度分级 + 踩坑清单） |
---

## 更新记录（2026-10-11 晚 · 实现已跟进）

以下几项**已经实现**，本说明上文若与代码冲突，以本节为准：

1. **奖励节点已按类型上色**（不再是"全都浅蓝"）。9 种类型各有独立材质 `Assets/Materials/mat_node_{weapon,enhancement,evolution,mutation,resource,heal,special,shop,portal}.tres`，最小两两 RGB 距离 **0.374**。
   - 实现方式：`RewardNode.ApplyIdleVisual()` 按 `Kind` 调 `MeshInstance3D.SetSurfaceOverrideMaterial(0, …)`（**Surface Override**），共享材质/网格**从不被修改** —— 所以"一局 10 个节点共享材质"这个坑已经被绕开了。
   - 材质文件名由枚举名自动映射：`mat_node_<kind 小写>.tres`，**加新类型只要按这个规则放一份材质**。
   - 每个节点头顶有 `Label3D` 名牌（billboard + 不遮挡），写着"强化池/进化池/…/前往下一关"。
2. **交互节点用掉后会消失**（不再是"变暗"）。`Interactable.OnUsed()` → `Disappear()`：Duplicate 材质 → 开 Alpha → 0.45s Tween（`scale 1→0.05` + 淡出 + 自发光归零）→ 隐藏并**从 `Interactable.All` 摘除**；`CanInteract` 增加 `!Vanished` 门槛。
   - `Interactable.All` 的语义已变为**"当前可交互的交互物"**（不是"场景里所有交互物"）。
   - `ResetNode()`（切关复位）会恢复 `Used/Vanished/Visible/Scale` 并**重新登记进 `All`**。
3. **降临物改为在 Boss 门消失的位置降临**：`StageController.SpawnPortal()` 找到 `BossDoor` → 取位置 → 让门一并收起 → 降临物在**门的正上方 12 米**生成、下落中 `Enabled=false`（不可交互）→ 三段 Tween（落 0.7s → 回弹 0.18s → 落定 0.18s）→ 落地回调里才 `Enabled=true` + `Appear()`。找不到门时退回玩家附近。
4. **动画口子已留好**：`RewardNode.tscn` 与 `BossDoor.tscn` 都有 `AnimationPlayer` 占位（节点名 `Animation`）。`Appear()`/`Disappear()` 会**先查有没有同名 `appear`/`disappear` 动画**：有就播动画并按动画时长收尾，没有才用代码 Tween。**美术只要往 `AnimationPlayer` 里加同名动画就能接管表现。**

**同步需要知道的其它变化**：`Interactable` 新增 `Vanished` 状态与 `Appear()`/`Disappear()`；`StageController` 的兜底落点从 `_playerRef`（原来永远是 null）改成了 `PlayerLocator.Player`。

**仍然待办**：19 个场景内联资源、7 处代码生成的视觉、9 敌+Boss 换模型、UI/音效接入、补拍子弹拖尾。

---

## 更新记录（2026-10 · 第五轮：UI 素材 + 主题 + 字体 + 准星 + glow）

> 本轮的**完整说明见 [ui.md](ui.md)**；逐项证据与截图见 [art-integration-log.md](art-integration-log.md) 的「第五轮」。

1. **UI 不再是"纯色 `ColorRect` + 默认字体"**：
   - 新增 `Assets/UI/`（`Textures/` + `Themes/` + `Fonts/` + `Licenses/`），素材来自 **Kenney UI Pack - Sci-Fi 2.0 / Crosshair Pack 1.1 / Fonts**（全部 **CC0**，包内许可原文已照抄进 `Assets/UI/Licenses/`）。（第五轮）
   - **主题** `Assets/UI/Themes/ui_theme.tres` 统管字体 / 字号 / 颜色 / 面板 / 按钮，挂在 **8 个界面场景的根 `Control`** 上；
     三个**类型变体**（`UiTitle` / `UiPrimaryButton` / `UiCaption`）让"标题带 / 主按钮 / 说明文字"一行搞定。
2. **中文字体（重要，发行前必做）**：`default_font` 是一个 **`SystemFont`**，按优先级从系统取
   「微软雅黑 UI → 微软雅黑 → Noto Sans SC → 思源黑体 …」。**实测中文 / `◆` / `◇` / 数字全部有字形**。
   ⚠️ 这是**开发期占位**：玩家机器上不一定有这些字体 → **发行前要换成一个自带的开源中文字体**
   （Noto Sans SC 17 MB / 得意黑 ~2.7 MB / 裁字后 1~2 MB，许可与步骤见 [ui.md](ui.md) §4）。
   英文/数字另配了 `font_ui_kenney_future.ttf`（Kenney Fonts, CC0），用在 HUD 的 `HP 120/120`。
3. **准星**：`Hud.tscn` 的 3 个 `ColorRect`（`CrosshairH`/`CrosshairV`/`CrosshairDot`，无代码引用）**已删除**，
   换成 `Root/Crosshair`（`TextureRect` + Kenney 描边准星 56×56）。
4. **玩家血条**：`BarPlayer.tscn` 的 `BarBg` 换成带端帽的九宫格边框贴图、`Fill`/`DelayBar` 从
   **`res://icon.svg`（Godot 图标平铺！）** 换成 Kenney 血条填充条 —— 配色仍由代码按血量写（绿→黄→红）。
   **`BarEnemy` / `BarBoss` 未动**（结构与 `BarPlayer` 一致，照抄即可）。
5. **竞技场开了 glow**（`TestArena.tscn` 的 `WorldEnvironment`，只动 `Environment` 这一块）：
   上一轮记录的"爆炸偏暗、以烟为主"就是因为这里没开 glow。**阈值取 2.0**（不是常见的 0.9~1.2），
   因为这张场地的地板本来就接近纯白，阈值低了会整块地板一起 bloom（实测过曝）。
   爆炸火球从"一块平黄"变成"白热核心 + 橙色外焰 + 光晕"，枪口火光带上暖色光晕，**空场亮度基本不变**。
6. **验证工具**：新增 `Scenes/UI/_UiShowcase.tscn` + `_ui_shot.gd`（一次运行拍完 7 个界面 + HUD + glow 前后），
   用法与两个坑（**别最小化窗口**、它拍的是真的 `TestArena.tscn` 环境）见 [ui.md](ui.md) §8。

---

## 更新记录（2026-10 · 第七轮：奖励节点换成宝石模型 + 待机/激活/失效状态动画）

> 需求包 §五 **#15**「给节点模型更换模型，素材路径 `…\素材\模型\LowPolyGemsPack2.zip`，这些应该是悬浮的，
> 且有待机/激活/失效的状态动画」。**交互判定 / 范围 / 消耗 / 复位逻辑一行未改。**

1. **只拷了 1 个模型进仓库**（整个包 405 KB，包里有 12 颗宝石 + 2 张贴图 + blend + Unity shadergraph）：
   - 源模型 `Assets/Models/nodes/gem_node_reward.fbx`（包内 `Meshes/gem1.fbx`）+ 它引用的
     `PalleteTex.png` / `ReflectionTex.png`（游戏里**不读**这两张：按类型上色的材质会整体覆盖表面材质）；
   - 用到的网格 `Assets/Models/nodes/mesh_node_gem.tres`（从 FBX **抽出来的 ArrayMesh**，轴心已居中在底部）；
   - 挑选依据：12 颗全部导入后并排渲染对比（体积小、一次拍完），`gem1` 是多面钻石、从远处最认得出。
2. **实测数据（不是猜的，见 §三.1.1）**：轴心 `min.y = 0.0000`（脚底）、尺寸 `1.2×1.0×1.2 m`、`scale = 1.35`、
   多面体无正反面所以**不需要转 180°**；⚠️ **FBX 的顶点绕序是反的**（有向体积 `V = -0.427193`），
   不修就会看到"内壁"（空心破面观感）→ 抽取时交换三角形索引修正（`V = +0.427193`）；法线本来就是朝外的（`+0.6759`）。
3. **悬浮**：`Float` 子节点上的动画，`idle` 0→0.18 m / 3 秒，`active` 0→0.28 m / 1.2 秒；网格基准高度 `Mesh.y = 0.55`，
   所以宝石是**离地悬浮**的（原来是 2.4 米高、底边贴地的圆柱）。
4. **三种状态动画**（`idle` / `active` / `dead`）**全部由已有的交互状态推导**，见 §三.1.2 的表；
   `Interactable.cs` 只加了一个 `RefreshStateAnimation()`（放表现）+ `VisualMesh` 改成"先直接子节点、再递归"。
5. **一个连带修好的静默失效**：把网格挂到 `Float` 下面会让 `GetNodeOrNull<MeshInstance3D>("Mesh")` 返回 null
   → 按类型上色 / 淡出 / 描边 / 抖动 / 图标高度**全部静默失效**。`VisualMesh` 现在会递归再找一次
   （名字仍是唯一契约，只是不再限定"必须是第一层子节点"）。**这是本次唯一动到既有取节点方式的改动，判定逻辑没动。**
6. **拍摄台**：`Scenes/Interactables/_NodeShowcase.tscn` + `_node_shot.gd`（三种状态并排 + 真实玩家触发 `active` +
   逐项读数 + 出图），抽取工具 `_ExtractGem.tscn` + `_extract_gem.gd`（换另一颗宝石时复用）。
   截图：`docs/screenshots/node_40_states.png` / `node_41_idle.png` / `node_42_active.png` / `node_43_dead.png` / `node_44_kinds.png`。

---

## 更新记录（2026-10 · 第八轮：9 种类型 = 9 颗**不同形状**的宝石 + 周身光圈颜色 = 价值程度）

> 用户上一轮看到实物后的纠正：「我之前让替换节点模型，虽然替换了，但是**我的期望是使用不同形状的模型
> 而不是一个模型去换色**，后面其实是考虑**加入一些周身特效来做信息展示的，比如光圈，光圈颜色来表示价值程度**」。
> **交互判定 / 范围 / 消耗 / 复位逻辑一行未改**（`Interactable.cs` 这一轮**没有改动**）。

1. **12 颗宝石全部入库**（`Assets/Models/nodes/gem1.fbx` … `gem12.fbx`，包内原名，约 230 KB；
   原来那颗 `gem_node_reward.fbx` 就是 `gem1.fbx` —— 实测 sha256 完全相同，已删掉避免两份；
   zip / blend / Unity shadergraph **不入库**；两张 png 在库里但**游戏不读**，见 §三.1.1.1）。
2. **9 种类型各一颗不同形状的宝石**，每颗抽成独立的 `Assets/Models/nodes/mesh_node_<kind>.tres`
   （形状 + 材质 + 挑选依据**全表见 §三.1.1.1**）：`Weapon`=细长六棱晶 / `Enhancement`=厚实盾形 /
   `Evolution`=**经典多面钻石** / `Mutation`=四角星 / `Resource`=六边形晶块 / `Heal`=水滴 /
   `Special`=倒五边形 / `Shop`=八边形圆块 / `Portal`=正方形板块。
   **备用 3 颗**（`gem2` / `gem3` / `gem12`）与它们的用途也写在那张表下面。
   `RewardNode.BuildShapes()` 按**枚举名自动映射**文件名（与 `BuildPalette()` 同一条规则），加新类型不用改代码。
3. **逐颗实测：12 颗的绕序**全是反的**（`V` 全为负）、**法线本来就都对**（外向度全为正，
   薄片的 `gem2` 只有 +0.0064 属正常）；轴心全部在脚底（`min.y = 0`，`gem8` 差 1 cm）；
   **高度全是 1.00 m**（宽 0.50~1.20 / 厚 0.20~1.20）→ 等比 `scale = 1.35` 即可，不用逐个调。见 §三.1.1.1。
4. **周身光圈**（`RewardNode.tscn` 新增 `Aura/Ring|Rim|Glow`）：贴地平放的光环，
   **颜色从价值标签配置读**（`Config/Tables/ValueTags/tag_*.tres` 新增 `AuraColor` 列）：
   高=金 / 中=青蓝 / 低=绿；`Ring` 用 `Unshaded` + HDR 亮度（亮地板与暗天空下一致、且吃 glow），
   `Rim` 是固定暗描边（准星那套"亮色 + 黑描边"），`Glow` 是同色半透明外发光。设计细节见 §三.1.3。
5. **光圈与三状态动画并存**：`dead`（`!Enabled` / `Used` / `Vanished`）把光圈缩到 0.55 并把三个环的
   `transparency` 拉到 0.82~0.95 —— **失效节点不再发光**；`idle` 呼吸、`active` 胀得更快更大。
   ⚠️ 动画**只动每实例的 `transparency` 与 `Aura:scale`**，绝不写材质（材质是按档位共享的）。
6. **一个自测抓到的真 bug**（已修，见 §三.1.3 末）：光圈颜色要读 `ConfigIndex` 的表索引，而那是
   `CallDeferred` 建的 → 节点的 `_Ready` 早于它 → 颜色被"读不到"的结果缓存住，**整局光圈都是中性白**
   （画面上看不出异常，只有日志里那行 warning 能证明）。修法：`CallDeferred` 再跑一次 + **只缓存成功的读取**。
7. **拍摄台扩了 4 张图**：`node_44_kinds9.png`（9 种形状 + 三档光圈总览）/ `node_45_aura_tiers.png`
   （三档光圈在**真 TestArena**、玩家视角下）/ `node_46_aura_dead.png`（失效 vs 可用并排）/
   `node_47_kinds9_far.png`（26 米外：形状认不清时光圈颜色还认得出）；抽取工具顺带出
   `gem_00_sheet.png`（正交正视剪影）/ `gem_01_sheet_tilt.png`（3/4 视角看厚度）—— **挑形状就是照这两张挑的**。
   工具用法见 §四。
