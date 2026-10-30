# 几何卡通重击敌人

> 2026-10-26：用户已取消此持锤石头人方向，退出当前美术制作清单；E16保留纯岩块Plus。下文为旧样件产线与接入记录，旧源文件和演示引用尚在，不代表当前外观选择。

用于 30 米自然美术样板中的首个敌人模型。造型来自本项目的重击敌人初稿，进一步收敛为大块石体、几片苔绿和简单皮带；无写实裂纹、碎石堆叠和贴图细节。

- 模型：`stone_slammer.glb`，约 2.96 米高，身体 1,848 三角面，石锤 144 三角面，合计 1,992 三角面。
- 23 骨基础人形骨架，石块按部件刚性蒙皮；每顶点一个骨骼权重，无未绑定顶点。
- 模型内有 `idle`、`walk`、`sprint`、`slam`、`hit`、`die` 六段自制动作；暂未进行 Mixamo 动作重定向验收。
- 导出及 Godot 导入均采用六十帧采样，并将动作起点移到零，砸地动作长 0.9 秒，0.55 秒的触地点落在实际采样帧上。保持 `.glb.import` 中 `animation/fps=60`，避免默认三十帧重采样抹掉快速落锤的触地点。
- 石锤是独立蒙皮网格，绑定右手，可在 Blender 中单独编辑和替换。
- `Animations/EnemyAction.tres` 从真实 `slam` 骨骼动画导出，在 0.55 秒处添加 `ATK` 方法轨道；正式 UnitAnimator 读取该帧驱动原有砸地伤害。

运行预制为 `res://Scenes/Enemies/Art/StoneSlammer.tscn`。它复用原重击敌人的 Unit、移动、AI 与 EnemyPlanAbility，替换模型及动画资源，按样件尺寸设置 2.95 米胶囊、0.72 米半径。

样板配置在 `Config/Testing/stone_slammer_preview.tres`，复制现有重击敌人行并指向专用技能动画目录。正式敌人表和随机刷怪池保持当前配置。

可编辑源文件：`ArtSource/Blender/stone-slammer/stone-slammer.blend`，活动场景为 `StoneSlammer_Geometric_v1`。生成脚本：`ArtSource/Tools/build_stone_slammer.py`；脚本通过 Blender MCP 的安全模式执行，创建目录由外部文件工具完成。

Godot 接入脚本：`ArtSource/Tools/NatureArena/prepare_stone_slammer.gd`，可通过 Godot MCP 的编辑器脚本工具调用 `build()` 重新生成预制、配置与技能动画。重建会覆盖这些生成文件。

打开 `res://Scenes/Debug/NatureArtLab.tscn` 或运行 `ArtSource/Tools/Start-NatureArtLab.cmd` 即可查看。

- 默认原地待机，方便看外形；WASD 可以绕到侧面、背面观察。
- F7 切换展示／实战；返回展示时停止攻击并回到展示位置。死亡后用 F6 重置。
- F8 在展示状态下播放一次完整抡锤砸地动作。
- F9 切换近景和实际游戏跟随镜头。
- 其他移动、跳跃、攻击、刷怪及总览操作沿用样板。

命令行 `-- --slammer-smoke` 验证动画、骨架、行走、真实 ATK 事件和砸地伤害；报告位于 `docs/screenshots/stone_slammer_validation.json`。
正常图形运行时用 `-- --slammer-capture=hero`、`follow`、`windup`、`impact` 捕获真实场景截图。

本次 Debug 和 ExportRelease 构建均通过。带画面的实战验收中，石人行走约 2.23 米，触发一次真实 ATK 事件和一次砸地，造成 25 点伤害；最终验收日志无警告／错误。
Godot 导入后的 0.55 秒姿势已与 Blender 对照，锤头最低点约在模型原点上方 5.2 厘米。样板导航已按 3 米高度、0.75 米半径重新烘焙并匹配管理器参数，共 173 个多边形。
