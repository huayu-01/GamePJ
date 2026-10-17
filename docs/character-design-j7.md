# 原始设计纠偏J7：Main 旧同步蓄力探针跨帧迁移

2026-10-17 执行交接单「下一步顺序」第 1 条。只改 `Scripts/Testing/SelfTest.cs`，**生产代码零改动**（`Ability.cs`／7 个 `Ability` 子类／`Weapon.cs`／敌人·首领·召唤物行都没碰）。Debug 与 ExportRelease 均 0 警告／0 错误；Main 自测连续两次退出 0、8 条硬指标与改前逐字一致；四存档保持，`selftest.flag` 不存在。[完整输出](character-design-j7-evidence.log)。

## 病根（改前）

`ProbeChargeMechanism` / `ReportCrossbowProbe` / `BeginBowProbe` 的武技1 段 / `ReportSwordQiProbe` 都在**同一帧**里
`OnInputHeld(整整一段蓄力时长)` → `OnInputReleased()` → 立刻读 `LastShotDamage`，中间用 `SettleShot`
（`ability._PhysicsProcess(0.02)`）和 `WaitUntilIdle` 手动 Tick **技能的物理回调**。

那两个手动 Tick **不推进动画**，于是"松手 → 释放段动画 → 动画 ATK 帧 → 真发射"这一段永远走不到，
读出来的是 `0`／上一轮的快照。表现就是 6 条红灯（完美时机、过完美窗口、后置蓄力、短弩武技2、弓武技1、剑气），
而同一批 J6 的**独立真实输入探针**（`ChargeReleaseProbe`）却是 80/0 —— 差别只在"有没有跨帧等真 ATK"。

## 改法（全是探针侧）

1. 新增分帧小工具（`SelfTest.cs` 内，私有）：
   - `Frame()`：等一个**进程帧**（`await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame)`，与 `ChargeReleaseProbe` 同一套）；
   - `HoldCharge(ability, 目标秒数)`：**每帧**像输入层那样 `OnInputHeld(真实帧 delta)`，不是一次性灌；
   - `WaitSettle(ability, 超时)`：等技能**自己收招**（释放段真 ATK → `Tick` 里的 `Finish`），超时返回 false；
   - `ReleaseAtkFired(ability, settled)`：判据 = `settled && CooldownRemaining > 0` ——
     **只有 `Finish()` 会写冷却，缺事件那条 `Cancel()` 不写**，所以它等价于"这次发射确实由动画事件帧触发"；
   - `WaitAbilityReady(ability)`：等上一个动作真的收招（`IsReady`），超时红灯。
2. 四个探针改成 `async Task`（`RunChargeMechanismProbe` / `RunCrossbowProbe` / `BeginBowProbe` / `RunSwordQiProbe`），
   状态机改成 `case 30/33/34/44: task ??= ...; if (ProbeTaskDone(task, 标签)) { task = null; Next(); }`
   —— **任务没结束就不 `Next()`**（与 J5 的 `ProbeBlinkDash`／`ProbeGuardStance` 同一条纪律，只是用 Task 而不是显式 stage 字段）。
3. **删掉 `SettleShot`**（"同帧手动 Tick 收尾"）：它正是假象来源。
4. 判据同时收严：每条绿行都要 `释放段真ATK到达=True` + `自然收招=True`，**缺事件 = 明确超时红灯**，
   并写清"自然收招=…、冷却=…"。**没有伪造 ATK**（一次都没调 `OnAnimEvent`／`anim_event`），
   **没有给生产能力加"计时出弹"兜底**（生产逻辑一行没改）。
5. 夹具连带修正（也是探针侧）：武技1 现在真的会射箭，原来摆在弹道 0.9m 的 60 血 Chaser 测试靶会被打死
   （`Unit.ApplyStatus` 对 `IsDead` 直接 return），于是 case 35 的标记层数／追踪弹会跟着红。
   两支真箭打完再建靶，并把 `MaxHealth` 抬到 4000 当纯靶子。**不改战斗规则**。

## 7 条检查逐项（改前 → 改后）

| 检查（`SelfTest` 原文行） | 改前 | 改后 |
|---|---|---|
| ① 机制·蓄力未完成松手（0.3s < 基础 0.5s 松手 → 不释放） | ✅（同步） | ✅（**真实帧按住 0.3s**） |
| ② 机制·完美时机（蓄满 + 窗口内松手 → 40 / 400） | 🔴 伤害 0 弹速 0 | ✅ 伤害 40 弹速 400，真 ATK 到达 |
| ③ 机制·过完美窗口松手（过窗 → 30 / 250） | 🔴 伤害 0 弹速 0、连 `TryActivate` 都被上一次占着 | ✅ 伤害 30 弹速 250，真 ATK 到达 |
| ④ 机制·后置蓄力（0.85s → 3 档×1.9；按住 → 封顶 5 档×2.5 → 75 / 625） | 🔴 档=0 伤害 0 | ✅ 档=3 ×1.9；封顶 5 档 ×2.5；伤害 75 弹速 625 |
| ⑤ P3·短弩武技2 大射天下（0 档 3 支 / 3 档 9 支） | 🔴 两轮都 0 支 | ✅ 3 支 / 9 支，两轮都真 ATK |
| ⑥ P3·弓武技1 箭在弦上（基础档 24/220；3 档 45.6/310） | 🔴 全 0 | ✅ 24/220、45.6/310，两轮都真 ATK |
| ⑦ P4·灵剑武技2 拔刀式（未蓄满不释放 / 蓄满生成 ≥1 个剑气） | 🔴 生成 0 个 | ✅ 未蓄满 0 个、蓄满 1 个（真 ATK） |

## 验收

- `build.ps1 --no-incremental -v minimal` → 0 warning / 0 error；`dotnet build -c ExportRelease --no-incremental` → 0 warning / 0 error。
- Main：`--headless --quit-after 40000 res://Scenes/Main.tscn -- --selftest` → **退出码 0**，连续三次（58.4s / 58.3s / 58.2s，
  第三次单独抓 stderr = **0 字节**），日志到 `=== 自测结束 ===`，红色行只剩改前就有、且内容写"期望=实际"的 5 条基线读数标记。
- 8 硬指标逐字一致（配置计数 · 10 节点 2/5/3 · 猎手 220/20/6.5 · 索引 `6/2/10/1/1/39/7/14/8/5/6/11/4` · Pressure 3/3/0 · 白板 boss 315/0 · 奖励 UI True/Paused True · 最小链路 1 动画事件→1 弹体→命中）。
- 独立 `_ChargeReleaseProbe.tscn --headless` 复跑：退出码 0，`通过 80，失败 0`。
- 四存档 SHA256 与交接点逐字相同；`selftest.flag` 不存在。
- 锁文件：`.ai_validation.lock` / `.ai_git.lock` 是 10-16 遗留的 0 字节空文件（gitignored、无持有者），已删除，未重建。

## 边界

- 只剩「5 条前缀为 🔴 的历史读数标记」——它们不是失败，改前就在、内容写的是"期望 315/实际 315"这种。
- `WaitUntilIdle`（14 处）与其余探针仍走"手动推物理回调"；它们不涉及"松手 → 释放段 ATK"，本批没动。
- 短弩武技1 结果那一行判据仍是 `LastShotCount > 0`（没顺手收严成 `== 3`，避免隐式换代）。
- 本批没有截图（证据是文本读数 + 独立探针计数 + 存档哈希）。
- 下一项回到交接单：发散／心眼按命中前标记结算（[DEEPSEEK_HANDOFF.md](DEEPSEEK_HANDOFF.md) 第 2 条）。
