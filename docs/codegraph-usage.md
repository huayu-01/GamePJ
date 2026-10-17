# CodeGraph 用法（给 AI 代理看的）

> **一句话**：这是本机的**代码结构索引器**，能**用自然语言任务描述换到"相关文件 + 行号 + 代码"** ——
> 比自己 `grep` + `read` 一个个翻**快得多**。**DSH 主代理和它的子代理都该先用它**。

---

## 1. 是什么

```
codegraph: C:\Users\huayu\AppData\Local\codegraph\current\bin\codegraph.cmd
版本:      0.9.3
自述:      Code intelligence and knowledge graph for any codebase
索引位置:  <项目根>\.codegraph\codegraph.db（本机 10MB，已 gitignore）
```

**本项目的索引现状**（2026-10-17 实测）：

```
Project: F:\Godot项目\项目a
Files: 206   Nodes: 5,840   Edges: 11,956
Nodes by Kind: method 1,703 / property 1,650 / field 1,036 / import 795 / class 234 / file 206
```

⚠️ **索引只覆盖 C# / Python 等"代码文件"** —— **不含 GDScript**（美术特效那些 `.gd` 不在索引里）。
⚠️ **索引会过期**（新加的文件不在里面）→ 需要时跑一次 `codegraph index`（或增量 `sync`）。

---

## 2. 五条命令（**按实用性排序**）

### ⭐ ① `codegraph context "<任务描述>"` —— **首选**

**它会输出 markdown**：Entry Points（类/枚举）→ Related Symbols（**每个方法的文件名+行号**）→ **直接贴出关键代码**。

```powershell
Set-Location 'F:\Godot项目\项目a'
$cg = 'C:\Users\huayu\AppData\Local\codegraph\current\bin\codegraph.cmd'
& $cg context "技能指示器 SkillIndicator 的四种样式"
```

**实测输出**（7376 字符）：

```
## Code Context
**Query:** 技能指示器 SkillIndicator 的四种样式
### Entry Points
- SkillIndicator (class) - Scripts/Combat/SkillIndicator.cs:56
- IndicatorStyle (enum) - Scripts/Combat/SkillIndicator.cs:59
### Related Symbols
- Scripts/Combat/SkillIndicator.cs: _Ready:183, ShowCurvePath:233, ShowAreaPoint:245, ...
### Code
#### SkillIndicator (Scripts/Combat/SkillIndicator.cs:56)
```csharp
public partial class SkillIndicator : Node3D { ... }
```
```

**用法建议**：**接到任何"要改某块代码"的任务时，先跑一次 `context`**，能省掉 5-10 次 `grep`/`read`。

### ② `codegraph query "<符号名>"` —— 搜符号

```powershell
& $cg query "SkillIndicator"
```

**输出**：`class / file / method` + **文件路径:行号** + 相似度排序。**适合"我知道名字，要找它在哪、有哪些成员"**。

### ③ `codegraph files` —— 看项目结构

```powershell
& $cg files
```

**输出**：目录树 + **每个文件的符号数**。**适合"我不知道这个功能在哪个目录"**。

### ④ `codegraph status` —— 看索引健康

```powershell
& $cg status
```

**输出**：文件/节点/边数 + 按类型分布 + DB 大小。**低成本**，可以在每批交付后跑一次。

⚠️ **必须在项目目录里跑**（`F:\Godot项目\项目a`）；在父目录跑会报 `Not initialized`。

### ⑤ `codegraph affected [files...]` —— 改动影响哪些测试

```powershell
& $cg affected Scripts/Combat/SkillIndicator.cs
```

**输出**：**受影响的测试文件**。改完代码想跑验证时有用。

---

## 3. 其他命令（知道有就行）

| 命令 | 用途 |
|---|---|
| `init` | 首次初始化（**本项目已初始化，不要再跑**） |
| `index [--force]` | 全量重建索引（**新加了很多文件才需要**，别每批都跑） |
| `sync` | 增量同步（**加了几十行就够**） |
| `unlock` | 清掉卡住的锁文件（**索引卡住时**） |
| `serve` | **起 MCP server 给 AI 用**（⚠️ 需要客户端支持挂 MCP；DSH 这边没挂） |
| `install` / `uninstall` | 把 MCP 装进 Claude Code / Cursor / **Codex CLI** / opencode / Hermes Agent |

---

## 4. 给子代理的用法（**派工时请把这条写进 prompt**）

```
结构查询先用 CodeGraph，再退到 grep：
  Set-Location 'F:\Godot项目\项目a'
  $cg = 'C:\Users\huayu\AppData\Local\codegraph\current\bin\codegraph.cmd'
  & $cg context "<你要做的事，用自然语言描述>"
  & $cg query "<符号名>"
然后才用 grep/read 补细节。
⚠️ 索引不含 GDScript（美术特效那些 .gd 搜不到，用 grep）。
```

**⚠️ 不要 `codegraph init`**（已初始化）· **不要每批 `index --force`**（慢；除非新加了很多文件）· **`status` 可以随便跑**（低成本）。

---

## 5. 与交接单的关系

`docs/DEEPSEEK_HANDOFF.md` 第 73 行原文：

> **「结构查询优先 CodeGraph context→一次 explore，已初始化，不再让用户 init；字面文本再 rg。每批低成本 `codegraph index --force`，不能阻塞计划。」**

⚠️ **那条"每批 `index --force`"我不建议照做** —— `index --force` 是全量重建，**对 206 文件的索引要跑一会儿**。
**更好的是**：**新加文件多就 `sync`（增量）**，**平时只跑 `status` 看健康**。

---

## 6. 实测记录

| 命令 | 耗时感 | 输出 |
|---|---|---|
| `status` | 快 | 18 行（文件/节点/边/按类型分布） |
| `files` | 快 | 目录树 + 符号数 |
| `query "SkillIndicator"` | 快 | 6 条命中（class/file/method + 行号） |
| `context "技能指示器..."` | 中（几秒） | **7376 字符**（入口点 + 相关符号行号 + 代码） |
