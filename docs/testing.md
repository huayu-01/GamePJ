# 用 Godot MCP（端口 9082）跑自动化验证的经验

编辑器必须开着目标工程，MCP 插件在编辑器面板里启动服务器。以下都是实际踩过的坑。

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
