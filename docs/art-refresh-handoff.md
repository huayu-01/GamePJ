# 画面改善专项交接

本轮以 `d4270fc` 为视觉对比基线，工作范围仅美术和独立验证。公共 HANDOFF、roadmap、architecture、todo-handoff 不修改，由功能任务整合。

## 已接入生产场景

- `Scenes/Levels/TestArena.tscn` 与 `Base.tscn` 共用 `Assets/ArtRefresh/env_level_garden.tres`：暖阳、青蓝环境光、轻雾、克制的 glow、SSAO。
- 竞技场近白科幻地面/墙柱视觉替换为灰绿石地、八面石柱与石墙；中央错缝石砖、浅色路径、外围草地分区。
  **7个原碰撞形状、尺寸、变换、物理层及导航设置保持**。斜坡和底座视觉现在对齐已有物理外形。
- 基地补天空、阴影、环境远景与终端基座；地面可见顶面从原来的0.5修正至碰撞顶面0。
  原 `surface_material_override/0` 没生效，改为有效 `material_override`，恢复四终端原本配置的四色，未改 Kind/交互/提示/尺寸。
- 两处实例化 `Scenes/ArtRefresh/GardenDressing.tscn`：自建低模橡树/松树、岩石、山峰与云。装饰只有 Node3D/MeshInstance3D，不创建碰撞/Unit/分组。
  导航生产脚本只解析 StaticColliders，装饰不会参与烘焙。基地使用岩柱支撑的边缘林景，竞技场树在内侧边缘，中央留空。
- Hit/Explosion/Muzzle 的 ShaderMaterial、ParticleProcessMaterial 全部按实例隔离；共享控制脚本只在 Ready autoplay 一次，避免 EnterTree/Ready 双启动。
  爆炸发光4→2.2是纯表现参数；没有改变 `Explosion.Lifetime`、半径、伤害或冷却。原脱离宿主自毁外壳保持。

## 新预制体与精确接线位置

`FxLootBeacon.tscn` 和 `FxBattleSwing.tscn` 已独立实例化和实拍验证，**本轮未修改保留的玩法/技能/UI文件进行接线**。

| 预制体 | 由功能任务整合的位置与契约 |
|---|---|
| `Scenes/ArtRefresh/FxLootBeacon.tscn` | 若用于奖励节点：在 `Scenes/Interactables/RewardNode.tscn` 根节点下新增纯视觉子节点 `LootBeacon`，局部y≈0.05。它是循环表现，应随节点一起退树；用现有奖励价值色设置实例 `primary_color/secondary_color/light_color`，并随原节点消耗态隐藏。不要替换原 Mesh、Aura 或改变拾取判定。现有Aura已经承担价值提示，整合时需二选一/减弱，避免堆叠光圈 |
| `Scenes/ArtRefresh/FxBattleSwing.tscn` | 在真正需要剑弧的武技表现节点下实例化；仅在现有动画事件或只读事实上升沿播放，不能用动画时长驱动伤害。一次性世界效果挂 CurrentScene，动画结束 QueueFree；如果宿主是短寿命投射物，沿用 `fx_detach_lifetime.gd` 外壳。场景默认one_shot/autoplay，主动画长度由AnimationPlayer回读。不要附碰撞、Unit或单位组 |

不需要为了这两项接线修改 ConfigIndex 或新增数值列。本轮未触碰 Ability、Unit、RunController、技能行、效果行与 HUD。

## 替换与风格扩展

环境材质/天空均为独立资源；改颜色不需要碰玩法。`GardenDressing` 导出布局、颜色、树高/数量/外圈、岩石/云参数和 `tree_scene:PackedScene`。
后续换正式树模型时，把无碰撞、无单位脚本的表现预制体赋给 tree_scene；先审材质、依赖与许可，继承本轮安全探针。

`Scenes/ArtRefresh/OptionalPixelFilter.tscn` 是独立 CanvasLayer + 屏幕采样 shader，**生产基地/Main/竞技场均默认不挂**。
需要像素风时放在世界层之后、HUD之前（layer=1；生产Hud层更高），移除即恢复原画面。材质 `pixel_size` 控制采样格，1为原分辨率。
它不改分辨率设置、模型、物理、输入或玩法；ColorRect mouse_filter=Ignore。截图只演示这条可插拔路线，不是默认风格切换功能。

## 验收入口

`Assets/ArtRefresh/tools/verify_art_refresh.ps1 -SkipImport -Before -ScopedSelfTest`：每个构建/运行均独占 `.ai_validation.lock`，进程结束立即释放；只退出自己启动的进程。
`-Before` 从 d4270fc 临时读取两处原场景，同机位拍前后对比；资源首次导入应由现有 Godot 编辑器完成，避免另起编辑器抢占9080。
脚本日志 `.ai_art_*` 留本地，交付证据见 `docs/art-refresh-evidence.log`，图片为 `docs/screenshots/art_refresh_*.png`。
完整自测使用 `-- --selftest`，未创建flag，保留用户存档并核对SHA-256。
`ScopedSelfTest` 仅隐藏并行B批技能栏绘制；未隔离的Main曾出现AbilityHud三角化错误，完整经过与边界见 `art-refresh-verification.md`。

## 仍需注意

- 这轮建立统一的卡通/低多面体方向，角色、敌人和四个终端主体仍是既有资产/基础几何，尚不是终版美术。
- Toon Trees Lite 没有加入仓库；其源码公开限制是真实包内许可要求，不能只看网页“免费商用”就上传。
- 已知导航空网格/cell_height提示，以及完整自测退出的旧资源泄漏提示需保留记录；P4原有落剑/长枪期望偏差不在本轮修复范围。
- 正式Main取证会显示共享工作区当时的HUD；技能HUD/GM属于并行B批，不计为本专项实现。
