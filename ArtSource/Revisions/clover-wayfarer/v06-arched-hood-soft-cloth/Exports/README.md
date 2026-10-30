# 三叶旅童 · Clover Wayfarer

2026-10-25，第六版可编辑造型候选：重新对照Nori公开侧面展示与线框图，校正帽顶弧线，并重做整件长斗篷的垂褶、自由边和布面明暗。在Blender中直接制作，保持简洁低多边形方向。

## 文件

- `front-back-preview.png`：同一模型的正背面对照，Blender实际渲染。
- `front-three-quarter.png`、`back-three-quarter.png`：单独三分之四视角。
- `head-detail.png`：碧绿小眼睛、额头三叶草、木板面具与领围特写。
- `head-side-profile.png`：正侧面帽顶轮廓。
- `head-front-side-preview.png`：同一模型的正面／正侧面头部对照，Blender直接渲染。
- `clover-wayfarer.glb`：角色本体、骨架与基础待机／行走动作；不包含摄影棚、地台、灯光或相机。
- `model-report.json`：导出结构、三角面、权重与动画检查。
- 可编辑主工程：`../../Blender/clover-wayfarer/clover-wayfarer.blend`。

## 当前造型

按用户标注的轮廓制作明显上挑的两侧尖角、宽直脸颊与尖下颌，面具使用暖木色大色块。平面木板具有实际厚度、外缘倒角、八边形雕刻眼孔和四条浅凿痕。两个深色眼孔内增加小巧的竖向碧绿眼睛，额头贴一枚三心叶加短茎的三叶草，两处共享 `CW_Jade` 材质，基础色为 `#2bc98e`，跟随Head骨骼。中央凿痕向旁边挪开以完整呈现图案，没有鼻、喙、嘴或实体头部填块。

角色没有手或手臂骨骼，保留短靴。帽顶前沿约1.765米，略低于木面具额头中央上缘约1.785米；后帽顶升至约1.870米，再沿后脑回落，形成侧面的拱弧。帽侧下垂布角收成自然的布边，领围改为略不对称的叠褶，不填充头部或身体空腔。

整件斗篷从规则锥形改为包住肩部后松垂的布片：褶皱宽窄、深浅和走向不同，颈部附近有牵拉褶，前襟随长度自然松开，下摆有轻微翻折。移除贯穿全身的亮色竖条和粗亮压边，主要依靠形体与柔和布面明暗表达材料；木面具继续保留清楚的棱角。斗篷仍到脚踝上方，平均下摆约0.245米，高低范围约0.195至0.285米，没有新增包或装备。

背面三叶草按每片布面三角形精确裁分、贴合褶皱，修正深褶处图案穿插，并减少原先冗余的细分。当前2742个三角面、20个网格、12个导出材质、15骨，GLB约256KiB。基础idle为2秒、walk为1.2秒，权重归一化误差小于0.000001；导出结构与Blender回读一致。木面具、碧绿眼睛、额头图案和六个靴部网格的位置与三角索引均与第五版一致。

这一版用于确认比例、形体与配色。骨架及两个动作是基础草稿，未做战斗动作、布料碰撞或正式Godot接入验收。身高和朝向等最终使用尺寸可在确认后按项目角色档案调整。

第一版源工程、导出与预览已保存在 `../../Revisions/clover-wayfarer/v01-round-mask/`，可随时对照。
第二版保存在 `../../Revisions/clover-wayfarer/v02-owl-mask-long-cloak/`。本次用户标注参考保存在 `../../Concepts/clover-wayfarer/references/user-owl-outline-v03.png`。
未加碧绿眼睛与额头图案的第三版保存在 `../../Revisions/clover-wayfarer/v03-carved-wood-owl-draped-hood/`。
收小兜帽前的第四版保存在 `../../Revisions/clover-wayfarer/v04-jade-eyes-forehead-clover/`。
整体压得过低的第五版保存在 `../../Revisions/clover-wayfarer/v05-smaller-upper-hood/`。

## 风格参考

- ORICUT：[Forest Spirit Nori](https://oricutstudios.itch.io/low-poly-forest-spirit-nori)
- ORICUT：[Campfire Scout Niko](https://oricutstudios.itch.io/low-poly-campfire-scout-niko)

参考公开展示中的头身比例、低面数大形体、帽顶纵向弧线与布片自由边。侧面与线框参考检查图保存在 `../../Concepts/clover-wayfarer/references/nori-gallery-review.jpg`，原始公开图链接保留在同目录的 `sources.json`。制作工具为 `../../Tools/build_clover_wayfarer.py`，正背面展示脚本为 `../../Tools/preview_clover_wayfarer.py`，导出使用 `../../Tools/export_clover_wayfarer.py`；均可直接作为Blender脚本执行。MCP安全模式下直接提交脚本内容，保存与导出使用Blender自带操作。
