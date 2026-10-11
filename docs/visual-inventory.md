# 视觉元素清单（美术替换用）

> **⚠️ 更新（第二轮 · 2026-10）**：**§3 单位**（9 敌 + Boss 换 Kenney 模型）、**§5 特效**（爆炸接 Binbun、命中火花就位）、
> **§8.5 精英染色**三条已按实际情况改写。**§1「项目视觉现状」第 1/3 条已过期**
> （天空盒 / 玩家模型 / 竞技场 / 枪口火光 / 子弹拖尾 / 敌人模型 / 爆炸 / 命中火花都已接入）——
> 以 [art-integration-log.md](art-integration-log.md)（尤其 §9）为准，本文档其余章节是**第一轮扫描时的快照**。

> **用途**：美术要换掉某个东西时，一眼看出「它在哪、现在长什么样、换起来有多难、会不会踩代码的坑」。
> **来源**：只读扫描 `Scenes/**/*.tscn`（**34 个场景**）+ `Scripts/**/*.cs`（**85 个脚本**）+ `Config/Balance.tres`。
> **本次没有修改任何 `.cs` / `.tscn`**；本文档是唯一产物。
> **生成时间点**：扫描时 `Scenes/Interactables/`、`Scenes/Combat/` 正由**另一个代理**做「内联资源外置到 `Assets/`」，
> 因此这两个目录的行内容可能已经被改动（本文档记录的是我读到的那一版）。`docs/assets.md` 在我扫描时**尚不存在**。

## 覆盖统计

| 类别 | 行数（＝视觉元素数） | 容易 | 中等 | 较难 |
|---|---|---|---|---|
| 交互物（§2） | 6 | 0 | 6 | 0 |
| 单位（§3） | 18 | 18 | 0 | 0 |
| 武器（§4） | 4 | 4 | 0 | 0 |
| 特效 / 粒子 / 代码生成视觉（§5） | 6 | 0 | 2 | 4 |
| UI（§6） | 44 | 36 | 5 | 3 |
| 场景环境（§7） | 10 | 3 | 7 | 0 |
| **合计** | **88** | **61** | **20** | **7** |

- **代码生成 / 代码改材质（较难，必须改 C#）**：7 处（特效 4 + UI 3，见 §5、§6）
- **按名字被代码引用的节点**：15 组共 **26 个视觉节点名** + 7 组纯逻辑节点名（见 §8.3）——**改名 = 先改代码**
- 表里的「行」都是**一个一个视觉元素**（三个木桩名牌算 3 个、五条基地按钮算 5 个）；§5 末尾两行标 `—` 的是**缺口**（目前不存在），不计入 88

---

## 0. 30 秒速查表

| 我想换… | 去哪 | 难度 |
|---|---|---|
| 奖励节点（强化池/进化池…） | `Scenes/Interactables/RewardNode.tscn` → 节点 `Mesh` | 中等 |
| Boss 门 | `Scenes/Interactables/BossDoor.tscn` → 节点 `Mesh` | 中等 |
| 基地 4 个交互台 | `Scenes/Levels/Base.tscn` → `TerminalUpgrades/Loadout/Help/Gate` → 各自 `Mesh` | 中等 |
| 玩家模型 | `Scenes/Player/Player.tscn` → 节点 `Model` | 容易 |
| 任意敌人模型 | `Scenes/Enemies/<名字>.tscn` → 节点 `Model` | 容易 |
| 召唤物 Drone | `Scenes/Summons/Drone.tscn` → 节点 `Model` | 容易 |
| 枪（第一/第三人称手里的） | `Scenes/Weapons/Pistol.tscn` → 节点 `Body` | 容易 |
| 子弹 | `Scenes/Combat/Bullet.tscn` → 节点 `Mesh` | 中等 |
| 爆炸 | `Scenes/Combat/Explosion.tscn` → 节点 `Mesh` | 中等 |
| 脚下三角环（挑战者标记） | **没有场景**，`Scripts/Combat/ChallengerMark.cs:36-77` 代码生成 | 较难 |
| 受击闪色 | `Scripts/Combat/UnitPresentation.cs:12-15`（`FlashColor`） | 较难 |
| 精英怪染色/放大 | `Scripts/Combat/EliteModifier.cs:18-19`（`ModelScale` / `Tint`） | 较难 |
| 敌人血条 / 伤害跳字 | `Scripts/UI/WorldOverlay.cs`（血条外观在 `Scenes/UI/Bars/BarEnemy.tscn`，尺寸在 `Config/Balance.tres:119-122`） | 容易（跳字较难） |
| 玩家血条（左上） | 预制体 `Scenes/UI/Bars/BarPlayer.tscn`（Hud.tscn 实例化为 `Root/PlayerBar`），代码只改填充宽度 | 容易 |
| Boss 屏幕上方血条 | 预制体 `Scenes/UI/Bars/BarBoss.tscn`，`Scripts/UI/PlayerHud.cs` 实例化 | 容易 |
| 准星 / HP 文本 / 弹药文本 | `Scenes/UI/Hud.tscn` | 容易 |
| 各个弹窗（奖励/商店/开始/基地/帮助/加载） | `Scenes/UI/*.tscn` | 容易（改贴图注意 §8.4） |
| 地面 / 柱子 / 墙 / 斜坡 | `Scenes/Levels/TestArena.tscn`（关卡实际用的竞技场） | 中等 |
| 基地地面 / 光照 | `Scenes/Levels/Base.tscn` | 中等 |
| 天空 / 环境光 / 太阳 | `Scenes/Levels/TestArena.tscn` 的 `WorldEnvironment` + `Sun` | 中等 |
| 相机手感（距离/俯角/FOV） | `Scenes/Camera/CameraRig.tscn` + `Scripts/Camera/CameraRig.cs` | 容易 |

---

## 1. 项目视觉现状（先看这一节，能省很多猜）

1. **全项目没有任何美术资源**：没有 `.png` 贴图（除 `icon.svg` 与 `docs/screenshots/` 截图）、没有字体（`.ttf/.otf` 0 个）、没有 3D 模型（`.glb/.gltf/.fbx/.obj` 0 个）、没有任何 `Theme`/`StyleBox` 资源。唯一的 `.tres` 是 `Config/Balance.tres`。
2. **所有 3D 可见物 = Godot 内置图元 mesh（`CapsuleMesh`/`BoxMesh`/`CylinderMesh`/`SphereMesh`）+ 纯色 `StandardMaterial3D`**。
3. **所有粒子效果不存在**：全项目 grep `Particles|GPUParticles|CPUParticles|MultiMesh|Decal|Trail|AnimationPlayer|Skeleton3D|ShaderMaterial` → **0 命中**（`.cs` / `.tscn` / `.tres` / `project.godot` 全部）。也就是说：**没有枪口火光、没有命中火花、没有脚步尘土、没有任何骨骼动画**。「特效」目前只有 `Explosion.tscn` 一个半透明球，和代码里的受击闪色。
4. **所有 UI = 纯色 `ColorRect` + `Label` + 默认主题的 `PanelContainer`/`Button`**，没有任何贴图。
5. 大量 mesh/material 是**场景内联 `sub_resource`**（如 `CapsuleMesh_body`、`StandardMaterial3D_body`），每个敌人场景各有一份，**不能跨场景复用**。
6. **奖励节点当前并没有"按类型上色"**：`RewardNode.tscn:5-9` 只有一份蓝色材质，`Main.tscn:147-195` 的 10 个实例只覆盖了 `Prompt` 和 `Kind`，没有覆盖材质 → 10 个节点全是同一种浅蓝（截图 `docs/screenshots/challenger_mark2.png` 可佐证）。按类型上色属于**待做的改进**，不是现状。

---

## 2. 交互物

> ⚠️ **另一个代理正在把这一节的场景内联 `sub_resource` 外置到 `Assets/`**。下面的行号可能已变，替换前以实际文件为准。

| 类别 | 对象 | 所在场景 | 视觉节点路径 | 当前实现（具体值） | 建议替换成 | 难度 |
|---|---|---|---|---|---|---|
| 交互物 | 奖励节点（10 个实例 + 运行时的"降临节点"） | `Scenes/Interactables/RewardNode.tscn` | `Mesh`（MeshInstance3D，`transform` 上移 1.2） | `CylinderMesh` top_r=0.35 / bottom_r=0.55 / h=2.4（`:11-15`）+ 内联 `StandardMaterial3D` albedo `(0.35,0.8,1)`、emission `(0.2,0.6,0.9)` ×0.8（`:5-9`） | 独立模型（浮空石台/水晶）+ **每种 Kind 一个材质**（Kind 见下表） | 中等 |
| 交互物 | Boss 门 | `Scenes/Interactables/BossDoor.tscn` | `Mesh`（`transform` 上移 2.5） | `BoxMesh` 4.4×5×0.6（`:11-13`）+ 内联材质 albedo `(0.75,0.2,0.35)`、emission `(0.5,0.1,0.2)` ×0.6（`:5-9`） | 独立门模型（双开门/传送门），保留"变暗表示已用"的单材质表面 | 中等 |
| 交互物 | 基地·局外强化台 | `Scenes/Levels/Base.tscn` | `TerminalUpgrades/Mesh`（`:63-70`） | 共用内联 `BoxMesh` 1.6×2.2×1.6（`:40-41`）+ `surface_material_override/0` = `Mat_upgrade` 绿 `(0.35,0.75,0.45)`（`:20-23`） | 独立台子模型；材质走 surface override 或独立材质文件 | 中等 |
| 交互物 | 基地·装备配置台 | 同上 | `TerminalLoadout/Mesh`（`:72-79`） | 同一 `BoxMesh` + `Mat_loadout` 蓝 `(0.4,0.6,0.95)`（`:25-28`） | 同上，换配色 | 中等 |
| 交互物 | 基地·操作说明台 | 同上 | `TerminalHelp/Mesh`（`:81-88`） | 同一 `BoxMesh` + `Mat_help` 黄 `(0.95,0.85,0.4)`（`:30-33`） | 同上，换配色 | 中等 |
| 交互物 | 基地·出击门 | 同上 | `TerminalGate/Mesh`（`:90-98`） | 同一 `BoxMesh` + `Mat_gate` 红 `(0.9,0.35,0.35)`（`:35-38`） | 同上，换成"门"的造型 | 中等 |

**奖励节点 10 个实例的位置与 Kind**（`Scenes/Main.tscn:147-195`，坐标 X,Z）：

| 节点名 | 位置 | Kind | Prompt |
|---|---|---|---|
| NodeEnhance3 | (-15,-10) | 1 Enhancement | 强化池 |
| NodeEnhance | (-5,-10) | 1 | 强化池 |
| NodeEvolution | (15,-10) | 2 Evolution | 进化池 |
| NodeMutation | (-15,10) | 3 Mutation | 异化池 |
| NodeShop | (-5,0) | 7 Shop | 补给站（花碎片） |
| NodeResource | (5,10) | 4 Resource | 材料 |
| NodeEnhance2 | (15,0) | 1 | 强化池 |
| NodeEnhance4 | (-15,0) | 1 | 强化池 |
| NodeHeal | (5,0) | 5 Heal | 恢复状态 |
| NodeSpecial | (5,-10) | 6 Special | 特殊奖励 |
| （运行时生成） | Boss 尸体附近 | 8 Portal | 前往下一关 |

- **只有 3 个高价值（2 进化/异化 + 5 中 + 3 低）会被启用**，其余 `Visible=false` 藏起来（`Scripts/Run/StageController.cs:255`、`:496-502`）。
- **用掉后变暗**：代码复制材质 → albedo `(0.22,0.22,0.25)`、关掉 emission（`Scripts/Interaction/Interactable.cs:103-114`）。切关时 `ResetNode()` 把 override 清空复原（`:93-100`）。
- **名字必须是 `Mesh`**：`Interactable.cs:96` 与 `:105` 用 `GetNodeOrNull<MeshInstance3D>("Mesh")` 拿它（**Boss 门同理**，因为都继承 `Interactable`）。

---

## 3. 单位

> **⚠️ 本节已于「第二轮」更新（2026-10）**：**9 个敌人 + Boss 的 `Model` 已全部换成真模型**
> （Kenney Mini Characters，CC0，与玩家同包）。下表「当前实现」列是**替换后**的真实值；
> 轴心/朝向/缩放的实测过程与证据见 [art-integration-log.md](art-integration-log.md) §9。
> 玩家、Drone、3 个木桩仍是胶囊（不在本轮范围）。

> 每个单位都是 `CharacterBody3D`，结构固定：`Body`(CollisionShape3D) + `Model`(glb 实例) + `Movement` + `Brain` + `Presentation`。
> **`Model` 现在是 `.glb` 的场景实例根节点（`Node3D`）**，`transform` 里一次性写全
> 「缩放 s + 绕 Y 180° + 位移」：`Transform3D(-s,0,0, 0,s,0, 0,0,-s, 0,0,0)`。
> **`y` 分量是 0**（模型的轴心实测在脚底），`s = 胶囊高度 / 模型原生高度` → 视觉与碰撞同高同底。
> 每个 `Model` 下都有一条 `AnimationPlayer` 属性覆盖（`index="1"`、`autoplay="idle"`，零 C#）。

| 类别 | 对象 | 所在场景 | 视觉节点路径 | 当前实现（模型 / 原生高 / 缩放） | 建议替换成 | 难度 |
|---|---|---|---|---|---|---|
| 单位 | 玩家 | `Scenes/Player/Player.tscn:44-48` | `Model` | **已换**：`mesh_player_body.glb`（male-a），H=0.6713，s=2.6814，y=0 | 已有模型 | 容易 |
| 单位 | Chaser 基础近战 | `Scenes/Enemies/Chaser.tscn` | `Model` | **已换**：`mesh_enemy_chaser.glb`（male-b，橙红大胡子），H=0.6613，**s=2.72191** | 已有模型 | 容易 |
| 单位 | Charger 冲锋兵 | `Scenes/Enemies/Charger.tscn` | `Model` | **已换**：`mesh_enemy_charger.glb`（male-c，蓝制服壮汉），H=0.7928，**s=2.52270** | 已有模型 | 容易 |
| 单位 | Exploder 自爆 | `Scenes/Enemies/Exploder.tscn` | `Model` | **已换**：`mesh_enemy_exploder.glb`（male-d，黑西装），H=0.7218，**s=2.07814** | 已有模型 | 容易 |
| 单位 | FastMelee 快速近战 | `Scenes/Enemies/FastMelee.tscn` | `Model` | **已换**：`mesh_enemy_fast_melee.glb`（female-e，白衣），H=0.7165，**s=2.23308** | 已有模型 | 容易 |
| 单位 | Guard 守卫 | `Scenes/Enemies/Guard.tscn` | `Model` | **已换**：`mesh_enemy_guard.glb`（male-e，白+橙），H=0.6760，**s=3.10651** | 已有模型 | 容易 |
| 单位 | JumpingMelee 跳跃近战 | `Scenes/Enemies/JumpingMelee.tscn` | `Model` | **已换**：`mesh_enemy_jumping_melee.glb`（female-f，深发蓝衣），H=0.6713，**s=2.68137** | 已有模型 | 容易 |
| 单位 | RangedEnemy 远程 | `Scenes/Enemies/RangedEnemy.tscn` | `Model` | **已换**：`mesh_enemy_ranged.glb`（male-f，棕发），H=0.6713，**s=2.68137**；它的 `Muzzle` 仍是空 `Marker3D`，**位置未动** | 已有模型 | 容易 |
| 单位 | Screamer 尖啸者 | `Scenes/Enemies/Screamer.tscn` | `Model` | **已换**：`mesh_enemy_screamer.glb`（female-a，黑发紫衣），H=0.7755，**s=2.57898** | 已有模型 | 容易 |
| 单位 | SkillElite 精英 | `Scenes/Enemies/SkillElite.tscn` | `Model` | **已换**：`mesh_enemy_skill_elite.glb`（female-b，金发黄衣），H=0.7234，**s=2.90296**。⚠️ `Elite` 节点的**染色已失效**（见 §8.5），`Scale=1.2` 仍生效 | 已有模型 | 容易 |
| 单位 | Boss | `Scenes/Enemies/Boss.tscn` | `Model` | **已换**：`mesh_enemy_boss.glb`（female-c，蓝衣灰发），H=0.7755，**s=3.61057**（2.8 m 的巨人） | 已有模型 | 容易 |
| 单位 | Drone 召唤物 | `Scenes/Summons/Drone.tscn:42-44` | `Model` | `CapsuleMesh` r0.3 h1.0，y=0.55；albedo `(0.35,0.95,0.85)` + emission `(0.2,0.7,0.6)` ×0.5 | 浮空无人机模型（要能悬空，它在 0.55 高度） | 容易 |
| 单位 | 训练木桩·纯木桩 | `Scenes/Training/TrainingDummy.tscn:33-35` | `Model` | `CapsuleMesh` r0.6 h2.2，y=1.1；albedo `(0.62,0.45,0.26)` | 木桩模型 | 容易 |
| 单位 | 训练木桩·打击木桩 | `Scenes/Training/TrainingAttacker.tscn:38-40` | `Model` | `CapsuleMesh` r0.45 h1.8，y=0.9；albedo `(0.78,0.55,0.3)` | 同上 | 容易 |
| 单位 | 训练木桩·远程木桩 | `Scenes/Training/TrainingShooter.tscn:36-38` | `Model` | `CapsuleMesh` r0.45 h1.8，y=0.9；albedo `(0.75,0.7,0.35)` | 同上 | 容易 |
| 单位 | 木桩名牌·纯木桩 | `Scenes/Training/TrainingDummy.tscn:37-43` | `Label`（`Label3D`，billboard，y=2.6） | `font_size=64`、`outline_size=12`、`no_depth_test=true`、文本"纯木桩" | 可保留；换成图标贴图 | 容易 |
| 单位 | 木桩名牌·打击木桩 | `Scenes/Training/TrainingAttacker.tscn:42-48` | `Label`（同上，y=2.3） | 同上，文本"打击木桩" | 同上 | 容易 |
| 单位 | 木桩名牌·远程木桩 | `Scenes/Training/TrainingShooter.tscn:40-46` | `Label`（同上，y=2.3） | 同上，文本"远程木桩" | 同上 | 容易 |

**注意**：所有单位的碰撞胶囊 `Body` 与 `Model` 同高同底（`Body.y = 胶囊高/2`、`Model.y = 0`），
**换模型不要动 `Body`**；本轮 10 个单位的 `vis_max_y` 与 `capsule_h` 误差 ≤ 0.0001 m（实测见 §9.2）。

**验证工具**：`Scenes/Enemies/_ArtShowcase.tscn` + `_art_shot.gd` —— 把 10 个敌人预制体按真实场景排成两排，
一次运行自动拍 9 张 PNG 到 `docs/screenshots/`。跑法见 [art-integration-log.md](art-integration-log.md) §9.6.1。

---

## 4. 武器

| 类别 | 对象 | 所在场景 | 视觉节点路径 | 当前实现 | 建议替换成 | 难度 |
|---|---|---|---|---|---|---|
| 武器 | 手枪（只有这一把武器场景） | `Scenes/Weapons/Pistol.tscn:28-29` | `Body` | `BoxMesh` 0.1×0.16×0.34；albedo `(0.22,0.23,0.27)`（`:6-11`） | 枪械模型（**注意朝向：`Muzzle` 在 -Z**） | 容易 |
| 武器 | 手枪枪口点 | 同上 `:31-32` | `Muzzle`（Marker3D，不可见） | 位置 `(0,0,-0.24)` | 必须与模型枪口对齐（子弹/特效从这里出） | 容易 |
| 武器 | 远程敌人的枪口点 | `Scenes/Enemies/RangedEnemy.tscn:89-90` | `AbilityRanged/Weapon/Muzzle` | Marker3D `(0,0,-0.5)`；**该武器没有任何 mesh** | 加了枪模型的话，把 `Muzzle` 挪到枪口 | 容易 |
| 武器 | 训练远程木桩的枪口点 | `Scenes/Training/TrainingShooter.tscn:83-84` | `AbilityRanged/Weapon/Muzzle` | Marker3D `(0,0,-0.5)`；同样无 mesh | 同上 | 容易 |

- 玩家手里的枪挂在 `Player.tscn:61-64` 的 `WeaponMount`（位置 `(0.36,1.15,-0.25)`）→ **手部高度约 1.15 米**，换人形模型时手的位置要对得上。
- 只有 `Pistol.tscn` 一个武器场景，但开始界面写着 3 种武器（手枪/狙击枪/步枪，`Scripts/UI/StartScreen.cs:46-48`）。**另外两把是同一场景 + 参数改的**（见 `Scripts/Rewards/RewardCatalog.cs`），所以换枪模型只需换 `Pistol.tscn` 一处。
- `Muzzle` **必须叫 `Muzzle`**：`Scripts/Weapons/Weapon.cs:95` 按名字取。

---

## 5. 特效 / 粒子 / 代码生成的视觉

> ⚠️ `Scenes/Combat/` 的两行正由**另一个代理**做内联资源外置。

| 类别 | 对象 | 所在场景 / 代码 | 视觉节点路径 | 当前实现（具体值） | 建议替换成 | 难度 |
|---|---|---|---|---|---|---|
| 特效 | 子弹 | `Scenes/Combat/Bullet.tscn:20-21` | `Mesh` | `BoxMesh` 0.07×0.07×0.45；albedo `(1,0.82,0.3)` + emission `(1,0.65,0.15)` ×**4.0**（`:5-13`） | 曳光弹：`MeshInstance3D` + `Trail`/`GPUParticles3D`（当前完全没有尾迹）；或换成细长发光体 | 中等 |
| 特效 | 爆炸 | `Scenes/Combat/Explosion.tscn` | `Mesh`（保留）+ **`FX/Vfx`（第二轮新增）** | **已接入 Binbun Explosion FX（CC0）**：`FX`(`fx_detach_lifetime.gd`, `fx_scale=2.5`) → `Vfx`(`res://Assets/Effects/fx_combat_explosion.tscn`, `emission=8`)。原来的半透明球 `Mesh` **保留**（0.12 s 的核心闪光，半径正好等于判定半径）。⚠️ **`Shape` 的 `SphereShape3D radius=1` 与 `Explosion.Lifetime=0.12` 都是玩法，原样未动** | 想更亮需要在 `TestArena.tscn` 的 Environment 开 glow（不在本轮范围） | 中等 |
| 特效 | 挑战者地面三角环 | **无场景**，`Scripts/Combat/ChallengerMark.cs` | 代码 `AddChild` 的 MeshInstance3D，`Name = "Mark"`（`:36`） | 代码用 `SurfaceTool` 生成 `ArrayMesh`：6 个三角形绕半径 1.15 排成环（`:55-77`）；代码建材质 unshaded + alpha，albedo `(1,0.75,0.25)`、emission `(1,0.6,0.15)`（`:40-47`）；出现/消失补间 + 自转 + 呼吸（`:79-128`） | **保留程序化**（要贴图就换成贴图环/Decal）；换成美术模型需改 C# | 较难 |
| 特效 | 受击闪色 | `Scripts/Combat/UnitPresentation.cs` | 无场景节点，运行时 `MaterialOverride` | 代码建材质 albedo/emission = `FlashColor` `(1,0.55,0.45)` ×1.6，持续 `FlashDuration=0.09s`（`:12-13`、`:29-35`）；**递归收集单位子树里所有 MeshInstance3D**（`:70-81`） | 想换成受击特效/贴图闪白：改 C# 或给单位加 `GPUParticles3D` 子节点（会被一起染色，注意） | 较难 |
| 特效 | 精英染色 / 放大 | `Scripts/Combat/EliteModifier.cs` | 无场景节点，运行时 `MaterialOverride` + `unit.Scale` | `ModelScale` 默认 1.35（精英场景写 1.2）→ **整个单位（含碰撞体）等比放大**（`:42-45`）；`Tint` 默认 `(0.85,0.3,0.9)`（场景写 `(0.75,0.4,0.95)`）盖在**直接子节点**的 mesh 上（`:47-61`） | 精英外观建议做成独立材质/附加装饰；注意它**不递归**，挂在更深层的 mesh 不会被染色 | 较难 |
| 特效 | 交互物"已用"变暗 | `Scripts/Interaction/Interactable.cs:103-114` | 运行时替换 `Mesh` 的 surface 0 材质 | 复制原材质 → albedo `(0.22,0.22,0.25)`、关 emission | 若新模型有多个 surface 或不是 `StandardMaterial3D`，这段会**静默失效**（见 §8.5） | 较难 |
| （缺口） | 命中火花 | **第二轮已就位**（尚未挂到命中事件上） | `Assets/Effects/fx_combat_hit.tscn` | **Binbun Hit FX（CC0）** `vfx_hit_02`：白色四角星 + 橙色辐射条纹 + 地面光斑，`one_shot=true / autoplay=true`。证据 `docs/screenshots/art_33_hit_t0.png`。⚠️ 真正"打中的那一刻"要 `Projectile.cs` / `UnitPresentation.cs` 加 3 行（本轮边界外，见 [art-integration-log.md](art-integration-log.md) §9.5） | 接线即可 | — |
| （缺口） | 粒子系统 | — | — | **已不再是 0**：枪口火光（上一轮）、子弹拖尾（上一轮）、爆炸、命中火花（第二轮）都有 `GPUParticles3D` 了；仍缺**落地尘土 / 召唤光圈 / 升级特效** | 继续从 `_art_staging` 里的 Kenney Particle Pack / Brackeys VFX Bundle 搭 | — |
| （缺口） | 骨骼动画 / AnimationPlayer | — | — | **已不再是 0**：玩家的 `.glb` 上一轮起就在用；**9 敌 + Boss 本轮全部接上 `autoplay="idle"`**（每个 32 段动画，零 C#）。仍**没有**按速度切 walk/run 的状态机、没有死亡动画 | 想要"跑起来"要新增 `UnitAnimator`（新代码） | — |

---

## 6. UI

### 6.1 常驻 HUD：`Scenes/UI/Hud.tscn`（CanvasLayer，脚本 `PlayerHud.cs`）

| 类别 | 对象 | 视觉节点路径 | 当前实现 | 建议替换成 | 难度 |
|---|---|---|---|---|---|
| UI | HP 文字 | `Root/Health`（Label） | 默认字体 26px；左下 `(24,16)-(520,52)` | 保留 / 换字体主题 | 容易 |
| UI | 血条底 | `Root/PlayerBar/BarBg`（**ColorRect**） | 264×20，黑 0.55 alpha；现在是预制体 `Scenes/UI/Bars/BarPlayer.tscn` 的内容 | 九宫格底板贴图（换成 `NinePatchRect` 也可以，代码只按名字找 `Fill`/`DelayBar`） | 容易 |
| UI | 血条填充 | `Root/PlayerBar/Fill`（**TextureRect**，贴图 `icon.svg`） | 260×16 绿 `(0.4,0.85,0.4)`；代码**只改填充宽度**（`PlayerHud.cs` 的 `UpdateHealthBar`），**宽度由预制体说了算**（不再有 `HealthBarWidth` 常量） | 直接换贴图（`TextureRect`，代码不认控件类型，`ColorRect`/`NinePatchRect` 也能换回来） | 容易 |
| UI | 血条残影（延迟条） | `Root/PlayerBar/DelayBar`（**TextureRect**） | 与 `Fill` 同 rect、默认隐藏；受伤时按旧血量亮相后缓慢缩到真实血量（`AdvanceDelay`，速度 0.6/秒），配色红 `(1,0.35,0.3)` | 换贴图 / 换成动画（预制体上有空 `AnimationPlayer`） | 容易 |
| UI | 准星（横） | `Root/CrosshairH`（ColorRect 18×2 白 0.9） | `:40-52` | 准星贴图 / 动态准星 | 容易 |
| UI | 准星（竖） | `Root/CrosshairV`（ColorRect 2×18 白 0.9） | `:54-66` | 同上 | 容易 |
| UI | 准星中心点 | `Root/CrosshairDot`（ColorRect 3×3 黄 `(1,0.85,0.3)`） | `:68-80` | 同上 | 容易 |
| UI | 状态行（关卡/剩余节点/Boss 血） | `Root/Status`（Label，顶部居中 24px） | `:82-92` | 保留 | 容易 |
| UI | 构筑说明 | `Root/Build`（Label，左下 20px） | `:94-104` | 保留 | 容易 |
| UI | 弹药 | `Root/Ammo`（Label，右下 26px） | `:106-119` | 换图标 + 数字排版 | 容易 |
| UI | Boss 屏幕上方血条 | 预制体 `Scenes/UI/Bars/BarBoss.tscn`，由 `PlayerHud.BuildBossPanel()` 实例化后挂到 HUD 层（节点名 `BossPanel`） | 头像 = 44×44 `TextureRect` 红 `(0.85,0.25,0.3)`（`modulate`，换贴图即可）；血条 = `ProgressBar`（填充色在预制体 `theme_override_styles/fill`）；名称/状态 = 两个 `Label`；整块 `Control` 锚在顶部居中 `(-240,14)-(240,92)`；只在玩家距 Boss ≤ `BossBarFullRange=22` 米时 `Visible` | 换美术图**不用改 C#**（改预制体：头像换贴图、血条换样式、字号/位置随便调） | 容易 |

- **HUD 没有可换的底图/边框**：`Root` 是全屏 `Control`，`mouse_filter = 2`（穿透），其余全是上面这些节点。

### 6.2 世界投影层：`Scenes/UI/WorldOverlay.tscn`

| 类别 | 对象 | 视觉节点路径 | 当前实现 | 建议替换成 | 难度 |
|---|---|---|---|---|---|
| UI | 血条容器 | `Bars`（空 Control，全屏、穿透） | `:9-15`，只当父节点用 | — | 容易 |
| UI | 跳字容器 | `Numbers`（空 Control，全屏、穿透） | `:17-23` | — | 容易 |
| UI | 敌人血条（每怪一条） | 预制体 `Scenes/UI/Bars/BarEnemy.tscn`，运行时实例化到 `Bars` 下 | `Content/Bg` 66×9 黑 0.65、`Content/Fill` 64×7、`Content/DelayBar`（残影，默认隐藏）；颜色按血量 绿`(0.4,0.9,0.4)`/黄`(1,0.8,0.3)`/红`(1,0.35,0.3)`（`WorldOverlay.HealthColor`）。尺寸来自 `Balance.tres:120-121`（`EnemyHealthBarWidth=64` / `Height=7`），预制体按同比例整体缩放；位置 = 单位原点**向下** `EnemyHealthBarTopOffset=0.25` 米投影（注意字段名叫 Top 其实是往脚下偏） | 换成贴图血条**不用改 C#**（改预制体：`Fill` 换 `TextureRect`、加边框、改颜色/圆角） | 容易 |
| UI | 伤害跳字 | 运行时在 `Numbers` 下新建 `Label` | 文本=伤害值；玩家造成 = 黄 `(1,0.85,0.35)`、受到 = 红 `(1,0.4,0.35)`；字号 `DamageNumberFontSize=24`；描边黑 0.85 / `outline_size=5`；位置 = 单位位置 + **`Vector3.Up * 1.5`（硬编码在 `WorldOverlay.cs:73`）**；上浮 `70px` / `0.8s`（`Balance.tres:116-118`） | 换字体/贴图/暴击样式都要改 C# | 较难 |

### 6.3 弹窗界面（全部是纯色底 + 默认主题控件）

> 六个界面都是 `OverlayScreen` 子类。**节点名不能改**（见 §8.3），但**配色/贴图随便换**。
> 所有 `Panel`/`PanelContainer` 用的是 **Godot 默认主题的 StyleBox**（灰底圆角）——项目里**没有任何 Theme/StyleBox 资源**，想换面板美术就得新建主题或换成 `NinePatchRect`。

| 界面 | 场景 | 视觉节点路径 | 当前实现 | 难度 |
|---|---|---|---|---|
| 三选一奖励 / 结算弹窗 | `Scenes/UI/RewardScreen.tscn` | `Root/Dim` | ColorRect 全屏黑 0.65（`:17-23`） | 容易 |
| | | `Root/Panel` | `PanelContainer` 640×380 居中（`:25-35`），默认主题 StyleBox | 中等（需加主题） |
| | | `Root/Panel/VBox/Title` | Label 30px "选择奖励"（`:40-43`） | 容易 |
| | | `Root/Panel/VBox/Cards/CardTemplate` | `Button` **600×78、`visible=false` 的模板**，运行时 `Duplicate()` 出来当卡片（`:48-50`，`RewardScreen.cs:76-84`） | 容易 |
| | | `Root/Panel/VBox/Hint` | Label 18px 操作提示（`:52-54`） | 容易 |
| 通用多选一（商店/改造） | `Scenes/UI/ChooseScreen.tscn` | `Root/Dim` | 黑 0.8（`:17-24`） | 容易 |
| | | `Root/Panel` | `PanelContainer` 800×420（`:26-36`） | 中等 |
| | | `Root/Panel/VBox/Title` | Label 30px（`:41-44`） | 容易 |
| | | `Root/Panel/VBox/Cards/CardTemplate` | `Button` 740×84 模板（`:49-51`） | 容易 |
| | | `Root/Panel/VBox/Hint` | Label 18px（`:53-55`） | 容易 |
| 开局选武器（装备配置台） | `Scenes/UI/StartScreen.tscn` | `Root/Dim` | 黑 0.82（`:17-24`） | 容易 |
| | | `Root/Panel` | `PanelContainer` 800×440（`:26-36`） | 中等 |
| | | `Root/Panel/VBox/Title` | Label 32px "配置 · 选择本局武器"（`:41-44`） | 容易 |
| | | `Root/Panel/VBox/Cards/CardTemplate` | `Button` 740×96 模板（`:49-51`） | 容易 |
| | | `Root/Panel/VBox/Hint` | Label 18px（`:53-55`） | 容易 |
| 基地·局外强化台 | `Scenes/UI/BaseScreen.tscn` | `Root/Dim` | ColorRect 深蓝黑 `(0.05,0.06,0.09,0.92)`（`:17-24`） | 容易 |
| | | `Root/Panel` | `PanelContainer` 760×460（`:26-36`） | 中等 |
| | | `Root/Panel/VBox/Title` | Label 32px "基地 · 出征准备"（`:41-44`） | 容易 |
| | | `Root/Panel/VBox/Shards` | Label 22px 碎片数（`:46-49`） | 容易 |
| | | `Root/Panel/VBox/Hint` | Label 18px，`autowrap`（`:51-55`） | 容易 |
| | | `Root/Panel/VBox/Upgrades/{Health,Damage,Speed,Crit,Reload}` | **5 个 `Button`，各 680×48**（`:57-73`） | 容易 |
| | | `Root/Panel/VBox/Start` | `Button` 680×56（`:75-76`） | 容易 |
| 操作说明 | `Scenes/UI/HelpScreen.tscn` | `Root/Dim` | 黑 0.75（`:17-24`） | 容易 |
| | | `Root/Panel` | `PanelContainer` 760×500（`:26-37`） | 中等 |
| | | `Root/Panel/VBox/Title` | Label 30px（`:42-45`） | 容易 |
| | | `Root/Panel/VBox/Body` | Label 19px，**整篇说明文字写死在场景里**（`:47-78`） | 容易 |
| 加载页 | `Scenes/UI/LoadingScreen.tscn` | `Root/Dim` | ColorRect 全屏不透明 `(0.03,0.03,0.05,1)`（`:17-24`） | 容易 |
| | | `Root/Panel` | `VBoxContainer` 520×120（`:26-37`） | 容易 |
| | | `Root/Panel/Title` | Label 34px "出击"（`:39-42`） | 容易 |
| | | `Root/Panel/Label` | Label 18px "加载中…"（`:44-47`） | 容易 |

---

## 7. 场景环境

| 类别 | 对象 | 所在场景 | 视觉节点路径 | 当前实现 | 建议替换成 | 难度 |
|---|---|---|---|---|---|---|
| 环境 | 天空 + 环境光 | `Scenes/Levels/TestArena.tscn:3-12` | `WorldEnvironment` | 内联 `ProceduralSkyMaterial` + `Sky` + `Environment`：`background_mode=2`（天空）、`ambient_light_source=3`、`tonemap_mode=2` | 天空盒贴图 / 程序化天空参数；想换 `Environment` 需外置成 `.tres` | 中等 |
| 环境 | 太阳 | 同上 `:62-64` | `Sun`（DirectionalLight3D） | `shadow_enabled=true`，默认能量 1.0 | 保留；可加 SSAO/体积雾 | 容易 |
| 环境 | 地面 | 同上 `:66-73` | `Ground/Mesh` | `BoxMesh` 80×1×80，albedo `(0.26,0.31,0.27)` 墨绿（`:17-22`） | 地面材质（草/金属）+ 平铺贴图；建议同时加 `Triplanar` | 中等 |
| 环境 | 柱子 ×3 | 同上 `:75-100` | `Pillar1/2/3` → `Mesh` | 共享 `BoxMesh` 1.6×6×1.6 + 内联 `StandardMaterial3D_block` 灰 `(0.62,0.62,0.66)`（`:24-32`） | 一根柱子模型复用到 3 处（**共享同一 mesh+材质**，改一处即可） | 中等 |
| 环境 | 墙 | 同上 `:102-109` | `Wall/Mesh` | 同为 `BoxMesh` 12×5×1 + 灰材质（`:34-39`） | 墙体模型 | 中等 |
| 环境 | 斜坡 | 同上 `:111-118` | `Ramp/Mesh` | `BoxMesh` 6×0.6×8，节点绕 X 转 15°（`:41-46`） | 斜坡模型；**倾斜角与碰撞体一致，别只改 mesh** | 中等 |
| 环境 | 斜坡底座 | 同上 `:120-127` | `RampBase/Mesh` | `BoxMesh` 6×2.4×8（`:48-53`） | 同上 | 中等 |
| 环境 | 基地地面 | `Scenes/Levels/Base.tscn:45-54` | `Ground/Mesh` | `BoxMesh` 26×1×26，albedo `(0.22,0.24,0.28)` 深蓝灰（`:13-18`） | 基地地面材质（基地**没有 WorldEnvironment**，用的是引擎默认环境，偏灰暗） | 中等 |
| 环境 | 基地太阳 | 同上 `:56-58` | `Sun`（DirectionalLight3D） | `light_energy=1.1`，无阴影设置 | 补 WorldEnvironment + 阴影 | 容易 |
| 环境 | 相机 | `Scenes/Camera/CameraRig.tscn` | `SpringArm`（SpringArm3D）+ `Camera`（Camera3D） | 弹簧臂长 8.0、margin 0.2、相机 `fov=78`；`PitchDegrees=20`、`TargetOffset=(0,1.5,0)`、`TargetScreenHeight=0.3`、`FollowSharpness=12` 都在代码里（`CameraRig.cs:20-32`） | 手感参数可调；**模型高度变了要动 `TargetOffset.y`**（§8.2） | 容易 |

**导航**：`TestArena.tscn:129-131` 有一个 `NavigationRegion3D`，但它的 `NavigationMesh` 是空的；`MapManager` 运行时重烘也始终得到 0 个多边形（`docs/todo-handoff.md:168` 记载为已知问题）。**换地面/建筑模型不影响视觉，但会继续影响导航烘焙**（面片数越多，出问题的概率不变，因为现在就是 0）。

---

## 8. 美术接入的注意事项（踩坑清单）

### 8.1 血条：外观在预制体里，尺寸一半在配置里

| 血条 | 尺寸 | 锚点 / 位置 | 可配吗 |
|---|---|---|---|
| 敌人血条 | `Balance.tres:120-121` `EnemyHealthBarWidth=64` / `EnemyHealthBarHeight=7` | `Root` 无锚点：每帧把屏幕投影点减去 半尺寸 后设为 `Position`（预制体 `Content` 以 `Root` 原点为中心）；垂直方向取 `单位位置 - Up*0.25` | 尺寸**可配**（改 `.tres`，预制体按比例缩放）；颜色/形状**在预制体里**（`BarEnemy.tscn`） |
| 玩家血条 | 预制体 `BarPlayer.tscn`：`BarBg` 264×20、`Fill` 260×16、`DelayBar` 同 `Fill`；代码**只改填充宽度**（`PlayerHud.UpdateHealthBar`） | 预制体 rect 固定（左上角 `24,56` 起），锚点 = 左上 | **全部可配**（改预制体；宽度不再被代码覆盖） |
| Boss 血条 | 预制体 `BarBoss.tscn`：面板 `-240..240 / 14..92`、头像 44×44、进度条 `56..480` | 顶部居中锚点（`Panel` 锚 `0.5`） | **全部可配**，改美术不用改 C# |
| 伤害跳字 | 字号/时长/上浮距离可配（`Balance.tres:116-118`） | **位置写死**：`单位位置 + Up*1.5`（`WorldOverlay.cs:73`） | 部分可配，**高度要改代码** |

> 另：血条只在**受伤后**出现、满血即隐藏；`Boss` 标签的单位在 `WorldOverlay` 里被跳过，交给 `PlayerHud`。高个模型（Boss 2.8 米）的伤害跳字在 1.5 米处 = 腰部；小 Drone（1.0 米）则在头顶上方 0.5 米 —— 尺寸不统一是**现有现象**，不是 bug。
> 三条血条都有**受伤残影条** `DelayBar`（按旧血量亮相后缓慢缩到真实血量，速度 0.6/秒）与**空 `AnimationPlayer` 占位**；填充比例写入（`SetRatio`）与表现更新（`AdvanceDelay`）是分开的两个方法，插动画只动后者。

### 8.2 单位模型的轴心与高度：有硬编码假设

1. **单位原点 = 脚底**。`Body` 胶囊的 `transform.y` = 高度/2（如玩家 1.8 高 → y=0.9，`Player.tscn:47-49`），胶囊底面正好在 local y=0。
2. **`Model` 的 `transform.y` 也 = 高度/2**（与 `Body` 同值）。→ **若换成"原点在脚底"的常规人形模型，模型会整体下沉半身高（玩家会陷进地面 0.9 米）**。两种改法：把 `Model` 的 y 改成 0，或把模型资源的上移补偿做在建模里。
3. **`CharacterMovement` 只做绕 Y 的旋转**：`_body.Rotation = new Vector3(0, LerpAngle(...), 0)`（`CharacterMovement.cs:189`）——没有任何俯仰/倾斜/动画。**模型必须"默认朝 -Z"**，否则转身方向会反（`Mathf.Atan2(-direction.X, -direction.Z)`，`:187`）。
4. **跳跃/重力与模型高度无关**（`JumpVelocity=6.5`、`GravityScale=1.8`，`CharacterMovement.cs:12-16`），换高个模型不影响跳跃，但**视觉上会显得跳得矮**。
5. **相机假设胸口高 1.5 米**：`CameraRig.TargetOffset = (0,1.5,0)`（`CameraRig.cs:23`），并且默认值写在代码里（`Player.tscn:59` 的 `CameraRig` 实例**没有覆盖它**）。换 2.5 米的模型 → 需要把 `TargetOffset.y` 提到 ~1.8，或在 `Player.tscn` 里覆盖该属性。另外 `TargetScreenHeight=0.3` 决定了角色在画面下方，它通过 `ScreenHeightLift()`（`:100-112`，用 `SpringArm.SpringLength=8` 与 `fov=78` 反算视锥高度）实现——**改 FOV 或弹簧臂长度会连带改变角色的屏幕位置**。
6. **精英/技能会二次缩放**：`EliteModifier` 直接改 `unit.Scale`（含碰撞体，`EliteModifier.cs:44`），所以精英会**比原型大 20~35%**。给敌人建模时留这个余量。
7. **摆放抬升 0.25 米**：交互物无论走导航吸附还是螺旋避障，最终节点原点都在地面上方 **0.25 米**（`NodePlacement.cs:62` / `MapManager.cs:217`）。当前奖励节点的圆柱底面恰好在节点原点 → **现状是悬空 0.25 米**（截图 `docs/screenshots/challenger_mark2.png` 里浅蓝节点确实离地一点点）。新模型的轴心放底面即可，想贴地就把节点原点下移 0.25。

### 8.3 代码里按名字取的视觉节点（**改名必须先改代码**）

> 用 `grep` 全量扫过 `Scripts/**/*.cs`（正则 `GetNode[A-Za-z]*[<(][^)]*"[^"]+"`），结果如下：**15 组共 26 个视觉节点名** + 7 组纯逻辑节点名。
> **`Model` 和 `Body` 没有任何代码引用，可以随便改名**；下表这些不能。

| # | 节点名 / 路径 | 取用位置 | 影响的对象 | 视觉? |
|---|---|---|---|---|
| 1 | `Mesh` | `Scripts/Interaction/Interactable.cs:96`、`:105` | 奖励节点、Boss 门（以及所有 `Interactable` 子类）的 3D 网格 | ✅ |
| 2 | `Muzzle` | `Scripts/Weapons/Weapon.cs:95` | `Pistol.tscn`、`RangedEnemy.tscn`、`TrainingShooter.tscn` 的枪口点 | ✅ |
| 3 | `CameraRig` | `Scripts/Player/PlayerController.cs:20`、`Scripts/Testing/SelfTest.cs:194` | 玩家下的相机 rig | ✅ |
| 4 | `SpringArm` | `Scripts/Camera/CameraRig.cs:54` | 相机的弹簧臂 | ✅ |
| 5 | `Root` | `PlayerHud.cs:42-46`、`BaseScreen.cs:28`、`ChooseScreen.cs:32`、`StartScreen.cs:28`、`RewardScreen.cs:20`、`HelpScreen.cs:20` | 六个界面的根 Control | ✅ |
| 6 | `Root/Health`、`Root/Ammo`、`Root/Status`、`Root/Build` | `Scripts/UI/PlayerHud.cs:42-45` | HUD 的 4 个文本 | ✅ |
| 7 | `Root/PlayerBar/Fill`、`Root/PlayerBar/DelayBar` | `Scripts/UI/PlayerHud.cs`（按名字取，**只当 `Control` 用**，不认控件类型） | 玩家血条填充与残影条（预制体 `BarPlayer.tscn`） | ✅ |
| 8 | `Root/Panel/VBox/Title`、`Root/Panel/VBox/Hint` | `RewardScreen.cs:34-35`、`ChooseScreen.cs:34-35` | 奖励/商店弹窗的标题与提示 | ✅ |
| 9 | `Root/Panel/VBox/Cards`、`Root/Panel/VBox/Cards/CardTemplate` | `RewardScreen.cs:36-37`、`ChooseScreen.cs:36-37`、`StartScreen.cs:30-31` | 三个选择界面的卡片容器与模板按钮（**必须是 `Button`**） | ✅ |
| 10 | `Root/Panel/VBox/Shards` | `Scripts/UI/BaseScreen.cs:30` | 基地碎片文本 | ✅ |
| 11 | `Root/Panel/VBox/Upgrades/{Health,Damage,Speed,Crit,Reload}`、`Root/Panel/VBox/Start` | `BaseScreen.cs:31-36` | 基地 5 个强化按钮 + 出击按钮（**必须是 `Button`**） | ✅ |
| 12 | `Root/Panel/Label` | `Scripts/UI/LoadingScreen.cs:23` | 加载页文案（找不到会静默 `new Label()`，不报错但会跑版） | ✅ |
| 13 | `Bars`、`Numbers` | `Scripts/UI/WorldOverlay.cs:31-32` | 世界投影层：血条/跳字的父节点（必须是 `Control`） | ✅ |
| 14 | `LoadingScreen` | `Scripts/Interaction/BaseTerminal.cs:84`（`CurrentScene.GetNodeOrNull<LoadingScreen>("LoadingScreen")`） | **`Base.tscn:108` 上的加载界面实例名**；改名 → 出击不再走加载页（静默退化为直接切场景） | ✅ |
| 15 | `Weapon` | `Scripts/Abilities/ProjectileAttackAbility.cs:24` | 远程敌人/木桩的武器节点（`Muzzle` 的父节点） | ✅（父） |
| 16 | `Movement` | `Scripts/Combat/Unit.cs:85`、`PlayerController.cs:19` | 移动组件 | 逻辑 |
| 17 | `Block` | `Scripts/Combat/Unit.cs:289` | 格挡组件（Guard / SkillElite） | 逻辑 |
| 18 | `Phases`、`Pressure` | `PlayerHud.cs:126-127`、`EncounterController.cs:662` | Boss 阶段 / 压力组件 | 逻辑 |
| 19 | `Attack` | `MeleeAttackAbility.cs:24`、`AreaAttackAbility.cs:21`、`ChargeAttackAbility.cs:23`、`LeapAttackAbility.cs:23`、`RunController.cs:107` | 命中盒（与视觉节点同层，容易连带改名） | 逻辑 |
| 20 | `AbilityMelee` / `AbilityRanged` / `AbilityBuff` / `AbilityControl` / `AbilityScream` / `AbilityCharge` / `AbilitySummon` | 靠 `UseAbilityBehavior.AbilityNodeName` 字符串（`SkillElite.tscn:76`、`Boss.tscn:67,72,81`）、`BossPerk.cs:98,111`、`Boss.tscn:168-169` | 技能节点 | 逻辑 |
| 21 | `KillZone` | `Scripts/World/MapManager.cs:135` | 区域杀 | 逻辑 |
| 22 | `Main/Stage`、`Main/RunController`、`Main/RunController/{RewardScreen,BaseScreen,StartScreen}` | `Scripts/Testing/SelfTest.cs:80-92`、`:175`（`#if DEBUG`） | 自测用的绝对路径；**只在 DEBUG 构建里生效** | 逻辑 |

**另外**：`UiService` 是按**类型**找界面（`Scripts/UI/UiService.cs:44-58` 递归 `FindInTree<T>`），所以**界面节点名改了不影响 `UiService`**——但上表里的路径会炸。界面必须仍然是 `OverlayScreen` 的子类，否则 `UiService` 找不到（`OpenUpgrades/OpenLoadout/OpenHelp/OpenRewards/OpenChoose/OpenMessage` 全部返回 false，静默失败）。

### 8.4 UI 换成贴图的类型陷阱

- `Root/PlayerBar/{Fill,DelayBar}` **不再强转类型**：代码按名字取节点、只当 `Control` 用（`HealthBarWidget`，`PlayerHud.cs` / `WorldOverlay.cs`）→ 填充条可以自由换成 `TextureRect` / `NinePatchRect` / `ColorRect`。⚠️ 节点名不能改。
- 首领血条的 `Panel/Bar` 仍是 `ProgressBar`（`PlayerHud.PublishBoss` 按名取），填充色写在预制体 `BarBoss.tscn` 的 `theme_override_styles/fill`。
- 卡片模板被 `GetNode<Button>` 强转（`RewardScreen.cs:37` 等）→ 只能保持 `Button`（想换美术就用 `Button` 的 `theme_override_icons` / 自定义 `StyleBox`）。
- `Bars`/`Numbers` 被 `GetNode<Control>` → 保持 `Control` 系即可。
- 界面容器 `Root/Panel/VBox` 的层级：`GetNode<Label>("Root/Panel/VBox/Title")` 是**绝对路径**，中间插一层节点就会炸。
- **本项目没有任何 `Theme` 资源**。要统一换字体/按钮九宫格，建议新建一个 `Theme` 资源挂到 `project.godot` 的 `gui/theme/custom`（改项目设置，不影响上面任何节点路径）。

### 8.5 三个"静默失效"的坑（不报错，但美术效果没了）

1. **`Mesh` 不是单 surface 或不叫 `Mesh`** → 交互物"用掉变暗"完全没反应（`Interactable.cs:105` 的 `GetActiveMaterial(0) is not StandardMaterial3D` 直接 `return`）。
2. **`Mesh` 的材质不是 `StandardMaterial3D`**（比如你自己换成 `ShaderMaterial`）→ 同上，变暗失效。
3. **单位的 mesh 挂在更深层的节点下** → `UnitPresentation` 的受击闪色**会**生效（它是递归的，`UnitPresentation.cs:70-81`），但 `EliteModifier` 的精英染色**不会**（它只看直接子节点，`EliteModifier.cs:55-61`）→ 精英看起来"没被染色"，而且**手里的枪/挂件会跟着闪色但不会被染精英色**（因为枪是 `WeaponMount/Pistol/Body` 这个三级子节点，会被 `UnitPresentation` 扫到）。

> **⚠️ 第二轮实测：这条坑在第 1 条上"踩实了"**。9 敌 + Boss 的 `Model` 换成 `.glb` 实例后，
> 它是 **`Node3D`**（子树里才是 `character-xxx/Skeleton3D/.../MeshInstance3D`），
> 所以 `unit.GetChildren()` 里**一个 `MeshInstance3D` 都没有** → `EliteModifier` 的染色**静默失效**。
> 脚本回读 10/10 全部 `direct_mesh_child = false`。
> **仍然生效的只有 `unit.Scale`（精英还是大 20~35%）**。
> 修法见 [art-integration-log.md](art-integration-log.md) §9.5（1 行：把那段改成递归），**本轮没改 `Scripts/`**。

### 8.6 其它

- **`physics_interpolation=true`**（`project.godot`）。所有可见节点都按渲染帧插值 → 给单位加挂点/特效时不需要自己插值，但**不要把视觉节点做成"每物理帧瞬移"的独立对象**（会重新出现抖动）。
- `CharacterMovement` 在单位死亡时直接 `return`（`:130-133`），所以**死亡后没有倒地/布娃娃表现** —— 目前只是延迟 `DeathLinger` 后 `QueueFree`（`UnitPresentation.cs:99-109`，Boss 是 2.5 秒，其余 1.1 秒）。想加死亡动画需要新代码。
- 交互物被"隔离/未抽中"时是 `Visible=false`（`StageController.cs:255`、`:502`），**不是透明**，所以美术不需要考虑淡出。
- `ChallengerMark` 的默认 `Height=-0.85`（`ChallengerMark.cs:23`）**疑似把三角环埋到地面以下**：单位原点在脚底，环因此在 y=-0.85。`Player.tscn:146-147` 没有覆盖这个值；我这边的旁证是截图 `docs/screenshots/challenger_mark2.png`（图中"敌人 4"说明挑战进行中，但玩家脚下看不到橙色三角环）。**这一点我没做运行时验证，标为"未确认"**——建议替换前先跑一次游戏确认，若确实不可见，先把 `Height` 改成 ~0.05。

---

## 9. 建议的替换顺序（按"收益 / 风险"排）

1. **玩家 `Model`**（`Scenes/Player/Player.tscn`）—— 镜头每帧都在看它；顺手把 §8.2 的轴心/朝向约定一次性定死，后面所有单位照抄。**先做这个**。
2. **9 种敌人 + Boss 的 `Model`** —— 9 个场景、9 行改动、零代码风险；建议先做一张"单位规格表"（高/轴心/朝向/配色）再动手。
3. **奖励节点 + Boss 门**（`Scenes/Interactables/`）—— 出现频率最高的交互物；换完顺手把"按 Kind 上色"补上（当前 10 个实例同色，玩家区分不出池子类型，是**现存的可玩性问题**）。注意另一个代理正在改这两个文件，**先跟他协调**。
4. **`TestArena.tscn` 的地面/柱子/墙/斜坡** —— 一眼灰盒，性价比最高；柱子 3 根共享同一个 mesh+材质，改一处即可。
5. **爆炸 + 子弹 + 新增枪口火光** —— 战斗反馈目前几乎为零（全项目 0 个粒子系统）；`Explosion.tscn` 的基准半径必须保持 1.0。
6. **HUD / 弹窗**（`Scenes/UI/`）—— 纯色 UI 能跑，可以最后做；做的时候优先"改颜色/加 Theme"，避免动节点名和 `ColorRect` 类型（§8.3 / §8.4）。
7. **代码生成的 7 处**（`ChallengerMark` 三角环、交互物"用尽变暗"、受击闪色、精英染色、敌人血条、伤害跳字、Boss 屏幕血条）—— 需要改 C#，建议排在美术资源到位之后，避免反复改码。

## 10. 未确认 / 存疑（不要当成事实用）

| # | 事项 | 状态 |
|---|---|---|
| 1 | `ChallengerMark` 的三角环是否真的被埋在地下 | **静态推断 + 截图旁证**，未做运行时验证（§8.6 最后一条） |
| 2 | 另一个代理外置 `Assets/` 后的最终路径与文件名 | **未确认**：我扫描时 `Assets/` 目录不存在、`docs/assets.md` 不存在 |
| 3 | `Scenes/Interactables/`、`Scenes/Combat/` 的最新内容 | 可能已被并发修改，本文档的行号以我读到的那一版为准 |
| 4 | `navigation` 烘焙为何始终 0 多边形 | 已知问题（`docs/todo-handoff.md:168`），与美术无关，未深查 |
| 5 | 精英 `ModelScale` 与 Boss 体型叠加后的实际视觉比例 | 未截图核对，仅按 `EliteModifier.cs:44` 的 `unit.Scale` 推断 |
