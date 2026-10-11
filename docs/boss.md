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

## Boss 构筑（词条，策划文档 §12）

Boss 也有自己的「构筑」：生成时按 Stage 配置随机抽 N 个**词条**（BossPerkCatalog），让同一个 Boss 每局不一样。

| 词条 | 效果 |
|---|---|
| 狂暴 | 伤害 +25%，攻速 +20% |
| 铁壁 | 最大生命 +40%，护甲 +150 |
| 疾行 | 移速 +35% |
| 提前觉醒 | 开场即带 1 级压力 |
| 召唤师 | 压力每级多召唤 1 个增援、更早开始召唤 |
| 尖啸强化 / 冲锋强化 | 开场就会对应技能（跳过阶段解锁） |

- 词条数量由每个 Stage 配置：Stage 1 = 1 个、Stage 2 = 2 个、Stage 3 = 3 个。
- 效果只走 StatSheet 的 Modifier 或直接改组件参数，不往 Boss 脚本里加分支；加词条只加一行定义。
- HUD 会在 BOSS 行显示本局词条，例如：`BOSS 900/900  阶段 1  压力 0  [冲锋强化]`。
- Boss Build 与 Boss Pressure、Boss Skill 三者相互独立（§6.3/§12 的要求）。

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
- Boss 没有「Boss Build」词条系统（策划文档 §4.5 的 Boss 构筑改造）——目前用阶段 Modifier 近似。
- 压力曲线、阶段阈值、Boss 血量都还是原型值。
- 技能精英只有一种（增益光环），没有按 Encounter 随机组合。
