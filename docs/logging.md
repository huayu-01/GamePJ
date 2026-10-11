# 游戏日志（分析测试游玩用）

用户重点需求：把局内交互全部落盘，方便游玩后分析问题。

## 输出位置与格式

- 文件：`user://logs/run_年月日_时分秒.log`
  （实际路径：`C:\Users\<用户>\AppData\Roaming\Godot\app_userdata\ACT Roguelike\logs\`）
- 每次启动游戏新开一个文件，格式：`时分秒 [分类] 消息`
- 同时 `GD.Print` 一份，便于 Godot MCP / 控制台抓取

## 分类

| 分类 | 覆盖内容 |
|---|---|
| `Flow` | 开局、选择角色/武器、进入关卡、Boss 阵亡、本局结算 |
| `Node` | 互动奖励节点（含节点类型）、打开 Boss 门（含已挑战次数 / 免费额度 / 初始压力） |
| `Reward` | 三选一选了哪一条（名称 + 类型） |
| `Spawn` | Encounter 开始、每次生成敌人（含剩余预算与场上数量）、清场完成 |
| `Combat` | 死亡、击杀、暴击、破甲，以及涉及玩家的伤害 |

战斗事件由 `GameplayEventBus` 自动记录（`GameLog.OnGameplayEvent`），关键流程由各系统显式调用 `ActRoguelike.Core.GameLog.Write(...)`。

## 加日志的方式

```csharp
ActRoguelike.Core.GameLog.Write("Flow", $"进入第 {index + 1} 关：{name}");
```

用全限定名，不需要 using。任何系统都可以直接调用（没有实例时只打印到控制台）。

## 实测

跑一局（Base → 角色/武器 → 关卡 → Boss 门 → 击杀 Boss → 下一关），日志文件 12 行，样例：

```
22:14:33 [Flow] === 新的一局 ===
22:14:42 [Flow] 选择角色 Gunner / 武器 Pistol
22:14:42 [Flow] 进入第 1 关：Stage 1 · 外围
22:14:53 [Node] 打开 Boss 门：已挑战 0 次 / 免费额度 2 → 初始压力 0
22:14:57 [Combat] 伤害 Player(260) → Boss(0) 900
22:14:57 [Combat] 死亡 Boss(0) 击杀者=Player(260)
22:14:57 [Combat] 击杀 Boss(0) by Player(260)
22:14:57 [Flow] 本关 Boss 阵亡 → 关卡完成判定
22:14:57 [Flow] 进入第 2 关：Stage 2 · 内环
```

## 已知可改进点

- 运行时生成的单位（Boss 召唤的增援）在日志里显示为 `@CharacterBody3D@8`，没有可读名字，建议改成记录敌人类型（生成项 DisplayName）。
- `[Combat] 伤害` 里括号中的血量是**受伤后**的值，读起来容易误解。
