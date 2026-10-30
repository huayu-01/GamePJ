# 外轮廓模糊与撕边：函数及动画事件接入

`UnitMotionFx` 是统一入口，`AnimeMotionFx` 和 `AnimeMotionCapture` 负责底层绘制。函数和动画事件使用同一组 `MotionFxPreset` 资源，不需要在每种技能中复制捕获、挂点更新和清理代码。

## 函数

先在单位的 `UnitVisualProfile.MotionEffects` 中引用预设。`UnitAnimator` 在绑定模型时自动安装单位下的 `MotionFx` 组件；没有预设的档案保持原流程，不创建捕获视口。

```csharp
var motionFx = unit.GetNodeOrNull<UnitMotionFx>(UnitMotionFx.NodeName);
motionFx?.Play("body_dash");
motionFx?.Play("double_fist");
motionFx?.Stop("body_dash"); // 停止续采样，让已有尾部收尽
motionFx?.CancelAll();       // 当帧撤销全部窗口、绘制与捕获
```

`Play` 返回是否成功；预设不存在、组件关闭、单位死亡或相机尚不可用时返回false。`Cancel(id)` 可单独立即撤销一个预设，`Enabled=false` 同时禁止函数与事件触发。

自动安装在父单位完成初始化后执行。如果生成单位后需要立刻播放，可以在 `AddChild(unit)` 返回之后调用 `UnitMotionFx.Configure(unit, model, profile)` 获取组件；不要从某个子节点的Ready里向尚在初始化的父单位同步添加节点。

## 动画事件

沿用项目的 `anim_event(string)` 和 `UnitAnimator.SkillEvent`，不另开原生方法轨道回调。预设的 `EventName` 触发Play，`StopEventName` 触发Stop；不需要扩充事件参数协议，也不改变ATK等战斗事件。

| 事件 | 播放预设 | 表现 |
|---|---|---|
| `FX_MOTION_BODY` | `body_dash` | 全身外轮廓沿运动方向拖散并带稀疏撕边 |
| `FX_MOTION_HANDS` | `double_fist` | 双拳外缘模糊与撕边 |
| `FX_MOTION_BODY_STOP` | 停止body_dash | 停止续采样，已有尾部自然结束 |
| `FX_MOTION_HANDS_STOP` | 停止double_fist | 停止双拳，保留其他预设 |

制作时在 `SkillAnimationConfig.EventNames`／`EventTimes` 中添加事件并按原流程生成。单个方法轨道的同一时刻只能有一个事件，FX可以稍早于ATK，例如FX@0.54、ATK@0.55；需要严格同一时间时使用不同方法轨道。森林测试场使用不同轨道，ATK与全身FX均在0.55秒，双拳FX在0.48秒。

## 预设与挂点

- `Config/MotionFx/stone_body_dash.tres`：0.65动画秒；没有Bone时，Offset使用单位本地坐标，当前身体中心高0.96米。
- `Config/MotionFx/stone_double_fist.tres`：0.22动画秒；Bones指定RightHand／LeftHand，Offset位于各骨骼本地坐标。
- `Config/VisualProfiles/stone_golem_plus_motion.tres`：模型、动画映射和两个预设的完整示例。其他模型改自己的视觉档案与骨骼名，通用组件不写死石头人或手部骨名。

`Duration`、`BlurRadius`、`BlurSeconds`、`BlurMaxDistance`、`ContourOpacity`、`Width`、`Height`、`Lifetime`、`TearOpacity`、`Tint`和`Shade`控制窗口、区域、方向模糊、撕边与色调。预设没有单位计时或播放状态，可以由多个单位共享。Id、事件名重复、无效数值和缺失骨骼会被视觉档案校验报告。

## 时间与生命周期

默认 `FollowAnimationSpeed=true`，窗口和尾部衰减跟随当前动画的真实播放倍速；设为false时按游戏秒推进。单位局部顿帧始终冻结表现，全局暂停同样冻结，并停止额外模型捕获；恢复后继续。

默认 `CancelOnActionChange=true`，播放新动作、显式停止或自然结束技能动画时取消旧表现。死亡、关闭组件和换模型始终立即清理；切到没有预设的档案会释放绑定。组件退树时解除实例事件订阅，随单位销毁全部节点。

同一角色的所有预设共享一个捕获视口，挂点和绘制面在绑定时建立，Play不创建视口、不复制模型、不烘焙蒙皮。换模型时才重建绑定。捕获参数与前一版剪影保护一致；受击闪色与精英边缘光的递归收集跳过本组件分支。

## 已接入范围与验证

`Scenes/Debug/AnimeMotionCombatLab.tscn` 已改用上述视觉档案和真实动画事件，移除了测试场中手工创建视口、更新手骨及按速度触发的分散代码。没有替换其他角色、敌种的档案，也没有给全项目所有ATK默认附加效果。

运行 `./_tools/verify_motion_fx_integration.ps1` 做真实模型、函数、动画事件、倍速、顿帧、暂停、事件隔离、资源复用、换模、死亡和退树专项；运行 `./_tools/verify_anime_motion_combat.ps1 -SkipBuild` 做新的实战性能对照。组件报告在 `Logs/evidence/anime-motion/integration-validation.json`。所有脚本共用验证锁、双构建、严格stderr检查和五份存档保护。

外观原型见[外轮廓预览](anime-motion-preview.md)，性能方法见[森林实战性能](anime-motion-combat.md)。

本轮组件专项27项通过，Debug／ExportRelease双构建0警告0错误；自动安装、函数、真实事件、暂停、打断、换模与销毁均由独立渲染进程验证。森林接入后的12阶段性能复验也已通过，详见性能页的新运行报告。
