# Boss（阶段 6）

对应策划文档 §12-13、执行文档 §七。Boss 同样是「Unit + Controller/Behavior + Ability + Modifier」的组合，没有专用基类。

## Boss 单位

`Scenes/Enemies/Boss.tscn`：900 HP / 200 护甲 / 击退免疫 / 移速 3.6，标签 `Boss, Melee`。

| 能力 | 阶段 | 说明 |
|---|---|---|
| AbilityMelee | 全程 | 重型挥击（24 伤害、击退 6） |
| AbilityScream | 阶段 2 解锁 | 范围尖啸（14 伤害、击退 8、附加 Weaken） |
| AbilityCharge | 阶段 2 解锁 | 直线冲锋（20 伤害、击退 10） |
| AbilitySummon | 阶段 3 解锁 | 召唤 3 个近战增援 |

行为组合：`UseAbilityBehavior`（尖啸/冲锋/召唤，各自绑定能力节点名）+ `MeleeAttackBehavior` + `ChaseBehavior`。
能力默认 `Enabled = false`，由阶段控制器解锁；`Ability.IsReady` 已包含 `Enabled`，所以行为会自动跳过未解锁技能。

## 阶段切换（BossPhaseController）

- 按**剩余生命比例**切阶段：`PhaseFractions = [1, 0.65, 0.33]`。
- 每次切阶段：先移除自己上次加的 Modifier，再按 `PhaseDamageMultipliers` / `PhaseAttackSpeedMultipliers` 重新加（Source = 组件自身，可干净叠加与替换）。
- 解锁对应阶段的能力名单（`Phase2Abilities` / `Phase3Abilities`）。
- 实测：0.65 以下 → 尖啸+冲锋解锁；0.33 以下 → 攻速 ×1.2、召唤解锁。

## Boss 构筑（词条，策划文档 §12；关卡核心文档 §7.1）

Boss 也有自己的「构筑」：生成时按 Stage 配置随机抽 N 个**词条**（BossPerkCatalog），让同一个 Boss 每局不一样。

**两套来源必须能分开（关卡核心文档 §7.1 的硬约束），配置表阶段 6 起有各自的抽取点**：

| 来源 | 表 | 候选 | 抽取点 | 效果 |
|---|---|---|---|---|
| **Boss Build**（行为/技能） | `Config/Tables/Skills/bhv_*.tres`（4 行） | 4 | `BossPerkCatalog.Roll(BossPerkCount)`（Boss 出生时） | 提前觉醒 / 召唤师 / 尖啸强化 / 冲锋强化 |
| **Boss Pressure**（数值） | `Config/Tables/Pressure/pmod_*.tres`（3 行） | 3（按 `Tags` 过滤 + 按 `Weight` 加权） | `BossPerkCatalog.RollPressure(tags)`（**每跨过一次 Pressure 阈值立即抽一次**） | 狂暴（伤害+25%/攻速+20%·权重3）/ 铁壁（生命+40%/护甲+150·权重2）/ 疾行（移速+35%·权重2） |

- 词条数量由每个 Stage 配置：Stage 1 = 1 个、Stage 2 = 2 个、Stage 3 = 3 个（**只数行为型**）。
- 效果只走 StatSheet 的 Modifier 或直接改组件参数，不往 Boss 脚本里加分支；加词条只加一行 `.tres`。
- HUD 会在 BOSS 行显示本局词条，例如：`BOSS 900/900  阶段 1  压力 0  [冲锋强化]`。
- ⚠️ **改动记录**：阶段 6 之前开场词条是从"数值型 + 行为型合并的 7 条"里抽的（过渡态），
  现在数值型改由 Pressure 转换产出 —— 所以**开场词条的结果会变**，这是 §7.1 要求的分离，不是回归。
- ⚠️ **没有**为 Pressure 设计"可被玩家增幅修改"的接口（§7.2 明令禁止）。

### Pressure 累积 → 转换 → 套用（配置表阶段 6）

```
挑战完成（清场）
  → 基础点数：节点表 NodeConfig.BossPressurePoints（挑战类型）
              → 关卡表按价值层的 PressurePerNodeHighValue / Normal
              → Balance.PressurePerHighValueNode（150）
  → × 关卡表 PressureGrowthMultiplier（固定倍率表）
  → × 关卡表 PressureEvolutionBonus(1.25) / PressureMutationBonus(1.5)
  → 每满 PressureThreshold（100）点【立即】抽一次数值强化（逐次检查，跨多个阈值就抽多次）
  → 余数清零、不结转（§7.3 第 5 条）
  → 抽到的强化排队；Boss 出生（EncounterController.BossSpawned 信号）时套到它的 StatSheet 上
```

实测（两轮，2026-10）：

```
Pressure 累积：+187.5（基础 150（来源：节点表 node_evolution）× 本关倍率 1 × 类型比例 1.25）阈值 100 → 本次跨阈值 1 次 / 本关累计 1 次 / 余数 0 / 立即抽取 [狂暴]
Pressure 累积：+225（基础 150（来源：节点表 node_mutation）× 本关倍率 1 × 类型比例 1.5）阈值 100 → 本次跨阈值 2 次 / 本关累计 3 次 / 余数 0 / 立即抽取 [疾行、狂暴]
Boss 出生：套用本关 Pressure 数值强化 3 条 [铁壁、疾行、疾行]（阈值转换 3 次，来源：表 Pressure）→ Boss 生命 1260 护甲 350 伤害×0.972 攻速×1.06 移速×1.914
Boss 出生：套用本关 Pressure 数值强化 3 条 [铁壁、狂暴、狂暴] → Boss 生命 1260 护甲 350 伤害×1.406 攻速×1.44 移速×1
```

数字对得上：铁壁 900×1.4 = 1260、护甲 200+150 = 350；疾行 ×2 → 1.35² = 1.8225（再乘 BossPressure 1 级的 +5% = 1.914）；
狂暴 ×2 → 伤害 0.9×1.25² = 1.406、攻速 1.2² = 1.44。

> ⚠️ **初始压力只由「超出免费挑战额度」决定**（§8.1/§8.2）：`BossInitialPressure = max(0, ChallengesUsed - FreeChallengeQuota)`。
> 阶段 6 之前它还叠加"节点累积 Pressure 折算出的压力等级"，那是 §7.5 转换池没落地时的替身，现已移除（不能与随机强化两头都算）。
>
> ⚠️ **已知边界（第八章，未做）**：§8.2 的"免费挑战不加 Pressure"闸门还没接，免费挑战目前也会累积 Pressure。

## Boss 压力（BossPressure）

拖得越久越强：每 `Interval` 秒升一级（上限 `MaxLevel`），每级 +8% 伤害 / +6% 攻速 / +5% 移速；
达到 `AddSpawnLevel` 后每次升级额外召唤增援。

**数值曲线目前是原型值**（策划文档 §16 把 Boss Pressure 曲线列为待定项），全部在 Inspector 可调。

## Boss Skill Elite

`Scenes/Enemies/SkillElite.tscn`：带格挡的护卫，独有能力是**增益光环**（`BuffAlliesAbility`）——
每隔数秒给半径 11 内的友军（含 Boss）挂 `Empowered` 状态（攻速 ×1.3、伤害 ×1.15）。

技能精英现在有**两种技能**挂在同一个预制体上，生成时由 `EliteSkillRandomizer` 随机启用一个：

- **增益光环**（BuffAlliesAbility）：给半径内友军挂 Empowered（攻速 ×1.3、伤害 ×1.15）
- **范围定身**（AreaAttackAbility + Stun）：半径 6 内敌人移速 ×0.12、攻速 ×0.5，持续 2 秒

于是同一波护卫里会同时出现不同种类的技能精英。候选技能名写在 ``SkillElite.tscn`` 的 ``SkillRandomizer.Skills`` 上，代码不认具体技能。

精英标记复用 `EliteModifier`（这里只用它的体型与配色）。

## 登场流程

`EncounterController` 新增：`BossScene` / `BossGuardScene` / `BossGuardCount` / `BossSpawnDelay` / `BossSpawnDistance`。
Encounter 完成（预算耗尽且场上清空）→ 延迟数秒在玩家前方出场 Boss 与护卫，发出 `BossSpawned`。
`SpawnBossNow()` 是公开接口，阶段 7 的 Node 流程可以直接调用。

## 实测补充（脚本挂载问题修复后）

| 检查项 | 结果 |
|---|---|
| 技能精英随机化 | 一次开门的 2 个护卫都随机到 AbilityControl：光环 False、控制 True ✓（候选被正确关闭、随机一个被启用） |
| 技能精英光环 | Boss 状态变成 Empowered；攻速倍率 1.378 = 1.3（Empowered）× 1.06（压力 1 级），逐项吻合 ✓ |
| Boss 构筑词条 | HUD 显示 BOSS 610/900  阶段 1  压力 1  [疾行] ✓ |
| Boss 第三阶段召唤 | 血量 900→890→590→290（32%）时阶段升到 2、AbilitySummon 解锁；随后计数器 Summoned = 3，召出 3 个增援 ✓ |
## 实测（Main）

- Boss 出场：`boss_units` 组 1 个，HP 900/900、护甲 200、护卫 2 个（`elite_units` = 2）。
- 伤害链路：320 伤害先被护甲吸收（只掉 24 点生命），护甲破后同伤害一次掉 320 点 —— 与护甲优先、破甲后减伤消失的规则一致。
- 阶段：`phase 0 → 1`（尖啸/冲锋 `Enabled=true`）→ `2`（`AttackSpeedMultiplier=1.2`、召唤解锁）。
- 压力：按间隔升级后 `DamageMultiplier` 明显高于阶段基础值。
- HUD：`BOSS 900/900  阶段 1  压力 0`。
- Boss 与护卫会把玩家打死（120 血），需要构筑成长才能对抗 —— 阶段 7 调曲线。

## 已知限制

- 没有 Boss 专属演出（登场警告、阶段切换提示、血条 UI），HUD 只有一行文字。
- 压力曲线、阶段阈值、Boss 血量都还是原型值。
- 技能精英只有一种（增益光环），没有按 Encounter 随机组合。
- ⚠️ **Pressure 的强度需要一轮平衡测试**：转换的产出从"压力等级"换成了"随机数值强化"（见上），
  而 §7.5 的池内容（3 条、权重 3/2/2）是原型值。
- ⚠️ §8.2 的"免费挑战不加 Pressure"闸门未接（第八章范围）。
