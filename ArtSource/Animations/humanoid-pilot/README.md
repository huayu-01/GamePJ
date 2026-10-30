# 首个人形试件动作

动作源保存在 `../../Blender/humanoid-pilot/humanoid-pilot.blend` 中，30 fps。

| 动作 | 源帧范围 | 用途 |
|---|---|---|
| Idle_Pilot | 1–61 | 呼吸及小幅头部变化 |
| Walk_Pilot | 1–31 | 原地步态、摆臂和基本屈膝 |
| JointCheck_Pilot | 1–91 | 肩、肘、髋和膝的变形检查 |

动作首尾相接，独立预览程序会设置循环播放。
这些是自制验证动作，尚未经过正式游戏节奏、足部锁定、攻击关键帧和武器接触点验收。
Mixamo 上传候选位于 `../../Exports/humanoid-pilot/mixamo-upload.fbx`。
