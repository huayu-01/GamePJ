# 原始设计 J11：符文系统（哈迪斯信物 = 效果 + 前置解锁）

2026-10-17。用户原话（**这是整个系统的定义**）：

> 「就是哈迪斯那样的，原文想表达的意思就是携带了这个东西之后，会产生些效果，比如雷之印，
> 这个效果就是最大生命值受限，仅此而已，然后新增的增幅里，有一些它是有前置，要求雷之印，
> 所以携带了这个雷之印等于满足它们的前置要求」

以及三条配套裁定：「雷之印 30 = **真的**把最大生命值限制为 30（构筑很强，限制必然够大）」「战神印 300% = 同样真的」
「所有 `{n}` 填**试跑最小值**」。

本批按用户划定的范围拆成 5 个独立提交：**① 表+类 / ② ConfigIndex / ③ 施效 / ④ 前置对接 / ⑤ 携带入口**。
`Weapon.cs`、**7 个**共用 `Ability` 子类、敌人·首领·召唤物的数据行与技能行**一个字都没动**；
`project.godot` **没有动**（没加输入绑定）。
Debug 与 ExportRelease 均 **0 警告 / 0 错误**（见下文 §9 的实测口径）；专项 `_RuneProbe` **83/0**；
回归 `_ChargeReleaseProbe` **80/0**、`_CharacterDesignProbe` **115/0**、`_DragonArrowProbe` **42/0**、`_BowSweepProbe` **23/0**；
Main 自测退出码 0、8 条硬指标里**只有索引串**按预期变成 14 个数（其余 7 条逐字一致）；
四存档 SHA256 跑前＝跑后；`selftest.flag` 不存在。
[完整输出](character-design-j11-evidence.log) · [系统契约](rune-system.md)。

---

## 1. 11 枚符文各自填了什么（**逐条注明"文档为 `{n}`"**）

> 表 = `Config/Tables/Runes/rune_*.tres`（一行一个文件），11 行 = `后续构筑.txt` 里那 11 条。
> 用户说"12 个装备位"，但原文只有 11 条 —— **按原文 11 条建**，第 12 个位留空，等用户给内容。

| # | 符文 | `Id` | 填进去的效果 | 文档给没给数 | `UnlocksRequires` |
|---|---|---|---|---|---|
| 1 | **雷之印** | `rune_lei_seal` | 生命上限**硬夹 30**（`MaxHealthCap`） | **文档给 30**（不是"减 30%"） | `["雷之印"]` |
| 2 | **战神印** | `rune_war_seal` | 暴击率 **+10%** ＋ 受到伤害 **×4**（`IncomingDamageMultiplier`） | **文档给 10% / 300%**（300% = 承受 4 倍） | `["战神刻印"]` |
| 3 | **风之印** | `rune_wind_seal` | 技能冷却 **×1.5**（`CooldownMultiplier`） | **文档给 50%** | `["风之印"]` |
| 4 | **死铠** | `rune_death_armor` | 初始护甲 **+20** ＋ **每 10 杀 +1 甲** ＋ 治疗 **×0**（禁疗） | ⚠️ **两个 `{n}` 文档都没给** → 预设 20 / 10 | `[]` |
| 5 | **行军** | `rune_march` | 移速 **×2** | **文档给 100%** | `[]` |
| 6 | **生死** | `rune_life_death` | **1 次**死亡避免 ＋ 触发时恢复 **30%** 生命 | 恢复 30% 文档给了；⚠️ **次数 `{n}` 没给** → 预设 1 | `[]` |
| 7 | **大力符** | `rune_strength_talisman` | 伤害 **×1.2** | **文档给 20%** | `[]` |
| 8 | **飞翼** | `rune_flying_wing` | 起跳 **×1.3** ＋ 正常下落上限 **×0.8** | ⚠️ **文档一个数都没给** → 预设 ×1.3 / ×0.8 | `[]` |
| 9 | **狙神** | `rune_sniper` | ≤3m **×0.5** → ≥15m **×1.5**（线性） | 近端 50% 文档给了；⚠️ **远端与两个距离阈值没给** → 预设 1.5 / 3m / 15m | `[]` |
| 10 | **巨人** | `rune_giant` | 每次攻击额外造成 **最大生命值 10%** | **文档给 10%** | `[]` |
| 11 | **小精灵** | `rune_pixie` | 绕行实体，**每 5 秒**打 8m 内**一个**敌人（伤害 20） | 5 秒文档给了；⚠️ **伤害与半径没给** → 预设 20 / 8m | `[]` |

**"激进值"的落地**：用户说「雷之印的构筑本身就很强，带来的限制必然也要够大才能平衡」，
所以三个负面符文按**文档原值**上满（30 / ×4 / ×1.5），**没有打折**；表行 `Note` 里逐条写了预设理由。
每条 `Note` 都带 `文档为 {n}` 的标注（上表"文档给没给数"那一列就是它的摘要）。

---

## 2. `ConfigIndex` 动了哪几处 + 新索引串

- **备份**：`F:\Godot项目\_refactor_backup\ConfigIndex.tres.p12before`（33841 字节，改前 SHA256 与 p8before 同源）。
- **三处同改**（`docs/DEEPSEEK_HANDOFF.md` 的纪律）：
  1. 顶部 **11 条 `[ext_resource]`**，id `283_rune_lei_seal` … `293_rune_pixie`（**取现有最大值 282 之后**，不动别人的编号）；
  2. `load_steps 197 → 208`（每加一条 ext_resource +1，与 P8「194 → 197」同一条算法）；
  3. 新增 `Runes = [...]` 数组（**排在 `Upgrades` 之后、`UpgradePools` 之前**，与 C# 的属性声明顺序一致）。
- **没有动** `Upgrades` / `UpgradePools` / `Audio` / 其它任何数组，也没有增删一行既有表行。
- 备份文件在仓库外（`F:\Godot项目\_refactor_backup`），**不入库**。

### ⚠️ 索引串：13 个数 → **14 个数**

`SelfTest.ReportTables` 的 `配置表比对·索引` 那一行是"每张表读了几行"的现场读数（**只打印、不比对**）。
本批多一张表，所以它从 13 个数变成 14 个数：

| | 索引串 |
|---|---|
| 改前（J10 基线） | `6/2/10/1/1/39/7/14/8/5/6/11/4` |
| **改后（J11）** | **`6/2/10/1/1/39/7/14/8/5/6/11/4/11`** |

**前 13 个数字一位都没动**，末尾多的那个 `11` 就是符文表（`rune_*` 11 枚）。
生成逻辑：`SelfTest.cs` 里 `ReportTables` 逐个 `ConfigRows.Count<T>()` 拼串（武器/角色/敌人/首领/召唤物/技能/
Pressure/效果/节点/局外/升级池/AI 档案/能力卡），本批在末尾加了 `/符文 {Count<RuneConfig>()}`。

⚠️ **刻意没把符文并进 `[Config] 配置表：…` 那条总览行**：那一行是 8 硬指标之一（runner 里对
`docs/e-batch-evidence.log` 逐字比对，marker = `配置表：武器 6`）。符文单独一行
（`[Config] 符文表（开局携带一件的信物，**不进奖励池**）：11 枚（来源：表 Runes）—— […]`），
与「价值标签」「升级池」「AI 档案」「能力卡」同一条纪律；这样 8 硬指标里**只有索引那一条**按预期变化。

---

## 3. ⭐ 符文 → 增幅前置（**本系统的核心价值**）

### 3.1 怎么做的（三处、零新判定层）

1. **判定入口收成一个**：`RunBuild.SatisfiesPrerequisite(id)` = `StacksOf(id) > 0 || _runeUnlocks.Contains(id)`；
   `RewardPool.HasPrerequisites` 改调它。**不携带符文时 `_runeUnlocks` 为空 → 与改动前逐字等价**。
2. **解锁名单怎么来**：`RunController.ApplyCarriedRune` 把 **符文行 Id**（`rune_lei_seal`）＋
   行上 `UnlocksRequires` 里的**派系名**（`雷之印`）一起写进 `RunBuild.SetRuneUnlocks` ——
   于是表里写哪种都行（文档写法 / 更稳的 Id 写法）。
3. **不串台**：符文**不进** `_stacks`，所以它不会出现在 HUD 的构筑列表、也不会满足**互斥**（`Excludes`）——
   它只影响**前置**这一层（用户原话只提了前置）。

### 3.2 ⭐ 实测读数（`_RuneProbe` 第 ⑥ 段，真实 `RewardPool.Initialize` + `Roll`）

⚠️ **先说清楚**：`后续构筑.txt` 里那些"雷系特化 / 战神特化"增幅（雷之心 / 引雷符 / 雷凯 / 血战 / 血铠 /
死斗 / 风之心 / 风凯 / 风剑 / 风痕 / 风场 / 雷法 / 暴雷 / 暴力 / 战斗之心 / 狂风 / 战斗之矛 / 战斗残影 /
嗜血 / 落雷术 / 暴风）**本批不建**（属于后续批次），所以**真实表里现在 0 条增幅的 `Requires` 命中这三个派系**
—— 启动日志里每枚符文都打 `→ 解锁前置的增幅 [（暂无增幅以此为前置）]`，这是**如实留痕**，不是"已经能抽到"。

为了证明**机制本身**通，探针临时往运行期目录插了两条合成行（`Requires = [雷之印]` / `[战神刻印]`），
走**真实**的池子初始化与抽取（`count=999` = 要全部候选），量完立刻恢复目录：

| 携带 | 可用候选 | `Requires=[雷之印]` 那条 | `Requires=[战神刻印]` 那条 |
|---|---|---|---|
| **不带符文** | **27 条** | ❌ 不进池 | ❌ 不进池 |
| **雷之印** | **28 条**（+1） | ✅ **进池** | ❌ 仍不进（派系不串） |
| **战神印** | **28 条**（+1） | ❌ 仍不进 | ✅ **进池** |

探针结束后运行期目录恢复成 **62 条**（没有残留探针行）；不带符文时的候选池与第 ② 段基线**同值**。
→ **"带雷之印后哪些增幅变可用了"的实测答案是：`+probe_lei_gated` 这 1 条**
（真实表里那 21 条还没建，见上）。

---

## 4. ⭐ 雷之印的真实效果：**220 → 30**

```
[RuneProbe] ⭐ ③ **雷之印**：玩家最大生命值 220 → 30；当前生命 30
[RuneProbe] PASS ③ 雷之印：最大生命值从 220 变成 30（期望 30 —— 用户确认「真的限制为 30」）
[RuneProbe] PASS ③ 雷之印：当前生命被同时夹到 30（不会留在 220）
[RuneProbe] PASS ③ StatSheet 的 MaxHealthCap = 30（期望 30）
```

- 实现：StatSheet 新键 **`MaxHealthCap`**（基础值 0 = 不夹），`Unit.RefreshStats` 里夹一刀；
  符文行走**既有的** `StatSet` 处理器 + `Modifiers` 列（**零新处理器**）。
- 220 = 角色行 200 + 元进度 20（与加符文之前**逐字一致**）。
- 摘下符文后回到 220（第 ③ 段末）。

## 5. ⭐ 战神印：受到伤害**真的 4 倍**

```
[RuneProbe] ⭐ ④ **战神印**：暴击率加成 0.27 → 0.37（+0.1）；受击倍率 ×4；
            同样 10 点伤害的最终伤害 7 → 28（比值 4）
[RuneProbe] PASS ④ 战神印：受击倍率 ×4（期望 ×4 = 「受到伤害增加 300%」）
[RuneProbe] PASS ④ 战神印：同一次 10 点伤害 → 最终 28，正好是不带符文时 7 的 4 倍（期望 4）
```

- 读数口径：同一名玩家、同样满血满甲，先量"不带符文"的一次 10 点命中（`FinalDamage = 7` =
  10 × (1 − 护甲减伤 30%)），再量"带战神印"的**同一次** 10 点命中（`FinalDamage = 28` = 7 × 4）。
  **比值正好 4.0000**，不是"约等于"。
- 实现：StatSheet 新键 **`IncomingDamageMultiplier`**（基础值 1），`Unit.ReceiveHit` 里乘在既有的
  格挡/减伤**之后**；暴击率走**既有**键 `CritChanceAdd`。符文行仍是 `StatSet` + `Modifiers`，**零新处理器**。

## 6. 其余符文的效果与读数

| 符文 | 读数（`_RuneProbe`） | 状态 |
|---|---|---|
| 风之印 | 技能 1 实际冷却 `0.3s → 0.45s`（×1.5） | ✅ 实现 |
| 行军 | 移速倍率 ×2 | ✅ 实现 |
| 大力符 | 伤害倍率 ×1.2 | ✅ 实现 |
| 飞翼 | 起跳 ×1.3（真实起跳速度 **16.9 m/s** = 13 × 1.3）／下落上限 ×0.8 | ⚠️ **4 条描述里实现了 3 条**，"下落攻击的下落速度更快"**没实现**（要动 `FallAttackAbility`，本批禁止） |
| 死铠 | 护甲 20→**40**；`Heal(50)` 真实恢复 **0**；9 杀不加甲、**第 10 杀 40→41** | ✅ 实现（初始甲 + 禁疗 + 击杀加甲三件全做） |
| 生死 | 100 血的靶子吃 **999 点致命伤** → 未死、生命回到 **30**；次数用完后再吃致命伤就死 | ✅ 实现 |
| 狙神 | 1.5m **×0.5** / 9m **×1.0** / 20m **×1.5** | ✅ 实现（复用 J8 的 `IOutgoingDamageScale`） |
| 巨人 | `RollHit` 基础 10 点 → **32**（= 10 + 220×10%） | ✅ 实现（新 `IOutgoingDamageBonus` 扁平加伤口） |
| 小精灵 | 7.5 秒内出手 **1 次**、目标 `PixiePrey` 掉 **20** 点血；实体**不在**任何单位分组、子树 **0 个物理体** | ✅ 实现（占位表现 = 一颗自发光小球） |

---

## 7. 哪些是"只填了表但没实现"（**明确标 ⚠️**）

| 符文 | 没实现的那一半 | 为什么 | 将来怎么补 |
|---|---|---|---|
| **飞翼** | 「**下落攻击的下落速度更快**」 | 要改 `FallAttackAbility.DiveSpeed`，而它是本批硬约束里**不许碰**的共用 `Ability` 子类 | 给它加一个读 `DiveSpeedMultiplier`（`Stats.Get`）的入口，然后本行加一条 Modifier —— **不用改符文系统的任何代码** |
| **小精灵** | 只做了机制，**没有模型/动画** | 没有可用素材；本批用自发光小球占位 | 把 `PixieCompanion` 的占位 `MeshInstance3D` 换成真正的模型/动画（效果逻辑不用动） |
| **雷之印 / 战神印 / 风之印** 的"**特化增幅**"那一半 | 那些增幅（21 条）**本批不建** | 用户把范围划成"符文系统"（表 + 效果 + 前置 + 入口），21 条增幅是后续批次 | 照 `docs/rune-system.md` §4 在增幅行的 `Requires` 里写派系名即可，**不用改 C#** |

⚠️ 除上表这 3 处，**其余全部真实实现并有实测读数**，没有"只填表"的假交付。

---

## 8. 默认（不带符文）行为逐位不变的证据

1. **`_RuneProbe` 第 ②/⑧ 段**（`_RuneProbe` 用真玩家 + 真表行）：
   - 生命 **220** / 护甲 **20** / 移速 **6.5** / 七条乘区（`DamageMultiplier`、`IncomingDamageMultiplier`、
     `CooldownMultiplier`、`MoveSpeedMultiplier`、`JumpVelocityMultiplier`、`MaxFallSpeedMultiplier`、
     `HealMultiplier`）**全是 1** / `MaxHealthCap` **0** / 死亡避免规则 **0** 条 / 符文规则节点 **0** 个；
   - 一次 10 点命中 → `FinalDamage` **7**（= 护甲减伤后的既有值）；摘下所有符文后再量一次仍是 **7**（逐位相等）；
   - 候选池 **35 → 35** 条（`RewardPool.Initialize` 的直接读数）；增幅项目录 **62 → 62** 条。
2. **四条回归探针全部保持原基线**：`_ChargeReleaseProbe` **80/0**、`_CharacterDesignProbe` **115/0**、
   `_DragonArrowProbe` **42/0**、`_BowSweepProbe` **23/0** —— 它们一个字节的期望值都没改。
3. **Main 8 硬指标**：除索引串（多一张表的必然结果，§2 已说明）外，其余 **7 条逐字一致**。
4. **新增代码全部"只加不改"**（见 §10 的清单），每个读取点都有"没配置时等于改动前"的论证
   （`x × 1f` / `x + 0f` / `if (count > 0)`）。

---

## 9. 验收

- `build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**。
- `dotnet build ActRoguelike.csproj --no-incremental -v minimal -c ExportRelease` → 见 §9.1（⚠️ 如实记录）。
- 专项 `_RuneProbe`（新）→ 退出码 **0**、**83/0**。
- 回归 `_ChargeReleaseProbe` **80/0** · `_CharacterDesignProbe` **115/0** · `_DragonArrowProbe` **42/0** ·
  `_BowSweepProbe` **23/0**，全部退出码 0。
- Main `--headless --quit-after 40000 res://Scenes/Main.tscn -- --selftest` → 退出码 **0**、
  到 `=== 自测结束 ===`、8 硬指标如 §8 所述。
- 四存档 SHA256 与交接点**逐字相同**（跑前＝跑后）；`selftest.flag` 跑前不存在、跑后不存在；
  **`user://rune.cfg` 跑前不存在、跑后也不存在**（自测走内存模式，不产生新存档文件）。
- 编辑器未关；没有 `stop_project`，也没有杀别人的进程。

### 9.1 ⚠️ ExportRelease 的一次实测异常（如实登记）

（见 §11 的"不确定处"第 1 条 —— 首次在 `pwsh -File` 里跑时出过 1 个 error，重跑口径与结论写在证据日志里。）

---

## 10. 新加了什么（"只加不删"清单）

| 文件 | 加了什么 | 默认（"没配置"）时的行为 |
|---|---|---|
| `Scripts/Config/RuneConfig.cs`（**新**） | `RuneConfig : RewardConfig` + `UnlocksRequires`（`Array<string>`）+ `IconPath` | 表里没有符文行 → 什么都不发生 |
| `Scripts/Config/ConfigIndexData.cs` | `Runes` 索引数组 + 一段中文说明 | 空数组 = 没有符文可携带 |
| `Scripts/Config/ConfigIndex.cs` | `ConfigRows.Rebuild(Config.Runes)` + `ReportRunes()` 一行日志 | 0 枚时打"与加符文之前逐位一致" |
| `Scripts/Combat/Unit.cs` | 4 个 StatSheet 键（`MaxHealthCap`/`IncomingDamageMultiplier`/`MaxFallSpeedMultiplier`/`HealMultiplier`）+ 三处读取 + `IDeathAvoid`/`IOutgoingDamageBonus` 登记表 + `RestoreHealthForDeathAvoid` | 基础值 0/1/1/1 → 读取端等于改动前；两个登记表为空时对应 `if` 不进 |
| `Scripts/Combat/CharacterMovement.cs` | 下落夹速乘 `MaxFallSpeedMultiplier` | ×1 → `-MaxFallSpeed` 逐位相等 |
| `Scripts/Combat/AttackInstance.cs` | `HitInstance.Damage` 末尾 `+ flatBonus` | 没登记 → `+0f`，`x + 0f == x` |
| `Scripts/Combat/IOutgoingDamageBonus.cs`（**新**） | 扁平加伤口 | 无实现方 → 恒 0 |
| `Scripts/Combat/IDeathAvoid.cs`（**新**） | 死亡避免口 | 无实现方 → `if` 整段不进 |
| `Scripts/Rewards/RuneRules.cs`（**新**） | 5 个规则类（击杀加甲 / 死亡避免 / 按距离加伤 / 按最大生命值加伤 / 小精灵） | 只有符文行带对应参数时才 `new` 出来 |
| `Scripts/Rewards/RewardEffectRegistry.cs` | 新处理器 `RuneEffect`（一个服务所有"需要行为"的符文行） | 没有符文行用它 → 零影响 |
| `Scripts/Rewards/RunBuild.cs` | `_runeUnlocks` + `SatisfiesPrerequisite` / `SetRuneUnlocks` / `DescribeRuneUnlocks` | 集合空 → `StacksOf(id) > 0` 逐位等价 |
| `Scripts/Rewards/RewardPool.cs` | `HasPrerequisites` 改调 `SatisfiesPrerequisite` | 同上 |
| `Scripts/Rewards/RunController.cs` | `CarriedRuneId`/`CarriedRune`/`SetCarriedRune`/`ApplyCarriedRune`/`FreeRuneNodes` + 开局读 `RuneCarry` | 不携带 → 只打一行"不携带"日志 |
| `Scripts/Run/RuneCarry.cs`（**新**） | `user://rune.cfg` 存档 | 文件不存在 → 空；自测内存模式不写盘 |
| `Scripts/Testing/RuneProbe.cs` + `Scenes/Debug/_RuneProbe.tscn`（**新**） | 83 条读数的专项验收 | 只被自测/人手动调用 |

⚠️ **没有碰**：`Weapon.cs`、7 个共用 `Ability` 子类、敌人/首领/召唤物的数据行与技能行、
`project.godot`（**没有**新增输入绑定）、四个既有存档文件格式。

---

## 11. 不确定处 / 没做的

1. ⚠️ **ExportRelease 首次异常**：见 §9.1 与证据日志（重跑结果如实记录）。
2. ⚠️ **"12 个装备位 vs 原文 11 条"**：按原文建 11 枚。第 12 条内容缺，等用户给。
3. ⚠️ **预设值等确认**：死铠 **20 / 每 10 杀**、生死 **1 次**、狙神 **远端 ×1.5 / 3m / 15m**、
   小精灵 **伤害 20 / 半径 8m**、飞翼 **×1.3 / ×0.8** —— 全部是"能跑通的最小可用值"，行 `Note` 里逐条写了理由。
4. ⚠️ **没做（明确不在本批范围）**：
   - **符文选择 UI**（基地交互物 / 面板 / 图标）—— `IconPath` 列已留占位，界面下一批做；
   - **`快乐小子`（异化）的"奔跑可穿过任何单位 + 角色虚化"** —— 新机制，单独做；
   - **21 条"雷系/战神系/风系特化"增幅** —— 后续批次；
   - **飞翼的"下落攻击速度"** 与 **小精灵的模型/动画**（§7）。
5. **符文与"装备位数量"的关系**：目前只有"携带一件"这一条通路（`CarriedRuneId` 是单值）。
   将来要"多装备位"时，`RuneCarry` 需要从单值改成数组 + `RunBuild.SetRuneUnlocks` 收多个来源的并集 ——
   **判定层不用改**（它已经是"一个字符串集合"的语义）。
6. **本批没有截图**（证据是文本读数 + 存档哈希）：符文没有 UI、也没有模型，
   真机手感（雷之印 30 血能不能玩）留给人手 MCP 复核。
