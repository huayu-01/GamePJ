# 美术资源规范（`Assets/`）

> 给"要把模型 / 材质 / 特效换成美术给的资源"的人看（程序或美术都适用）。
> 项目：`F:\Godot项目\项目a`（Godot 4.7.2 stable mono / C#）。**本文是资源目录与命名的唯一约定。**
> 相关：[architecture.md](architecture.md)（代码分层）、[config.md](config.md)（数值放 `Config/ConfigIndex.tres`）。

---

## 0. 为什么要立这个规范

在此之前，项目里**没有资源目录**：所有网格与材质都写成场景文件里的 `sub_resource`，例如
`Scenes/Combat/Bullet.tscn` 里的 `[sub_resource type="BoxMesh" id="BoxMesh_bullet"]`。
后果是换一个颜色要找场景、要认 `id="BoxMesh_bullet"` 这种自动生成的名字，还容易漏改 `load_steps`。

现在 `Assets/` 是新的**资源根目录**，网格与材质各自是一个独立文件，场景只保留一行引用。
第一批（4 个场景）已经改完，它们是**样板**，照着抄就行。

| 状态 | 场景 |
|---|---|
| ✅ 已外置（2026-10） | `Scenes/Interactables/RewardNode.tscn`、`Scenes/Interactables/BossDoor.tscn`、`Scenes/Combat/Bullet.tscn`、`Scenes/Combat/Explosion.tscn` |
| ⏳ 仍内联 | `Scenes/Enemies/*`（9 个）、`Scenes/Levels/Base.tscn`、`Scenes/Levels/TestArena.tscn`、`Scenes/Player/Player.tscn`、`Scenes/Summons/Drone.tscn`、`Scenes/Training/*`、`Scenes/Weapons/Pistol.tscn` |

> 场景文件**位置不动**：`Scenes/` 是"游戏从哪儿加载"，`Assets/` 是"美术改哪儿"，两者不要混。
> 移动场景文件会牵连一堆 `ExtResource` 路径，收益是零。

---

## 1. 目录约定

按**资源类型**分目录（不按"哪个关卡用到"分）：

```
Assets/
  Materials/   材质（.tres）              ← 已有
  Models/      网格（.tres）与导入模型（.glb/.fbx） ← 已有
  Textures/    贴图（.png/.svg）
  Effects/     独立特效场景（.tscn：爆炸、命中火花、升级光柱）
  Particles/   粒子资源（GPUParticles3D 的 .tres）
  UI/          界面用的图标 / 主题 / 字体
  Audio/       音效与音乐
```

**规则：用到才建目录，不留空目录。** 所以现在只有 `Materials/` 和 `Models/` 两个目录存在——
`Textures/` 这些等真有资源时再建，避免一堆空目录让人以为里面丢过东西。

分类的判断标准只有一条：**"这东西被换掉时，是谁来换"**——材质/贴图是美术，`Effects/` 里的特效场景是特效。
类型分目录的另一个好处：`Assets/` 整个可以按目录粒度做导入设置（例如 `Textures/` 统一 sRGB）。

---

## 2. 命名规范

```
<类别>_<分组>_<变体>.tres
```

全小写 `snake_case`，三段用 `_` 连接。

### 2.1 类别前缀（表"是什么"）

| 前缀 | 含义 | 例 |
|---|---|---|
| `mat_` | 材质 | `mat_node_reward.tres` |
| `mesh_` | 网格（Godot 内建 PrimitiveMesh 存成 .tres） | `mesh_combat_bullet.tres` |
| `tex_` | 贴图 | `tex_ui_icon_dash.png` |
| `fx_` | 独立特效场景 | `fx_levelup.tscn` |
| `ps_` | 粒子资源 | `ps_hit_spark.tres` |
| `ui_` | 界面主题 / 样式 | `ui_theme_base.tres` |
| `sfx_` | 音效 | `sfx_hit_flesh.ogg` |

### 2.2 分组词表（表"给哪一族东西用"）

| 分组 | 用在哪 |
|---|---|
| `node` | 场景里可交互的物件（`RewardNode`、`BossDoor`、以后的宝箱/传送门） |
| `combat` | 战斗表现（子弹、爆炸、命中特效） |
| `enemy` | 敌人本体（`mat_enemy_chaser.tres`） |
| `player` | 玩家本体与随身物件 |
| `weapon` | 武器 |
| `level` | 关卡 / 基地的静态物件（地面、平台、终端台） |
| `ui` | 界面 |

### 2.3 变体（表"具体是哪一个"）

用它在游戏里本来的名字，`snake_case`：`reward` / `boss_door` / `bullet` / `explosion` / `chaser`。
同类有多个时加特征后缀或序号：`mat_enemy_chaser_elite.tres`、`mesh_level_platform_small.tres`。

### 2.4 已存在的文件对照

| 文件 | 用在哪 | 内容 |
|---|---|---|
| `Assets/Materials/mat_node_reward.tres` | `RewardNode.tscn` 的圆柱 | 青色 (0.35, 0.8, 1)，自发光 (0.2, 0.6, 0.9) ×0.8 |
| `Assets/Models/mesh_node_reward.tres` | 同上 | `CylinderMesh` 上 0.35 / 下 0.55 / 高 2.4 |
| `Assets/Materials/mat_node_boss_door.tres` | `BossDoor.tscn` | 品红 (0.75, 0.2, 0.35)，自发光 (0.5, 0.1, 0.2) ×0.6 |
| `Assets/Models/mesh_node_boss_door.tres` | 同上 | `BoxMesh` 4.4 × 5 × 0.6 |
| `Assets/Materials/mat_combat_bullet.tres` | `Bullet.tscn` | 金黄 (1, 0.82, 0.3)，自发光 (1, 0.65, 0.15) ×4 |
| `Assets/Models/mesh_combat_bullet.tres` | 同上 | `BoxMesh` 0.07 × 0.07 × 0.45 |
| `Assets/Materials/mat_combat_explosion.tres` | `Explosion.tscn` | 橙色 50% 透明 (1, 0.6, 0.2, 0.5)，`transparency=1`、`shading_mode=0`（无光照） |
| `Assets/Models/mesh_combat_explosion.tres` | 同上 | `SphereMesh` 半径 1 / 高 2 |

### 2.5 为什么这么定（理由，供以后争论时翻）

1. **前缀让同类在文件列表里自动聚在一起**。资源管理器按名字排序时，所有材质连着、所有网格连着，
   而不是 `RewardNodeMaterial` / `BossDoorMat` / `bullet_material` 三种风格混排。
2. **信息不重复**。文件里已经有 `[gd_resource type="StandardMaterial3D"]` 说明"是什么类型"，
   文件名再说一遍 `StandardMaterial3D_node`（Godot 自动生成的 id 就是这样）等于没写。文件名要回答的是
   "给谁用"。
3. **全小写 + `_` 分隔是跨平台安全的**。Windows 大小写不敏感、Linux 敏感，`BossDoor.tres` 和
   `bossdoor.tres` 在两边会变成两个文件；导出/打包/版本管理都可能因此出错。全小写从根上避免。
4. **`类别_分组_变体` 的顺序让前缀检索有用**：搜 `node_` 一次拿到所有交互物资源，
   搜 `mat_enemy_` 一次拿到所有敌人材质。先写变体（`reward_mat`）就做不到了。

---

## 3. 依赖方向：材质挂在网格上，不挂在场景上

```
Scene.tscn  ──ExtResource──▶  Assets/Models/mesh_xxx.tres  ──ExtResource──▶  Assets/Materials/mat_xxx.tres
   (只引用网格)                      (网格自带材质)
```

读一下 `Scenes/Interactables/RewardNode.tscn` 就懂了（全文，14 行）：

```
[gd_scene load_steps=3 format=3]

[ext_resource type="Script" path="res://Scripts/Interaction/RewardNode.cs" id="1_node"]
[ext_resource type="Mesh" path="res://Assets/Models/mesh_node_reward.tres" id="2_mesh"]

[node name="RewardNode" type="Node3D"]
script = ExtResource("1_node")
Prompt = "强化池"
InteractRadius = 3.2
Kind = 1

[node name="Mesh" type="MeshInstance3D" parent="."]
transform = Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 1.2, 0)
mesh = ExtResource("2_mesh")
```

好处很实在：**换颜色不用打开场景**（改 `mat_node_reward.tres`），**换模型只改场景里那一行 `mesh =`**。
场景里没有材质引用是有意的——如果材质同时也挂在场景上，就会出现"到底哪个生效"的困惑。

例外（要用的时候才用）：只想改**某一个实例**（比如"被用掉的那个节点变灰"），
才用 `MeshInstance3D` 的 **Surface Material Override**（场景里写成 `surface_material_override/0 = ...`）。
`Scripts/Interaction/Interactable.cs` 就是这么干"已用变暗"的。

---

## 4. 替换步骤（照着做）

> 改完**每一步都要跑 §6 的验证**，不要攒着一起跑。

### A. 只换颜色 / 发光（最常见，不碰场景）

1. 打开对应的 `Assets/Materials/mat_*.tres`（或在资源管理器里双击）。
2. 检查器里改 `Albedo > Color`、`Emission > Enabled / Color / Energy`。
3. 保存。**不用改任何场景，不用重新编译。**

改之前先看清楚这个材质被谁用（`Assets/Models/mesh_*.tres` 里的 `material = ExtResource(...)`）——
同一个材质被多个网格引用时，改一处会**同时影响所有引用者**，这是设计意图，但要知道。
（当前 4 组材质各自只被 1 个网格引用，见 §2.4 表。）

### B. 换形状 / 尺寸（仍然是 Godot 内建网格）

1. 打开 `Assets/Models/mesh_*.tres`。
2. 改 `size`（BoxMesh）/ `top_radius`、`bottom_radius`、`height`（CylinderMesh）/ `radius`、`height`（SphereMesh）。
3. 保存。**不用改场景。**

注意 `CylinderMesh` 的 `height` 是**总高**，节点仍在 `y = height/2` 的位置（`RewardNode` 的圆柱高 2.4，
节点 transform 是 `y = 1.2`）——改高度时要么接受它一半埋进地里，要么同时改场景里的 `transform`（那一步会动场景）。

### C. 换成美术给的 3D 模型（`.glb` / `.fbx`）

这一步**必须动场景**（一个 `.tres` 没法表达"另一个文件里的网格"），但只动一行：

1. 模型放到 `Assets/Models/`，命名按 §2（例如 `mesh_node_reward.glb`；Godot 会自动生成 `.import`）。
   导入设置：`Meshes > Ensure Tangents`、按需勾 `Materials > Extract`。
2. **先决定材质走哪条路**：
   - 用模型自带的材质 → 什么都不用做（材质藏在 `.glb` 里，美术在 DCC 里定）。
   - 用项目里的 `mat_*.tres` → 在场景的 `MeshInstance3D` 上填 **Surface Material Override**，
     或在导入设置的 `Materials > Path` 指向共用的 `.tres`。
3. 打开场景，把 `MeshInstance3D.mesh` 指向 `.glb` 里的网格（拖进去，场景里会写成
   `mesh = ExtResource("...")`，路径形如 `res://Assets/Models/mesh_node_reward.glb::Cylinder`）。
4. 只改 `mesh = ` 一行 + 需要时加一条 `[ext_resource type="Mesh" ...]`；`load_steps` 同步 +1（见 §5）。
5. 用 §6.3 的资源级校验确认几何/材质没歪。

### D. 给新东西建资源（新交互物 / 新特效）

1. 先想清楚分组词（§2.2）和变体名，再决定文件名——文件名改起来很烦，因为场景里引用的是路径。
2. 材质先建 `Assets/Materials/mat_<分组>_<变体>.tres`，再建 `Assets/Models/mesh_<分组>_<变体>.tres`
   并在 `material =` 里引用它（照抄 §3 的样板）。
3. 在场景里引用网格：`mesh = ExtResource("N_mesh")`，`id` 用 `序号_用途`（`2_mesh`）这种本项目通用写法。
4. 独立特效（爆炸、光柱）建成 `Assets/Effects/fx_<用途>.tscn`，**由代码 `GD.Load<PackedScene>` 加载**
   （现有例子：`Scripts/Combat/Projectile.cs` 载入 `res://Scenes/Combat/Explosion.tscn`）。
   注意现有战斗预制体仍留在 `Scenes/Combat/`，新特效再往 `Assets/Effects/` 放。

---

## 5. 手写 `.tscn` / `.tres` 的格式规矩

这些文件是纯文本，可以手写，但规矩不能破：

1. `[ext_resource]` 和 `[sub_resource]` **必须写在所有 `[node ...]` 之前**，写反了加载直接失败。
2. `load_steps = ext_resource 数 + sub_resource 数 + 1`。写错不会崩（只影响加载进度条），但要跟着改。
   只有 1 个时 Godot 自己保存时会省略，本项目统一显式写出来。
   历史遗留：`Explosion.tscn` 原来是 `load_steps=4` 而实际该是 5，已顺手修正。
3. `[gd_resource type="..."]` 里的 `type` 用**属性的基类型**，不要用具体类：
   `type="Mesh"`（而不是 `BoxMesh`）、`type="Material"`（而不是 `StandardMaterial3D`）。
   这样以后把网格换成导入模型（`ArrayMesh`）、把材质换成 `ShaderMaterial` 时，场景不用跟着改。
   （Godot 自己也是这么写的：`icon.svg` 在场景里就是 `type="Texture2D"`。）
4. 项目里所有 `ext_resource` **只写 `path=`，不写 `uid=`**——保持一致，别手搓 `uid://`。
5. 文件编码 UTF-8 **无 BOM**、换行 **LF**。改文件用 `edit` 工具或整文件重写，
   **不要**用 PowerShell 做多行文本替换（`\r\n` / `\n` 不一致会静默失败，本项目栽过 7 次以上）。

---

## 6. 验证（改完必跑）

### 6.1 编译

```powershell
& 'F:\Godot项目\项目a\build.ps1' -v minimal   # 必须 0 warning / 0 error
```

> 只改 `.tscn` / `.tres` / `.md` 不会影响 C# 编译，但这是"我现在改的东西没把项目弄坏"的最快信号，照样跑。

### 6.2 游戏内自测（证明场景真的能加载、玩法没变）

```powershell
'1' | Set-Content 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'
$cli = 'F:\Godot项目\项目a\.gdmcp\bin\gdmcp.exe'
& $cli tool-call run_project --args-json '{"allow_window":true,"scene_path":"res://Scenes/Main.tscn"}' --apply
Start-Sleep -Seconds 22
Remove-Item 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'   # 必须删！
& $cli tool-call stop_project --args-json '{"allow_window":true}' --apply
$dir = 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\logs'
$f = Get-ChildItem "$dir\*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Get-Content $f.FullName | Where-Object { $_ -match '波开始|清场完成|端到端' } | Select-Object -Last 8
```

看到这几行才算过：`第 N 波开始`、`Encounter 清场完成`、`端到端：按 F 之后 奖励界面打开 = True`、`=== 自测结束 ===`。
`selftest.flag` **必须删掉**，否则玩家自己玩时会被自测接管。

### 6.3 资源级校验（逐项比对属性，最硬的证据）

`.tscn` 能加载 ≠ 数值没变。在编辑器里跑一段只读 GDScript，把实例化出来的网格/材质与**原始数值**逐项比对：

```gdscript
var ps = ResourceLoader.load("res://Scenes/Interactables/RewardNode.tscn", "", ResourceLoader.CACHE_MODE_IGNORE)
var m = ps.instantiate().get_node("Mesh").mesh
print(m.resource_path, " ", m.top_radius, " ", m.bottom_radius, " ", m.height,
      " ", m.material.albedo_color, " ", m.material.emission_energy_multiplier)
```

**坑：必须带 `ResourceLoader.CACHE_MODE_IGNORE`。** 编辑器进程会缓存已加载的场景，
直接 `load()` 很可能拿到**改动前的旧副本**，于是你会看到 `mesh.resource_path = ...tscn::BoxMesh_bullet`
这种"改了没生效"的假象（实测踩到过）。游戏进程是独立进程、从磁盘重新加载，不受这个影响。

跑法：

```powershell
$code = @'
    ...上面的 GDScript...
'@
$a = @{ code = $code } | ConvertTo-Json -Compress
& $cli tool-call execute_editor_script --args-json $a --apply --allow-open-world
```

2026-10 那次外置的实测结果：4 个场景 / **44 项属性检查 0 失败**（几何、颜色、自发光、发光强度、
透明度、着色模式、节点位置、`sub_resource` 残留数全部逐项比对）。

### 6.4 视觉确认

```powershell
& $cli runtime screenshot --format png --save-path 'user://assets_check.png'
```

图片落在 `C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\`，用看图工具打开。
`Scenes/Levels/TestArena.tscn` 里跑一局，**青色发光圆锥 = 强化池节点**、**品红色横条 = Boss 之门**，
颜色/尺寸一眼可比。

---

## 7. 红线（这些不是美术资源，别顺手改）

| 东西 | 在哪 | 为什么不能动 |
|---|---|---|
| `SphereShape3D radius = 1.0` | `Scenes/Combat/Explosion.tscn` | **玩法参数**：`Explosion.Detonate()` 用 `Scale = radius` 缩放它，半径 1 是"每单位缩放"的基准。改了 = 改了爆炸范围 |
| `CollisionShape3D` / `BoxShape3D` | 各敌人场景 | 命中判定，同理 |
| `InteractRadius` / `Prompt` / `Kind` | `RewardNode.tscn` 等 | 交互距离与文案，是脚本导出属性 |
| `Speed` / `MaxLifetime` | `Bullet.tscn` | 玩法数值不是外观：`Speed` 是弹速（`Projectile.Launch()` 直接用），`MaxLifetime` 会被开火武器上的 `MaxLifetime` 覆盖（`Weapon.cs` → `AttackInstance.Lifetime`）。换美术时别顺手改 |

**材质必须是 `StandardMaterial3D`**：`Scripts/Interaction/Interactable.cs` 的"已用变暗"是
`GetActiveMaterial(0) is not StandardMaterial3D → return`，也就是**换成 `ShaderMaterial` 会静默失效**
（不报错，只是用过的节点不再变灰）。要换成 shader 就得同步改那段 C#。

**共享是有意的**：`RewardNode.tscn` 一局会实例化 10 份，它们共用同一个 mesh / material 对象。
所以"改一个节点的颜色"必须走 Surface Material Override（`Interactable.OnUsed()` 就是
`Duplicate()` 一份再改），改共享文件会让 10 个节点一起变。

---

## 8. 待办

1. 把 19 个仍内联的场景按同样模式外置，优先级建议：**敌人 9 个**（`Scenes/Enemies/*`，一眼就要换模型）
   → `Player/Player.tscn`、`Weapons/Pistol.tscn` → `Levels/*`（地面/终端台）→ `Training/*`。
2. 敌人 9 个场景的写法完全一致（各 1 个 `StandardMaterial3D` + 1 个 `BoxMesh`/`CapsuleMesh`），
   可以一次性抽出 `mat_enemy_*` / `mesh_enemy_*`，顺手统一配色。
3. 真出现贴图 / 音效 / 粒子时，按 §1 建目录（`Textures/`、`Audio/`、`Particles/`）。
### 关于 `uid=`（2026-10-11 更新）

**约定修正**：早期我们规定"手写 `.tres`/`.tscn` 时 `ext_resource` 只写 `path=`、不写 `uid=`"。这条**仍然适用于手写新文件**，但要补充一点：

**Godot 4.7 的编辑器在保存/重导入时会自动补上 `uid=`**（同时会去掉 `load_steps`、重排属性顺序）。这是引擎行为，无法阻止，也不需要阻止 —— 实测这些自动改动**不影响加载与运行**（自测逐项比对通过）。

所以：

- **手写**：只写 `path=` 即可，简单可靠
- **编辑器重存后**：出现 `uid=` 是正常的，**直接提交**，不要反复 `git checkout` 还原（否则每次打开编辑器都会重新变脏）
- ⚠️ 唯一要注意的：**YARD 那类"以 UID 为核心"的插件**与这套轻量约定不兼容（这也是 YARD 被移除的原因之一）