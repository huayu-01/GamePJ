# 046 可复算评估材料

日期2026-10-28。本目录根文件保留首次评估基线：当时未启动Godot，表格均为静态提取或注明前提的模型结果。后续用户裁决、实际改动及引擎验证单独保存于[implementation目录](implementation/README.md)，不覆盖首次分析的原始数据。完整12分钟帧时和玩家分层体验仍未实测。

入口：[评估报告](../../docs/reviews/20261028/balance-046.md)。

复算（PowerShell，真实工程工作目录）：

```powershell
Set-Location -LiteralPath 'F:\Godot项目\项目a'
python outputs/balance-046-20261028/evaluate_046.py
```

脚本只读生产来源和原验收工作簿，输出限定本目录。需Python标准库，无第三方依赖。重跑会刷新本目录快照，若要保留某次评审基线请先复制本目录；不运行旧的会覆盖正式文档/资源的制作脚本。

| 文件 | 用途与口径 |
|---|---|
| evaluate_046.py | 可编辑模型与只读提取器；中文注释、英文标识 |
| summary.json | 证据等级、假设、关键结果、验收原文和工作簿指纹 |
| source-manifest.json | 本次880份来源SHA256；当前快照不等于Git提交 |
| source-snapshot.json | 当前显式资源字段及行号；并非Godot反射，未做全引擎继承求值 |
| enemy-speeds.csv | 21个非全局原型；普通和假设附加常规精英倍率的速度；专用技能护卫不据此再次附加精英 |
| player-speeds.csv | A/B、快速0/3/5级、行军开关、走/跑/攻击/浮空维持；稳态主动输入速度 |
| approach-times.csv | 15m同向后方接近至统一2m比较圈；普通/精英/迅捷；“不会追上”非全场无威胁 |
| phase-model.csv | 720秒正式阶段参数和预算积分；非实际投放或经验 |
| durability.csv | 无其他减伤/格挡/特性、固定单击的逐击护甲溢出与近似EHP |
| combat-scenarios.csv | 六武器三个可控构筑的防御前每击期望；协击/脉冲是相应路线的独立假设，不等于DPS或同时持有 |
| pursuit-surrogate.csv | 60/120Hz点质量反例，4轨迹×2速度档；无RVO/地形/攻击/伤害，非游戏实测 |

原始CSV使用UTF-8 BOM供Excel查看。本目录含.gdignore，避免Godot把分析CSV当作本地化资源导入。
