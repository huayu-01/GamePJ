# 符文携带与扩展

符文在基地选择，一次携带一枚，也可不携带。进入冒险后施加自身效果并满足指定增幅的前置条件；符文不进入局内奖励池。HUD在开局显示携带图标和名称。当前有11枚，未设计的第12枚不作占位内容。

## 制作入口

1. 在 `Config/Tables/Runes/` 复制相近符文行，填写稳定 `Id`、名称、说明、槽位及携带图标。
2. 在 `Effects` 组合效果资源，与增幅共用定义、实例、事务和来源清理。纯数值用 `StatRewardEffect`；死铠、保命、距离伤害、生命伤害与小精灵已有独立效果定义可复用。新机制的扩展方式见[制作指南](content-authoring.md)。
3. 新增增幅的 `Requires` 引用符文稳定Id，例如 `rune_lei_seal`。不再用显示名作为新条件；`LegacyPrerequisiteAliases` 只负责历史中文条件的兼容。
4. 加入 `ConfigIndex.Runes`，核对后更新显式内容数量基线，再运行 `python _tools/sync_content_text.py --apply` 同步中文目录。
5. 运行 `./_tools/verify_gameplay_fixes.ps1 -Only content,effects,rune,reward_rules`，实际检查基地选择、开局图标、效果、前置、换装和整局取消回退。

新增符文使用已有行为时只需资源；真正新行为增加自己的效果定义和实例。全部生产符文已使用 `Effects`，`RewardEffectRegistry` 负责元数据和编译，不需要新增字符串委托。

## 生命周期与数值

`RuneCarry` 保存携带选择，`RunController` 在本局安装效果，`RunBuild` 保存前置资格；`RewardEffectHost` 按来源释放规则、修饰和订阅，不再依赖节点名前缀。普通换装按既有契约保留携带符文。整局取消恢复出击前快照，设置保留。

规则撤销时同步停止事件与伤害提供者，迟到回调不能继续触发。组合失败逆序回滚；正常移除不追缴已发放的一次性护甲。属性倍率里的零可能有明确含义，例如 `HealMultiplier=0` 禁疗，不能统一替换为1。飞翼的起跳、下落和下冲倍率分别由实际动作消费。

完整候选规则见[奖励契约](rewards.md)，特化实现见[解耦记录](content-extension-plan.md)。迁移前J11至J27参数和处理器说明已[归档](archive/reference-before-2026-10-25/rune-system.md)，不能用作当前新增步骤。
