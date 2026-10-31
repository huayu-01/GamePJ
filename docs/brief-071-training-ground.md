# 交接：071「基地木桩 → 测试场入口」

> 给新会话的上下文包。读完这一份 + `docs/SESSION-SNAPSHOT.md`，就能直接开工。
> 需求来源：`F:\Godot项目\项目a\验收和需求\更新与修复验收.xlsx` → 需求清单第 80 行（序号 **71**），当前状态 `待处理 / 未验收`。

---

## 1. 需求原文（逐字，别改写）

> **修改｜将基地的木桩移除,换成一个新的测试场入口**
>
> 测试场里包含一些不同类型的木桩,应当包含:单个普通木桩/持续刷护甲的木桩/会格挡的木桩/有无敌的木桩/会朝前发射子弹的木桩/会攻击前方区域的木桩/木桩群;这些都在场景里.并且合理分区;
>
> 然后可以选增幅,可以重置状态清空增幅等;

拆成可验收的 4 条：

1. 基地里的练习木桩**移除**，原位置换成**测试场入口**（交互物，走近按 F 进场）。
2. 测试场场景里**同时存在 7 类木桩**：普通／持续刷护甲／会格挡／无敌／朝前发射子弹／攻击前方区域／木桩群，且**分区合理**。
3. 测试场里**能选增幅**（拿到构筑条目）。
4. 测试场里**能重置状态、清空增幅**。

## 2. 现状取证（这些是我今天实际读过的，可直接用）

### 2.1 基地里的那块木桩

- 场景：`Scenes/Levels/Base.tscn:36` → `[node name="PracticeTarget" ... instance=ExtResource("25_target")]`，位置 `(0, 0, -7)`。
- 预制体：`Scenes/Training/BasePracticeTarget.tscn`（`PracticeTarget.cs`）→ 内部实例化 `Scenes/Training/TrainingDummy.tscn`，`RespawnSeconds = 2` 自动复原，Scene 里带一个 `Label3D`：`练习区 · 伤害真实结算 / 木桩击破后自动复原`。
- `PracticeTarget.cs`：`Target` / `HitCount` / `RespawnCount`，只统计真实 `Unit.Damaged`，不参与战斗判定。

### 2.2 已有的木桩预制体（可复用，不用从零做）

| 文件 | 关键值 |
|---|---|
| `Scenes/Training/TrainingDummy.tscn` | `Faction = Enemy`，`MaxHealth 600`，`StartingArmor 60`，`ArmorDamageReduction 0.25`，`Tags = ["Dummy","Tank"]`，`MeshInstance3D` 模型 |
| `Scenes/Training/TrainingAttacker.tscn` | `MaxHealth 400`，`Tags = ["Melee","Dummy"]`，带 `Movement` + `Label3D` |
| `Scenes/Training/TrainingShooter.tscn` | `MaxHealth 300`，`Tags = ["Ranged","Dummy"]`，带 `Movement` + `Label3D` |

### 2.3 基地入口是怎么做的（照抄这个模式最省事）

`Scripts/Run/BaseCamp.cs:27-39`：

```csharp
var trial = new Node3D { Name = "SurvivalGate", Position = new Vector3(-10, 0, -5) };
trial.AddChild(new InteractionComponent {
    Name = "InteractionComponent", Kind = InteractionKind.Expedition, Prompt = "暮光回廊 · 生存试炼",
    Destination = RuntimeResources.Load<ExpeditionDestination>("res://Config/Modes/survival_destination.tres")
});
GetParent().AddChild(trial);
```

- `InteractionComponent`（`Scripts/Interaction/InteractionComponent.cs`）：`Kind`（16 种内置）+ `Definition`（自定义后端/模型）+ `Prompt` + `TargetScene` + `Destination`。
- `Kind = Expedition` 的后端是 `BaseTerminal`；`Scripts/Interaction/BaseTerminal.cs:91` 用 **`Destination?.ScenePath ?? TargetScene`** 决定去哪。
- `ExpeditionDestination`（`Scripts/Config/ExpeditionDestination.cs`）：`Title` / `Instructions` / `ScenePath` / `ModeSettings`；样例 `Config/Modes/survival_destination.tres`。
- ⚠️ **结论：新开一个测试场不需要加新的 `InteractionKind`** —— 新建一个 `ExpeditionDestination` 指向测试场场景即可（这比动枚举干净得多）。若你确实要独立外观，用 `Definition` + `VisualStyle`。

### 2.4 增幅与重置，已有的东西

- `Scripts/UI/BuildScreen.cs`：基地的构筑面板（读 `RunController.Instance?.Build`，列出 `build.Acquired`）。
- `Scripts/Rewards/RunBuild.cs`：`Acquired` / `StacksOf(id)` / `Add(definition, context)`。
- `Scenes/Debug/GmConsole.tscn`：GM 控制台，已支持**按类别发放增幅**（可当"选增幅"的现成入口）。
- `RunController`：整局构筑/奖励的宿主；基地练习的生命周期在 `BaseCamp`（**基地没有 `RunController` 的奖励池**，见 `BaseCamp` 注释）。
- ⚠️ **别另造一套增幅系统**。先决定：测试场是"复用 `BuildScreen` + `RunBuild` 的临时构筑"，还是"直接开 GM 面板"。两者都能满足 071，成本差很多。

### 2.5 木桩要做到 7 类，Unit 这边的现成 API

- `Unit.ApplyBaseStats(maxHealth, armor, maxShield, moveSpeed)`、`Unit.FillVitals()`、`Unit.GrantArmor(amount)`、`Unit.Armor`、`Unit.Health`、`Unit.Heal(...)`
- **无敌**：`Unit.IsInvulnerable { get; set; }`（探针里就是这么用的）
- **格挡**：`Unit.ResolveBlock` + `HitInstance.BypassBlock`；玩家侧实现是 `Scripts/Abilities/GuardStanceAbility.cs`（它依赖输入/状态机，**木桩别直接挂它**，要么写一个 10 行的常驻格挡组件，要么把 `IsInvulnerable` 换成"只挡正面"的简化判据）
- **攻击前方区域**：`MeleeAttackAbility` + `AttackHitbox`（`Scenes/Enemies/Chaser.tscn` 就是这个组合的最小样例）
- **朝前发射子弹**：`ProjectileAttackAbility` + 武器；也可直接复用 `TrainingShooter`

## 3. 建议的实现顺序（4 步，每步可独立验收）

1. **入口就位**：新建 `Config/Modes/training_destination.tres`（`Title = "测试场"`，`ScenePath = "res://Scenes/Training/TrainingGround.tscn"`）；`BaseCamp.cs` 里照 §2.3 加第二个入口，位置沿用 `(0, 0, -7)`；**删掉** `Base.tscn` 的 `PracticeTarget` 节点（含它的 `ext_resource`）。
2. **测试场骨架**：新建 `Scenes/Training/TrainingGround.tscn`：地面 + `Player`（`Scenes/Player/Player.tscn`）+ `Hud` + `WorldOverlay`。**不要挂 `RunController`/`StageController`**（那是冒险/死战的生命周期）；要一个轻量控制器就新写，或者纯场景 + 一个自己的脚本。
3. **7 类木桩 + 分区**：建议按"一圈 6 个分区 + 中间一群"布局，每个分区用 `Label3D` 写清用途（照 `BasePracticeTarget.tscn` 里 `Sign` 的写法）。木桩统一走一个 `TrainingDummyKind` 枚举驱动的组件（普通／护甲／格挡／无敌／射击／区域／群），**别为每一类复制一遍场景**。
4. **增幅与重置**：两个交互物（或一个面板）：
   - 选增幅：复用 `BuildScreen` 或 GM 的发放路径；
   - 重置：清空构筑 + `Player.ApplyBaseStats(...)/FillVitals()` 复位。**要能反复用**（验收会按两次）。

## 4. 验收（照这个跑，别只说"应该能用"）

| 项 | 标准 |
|---|---|
| 双构建 | `build.ps1 --no-incremental -v minimal` 与 `dotnet build ActRoguelike.csproj --no-incremental -v minimal -c ExportRelease` 均 **0 警告 0 错误** |
| Main 自测 | `Godot…console.exe --path F:\Godot项目\项目a --headless --quit-after 40000 res://Scenes/Main.tscn -- --selftest` → **退出 0、stderr 0 字节**，8 硬指标逐字一致 |
| 现有探针 | 蓄力 **80/0** · 原文 **115/0** · 龙之箭 **42/0** · 弓 **39/0** · 符文 **83/0** · 角色替换 **61/0** · 草地基准 **34/0** |
| 存档 | 五份真实存档 SHA256 跑前＝跑后；自测后 `selftest.flag` 必须不存在 |
| 新专项 | 建议 `Scenes/Debug/_TrainingGroundProbe.tscn`：headless 校验 7 类木桩都存在、分区标记齐全、入口 `Destination.ScenePath` 指对、重置两次后 `RunBuild.Acquired` 为空；渲染模式再拍 4 张分区截图 |
| 交付 | `docs/reviews/<日期>/071-training-ground.md` + `Logs/evidence/`；commit（精准清单）+ push |

## 5. 已知会踩的坑

1. **`.tscn` 的 `parent` 必须指向已定义节点**，否则节点被引擎**静默丢弃**（历史上修浮层卡了 3 轮）。
2. 删 `Base.tscn` 的 `PracticeTarget` 时，**连 `[ext_resource]` 一起删**，否则 `load_steps` 与实际不符（Godot 容忍，但会留隐患）。
3. **别关用户的 Godot 编辑器**（它是 MCP 宿主），也别 `stop_project`（会杀掉别人的自测）；只结束自己启动的 PID。
4. 构建/运行前确认锁：`.ai/locks/validation.lock`（构建/游戏验证）、`.ai/locks/git.lock`（git）。
5. 自测走内存存档（`MetaProgress` / `RuneCarry` 的 `UseInMemoryStoreForSelfTest`），跑完清 `selftest.flag`。
6. 日志写 `Logs/`（`Logs/.gdignore` 已阻止进资源扫描），**不要写回项目根或 docs**。
7. `project.godot` 有用户/编辑器未提交改动，**保留、不混进提交**。
8. 场景切换用 `RuntimeResources.ChangeScene`；测试场必须自带 `Player` / `Hud` / `WorldOverlay`，否则进场是黑屏。
9. 木桩如果要"会格挡"，注意别把玩家输入抢走 —— 木桩没有输入，建议写成"无输入常驻格挡"的小组件，而不是挂玩家那套 `GuardStanceAbility`。

## 6. 先读这几份（按顺序，30 分钟够）

1. `docs/SESSION-SNAPSHOT.md` —— **当前状态唯一接续入口**（2026-10-30 更新）
2. `docs/README.md` —— 专题索引
3. `docs/architecture.md` —— 分层与契约（表现只读、事件驱动）
4. `docs/interaction-authoring.md` —— 交互物怎么加（本次 071 的核心）
5. `docs/content-authoring.md` —— 增幅/奖励怎么加（第 3 步要用）

---

## 7. 开局提示词（直接粘给新会话）

```text
读 F:\Godot项目\项目a\docs\brief-071-training-ground.md 和 docs\SESSION-SNAPSHOT.md，
然后按里面的 071 需求做：把基地的木桩换成测试场入口，测试场里做 7 类木桩（普通/持续刷护甲/会格挡/无敌/朝前发射子弹/攻击前方区域/木桩群）并合理分区，
进场后能选增幅、能重置状态清空增幅。
按文档里的验收清单跑（双构建 0/0、Main 自测退出 0+8 硬指标、7 个既有探针、五存档不变），
做完更新 docs/reviews/ 并 commit+push。简体中文汇报，未验证的标 ⚠️。
```
