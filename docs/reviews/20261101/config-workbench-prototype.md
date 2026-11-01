# 配置工作台 0.4：平铺编辑 + 数组结构编辑

2026-11-01。按“先做平铺原型，再接编辑”的路线落地；`.tres` 仍是唯一数据源。

## 已实现

- 新插件 `addons/config_workbench/`，已加入 `project.godot` 的 `editor_plugins`。
- 编辑器主页面「配置工作台」（`_has_main_screen()`，和 ResourceTable 一样是顶部页签，不是侧边 Dock）：
  - 顶部栏：放大/变色的标题、编辑徽标、表筛选、搜索、刷新、保存、还原；
  - 自适应：左侧固定 20%，右侧 80%；上下铺满编辑器主页面；
  - 左侧按表筛选：技能 / 增幅 / 敌人 / 角色 / 武器 / Boss / 效果 / 符文 / 节点 / AI；
  - 顶部搜索 Id、名称、路径；
  - 在 FileSystem 中选中 `.tres` 时自动打开；
  - 右侧全部字段平铺，不使用折叠树。
- 递归展开：
  - `RewardConfig.Effects` → `AbilityReplacementRewardEffect` → `TargetAbility`（外部 AbilityConfig）→ `Params` → `ParamEntry`；
  - 每层嵌套资源有边框卡片 + 左侧缩进，直接看出层级；
  - 外部资源显示“外部资源 + 打开”，避免误改共享文件；
  - 递归上限 10 层，带循环检测。
- “打开”使用 deferred 调用，避免按钮 `pressed` 信号发射期间销毁表单导致
  `freed while signal is being emitted`。
- 编辑：
  - bool / int / float / string / enum / `PackedStringArray` 标量可编辑；
  - `ParamEntry` 的 Key、值、说明可编辑；**值类型只读显示**（Float/Int/Bool/String/String[]），
    避免在编辑器里把消费方约定的 Variant 类型改坏；
  - 嵌套 Effect / TargetAbility / 内嵌子资源字段可编辑；
  - `Effects` 可“添加效果…”（按 RewardEffects 脚本类型）和删除；
  - `Params / AttackParams / HandlerParams` 可“+ 添加参数”和逐条删除；
  - 保存按 `_dirty` 里的真实 Resource 写回，避免“路径重新 load 出另一份默认值”；
  - 未保存计数、保存、还原；Ctrl+S / Ctrl+R。
- `ParamEntry` 专用卡：
  - `Key` / `Value` / `Value` 类型（只读）/ `说明` 直接展示；
  - `说明` 为空或命中 `★|文档|第…轮|Jxx|验收|修复|用户|当前` 时标黄，提示改成用途说明。
- 无编辑器预览：`Scenes/Debug/_ConfigWorkbenchPreview.tscn`，加载真实 `reward_1351` 和 `skl_a3_w1_1` 并截图。预览脚本**不是 `@tool`**，并带 `Engine.is_editor_hint()` 保护。

## 证据

- 配置工作台嵌套效果（1351）：
  `Logs/evidence/config-workbench/reward-1351-flat.png`
- 技能 Params / ParamEntry 说明：
  `Logs/evidence/config-workbench/skill-params-flat.png`
- GDScript 语法：`--check-only` 通过。
- 无头编辑器加载插件：无工作台脚本错误；MCP 端口冲突与现有插件重复加载是环境噪声。
- 预览场景已改为非 `@tool`；上次编辑器闪退的根因是预览脚本在编辑器里执行 `get_tree().quit()`，现已移除。
- 已接顶部主页面、20/80 分栏和全高布局；左侧宽度按主页面宽度实时重算。
- 保存探针：临时资源 `Weight 1.0→1.23` 保存后磁盘回读通过（测试夹具已删除）。
- 数组结构探针：`Effects 1→2`、`Params 17→18`、再删除回 17；只在内存中验证，未写生产资源。

## 当前限制

- 外部资源保存会明确写回该外部 `.tres`；当前没有“保存前确认”弹窗。
- `ParamEntry.Note` 已清洗 65 条旧说明：172 条 ParamEntry Note 中，旧变更/文档口吻的
  `★|文档|第…轮|Jxx|验收|修复|用户|当前` 命中数从 65 降到 **0**；全部改为“用途 + 单位/范围/例外”。
- 常用字段已加单位/范围提示（MaxHealth 点、Cooldown 秒、MaxStacks 0=不限等）；未覆盖全部字段。
- `Skills` / `ConvertedRewards` 等外部资源数组仍只显示“打开”，未做结构增删。
- 未接撤销（Ctrl+Z）；目前是保存/还原。
- 未做批量编辑；ResourceTable 仍可用于多行数值对比。

## 下一步建议

1. 接撤销、外部资源保存确认。
2. 继续补冷门字段的单位/范围与字段级说明。
3. 用工作台逐项复看 172 条 Note 的语义，而不是只看格式。
