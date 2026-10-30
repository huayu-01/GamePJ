# 046实施与验证材料

2026-10-28。当前规则见[实施记录](../../../docs/reviews/20261028/balance-046-implementation.md)。父目录保留首次评估的历史模型，本目录单独记录后续实施，避免覆盖基线。

- `before-manifest.json`与`before/`：本会话开始实施前的相关文件与SHA256；备份统一用.txt后缀，避免被C#编译器扫描。
- `after-manifest.json`、`changes.diff`：实际改动的来源指纹和可审阅差异，保留原工作区已有修改。
- `weapon-evaluation-matrix.csv`：六武器40个距离/目标组，含动作、暴露和输出归属记录项。全部待实战采样，没有把空数据当作已测试。
- `evidence.json`：本轮构建/引擎脚本记录、通过数、限制及原始失败证据路径。

复验入口：

```powershell
Set-Location -LiteralPath 'F:\Godot项目\项目a'
& ./_tools/verify_survival_augments.ps1
& ./_tools/verify_survival_integration.ps1 -SkipBuild -Only content,localization,enemy_actions,reward_rules,snapshot,survival
& ./_tools/verify_elite_traits.ps1 -SkipBuild -SkipImport -Only BalanceTuning,Codex
```

这些入口都使用共享锁、隔离用户目录与真实存档指纹检查；顺序运行，不并发启动多组Godot。旧四路线制作源已停用，不应作为复验或新增内容工具运行。
