# 死亡演出与结算确认

实际流程由 [DeathScreen](../Scripts/UI/DeathScreen.cs) 控制，整局记录和收益由 `RunController` 提供。本轮实现与验证记录见 [J44](archive/development-batches/character-design-j44.md)；旧“倒计时结束自动回基地”的正文和当时实测已完整保留在 [历史归档](archive/reference-before-2026-10-21/death-flow.md)，不再作为当前契约。

## 当前时间轴

| 阶段 | 默认值／状态 | 玩家操作与结果 |
|---|---|---|
| 死亡动作 | `DeathAnimationSeconds=1.4s`；世界不暂停 | 显示角色死亡动作；确认输入可跳过演出进入结算 |
| 占位过渡演出 | `HoldSeconds=3.2s`，`FadeSeconds=0.45s`；世界暂停 | Enter／空格可跳过；计时结束进入结算 |
| 阅读结算 | `Phase.Summary`，无自动关闭倒计时；世界保持暂停、鼠标可见 | 新按一次 Enter／空格，或点击“返回基地”后继续 |
| 返回基地 | 默认 `TargetScene=res://Scenes/Levels/Base.tscn` | 解除暂停、清理本次出征选择状态，通过 `RuntimeResources.ChangeScene` 切换 |

**3.2秒是占位过渡演出的默认时长，不是阅读结算的期限。** 演出结束或被跳过后，结算持续保留，直到玩家主动确认。

## 输入与重复调用

- `BeginDeath` 关闭已有界面并解除暂停，让死亡动作能够播放；流程已开始时重复调用直接返回。
- 键盘使用 `ui_accept`。开始时先等待按键释放，再接受一次新按下；死亡时按住空格不会跳过或误确认。
- 同一次按键只前进一步：跳过演出会进入结算，不会继续返回基地。继续按住不会产生重复确认，需松开后再按。
- 键盘与返回按钮共用 `ContinueDeathFlow`。返回前先将阶段改为 `Idle`，重复确认不再切场景。

## 接入与验证边界

`DeathScreen` 是 `Main.tscn`／`Expedition.tscn` 下的流程节点，自身 `ProcessMode.Always`，不走 `OverlayScreen` 栈；它分别控制死亡动作与演出／结算阶段的暂停时机。UI只展示整局已计算的结算内容，不在重复确认时再次发放收益。

后续正式演出可以替换占位过渡阶段，保留“可跳过 → 结算等待确认 → 仅返回一次”的契约。当前阶段、等待确认和进度可分别读取 `PhaseName`、`AwaitingConfirmation`、`CountdownProgress`。

本次文档整理未启动游戏或重复运行验证；最终证据与验收状态以 [J44](archive/development-batches/character-design-j44.md) 和 [当前快照](SESSION-SNAPSHOT.md) 为准。关卡推进与整局结算见 [关卡流程](stages.md)。
