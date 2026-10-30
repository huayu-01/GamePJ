# 验证与调试

当前统一验证优先使用 `_tools/verify_gameplay_fixes.ps1`，不带 `-Only` 运行维护中的全部机制与渲染专项；旧 `_tools/verify_projectile_capsule.ps1` 保留历史开关组合；先遵守[快照](SESSION-SNAPSHOT.md)的独占锁、进程与存档保护约定。本轮命令为：

所有维护中的验证脚本通过`_tools/LogPaths.ps1`初始化日志目录。构建与进程输出、引擎日志及存档指纹在`Logs/validation/`，完整报告和J11等历史比较基线在`Logs/evidence/`；原`docs/*.log`已迁移，不在docs继续生成报告。`run_selftest.ps1`现在复用同一Main入口及锁／存档保护。

```powershell
pwsh ./_tools/verify_gameplay_fixes.ps1 -Only content,effects,decoupling,interaction
pwsh ./_tools/verify_gameplay_fixes.ps1
pwsh ./_tools/verify_adventure_rollback.ps1
pwsh ./_tools/test_validation_infrastructure.ps1
```

历史 `verify_projectile_capsule.ps1 -ChallengeFeedback` 组合仍串行执行双构建、挑战反馈、隔离编辑器模型预览、交互组件、增援与召唤、整局及玩家流程、基地真实输入和角色展示、实际渲染、导航、敌人、符文UI、原五项回归和Main比对，保存stdout/stderr、当前截图及存档指纹。编辑器检查使用相同源码与程序集的最小隔离工程，不启用用户编辑器插件，不占用MCP端口；`Scenes/Debug/_InteractionEditorProbe.tscn`仅专用命令行参数才执行并退出自建编辑器，正常打开不会自动运行。可F6运行`Scenes/Debug/InteractionComponentLab.tscn`观察类型和新模型，接入见[交互组件](interaction-authoring.md)。加`-ProbeOnly`跳过图形和原五项回归。正式玩家入口为Base → Expedition；Main自测始终显式指定`res://Scenes/Main.tscn -- --selftest`。

J48在此基础上增加真实挑战反馈专项及实际渲染：检查预算耗尽仍有活怪、真实F拒绝与碰撞弹开、清场解除与完成提示、暂停／失败／停止／换关／死亡，以及限定多波的波间空场。最新证据见[J48](archive/development-batches/character-design-j48.md)，`-InteractionModels`保留J47组合。

2026-10-24新增冒险与组合验证：`_tools/verify_gameplay_fixes.ps1 -Only synergy,specialization,specialization_render,landing,reward_ui,reward_ui_render`。取消冒险专项需要真实落盘，使用`_tools/verify_adventure_rollback.ps1`自动创建独立工程、独立程序集和`CodexValidation`用户目录；不能在玩家真实目录直接运行`AdventureCancelProbe`。两套入口共用原工程验证锁，并比较运行前后的五份存档。机制和原始证据见[冒险回退与联动](adventure-synergy-20261024.md)。

公共设施在 `_tools/Validation.Common.ps1`：独占锁、双构建0/0、只管理自己启动的进程、严格空stderr、五份存档检查、独立工程与用户目录、Main八项硬指标和显式内容数量核验。拼错 `-Only` 名称会在构建前失败。`Validation.Legacy.ps1` 为较早的批次入口复用这些设施；可能写盘的旧入口统一在隔离用户目录运行，保留各自场景、机制断言和截图核对。

`test_validation_infrastructure.ps1` 需要先有一次Main日志，或用 `-MainLog` 指定；覆盖错误数量、存档变化保留现场、锁冲突、非零退出、stderr、超时、错误选择器与新增红标。新增内容只更新 `Config/Validation/content_inventory.tres` 的相关表与奖励类别基线；不要自动按当前运行结果更新期望值。

GC缓存竞态回归使用 `./_tools/verify_content_gc.ps1 -Render`：从生产读取入口首次加载真实增幅，阻塞专用进程的终结器，确认弱引用已失效后再调用实际内容校验器，默认重复三次；另跑八轮完整扫描与强制GC、基础资源和原生包装器对照、实际基地渲染。屏障有超时，只用于测试，不在游戏里暂停GC。所有正例必须退出0且stderr为空，五份存档逐次核对；输出在`Logs/validation/gc_*`与`Logs/evidence/content-gc-*`。根因与历史负例见[资源生命周期](resource-lifetime.md)。

以下保留早期通过Godot MCP插件调试的经验，仅适用于确实使用对应插件与接口时。那种方式要求编辑器开着目标工程，并从编辑器面板启动MCP服务器；它不是上述命令行验证的前置条件。

## 先跑 TestLab，不要临时传送敌人

`run_project` 支持 `scene_path`。**不带 `scene_path` 时跑的是编辑器当前打开的场景**（不是主场景），验证时一律显式指定：

```
res://Scenes/TestLab.tscn      纯木桩 / 打击木桩 / 远程木桩 → 命中、伤害、无敌、击退
res://Scenes/TestEnemies.tscn  7 种敌人 + 精英守卫（玩家血量 3000，便于观察行为）
res://Scenes/Main.tscn         Encounter + Spawn Budget 正常流程
```

木桩在场景里**按固定坐标摆好**，测试时不再传送：既避免物理体不同步，也不受 AI 走位影响。

## 摆位与漂移的坑（都真实发生过）

- **传送 `CharacterBody3D` 不会同步物理体**：只改 `global_position`，物理体仍在旧位置。要让 `MoveAndSlide()` 至少跑一帧。
- 不要用 `process_mode = 4` 冻结敌人当靶子：那样 `MoveAndSlide` 不跑，物理体位置永远停在出生点。
- 敌人移动 + 玩家被击退会让预先摆好的几何关系失效。要构造稳定命中，先读 `Muzzle.global_position` 与相机 `project_ray_origin/normal`，沿弹道取采样点再摆目标。
- 准心落点由相机决定（俯角 20° 时在角色前约 4 个单位的地面）。把目标放在角色前 2~2.5 个单位，弹道正好穿过它的胸口。
- 玩家会被打死，验证 Encounter 这类长流程时用 `call_runtime_node_method` 调 `Heal(500)` 续命。
- MCP 偶发返回失败（约三成），脚本里要重试；`evaluate_runtime_expression` 返回空字符串通常就是失败而不是“值为空”。

## 调用方式

- 端点 `http://127.0.0.1:9082/mcp`，POST JSON-RPC `tools/call`。
- 参数必须是对象：`"arguments": {}`。**在 PowerShell 里不要把函数参数命名为 `$args`**（自动变量），否则 `arguments` 会被序列化成数组，工具会卡住不返回。
- “免打扰模式（Vibe Coding）”会拦 `open_scene`/`run_project`/`stop_project`，需要带 `allow_ui_focus=true` / `allow_window=true`。
- 面板改设置时编辑器主线程被占用，此时请求会一直卡住，等它改完再发。

## 输入动作

- `upsert_project_input_action` 写入的事件 `device` 是 16，**真实键盘（device 0）不会触发**。写完必须用 `execute_editor_script` 把所有 `input/*` 事件的 `device` 改成 `-1`，再用 `InputMap.event_is_action` 复核。
- 注意只改 `input/` 下的自定义动作；遍历 `InputMap.get_actions()` 会连编辑器快捷键一起改。

## 运行时验证

- `evaluate_runtime_expression` **只支持单表达式**，多行脚本会报 “Expression execution failed”。读属性优先用 `node_path` + 属性名（相对表达式）。
- 表达式里不能用 `func():` lambda。
- **引擎节点的属性是 snake_case**（`global_position`、`rotation_degrees`、`monitoring`），**C# 脚本属性是 PascalCase**（`Health`、`IsBusy`、`CooldownRemaining`）。写错只会得到空值，不会报错。
- 场景根节点名要和路径一致：`TestLab.tscn` 的根叫 `TestLab`，所以路径是 `/root/TestLab/...`。
- `get_debug_output` 能看到游戏 stdout（含 `GD.Print`）；`get_editor_logs(source='editor_panel')` 看编辑器输出面板。

## 表现层回归（残影 / 抖动，2026-01 加）

`SelfTest` 的最后两个阶段会做一次**表现采样**（只在自测运行时执行，正常游玩零开销）：

1. 按住 W 前进 2.5 秒（`Input.ParseInputEvent` 注入按键）；
2. 每帧记录并汇总：物理位置单帧位移、`GetGlobalTransformInterpolated()` 位移、相机位移、
   角色↔相机相位差、渲染帧间隔。

日志前缀 `[SelfTest] 表现采样：`，判读标准：

| 指标 | 健康值 | 含义 |
|---|---|---|
| 「物理位置位移为 0 的帧」 | 可以很高（60Hz 物理 / 120fps 渲染时约 50%） | 这是**物理量**，正常 |
| 「插值后位移为 0 的帧」 | **必须是 0** | 非 0 = 渲染器在用离散的物理位置 → `physics/common/physics_interpolation` 没开 |
| 「插值后单帧位移 标准差」 | 越小越好（当前 0.006 量级） | 越大越"一顿一冲" |
| 「角色↔相机相位差 标准差」 | 越小越好（当前 0.007 量级） | 大 = 跟随逻辑与物理错拍（跟随必须写在 `_PhysicsProcess`） |

临时把 `project.godot` 的 `common/physics_interpolation` 去掉，即可复现修复前的数值（角色 50% 帧位移为 0、相机单帧位移在 `0 ↔ 0.1167` 之间交替），用来确认这套探针本身有效。

## 生命周期与节点类型注意

1. Godot **先执行子节点 `_Ready`**。组件在 `_Ready` 里读父节点 `_Ready` 才赋的值会拿到 null（曾导致滑铲取消、木桩不攻击、精英把基础血量当 1）。用惰性属性，基础值放 `_EnterTree`。
2. **Godot 只沿直接父节点解析 3D 变换**：把 `Area3D`（命中体）挂在普通 `Node`（Ability）下，它会掉到世界原点——曾导致近战全部空挥、子弹从原点射出。用 `get_node(...).global_position` 对照父节点位置可以立刻发现。
3. **`Area3D.monitoring` 不要反复开关**：常开、只切换 `_active`，否则静止目标不进重叠列表。

## 构建

- agent/CI shell 缺 `SystemDrive` 等变量会导致 NuGet 报 “Failed to load NuGet settings”，用 `pwsh ./build.ps1`。

## 用 MCP 传 GDScript 表达式时的引号规矩

`evaluate_runtime_expression` 的表达式是从 PowerShell 传进去的，踩过的坑：

| 写法 | 结果 |
|---|---|
| `"...get_node(\"X\")..."` | 不行：`\"` 在 GDScript 里是语法错误 |
| `'...get_node('X')...'` | 不行：单引号提前结束 PowerShell 字符串 |
| `"...get_node(`"X`")..."` | 时好时坏 |
| **`'...get_node("X")...'`（外层单引号 + 内层双引号）** | 可靠，推荐 |

另外 **不要在双引号里插值再取属性**：`"$B.Health"` 会被 PowerShell 解析成 `$B.Health` 属性访问。需要拼接时用
`$expr = $B + '.Health'` 再用单引号包好其余部分。

**同一个节点的多个属性连续读取失败时，先怀疑节点本身**（脚本没挂上、路径不对），不要先归咎于工具抖动 —— 我们曾因此把一个「.cs 里放了两个类导致脚本静默挂载失败」的问题误判成 MCP 不稳定。
