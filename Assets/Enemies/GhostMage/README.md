# 黑圆头小法师：首版 3D 样件

2026-10-21。对应 E13／`enemy_meteor_mage`，按用户认可的黑圆头、紫帽、黑色幽灵身体及小法杖定稿制作。

- 使用 **Blender MCP 直接建模**，没有调用混元、Tripo 或付费 3D 生成；本模型生成积分 **0**。
- **4,584 三角面／2,312 源顶点／6 个简单材质**，无贴图。
- 非人形 **7 骨**：Root／Body／Tail／Head／Hat／Grip／Staff；自制 `idle` 漂浮与 `cast` 悬浮法杖施法动作，60 fps 导入。Grip 仅是法杖动画枢轴，不对应可见的手。
- 保留圆头、等大的暖色眼睛、紫色法师帽、圆润黑色上身与向下收尖的幽灵尾部。没有腿脚或南瓜。
- 按用户圈图要求，已删除手掌和短手臂，法杖独立悬浮在身旁。法杖杆与杖头圆珠可在源工程单独编辑。原始根节点到帽顶约 2.12 米；外观预制体统一缩放 0.82，帽顶约离地 1.74 米。

可编辑源：`ArtSource/Blender/ghost-mage/ghost-mage.blend`，场景 `GhostMage_Approved_v1`。
源文件是从当前 Blender 安全另存的副本，保留其他已打开的场景；GLB 仅导出本角色。

重建脚本：`ArtSource/Tools/build_ghost_mage.py`，通过 Blender MCP 直接执行脚本正文。

运行时模型：本目录 `ghost_mage.glb`。可复用外观场景：`Scenes/Enemies/Art/GhostMageVisual.tscn`。

森林美术样板额外使用 `Assets/Materials/EnemyAnime/` 的动漫三渲二上色，F12 可对比原材质。漆黑主体与无手造型保留；GLB 内的基础材质未被覆写。

2026-10-29：已通过 `Config/VisualProfiles/ghost_mage.tres` 接入正式 `enemy_meteor_mage`，沿用原落弹技能、预警与地面导航；运行时直接引用GLB，不携带样板预览脚本。当前仅映射已有idle，施法动作接线按用户要求暂缓。森林样板内按 F10 切换镜头，F11 演示悬浮法杖施法；悬浮只属于外观表现，不新增飞行导航。
