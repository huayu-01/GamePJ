# 增幅制作与候选规则

当前增幅统一由效果资源组成。新增步骤和完整代码样板见[内容制作指南](content-authoring.md)，抽取、节点与生命周期见[奖励契约](rewards.md)。旧P5至J27处理器说明已[归档](archive/reference-before-2026-10-25/upgrades.md)，其中的待办、数量和旧入口仅代表当时状态。

## 新增一项增幅

1. 在 `Config/Tables/Upgrades/` 复制同类资源，填写唯一稳定 `Id`、显示名、说明、类别、权重及获取次数限制。
2. 在 `Effects` 组合已有 `RewardEffectDefinition`。属性使用 `StatRewardEffect`，技能参数使用 `AbilityTuningRewardEffect` 与专用 `AbilityRewardTuning` 子类，特化使用 `SpecializationRewardEffect` 与独立行为定义。参数直接存入这些资源。
3. 配置角色、武器、前置与互斥条件，加入 `ConfigIndex.Upgrades`。核对新增数量后显式更新 `Config/Validation/content_inventory.tres` 对应总数及分类数。
4. 运行 `python _tools/sync_content_text.py --apply` 同步文本键和中文原文；说明引用实际效果字段，不另抄一套数值。
5. 运行 `./_tools/verify_gameplay_fixes.ps1 -Only content,effects,reward_rules`，再通过GM实际获取，检查真实命中、取消和换装。涉及特化时补 `-Only specialization,synergy`。

新机制继承 `RewardEffectDefinition` 并创建独立 `RewardEffectInstance`，运行状态归实例。安装、来源撤销和失败回滚由公共宿主管理；无需向 `RewardEffectRegistry` 增加业务委托。新技能参数类型继承 `AbilityRewardTuning`，通过 `Supports(Type)` 声明支持的能力，制作校验不构造临时技能节点。

## 候选过滤

| 层 | 配置与规则 |
|---|---|
| 角色限定 | `RequiredCharacterIds` 使用角色稳定Id，未知角色不能获得限定项 |
| 武器限定 | 专属池优先，其次武器标签，均无则通用；专属池不匹配不能回退 |
| 前置 | `Requires` 必须全部满足，来源包括已获增幅和当前携带符文 |
| 互斥 | `Excludes` 双向排斥，`ConflictsWith` 为候选声明的单向冲突 |
| 获取次数 | `MaxStacks` 为0不限、1唯一、正数为上限；UI不显示层数，机制保留用户允许重复的条目 |
| 抽取 | `Weight` 决定加权抽取，零权重不抽；每次重新检查资格，候选不足不补造失效项 |

前置、互斥与效果身份使用稳定Id。中文显示名和翻译可以修改，`LegacyPrerequisiteAliases` 只兼容旧条件。旧 `ApplyHandler`／`HandlerParams` 已从全部生产行移除，兼容层仍可读取历史资源；不要用这些入口制作新内容。
