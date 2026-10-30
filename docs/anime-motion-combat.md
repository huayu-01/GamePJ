# 外轮廓模糊：森林实战与性能对照

独立入口 `Scenes/Debug/AnimeMotionCombatLab.tscn`，F6 可进入，也可双击 `ArtSource/Tools/Start-AnimeMotionCombatLab.cmd`。复用正式 `ExpeditionForest` 森林、`Player.tscn` 玩家、MapManager 导航、HeavySlammer 单位结构，以及 EnemyPlanAbility 的动画事件、冲锋、预警和命中执行器。测试行通过 UnitVisualProfile 使用石头人 Plus，源模型与生产敌人表保持原样。

F3 开关效果，F4 切换效果人数上限 1／4／8，F5 复位敌人位置。该入口是实际可受击的战斗沙盒，没有奖励、整局推进与存档写入；不改变正式出征流程。现已使用[统一表现组件](motion-fx-authoring.md)及资源预设，FX事件与ATK分别转发，测试场不再手工更新骨骼挂点或按速度调用底层效果。

## 性能方法

运行 `./_tools/verify_anime_motion_combat.ps1`：公共独占锁、Debug／ExportRelease 双构建、实际 1920×1080 Forward+ 渲染、关闭垂直同步与帧率上限。性能进程不录屏、不读回图片，也不在采样中写报告。五份真实存档在每次运行后即时比较，严格要求进程退出0、stderr为空以及战斗检查通过。

- 始终使用八名真实AI敌人，仅改变启用特效的人数上限 1／4／8，避免把敌人数变化混入特效差值。
- 按每种上限“关闭→开启→开启→关闭”采样；每段7.2秒，前0.4秒排除，开始前预热4.5秒。四段各自等权汇总，降低采样帧数与顺序偏差。
- 每3.2秒通过正式能力取消生命周期复位一轮，用同一组通过导航／净空检查的位置重现冲锋。测试进程提高双方生命并为玩家持续治疗，避免不同阶段死亡或击退改变负载；不改伤害结算和正式配置。
- 旧性能报告采用ATK开启双拳、速度阈值开启全身；封装后改为正式 UnitAnimator 实例的 `FX_MOTION_HANDS`／`FX_MOTION_BODY` 事件，并跟随动画播放倍速。没有用模型传送模拟高速运动；复位期间清空效果。两种接入方式的报告保留独立运行目录，不混用结果。
- 主视口与每个角色捕获视口均启用 RenderingServer 的 CPU／GPU 计时；GPU总数累计当帧绘制的捕获。脚本／物理耗时与真实帧间隔另存，不能用渲染CPU计时冒充全部CPU。
- 动态GPU计时有回读延迟，捕获启停边界的累计值作为估计；全阶段平均包含动作间歇，同时触发窗口的平均值与有效样本数单列。该固定镜头、本机开发构建的结果不代表所有硬件或全部关卡负载。

原始逐帧数据、12阶段统计、严格运行日志保存在 `Logs/evidence/anime-motion-combat/run-时间戳/`。运行 `python ./_tools/summarize_anime_motion_combat.py` 可为最新一次性能运行生成 `summary.json` 和 `report.md`；日志验证失败的运行不会被标为通过。

GPU计时口径见 [Godot RenderingServer 文档](https://docs.godotengine.org/en/stable/classes/class_renderingserver.html#class-renderingserver-method-viewport-get-measured-render-time-gpu)。

## 录制与表现检查

另跑 `./_tools/verify_anime_motion_combat.ps1 -SkipBuild -CaptureOnly -Movie`。此进程只负责实战图和视频，录制速度及画面FPS不作为性能数据。输出同在独立 run 目录。原型效果与剪影保护的专项说明见[外轮廓预览](anime-motion-preview.md)。

捕获相机已经消费主相机的插值后变换，自身关闭物理插值，避免在渲染回调中更新时出现重复插值警告。

## 本机实测

2026-10-25 干净复测：[报告](../Logs/evidence/anime-motion-combat/run-20261025_122841/report.md)、[逐帧CSV](../Logs/evidence/anime-motion-combat/run-20261025_122841/frames.csv)。RTX 4070 Ti、Ryzen 7 5800X、1920×1080、Godot 4.7.2 开发构建，固定八名敌人。

| 特效人数上限 | 循环平均额外帧耗时 | 同时触发时总帧耗时均值 | 同时触发时总帧耗时p95 |
|---|---:|---:|---:|
| 1 | +0.04 ms | 2.45 ms | 5.05 ms |
| 4 | +0.08 ms | 2.87 ms | 5.47 ms |
| 8 | +0.13 ms | 3.70 ms | 6.32 ms |

实战检查包含208次玩家真实受击、八名敌人均发生真实攻击、八份捕获同时工作，1／4／8并发分别有940／801／626个有效渲染样本。严格stderr为空，五份存档未变化。该负载下适合动作短窗口使用；更近的镜头、更大的屏幕覆盖和更多敌人仍需按目标场景测试。

首次运行出现捕获相机插值警告，日志保留在 `Logs/validation/motion_combat_20261025_122648.*`，未作为通过结果。修复后使用上面的完整重跑结果。

封装为动画事件组件后的独立复测：[20261025_144312报告](../Logs/evidence/anime-motion-combat/run-20261025_144312/report.md)。同机同镜头，1／4／8只的动作循环平均额外帧耗时为+0.06／+0.13／+0.18ms；8只同时触发时，总帧耗时平均3.86ms、p95为6.50ms。每组保持OFF ON ON OFF，不把新旧运行的基线互相相减。完整实战通过且stderr为空。
