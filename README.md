# ACT Roguelike（3D 动作 Roguelike）

Godot 4.7.2 + C#（.NET 8）。策划与执行规范见 `F:\Godot项目\3d肉鸽雨中哈迪斯\`。

命名：显示名 `ACT Roguelike`，程序集 / 工程文件 / 根命名空间均为 `ActRoguelike`（C# 命名空间不能带连字符）。工程目录仍叫 `项目a`，需要改名时在 Godot 项目管理器里操作。

## 环境

- Godot 4.7.2 stable mono（Forward+，Jolt Physics，Windows 使用 D3D12）。
- .NET SDK 8/9/10 均可，工程目标框架为 `net8.0`（与 GodotSharp 4.7.2 一致）。
- Godot MCP 插件（编辑器内 `MCP` 面板，默认端口 9082）。

## 运行与构建

- 编辑器内直接按运行（主场景 `res://Scenes/Main.tscn`）。
- 命令行构建：`pwsh ./build.ps1`（脚本会补齐 agent shell 缺失的环境变量）。

## 操作

| 操作 | 按键 |
|---|---|
| 移动 | W/A/S/D 或方向键 |
| 跳跃 | 空格 |
| 射击（单发，不连射） | 鼠标左键 |
| 预瞄 | 鼠标右键 |
| 装填 | 被动：3 秒没开火 或 打空弹匣 自动装填 |
| 战术滑铲（带无敌帧） | Q |
| 火力倾泻（打空弹匣） | E |
| 三选一奖励 | 使用地面节点后自动弹出 |
| 互动（地面节点 / Boss 门） | F |
| 主动召唤（角色 3） | G |
| 范围定身（角色 3） | V |
| 帮助 / 操作说明 | H |
| 视角 | 鼠标 |
| 菜单 / 关闭顶层弹层 | Esc |

## 测试

跑 `Scenes/TestLab.tscn`：含**纯挨打木桩 / 只攻击不移动的木桩 / 只远程不移动的木桩**；
跑 `Scenes/TestEnemies.tscn`：一次摆出全部敌人与精英守卫，便于观察行为；
跑 `Scenes/Main.tscn`：Encounter + Spawn Budget 的完整流程。
经验与坑见 `docs/testing.md`。

## 目录

```
Scripts/Combat/      Unit、命中/伤害结算、Hit Policy、投射物、移动能力、属性表、事件总线、受击表现
Scripts/Abilities/   Ability 基类 + 近战/远程/范围/位移/火力倾泻
Scripts/AI/          EnemyBrain（Controller）+ Behavior 与各种行为
Scripts/Weapons/     武器基类（攻击节奏、弹匣、换弹、投射物生成）
Scripts/Player/      玩家控制器（移动）、武器输入（准心/开火/预瞄）、技能输入
Scripts/Camera/      第三人称镜头
Scripts/UI/          HUD
Scenes/              CameraRig / Player / Weapons / Combat / Enemies / Training / Levels / UI / TestLab / Main
docs/architecture.md 架构与扩展方式
docs/combat.md       战斗、命中与伤害结算
docs/rewards.md      强化 / 进化 / 异化与触发器
docs/boss.md         Boss 阶段、Boss 压力、Boss 构筑与技能精英
docs/summons.md      召唤物系统
docs/stages.md       Stage/Node 流程、Base/Configure、局外成长
docs/config.md       数值配置（调参唯一入口：Config/Balance.tres）
docs/testing.md      用 Godot MCP 做自动化验证的经验
docs/logging.md      游戏日志（测试游玩后的分析入口）
```
