# 三叶旅童 · Clover Wayfarer

2026-10-25，第二版可编辑造型候选：猫头鹰脸形面具与脚踝上方长斗篷。按用户指定的森林孩童低多边形方向，在Blender中直接制作。

## 文件

- `front-back-preview.png`：同一模型的正背面对照，Blender实际渲染。
- `front-three-quarter.png`、`back-three-quarter.png`：单独三分之四视角。
- `clover-wayfarer.glb`：角色本体、骨架与基础待机／行走动作；不包含摄影棚、地台、灯光或相机。
- `model-report.json`：导出结构、三角面、权重与动画检查。
- 可编辑主工程：`../../Blender/clover-wayfarer/clover-wayfarer.blend`。

## 当前造型

保留大头短身、宽兜帽与绿黄、奶白、棕色大色块。面具改为双眉弧、中央浅凹、两侧微尖与下颌收拢的猫头鹰脸轮廓，只有两个贯穿眼孔，没有鼻、喙、嘴或花纹。面具后面没有头球、眼球等填充形体，黑绿色来自兜帽本身的薄布内衬。

斗篷延长到脚踝上方，只保留双面可见的布面，去掉隐藏躯干、裤腿和袖子体块，保留外露手套与短靴。下摆高低不等，使用疏密不同的纵向褶皱，平均高度约0.235米、起伏范围约0.180至0.286米。背后没有包或装备，三个心形叶片与一根茎组成的三叶草图案贴合背部布面。

4108个三角面、22个可编辑网格对象、12个导出材质、21骨；三片斗篷骨延伸到新的下摆。GLB约504KiB，基础idle为2秒、walk为1.2秒。导出权重归一化误差小于0.000001，已检查空眼孔与面具封闭拓扑，GLB结构与Blender回读结果一致。

这一版用于确认比例、形体与配色。骨架及两个动作是基础草稿，未做战斗动作、布料碰撞或正式Godot接入验收。身高和朝向等最终使用尺寸可在确认后按项目角色档案调整。

第一版源工程、导出与预览已保存在 `../../Revisions/clover-wayfarer/v01-round-mask/`，可随时对照。

## 风格参考

- ORICUT：[Forest Spirit Nori](https://oricutstudios.itch.io/low-poly-forest-spirit-nori)
- ORICUT：[Campfire Scout Niko](https://oricutstudios.itch.io/low-poly-campfire-scout-niko)

参考公开展示中的头身比例、低面数大形体、兜帽与简洁色块。制作工具为 `../../Tools/build_clover_wayfarer.py`，正背面展示脚本为 `../../Tools/preview_clover_wayfarer.py`，导出使用 `../../Tools/export_clover_wayfarer.py`；均可直接作为Blender脚本执行。MCP安全模式下直接提交脚本内容，保存与导出使用Blender自带操作。
