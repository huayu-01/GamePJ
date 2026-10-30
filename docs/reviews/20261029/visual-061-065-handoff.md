# 061／065 视觉交接

状态：本轮目标已完成。未提交推送，未写验收xlsx，未修改模型、骨架、源动画及真实存档。主会话已交回的事件桥作为现状继续完善；不需要另一会话代为接手。

## 已实现

- 061：辅助瞄准文字位于左下血条上方，18号浅青文字加轻微阴影；屏幕中央保留原目标标记。真实中键开关、目标选择和投影位置不变，文字不拦鼠标。
- 065：蓄力状态机在基础完成和真实ChargeSteps变化时派发事件；表现只接受当前存活角色槽位的能力。基础完成一次薄层身体闪光、扩张圈和音效；后置档位只短闪并提高音高，不再额外生成圈，不再通过基础完成轮询播放第二次旧音效。
- 闪光固定0.18秒，与峰值强度解耦。主体闪光透明度上限0.12，轮廓合成上限0.62；青蓝基础档、薄荷绿强化档、金色完美档。保留菲涅尔方向和模型原有颜色、图案。
- 蓄力叠加优先级5，低于精英10及受击／攻击预警；护甲外壳独立。换模、换装、取消、槽位撤销、死亡、退树都归还叠加通道并清掉本组件自己的扩张圈。暂停冻结表现，空闲停止更新。
- 长帧跨越多个阶段时沿用事件桥的合并策略，只强调本次到达的最高真实档位，避免同帧叠音；正常逐档均触发，不虚构额外档位。

## 文件

| 文件 | 改动 |
| --- | --- |
| `Scripts/UI/PlayerHud.cs` | 左下提示定位与字体；血条、构筑、回血跳字锚点及标记行为保留 |
| `Scripts/Abilities/Ability.cs`、`Ability.ChargeMilestones.cs` | 接续主会话已加入的真实阶段与生命周期事件桥 |
| `Scripts/Combat/ChargeBodyFx.cs` | 阶段声光、固定短闪时长、当前槽位过滤、材质优先级与生命周期清理 |
| `Assets/Effects/charge_rim_placeholder.gdshader` | 薄层身体闪光；文件与内联兜底画法一致 |
| `Scripts/Combat/LandingShockwave.cs` | 只增加最近生成节点的只读句柄，供调用方清理自有圈 |
| `Scripts/Combat/UnitRimOverlay.cs` | 仅同步优先级说明注释，无通道逻辑改动 |
| `Scripts/Testing/VisualChargeMilestoneProbe.cs`、对应Debug场景 | 061／065真实输入及正式模型专项 |
| `Scripts/Testing/VisualReadabilityProbe.cs` | 新增明确的`--rims-only`入口；按最新蓄力优先级及入树后的实际美术材质核验 |
| `_tools/verify_visual_061_065.ps1` | 共享锁、隔离镜像、双构建、严格stderr、五存档与专项入口 |

额外解除启动阻塞：`Config/Modes/twilight_survival.tres`的碎片预制体ext_resource原先位于sub_resource之后，引擎报Unknown tag；仅将声明移动到文件头的外部资源段，未修改碎片数量、掉落或结算逻辑。未修改ArrowVolleyAbility及其partial、Weapon、EnergyWaveAbility、ProjectileDispatch、生命互斥、后撤配置、死战碎片脚本及ShardPickup场景。

## 验证与证据

最终验证均持有`Validation.Common.ps1`共享锁，使用独立用户目录；只管理自建进程，未关闭用户编辑器。

| 验证 | 结果与日志 |
| --- | --- |
| Debug／ExportRelease | 0警告、0错误；`Logs/validation/visual-061-065/regression-final/build-*.log` |
| 061／065专项 | 无头69、实际渲染74，均失败0；`Logs/validation/visual-061-065/final/VisualChargeMilestone-*.log` |
| 原蓄力释放 | 80／0；`Logs/validation/visual-061-065/final/ChargeRelease-headless.log` |
| 身体材质相关回归 | `--rims-only`23／0；`Logs/validation/visual-061-065/regression-final/VisualReadability-headless.log` |
| 图鉴 | 无头50、渲染53；`outputs/visual-polish-20261029/final/Codex.log`及`Logs/validation/visual-061-065/regression-final/Codex-render.log` |
| 其它UI回归 | 构筑奖励37、语言12、奖励规则74、通用UI39；`outputs/visual-polish-20261029/final/*.log` |
| 真实存档 | 最终成功运行即时逐字节检查五份存档；两个最终目录均有`saves-unchanged.txt` |

新专项覆盖六种武器当前槽位内全部实际蓄力技能：能量掌武技2、飞刀武技2、弓武技1；检查基础和每个真实档位的实际音频次数、封顶不重复、正常释放、自动释放最终档位、暂停、硬控中断、旧仓库能力隔离、受击／精英优先级、换装、角色换模、槽位直接撤销及死亡。

截图已打开检查，均为真实输入状态，没有用DebugPreview冒充完成：

- [061左下提示](../../../Logs/evidence/visual-061-065/061-aim-hud-edge.png)
- [065基础完成](../../../Logs/evidence/visual-061-065/065-knife-base.png)
- [065强化档1](../../../Logs/evidence/visual-061-065/065-knife-step-1.png)
- [065强化档3](../../../Logs/evidence/visual-061-065/065-knife-step-3.png)
- [065取消清理](../../../Logs/evidence/visual-061-065/065-cancel-clean.png)

上述相对链接的工程根目录为`F:/Godot项目/项目a`；机器报告及同名截图集中在`Logs/evidence/visual-061-065/`。

旧完整VisualReadability专项仍在长枪弹道段出现“路径采样为空”的历史问题，本轮未将其宣称修复；该段原断言保留，未修改长枪或投射物逻辑。新增`--rims-only`明确用于独立验证本次相关的身体材质部分。早期失败日志保留在`first`、`second`和`final/VisualReadability-headless.*`，最终身体材质结果以`regression-final`为准。

## 用户本次参考图精修

同时按用户“和参考图一致、多迭代几次”的要求，对奖励、构筑和图鉴新增四轮并排比对：收窄白雾、柔化边缘、调整中央透明度与清晰度、增加浅底磨砂层次、减弱选中填充、对齐图标／卡名／正文分隔线、压缩键帽。1080p和720p各26项通过，已实际检查PNG尺寸与布局。最终图在`outputs/visual-polish-20261029/final/`；过程、局部对照与范围见工程根`design-qa.md`，结果passed。
