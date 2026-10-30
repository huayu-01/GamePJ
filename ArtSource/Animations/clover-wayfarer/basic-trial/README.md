# 三叶旅童：六项基础动作试绑

2026-10-25，v09动作试绑。用户认为动作目前可以，待装入后测试；尚未替换生产角色。当前媒体已同步v10浅球面面具，动作关键帧保持一致；24网格、3362三角面、15骨。v09平板面具和媒体保存在修订备份中。

打开 [index.html](index.html) 逐项播放，支持原速／半速；[原速合集](clover-basic-six.mp4)、[六项同屏GIF](six-motion-preview.gif)也可直接查看。每个画面左边为正斜面、右边为侧面，同步播放同一动作。

| 动作 | 时长 | 处理 |
|---|---:|---|
| idle 待机 | 2.50秒 | Idle_Loop重定向，保留轻微呼吸和重心变化 |
| walk 走路 | 1.33秒 | Walk_Loop足迹节奏，按短腿缩步，解析两骨IK校正脚底 |
| sprint 跑步 | 0.67秒 | Sprint_Loop，收小步幅、前倾、侧摆和上下弹跳 |
| hit 受击 | 0.47秒 | Hit_Chest，补入短促冲击建立与回稳，双脚保持原位 |
| die 死亡 | 2.40秒 | Death01失衡与倒地，末姿态适配大头卧地，整身最低点校正 |
| jump 跳跃 | 0.80秒 | 新编短促起跳／落地缓冲，参考Jump_Loop空中姿态，无长蓄力 |

前3项循环首尾严格对齐；后3项单次，运行时应按动作完成状态切换。预览视频为单次动作加了首尾观察停顿。死亡应保持末帧。

跳跃资产只烘焙姿态与小幅局部重心。视频中最高约0.30米的升降是渲染器给集合实例施加的展示轨迹，不在GLB骨骼或对象动画中；正式游戏使用自身移动／重力。受击也不包含后退距离，击退复用受击姿态并由运行位移后移。

已排除Jog_Fwd_Loop慢跑、长蓄力Jump_Start与Hit_Knockback击倒式反应。翻滚、施法、转向、急停等特项本轮没有制作。斗篷只有3根辅助骨的跟随回摆，没有实时布料／腿部碰撞解算，仍需在最终动作手感确认后细修。

源动作来自本地 [UAL1](../../UAL/Source/UAL1_Standard.glb)，许可与来源见[参考册](../../References/clover-wayfarer-basics/README.md)。源人形未混入角色导出，手臂和手指通道未映射到当前无手模型。

## 文件与复现

- [主工程](../../../Blender/clover-wayfarer/clover-wayfarer.blend)、[GLB](../../../Exports/clover-wayfarer/clover-wayfarer.glb)。
- [动作数据](motion-manifest.json)、[GLB回读检查](validation.json)。
- 先前造型及两个草稿动作保存在 [v08备份](../../../Revisions/clover-wayfarer/v08-stocky-child-proportions/)。
- 在当前造型工程运行 [动作制作脚本](../../../Tools/animate_clover_wayfarer_basics.py)，再用独立后台Blender运行 [渲染脚本](../../../Tools/render_clover_basics.py)，最后用系统Python运行 [媒体打包脚本](../../../Tools/package_clover_basics.py)。
- [回读脚本](../../../Tools/verify_clover_basics.py)从实际GLB检查6动作、几何数量、蒙皮归一、3个循环接缝及每段25帧落地高度。造型构建脚本会重建原始草稿动作，重建造型后须重新运行本轮动作制作步骤。

实际跑速与步频匹配、游戏重力下的跳跃切换、连续受击混合、布料与身体碰撞，均属于后续接入检查。本轮预览不代表已完成生产场景验收。
