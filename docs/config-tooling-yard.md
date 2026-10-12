# YARD 在 C# 项目里的可用性调研（`docs/config-tooling-yard.md`）

> 调研对象：**YARD — Yet Another Resource Database**（作者 Elliot Fontaine，MIT）
> 主页 <https://github.com/elliotfontaine/yard-godot> · Asset Store <https://store.godotengine.org/asset/elliotfontaine/yard-yet-another-resource-database/>
> 本文**只回答"YARD 能不能用在 C# 项目里"**。工具选型的主结论见 [config-tooling.md](config-tooling.md)（本文不改它）；表字段设计见 [config-schema.md](config-schema.md)。
>
> **标记约定**
>
> | 标记 | 含义 |
> |---|---|
> | ✅ **已验证** | 我**实际读到/实际跑过**（读源码原文、反射本地 DLL、或在本机编译验证） |
> | ⚠️ **未验证 / 推断** | 有依据的推断，或明确没能验证，已注明卡在哪 |
> | ❌ **查不到** | 明确找了、没找到 |
>
> **本机环境核对**：`项目a` 是 `Godot.NET.Sdk/4.7.2` + `net8.0` + `<Nullable>enable</Nullable>`，`project.godot` 的 `config/features` 是 `PackedStringArray("4.7", "C#", "Forward Plus")`。本机 NuGet 缓存里有 `godotsharp 4.7.2` / `godot.sourcegenerators 4.7.2`，所以本文凡写"实测"都是**真在 4.7.2 的工具链上编译过**。

---

## ⚠️ 现场状态更新（写本文过程中发现，请先读这段）

**YARD 已经装进项目了 —— 不是我装的。** 我在收尾核对 `git status` 时发现：

```
?? addons/yard/                        ← 未跟踪，新增
 M project.godot                       ← [editor_plugins] enabled 里多了 yard
```

核实结果：

| 项 | 实测值 |
|---|---|
| 安装时间 | `addons/yard/` 内文件时间戳 **2026-10-11 18:12:46 ~ 18:12:47**（我的调研期间，很可能是用户或另一个 agent 装的） |
| 版本 | **`plugin.cfg` → `version="1.3.1"`**，即最新 release（**不是** main 的 1.3.0） |
| `project.godot` 改动 | `enabled=PackedStringArray("res://addons/godot_mcp/plugin.cfg", "res://addons/yard/plugin.cfg")` |
| 与上游 v1.3.1 是否一致 | ✅ **逐字节相同**。我下载 `v1.3.1` tag 的 8 个关键文件（`Registry.cs` / `registry.gd` / `plugin.cfg` / `plugin.gd` / `editor_inspector_plugin.gd` / `registry_io.gd` / `compat.gd` / `yard_settings.gd`）与项目里的安装副本做 SHA256 比对，**8/8 `IDENTICAL`** |
| 体积 | **173 个文件**（62 个 `.gd` + 63 个 `.uid` + 1 个 `.cs`），共 **939.4 KB** |

**这件事让本文的价值变了**：它不再是"要不要装"的推演，而是"**装完之后会发生什么**"的核对。结论如下（都是可直接行动的事实）：

1. ✅ **本文全部分析对当前安装的版本 100% 适用。** 我编译验证用的 `Registry.cs` 与项目里现在这个文件 **SHA256 完全相同**（`EF54A5A399A108D6A86910693EB228A28178CA14ED3E47BD47ACECC465396C20`），我读的 `registry.gd` / `editor_inspector_plugin.gd` 等也全部与安装副本一致。**没有"文档写的是另一个版本"的问题。**
2. 🔴 **下一次构建会多出 1 条编译警告。** `ActRoguelike.csproj` 是 `<Nullable>enable</Nullable>` 且**没有任何 `Exclude` / `Compile Remove`**，所以 `addons/yard/Registry.cs` 会被编进 `ActRoguelike` 程序集，并产生：
   ```
   addons/yard/Registry.cs(50,9): warning CS8618: 在退出构造函数时，不可为 null 的 字段 "_registry" 必须包含非 null 值。
   ```
   这**会破掉你们 `build.ps1 -v minimal` 的 0 warning 验收线**。我在项目里的安装副本上确认了第 50 行正是 `public Registry(string path)`、第 52 行是 `_registry = ResourceLoader.Load<Resource>(path);`，与我在探针里复现出该警告的代码**完全一致**。
   → 处理办法见 §2.4(1)。
3. ⚠️ **`project.godot` 里现在有 `yard/updates/channel` 那条设置的落点**，且**编辑器一启动就会联网检查 GitHub 更新**（见 §3.4）。如果这台机器/CI 是离线的，注意编辑器启动时的网络等待。
4. ⚠️ **我给的建议因此要调整**：原来第 1 条是"先别换、先装 ERAT"。**现在 YARD 已经在项目里了**，所以现实的下一步不是"装不装"，而是：**① 立刻跑一次 `build.ps1 -v minimal` 看那条警告、并确认注册表 `.tres` 的 git diff 长什么样；② 决定它和 ERAT 是"共存（必须分目录，见 §4.4）"还是"撤掉（删 `addons/yard/` + 还原 `project.godot` 那一行）"。**

> **声明**：以上"安装时间 / 版本 / 字节一致性 / 文件数 / 体积"都是我实测的。**我没有对项目做任何修改**——没有安装、没有构建、没有动 `project.godot`；本文只新增了 `docs/config-tooling-yard.md` 这一个文件。

---

## 零、一页结论（先看这个）

| 问题 | 结论 |
|---|---|
| **1. `@export_custom` 在 C# 里怎么写？** | ✅ **有等价写法，能编译**：`[Export((PropertyHint)1024, "res://…/xxx.tres")]`，用于 `StringName` / `string`。**但 `Array[StringName]` / `Array[string]` 上不行**（hint 被源码生成器覆盖成 `TypeString`） |
| **2. 运行时 API 能不能从 C# 调？** | ✅ **顺畅**。YARD **官方自带 C# 包装类** `addons/yard/Registry.cs`（`YARD.Registry<TResource>`）。我把它加进本机探针项目**实测编译通过**，含 `Callable.From(...)` lambda 谓词 |
| **3. 需要在项目里放什么？** | `addons/yard/` 一个目录；**没有 autoload**；只加 **1 条** ProjectSettings（`yard/updates/channel`）；**你不需要写任何 GDScript**；Asset Store 明确声明支持 **4.4 – 4.8.x**（列表里**有 4.7.2**） |
| **4. 比 `Edit Resources as Table` 更好还是更差？** | ⚠️ **不是同一个层级的工具，不能简单比"更好/更差"**。ERAT 强在**批量改数值**（YARD 明确没有多选批量编辑）；YARD 强在**表间按稳定 ID 互相指向 + 运行时按需加载**（ERAT 完全没有）。两者前提相同：**都得先拆表** |
| **`@export_custom` 在 C# 里用不了吗？** | ❌ **这个前提不成立 —— 能用**。标量下拉在 C# 里可用。只损失"数组列的一栏多选" |
| **能不能两个都装？** | ✅ 可以（不同插件、不同文件），**但有一条必须遵守：注册表 `.tres` 和行资源 `.tres` 必须分目录放** |

---

## 一、⭐ 关键问题 1：`@export_custom` 在 C# 里怎么写

### 1.1 先说结论

| 属性类型 | C# 写法 | 生成的 `PropertyInfo` | YARD 下拉能用吗 |
|---|---|---|---|
| `StringName` | `[Export((PropertyHint)1024, "res://…/reg.tres")]` | `hint=1024, hintString="res://…/reg.tres"` ✅ | ✅ **能** |
| `string` | 同上 | `hint=1024, hintString="res://…/reg.tres"` ✅ | ✅ **能** |
| `Godot.Collections.Array<StringName>` | 同上 | **`hint=23`（`TypeString`）、`hintString="21/0:"`** ❌ | ❌ **不能** |
| `Godot.Collections.Array<string>` | 同上 | 同上（`hint=23`）❌ | ❌ **不能** |

**确切能编译的写法**（我实测编译通过，0 error）：

```csharp
// YARD 的 PROPERTY_HINT_CUSTOM = 1024。C# 侧没有这个常量，必须自己硬编码 —— 见 1.4。
public const PropertyHint RegistryHint = (PropertyHint)1024;

[GlobalClass]
public partial class WeaponDef : Resource
{
    [Export]
    public float Damage { get; set; } = 24f;

    // 指向"属于它的升级池"注册表里的某一行
    [Export(RegistryHint, "res://Config/Registries/upgrade_pools.tres")]
    public StringName UpgradePool { get; set; } = "";
}
```

> ⚠️ **C# 语言限制**：特性（Attribute）的实参**必须是编译期常量**，所以**不能**写成
> `[Export((PropertyHint)Registry.PROPERTY_HINT_CUSTOM, …)]` —— GDScript 的 `Registry` 类在 C# 里**不是类型**（它是 `class_name` 注册的 GDScript 全局类），拿不到它的常量。**`1024` 只能硬编码。**

### 1.2 为什么没有官方的 `[ExportCustom]` —— ✅ 已验证

我把本机 `godotsharp 4.7.2` 的 `GodotSharp.dll` 反射了一遍，**GodotSharp 4.7.2 里全部 Attribute 类型**是：

```
Godot.AssemblyHasScriptsAttribute     Godot.ExportCategoryAttribute    Godot.RpcAttribute
Godot.ExportAttribute                 Godot.ExportGroupAttribute       Godot.ScriptPathAttribute
Godot.ExportSubgroupAttribute         Godot.ExportToolButtonAttribute  Godot.SignalAttribute
Godot.GlobalClassAttribute            Godot.GodotClassNameAttribute    Godot.ToolAttribute
Godot.IconAttribute                   Godot.MustBeVariantAttribute     Godot.RDVertexAttribute
```

**没有 `ExportCustomAttribute`。** ✅ 已验证（本地反射 `C:\Users\huayu\.nuget\packages\godotsharp\4.7.2\lib\net8.0\GodotSharp.dll`）

官方 C# 文档页 <https://raw.githubusercontent.com/godotengine/godot-docs/master/tutorials/scripting/c_sharp/c_sharp_exports.rst> **全文没有出现 `export_custom`、也没有"自定义 hint"这一节**。它文档化的只有 `PropertyHint.File/Dir/Range/Enum/Flags/ExpEasing/None/MultilineText/…`。✅ 已验证（读 RST 原文）

> **所以"官方文档层面"的答案是：C# 没有 `@export_custom` 的等价物。** 这是事实，不能含糊。

### 1.3 但它实际上**能用** —— ✅ 实测（证据链完整）

**关键事实**：`ExportAttribute` 的构造函数就是 `ExportAttribute(PropertyHint hint, string hintString)`，而 `PropertyHint` 在 4.7.2 里的**底层类型是 `Int64`**，所以 `(PropertyHint)1024` 是合法的。✅ 已验证（反射 + 枚举值检查；`PropertyHint.Max = 45`，1024 不在枚举里但可以强转）

**最关键的一步**：Godot 的 C# 源码生成器（Roslyn analyzer）**不做任何 hint 校验**，直接把构造函数参数当整数透传。源码 `modules/mono/editor/Godot.NET.Sdk/Godot.SourceGenerators/ScriptPropertiesGenerator.cs` 的 `DeterminePropertyInfo()` 里：

```csharp
if (!TryGetMemberExportHint(typeCache, memberType, exportAttr, memberVariantType,
        isTypeArgument: false, out var hint, out hintString))
{
    var constructorArguments = exportAttr.ConstructorArguments;
    if (constructorArguments.Length > 0)
    {
        var hintValue = exportAttr.ConstructorArguments[0].Value;
        hint = hintValue switch {
            null => PropertyHint.None,
            int intValue => (PropertyHint)intValue,   // ← 1024 原样透传
            _ => (PropertyHint)(long)hintValue
        };
        hintString = constructorArguments.Length > 1
            ? exportAttr.ConstructorArguments[1].Value?.ToString() : null;
    }
    ...
}
```
✅ 已验证（读 Godot 源码原文）

**我在本机实际编译验证了**（在**系统临时目录**建了一个独立探针项目，用与 `项目a` **完全相同的** `Godot.NET.Sdk/4.7.2` + `net8.0`，打开 `EmitCompilerGeneratedFiles` 把 Roslyn 生成的代码 dump 出来看）。生成结果**逐字**如下：

```
properties.Add(new(type: (global::Godot.Variant.Type)21, name: PropertyName.@ItemIdName,
  hint: (global::Godot.PropertyHint)1024, hintString: "res://data/item_registry.tres",
  usage: (global::Godot.PropertyUsageFlags)4102, exported: true));

properties.Add(new(type: (global::Godot.Variant.Type)4, name: PropertyName.@ItemIdStr,
  hint: (global::Godot.PropertyHint)1024, hintString: "res://data/item_registry.tres",
  usage: (global::Godot.PropertyUsageFlags)4102, exported: true));

properties.Add(new(type: (global::Godot.Variant.Type)28, name: PropertyName.@ItemIds,
  hint: (global::Godot.PropertyHint)23, hintString: "21/0:",          // ← 数组：hint 被覆盖！
  usage: (global::Godot.PropertyUsageFlags)4102, exported: true));

properties.Add(new(type: (global::Godot.Variant.Type)2,  name: PropertyName.@Level,
  hint: (global::Godot.PropertyHint)1,  hintString: "0,10",           // ← 对照组：Range 正常
  usage: (global::Godot.PropertyUsageFlags)4102, exported: true));
```
✅ 已验证（本机 `dotnet build` 后读 `generated/Godot.SourceGenerators/…ScriptProperties.generated.cs`）

`usage = 4102 = 4096 | 6 = SCRIPT_VARIABLE | STORAGE | EDITOR`，与 GDScript `@export` 产生的 usage 相同。

**而这正是 YARD 需要的**。YARD 的下拉是靠 `addons/yard/editor_only/editor_inspector_plugin.gd` 实现的，它第一行就检查：

```gdscript
func _parse_property(object, type, name, hint: PropertyHint, hint_text: String, _usage: int, _wide: bool) -> bool:
	if hint != Registry.PROPERTY_HINT_CUSTOM:      # 1024
		return false
	var parts: PackedStringArray = hint_text.split(",")
	var registry_path: String = parts[0]
	...
	match type:
		TYPE_STRING, TYPE_STRING_NAME:  → 建下拉（EditorPropertyOptionWrapper）
		TYPE_ARRAY:                     → 要求 array 是 typed 且 builtin 为 String/StringName
```
✅ 已验证（读源码原文 <https://raw.githubusercontent.com/elliotfontaine/yard-godot/main/addons/yard/editor_only/editor_inspector_plugin.gd>）

C# 生成出来的 `PropertyInfo(hint: 1024, hintString: "res://…")` 与 GDScript `@export_custom(1024, "res://…")` **是同一组字段、走同一条注册路径**，所以 `hint == 1024` 成立、`hint_text` 就是那个路径。

**为什么这不是"猜"**：GDScript 官方文档在 `@export_custom` 一节明确写

> "This allows defining **any property hint**, hint string and usage flags…"
> ⚠️ **"When using `@export_custom`, GDScript does not perform any validation on the syntax."**
> —— <https://raw.githubusercontent.com/godotengine/godot-docs/master/tutorials/scripting/gdscript/gdscript_exports.rst>

即**引擎不校验 hint 数值**，1024 会原样送到 Inspector。✅ 已验证（读官方文档原文）

> ⚠️ **本文唯一一处"未在真机点过"的地方**：**本机没有安装 Godot 编辑器可执行文件**，所以"生成出来的 PropertyInfo 在 Inspector 里真的会长出下拉框"这一步是 **代码生成层已实测 + 引擎层强推断**，不是我在编辑器里肉眼看到过。见第五节的验证账目。

### 1.4 ⚠️ C# 侧的确切缺口：数组列用不了

对 `Array[StringName]` / `Array[string]`，源码生成器走的是**另一条分支**：

```csharp
if (!isTypeArgument && variantType == VariantType.Array)
{
    var elementType = MarshalUtils.GetArrayElementType(type);
    ...
    hintString = (int)elementVariantType + "/" + (int)PropertyHint.None + ":";  // "21/0:"
    hint = PropertyHint.TypeString;      // ← 无条件覆盖成 23，你写的 1024 被丢掉
    return hintString != null;           // 返回 true ⇒ 上面的构造函数参数分支被跳过
}
```
✅ 已验证（读源码 + 实测生成结果 `hint: 23, hintString: "21/0:"`）

后果：YARD 的 `_parse_property` **第一句 `if hint != 1024: return false` 就返回了**，所以 **`@export_custom` 的"一栏多选"编辑器在 C# 里完全拿不到**。

**对你的场景意味着什么**（用需求原话对照）：

| 你的需求原话 | C# 里可行吗 |
|---|---|
| "武器表的某一行要指向属于它的升级池" | ✅ 可行 —— **一个标量 `StringName`**，正好是能用的那个形态 |
| "池表里一个选项占一栏，每个选项要指向它的执行逻辑" | ✅ 可行 —— **"一个选项占一栏" = 一栏一个标量 `StringName`**，也正好是能用的形态 |
| 如果哪天想要"**一栏里放多个**选项"（`Array[StringName]`） | ❌ 编辑器下拉拿不到。退路：多个标量字段，或一个逗号分隔 `string`（失去 YARD 的编辑器和类型约束），或干脆 `[Export] SomeDef` 直接引用资源 |

> **所以：YARD 最核心的那个优势（Inspector 下拉选注册表条目）在 C# 里是"能用"的**，只要按"一个引用 = 一个标量 `StringName`"来设计字段。**这恰好就是你描述的用法。**

### 1.5 顺带两个坑

1. **`1024` 是魔法数字。** `YARD.Registry<T>` **没有**暴露 `PROPERTY_HINT_CUSTOM`。
   - 想在运行时拿到它，可以 `GD.Load<Script>("res://addons/yard/registry.gd").GetScriptConstantMap()["PROPERTY_HINT_CUSTOM"]` —— ✅ 已验证 `Script.GetScriptConstantMap()` 在 4.7.2 里存在（反射确认）。
   - **但这个办法对特性没用**（特性要编译期常量）。所以定义里硬编码，并在注释里写明出处。
2. **值是"字符串 ID"，不是资源引用。** 下拉选出来的是 `&"upgrade_pool_common"` 这种 ID，不是 `UpgradePoolDef` 对象。运行时你要 `pools.LoadEntry(weapon.UpgradePool)`。
   - **好处**：文件改名/搬家不会断（这正是 YARD 的卖点）；目标资源**不会**因为引用而被自动加载。
   - **代价**：**没有编译期校验**。`.tres` 里的 ID 打错了 → 运行时 `LoadEntry` 返回 `null`。**必须给 `null` 兜底**，否则就违反了本项目 `ConfigIndexData.cs` 那套"读不到就用默认值"的语义（见 [config-tooling.md](config-tooling.md) 第零节）。默认值建议 `= ""`，并在 `LoadEntry` 后判空。

---

## 二、⭐ 关键问题 2：运行时 API 能不能从 C# 调用

### 2.1 结论：**顺畅**（不是"别扭但可用"，更不是"不可用"）

**原因：YARD 官方自带一个 C# 包装类。**

- 文件：`addons/yard/Registry.cs`（15428 字节），外加 `Registry.cs.uid`
- 命名空间 / 类型：`namespace YARD;` → `public class Registry<[MustBeVariant] TResource> where TResource : Resource`
- ✅ 已验证（读源码原文 <https://raw.githubusercontent.com/elliotfontaine/yard-godot/main/addons/yard/Registry.cs>；文件清单来自 GitHub tree API）

**官方依据（原文引用）**：

- v1.2.0 发布说明：**"### C# support (#36) — C# projects are now first-class citizens, thanks to @verfeon. A `Registry.cs` wrapper ships with the plugin, exposing the same API so you can preload registries and query them from `.cs` scripts."**
  ✅ 已验证（<https://api.github.com/repos/elliotfontaine/yard-godot/releases>）
- v1.1.0 发布说明：**"C# resource scripts are now fully supported, making YARD usable in mixed or C#-only projects."**（对应 `Add support for C#-defined resource scripts [@Cer0reZ in #4]`）
  ✅ 已验证（同上）
- CHANGELOG：`Add C# wrapper class [@Verfeon in #36]` / `Add missing uid file for Registry.cs wrapper` / `[Update] Registry.cs with new Filter call.`（PR #52，2026-04-01）
  ✅ 已验证（<https://raw.githubusercontent.com/elliotfontaine/yard-godot/main/CHANGELOG.md>）

### 2.2 ✅ **实测：我真的把它编译过了**

我在系统临时目录的探针项目里，把 YARD 的 `Registry.cs` 原样放到 `addons/yard/` 下，然后写了一段**调用全部运行时 API** 的代码，用 `Godot.NET.Sdk/4.7.2` 编译：

```csharp
using Godot;
using YARD;

public partial class Usage : Node
{
    public void Run()
    {
        var items = new Registry<ItemDef>("res://data/item_registry.tres");

        ItemDef one            = items.LoadEntry("wpn_pistol");     // ✅
        int n                  = items.Size();                       // ✅
        bool has               = items.Has("wpn_pistol");            // ✅
        StringName sid         = items.GetStringIdOf(one);           // ✅
        Godot.Collections.Array<StringName> all = items.GetAllStringIds();          // ✅
        Godot.Collections.Dictionary<StringName, ItemDef> every = items.LoadAllBlocking(); // ✅
        Registry<ItemDef>.RegistryLoadTracker tracker = items.LoadAllThreadedRequest();     // ✅

        // 精确值筛选
        Godot.Collections.Array<StringName> legendaries = items.Filter("rarity", 3);

        // ⭐ lambda 谓词 —— 可行！
        Godot.Collections.Array<StringName> highLevel =
            items.Filter("level", Callable.From((Variant v) => v.AsInt32() >= 10));

        // Where：精确值 + lambda 混用
        var criteria = new Godot.Collections.Dictionary<StringName, Variant>
        {
            { "rarity", 3 },
            { "level", Callable.From((Variant v) => v.AsInt32() >= 10) },
        };
        Godot.Collections.Array<StringName> both = items.Where(criteria);           // ✅
    }
}
```
**结果：`0 error`（1 个 warning，见 2.4）。** ✅ 已验证（本机 `dotnet build`）

一个额外的好消息：`addons/yard/Registry.cs` **会被 Godot.NET.Sdk 的默认 glob 自动编进你的程序集**——我探针里就是把它放在 `addons/yard/` 下、什么都没配，它就编进去了（**不需要**改 `.csproj`）。✅ 已验证

### 2.3 逐个回答用户提的三个子问题

**(a) 这些 GDScript 类在 C# 里能不能访问？**
可以，但**不是**通过"把 GDScript 类当 C# 类型"。
- `registry.gd` 有 `class_name Registry extends Resource`，它是 **GDScript 全局类**，**C# 里没有对应的 C# 类型**（这是 Godot 的既有边界，不是我推断的——YARD 的解决办法就是另写一个 `namespace YARD` 的普通 C# 类，内部走 `GodotObject.Call`）。
- YARD 已经把这层封装好了：`YARD.Registry<T>` 内部就是 `_registry.Call("load_entry", id, typeHint, (int)cacheMode)` 这种调用（`GodotObject.Call(StringName, params Variant[])` 在 4.7.2 存在，✅ 反射确认）。
- **`YARD.Registry<T>` 与 GDScript 的 `class_name Registry` 不冲突**（一个在 `YARD` 命名空间、一个是引擎全局类），我实测同项目共存编译通过。✅

**(b) `filter(&"rarity", Rarity.LEGENDARY)` 这种带 Callable 谓词的调用，从 C# 传 lambda 可行吗？**
**可行。** 包装类签名为 `Filter(StringName property, Variant criterion)`，而 `Callable` 可以隐式转成 `Variant`。要传 lambda 就显式写 `Callable.From(...)`：

```csharp
// 精确值
items.Filter("rarity", 3);
// 谓词（注意用 Variant 形参，因为索引里存的是 Variant）
items.Filter("level", Callable.From((Variant v) => v.AsInt32() >= 10));
```
- `Callable.From` 在 4.7.2 有完整的 `Action<>/Func<>` 重载族（✅ 反射确认）。
- ⚠️ **一个语义坑**：GDScript 那边是 `criterion.call(value)`，`value` 来自烘焙进 `.tres` 的 `_property_index`，**运行时是 `Variant`**。所以 C# lambda 的形参**应该声明为 `Variant`**（然后 `AsInt32()` / `AsStringName()` 自己转），声明成 `int` 会在参数封送时不匹配。**这是我从两边源码对照得出的，没有实机跑过谓词分支** ⚠️。

**(c) 如果只能走 `GodotObject.Call("filter", …)`，返回的 `Dictionary[StringName, Resource]` 在 C# 里怎么接？**
**你不需要自己接。**
- `LoadAllBlocking()` 直接返回 **`Godot.Collections.Dictionary<StringName, TResource>`**（包装类内部做 `new Dictionary<StringName, TResource>(rawDict)`）——`TResource` 就是你的 C# 资源类，**强类型**。
- `Where` / `Filter` 返回 `Godot.Collections.Array<StringName>`。
- `LoadAllThreadedRequest()` 返回 `Registry<ItemDef>.RegistryLoadTracker`（`Progress` / `GetLoadedResources()` / `Requested` / `Status`）。
- 万一你要手写：`(Godot.Collections.Dictionary)obj.Call("load_all_blocking", "", 1)`，然后逐个 `As<T>()`。这是退路，正常不用。
✅ 已验证（实测编译 + 读源码原文）

### 2.4 ⚠️ 但有两个必须知道的小瑕疵（都是实测发现的）

**(1) `<Nullable>enable</Nullable>` 下会多出 1 条编译警告 —— 对本项目是"破 0-warning"**

```
addons/yard/Registry.cs(50,9): warning CS8618: 在退出构造函数时，不可为 null 的 字段 "_registry" 必须包含非 null 值。
```
原因：`private readonly Resource _registry;` 在构造函数里由 `ResourceLoader.Load<Resource>(path)` 赋值，编译器无法证明非 null。

**这直接命中本项目的验收线**：`ActRoguelike.csproj` 写着 `<Nullable>enable</Nullable>`，而 [config-tooling.md](config-tooling.md) 记录的验收方式是 `build.ps1 -v minimal` **0 warning / 0 error**。
- ✅ 已验证：把探针项目的 `<Nullable>` 改成 `disable` 后，该警告**消失**；保持 `enable` 则**稳定出现 1 条**。
- 处理办法（三选一）：① 接受这 1 条警告并记录；② 在 `.csproj` 里对这个文件 `#pragma warning disable CS8618`（不改上游文件）；③ 直接改 `Registry.cs`（**会与上游分叉**，升级时要重新打补丁——不推荐）。

**(2) 包装类的 XML 文档有过期内容，作者自己承认**
- `GetIndexedProperties` 的文档仍引用已废弃的 `filter_by` / `filter_by_value` / `filter_by_values`。
- 作者在 open issue #85 里原话：**"I should fix the C# `IsPropertyIndexed` doc that still cites the removed methods, woops."** ✅ 已验证（issue #85 正文，<https://github.com/elliotfontaine/yard-godot/issues/85>）
- 只是文档问题，不影响编译。**但说明 C# 侧是"能用但不是最上心"的那部分。**

⚠️ **API 变动预警**：同一个 issue #85（作者自己开，状态 open）计划把 `load_all_blocking` 改名为 `load_all`，并**删除 `filter_by*` 及"their C# equivalents"**。截至 `main`（2026-10-08 最后一次推送）**尚未落地**（我读 `registry.gd` 时 `load_all_blocking` 还在），但这是一个**即将到来的破坏性变更**，升级 YARD 大版本时要预期改代码。

---

## 三、关键问题 3：它需要在项目里放什么

### 3.1 装完项目里多了什么

| 项目 | 内容 | 依据 |
|---|---|---|
| **目录** | `addons/yard/`（单目录）。**实测 v1.3.1 共 173 个文件**（62 `.gd` + 63 `.uid` + 1 `.cs` + 场景/图标/翻译等），**939.4 KB**。含 `plugin.cfg`、`plugin.gd`、`registry.gd`、**`Registry.cs` + `Registry.cs.uid`**、`editor_only/`（编辑器 UI，全 GDScript）、`locale/`（**含 `zh_CN.po`，有简体中文界面**）、`LICENSE` | ✅ 已验证（GitHub tree API + **项目内安装副本实测计数**） |
| **autoload** | ❌ **不加任何 autoload**。`plugin.gd` 是纯 `EditorPlugin`：只做 `add_context_menu_plugin` / `add_inspector_plugin` / `add_export_plugin`，并在 `_init` 里注册翻译和设置 | ✅ 已验证（读 `plugin.gd` 原文；对照 `project.godot` 现有 `[autoload]` 段） |
| **ProjectSettings** | ✅ **只加 1 条**：`yard/updates/channel`（枚举 `Stable` / `Prerelease`，默认 `Stable`）。`register_settings()` 只在不存在时写入，**不覆盖**你已有的值 | ✅ 已验证（读 `editor_only/classes/yard_settings.gd` 原文，`SETTINGS` 数组里只有这一项） |
| **编辑器 UI** | Godot < 4.8 → **顶部主屏幕多一个 "Registry" 标签页**（`_has_main_screen()` 返回 true，挂到 `EditorInterface.get_editor_main_screen()`）；Godot ≥ 4.8 → 改用新的 `EditorDock` API。**4.7.2 走前者** | ✅ 已验证（读 `plugin.gd` 原文 + `compat.is_engine_version_equal_or_newer(4, 8)` 分支） |
| **注册表 `.tres`** | 放哪由你定（FileSystem 右键 → Create New → Registry File…）。**建议单独开一个目录**，理由见第四节 | ✅ |
| **版本号** | `plugin.cfg` 里 `version="1.3.0"`（main 分支），但最新 release 是 **`v1.3.1`（2026-10-08）**，在 `release/1.3` 分支。**想要 1.3.1 就用 release 的 zip，别直接 clone main** | ✅ 已验证（`plugin.cfg` 原文 + releases API） |

### 3.2 有没有"必须写 GDScript 才能用"的部分？

**没有。** 分开说：

- **运行时**：全走 C#（`YARD.Registry<T>`）。✅ 已验证（实测编译）
- **编辑器**：YARD 的表格式编辑器、设置对话框、下拉，全是插件自带的 GDScript——**你不需要写**，只需要点。
- **但是**：项目里**一定会混入 GDScript**——**实测 v1.3.1 是 62 个 `.gd` + 63 个 `.uid`**。这是"混 GDScript"的真实含义（不是 200 个，我先前估多了，这里按实测改）：
  - 对 C# 编译**无影响**（GDScript 不参与 `dotnet build`）。
  - **git 体积 / 文件数**增加（+173 个文件、+939 KB）；`.uid` 文件必须一起提交（YARD 自己就为漏提交 `Registry.cs.uid` 修过一次 bug —— CHANGELOG：`Add missing uid file for Registry.cs wrapper`）。
  - 卸载 = 删掉 `addons/yard/` 一个目录，**零残留**（唯一残留是 `project.godot` 里那行 `yard/updates/channel`，手工删即可）。

### 3.3 它要求 4.4+ 的什么专有特性？4.7.2 上有已知问题吗？

**(a) 4.4 是"地板"，不是"依赖某个 4.4 独有 API"** ✅ 已验证

`addons/yard/editor_only/classes/compat.gd` 原文写得很清楚——它是**向前兼容**的：

```gdscript
## Returns the UID string for a resource path (works on Godot 4.4 and >= 4.5).
static func path_to_uid(path: String) -> String:
	if is_engine_version_equal_or_newer(4, 5):
		return ResourceUID.call(&"path_to_uid", path)      # 4.5+ 的 API
	else:
		var uid: int = ResourceLoader.get_resource_uid(path)   # 4.4 的退路
		return ResourceUID.id_to_text(uid) if uid != -1 else path
```
即 `ResourceUID.path_to_uid` / `uid_to_path` / `ensure_path` 是 **4.5 才有**的，YARD 在 4.4 上退回旧 API。`plugin.gd` 里还有 **4.8 分支**。**所以不存在"绑死在 4.4 某个 API 上"的问题。**

（旁证：v1.3.0 发布说明标题就是 **"### Godot 4.4 to 4.8 — YARD now runs on more Godot versions in both directions. The minimum drops from 4.5 to 4.4 (#143), and YARD already runs on the Godot 4.8 dev snapshots."** ✅）

**(b) 4.7.2 兼容性：官方明确声明支持** ✅

Asset Store 页面底部原文：

> **Minimum Godot Version: 4.4**
> **Maximum Godot Version: 4.8.x**

而且它的兼容性选择器里**明确列出了 `Godot 4.7.2`**（列表含 4.4 / 4.4.1 / 4.5 / 4.5.1 / 4.5.2 / 4.6 / 4.6.1 / 4.6.2 / 4.6.3 / **4.7** / **4.7.1** / **4.7.2** / 4.8）。
✅ 已验证（读 Asset Store 页原文 <https://store.godotengine.org/asset/elliotfontaine/yard-yet-another-resource-database/>）

**这比 `Edit Resources as Table` 强得多** —— 后者**没有任何 4.7 声明**，最新实测只到 4.5.1（见 [config-tooling.md](config-tooling.md) A1 节）。

**(c) ⚠️ 但 4.7 上有真实的事故记录，必须知道**

| issue | 状态 | 环境 | 内容 |
|---|---|---|---|
| **#95** | 🔴 **OPEN**（2026-06-24 开，2026-09-28 被 **reopen**） | **Godot 4.7.stable** | 标题 "Upgrading from 1.1.0 to 1.2.0 broke the registries"。报告者升级插件后**两个注册表的数据被清空**，原话："my two registries with all the settings and resources I've previously created were wiped out. I can see from the commit that bumped the version, **most of the lines of code from the two tres files were deleted**" |
| **#104** | closed（2026-10-02） | Godot 4.6.1 | "Godot UID cache corruption prevents some registries from opening"。注册表 `.tres` 里的 UID 与 Godot 的 `ResourceUID` 缓存不一致 → 注册表打不开；作者原话可"**通过清空 `.godot/` 缓存手动修复**" |
| **#122** | closed（2026-09-27） | **Godot 4.7 stable** | "Moving a base script for a type reverts all string IDs to filenames" —— 移动类脚本会让 string ID **全部退回文件名**，进而打断所有 `@export_custom` 下拉 |
| **#124** | closed | Godot 4.7 | `RegistryLoadTracker` 里 string_id / uid 键用混，调 `Progress` / `Status` 报错。**已修** |
| **#43** | closed（当天） | **Godot 4.6.1 Mono** | "Getting several errors on latest refactor … Fails on Godot project startup"。报告者正是 C# 侧贡献者，当天修好 |

✅ 已验证（GitHub issues 搜索 API：`repo:elliotfontaine/yard-godot+C%23`、`repo:elliotfontaine/yard-godot+4.7`，读 issue 正文原文）

> **怎么读这张表**：#122 / #124 / #104 都已修（多数落在 v1.3.0）；**#95 至今 open 且被 reopen，而且它的核心是"注册表数据被清空"**。
> **结论：4.7.2 被官方声明支持，且 4.7 上确实有人在用、报了 bug 也修了；但"注册表文件 + Godot UID 系统"这个组合出过数据丢失级事故 —— 用它就必须上 git、勤提交、升级插件前先提交。**

### 3.4 ⚠️ 另外两件必须告诉用户的事

**(1) 编辑器启动会联网** ✅ 已验证
v1.3.0 发布说明原文：**"When the editor starts, YARD checks GitHub for a newer release, and the version button in the top bar shows whether one is available."**
- ⚠️ **未确认能否完全关闭**：发布说明只提到可以选 **Stable / Prerelease** 渠道；`yard_settings.gd` 的 `SETTINGS` 里也只有 `yard/updates/channel` 一项，**没有开关**。想关只能改插件代码或断网。

**(2) C# 侧的"数组列下拉"确实缺失**（重复强调，因为这是本文最可能被误读的一条）
- `StringName` / `string` 下拉：✅ **能用**
- `Array[StringName]` / `Array[string]` 多选下拉：❌ **不能**（原因见 1.4）
- **但这不是"YARD 的 C# 支持不完整"** —— 这是 **Godot C# 源码生成器**的行为（它无条件把数组的 hint 覆盖为 `TypeString`），YARD 无能为力，任何依赖自定义 hint 的插件在 C# 里都会遇到同一堵墙。

---

## 四、关键问题 4：与 `Edit Resources as Table` 的对比结论

> 前提：我已通读 [config-tooling.md](config-tooling.md) 全文（600 行，结论是推荐 `Edit Resources as Table`，下称 **ERAT**），下述 ERAT 的能力与坑都引自它已核实的记录，我不重复论证。

### 4.1 先纠正一个框定问题：两者不是同一层

| | ERAT | YARD |
|---|---|---|
| **心智模型** | **一个文件夹 = 一张表，一行 = 一个 `.tres`** | **一份注册表 `.tres` = 一张"名单"**（只存字符串 ID ↔ UID），列表是它的视图 |
| **它解决的核心问题** | **批量编辑**（表格手感） | **身份与关系**（稳定 ID、表间引用、运行时按需加载） |
| **看家本领** | 多选单元格一次改、数字列批量乘/加、CSV 导入导出、Undo/Redo、把 Resource 列单独开成子表 | `@export_custom` Inspector 下拉、属性索引（不加载资源就能筛选）、`load_entry` / `load_all_blocking` / `load_all_threaded_request` |

**所以"YARD 比 ERAT 更好还是更差"这个问法本身是错的。** 正确问法是："**在 C# 项目里，YARD 比 ERAT 多给了什么、少给了什么？**"

### 4.2 在 **C# 项目** 里逐项对比

| 维度 | ERAT | YARD | 谁赢 |
|---|---|---|---|
| **C# 资源类当列来源** | ✅ 有 C# 用户一手实测：issue #72 原话 **"It works with C# Scripts."**（[config-tooling.md](config-tooling.md) §A1 已核实） | ✅ v1.1.0 "fully supported" + v1.2.0 "first-class citizens" + 自带 `Registry.cs` | 平（都能用） |
| **Godot 4.7.2** | ⚠️ **无任何声明**，最新实测 4.5.1（2025-11） | ✅ **Asset Store 声明 4.4 – 4.8.x，列表含 4.7.2** | **YARD 赢** |
| **批量编辑（多选/乘加）** | ✅ **头号能力** | ❌ **没有**。CHANGELOG 全文搜不到批量乘/加；Asset Store 一条 5 星评价（Sacha Lasry，2026-08-01）明确把它列为缺失：**"Only features that are missing for me: - The ability to edit multiple rows of data at once"** | **ERAT 赢** |
| **表与表互相指向** | ⚠️ 只有 `[Export] Resource` 引用（Inspector 里拖资源） | ✅ **下拉选 string ID**（标量版在 C# 可用） | **YARD 赢** |
| **运行时查询 / 按 ID 加载 / 线程加载 / 不加载就能筛选** | ❌ 完全没有 | ✅ `YARD.Registry<T>` 全套（**实测编译通过**） | **YARD 赢** |
| **CSV 导入导出** | ✅（但数组/字典不支持，issue #61/#72） | ❌ 无（靠"扫描目录"从文件系统同步） | **ERAT 赢** |
| **嵌套资源展平** | ✅ 把 Resource 列单独开成另一张表 | ✅ v1.3.0 "Sub-resource tables (#151)" 可逐层下钻 + 面包屑 | 平 |
| **文件改名/搬家抗性** | ⚠️ 表是按文件夹扫的，改名要重扫 | ✅ **这正是 YARD 的设计目的**（string ID 稳定，底层用 UID） | **YARD 赢** |
| **维护活跃度** | master 到 2026-01-27 | 最新 release **2026-10-08**（352 stars / 26 forks / 13 open issues） | **YARD 赢** |
| **装完对项目的侵入** | 纯 GDScript 插件，删目录即净 | 纯 GDScript 插件 + **`Registry.cs` 进你的 C# 编译**（`Nullable=enable` 下 **+1 条 CS8618 警告**）+ **+1 条 ProjectSettings** + **编辑器启动联网** | **ERAT 略赢** |
| **本项目既有约定（`.tres` 不写 `uid=`）** | 不涉及 | ⚠️ 注册表**以 UID 为核心**（存 `uid://…` 字符串；已出过 #104 那类 UID 缓存事故） | **ERAT 赢** |

### 4.3 "如果 `@export_custom` 在 C# 里用不了，YARD 的优势是不是就没了？"

**这个前提不成立 —— 它用得了。** 见第一节：
- `StringName` / `string` **能**（我实测了 4.7.2 生成的 `PropertyInfo`，`hint=1024` 完整保留）
- 只有 `Array[StringName]` / `Array[string]` **不能**

**因此准确回答是：YARD 的主要优势保留下来了，损失的是"数组列的一栏多选"这一块。**

而且**对你的具体需求，损失几乎为零**：

| 需求原话 | 需要的字段形态 | C# 可用？ |
|---|---|---|
| "武器表的某一行要指向属于它的升级池" | 单个 `StringName` | ✅ |
| "池表里一个选项占一栏，每个选项要指向它的执行逻辑" | 单个 `StringName` | ✅ |

**"一个选项占一栏"天然就是一个标量字段**，不落在数组那个缺口上。所以你可以放心：**这个项目的用法，YARD 的 `@export_custom` 完全够用**。

> 反过来讲：如果当初 YARD **真的**在 C# 里完全用不了 `@export_custom`，那它的优势确实会塌掉大半 —— 因为"运行时按 ID 加载"你自己写 50 行 C# 也能做到，而"Inspector 下拉帮你不出错地建立表间引用"是**只有插件能给**的那部分。**幸运的是这没发生。**

### 4.4 能不能两个都用？—— **可以，但必须做目录隔离**

**结论：能共存，但要遵守一条硬约定。**

**为什么不冲突** ✅（结构性事实，逐条已核实）：
1. **两者都是纯 GDScript 的 `EditorPlugin`**，注册的是不同的编辑器 UI：ERAT 是一个 `ResourceTables` 标签页；YARD 是顶部主屏幕的 `Registry` 标签页（4.7.2 上）。**插件注册层面无交集。**
2. **它们读写的是不同的文件**：
   - ERAT 读写"**行资源** `.tres`"（`WeaponDef` 的 `Damage` 这类字段）
   - YARD 读写"**注册表** `.tres`"（ID↔UID 映射 + 属性索引）
   - **互不覆盖。** ✅
3. YARD 的注册表**不会**为每个条目生成 `ext_resource`——它把 `uid://…` 当**字符串数据**存进 `Dictionary[StringName, StringName]`（`registry.gd` 的 `_uids_to_string_ids`；`registry_io.gd` 的 `add_entry` 用 `Compat.path_to_uid(...)`）。所以它不碰行资源文件。✅ 已验证（读 `registry.gd` + `registry_io.gd` 原文）

**⚠️ 唯一真实的互相干扰（必须规避）**：
> **注册表本身也是一个 `.tres`（`Registry` 资源）。ERAT 的规则是"文件夹里所有 Resource 都是表里一行"——所以如果你的注册表文件躺在 ERAT 扫描的那个文件夹里，ERAT 会把它当成表中一行**，你能在表格里编辑它的 `_uids_to_string_ids` / `_property_index` 等 `@export_storage` 内部字段。**这是能造成真实损坏的路径。**

**规避方法（一条就够）**：
```
Config/
  Registries/            ← YARD 的注册表 .tres 放这里，ERAT 不扫这个目录
    upgrade_pools.tres
    weapons.tres
  Tables/                ← ERAT 扫这里
    Weapons/  wpn_pistol.tres  wpn_rifle.tres
    Pools/    pool_common.tres
```
> ⚠️ **声明**：这一条是**基于两者各自规则推导**的（ERAT 的规则引自 [config-tooling.md](config-tooling.md) 的已核实 feature 列表；YARD 的规则来自我读的源码）。**ERAT 目前没有装，所以这个干扰我没有实测。** 现实情况是 YARD **已经装上了**（见开头"现场状态更新"），所以顺序变成了：**装 ERAT 之前先把注册表目录定下来**（`Config/Registries/`），再让它去扫 `Config/Tables/`。

**⚠️ 另一条**：ERAT 有"首装整张表空白"的已知 bug（重开项目即好，见 [config-tooling.md](config-tooling.md) §A1），YARD 有 UID 类 bug（§3.3）。两个插件的故障表现都包含"表看起来是空的"，**叠加时你会分不清是谁的问题**。这也是"先各自单独冒烟"的理由。

### 4.5 注册表 `.tres` 与本项目既有约定的冲突（一个需要你决策的点）

[config-tooling.md](config-tooling.md) §6 转述了 `assets.md` §5 的既有约定：**"`ext_resource` 只写 `path=`，不写 `uid=`"**。

- YARD 的注册表**没有字面违反**这条（它不为条目生成 `ext_resource`）。✅
- **但它的整个设计建立在 Godot 的 UID 系统上**：条目键是 `uid://…`，每次加条目都 `Compat.path_to_uid(...)`。**这正是 `assets.md` 想避免的那类耦合**，也是 §3.3 里 #104 那类 UID 缓存事故的土壤。⚠️ **需要你和项目约定做决策。**
- **另一条卫生问题**：注册表里的 `_uids_to_string_ids` 和 `_property_index` 都是**烘焙进去的派生数据**——`rebuild_property_index()` 的最后一句就是 `ResourceSaver.save(registry)`。所以：
  - **每次点 Reindex 都会重写这个 `.tres`**，git diff 会动；
  - 它**不是"手写数据"，更像"带生成物的清单"**。
  - **建议**：把注册表当**生成物**看待（可随时重建），别在里面手工塞东西；Reindex 和业务改动**分提交**，避免 diff 噪音。✅ 已验证（读 `registry_io.gd` 的 `rebuild_property_index` / `add_entry` / `erase_entry`，每处都以 `ResourceSaver.save(registry)` 结尾）

### 4.6 与 [config-tooling.md](config-tooling.md) 结论的关系

**不冲突，是补充。** [config-tooling.md](config-tooling.md) 的**第 0 步（把 `ConfigIndexData` 那张 ~100 列的宽表拆成"一类一个 `[GlobalClass]` 类、一行一个 `.tres`"）是本文所有结论的入场券**：
- **不拆表 → ERAT 无事可做**（没有"表"）
- **不拆表 → YARD 也无事可做**（没有行资源可以登记进注册表）
→ **两者对前提的要求完全一样。所以先做第 0 步，这件事没有分歧。**

---

## 五、⭐ 给用户的直白建议

> **⚠️ 前提已变，先读这句**：**YARD v1.3.1 已经在项目里了**（见开头"现场状态更新"，不是我装的）。所以下面第 1 条不再是"要不要装"，而是"**装了之后怎么定位它**"。

**1. 别把 YARD 当批量编辑器用 —— 它明确没有这个能力，请把"批量改数值"继续交给 `Edit Resources as Table`。** 你原始需求的关键词是"**在这些表项目进行快速的编辑**"，而 YARD 恰好**没有**多选/批量编辑——这不是我猜的，是它自己 5 星用户在 Asset Store 上的原话："missing for me: **the ability to edit multiple rows of data at once**"。ERAT 的批量能力是它的招牌，而 YARD 的招牌（下拉 + 运行时查询）你现在**还吃不到**，因为项目里"表"还不存在（[config-tooling.md](config-tooling.md) 第 0 步的拆表还没做）。

**2. YARD 绝不是"用不了"被否掉，是"用在别的地方"。** 它在 C# 里**能用**：有官方 C# 包装类（我把它在本机 4.7.2 上**实测编译通过**，lambda 谓词也能传）、标量 `StringName` 的 Inspector 下拉**实测能拿到 hint=1024**、Asset Store **明确声明支持 4.7.2**（比 ERAT 只到 4.5.1 实测强得多）。它该干的活是**注册表 + 表间按稳定 ID 互相指向 + 运行时按需加载**，正好是你需求里"**表与表之间要能互相指向**"那句。

**3. 既然它已经装上了，接下来做两件事（都很便宜）：** ① **立刻跑一次 `build.ps1 -v minimal`** —— 预计会多出 1 条 `addons/yard/Registry.cs(50,9): warning CS8618`，破了你们 0-warning 的线，按 §2.4(1) 三选一处理；② **在编辑器里做一次真机冒烟**（这是本文唯一"代码层验证过、编辑器层没验证"的点）——新建注册表 → 指向扫描目录 → 在 C# 类上写 `[Export((PropertyHint)1024, "res://Config/Registries/xxx.tres")] public StringName` → 编译 → **确认 Inspector 真的出下拉**，并用 `git diff` 看一眼注册表 `.tres` 长什么样（它有烘焙的派生数据，每次 Reindex 都会重写）。

**4. 你的两个具体场景，YARD 在 C# 里都落在"能用"的形态上。** "武器行指向升级池"、"池表一栏一个选项指向执行逻辑" —— **一个引用 = 一个标量 `StringName`**，正好是 C# 里 hint=1024 能透传的那个形态；唯一用不了的是"一栏放多个选项"的数组列。所以只要按这个约定设计字段，`@export_custom` 的优势**没有丢**。

**5. 想加 ERAT 的话，先把注册表目录定下来再装。** 两者技术上能共存（不同插件、不同文件），但**注册表 `.tres` 必须和 ERAT 扫描的行资源分目录放**（`Config/Registries/` vs `Config/Tables/`），否则 ERAT 会把注册表当成表里一行、让你有机会编辑它的内部字段。另外 YARD 有两个需要你接受的事实：**编辑器一启动就联网检查 GitHub 更新**（我没找到关闭开关），以及**它历史上出过注册表数据被清空的 issue（#95，至今 open）**——用它就勤提交。

---

## 六、验证账目

### ✅ 实际读取 / 实际执行的（已验证）

| # | 来源 | 用途 |
|---|---|---|
| 1 | `F:\Godot项目\项目a\docs\config-tooling.md`（全文 600 行） | 对比基线 |
| 2 | `F:\Godot项目\项目a\project.godot`、`ActRoguelike.csproj`、`.godot/` 目录 | 确认 4.7.2 / C# / `Nullable=enable` / 现有 autoload |
| 3 | <https://raw.githubusercontent.com/elliotfontaine/yard-godot/main/addons/yard/README.md> | 官方用法与 feature 列表 |
| 4 | `…/addons/yard/registry.gd`（全文源码） | `class_name Registry`、`PROPERTY_HINT_CUSTOM=1024`、`filter`/`where`/`load_*` 签名 |
| 5 | `…/addons/yard/Registry.cs`（全文源码，15428 B）+ 本机**实测编译** | C# 运行时 API |
| 6 | `…/addons/yard/plugin.gd`、`plugin.cfg` | 无 autoload、4.8 分支、version 1.3.0 |
| 7 | `…/addons/yard/editor_only/editor_inspector_plugin.gd`（全文） | 下拉的 hint 判定逻辑 |
| 8 | `…/addons/yard/editor_only/registry_io.gd`（全文） | 注册表以 UID 为核心、Reindex 会重写文件 |
| 9 | `…/addons/yard/editor_only/classes/compat.gd`（全文） | 4.4/4.5/4.8 兼容分支 |
| 10 | `…/addons/yard/editor_only/classes/yard_settings.gd`（全文） | 只加 1 条 ProjectSettings |
| 11 | GitHub tree API `…/git/trees/main?recursive=1` | 全量文件清单（含 `Registry.cs`） |
| 12 | GitHub API：repo 元数据 / `releases` / `search/issues?q=…+C%23` / `search/issues?q=…+4.7` | 版本、C# 支持沿革、4.7 相关 issue |
| 13 | <https://raw.githubusercontent.com/elliotfontaine/yard-godot/main/CHANGELOG.md>（全文） | feature 沿革、批量编辑能力的有无 |
| 14 | Godot 源码 `modules/mono/editor/Godot.NET.Sdk/Godot.SourceGenerators/ScriptPropertiesGenerator.cs`（全文） | **hint 透传 / 数组被覆盖** 的机制 |
| 15 | Godot 官方文档 RST：`c_sharp_exports.rst`、`gdscript_exports.rst`（全文） | `@export_custom` 语义、C# 无等价属性 |
| 16 | **本机反射** `godotsharp 4.7.2 / GodotSharp.dll` | Attribute 完整清单、`ExportAttribute` 构造函数、`PropertyHint` 为 Int64、`Max=45`、`Callable.From` 重载、`Script.GetScriptConstantMap`、`GodotObject.Call` |
| 17 | **本机实测编译**（临时目录探针，`Godot.NET.Sdk/4.7.2` + `net8.0`）：dump Roslyn 生成的 `*_ScriptProperties.generated.cs` | **`hint=1024`（StringName/string）与 `hint=23`（Array[StringName]）的直接证据** |
| 18 | 同上，把 `Registry.cs` 放进 `addons/yard/` + 写全 API 调用（含 `Callable.From` lambda） | C# 运行时 API 可编译、`addons/**` 会被自动编译、`CS8618` 警告 |
| 19 | <https://store.godotengine.org/asset/elliotfontaine/yard-yet-another-resource-database/> | **Min 4.4 / Max 4.8.x，兼容列表含 4.7.2**；用户评价原文 |
| 20 | **项目内安装副本** `F:\Godot项目\项目a\addons\yard\`（173 个文件） | 发现"已安装"；实测文件数 / 体积；确认第 50 行即 `CS8618` 位置 |
| 21 | **SHA256 比对**：安装副本 vs 上游 `v1.3.1` tag（8 个关键文件）vs 上游 `main`（`Registry.cs`） | **8/8 `IDENTICAL`**；`Registry.cs` 三处哈希一致 = `EF54A5A399A108D6A86910693EB228A28178CA14ED3E47BD47ACECC465396C20` ⇒ **我编译验证用的文件与项目里现在这个完全相同** |
| 22 | `F:\Godot项目\项目a\ActRoguelike.csproj` + `.gitignore` | 无 `Exclude`/`Compile Remove` ⇒ `Registry.cs` 必被编译；`obj/`、`.godot/` 已忽略 |
| 23 | `git -C 项目a status --porcelain` / `diff -- project.godot` | 确认 YARD 安装事实、`editor_plugins` 那一行改动，以及**我没有改动任何项目文件** |

### ⚠️ 未验证 / 推断（已标注）

1. **引擎层未经实机确认**：本机**没有安装 Godot 编辑器可执行文件**（我在 `C:\`、`D:\`、`E:\`、`F:\`、`G:\` 逐层搜索 `Godot*.exe`，以及 `%APPDATA%\Godot`、`PATH`、`build.ps1`，都没找到）。所以 **"`hint=1024` 在 Inspector 里真的长出下拉框"这一步没在编辑器里肉眼验证过**。依据链是：源码生成器已实测输出 `hint=1024` + GDScript 官方文档明确"引擎不校验 hint" + YARD 自己的 `_parse_property` 只看 `hint == 1024`。
2. **`CS8618` 警告没有在项目本体上跑构建确认。** 我**没有**执行 `build.ps1` / `dotnet build`（避免改动项目、也避免与正在同时改 `Scripts/Config/*.cs` 的另一个 agent 打架）。依据是：探针项目用**同一个 SDK 版本、同一个 `<Nullable>enable</Nullable>`、逐字节相同的 `Registry.cs`** 复现出了该警告，且项目 csproj 没有排除 `addons/`。**建议由用户/主 agent 跑一次 `build.ps1 -v minimal` 定案。**
3. **`Callable.From((Variant v) => …)` 谓词分支没有实机跑过**（只验证了能编译）。形参必须用 `Variant` 这一点是从两边源码对照推出的。
4. **"ERAT 会把注册表当成一行"没有实测**（ERAT 目前没装）。是基于 ERAT 已核实的"文件夹 = 表"规则 + YARD 注册表是 `.tres` 推导的。
5. **YARD 能否完全关闭编辑器启动时的联网更新检查 —— 查不到。** 发布说明只提到可切 `Stable`/`Prerelease` 渠道；`yard_settings.gd` 里也只有 `yard/updates/channel` 一项，**没有开关**。没有实测断网行为。
6. **没有找到任何"C# 项目 + `@export_custom` 下拉"的第三方实测报告。** 我搜了 issue（`C%23` 命中 12 条）和 21 条 Asset Store 评价，**没有人提到 C# 场景下的下拉**。ERAT 那边有 C# 用户原话，YARD 这边只有官方"first-class citizens"的声明。这是本文最值得你自己冒烟一次的点。
7. **"数组列在 C# 里确实不出下拉"没有在编辑器里确认**（是从生成代码 `hint=23` 推出来的，而 `hint != 1024` 会直接 `return false`，逻辑上必然）。

### ❌ 明确查不到的

- **C# 版的 `[ExportCustom]` / 任何官方"自定义 hint"属性** —— **不存在**。我反射了 4.7.2 全部 Attribute 类型，并读了官方 C# 导出文档全文。
- **YARD 官方 README / Asset Store 页面里对 C# 用法的任何说明** —— **完全没有**。README 只有 GDScript 示例；`Registry.cs` 的存在**只能从源码仓库里发现**（README 一字未提）。这是它文档的一个明显缺口。
- **YARD 有没有 batch / multi-cell / multiply 编辑能力** —— CHANGELOG 全文与 Asset Store 页面**都搜不到**，且有 5 星用户明确说这是缺失功能。

---

*本文只回答"YARD 在 C# 项目里能不能用"。工具选型主结论见 [config-tooling.md](config-tooling.md)；表字段设计见 [config-schema.md](config-schema.md)；数值分层与"唯一入口"约定见 [config.md](config.md)；`.tres` 格式规矩见 [assets.md](assets.md) §5。*
