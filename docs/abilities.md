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

---

## 8. P4 接入记录（角色 B［武器大师］3 把近战武器 + 技能 1/2 + 被动 + 9 条新机制，2026-10-15）

> 这一节是**给 P5（增幅接入）与 P6（表现）看的接入契约**：P4 给基类补了什么、给"数组型表参数"立了什么规矩、
> "武技1 统一驱动"这件事到底落在哪一行代码上。

### 8.1 ⭐「武技1 应当使用同一个技能或状态驱动」是怎么落地的 —— **一个能力 + 一串表参数**

设计文档第 98 行的条注是**架构硬要求**：

```
角色B[武器大师](注:武器武技1基本是动画驱动碰撞和间隔的,这里的cd只是参考值,
               几个武器的武技1应当是使用同一个技能或状态驱动,因为都是由动画控制);
```

**落地 = 一个能力类 `MeleeComboAbility` 服务三把武器**（灵剑 5 段 / 镰刀 3 段 / 长枪 5 段循环）。
理由三条：

1. 文档**明确要求**"同一个技能或状态驱动"；
2. 三把武器的连招**只差"段数 + 每段的数值"**，而这两样都是表列 —— 三个类会让"给某把武器加一段"变成改 C#，
   违反 `character-redesign.md` §1.5「加武器/技能不改代码」；
3. 那份"cd"本文档自己就写着"**只是参考值**"：段与段的间隔由**动画长度**决定（每段一段独立动画），
   CD 是**连招结束后的冷却**。

差异全部落在 `Config/Tables/SkillSeries/<技能行Id>.tres`（见 §8.2）：

| 武器 · 武技1 | `ComboAnimations` | `ComboLoop` | 备注 |
|---|---|---|---|
| 灵剑「破剑式」 | `Skill_B1_Sword1..5`（5 段） | false | 每段都能斩断子弹（`CutSceneNames=Bullet`，排除 `EnergyWave`） |
| 镰刀「憎恶」 | `Skill_B2_Scythe1..3`（3 段） | false | 最后一刀的命中盒 z=0 → **切成圆柱**（大回旋 360°） |
| 长枪「横扫千军」 | `Skill_B3_Spear1..5`（5 段） | **true** | 挥砍→上挑→下劈→突刺→挥砍，打满回到第 1 段（"这么循环"） |

**一次施法的状态机**（代码在 `MeleeComboAbility`，三把武器共用）：

```
TryPress(第一次) → TryActivate → OnActivate：播 ComboAnimations[0]
   └─ 事件帧 ATK（动画驱动）→ Strike()：开命中盒（伤害 ×StepDamageScales[段]）+ CutBullets() + 开输入窗口
        ├─ 窗口内再按 TryPress → Advance()：段 +1 → 播 ComboAnimations[段]（不再查冷却、不再查动画）
        └─ 窗口超时（ComboWindowSeconds）→ Finish()（进冷却）
```

- **输入层不用改**：`PlayerAbilityInput` 调的是 `Ability.TryPress`（基类默认实现 = `TryActivate`），
  连招子类覆写它 —— "哪个技能是连招"仍然是**表**（`ClassHint` + 串文件）说了算。
- **`ATK` 帧没来时的兜底**：`Tick` 里超时 `Cancel()` + 明确日志（不做时间驱动兜底，与全项目同一条纪律）。
- ⚠️ **自测用 `DebugFireAnimEvent()` 模拟"事件帧到了"**（自测在同一帧里跑、推不出真实动画事件帧）——
  它只是 `OnAnimEvent(ComboNextEvent)` 的公开入口，**不参与任何正式流程**。

### 8.2 ⚠️ 数组型表参数：P4 当年走了「整串文本」—— **P8 已推翻并改回正规数组列（见 §11）**

> ⚠️ **这一节原来写的东西有一半是误诊**：P4 把「编辑器加载了**旧的 C# 程序集**」记成了
> 「Godot 的 C# 绑定限制」。P8 在**已重启的**编辑器/运行期下逐条实测（原始输出在 [§11.1](#111--第-0-步探针实测结论走-a-方案正规数组)）：

| P4 当年的说法 | P8 实测结论 |
|---|---|
| `[Export] float[]` / `string[]` 编译不过（`GD0102`） | 🔴 **不成立** —— `float[]` / `string[]` / `Vector3[]` 都编译得过（那两条 `GD0102` 出现在 `PackedFloat32Array` / `PackedStringArray` 上，而且它们先报的是 `CS0246` 未找到类型） |
| `[Export] Godot.Collections.Array<T>` 同 `GD0102` | 🔴 **不成立** —— 编译通过；运行期属性表里带元素类型（`hintString="4/0:"` = `Array[String]`、`"3/0:"` = `Array[float]`、`"9/0:"` = `Array[Vector3]`） |
| `Array<T>` / `T[]` 写下去**读回来是空**（`Count` 读得到、每一项 `null`） | 🔴 **不成立** —— 写盘 → 读回：项数对、**每一项非空**、值逐位对 ✅ |
| 多个 `[Export] Resource`（`Items0..Items7`）读不回来 | 🔴 **不成立** —— 技能行的 `Params` 就是 `ParamEntry[]`（托管对象数组），P1 起一直在用（探针②：`Length=1 每项[StrikeActiveTime=0.1]`） |
| ✅ `[Export] string`（一整串文本）任何情况下都能读 | ✅ **这条是真的**，但**「能读」不等于「该这么写」**（见下） |

**那个「读回来全是 null」的真身 = 编辑器加载的是旧程序集**：新加的 `[Export]` 属性在旧程序集里根本不存在，
于是 `.tres` 里写着的那一列**读得到 `Count`、每一项却是 `null`**。**重启编辑器就好了**
（P5/P6/P7 加的表列都正常）。P4 自己在同一轮里也写过「`Items0..7` 读回来全是 null（因为编辑器加载的是旧程序集）」，
只是把它和「Godot 的限制」混成了一个。

**所以 P4 的两条绕行 —— ① `Entries` 一整串文本 + 运行期 `ParseFloats/ParseTexts/ParseVectors`、
② 按技能行 Id 约定路径 `load` —— 已在 P8 全部删除**，改成：
**正规数组列**（`Array<string>` / `Array<float>` / `Array<Vector3>`）+ 技能行上的**资源引用列**
（`AbilityConfig.ComboSeries`）+ **连招表进 `ConfigIndex`**（三处同改）。

为什么「宁可老老实实加一个列 / 改一次 C#」：约定路径 + 文本解析**坏了不吭声**
（解析失败静默、编辑器里读不出内容、改一个值要数分隔符、路径改名要满地找），
维护它的代价比改一次 C# 高得多 —— 用户本轮的原话见 [§11](#11-p8-接入记录把-p4-的string--约定路径绕过改造成正规表驱动2026-10-16) 开头。

### 8.3 基类与共用类这一轮多出来的东西（全部**只加不删**）

| 新增 | 语义 | 不填时 |
|---|---|---|
| `Ability.PlaySkillAnimationNamed(name, speedScale)` | **按名播一段技能动画**（连招的每段名字不同） | 没人调用 → 0 影响 |
| `Ability.AnyAnimationAvailable(string[])` | "这些动画里有没有至少一个存在" | 同上 |
| `Ability.OnFinishOrCancel()` | **收招钩子**（`Finish`/`Cancel` 都调）—— 连招用它把命中盒还原回场景基线 | 默认空实现 → 既有子类逐位不变 |
| `Ability.TryPress(target)` | **"玩家按了这个键"**的入口；默认实现 = `TryActivate` | 同上 |
| `Ability.BeginEventDrivenCast()` | 让"事件驱动的被动"进入施法态（`LandingBurstAbility` 用） | 同上 |
| `Ability.LoadSeries/BindSeries` | 按路径读 `AbilitySeries` | 同上 |
| `CharacterMovement.FallHeight` / `IsAirborne` | **本次离地后的净下落高度**（下落攻击的高度档） | 只多算两个浮点量，不参与 `MoveAndSlide` 输入 |
| `Projectile.CutBySceneName(...)` / `SourceSceneName` / `_live` | 近战斩断子弹（按**来源场景名**匹配 + 半径过滤 + 排除名单） | 名单为空 → 一次都不查 |
| `ArrowVolleyAbility.ProjectileSizeScale` | 投射物整体缩放（剑气"大小等比攻击盒大小"） | ≤0 → 一行不进 |

**没有改语义**的既有文件：`Weapon.cs`、7 个共用 `Ability` 子类的行为、敌人/首领/召唤物的数据行与技能行。
`MeleeAttackAbility`（既有共用近战能力）**一个字没动** —— 角色 B 走的是新类。

### 8.4 新增的表行与索引

| 分组 | 新增行 |
|---|---|
| 角色（1） | `chr_weapon_master`（Slot 5） |
| 武器（3） | `wpn_spirit_sword`(7) · `wpn_scythe`(8) · `wpn_spear`(9) |
| 技能（10） | `skl_b1_w1_1` 破剑式 · `skl_b1_w1_2` 拔刀式 · `skl_b1_w1_3` 落剑式 · `skl_b2_w1_1` 憎恶 · `skl_b2_w1_2` 无情 · `skl_b3_w1_1` 横扫千军 · `skl_b3_w1_2` 长虹贯日 · `skl_b_fall` 落 · `skl_b_skill_1` 闪 · `skl_b_skill_2` 架 |
| 技能动画（17） | `anim_b1_sword1..5` / `anim_b1_swordqi` / `anim_b2_scythe1..3` / `anim_b3_spear1..5` / `anim_b_fall` / `anim_b_skill1` / `anim_b_skill2` |
| 连招串（3） | `Config/Tables/SkillSeries/skl_b1_w1_1.tres` · `skl_b2_w1_1.tres` · `skl_b3_w1_1.tres` | ← **不进 ConfigIndex**（按技能行 Id 约定路径读，见 §8.2） |
| 场景（2） | `Scenes/Combat/MeleeStrike.tscn`（近战命中体复用件）· `Scenes/Combat/SwordQi.tscn`（剑气投射物） |

`ConfigIndex.tres`：`load_steps 147 → 165`、31 个 `[ext_resource]`、四个数组各加项
（`Weapons` +3 / `Characters` +1 / `Skills` +10 / `SkillAnimations` +17）。
日志核对：`（ConfigIndex.tres 已加载）`，索引从 `6/4/10/1/1/34/7/10/8/6/3/11/8`
→ **`9/5/10/1/1/44/7/10/8/6/3/11/8`**（差额只来自新增行）。

> ⚠️ **2026-10-15 P6a 清旧之后**：索引是 **`6/2/10/1/1/39/7/13/8/6/6/11/6`**
> （旧 3 角色 / 3 武器 / 5 条旧技能行 / 3 个旧池 / 2 张旧卡已删；效果 10 → 13 是 P5b 的三条状态行）。
> 见 [character-redesign.md](character-redesign.md) §六。

**动画对齐**（`AnimSpeedScale` 逐条算过：事件帧绝对时刻 = 目标长度 × 源时刻/源长）：

| 技能 | `AnimSpeedScale` | 事件帧落在 |
|---|---|---|
| 破剑式 / 憎恶 / 横扫千军（每段） | 1.44 | `ATK`@0.363s、`Combo`@0.375s（÷1.44 → **0.25/0.26s**；段间隔由动画长度决定） |
| 拔刀式（蓄力 1s） | 0.35 | `ATK`@0.364 ÷ 0.35 ≈ **1.04s** ≈ 基础蓄力 ✓ |
| 落 / 闪（起手） | 1.0 | `Action`@0（起手即生效） |
| 架 | 1.0 | `GuardOn`@0、`GuardOff`@0.75（架势区间） |

### 8.5 自测取证（case 42–52，`--selftest`，**退出码 0**，`run_20261015_201256.log`）

```
✅ P4·灵剑武技1 破剑式：段数表=5 连续按键推进 4 次（期望 ≥4） 最终段=5 累计连击=4
✅ P4·破剑式斩断子弹：友军的 Bullet 被清=是 敌方的 Bullet 被清=是 能量波还在=是（斩不断）本段斩断计数=2
✅ P4·灵剑武技2 拔刀式：基础蓄力 1s / 没蓄满松手 → 剑气 0 个 / 蓄满 → 1 个；穿透 8、可被格挡=True、存活 0.45s；剑气尺寸 1.4 = 武技1 命中盒最长边 2.8 × 0.5
✅ P4·技能1 闪：方向朝准星；碰撞层 7 → 1（只剩世界层）→ 冲刺后还原 7；可视化路径 17 点
✅ P4·技能2 架：推 0.2s 后在架势中=True；架势内挨 40 点 → 无效化=True 掉血 0；架住计数 +1 并退出架势
✅ P4·被动 落：地面时 TryPress=False；空中 TryPress=True、下落速度 -34 m/s；每 2m +20%、封顶 +100%（0m ×1 / 2 档 ×1.4 / 20 档 ×2）
🔴 P4·武技3 落剑式：上游落地=True、上游伤害 18 未被改动、额外那圈触发次数 +1 —— ⚠️ 但"那一圈打掉了多少血"没测出来（见下）
✅ P4·镰刀：武技1 段数=3、最后一刀命中盒 (7.2, 1.8, 0) = **圆柱**；武技2 抛出/在飞/周期伤害/再次按键收回；武器门绑定=True、飞行期间武技1 被锁=True、收回后解锁=True
🔴 P4·长枪武技1：段数=5、ComboLoop=True、推进 10 次、循环后最大段号=5（打满回第 1 段）—— ⚠️ 但"3 档的路径中线应该 20.74m"实测仍是 12m（见下）
```

**旧硬指标逐位不变**（同一份日志）：节点布置 `10（2/5/3）`、`pressure=3 pmods=3 bank=0`、
白板首领 `315/甲 0`、玩家 `270/40/7 减伤 0`、端到端 `奖励界面打开=True Paused=True`、
`（ConfigIndex.tres 已加载）`、最小闭环链路 ✓、`=== 自测结束 ===`（退出码 0）。

### 8.6 本轮**没做完 / 已知不达标**（交给下一轮）

1. 🔴 **长枪武技2 的"每档 +20%"在自测里没体现**：`LastHorizontalSpeed`/`LastHorizontalDistance`
   两次都是 15 / 12m。**根因**：`ChargeSteps` 在自测的"同步推蓄力"那条路上没被算到
   （`OnInputHeld` 推了 1.75s，但 `ChargeMaxStepsEffective = 4`、`ChargeMaxSeconds = 2.0`）。
   代码本身走的是基类的 `ChargePowerScale`（与角色 A 的弓同一条路，那条已验证过），
   所以**大概率是自测驱动的口径问题**，但**没有实测证据**，如实记为"未达标"。
2. 🔴 **落剑式那一圈"打掉多少血"没有实测证据**：`AttackHitbox` 是 `Area3D`，
   重叠列表要等**一个物理帧**才更新，而自测在**同一帧**里连续驱动能力 → 那一圈永远打不到人。
   判据改成"落地事件触发 + 上游伤害没变"（这两条过了）。**要拿到"打掉血"的证据需要一次真人/MCP 手测**
   （摆一个敌人在落点旁，真按下落攻击）。
3. ⚠️ **`WeaponGate` 的两个技能行 Id 是按节点名推的默认值**（`WeaponGate5 → skl_b2_w1_1/2`）。
   表列（`ThrowAbilityId` / `ComboAbilityId`）还没有行真的填过 —— 目前只走默认值那条路。
   要"纯表驱动"就把两列填上（代码已经优先用表列）。
4. ⚠️ **表现层完全没做**：剑气的拖尾、落剑式的特效圈、冲刺残影、蓄力指示器都属 P6。
   路径可视化只做了"一条折线"（`DashPathIndicator`）。
5. ⚠️ **`<B限定>` / `<B1/B2/B3限定>` 的 12 条增幅一行都没建**（地爆 / 闪身步 / 招架反击 /
   金光术 / 大拔刀术 / 神降术 / 怜悯 / 普渡 / 喜爱 / 气贯长虹 / 金光大道 / 浮光跃金）——
   那是 P5 的范围。**机制侧已经备好**：`ParryCount`（招架反击）、落地事件（地爆）、
   串里的倍率列（怜悯/金光大道）、`MeleeComboAbility.ExternallyBlocked`（气贯长虹的"不再能收回"要改抛掷侧）。
6. ⚠️ **升级池**：三把近战武器行没有 `Pool` 引用（`UpgradePools` 数组还是 3 行）——
   P5 做池重构时一起加。
7. ⚠️ **剑气的 `AnimSpeedScale = 0.35` 是"事件帧 ÷ 基础蓄力"的比值**（0.364 ÷ 1.0），
   与角色 A 那两条（能量掌 0.2 / 弓 0.7）同一个口径。
8. ⚠️ **`AbilitySeries` 的整套写法（§8.2）是"绕过 Godot C# 绑定限制"的方案**，
   不是"漂亮做法"：以后 Godot 的 C# 绑定支持了 `PackedStringArray`，应当改回"技能行上的一个数组列"。

---

## 9. P6c 接入记录（表现层 4 项：开镜 / 后撤残影 / 剑气拖尾 / 落圈，2026-10-16）

> 这一节是**给"以后再加特效"的人看的接入契约**：P6c **一行战斗逻辑都没改**（唯一的例外见 §9.2），
> 只在 4 个技能已有的公开只读状态上各挂了一个表现层组件。逐条细节与坑见 [vfx.md](vfx.md)。

### 9.1 四个特效读的是哪四个"已经存在的只读量"

| 设计文档原文 | 表现层 | 读的只读量（**都是 P2/P3/P4 就有的**） |
|---|---|---|
| 第 87 行「基础蓄力 0.3 秒后**放大视角**」（弓 武技2 刹那之眼） | `Scripts/UI/ScopeOverlay.cs`（`Hud.tscn` → `Root/ScopeOverlay`） | `HawkEyeAbility.IsAiming` / `ZoomApplied` / **`ZoomProgress`（§9.2 新增）** |
| 第 26 行「角色快速向后移动……**有无敌帧**」（技能1 后撤） | `Scripts/Combat/AfterimageTrail.cs`（`Player.tscn` → `Player/Afterimage`） | `BackdashAbility.InvulnerableOwned`（P3 就有） |
| 第 110 / 114 行「发射**一发剑气**……能穿透 / 路径短 / 可被格挡」（灵剑 武技2 拔刀式） | `Scripts/Combat/ProjectileTrail.cs`（**挂在剑气本体** `Scenes/Combat/SwordQi.tscn` 上） | `Projectile.VelocityDirection`（P4 就有） |
| 第 112 行「落地时会造成额外的大范围攻击（……**多一圈攻击判定 + 特效**）」+ 第 99 行被动「落」 | `Scripts/Combat/LandingShockwave.cs`（`Player/AbilityFallAttack/Shockwave` + `Player/AbilityPassive2/ShockwaveBurst`） | `FallAttackAbility.Landed`（公开事件）/ `LandingBurstAbility.TriggerCount`（公开只读计数）/ 命中体 `Attack/Shape` 的 `SphereShape3D.Radius` |

**特别注意第 4 条**：圈的**半径不是写死的**，它读的就是 `AttackHitbox` 真正在用的那个
`SphereShape3D.Radius`（`3.2`，也就是 `RewardCatalog.PulseDamage.Around` 口径的"范围"）。
表/场景把范围调大，圈跟着变大 —— 表现层与判定**同一个几何量**，不可能漂移。

### 9.2 唯一的战斗层改动：`HawkEyeAbility.ZoomProgress`（新增**只读**属性）

```csharp
/// 放大视角的进度（0 = 完全没放大，1 = 已经到 ZoomFov）—— 只读，供 ScopeOverlay 消费。
public float ZoomProgress { get; private set; }
```

为什么加它：表现层**拿不到"施法前那个 FOV"**（`ZoomFov` 只是目标值），所以进度只能由知道基准的人算。
它就是把 `(基准FOV − 当前FOV) / 跨度` 这一次除法暴露出来 ——
**FOV 的写入、平滑速度、还原时机、收招条件一个字都没动**（`StepZoom` 里只多了 3 行赋值）。

### 9.3 实拍与探针（四张图 + 四行 `DebugState()`）

- `screenshots/p6c_scope.png`：`开镜特效：强度=1 镜筒半径=183.6px 渐变 24 档/256px｜只读源 技能行=skl_a3_w1_2
  缩放进度=1 开镜中=True 视角已放大=True｜中心净空 167.6px｜页面鼠标穿透 = True`
- `screenshots/p6c_afterimage.png`：`后撤残影：无敌帧=False 场上残影=4 累计=4｜只读源 技能行=skl_a1_skill_1
  无敌帧拥有=False｜安全性 碰撞体=0 单位=0 分组=（无） 网格=2 抄骨骼=7`
- `screenshots/p6c_swordqi.png`：`剑气拖尾：可见=True 采样点=32 头尾间距=12.917m（面向镜头=True）
  ｜只读源 投射物=SwordQi 在飞=True｜纯表现 网格子节点=1 碰撞体=0 分组=0`
- `screenshots/p6c_landing.png`：`落圈特效：挂=下落攻击（订阅 Landed 事件） 已画=1 圈 最近半径=3.2m
  （命中体 Attack/Shape/SphereShape3D.Radius）｜纯表现 碰撞体=0 分组=0`（"落剑式"那一圈同样一行）

### 9.4 自测与硬指标

`build.ps1 --no-incremental` **0 warning / 0 error**；`--selftest`（`user://selftest.flag`，跑完即删）
`=== 自测结束 ===`、**退出码 0**；硬指标逐位不变 ——
节点布置 `10(2/5/3)` · `pressure=3 pmods=3 bank=0` · 白板首领 `315/甲 0` · 端到端 `奖励界面打开=True` ·
玩家 `220/20/6.5` · 索引 `6/2/10/1/1/39/7/14/8/5/6/11/4` · 最小闭环链路 ✓ ·
`（ConfigIndex.tres 已加载）`。**`ConfigIndex.tres` 一行没动**（表现层不该新增表行）。
备份：`_refactor_backup/ConfigIndex.tres.p6cbefore`。

---

## 10. P7 接入记录（蓄力表现三件事：准星收敛 / 准星时序 / 完成反馈 + 描边占位，2026-10-16）

> 这一节给"以后改蓄力表现"的人看。**战斗层（`Ability` 基类的三段状态机）一个字没动** ——
> 本轮的输出全是"读 §1.1 那张表里的只读量"。逐条细节与踩的坑见 [vfx.md](vfx.md) §6.2。

### 10.1 三条表现分别读什么

| 用户要的 | 落在哪 | 读的只读量 |
|---|---|---|
| ① 「方形蓄力圈就是**能量掌武技2独有的**」 | `Scripts/UI/ChargeIndicator.cs`：数据源从"**按输入动作**找"改成"**按技能行 Id** 找"（`[Export] string AbilityId = "skl_a1_w1_2"`） | 不变（`IsCharging` / `ChargeProgress` / `ChargeBaseComplete`），只是**换了找的是哪一个技能** |
| ② 「缩 → 闭合 → 变色 → **0.3s 后**变回原来的颜色，但**依旧是闭合形状**，还没松手的情况下」 | 同一个 `ChargeIndicator`：`ChargeBaseComplete` 的**上升沿**起一个**表现层自己的**计时器 | `ChargeBaseComplete`（上升沿）+ `ChargeProgress`（决定缺口）；`IsPerfectWindow` 只**读出来写进探针** |
| ③ 「**所有武器**的蓄力表现，需要在**每次完成蓄力时**都有特效 + 音效」 | `Scripts/Combat/ChargeBodyFx.cs`（挂 `Player`）：遍历单位身上的**全部** `Ability`，任何一个 `IsCharging && ChargeBaseComplete` 的上升沿就发一次反馈（闪 + 一圈 + 音效） | `IsCharging` / `ChargeBaseComplete`（上升沿）/ `IsPerfectWindow`（换档用） |
| ④ 「蓄力时的身体变化，比如模型多一圈菲涅尔描边……现在可以加一个**占位**的」 | 同上组件：蓄力期间把 `MaterialOverlay` 上的 `ShaderMaterial`（`Assets/Effects/charge_rim_placeholder.gdshader`，**占位**）的 `rim_strength` 拉起来、松手淡出 | 只读 `IsCharging` |

**接入契约（加一个新的蓄力武器要不要改这里）**：**不用**。③ 是按"单位身上所有 `Ability`"扫的，
不认类名、不认技能行 Id —— 新武器只要在表里填 `HoldToCharge = true` + `ChargeBaseSeconds > 0`，
它蓄满那一刻自动就有闪 + 圈 + 音效。① 才是"某一条技能行专属"（要改就改那一个 `[Export]`）。

### 10.2 ⭐ 两个"秒数"不是一个量（**别把它们看成同一个**）

| 量 | 值 | 属于谁 | 含义 | 谁能改 |
|---|---|---|---|---|
| `Ability.PerfectWindowSeconds` | **0.2s** | 战斗层（**表列**，`skl_a1_w1_2`） | 能打出"完美一击"的时间窗：**松手**落在 `[基础蓄力完成, +0.2s]` 里才算完美 | 表（有平衡含义：改它等于改能量掌的手感） |
| `ChargeIndicator.PerfectColorSeconds` | **0.3s** | 表现层（`[Export]`） | 准星闭合后**变色的持续时间**；过了就变回原来的颜色，**形状照样闭合** | 表现层导出属性（只影响观感） |

用户给的原话里那个 **0.3s 是后者**。P7 里把前者**原样读出来**写进
`ChargeIndicator.DebugState()`（`完美窗口=… （战斗层的 0.2s，与上面的变色时长是**两个量**）`），
实测同一帧能读到 `变色中=True（还剩 0.1s）` 而 `完美窗口=False` —— 这就是"两个量"的直接证据。
**表值一个都没改。**

### 10.3 新增的音效表行（`ConfigIndex.tres` 三处同改）

| 项 | 值 |
|---|---|
| 新文件 | `Config/Tables/Audio/audio_sfx_charge_complete.tres` |
| `Id` | `sfx_charge_complete`（代码里只有 `AudioIds.ChargeComplete` 这个常量，**没有路径字符串**） |
| `Bus` | `Sfx`（0）—— 它是战斗反馈，不是界面音 |
| 触发点 | `ChargeBodyFx`：任意蓄力技能的 `ChargeBaseComplete` 由 false 变 true |
| 素材 | **临时借用**项目自制的 `ui_card_unlock.wav`（"能力卡·解锁"那一声，语义最接近"就绪"）；正式素材出来把 `Stream` 指过去即可，**代码不用改** |
| `ConfigIndex.tres` | `load_steps` 193 → **194**；新增 `[ext_resource] id="279_audio_sfx_charge_complete"`；`Audio` 数组末尾 +1 项（**音效 13 → 14 条**，`[Audio] 配置表：音效 14 … sfx_charge_complete(Sfx)` 可验） |

> ⚠️ 表索引那 13 个数（`6/2/10/1/1/39/7/14/8/5/6/11/4`）**不含音效表**，所以"加一行音效"不会动它 ——
> 这也是为什么这次敢动 `ConfigIndex.tres`（另一条硬指标 `（ConfigIndex.tres 已加载）` 就是它的护栏）。

### 10.4 自测与硬指标

`build.ps1 --no-incremental` **0 warning / 0 error**；`--selftest`
`=== 自测结束 ===`、**退出码 0**；硬指标逐位不变（见 §9.4 那串）。
`ConfigIndex.tres` **动了 3 处**（如上），备份：`_refactor_backup/ConfigIndex.tres.p7before`。

---

## 11. P8 接入记录（把 P4 的"string + 约定路径"绕过改造成正规表驱动，2026-10-16）

> ⭐ **用户本轮澄清的理念（本轮所有取舍都按它做）**：
> 「我倒没要求"**加内容不改代码**"，而是要求**尽量避免后期增加内容时改代码，但该改的时候还是得改**」
>
> 所以：**表驱动是"让加内容变便宜"的手段，不是"禁止改代码"的教条**。
> 如果为了维护"不改代码"而让数据变得难懂/脆弱（约定路径、字符串解析、按名字查属性写错才告警），
> 那就是本末倒置 —— **宁可老老实实加一个表列 / 改一次 C#**。

### 11.1 ⭐ 第 0 步：探针实测（**结论：走 A 方案「正规数组」**）

**① 编译探针**（临时文件 `Scripts/Config/_CompileProbeTemp.cs`，探完即删；`build.ps1 --no-incremental` 原文）：

```text
error GD0102: The type of the exported member 'ActRoguelike.Config._CompileProbeTemp.PackedFloats' is not supported
error GD0102: The type of the exported member 'ActRoguelike.Config._CompileProbeTemp.PackedStrings' is not supported
error CS0246: 未能找到类型或命名空间名"PackedFloat32Array"(是否缺少 using 指令或程序集引用?)
error CS0246: 未能找到类型或命名空间名"PackedStringArray"(是否缺少 using 指令或程序集引用?)
    0 个警告
    4 个错误
```

同一个文件里 `[Export] float[]` / `string[]` / `Vector3[]` / `Godot.Collections.Array<string|float|Vector3|ParamEntry>`
**一条错都没有**（那 4 个错全是 `Packed*Array` 两条引出来的）。

**② 运行期探针**（`Scripts/Testing/SeriesArrayProbe.cs`，由自测的"配置表比对"那一段触发；原文，只删了时间戳）：

```text
P8探针① 属性表（AbilitySeries 的数组列在 Godot 那边注册成了什么）：
    GodotObject.GetPropertyList() 共 35 项；Script.GetScriptPropertyList() 共 27 项（后者就是编辑器 Inspector 的那一份）
    ComboAnimations: Variant.Type=28 hint=TypeString hintString="4/0:"
    StepDamageScales: Variant.Type=28 hint=TypeString hintString="3/0:"
    StepBoxSizes: Variant.Type=28 hint=TypeString hintString="9/0:"
    StepBoxOffsets: Variant.Type=28 hint=TypeString hintString="3/0:"
    StepActiveTimes: Variant.Type=28 hint=TypeString hintString="3/0:"
    CutSceneNames: Variant.Type=28 hint=TypeString hintString="4/0:"
    CutExcludeSceneNames: Variant.Type=28 hint=TypeString hintString="4/0:"
P8探针② 现有数据里的 ParamEntry[]（res://Config/Tables/Skills/skl_b1_w1_1.tres 的 Params）：Length=1 每项[StrikeActiveTime=0.1]
P8探针③ 写盘 ResourceSaver.Save("user://p8_series_probe.tres") → Ok（Ok=0）
P8探针③ 盘上原文（这就是「正规数组」在 .tres 里的样子）：
    [gd_resource type="Resource" script_class="AbilitySeries" format=3]
    [ext_resource type="Script" path="res://Scripts/Config/ParamEntry.cs" id="1_w4e8e"]
    [ext_resource type="Script" path="res://Scripts/Config/AbilitySeries.cs" id="2_gjxo5"]
    [sub_resource type="Resource" id="Resource_uadwq"]
    script = ExtResource("1_w4e8e")
    Key = "P1"
    Value = 1.0
    [sub_resource type="Resource" id="Resource_yqf07"]
    script = ExtResource("1_w4e8e")
    Key = "P2"
    Value = "text"
    [resource]
    script = ExtResource("2_gjxo5")
    ComboAnimations = Array[String](["A1", "A2"])
    ComboLoop = true
    StepDamageScales = Array[float]([1.0, 1.15])
    StepBoxSizes = Array[Vector3]([Vector3(2.2, 1.6, 2.8), Vector3(7.2, 1.8, 0)])
    StepBoxOffsets = Array[float]([1.4])
    StepActiveTimes = Array[float]([0.1])
    CutSceneNames = Array[String](["Bullet"])
    CutExcludeSceneNames = Array[String](["EnergyWave"])
    Id = "p8_probe"
    DisplayName = "P8 探针"
    Params = [SubResource("Resource_uadwq"), SubResource("Resource_yqf07")]
P8探针③ 读回：Variant.Type(ComboAnimations)=Array｜段动画 2 项 [A1,A2]｜伤害倍率 2 项 [1,1.15]｜盒子尺寸 2 项 [(2.2,1.6,2.8),(7.2,1.8,0)]｜前伸 1 项 [1.4]｜判定时长 1 项 [0.1]｜斩断名单 [Bullet] 排除 [EnergyWave]｜窗口 0.45 循环 True 事件帧 ATK
P8探针③ 读回 Params（ParamEntry[]）：Length=2 每项[P1=1.0, P2=text]
✅ P8探针·结论：项数对=True 每一项非空且键对=True 值逐位对=True（P4 的第 ③ 条说「Count 读得到、每一项都是 null」 —— 这条**不成立**）
```

**→ 判定：走 A 方案（正规数组）**。P4 的三条"实测限制"里，
第 ① 条对托管数组**不成立**（只对 `Packed*Array` 成立）、
第 ② 条**成立**（`Packed*Array` 在 C# 侧确实没有类型，`CS0246`）、
第 ③ 条**不成立**（数组能读回来，那是"编辑器旧程序集"的症状）。

> 📌 探针**保留**在 `Scripts/Testing/SeriesArrayProbe.cs`（自测里跑，只读 + 只写 `user://`）——
> 以后 Godot 升级、或再有人怀疑"数组列读不回来"，这条会第一时间给出原始证据。

### 11.2 A 方案落地：删了什么、换成什么

| 文件 | 删掉 | 换成 |
|---|---|---|
| `Scripts/Config/AbilitySeries.cs` | `Entries`（整串文本）、`ItemType`、`ValueSeparatorText`、`PairSeparator` / `ValueSeparator`、`Parse` / `Raw` / `ParseFloats` / `ParseTexts` / `ParseVectors` / `ParseVector3` —— **全部删除** | **10 个正规列**：`ComboAnimations: Array<string>`、`StepDamageScales: Array<float>`、`StepBoxSizes: Array<Vector3>`、`StepBoxOffsets: Array<float>`、`StepActiveTimes: Array<float>`、`CutSceneNames: Array<string>`、`CutExcludeSceneNames: Array<string>` + `ComboNextEvent` / `ComboWindowSeconds` / `ComboLoop`（窗口/循环/事件帧也住这一行）+ `StepCount` |
| `Scripts/Config/AbilityConfig.cs` | `ComboSeriesPath`（`[Export(PropertyHint.File,"*.tres")] string`，一个**路径字符串**）+ `ApplyTo` 里的 `ability.Get("ComboSeriesPath")` / `Set(...)` **字符串反射** | `ComboSeries`（`AbilitySeries?` **资源引用**）+ **强类型赋值**：`if (ComboSeries != null && ability is MeleeComboAbility combo) combo.Series = ComboSeries;` |
| `Scripts/Abilities/Ability.cs` | `LoadSeries(path)` / `BindSeries(inline, path)`（**"按路径 load"这条路**） | 无（连招行由技能行直接给） |
| `Scripts/Abilities/MeleeComboAbility.cs` | `SkillSeriesDir` 常量、`ComboSeriesPath` 字段、**7 个 `[Export] AbilitySeries?` 字段**（实测一个都没被填过）、`ReadSeries` 里的"按技能行 Id 约定路径 load + 七串文本解析" | 一个 `Series`（`AbilitySeries?`）+ `ReadSeries` 直接读 `Series.ComboAnimations/StepDamageScales/...`（不再 load、不再解析） |
| `Scripts/Config/ConfigIndexData.cs` / `ConfigIndex.cs` | —— | `[Export] AbilitySeries[] SkillSeries` 索引数组 + `ConfigRows.Rebuild(Config.SkillSeries)` + `ReportSkillSeries()`（日志 `连招参数：…`）+ 总日志多一项 `连招参数 N` |

**逗号后面的两条纪律没变**：① 数值仍然只存在于表里（代码里没有任何段数/尺寸/倍率常数）；
② 读不到连招行时**写明确日志**并退化成单段（`⚠ 连招：{AbilityId} 的技能行没填 ComboSeries…`），不静默换挡。

> ⚠️ 一个副产品：P4 那 7 个 `[Export] AbilitySeries?` 字段是"半配就能跑"的陷阱
> （只填 `ComboAnimations` 不填 `StepDamageScales` 也能启动，段数对、数值全兜底）——
> 删掉后**只有"一行全给"这一条路**。`Ability.AnyAnimationAvailable` 仍是**无调用点的死代码**，
> 本轮没动它（见 §11.6）。

### 11.3 数据与 `ConfigIndex`（**三处同改**）

| 项 | 值 |
|---|---|
| 连招行（3 行） | `Config/Tables/SkillSeries/ser_b1_combo.tres`（5 段）/ `ser_b2_combo.tres`（3 段）/ `ser_b3_combo.tres`（5 段循环）—— 内容是**真数组列**（`Array[String]([…])` / `Array[float]([…])` / `Array[Vector3]([Vector3(…)])`），不是文本 |
| 技能行（3 行） | `skl_b1_w1_1` / `skl_b2_w1_1` / `skl_b3_w1_1` 各加**一个引用列** `ComboSeries = ExtResource("3_series")` + 顶部一个 `[ext_resource type="Resource" path=…ser_b*_combo.tres]` |
| `ConfigIndex.tres` 三处 | ① `[ext_resource]` 三条（id `280_ser_b1_combo` / `281_ser_b2_combo` / `282_ser_b3_combo` —— **取现有最大值 279 之后**，不动别人的编号）；② `[resource]` 里新增 `SkillSeries = [ExtResource("280…"), …]`；③ `load_steps 194 → 197` |
| 备份 | `_refactor_backup/ConfigIndex.tres.p8before` |
| 日志核对 | `连招参数：3 行（来源：表 SkillSeries）—— 技能指向 [skl_b1_w1_1→ser_b1_combo（5 段）、skl_b2_w1_1→ser_b2_combo（3 段）、skl_b3_w1_1→ser_b3_combo（5 段）]` + `（ConfigIndex.tres 已加载）` |

> ⚠️ 索引**不进**自测那条 13 个数的硬指标行（`6/2/10/1/1/39/7/14/8/5/6/11/4`）——
> 那一行是"别的表的行数"，本轮**一个数都没动**；连招表的行数报在 `[Config] 配置表：… 连招参数 3 …` 那一行。

### 11.4 ⭐ "`ser_b1_combo` 与 `skl_b1_w1_1` 是不是重复" —— 查清了，是"空壳 + 真数据"

- `ser_b1_combo.tres`：**一个数据字段都没填**（只有 `Id` / `DisplayName` / `Note` + 一个 `uid`），
  而且**全仓库零引用**（`grep -r ser_b1_combo` 只命中它自己那行 `Id`）——是 P4 第一次尝试
  （`Items0..Items7` 固定列）留下的空壳。
- `skl_b1_w1_1.tres`：**真数据**（七串全在里面），而且是运行期真正被读的那一份（按技能行 Id 约定路径）。
- **合并方式**：保留 `ser_b1_combo.tres`（**uid 不变**）作为"灵剑武技1 的连招行"、把真数据搬进来；
  `skl_b1_w1_1.tres` 删除。顺带把另外两行也改成 `ser_b2_combo.tres` / `ser_b3_combo.tres` ——
  它们装的是**连招行（SkillSeries 表）**，不是技能行；"文件名 = 技能行 Id"这条约定本来就已删除，
  留着这个名字只会让人以为它是技能行。**4 行 → 3 行，一行一个技能。**

### 11.5 验证：连招逐位不变 + 硬指标逐位不变

**① 构建**：`build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**。

**② 无头自测**（`=== 自测结束 ===`、**退出码 0**）：

```powershell
$godot='F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
& $godot --path 'F:\Godot项目\项目a' --headless --quit-after 40000 res://Scenes/Main.tscn -- --selftest
# → [log] 02:07:05 [SelfTest] === 自测结束 ===    EXIT=0
```

> ⚠️ **必须带场景参数**：主场景是**基地**，不带会进基地 → 找不到 `Stage` → 第 20 阶段起全是空值
> （`--quit-after` 只是兜底帧数，别当超时用）。MCP 那条路（`run_project` + `user://selftest.flag`）
> 同样要显式 `scene_path: res://Scenes/Main.tscn`。

**③ 连招手感冒烟：逐位不变** —— 同一条自测链路上的**三条 `连招表读取`** 与**四条 P4 结论行**
与改动前（P7 代码，`run_20261016_015117.log`）**逐字节相同**（除了 `串=[路径]` 变成 `行=[Id]`、
以及新加的"窗口/循环/事件帧"尾巴）：

```text
# 改动前（串=路径）                                  → 改动后（行=资源引用）
连招表读取：skl_b1_w1_1 串=[…/skl_b1_w1_1.tres] 段动画 5 个 [Skill_B1_Sword1…Sword5]｜伤害倍率 5｜盒子尺寸 5｜前伸 5｜判定时长 5｜斩断名单 [Bullet] 排除 [EnergyWave]
连招表读取：skl_b2_w1_1 串=[…/skl_b2_w1_1.tres] 段动画 3 个 [Skill_B2_Scythe1…Scythe3]｜伤害倍率 3｜盒子尺寸 3｜前伸 3｜判定时长 3｜斩断名单 [] 排除 []
连招表读取：skl_b3_w1_1 串=[…/skl_b3_w1_1.tres] 段动画 5 个 [Skill_B3_Spear1…Spear5]｜伤害倍率 5｜盒子尺寸 5｜前伸 5｜判定时长 5｜斩断名单 [] 排除 []
```

```text
✅ P4·灵剑武技1 破剑式：TryPress(起手)=True 段数表=5 首段动画「Skill_B1_Sword1」 AnimEvent=ATK AnimEventEnd=Combo → 连续按键推进 4 次 最终段=5 累计连击=4 窗口=0.45s｜CD=0.3｜靶子 … 生命 60 → 60（期望下降）
✅ P4·破剑式斩断子弹：名单 [Bullet] 排除名单 [EnergyWave]｜友军的 Bullet 被清=是 敌方的 Bullet 被清=是 能量波还在=是（斩不断）｜本段斩断计数=2 累计=2
✅ P4·镰刀：武技1 憎恶 段数=3 CD 0.5 连续按键推进=2 最后一刀命中盒 (7.2, 1.8, 0)（= **圆柱 = 大回旋 360°**）｜武技2 … 门绑定=True 武技1 被锁=True 收回后武技1 解锁=True
🔴 P4·长枪武技1 横扫千军：段数=5 循环=True CD 0.6 连续按键推进=10 循环后最大段号=5（期望 ≤5）｜武技2 长虹贯日 …（这一行的 🔴 是 P4 §8.6 就记下的"每档 +20% 没体现"，**与本轮无关、逐字未变**）
```

**④ 硬指标逐位不变**（同一次运行）：节点布置 `10（2/5/3）` · `pressure=3 pmods=3 bank=0` ·
白板首领 `315/甲 0` · 端到端 `奖励界面打开=True` · 最小闭环链路 `✓`（`最小闭环·链路：按键 weapon_skill_1 → 技能 skl_a1_w1_1 → 动画 1 个事件帧 → 投射物 1 → 命中掉血 ✓`）·
玩家 `220/20/6.5` · 索引 `6/2/10/1/1/39/7/14/8/5/6/11/4` · 增幅 **62（9/23/30）** · 音效 **14** · `（ConfigIndex.tres 已加载）`。

**⑤ 控制实验（证明③④不是"我看漏了"）**：把两份日志归一化（去时间戳、去自动节点名
`@CharacterBody3D@N`，只留 `[SelfTest]/[Config]/[Combat]/[Audio]` 行，剔除随机抽签/刷怪/表现采样那几类）
后 `Compare-Object`：

| 对比 | 差异行数 |
|---|---|
| **改动前 vs 改动前**（两跑基线，同类随机性） | **66** |
| 改动前 vs 改动后 | **89** |

多出来的 **23 行全部是**：P8 探针输出（26 行，属新增）与**我故意改的两行日志**
（`配置表：… 连招参数 3 …` / 新增 `连招参数：3 行…`）—— 也就是说
**所有硬指标行、四条 P4 结论行、`连招表读取` 的数据部分，在两跑之间是逐字节相同的**。

### 11.6 没做 / 不确定（照实说）

1. ⚠️ **`Ability.AnyAnimationAvailable` 仍是死代码**（P4 加、无调用点）。这轮**没碰它** ——
   它不属于"string + 约定路径"那两条绕行；要删是另一次清理。
2. ⚠️ **`ComboNextEvent` 与技能行的 `AnimEvent` 值重复**（三行都是 `ATK`）：
   `MeleeComboAbility.OnAnimEvent` 判的是 `ComboNextEvent`，而基类的动画事件帧名来自技能行 `AnimEvent`。
   合并成一个（直接用 `AnimEvent`）要动 `OnAnimEvent` 的判据，**收益小、回归面大**，本轮不动。
3. ⚠️ 探针会往 `user://p8_series_probe.tres` 写一个临时文件（自测时覆盖一次），不进仓库、不影响玩法。
4. ⚠️ **编辑器里那份 `ConfigIndex.tres` 的内存副本**：本轮是手改磁盘文件 + 无头自测验证的。
   如果编辑器之后自己保存了它，请以仓库里的版本为准并重跑一次自测（日志里看 `连招参数 3 行` + `已加载`）。
5. ⚠️ 我这轮开始时编辑器里**有一个 01:46 起的旧游戏实例**（跑的是改动前的 dll），
   它挡住了 MCP 的 `run_project`（`Project is already running`）—— 我把它关掉了，
   没有用全局的 `stop_project`。编辑器本身全程开着。



