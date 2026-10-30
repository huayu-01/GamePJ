# 066/067与成长计算材料

只读计算，不启动引擎、不写生产参数、验收工作簿或存档。报告：[数值复核](../../docs/reviews/20261029/balance-066-067-growth.md)。

在真实工程工作目录运行：

```powershell
Set-Location -LiteralPath 'F:\Godot项目\项目a'
python outputs/balance-066-067-20261029/calculate.py
```

Python标准库即可。会刷新本目录计算表和来源快照；主会话并行实现时，快照保存本次读取到的来源，需按SHA256区分。脚本固定原6米/.5秒对照与已授权12米/.3秒目标，不反写配置。

| 文件 | 口径 |
|---|---|
| phase-supply.csv | 720秒阶段、当前实际行为成本和敌种权重、三次强敌分配 |
| elite-phase-detail.csv | 不同预算兑现/精英附费假设的各阶段生成与精英期望参考 |
| income-scenarios.csv | 25/50/100%预算兑现、0/2附费、50/80/100%拾取组合；非实测或硬上界 |
| economy-costs.csv | 当前四卡解锁和逐级消费；容量扩充另按30+20×等级计算 |
| dash-model.csv | 速度、帧位移、0.35秒事件时窗、跟随滞后与三层后撤情景 |
| growth-scenarios.csv | 本人击杀、Boss已含于K、巨化开关、巨人/雷印/死铠独立配装比较 |
| growth-acquisition-timing.csv | 取得时间对剩余供给和成长的连续参考，不把期望杀数取整当作精确成长期望 |
| source-snapshot.json | 实际读取到的资源/源码文本与SHA256、只读验收C75/D75/C76/D76原文 |
| calculation-summary.json | 本次脚本标准输出摘要，可由命令重定向刷新 |

CSV采用UTF-8 BOM供Excel查看；目录含.gdignore，防止Godot将分析表误导入为本地化。所有收益与成长浮点为期望/连续参考，真实单次发放仍按整数门槛和实际拾取结算。
