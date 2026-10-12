# 战斗系统

对应《战斗规则说明》与执行文档 §五。当前为原型实现，接口按“可扩展”设计，功能只做到 Vertical Slice 需要的部分。

## 结算流程

```
攻击产生（Ability/Weapon）
  └─ 生成 AttackInstance：攻击侧属性快照 + Hit Policy + 标签
命中判定（AttackHitbox / Projectile）
  └─ 目标过滤（阵营、Death、Tag）→ HitTracker 按 Policy 判定
  └─ 生成 HitInstance（含暴击判定，暴击属于攻击侧）
目标结算（Unit.ReceiveHit）
  OnHit → 无敌 → 格挡钩子 → 伤害减免 → Armor → Shield → HP
  → OnTakeDamage → 死亡判定 → 击退
```

- **快照 vs 命中读取**：攻击侧属性在 `AttackInstance` 创建时快照（战斗规则说明 §4）。需要“命中时读取”的效果属于后续 Trigger/Effect。
- **护甲/护盾顺序**：采用 战斗规则说明 §12 的当前推荐 `Damage → Armor → Shield → HP`（全局唯一，不在各单位里各写一套）。
- **减伤叠加**：`1-(1-护甲减伤)(1-额外减伤)`，总量上限 90%。公式属待策划确认项，先固定在此处。
- **护甲是所有单位的通用属性，但"有甲"要看数据**：`Unit` / `UnitConfig` 基类上人人都有 `MaxArmor`，
  实际谁有甲由单位表给（玩家 40/140/60、守卫 60、冲锋兵 30、技能精英 90、**首领 0**）。
  ⚠️ **甲为 0 时管线会自动跳过两件事**：① `CombinedDamageReduction()` 里 `Armor > 0f ? ArmorDamageReduction : 0f`
  → 护甲减伤那一项取 0（所以场景里留着 `ArmorDamageReduction = 0.3` 也不会偷偷减伤）；② `Armor` 阶段整段跳过
  （不吸收伤害、也不发 `ArmorBroke`）。**护甲现在唯一的"额外获取手段"是精英怪的 `EliteModifier`（`MaxArmorAdd +120`）**，
  首领没有任何获取途径（2026-10 用户要求「去掉首领的甲」；判据见 [config.md](config.md) 的「首领去甲 + 单位动画实测」）。
- **死亡**：HP ≤ 0 立即 `IsDead = true` 并从碰撞层移除（逻辑死亡与视觉死亡分离）；死亡单位不再被射线/AOE/索敌选中。

## 护甲的表现层（2026-10 第六轮，**管线一行没改**）

用户需求 §五 #8：「玩家和怪物**有护甲时血量上应该要有其他效果来显示护甲的进度和碎甲效果**，甚至是**模型的额外显示效果**」。

| 要显示的东西 | 读的是哪个既有事实 | 谁在读 |
|---|---|---|
| **护甲进度**（血条上多一条甲条） | `Unit.Armor` / `Unit.MaxArmor`（`Unit` 上早就有；`MaxArmor > 0` 才显示，甲打光后留**空条**） | `WorldOverlay.UpdateBar`（敌人）/ `PlayerHud.UpdateArmorBar`（玩家） |
| **碎甲**（甲条闪白 + 模型上炸一圈） | **`Unit.ArmorBroke` C# 事件** —— 它只在**甲从 >0 掉到 0** 那一次触发（`Unit.ReceiveHit` 的 Armor 段），所以"甲被扣"与"甲碎了"天然分开 | `WorldOverlay.OnUnitArmorBroke` / `PlayerHud.OnPlayerArmorBroke` |
| **跳字里的「甲 N」** | `DamageResult.ArmorLost`（`Unit.Damaged` 事件带出来的那次结算结果） | `WorldOverlay.OnUnitDamaged` |
| **模型的额外效果** | 上面那个 `ArmorBroke` → `Scripts/UI/ArmorBreakFx.Spawn()`：水平扩散环 + 一层快速褪去的亮壳，0.42 秒自己回收 | 同两处 |

⚠️ **甲为 0 的单位（首领 / 木桩）不会误播碎甲**：`Unit.ReceiveHit` 的 Armor 段整段跳过，`ArmorBroke` 根本不会触发
（首领去甲的判据见上面的「护甲是所有单位的通用属性」那一条）。实测：`白板首领实例：生命 315 护甲 0`，战斗中没有任何碎甲特效。

⚠️ **"只掉甲、没掉血"的单位也会出血条**：`GameplayEventBus` 的 `Damaged` 事件里 `Value` 只有 `HealthLost`
（甲被吸收时那条事件是 0），所以血条登记多了一条判据 —— 见 `WorldOverlay.OnUnitDamaged` 与 `UpdateBar` 里的注释；
**没有甲的单位（`MaxArmor <= 0`）行为与改动前逐位一致**（只看血量），所以"满血不显示血条"这条老设计对无甲怪没有任何变化。

证据（截图与逐条打印见 [ui.md](ui.md) §16.6）：`docs/screenshots/feel_01_armor_progress.png`（甲条 48/64 = 0.75）、
`feel_02_armor_break.png`（碎甲环 + 「甲 45」「105」）、探针输出 `甲条 0px / 底 66px（比例 0） 甲条可见=True 血条 52.8px`、
`碎甲模型特效：ArmorBreakFx 节点 1 个 → 0 个`。

## 模块

| 模块 | 职责 |
|---|---|
| `Unit` | 生命/护甲/护盾、受伤结算、死亡、事件（OnHit/OnDamage/OnDeath/OnKill/破甲/破盾）、属性表 |
| `AttackInstance` | 一次攻击的攻击侧快照：伤害、暴击、击退、Hit Policy、生命周期、标签 |
| `HitInstance` | 一次具体命中：来源、目标、伤害、暴击、方向、位置、EventContext |
| `HitTracker` | 按 Hit Policy 判定同一目标能否再次被本次攻击命中 |
| `AttackHitbox`(Area3D) | 近战/AOE/持续区域统一命中体，激活期间每物理帧检查重叠 |
| `Projectile`(Node3D) | 投射物统一实现：分步射线防穿透、弹道下坠、穿透次数 |
| `CharacterMovement` | 单位移动能力：重力、加速度、跳跃次数、位移技能、外力（击退）与朝向 |
| `StatSheet` / `StatModifier` | 属性表：基础值 + 修正器，结算顺序 Override→Add→Multiply→Cap |
| `GameplayEventBus` | 已发生事实的广播（命中/伤害/死亡/击杀/开火/换弹），阶段 5 的触发效果订阅它 |
| `Targeting` | 按阵营选择最近敌对单位 |
| `UnitPresentation` | 只读事件的受击闪色与死亡销毁（表现层） |
| `UnitAnimator` | 只读速度与死亡事件的动画切换（表现层）：按移速在 `idle`/`walk`/跑 之间切，死亡播一次 `die`；参数在单位表的「表现 · 动画」6 列里，**留空 = 组件默认值** |

## 能力与行为（阶段 3）

- `Ability`：`Cooldown / Windup / Recover` + 阶段机。挂在 Unit 下，**不读输入**；由 Behavior（敌人）或 PlayerAbilityInput（玩家）触发。
- `Behavior` + `EnemyBrain`：Brain 每物理帧刷新目标，按 `Priority` 取第一个 `CanRun()` 的行为执行；能力执行期间不做新决策（保持站桩与朝向）。
- 已有能力：`MeleeAttackAbility`、`ProjectileAttackAbility`、`AreaAttackAbility`（可自爆）、`DashAbility`（位移+无敌帧）、`BurstFireAbility`（火力倾泻）。
- 已有行为：`ChaseBehavior`、`KeepDistanceBehavior`、`RetreatBehavior`、`RepositionBehavior`、`MeleeAttackBehavior`、`RangedAttackBehavior`。
- **能力与行为分离**：具备跳跃/冲刺能力不代表 AI 会使用，是否使用由 Behavior 与配置决定（执行文档 §4.4）。

## 敌人（阶段 4）

8 种普通敌人全部由「Unit + Movement + Brain(行为) + Ability(能力)」组合而成，没有各自的专用脚本：

| 预制体 | 行为组合 | 验证点 |
|---|---|---|
| Chaser（基础近战） | Chase + MeleeAttack | 追击、近战命中 |
| FastMelee（快速近战） | Chase(快) + MeleeAttack(短前摇) | 高频压制 |
| JumpingMelee（跳跃近战） | Chase + LeapAttack | 起跳高度 y≈1.4→2.4，落地判定 |
| Charger（冲锋） | Chase + ChargeAttack | 冲锋速度 14，命中即停 |
| RangedEnemy（远程） | KeepDistance + Shoot | 保持 9 距离射击 |
| Screamer（尖啸） | KeepDistance + AreaAttack(附带 Slow) | 范围减速状态 |
| Guard（守卫） | Chase + MeleeAttack + BlockComponent | 正面格挡（伤害 ×0.25） |
| Exploder（自爆） | Chase + AreaAttack(KillSelf) | 贴近后爆炸并自身死亡 |

**精英**：`EliteModifier` 挂在普通敌人下即可（也能由 Encounter 按概率运行时添加），只加 Modifier + 体型/配色，不复制敌人脚本。
实测：守卫 MaxHealth 130→325、MaxArmor 60→180、scale 1.35，并加入 `elite_units` 组。

**避障**：`Behavior.AvoidObstacles` 在 Chase / KeepDistance 前进前探路，被柱子/墙角挡住时改为侧移（每 1.2 秒翻转方向）。没有寻路系统，复杂地形仍可能绕远。

## Encounter 与 Spawn Budget（阶段 4）

`EncounterController` 区分两个量：

- **总预算 TotalBudget**：整场 Encounter 的生成额度，生成时按 Cost 扣减，**不再生**；归零且场上清空 → `IsCompleted`，发出 `EncounterCompleted`。
- **可支配额度**：受 `BudgetCap` 限制、按 `BudgetRegenPerSecond` 再生，用来控制生成节奏。

另有 `MaxConcurrent`（并发上限）、`SpawnInterval`、`SpawnRadius`/`MinSpawnDistance`（围绕玩家生成）、以及每个生成项的 `MinElapsedSeconds`（时间门槛）与 `EliteChance`。
玩家阵亡 → `EncounterFailed`。

实测（Main）：预算 30 → 26 → 21 → 15 → 6 → 0，并发稳定封顶 6，预算耗尽且场上清空后 `IsCompleted=True`。

## 击退的实现约定

`CharacterMovement` 把**输入速度**与**外力速度**分开维护：外力指数衰减，绝不能再从刚体速度里读回来叠加（否则击退会正反馈放大，实测会把玩家推飞 20+ 单位）。新增位移类效果请走 `AddImpulse` / `BeginDash`。

## 物理层约定

| 位 | 用途 |
|---|---|
| 1 | 场景静态碰撞（地面/墙） |
| 2 | 玩家单位 |
| 3 | 敌人单位 |
| 4 | 保留（投射物不走物理层，用射线查询） |

`Unit._Ready` 按阵营自动设置 `CollisionLayer`；玩家 `CollisionMask` 额外包含单位层（会被敌人挡住），敌人只碰撞场景（避免成群卡死）。

## Hit Policy

`OncePerTarget` / `IntervalPerTarget` / `OncePerPhase` / `MultiHit`，由攻击自身携带。
激光、火焰区域、旋转攻击用 `IntervalPerTarget`；多段斩击用 `OncePerPhase`+相位号或 `MultiHit`。

## 事件链

`EventContext`（ChainId / Depth / Source / OriginalSource / Target / OriginalTarget）随每次命中传递，`MaxDepth = 4`，`Child()` 在超深度时返回 null。
目前只有命中链使用；阶段 5 的 Trigger/Effect 会复用它来防递归。

## 已知限制

- 格挡只有正面判定与破防阈值，没有格挡硬直、格挡反击与格挡资源。
- 状态效果只有 Slow / Weaken / Burn（Burn 按秒走一次普通伤害流程，会被护甲/护盾吸收）。
- 击退为瞬时冲量 + 衰减，没有受击硬直与 Super Armor 的区分。
- 传送类位移（牵引/降临）需要额外同步物理体，见 `docs/testing.md`。
- 敌人没有寻路，只靠射线避障；复杂凹形地形仍可能绕远。
