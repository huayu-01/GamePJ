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
- **护甲是所有单位的通用属性，但"有甲"要看数据**：`Unit` / `UnitConfig` 基类上人人都有 `StartingArmor`（**开局护甲**），
  实际谁有甲由单位表给（玩家 40/140/60、守卫 60、冲锋兵 30、技能精英 90、**首领 0**）。
  ⚠️ **护甲没有上限**（用户 2026-10-12 第三轮 C1：「护甲没有上限额度，所以可以删除掉 `MaxArmor` 的」）——
  它是**累加值**：`StartingArmor` 只是"进场给多少甲"，之后被打就减、只有 `Unit.GrantArmor()` 能加，
  **不存在"最大护甲"**（`MaxArmor` / `MaxArmorMultiplier` / `MaxArmorAdd` / `UnitConfig.MaxArmor` 全部已删除）。
  ⚠️ **甲为 0 时管线会自动跳过两件事**：① `CombinedDamageReduction()` 里 `Armor > 0f ? ArmorDamageReduction : 0f`
  → 护甲减伤那一项取 0（所以场景里留着 `ArmorDamageReduction = 0.3` 也不会偷偷减伤）；② `Armor` 阶段整段跳过
  （不吸收伤害、也不发 `ArmorBroke`）。**护甲现在唯一的"额外获取手段"是精英怪的 `EliteModifier`（`ArmorAdd +120` → `GrantArmor`）**，
  首领没有任何获取途径（2026-10 用户要求「去掉首领的甲」；判据见 [config.md](config.md) 的「首领去甲 + 单位动画实测」）。
- **死亡**：HP ≤ 0 立即 `IsDead = true` 并从碰撞层移除（逻辑死亡与视觉死亡分离）；死亡单位不再被射线/AOE/索敌选中。

## 护甲的表现层（2026-10 第六轮，**管线一行没改**；判据在 C1 改过一次）

用户需求 §五 #8：「玩家和怪物**有护甲时血量上应该要有其他效果来显示护甲的进度和碎甲效果**，甚至是**模型的额外显示效果**」。

| 要显示的东西 | 读的是哪个既有事实 | 谁在读 |
|---|---|---|
| **护甲进度**（血条上多一条甲条） | `Unit.Armor` / `Unit.ArmorPeak`（`Armor > 0` 才显示；比例 = `Armor / ArmorPeak`，**甲打光后整条隐藏**） | `WorldOverlay.UpdateBar` / `WorldOverlay.CreateBar`（敌人）/ `PlayerHud.UpdateArmorBar`（玩家） |
| **碎甲**（甲条闪白 + 模型上炸一圈） | **`Unit.ArmorBroke` C# 事件** —— 它只在**甲从 >0 掉到 0** 那一次触发（`Unit.ReceiveHit` 的 Armor 段），所以"甲被扣"与"甲碎了"天然分开 | `WorldOverlay.OnUnitArmorBroke` / `PlayerHud.OnPlayerArmorBroke` |
| **跳字里的「甲 N」** | `DamageResult.ArmorLost`（`Unit.Damaged` 事件带出来的那次结算结果） | `WorldOverlay.OnUnitDamaged` |
| **模型的额外效果** | 上面那个 `ArmorBroke` → `Scripts/UI/ArmorBreakFx.Spawn()`：水平扩散环 + 一层快速褪去的亮壳，0.42 秒自己回收 | 同两处 |

> **C1 改了什么（2026-10-12 第三轮）**：显示判据从 `MaxArmor > 0` 改成 **`Armor > 0`**（用户：「有甲了才会显示」），
> 所以**甲打光之后连空壳都不留**（`Armor == 0` 整个隐藏）；`ArmorBroke`（碎甲表现）**判据与触发点一行没动**。
> 比例的分母换成运行期派生的 `Unit.ArmorPeak`（这个单位见过的最高甲，不来自任何配置、不参与结算）——
> 护甲没有上限之后 `Armor` 自己算不出"还剩几成"，而甲条要显示进度。
> ⚠️ **精英那一路的语义变了**（C1 的必然结果）：`EliteModifier.ArmorAdd` 以前是"抬高护甲上限"
> （`MaxArmorAdd` 修正器），对**本来 0 甲的敌人**（追猎者/自爆兵/远程兵…）只会把上限抬到 120 而甲停在 0
> （等于空壳、不吸收伤害）；现在它是**直接加甲**，所以那些精英怪真的多出 120 甲。有甲敌种（守卫 60→180、冲锋兵 30→150）与改动前一致。

⚠️ **甲为 0 的单位（首领 / 木桩）不会误播碎甲**：`Unit.ReceiveHit` 的 Armor 段整段跳过，`ArmorBroke` 根本不会触发
（首领去甲的判据见上面的「护甲是所有单位的通用属性」那一条）。实测：`白板首领实例：生命 315 护甲 0`，战斗中没有任何碎甲特效。

⚠️ **"只掉甲、没掉血"的单位也会出血条**：`GameplayEventBus` 的 `Damaged` 事件里 `Value` 只有 `HealthLost`
（甲被吸收时那条事件是 0），所以血条登记多了一条判据 —— 见 `WorldOverlay.OnUnitDamaged` 与 `UpdateBar` 里的注释；
**没有甲的单位（`ArmorPeak <= 0`）行为与改动前逐位一致**（只看血量），所以"满血不显示血条"这条老设计对无甲怪没有任何变化。

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
- 已有能力：`MeleeAttackAbility`、`ProjectileAttackAbility`、`AreaAttackAbility`（可自爆）。
  ⚠️ **2026-10-15 P6a**：`DashAbility`（位移+无敌帧）与 `BurstFireAbility`（火力倾泻）**已随旧角色删除**
  —— 前者的替代品是角色 A 的「后撤」`BackdashAbility` / 角色 B 的「闪」`BlinkDashAbility`，
  "火力倾泻"在新设计里没有对应物。其余共用子类（含 `MeleeComboAbility` / `EnergyWaveAbility` /
  `ArrowVolleyAbility` / `GuardStanceAbility` / `FallAttackAbility` 等）**都在**。
- 已有行为：`ChaseBehavior`、`KeepDistanceBehavior`、`RetreatBehavior`、`RepositionBehavior`、`MeleeAttackBehavior`、`RangedAttackBehavior`。
- **能力与行为分离**：具备跳跃/冲刺能力不代表 AI 会使用，是否使用由 Behavior 与配置决定（执行文档 §4.4）。

## 冷却缩减 / 技能层数 / 蓄力（P2 通用机制，2026-10-15）

设计文档第 13/14 行的两个**通用机制** + 完美时机 + 冷却缩减（CDR）已经落在 `Ability` 基类上，
**接入契约与"加一个蓄力技能要做什么"见 [abilities.md](abilities.md)**。这里只记战斗侧的要点：

| 机制 | 代码位置 | 数据位置 | 默认行为 |
|---|---|---|---|
| **冷却缩减（CDR）** | `Ability.EffectiveCooldown`（`Finish()` 写的就是它）+ `ReduceCooldownFraction` / `ReduceCooldown` / `RefreshCooldown` | 技能行 `AbilityConfig.CooldownMultiplier`（0 = 不覆盖）+ `StatSheet` 的 `CooldownMultiplier`（单位级常驻，洞悉/速攻） | 两处都没配 → `Cooldown × 1 × 1`，**与改动前逐位相同** |
| **技能层数** | `Ability.MaxCharges` / `RechargeSeconds` / `ChargesRemaining`（消耗在 `TryActivate`，回复在 `TickCooldown`） | 技能行 `MaxCharges` / `RechargeSeconds`（0 = 不覆盖） | `MaxCharges ≤ 1` 走改动前那条 `CooldownRemaining` 倒计时，层数代码**不参与判定** |
| **蓄力 / 完美时机** | `Ability` 的三段状态机（`OnInputHeld` / `OnInputReleased` / `OnChargeReleased` / `OnChargeAborted`） | 技能行 `ChargeBaseSeconds` / `ChargeStepSeconds` / `ChargeStepScale` / `ChargeMaxSteps` / `ChargeMaxSeconds` / `PerfectWindowSeconds` | 非蓄力技能（`HoldToCharge = false`）**一行代码都不进** |

**实现纪律**（用户点名要的那条）：**代码里没有任何百分比 / 秒数 / 伤害数字**。
`0.2s 完美窗口`、`-80% 冷却`、`每次 +30%` 这类值全部来自技能行或增幅行；
`StatSheet` 新增的 `CooldownMultiplier` 基础值 1 在 `Unit.SeedStats()` 里给
（乘法基数没有基础值会静默变成"零冷却"，见 [architecture.md](architecture.md) 第 8 条）。

**证据**（`--selftest`，`run_20261015_133715.log`，**退出码 0**）：自测里有一段专门的机制取证
（`机制·默认值` / `机制·冷却缩减通路` / `机制·CDR 落在 Finish() 上` / `机制·一次性 CDR 接口` /
`机制·技能层数…` / `机制·蓄力未完成松手` / `机制·完美时机` / `机制·过完美窗口松手` / `机制·后置蓄力` /
`机制·首领增援走单位表`），全部 ✅；同一份日志里的旧硬指标逐位不变（见 [abilities.md](abilities.md) §5）。

## 标记 · 无敌帧 · 滞空（P3 新机制，2026-10-15）

角色 A［猎手］的武器 2/3 + 技能 1/2 + 被动带来了 10 条新机制，**全部"只加不改语义"**
（每条都有"不配置时逐位等于改动前"的判据）。清单与触发点：

| 机制 | 代码位置（唯一实现处） | 数据位置 | 不配置时 |
|---|---|---|---|
| **标记（可叠加、不限单目标）** | `Unit.ApplyStatus` 的 `MaxStacks` 路径（+ `ApplyStatusModifiers` 按层数重算修正器）+ `Unit.GetStatusStacks` | `Config/Tables/Effects/eff_mark.tres`（`MaxStacks` 列） | `MaxStacks ≤ 1` 走原来那一行"同名刷新时间、不叠加层数"，逐位不变 |
| **无敌帧（按动画帧开关）** | `BackdashAbility`（读 `Ability.AnimEventEnd` → `Unit.IsInvulnerable`） | 技能行 `AnimEvent = Dodge` / `AnimEventEnd = Dodger` | 留空 `AnimEventEnd` → `OnAnimationEvent` 的过滤与改动前逐字等价 |
| **"被击中则无效化"** | `HawkEyeAbility.OnOwnerHit`（订阅 `Unit.HitReceived` —— 它在 `ReceiveHit` 里**先于无敌判定**触发，所以置位 `IsInvulnerable` 就等价于"这一次无效化"，`Unit` 一行没改） | 技能行 `ChargeBaseSeconds` + `DodgeAbilityId` | 没有这个技能就没有订阅 |
| **滞空（按住跳跃键减缓 90% + 脚底光环）** | `Scripts/Combat/AirHoverController.cs`（玩家场景里的常驻 `AirHover` 节点；`ProcessPhysicsPriority = 50` 保证在 `CharacterMovement` 之后跑） | `LeapAbility` 的 `HoverSeconds` / `HoverFallSlowPercent` / `Ring*` 四项（技能行 Params） | 没人 `Grant()` 时它每帧只做一次 `HoverRemaining <= 0` 早退 |
| **额外跳跃次数（上限 3）** | `CharacterMovement.SetExtraAirJumps(value, cap)`（幂等写入 + 只抬高不压低） + `AirJumpPassiveAbility` | 技能行 `Grant` / `MaxExtraAirJumps` | 场景里 `ExtraAirJumps` 是 0，不接线就没有二段跳 |
| **散射（多角度多弹）** | `ArrowVolleyAbility.FireVolley`（绕 `Vector3.Up` 旋转，按发数对称展开） | 技能行 `Count` / `SpreadStepDegrees` | `Count = 1` / `SpreadStepDegrees = 0` → 与单发完全相同 |
| **下坠子弹（飞一段距离后才受重力）** | `Projectile.GravityStartDistance`（`TravelledDistance` 累加判断） | 技能行 `GravityStartDistance` / `GravityScale` | `GravityStartDistance ≤ 0` → 走改动前那条"一发射就受重力" |
| **击退（射速较高时）** | `ArrowVolleyAbility.FireVolley`（`shotSpeed < KnockbackMinSpeed` 就不给击退） | 技能行 `Knockback` / `KnockbackMinSpeed` | `KnockbackMinSpeed = 0` → 永远给击退（= 旧行为） |
| **追踪弹（飞向最近被标记目标）** | `Projectile.SteerTowardsMark`（Slerp 转向；`HomingStatusId` 空则整段不进） | 技能行 `HomingStatusId` / `HomingTurnRateDegrees` / `HomingRange` | 留空 `HomingStatusId` → 弹道与改动前逐位相同 |
| **准星视角放大** | `HawkEyeAbility.StepZoom`（改当前 `Camera3D.Fov`，收招时还原到施法前的值） | 技能行 `ZoomFov` / `ZoomSharpness` | 没有这个技能就没有人碰 `Fov` |

**两条实现纪律**（与 P2 同一条）：

1. **数值全在表里**：上表右列的每一项都是 `AbilityConfig` 的列或 `Params` 条目，
   代码里没有"12 m/s 的后撤力""90% 的减缓""5 层标记"这类字面量（`ArrowVolleyAbility` 的
   `[Export]` 只是"表读不到时"的兜底，与项目其它组件同一条纪律）。
2. **默认行为逐位不变**：上表最后一列就是判据 —— 每一条都能"什么都不配"地退化成改动前的样子。

### 标记（Mark）为什么做成状态而不是新容器

设计文档的定义是「**可叠加**且**不限定单个目标**的 buff」。`Unit` 的 `_statuses` 本来就是**单位级**容器
（每个单位一份），所以"不限单个目标"是免费的；缺的只是"可叠加"（`refactor-survey.md` 硬编码审计第 6 条）。
于是加一列 `EffectConfig.MaxStacks`：层数记在 `ActiveStatus.Stacks`，修正器按层数各加一份，
读层数用 `Unit.GetStatusStacks(id)`。`eff_mark.tres` 是第一条这样的行（无任何数值修正，纯计数），
所以 `EffectConfig.IsStatus` 额外认「`MaxStacks > 1`」。

### 无敌帧为什么需要"第二个事件帧"

设计文档（技能 1「后撤」）：「帧 `Dodge` 到 `Dodger` 期间无敌」—— 这是**一段区间**，
而 `Ability` 原来只认一个 `AnimEvent`。新增列 `AnimEventEnd`（`Ability.AnimEventEnd`）后，
两个帧都会送到 `OnAnimEvent`，子类按名字分支即可（`Dodge` 开、`Dodger` 关）。
`BackdashAbility` 还带一条**安全时长**（`InvulnerableSafetySeconds`）：`Dodger` 帧没来也要把无敌关掉，
否则生成表配错会让玩家永久无敌。

### 证据（自测 + 真人输入）

`--selftest`（**退出码 0**）新增 case 32–41，逐条打印 ✅/🔴：短弩武技1（3 发）·
短弩武技2（0 档 3 发 / 3 档 9 发）· 弓武技1（每档 ×1.3 伤害、+30 弹速）·
刹那之眼（蓄满后放大视角 78→32.4、每 0.5s 标记到真敌人、基础蓄力被击中 → 无效化 + 不消耗层数的后撤）·
标记（3 层→封顶 5 层）· 追踪弹（与目标夹角逐帧 90.9°→47°）· 后撤（无敌期间挨 40 点掉血 0、朝向不变）·
被动（`ExtraAirJumps` 0→1）· 跃升（起跳 16 m/s + 3s 滞空）· 滞空（垂直速度 -20→-2）。

真人输入（自己起的进程 + 真鼠标 / 真键盘，日志原文）：

```
19:03:00 [Combat] 箭矢齐射：skl_a2_w1_1 发数 3（基础 3 + 档 0×0） 单发伤害 12 弹速 200 夹角 20°（… 击退 6 …）
19:03:03 [Combat] 箭矢齐射：skl_a2_w1_2 发数 9（基础 3 + 档 3×2） 单发伤害 12 弹速 255 夹角 20°（蓄力松手，蓄力 1.51s 档 3 …，方向 (-0, -0, -1)）
19:04:39 [Animation] 技能动画开播：Player → skills/Skill_A1_Backdash（… 事件帧 2 个：Dodge@0s、Dodger@0.35s）
19:04:39 [Combat] 后撤：skl_a1_skill_1 后撤力 12（方向 (0, 0, 12)，**不改变朝向**）｜无敌帧开（Dodge）
19:04:39 [Combat] 后撤：skl_a1_skill_1 无敌帧关（Dodger）
19:05:06 [Animation] 技能动画开播：Player → skills/ATK_A3_HawkEye（… 事件帧 3 个：AimOn@0s、Mark@0.1s、ATK@0.18s）
19:05:06 [Combat] 刹那之眼：skl_a3_w1_2 开始蓄力（基础 0.3s，蓄满后放宽视角到 FOV 32 …）
```

截图：`docs/screenshots/p3_hover_ring_airborne.png`（真按 E + 按住空格 → 角色悬空、脚底光环可见）、
`p3_backdash_moment.png`（真按 Q 之后的一帧）、`p3_arena_crossbow.png`、`p3_hawkeye_charge_hud.png`、
`p3_crossbow_volley3_log.png`（编辑器输出面板里的 `箭矢齐射 发数 3` 那一行）。

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
实测：守卫 MaxHealth 130→325、护甲 60→180（`ArmorAdd` 走 `Unit.GrantArmor`）、scale 1.35，并加入 `elite_units` 组。
⚠️ C1 之后 `ArmorAdd` 是**纯粹的加甲**：本来 0 甲的敌种（追猎者/自爆兵/远程兵…）精英化后也会真的拿到 120 甲
（改动前那条 `MaxArmorAdd` 只把它们的上限抬到 120、甲仍是 0，等于一层不吸收伤害的空壳）。

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

## 运行期新生成的单位「同帧就以 0 血死亡」（2026-10-15 追查结论）

**现象**（上一轮调试时的原话）："在 run 刚重启后手动 `add_child(Chaser.tscn)` 的敌人**有时同帧就以 0 血死亡**"
（`死亡 Chaser(0) 击杀者=-`），而定期刷怪出来的敌人正常（45 血）。

**结论：不是 bug、也不是 `StatSheet` 那条老 bug 的另一种表现 —— 是"清场 + 两个读数口径"叠出来的错觉。**
**稳定复现 12/12**（4 个进程 × 每进程 3 轮"同一帧里裸 add_child + 立刻重开一局"），根因由**托管调用栈**钉死：

```
[Combat] 死亡 Chaser(0) 击杀者=-
      受伤调用栈（Chaser(60)：致命的一发 / 无来源伤害）:
            at ActRoguelike.Combat.Unit.ForceKill(Unit killer) in Scripts/Combat/Unit.cs:line 538
            at ActRoguelike.Run.StageController.ClearField() in Scripts/Run/StageController.cs:line 340
```

### 三条并列的原因（缺一条都拼不出这个现象）

| # | 事实 | 代码位置 |
|---|---|---|
| ① **谁杀的** | `StageController.ClearField()` 把 `enemy_units`/`boss_units` 里**所有活着的单位** `ForceKill()`（**不传击杀者**）。它在**每次关卡建立/重置**（`RunController.BeginRunWithCharacter → ApplyStage → ResetStage`）与 **Boss 阵亡**时各跑一次，**一帧内扫完**。手动 `add_child` 的 Chaser 正好落在这个窗口里 → 它"没被任何攻击打中"就死了。注意它是**同步**调用：`add_child` 与"重开一局"同一帧发生时，死亡与生成就在同一帧（这正是"同帧"的来源） | `StageController.cs:326/332`（清场）、`:867`（Boss 阵亡）、`Unit.cs:525`（`ForceKill` → `Die`） |
| ② **"0 血"是日志口径，不是血量** | `Unit.Die()` **先把 `Health = 0f` 再发 `Died`**，而 `GameLog` 的 `Describe(unit)` 打的就是 `unit.Health` → **任何**死亡日志都是 `(N)` 里的 0（`docs/logging.md` 的 `死亡 Boss(0) 击杀者=Player(260)` 是同一回事）。它**不能**用来说明"死的时候有多少血" | `Unit.cs:473-478`、`GameLog.cs:81/107` |
| ③ **`Health` 在 `_Ready` 才有值** | `Unit.Health` 唯一被赋值的地方是 `_Ready`（`Health = MaxHealth`）→ **进树前**、以及 `NodeAdded`/`_EnterTree` 那一刻读它都是 **0**（探针实测 `进树前 生命=0 上限=60`；`Damage` 用 `Health` 当基数的读法在那一刻也会读到 0）。所以"刚生成的单位 0 血"往往只是**读早了** | `Unit.cs:134-148` |

### 不是老 bug 的另一出口（逐条取证）

- **上限没有被算成 1**：探针在 3 个时刻打印 `MaxHealth`：进树前 `60`、进树时 `60`、进树后 `60`（老 bug 的表现是 `45→1`）。
  `SeedStats()` 仍在构造函数里，`_EnterTree` 捕获的就是 60。
- **"先 AddChild、后 ApplyTo"也不会被 `_EnterTree` 的捕获吃掉**（这是最像"竞态"的一条路径，实测排除）：
  `UnitConfig.ApplyTo → Unit.ApplyBaseStats` 会**重新写** `_baseMaxHealth`，所以晚套用得到的仍是 `60 → 60`，不是 1、也不是 0。
  （⚠️ 生产代码里 4 处生成路径**本来就是"先 ApplyTo、再 AddChild"**：波次刷怪 `EncounterController.cs:571`、定期刷怪
  `:383`、首领 `:856`、护卫 `:895`；`SummonAbility.cs:61` 召唤物同序。顺序没有被违反过。）
- **生产路径不可能"生成即死"**：落点统一走 `MapManager.RingPoint/SpawnPoint`（导航网格不可用时是"随机环点 + 地面射线 + 球形避障"），
  结果恒为"地面 +0.3"或"玩家 +0.3"，**落不进区域杀**（中心 Y=-38、厚 20）。实测游离怪 `Chaser(45)`（= 60×0.75 全局倍率）、
  探针同序生成的 `@CharacterBody3D@54(60)` 观察 30 帧以上都不掉血。
- **另一条会打出同一条日志的路径（但不是同帧）**：把单位裸 `add_child` 到**没有地板**的位置（探针用 `(60, 30, 60)`）——
  它会掉下去，约 3 秒后 `落出地图（进入区域杀）→ 击杀` + `死亡 …(0) 击杀者=-`（`World/KillZone.cs:51-52`）。
  判据：这条路径**一定会多一行 `落出地图（进入区域杀）→ 击杀`**；`ClearField` 那条**一行多余的都没有**。

### 影响面判断

- **只影响调试/自测里"手动 `add_child`"这条路径**（正常玩法里没有"绕过单位表的手动生成"）。
  生产路径一个都没踩：波次/游离怪/首领/护卫/召唤物都是"先 ApplyTo、再 AddChild + 可行走落点"。
- **但这条边界对"以后要做的动态生成"是真的**：任何**在关卡建立/重置窗口内**（`ApplyStage`/`ResetStage`/Boss 阵亡那一帧前后）
  生成进 `enemy_units` 的单位，都会被 `ClearField` 静默扫掉（日志只有 `死亡 X(0) 击杀者=-`）。
  将来做"首领召唤的小怪""堕落类机制"时，要么生成时避开这个窗口，要么把它登记进 Encounter 自己的名单（`ForceComplete` 只清自己那份）。
- ⚠️ **一个已知的边界不一致**（本轮只记录、没改）：`ClearField` 只扫 `enemy_units` + `boss_units`，而
  `Unit._Ready` 把**带 `Summon` 标签**的单位放进 `summon_units`（`Unit.cs:143`）—— 也就是说**敌方**召唤物（若将来给了 `Summon` 标签）
  **不会**随关卡切换被清掉；目前 `Summon` 标签只有玩家无人机（`summon_drone.tres`），所以还看不出问题。
- ✅ **已修（2026-10-15，P2）**：`BossPressure.SpawnAdds()`（`Scripts/Boss/BossPressure.cs`）以前**只 `AddChild`、完全不套单位表**
  （用的是 `Chaser.tscn` 的场景值 **60 血**）—— 既漏掉全局倍率（`enemy_global` 的 ×0.75），也漏掉敌人行上的 **AI 档案与技能行**。
  现在它走**与其它 4 处生成路径同一条**：`ConfigRows.ByScenePath<EnemyConfig>(AddScene.ResourcePath)?.ApplyTo(add)` →
  `AddChild` → `ConfigIndex.ScaleEnemy(add, this, false)`（`EncounterController.cs:571/383/856/895`、`SummonAbility.cs:61` 同序）。
  ⚠️ **效果是血量下降，这是预期修正**：首领召唤的小怪 **60 → 45 血**（= 敌人行 60 × 全局倍率 0.75），
  并且真的带上 `ai_chaser` 的行为与 `skl_enemy_chaser_melee` 技能行（技能 Id 不再是 C# 类名）。
  自测取证（`run_20261015_133715.log`）：`✅ 机制·首领增援走单位表：SpawnAdds 生成 Chaser 生命 45（= 敌人行 60 × 全局倍率 0.75 = 45）｜场景值（没套表的那一份）60（改前就是这个值）｜标签 [Melee,Chaser] 技能行 Id=skl_enemy_chaser_melee`。
  **旧硬指标没有漂**：`白板首领 315/甲 0`（走的是 `EncounterController` 那条路，本轮**一个字没改**）、节点布置 `10（2/5/3）`、
  `pressure=3 pmods=3 bank=0`、玩家 `270/40/7`、端到端 `True/True`、索引 `4/4/10/1/1/27/7/9/8/6/3/11/8` 全部逐位一致。

### 修了吗

**没有改生产代码** —— 清场（`ClearField`）是**有意行为**（"清掉上一关残留的单位"），把它改掉会动到关卡流程语义，
而本现象在玩法上无害。**建议的修法（要做再加，风险极低）**：给 `Unit.ForceKill`/`Die` 的日志带一个**来源标记**
（`ClearField` / `SummonLifetime` / 自爆 / 区域杀），把 `击杀者=-` 换成"击杀者=（关卡清场）"之类 ——
纯日志改动、只加一个可选参数，能让下一个人一眼分清来源。**复现脚本**（任务 1 用的临时探针）：
挂 `SceneTree.NodeAdded` 观察每个 `Unit` 的进树生命 + 订阅 `Unit.Damaged`/`Died` 打印 `System.Environment.StackTrace`
（只留 `ActRoguelike` 的帧），然后在同一帧里"裸 `add_child` + `RunController.BeginRunWithCharacter(...)`"。

## P4 新增战斗机制（角色 B［武器大师］，2026-10-15）

> 设计权威：`F:\Godot项目\3d肉鸽雨中哈迪斯\角色设计.txt` 第 98–163 行；接入契约见 [abilities.md](abilities.md) §8。

| 机制 | 落点（文件 / 触发点） | 语义要点 | 表列 |
|---|---|---|---|
| **多段连招** | `Scripts/Abilities/MeleeComboAbility.cs`（**三把近战武器共用这一个类**） | 一次施法 = 打一段 + 开一个输入窗口；**事件帧 `ATK` 才判定**、`ATK` 同时开窗口；窗口内再按 → 段 +1（`TryPress`）；窗口超时 → `Finish()` 进冷却 | `Config/Tables/SkillSeries/<技能行Id>.tres` 的 `ComboAnimations` / `StepDamageScales` / `StepBoxSizes` / `StepBoxOffsets` / `StepActiveTimes` / `ComboWindowSeconds` / `ComboLoop` |
| **近战斩断子弹** | `Projectile.CutBySceneName` ← `MeleeComboAbility.CutBullets`（`ATK` 帧） | 判据是"投射物的**来源场景名**"（`Bullet`），**不是敌我** → 友军的也清；**能量波在排除名单里 → 斩不断** | `CutSceneNames` / `CutExcludeSceneNames` / `CutRadius` |
| **下落攻击** | `Scripts/Abilities/FallAttackAbility.cs` | 空中 + 按键（`fall_attack` = Ctrl）才可用；`DiveSpeed` 直接写垂直速度；下落期间命中盒常开（`IntervalPerTarget`）；**高度取"这次离地后净下落多少米"**（`CharacterMovement.FallHeight`）；落地抛 `Landed` 事件 | `DiveSpeed` / `HeightStep` / `DamageBonusPerStep` / `MaxDamageBonus` / `HomingMode` / `MaxDiveAngleDegrees` |
| **冲刺（穿透 + 可视化路径）** | `Scripts/Abilities/BlinkDashAbility.cs` + `Scripts/Combat/DashPathIndicator.cs` | 冲刺期间 `CollisionMask` 只留世界层（**穿透单位、仍撞地形**），结束后**一定还原**；方向保留 Y 分量（可冲至空中）；路径 = `ImmediateMesh` 折线 | `Distance` / `Duration` / `PierceUnits` / `ShowPath` / `PathColor` / `PathWidth` / `PathSteps` |
| **架势（架）** | `Scripts/Abilities/GuardStanceAbility.cs` | 架势内被命中 → 把这次命中**无效化**（`Unit.IsInvulnerable`）并立刻收招；**每帧把"自己设的那次规避"清掉** → 下一次命中仍会被规避（"挡一下"而不是"无敌到架势结束"） | `StanceSeconds` / `GuardStartSeconds`（`0.75` 来自文档） |
| **武器抛出 / 收回** | `Scripts/Abilities/WeaponThrowAbility.cs`（镰刀/长枪共用）+ `ScytheThrowAbility` / `SpearThrowAbility` | 抛出 → 旋转 → 每 `DamageInterval` 秒一次判定 → 再次按键 / 靠近 / 到最远距离收回；收回途中也能再打一次 | `ThrowSpeed` / `ReturnSpeed` / `MaxThrowDistance` / `RecallOnSecondPress` / `RecallDistance` / `SpinDegreesPerSecond` / `DamageInterval` / `DamageOnReturn` |
| **抛物线投射 + 路径可视化** | `SpearThrowAbility` + `DashPathIndicator.SampleArcToDistance` | 基础蓄力 1s；每蓄力 0.25s（档）**伤害 ×1.2、水平速度 ×1.2、路径中线 ×1.2**；"路径中线延长"是**非倍率维度**（速度放大、飞行时间不变） | `ArcGravity` / `FlightSeconds` / `ArcPathSteps` + 基类蓄力列 |
| **武器不在手上 → 武技1 不能用** | `Scripts/Combat/WeaponGate.cs`（场景里的 `WeaponGate5` / `WeaponGate6`） | `WeaponThrowAbility.InFlightChanged` → `WeaponGate` → `MeleeComboAbility.SetExternallyBlocked`；默认按节点名推两个技能行 Id | — |
| **落地额外一圈** | `Scripts/Abilities/LandingBurstAbility.cs` | 挂 `<技能行Id>_fall` 的 `Landed` 事件，**不改上游那一次攻击**，只多开一圈命中盒；CD 就是"触发间隔" | `TriggerAbilityId` / `DamageScale` / `ActiveTime` |
| **朝准星方位冲下** | `FallAttackAbility.HomingMode = Crosshair`（表值） | 夹角超过"指向范围"就夹到最大范围的同方向；准星没指向地面就直直下落 | `HomingMode` / `MaxDiveAngleDegrees` |

> ⚠️ **自测口径的两个限制**（也写在代码注释里）：
> ① `Area3D` 的重叠列表要等**一个物理帧**才更新，而自测是在同一帧里连续驱动能力的 ——
> 所以"下落攻击/落圈到底打掉了多少血"这一类**不在自测的判据里**（判据是"落地事件触发"与"上游伤害没变"）；
> ② 同理，自测用 `MeleeComboAbility.DebugFireAnimEvent()` **模拟"事件帧到了"**来取证连招推进 ——
> 真人那一路仍然完全由 `UnitAnimator` 的事件帧驱动（同一条 `OnAnimEvent`）。

## 已知限制

- 格挡只有正面判定与破防阈值，没有格挡硬直、格挡反击与格挡资源。
- 状态效果只有 Slow / Weaken / Burn（Burn 按秒走一次普通伤害流程，会被护甲/护盾吸收）。
- 击退为瞬时冲量 + 衰减，没有受击硬直与 Super Armor 的区分。
- 传送类位移（牵引/降临）需要额外同步物理体，见 `docs/testing.md`。
- 敌人没有寻路，只靠射线避障；复杂凹形地形仍可能绕远。
