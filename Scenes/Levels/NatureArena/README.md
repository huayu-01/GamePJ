# 林间战斗场

环境预制：`NatureArena.tscn`，尺寸 120×120 米。战斗测试入口：`res://Scenes/Debug/NatureCombatLab.tscn`。
在 Godot 打开测试入口并按 F6，或双击项目 `ArtSource/Tools/Start-NatureArena.cmd`。

## 操作

WASD 移动，空格跳跃，左键双段剑击，F5 生成五名追击敌人，F6 重置，Tab 切换总览，Esc 释放／捕获鼠标。
场内有两个固定木桩。测试入口复用项目的 PlayerController、CharacterMovement、UnitAnimator、MeleeComboAbility 和真实伤害判定。

## 场景维护

| 位置 | 用途 |
|---|---|
| `NatureArena.tscn / Terrain` | 120 米地面、地面碰撞 |
| `Sectors/NW.tscn` 等四个分区 | 单独编辑、替换局部布置 |
| `Prefabs/` | 29 个使用中的资源预制；树干圆柱和岩石盒体代理碰撞 |
| `NatureArena.tscn / WorldBounds` | 四面场地边界 |
| `NatureArena.tscn / CombatMarkers` | 玩家起点、中心及四个外围战斗点 |
| `NatureArena.tscn / Navigation` | 已烘焙导航资源 |
| `NatureArena.tscn / Lighting、Sun` | 天空、雾与主光照 |

中央战斗区直径约 40 米，内设四处岩石掩体；中心半径 10 米保持空旷。
外围有半径 34 米的环路、十字连接路及四个战斗点。共 1,043 处布置：119 棵树、24 处岩石、65 处灌木、815 处低植被、20 处踏石。
815 处低植被合并为 24 个 MultiMesh 批次；树和岩石保留单独实例，便于移动和更换。

移动树、岩石或边界后，选中 `Navigation` 重新烘焙，并保存导航资源和场景。
运行时 MapManager 优先复用该导航资源；需要临时重建时可调用其 `Rebuild()`。
原始模型的材质调整集中在 `Assets/Environment/StylizedNature/Materials/`。
地表环路和空地由 `arena_ground.gdshader` 生成，不增加地形碰撞复杂度。

## 新角色与武器

角色预制：`res://Scenes/Player/GeneratedPilot.tscn`。
模型：`res://Assets/Characters/GeneratedPilot/pilot_ual.glb`，沿用混元样件的 23 骨，烘焙了 11 段 UAL 动作。
测试入口使用待机、走跑、跳跃、死亡和两段剑击。剑击分别在 0.18 秒和 0.21 秒的 ATK 帧触发真实判定。
双段参数位于 `res://Config/Testing/nature_pilot_combo.tres`，不改正式角色或技能表。

测试剑预制：`res://Scenes/Weapons/Visuals/NaturePilotSword.tscn`。
测试场根节点的 `PilotWeapon`、`PilotWeaponRotation`、`PilotWeaponScale` 可在 Inspector 调整。
当前样件仍没有独立手指骨骼，握持细节和专属战斗动作属于后续精修。

自然资源来自 Quaternius Stylized Nature MegaKit Standard，导入全部 68 个模型，包内标注 CC0。
武器来自 KayKit Fantasy Weapons Bits FREE 1.0，导入 31 个模型，包内标注 CC0；当前挂载 sword_A。
作者页面、版本、压缩包哈希和原许可分别保存在对应 Assets 目录下的 source.json 与许可文件中。

## 布局与复现

最终场景通过项目自带 Godot MCP Native 的编辑器脚本工具生成、保存并检查。
Blender MCP 最初的布局草案和对应 JSON 保存在 `ArtSource/Blender/NatureArena/`。
`layout.json` 是布局数据源；`ArtSource/Tools/NatureArena/build_arena.gd` 在 Godot 编辑器中生成预制、材质、碰撞与导航。
需要批量重建时，在 Godot MCP 的 execute_editor_script 中运行：

```gdscript
var builder = load("res://ArtSource/Tools/NatureArena/build_arena.gd").new()
_custom_print(builder.build())
```

重建会按 JSON 重新保存生成的分区和预制；手工布置的调整应同步到布局数据中。
角色烘焙脚本为 `ArtSource/Tools/NatureArena/bake_pilot.gd`，独立运行项目为同目录的 `BakeProject/`。

## 验收

`docs/screenshots/nature_arena_validation.json` 记录实际移动、跳跃、边界碰撞、四向通路、五名敌人追踪和动画事件命中结果。
`nature_arena_overview.png` 和 `nature_arena_player.png` 为真实 Godot 运行截图。
本次新增专用测试场景和资源，主场景入口、正式角色配装及既有战斗系统保持当前设置。
