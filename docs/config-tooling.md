# 批量编辑数据表的工具调研（`docs/config-tooling.md`）

> 需求原话：**"最后有一个编辑器可以在这些表项目进行快速的编辑，以往的做法是 excel 表，但目前项目是 tres，你也可以帮我看看是否有别的现成工具软件满足我这个需求。"**
>
> 本文只回答**"用什么工具"**。**表的字段怎么设计**（武器表该有哪些列）由 [config-schema.md](config-schema.md) 负责，本文不重复。
>
> **调研方式**：`web_search` + 逐页 `web_fetch`。每条标注验证状态：
>
> | 标记 | 含义 |
> |---|---|
> | ✅ 已验证可访问 | 我**实际打开了**该页面并读到了内容 |
> | ⚠️ 仅搜索结果出现，未验证 | 只在搜索摘要里见到，没打开官方页 |
> | ❌ 未能验证 | 明确找了但没找到 / 不存在 |
>
> **价格与版本核对日期：2026-10-11。** 价格会变，采购前请回原文再确认一次。

---

## 零、先看清现状（这决定了后面所有选项的前提）

我读了 `F:\Godot项目\项目a` 的实际文件，有两处和需求描述**不一致**，必须先说：

| 需求描述 | 实际情况 |
|---|---|
| "如 `Config/ConfigIndex.tres`、`Stages/*.tres`" | **`Stages/` 目录不存在。**全项目 `Assets/`、`Scenes/` 之外的 `.tres` **只有一个**：`Config/ConfigIndex.tres` |
| 关卡分文件存 | 关卡是 `ConfigIndex.tres` 里的 **`sub_resource` 块**，由 `[Export] public StageDefinition[] Stages` 承载（见 `Config/ConfigIndex.tres` 的 `[sub_resource type="Resource" id="Stage_1"]`） |

**现在的真实形态**：一个 C# 类 `ConfigIndexData`（**约 100 个 `[Export]` 属性**，全部是扁平的 `PlayerMaxHealth` / `PistolDamage` / `EnemyHealthMultiplier` 这种"一属性一值"）+ 一个 `ConfigIndex.tres` + 一个嵌套的 `StageDefinition[]`。

**关键的好消息 —— "读不到就用默认值"的语义已经在代码里了**：

```csharp
// Scripts/Config/ConfigIndexData.cs:31
[GlobalClass]
public partial class ConfigIndexData : Resource
{
    [Export] public float PlayerMaxHealth { get; set; } = 160f;   // ← C# 属性初始化器 = 默认值
    [Export] public float PistolDamage { get; set; } = 24f;
```

也就是说：**只要新表也用 `[GlobalClass]` + `[Export]` + 属性初始化器写，回退语义自动成立**，不需要额外机制。这一点直接决定了推荐方案（见第五节）。

### ⚠️ 最重要的前提结论

**目前这个项目"没有表"，只有"一张宽表"。** 任何"按类型分表批量编辑"的工具都要求一个前提：

> **一类实体 = 一个 C# Resource 类，一行 = 一个 `.tres` 文件（放在同一个文件夹里）。**

今天 `PistolDamage` / `RifleDamage` / `SniperDamage` 是 `ConfigIndexData` 上的三个**列**，不是三**行**。**在把"武器"从三列拆成一张有 N 行的武器表之前，本文推荐的所有工具都无事可做** —— 因为"表"还不存在。这个拆分是**工具的入场券**，不是工具的功能。

---

## 一、路线 A：Godot 插件（在编辑器内编辑）—— **推荐路线**

### A1. Edit Resources as Table —— ⭐ 本次调研的最优解

| 项 | 内容 |
|---|---|
| **插件名** | Edit Resources as Table（Asset Library 上叫 **"Edit Resources as Table 2"**；`plugin.cfg` 里叫 "Edit Resources as Table"） |
| **作者** | Don Tnowe（`don-tnowe`） |
| **链接** | Asset Library：<https://godotengine.org/asset-library/asset/1479> · 源码：<https://github.com/don-tnowe/godot-resources-as-sheets-plugin> |
| **验证状态** | ✅ **已验证可访问**（Asset Library 页 + GitHub README + `plugin.cfg` + 提交历史 + issue 记录全部读过原文） |
| **支持 Godot 版本** | **Godot 4**（Asset Library 标 `4.0` 起；另有 `godot-3` 分支）。⚠️ **未显式声明支持 4.7，见下方风险** |
| **最近更新** | **`master` 分支 HEAD = 2026-01-27**（社区 PR 到 2026-02-10 仍开着）。说明**仍在维护** |
| **许可** | **MIT** |
| **语言 / 规模** | GDScript；★1028 stars / 43 forks；35 个 open issue |
| **版本号** | ⚠️ **对不上**：Asset Library 列表写 `2.17.4`，但仓库 `addons/resources_spreadsheet_view/plugin.cfg` 里写的是 `version="2.7"`。**别信版本号，看提交日期** |

**它能做什么**（原文 feature 列表）：

- **把一个文件夹里的所有 Resource 当成一张表来编辑**，**一行 = 一个 Resource** —— 这正是"按类型分表"的形状
- 键盘直接打字改 Text / Number / Color / Bool；Resource / Array / Enum 通过 Inspector 改
- **一列里多选单元格（Shift/Ctrl+Click）然后一次改完** —— 这就是"批量编辑"
- 数字列的**批量乘 / 加**（"所有武器伤害 ×1.1"一步到位）
- **CSV 导入 / 导出 / 直接编辑**（强类型列）
- **把一个 Resource 列、或 Resource 数组列，单独打开成另一张表** ← **这一条正好治 `StageDefinition[] Stages` 这种嵌套**
- 复制粘贴单元格、`Ctrl/Cmd+D` 复制行、`Ctrl/Cmd+R` 重命名资源
- 按列排序；按 **Resource 类 / 脚本**筛选行；按 GDScript 表达式筛选 / 批量改值
- **完整的 Undo / Redo**

**它不能做什么 / 已知坑**（全部来自 issue 原文，不是我的推测）：

| 限制 | 证据 |
|---|---|
| **CSV 导入无法处理数组和字典** | issue #61（open，2024-05 至今）、issue #72 维护者自述 "some datatypes can't be imported at all, including arrays and dicts" |
| **嵌套 Resource 不会被展平** | issue #73（open）：想让表头变成 `MaxHP.Value \| MaxHP.Growth`，**没实现** |
| **CSV 导入"新建行"这条路径不稳** | issue #72 里 C# 用户的实测："it breaks during creation of the first new object in the spreadsheet. If I add multiple new rows - only one gets added"、`Another resource is loaded from path ... (possible cyclic resource inclusion)`、`Out of bounds get index '2'` |
| 类型为空的导出属性会让表格视图报错 | issue #111（open，维护者自己提的）：`@export_node_path` / `@export_dir` |
| 字符串单元格编辑手感差（不是标准 `TextEdit`） | issue #116（open，2025-11，**Godot 4.5.1** 上实测） |
| 首次安装 / 新项目偶尔整张表空白 | issue #113 维护者原话："sometimes the plugin just does this for new projects, when first installed" —— **重开一次项目通常就好了，别急着重装** |

#### 🔴 C# 兼容性 —— 这是本项目最关心的一条，**答案是"可以"**

我特地查了 C# 相关的 issue，结论是有**一手证据**的：

- **issue #113「C# support」**（2025-09-07）：提出者说表是空白的，维护者回 "Yep, it worked then"（意思是以前 C# 是能跑的），最后**提出者自己复现：重开项目后 "it is working perfectly fine"**，并道歉关掉 issue。→ **不是 C# 不支持，是首装 bug。**
- **issue #72**（2024-12）：一位 C# 用户拿**货真价实的 C# Resource 类**（含 `public enum TestEnum`、`[Export] Texture2D`、`[Export] bool`）测导入导出，在报告 bug 的同时明确写了一句：

  > **"It works with C# Scripts."**

  维护者随后加了 **"Use Script"** 导入选项，让 CSV 导入能挂到**你已有的脚本**上，而不是新建 `.gd`。

**⚠️ 但 issue #72 至今 open**，剩余待办里还挂着 "Verify how it works with polymorphism"。所以准确表述是：**编辑 C# 资源、C# 类作为列来源 —— 已验证可用；CSV → 新建 C# 资源的复杂场景 —— 有毛刺。**

#### ⚠️ Godot 4.7 兼容性风险（必读）

我**没有找到任何 4.7 上的实测报告**。有据可查的时间线是：

- 2025-05-06（`Godot-4` 分支）：提交信息 **"Auto-update to Godot 4.4"**
- 2025-11-10（issue #116）：用户在 **Godot 4.5.1** 上正常使用（只是抱怨字符串编辑手感）
- 2026-01-27（`master`）：仍在提交功能修复，但**没有版本号 bump**

→ **结论：不能声称它支持 4.7。必须按下面的"第 2 步冒烟测试"亲自验一遍再用。**好消息是它是**纯 GDScript、不改引擎、纯编辑器插件**，这类插件跨小版本失效的概率低，而且**卸载 = 删一个 `addons/` 目录**，零残留风险。

---

### A2. ExcelTableImporter —— 唯一明确列出 4.7.2 的"Excel 导表"插件

| 项 | 内容 |
|---|---|
| **作者** | wolflanlan |
| **链接** | <https://store.godotengine.org/asset/wolflanlan/excel-table-importer/> |
| **验证状态** | ✅ **已验证可访问**（Asset Store 页原文） |
| **支持 Godot 版本** | 最低 4.0；**兼容列表里明确勾选了 `Godot 4.7.2`** ✅ |
| **最近更新** | **2026-09-11**（v0.3.0，33.97 KB）—— 很新 |
| **许可** | **MIT** |
| **源码仓库** | ⚠️ **商店页没有给出仓库链接**（不可审计，这是扣分项） |

**能做**：把 `.xlsx` / `.csv` **一键**变成"强类型 Resource + 自动生成的加载代码"，输出 `.gd`（Resource 类 + loader）、`.tres`、`.json` 到 `res://tables`；`.xlsx` 解析**零外部依赖**（用 Godot 自带 `ZIPReader` + `XMLParser`）；多 sheet、quote 转义、UTF-8 带/不带 BOM 都处理；编辑器里有表预览 dock、有交互式导出对话框（可改类名、选输出格式和目录）。

**不能做 / 对本项目的硬伤**：

1. **它生成的是 `.gd`（GDScript）Resource 类。**本项目是 **C# mono**，配置类都是 `[GlobalClass] public partial class ... : Resource`。接进来会在 `res://tables` 里长出一套**平行的 GDScript 配置类型**，C# 侧只能用 `Resource.Get("字段名")` 弱类型读取，**丢掉 `ConfigIndex.Settings.PistolDamage` 这种编译期检查**。商店页只说"可以改类名"，**没有说能复用你已有的 C# 类** → 想用必须实测。
2. **V1 只做全量重导出，没有增量 diff**（官方 Limitations 自述）。整表重导 → 所有 `.tres` 时间戳/内容全变 → **git diff 变噪音**。
3. 面向"**从 xlsx 生成**"，不是"**编辑已有的 `.tres`**"——和本项目现状方向相反。

> **判断**：如果本项目是 GDScript，这个插件会是很好的选择（4.7.2 兼容 + MIT + 零依赖 + 很新）。**因为项目是 C#，它降级为"备选"，而不是首选。**

---

### A3. CSV Data Importer（Haoyu Qiu / `timothyqiu`）

| 项 | 内容 |
|---|---|
| **作者** | Haoyu Qiu（Godot 文档维护者 `timothyqiu`，可信度高） |
| **链接** | <https://store.godotengine.org/asset/timothyqiu/csv-data-importer/> · 源码 <https://github.com/timothyqiu/godot-csv-data-importer> |
| **验证状态** | ✅ **已验证可访问**（Asset Store 页原文） |
| **支持 Godot 版本** | 最低 4.1；**兼容列表明确勾选 `Godot 4.7.2`** ✅ |
| **最近更新** | **2026-06-23**（v2.1，3.99 KB） |
| **许可** | **MIT**，**有公开源码** ✅ |

**能做**：把 `.csv` / `.tsv` 当**原生资源**导入（Import dock 里 "Import As → CSV Data"）。导入选项齐：Delimiter（逗号/Tab）、**Headers**（首行当表头 → 每行变 Dictionary）、**Detect Numbers**（字符串自动转 int/float）、Force Float、**Detect Booleans**。

```gdscript
var data = preload("res://example.csv")
print(data.records)   # 数组
```

**不能做（关键区别）**：**它是"一个 CSV 文件 = 一个 Resource"，不是"一行 = 一个 `.tres`"。** 它给不了你"每个武器一个独立 `.tres` 文件"的形态。它适合的架构是**"整张表在运行时读成字典数组"**，而不是本项目现在这种"每个实体一个资源文件、由编辑器 Inspector 精修"的形态。

> **价值**：如果以后想让**运行时**直接从 CSV/TSV 读数值（绕过 `.tres`），这是最干净、最可信、又明确支持 4.7.2 的方案。**但它是"运行时数据加载"，不是"批量编辑器"**，不解决你提的需求。

---

### A4. VortarisCSV（ASDF Studio）

| 项 | 内容 |
|---|---|
| **链接** | <https://store.godotengine.org/asset/asdf-studio/vortaris-csv/> · 源码 <https://github.com/Vortaris/VortarisCSV> |
| **验证状态** | ✅ **已验证可访问**（Asset Store 页原文） |
| **支持 Godot 版本** | **最低 Godot 4.7**（GDExtension / C++），兼容列表含 `4.7.2` ✅ |
| **最近更新** | **2026-08-21**（v0.4.1，4.30 MB） |
| **许可** | **MIT**，有公开源码 |

**能做**：单遍 C++ 状态机解析（引号、转义引号、多行字段、CRLF/LF/CR、**UTF-8 BOM**、注释行、可配分隔符/引号、严格/宽松行、行列级报错）；**UE-DataTable 风格的反射绑定** —— 把 CSV 行直接绑到"你自己的行类"上，列名自动映射到 `@export` 属性并按类型转换（int/float/bool/String/枚举/Vector2-4/Color/Rect2/数组/JSON 字典）；编辑器导入：`.csv`/`.tsv` 拖进项目就导入成 `VCSVDataTable` 资源（`.tres`）；另有排序/搜索/筛选/去重/聚合/外键 join/**GBK/GB2312 编码**；**中英双语文档**。预编译二进制含 **Windows / Linux / macOS**（debug + release）。

**对本项目的硬伤**：

1. 绑定目标是 **"GDScript `extends Resource` 或 C++ `ClassDB` 类"** —— **C# 行类不在列表里**。
2. 它是 **GDExtension**，会往仓库里塞**平台二进制**（4.30 MB）；每个平台一份，git 体积和"换引擎小版本要不要重编"都是新负担。
3. ⚠️ 商店页描述里写 "Current: 0.1.2" 而版本选择器最高是 `v0.4.1` —— **文档自身不一致**，说明项目还年轻。

> 只作"知道有这个东西"记录。**对 C# 项目不推荐**。

---

### A5. ResPort

| 项 | 内容 |
|---|---|
| **作者** | WitchHat07（Asset Library 提交者 `Elixir99`） |
| **链接** | <https://godotengine.org/asset-library/asset/4734> · 源码 <https://github.com/WitchHat07/ResPort> |
| **验证状态** | ✅ Asset Library 页已验证可访问；⚠️ GitHub README 我请求两次都 404（拿不到细节） |
| **支持 Godot 版本** | Asset Library 标注 `4.5` |
| **最近更新** | **2026-02-04** |
| **许可** | **MIT** |

**自述**："A plugin for importing and exporting Resources via CSV... enables easy bulk editing for beginners and explicit, predictable data workflows for advanced users, with support for **creating new Resources directly from CSV imports**."

**判断**：功能定位（CSV ↔ Resource 双向、支持从 CSV 新建 Resource）**非常对味**，MIT、还很新。**但版本是 `0.1`**，且我拿不到 README 细节、看不到 issue 历史。→ **值得放进候选清单实测，但现在不能推荐它当主力。**

---

### A6. Gnumaru's Static Data Importer

| 项 | 内容 |
|---|---|
| **链接** | <https://godotengine.org/asset-library/asset/3048> · 源码 <https://github.com/Gnumaru/GnumarusStaticDataImporter> |
| **验证状态** | ✅ Asset Library 页已验证可访问 |
| **支持 Godot 版本** | `4.0` |
| **最近更新** | **2024-06-07**（v1.0）—— **较旧** |
| **许可** | **MIT** |

**能做**（这一条很有意思）：把 **yaml / toml / hjson / xml / csv / xlsx / ods / odb / sqlite** 都像 JSON 资源一样 `load()`，字符串用 `str_to_var` 处理。**而且它是"导入期"插件：导出正式版时可以直接把插件删掉，数据不依赖它。**

**硬伤**：

1. **作者自己在描述开头用大写警告**："This add-on has **system prerequisites and does not work without them**" —— 要装外部依赖。
2. 基于 `str_to_var`（GDScript 语义），**C# 侧不适用**。
3. **2024-06 之后没更新**，4.7 兼容性存疑。

> **注意**：它是本文里**唯一一个能直接读 `sqlite` 和 `xlsx` 的 Godot 插件**。如果以后真要上数据库，这是参考点（但方案本身不建议）。

---

### A7. CSV Parser（Shadow9876）

| 项 | 内容 |
|---|---|
| **链接** | <https://godotengine.org/asset-library/asset/4815> · 源码在 **Codeberg**：<https://codeberg.org/Shadow9876/CSV-Parser> |
| **验证状态** | ✅ Asset Library 页已验证可访问（**只读了摘要**，没读 Codeberg 仓库） |
| **支持 Godot 版本** | 标注 `4.4` |
| **最近更新** | **2026-02-23**（v0.98.0） |
| **许可** | **MIT** |
| **自述** | "Import and export CSV data and use it for more than translations **similarly to JSON**" |

**判断**：定位同 A3（CSV 当数据源），但版本 `0.98`、托管在 Codeberg、无 4.7 说明。**A3 在各方面都更强**，这个仅作备选记录。

---

### A8. Godot **自带**的 CSV 能力 —— 边界在哪

需求里问到"`FileAccess.GetCsvLine()`、`ResourceImporterCSVTranslation` 之外还有什么"。逐条回答：

| 能力 | 状态 | 说明 |
|---|---|---|
| `FileAccess.get_csv_line()` / `store_csv_line()` | ⚠️ **仅搜索结果确认**（godot-docs `class_fileaccess.rst` 的 API 列表命中；我没能逐字打开该页，官方文档站在本次抓取时只返回了导航栏） | **运行时**逐行读写 CSV 的辅助函数。它只是**解析工具**，不是导入管线 —— 你得自己写循环、自己建 Resource、自己 `ResourceSaver.Save()` |
| `ResourceImporterCSVTranslation` | ✅ 页面存在（HTTP 200），**但它是"翻译导入器"** | 把 CSV 导入成翻译表（`Translation`），**用于多语言，不是通用数据表**。不要指望它导武器表 |
| **"CSV → 任意 Resource"的通用导入器** | ❌ **Godot 本体没有** | 这恰恰是 A2/A3/A5/A6/A7 这堆插件存在的理由。**本体不给，必须靠插件或自己写脚本** |

**所以"用 Godot 自带能力搞定"这条路是走不通的** —— 自带能力只到"能解析 CSV 文本"为止，从"解析出的行"到"一个文件夹的 `.tres`"，中间那段生成逻辑得自己写（见 B3）。

---

## 二、路线 B：外部表格工具（Excel / CSV 工作流）

### B1. 本地 CSV / TSV 编辑器（Windows）

#### Modern CSV —— ✅ 已验证

| 项 | 内容 |
|---|---|
| **厂商** | Gallium Digital |
| **链接** | <https://www.moderncsv.com/>（价格页 <https://www.moderncsv.com/buy/>，免费版说明 <https://www.moderncsv.com/free-version/>） |
| **验证状态** | ✅ **已验证可访问**（价格页 + 免费版页原文） |
| **平台** | **Windows**（Installer EXE / **Portable Zip**）、macOS（DMG）、Linux（Tarball） |
| **价格**（2026-10-11 核对） | **Free = $0**；**Premium Personal = $39 一次性**；**Premium Business = $59 一次性**（10 份起递减到 $39；1–9 份 $59） |

**免费版不是试用版** —— 官方原话："My goal in designing the free version was to make it functional and not just a de facto trial"。免费版已包含**多单元格编辑**、**大文件处理（无文件大小限制）**、增删改行列、正则查找替换、排序、**分隔符自动识别**、冻结表头、筛选、去重、列联查、数据统计、透视表、JSON/XML 导入导出。

**收费版才有的**：插入单元格并右移、连接单元格、**加/更新 ID 列**。

> **对本项目**：免费版就够了。**"多单元格编辑" + "排序" + "筛选" + "列联查" + "去重" 正好覆盖批量改数值的需求。** 有 portable zip（不写注册表、可放项目外）。

#### Tablecruncher —— ✅ 已验证，**已开源免费**

| 项 | 内容 |
|---|---|
| **链接** | <https://tablecruncher.com/> · 源码 <https://github.com/Tablecruncher/tablecruncher> · 网页版 <https://app.tablecruncher.com/> |
| **验证状态** | ✅ **已验证可访问**（官网原文 + 下载链接） |
| **平台** | **Windows（v1.8，Win64 installer）**、macOS（ARM / Intel）、Linux（v1.8-beta1 x86_64） |
| **价格** | **免费 + 开源**（2025-04-25 起，作者博客《Tablecruncher is Going Open Source》） |
| **技术栈** | C++17 + FLTK；JavaScript（Duktape）当宏语言 |

**能做**：打开 >2GB / >1500 万行文件（100MB / 50 万行 <5 秒）；**JavaScript 宏**批量改单元格；**导出 JSON**（有表头 → array-of-objects）；**自动识别格式与编码**，可手动覆盖；支持的编码：**UTF-8 / UTF-16LE / UTF-16BE / Latin-1 / Windows-1252**；正则查找替换；**标记行并单独导出**；排序、移动/删除列、设首行为表头。

> **对本项目**：**免费开源 + Windows + 能导出 JSON + JS 宏**，是本条路线里性价比最高的。**宏语言是 JS** —— 想"给所有武器伤害 ×1.1"可以写 3 行宏。缺点是 FLTK 界面朴素（作者自己承认"never win any design price"）。

#### Rons Data Edit —— ✅ 已验证

| 项 | 内容 |
|---|---|
| **厂商** | Rons Place Software（加拿大） |
| **链接** | <https://www.ronsplace.ca/products/ronsdataedit> |
| **验证状态** | ✅ **已验证可访问**（产品页原文） |
| **平台** | **Windows**（"A Windows CSV editor"） |
| **价格**（2026-10-11 核对） | **Lite = 免费**，但**保存上限 2500 行**（读取不限）+ 带广告横幅；**Pro = $37.50 一次性**（无行数限制、无广告） |

**能做**：CSV/TSV/PSV/TAB 及任意分隔文本；**Auto-Detect 分隔符**；全编码支持 **UTF-7 / UTF-8 / UTF-16 / UTF-32** 并自动识别；单元格内联编辑 + 长文本/多行单元格专用编辑面板；**Toolbox / Columns Tools / Row Tools / Cell Tools**（拆分合并行列、去重、自动填充、透视）；**Page per Row** 逐行表单视图；导入支持 **JSON / XML / HTML / tokenized**，可用行键 append 或 join；导出同理，还能**直接导出到 Excel**；Cleaners（自动化编辑规则集，可复用）；完整 undo/redo。

**官方定位很关键**（原文）：

> "Excel is an excellent tool... However, when it comes to editing raw CSV or text files, **Excel can inadvertently alter data with auto-formatting and introduce errors**. Rons Data Edit, on the other hand, **treats your data as pure text**."

> **对本项目**：**"纯文本、绝不自动改数据"正是游戏数值表最需要的性质**（Excel 会把 `007` 变 `7`、把 `1E5` 变科学计数）。Lite 的 2500 行保存上限对**几百行的数值表完全不是问题**（读取不限）。**免费版即可用。**

#### CSVed / Tad —— ⚠️ 未验证

需求里点名了这两个，我**没有打开它们的官方页**，因此**不给结论、不写价格、不写链接**。按"宁缺勿假"原则：**这两条本次调研未验证，需要时请单独核实。** 上面三个（Modern CSV / Tablecruncher / Rons Data Edit）已经覆盖了同样的功能区间，且都是实地读过的。

### B2. 在线协作表格

| 工具 | 价格 / 定位 | 能否导出 CSV | Godot 集成 | 验证状态 |
|---|---|---|---|---|
| **Gridly** | **SaaS，€99/月起**（Basic/Team 档，含 3 席，加席 €29/月；14 天全功能试用）。上限：2,500 次自动更新/月、3,000 次 API 请求/月、10 万条记录/Grid、50 万条总记录 | 是（游戏本地化/内容管理定位） | ❌ **未找到任何 Godot 集成**（见第三节） | ✅ 价格页已验证可访问 <https://www.gridly.com/pricing/> |
| **Google Sheets** | 免费（个人） | 是（"下载 → CSV"） | ❌ 无 | ⚠️ **未验证** |
| **Airtable** | 未验证 | 是 | ❌ 无 | ⚠️ **未验证** |

> **Google Sheets / Excel 导出 CSV 的真实风险**（工程师提示，**本次未实测**，使用前请自己验一遍）：
> 1. **区域设置决定了分隔符** —— 中文/欧洲区 Windows 的 Excel 常用 **分号 `;`**，而插件和 Godot 都默认**逗号 `,`**。导完第一件事是 `Get-Content 表.csv -TotalCount 3` 看一眼分隔符。
> 2. **Excel 会静默改数据** —— `007` → `7`；长数字串 → 科学计数法 `1.23E+11`；日期 → 区域化格式。**这是 B1 里 Rons Data Edit 官方明确点出的问题。**
> 3. **BOM** —— UTF-8 BOM 会让幼稚的解析器把第一列列名读成 `\ufeffName`。Godot 的 CSV 相关实现（如 VortarisCSV，见 A4）明确宣称处理 BOM，但**你自己写的脚本不一定**。
> 4. **行尾** —— Excel 导出是 **CRLF**，而本项目 `assets.md` §5 明文要求 **LF**。整表 CRLF 会让 git 显示"整个文件都改了"。

### B3. 从 CSV 生成 `.tres` 的脚本方案

**结论：没有找到"拿来就能用、且适配 C# `[GlobalClass]`"的现成开源脚本。** 现有插件分两类，都不完全对口：

| 类型 | 代表 | 为什么不对口 |
|---|---|---|
| 生成**新的 GDScript** Resource 类 | ExcelTableImporter（A2）、Gnumaru（A6）、ResPort（A5，部分） | 会在 C# 项目里造出平行的 GDScript 类型体系 |
| 只负责**解析**，不负责生成 `.tres` | CSV Data Importer（A3）、CSV Parser（A7）、`FileAccess.get_csv_line()` | 你得自己写"行 → Resource → 保存" |

**自己写的话，正确的做法**（Godot 4 C# API 名，供实现时对照）：

1. **用 `EditorScript`**（编辑器里 `File → Run`），不要用 `@tool` 节点 —— 你不需要它每帧跑，只需要"我按一下，跑一次"。
   ```csharp
   // 放在 res://Tools/CsvToTres.cs 之类的编辑器脚本
   public override void _Run() { /* ... */ }
   ```
2. **读 CSV**：`FileAccess.Open(path, FileAccess.ModeFlags.Read)` + `GetCsvLine()` 循环；或自己按 `,` 切（注意带引号的字段里有逗号）。
3. **建对象**：`var def = new WeaponDef();`（C# 里 `new` 你自己的 Resource 子类，**不需要** GDScript 那套 `ClassDB`/`ResourceLoader`）
4. **写文件**：`ResourceSaver.Save(def, "res://Config/Tables/Weapons/wpn_pistol.tres")`
5. **⚠️ 校验**：`assets.md` §5 的格式规矩同样适用（`ext_resource` 在前、`type` 用基类型、UTF-8 无 BOM + LF）。

**⚠️ 更重要的一条**：`art-onboarding.md` §4 / `assets.md` §6.3 都记着这个坑 ——

> **编辑器会缓存场景/资源**，用编辑器脚本 `load()` 验证时拿到的是**改动前的副本**，必须 `ResourceLoader.CACHE_MODE_IGNORE` 强制读盘，否则"改了没生效"是假象。

写生成脚本时**输出后立刻回读校验**，这一步别省。

---

## 三、路线 C：数据库 / 专用游戏数据工具

**先说结论：这一整条路线对本项目都是"杀鸡用牛刀"，而且其中"有 Godot 集成"的部分基本不存在。**

### C1. 逐个核实"有没有 Godot 支持"

我用 GitHub 仓库检索做了**反向验证**（找不到也是结论）：

| 工具 | 定位与价格 | Godot 集成 | 验证方式 |
|---|---|---|---|
| **Gridly** | 游戏本地化 / 内容管理 SaaS，**€99/月起** | ❌ **未找到**。GitHub 搜 `gridly godot` 返回 308 条，**全是 TileMap / grid 布局之类的无关仓库，没有一个是 Gridly 集成** | ✅ 检索已验证（<https://api.github.com/search/repositories?q=gridly+godot>）；价格页已验证 |
| **Lokalise** | 本地化 SaaS | ❌ **未找到**。搜 `lokalise godot` → **0 条结果** | ✅ 检索已验证 |
| **Crowdin** | 本地化 SaaS | ❌ **未找到专用插件**。搜 `crowdin godot` → **仅 1 条**，且是一个"自己用 Crowdin 做翻译的游戏"，**不是集成插件** | ✅ 检索已验证 |
| **Baserow** | 开源 Airtable 替代，可自托管 | ⚠️ 本次**未验证** | ⚠️ 仅搜索摘要出现 |
| **NocoDB** | 开源 Airtable 替代，可自托管 | ⚠️ 本次**未验证** | ⚠️ 仅搜索摘要出现 |
| **Airtable** | SaaS 表格 | ❌ 无 | ⚠️ 未验证 |

> **补充说明**：搜到的 Godot 本地化类插件（`LocalizationSystem`、`Loxily Localization SDK`、`EthanGrahn/godot-localization-editor` 等）都是**翻译字符串管理**，和"武器表 / 技能表 / 增幅表"这种**数值配置**不是一回事，不要混淆。

### C2. SQLite + 导出脚本，值不值得？

| 环节 | 现实 |
|---|---|
| **编辑器** | **DB Browser for SQLite (DB4S)** —— ✅ **已验证可访问** <https://sqlitebrowser.org/>。**免费、开源**、**Windows**/macOS/Linux、当前 3.13.0。给数据库一个**类电子表格界面**，另有完整 SQL；**能导入导出 CSV**、SQL dump；可建表/改表/建索引/排序/搜索/画图 |
| **Godot 读 SQLite** | ⚠️ **Godot 本体不支持 SQLite**（无内建驱动）。要么找 GDExtension 的 sqlite 插件，要么走"导出成中间格式" |
| **C# 读 SQLite** | ⚠️ 本次**未验证**具体库版本（`Microsoft.Data.Sqlite` / `sqlite-net` 等）。但要注意：**Godot C# 项目要用它们需要 `e_sqlite3` 原生库**且得跟着导出平台走，**这是一个新的原生依赖**，不是"加个 NuGet 就完事" |
| **可参考的现成路径** | Gnumaru's Static Data Importer（A6）**确实能直接读 `sqlite`**，但它 2024-06 停更、且基于 GDScript `str_to_var`，**C# 项目用不了** |

**判断：不值得。** 理由：

1. **多一跳**：`SQLite → 中间格式 → .tres`。每多一跳就多一个**能静默丢数据**的地方。
2. **版本管理变差**：`.sqlite` 是**二进制**，git 完全无法 diff/merge —— **比 `.xlsx` 更糟**。
3. **要写两套代码**：查询逻辑 + 导出逻辑，还得自己维护"表和字段变了要同步"。
4. **收益为零**：本项目是**几百行的数值表**。SQLite 的强项（百万行、复杂 join、并发写、事务）**一个都用不上**。SQL 的 `JOIN` 对"武器表引用一张技能表"确实优雅，但**在 Godot 里那只是一个 `[Export] SkillDef` 引用**，比外键更好。

> **唯一值得上数据库的信号**：数据量到**几万行以上**、或**多个非程序员同时编辑同一批数据**、或**需要在运行时做复杂查询**。这三条本项目都不满足。

---

## 四、路线对比总表

评分口径：**⭐ 越多越好**；"出错风险"一列是**越低越好**。

| 方案 | 上手成本 | 批量编辑体验 | 版本管理友好度 | 出错风险 | 4.7.2 兼容 | 额外依赖 |
|---|---|---|---|---|---|---|
| **A1. Edit Resources as Table** ⭐ | ⭐⭐⭐ 装插件即用（但**前提是先拆表**） | ⭐⭐⭐⭐⭐ 多选批量 / 乘加 / 复制行 / 排序 / 筛选 / 撤销 | ⭐⭐⭐⭐⭐ **只动 `.tres`，文本，diff 完美** | **低** | ⚠️ **未证实，须实测** | 无（纯 GDScript） |
| A2. ExcelTableImporter | ⭐⭐⭐ | ⭐⭐⭐⭐ 从 xlsx 一键生成 | ⭐⭐ 全量重导 → **diff 变噪音** | 中（**生成 GDScript 类，与 C# 冲突**） | ✅ **明确支持 4.7.2** | 无 |
| A3. CSV Data Importer | ⭐⭐⭐⭐ 极小 | ⭐⭐ 它管导入，**不管"表"** | ⭐⭐⭐⭐ CSV 是文本 | 低 | ✅ **明确支持 4.7.2** | 无 |
| A4. VortarisCSV | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ 二进制进仓库 | 中（**C# 行类未支持**） | ✅ 最低 4.7 | **GDExtension 二进制** |
| A5. ResPort | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐ | **中高**（v0.1，信息少） | ⚠️ 标注 4.5 | 无 |
| **B. Excel/CSV + 外部编辑器** | ⭐⭐⭐⭐ 用户已熟 Excel | ⭐⭐⭐⭐（工具本身）/ ⭐⭐⭐（含导表管线） | ⭐⭐⭐ 取决于源文件入不入库（见第六节） | **中高**（BOM / 分隔符 / 自动改数据 / CRLF） | 工具无关；**生成脚本要自己写** | CSV 编辑器（免费可选） |
| C. Gridly / Lokalise / Crowdin | ⭐ 贵且重 | ⭐⭐⭐⭐（SaaS 协作） | ⭐⭐ 数据在云端 | 中（多一跳同步） | ❌ **无 Godot 数据表集成** | **€99/月起** |
| C. SQLite + 导出脚本 | ⭐⭐ 要写两套代码 | ⭐⭐⭐⭐（DB4S 免费好用） | ⭐ **二进制，git 无法 diff** | **高**（多一跳 + 原生依赖） | Godot 本体不支持 | 原生库 |

---

## 五、⭐ 推荐（本节最重要）

### 推荐：**路线 A —— 装 `Edit Resources as Table`，不引入任何表格文件**

#### 理由

1. **它是唯一"一装就能解决"的工具。** 它做的事**恰好就是需求原话**："一个编辑器可以在这些表项目进行快速的编辑"。**把文件夹当表、一行一个资源、一列多选批量改** —— 不需要 CSV、不需要 Excel、不需要生成脚本、不需要数据库。**没有任何中间产物，因此没有任何中间产物能出错。**
2. **版本管理天然满分。** 它**只改 `.tres` 本身**，而 `.tres` 是文本。改动是**一行一个字段**的 diff，`git blame` 能追到"谁把 `PistolDamage` 从 24 改成 30"。**这一条直接消灭了第六节那个 `.xlsx` 难题** —— 因为你根本不需要引入 `.xlsx`。
3. **C# 兼容有一手证据。** issue #72 里 C# 用户实测报告 **"It works with C# Scripts."**，维护者据此加了 "Use Script" 选项。**这不是我推断的，是原始 issue 文本。**
4. **"读不到就用默认值"的回退语义不受影响。** 数值始终留在同一批 `.tres` 里，缺失字段照样回退到 `ConfigIndexData.cs` 的 C# 属性初始化器（`= 160f`）。**这条管线不新增任何环节，所以不可能破坏已有语义。**
5. **MIT + 仍在维护 + 卸载零成本。** MIT 许可（可改可商用）；`master` 到 **2026-01-27** 还在提交（社区 PR 到 2026-02 仍活跃）；纯 GDScript 编辑器插件，**不想用了删掉 `addons/` 目录就干净了**，不会在项目里留残渣。
6. **它连"嵌套"都有解。** `StageDefinition[] Stages` 这种数组列，可以用**"把 Resource 数组列单独打开成另一张表"**来处理 —— 这是它 feature 列表里的原生能力，不用你自己拆。

#### 为什么**不**推荐其他

- **A2 ExcelTableImporter**：技术质量最高（4.7.2 明确支持、MIT、零依赖、2026-09 更新），**但它生成 GDScript 类**。C# 项目接进来会长出一套平行类型体系。**如果哪天项目转 GDScript，这是第一选择。**
- **A4 VortarisCSV**：最低 4.7、功能极强，但**明确只支持 GDScript / C++ 行类**，还要往仓库塞 4MB 平台二进制。
- **B 路线**：**用户熟悉 Excel 是真实优势**，所以 B 不是错的 —— 但它**引入了一条需要长期维护纪律的管线**（源文件入不入 git、BOM、分隔符、CRLF、自动改数据）。**在有 A1 的前提下，为了"用 Excel"而付这些代价不划算。**
- **C 路线**：Gridly / Lokalise / Crowdin **连 Godot 数据表集成都找不到**（我用仓库检索反向验证过），而且 **€99/月起**。SQLite 更糟：**二进制 + 多一跳 + 原生依赖**，对一个几百行的数值表是明确的过度工程。

---

### 落地步骤（照着做）

#### 第 0 步（**前提，必须做**）：先把"宽表"拆成"按类型的表"

**不做这一步，A1 装了也没东西可编。** 目标形态：

```
Scripts/Config/
  WeaponDef.cs        [GlobalClass] public partial class WeaponDef : Resource
  SkillDef.cs
  UpgradeDef.cs
  CharacterDef.cs

Config/
  ConfigIndex.tres        ← 保持不动（全局倍率、表现开关、BOSS 压力曲线…）
  Tables/
    Weapons/    wpn_pistol.tres   wpn_rifle.tres   wpn_sniper.tres
    Skills/     skl_x.gd          ...
    Upgrades/   upg_*.tres
    Characters/ chr_*.tres
```

三条硬性约定：

1. **每个类都写 `[GlobalClass]`** —— 否则 Godot 的"新建资源"对话框里选不到它，表也就建不起来。
2. **每个 `[Export]` 都保留 C# 属性初始化器当默认值**（`[Export] public float Damage { get; set; } = 24f;`）—— **这是"读不到就用默认值"语义的唯一实现方式**，照抄 `ConfigIndexData.cs` 的写法。
3. **一个文件夹 = 一张表。** 插件的分组单位就是文件夹（外加按类/脚本筛选）。**不要把武器和技能混在一个目录里。**

> 字段具体怎么定（武器表该有哪些列）→ **见 [config-schema.md](config-schema.md)，本文不越界。**

#### 第 1 步：装插件

- 编辑器 → `AssetLib` → 搜 **"Edit Resources as Table"** → 下载安装 → `Project Settings → Plugins` 启用。
- 或 clone 仓库（想拿最新就 clone `master`）：`https://github.com/don-tnowe/godot-resources-as-sheets-plugin`
- 装完会多一个 **ResourceTables** 标签页。

#### 第 2 步：**在 Godot 4.7.2 上先做冒烟测试**（这是兼容性关卡，别跳过）

| 测什么 | 期望 |
|---|---|
| 打开 `res://Config/Tables/Weapons/` | 出现表格，**列名 = C# 的 `[Export]` 属性名** |
| ⚠️ 如果表是**空白** | **先重开一次项目再说** —— 这正是 issue #113 的症状，维护者原话 "sometimes the plugin just does this for new projects"，提出者重开后 "working perfectly fine" |
| 改一个数字 → 保存 | 用文本编辑器打开那个 `.tres`，**确认文本真的变了** |
| `Ctrl+Z` | 能撤销 |
| 跑一遍项目验证 | `build.ps1 -v minimal` 0 warning/0 error + `art-onboarding.md` §4 的 `selftest.flag` 流程 |

**只有这一步全过了，才继续往下。** 如果 4.7.2 上真的坏了，退路是：① 等修 / 提 issue（维护者响应积极，4.4 的崩溃 issue 一周内就关了）；② 退到 **A3 CSV Data Importer**（明确支持 4.7.2）+ 自己写生成脚本；③ 走路线 B。

#### 第 3 步：日常工作流（这就是你要的"快速编辑"）

| 我要做的事 | 怎么做 |
|---|---|
| 改一列里的很多个值 | 点列头，**Shift+Click / Ctrl+Click 多选单元格**，直接打字 → 一次改完 |
| 全体武器伤害 ×1.1 | 选中伤害列 → **批量乘**（feature 列表里的 "Multiply/add numbers"） |
| 加一行（新武器） | **`Ctrl/Cmd+D` 复制一行**，再 `Ctrl/Cmd+R` 重命名文件 → **比走 CSV 导入新建行安全得多**（CSV 新建行是已知的毛刺路径） |
| 只想看"稀有度=史诗"的武器 | 按 **Resource 类/脚本**筛选，或写 **GDScript 表达式**筛选 |
| 改 `Stages` 这种数组 | **把该列单独打开成另一张表**（原生能力），或退回 Inspector。**⚠️ 绝对不要走 CSV 那条路 —— 数组/字典的 CSV 导入至今没实现（issue #61）** |

#### 第 4 步（**可选**）：确实想用 Excel 的话

**只在"一次性灌入一大批新数据"时用**，日常改动仍回到第 3 步的网格里。

1. Excel 里按"一行一个武器"排好，**第一行是表头，表头名必须与 C# 属性名逐字一致**（`Damage`、`FireInterval`、`MagazineSize`…）
2. **导出为「CSV UTF-8（逗号分隔）」**，不要用普通 CSV（后者可能是 ANSI/GBK）
3. 导完**先抽查三行**：① 分隔符是 `,` 还是 `;`；② 有没有 BOM（第一列列名是否多了个不可见字符）；③ `007` 有没有被吃成 `7`
4. 插件里 → `Import/Export CSV` → **Import** 页 → **"Use Script" 选你的 C# 类** → 给每列**指定属性类型** → `Convert to Resources and edit`
5. ⚠️ **数组/字典列跳过**（issue #61 未支持）；⚠️ 导入报 `possible cyclic resource inclusion` 或 `Out of bounds get index` 时，**改用第 3 步的"复制行"**（这些错误在 issue #72 里有完整记录）

---

## 六、⚠️ 版本管理提醒：`.tres` 是文本，`.xlsx` 是二进制 —— 这个矛盾怎么处理

### 矛盾的本质

**`.xlsx` 其实是一个 ZIP 包，里面是一堆 XML。** 对 git 来说它是**一个不透明的二进制 blob**：

| 后果 | 说明 |
|---|---|
| **无法 diff** | `git diff` 只吐 "Binary files differ"。**看不出谁改哪个数值** |
| **无法 merge** | 两人同时改表 → 冲突**不可解决**，只能二选一重做 |
| **每次保存重写整个文件** | 哪怕只改一个格子，ZIP 内部结构全变 → **每次提交都是一整个新 blob**，仓库迅速膨胀 |
| **行尾 / BOM 无常** | Excel 导出是 CRLF，可能带 BOM。本项目 `assets.md` §5 明文要求 **UTF-8 无 BOM + LF**，而 `assets.md` 里还写着"**本项目栽过 7 次以上**"行尾不一致的坑 |

**而 `.tres` 正好相反**：纯文本、一行一个字段、diff 干净、blame 可用。**这是这个项目已经选对的资产，不应该为了编辑方便把它换掉。**

### 三种处理方式，推荐第 3 种

| 方案 | 做法 | 评价 |
|---|---|---|
| ① **只入 `.tres`，不引入 `.xlsx`** | 用 A1 的网格当编辑器，`.tres` 是唯一产物 | ✅ **推荐（= 本文的推荐方案）**。矛盾根本不存在。**"excel 表"要解决的是"批量快速编辑"，不是"必须用 Excel 这个软件"** —— A1 给的就是批量快速编辑 |
| ② `.xlsx` 和 `.tres` **都入库** | 两个源真相 | ❌ **不要**。必然出现"有人改了 `.tres`，另一个人拿旧 `.xlsx` 重导 → 改动被静默回滚"。**静默回滚是最贵的一类 bug** |
| ③ **`.xlsx` 不入库，`.csv` 入库** | `.gitignore` 掉 `*.xlsx`；把导出的 `.csv`（**文本，可 diff**）和 `.tres` 一起提交 | ⚠️ **如果 Excel 不可让步，选这个**。折中：diff 虽然比 `.tres` 粗（整行），但**至少能看见、能 review、能 blame** |

### 如果走方案 ③，三条纪律必须落到文档里

1. **`.gitignore` 加 `*.xlsx`**（或放到项目外的 `~/Documents`），**只提交 `.csv`**。
2. **在 `Config/Tables/` 下放一个 `README.md`，写明方向**：
   > 数据流：`.xlsx`（本地草稿，不入库）→ 导出 `.csv`（入库）→ 生成 `.tres`（入库，**游戏只读这个**）。
   > **`.tres` 是唯一真相。** 小改动直接在 Godot 里改 `.tres`，**不要**改完再回头动 xlsx —— 否则下次重导会把你的改动覆盖掉。
   > **xlsx 只用于"一次性批量新增/重构整张表"**，用完即弃。
3. **⚠️ 永远不要用二进制 `.res` 存配置。** Godot 可以把资源存成二进制 `.res`（A1 插件甚至支持编辑 `.res`），但**配置必须保持文本 `.tres`** —— 二进制化等于把上面的问题全套搬进来。本项目现在用的就是文本 `.tres`，**保持住**。

### 顺带一条：`.tres` 的 uid

注意本项目 `assets.md` §5 第 4 条的既有约定：

> 项目里所有 `ext_resource` **只写 `path=`，不写 `uid=`** —— 保持一致，别手搓 `uid://`。

**A1 插件是通过 Godot 的资源系统保存的，它会按引擎默认行为写文件。** 建议装完插件后**随手改一个字段保存，然后 `git diff` 看一眼**：确认它没有引入 `uid=` 或把 `load_steps` 写乱。**如果引入了，这是一个需要立刻知道的事**（改一个决定 vs 改几十个文件）。

---

## 七、一页速查

| 问题 | 答案 |
|---|---|
| **"有没有一用就能解决的工具？"** | **有** —— **`Edit Resources as Table`**（<https://godotengine.org/asset-library/asset/1479>）。装完即得"文件夹当表、一行一资源、多选批量改"。**前提是先把配置拆成"一类一个 C# `[GlobalClass]` 类、一行一个 `.tres`"** |
| 它支持 4.7 吗？ | ⚠️ **没有明确声明，必须实测**。最后明确版本适配是 4.4（2025-05），最新的用户实测是 4.5.1（2025-11） |
| C# 能行吗？ | ✅ **能**。issue #72 里 C# 用户实测原话 "It works with C# Scripts." |
| 要先做什么？ | **拆表**（第 0 步）。不拆表，所有工具都无事可做 |
| 要不要 Excel？ | **不需要**。要的是"批量快速编辑"，A1 直接给 |
| 一定要 Excel 呢？ | Excel → **CSV UTF-8** → 插件的 CSV 导入（**用 "Use Script" 选 C# 类**）。**别用 CSV 新建行，用"复制行"** |
| `.xlsx` 进 git 吗？ | **不进**。要留就留 `.csv`（文本可 diff）。**`.tres` 是唯一真相** |
| Gridly / Lokalise / Crowdin？ | ❌ **没找到 Godot 数据表集成**，且 €99/月起 |
| SQLite？ | ❌ **过度工程**：二进制 + 多一跳 + 原生依赖，而数据只有几百行 |
| 最省事的兜底？ | 先把 `ConfigIndexData` 里那 **~100 个扁平 `[Export]`** 按类型拆成几个 `[GlobalClass]` 类，**哪怕暂时不装插件**，光靠 Inspector 也会比现在好改 |

---

## 八、本次调研的验证账目

**实际打开并读取原文的页面（✅ 已验证可访问）：**

| # | 页面 |
|---|---|
| 1 | <https://godotengine.org/asset-library/asset/1479>（Edit Resources as Table 2） |
| 2 | <https://github.com/don-tnowe/godot-resources-as-sheets-plugin>（README，master 与 Godot-4 分支） |
| 3 | <https://raw.githubusercontent.com/don-tnowe/godot-resources-as-sheets-plugin/master/addons/resources_spreadsheet_view/plugin.cfg> |
| 4 | <https://api.github.com/repos/don-tnowe/godot-resources-as-sheets-plugin>（stars / pushed_at / license） |
| 5 | GitHub API：该仓库 `master` 与 `Godot-4` 分支提交历史、issue #113 / #72 正文与评论、issue #81 / #116 / #111 摘要 |
| 6 | <https://store.godotengine.org/asset/wolflanlan/excel-table-importer/>（ExcelTableImporter） |
| 7 | <https://store.godotengine.org/asset/timothyqiu/csv-data-importer/>（CSV Data Importer） |
| 8 | <https://store.godotengine.org/asset/asdf-studio/vortaris-csv/>（VortarisCSV） |
| 9 | <https://godotengine.org/asset-library/asset/4734>（ResPort） |
| 10 | <https://godotengine.org/asset-library/asset/3048>（Gnumaru's Static Data Importer） |
| 11 | <https://godotengine.org/asset-library/asset/4815>（CSV Parser） |
| 12 | <https://www.moderncsv.com/buy/> + <https://www.moderncsv.com/free-version/> |
| 13 | <https://tablecruncher.com/> |
| 14 | <https://www.ronsplace.ca/products/ronsdataedit> |
| 15 | <https://www.gridly.com/pricing/> |
| 16 | <https://sqlitebrowser.org/> |
| 17 | GitHub 仓库检索：`gridly+godot`（308 条全无关）、`crowdin+godot`（1 条无关）、`lokalise+godot`（**0 条**） |

**⚠️ 仅搜索结果出现，未验证：**

- **CSVed**、**Tad** —— 需求点名的两个 CSV 工具，**本次未打开官方页**，故不给链接与价格
- **Google Sheets** / **Airtable** —— 价格与 CSV 导出行为未验证
- **Baserow** / **NocoDB** —— 未验证
- **`FileAccess.get_csv_line()`** —— 文档 API 列表命中，但官方文档站抓取时只返回导航栏，**未能逐字读该页**
- Godot C# 侧的 SQLite 库（`Microsoft.Data.Sqlite` 等）—— 未验证版本与原生库打包方式

**❌ 明确未能验证 / 不存在的：**

- Gridly、Lokalise、Crowdin 的 **Godot 数据表集成** —— 检索后确认不存在
- "现成的、适配 C# `[GlobalClass]` 的 CSV → `.tres` 开源脚本" —— **没找到**

---

*本文只写工具选型。表结构设计见 [config-schema.md](config-schema.md)；数值分层与"唯一入口"约定见 [config.md](config.md)；`.tres` 的格式规矩见 [assets.md](assets.md) §5。*
