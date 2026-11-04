# 第五轮数值落地：20 分钟、4 首领、强化扩级、猎手增强

2026-10-31。按用户确认的 v5 方案落地数值/配置与相关代码；已提交并推送。071 单区域专用面板另见 [071-training-ground.md](071-training-ground.md)。

> **2026-11-04 修正**：新增的 11 行扩级强化把“20%”写成了 `20.0`，但 `CritChanceAdd` / `CritMultiplierAdd` / `DamageReductionAdd` 在运行期按小数增量结算（0.2 = 20%）。已统一改为小数：
> 暴击 `0.2/0.25/0.3/0.35/0.4`，爆伤 `1.0/1.25/1.5`，坚硬 `0.2/0.25/0.3`。原 1/2 级保持 `0.1/0.15...` 不变。

## 1. 实际改动

### 生存节奏与首领

- `Config/Modes/twilight_survival.tres`
  - `DurationSeconds 720→1200`（20 分钟）。
  - 六阶段预算/s：2.0→2.5、2.6→3.25、3.1→3.875、3.8→4.75、4.7→5.875、5.5→6.875（+25%）。
  - 阶段比例、人数/压力上限、生命/伤害倍率保持。
- `Scripts/Config/SurvivalDefinition.cs`
  - 新增 `BossSpawnFractions`（默认 .25/.5/.75/1）、`BossHealthScales`（.4/.6/.8/1）、`BossDropsEvolution`；含等长、递增、末项=1 校验。
  - `DurationSeconds` 默认 720→1200。
- `Scripts/Run/Survival/SurvivalDirector.cs` / `.Spawning.cs`
  - 生存阶段按时间表生成中间首领；终局首领仍由 `FinalBoss` 生成。
  - 4 只首领的 HP 倍率按 0.4/0.6/0.8/1.0；三信标削弱只作用于终局首领。
  - 首领死亡时排入一次 `RewardKind.Evolution`；终局首领不再清空奖励队列，击败后先领取进化再撤离。
  - 终局首领死亡仍保持 `AwaitingExit`、不提前结算。
- `Config/Tables/Bosses/boss_final.tres`、`Scenes/Enemies/Boss.tscn`
  - `MaxHealth 900→1800`；全局 2.5、生存 0.8 后终局有效 3600。

### 强化扩级

- 新增 11 行：
  - 暴击 4～8 级：`upg_crit_plus3..7`，5% 递增，满级 40%（8 级）。
  - 爆伤 4～6 级：`upg_critdmg_plus3..5`，25% 递增，满级 150%（6 级）。
  - 坚硬 4～6 级：`upg_hard_plus3..5`，5% 递增，满级 30%（6 级）。
- `Config/ConfigIndex.tres` 登记 11 个 ext_resource 与 Upgrades 数组。
- `Config/Localization/zh.tres`、`en.tres` 补名称/描述键。
- `Config/Validation/content_inventory.tres`：Upgrades 120→131，Enhancement 30→41。
- 新增 11 行百分比值统一用**小数**，与原有 1/2 级一致：
  - 暴击：`0.20 / 0.25 / 0.30 / 0.35 / 0.40`
  - 爆伤：`1.00 / 1.25 / 1.50`
  - 坚硬：`0.20 / 0.25 / 0.30`
- 全档位顺序探针通过：暴击 `0.05…0.40`（8 档）、爆伤 `0.25…1.50`（6 档）、坚硬 `0.05…0.30`（6 档）。
- 力量/生命/快速仍未设上限，未改权重。

### 猎手

- `skl_a1_w1_1` 伤害 10→13.5。
- `skl_a2_w1_1` 伤害 12→16.2。
- `skl_a3_w1_1` 伤害 24→32.4；`ChargeBaseSeconds 0.5→0.35`（满档 1.25s，冷却 0.3 未改）。
- `skl_a1_passive.tres` 新增 `AirborneDamageBonus=0.2`，描述同步“空中伤害 +20%”。
- `AirJumpPassiveAbility`：空中时给 `DamageMultiplier ×1.2`，落地/禁用/换装/退树移除；不叠加。

### 测试探针

- `SurvivalModeProbe`：生产配置断言改为 20 分钟、4 首领；12 秒副本只保留终局首领，避免副本插入中间首领；终局首领死亡后排空进化奖励再撤离；摘要断言“20”。

## 2. 验证结果

| 项 | 结果 | 证据 |
|---|---|---|
| Debug 构建 | **0 警告 / 0 错误** | `build.ps1 --no-incremental -v minimal` |
| ExportRelease 构建 | **0 警告 / 0 错误** | `dotnet build ... -c ExportRelease` |
| Main 自测 | **exit 0、stderr 0 字节、selftest.flag 不存在** | `Logs/validation/v5-main-selftest.out.log` / `.err.log` |
| 生存无头专项 | **PASS** | `Logs/evidence/survival/run-20261031_200034.json` |
| 生存渲染专项 | **PASS** | `Logs/evidence/survival/run-20261031_200244.json` |
| 内容校验 | **PASS** | `Logs/evidence/update-fixes/run-20261031_195959.json` |
| 五存档 | 生存专项报告 `SavesUnchanged=true` | 同上 |
| 4 首领中间波次 | ⚠️ 生产配置/代码已接入，但现有短副本只测终局首领；未跑 20 分钟真实长局 | — |
| 空中 +20% 实际伤害 | ⚠️ 代码/配置已接入，未做独立运行时断言 | — |
| 奖励多样性专项 | ❌ 失败；该探针仍在断言已退用的“十二专属项”，不是本轮新增失败 | `Logs/validation/update_fixes_20261031_200008.reward_variety.headless.log` |
| 071 专用面板 | ✅ 已实施；26/0 渲染专项 + 截图 | [071-training-ground.md](071-training-ground.md) |

## 3. 边界

- 未改 `docs/reviews` 之外的验收工作簿；未写真实存档；未关闭用户编辑器。
- 未提交/推送；`project.godot` 等既有未提交改动未混入。
- 4 首领的中间波次、空中伤害和 20 分钟实际等级/帧时仍需真实长局或专项夹具验证；不能把当前短副本通过写成完整验收通过。

## 4. 主要文件

`Config/Modes/twilight_survival.tres`、`Config/Tables/Bosses/boss_final.tres`、`Scenes/Enemies/Boss.tscn`、`Scripts/Config/SurvivalDefinition.cs`、`Scripts/Run/Survival/SurvivalDirector*.cs`、`Config/Tables/Upgrades/upg_{crit,critdmg,hard}_plus*.tres`、`Config/ConfigIndex.tres`、`Config/Localization/*.tres`、`Config/Validation/content_inventory.tres`、`Config/Tables/Skills/skl_a1_w1_1.tres`、`skl_a2_w1_1.tres`、`skl_a3_w1_1.tres`、`skl_a1_passive.tres`、`Scripts/Abilities/AirJumpPassiveAbility.cs`、`Scripts/Testing/SurvivalModeProbe.cs`。
