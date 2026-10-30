# 三叶旅童 · Clover Wayfarer

2026-10-25，第一版可编辑造型候选。按用户指定的森林孩童低多边形方向，在Blender中直接制作。

## 文件

- `front-back-preview.png`：同一模型的正背面对照，Blender实际渲染。
- `front-three-quarter.png`、`back-three-quarter.png`：单独三分之四视角。
- `clover-wayfarer.glb`：角色本体、骨架与基础待机／行走动作；不包含摄影棚、地台、灯光或相机。
- `model-report.json`：导出结构、三角面、权重与动画检查。
- 可编辑主工程：`../../Blender/clover-wayfarer/clover-wayfarer.blend`。

## 当前造型

大头短身、宽兜帽、开襟斗篷、圆润奶白色面具和深色圆眼孔。绿黄与棕色组成主要色块，背后没有包或装备，斗篷背面是三个心形叶片与一根茎组成的三叶草图案。

4430个三角面、32个可编辑网格对象、16个材质、21骨；骨架含独立斗篷骨。GLB约514KiB，基础idle为2秒、walk为1.2秒。导出权重归一化误差小于0.000001。面具眼孔为实际贯穿几何，三叶草贴合斗篷表面，没有额外背部体积。

这一版用于确认比例、形体与配色。骨架及两个动作是基础草稿，未做战斗动作、布料碰撞或正式Godot接入验收。身高和朝向等最终使用尺寸可在确认后按项目角色档案调整。

## 风格参考

- ORICUT：[Forest Spirit Nori](https://oricutstudios.itch.io/low-poly-forest-spirit-nori)
- ORICUT：[Campfire Scout Niko](https://oricutstudios.itch.io/low-poly-campfire-scout-niko)

参考公开展示中的头身比例、低面数大形体、兜帽与简洁色块。制作工具为 `../../Tools/build_clover_wayfarer.py`，正背面展示脚本为 `../../Tools/preview_clover_wayfarer.py`，导出使用 `../../Tools/export_clover_wayfarer.py`；均可直接作为Blender脚本执行。MCP安全模式下直接提交脚本内容，保存与导出使用Blender自带操作。
