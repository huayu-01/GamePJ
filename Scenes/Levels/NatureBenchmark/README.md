# 林间美术样板

入口：`res://Scenes/Debug/NatureArtLab.tscn`。在 Godot 打开后按 F6，或双击 `ArtSource/Tools/Start-NatureArtLab.cmd`。

这是用免费 Standard 素材和项目自制材质搭建的美术标准样板。可玩区域约 30×30 米，外围 80×80 米地形和树林用于建立远景。采用林间空地、弯曲土路、岩石坡地的构图，优先检查实际游戏镜头下的色彩、层次和战斗可读性。

## 操作与角色

新增重击敌人几何样件，放在道路前方。F7 切换展示／实战，F8 播放抡锤动作，F9 切换近景；F6 重置。模型、骨骼、动画与接入说明见 `Assets/Enemies/StoneSlammer/README.md`。

样板导航按 3 米高度、0.75 米半径烘焙，入口 MapManager 使用相同参数，容纳约 2.96 米高的石人并直接复用预烘焙网格。

WASD 移动，空格跳跃，左键双段剑击，F5 生成五名追击敌人，F6 重置，Tab 总览，Esc 释放／捕获鼠标。

使用已生成的人形、UAL 动作和 KayKit 测试剑，复用原战斗测试入口的移动、动画事件和伤害逻辑。默认隐藏木桩，方便观察环境；F5 仍可进行真实战斗。小场地的刷怪半径为 7–12 米，额外检查刷怪点与玩家之间的导航连通性，避开坡地上的孤立导航面。原 120 米测试场另存于 `NatureArena` 目录。

## 可维护位置

| 位置 | 用途 |
|---|---|
| `NatureBenchmark.tscn / Terrain` | 起伏地形及与网格匹配的静态碰撞 |
| `Trees`、`RockGroups`、`Understory` | 主景树、岩石和灌木，可单独移动 |
| `FoliageChunks` | 每八米一区块的 MultiMesh 草与小装饰 |
| `CombatMarkers` | 出生点、中心和四个通路验收点 |
| `ArtViews / Hero、Trail` | 两个固定美术观察机位 |
| `WorldBounds`、`Navigation` | 样板可玩范围和已烘焙导航 |
| `ArtLighting`、`WarmAfternoonSun` | 世界环境和暖色主光 |
| `Assets/Environment/NatureBenchmark/Materials/` | 样板专用材质与环境资源 |

自制 shader 位于 `Assets/Environment/NatureBenchmark/Shaders/`：

- `foliage.gdshader`：树冠整体法线、上下颜色层次、柔和明暗及叶片风动。花草的 Leaves 材质保留原贴图颜色。
- `grass.gdshader`：草根到草尖渐变、大片颜色变化和风动。
- `ground.gdshader`：与草地一致的地色、弯曲土路、空地、细碎纹理和不规则边缘。
- `sky.gdshader`：蓝色天空与连续噪声云层。

光照为实时主光、环境补光、轻微远景雾和接触遮蔽。样板入口单独启用四倍 MSAA 与 TAA。细草不投射单独阴影，按区块设置可见距离，便于后续扩大场景时调整密度与性能。

## 重建与编辑

布局和生成逻辑集中在 `ArtSource/Tools/NatureArena/build_benchmark.gd`：

- `elevation()` 控制坡地；视觉和碰撞共用同一函数。
- `road_x()` 与地表 shader 的道路公式一致，修改路线时两处同步。
- `vegetation()` 开头的坐标表控制主景树和岩石，其余植被由固定种子生成。
- `surface_material()` 控制不同植物和石头的配色；shader 参数也能直接在材质 Inspector 中调整。
- `lighting()` 控制天空、雾、环境光及太阳。

通过 Godot MCP 的 `execute_editor_script` 执行：

```gdscript
var builder = ResourceLoader.load("res://ArtSource/Tools/NatureArena/build_benchmark.gd", "GDScript", ResourceLoader.CACHE_MODE_IGNORE).new()
_custom_print(builder.build())
EditorInterface.get_resource_filesystem().scan()
```

生成时使用正常图形模式的 Godot 编辑器；无图形后端不会保存 MultiMesh 实例缓冲，脚本对此做了检查。场景生成后可正常使用 headless 模式验收战斗。

重建会覆盖样板场景与生成的材质。长期保留的手工布景请同步到生成脚本；移动树石或修改地形后，重新烘焙并保存导航。

## 验收与截图

命令行在场景后增加 `-- --nature-smoke`，会验证移动、跳跃、双段命中、四向通路、边界和五名敌人追踪，结果保存到 `docs/screenshots/nature_benchmark_validation.json`。

正常图形运行时增加以下参数可自动截图后退出：

- `-- --nature-capture=Hero`：固定观察机位，隐藏开发 HUD。
- `-- --nature-capture=follow`：实际游戏跟随镜头，保留 HUD。
- `-- --nature-capture=overview`：总览布局。

截图保存为 `docs/screenshots/nature_benchmark_*.png`，是实际 Godot 渲染输出。

原模型来自 Quaternius Stylized Nature MegaKit Standard（CC0），原许可和来源记录保留在 `Assets/Environment/StylizedNature/`。样板的 shader 是项目内自行编写的实现。下一阶段应先确认这块样板的画面，再将材质与布景规则推广到大地图，并针对目标设备测量性能。
