# 召唤物系统（策划文档 §13）

## 最小实现

召唤物就是**另一个 Unit**：同一个 `Unit` + `CharacterMovement` + `Presentation` + `Brain(Behavior)` + `Ability`，只是阵营与分组不同。没有专门的基类。

`Scenes/Summons/Drone.tscn`（无人机）：40 生命、移速 6、近战（6 伤害 / 0.9 秒间隔）、青色发光小胶囊。

## 三个关键约定

1. **阵营决定敌人**：召唤物 `Faction = Player`，于是 `Targeting.FindNearestHostile` 自动把 `enemy_units` 当目标 —— 索敌、追击、攻击**零改动复用**敌人那套 Behavior 与 Ability。
2. **单独分组**：`Unit` 现在把带 `Summon` 标签的单位放进 `summon_units` 组，而不是 `player_units`。
   - 否则 `RunController.ResolvePlayer()` 之类的「取第一个玩家单位」会抓到召唤物；
   - 敌人也不会瞄准召唤物（`Targeting` 按阵营选组），所以当前召唤物是**不会被攻击**的 —— 这属于简化，策划文档若要求召唤物可被击杀需要再补。
3. **不会被自己的子弹打到**：投射物命中判定走 `IsHostileTo`，同阵营直接跳过。

## 召唤来源

异化奖励「**亡者随从**」（`RewardCatalog.mut_summon`）：每击杀 1 个敌人召唤 1 架无人机，最多 2 架。
实现是 `SummonOnKillRule`（`TriggerRule`）—— 复用阶段 5 的击杀事件链，带数量上限，不新增系统。

同一个 `SummonAbility` 也用在 Boss 的第三阶段（召唤近战增援），两条路径共用同一个「实例化一个 Unit」的写法。

## 实测

| 检查项 | 结果 |
|---|---|
| 授予奖励 | 构筑显示「亡者随从」✓ |
| 击杀召唤 | `summon_units` 组里出现无人机：生命 40、阵营 0（Player）✓ |
| 自动索敌 | 召唤物的 `Brain.Target != null`，正在追击敌人 ✓ |
| 主动召唤（角色 3） | 按 G → 计数器 0→1、`summon_units` = 1、HUD 显示「召唤 1」；冷却中再按被拒 ✓ |
| 数量上限 | `SummonAbility.MaxAlive`（玩家侧 3 架）与触发规则的 `MaxSummons`（2 架） |

## 存活时间

`Scenes/Summons/Drone.tscn` 挂了一个 `SummonLifetime` 组件（默认 25 秒），到点自动 `ForceKill`，避免场上越堆越多。

实测：召唤后组件 `Remaining` 从 25 正常倒计时（读到 23.98）；「到点消失」这一步没实测到（需要等满 25 秒或改短预制体数值，本轮没跑完）。

## 待办

- 召唤物不会被攻击、没有存活时间与召唤上限以外的限制（策划文档 §13 的具体规则需要时再补）。
- 没有召唤物专属的 HUD 指示（数量/剩余时间）。
- 玩家目前只能通过奖励获得召唤能力，还没有主动召唤技能。
