# 胆小菇形敌人：首版 3D 样件

2026-10-21。对应 E10／`enemy_burrow_hunter`，按用户选定的第三版胆小菇参考图生成。

- 混元 3.0 LowPoly 单图生成，任务 `1504328840385544192`；服务返回消耗 **25 积分**，没有重复提交。
- 原始模型为 9,162 三角面；Blender 补回六处圆形菌盖斑点后，最终 **9,594 三角面／5,869 源顶点**。
- 归一化身高 **1.55 米**；运行时使用 2K 贴图，原始 4K 图和模型保留在 ArtSource。
- 非人形 **4 骨**：Root／Stem／Head／Cap。自制 `idle`、`burrow`、`emerge`、`shoot` 四段动作，60 fps 导入。
- 缩地时压缩菌柄、保留头部与菌盖体积，再沉入地下；出土反向复位。

原始下载与费用记录：`ArtSource/Generated/scaredy-shroom-v1/generation.json`。

可编辑源：`ArtSource/Blender/scaredy-shroom/scaredy-shroom.blend`，场景 `ScaredyShroom_Hunyuan_v1`。
源文件是从当前 Blender 安全另存的副本，保留其他已打开的场景；GLB 仅导出本角色。

整理脚本：`ArtSource/Tools/prepare_scaredy_shroom.py`，通过 Blender MCP 直接执行脚本正文。

运行时模型：本目录 `scaredy_shroom.glb`。可复用外观场景：`Scenes/Enemies/Art/ScaredyShroomVisual.tscn`。

森林美术样板额外使用 `Assets/Materials/EnemyAnime/` 的动漫三渲二上色，重新整理菌盖与菌柄底色，F12 可对比原材质。GLB 原有贴图及源文件保留。

目前是外观与动作样件，未替换生产敌人场景、追踪弹或遁地换位 AI。森林样板内按 F10 切换镜头，F11 演示缩地与出土。
