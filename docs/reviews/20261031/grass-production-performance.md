# 草原接入正式场景的性能实测（2026-10-31）

用户要求「装到现在的游戏场景里试试性能」。本轮把草地作为**正式入口**接进 `Scenes/Survival.tscn`（暮光回廊），并在真实场景里量静态／战斗 × 有草／无草四组。

## 接入方式（已有改动，本轮只补测量）

`Scripts/Visual/SurvivalMeadow.cs`：正式场景根下新增 `Meadow` 节点，`_Ready` 里等两帧物理同步后构建 `JungleGrassScenery`（丛林树石替换）与 `ShaderGrassPatch`（草地），`SetGrassEnabled(bool)` 只切草地、不动树石。碰撞与导航仍由 `Arena` 提供，草地只做视觉。

## 测量方法

- 探针 `Scripts/Testing/SurvivalMeadowBench.cs` + `Scenes/Debug/_SurvivalMeadowBench.tscn`，直接实例化 `Scenes/Survival.tscn`：**正式 HUD、跟随相机、导航、真实敌人 AI 全部保留**。
- 静态组无敌人；战斗组围绕玩家 6.5 米放 **12 只真实 Chaser**（真实敌人行、AI 开、导航开、近战开）。玩家 50000 生命，采样窗口内实测被命中 16–17 次 → 战斗负载是真的。
- 八次运行：`静态无草 → 静态有草 → 战斗无草 → 战斗有草 → 战斗有草 → 战斗无草 → 静态有草 → 静态无草`，正反各一遍抵消漂移；每次暖场 1.5 秒、采样 3 秒。
- 帧时间 = 相邻帧 `TicksUsec` 差；GPU／渲染 CPU = `ViewportSetMeasureRenderTime` + `ViewportGetMeasuredRenderTimeGpu/Cpu`；draw call／三角面 = `Performance.GetMonitor`。
- ⚠️ **第一轮垂直同步没关**：显示器 160Hz 把帧时间钉死在 6.25ms，草地开销只体现在 GPU 里（+0.44ms）而帧时间一模一样。第二轮起基准内**临时关闭 vsync 与帧率上限**（跑完恢复），帧时间才反映真实开销。

环境：RTX 4070 Ti / D3D12 / Forward+ / 1920×1080 / MSAA 2x / Godot 4.7.2。

## 结果（vsync 关闭）

| 组 | 平均帧 | p95 | GPU | 渲染 CPU | draw call | 三角面 |
|---|---:|---:|---:|---:|---:|---:|
| 静态 · 无草 | 2.16 ms | 3.04 | 1.82 | 0.64 | 569 | 963k |
| 静态 · 有草 | 2.87 ms | 4.16 | 2.37 | 0.98 | 1381 | 1.12M |
| 战斗 · 无草 | 2.85 ms | 4.62 | 1.88 | 0.80 | 824 | 1.80M |
| 战斗 · 有草 | 3.60 ms | 5.61 | 2.41 | 1.10 | 1635 | 1.81M |

（每组两个数取均值；原始每次运行在 `benchmark.json`。）

**草地本身的增量**（有草 − 无草）：

| | 平均帧 | GPU | 渲染 CPU | draw call | 三角面 |
|---|---:|---:|---:|---:|---:|
| 静态 | **+0.71 ms** | +0.55 ms | +0.34 ms | **+812** | +155k |
| 战斗 | **+0.75 ms** | +0.53 ms | +0.30 ms | **+811** | +10k※ |

※ 战斗组三角面差值被敌人动画与遮挡剔除的波动吃掉，不以此为据；draw call 与 GPU 时间是稳定的。

**结论**：
1. **草地开销与有没有战斗基本无关**（+0.7ms 两边一致），它是一笔固定渲染成本，不随 AI 数量增长而放大。
2. 按 60fps（16.7ms）预算算，**约 4.3%**；即便战斗时整帧 3.6ms（约 278fps），离掉帧还很远。
3. 真正的代价是 **draw call 从 569 涨到 1381（2.4 倍，+812）**——草地是 623 个 4×4 米分块，每块一次合批绘制。想要更低，把 `ChunkSize` 从 4 米调大（8 米大约能砍掉一半 draw call），代价是视锥剔除变粗。本轮没改。
4. **构建成本**：一片草地首次生成 **830 ms / 分配 40.7 MB**（19020 簇／623 块／40 个禁草区）。这是进关一次性的，不是每帧成本。

## 验证

- 探针 `[MeadowBench] SUMMARY pass=19 fail=0`，进程退出 0，stderr **0 字节**。
- 五份真实存档跑前＝跑后（`meta B83F…` / `ability_cards D1FA…` / `settings 7C5C…` / `loadout 4E84…` / `rune 70F0…`）。
- 真渲染截图：`static_grass.png`、`combat_grass.png`（同目录）。
- 原始数据：`Logs/evidence/grass-production/benchmark.json`；运行日志 `Logs/validation/grass-production-bench.log`。

## 边界（没做的）

- 没有做**长局**（12 分钟生存）帧率趋势，也没有做满场敌人（当前 12 只）压力上限。
- 没有测分块合并（`ChunkSize`）后的 draw call 收益，只是给出建议。
- 没有做树叶／树模型的性能分解（本轮只切草地）。
- 战斗组的敌人是脚本放置的 12 只 Chaser，不是 Survival 模式真实的波次生成器。
