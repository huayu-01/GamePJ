# 日志与验证输出

项目日志统一入口为[Logs目录](../Logs/README.md)，该目录用`.gdignore`排除Godot资源扫描。

| 内容 | 当前输出 |
|---|---|
| 编辑器／开发工程GameLog | `res://Logs/runtime/run_年月日_时分秒_毫秒.log` |
| 导出游戏，或开发目录不可写 | `user://logs/run_年月日_时分秒_毫秒.log` |
| PowerShell验证、构建stdout／stderr及指纹 | `Logs/validation/` |
| 文档引用的完整验收记录与比较基线 | `Logs/evidence/` |
| 本轮收拢的旧根目录日志 | `Logs/archive/root-20261024/` |
| 清理清单、校验和改动前备份 | `Logs/maintenance/` |

当前原始截图、模型验证JSON及制作源保留原路径。运行日志和临时输出本地忽略，完整证据保留版本管理；不要根据“旧”或“失败”直接删除仍有诊断用途的唯一证据。

## 加日志

```csharp
GameLog.Write("Flow", $"进入关卡：{stageId}");
```

同时打印到控制台并写入本次GameLog。格式为`时分秒 [分类] 消息`；分类包括Flow、Node、Reward、Spawn、Combat、Config、UI、Audio。战斗事实由`GameplayEventBus`订阅记录，流程节点显式调用。命中日志中的括号生命是受伤后的值。

开发工具通过`_tools/LogPaths.ps1`创建输出目录，新的验证入口沿用它。不得向工程根目录或`docs/`写入新的日志；文档使用相对链接引用`Logs/evidence/`的报告。

## 整理与检查

```powershell
python _tools/maintain_project_files.py
python _tools/maintain_project_files.py --check
```

默认只预览；`--apply`按清单迁移根目录日志和已分类的历史文档。唯一日志移到独立目录，逐字相同的重复输出只保留一份，重复映射和SHA256记录在maintenance清单；文档链接和验证脚本路径同步迁移。它不删除模型、截图、源文件、`.uid`／`.import`或玩家存档，不跟随旧验证工程的目录链接进行递归删除。

日志等级、采样与保留策略可后续单独设计；本轮不改变战斗事件记录规则。

## 2026-10-24整理结果

根目录2655份日志与docs内235份日志，共2890份原始输出均已核对SHA256；唯一内容保留到新位置，删除1072份逐字重复副本。另收拢185份历史存档指纹JSON。70份历史文档归档、5份重复跳转页删除，docs顶层Markdown由124份降到51份，新增内容手册和解耦计划计入其中。

双构建0警告0错误，特化221项及Main八指标／索引核验通过，无新增Main失败，原玩家五存档一致；161份Markdown的文件链接检查无断链，23份PowerShell语法检查通过。开发运行日志实际写入Logs/runtime，验证及引擎输出进入Logs/validation。[运行证据](../Logs/evidence/gameplay-fixes-evidence-20261024_143657.log)、[整理摘要](../Logs/evidence/project-maintenance-20261024.json)。详细迁移映射与改动前备份为本机maintenance数据，不要求随正式游戏导出。
