# 石头人 Plus：纯岩块低多边形样件

2026-10-21。使用本聊天确认的第一张纯石块图生成几何，按照第二张简洁黑灰平涂图整理颜色。无武器、衣物、皮带、苔藓或其他装饰。

- 混元 3.0 LowPoly 单次任务 `1504389242456629248`，实际消耗 25 积分；原始模型及校验记录保存在 `ArtSource/Generated/stone-golem-plus-v1/`。
- 模型高 3 米；原始 4,447 三角面，整理、关节分区和封口后为 4,584 三角面，2,481 源顶点。
- 17 骨刚性分区蒙皮，每顶点一个权重，无未绑定顶点；分区连通性清理避免抬臂后留下漂浮小片。
- `idle`、`walk`、`slam`、`hit`、`die` 五段自制动作，60 fps 导出及导入。空手双拳重砸的落点时间为 0.62 秒。
- 仅三种源材质：黑灰石体、深色眼窝、暖黄眼睛。运行时使用简单三渲二色阶，关闭纹理和程序化碎色；细外壳描边按位置扩大，避免硬边法线产生小裂缝。

可编辑源：`ArtSource/Blender/stone-golem-plus/stone-golem-plus.blend`。整理脚本：`ArtSource/Tools/prepare_stone_golem_plus.py`，通过 Blender MCP 执行正文；只重建其专用场景，不修改其他模型。

运行外观预制：`Scenes/Enemies/Art/StoneGolemPlusVisual.tscn`。当前以独立样件加入 `Scenes/Debug/NatureArtLab.tscn`，按 F3 切换近景、侧面及玩家镜头，F4 演示空手重砸。原持锤石人及其 F7／F8／F9 功能保留。

本版本仅完成外观、骨骼和演示动作，未替换正式敌人 Unit、AI、碰撞配置或攻击伤害事件。后续正式接战斗时须另行对齐空手攻击的实际触地点和受击体积。

真实场景截图工具：`ArtSource/Tools/Capture-StoneGolemPlus.ps1 -View hero`，另支持 `side`、`walk`、`windup`、`impact`。工具持有独占验证锁，只结束自己启动的进程，逐次核对五份玩家存档。单件截图隐藏其他展示对象以免遮挡；交互样板中所有样件共同显示。
