# 会话交接快照（2026-10-18）

> **给下一个会话**：读完这份 + `docs/DEEPSEEK_HANDOFF.md`，你就能无缝接上。
> **别复述历史，直接按「下一步」干。**

---

## 1. 现在在哪

| 项 | 值 |
|---|---|
| **HEAD** | J12 本批提交（此前 `b206b64`；具体哈希以 `git log -1` 为准） |
| **已完成批次** | A/E/B/C/D · F/G/H · I · J1~J12 |
| **当前正在做** | 交接单 8 项里 **第 1、2、3 项已完成**；**第 4-8 项未开始** |
| **改动过的用户裁定** | 弓武技1 档位/弹速/不漏怪（J10）· 符文系统（J11）· 胶囊扫掠/全局实际弹速200（J12） |

## 2. 硬指标基线（**必须逐位不变**）

```
配置计数（8 硬指标之一，逐字比对）：
  武器6/角色2/敌人10/首领1/召唤物1/技能39/连招3/Pressure7/效果14/节点8/局外5/AI11/能力卡4
  +「（ConfigIndex.tres 已加载）」
节点 10（高2/中5/低3）
猎手 220/20/6.5
索引（14 个数，J11 后）：6/2/10/1/1/39/7/14/8/5/6/11/4/11
  ⚠️ J11 前是 13 个数；末尾 11 = 符文表
Pressure 3/3/0
白板 boss 315/0
奖励 UI True/Paused True
最小链路 1 动画事件→1 弹体→命中
```

**存档 SHA256（跑前=跑后）**：`meta 6C29…7EA7` · `ability_cards 594B…99CA` · `settings 3389…0864` · `loadout CD92…5462`

⚠️ **J11 新增了独立存档 `user://rune.cfg`** —— **没动 `meta.cfg`**，所以 meta 基线不变。

## 3. 五个回归探针（**改动后必须全绿**）

```powershell
_ChargeReleaseProbe     80/0
_CharacterDesignProbe  115/0
_DragonArrowProbe       42/0
_BowSweepProbe          39/0（渲染40/0；J12新增16条，旧23条保留）
_RuneProbe              83/0
```

**⚠️ 它们的期望值是"真实读数"，不是"随手改绿"** —— 如果你看到某条要从 80 掉到 79，**先查是不是真的回归了**。

当前一键复验：`& .\_tools\verify_projectile_capsule.ps1`（双构建＋五探针＋弓渲染＋Main八指标＋存档保护）。J12 索引维护已完成，MCP状态健康：217文件／6216节点／13261边；CLI重建2.9s。

## 4. 双构建的正确姿势（**踩过坑**）

```powershell
Set-Location 'F:\Godot项目\项目a'

# ✅ 必须在同一个 pwsh 会话里：先 build.ps1（它设置 SystemDrive / DOTNET_ROOT）
& 'F:\Godot项目\项目a\build.ps1' --no-incremental -v minimal

# 然后才跑 ExportRelease（否则报 "找不到指定的 SDK Godot.NET.Sdk/4.7.2"）
dotnet build ActRoguelike.csproj --no-incremental -v minimal -c ExportRelease

# Main 自测（必须带场景 + --selftest）
& 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' `
  --path 'F:\Godot项目\项目a' --headless --quit-after 40000 res://Scenes/Main.tscn -- --selftest
```

## 5. ⭐ 下一步（**5.1/5.2 已完成，从 5.3 继续**）

### 5.1 ✅ 胶囊 `IntersectShape`（J12 已完成）

`Projectile.SweepFrame` 已改为覆盖整段的胶囊查询、沿弹道投影排序、同 RID 去重、预算停点续扫；World 按球扫掠求近面阻挡，覆盖长墙、墙内出生和侧墙擦边。`SweepRadius` 导出默认 **0.08 世界米**；不开扫掠仍走原射线。常规一次 IntersectShape，容量填满时扩容重取。⚠️ 未做性能基准，不宣称比旧实现更快。

双靶间距仍 **1.6m**，因限速后单帧长 **3.333m**，位置改为 z=18.2/16.6；两靶同一物理帧按顺序命中，各一次。实测、截图和新 16 条覆盖见 [J12](character-design-j12.md)。

### 5.2 ✅ 全局实际弹速上限 200（J12 已完成，供用户后续肉眼调）

调节入口：`Config/ConfigIndex.tres` 的 **ProjectileMaxSpeed=200.0**（`ConfigIndexData` 的 `[Export]`，≤0 不限速）。所有 Projectile 在 Launch、弹射及每帧重力/追踪后统一夹取真实速度；实际读 `ActualSpeed`，原始 `Speed` 和技能 `LastShotSpeed` 不改。

实测：基础箭160→160，满蓄弓220→200，满蓄龙之箭320→200；重力后单帧位移3.333m；改全局参数120后新弹初速120。双构建0/0、Main退出0/八指标逐字一致、五探针80/115/42/39/83全0失败、渲染40/0、四存档保持、flag清理，见 [完整证据](character-design-j12-evidence.log)。

### 5.3 交接单第 4 项：**逐项补 11 条进化**

见 `docs/DEEPSEEK_HANDOFF.md` 第 34-45 行。**清单**：
- 金光术 `evo_golden_qi`（灵剑武技1 每段真 ATK 额外剑气，**无 handler**）
- 大拔刀术（**先核实真实表行 Id**，审计写的 `evo_big_draw` 不可靠；**收窄到拔刀式自身**）
- 神降术（灵剑额外落剑式范围/伤害/CD2；**真实落地**）
- 怜悯 `evo_mercy`（**只出镰刀最后一刀、该刀 +50%**，旧全局倍率错）
- 普渡 `evo_salvation`（**仅镰刀末刀击杀回血**，可能需给攻击/击杀事件加段序号）
- 喜爱 `evo_affection`（**飞镰每 2 秒移向离玩家最近敌**，当前未接）
- 气贯长虹 `evo_rainbow`（禁手动召回、落地立刻自动收、**全部蓄力时间减半**；旧行只改 base/step，**max 也应同步 1 秒**）
- 金光大道 `evo_golden_road`（**只长枪 index2 下劈**，旧全局修饰错）
- 浮光跃金 `evo_light_leap`（**额外次数换基础跳高并直接瞬移**，旧 `JumpVelocityMultiplier 1.25` 错）
- 地爆 `evo_ground_burst`（**共通落地击退，覆盖镰刀/长枪**，旧只改灵剑落剑式）
- 专注/专一/花心/心流/乱金柝（真实链路验证；**心流要"基础/后置蓄力 0.1 + 完成自动发 1 箭"**）

### 5.4 交接单第 5-8 项

- **第 5**：异化回原文逐条审（**杀戮践踏原文是"起跳"伤害周围**，旧类 `StompOnLandRule` 按落地取证）
- **第 6**：**统一玩家伤害与暴击快照**（5% 暴击 / 2 倍爆伤；很多近战/落/闪/投掷 `CanCrit=false` 且未合元进度）
- **第 7**：独立动作与美术
- **第 8**：六配装平衡重测（**旧 H 的长枪五段不能当现在四段的 DPS 证据**）

### 5.5 符文系统**还没做**的（J11 明确登记）

| 项 | 状态 |
|---|---|
| **符文选择 UI**（基地交互物 / 面板 / 图标） | ❌ `IconPath` 列已占位 |
| **21 条"雷/战神/风系特化增幅"** | ❌ **本批没建** → 所以"带符文能解锁增幅"目前只有机制、没有内容 |
| **`快乐小子` 奔跑穿透 + 角色虚化** | ❌ 新机制（触发器 + 虚化） |
| **飞翼"下落攻击速度更快"** | ❌（要动 `FallAttackAbility`） |
| **小精灵的模型/动画** | ⚠️ 机制有、模型是占位小球 |
| **第 12 枚符文** | ❌ **原文只有 11 条**，等用户给内容 |

⚠️ **预设值待用户确认**：死铠 20/10 杀 · 生死 1 次 · 狙神远端 ×1.5 + 3m/15m · 小精灵 20/8m · 飞翼 ×1.3/×0.8

## 6. 关键坑（**每个都真踩过**）

| 坑 | 说明 |
|---|---|
| ⚠️ **双构建顺序** | **先 `build.ps1` 再 ExportRelease**，否则 SDK 解析失败 |
| ⚠️ **`.tscn` 的 `parent` 必须引用已定义节点** | 否则节点被静默丢弃（修 UI 浮层卡了 3 轮） |
| ⚠️ **`ConfigIndex.tres` 三处同改** | `[ext_resource]` + 数组项 + `load_steps`；**少一处 → 全数值静默回退** |
| ⚠️ **浮点档数** | `(1.4-0.5)/0.3` 在 float 下是 `2.9999998` → `Floor` 少一档。J10 已加 `+1e-4` 容差 |
| ⚠️ **`#if DEBUG` 探针** | 探针里调 `#if DEBUG` 的方法 → **ExportRelease 编译失败**。整个探针要包进 `#if DEBUG` |
| ⚠️ **`evaluate_runtime_expression` 限制** | 不支持 `load()`；属性必须 snake_case（`global_position` 可以、`GlobalPosition` 报错） |
| ⚠️ **MCP `run_project` 拒启动** | 已有会话时；**按 PID 关子进程，不要用全局 `stop_project`** |
| ⚠️ **自测必须带场景** | `res://Scenes/Main.tscn`；不带会进基地 → 卡在第 20 阶段 |
| ⚠️ **`selftest.flag` 跑完必须删** | |
| ⚠️ **不要关 Godot 编辑器** | 它是 MCP 宿主 |

## 7. 有用的工具

- **`codegraph`**（代码结构索引，v0.9.3）—— **先用它再 grep**：`codegraph context "<任务>"` / `query "<符号>"` / `files` / `status`
  - 详见 `docs/codegraph-usage.md`
  - ⚠️ **不含 GDScript**
- **`_tools/verify_*.ps1`** —— 各批的完整复验脚本
- **`.gdmcp/bin/gdmcp.exe`** —— MCP CLI（`doctor` / `tool-call run_project` / `runtime screenshot`）

## 8. 用户偏好（**记住这几条就够**）

1. **中文汇报**
2. **证据优先** —— 给**实测输出**，不要说"应该可用"
3. **未验证的必须标 ⚠️**
4. **每步构建通过 + 实跑验证 + 同步 docs**
5. **宁缺勿假** —— 不编链接/API
6. **简洁**
7. **表驱动是手段不是教条** —— **该改 C# 就改**，别为了"不改代码"搞脆弱间接
8. **需要他确认的记起来**，不要每题都打断；**有歧义先滞后，等他空了再定**

## 9. 一句话

**从 5.3 的进化补齐开始** → **5.4 的 5-8 项** → **5.5 的符文补全**。5.1（胶囊）+5.2（弹速200）已由J12完成，不要重做。用户已明确授权Codex继续项目，不受旧“停工交DeepSeek”历史段落限制；上下文较长时可直接新建对话接续。

**每批交付前**：双构建 0/0 · Main 退出 0 · 8 硬指标 · 5 个回归探针 · 存档 SHA256 · 提交 push · 更新 docs。
