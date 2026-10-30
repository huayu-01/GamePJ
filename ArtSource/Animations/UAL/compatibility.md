# 两套 Universal Animation Library 兼容性检查

检查日期：2026-10-19。源压缩包位于 `F:\Godot项目\3d肉鸽雨中哈迪斯\动画库`，未修改源包或游戏资源。

## 结论

两套动画库可以合并作为人形基础动作库；当前生成人形样件需完成骨骼映射和静止姿势适配后使用。
已在 Godot 4.7.2 中抽测重定向，并将抽测动作烘焙回 GLB，在 Blender 5.2.2 中确认模型随骨骼变形。
这是兼容性验证，尚未完成全部动作的足底、握持、碰撞和战斗节奏验收。

| 项目 | UAL 1 Standard | UAL 2 Standard |
|---|---|---|
| 格式 | FBX、GLB | FBX、GLB，另附女性人偶 |
| 骨架 | 65 骨 | 65 骨 |
| 动画条目 | 43 | 43 |
| 主要内容 | 待机、走跑、跳跃、翻滚、受击死亡、拳击、基础剑击和施法 | 剑连段、重击、格挡、盾冲、击退、滑行、攀爬和交互 |
| 根运动 | 普通版及 `_RM` 版 | 普通版及 `_RM` 版 |

两库的骨骼名称、父子层级及静止变换逐项完全一致。
唯一重名的动画是 `A_TPose`；剔除这两份参考姿势后，共 84 个不同名称的动作。
`_RM` 文件是同一批动作的根运动版本，不能另算一套新动作。
包内许可文件均标注 CC0 1.0，作者为 Quaternius；原许可副本保存在本目录。

## 当前人形样件的实测

目标为上一轮混元生成的 `humanoid-pilot.glb`，23 骨，其中 22 根用于变形。
源骨架的 23 个主要人形节点均能映射到目标，没有缺失的主要四肢或躯干节点。
测试采用 Godot 的 `SkeletonProfileHumanoid` 和 `RetargetModifier3D`，只转移旋转，保留目标骨长。
处理了 Godot 将 `mixamorig:` 骨名转换为 `mixamorig_` 的差异，并同步修正动画路径与蒙皮绑定。

| 库 | 抽测动作 | 结果 |
|---|---|---|
| UAL 1 | Walk_Loop | 可驱动目标；已检查烘焙后的蒙皮画面 |
| UAL 1 | Sword_Attack | 可驱动目标骨骼 |
| UAL 2 | Sword_Regular_Combo | 可驱动目标；已检查烘焙后的蒙皮画面 |
| UAL 2 | Hit_Knockback | 可驱动目标骨骼 |

详见 `godot-audit.json`，两张 `*-retarget-preview.png` 为样件应用库内动作后的渲染。
检测中四个源 GLB 均成功读取，每个均包含 65 根骨骼和 43 个动作。

## 接入建议

1. 两库统一通过 Godot 人形映射使用，或在 Blender 中烘焙到最终角色骨架；不要只改骨名后直接复制旋转曲线。
2. 当前先采用不带 `_RM` 的版本，保留游戏代码对角色位移的控制。根运动版留待冲刺、翻滚等专项动作确定后接入。
3. 正式人形补上手指骨骼。现有样件的手指整体跟随手掌，库内握拳和握剑手型无法完整转移。
4. 调整脚底接触、肩胯权重和武器挂点；本次旋转抽测没有复制髋部位移、根运动或足部 IK。
5. 攻击前摇、命中与恢复、连段分段和方法轨道事件需按游戏战斗逻辑配置，动画库不能自动替代这些数据。
6. 两套 Standard 包没有专门的弓、长枪、镰刀或羽翼动作，可用作身体动作基础，专属动作仍需补充。

参考：[Godot 骨骼重定向流程](https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/retargeting_3d_skeletons.html)、
[RetargetModifier3D](https://docs.godotengine.org/en/stable/classes/class_retargetmodifier3d.html)。
测试脚本与烘焙 GLB 位于本机 `ArtSource/Tools/cache/ual-compatibility/`，属于忽略提交的验证缓存。
