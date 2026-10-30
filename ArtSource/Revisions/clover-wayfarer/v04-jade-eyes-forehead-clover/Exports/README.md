# 三叶旅童 · Clover Wayfarer

2026-10-25，第四版可编辑造型候选：在木板猫头鹰面具上增加碧绿色小眼睛与同色额头三叶草。在Blender中直接制作，保留无手造型、下垂兜帽、围巾式领围、长斗篷、短靴与背面三叶草。

## 文件

- `front-back-preview.png`：同一模型的正背面对照，Blender实际渲染。
- `front-three-quarter.png`、`back-three-quarter.png`：单独三分之四视角。
- `head-detail.png`：碧绿小眼睛、额头三叶草、木板面具与领围特写。
- `clover-wayfarer.glb`：角色本体、骨架与基础待机／行走动作；不包含摄影棚、地台、灯光或相机。
- `model-report.json`：导出结构、三角面、权重与动画检查。
- 可编辑主工程：`../../Blender/clover-wayfarer/clover-wayfarer.blend`。

## 当前造型

按用户标注的轮廓制作明显上挑的两侧尖角、宽直脸颊与尖下颌，面具使用暖木色大色块。平面木板具有实际厚度、外缘倒角、八边形雕刻眼孔和四条浅凿痕。两个深色眼孔内增加小巧的竖向碧绿眼睛，额头贴一枚三心叶加短茎的三叶草，两处共享 `CW_Jade` 材质，基础色为 `#2bc98e`，跟随Head骨骼。中央凿痕向旁边挪开以完整呈现图案，没有鼻、喙、嘴或实体头部填块。

删除全部手套、拇指、袖口以及不再使用的手臂骨骼，仅保留短靴。兜帽从球形改为上窄下垂的布罩，两侧布角落到领口，后部使用纵向布褶闭合，避免束袋状收结；领口加入开放布带组成的V形叠褶领围，不填充头部或身体空腔。

长斗篷继续延伸到脚踝上方，只保留双面可见布面。下摆高低不等，平均高度约0.235米、起伏范围约0.180至0.286米。背后没有包或装备，三个心形叶片与一根茎组成的三叶草图案贴合背部布面。

3378个三角面、19个可编辑网格对象、14个导出材质、15骨，含独立斗篷骨。本次仅增加94个三角面，两只眼睛合为一个网格、额头图案为一个网格。GLB约428KiB，基础idle为2秒、walk为1.2秒。导出权重归一化误差小于0.000001，已检查贯穿眼孔、面具封闭拓扑、同色材质和Head绑定，GLB结构与Blender回读结果一致。

这一版用于确认比例、形体与配色。骨架及两个动作是基础草稿，未做战斗动作、布料碰撞或正式Godot接入验收。身高和朝向等最终使用尺寸可在确认后按项目角色档案调整。

第一版源工程、导出与预览已保存在 `../../Revisions/clover-wayfarer/v01-round-mask/`，可随时对照。
第二版保存在 `../../Revisions/clover-wayfarer/v02-owl-mask-long-cloak/`。本次用户标注参考保存在 `../../Concepts/clover-wayfarer/references/user-owl-outline-v03.png`。
未加碧绿眼睛与额头图案的第三版保存在 `../../Revisions/clover-wayfarer/v03-carved-wood-owl-draped-hood/`。

## 风格参考

- ORICUT：[Forest Spirit Nori](https://oricutstudios.itch.io/low-poly-forest-spirit-nori)
- ORICUT：[Campfire Scout Niko](https://oricutstudios.itch.io/low-poly-campfire-scout-niko)

参考公开展示中的头身比例、低面数大形体、兜帽与简洁色块。制作工具为 `../../Tools/build_clover_wayfarer.py`，正背面展示脚本为 `../../Tools/preview_clover_wayfarer.py`，导出使用 `../../Tools/export_clover_wayfarer.py`；均可直接作为Blender脚本执行。MCP安全模式下直接提交脚本内容，保存与导出使用Blender自带操作。
