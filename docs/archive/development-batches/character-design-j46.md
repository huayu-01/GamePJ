> 历史记录：仅用于追溯。本文件中的旧数量、待办与阶段结论不代表当前规则；当前入口见[开发快照](../../SESSION-SNAPSHOT.md)。

# J46：可挂载的交互类型组件

2026-10-22。原奖励节点、首领门和基地台分别承载行为，但共用 `Interactable`。本轮新增 `InteractionComponent`：挂在满足三维范围需求的场景物体下，通过 Inspector 的 `Kind` 选择用途，运行时创建一个原交互后端。工作区尚未提交推送；制作入口见[交互组件接入](../../interaction-authoring.md)，接续见[当前快照](../../SESSION-SNAPSHOT.md)。

## 当前行为

- 提供None与16种有效类型：七种奖励、首领门、出口、出击、能力矩阵、配装、符文、帮助、角色和自定义信号。类型参数在开局读取，修改后停止并重新运行。
- `TriggerArea` 可显式指定，也可采用父Area／宿主Area／宿主唯一直接子Area；没有Area时用半径。Area配置错误时不退回半径；进入范围不自动触发，仍按F。范围在按下时复核，长按或echo不重复交互。
- `Signal` 类型成功后发出 `Triggered`；`UseMode` 支持沿用类型默认、一次性和可重复。`HighlightMesh` 使用原模型做描边反馈，不替换网格或材质；`HideOnUse` 随一次性使用隐藏并在Reset后恢复。
- 组件提供类型、角色、宿主重复、多个Area及Mask／Shape配置警告；None、缺角色、重复宿主或模糊Area不会创建额外后端。包括跨分支显式共用宿主的情况。
- 基地出击、能力矩阵和帮助三个台已迁移，保留原摆位与模型。旧角色展台和预制体继续兼容。
- Stage收集嵌套奖励／门／预置出口，布点移动整个宿主；预置出口不占10个普通节点配额，首领结束原位激活。真实F只离场一次，正式换关 `ApplyStage → ResetStage` 后同一出口重新隐藏禁用；没有预置出口时沿用原降临流程。

可实例化 [InteractionComponent.tscn](../../../Scenes/Interactables/InteractionComponent.tscn)。[InteractionComponentLab](../../../Scenes/Debug/InteractionComponentLab.tscn) 包含基地与Area机关／Area帮助／半径符文三个例子，可F6运行。

## 验证

完整入口 `pwsh ./_tools/verify_projectile_capsule.ps1 -InteractionComponents`，运行编号 `20261022_113641`，见[完整证据](../../../Logs/evidence/character-design-j46-evidence.log)。之后追加预置出口生命周期检查、补齐测试命名空间引用，并修正显式HideOnUse在首次显隐轮询时可能被重新显示的边界；同入口加 `-ProbeOnly`，编号 `20261022_114427`，见[最终补测](../../../Logs/evidence/character-design-j46-probe-20261022_114427.log)。

| 检查 | 结果 |
|---|---|
| 双构建 | 两轮Debug／ExportRelease均0警告、0错误 |
| 交互组件 | 完整运行75通过；保留全部原断言并增加出口生命周期后最终91通过、0失败 |
| 专项覆盖 | 导出枚举／PackedScene引用往返、16种后端接线、真实F、Area边界、半径即时复核、一次性与复位、整宿主移动、唯一后端、原模型保留、嵌套Stage的10节点及2／5／3配额 |
| 增援／整局／玩家流程 | 完整与补测均37／73／38通过、0失败；完整实际渲染玩家44通过 |
| 组件示例实际渲染 | 6组件／6后端／2触发器，截图成功，正常退出 |
| 导航／敌人／符文界面 | 39／95／42通过；符文实际渲染43通过、0失败 |
| 原五项回归 | 弓39、蓄力80、原始设计115、龙之箭42、符文83通过、0失败 |
| Main | 正常退出；八指标及14项索引比对通过，保留原5条历史红色打印 |
| 存档与进程 | 两轮分别即时比较五存档SHA256，前后一致；selftest.flag结束不存在；串行持锁，不关闭用户编辑器 |

补测首次因Probe缺少 `ActRoguelike.Rewards` 引用而构建失败，已修正并重新双构建通过，[失败现场](../../../Logs/evidence/character-design-j46-failure-20261022_114344.log)保留；未删除断言或降低通过标准。最终补测未重复不受影响的图形与武器回归。

图形使用J46独立文件名，完整日志保存即时指纹，不覆盖旧截图。已查看[组件示例](../../screenshots/j46_interaction_lab.png)与[设置覆盖摘要](../../screenshots/j46_summary_settings.png)，当前用户设置为1920×1080；基地、摘要、森林节点、奖励与挑战截图一并留存。

交付静态检查：34份当前／归档文档的437个本地链接存在，4个接入场景的26个资源路径有效，14份本轮C#文件符合UTF8无BOM／LF，`git diff --check`通过。CodeGraph已强制更新（9,131节点／8,817边）。

## 接续边界

本轮完成编辑器配置入口和现有业务接线；示例体块用于演示组件，最终地形、构筑解锁通路、完整人工战斗试玩继续按[体验计划](../../player-experience-plan.md)后置。触发器只控制交互范围，不替代道路、碰撞与可达性设计；测试中的模拟首领结束用于验证状态转换，不代表完整首领战试玩。
