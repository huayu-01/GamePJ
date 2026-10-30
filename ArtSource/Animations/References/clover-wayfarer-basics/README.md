# 三叶旅童：首批基础动作参考

用户筛选后，已将待机／走／跑／受击／死亡／跳跃[试绑到当前模型并制作媒体](../../clover-wayfarer/basic-trial/README.md)。排除像跳跑的慢跑、长蓄力起跳与击倒式击退；本页12段源参考保留用于对照，其余特项暂缓。

2026-10-25。为当前圆墩孩童、木面具、长斗篷角色整理。完整人形源动作可以包含手部，不以有无手部动作作为筛选条件。当前角色造型和动作文件没有被这批参考替换。

## 直接观看

- [可播放参考册](index.html)：12段独立视频，附用途与改编提示；可在浏览器打开。
- [原速动作合集](basic-motion-reference-reel.mp4)：约半分钟，依次观看全部候选。
- [动作总览图](motion-reference-overview.jpg)：按编号查找片段。
- [逐段来源、时长与合集起点](selection.json)。

本地媒体是从已有Quaternius Standard动画库实际渲染的人形源动作，包含完整手臂和手部。MP4动作阶段按原速播放，循环重复两轮，一次性动作首尾留短暂停顿；GIF为0.5×慢放。尚未重定向到三叶旅童。

## 首批12段

| 编号 | 动作与慢放预览 | 源片段 | 用于当前角色时重点观察 |
|---|---|---|---|
| 01 | [待机](media/01-idle.gif) | UAL1 · `Idle_Loop` | 呼吸、轻微重心变化；适合基础待机。 |
| 02 | [走路](media/02-walk.gif) | UAL1 · `Walk_Loop` | 主参考：脚掌落点、髋部节奏；短腿角色缩短步幅。 |
| 03 | [慢跑](media/03-jog.gif) | UAL1 · `Jog_Fwd_Loop` | 主参考：较轻的上下起伏和小幅前倾。 |
| 04 | [快跑](media/04-sprint.gif) | UAL1 · `Sprint_Loop` | 快速移动候选；调整为短腿步幅，收小原动作前倾。 |
| 05 | [起跳](media/05-jump-start.gif) | UAL1 · `Jump_Start` | 压低、蹬地、伸展的先后关系。 |
| 06 | [空中循环](media/06-air-loop.gif) | UAL1 · `Jump_Loop` | 空中重心、腿部收放；不是专门的下落片段。 |
| 07 | [落地](media/07-land.gif) | UAL1 · `Jump_Land` | 屈膝缓冲与回稳，斗篷应迟于身体回落。 |
| 08 | [轻受击](media/08-hit.gif) | UAL1 · `Hit_Chest` | 短促的躯干反应，参考胸腹与头部节奏。 |
| 09 | [击退反应](media/09-knockback.gif) | UAL2 · `Hit_Knockback` | 较大的重心后移和恢复。 |
| 10 | [死亡](media/10-death.gif) | UAL1 · `Death01` | 失衡、倒地与停住的节奏；最终需设计面具和斗篷的落地形态。 |
| 11 | [翻滚](media/11-roll.gif) | UAL1 · `Roll` | 闪避备选，长斗篷需另外处理穿插与回摆。 |
| 12 | [基础施法](media/12-cast.gif) | UAL1 · `Spell_Simple_Shoot` | 保留完整人形源动作作参考，提取躯干发力与释放节奏。 |

优先先看02走路、03慢跑、05起跳、07落地、08受击。这几段能较快确定小个子角色的动作节奏；04快跑、09击退、11翻滚幅度较大，可作为后续动作幅度的备选。

## 其他风格参考与作者媒体

| 来源 | 作者页面／媒体 | 参考用途 |
|---|---|---|
| ORICUT · Nori | [资产页](https://oricutstudios.itch.io/low-poly-forest-spirit-nori) · [作者视频](https://www.youtube.com/watch?v=nb_wj9aTnOM) | 与当前角色比例、面具和斗篷最接近。作者列有走、跑、跳、转向、鞠躬、摇头、点头等；用于整体性格和布料跟随的风格对照。 |
| KayKit Character Animations | [作者页面](https://kaylousberg.itch.io/kaykit-character-animations) · [作者视频](https://www.youtube.com/watch?v=zSzzEmdIiXI) | 卡通动作方向，包含通用反应、移动、闪避与战斗分类，可补充比较重心和夸张幅度。 |
| Quaternius UAL1 | [作者页面](https://quaternius.itch.io/universal-animation-library) · [作者视频](https://www.youtube.com/watch?v=-VXFlXhvD6A) | 作为本批大部分基础人形动作来源。 |
| Quaternius UAL2 | [作者页面](https://quaternius.itch.io/universal-animation-library-2) · [作者视频](https://www.youtube.com/watch?v=2Hd5nH122OE) | 补充击退、闪避、近战、重击等方向。 |

四个视频链接均取自作者页面的嵌入视频；媒体站点能否播放取决于当前网络。来源与版本范围另见[sources.json](sources.json)。

## 适配判断与后续缺口

- 当前角色腿短、脚掌大、躯干宽。先学髋部节奏、身体压缩伸展和落脚顺序，再按比例缩短步幅，不能把成人幅度直接当成最终效果。
- 手部动作不影响挑选源参考；也不需要因此给当前角色添加可见双手。
- 源库没有当前长斗篷的骨骼与形变。斗篷的起步拖后、急停回摆、跳跃上掀和落地回落需要单独制作。
- 本批没有把 `Jump_Loop` 叫作专用下落动作，也没有用摇头动作冒充转身。独立下落、急停、转向仍待补充合适的示例。
- 本地UAL1／UAL2各43个条目（含参考姿势），与作者完整版本标注的120+／130+不是同一版本范围。本批只用本地确认存在的片段。

## 文件与许可

本地源文件为 `../../UAL/Source/UAL1_Standard.glb`、`../../UAL/Source/UAL2_Standard.glb`，沿用已有[兼容性记录](../../UAL/compatibility.md)。两套源库的本地许可副本为[UAL1许可](../../UAL/UAL1-License.txt)、[UAL2许可](../../UAL/UAL2-License.txt)，标注CC0。KayKit作者页也标注CC0；Nori本次只引用公开展示，没有获取或使用其付费模型与动画文件。

渲染工具：[build_clover_animation_references.py](../../../Tools/build_clover_animation_references.py)。媒体整理工具：[package_clover_animation_references.py](../../../Tools/package_clover_animation_references.py)。独立后台Blender渲染使用源人形，不改动用户正在编辑的模型或生产资源。
