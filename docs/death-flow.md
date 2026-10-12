# 死亡流程（阵亡 → 死亡动画 → 演示过渡 → 回基地）

> 用户需求 §五 #3 原话：「玩家死亡时 → **进死亡动画** → 然后进**演示动画**（相当于场景切换的过渡，
> 目前没有可以给个**简单的面板做占位**）→ **回到基地**」。
> 相关：[stages.md](stages.md)（关卡与胜负）、[ui.md](ui.md)（界面外观唯一入口）、[combat.md](combat.md)（护甲/伤害管线）。

---

## 0. 30 秒结论

| 问题 | 答案 |
|---|---|
| 谁触发死亡流程？ | `RunController._Process` 里既有的那一句 `if (_player is { IsDead: true } && !_runEnded) EndRun(false)` |
| 三段时间轴在哪？ | `Scripts/UI/DeathScreen.cs`：**死亡动画**（1.4s，整棵树**不暂停**，倒地动画能播）→ **演示过渡面板**（3.2s，整棵树暂停 + 倒计时进度条）→ `ChangeSceneToFile(Base.tscn)` |
| 面板长什么样？ | `Scenes/UI/DeathScreen.tscn`：整屏 Dim + 主题面板 + 「本局结束」标题带 + 结算文案 + **「演示动画（占位）」** 一行 + 倒计时条 + 「按 Enter / 空格跳过」 |
| 动过 `RunController` 吗？ | **动了两处**（下面 §3 逐行列出）：① 阵亡分支改成"先交给 DeathScreen"；② `CharacterCardText` 改成 `public`（给基地配置台复用文案）。伤害管线、Stage/Encounter 一行没动 |
| 没有 DeathScreen 的场景会怎样？ | 自动落回**原来的老路**（`RewardScreen` 弹一条"本局结束"、按 Enter 回基地）—— 单独跑某个测试场景不会卡住 |
| 占位的是什么？ | 用户明确说"目前没有可以给个简单的面板做占位"，所以**没有做结算系统**：面板只把 `RunController` 已经算好的文案显示出来，倒计时结束就回基地 |

---

## 1. 时间轴（都能在 Inspector 上改）

| 段 | 导出属性 | 默认 | 这期间的树 | 画面 |
|---|---|---|---|---|
| ① 死亡动画 | `DeathAnimationSeconds` | **1.4 s** | **不暂停**（`GetTree().Paused = false`） | 玩家的 `die` 倒地动画由 `UnitAnimator` 播、`UnitPresentation` 在这段时间后销毁尸体（`DeathLinger = 1.1s`） |
| ② 演示过渡（占位） | `HoldSeconds` | **3.2 s** | **暂停**（这一段背后不该再打） | 面板淡入 0.45s（`FadeSeconds`）+ 倒计时 `ProgressBar` 0→1 |
| ③ 回基地 | `TargetScene` | `res://Scenes/Levels/Base.tscn` | 切场景前解除暂停 | `RunSetup.Clear()` → `ChangeSceneToFile` |

中途按 **Enter / 空格**（`ui_accept`）可以跳过第 ② 段的等待，直接回基地。

⚠️ **为什么第一段不能暂停**：`UnitAnimator`/`UnitPresentation` 都是普通节点（`ProcessMode.Inherit`），
整棵树一暂停，倒地动画就冻在第一帧 —— 那就不是"进死亡动画"了。所以暂停是**第二段才打开**的，
而 `DeathScreen` 自己是 `ProcessMode.Always`，两段都照常走（`_Process` 里的倒计时不受暂停影响）。

⚠️ **为什么它不继承 `OverlayScreen`**：与 `LoadingScreen` 同样的理由 —— 它是**流程节点**不是"弹层"：
不走界面栈、要自己决定暂停时机（第一段绝不能暂停）。因此它挂在场景根下（`Main.tscn` 的
`DeathScreen` 节点），由 `RunController` 按名字取用。

---

## 2. 死亡时顺手关掉界面

`DeathScreen.BeginDeath()` 第一件事是 `UiService.Instance?.CloseAll()` + `GetTree().Paused = false`：
玩家可能是"奖励三选一/商店界面开着的时候"被打死的（那种情况树是暂停的），不先放开界面，第一段动画会被冻住。

---

## 3. 改动清单（**只动了一个玩法文件**）

### `Scripts/Rewards/RunController.cs`（`EndRun`）

```csharp
int earned = RunSettlement.ShardsFor(_stageIndex, _stage?.ChallengesUsed ?? 0, victory);
MetaProgress.AddShards(earned);
body += $"\n获得碎片 +{earned}（累计 {MetaProgress.Shards}）";

// 阵亡 → 死亡流程（#3）：死亡动画 → 演示过渡面板（占位）→ 回基地。
if (!victory && GetTree().CurrentScene?.GetNodeOrNull<DeathScreen>("DeathScreen") is { } death)
{
    _restartOnDismiss = false;          // 回基地由 DeathScreen 自己切场景
    death.BeginDeath("本局结束", body);
    return;
}

_screen?.OpenMessage(victory ? "胜利" : "本局结束", body, "按 Enter 或点击重新开始");
```

**与改动前逐条对照**：

| 项 | 改动前 | 改动后 |
|---|---|---|
| 失败路径 | 立刻弹 `RewardScreen` 消息，等玩家按 Enter → `OnMessageDismissed` → 回基地 | 先播死亡动画 → 演示过渡面板 → 自动回基地（血条/界面状态由 DeathScreen 负责） |
| 胜利路径 | 不变 | 不变（还是 `OpenMessage` → 按 Enter） |
| 碎片结算 | `MetaProgress.AddShards(earned)` | 不变（位置提前到分支之前，数值一样） |
| 顺手修的既有 bug | 原来连着调了两次 `OpenMessage`（第二次想补上"获得碎片 +N"那行），而 `UiService.OpenMessage` 要求 `IsOpen == false` → **第二次必然被拒**，那行从来显示不出来 | 现在只有一次调用，`body` 里已经带了碎片行 —— 截图里能看到「获得碎片 +0（累计 279）」 |

### `Scripts/Rewards/RunController.cs`（`CharacterCardText` / `DefaultCharacterTitle`）

只是把角色卡文案的**唯一来源**开放给基地配置台（用户需求 §五 #4 的界面复用），见 [ui.md](ui.md) §16.5：
`CharacterCardText` 改为 `public static`（并把标题抽成 `DefaultCharacterTitle`），**行为/文案一字未改**。

### 新增（都是"只加"）

| 文件 | 作用 |
|---|---|
| `Scripts/UI/DeathScreen.cs` | 三段时间轴（`BeginDeath` / `PhaseName` / `CountdownProgress` 只读属性给探针用） |
| `Scenes/UI/DeathScreen.tscn` | 占位面板的外观（主题驱动：`UiTitle` 标题带、`UiCaption` 说明） |
| `Scenes/Main.tscn` +1 个节点 | `DeathScreen`（`layer = 30`，在 HUD/所有弹层之上） |

---

## 4. 怎么验证（命令行，不依赖 MCP）

拍摄台 = **继承自 `Main.tscn` 的场景** `Scenes/UI/_FeelDeath.tscn`（多挂了一个 `FeelProbe` 节点）+
`Scripts/Testing/_FeelProbe.cs` 的 `Mode = "death"`：

```powershell
& 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe' `
  --path 'F:\Godot项目\项目a' 'res://Scenes/UI/_FeelDeath.tscn'
```

它会：把 `RunSetup.HasSelection = true`（跳过开局界面）→ 等 1.6 秒 → 对玩家 `ForceKill()` →
按时间点打印三段的 `阶段 / 倒计时 / 暂停`，并各拍一张图 → 等它自己切场景 → 断言"当前场景 = Base"。
拍摄台自己会**挪到树根**（否则切场景时会被一起释放，断言不到最后一步）。

### 实测输出（`UserDir/logs/run_20261012_194409.log`，0 条 ERROR）

```
[Feel] 当前场景=Main ... DeathScreen 找到=True 死亡动画 1.4s / 演示面板 3.2s
[Feel] 拍摄台已挪到树根（切场景时不会被一起释放）
[Feel] 关卡玩家：找到=True 生命=270/270 暂停=False
[log] [Flow] 死亡流程：进死亡动画（1.4s，期间不暂停）→ 演示过渡面板（占位 3.2s）→ res://Scenes/Levels/Base.tscn
[Feel] 死亡流程①死亡动画：正在播=True 阶段=death 倒计时=0（期望 正在播=True 阶段=death）
[log] [Flow] 死亡流程：进演示过渡面板（占位）—— 本局结算已显示，3.2s 后自动回基地
[Feel] 死亡流程②演示面板：正在播=True 阶段=panel 倒计时=0.21 暂停=True（期望 阶段=panel）
[Feel] 死亡流程②进行中：正在播=True 阶段=panel 倒计时=0.5
[log] [Flow] 死亡流程：演示过渡结束 → 回到基地场景 res://Scenes/Levels/Base.tscn
[log] [Flow] 基地换装预览：角色 Gunner / 武器 Pistol（基地场景真的重新加载了）
[Feel] 死亡流程③回到基地：当前场景=Base 文件=res://Scenes/Levels/Base.tscn HasSelection=False 暂停=False
```

| 截图 | 内容 |
|---|---|
| `docs/screenshots/feel_10_death_anim.png` | ① 死亡动画段（面板还没出现，世界里能看到倒地） |
| `docs/screenshots/feel_11_death_panel.png` | ② 演示过渡面板淡入（标题 + 结算文案 + 「演示动画（占位）」+ 倒计时条） |
| `docs/screenshots/feel_12_death_panel_full.png` | ② 面板完全淡入、倒计时到一半 |
| `docs/screenshots/feel_13_back_to_base.png` | ③ 已经回到基地场景 |

---

## 5. 已知边界（都是"故意没做"）

| 项 | 现状 | 要做的话 |
|---|---|---|
| 真正的"演示动画" | 按用户原话用**占位面板**（一段说明 + 倒计时） | 把 `DeathScreen` 第 ② 段换成回放/演出即可 —— `HoldSeconds` 那一段是唯一的落点 |
| 不做结算系统 | 面板只显示 `RunController` 已经算好的文案（关卡、用时、构筑、碎片），**没有**单独的结算界面/动画 | 用户明确"别真去做结算系统" |
| 阵亡后的碎片结算 | 与改动前一致（`RunSettlement.ShardsFor`，失败也有基础碎片） | —— |
| Boss 战里的阵亡 | 走同一条路（`EncounterFailed` 之外不再有别的分支） | —— |
| 没有"续命/复活" | —— | 需要的话在 `BeginDeath` 之前拦一道 |
