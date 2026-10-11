# 美术资源接入说明

> 给"要给交互物 / 特效 / 粒子 / UI 上模型或做替换"的人看。
> 规范细节见 [assets.md](assets.md)，逐元素的当前实现见 [visual-inventory.md](visual-inventory.md)。

---

## 一、30 秒现状

- **项目目前零美术资源**：没有贴图、没有字体、没有 3D 模型、没有 Theme、没有粒子、没有动画（`AnimationPlayer`/`Skeleton3D` 全项目 0 命中）。所有可见物都是**程序化基础网格**（`BoxMesh` / `CapsuleMesh` / `CylinderMesh` / `SphereMesh`）+ 纯色材质。
- **规范已立**：资源目录 `Assets/`，命名 `<类别>_<分组>_<变体>`。
- **已有样板**：交互物（奖励节点、Boss 门）与特效（子弹、爆炸）这 4 个已经**从场景内联改成独立资源文件**，照它们的做法抄就行。
- **转换进度**：4 个场景已外置，**还剩 19 个**内联（9 个敌人 + Base / TestArena / Player / Drone / Training×3 / Pistol）。

## 二、东西放哪、叫什么

```
Assets/
  Models/       mesh_*.tres   （网格：几何形状与尺寸）
  Materials/    mat_*.tres    （材质：颜色、发光、透明）
  Textures/     tex_*.png     （以后加）
  Effects/      fx_*.tscn     （以后加：特效场景）
  Particles/    ps_*.tscn     （以后加：粒子场景）
  UI/           ui_*.png      （以后加：界面素材）
  Audio/        sfx_*.ogg     （以后加）
```

**命名**：`<类别>_<分组>_<变体>`，全小写。
类别 `mat_` / `mesh_` / `tex_` / `fx_` / `ps_` / `ui_` / `sfx_`；分组 `node`（交互物）/ `combat`（战斗）/ `enemy` / `player` / `weapon` / `level` / `ui`。

例：`mesh_node_reward.tres`（奖励节点的网格）、`mat_enemy_chaser.tres`（追猎者的材质）、`fx_levelup.tscn`（升级特效）。

> **为什么这样命名**：前缀让同类在文件列表里自动聚拢（搜 `mat_enemy_` 一次看齐）；文件名回答"**给谁用**"，而不是重复文件里已经有的类型信息；全小写避免 Windows/Linux 大小写差异退化成两个文件。

**关键约定：材质挂在网格上，不挂在场景上。**
场景里只引用网格（`mesh = ExtResource(...)`），网格的 `.tres` 再去引用材质。这样**美术改颜色不用碰场景**，换模型只改场景里的一行。

## 三、动手路径（按类别）

### 1. 换某个交互物的模型（奖励节点 / Boss 门）

1. 改 `Assets/Models/mesh_node_reward.tres` 里的几何参数 —— **只改这一个文件，10 个节点同时生效**
2. 或者新建一个网格资源（如 `mesh_node_reward_v2.tres`），把 `Scenes/Interactables/RewardNode.tscn` 里的 `mesh = ExtResource(...)` 指向它
3. **⚠️ 节点名 `Mesh` 不能改** —— 代码按名字取它（`Interactable.cs` 的"用尽变暗"逻辑）

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

`Scenes/UI/` 下每个界面。**⚠️ 换之前先看这几条**：

| 坑 | 说明 |
|---|---|
| 一批控件是**按名字取的** | 改名必须先改代码（见下面第五节） |
| 血条的**填充条 / 残影条 / 文字**按名字取 | 见下面的预制体说明；改 `Fill` / `DelayBar` / `Label` 这三个名字要同步 `PlayerHud.cs` / `WorldOverlay.cs` 里的常量 |
| 血条配色仍由**代码**按血量写（绿 → 黄 → 红） | 美术在预制体里设的颜色会被覆盖；要换配色改 `HealthColor()`（`PlayerHud.cs`、`WorldOverlay.cs` 各一份） |

**三条血条已经是预制体**（`Scenes/UI/Bars/`），改外观不用碰 C#：

| 预制体 | 用在哪 | 谁实例化 |
|---|---|---|
| `BarEnemy.tscn` | 敌人脚下血条（每个受伤的怪一份） | `WorldOverlay.cs`（尺寸取 `Balance.tres`） |
| `BarBoss.tscn` | 界面顶部首领主血条 | `PlayerHud.cs`（挂到 HUD 层，节点名 `BossPanel`） |
| `BarPlayer.tscn` | 左上角玩家血条 | `Hud.tscn`（实例节点名 `PlayerBar`） |

- 三者结构一致：`Fill`（填充）、`DelayBar`（延迟条 / 受伤残影，受伤后缓慢跟随，可改颜色）、`Animation`（空 `AnimationPlayer` 占位）、可选 `Bg`（底色 / 边框）与 `Label`（文字占位）。
- **控件类型随意换**：代码只按 `Control` 用它们（`size` / `modulate`），`ColorRect` / `TextureRect` / `NinePatchRect` 都行，`Fill` 已经是 `TextureRect`（贴图条）。
- **尺寸**：`BarPlayer` 的宽高**由场景说了算**（代码只改填充宽度，不再每帧覆盖）；`BarEnemy` 的尺寸由 `Balance.tres` 的 `EnemyHealthBarWidth/Height` 决定，预制体按同比例整体缩放。
- ⚠️ `BarEnemy` 里可缩放的整条内容都放在 `Content` 下、且**以 Root 原点为中心**（现在是 `Content` 摆在 `(-1,-1)`、尺寸 +2）—— 代码把 `Root.Position` 当血条中心，改布局要保持这个中心约定（偏了会打警告）。
- 首领血条的 `Bar` 是 `ProgressBar`（不是 `Control` 填充条），填充色在预制体的 `theme_override_styles/fill` 里。


### 5. 换颜色（最快的收益）

如果只想让玩家**区分池子类型**（现在 10 个奖励节点全是一种浅蓝，玩家分不清强化/进化/异化/商店），最小改动是在 `RewardNode` 里按 `Kind` 用 **Surface Material Override** 上色 —— 注意这一局 10 个节点**共享同一个 mesh/material**，直接改共享材质会把 10 个一起改掉。

## 四、改完必须验证

```powershell
# 1) 编译（必须 0 warning / 0 error）
& 'F:\Godot项目\项目a\build.ps1' -v minimal

# 2) 跑游戏内自测（会走完：进关 → 交互 → 奖励 → 战斗 → 结算）
'1' | Set-Content 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'
$cli = 'F:\Godot项目\项目a\.gdmcp\bin\gdmcp.exe'
& $cli tool-call run_project --args-json '{"allow_window":true,"scene_path":"res://Scenes/Main.tscn"}' --apply
Start-Sleep -Seconds 20
Remove-Item 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'   # 必须删！

# 3) 看日志有没有加载错误
$dir = 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\logs'
$f = Get-ChildItem "$dir\*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Get-Content $f.FullName | Select-String -Pattern 'ERROR|Exception|清场完成|端到端'
```

**⚠️ 编辑器会缓存场景**：用编辑器脚本 `load()` 验证时拿到的是**改动前的副本**，必须用 `ResourceLoader.CACHE_MODE_IGNORE` 强制读盘，否则会看到假失败。

## 五、改名前必须先改代码的节点

这些节点名被 C# 按字符串取用，**改名会静默失效**（不报错，功能没了）：

| 节点名 | 谁在用 | 影响的场景 |
|---|---|---|
| `Mesh` | `Interactable`（"用尽变暗"） | 奖励节点、Boss 门、基地 4 个交互台 |
| `Muzzle` | `Weapon`（枪口位置） | 各武器 |
| `Hud/Root/PlayerBar`、`BarPlayer/Fill`、`BarPlayer/DelayBar` | `PlayerHud`（`_Ready` 按名字抓） | HUD |
| `Hud/BossPanel`、`Panel/{Icon,NameLabel,Bar,StateLabel}` | `PlayerHud`（`BuildBossPanel`，`BossPanel` 是代码挂上去时的名字） | `BarBoss.tscn` |
| `Bars/*/Content/Fill|DelayBar|Label` | `WorldOverlay`（每个敌人血条实例） | `BarEnemy.tscn` |
| `Model` / `Body` | **无引用，可自由改名** | 所有单位 |

完整清单（15 组共 26 个节点名）见 [visual-inventory.md](visual-inventory.md) 第 8 节。

## 六、红线（这些不是美术资源，别改）

1. `Explosion.tscn` 的 **`SphereShape3D`** 是伤害判定基准
2. `Interactable.OnUsed()` 要求材质是 **`StandardMaterial3D`** —— 换成 `ShaderMaterial` 会让"已用变暗"**静默失效**
3. 交互物的**交互半径、提示文字**是玩法参数，不是表现
4. `Balance.tres` 里的 `EnemyHealthBar*` 等是**数值配置**，不要当美术参数调

## 七、还没做的（可以接着推）

| 项 | 说明 |
|---|---|
| 19 个场景仍是内联网格/材质 | 9 个敌人 + Base / TestArena / Player / Drone / Training×3 / Pistol，模式与外置的 4 个一样 |
| 视觉由**代码生成**的还剩 5 处 | `ChallengerMark` 的三角环（`ArrayMesh`）、受击闪色、精英染色、伤害跳字、"用尽变暗" —— 换这些要改 C#。**三条血条已是预制体**（见第三节第 4 条） |
| 血条配色仍是代码写死 | 绿→黄→红在 `HealthColor()` 里；要按美术主题色走需要改成读预制体颜色 |
| 血条的动画还没做 | 预制体上有空的 `AnimationPlayer` 与"数值更新 / 表现更新"两个方法（`SetRatio` / `AdvanceDelay`），插入动画的接口已留好 |
| 奖励节点按类型上色 | 现存可玩性问题，见第三节第 5 条 |
| 粒子 / 动画 / 骨骼 | 全空白，从零加 |

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