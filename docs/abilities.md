# 能力机制：蓄力 · 完美时机 · 技能层数 · 冷却缩减（P2 通用地基）

> **本轮范围**：把设计文档第 13/14 行的两个**通用机制**（技能层数、蓄力）+ **完美时机** + **冷却缩减（CDR）**
> 做成**6 把武器公用的地基**。落地位置：`Ability` 基类（机制）+ `AbilityConfig`（表列）。
> **本文件就是"后续 6 把武器照它做"的接入契约。**
>
> 权威：`F:\Godot项目\3d肉鸽雨中哈迪斯\角色设计.txt`（第 13/14 行 = 层数与蓄力的定义）；
> 澄清：`docs/character-redesign.md`；动画那一段的机制见 [anim-pipeline.md](anim-pipeline.md)；
> 战斗管线见 [combat.md](combat.md)；架构铁律见 [architecture.md](architecture.md)。

---

## 0. 一句话

**加一个蓄力技能 = 加/改一行表（`Config/Tables/Skills/skl_*.tres` 的列与 Params），不写 C#。**
机制（三段蓄力 / 完美窗口 / 后置蓄力档位 / 层数 / CDR）全部在 `Scripts/Abilities/Ability.cs` 里，
子类**只负责"按蓄力结果算效果"**，不自己计时。

```powershell
# 编译（必须 --no-incremental；要求 0 warning / 0 error）
& 'F:\Godot项目\项目a\build.ps1' --no-incremental -v minimal
# 自测：CLI 直跑（拿退出码）
& 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' `
  --path 'F:\Godot项目\项目a' res://Scenes/Main.tscn -- --selftest
# 或 user://selftest.flag（跑完必须删）+ 编辑器/MCP 的 run_project
```

---

## 1. 蓄力（charge / hold）—— 三段状态机

设计文档第 14 行：

> 蓄力：当按键被按住时激活，按键抬起时结束蓄力并释放效果，**基础蓄力完成之前抬起则不会释放效果**。

`Ability` 把一次蓄力拆成**三段**（第三段只有短弩/弓/长枪用得到）：

```
按下(激活)                基础蓄力完成                     窗口结束
   │──── 基础蓄力段 ────│──── 完美时机窗口 ────│──── 后置蓄力段（逐档） ────→ 一直按住
   │  ChargeBaseSeconds  │  PerfectWindowSeconds │  每 ChargeStepSeconds 升一档
   └─ 这段时间里松手 = 不释放（LastReleaseAborted） └─ 窗口内松手 = 完美释放   └─ 每档 ChargeStepScale 倍
```

### 1.1 运行时状态（**给 UI 与自测读**，全是只读属性）

| 属性 | 含义 | 典型用途 |
|---|---|---|
| `IsCharging` | 是否**正在**按住蓄力（松手立刻 false） | 表现层开关 |
| `ChargeTime` | 本次蓄力累计秒数（松手后**保留最后一次的快照**） | 自测 / 排障 |
| `ChargeProgress` | 蓄力进度 0~1（**按基础蓄力算**） | 准星指示器"往中心缩" |
| `ChargeBaseComplete` | 基础蓄力是否完成 | 判断"松手会不会释放" |
| `IsPerfectWindow` | 是否处在完美时机窗口里 | 准星变色 |
| `PerfectWindowSeconds` | 窗口长度（表列，能量掌武技2 = 0.2s） | — |
| `ChargeSteps` | 后置蓄力已升到第几档（0 = 只有基础段） | 短弩 +N 支 / 长枪路径增强 |
| `ChargePowerScale` | 后置蓄力的总倍率 = `ChargeStepScale ^ ChargeSteps` | 弓 / 长枪的伤害与弹速 |
| `ChargeMaxStepsEffective` | 档数上限（把 `ChargeMaxSteps` 与 `ChargeMaxSeconds` 合成一个数） | 自测 / UI 显示 |
| `LastReleasePerfect` | 上一次**松手释放**是不是完美时机 | 表现层 / 自测 |
| `LastReleaseAborted` | 上一次松手是不是"没蓄满就松"（没释放任何效果） | 自测 |

### 1.2 两个回调（子类只覆写这两个）

```csharp
// 蓄满之后松手：三个入参就是那一刻的蓄力结果（也可以直接读上面那些只读属性）
protected override void OnChargeReleased(bool perfect, int steps, float powerScale) { /* 按结果算效果 */ }

// 基础蓄力没完成就松手：设计文档要求"不释放"，这里通常只收尾（停动画 / 退出施法态）
protected override void OnChargeAborted() { /* 一般什么都不做，或 Cancel() */ }
```

**基类已经把这几件事做完了**（子类不要重复实现）：

- `OnInputHeld(dt)` 累加 `ChargeTime`、换算档位（`RecountChargeSteps`）；
- `OnInputReleased()` 判"蓄满没有" → 分发到上面两个回调之一；
- **未蓄满就松手的施法不消耗冷却**（`Cancel()` 路径，`CooldownRemaining` 不动）；
- 每次 `TryActivate()` 前把蓄力状态清零（`ResetChargeState`）。

### 1.3 接入契约：**加一个蓄力技能要做什么**

1. **行必须有的列**（`AbilityConfig`）：
   `InputAction`（哪个键）、`HoldToCharge = true`、`AnimName`（**必须存在**，否则技能不执行 ——
   用户裁定「没有对应动画 → 不执行」）、`Cooldown`。
2. **蓄力列**（全部是表值）：`ChargeBaseSeconds` / `ChargeStepSeconds` / `ChargeStepScale` /
   `ChargeMaxSteps` / `ChargeMaxSeconds` / `PerfectWindowSeconds`。
3. **动画对齐**（这一条是硬要求，别漏）：让动画里那个"闭合帧"（`AnimEvent`，例如 `ATK`）**正好落在
   `ChargeBaseSeconds` 处**：

   ```
   AnimSpeedScale = 源动画里「闭合帧」的时刻 ÷ ChargeBaseSeconds
   ```

   实测（能量掌武技2）：`Skill_A1_ATK2` 的 `ATK` 在源段的 **0.10s**，`ChargeBaseSeconds = 0.5`
   → `AnimSpeedScale = 0.2`（0.10 ÷ 0.2 = 0.5s）。改蓄力时长就按同比例改这个列，事件帧仍然对齐。

   > ⚠️ **`AnimEvent` 在蓄力技能里只是"闭合"的动画标记，不是触发点**。
   > 触发点是**松手**（设计文档）。如果让事件帧去发射，就永远等于"一到点就打出去"，
   > 完美时机窗口（"刚完成蓄力时松手才算"）就没有意义了 —— 这一点与武技1 那种
   > 「子弹在事件帧到 ATK 时发射」的**非蓄力**技能不同（那条路照旧，见 `EnergyWaveAbility.OnAnimEvent`）。
4. **效果层**（子类）：
   - 纯倍率型（弓 / 长枪的伤害与弹速）→ 直接乘 `ChargePowerScale`；
   - "每档 +N 个"（短弩 +2 支/档）→ 读 `ChargeSteps`，配一个**自己子类的表列**
     （如 `ProjectilesPerStep`），`数量 = 基础 + ChargeSteps × 每档`（基类只负责"几档"，不猜单位）；
   - 完美时机 → 表里给**绝对值**（见 §2）。
5. **不要**在子类里再写一套计时 / 再写一个"到时间就发射"的兜底。

### 1.4 四把武器照这个结构怎么填（设计文档里的原话 → 表列）

| 武器 · 武技 | `ChargeBaseSeconds` | `ChargeStepSeconds` | `ChargeStepScale` | 档数上限 | `PerfectWindowSeconds` |
|---|---|---|---|---|---|
| **能量掌 武技2**「憋个大的」 | 0.5 | 0（无后置段） | 1 | — | **0.2**（本轮已落地） |
| **弓 武技1**「箭在弦上」 | 0.5 | 0.1 | 1.3（每档 +30% 伤害与射速） | `ChargeMaxSeconds = 1.0` → 5 档 | 0 |
| **短弩 武技2**「大射天下」 | 0.4 | 0.2 | 1（伤害不变） | `ChargeMaxSteps = 3`（+2 支/档 → 总 9） | 0 |
| **长枪 武技2** | 1.0 | 0.25 | 1.2（每档 +20%） | `ChargeMaxSteps = 4`（或 `ChargeMaxSeconds = 2.0`） | 0 |

> 短弩那条的"+2 支"不在基类里：基类给 `ChargeSteps`，短弩的子类自己加一列"每档几支"
> （它和"伤害倍率"不是同一个维度，硬塞进 `ChargeStepScale` 会算错）。

---

## 2. 完美时机（perfect timing）

设计文档（能量掌武技2）：

> 有完美时机机制，**刚完成蓄力时会有短暂时间触发完美时机**，时间 **0.2s**，
> 完美时机发射一枚更强的能量波，伤害 40，弹速 400。

- **窗口** = `[ChargeBaseSeconds, ChargeBaseSeconds + PerfectWindowSeconds]`
  （`IsPerfectWindow`）。**在这个窗口里松手** → `OnChargeReleased(perfect: true, …)`。
- **过了窗口还在按住** → 松手走**普通那一发**（`ChargeTime` 上不封顶，所以"按住很久"与"刚闭合"
  一定分得开 —— 这是刻意设计：封顶会让"一直按住"永远落在窗口里）。
- **数值全部进表**，而且是**绝对值**（不是倍率）：能量掌武技2 的
  `Damage = 30` / `Speed = 250` / `PerfectDamage = 40` / `PerfectSpeed = 400`（`Params` 列），
  `PerfectKnockback = 2`。代码里没有 30/40/250/400 任何一个字面量。
- **表现层（准星指示器）不在本文件范围**：`ChargeProgress`（往中心缩）、`IsPerfectWindow`（闭合变色）
  这两个状态已经可以直接读，P6 的表现只需接它们。

---

## 3. 技能层数（charges）

设计文档第 13 行：

> 技能层数：每次使用技能消耗一层，间隔 0.5s。

| 表列（`AbilityConfig`） | 语义 | 留空/0 |
|---|---|---|
| `MaxCharges` | 最多几层（**1 = 现在的行为**） | 不覆盖（保留场景里的 1） |
| `RechargeSeconds` | **每层回复间隔**（秒） | 不覆盖 → 退化成 `Cooldown` |

- **消耗点**：`TryActivate()` 成功时（"每次使用技能消耗一层"逐字对应）。
- **回复**：每 `EffectiveRechargeSeconds`（= `RechargeSeconds × CDR`）回**一层**；回满就停。
- **用例**：角色 A 技能1「后撤」的增幅「有备无患：后撤现在可叠 3 层」
  → 该增幅把 `MaxCharges` 改成 3（`RechargeSeconds = 0.5`）。
- **默认（`MaxCharges ≤ 1`）逐位不变**：走的是改动前那条 `CooldownRemaining` 倒计时，
  层数代码一行都没参与（见 §5 的自测证据）。

---

## 4. 冷却缩减（CDR）

**全项目原来的冷却只有一条**：`Ability.Finish()` 里 `CooldownRemaining = Cooldown`（零 CDR 通路）。
现在有三条入口，**数值全在表里，代码里没有任何百分比**：

| # | 通路 | 位置 | 谁用 |
|---|---|---|---|
| ① | **单位级常驻 CDR** | `StatSheet` 的 `CooldownMultiplier`（基础值 1，乘法） | 强化池「洞悉」、异化池「速攻」这类"整段时间都有效"的增幅 |
| ② | **行级 CDR** | `AbilityConfig.CooldownMultiplier`（0 = 不覆盖） | 某一条技能行自己的冷却倍率（"神降术：武技3 CD 变 2 秒"就是这一列的取值） |
| ③ | **一次性事件触发** | `Ability` 的公开接口（下面三个） | 闪身步（技能1 击杀 → 冷却 -80%）、招架反击（技能2 格挡成功 → 刷新冷却） |

合成口径（**只有一处**）：

```
EffectiveCooldown = Cooldown × CooldownMultiplier(行) × CooldownMultiplier(单位 StatSheet)
```

一次性接口（**比例/秒数由调用方从**增幅行**读出来传进来**，代码里不写死）：

```csharp
ability.ReduceCooldownFraction(0.8f);  // 剩余冷却 ×(1-0.8)（闪身步那种）
ability.ReduceCooldown(2f);            // 剩余冷却直接减 2 秒
ability.RefreshCooldown();             // 冷却清零 + 层数补满（招架反击那种）
Ability.FindByAbilityId(unit, "skl_xxx");  // 增幅按**技能行 Id** 找技能（不认节点名/类名）
```

> ⚠️ **`CooldownMultiplier` 是乘法基数**：`Unit.SeedStats()` 必须给它基础值 1
> （少了会读到 0 = 零冷却，`architecture.md` 第 8 条）。`Ability` 侧还兜了一层：
> 单位级读到 ≤0 时按 1 算，绝不静默变成零冷却。
>
> ⚠️ 它**不影响**既有的"改 `Cooldown` 自己"那条老路（Boss 词条 `ScaleAbility(..., "Cooldown", 0.8)`
> 与能力卡的 `GrantedCooldown` 都还在用），两条是相乘关系。

---

## 5. "默认行为不变"的约束与证据

只要**不填新列**，行为必须与 P2 之前**逐位一致**：

| 场景 | 保证方式 |
|---|---|
| `MaxCharges = 1` | `IsReady` / `Finish()` 走的就是改动前那两行（`CooldownRemaining` 倒计时） |
| 没有任何 CDR | `EffectiveCooldown = Cooldown × 1 × 1`，IEEE 乘法 ×1 逐位相同 |
| 非蓄力技能 | 蓄力代码只在 `HoldToCharge == true` 时进入；`OnInputHeld/OnInputReleased` 本来就只有蓄力技能会被输入层调用 |

**自测证据**（`--selftest`，`run_20261015_133715.log`，**退出码 0**，日志原文）：

```
机制·默认值：武技1[skl_a1_w1_1] 层数=1（期望 1） HoldToCharge=False（期望 False） CDR 行×1（期望 1）
  单位×1（期望 1） 实际冷却=0.5（期望 = Cooldown 0.5，×1 乘出来逐位相同）
机制·冷却缩减通路：行级列×0.5 × 单位级×0.5 = ×0.25 → 实际冷却 10×0.25 = 2.5（期望 2.5）
✅ 机制·CDR 落在 Finish() 上：冷却剩余=2.2（期望 ≤ 2.5 且 > 0；没走 CDR 的话这里会是 10）
机制·一次性 CDR 接口：剩余 2.2 —ReduceCooldownFraction(0.8)→ 0.44（期望 0.44） —ReduceCooldown(999)→ 0 —RefreshCooldown()→ 0
机制·技能层数：满层=3（期望 3）→ 用掉一次后 层数=2（期望 2） 回层计时=0.5（期望 0.5 = 2 × CDR 0.25）
机制·技能层数·推 0.2s：层数=2（期望 2，还没到 0.5s） 计时=0.3 ／ 再推 0.35s：层数=3（期望 3） 计时=0
✅ 机制·蓄力未完成松手：蓄力=0.3s 基础完成=False（期望 False）→ 放弃=True 伤害未变=True 仍在施法=False
✅ 机制·完美时机：蓄力=0.55s 窗口内=True → 完美=True 伤害=40 弹速=400（表值 40 ×玩家伤害倍率）
✅ 机制·过完美窗口松手：蓄力=1.2s 窗口内=False → 完美=False 伤害=30 弹速=250
✅ 机制·后置蓄力（两段结构）：蓄 0.85s 档=3 威力×2.197（=1.3³）｜一直按住 档=5（封顶）威力×3.713
```

**旧硬指标逐位不变**（同一份日志；对照基线 `run_20261015_131758.log`）：
节点布置 `10（2/5/3）`、`pressure=3 pmods=3 bank=0`、白板首领 `315/甲 0`、玩家 `生命 270 护甲 40 移速 7 减伤 0`、
端到端 `奖励界面打开 = True  Paused = True`、索引 `4/4/10/1/1/27/7/9/8/6/3/11/8`、`（ConfigIndex.tres 已加载）`、
最小闭环 `✅ 动画事件帧收到=1 次 投射物生成=1 个 生命 60 → 50（扣 10）` + 链路 `命中掉血 ✓`。
**`Config/ConfigIndex.tres` 本轮一个字都没改**（`git diff --stat -- Config/ConfigIndex.tres` 为空）——
新增内容全是 `AbilityConfig` 的**列**（`.tres` 行不写这些列就是默认值），所以没有"三处同改"的风险。

---

## 6. 没做 / 边界（下一轮接着做）

1. **表现层**：准星处的三角形/四边形蓄力指示器（往中心缩、闭合变色）**没做** ——
   状态已经齐了（`ChargeProgress` / `IsPerfectWindow`），留 P6。
2. **后置蓄力目前只有"档数 + 通用倍率"**：短弩"每档 +2 支"、长枪"路径增强"这类**非倍率**维度，
   要等对应的武器子类出现时按 §1.3 第 4 条自己加一列（基类故意不猜单位）。
3. **CDR 的 5 类增幅行本身还没建**（P5 是"增幅接入 + 池重构"）：本轮只做通了**通路**
   （列 / stat / 一次性接口），百分比数值等着写在增幅行里。
4. **层数的 UI**（层数点）没做；`ChargesRemaining` 已经是给 UI 读的。
5. 蓄力期间**没有**"松手后仍要播完收招动画"这一层（当前松手即发射，动画由 `Finish()` 停）。
6. `ChargeTime` 上不封顶（刻意）：UI 读进度请用 `ChargeProgress`（已 clamp）。
7. **首领增援（第 5 条小修）**：`BossPressure.SpawnAdds()` 以前只 `AddChild`（Chaser 用场景值 **60 血**），
   现在与其它 4 处生成路径同一条（`ApplyTo` → `AddChild` → `ScaleEnemy`）→ **45 血**（= 行 60 × 全局 0.75）
   并带上 `ai_chaser` 与 `skl_enemy_chaser_melee`。自测：`✅ 机制·首领增援走单位表：SpawnAdds 生成 Chaser 生命 45 …`。
   ⚠️ 这是**预期修正**（血量下降），旧硬指标一条没漂（见 §5）。

---

## 7. P3 接入记录（角色 A 武器 2/3 + 技能 1/2 + 被动，2026-10-15）

> 这一节是**给 P4（角色 B）与 P5（增幅接入）看的接入契约**：P3 给基类补了哪两列、
> 给"非倍率维度"立了什么规矩、"武技归武器"这条接线怎么走。

### 7.1 基类只多了两样东西（P2 契约仍然成立）

| 新增 | 语义 | 不填时的行为 |
|---|---|---|
| `Ability.AnimEventEnd`（技能行列名同名） | **第二个事件帧**：一段动画里夹出一个区间（后撤的 `Dodge`→`Dodger` 无敌帧；角色 B 的「架」`GuardOn`→`GuardOff` 也是这个形状） | 空 → `OnAnimationEvent` 只认 `AnimEvent` 一个名字，**逐字等于改动前** |
| `CharacterMovement.SetExtraAirJumps(value, cap)` | 额外空中跳跃次数的**接入点**（上限是参数：文档说"最多 3 次"） | 没人调用 → `ExtraAirJumps` 保持场景值 |

**其余机制一律不碰基类**：散射 / 下坠起点 / 击退门槛 / 追踪 / 滞空 / 标记 / 视角放大 / 无效化
都是"子类 + 表列"或"独立组件"（见 [combat.md](combat.md) 的「标记 · 无敌帧 · 滞空」表）。

### 7.2 "非倍率维度"照 §1.3 第 4 条办 —— 实做示例

短弩「大射天下」的"每档 +2 支"与弓「箭在弦上」的"每档 +30 弹速"都**不是倍率**，
所以走 `ArrowVolleyAbility` 自己的列，而不是硬塞 `ChargeStepScale`：

| 需求 | 表列（子类自己的） | 基类负责的部分 |
|---|---|---|
| 每档 +N 支 | `CountPerChargeStep` | `ChargeSteps`（第几档） |
| 每档 +N 弹速 | `SpeedPerChargeStep` | 同上 |
| 每档 ×1.3 伤害 | （复用基类的）`ChargeStepScale` | `ChargePowerScale` |

`数量 = Count + ChargeSteps × CountPerChargeStep`、`弹速 = Speed + ChargeSteps × SpeedPerChargeStep`、
`伤害 = Damage × ChargePowerScale` —— 三行都在 `ArrowVolleyAbility.FireVolley` 里，
**基类一行没改**（它只回答"几档"，不猜单位）。

### 7.3 武技归**武器行**，角色行只放与武器无关的技能

P3 新增了 `WeaponConfig.Skills`（武器 → 它的武技行，资源引用）。理由与后果：

- 一个角色的 3 把武器**都用同样的两个按键**（`weapon_skill_1` / `weapon_skill_2`），
  把 6 条武技行都挂在角色行上会让"换武器"时上一把的武技仍绑在同一个键上（就是
  `anim-pipeline.md` §6.4.6 那类残留）。
- 套用时机：`RunController.ApplyBalance` 在**角色行的技能之后**调
  `UnitConfig.ApplySkills(_player, weaponRow.Skills, weaponRow.Id)`（旧 3 把枪的该列是空数组 → 空操作）。
- 落地：`chr_hunter.tres` 只留 `skl_a1_skill_1` / `skl_a1_skill_2` / `skl_a1_passive`；
  `wpn_energy_palm` / `wpn_crossbow` / `wpn_bow` 各指向自己的两个武技行。
- **加一把新武器 = 加一行武器 + 两个技能行 + 在武器行里列出来**，C# 一行不改（P4 照这个做）。

### 7.4 新增的表行（ConfigIndex.tres 三处同改）

| 分组 | 新增行 |
|---|---|
| 技能（7） | `skl_a2_w1_1` 射射射 · `skl_a2_w1_2` 大射天下 · `skl_a3_w1_1` 箭在弦上 · `skl_a3_w1_2` 刹那之眼 · `skl_a1_skill_1` 后撤 · `skl_a1_skill_2` 跃升 · `skl_a1_passive` 灵动 |
| 武器（2） | `wpn_crossbow`（Slot 5）· `wpn_bow`（Slot 6） |
| 技能动画（6） | `anim_a1_backdash` · `anim_a1_leap` · `anim_a2_scatter` · `anim_a2_charged_volley` · `anim_a3_draw` · `anim_a3_hawkeye` |
| 效果（1） | `eff_mark`（标记，`MaxStacks = 5`） |

`ConfigIndex.tres`：`load_steps 131 → 147`、16 个 `[ext_resource]`、四个数组各加项
（`Weapons`/`Effects`/`Skills`/`SkillAnimations`）。日志核对：`（ConfigIndex.tres 已加载）`，
索引从 `4/4/10/1/1/27/7/9/8/6/3/11/8` → `6/4/10/1/1/34/7/10/8/6/3/11/8`（**差额只来自新增行**）。

**动画对齐**（§1.3 第 3 条那条硬要求，逐条算过）：

| 技能 | 源段 | 事件帧（生成后的**绝对时刻**） | `AnimSpeedScale` | 闭合帧落在 |
|---|---|---|---|---|
| 短弩武技1 | `holding-right-shoot` | `ATK@0.08` | 1 | 事件帧即触发点（非蓄力） |
| 短弩武技2 | `holding-both-shoot` | `ATK@0.12` | 0.3 | 0.12 ÷ 0.3 = **0.4s** = 基础蓄力 ✓ |
| 弓武技1 | `holding-both`（Length 0.5） | `Charge@0`、`ATK@0.3501` | 0.7 | 0.3501 ÷ 0.7 ≈ **0.5s** = 基础蓄力 ✓ |
| 弓武技2 | `holding-left-shoot` | `AimOn@0`、`Mark@0.10`、`ATK@0.18` | 1 | 效果按时长走（蓄力型） |
| 后撤 | `jump`（Length 0.35） | `Dodge@0`、`Dodger@0.35` | 1 | 无敌区间 = 整段 0.35s |
| 跃升 | `jump` | `Action@0.15` | 1 | 效果在 Action 帧（文档原话） |

### 7.5 自测取证（case 32–41，`--selftest`，**退出码 0**）

```
✅ P3·短弩武技1 结果：发数 3（期望 3） 单发伤害 12 弹速 200｜链路：动画 ATK_A2_Scatter → 事件帧 ATK → 投射物 3 个
✅ P3·短弩武技2 大射天下：基础蓄力 0.4s / 每档 0.2s / 上限 3 档 / 每档 +2 支 → 0 档松手 发数 3；3 档松手 发数 9（= 3 + 2×3）
✅ P3·弓武技1 箭在弦上：0.5s / 0.1s一档 / ×1.3 / +30 弹速 → 基础档 伤害 27.6 弹速 220；3 档 伤害 60.637 弹速 310
✅ P3·刹那之眼·瞄准与标记：蓄满后 IsAiming=True → 推 0.6s：标记次数 1 相机 FOV 78 → 32.4
   P3·刹那之眼·收招还原视角：FOV=78（回到施法前） 仍在施法=False
   P3·弓武技2 刹那之眼：基础蓄力期间被 40 点命中 → 无效化=True 掉血 0 生命 220 → 220｜强制后撤 触发 1 次，层数 1 → 1（不消耗）
✅ P3·标记（可叠加、不限单目标）：上限 5 层 → 已有 1 层 + 连贴 3 次 = 4 层 → 再贴 10 次 = 5 层（封顶）
✅ P3·追踪弹：HomingStatusId=「Mark」 锁定 Chaser → 夹角逐帧 90.9° → 85.5° → … → 47°（单调变小）
✅ P3·后撤·无敌帧开（帧 Dodge）：IsInvulnerable=True｜无敌期间挨 40 点：无效化=True 掉血 0｜后撤力 (0,0,12) 朝向不变
✅ P3·后撤·无敌帧关（帧 Dodger）：IsInvulnerable=False 仍在施法=False 朝向仍未改变
   P3·被动 灵动：授予 1 次 / 上限 3 次 → CharacterMovement.ExtraAirJumps=1（场景基线 0 + 授予 1）
✅ P3·跃升结果：起跳速度 16 m/s 授予滞空 3s → 剩余 2.77s 在空中=True 减缓比例 0.9 光环存在=True
✅ P3·滞空·按住跳跃键减缓下落：垂直速度 -20 → -2（= ×(1-0.9)） 光环可见=True
```

**旧硬指标逐位不变**（同一份日志）：节点布置 `10（2/5/3）`、`pressure=3 pmods=3 bank=0`、
白板首领 `315/甲 0`、玩家 `270/40/7 减伤 0`、端到端 `奖励界面打开=True Paused=True`、
`（ConfigIndex.tres 已加载）`、最小闭环链路 ✓、`=== 自测结束 ===`（退出码 0）。

### 7.6 本轮没做（留给 P5/P6）

1. **`<A2限定>` / `<A3限定>` 的 6 条增幅**（专一 / 花心 / 心流 / 变奏 / 心眼 / 龙之箭）**一行都没建** ——
   P5 是"增幅接入 + 池重构"，这 6 条要跟"限定 / 互斥 / 前置"三层判定一起做（专一↔花心 是互斥、
   龙之箭要"蓄力 3 秒 + 2 秒后全穿透"这种新档位语义）。**机制侧已经备好**：标记是可叠加状态、
   箭能挂 `Status`（`AttackInstance.Status`）、蓄力档位/上限都是表列。
2. **表现层**：准星蓄力指示器、后撤的残影、刹那之眼的开镜特效都属 P6（本轮只做了"滞空脚底光环"，
   因为文档把它写进了机制描述里）。
3. **`大射天下` 的 `Aim`**：文档这一行写的是「朝**角色方向**」（对比武技1 的「朝准星方向」），
   照原文取 `Aim = Facing`；若实际手感要改成准星，改那一格即可（不改代码）。
