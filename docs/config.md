# 数值配置（调参唯一入口）

> ⚠️ **2026-10-15 P6a 清旧之后的当前状态**：旧 3 角色 / 3 武器 / 5 条旧技能行 / 17 条旧增幅 /
> 3 个旧升级池 / 2 张旧能力卡**已删除**；`BaseScreen`（旧「永久强化」页）**整份删除**，
> 局外成长台现在开**能力卡界面**；「左键归谁」由**武器行**的 `WeaponConfig.UsesWeaponComponent` 决定
> （不再有 `CharacterConfig.WeaponEnabled`）。
>
> ⚠️ **2026-10-16 P6b 之后**（见 [character-redesign.md](character-redesign.md) §五）：
> **弹匣/装填整条横向清理** —— 删 3 个行文件（`card_magazine` / `card_reload` / `meta_reload`）、
> `Weapon` 的装填逻辑与 HUD 弹药显示、`reload` 输入动作、`AmmoOnKill` handler；
> 新增 2 条通用异化（`mut_stomp` 杀戮践踏 / `mut_homing_missile` 巡飞弹）+ 1 条效果行
> （`eff_missile_beacon` 飞弹信标）。
> **索引现在是 `6/2/10/1/1/39/7/14/8/5/6/11/4`**
> （P6a 之后是 `6/2/10/1/1/39/7/13/8/6/6/11/6`；差额：效果 +1 / 局外 −1 / 能力卡 −2）。
> **增幅项 59 条**（强化 9 / 进化 23 / 异化 27；P6a 之后是 57 = 9/23/25）。
> 本文件下面大量读数（枪手 / 手枪 / 旧武器 / 池 `pool_pistol` / 玩家 270/40/7 …）都是**当时**的实测记录，
> **保留作历史、不逐条改写**；要看当前值与每一处差额的来由，见
> [character-redesign.md](character-redesign.md) §六。

> **`ConfigIndex`（原 `Balance`）不是"平衡参数表"** —— 它是**全局标量 + 17 张表的索引**。数值本体在各表的行文件里。
> 换句话说：它**只回答"数值在哪"**（`Settings.Xxx` 指向哪一行），不回答"数值是多少"（那是 `Config/Tables/<分组>/*.tres` 里一行一个文件的事）。
> 所以看见 `ConfigIndex.PlayerMaxHealth` 这类名字时不要以为数值写死在这里 —— 那只是**表读不到时的代码默认值**。

**所有可调数值拆成 17 张表，全部挂在 [`Config/ConfigIndex.tres`](../Config/ConfigIndex.tres) 上；运行期由 autoload `ConfigIndex` 读取并建索引。**

> **2026-10 配置表阶段 8（能力卡 = 局外装备层）**：新增第 17 张表 **能力卡**
> （`Config/Tables/AbilityCards/card_*.tres`，**一行 = 一张卡**，现有 8 行）+ 局外成长表多一行
> **`meta_quota`（额度扩增）** + `ConfigIndexData` 多两个字段（`AbilityCards` 索引数组、`AbilityQuotaBase` 基础额度）。
> ⚠️ **能力卡与 `Skills/`（局内单位技能，25 行）是两层不同的东西**，所以是**两张表**而不是一张 ——
> 判断依据写在 [ability-cards.md](ability-cards.md) §1，唯一的接触点是 `AbilityCardConfig.Skill` 那一列（资源引用）。
> 这一轮顺带把 `配置表比对·索引` 的期望值从 `…/局外 5/…` 改成 `…/局外 6/…/能力卡 8`（加表的必然结果，其余 11 个数字逐位不变）。
> 细节、实测与截图见 [ability-cards.md](ability-cards.md) 与本文末「配置表阶段 8 实测」。

> **2026-10 配置表阶段 2（重要变化）**：`ConfigIndex` 从"唯一数值**文件**"变成"唯一数值**门面 + 表索引**"。
> 业务代码**仍然写 `ConfigIndex.Settings.Xxx`**（这条约定没变），但数值的实际家在 `Config/Tables/<分组>/` 里，
> **一行一个 `.tres` 文件**。字段定义见 `Scripts/Config/*.cs`，缺字段/缺表都会回退到代码默认值。
>
> **2026-10 配置表阶段 3（单位表 + 技能表）**：新增 **单位表基类 `UnitConfig`**（攻击/生命/移速/持有技能/行为&类/表现）
> 与 **技能表 `AbilityConfig`**；敌人 9 个敌种 + 首领 + 召唤物各成一行；白名单从"按中文显示名"改成"按 Id"。
> 细节与实测见文末「配置表阶段 3」一节。
>
> **2026-10 配置表阶段 4（武器 → 升级池 → 选项 的三层关联）**：新增第 13 张表 **升级池**
> （`Config/Tables/UpgradePools/pool_*.tres`，一行一个池）；武器行多一列 **`Pool`**（正向指向池），
> 增幅项行多一列 **`Pool`**（一个选项占一行 + 这个引用列 = "池成员"的唯一真源），
> 并**删掉了 `PoolTags` 死字段、把 `ConflictsWith` 接上了**。细节与实测见文末「配置表阶段 4」一节。
>
> **2026-10 配置表阶段 6（首领增强的"节点驱动抽奖"，关卡核心文档 §7）**：**Pressure 转换池真的接线了**。
> `PressureModConfig` 多一列 **`Tags`**、`Weight` 真的按权重抽；`NodeConfig` 的三个空壳字段
> （`BossPressurePoints` / `BossModifierRolls` / `BossModifierTags`）全部接进 `StageController.AddPressureForNode`；
> **两个抽取点彻底分开** —— Boss 开场词条只抽行为型（Boss Build），Pressure 转换只抽数值型（§7.5）。
> 细节、实测与"随机序列为什么可以漂"见文末「配置表阶段 6」一节。
>
> **2026-10 配置表阶段 5（AI 行为表）**：新增第 14 张表 **AI 行为档案**
> （`Config/Tables/AiProfiles/ai_*.tres`，**一行一个 AI 档案** = 一个单位的整套 AI 参数）；
> 单位表（`UnitConfig`）多一列 **`AiProfile`**（**资源引用**，指向档案行）。
> 以前"让所有敌人更激进"要开 10 个场景改 30 处，现在在 `AiProfiles/` 这一张表里按列排序/批量改即可。
> 细节、实测与两次回退验证见文末「配置表阶段 5」一节。
>
> **2026-10 用户三项修正（本轮）**：① **Pressure 池里的「铁壁·护甲」整行下线**（用户：「护甲目前就只给
> 精英怪使用就好了，首领不需要」）→ 池子 **8 → 7 条**，护甲的**唯一**来源现在是精英怪的 `EliteModifier`；
> ② **术语修正**（用户：「行为型的词条其实不太对，这个应该是算是技能」）→ **行为型 = 技能、数值型 = 词条**，
> 界面/日志/文档统一（`◆` / `◇` 两个标记不变，改成"计数带名词"）；
> ③ **命中特效改到实际命中点**（用户：「实际应该在命中的位置发生特效」）→ `AttackHitbox` 用
> **目标碰撞体表面最近点**取代"目标原点 + 全局高度偏移"，`HitEffectHeightOffset` 这一列因此删除。
> 细节、判据与两次连跑的实测见文末「用户三项修正实测」。

## 调参去哪张表

| 要调什么 | 打开哪个目录 | 一行是什么 | 行数 |
|---|---|---|---|
| **敌人（按敌种）**：生命/护甲/移速/攻击四项/标签/持有技能/刷怪成本与权重 | `Config/Tables/Enemies/enemy_*.tres` | 一个敌种 | **10**（1 行全局倍率 + 9 个敌种） |
| **AI 行为（按敌种/首领/召唤物）**：索敌半径、目标刷新间隔、每个行为节点的**优先级与参数**（追击距离 / 舒适圈 / 撤退阈值 / 技能槽开关） | `Config/Tables/AiProfiles/ai_*.tres` | 一个单位的整套 AI 参数 | **11**（9 敌种 + 首领 + 无人机） |
| **首领**：生命/护甲/移速/攻击四项/持有技能/白板副本血量 | `Config/Tables/Bosses/boss_final.tres` | 一个首领 | 1 |
| **召唤物**：数值 + 存活时间 | `Config/Tables/Summons/summon_drone.tres` | 一个召唤物 | 1 |
| **技能**：冷却/前摇/后摇/朝向/开关/接近区间 + 子类专属参数 + 命中体参数 | `Config/Tables/Skills/skl_*.tres` | 一个技能 | **25**（5 玩家 + 16 单位 + 4 首领行为技能） |
| **首领数值强化池（Pressure = 界面上的「词条」）**：权重 / **候选标签** / 改哪些属性 / **技能参数** / **限定词**（只对哪个 Boss、哪几关） | `Config/Tables/Pressure/pmod_*.tres` | 一条数值强化 = **一个参数栏** | **7**（4 数值型参数栏 + 3 技能参数栏） |
| 玩家基础数值与攻击组 | `Config/Tables/Characters/chr_*.tres` | 一个角色 | 3 |
| 武器数值（伤害/射速/弹匣/装填/散布/**击退**/暴击/弹速/**升级池**） | `Config/Tables/Weapons/wpn_*.tres` | 一把武器 | 3 |
| **升级池**（"某把武器的升级池"这个粒度；武器表正向指过来） | `Config/Tables/UpgradePools/pool_*.tres` | 一个池 | 3 |
| 效果参数（燃烧 dps / 减速 / 眩晕 / 爆炸 / 吸血 / 召唤上限 / 连锁闪电） | `Config/Tables/Effects/eff_*.tres` | 一个效果 | 9 |
| 奖励节点（标题 / 层 / 波数 / 商店价目 / **给多少 Pressure、额外抽几次、候选标签**） | `Config/Tables/Nodes/node_*.tres` | 一种节点 | **8**（C2 合并了两个进化池节点行，见「节点表」一节） |
| 增幅项（19 条强化·进化·异化，含"属于哪个升级池"一列） | `Config/Tables/Upgrades/*.tres` | 一条增幅 | 19 |
| 关卡（预算 / 波次 / 敌人白名单 / Boss 词条数 / Pressure 规则） | `Config/Tables/Stages/stg_*.tres` | 一关 | 3 |
| 局外成长（碎片价格 / 每级效果） | `Config/Tables/Meta/meta_*.tres` | 一项 | **6**（5 项永久强化 + **1 项能力卡额度扩增** `meta_quota`） |
| **能力卡（局外装备层）**：解锁价 / 各级升级价 / **占用几点额度** / 等级上限 / 图标 / 说明 / **激活后的效果** | `Config/Tables/AbilityCards/card_*.tres` | **一张卡** | **8**（6 张数值 + 2 张授予技能）｜详见 [ability-cards.md](ability-cards.md) |
| **全局开关**（敌人/Boss 倍率、节奏、Boss 压力曲线、伤害跳字、血条、节点间距、区域杀高度、**能力卡基础额度**） | **`Config/ConfigIndex.tres`**（还在原地） | — | 31 个字段 |

- **改完要重新运行游戏才生效**（数值在开局一次性应用，不热重载）。
- 两种改法都行：Godot 编辑器里双击 `.tres` 用 Inspector 改（有中文分组），或直接用文本编辑器改。
- 字段的中文注释在对应的行类里：`Scripts/Config/{UnitConfig,EnemyConfig,BossConfig,SummonConfig,AbilityConfig,AiProfileConfig,PressureModConfig,CharacterConfig,WeaponConfig,EffectConfig,NodeConfig,RewardConfig,MetaUpgradeConfig,StageDefinition}.cs`。
- **单位表是继承关系**：`EnemyConfig` / `BossConfig` / `SummonConfig` / `CharacterConfig` 都继承 `UnitConfig`，
  所以"生命 / 攻击四项 / 移速 / 持有技能 / 标签 / **AI 档案** / 表现 / **动画**"这八组列在所有单位表里长得一样，只有各自的私有列不同。
  **攻击四项是整个单位表通用的一组**（伤害/间隔/范围/击退，套到该单位的**主攻击**上）：
  `AttackDamage` / `AttackInterval`（→ 能力 `Cooldown`）/ `AttackRange`（按能力子类依次试
  `AttackRange`→`TriggerRadius`→`MaxRange`→`MaxDistance`）/ `AttackKnockback`。**留 0 = 不覆盖**。
- **动画那一组是 6 列**（本轮新增，套到 `UnitAnimator` 上）：`AnimIdle` / `AnimWalk` / `AnimRun` / `AnimDeath`
  （动画名，**留空 = 不覆盖**）+ `AnimWalkMinSpeed` / `AnimRunMinSpeed`（走/跑的速度阈值，**留 0 = 不覆盖**）。
  **一行都不填也能动** —— 走组件默认值（`idle` / `walk` / `sprint` / `die`，阈值 0.15 / 4）。

> ⚠️ **操作纪律（血泪教训）**：不要单独删/改 `ConfigIndex.tres` 引用到的行文件。
> 只要引用的**任何一个**行文件缺失/改名，Godot 会**整份 `ConfigIndex.tres` 加载失败**（不是只丢那一行），
> 于是**所有**数值静默回到代码默认值（实测：玩家生命 250 → 140、武器变成 22/0.32/8/1.15）。
> 要下线一行，就把 `ConfigIndex.tres` 里的数组项与顶部对应的 `[ext_resource]` 一起删。
> 详见 [config-schema.md](config-schema.md) §8.3。

## 参数中文说明（表里每个参数项怎么看懂）

**需求**：表里每个参数项都要有中文说明，显示在**右侧 Inspector（检查面板）**上或**悬停**时，**且不改变原变量名/参数名**。

### 30 秒结论：**Godot 4.7 的 C# 导出没有 tooltip 机制，悬停显示做不到**

先调研再动手，结论是**做不到"Inspector 悬停弹出中文说明"**。三条独立证据（都在本机 4.7.2 上验过）：

| # | 证据 | 说明 |
|---|---|---|
| ① | **本地反射 `GodotSharp.dll`（程序集版本 `4.7.2.0`）**：`Godot.ExportAttribute` 的**全部**成员只有构造函数 `ExportAttribute(PropertyHint hint, string hintString)` + 属性 `Hint` / `HintString` | **没有任何** tooltip / description / doc 参数。`ExportGroupAttribute(string name, string prefix)`、`ExportSubgroupAttribute`、`ExportCategoryAttribute(string name)` 同样只有一个字符串 —— 那个字符串**就是**分组标题，所以它能显示中文 |
| ② | **引擎源码 `editor/inspector/editor_inspector.cpp`（`_update_property_tooltip` 的构建处）**：<br>`// Build the doc hint, to use as tooltip.`<br>`Vector<DocData::ClassDoc> docs = scr->get_documentation();`<br>`doc_tooltip_text = "property\|" + classname + "\|" + propname;` | 悬停提示的文本是一个**文档符号**（`property\|类名\|属性名`），由 `EditorHelpBitTooltip` 去**文档数据库**里查（引擎类 XML 文档 + 脚本文档）。它**完全不读 `hint_string`**，也不读任何 C# 特性 |
| ③ | **`modules/mono/csharp_script.h:301-307`**：`get_doc_class_name()` 与 `get_documentation()` 是**空的 `// TODO`**（`return StringName();` / 返回空 `Vector`）；而 `modules/gdscript/gdscript.h:288` 的 GDScript 版本 `return docs;` 是**真的**从 `##` 注释解析出来的 | **C# 的 XML 文档注释永远进不了编辑器**（既不是 Help、也不是 Inspector tooltip）。侧证：本地反射整个 `GodotSharp` API，唯一带 "Documentation" 的成员是 `ScriptExtension._GetDocumentation` / `ScriptLanguageExtension._SupportsDocumentation`（GDExtension 扩展点），`Godot.Script` 自己**没有任何** `GetDocumentation` |

补充两条**实测**：

- **`PropertyHint` 里没有能显示说明文字的取值**（本地 4.7.2 全量 46 个：`None`/`Range`/`Enum`/…/`MultilineText`(18)/`PlaceholderText`(20)/`ToolButton`(39)/`FilePath`(44)/`Max`(45)）——
  没有"只读说明行"这种 hint。
- **GDScript 侧也一样没有**：本机跑 `Godot_v4.7.2-stable_mono_win64.exe --headless --check-only --script`，
  `@export_tooltip("…")` 直接 **`Parse Error: Unrecognized annotation: "@export_tooltip"`**（同一套壳里只有 `@export` 的对照文件 exit=0，证明测试本身有效）。
  （2020 年那个 PR #41033 "export variable tooltips" 并没有进入 4.x 的 `@export` 语法。）

> ⚠️ 所以：**不要**去硬造一个"悬停 tooltip"（比如写 `EditorInspectorPlugin` 去给每个属性挂 `TooltipText`）。
> 那需要自己解析 C# 源码里的 `/// <summary>`，是把注释变成第二套真源，得不偿失。

### 实际采用的方案：`[ExportGroup("中文分组 · 一句话说明")]` + 每个字段的中文 XML 注释

两条路一起走，**都不改变量名/参数名**：

1. **`[ExportGroup("中文")]` —— Inspector 里真的能看到中文**（这就是"在右边的检查面板上显示"）。
   分组的字符串是**唯一**能被 C# 特性送进 Inspector 的可见文本，所以每个字段都必须落在某个中文标题下面
   （**没有落在任何 `[ExportGroup]` 里的字段会挤进上一个分组的标题下**，那是错的 —— 本轮顺手修了
   `ConfigIndexData` 里 `MinNodeSpacing`/`PressurePerPoint`/`KillPlaneY` 等挤在「表现 · 血条」下的问题）。
2. **每个字段一句中文 `/// <summary>`** —— IDE（Rider / VS / VS Code）里鼠标停上去就能看，
   读代码时就在手边；虽然 Godot Inspector 不显示它（证据 ③），但它对人是有效的"悬停说明"。

> **覆盖范围（用脚本数出来的实数，不是估的）**：`Scripts/Config/` 下 **18 个**会被人编辑的配置/资源类全部覆盖 ——
> `ConfigRow`（基类，它的两个分组会出现在**每一张表**的行文件上）/ `UnitConfig` / `EnemyConfig` / `BossConfig` /
> `SummonConfig` / `CharacterConfig` / `WeaponConfig` / `AbilityConfig` / `AiProfileConfig` / `EffectConfig` /
> `NodeConfig` / `RewardConfig` / `MetaUpgradeConfig` / `StageDefinition` / `UpgradePoolConfig` / `ParamEntry` /
> `StatOverride` / `ConfigIndexData`（全局字段）。并行代理同期把 `PressureModConfig` 改成了同一套写法（风格一致）。
>
> | 指标 | 数量 |
> |---|---|
> | `[ExportGroup("中文…")]` | **75 个**（`ConfigIndexData` 22 个、`ConfigRow` 基类 2 个） |
> | `[Export]` 参数项 | **218 个** |
> | 其中带紧邻中文 `/// <summary>` 的 | **218 个（100%）** |
>
> **逐类的真实分组**（headless 探针读 `get_property_list()` 打的，就是 Inspector 消费的那份元数据；
> 括号里是不含 Godot 自带的那个类名分组）：
> `ConfigIndex.tres` 22 组 · `enemy_chaser.tres` 11 组 · `chr_gunner.tres` 11 组 · `boss_final.tres` 10 组 ·
> `summon_drone.tres` 10 组 · `ai_chaser.tres` 9 组 · `node_shop.tres` 7 组 · `skl_dash.tres` 6 组 ·
> `eff_burn.tres` 5 组 · `wpn_pistol.tres` 5 组 · `upg_dmg_up.tres` 5 组 · `stg_outer.tres` 5 组 ·
> `meta_health.tres` 4 组 · `pool_pistol.tres` 2 组。
>
> **红线遵守**：本次只加/改 `[ExportGroup]` 特性行与 `/// <summary>` 注释行 ——
> 用 `git diff -U0` 核对过，`Scripts/Config/**` 里**没有一行 `[Export]` 属性签名被改动**
> （字段名 / 类型 / 默认值一个都没动），也没有动 `Config/Tables/**` 的任何数据。
> 唯一的字段"搬动"是 `ConfigIndexData.BossBarFullRange` 从文件末尾移到「表现 · 血条」组里（名字/类型/默认值不变），
> 因为它原来挂在错误的组标题下面；同时给 `MinNodeSpacing` / `PressurePerPoint` / `KillPlaneY` 等十个
> "关卡约束"字段补了它们自己的「节点 / 关卡约束」标题（原来它们挤在「表现 · 血条」底下，标题是错的）。
>
> 截图：[config_zh_inspector_groups.png](screenshots/config_zh_inspector_groups.png) —— 编辑器右侧
> Inspector 打开一个行文件时的分组标题。其中 `ConfigRow` 那一节的「身份与文案（所有表通用）」与
> 「额外键值对（逃生口：没有独立列的参数走这里）」就是本方案为**基类**加的两个分组，
> **每一张表的每一行都会有它们**。

顺带说明**两列本来就有、可以直接写中文**的地方：`ConfigRow.Note` / `DescriptionTemplate`
（都是 `PropertyHint.MultilineText`，一行一条中文备注；`DescriptionTemplate` 还支持 `{字段名}` 占位符，见 `ConfigText`）。

> ✅ **`{字段名:格式}` 也支持了（2026-10 修）**：`{MaxHealth:0}` / `{FireInterval:0.00}` / `{PerLevel:0}` 里的格式串会交给
> .NET 数字格式化（**只对数字字段生效**，字符串/布尔字段忽略冒号后的部分；格式串写错退回默认写法、不报警告）。
> 在此之前它是**静默失效**的：整段 `MaxHealth:0` 被当字段名 → 玩家界面上原样打出 `{MaxHealth:0}` + 一条 WARNING。
> 细节与证据见 [config-schema.md](config-schema.md) §4.4.1。

## 各表参数（当前取值）

### 角色表 `Config/Tables/Characters/`（3 行）

> ⚠️ **"护甲"这一列现在是 `StartingArmor`（开局护甲），不是"最大护甲"**（2026-10-12 用户第三轮 C1：
> 「护甲没有上限额度，所以可以删除掉 `MaxArmor`」）。同名列在 `UnitConfig` / `Unit` 上已改名，
> 数值一个都没动；4 个带甲的敌人场景（`Guard` / `Charger` / `SkillElite` / `TrainingDummy`）里的导出一并改名。
> 详见文末「护甲无上限 + 进化池合并实测」§一。

| 行 | 生命 | 护甲 | 移速 | 减伤 | 武器 | 能力开关 | **攻击组**（伤害/间隔/范围/击退） | **跳跃**（起跳速度 / 重力缩放，固定值） |
|---|---|---|---|---|---|---|---|---|
| `chr_gunner`（槽 1） | 250 | 40 | 7.0 | 0 | ✅ | 关 Melee/Summon/Control | 32 / 0.75 / 2.4 / 5 | **13.0 / 3.6** |
| `chr_heavy`（槽 2） | 380 | 140 | 6.2 | 12% | ❌ | 开 Melee，关 Summon/Control | 32 / 0.75 / 2.4 / 5 | **13.0 / 3.6** |
| `chr_summoner`（槽 3） | 210 | 60 | 6.8 | 0 | ✅ | 开 Summon/Control，关 Melee | 32 / 0.75 / 2.4 / 5 | **13.0 / 3.6** |

角色表**继承 `UnitConfig`**，所以"攻击组"就是**左键近战重击的四个数**（原先散在 `ConfigIndex.tres` 的
`HeavyMeleeDamage` 与 `Player.tscn` 的命中体/能力上，现在合成一处）：伤害 32 / 间隔 0.75 / 范围 2.4 / 击退 5。
"持有技能"列指向技能表的 5 行（`skl_dash` / `skl_burst` / `skl_melee` / `skl_summon` / `skl_control`）——
**技能自己的前摇/后摇/朝向/命中体细节在技能行里，而主攻击的四个数由角色行说了算**（技能行的 `Cooldown` 留空 = 不覆盖）。

`EnabledAbilityIds` / `DisabledAbilityIds` 里写的是**能力脚本类名**（`MeleeAttackAbility` 等）——
矩阵按 `ability.GetType().Name` 比对（技能表落地后 `Ability.AbilityId` 会变成技能行的 Id，用类名在"表在/表不在"两种情况下都成立）。
近战角色会**关闭枪械**（`Weapon.Enabled = false`），左键改由近战能力接管（`PlayerWeaponInput` 里分流）；
近战需要一个「范围内的目标」才会出招，够不着就不挥空。

### 敌人表 `Config/Tables/Enemies/`（10 行 = 1 行全局倍率 + 9 个敌种）

| 行 | 生命 | 护甲 | 移速 | 攻击组（伤害/间隔/范围/击退） | 成本/权重 | 精英率 | 标签 |
|---|---|---|---|---|---|---|---|
| `enemy_chaser` | 60 | 0 | 4.2 | 12 / 1.3 / 2.3 / 3 | 2 / 3.0 | — | Melee, Chaser |
| `enemy_fast_melee` | 35 | 0 | 6.5 | 7 / 0.8 / 2.1 / 2 | 1 / 2.5 | — | Melee, Fast, Chaser |
| `enemy_exploder` | 25 | 0 | 5.5 | 22 / 30 / 2.4 / 5 | 1 / 1.5 | — | Suicide, AreaDenial, Chaser |
| `enemy_jumping_melee` | 50 | 0 | 3.6 | 14 / 2.2 / 0（跳跃类没有射程属性）/ 4 | 2 / 2.0 | — | Melee, Diver |
| `enemy_ranged` | 45 | 0 | 3.4 | 8 / 1.8 / 18 / 1.2 | 2 / 1.6 | — | Ranged, Harasser |
| `enemy_charger` | 90 | 30 | 3.2 | 18 / 2.6 / 14 / 8 | 3 / 1.6 | 0.25 | Melee, Bruiser, Charger |
| `enemy_screamer` | 70 | 0 | 3.0 | 6 / 4.0 / 7 / 6 | 2 / 1.2 | — | Controller, AreaDenial |
| `enemy_guard` | 130 | 60 | 2.6 | 14 / 1.6 / 2.4 / 3 | 3 / 1.2 | 0.2 | Melee, Guard, Tank |
| `enemy_skill_elite` | 200 | 90 | 2.9 | 12 / 1.6 / 2.5 / 3 | （护卫，不走生成项） | — | Elite, Support, Melee |

- **生成项与行按"预制体路径"配对**（`Main.tscn` 的 `SpawnXxx` ↔ 行里的 `ScenePath`），
  所以改中文显示名**不再**影响任何逻辑；行的 `DisplayName` 会覆盖生成项的显示名（只影响界面与日志）。
- **每行都有"持有技能"**指向 `Config/Tables/Skills/skl_enemy_*.tres`（技能行里放着前摇/后摇/命中体参数）。
- 敌人的 `Unit` 数值仍然是"表优先、场景兜底"：行的数值在**进场景树之前**套用（生成时"先 ApplyTo、再 AddChild"）；
  读不到行时单位保留 `Scenes/Enemies/*.tscn` 里的值。
- `enemy_global` 那一行是**全局倍率行**（`IsGlobal = true`）：生命 ×0.75 / 伤害 ×0.3 / 移速 ×1 / 精英率 ×1。

### 首领 / 召唤物 / 技能 / Pressure（阶段 3 新增）

| 表 | 行 | 关键内容 |
|---|---|---|
| `Config/Tables/Bosses/boss_final.tres` | 1 | 生命 900 / **护甲 0**（用户要求「去掉首领的甲」；护甲列仍留在 `Unit` / `UnitConfig` 基类上 —— C1 之后叫 `StartingArmor`，只是首领没有任何获取手段 —— 见文末「首领去甲 + 单位动画实测」）/ 移速 3.6 / 攻击 24·1.8·3.4·6 / 标签 Boss,Melee；**持有技能 4 行**（`skl_boss_melee` / `skl_boss_scream` / `skl_boss_charge` / `skl_boss_summon`）；`EliteCopyHealthScale = 0.35`（白板首领副本的血量倍率） |
| `Config/Tables/Summons/summon_drone.tres` | 1 | 生命 40 / 移速 6.0 / 攻击 6·0.9·1.8·1.5 / 标签 Summon / `LifetimeSeconds = 25` |
| `Config/Tables/Skills/skl_*.tres` | 21 | 技能：`NodeName`（目标能力节点）+ 通用项（Aim/Cooldown/Windup/Recover/Enabled/Approach*）+ `Params`（子类专属，Key = 属性名）+ `AttackParams`（命中体/武器组件） |
| `Config/Tables/Skills/bhv_*.tres` | 4 | **首领行为技能**（Boss Build）：`ApplyHandler` 非空的技能行，由 `BossPerkCatalog` 编译成运行期条目。**界面上叫「技能」**（用户拍板：行为型的那些本来就是技能，不是"词条"） |
| `Config/Tables/Pressure/pmod_*.tres` | **7** | **首领数值强化池**（Pressure，**界面上叫「词条」**）：**一栏一个参数** —— 狂暴·伤害(3/Offense) / 狂暴·攻速(3/Offense) / 铁壁·生命(2/Defense) / 疾行(2/Mobility) / 尖啸回响(1/Offense,Skill) / 冲锋冲击(1/Offense,Skill) / 召唤急速(1/Defense,Skill)。前 4 条是 `StatOverride` 单位属性，后 3 条是**技能参数**（`AbilityScales` / `AttackScales`，见下）。括号里是 **`Weight` / `Tags`**。⚠️ **原「铁壁·护甲」(+150 甲) 已按用户要求整行删除**（「护甲目前就只给精英怪使用就好了，首领不需要」） |

**⚠️ 首领强化的两套来源必须能分开（关卡核心文档 §7.1）**：
行为/技能型在**技能表**里（`ApplyHandler` 非空的那 4 行 = 界面上的**技能**），数值型在 **Pressure 表**里（7 行 = 界面上的**词条**）。
两张表分别编译、各自独立回退（一侧读不到不会让另一侧变空），开局日志会分开打条数：
`首领强化来源（关卡核心文档 §7.1：两套来源必须能分开）：数值型 Pressure（**词条**）7 条（来源：表 Pressure）/ 行为型 Boss Build（**技能**，用户拍板：行为型的那些本来就是技能）4 条（来源：表 Skills）`。

**配置表阶段 6 起，两个抽取点也彻底分开了**（这是 §7.1/§7.5 的硬要求）：

| 抽取点 | 候选 | 谁触发 | 代码入口 |
|---|---|---|---|
| **Boss 开场技能**（Boss Build） | **只有行为型 4 条** | Boss 出生时，技能数 = `StageDefinition.BossPerkCount` | `BossPerkCatalog.Roll(count)` |
| **Pressure 转换**（§7.5 数值池） | **只有数值型 7 条**（按 `Tags` 过滤 + 按 `Weight` 加权） | 每跨过一次 Pressure 阈值**立即**抽一次（**抽签**不判限定词） | `BossPerkCatalog.RollPressure(tags)` |
| **限定词判定**（阶段 7） | 上面抽到的那一条 | **Boss 出生时**（两条路都要经过 `perk.Apply`） | `BossPerkCatalog.ApplyWithRefresh` |

⚠️ 两套来源因此在代码里可以**独立配置、独立追踪、独立结算**（§7.5 最后一句）。
**没有**为 Pressure 设计"可被玩家增幅修改"的接口（§7.2 明令禁止）。
⚠️ **行为变化**：开场技能的候选集从"数值型 + 行为型合并的 7 条"变成"只有行为型 4 条" ——
这是文档要求的变化（不是回归）；数值型改由 Pressure 转换产出。详见文末「配置表阶段 6」。
⚠️ **术语（用户 2026-10 修正）**：**行为型 = 技能、数值型 = 词条**，两者不能再统称"词条"
（行为型的那 4 行本来就是技能表里的行）。界面上 `◆` = 技能、`◇` = 词条。见文末「用户三项修正实测」。

**技能表的两个约定**（避免两个真源）：
1. **单位的"主攻击"四个数以单位行为准**（`ApplyAttackTo` 在技能行之后套用）。
   所以 `skl_melee` / `skl_enemy_*_melee` 这类**主攻击的技能行把 `Cooldown` 留空（0 = 不覆盖）**，
   间隔由单位行的 `AttackInterval` 给。
2. **命中体/武器组件的参数写在技能行的 `AttackParams` 里**（`Damage` / `Knockback` / `StatusId` /
   `StatusDuration` / `AttackTags` / `FireInterval` / 弹速 / 弹丸预制体…）；只有**主攻击的伤害与击退**
   由单位行的攻击组统一给。这条规则让"改文案/改标签"与"改数值"各归其位。

### 武器表 `Config/Tables/Weapons/`（3 行）

| 行 | 伤害 | 间隔 | 弹匣 | 装填 | 散布 | **击退** | 穿透 | 连发 | **升级池** |
|---|---|---|---|---|---|---|---|---|---|
| `wpn_pistol`（槽 1） | 24 | 0.26 | 12 | 0.7 | 0.5 | **2.5** | 0 | ❌ | `pool_pistol` |
| `wpn_sniper`（槽 2） | 60 | 0.95 | 5 | 1.4 | 0.2 | **4.0** | 2 | ❌ | `pool_sniper` |
| `wpn_rifle`（槽 3） | 13 | 0.12 | 30 | 1.2 | 1.6 | **1.2** | 0 | ✅ | `pool_rifle` |

三把武器**共用同一个 `Pistol.tscn` 节点**，只换数值（`RunController.EquipWeapon` → `WeaponConfig.ApplyTo`），
所以不会重建节点、不会弄丢挂在武器下的输入组件。

**升级池列（阶段 4 新增）**：`WeaponConfig.Pool` **正向指向** `Config/Tables/UpgradePools/pool_*.tres`
的一行；套表时写进 `Weapon.PoolId`，奖励池用它做"精确池"匹配（三级回退的第一层）。
**池行不反过来指武器** —— 详见下面的「配置表阶段 4」。

> ℹ️ 分表前狙击/步枪的暴击率、暴击倍率、弹速、预瞄 FOV、被动装填延迟**全部借用 `Pistol*` 的值**；
> 分表后它们各自有独立字段，当前**默认值仍与手枪相同**（0.15 / 2.0 / 90 / 55 / 3），所以行为没变。
> 要按武器区分，改行文件即可。

### 火力倾泻（角色 1 技能，按武器分两种模式）

| 武器 | 行为 |
|---|---|
| 手枪 / 激光狙击枪（非连发） | 无视当前弹药，**直接打出一整个弹匣**（1/12 也能打满 12 发），随后自动装填 |
| 步枪（连发） | **持续 2.5 秒自动连射且不消耗子弹**（弹匣 0/N 也能用），到时停止 |

判定依据是武器表的 `Automatic`，以后新增连发武器会自动走第二种。

### 效果表 `Config/Tables/Effects/`（9 行）—— 用户点名要的"效果参数"

> 🔧 **2026-10 阶段 3 的清理**：原 `eff_knockback.tres` 与 4 列（`Value` / `Value2` / `Radius` / `TickSeconds`）
> **全项目 0 消费者**（真正生效的是增幅项行 `upg_*` / `mut_*` 的 `Params`）——
> 它们只是"把 `Rewards/` 里的数抄一份供人看"的镜像。按"不留让人误以为能调的列"这条纪律，
> **这 4 列已删除**，`eff_knockback` 那一行（删完只剩注释）整行删除。
> `EffectConfig` 现在只保留真正被读的列：`Kind` / `Duration`（第二优先）/ `Modifiers` / `DamagePerSecond`。

| 行 Id | 类型 | 数值 | 原来在哪 |
|---|---|---|---|
| `Slow` | 减速 | 移速 ×0.55 | `StatusEffect.cs` switch（硬编码） |
| `Weaken` | 虚弱 | 伤害 ×0.75 | 同上 |
| **`Burn`** | 持续伤害 | **6 伤害/秒** | 同上（用户点名的"燃烧值"） |
| `Empowered` | 增益 | 攻速 ×1.3 + 伤害 ×1.15 | 同上 |
| `Stun` | 眩晕 | 移速 ×0.12 + 攻速 ×0.5 | 同上 |
| `Explosion` | 爆炸 | （数值在 `upg_evo_blast` 的 `Params`） | `RewardCatalog` 闭包 |
| `Lifesteal` | 吸血 | （数值在 `mut_lifesteal` 的 `Params`） | 同上 |
| `SummonOnKill` | 击杀召唤 | （上限在 `mut_summon` 的 `Params`） | 同上 |
| `ChainLightning` | 连锁闪电 | （数值在 `mut_chain` 的 `Params`：14 伤害 / 7 范围 / 3 跳 / 0.12 冷却） | 同上 |

> ⚠️ **"击退值"在哪，别找错地方**：**每个单位实际给多少击退**在
> ① **武器表**的 `Knockback`（手枪 2.5 / 狙击 4 / 步枪 1.2）；
> ② **单位表**的 `AttackKnockback`（敌人的攻击组，例如守卫 3 / 冲锋兵 8）；
> ③ **技能行**的 `AttackParams → Knockback`（技能自己的命中体，例如玩家控制立场 0 / 首领冲锋 10）。
> 全局倍率那个旋钮（原来在 `eff_knockback`）**尚未实现**，所以没有留在表里当摆设。

### 节点表 `Config/Tables/Nodes/`（**8** 行，2026-10-12 C2 合并后）

| 行 | 节点类型 | 层 | 波数 | 价值标签（→ 波数由标签表给） | 奖励过滤 | 界面标题 |
|---|---|---|---|---|---|---|
| `node_enhance` | Enhancement | 1 | 3 | `tag_mid` | 强化 | 强化池 |
| `node_evolution` | Evolution | 2 | 4 | `tag_high` | 进化 | **进化池**（唯一的一行；`Kind = Evolution`） |
| `node_mutation` | Mutation | 2 | 4 | `tag_high` | 异化 | 异化池 |
| `node_resource` | Resource | 0 | 1 | `tag_low` | 不过滤 | 局外资源 |
| `node_heal` | Heal | 0 | 1 | `tag_low` | 不过滤 | 恢复状态 |
| `node_special` | Special | 0 | 1 | `tag_low` | 不过滤 | 特殊奖励 |
| `node_shop` | Shop | 1 | 3 | `tag_mid` | 不过滤 | 补给站 |
| `node_portal` | Portal | 0 | 1 | `tag_low` | 不过滤 | 降临节点（不触发战斗） |

> ⚠️ **`node_weapon` 那一行已整行删除（2026-10-12 用户第三轮 C2）**：它和 `node_evolution` 的
> `Title`/`DisplayName`/`RewardKind`/`Layer`/`WaveCount`/`ValueTags` **逐列相同**，唯一区别是
> `Kind = Weapon`（Slot 1）—— 而用户明确说了「**并没有"放哪个进化池节点"的说法**」，即**不区分多个进化池节点**。
> 所以两行合并成 `node_evolution` 一行：`Id` / `Kind` / `Slot` **都没有改名**（C2 要求不动类与目录），
> 场景 `Main.tscn` 里本来就只有 `Kind = Evolution` 的实例、一个 `Kind = Weapon` 的实例都没有。
> 「按攻击方式分的限定池」落在 `Config/Tables/UpgradePools/pool_pistol|sniper|rifle.tres` 三行上（**保留不动**）——
> 那才是"进化池的选项由角色持有的攻击方式（武器）决定"这条链路的落点。
> 排序/索引不受影响（`ConfigIndex.tres` 的 `Nodes` 数组删掉一项、`load_steps` 同步减一）。
> ⚠️ **`Kind = Weapon` 的价值层兜底改成 2**（`NodePlacement.LayerOf`）：表里再没有 `Kind = Weapon` 的行之后，
> 这个分支就是它唯一的价值层来源（不加会退化成 Layer 0 / 1 波，与 A3「武器限定池 = 进化池 = 高价值」自相矛盾）；
> 自测里 `Weapon=4` 因此**逐位不变**。
>
> **波数这一列现在只是兜底**：真正生效的波数来自**价值标签表**（下一节），由
> `NodePlacement.WavesFor` 按「标签行 → 本行 `WaveCount` → 按价值层的代码兜底」取值。
> 表里两个数写成一致（4/4/3/3/1…）只是为了"关掉标签表也不会突变"。
> 加一种新奖励（彩蛋之类）时**不用改代码**：给它的节点行挂一个已有标签即可。
>
> 改动由来见文末「本轮五项修正实测 → #7 节点波数按价值标签」。

`node_shop` 还带三项价目：**恢复 5 / 强化 12 / 进化 18 碎片**（花局外碎片买局内收益）。
碎片不足时不会扣款、也不会产生收益，但**依然会触发本场 Encounter**（节点 = 一次收益 + 一次挑战）。

### 价值标签表 `Config/Tables/ValueTags/`（3 行，2026-10 新增）

**这张表是"节点开几波"的唯一驱动者**（取代原来按 `RewardNodeKind` 枚举硬编码的 `Layer` 开关）；
**2026-10 第八轮起，它同时是"节点脚下那圈光圈什么颜色"的唯一驱动者**（用户：「加入一些周身特效来做信息展示的，
比如**光圈**，**光圈颜色来表示价值程度**」）：

| 行 | 标签 | 波数 | **光圈颜色 `AuraColor`** | 谁挂它 |
|---|---|---|---|---|
| `tag_high` | 高价值 | **4** | **金** `(1, 0.75, 0.15)` | `node_evolution` / `node_mutation`（Layer 2）· 代码兜底里 `Kind = Weapon` 也按高价值算（A3：武器限定池 = 进化池） |
| `tag_mid` | 中价值 | **3** | **青蓝** `(0.15, 0.72, 1)` | `node_enhance` / `node_shop`（Layer 1） |
| `tag_low` | 低价值 | **1** | **绿** `(0.4, 0.86, 0.45)` | `node_resource` / `node_heal` / `node_special` / `node_portal`（Layer 0） |

- 索引：`ConfigIndex.tres` 的 `[Export] ValueTagConfig[] ValueTags`（元素指向上面三个行文件）。
- 消费链只有一处：`NodePlacement.WavesFor(kind)` ——
  ① 节点行 `ValueTags` 命中的标签行取 `WaveCount`（多个标签命中时取**最大**）→
  ② 节点行自己的 `WaveCount` → ③ 按 `Layer` 的代码兜底（2→4 / 1→3 / 其它→1）。
- **光圈颜色（第八轮新增列）**：`ValueTagConfig.AuraColor` → `RewardNode.AuraColorFor(kind)`，
  取值链与上面**完全同一条**（节点行 `ValueTags` → 标签行，多标签取 `WaveCount` 最大）。
  - **规格**：材质 `Unshaded`（不参与光照 → 亮地板与暗天空下颜色一致）+ HDR 亮度（> 场地 glow 阈值 2.0 才会带光晕）；
    外圈另有一条固定**暗描边**（"亮色 + 黑描边"的思路，白地板上靠它画出边界）。见 [art-onboarding.md](art-onboarding.md) §三.1.3。
  - **三档只靠色相区分**（金 / 青蓝 / 绿），**别只改明度** —— 明度差在近白地板上几乎看不出来。
  - 读不到（节点行没有 / 标签拼错）→ 用中性白 `(0.85, 0.9, 1)`，并在日志里说一声（有节点行却解析不出标签才报 warning）。
    ⚠️ **`Kind = Weapon` 是中性白**：它没有节点表行（C2 已并入进化池），这是**刻意**的，不是漏配。
  - 开局日志：`[Node] 奖励节点光圈颜色：Evolution → 价值标签「tag_high」（4 波）的 AuraColor (1, 0.75, 0.15, 1)（来源：表 ValueTags）`
    （每种类型一条，只报一次）。
- 节点行挂了一个**拼错的标签**时，`ConfigIndex.ReportValueTags()` 会写 warning + 日志（不静默落到下一层）：
  `⚠ 节点行 X 的价值标签「Y」在 ValueTags 表里找不到。`
- 开局日志会同时打出**标签定了几波**与**每个节点实际算出几波**：
  `价值标签（节点波数来源）：3 行（来源：表 ValueTags）—— [tag_high→4 波、tag_mid→3 波、tag_low→1 波]`
  `节点波数（高 4 / 中 3 / 低 1）：node_evolution（tag_high）4 波 · node_mutation（tag_high）4 波 · …`
  （2026-10-12 C2 之前这一串里还有 `node_weapon（tag_high）4 波`；那一行已合并进 `node_evolution`。）
  ⚠️ `node_evolution` 自 2026-10-12（用户拍板 A3，C2 起它也是**唯一**的进化池行）挂 `tag_high`、`Layer = 2`、**4 波** ——
  它就是**进化池**（用户：「没有定义武器池，只是一个说法，确切来说是进化池…进化池算高价值，因为攻击方式的改动巨大」）。
  `Config/Tables/UpgradePools/` 那 3 行（`pool_pistol` / `pool_sniper` / `pool_rifle`）只有身份、**没有价值标签列**（C2 明令保留），
  所以价值标签这件事落在**节点行**这一侧（价值标签本来就是 `NodeConfig.ValueTags` 的列）。

### 奖励池的候选集是什么时候定的（2026-10-12 用户第三轮 C2）

用户原话：「**进化池里会出什么选项是根据交互的角色持有的攻击方式（武器）**……而**池里一开始有什么，
应该在进入关卡初始化的时候就判断好的**」。

| 问题 | 答案 |
|---|---|
| 候选集是**什么时候**算的 | **进关卡初始化时**（`RunController.ApplyBalance` → `InitializePool()`，即 `BeginRunWithCharacter` 里"角色/武器都定了"的那一刻）；`InitializePool` 之前它是**四个交互点里的惰性分支**（谁先被交互谁建池） |
| 交互时还算不算 | **只做"从定好的池里抽"**：按节点类型（`Kind` 过滤）+ 当前构筑（互斥 / 层数上限 / `ConflictsWith` / 角色限定）筛一遍再加权抽 —— 这几条**天然必须每次交互算**（它们依赖"你现在已经有什么"），但**候选项的来源集合**不再变 |
| 池内容由什么决定 | ① **武器**：`RewardPool.Initialize(weapon)` 的三层（专属池 `Pool` → 武器标签 `RequiredWeaponTags` → 通用）；② **角色**：`SummonerAllowed`（`RequiresSummoner` 的召唤类奖励） |
| 顺手修的 bug | 原来四个懒建分支里只有两个设了 `SummonerAllowed` —— 如果某局第一个被交互的是**特殊奖励**节点，召唤控制角色整局抽不到「亡者随从」。现在初始化只有一条路，两层一起定 |
| 留痕 | 开局日志 `[Reward] 奖励池按当前角色/武器定下来了（进关卡初始化）：角色 Gunner · 武器 Pistol（PoolId pool_pistol）→ 候选 19 条（武器层之后；角色限定项在抽取时再筛 SummonerAllowed=False）`。⚠️ 这里 19 = **武器层过滤后**的候选数（含只在召唤角色才进抽取的「亡者随从」），枪手**实际可抽** 18 条（`docs/rewards.md` 的硬指标） |

### 增幅项表 `Config/Tables/Upgrades/`（19 行）

10 条强化 + 4 条进化 + 5 条异化，数值与分表前**逐条一致**。
每条行的 `ApplyHandler` 决定用哪个 C# 处理器（`Scripts/Rewards/RewardEffectRegistry.cs`）：

| handler | 条数 | 说明 |
|---|---|---|
| `StatSet` | 14 | 纯数据：改 `StatSheet` 的某项（数值在 `Modifiers` 里） |
| `ChainLightning` / `Lifesteal` / `BurnOnHit` / `SummonOnKill` / `AmmoOnKill` | 各 1 | 需要一行 C#（加新机制仍要写一行代码，见 [config-schema.md](config-schema.md) §6.1） |

**阶段 4 改动（19 行的数值一个都没动）**：
- ➕ 新增一列 **`Pool`**（指向 `Config/Tables/UpgradePools/pool_*.tres`）—— 现在**19 行全是空的**，
  空的语义 = "走标签匹配 / 通用两层"，所以**行为与分表前逐位一致**。要加"某把武器专属"的选项就往这一列填。
- ➕ 新增一列 **`ConflictsWith`**（单向互斥：已拥有列表里的 Id → 本项不再进池）。原来这个字段只存在于
  运行期视图上、**表里没有入口**，所以永远为空；现在能填了（默认空 = 行为不变）。
- ➖ **删掉 `PoolTags` 死字段**（原来 19 行都在认真填它，但全项目**没有任何消费者**）。

> ⚠️ `Weight` 目前 19 条**都写成 1.0** → 加权抽取仍退化成等概率（与分表前一致，**故意不改**，改了抽样分布会变）。
> 现在**改得动了**：想调某条的出率就改它的 `Weight` 一行。

### 局外成长表 `Config/Tables/Meta/`（6 行 → **P6b 后 5 行**）

> ⚠️ **P6b**：`meta_reload`（装填速度 +6%/级，存档键 `reload_level`）随用户裁定
> 「**没有弹匣的概念了**」**整行下线**（行文件 + `ConfigIndex` 数组项 + `MetaProgress` 的读写 +
> `RunController` 的 `ReloadSpeedMultiplier` 修饰符一起删）。见 [character-redesign.md](character-redesign.md) §五。

| 行 | 存档键 | 每级效果 | 价格 |
|---|---|---|---|
| `meta_health` | `health_level` | 最大生命 +10 | 10 × (等级+1) |
| `meta_damage` | `damage_level` | 伤害 +5% | 同上 |
| `meta_speed` | `speed_level` | 移速 +3% | 同上 |
| `meta_crit` | `crit_level` | 暴击率 +3% | 同上 |
| ~~`meta_reload`~~ | ~~`reload_level`~~ | **P6b 下线**（无弹匣概念） | — |
| `meta_quota` | `quota_level` ⚠️**不在 meta.cfg** | **能力卡额度上限 +1** | **30 + 20 × 等级** |

> ⚠️ `meta_quota` 是配置表阶段 8 新增的第 6 行，也是"**额度上限本身也能用局外资源扩增**"（需求 §一）的落地形式。
> 它的**等级**存在 `user://ability_cards.cfg` 的 `[quota] quota_level`（由 `AbilityStore` 读写），
> **不在 `user://meta.cfg`**（`MetaProgress` 只认它自己那 5 个 `*_level` 键）；这一行只提供"每级几点 / 每级多少钱"。
> 所以 `SaveKey` / `Stat` 两列对它是"给人看的元数据"，**不要照着它去 `MetaProgress` 里找 `quota_level`**。
> 细节见 [ability-cards.md](ability-cards.md) §2.3。

> ⚠️ **存档兼容红线**：存档键名（`SaveKey`）必须保持 `health_level` 等原值，改了老玩家进度会归零。

## 还留在 `ConfigIndex.tres` 的全局字段

### 敌人 · 全局倍率（`Config/Tables/Enemies/enemy_global.tres` 是**优先来源**，这里是回退值）
| 参数 | 含义 |
|---|---|
| `EnemyHealthMultiplier` | 所有普通敌人生命倍率（**回退值 = 0.75，与表一致**） |
| `EnemyDamageMultiplier` | **所有普通敌人伤害倍率（难度总开关）**（**回退值 = 0.3，与表一致**） |
| `EnemyMoveSpeedMultiplier` | 所有普通敌人移速倍率（1） |
| `EliteChanceMultiplier` | 精英出现概率倍率（1 = 按各节点的配置，0 = 不出精英） |

> 敌人表的 `enemy_global` 行里同名四个值是**优先来源**，读不到才用上面这四个。
> 🔧 **阶段 3 修的静默换挡**：这两个回退值原来是 `1f` / `0.8f`，而表里是 `0.75` / `0.3` ——
> 表一读不到，敌人伤害会**静默 ×2.67 且没有任何日志**。现在两处对齐，并且 `ConfigIndex.ScaleEnemy`
> 第一次套用时会在日志里写一行「敌人倍率：…（来源：表 enemy_global / 代码默认值 ConfigIndexData）」。

### 节点 / 关卡约束
| 参数 | 含义 |
|---|---|
| `MinNodeSpacing` | 节点之间的最小间距（8） |
| `KillPlaneY` | 地图下方的区域杀高度（-8） |
| `NodeHealRatio` | 战斗型回复节点先免费恢复的生命比例（0.3） |
| `PressurePerHighValueNode` / `PressureEvolutionBonus` / `PressureMutationBonus` / `PressurePerPoint` | Pressure 结算的全局默认值（关卡行里填了就用行里的） |
| `FreeChallengeBaseQuota` / `FreeChallengeMaxQuota` | 免费挑战额度（1 / 2） |

> 🔧 **阶段 3 删掉的死字段**：`EliteBossHealthScale`（全项目 0 消费者，生效的是
> `EncounterController` 自己那份场景导出值）→ 搬进 **`Config/Tables/Bosses/boss_final.tres` 的
> `EliteCopyHealthScale`**（行优先、场景导出值兜底）；
> `BossHealthBarScale`（全项目 0 消费者，血条尺寸实际由 `BarBoss.tscn` 的 offsets 决定）→ **直接删除**。

### 节奏 · Stage
| 参数 | 含义 |
|---|---|
| `NodeBudgetMultiplier` | 节点总预算倍率（越大敌人越多、节点越久） |
| `SpawnIntervalMultiplier` | 生成间隔倍率（越大出怪越慢） |

### 节奏 · 关卡内的定期刷怪（需求 #6，2026-10-12 新增：定期刷怪 + 每 120 秒增强）
| 参数 | 含义 | 现值 |
|---|---|---|
| `AmbientSpawnInterval` | 进关后每隔多少秒在**有效位置**刷一只"游离怪"。**0 = 关掉这套机制** | **30** |
| `AmbientSpawnEscalateSeconds` | 每隔多少秒给**之后**刷出的怪加一档强度（用户点名的参数） | **120** |
| `AmbientSpawnStrengthStep` | 每档强度加多少（0.25 = 每档 生命/伤害 ×1.25） | **0.25** |
| `AmbientSpawnMaxAlive` | 场上最多同时几只游离怪（0 = 不限） | **6** |

- 参数在**全局标量**而不是关卡表：这是所有关卡共用的压力节奏（与上面两个同类），按关卡行放会让 3 行各存一份相同的数。
- 落点**复用** `EncounterController.SpawnAmbient` → `SpawnPlanner.FindSpawnPoint` → `MapManager.SpawnPoint`（**没有第二套落点实现**）。
- 游离怪**不扣 Encounter 的预算、不进它的 `AliveCount`/`MaxConcurrent`、不参与清场判定**（否则节点挑战永远结不了算）。
- 细节、实现位置与实测见 [stages.md](stages.md) 的「关卡内的定期刷怪 + 每 120 秒增强」一节。

### Boss
| 参数 | 含义 |
|---|---|
| `BossHealthMultiplier` / `BossDamageMultiplier` | Boss 生命 / 伤害倍率 |
| `BossPressureInterval` | 压力升级间隔（秒） |
| `BossPressureDamagePerLevel` | 每级压力增加的伤害比例 |
| `BossPressureAttackSpeedPerLevel` | 每级压力增加的攻速比例 |

### 成长
| 参数 | 含义 |
|---|---|
| `RewardChoiceCount` | 三选一给几个选项（默认 3） |

### 表现 · 伤害跳字
| 参数 | 含义 |
|---|---|
| `ShowDamageNumbers` | 是否显示伤害数字 |
| `DamageNumberLifetime` / `DamageNumberRise` / `DamageNumberFontSize` | 跳字存活时间 / 上飘距离 / 字号 |

### 表现 · 血条
| 参数 | 含义 |
|---|---|
| `ShowEnemyHealthBars` | 是否显示敌人头顶血条（满血的杂兵不显示，Boss 常显） |
| `EnemyHealthBarWidth` / `EnemyHealthBarHeight` | 血条尺寸 |
| `EnemyHealthBarTopOffset` | 血条相对单位原点的抬升高度 |
| `BossBarFullRange` | 展开完整血条的距离阈值 |

> 🔧 **阶段 3 顺带修的**：`Scenes/UI/Bars/BarEnemy.tscn` 的 `Content` 原来摆在 `(-1,-1)`、尺寸 66×9，
> 即**没有以 Root 原点为中心** —— `WorldOverlay.CreateBar` 每次建血条都会推一条警告「血条位置会偏」。
> 现在改成 `(-33,-4.5)~(33,4.5)`（尺寸不变、中心落在 Root 原点），警告消失。

### 表现 · 命中特效（2026-10 新增：命中 / 击中特效接线）
| 参数 | 含义 |
|---|---|
| `ShowHitEffects` | 命中 / 击中时是否播放命中特效（**一个开关管两条路径**：投射物命中 + 命中体击中） |
| `HitEffectScenePath` | 命中特效预制体（默认 `res://Assets/Effects/fx_combat_hit.tscn` = Binbun Hit FX，CC0） |
| `HitEffectLifetime` | 特效存活多少秒之后自毁（默认 **1.8** = 素材 `main` 动画 1.6s + 0.2s 尾巴）。⚠️ 给 0 = 每次命中都在场景里留一个常驻节点 |

> **`HitEffectHeightOffset` 已删除（用户 2026-10 指示 ⑤：「实际应该在命中的位置发生特效」）**：
> 命中体那一路原来落在"目标原点 + 这个全局高度"（单位原点在脚底，所以要 +1.0 抬到胸口），
> 现在改成落在**目标碰撞体表面上离命中体最近的那一点**（`AttackHitbox.ResolveHitPoint`），
> 兜底点也是碰撞体自己的中心 —— 全局高度偏移因此**没有任何消费者**了，留一列"改了没用的配置"
> 是项目反复清理过的东西，所以整列删掉（字段 + `ConfigIndex.tres` 的那一行一起删）。
> 细节、判据与实测见文末「用户三项修正实测」§三。

> 出生逻辑只有一处：`Scripts/Combat/HitEffect.cs`（`Projectile.ResolveImpact` 与
> `AttackHitbox._PhysicsProcess` 各调一行）。为什么必须由 C# 管寿命、以及两条路径的截图证据，
> 见 [art-integration-log.md](art-integration-log.md) 的「第三轮」。
> 两条路径的**位置**口径：投射物用**射线命中点**（`Projectile` 本来就有）；命中体用
> **碰撞体表面最近点**（`AttackHitbox.ResolveHitPoint`，几何算法，不读物理空间）。

## 当前取值（2026-10 原型）

玩家 250 血 / 40 甲 / 7 移速；手枪 24 伤害、0.26 秒间隔、12 发弹匣、**0.7 秒装填**；
普通敌人 **生命 ×0.75、伤害 ×0.3**；节点预算 ×1；Boss 生命 ×1、伤害 ×0.9。

调难度建议：
- 整体太难 → 先调 `EnemyDamageMultiplier`（0.3 → 0.2）
- 打起来太肉 → 调 `EnemyHealthMultiplier` 或 `Config/Tables/Weapons/wpn_pistol.tres` 的 `Damage`
- 一波接一波太密 → 调 `SpawnIntervalMultiplier`（1 → 1.5）
- 关卡太长/太短 → 调 `NodeBudgetMultiplier`
- 某个节点给的奖励太强 → 改 `Config/Tables/Upgrades/` 里对应那一行的 `Modifiers`

## 分表实测（2026-10，阶段 2）

自测（`selftest.flag` + gdmcp CLI）里新增了一组**逐项比对**日志，改完表可以照着核对：

```
[Config] 配置表：武器 3 / 角色 3 / 敌人 1 / 效果 10 / 节点 9 / 局外成长 5（ConfigIndex.tres 已加载）
[Config] 增幅项表：19 条（来源：表）—— 强化 10 / 进化 4 / 异化 5。
[SelfTest] 配置表比对·玩家：生命 270（期望 250 + 元进度加成 20） 护甲 40 移速 7 减伤 0
[SelfTest] 配置表比对·武器：伤害 24 间隔 0.26 弹匣 12 装填 0.7 散布 0.5 击退 2.5 暴击 0.15 弹速 90 FOV 55 穿透 0 连发 False
[SelfTest] 配置表比对·节点表：进化层 2/波 3 · 强化层 1/波 2 · 恢复层 0/波 1 · 商店价目 5/12/18 · 标题 异化池
[SelfTest] 配置表比对·效果表：燃烧 6 dps 减速修正 [MoveSpeedMultiplier=0.55]
[SelfTest] 配置表比对·增幅项：共 19 条（强化 10 / 进化 4 / 异化 5） 用了表=True 当前池子=18 条（枪手期望 18 条）
```

**回退路径**（把 8 个数组全清空再跑）：日志里会写"来源：代码默认值"，
玩家变成 `Unit` 场景默认 140（+ 元进度 20 = 160）、武器变成 `Weapon.cs` 默认 22/0.32/8/1.15/75，
而节点/效果/局外/增幅**与表在时逐位一致**（因为 C# 兜底就是分表前那份实现），自测全程通过、端到端按 F 仍能打开奖励界面。

## 配置表阶段 3 实测（2026-10：单位表 + 技能表）

**编译**：`& build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**（每改一步都跑，全程 0/0）。

**自测（`selftest.flag` + gdmcp）实测日志**：

```
[Config] 首领强化来源（关卡核心文档 §7.1：两套来源必须能分开）：数值型 Pressure 3 条（来源：表 Pressure）/ 行为型 Boss Build 4 条（来源：表 Skills）
[Config] 配置表：武器 3 / 角色 3 / 敌人 10 / 首领 1 / 召唤物 1 / 技能 25 / Pressure 3 / 效果 9 / 节点 9 / 局外成长 5（ConfigIndex.tres 已加载）
[SelfTest] 配置表比对·索引：武器 3/角色 3/敌人 10/首领 1/召唤物 1/技能 25/Pressure 3/效果 9/节点 9/局外 5（期望 3/3/10/1/1/25/3/9/9/5）
[SelfTest] 配置表比对·敌人表：10 行（期望 10 = 1 全局 + 9 敌种） 全局行 生命×0.75/伤害×0.3 本关白名单 [enemy_chaser,enemy_fast_melee,enemy_exploder]（期望按 Id） 实际可生成 [enemy_chaser(成本2), enemy_fast_melee(成本1), enemy_exploder(成本1)]
[SelfTest] 配置表比对·敌人行 enemy_chaser：生命 60 移速 4.2 攻击 12 间隔 1.3 射程 2.3 击退 3 标签 [Melee,Chaser] 成本 2/权重 3 持有技能 [skl_enemy_chaser_melee]
[SelfTest] 配置表比对·技能表：25 行（期望 25 = 5 玩家 + 16 单位 + 4 首领行为） dash 行：冷却 1.2 前摇 0.3 节点 AbilityDash
[SelfTest] 技能表实测（玩家）：滑铲 冷却 1.2 位移 6 AbilityId skl_dash ｜ 近战 伤害 32 间隔 0.75 射程 2.4 击退 5 命中标签 [Melee,Heavy]
[SelfTest] 单位表实测（FastMelee）：生命 26 移速 6.5 伤害 7 间隔 0.8 击退 2 标签 [Melee,Fast,Chaser] 技能 1 个 技能行 Id skl_enemy_fast_melee_melee（非类名 = 技能行真的套上了）
[Spawn] 白板首领降临（血量倍率 0.35（来源：表 boss_final），无压力 / 无词条）
[Config] 敌人倍率：普通 生命×0.75 伤害×0.3 移速×1 / Boss 生命×1 伤害×0.9（来源：表 enemy_global）
```

逐项说明：
- `间隔 0.75`（不是技能行的默认值 1.0）证明 **`AbilityConfig.Cooldown` 留空 = 不覆盖**这条规则真的生效，
  主攻击的四个数以角色行为准。
- `技能行 Id skl_enemy_fast_melee_melee`（而不是类名 `MeleeAttackAbility`）证明**技能行真的套到了敌人身上**
  （场景里没有填过 `AbilityId`，只有套表才会变成行 Id）。
- `白板首领 血量倍率 0.35（来源：表 boss_final）`证明首领行的 `EliteCopyHealthScale` 接管了以前那个死字段。

**回退路径主动验证**（把 `Enemies` / `Bosses` / `Summons` / `Skills` / `PressureMods` **五个数组全清空**再跑）：

| 观察点 | 实测 |
|---|---|
| 日志留痕 | `配置表：… 敌人 0 / 首领 0 / 召唤物 0 / 技能 0 / Pressure 0 …（ConfigIndex.tres 已加载）`、`首领强化来源：数值型 Pressure 3 条（来源：代码兜底）/ 行为型 Boss Build 4 条（来源：代码兜底）`、9 条「关卡白名单里的 Id 在敌人表里找不到」的警告 |
| **不再静默换挡** | `敌人倍率：普通 生命×0.75 伤害×0.3（来源：代码默认值 ConfigIndexData（表读不到））` —— 与表在时**同值** |
| 白名单 | 读不到敌人行 → **不筛**（8 个生成项全在，回退路径仍能玩），而不是"整关没怪" |
| 敌人 | 用 `Scenes/Enemies/*.tscn` 的值（实测 JumpingMelee 生命 38 = 场景 50 × 0.75），技能行的 Id 变回类名 |
| 白板首领 | `血量倍率 0.35（来源：场景导出值）` |
| 玩法可用性 | 自测全程通过、端到端按 F 仍打开奖励界面 |

> ⚠️ 一个值得记住的边界：**清空 `Skills` 索引数组不会让"某个单位行的持有技能"失效** ——
> 技能引用写在**单位行文件**里（`chr_*.tres` / `enemy_*.tres` 的 `ext_resource`），
> 数组只是"给 `ConfigRows` 建索引、给日志报数"用的。要真正回退到场景值，得清空**单位表**那一侧的数组。

## 配置表阶段 4 实测（2026-10：武器 → 升级池 → 选项 的三层关联）

**编译**：`& build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**（每改一步都跑，全程 0/0）。

### 结构（用户拍板：武器指池、池不指武器、一个选项占一行）

```
WeaponConfig（Config/Tables/Weapons/wpn_*.tres，3 行）
  └─ Pool ──────► UpgradePoolConfig（Config/Tables/UpgradePools/pool_*.tres，3 行：一行一个池）
                      ▲
RewardConfig（Config/Tables/Upgrades/*.tres，19 行：一行一个选项）
  └─ Pool ────────────┘（留空 = 落到"标签匹配 / 通用"两层）
```

| 问题 | 结论 |
|---|---|
| **"一个选项占一栏"是什么意思** | **一行 = 一个选项**（`Upgrades/` 里 19 行就是 19 个选项），"属于哪个池"是这行上的**一个引用列** `Pool`。**不是**"池行里塞一个选项数组" —— 用户原话的解读见 [config-tooling-yard.md](config-tooling-yard.md) §4.3：「"一个选项占一栏"天然就是一个标量字段……一栏里放多个选项（`Array[StringName]`）反而拿不到编辑器下拉」 |
| **池表里有什么** | 只有**身份**（`Id` / `Slot` / `DisplayName` / `Note`）。**成员关系不在池行里** —— 池行既不列武器也不列选项，成员关系只有一个真源（选项行的 `Pool` 列） |
| 为什么池行这么薄 | 它存在的意义是让"武器 → 池"成为**资源引用**（改名/删文件时 Godot 把引用置 null，能检测），而不是拼字符串（拼错会静默失效）。与 `chr_*.tres → skl_*.tres` 是同一套写法 |

### 三级回退（`RewardDefinition.AllowsWeapon`）

| 层 | 条件 | 现有内容 |
|---|---|---|
| ① **专属池** | 选项行的 `Pool` 非空 → 只有 `Weapon.PoolId` 等于它的武器能拿到（**此时不看标签**） | 今天 **0 条**（新增能力，留白） |
| ② **武器标签** | `Pool` 为空，且 `RequiredWeaponTags` 全部命中 `weapon.AttackTags` | **现有 4 条进化**（`"Gun"`）—— 行为与分表前**逐位一致** |
| ③ **通用池** | 两层都为空 | **现有 10 条强化 + 5 条异化** |

`Weapon.PoolId` 由 `WeaponConfig.ApplyTo` 从武器表的 `Pool` 列写入；
**武器行没填 / 池表读不到 / 整份表没加载 → 留空 → 自动落到 ②③**。
`RewardPool` / `RunBuild` / `RunController` **一行都没改**（`Initialize(Weapon)` 与 `AllowsWeapon(weapon)` 的签名不变）。

### 自测实测（`selftest.flag` + gdmcp）

```
[Config] 升级池：3 个（来源：表 UpgradePools）—— 武器指向 [wpn_pistol→pool_pistol、wpn_sniper→pool_sniper、wpn_rifle→pool_rifle]；选项行里指定了池的 0 条，其余走「标签匹配 → 通用」两层
[SelfTest] 配置表比对·索引：武器 3/角色 3/敌人 10/首领 1/召唤物 1/技能 25/Pressure 3/效果 9/节点 9/局外 5/升级池 3（期望 3/3/10/1/1/25/3/9/9/5/3）
[SelfTest] 配置表比对·增幅项：共 19 条（强化 10 / 进化 4 / 异化 5，期望 19 = 10/4/5） 用了表=True 当前池子=18 条（枪手期望 18 条）
[SelfTest] 配置表比对·升级池：武器 PoolId「pool_pistol」（期望 pool_pistol） 池表 3 行（期望 3）/ 选项行里指定了池的 0 条 ｜ 专属层 0 条（命中 0）· 类别层 4 条（命中 4）· 通用层 15 条 ｜ 召唤控制池 19 条（期望 19）
```

**"枪手 18 / 召唤 19"这个硬指标（[rewards.md](rewards.md):79-86）逐位不变**：
- 枪手 18 条 —— `当前池子=18 条`，与改前逐字相同；
- 召唤 19 条 —— 阶段 4 给自测补了这一侧的**实测**（用同一个武器另建一个 `RewardPool { SummonerAllowed = true }`，
  不动 `RunController` 的状态），实测 **19 条**，与文档记录一致（差的那 1 条就是 `mut_summon`）。

改前 / 改后两次自测日志逐行 diff，**除了上面这两行新增内容与索引行的扩展，
所有配置相关行（玩家 / 武器 / 节点 / 效果 / 敌人 / 技能 / 增幅项）完全相同**（其余差异只有随机敌种与帧率）。

### 第一层真的会拦人（两次临时实验，做完已还原）

| 实验 | 做法 | 实测 |
|---|---|---|
| **A（正向）** | 临时给 `upg_dmg_up` 填 `Pool = pool_pistol`（= 手枪自己的池） | `专属层 1 条（命中 1）`、**池子仍是 18 条** ✓ |
| **B（反向）** | 把同一行改指 `pool_sniper`（≠ 手枪的池） | `专属层 1 条（命中 0）`、**池子掉到 17 条** ✓（手枪真的拿不到它了） |

### 回退路径主动验证（两次，各跑一整轮自测）

| 清空什么 | 日志留痕 | 池子条数 | 玩法数值 |
|---|---|---|---|
| **三个武器行的 `Pool` 字段** | `升级池：3 个（…）—— 武器指向 [wpn_pistol→（未填）、wpn_sniper→（未填）、wpn_rifle→（未填）]`、`武器 PoolId「（留空 → 走「标签匹配 → 通用」两层）」`；**没有 warning**（"武器没指定专属池"是合法状态，不是错误） | **18 条（不变）** | 武器 24 / 0.26 / 12 / 0.7 … 全部不变 |
| **`ConfigIndex.tres` 的 `UpgradePools` 索引数组**（模拟池表读不到） | `升级池：0 个（来源：代码默认值（表读不到））` + 3 条 `⚠ 武器 wpn_x 指向的升级池「pool_x」不在 UpgradePools 索引里` | **18 条（不变）** | 全部不变 |

> ⚠️ **从第二次验证挖出来的边界（与技能表那次同一类坑）**：清空 `UpgradePools` 索引数组**不会**让武器丢掉池 Id ——
> 池引用写在**武器行文件**里（`wpn_*.tres` 的 `ext_resource`），数组只是"给 `ConfigRows` 建索引、给日志报数"用的，
> 实测那时 `武器 PoolId` 仍然是 `pool_pistol`。要真正回退到"这批武器没有专属池"，得清空**武器行**那一侧的字段（上表第一行）。

### 顺手处理的三个死字段

| 字段 | 处置 |
|---|---|
| `RewardConfig.PoolTags` / `RewardDefinition.PoolTags`（19 行都在认真填，**全项目 0 消费者**） | **删除**：行类字段、运行期视图字段、`RewardEffectRegistry.Compile` 的赋值、`RewardCatalog` 三个工厂的 `tag` 参数与 15 个调用点、19 个 `.tres` 里的那一行。语义被**两张新列**取代（`RewardConfig.Pool` 专属池 + 原有的 `RequiredWeaponTags` 标签层） |
| `RewardDefinition.ConflictsWith`（`Compile` 里**没有填入口** → 永远为空数组） | **接上**：`RewardConfig` 加 `ConflictsWith` 列、`Compile` 填进运行期视图（`RewardPool.Roll` 早就在读它）。语义 = **单向**：已拥有列表里任一 Id → 本项不再进池（与 `Excludes` 的双向判定互补）。默认空 = 行为不变 |
| `WeaponConfig.ApplicableWeapons`（设计文档 §4.5 推荐的"反向指"） | **全项目不存在**（`grep` 只命中文档）。用户拍板改成**正向指**（武器 → 池），所以这个反向字段**不需要建**；[config-schema.md](config-schema.md) §4.5 / §6.9 的推荐已标注为"被拍板取代" |

### 顺藤摸瓜（以后怎么用）

1. **看某把武器有哪些专属选项**：在 `Config/Tables/Upgrades/` 里按 `Pool` 列筛选（或按它排序）→ 属于该池的行聚在一起。
2. **加一条只给狙击枪的进化**：新建一行 `.tres`，`Pool` 填 `pool_sniper`（一个引用列，不必碰别的文件）。
3. **调"某把武器的升级池里全部增幅的强度"**：筛出那几行 → 在 `Modifiers` 列上批量改。
4. **想让两把武器共享一个池**：让这两个武器行的 `Pool` 指向**同一个池行**；只想共享其中一部分选项，直接用第②层的武器标签（`AttackTags` ↔ `RequiredWeaponTags`）。

### 编辑器里的可编辑性实测（`Edit Resources as Table` v2.7，真机）

在**运行中的编辑器**里调插件自己的 API（`EditorInterface.set_main_screen_editor("ResourceTables")` + `editor_view.display_folder(...)`），
实测三张表的行数与列：

| 表 | 行数 | 列（节选关键项） |
|---|---|---|
| `Config/Tables/Upgrades/` | **19** | …`RequiredWeaponTags`、**`Pool`**、`RequiresSummoner`、`Excludes`、**`ConflictsWith`**、`Modifiers`…（**`PoolTags` 已消失**） |
| `Config/Tables/Weapons/` | **3** | …`AttackTags`、**`Pool`**、`Damage`、`FireInterval`… |
| `Config/Tables/UpgradePools/` | **3** | `Id` / `Slot` / `DisplayName` / `DescriptionTemplate` / `Note` / `Params`（新目录 = 新表，一行一个池） |

属性元数据（`PROPERTY_USAGE_EDITOR`）实测：**`Pool(type=24, hint=17)`**（24 = `OBJECT`、17 = `RESOURCE_TYPE` → **资源引用列**）、
**`ConflictsWith(type=34)`**（`PackedStringArray` → 普通可编辑数组列）。
两边的 `Pool` 列都可用编辑器的资源选择器改（与 `chr_*.tres → skl_*.tres` 那套引用列一样）。

> ⚠️ 观察记录（**与阶段 4 无关**）：程序化快速切表时插件偶发一条
> `editor_view.gd:296 … Invalid access … on base object of type 'Array[Object]'`（`insert_row_sorted` 对"数组列"取值那一段），
> 属于上一轮已记录的那批插件脆弱点（commit `7972a4f` 修过 5 类，未含这一处）；
> **不影响表格的渲染与编辑** —— 复测连续两次 `display_folder` 后 `rows` 仍是 19 / 3 且元素全是 `Resource`。

## 配置表阶段 5 实测（2026-10：AI 行为表）

**为什么是这张表**：AI 参数**密度最高、而且完全不可批量调** —— 它们原先**每个单位场景各配一份**
（`Brain` 子树 + 各 `Behavior` 的 `[Export]`），10 个敌人场景里写着 **53 处**（Boss 一个就 10 处），
想"让所有敌人更激进"得开 10 个场景改 30 处。

### 盘点到的东西（**以代码里实际存在的为准**，不是文档举例）

| 来源 | 参数（默认值） | 敌人场景里配了几处 |
|---|---|---|
| `EnemyBrain` | `SearchRange`(40) / `TargetRefreshInterval`(0.4) | 10 / 0（只有无人机配 0.3） |
| `Behavior`（基类） | `Priority`(0) / `MinRange`(0) / `MaxRange`(0) | **25** / 0 / 0 |
| `ChaseBehavior` | `StopDistance`(1.5) / `SpeedScale`(1) | 8 / 0 |
| `KeepDistanceBehavior` | `PreferredDistance`(8) / `Tolerance`(1.5) / `SpeedScale`(1) / `Strafe`(false) / `StrafeScale`(0.6) | 2 / 2 / 0 / 0 / 0 |
| `RetreatBehavior` | `HealthThreshold`(0.3) / `SpeedScale`(1.1) / `KeepDistance`(12) | 1 / 0 / 1 |
| `UseAbilityBehavior` | `AbilityNodeName`("") | 4（`AbilityScream`/`AbilityCharge`/`AbilitySummon`/`AbilityBuff`） |
| `MeleeAttackBehavior` / `RangedAttackBehavior` | **无自有参数**（射程由能力/技能行给） | — |
| `RepositionBehavior` | `MinDistance` / `MaxDistance` / `ArriveTolerance` / `RestartDelay` / `SpeedScale` | **0 —— 全项目没有任何场景用它** |
| `Behavior.AvoidObstacles`（硬编码） | 探针 2.4 / 射线抬高 0.6 / 侧移翻转 1.2s / 前向权重 0.35 | 硬编码在 `Behavior.cs:70-96` |

合计 **25 个可配 AI 参数**（不含 4 处硬编码避障启发式）+ 62 处场景赋值
（敌人 53 / 召唤物 5 / 训练场 4）。

### 表结构：一行一个 AI 档案，**扁平静态列**

```
Config/Tables/AiProfiles/ai_*.tres（11 行 = 一行一个 AI 档案）
ai_chaser ai_fast_melee ai_exploder ai_jumping_melee ai_ranged
ai_charger ai_screamer ai_guard ai_skill_elite ai_boss ai_drone
```

| 组 | 列 |
|---|---|
| 决策 · Brain | `SearchRange` / `TargetRefreshInterval`（0 = 不覆盖） |
| 行为槽（**按节点名开列**） | `AttackPriority`、`ChasePriority`+`ChaseStopDistance`+`ChaseSpeedScale`、`ShootPriority`、`KeepDistancePriority`+`PreferredDistance`+`Tolerance`+`SpeedScale`+`Strafe`+`StrafeScale`、`RetreatPriority`+`HealthThreshold`+`SpeedScale`+`KeepDistance`、`Explode`/`Leap`/`Scream`/`Charge`/`Summon`/`Buff` 各一列 `XxxPriority` |
| 逃生口 | 基类的 `Params`，键 = `节点名.属性名`（例：`Chase.MaxRange = 8`） |

三处**刻意的设计选择**：

| 选择 | 理由 |
|---|---|
| **扁平列**（`ChaseStopDistance`）而不是 `Array[行为]` | 一列 = 一个标量属性才拿得到编辑器的排序/批量改；数组列在表插件里拿不到这些，而且阶段 4 实测过 `Array[Object]` 那条插件报错 |
| **按节点名开列**而不是按行为类 | Boss 的 `Brain` 下有 **3 个** `UseAbilityBehavior`（Scream P120 / Charge P110 / Summon P60）—— 按类开列这三个会撞成一列 |
| **不放 `AbilityNodeName` 列** | 它是**身份**不是数值（哪个节点放哪个技能），场景里已经写死；抄一份进表就是第二个真源（项目文档反复点名的坑）。要改它走 `Params`（`Summon.AbilityNodeName = "AbilitySummon"`） |
| **不增删行为节点** | 档案只调数值，"谁有 Chase / 谁有 Retreat"这个**拓扑仍在场景里** —— 与 `Main.tscn` 的 `SpawnXxx` 提供预制体与位置、敌人表覆盖成本与权重是同一条分工 |

两条语义约定（都与既有表一致）：**数值列 0 = 不覆盖**（`RetreatHealthThreshold` 用 -1，因为 0 = 永不撤退是有意义的），
**`Priority <= 0` = 整个槽位不生效**（该节点在本档案里"不管"，它下面的参数一起跳过）。

### `UnitConfig` 怎么关联档案：**资源引用**（不是字符串 Id）

```csharp
// Scripts/Config/UnitConfig.cs
[Export] public AiProfileConfig? AiProfile { get; set; }   // 单位行 → 档案行
```

阶段 3 预留的位置叫 `AiProfileId`，落地时**改成了资源引用、属性名 `AiProfile`**，理由：

| 资源引用（采用） | 字符串 Id（未采用） |
|---|---|
| 与 `UnitConfig.Skills` / `WeaponConfig.Pool` 同一套写法 | 又要多一种关联方式 |
| 行文件被删/改名 → Godot 把引用**置 null**（`row.AiProfile == null` 立刻可判定 + 可警告） | 拼错一个字符就**静默失效**（`docs` 里 11 处字符串外键那条老问题） |
| Inspector 里是**类型过滤后的资源选择器**；`Edit Resources as Table` 里也是资源列（与阶段 4 实测的 `Pool(type=24, hint=17)` 同一种列） | 手打字符串 |
| `AiProfileConfig.Id` 仍然存在，用于日志/自测（`AI 档案：… 单位指向 [enemy_chaser→ai_chaser、…]`） | — |

**套用点只有一个，而且没有碰 `EncounterController`**：`UnitConfig.ApplyTo()` 里加了一行
`ApplyAiProfileTo(unit)` —— 生成路径本来就是"实例化 → `ApplyTo` → `AddChild`"，
而 `EnemyBrain._Ready`（按 `Priority` 排序行为列表）在 `AddChild` 才跑，所以时机天然正确。
`ApplyTo` 是敌人 / 首领 / 白板首领副本 / Boss 护卫 / 召唤物的**唯一入口**，所以一处改动全覆盖。
玩家侧走 `RunController.ApplyBalance`（不经过 `ApplyTo`），而玩家没有 `EnemyBrain`，本来也不该有档案。

### 自测实测（`selftest.flag` + gdmcp）

```
[Config] AI 档案：11 个（来源：表 AiProfiles）—— 单位指向 [enemy_chaser→ai_chaser、enemy_fast_melee→ai_fast_melee、enemy_exploder→ai_exploder、enemy_jumping_melee→ai_jumping_melee、enemy_ranged→ai_ranged、enemy_charger→ai_charger、enemy_screamer→ai_screamer、enemy_guard→ai_guard、enemy_skill_elite→ai_skill_elite、boss_final→ai_boss、summon_drone→ai_drone]；有预制体但没挂档案的 0 个
[Config] 配置表：武器 3 / 角色 3 / 敌人 10 / 首领 1 / 召唤物 1 / 技能 25 / Pressure 3 / 效果 9 / 节点 9 / 局外成长 5 / AI 档案 11（ConfigIndex.tres 已加载）
[SelfTest] 配置表比对·索引：武器 3/角色 3/敌人 10/首领 1/召唤物 1/技能 25/Pressure 3/效果 9/节点 9/局外 5/升级池 3/AI 档案 11（期望 3/3/10/1/1/25/3/9/9/5/3/11）
[SelfTest] 配置表比对·AI 档案表：11 行（期望 11 = 9 敌种 + 首领 + 召唤物） 敌人行 enemy_chaser 挂的档案「ai_chaser」（期望 ai_chaser） 档案行 SearchRange 40（期望 40）
[SelfTest] AI 档案实测（Chaser 预制体：先把 SearchRange/Chase.Priority 伪造为 -1，再 ApplyTo，与生成路径同序）：SearchRange 40（期望 40） Chase.Priority 10（期望 10） Chase.StopDistance 1.6（期望 1.6） Attack.Priority 100（期望 100）
[SelfTest] AI 档案实测（场上 Chaser）：SearchRange 40 目标刷新 0.4 行为 [Attack=P100 Chase=P10]
```

**"真的写进去了"这条硬证据怎么来的**：因为档案里的数值**逐条抄自场景**（这是零行为变化的前提），
单看数值相等**证明不了**是谁写的 —— 所以自测里的探针**先把预制体上的 `SearchRange` / `Chase.Priority`
伪造成 `-1`，再调 `ApplyTo`**：值回到 40 / 10 / 1.6 / 100，只可能是档案写进去的。

**改前 / 改后逐行 diff**：拿基线日志（`selftest.flag` 全流程、含 `=== 自测结束 ===`）与改后对比，
**所有既有的 `配置表比对·*` 行逐位相同**（玩家 / 武器 / 节点表 / 效果表 / 增幅项 / 升级池 / 敌人表 /
敌人行 / 技能表 / 技能表实测 / 节点布置 / 节点间距 / 单位表实测），索引行只在**末尾**追加了 `/AI 档案 11`
（前 11 个数字一个都没动），新增的只有上面 6 行。

### 回退路径主动验证（两轮，各跑一整轮自测）

| 清空什么 | 日志留痕 | AI 实际行为 |
|---|---|---|
| **`ConfigIndex.tres` 的 `AiProfiles` 索引数组**（模拟"档案表读不到"） | `AI 档案：0 个（来源：代码默认值（表读不到））`、`配置表：… AI 档案 0 …`、**11 条** `⚠ 单位行 X 指向的 AI 档案「ai_x」不在 AiProfiles 索引里（引用还在，索引里查不到）`、自测那一行变成 `0 行（期望 11）` | **一点没变**（探针仍是 40/10/1.6/100、场上敌人仍是 `SearchRange 40 [Attack=P100 Chase=P10]`） |
| **敌人行 `enemy_chaser.tres` 的 `AiProfile` 字段**（模拟"这个单位没挂档案"） | `单位指向` 列表里少了 `enemy_chaser→ai_chaser`、`有预制体但没挂档案的 1 个`、`敌人行 enemy_chaser 挂的档案「」（期望 ai_chaser）` | **组件默认值生效**：探针**保留我们伪造的 `SearchRange -1` / `Chase.Priority -1`**（`ApplyTo` 里整段跳过），`StopDistance 1.6` / `Attack.Priority 100` 也原样留在场景值上 |

> ⚠️ **第一轮又验证了阶段 3/4 那条边界（同一类坑，第三次了）**：清空 `AiProfiles` **索引数组**
> 不会让单位丢掉档案 —— 引用写在**单位行文件**里（`enemy_*.tres` / `boss_final.tres` / `summon_drone.tres`
> 的 `[ext_resource]`），数组只是"给 `ConfigRows` 建索引、给日志报数"用的。
> 要真正回退到"各单位没有 AI 档案"，得清**单位行**那一侧的字段（上表第二行）。

## 按键映射（当前）

| 操作 | 按键 | 定义位置 |
|---|---|---|
| 移动 | W/A/S/D + 方向键 | `project.godot` → `[input]` 的 `move_*` |
| 跳跃 | 空格 | `jump` |
| 射击 / 预瞄 | 鼠标左键 / 右键 | `fire` / `aim` |
| 战术滑铲 | **Q** | `dash` |
| 火力倾泻 | E | `burst` |
| 帮助界面 | H | `HelpScreen.ToggleKey`（导出属性，非 InputMap） |
| **首领状况预览** | **B** | `BossPreviewScreen.ToggleKey`（导出属性，非 InputMap） |
| 菜单 / 关闭顶层界面 | Esc | `UiRouter`（`ui_cancel` 动作）；有弹窗就关掉最顶层那个，没有弹窗就打开菜单 |
| 装填 | 无按键 | 被动：武器表的 `AutoReloadDelay` 秒没开火或打空弹匣自动装填 |
| 三选一奖励 | 无按键 | 节点清空后自动弹出 |

> **Esc 语义（已按需求实现）**：只用于「打开菜单 / 关闭当前弹层」，**不再切换鼠标捕获** —— 鼠标由 `UiRouter` 按「有没有弹窗」自动 Captured / Visible。
> 现在改键只能手改 `project.godot` 的 `[input]` 段（注意每个事件的 `device` 必须是 `-1`，否则真键盘匹配不上；已踩过一次）。
> 后续要做的是一个设置界面：读取/写入 `InputMap` + 存到 `user://settings.cfg`，并把 H 这类 `ToggleKey` 导出属性也纳入同一套。


## 相机构图

| 参数（`Scenes/Player/Player.tscn` 的 `CameraRig`） | 含义 |
|---|---|
| `TargetScreenHeight` | 角色出现在屏幕的高度比例。`0.5` = 正中，越小角色越靠下。当前 **0.3**（视野更多留给前方） |
| `TargetOffset` | 相机绕的目标点（角色胸口高度） |
| `MouseSensitivity` / `MinPitchDegrees` / `MaxPitchDegrees` | 灵敏度与俯仰范围 |

## 技能瞄准方向

`Ability` 上有 `Aim` 配置项（不是所有技能都朝准星）：

| 取值 | 含义 |
|---|---|
| `Facing`（0） | 朝角色自身前方（敌人默认） |
| `MovementIntent`（1） | 朝当前移动输入方向 |
| `Crosshair`（2） | 朝玩家准星（屏幕中心射线）；没有相机时自动回落为前方 |

玩家滑铲（`Player.tscn` 的 `AbilityDash`）配的是 **2 = Crosshair**。取方向统一走 `Ability.ResolveAimDirection()`，射线工具在 `Scripts/Combat/AimUtil.cs`。

## 界面与弹窗（后续 UI 框架的基础）

- `OverlayScreen`（基类）：`OpenGuardSeconds`(默认 0.3s) 打开后禁输入防误触；`OpeningDuration` 供以后加缓动 —— 保护期取两者较大值，**加缓动不用改逻辑**。`CloseOnCancel` 控制 Esc 能否关闭（奖励 / 结算界面设为 false，避免流程卡住）。
- `UiRouter`（autoload）：维护界面栈，栈空 → 恢复游戏并捕获鼠标；栈非空 → 暂停并放开鼠标。Esc 先尝试关闭最顶层可关闭界面，没有界面时调用 `OpenMenu`（当前指向帮助界面，等真正的菜单）。
- **界面清单与所属层**（层决定是否暂停）：

| 界面 | 层 | 打开方式 |
|---|---|---|
| `Hud` / `WorldOverlay` | 常驻（不是 OverlayScreen） | 场景里常驻 |
| `HelpScreen` | `Panel` | H（`ToggleKey`）或没有弹窗时按 Esc |
| `BossPreviewScreen`（首领状况预览，2026-10） | **`Panel`** | **B**（`ToggleKey`）/ `UiService.OpenBossPreview()` |
| `BaseScreen` / `StartScreen` | `Panel` | 基地交互台 / 出征 |
| `RewardScreen` / `ChooseScreen` | `Modal` | 节点奖励、商店（必须选择） |

> **为什么首领状况预览用 `Panel` 层**：它是"停下来看一眼资料"的信息面板，与帮助界面同类 ——
> 打开即暂停并放开鼠标，读的时候不该被打死；`Hud` 层留给**常驻**元素（常驻 = 不暂停、不拦输入），
> `Modal` 层留给必须做出选择的界面。想改成"边打边看"，只把 `Scenes/UI/BossPreviewScreen.tscn` 的
> `ScreenLayer` 改成 `Hud` 即可（代码不用动）。
> 面板正文由 `BossPreviewScreen.Describe(stage, encounter)` 生成（**静态 + 只读入参**，所以自测不开界面也能断言内容）；
> 界面由 `UiService.OpenBossPreview()` 打开，**没有绕过 UiService / UiRouter**。

## 开局配置与进化池

- 开局先弹 **Configure 画面**（`Scenes/UI/StartScreen.tscn`）：按 1 选手枪 / 按 2 选狙击枪 / 按 3 选步枪 → 进入 Stage 1。
  卡片文案来自武器表的 `DescriptionTemplate`（支持 `{Damage:0}` 这类占位符）。
- **进化池节点**：互动后弹出**三选一**，选定即触发本场 Encounter。用的是通用选择界面 `ChooseScreen`，以后商店/事件节点可直接复用。
  （旧文档里这一段写作"武器池"；用户 2026-10-12 拍板 A3：「没有定义武器池，只是一个说法，确切来说是进化池」——
  它 = 武器（角色的攻击方式）的**限定池**，算**高价值**。见上面「节点表」/「价值标签表」与 [stages.md](stages.md)。）

## 节点自身的数值

每个节点的预算、并发上限、生成间隔写在 `Scenes/Main.tscn` 的 `Stage/Node1`、`Node2`、`Node3Boss` 上（Inspector 里可直接改）。
每个节点的敌人种类与权重**现在来自敌人表**（`Config/Tables/Enemies/enemy_*.tres` 的
`ScenePath` / `Cost` / `Weight` / `MinElapsedSeconds` / `EliteChance`）—— 场景里的 `SpawnXxx` 子节点
仍然在（提供预制体引用与位置），但每次开战前都会由 `EncounterController.ApplyEntryFilter` **按表覆盖**；
读不到行时才用场景里的值（见上面的「敌人表」一节）。
**节点的"类型 → 层 / 波数 / 标题 / 奖励过滤"在 `Config/Tables/Nodes/node_*.tres` 里**（不在场景里）。

> **成本与预算的整除关系**：节点结束时余额若低于任何可用敌人的成本，会被当作「已花完」直接结算（不会卡住）。所以节点预算最好是成本的整数倍，否则最后会有一点余额被浪费。

## 跳跃参数（用户需求 A2，2026-10-12 **改成固定值**）

**跳跃的两个数在角色表里**（`Config/Tables/Characters/chr_*.tres`），不在 `Player.tscn` 上 —— 场景里的
`Movement.JumpVelocity = 6.5` / `Movement.GravityScale = 1.8` 只是**回退值**（表整个读不到时才用）。

用户 2026-10-12 拍板（需求 A2）：「要的是**直接改固定值**哦不是做**逻辑运算**，就是觉得之前
**跳跃的过程太漫长，起跳速度也要快**」。所以上一批那套 `JumpHeightMultiplier`(1.4) + `JumpSpeedMultiplier`(2.0)
的**倍率运算已经整段删除**（两列 + 读它们的代码路径一起删），现在表里给的是**绝对值**：

| 列 | 含义 | 现在三行都填 |
|---|---|---|
| `JumpVelocity` | **起跳速度的固定值**（m/s）—— 直接套到 `CharacterMovement.JumpVelocity`，填多少就是多少（不做平方根、不做倍率） | **13.0**（场景回退 6.5） |
| `JumpGravityScale` | **重力缩放的固定值** —— 直接套到 `CharacterMovement.GravityScale`；留 0 = 不覆盖 | **3.6**（场景回退 1.8） |

> ⚠️ **为什么还给了重力这一列**：跳跃高度与起跳速度是平方关系（`h = v²/2g`），**滞空时间**是 `2v/g` ——
> 只把起跳速度翻倍会让高度 ×4 且**滞空时间也翻倍**（那正好是用户抱怨的"过程太漫长"）。
> 所以"起跳更快 + 过程更短"必须两个固定值一起给：`v 6.5 → 13`、`g 1.8 → 3.6`
> → 起跳速度 ×2、跳跃高度 ×2（1.2 m → 2.4 m）、**滞空 0.74 s（比改前的 0.87 s 更短）**。
> 两列都是固定值，**没有任何倍率/开方运算**（这正是用户要的"直接改固定值"）。
> 空中加速（15）与最大下落（60）**不再被乘任何倍率**，保持场景值。

**只有玩家读这两列**（`CharacterMovement.ApplyJumpConfigDeferred`：按 `player_units` 组判定 + 按
`RunSetup.Character` 选行）。敌人/召唤物的跳跃留在各自场景上（`JumpingMelee.tscn` 的 `JumpVelocity = 9`），
否则调玩家的跳跃会连带改掉 9 个敌种的扑击高度。

> ⚠️ 这个调用**必须是 `CallDeferred`**：归属组是父节点 `Unit._Ready` 里 `AddToGroup` 的，
> 而**子节点 `_Ready` 早于父节点** —— 直接在 `_Ready` 里问 `IsInGroup("player_units")` 拿到的永远是 false。
> 首轮实测就是这么静默失效的（12 个单位全打出 `player_units 组=False`，跳跃配置一次都没生效，
> 而且**没有任何报错**）。修好之后的证据是日志里**只有一行**跳跃（玩家），12 个敌人一个都不打。

读不到角色行时**什么都不做**（玩家保留场景里的 6.5 / 1.8）。生效时日志有一行留痕：

```
[Config] 跳跃：角色行 chr_gunner → 起跳速度 13 m/s / 重力缩放 3.6（两列都是**固定值**、直接套用，没有任何倍率运算）｜ 空中加速 15 / 最大下落 60 保持场景值（来源：表 Characters，读不到则用场景导出值）
[SelfTest] 配置表比对·跳跃：起跳速度 13（期望 13 = 角色行固定值，场景回退 6.5）重力缩放 3.6（期望 3.6 = 角色行固定值，场景回退 1.8）空中加速 15（期望 15 = 场景值，不再乘倍率）最大下落 60（期望 60 = 场景值）
```

> 已知边界：玩家在基地**中途换角色**不会重算这两列（`RunController.ApplyBalance` 换角色时会重建数值，
> 那属于它的文件边界）。跳跃数值本身按"开局选定的角色行"算一次。

## 代码里不写死数值

新增可调项时：
1. 属于某张表的一行 → 在对应行类上加 `[Export]`，在建一个 `.tres` 行文件，再加进 `ConfigIndex.tres` 的索引数组；
2. 是跨表全局开关 → 在 `ConfigIndexData` 里加一个 `[Export]` → 在 `ConfigIndex.tres` 里给出默认值；
3. 套用它的地方读 `ConfigIndex.Settings` 或 `ConfigRows.ById<T>(...)`。

`EncounterController` 生成敌人后调用 `ConfigIndex.ScaleEnemy(unit, this, isBoss)` 套用倍率（优先读 `enemy_global` 行），
`RunController.ApplyBalance()` 套用角色/武器数值（读角色表与武器表）。

## 血条（用户需求，2026-01）

| 项目 | 规则 | 配置 |
|---|---|---|
| 普通怪物血条 | 放在**脚下**（沿 -Up 偏移），满血不显示 | `EnemyHealthBarTopOffset = 0.25`（现在按脚下偏移使用） |
| 触发方式 | **事件驱动**：订阅 `GameplayEventBus.Raised`，收到 `Damaged` 才 O(1) 登记该单位并建血条，`Died` 立即移除；**不再每帧遍历** `enemy_units` / `boss_units` 组 | — |
| 首领血条 | **不再画头顶血条**，改为屏幕**上方**的主血条面板（`WorldOverlay` 按 `HasTag("Boss")` 跳过） | — |
| 近距离 | 与首领距离 ≤ `BossBarFullRange` 时展开完整血条（名称 + 血量 + 关卡 **+ 词条行**） | `BossBarFullRange = 22` |
| 远距离 | 只保留首领头像色块 | 同上 |
| **首领词条行**（2026-10 用户需求） | 血条栏与顶部状态行都列出本关词条：`◆` = 行为型（Boss Build，来自 `EncounterController.BossPerks`）、`◇` = 数值型（Pressure，来自 `StageController.PressurePerks`）；同名词条折叠成 `名字×N`，超过上限折叠成 `…+N`（血条栏 5 个 / 状态行 4 个） | 文案统一在 `Scripts/UI/BossPerkText.cs`；节点在 `Scenes/UI/Bars/BarBoss.tscn` 的 `Panel/PerkLabel` |

代码位置：`Scripts/UI/WorldOverlay.cs`（怪物血条）、`Scripts/UI/PlayerHud.cs`（首领面板，含验证用只读属性 `BossBarExpanded` / `BossDistance`）、`Scripts/UI/BossPerkText.cs`（词条文案）。

**验证状态**：
- 首领面板：**已通过** —— 无首领时 `BossBarExpanded=False`；首领出场近距离（20.7 米）`True`、面板与名称条都可见；走远到 48.5 米（>22）自动收起为 `False`、只留头像；再靠近又展开为 `True` ✓
  （踩过的坑：`UpdateBossPanel()` 一开始被插在 `PlayerHud._Process` 的**某一个状态分支末尾**，Boss 战走的是另一个分支，所以从不执行；移到所有分支之前才生效。）
- 🔧 **2026-10 顺带修好一处"面板其实根本没显示"**：`Scenes/UI/Bars/BarBoss.tscn` 里的 `Panel` 节点
  自**首个提交**起就写着 `visible = false`，而代码只切根节点（`BarBoss`）的可见性 ——
  于是头像 / 名字 / 血条**从来没有真正画出来过**，只有 `BossBarExpanded` / `BossDistance` 两个只读属性在动。
  现在 `BuildBossPanel()` 明确把 `Panel` 打开，并把整块面板从 `y=14` 下移到 `y=64`（原先与顶部状态文字叠在同一处），
  同时**面板展开（近距离）时顶部状态行不再重复列词条** —— 词条由血条栏那一条负责，避免同一串词条出现两次。
  实测截图：[boss_bar_perks.png](screenshots/boss_bar_perks.png)（血条栏显示 `词条 4 ◆尖啸强化 ◇狂暴·攻速×2 ◇铁壁·生命`）
  与 [boss_preview_panel.png](screenshots/boss_preview_panel.png)（B 键的首领状况面板）。
- 怪物血条改到脚下：代码已改（`WorldOverlay` 用 `- Vector3.Up * offset`），**仍缺视觉确认**，建议你游玩时看一眼。
- 血条事件驱动：**机制已通过**（2026-10 自测实测日志）——满血敌人不建血条（`满血HP=60 血条数=0`）；扣 5 点（60 → 55）后 `Damaged` 事件立刻建出血条（`登记 Chaser HP=55/60 现有血条=1`），此后逐帧采样 121 帧期间**只登记一次、没有重复建条、单位存活且 HP 实时为 55**；致死 `Died` 事件立刻移除（`移除 ... 剩余血条=0`）。首领按 `HasTag("Boss")` 跳过（`跳过 Boss ... 候选=False`）。
  注意：若一次伤害直接致死，`Damaged` 与 `Died` 在同一调用栈内先后触发，血条会「建好又立刻拆掉」——本作实测几乎每次伤害都是即死，所以用 0.5 秒级轮询 `Bars` 子节点数常常看到 0，这是正确行为，不是丢事件。

## 关卡安全（用户需求，2026-01）

| 项目 | 规则 | 配置 |
|---|---|---|
| 地图下方区域杀 | **所有单位**（玩家/敌人/Boss/召唤物）进入区域即击杀 | `KillPlaneY = -8`（ConfigIndex.tres）→ 区域中心取 `KillPlaneY - 30`，由 `MapManager.EnsureKillZone()` 建 `KillZone`（Area3D，进入即杀）；旧的 `KillPlane` 每帧遍历实现已删除 |
| 节点生成避障 | 节点启用/降临时先找脚下地面，再从原点螺旋外扩找一个球形探测不重叠的空位 | `StageController.PlaceSafely()` |

避障实现要点（踩过的坑）：
- 探测球心必须抬到**半径 + 余量**之上，否则球体永远与脚下地面重叠，所有节点都会被判成「找不到空位」。
- 找地面的射线起点只抬高 2 米，否则会打到上层平台/斜坡的顶面，把节点放到半空（实测曾把节点放到 y=2.65）。

实测：

```
[Node] 节点避障：NodeWeapon   从 (9, 0, -7)  → (9, 0.25, -7)
[Node] 节点避障：NodeEnhance  从 (-9, 0, -5) → (-9.95, 1.86, -0.19)   ← 它在斜坡上，贴的是坡顶
[Node] 节点避障：NodeEvolution 从 (7, 0, 10) → (7, 0.25, 10)
[Node] 节点避障：NodeMutation 从 (-8, 0, 9) → (-8, 0.25, 9)
[Node] 节点避障：NodeShop     从 (0, 0, 13) → (0, 0.25, 13)
```

区域杀实测：把玩家 Y 设为 -20 → 生命 260 → **0**、`IsDead = true`，日志记「玩家掉出地图 → 击杀」。

⚠ 已知边界：如果人为把节点塞到地图正中央的地下（约 19 米内都有碰撞体），避障会放弃并保持原位并写日志。正常布置不会遇到。

## Pressure 结算（关卡核心文档 §7.2–§7.5，配置表阶段 6 起）

| 参数 | 放哪 | 含义 |
|---|---|---|
| `BossPressurePoints` | **节点表**（`node_*.tres`） | **挑战这个节点给多少 Pressure 基础点数**（§7.2「不同挑战类型具有各自的 Pressure 增加值」） |
| `BossModifierRolls` | **节点表** | 挑战这个节点**额外**抽几次数值强化（与阈值无关；默认 0） |
| `BossModifierTags` | **节点表** | 这个节点触发的抽取允许哪些候选标签（留空 → 用关卡行的 `PressureUpgradeTags`；两层都空 = 不限制） |
| `PressureGrowthMultiplier` / `PressureThreshold` / `PressurePerNodeNormal` / `PressurePerNodeHighValue` / `PressureEvolutionBonus` / `PressureMutationBonus` / `PressureUpgradeTags` | **关卡表**（`stg_*.tres`） | Stage 倍率表的 7 项（§7.4） |
| `PressurePerHighValueNode` / `PressurePerPoint` | `ConfigIndex.tres` | 全局默认值（行里留 0 时用它） |
| `Weight` / `Tags` / `Modifiers` | **Pressure 表**（`pmod_*.tres`） | §7.5 转换池的**候选内容与权重** |

**计算规则（全项目只有这一处定义，在 `StageController.AddPressureForNode`）**：

```
§8.2 闸门：这次挑战是「免费挑战」吗？（挑战序号 ≤ 本关免费额度）
        是 → 直接返回：不增加 Pressure、也不抽数值强化（连节点行的"额外抽几次"也拦）
        否 ↓
基础点数 = 节点行 BossPressurePoints        （挑战类型最具体，§7.2）
         → 关卡行按价值层的 HighValue / Normal（§7.4 的两个"基础 Pressure"，留 0 = 不表态）
         → ConfigIndex.PressurePerHighValueNode（150，全局默认值）

本次点数 = 基础点数 × 本关增长倍率 × 挑战类型额外比例      ← 组合顺序（§7.2 要求"明确的计算配置"）
                            Stage 行       进化 1.25 / 异化 1.5（Stage 行优先，留 0 用 ConfigIndex）

阈值     = 关卡行 PressureThreshold → ConfigIndex.PressurePerPoint（100）
```

**转换（§7.3，逐次处理）**：入账后**每满一次阈值就立即抽一次数值强化**（所以一次结算跨过多个阈值 = 多次抽取，
§7.3 第 6 条明令不能只转一次）；转换后不足阈值的余数**清零、不结转**（第 5 条）。
抽到的强化**先排队**，Boss 出生时才套到它身上（挑战发生时 Boss 还不存在）——
套用挂在 `EncounterController.BossSpawned` 信号上，所以关卡逻辑不渗进"一场遭遇"那一层。

**⚠️ 数值型 / 行为型不许合并**（§7.1）：这条路只抽 `pmod_*`（数值型）；行为/技能词条走
`BossPerkCatalog.Roll`（只抽行为型），两条线各有各的抽取点、日志里分别留痕。

累积结果进入 `OpenBossDoor()` 的初始压力：

```csharp
// 初始压力只由"超出免费挑战额度"这一项决定（§8.1/§8.2）。
// ⚠️ 阶段 6 之前这里还有一个 `+ _bossPressureBonus`：那是 §7.5 转换池没落地时把 Pressure 折算成压力等级的
//    替身。现在转换的产出是"从数值池抽一条强化套到 Boss 身上"（§7.3 第 3 条），所以那一段被移除，不能两头都算。
_bossEncounter.BossInitialPressure = Mathf.Max(0, ChallengesUsed - FreeChallengeQuota);
```

**实测（`selftest.flag` + 实机，2026-10）**：

```
Pressure 累积：+187.5（基础 150（来源：节点表 node_evolution）× 本关倍率 1 × 类型比例 1.25）阈值 100 → 本次跨阈值 1 次 → 本关累计 1 次 / 余数 0 / 立即抽取 [狂暴]
Pressure 累积：+225（基础 150（来源：节点表 node_mutation）× 本关倍率 1 × 类型比例 1.5）阈值 100 → 本次跨阈值 2 次 → 本关累计 3 次 / 余数 0 / 立即抽取 [疾行、狂暴]
再加一次中价值（强化）→ 什么都不发生（基础 0、额外 0 → 提前返回，pressure 仍为 3 ✓）
Boss 出生：套用本关 Pressure 数值强化 3 条 [铁壁、疾行、疾行]（阈值转换 3 次，来源：表 Pressure）→ Boss 生命 1260 护甲 350 伤害×0.972 攻速×1.06 移速×1.914
```

- **`+187.5 → 1` / `+225 → 3` 这两个硬指标逐位不变**：含义从"折算成 1 / 3 点首领额外压力"变成
  "跨过 1 / 3 次阈值（= 抽了 1 / 3 次数值强化）"，**数字本身没漂**（`DebugState` 的 `pressure=` 打印的就是跨阈值次数）。
- 两次 150 得到 2 次转换而不是 3 次，仍是文档要求的「余数清零」造成的（每次转换后不足 100 的 50 点被丢弃）。
- 第 4 行是**数值真的落到 Boss 身上**的证据：`铁壁` 让 900 → 1260（×1.4）、护甲 200 → 350（+150）；
  `疾行`×2 让移速 ×1.35² = 1.8225（日志 1.914 = 1.8225 × 1.05，那 1.05 是 BossPressure 1 级的 +5%）。
- 「已转换次数」现在**不再**计入 `BossInitialPressure`（见上面的代码块注释）—— 这是阶段 6 唯一一处
  **对玩法数值可见的变化**：以前打 2 个高价值节点白送 3 级压力曲线，现在换成 3 条随机数值强化。
  两者强度量级接近（3 级压力 ≈ +24% 伤害/+18% 攻速/+15% 移速；3 条强化看抽到什么），但**仍需一轮平衡测试**。

> ✅ **闸门已接（2026-10，第八章 §8.2）**：`AddPressureForNode` 的第一件事就是判断"这次挑战是不是免费挑战"，
> 是则直接返回（不累积 Pressure、不抽数值强化）。详见下面「免费挑战额度（§8.2）」一节。

## 免费挑战额度（§8.2「免费挑战不加 Pressure」，2026-10）

**文档要求（关卡核心文档 §8.2）的结算顺序**，以及它落到哪一行代码：

| §8.2 步骤 | 落在哪 |
|---|---|
| 1. 检查当前免费挑战额度 | `StageController.FreeQuotaLeft`（= 额度 − 已挑战数） |
| 2. 若有额度，则扣除 1 次免费额度 | `StartEncounter()` 里 `ChallengesUsed++` —— **额度与挑战数是同一份记账**（不刷怪的节点不进这里，也就不扣额度，§8.1） |
| 3. 启动 Encounter | `_encounter.Start()` |
| 4. Encounter 完成后，依据这次是否免费决定加不加 Pressure | `AddPressureForNode()` 第一行读 `StageController.CurrentChallengeFree` |
| 5. 没有免费额度 → 挑战完成后增加对应类型的 Pressure | 同一个方法往下走 §7.2–§7.5 的完整计算 |
| （§8.2 末）挑战未正常完成时不发放结算 | Pressure 只在 `IsCleared` 之后结算；开了 Boss 门会冻结普通 Encounter，不会补发 |

```csharp
/// §8.2 闸门：第 N 次挑战是否免费 = N ≤ 本关额度（额度在 StartEncounter 里扣）
public bool CurrentChallengeFree => ChallengesUsed <= FreeChallengeQuota;

public void AddPressureForNode(RewardNodeKind kind)
{
    if (CurrentChallengeFree) { /* 写日志，return：不累积、不抽数值强化 */ }
    ... // §7.2–§7.5
}
```

**两处必须一起看的地方**：

1. 闸门拦的是**整段 Pressure 结算** —— 包括节点行配的 `BossModifierRolls`「额外抽几次」。
   理由：那也属于"挑战完成时增加 Pressure"的结算（§7.3/§7.5），免费挑战按 §8.2 不该有任何 Pressure 产出。
2. `StageController._currentNodeKind` **以前从来没被赋值**（字段存在、结算分支在用它，但 `StartEncounter` 忘了写），
   所以**正常游玩时 Pressure 一次都不会累积**，只有自测直接调 `AddPressureForNode` 才有数字。
   本轮把它接上了：`StartEncounter` 里 `_currentNodeKind = node?.Kind`。
   证据 = 自测端到端那行的 `node=Enhancement`。

### 自测改了哪一段、为什么、新期望值

**改的是 `SelfTest.ReportPressureGate()`（原来是 `case 1` 里直接调两次 `AddPressureForNode`）**。

⚠️ **旧硬指标 `Pressure 累积：+187.5 / +225` 直接跑会变成 0** —— 那时自测的 `challenges=0`，
按 §8.2 就是**免费挑战**，闸门**正确地**把 Pressure 全拦了。**这是预期内的变化，不是回归**；
为了让自测仍然能验证 Pressure 机制，改成"**先把免费额度真的用掉，再走同一个公开结算入口**"，
**闸门本身一行都没放宽**：

```
① 闸门关着（额度内）：
   Pressure 累积：跳过（§8.2 免费挑战）—— 挑战数 0 ≤ 本关免费额度 1（剩余 1）→ 本次不增加 Pressure、也不抽取数值强化
   状态：challenges=0 freeQuota=1 free=True pressure=0 pmods=0 bank=0        ← 新增的硬指标（期望就是 0）
② 用掉额度（DebugExhaustFreeQuota，与 StartEncounter 同一套账）：
   状态：challenges=2 freeQuota=0 free=False
③ 再结算（闸门打开）：
   Pressure 累积：+187.5（…× 1.25）阈值 100 → 本次跨阈值 1 次 → 本关累计 1 次 / 余数 0 / 立即抽取 [狂暴]
   Pressure 累积：+225（…× 1.5） 阈值 100 → 本次跨阈值 2 次 → 本关累计 3 次 / 余数 0 / 立即抽取 [狂暴、狂暴]
   状态：pressure=3 pmods=3 bank=0                                            ← 与接闸门之前**逐位一致**
④ 词条显示（血条栏那一行，同名折叠）：
   ◇狂暴×3（本关抽到 3 条）
```

- **`+187.5` / `+225` 的数字没变**：闸门打开后走的还是原来那条路（三层取值 → 倍率 → 逐次跨阈值）。
- **新增的硬指标是 ①**：免费挑战必须让 `pressure`/`pmods`/`bank` 三项**全为 0**，且日志里出现"跳过（§8.2 免费挑战）"。
- `DebugExhaustFreeQuota()` 是**显式的测试入口**（只在 Debug 自测里调用，不参与玩法逻辑）：
  它按真实记账把 `ChallengesUsed` 顶到 `额度 + 1`，所以"下一次结算按超出额度处理"。
  之所以不用"直接调用一个绕过闸门的结算"：那样只能证明数值链没坏，证明不了闸门会开。
- 自测其余部分只多了一位数：端到端那行 `challenges` 从 1 → 5（多出的是本次消耗的额度与两场调试战斗），
  其余 `配置表比对·*` 行**逐字未变**。

## 节点生成约束（关卡核心文档 L105-107）

| 规则 | 实现 |
|---|---|
| 节点位置从地图预设点位中选择 | `Scenes/Main.tscn` 里 10 个 `RewardNode` 实例的位置 |
| 节点之间保持**最小间距** | `MinNodeSpacing = 8`；`EnableLayer()` 挑选时**跨层**判定，过近的候选跳过 |
| 不与障碍物冲突 | `StageController.PlaceSafely()`（射线找地面 + 球形探测螺旋外扩） |
| 挑不够时怎么办 | 放宽补足并写日志（避免因为间距约束导致每关节点数不足） |

实测：

```
节点间距：最小 9.74（NodeShop↔NodeHeal），阈值 8        ← 无需放宽
节点布置：启用 10 个（高 2 / 中 5 / 低 3）
```

为了让间距能满足，10 个节点的预设点位重排成 **间距 10 的网格**（±15 × ±10 范围内），否则原位置最近的一对只有 3.16。

## 白板首领副本（关卡核心文档 L231/L315/L317；**2026-10 加了波次门槛**）

文档里叫"第 8 波"的那个「白板首领副本」—— 明确要求**不能与常规波数共用参数**；同时提醒它**不是** Boss Door 里的最终首领。

| 项目 | 实现 |
|---|---|
| 触发时机 | 常规波全部清空后（`CheckCompleted()` 里，早于"结算完成"）**且过了波次门槛** |
| **波次门槛** | `StageDefinition.EliteBossMinWave`（关卡表列，三行都是 **8**）→ 每次开战前写给 `EncounterController.MinWaveForEliteBoss`；**留 0 = 闸门关掉**（回到"每场末尾都出"） |
| 门槛判定的量 | **本关累计波数** = `EncounterController.WavesOpenedBefore`（本场之前开过的波数，StageController 每次开战前写入）+ 本场 `WaveIndex` |
| 独立参数 | `EncounterController.EliteBossScene`（**独立于** `BossScene`/`IsBossNode`） |
| 弱化 | `boss_final.tres` 的 `EliteCopyHealthScale = 0.35`（白板首领血量只有普通首领的 35%；读不到行才用场景导出值） |
| 不带 Boss 构筑 | 不设置 `BossPerkCount`/`BossInitialPressure`，所以无词条、无压力 |
| 完成条件 | 白板首领**死亡后**才算完成（它在 `_spawned` 里，残留会阻止完成判定） |
| 复位 | `ResetEncounter()` 复位 `_eliteBossSpawned`；**本关累计波数每关清零**（`StageController.ResetStage()`），每场开战前重新写入 |

> ⚠️ **为什么用"本关累计"而不是"单场第几波"**：节点表给一场 Encounter 的波数只有 **1～4**（低 1 / 中 3 / 高 4），
> 按单场算的话门槛取 8 **永远到不了** —— 白板首领会从"每场都出"直接变成"永远不出"。
> 按本关累计算：第 1～7 波打完都不出，累计到第 8 波清空时才降临。

实测（自测同一轮里两种分支都跑到了）：

```
白板首领跳过：本关累计才第 1 波（门槛 8），本场直接结算      ← 门槛没过
白板首领跳过：本关累计才第 3 波（门槛 8），本场直接结算
白板首领闸门·正向：… 压到 1 后闸门=已开                      ← 门槛过了（自测把门槛临时压到 1）
常规波已清空 → 进入白板首领副本（本关累计第 1 波，门槛 1）
白板首领降临（血量倍率 0.35（来源：表 boss_final），无压力 / 无词条 / 本关累计第 1 波）
🔴 白板首领闸门·正向：本场第 1 波后降临=True（期望 True）场上存活=1（期望 1）
```

**踩坑（本轮修掉的一个真 bug）**：`SpawnEliteBossNow()` 里原来手写了一句 `AliveCount++`。
`AliveCount` 是 `_spawned` 的**缓存**、只在 `PruneSpawned()` 里重算 —— 同一个物理帧里多写一次，
下一帧就被算回真实值，于是 `CheckCompleted()` 开头的 `if (AliveCount > 0) return;` 会让
**"白板首领被打死之后本场永远结算不了"**。现在改成生成后直接 `PruneSpawned()`（缓存与真相一致），
`TotalSpawned++` 保留（它是累计数，不是缓存）。

**旧踩坑（保留记录）**：第一版把这段分支写进了 `if (BossScene != null)` 里，而普通 Encounter 的 `BossScene` 是 null（只有 BossFight 才有），所以永不执行；而且它还在 `IsCompleted = true` 之后，即使执行也拦不住结算。正确位置是 `CheckCompleted()` 开头、`AliveCount > 0` 判断之后、`IsRunning = false` 之前。

### 进化 / 异化对 Pressure 的额外比例（关卡核心文档 §7.2）

| 参数 | 含义 |
|---|---|
| `PressureEvolutionBonus` | 进化奖励的 Pressure 比例（1.25 = +25%）—— 行里没填时的全局默认值 |
| `PressureMutationBonus` | 异化奖励的 Pressure 比例（1.5 = +50%）—— 行里没填时的全局默认值 |

`AddPressureForNode()` 按奖励类型乘比例后再走「每满阈值立即抽一次、余数清零」。

## 配置表阶段 6 实测（2026-10：首领增强的"节点驱动抽奖"）

**编译**：`& build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**（每改一步都跑，全程 0/0）。

### 结构（关卡核心文档 §7.1：两套来源必须能分开）

```
关卡核心文档 §7.2  Pressure 增长  ──►  节点表 BossPressurePoints（挑战类型各自的增加值）
                                       × 关卡表 PressureGrowthMultiplier（固定倍率表，§7.4）
                                       × 关卡表 PressureEvolutionBonus / PressureMutationBonus
                                            │
§7.3  每满 PressureThreshold 点 ────────────┘  逐次检查：每满一次就【立即】抽一次
                                            │
§7.5  Pressure 转换池（只有数值型）──────────┘  pmod_*.tres：Weight 加权 + Tags 过滤候选
                                            │
                                      排队 → Boss 出生（BossSpawned 信号）→ 套到 Boss 的 StatSheet

§7.1  Boss Build（只有行为型）──► 技能表 bhv_*.tres ──► BossPerkCatalog.Roll(BossPerkCount)   ← 另一个抽取点，互不共用候选
```

### §7.2 / §7.3 / §7.4 / §7.5 各自落到哪

| 文档条款 | 实现 | 状态 |
|---|---|---|
| §7.2 挑战完成后增加 Pressure | 清场后 `AddPressureForNode`（原本就有） | ✅ 原本就有 |
| §7.2 **不同挑战类型有各自的增加值** | `NodeConfig.BossPressurePoints`（节点表 = 挑战类型）；留 0 → 关卡行按价值层 → ConfigIndex 150 | ✅ **阶段 6 新接** |
| §7.2 **每个 Stage 用固定倍率表** | `StageDefinition.PressureGrowthMultiplier`（阶段 1 已就位，不随机） | ✅ 已就位 |
| §7.2 进化/异化**额外比例**，**组合顺序明确** | `1.25` / `1.5`，顺序 `基础 × 本关倍率 × 类型比例`（只在 `AddPressureForNode` 定义一处，日志把三个因子都打出来） | ✅ 已就位 + 日志可核 |
| §7.3 每 100 点 → 一次**随机强化** | 跨阈值时**立即**调用 `BossPerkCatalog.RollPressure`（数值池） | ✅ **阶段 6 新接** |
| §7.3 余数清除不结转 | `crossed > 0` 时把银行清零 | ✅ 原本就有 |
| §7.3 **跨多个阈值逐次处理** | 显式 `while (_pressureBank >= perPoint)` 循环，每轮抽一次（原来是 `(int)(bank/perPoint)` 一次算完，结果等价但不符合"逐次检查和处理"的措辞） | ✅ **阶段 6 改成显式循环** |
| §7.4 Stage 倍率表 7 个建议字段 | `Id` / `PressureGrowthMultiplier` / `PressureThreshold` / `PressurePerNodeNormal` / `PressurePerNodeHighValue` / `PressureEvolutionBonus` / `PressureMutationBonus` / `PressureUpgradeTags` | ✅ 8 个字段全在；`PressureUpgradeTags` **阶段 6 起真的生效**（原来是"半活"：只进日志） |
| §7.5 候选内容和权重由配置定义 | `pmod_*.tres` 的 `Weight`（3/2/2）与 `Tags`（Offense/Defense/Mobility）；启动日志把候选、权重与**由权重算出的概率**打出来 | ✅ **阶段 6 新接** |
| §7.5 独立配置、独立追踪、独立结算 | 两个抽取点各用各的候选集（`BehaviorOnly` / `PressureOnly`），各自日志留痕 | ✅ **阶段 6 新接** |
| §7.1 不得合并成一个来源不明的池 | 开场词条 `Roll()` 只抽行为型；Pressure 转换 `RollPressure()` 只抽数值型 | ✅ |
| §7.2 禁止"可被玩家增幅修改"的接口 | 本次没有加任何这类钩子（也没改 `Stats` 的乘区来源） | ✅ 遵守 |

### 节点驱动怎么接的

| 字段 | 语义 | 消费者 |
|---|---|---|
| `NodeConfig.BossPressurePoints` | 挑战这个节点给多少 Pressure **基础点数** | `StageController.ResolveBasePressure`（三层取值的第一层） |
| `NodeConfig.BossModifierRolls` | 挑战这个节点**额外**抽几次（与阈值无关） | `StageController.AddPressureForNode` 的 ④ 段 |
| `NodeConfig.BossModifierTags` | 这次抽取的候选标签（留空 → 关卡行 `PressureUpgradeTags` → 不限制） | `StageController.PickTags` |

当前三行节点表里 **`node_evolution` 与 `node_mutation` 填了 `BossPressurePoints = 150`**（= 文档 §7.2 的固定值），
其余 7 行留 0（走回退）。所以 `+187.5` / `+225` 与接之前**逐位一致**，但来源从
`ConfigIndex 全局默认值` 变成了 **`节点表 node_evolution` / `节点表 node_mutation`**（日志可核）。

### `Weight` 真的生效了吗（实测证据）

启动日志（**确定性**，可逐字比对）：

```
Pressure 转换池（§7.5，阈值达成后立即抽取；只出数值型）：候选 3 条 [pmod_berserk 权重 3 标签 [Offense] → 42.9%
 · pmod_fortress 权重 2 标签 [Defense] → 28.6% · pmod_swift 权重 2 标签 [Mobility] → 28.6%]
```

**临时实验**（加了一个用**独立固定种子 RNG**（不动游戏那把）抽 30000 次的探针，做完已删除）：

| 实验 | 改什么 | 实测频率 |
|---|---|---|
| **W1** | 表里的权重 `3 / 2 / 2`（当前值） | `berserk 42.0% · fortress 29.0% · swift 28.9%`（配置概率 42.9/28.6/28.6） |
| **W2** | 三行权重都改成 `1 / 1 / 1` | `berserk 32.9% · fortress 33.3% · swift 33.8%`（均匀） |
| **W3** | 只把 `pmod_swift` 改成 `0` | `berserk 49.0% · fortress 50.5% · swift 0.5%`（权重 0 被 `WeightedPick.MinWeight = 0.01` 兜住 → 0.5%，不是彻底排除） |

W2/W3 只改了 `.tres` 的 `Weight` 列（没重新编译）就换了分布 → **权重确实是表在说话**。

### 另外两个新字段的实验（各一轮，做完已还原）

| 实验 | 改什么 | 实测 |
|---|---|---|
| **T1 标签过滤** | `node_evolution.BossModifierTags = ["Defense"]` | `立即抽取 [铁壁]` —— 强制定向（只有铁壁带 `Defense` 标签）；对照：不设标签时同一个节点抽到过 疾行 / 狂暴（随机） |
| **R1 额外次数** | `node_enhance.BossModifierRolls = 2` | `Pressure 累积：+0（基础 0（来源：未配置）× …）阈值 100 → 本次跨阈值 0 次 + 节点行额外 2 次 → 立即抽取 [疾行、狂暴]`，`pmods` 从 3 → **5** 而 `pressure` 仍为 **3** |

### 抽奖序列是否变化（以及为什么可以漂）

| 项目 | 阶段 3 之后（改前） | 阶段 6（改后） | 结论 |
|---|---|---|---|
| Boss 开场词条候选 | `All` = 数值型 3 + 行为型 4（共 7） | `BehaviorOnly` = 行为型 4 | **变了**，见下 |
| Pressure 转换抽取 | 不存在（转换只折算成首领压力等级） | `PressureOnly` = 数值型 3（加权 + 标签） | **新增** |
| `All` 的拼接顺序 | 数值型在前 + 行为型在后 | **一字未改**（仍是这样，但已不是任何抽取点的候选集） | 保留 |
| 每次开场的 RNG 消耗次数 | `BossPerkCount` 次 | `BossPerkCount` 次（但 Pressure 抽取现在也会用同一把静态 RNG，且发生得更早） | 抽签结果会不同 |
| 自测硬指标 `+187.5 → 1` / `+225 → 3` | 1 / 3 | **1 / 3（逐位不变）** | ✅ 没漂 |

**为什么允许漂**：关卡核心文档 §7.1 明令「Boss Pressure 与 Boss Build 必须保持分离，不能合并成一个
无法区分来源的强化池」，§7.5 又要求两者「独立配置、独立追踪、独立结算」。
阶段 3 为了"玩法不变"把候选集保留成合并的 7 条，那是**过渡态**；本轮把 §7.5 的池真正接上，
自然要把它拆开 —— 所以**开场词条的结果会变**（数值型不再从开场词条出，改由 Pressure 转换出）。
**自测里那两个 Pressure 数字没漂**（含义从"首领额外压力等级"变成"跨阈值次数"，数值等价于原来的整除结果）。

### 回退路径主动验证（把本轮新增的配置全部清空，跑一整轮自测）

清空的东西：`pmod_*.tres` 的 `Weight`（改回 1.0）与 `Tags`（删掉）、`node_evolution` / `node_mutation`
的 `BossPressurePoints`（删掉）。

| 观察点 | 实测 |
|---|---|
| 池变成均匀 + 无标签 | `Pressure 转换池：候选 3 条 [pmod_berserk 权重 1 标签 [无标签] → 33.3% · …]` |
| 节点表明确写出"全走回退" | `Pressure 节点驱动：节点表 9 行都没配 → 全部走「关卡行 → ConfigIndex 全局默认值」回退` |
| 来源留痕 | `Pressure 累积：+187.5（基础 150（来源：ConfigIndex 全局默认值）× 本关倍率 1 × 类型比例 1.25）…` |
| **数值不变** | `+187.5` / `+225` 与表在时**逐字相同**；`pressure=3 pmods=3 bank=0` 相同 |
| 只有 warning、没有 error | 与 Pressure 相关的 warning / error **0 条**；自测全程走到 `=== 自测结束 ===` |
| 玩法可用性 | 节点布置 / 间距 / 战斗 / 端到端按 F 打开奖励界面全部照旧通过 |

### 一处需要平衡测试的可见变化

`BossInitialPressure` 现在**只由「超出免费挑战额度」决定**（§8.1/§8.2），
不再叠加节点累积 Pressure 折算出来的压力等级 —— 那段是 §7.5 落地前的替身，本轮移除（否则与随机强化两头都算）。
实测对照：改前打 2 个高价值节点 → 初始压力 **5**（2 来自超额 + 3 来自节点）；改后 → **2**，
但那 3 次转换换成了 **3 条数值强化**（例如 `[铁壁、疾行、疾行]` → 生命 1260 / 护甲 350 / 移速 ×1.914）。
强度量级接近，但**属于新曲线，需要一轮平衡测试**。

## 配置表阶段 7 实测（2026-10：Pressure 词条拆成"一参数一栏" + 技能参数入池 + 限定词刷新判定）

> ⚠️ **本节是阶段 7 当时的记录（池子 8 条）。后续一轮按用户指示做了三处修正**：
> **①「铁壁·护甲」整行删除（池子 8 → 7 条）**、② 行为型改称**技能**（数值型才叫**词条**）、
> ③ 命中特效改到**实际命中点**。所以本节里的 `8 条`、`20% / 13.3% / 6.7%` 概率、
> 以及带「铁壁·护甲」的日志样本都是**历史样本**，当前口径见文末「用户三项修正实测」。

**需求原话**：「词条我这里只看到了 3 条，`Pressure/` 里的，不过这三条应该**拆成 5 条**的，
**每个参数单独占一个词条栏**。如果可以的话**把一些技能的参数也加入词条里**
（尽管每个 boss 可能都不同，**限定词记得做好刷新判定**）」

**编译**：`& build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**（改一步跑一次，全程 0/0）。

### 一、3 条 → 8 条（5 条数值型参数栏 + 3 条技能参数栏）

| 新行 Id | 显示名 | 内容（与拆分前**逐位一致**） | 权重 | 单条概率 | 母行 / 来源 |
|---|---|---|---|---|---|
| `pmod_berserk_dmg` | 狂暴·伤害 | `DamageMultiplier` ×1.25 | **3** | 20.0% | ← `pmod_berserk`（拆） |
| `pmod_berserk_speed` | 狂暴·攻速 | `AttackSpeedMultiplier` ×1.2 | **3** | 20.0% | ← `pmod_berserk`（拆） |
| `pmod_fortress_hp` | 铁壁·生命 | `MaxHealthMultiplier` ×1.4 | **2** | 13.3% | ← `pmod_fortress`（拆） |
| ~~`pmod_fortress_armor`~~ | ~~铁壁·护甲~~ | ~~`MaxArmorAdd` +150（Add）~~ | ~~2~~ | ~~13.3%~~ | ← `pmod_fortress`（拆）+ 限定关卡 —— **该行已在后续一轮整行删除**（用户：「护甲目前就只给精英怪使用就好了，首领不需要」） |
| `pmod_swift` | 疾行 | `MoveSpeedMultiplier` ×1.35 | **2** | 13.3% | 本来就是单参数，**文件名不变** |
| `pmod_scream_echo` | 尖啸回响 | `AbilityScream.Cooldown` ×0.8 | **1** | 6.7% | 🆕 **技能参数** |
| `pmod_charge_impact` | 冲锋冲击 | `AbilityCharge` 命中体 `Damage` ×1.25 | **1** | 6.7% | 🆕 **技能参数** |
| `pmod_summon_haste` | 召唤急速 | `AbilitySummon.Cooldown` ×0.75 | **1** | 6.7% | 🆕 **技能参数** |

`pmod_berserk.tres` / `pmod_fortress.tres` **两个行文件已删**（数组项与 `[ext_resource]` 一起删，符合操作纪律）。

#### 1.1 权重怎么分（分配依据：三条规则）

| 规则 | 做法 | 为什么 |
|---|---|---|
| ① **继承母行权重，不均分** | 母行 3 → 两个子栏各 **3**；母行 2 → 两个子栏各 **2** | "母行权重" 表达的是"这一路（攻击向/防御向）该占多少概率质量"。均分（1.5/1.5）会把攻击向从"最重"打成"最轻"；继承则保住原设计意图（老注释写着"权重 3 = 攻击向略高"）。副作用还很好解释：**每一个老参数的概率都恰好变成原来的 7/15 = 46.7%**（分母从 7 变 15），谁也没被单独放大 |
| ② **技能型每条只给 1** | 3 条技能词条合计 3/15 = **20%**，5 条数值型占 **80%** | §7.5 要求 Pressure 池"**优先**定位为数值型强化，例如生命值、伤害、攻击速度或技能冷却"；而且技能参数词条只改**一个技能的某一个参数**，尖啸/冲锋/召唤还都要到阶段 2/3（或被 Boss Build 词条提前解锁）才真的会放 —— 有效强度低于名义强度，所以给最小权重 |
| ③ **单条数值一个都没动** | 1.25 / 1.2 / 1.4 / +150 / 1.35 与原文件**逐字相同**，技能参数取自 `skl_boss_*.tres` 的现值 | 本轮是**结构**改动（一栏一个参数），不该顺手改手感；数值一变，"抽到 N 次 = ×M" 的对照就要重新算 |

改前 / 改后的概率对照（**每个老参数都是 46.7%**，不是有人被针对）：

| 参数 | 改前（母行概率） | 改后（本栏概率） | 比值 |
|---|---|---|---|
| 伤害 | 42.9%（狂暴：一抽**同时**给伤害+攻速） | 20.0% | 0.467 |
| 攻速 | 42.9%（同上，同一次抽） | 20.0% | 0.467 |
| 生命 | 28.6%（铁壁：一抽同时给生命+护甲） | 13.3% | 0.467 |
| 护甲 | 28.6%（同上，同一次抽） | 13.3%（第 1 关还会被限定词挡掉 → 见第三节） | 0.467 |
| 移速 | 28.6%（疾行） | 13.3% | 0.467 |
| 尖啸回响 / 冲锋冲击 / 召唤急速 | —（不存在） | 各 6.7% | 新增 |

#### 1.2 "整体强度不失控"——算一遍（以及为什么会**变弱**）

口径：每抽的期望强度 = Σ 概率 × 效果强度，效果强度取 `ln(倍率)`（乘区天然可加）；
护甲是加法，按首领基线 900 血折算（+150 ≈ +16.7% → `ln1.1667 = 0.154`）。

| 参数 | 改前每抽期望 | 改后每抽期望 |
|---|---|---|
| 伤害 | 0.4286 × 0.2231 = **0.0956** | 0.20 × 0.2231 = **0.0446** |
| 攻速 | 0.4286 × 0.1823 = **0.0781** | 0.20 × 0.1823 = **0.0365** |
| 生命 | 0.2857 × 0.3365 = **0.0961** | 0.1333 × 0.3365 = **0.0449** |
| 护甲 | 0.2857 × 0.1542 = **0.0440** | 0.1333 × 0.1542 = **0.0206** |
| 移速 | 0.2857 × 0.3001 = **0.0857** | 0.1333 × 0.3001 = **0.0400** |
| 技能 3 条 | — | (1/15)×(0.2231+0.2231+0.2877) = **0.0489** |
| **合计** | **0.3996** | **0.2354（= 原来的 58.9%）** |

**结论（重要，属于"需要平衡测试"的可见变化）**：
拆成"一抽只买一个参数"之后，每抽的期望强度**必然下降**（-41%），这是拆分的**定义**决定的，不是权重没调好：
**理论上界** = 最强的那一条（铁壁·生命 `ln1.4 = 0.3365`）**仍然小于**原来的 0.3996 ——
所以**无论怎么分配权重都不可能补回原来的总量**（只有把权重全压在一条上才会接近，那显然更糟）。
要补，只有两个旋钮：① 提高单条数值（`Modifiers` 的 `Value`）；② 降低转换阈值（§7.3 的 100 点 / 关卡行 `PressureThreshold`）。
**故意没有**用"悄悄加强单条数值"来掩盖这一点 —— 那会让"抽到 N 次 = ×M"的对照全部失效。

> 反过来看，**不会失控变强**是确定的：没有任何一条效果被放大，每条参数的出现概率都 ≤ 原来的 42.9%。

#### 1.3 "抽到 3 次狂暴 = 伤害 ×1.95" 的叠加语义还在吗

| | 改前 | 改后 |
|---|---|---|
| 3 次狂暴 | 伤害 ×1.25³ = **1.953** **且** 攻速 ×1.2³ = 1.728（一抽买两个乘区） | 3 次"狂暴·**伤害**" = ×1.25³ = **1.953（逐位相同）**；攻速要另外抽"狂暴·攻速" |
| 凑出 ×1.95 伤害需要几次 | 期望 2.33 次（1 ÷ 42.9%） | 期望 5 次（1 ÷ 20%） |
| P(连中 3 次) | (3/7)³ = 7.9% | (3/15)³ = **0.8%** |

**所以**：叠乘本身**没变**（效果值没动，抽两次就是乘两次；技能参数也是倍率，`7 → 5.6 → 4.48` 同样是叠乘，不是覆盖），
变的是"一抽买几个参数"和"凑齐要几抽"。这是用户要的语义（**每个参数单独占一个词条栏**）。

### 二、技能参数怎么进词条的（§7.5 依据 + 三个新列）

**放哪张表**：进 **`Pressure/`（数值型）**，不进 `Skills/bhv_*`（行为型）。
依据是关卡核心文档 §7.5 原文：「建议优先将 Pressure 强化定位为**数值型**强化，例如生命值、伤害、攻击速度或**技能冷却**等参数。
首领**新增技能行为**则由 Boss Skill Elite 系统负责。」——**技能冷却/伤害是数值**，**技能开不开是行为**，两者分属 §7.1 的两套来源。

`PressureModConfig` 新增三列（都是给人编辑的扁平列）：

| 列 | 作用 | 例子 |
|---|---|---|
| `AbilityNode` | 要改的**技能节点名**（首领的子节点） | `AbilityScream` |
| `AbilityScales` | 作用在**能力节点**上的参数倍率：`Key` = 属性名，`Value` = **相对当前值的倍率** | `Cooldown ×0.8` |
| `AttackScales` | 作用在这个技能**命中体**上的参数倍率 | `Damage ×1.25`（冲锋） |

三点设计取舍：

1. **倍率语义（不是绝对赋值）**：`Value` 是"乘在当前值上"。绝对赋值会让**同一条抽到两次时第二次白抽**
   （7 → 5.6 → 5.6），而倍率是 7 → 5.6 → 4.48 —— 与"词条可重复抽中"的既有语义一致。
2. **`AbilityNode` 同时是一条限定词**：填了它就等于声明"这个首领必须真的有这个技能节点"（见第三节）。
3. **§7.1 的边界由代码兜着**：数值行想在技能上写 `Enabled` / `ApplyHandler` / `NodeName`（= 行为型开关），
   `BossPerkCatalog.ScaleProperty` 会**写警告并跳过**，不会让数值池偷偷变成行为池。

### 三、限定词与"刷新判定"

#### 3.1 三个维度（都在 `Config/Tables/Pressure/` 的词条行上）

| 列 | 语义 | 例子 |
|---|---|---|
| `BossIds` | 只对哪些**首领**生效：写单位行 Id（也接受场景路径兜底） | `["boss_final"]` |
| `StageIds` | 只对哪几关生效：写关卡行 Id（也接受关号 `"2"`） | `["stg_inner","stg_core"]` |
| `AbilityNode` 非空 | 这个首领必须**真的有**这个技能节点（技能参数词条的隐含限定） | `AbilityScream` |

首领身份取**单位行 Id**（`ConfigRows.ByScenePath<BossConfig>` 查表），读不到才退到场景路径 ——
**不用中文显示名**（改文案不该改变词条的适用性，与敌人白名单改按 Id 是同一条纪律）。

#### 3.2 判定做在哪：**Boss 出生时**（这是本次的关键）

```
抽签（挑战完成、跨过阈值就抽）          出生（Boss 真的被生成）
StageController.AddPressureForNode  →  EncounterController.SpawnBossNow
  BossPerkCatalog.RollPressure(tags)      EncounterController：BossPerkCatalog.Roll() → perk.Apply(ctx)
  只按 Tags 预筛候选，不判限定词            StageController.ApplyPendingPressurePerks()（BossSpawned 信号）
  抽到的 BossPerk 排队                      → perk.Apply(ctx)  ← **限定词在这里判**
```

`BossPerk` 因此被拆成"效果本体 `Effect`" + "入口 `Apply`"：`Apply` = 先判限定词、再决定生效还是换抽。
**两个调用点的签名与接线一行都没改**（`StageController` / `EncounterController` 依旧写 `perk.Apply(context)`；
这两个文件本轮**没碰**），所以"刷新判定"落在正确的位置 —— 也不需要把关卡逻辑渗进遭遇层。

**为什么不在抽签时判**：抽签发生在"一次挑战完成"时，那时**首领还不存在**（这一关可能根本不打首领），
拿不到"这个 Boss 有没有这个技能"这个事实。所以抽签只筛 `Tags`（节点行/关卡行给的那层候选），
限定词一律留到出生时判 —— 这正是用户说的"不能因为是同一局就复用上次的结果"。

#### 3.3 判不成立怎么办：**换抽**（不让这一抽白费）

`ApplyWithRefresh` 的规则（循环上界 = 候选池大小，每次换抽都排除一个 Id，不会死循环）：

1. 成立 → `Effect` 生效，写一行日志：`词条限定词（Boss 出生时判定）：<id>「<名>」限定 […] → 成立 → 已生效（Boss <身份> · 关卡 <关>）`。
2. 不成立 → 写一行日志说明**哪一条限定词不成立、当时的 Boss/关卡是什么**，然后从**同一组标签**的池里
   换抽另一条（排除已经判过的 Id）并**重新判定**。
3. 全部判过仍无适用者 → 只写日志，**不静默变强**（不会临时造一条效果出来）。

⚠️ **一个刻意的设计**：换抽后 `BossPerk.Name` 会被改写成 `原词条→实际生效的词条`。
这样 `StageController` / `EncounterController` 里那两条**既有日志**（它们先 `Apply`、后取 `Name`）
说的仍然是真正生效的那一条（HUD 的词条名同理）。实测日志里就能看到 `铁壁·护甲→狂暴·伤害`。

### 四、实测证据

#### 4.1 真实出生实测（`user://logs/run_20261011_233032.log`，一整局自测里的真 Boss 出生）

```
[Node] Pressure 累积：+225（基础 150（来源：节点表 node_mutation）× 本关倍率 1 × 类型比例 1.5）
       阈值 100 → 本次跨阈值 2 次 → 本关累计 3 次 / 余数 0 / 立即抽取 [铁壁·护甲、尖啸回响]      ← 抽签（第 1 关也抽得到护甲）
[Config] 词条限定词（Boss 出生时判定）：pmod_fortress_armor「铁壁·护甲」限定 [关卡 [stg_inner,stg_core]]
       → **不成立**（限定关卡 [stg_inner,stg_core]，而这一关是「stg_outer（第 1 关）」· Boss boss_final）
       → 换抽另一条（每次出生都重新判，不复用抽签时的结论）                                    ← 出生时才判 + 换抽
[Config] Pressure 技能参数：pmod_scream_echo「尖啸回响」→ 技能 AbilityScream.Cooldown 7 → 5.6（×0.8）  ← 技能参数真的落到技能上
[Node] Boss 出生：套用本关 Pressure 数值强化 3 条 [狂暴·攻速、铁壁·护甲→狂暴·伤害、尖啸回响]（阈值转换 3 次，来源：表 Pressure）
       → Boss 生命 900 护甲 200 伤害×1.485 攻速×1.488 移速×1.2
```

数字对得上：**护甲停在 200**（不是 +150 的 350）证明护甲那一栏**真的没生效**；
伤害 ×1.485 = 0.9（Boss 全局）× 1.25（换抽到的"狂暴·伤害"）× 1.32（压力 4 级）证明**换抽到的效果真的落了地**。
另一次真实出生（`run_20261011_233231.log`）同样出现 `铁壁·护甲→狂暴·攻速`，护甲仍是 200。
`run_20261011_233129.log` 里则是 `[尖啸回响、狂暴·伤害、狂暴·攻速]` 三条全中（尖啸冷却 7 → 5.6）。

#### 4.2 自测探针（`Scripts/Boss/BossPerkProbe.cs`，挂在 `SelfTest.ReportTables` 末尾）

```
[SelfTest] Pressure 词条池：8 条（期望 8 = 5 数值型参数栏 + 3 技能参数栏）｜ 带限定词的 4 条（期望 4）
           明细：[… pmod_fortress_armor「铁壁·护甲」权重 2 限定 [关卡 [stg_inner,stg_core]] ·
                 pmod_scream_echo「尖啸回响」权重 1 限定 [首领 [boss_final] · 技能节点 AbilityScream] · …]
[SelfTest] Pressure 词条池·出率（权重真进分母）：候选 8 条 [pmod_berserk_dmg 权重 3 → 20% · pmod_berserk_speed 权重 3 → 20%
           · pmod_fortress_hp 权重 2 → 13.3% · pmod_fortress_armor 权重 2 → 13.3% · pmod_swift 权重 2 → 13.3%
           · pmod_scream_echo 权重 1 → 6.7% · pmod_charge_impact 权重 1 → 6.7% · pmod_summon_haste 权重 1 → 6.7%]
[SelfTest] 技能参数词条实测（pmod_scream_echo → AbilityScream.Cooldown）：7（技能行 skl_boss_scream）→ 抽 1 次 5.6 → 抽 2 次 4.48
[SelfTest] 技能参数词条实测（pmod_charge_impact → AbilityCharge 命中体.Damage）：20 → 25
[SelfTest] 限定词刷新判定·基准：当前 Boss 身份「boss_final」（期望 boss_final = 单位行 Id）· 当前关卡「stg_outer（第 1 关）」
[SelfTest] 限定词刷新判定·同一条词条两次判定：Boss.tscn → 成立 ｜ 换成 Chaser.tscn（身份「res://Scenes/Enemies/Chaser.tscn」）
           → 不成立（限定首领 [boss_final]，而这次出生的是「…Chaser.tscn」）        ← 同一个词条对象、同一帧，两次判定结果不同 = 没缓存
[SelfTest] 限定词刷新判定·换抽实测（抽签副本 pmod_scream_echo，限定词不成立）：名字被改写为「尖啸回响→狂暴·伤害」
           ｜ 该单位身上发生的变化：[伤害×1 → 伤害×1.25]                              ← 换抽真的换了效果，不是硬套
[SelfTest] 限定词刷新判定·关卡限定（pmod_fortress_armor，限定 [stg_inner,stg_core]）：当前第 1 关 → 不成立 ｜ 把当前关卡换成 stg_core 再判 → 成立
           ｜ 成立那次真的落地：护甲 200 → 350                                        ← 同一帧两次判定，结果随条件变化
[SelfTest] 限定词刷新判定·端到端 3 次「抽签 → 出生套用」：铁壁·生命[生命 900 → 1260] ｜ 狂暴·攻速[攻速×1 → ×1.2] ｜ 狂暴·伤害[伤害×1 → ×1.25]
```

> 探针里的 `PerkQualifier.DebugStageIdOverride` 是**自测专用**开关（`#if DEBUG`，Release 里这个属性不存在），
> 只用来说明"同一帧换个关卡条件 → 判定结果就变"；跑到最后会复位，正式代码从不设置它。

#### 4.3 多次开局 / 多次出生：结果确实随限定条件变化（12 局真实出生的汇总）

| 日志 | 真实出生时套用的 3 条 | 其中被判不成立并换抽的 |
|---|---|---|
| `run_…_233019.log` | 疾行、冲锋冲击、狂暴·攻速 | — |
| `run_…_233031.log` | 尖啸回响、铁壁·生命、尖啸回响 | — |
| `run_…_233032.log` | 狂暴·攻速、**铁壁·护甲→狂暴·伤害**、尖啸回响 | 1（第 1 关 → 护甲被挡） |
| `run_…_233129.log` | 尖啸回响、狂暴·伤害、狂暴·攻速 | — |
| `run_…_233150.log` | 冲锋冲击、狂暴·攻速、召唤急速 | — |
| `run_…_233223.log` | 尖啸回响、召唤急速、狂暴·伤害 | — |
| `run_…_233231.log` | 疾行、铁壁·生命、**铁壁·护甲→狂暴·攻速** | 1（同上） |
| `run_…_233239.log` | 狂暴·伤害、狂暴·攻速、狂暴·伤害 | — |

每一局的抽取名单都不同（RNG 是随机的），但**判定每一次出生都重新跑了一遍** ——
抽到护甲那两次都在第 1 关被判不成立并换抽，护甲值始终停在 200（见 4.1）。

### 五、自测硬指标：哪些**没漂**、哪些**应该变**

| 日志行 | 改前 | 改后 | 说明 |
|---|---|---|---|
| `Pressure 累积：+187.5 … 本次跨阈值 **1** 次 / 本关累计 **1** 次 / 余数 0` | 1 / 1 / 0 | **1 / 1 / 0** | 只由「基础点数 × 本关倍率 × 类型比例」与阈值（100）决定，**与池子有几条、权重怎么分完全无关** → 不漂 |
| `Pressure 累积：+225 … 本次跨阈值 **2** 次 / 本关累计 **3** 次 / 余数 0` | 2 / 3 / 0 | **2 / 3 / 0** | 同上 → 不漂 |
| `Boss 出生：套用本关 Pressure 数值强化 **3** 条 […]` | 3 条 | **3 条** | 条数 = 跨阈值次数，与池大小无关 → 不漂 |
| `配置表比对·技能表：**25** 行 ｜ Boss Build 行为型 **4** 条` | 25 / 4 | **25 / 4** | 本轮**没往技能表加行**（技能参数词条在 `Pressure/`），所以这两个数一个都没动 |
| `配置表比对·索引：… 技能 25/**Pressure 3**/…（期望 …25/**3**/…）` | 3 | **8**（期望串同步改成 8） | 这是本次任务的**目的**：池子从 3 条变 8 条。**前 11 个数字一个都没动**，只动了第 7 位 |
| `首领强化来源：数值型 Pressure **3** 条` | 3 | **8** | 同上 |
| `立即抽取 [疾行]` / `[疾行、狂暴]` | 老名字 | **`[尖啸回响]` / `[狂暴·伤害、狂暴·攻速]`**（每局随机不同） | 名字**应该**变（3 条池 → 8 条池）；括号里的这份名单就是"抽到了哪一栏" |
| 其余 `配置表比对·*`（玩家 / 武器 / 节点 / 效果 / 增幅项 / 升级池 / 敌人表 / 敌人行 / AI 档案） | — | **逐位相同** | 与本次改动无关 |

### 六、回退路径主动验证（把 `PressureMods` 索引数组清空，跑一整轮自测）

| 观察点 | 实测 |
|---|---|
| 日志留痕 | `配置表：… 技能 25 / **Pressure 0** / …（ConfigIndex.tres 已加载）` + `首领强化来源：数值型 Pressure **8 条（来源：代码兜底）** / 行为型 Boss Build 4 条（来源：表 Skills）` |
| **兜底与表同结构** | 池明细与表在时**逐字相同**（同样 8 条 Id / 权重 3·3·2·2·2·1·1·1 / 4 条限定词 / 同样的概率 20/20/13.3/13.3/13.3/6.7/6.7/6.7）—— 兜底**不再**退回"伤害+攻速挤在一栏"的旧形态（那会是一次静默换挡） |
| 技能参数仍然生效 | `AbilityScream.Cooldown 7 → 5.6 → 4.48`、`AbilityCharge 命中体 Damage 20 → 25`；真实出生 `[狂暴·攻速、铁壁·生命、狂暴·攻速]` → `Boss 生命 1260` |
| 限定词与换抽仍然生效 | 探针：Chaser 判定不成立 → 换抽；`铁壁·护甲→铁壁·生命`（成立那次真的落地：生命 900 → 1260）；端到端 3 抽 `疾行 ｜ 尖啸回响 ｜ 铁壁·护甲→铁壁·生命` |
| 硬指标 | `+187.5 → 1 次 / +225 → 2 次 → 累计 3 次` **与表在时逐字相同** |
| 只有 warning、没有 error | 与 Pressure 相关的 warning / error **0 条**，自测走到 `=== 自测结束 ===` |
| ⚠️ 已知不准确的一行（**本轮没改，StageController 不在本轮边界内**） | 清空索引时 `StageController` 那条日志仍写 `（阈值转换 3 次，**来源：表 Pressure**）` —— 这个字符串是硬编码的；真实来源以 `BossPerkCatalog` 打的那一行为准（这一行在回退时写的是"代码兜底"） |

**清空"索引数组"不会让词条消失**（第三/四/五次踩到同一类坑）：行引用写在 `ConfigIndex.tres` 的
`[ext_resource]` 上，数组只是"建索引 + 报数"用的。要真正回退到"这批词条不存在"，
得连 `[ext_resource]` 与行文件一起删。

### 七、顺带说明：为什么技能参数词条**没有**放进 `Skills/`

- §7.1 要求两套来源**可分离**；技能**参数**（冷却/伤害）属数值型，放进 `Skills/` 会让
  `ConfigRows.Count<AbilityConfig>()`（自测的 `25 行`）与 `BossPerkCatalog.BehaviorOnly`（自测的 `4 条`）
  两个既有回归指标**同时漂**，也模糊了"行为 vs 数值"的界线。
- 想让**行为型**词条也带限定词（比如"某个行为词条只对某关生效"）需要给 `AbilityConfig` 加列 ——
  本轮没做（`Scripts/Config/` 的其它行类同时有别的改动在进行）。行为型条目天然的自我限定是
  "它引用的能力节点找不到就写警告跳过"（`EnableAbility` 处理器）。

## 用户三项修正实测（2026-10：护甲下线 + 术语「技能 / 词条」+ 命中特效落在**命中点**）

**需求原话（三句，来自同一轮评审）**：
> ②「**护甲目前就只给精英怪使用就好了，首领不需要**」
> ④「**行为型的词条其实不太对，这个应该是算是技能**」
> ⑤「不需要（细分开关），**实际应该在命中的位置发生特效**」

**编译**：`& build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**（每一步都跑）。
**自测**：`selftest.flag` + gdmcp `run_project`，**三次连跑**全程通过、都跑到「=== 自测结束 ===」并自动退出，`selftest.flag` 跑完即删：
`run_20261012_003652.log` / `run_20261012_003734.log`（这两次的命中特效数字**逐位相同**：`0.026` / `0.400`）/
`run_20261012_004120.log`（这一次探针打到的是**首领**，半径 1.0 → 偏差 `0.040`，旧做法基线 `1.000`）。

### 一、② 护甲只给精英怪：`pmod_fortress_armor` 整行下线（Pressure 8 → 7 条）

一行数据要在**三个地方同时**下线，缺一个就会出问题（第二个是项目反复踩的静默回退坑）：

| # | 改哪里 | 怎么改 | 不改会怎样 |
|---|---|---|---|
| ① | 行文件 `Config/Tables/Pressure/pmod_fortress_armor.tres` | **删除文件**（该目录没有 `.uid`） | 留下没人引用的孤儿行 |
| ② | `Config/ConfigIndex.tres` | **`[ext_resource]` 那一行**（`id="111_pmod_fortress_armor"`）与 **`PressureMods` 数组里那一项**一起删；`load_steps` 114 → 113 | 只删行文件 → **整份 `ConfigIndex.tres` 加载失败**、所有数值静默回退（config.md 顶部那条纪律） |
| ③ | **代码兜底** `Scripts/Boss/BossPerk.cs` 的 `FallbackPressure` | 删掉那条 `BossPerk`（表读不到时用的那份） | 表一读不到，首领**又会吃护甲**（静默换挡）—— 正是阶段 7 注释里防的那类坑 |

**精英怪的护甲来源（读代码确认，没误伤）**：精英护甲**与 Pressure 无关**，来自
`Scripts/Combat/EliteModifier.cs` 的 `ArmorAdd = 120`（由 `EncounterController` 在生成时 `enemy.AddChild(new EliteModifier())` 挂上）。
⚠️ **C1（2026-10-12 第三轮）之后它走 `unit.GrantArmor(ArmorAdd)`**，不再是 `Stats.AddModifier("MaxArmorAdd", …)`
（护甲没有上限，"抬高上限"这套已经没有意义）：`MaxArmorAdd` / `MaxArmorMultiplier` 两个 StatSheet 键**整对删除**，
全项目再无生产者，所以"护甲只给精英怪"这条仍然是代码事实（生产者只剩 EliteModifier 这一处，走 `GrantArmor`）。

**实测**（两次连跑一致）：

```
[Config] 首领强化来源（…§7.1…）：数值型 Pressure（**词条**）7 条（来源：表 Pressure）/ 行为型 Boss Build（**技能**）4 条（来源：表 Skills）
[Config] Pressure 转换池（§7.5…）：候选 7 条 [pmod_berserk_dmg 权重 3 → 23.1% · pmod_berserk_speed 权重 3 → 23.1% ·
         pmod_fortress_hp 权重 2 → 15.4% · pmod_swift 权重 2 → 15.4% · pmod_scream_echo 权重 1 → 7.7% ·
         pmod_charge_impact 权重 1 → 7.7% · pmod_summon_haste 权重 1 → 7.7%]
[SelfTest] 配置表比对·索引：… 技能 25/Pressure 7/…（期望 3/3/10/1/1/25/7/9/9/5/3/11）
[SelfTest] Pressure 词条池（数值型 = 界面上的**词条**）：7 条（期望 7 = 4 数值型参数栏 + 3 技能参数栏）
         ｜ 带限定词的 3 条（期望 3 = 3 技能参数）｜ 明细：[…没有 pmod_fortress_armor…]
[SelfTest] 限定词刷新判定·关卡限定（合成限定词 StageIds=[stg_core]，池里已无行用它）：当前关卡 → 不成立 ｜
         把当前关卡换成 stg_core 再判 → 成立
```

**连带的三处同步**（都是"描述必须跟上数据"）：
- 自测期望串 `…/25/**8**/9/…` → `…/25/**7**/9/…`（`SelfTest.ReportTables`）；探针
  `Pressure 词条池：… 期望 8 = 5+3` → `期望 7 = 4+3`、`带限定词的 4 条` → `3 条`（`BossPerkProbe`）。
- ⚠️ **`StageIds` 现在池里没有行再用它了**（它是 `pmod_fortress_armor` 独有的限定词）。
  字段与判定逻辑**保留**（以后要给某条加关卡限定不用重写判定），探针改成用一个**合成限定词**
  `StageIds=[stg_core]` 继续验证"同一帧换关卡 → 结论不同"，`DebugStageIdOverride` 因此仍然有消费者。
- 行文件里的 Note（`pmod_fortress_hp.tres`）与 `BossPerkCatalog` 的类注释都写明了"护甲那一栏已删除"。
- 权重分母 15 → **13**，所以**每一条的概率都变了**（20% → 23.1% 等）；抽取的**随机序列也会漂**
  （候选少一条）—— 这是"下线一条"的必然结果，不是回归。

### 二、④ 术语：行为型 = **技能**，数值型 = **词条**（只改面向人的文案）

用户指出的问题：HUD 把两类东西**统称"词条"**（`◆` 行为型 / `◇` 数值型），但行为型的那些
**本来就是技能**（`Config/Tables/Skills/bhv_*.tres`，是技能表里的行，首领"学会/觉醒一个技能"）。

**改成的口径**（全项目统一这一条）：

| 来源 | 表 | 界面/日志/文档里的叫法 | 标记 |
|---|---|---|---|
| 行为型 Boss Build | `Config/Tables/Skills/bhv_*.tres`（`ApplyHandler` 非空） | **技能** | `◆` |
| 数值型 Pressure | `Config/Tables/Pressure/pmod_*.tres` | **词条** | `◇` |

**`◆` / `◇` 符号体系保持不变**（判断与理由）：两个记号本来就区分了来源，用户提的是**叫法**不对；
换字形会让既有截图 / 日志 / 文档里的记号失去对应，收益为零。区分手段改成**计数带名词**：

```
词条 4 ◆提前觉醒 ◆冲锋强化 ◇狂暴 ◇疾行×2          ← 改前（两类都叫"词条"，是错的）
技能 2 · 词条 2 ◆提前觉醒 ◆冲锋强化 ◇狂暴 ◇疾行×2   ← 改后（`BossPerkText.Summary` 生成）
```

**改了哪些文件**（**没有**改任何 `bhv_*` 文件名 / 目录 / 类名 / 字段名 —— 用户明确要求不动）：

| 文件 | 改了什么 |
|---|---|
| `Scripts/UI/BossPerkText.cs` | 类注释与两个标记的注释改口径；新增 `Summary()`（"技能 N · 词条 N"）；`FormatLine`/`Lines`/`Total` 的注释同步 |
| `Scripts/UI/PlayerHud.cs` | 血条栏那一行改用 `Summary()`（原来写死 `"词条 {总数}"`）；相关注释 |
| `Scripts/UI/BossPreviewScreen.cs` | 【词条】一节拆成 **【技能】**（行为型 Boss Build）+ **【词条】**（数值型 Pressure）两节 |
| `Scripts/Boss/BossPerk.cs` | 日志（`首领强化来源` / `Pressure 转换池` / 抽取点）、类注释、`Roll()` 的注释改成"开场技能" |
| `Scripts/Config/AbilityConfig.cs` | `[ExportGroup("首领行为技能（Boss Build；…）")]`（**编辑器里能看到的那一行**）+ 注释；⚠️ 只改标题字符串，不改字段名/类型/默认值 |
| `Scripts/Config/{BossConfig,StageDefinition,NodeConfig,PressureModConfig}.cs` | 注释里的"行为词条"→"行为技能"/"开场技能"，`StageDefinition` 的分组标题「开场词条数」→「开场技能数」 |
| `Scripts/Encounter/EncounterController.cs` | 注释与"白板首领"的措辞（`BossPerkCount` 的注释） |
| `docs/config.md`、`docs/boss.md`、`docs/todo-handoff.md`、`docs/art-integration-log.md` | 本文档各处 + 交接文档的任务条目 + 特效那一节 |

**刻意没碰的两处**（写明原因，避免下一个人以为漏了）：
- `Scripts/Run/StageController.cs`：题面边界是"Pressure 结算逻辑只改数据不改算法"，所以哪怕
  那两条日志里还写着"与 Boss Build **行为词条**分开结算"也没动 —— 要改建议单独一次提交、只动字符串。
- `Scripts/Encounter/EncounterController.cs` 的 `SpawnBossNow` 日志 `无压力 / 无词条`：这里的"词条"
  是**泛称"没有任何强化"**（技能与数值都没有），不是把行为型叫成词条，所以保留原样（保持既有日志可比对）。

### 三、⑤ 命中特效：落在**实际命中点**（并删掉 `HitEffectHeightOffset`）

**改前**：`AttackHitbox._PhysicsProcess` 里写的是
`target.GlobalPosition + Up × ConfigIndex.Settings.HitEffectHeightOffset`（默认 1.0）——
即"目标原点（脚底）+ 一个全局胸口高度"。**改后**：`AttackHitbox.ResolveHitPoint(target)`。

**取的到底是什么点**：把**命中体自己的世界坐标**投到**目标碰撞体**上，取**表面上最近的那一点**
（单位预制体里的直接子节点 `Body`：胶囊 → 先投到中轴线段、再沿半径推到表面；球 → 沿半径推；
盒 → 转进局部空间再夹到盒面上；认不出的形状 / 没有碰撞体 → 退回**碰撞体中心**）。
语义就是"这一刀落在身上的哪一面"，而且完全由**碰撞体几何**决定。

**为什么不用射线（试过，被实测否掉）**：重叠判定（`GetOverlappingBodies()`）只回答"撞上了谁"，
**不给接触点**，所以第一版实现是"从命中体朝目标身体中心打一条射线、取 `hit["position"]`"
（与 `Projectile` 同款 API）。**自测里 7 次跑出 3 次"打空"**：射线读的是**物理空间在查询那一瞬间**
的状态，而下面三种情况里脚本位置 ≠ 物理位置 —— 目标被脚本挪动（探针 `GlobalPosition += 10m` 后
同一帧射线直接打空）、刚被冻住、或刚被打死（`Unit.Die()` 把 `CollisionLayer` 归零，射线再也找不到它）。
打空的后果是**静默退化**成兜底点（特效落回身体中心），不报错、不写警告。几何最近点没有这些时序依赖。
（顺带留下的两处纪律：命中点**先算、再 `ReceiveHit`**；射线版本还需要"逐个排除挡路的怪"才能在
人堆里打中目标 —— 几何版不需要。）

**`HitEffectHeightOffset` 的处置 = 删除**（字段 + `ConfigIndex.tres` 的那一行一起删）：它唯一的消费者
就是被替换掉的那一行代码，留着就是"一列改了没用的死配置"（与配置表阶段 3 清理死字段同一条纪律）。
投射物那条路径（`Projectile.ResolveImpact`）**一行没动** —— 它本来就有精确的射线命中点。
⚠️ 红线遵守：`Scenes/Combat/Explosion.tscn` 的 `SphereShape3D radius=1` 与 `Lifetime=0.12` **一个字节没动**
（`Explosion` 继承 `AttackHitbox`，所以爆炸打到谁就用谁的表面点）。

**判据与证据**（探针不重算命中点，而是量"这个点离碰撞体表面有多远"——落进身体里就会 ≈ 半径）：
`SelfTest` 新增命中特效探针：把玩家的近战命中体挪到**目标正东 1.0m**（敌人在命中体盒内、命中体中心在
敌人体外）并激活；目标**先冻住**（`ProcessMode.Disabled`，否则 0.2 秒的结算窗口里它自己会跑掉，
量出来的距离里会混进位移）；结算时读 `HitEffect.RecentSpawns`（DEBUG-only 的出生点记录，
`AttackHitbox` 真的传进去的那个点）+ 场景里新出生的特效节点互为佐证。

```
[SelfTest] 命中特效探针·准备：玩家近战命中体已挪到 FastMelee 正东 1.0m 并激活 ｜ 命中体原点 (2.33, 0, 4.06) ｜
         目标身体中心 (1.33, 0.8, 4.06)（= 它 `Body` 碰撞体自己的位置）· 碰撞体半径 0.4 ｜ 目标已临时冻结（结算时还原）
[SelfTest] 命中特效位置实测（用户要求 5：特效出现在**实际命中的位置**）：正式代码写下的出生点 (1.21, 0.8, 3.71) ｜
         到 FastMelee 碰撞体**表面**的偏差 0.026 米（半径 0.4 → 落在表面上；偏差 ≈ 半径 才叫「掉进身体里」）·
         离地 0.8（身体中心离地 0.8）｜ 对照·旧做法基线（目标原点 + 高度 1.0）到表面偏差 0.4 米（= 掉在身体里、离地写死 1.0）｜
         目标这次被打死了=True ｜ 旁证·同批新出生节点 4 个：[「VFXHit_02」(3 粒子) 距身体中心 0.66 · …]
```

读法：**新做法 0.026 米**（= 贴在碰撞体表面上，残差来自命中体自己在这 0.2 秒里被玩家带着走了 2~3 厘米）；
**旧做法 0.4 米**（正好等于胶囊半径 —— 点落在中轴上，也就是**埋在身体里**）。
两次连跑数字**逐位相同**（`0.026` / `0.400`），这条现在是可回归的硬指标。
**换一种目标也一样**（第三次跑打到的是首领：半径 1.0 的胶囊，偏差 `0.040`、旧做法 `1.000`，
且那一次**没打死**它）—— 所以"打死"与"没打死"两种结算路径都验过：

```
[SelfTest] 命中特效位置实测：正式代码写下的出生点 (2.64, 1.2, 1.05) ｜ 到 Boss 碰撞体**表面**的偏差 0.04 米
         （半径 1 → 落在表面上；偏差 ≈ 半径 才叫「掉进身体里」）· 离地 1.2（身体中心离地 1.4）｜
         对照·旧做法基线（目标原点 + 高度 1.0）到表面偏差 1 米（= 掉在身体里、离地写死 1.0）｜
         目标这次被打死了=False ｜ 旁证·同批新出生节点 1 个：[「@Node3D@19」(3 粒子) 距身体中心 1.31]
```

`HitEffect.RecentSpawns` 是 `#if DEBUG` 的取证字段（Release 构建里不存在），正式逻辑从不读它。
**术语那一路也有日志覆盖**（否则"技能 + 词条并存"这条分支永远走不到）：

```
[SelfTest] 强化显示（血条栏一行，上限 5：同名折叠成 ×N、超出折叠成 …+N）：词条 3 ◇狂暴·伤害×2 ◇狂暴·攻速（本关抽到 3 条…）
[SelfTest] 强化显示（技能 + 词条并存，计数名词 = 「技能 N · 词条 N」）：技能 4 · 词条 3 ◆提前觉醒 ◆召唤师 ◆尖啸强化 ◆冲锋强化 ◇狂暴·伤害×2 …+1
[SelfTest] 首领状况面板（… ／ 【技能】（行为型 · Boss Build：首领降临时抽，学到就是它的了） ／ ◆ 首领降临时抽 1 条（现在还没有） ／
           【词条】（数值型 · Pressure 阈值转换：一个参数一栏） ／ ◇ 词条 3 条（首领降临时套用） …）
```

### 四、这一轮**没有**做的事（留给下一轮决定）

- ✅ **首领本体的护甲已在本轮去掉（用户明确要求「去掉首领的甲」）** —— 上一轮记的"没动"这一条**已作废**：
  `boss_final.tres` 的那一列现在是 **0**（C1 之后叫 `StartingArmor`，值不变）、`Boss.tscn` 那份同值导出也一并归零（表是权威、场景是回退，两处必须同值）。
  ⚠️ 当时"护甲字段一个都没删"（`Unit.MaxArmor/Armor/ArmorDamageReduction` + `UnitConfig.MaxArmor/ArmorDamageReduction`
  全部原样保留）—— 用户澄清："护甲是**所有单位**都能有的通用属性，只是首领**默认没有获取手段**"。
  **2026-10-12 第三轮 C1 覆盖了后半句**：用户改口「**护甲没有上限额度，所以可以删除掉 `MaxArmor` 的**」，
  于是 `MaxArmor` → **`StartingArmor`（开局护甲）**、`MaxArmorMultiplier` / `MaxArmorAdd` **删除**
  （`Armor` / `ArmorDamageReduction` / `ArmorBroke` / `DamageResult.ArmorLost` 全部保留），详见文末「护甲无上限实测」。
  完整判据、伤害管线怎么走、以及"还有谁能给首领加甲"的 grep 结论见文末「首领去甲 + 单位动画实测」。
- ⚠️ **Pressure 的强度需要重新平衡**：池子从 8 条变 7 条，单条概率整体上扬（20% → 23.1% 等），
  且阶段 7 已知的"每抽期望强度只有拆分前的 58.9%"这条结论**不受影响**（它由"一抽只买一个参数"决定）。
- `StageController` 里那两条日志的措辞没有跟着改（边界约束，见 §二末尾）。

## 首领去甲 + 单位动画实测（2026-10）

> 本轮两件事：① 用户要求「**去掉首领的甲**」；② 用户要求「**单位动画系统**」（原话："都要做但是可以按你理解来决定
> 哪些要先做，反正就是起码位置都先占了有个好的示范例子方便后续增量的维护"）。
> 边界：C# 只改 `Scripts/Combat/UnitAnimator.cs`（新建）/ `UnitPresentation.cs` / `Scripts/Config/UnitConfig.cs`；
> 数据只改 `Config/Tables/**` + `Scenes/Player/Player.tscn` + `Scenes/Enemies/*.tscn`。
> **没有碰** `Scenes/UI/**`、`Assets/UI/**`、`Assets/Audio/**`、`Assets/Fonts/**`、`Scenes/Levels/TestArena.tscn`。

### 一、首领去甲：权威在哪、改了什么

> ⚠️ 本节的列名按**当时的** `MaxArmor` 记录；**C1（2026-10-12 第三轮）之后同一列叫 `StartingArmor`**（值不变），
> `MaxArmorMultiplier` / `MaxArmorAdd` 已删除 —— 见文末「护甲无上限实测」§一。

| 问题 | 结论 |
|---|---|
| **权威在哪** | **`Config/Tables/Bosses/boss_final.tres` 的 `MaxArmor`（现名 `StartingArmor`）**。生成路径 `EncounterController.SpawnBossNow()` 是 `Instantiate → BossRow(BossScene)?.ApplyTo(Boss) → AddChild`（`EncounterController.cs:708-712`），`UnitConfig.ApplyTo` 里 `unit.ApplyBaseStats(...)` 会把基础护甲与 `StartingArmor` 一起写成行里的值，而 `Unit._EnterTree` 在 `AddChild` 时才捕获基础值 → **行覆盖场景**。白板首领副本（`SpawnEliteBossNow`）走同一条路（`:670-672`）。 |
| **场景那一份** | `Scenes/Enemies/Boss.tscn` 当时那份 `MaxArmor = 200.0` 是**回退值**（表读不到时才生效）。两处不同值 = 项目反复警告的"**静默换挡**"，所以本轮**两处一起归零**：行里写 `MaxArmor = 0.0`（现名 `StartingArmor = 0.0`，与 `enemy_chaser.tres` 等 0 甲行的写法一致），场景里**删掉那一行**（0 甲的敌人场景都不写它）。 |
| **字段有没有被删/摘掉** | **当时完全没有**（这一轮只改数值）。⚠️ **C1 之后不是这样了**：`Unit.MaxArmor` / `UnitConfig.MaxArmor` 改名为 **`StartingArmor`**，`MaxArmorMultiplier` / `MaxArmorAdd` **删除**；`Unit.Armor` / `ArmorDamageReduction` / `ArmorBroke` 事件 / `DamageResult.ArmorLost` / HUD 的"甲 N" / `BossPreviewScreen` 的护甲一栏 —— **仍然一个都没动**。 |
| **还有谁能给首领加甲？** | 全项目 grep（`MaxArmorAdd` / `MaxArmorMultiplier` / `ArmorAdd` / `MaxArmor`、`Scripts/**` + `Config/**` + `Scenes/**`）只有**一个**生产者：`Scripts/Combat/EliteModifier.cs`（`ArmorAdd = 120`；C1 之后走 `Unit.GrantArmor`）。它**只挂在普通敌人身上**：`EncounterController.cs:454-457`（`SpawnEnemy` 里按 `eliteChance` 添加）与 `Scenes/Enemies/SkillElite.tscn` 里手挂的那一个（该场景写 `ArmorAdd = 0.0`）。首领那条路（`SpawnBossNow` / `SpawnEliteBossNow`）**不加** `EliteModifier`。另外查过三个"看起来会加甲"的地方，结论都是**不加**：`BossPressure`（只加 `DamageMultiplier` / `AttackSpeedMultiplier` / `MoveSpeedMultiplier`）、`ConfigIndex.ScaleEnemy`（生命/伤害/移速）、Pressure 池 7 行（`DamageMultiplier` / `AttackSpeedMultiplier` / `MaxHealthMultiplier` / `MoveSpeedMultiplier` + 3 条技能参数）。**结论：首领现在没有任何获取护甲的途径。** |
| **精英的甲** | **保留不动**（`EliteModifier.ArmorAdd = 120`）—— 它是精英怪的获取手段，与首领无关。C1 之后它是**直接加甲**（`GrantArmor`），语义比原来更准确。 |

**去掉之后伤害管线怎么走**（`Unit.ReceiveHit`，战斗规则说明 §12 的顺序一个字没改）：

```
命中 → OnHit（HitReceived + GameplayEventKind.Hit）→ IsInvulnerable? 停
     → Block（Boss 没有 Block 组件 → 系数 1）
     → DR：CombinedDamageReduction() = 1-(1-护甲减伤)(1-额外减伤)
          ⚠️ 护甲减伤那一项是 `Armor > 0f ? ArmorDamageReduction : 0f` → 甲为 0 时**自动**取 0，
             所以 Boss.tscn 里那句 `ArmorDamageReduction = 0.3` 变成惰性值（不必删，也不会偷偷减伤）
     → Armor 阶段：`Armor > 0f` 为假 → **整段跳过**（不再吸收伤害，也不再发 `ArmorBroke` 事件）
     → Shield 阶段：Boss 护盾 0 → 跳过
     → HP
```

数值影响（**这是有意的数值变化，不是回归**）：首领从"先吃 200 点护甲吸收 + 全程 30% 减免"变成
**全额吃伤害**。以玩家手枪 24 伤害为例：改前每发先扣甲、血只掉在甲耗尽后；改后每发直接进血。
**没有做任何补偿性调参**（例如调 `BossHealthMultiplier` 或 `boss_final` 的 `MaxHealth`）—— 那是数值决策，留给下一轮。

**实测证据**：
- `build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**。
- 自测运行日志里，探针把行套到 Boss 上之后打印 `→ Boss 生命 900 护甲 0 ...`（改前是 `护甲 200`）；
  同一次运行 `ERROR/WARNING` 命中 **0** 条。
- 归零的是**数据**，所以"回退路径"这次是**同值**的：表读不到时场景也是 0 → 不存在"表没了首领又长回 200 甲"的静默换挡。

### 二、单位动画：`UnitAnimator` 的接入契约（后续加敌人照这个抄）

**新增文件**：`Scripts/Combat/UnitAnimator.cs`（约 300 行，一个 `Node` 组件）。

| 问题 | 答案 |
|---|---|
| **它做什么** | 按**移动速度**在 `idle` / `walk` / 跑 之间切；死亡时播一次倒地动画（`LOOP_NONE`，停在最后一帧，撑到 `UnitPresentation.DeathLinger` 到期）。速度读 `Unit.Velocity` 的平面分量，每物理帧比一次阈值。 |
| **循环** | 必须由代码设：`.glb` 导入的 32 段**全是 `LOOP_NONE`**，所以场景里那句 `autoplay="idle"` 只是摆了个**静止姿势**。`Bind()` 里把 idle/walk/跑 三段设成 `Linear`、`die` 保持 `None`。 |
| **新敌人要做什么** | **什么都不用做**。只要场景里 `Model` 子树里有 `AnimationPlayer`（`.glb` 实例根下 `index="1"` 那个），动画就自动生效：`UnitPresentation._Ready` 会调 `UnitAnimator.Ensure(unit)`，**没挂就补一个**。 |
| **新敌人可选做什么** | ① 想改名字/阈值：在 `Config/Tables/Enemies/enemy_xxx.tres` 的新「表现 · 动画」6 列里填（**空 / 0 = 不覆盖**）；② 想在编辑器里直接改：给场景挂一个 `Animator` 子节点（本轮 11 个单位场景都显式挂了，右侧 Inspector 有中文分组）。 |
| **不用做什么** | 不用写任何 C#、不用改 10 个敌人场景的其它地方、不用在生成路径（`EncounterController` / `SummonAbility`）里加一行。 |
| **读不到表会怎样** | 走组件自身的 `[Export]` 默认值：`idle` / `walk` / `sprint` / `die`，阈值 **0.15 / 4**。名字解析顺序 = 精确匹配 → **忽略大小写**（新素材写 `Idle`/`Run` 也能直接用）→ 退回上一档（跑→走→待机），退回时写一条警告（**每种单位只提醒一次**，不刷屏）。 |
| **没有 AnimationPlayer 的单位** | 静默不参与（木桩、无人机…），零每帧开销，不报错。 |
| **日志** | 每种场景各一行：`单位动画：Chaser 共 32 段 · idle=idle walk=walk run=sprint die=die · 阈值 0.15/4（来源：组件默认值 或 表 enemy_chaser）`；每次**切换**再一行（每种单位每种切换只记一次，不会刷屏）。 |
| **"跑"是哪个动画** | Kenney Mini Characters 的 32 段里**没有 `run`**，跑就是 **`sprint`**（列名仍叫 `AnimRun`，语义是"跑"）。 |

**参数进了哪张表**：`Scripts/Config/UnitConfig.cs` 新增一组 `[ExportGroup("表现 · 动画（按移速在 idle/walk/跑 之间切；空 = 不覆盖，用 UnitAnimator 的默认值）")]`
—— 6 列，**只加列**（没有改任何已有字段的名字/类型/默认值）：`AnimIdle` / `AnimWalk` / `AnimRun` / `AnimDeath`
（字符串，空 = 不覆盖）+ `AnimWalkMinSpeed` / `AnimRunMinSpeed`（float，0 = 不覆盖）。
套用点在 `UnitConfig.ApplyTo` 末尾（新增的 `ApplyAnimationTo(unit)`）：
**一行都不填 → 连组件都不建**，完全交给 `UnitPresentation` 的自动补挂 + 组件默认值。

> ⚠️ **一个已知的"半生效"边界（写清楚，不留陷阱）**：**玩家那 3 行（`chr_*.tres`）的动画列当前不生效** ——
> `RunController.ApplyBalance` 与"攻击组"那处一样，只调 `ApplySkillsTo` / `ApplyAttackTo`，**不调** `ApplyTo`
> （`RunController.cs:111-117`）。玩家的动画参数在 `Scenes/Player/Player.tscn` 的 `Animator` 节点上
> （同一套组件、同一套默认值，所以行为与"填了默认值"逐位一致）。
> **要让它也生效 = 一处一行的改动**：在 `RunController.ApplyBalance` 的 `character != null` 分支里加
> `character.ApplyAnimationTo(_player);`。本轮边界不含 `RunController.cs`，所以没动。

### 三、实测证据

**编译**：每一步都跑 `& build.ps1 --no-incremental -v minimal` → 全程 **0 warning / 0 error**。

**单位绑定 + 速度切换**（`run_20261012_010245.log`，`Scenes/Enemies/_AnimShowcase.tscn` 展示场景：

```
01:02:45 [Combat] 单位动画：FastMelee 共 32 段 · idle=idle walk=walk run=sprint die=die · 阈值 0.15/4（来源：组件默认值）
01:02:45 [Combat] 单位动画：Chaser 共 32 段 · … 阈值 0.15/4（来源：组件默认值）
01:02:45 [Combat] 单位动画：Guard 共 32 段 · … 阈值 0.15/4（来源：组件默认值）
01:02:45 [Combat] 单位动画：Screamer 共 32 段 · … 阈值 0.15/4（来源：组件默认值）
01:02:45 [Combat] 单位动画切换：FastMelee idle → walk（当前速度 1，阈值 0.15/4）
01:02:45 [Combat] 单位动画切换：Chaser idle → walk（当前速度 1，阈值 0.15/4）
01:02:45 [Combat] 单位动画切换：Guard idle → walk（当前速度 1，阈值 0.15/4）
01:02:45 [Combat] 单位动画切换：Screamer idle → walk（当前速度 1，阈值 0.15/4）
01:02:45 [Combat] 单位动画切换：FastMelee walk → sprint（当前速度 4，阈值 0.15/4）
01:02:45 [Combat] 单位动画切换：Chaser walk → sprint（当前速度 4，阈值 0.15/4）
01:02:48 [Combat] 单位动画切换：FastMelee sprint → walk（当前速度 3.5，阈值 0.15/4）
01:02:48 [Combat] 单位动画切换：Chaser sprint → walk（当前速度 3.2，阈值 0.15/4）
01:02:48 [Combat] 单位动画切换：FastMelee walk → idle（当前速度 0，阈值 0.15/4）
01:02:49 [Combat] 单位动画切换：Guard walk → idle（当前速度 0，阈值 0.15/4）
```

读法：**4 个单位在同一帧越过 4.0 → 4 个 `walk → sprint`**（`FastMelee` 6.5、`Chaser` 4.2 都 ≥ 4；
`Guard` 2.6、`Screamer` 3.0 一直 `walk`）；减速回到 3.5 / 3.2 时**又切回 `walk`**（证明是真按速度算的，不是一次性状态）。

**死亡**（同一批自测日志）：`Chaser idle → die`、`Boss idle → die`、`Exploder idle → die`、
`@CharacterBody3D@5 idle → die`（首领召唤出来的小怪也自动有动画 = 自动补挂这条路真的走到了）。

**表里的列真的是旋钮**（一次临时实验，做完已还原 —— 与阶段 4 的"两次临时实验"同一套做法）：

| 步骤 | 做法 | 实测 |
|---|---|---|
| 正向 | 给 `enemy_chaser.tres` 临时填 `AnimRunMinSpeed = 1.0` | `单位动画：Chaser … 阈值 0.15/1（来源：表 enemy_chaser）`；同一帧其它单位仍是 `阈值 0.15/4（来源：组件默认值）` → **逐行生效**；行为也变了：`Chaser idle → sprint（当前速度 1，阈值 0.15/1）`（默认阈值 4 时这一步只会是 `→ walk`） |
| 还原 | 删掉那一列 | 又变回 `Chaser … 阈值 0.15/4（来源：组件默认值）`（`run_20261012_010000.log` 里 9 个单位全是这一条） |

**截图**（`Scenes/Enemies/_AnimShowcase.tscn` = 本轮新增的展示台：一块地面 + 固定侧视相机 + 4 个单位 + 一个
玩家阵营的木桩目标，单位会自己走过去；留着给以后换模型/调动画当对照台）：
[anim_01_walk_showcase.png](screenshots/anim_01_walk_showcase.png) —— `Screamer` 迈步中（一条腿在前、一条在后，
不是旧的"站姿定格"）；[anim_02_walk_showcase_next.png](screenshots/anim_02_walk_showcase_next.png) —— 0.4 秒后的
同一批单位，腿部姿势已经换了一档（**两帧不同 = 动画真的在播**，而不是停在 `autoplay="idle"` 的那一帧）。

**自测**：`selftest.flag` + `run_project` / 直接跑 `Main.tscn` 各跑过整轮，日志里有
`=== 自测开始 ===` … `=== 自测结束 ===`、`Encounter 清场完成`、`端到端：按 F 之后 奖励界面打开 = True`，
`ERROR/WARNING` **0 条**；`selftest.flag` 跑完即删（`Test-Path` 复查 False）。

### 四、这一轮**没有**做的事（留给下一轮）

- ⚠️ **玩家那 3 行的动画列不生效**（原因与一行修法见上面 §二末尾那个引用块）—— 需要改 `RunController.cs`。
- ⚠️ **掉落 / 跳跃 / 攻击动画没接**：`.glb` 里现成还有 `jump` / `fall` / `attack-melee-right` 等 28 段。
  接入点已经占好（`UnitAnimator` 是唯一改动画的地方），但要新增"空中/出招"两个状态机分支 + 各自的列。
- ⚠️ **动画播放速率没有跟随移速**（`SpeedScale` 固定 1）：7 m/s 的玩家用 `sprint` 仍然会有一点点滑步。
  要调就在 `UnitAnimator` 里加一处 `SpeedScale = clamp(speed / 参考速度)`；本轮没有可靠数据定"参考速度"，所以故意留空。
- ⚠️ **首领去甲之后没有补偿调参**：首领的有效血量明显下降（少 200 点吸收 + 少 30% 减免），要不要上调
  `MaxHealth` 或 `BossHealthMultiplier` 是数值决策。

## 本轮五项修正实测（2026-10：需求包 §五 的 #2 / #5 / #7 / #11 / #12）

需求文档：`docs/requirements-ability-system.md` 第五节。本轮只做其中**不依赖新系统**的五项。

**编译**：`& build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**（每改一步都跑，全程 0/0）。

### 一、五项各自改了什么

| # | 内容 | 落点 |
|---|---|---|
| **2** | 跳跃高度 +40% / 速度 +100% | 角色表新增两列 `JumpHeightMultiplier = 1.4` / `JumpSpeedMultiplier = 2.0`（三行都填）+ `CharacterMovement.ApplyJumpConfigDeferred`（`CallDeferred`，只有玩家读表）。见上面「跳跃参数」一节。⚠️ **这两列已在 2026-10-12（用户拍板 A2）整段删除**，改成固定值 `JumpVelocity` / `JumpGravityScale` —— 见下面「用户 A2/A3 + 需求 #6 实测」 |
| **5** | 白板首领只在第 8 波出现 | 关卡表新增列 `EliteBossMinWave = 8`（三行都填）+ `EncounterController.MinWaveForEliteBoss` / `WavesOpenedBefore` / `WavesTotal` 门槛判定 + `StageController._wavesOpenedInStage` 累计。见上面「白板首领副本」一节 |
| **7** | 节点波数按价值标签（高 4 / 中 3 / 低 1） | 新增**价值标签表** `Config/Tables/ValueTags/tag_*.tres`（3 行）+ `NodeConfig.ValueTags` 列（9 行都填）+ `NodePlacement.WavesFor` 改成三层取值。见上面「价值标签表」一节 |
| **11** | 准星改回一个点 | `Assets/UI/Textures/tex_ui_crosshair_dot.png`（48×48，实心白点）+ `Scenes/UI/Hud.tscn` 的 `Root/Crosshair` 换成它（`stretch_mode = 5`）。旧的 Kenney 描边十字 `tex_ui_crosshair_default.png` 已删（全项目只有 `Hud.tscn` 引它） |
| **12** | 打 Boss 后卡在「挑战者状态」 | `StageController.ChallengerActive` 被判成"挑战中"的条件收窄（见下） |

### 二、#12 的根因（用户报的 bug，已复现并修掉）

**根因**：`ChallengerActive` 原来的式子是

```csharp
_encounterRunning || (_bossFight && !IsVictory)
```

而 `IsVictory` **只有互动降临节点那一下才会变 true** —— 于是形成一个死循环：

```
玩家开 Boss 门 → _bossFight = true
Boss 阵亡      → _bossCleared = true（但 _bossFight 仍是 true，IsVictory 仍是 false）
                → ChallengerActive 仍然 true
ChallengerMark → 脚下那圈「挑战中」标记一直亮着
玩家按 F 想进降临节点 → Interactable.Interact 的挑战者闸门把它拒掉
                → 而 IsVictory 只能靠这次交互翻 → 永远卡住
```

**复现证据（修复前的实测日志，自测阶段 18–22）**：

```
🔴 Boss 阵亡 → 挑战者状态=False（期望 False…）      ← 这是修好之后的输出
降临节点：找到=True 提示=[按 F 使用：前往下一关]
🔴 互动降临节点：接受=True（期望 True）
```

修复前同样的位置是：`挑战者状态=True` + `「前往下一关」拒绝交互：挑战尚未完成（挑战者状态）`，
`accepted=False`，关卡再也推进不了。

**修法**：`ChallengerActive` 现在把 `_bossCleared` 也算进"这一战结束了"：

```csharp
!IsVictory && (_encounterRunning
    || (_bossFight && !_bossCleared && _bossEncounter?.Boss is { } boss && !boss.IsDead))
```

语义上 Boss 战**从来不是**节点挑战（和 `_encounterRunning` 是两条线），只有"还有 Boss 活着"期间才算挑战中。
`!IsVictory` 仍放在最前面（本关已通关时任何挑战态都不算数），只是它不再兼任"Boss 打完了吗"的判断。

### 三、自测新增的四段硬指标

`Scripts/Testing/SelfTest.cs` 阶段 13–22 是本轮新加的（阶段号有重排，末尾的"自测结束"现在是 23）：

| 阶段 | 验什么 | 实测输出 |
|---|---|---|
| 13–17 | **白板首领闸门**：把门槛临时压到 1（跑完还原），清空后必须真的降临 | `白板首领降临（血量倍率 0.35）` / `本场第 1 波后降临=True（期望 True）场上存活=1（期望 1）` / `白板首领结算后：本关累计第 N 波（进这一段时是 N-1，期望 +1）` |
| 18–22 | **#12 的复现三步**：开门 → 打死 Boss → 进降临节点 | `挑战者状态=True`（Boss 活着）→ `挑战者状态=False`（Boss 阵亡）→ `互动降临节点：接受=True` / `关卡 1→2（期望 +1）` |

两处**必须遵守的顺序/前提**（踩过，都写进代码注释了）：

1. 阶段 13–17 必须排在"开 Boss 门"**之前** —— 开门之后 `CurrentNode` 会指向 Boss 那条 Encounter，
   而且 `OpenBossDoor` 会把普通 Encounter 冻住（`StopSpawning`），那之后就验不了普通节点。
2. 阶段 19 **把玩家挪到 (18, 0.8, 18)** —— Boss 与护卫是"在玩家附近"生成的，满压 Boss 攻速可能到 ×2 以上、
   2~3 个技能精英护卫也在打，站在它脸上必被秒（跑过好几次都是这么废掉的：玩家一死整局结束、
   后面阶段全废，最后一次连降临节点都没生成）。随后阶段 20 **每帧**打一次直到 Boss 阵亡
   （生成延迟与 AI 出手的相对顺序不确定，只打一帧会漏）。
3. 阶段 23 退出前先 `GetTree().Paused = false`：这一段之前刚切过关，`UiRouter` 正把树暂停着，
   不解除就退不出去（日志会缺"自测结束"那一行、进程会留着）。

### 四、自测硬指标：哪些**没漂**、哪些**按预期变了**

**逐位一致（没漂）**：

```
配置表比对·索引：武器 3/角色 3/敌人 10/首领 1/召唤物 1/技能 25/Pressure 7/效果 9/节点 9/局外 5/升级池 3/AI 档案 11（期望 3/3/10/1/1/25/7/9/9/5/3/11）
节点布置：启用 10 个（高 2 / 中 5 / 低 3）→ 期望 10（2/5/3）
Pressure 测试（进化 150×1.25 + 异化 150×1.5）：… pressure=3 pmods=3 bank=0（期望 pressure=3 pmods=3 bank=0）
节点间距：最小 9.74（NodeShop↔NodeHeal），阈值 8
```

> 「节点布置 2/5/3」没变是因为本轮**没有动任何一个节点的 `Layer`**：
> `node_weapon` 我一度想按"高价值"给它 `Layer = 2`，但那会让 2/5/3 变成 3/4/3（硬指标漂），
> 而且文档 L87-93 的分层只有进化/异化算高价值 —— 所以 `node_weapon` 留在 Layer 1（`tag_mid`，3 波）。

**按预期变了（并说明为什么）**：

| 硬指标 | 旧值 | 新值 | 为什么 |
|---|---|---|---|
| `波数映射：进化 / 异化 / 强化 / 商店` | 3 / 3 / 2 / 2 | **4 / 4 / 3 / 3** | 用户要求高 4 / 中 3（`tag_high = 4` / `tag_mid = 3`），进化与异化挂 `tag_high`、强化与商店挂 `tag_mid` |
| `配置表比对·节点表：进化层/波` | `2/3` | `2/4` | 同上（层没变，波数按标签表） |
| `配置表比对·节点表：强化层/波` | `1/2` | `1/3` | 同上 |
| 白板首领 | 每场末尾必出 | 本关累计 ≥ 8 波才出 | 用户要求「基本只在第 8 波怪刷出」 |

**新增的硬指标行**（原来没有这三行，本轮加的）：

```
配置表比对·跳跃：起跳速度 7.69（期望 7.69 = 场景 6.5 × √1.4）空中加速 30（期望 30）最大下落 120（期望 120）重力缩放 1.8（期望 1.8，没动过）
价值标签表：标签 [tag_high=4波 tag_low=1波 tag_mid=3波]（期望 tag_high=4 tag_low=1 tag_mid=3）→ 节点 [Weapon=3 Enhancement=3 Evolution=4 Mutation=4 Resource=1 Heal=1 Special=1 Shop=3 Portal=1]（期望 …）
配置表比对·价值标签：（同上，独立第二次取样）
```

自测整轮 `ERROR / WARNING` **0 条**，跑完 `user://selftest.flag` 已删（`Test-Path` 复查 `False`）、进程自行退出。

### 五、这一轮**没有**做的事（留给下一轮）

- ⚠️ **跳跃的"+40% 高度"与"+100% 速度"没有物理上同时兑现**（见上面「跳跃参数」的警告框）：
  现在高度 ×1.4、速度（空中加速 / 下落）×2.0。若用户要的是"起跳速度 ×2"（=> 高度 ×4），改一个数即可。
- ⚠️ **准星只是一个点，没有跟随散布/瞄准变化**（原来也没有；`Hud.tscn` 的 `Crosshair` 仍是纯 `TextureRect`）。
- ⚠️ **白板首领的"第 8 波"是**本关累计**而不是"某个 Encounter 的第 8 波"（原因见上）。
  当前三关的节点波数（1～4）加起来最多 16 波，所以第 8 波通常落在第 3～4 个节点上。
- ⚠️ **`Weapon` 节点已按用户 2026-10-12 的拍板（A3）改成 `tag_high` / `Layer = 2` / 4 波**：
  用户明确"没有武器池这个东西，就是进化池、算高价值"（见上面「节点表」「价值标签表」）。
  自测的「节点布置 10（高 2 / 中 5 / 低 3）」**没有漂** —— 场景里没有 `Kind = Weapon` 的实例；漂的是
  `价值标签表 → 节点 [Weapon=4]`。详见下面「用户 A2/A3 + 需求 #6 实测」。

## 配置表阶段 8 实测（2026-10：能力卡 = 局外装备层）

**做了什么**：新增第 17 张表 `Config/Tables/AbilityCards/card_*.tres`（8 行）+ 局外成长表第 6 行
`Config/Tables/Meta/meta_quota.tres`（额度扩增）+ `ConfigIndexData` 两个字段
（`AbilityCards` 索引数组、`AbilityQuotaBase = 6`）+ 4 条 `ui_card_*` 音效行 + 8 张卡图标 SVG。
**完整说明、表结构、加卡步骤、界面与交互的实现见 [ability-cards.md](ability-cards.md)。**

**编译**：`& build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**（每改一步都跑，全程 0/0）。

**开局日志**（新的两行）：

```
[Config] 能力卡（局外装备层，与 Skills 是两层）：8 张（来源：表 AbilityCards）—— [card_vitality（2 点/解锁 30/上限 Lv5）、
         card_damage（3 点/解锁 40/上限 Lv5）、card_speed（2 点/解锁 30/上限 Lv5）、card_crit（2 点/解锁 35/上限 Lv5）、
         card_reload（2 点/解锁 25/上限 Lv5）、card_magazine（2 点/解锁 30/上限 Lv5）、card_summon（4 点/解锁 60/上限 Lv3）、
         card_control（4 点/解锁 60/上限 Lv3）]；额度上限 = 基础 6 + 扩增行 meta_quota 每级 +1（1 级价格 30，每级 +20）
[Config] 配置表：武器 3 / 角色 3 / 敌人 10 / 首领 1 / 召唤物 1 / 技能 25 / Pressure 7 / 效果 9 / 节点 9 / 局外成长 6 / AI 档案 11 / 能力卡 8（ConfigIndex.tres 已加载）
[Audio] 配置表：音效 9（来源：表 Audio）—— … ；ui_card_unlock(Ui)、ui_card_upgrade(Ui)、ui_card_fail(Ui)、ui_card_reset(Ui)
```

**自测（`selftest.flag` + 直跑 `res://Scenes/Main.tscn`）实测**：

```
[SelfTest] 配置表比对·索引：武器 3/角色 3/敌人 10/首领 1/召唤物 1/技能 25/Pressure 7/效果 9/节点 9/局外 6/升级池 3/AI 档案 11/能力卡 8
           （期望 3/3/10/1/1/25/7/9/9/6/3/11/8）
[SelfTest] 配置表比对·能力卡：8 张（来源：表 AbilityCards） 额度上限 6 = 基础 6 + 扩增 Lv0 × 1（扩增行 meta_quota 费用 30）
           全部卡 Id [card_vitality,card_damage,card_speed,card_crit,card_reload,card_magazine,card_summon,card_control]
[SelfTest] 能力卡·冒烟⑪ 额度不足（上限 6）：装下 2 张后额度 5/6，再激活「疾风之靴」（要 2 点）→ QuotaFull（期望 QuotaFull）｜额度未变 5/6
[SelfTest] 能力卡·冒烟⑮ 还原后：额度 0/6（扩增 Lv0）｜碎片 279｜…（期望与起点一致；内存钱包剩 970，玩家的 user:// 文件未改动）
[SelfTest] 能力卡实测（激活真的影响局内）：强化肌体（card_vitality Lv2）MaxHealthAdd +40 → 最大生命 270 → 310（+40）
           ｜ 无人机协议（card_summon Lv1）授予 skl_summon → AbilitySummon.Enabled False → True … ｜ 清理后最大生命 270（期望还原到 270）
```

**⚠️ `配置表比对·索引` 的期望值变了（加表的必然结果，已同步）**：
`局外 5 → 6`（新增 `meta_quota` 行）+ 末尾多一项 `能力卡 8`。

| 硬指标 | 改动前（`run_20261012_185256.log`） | 本轮（`run_20261012_191238.log`） | 判定 |
|---|---|---|---|
| 节点布置 | 启用 10 个（高 2 / 中 5 / 低 3） | 启用 10 个（高 2 / 中 5 / 低 3） | **逐位一致** |
| Pressure 测试（进化+异化） | pressure=3 pmods=3 bank=0 challenges=2 … | 同 | **逐位一致** |
| 端到端按 F | 奖励界面打开 = True Paused = True | 同 | **逐位一致** |
| 配置表比对·玩家 | 生命 270（250 + 元进度加成 20） | 生命 270（同上） | **逐位一致** |
| 配置表比对·索引 | …局外 5/升级池 3/AI 档案 11 | …局外 6/升级池 3/AI 档案 11/能力卡 8 | **预期内变化**（新增一行表 + 新表） |

整轮 `ERROR / Exception` **0 条**，`selftest.flag` 已删（`Test-Path` = False）。

**回退路径（本轮 ⚠️ 只做了代码层设计，没有主动跑一遍空表）**：与其它表同一条纪律 ——
`AbilityStore.Rows` 直接读 `ConfigIndex.Settings.AbilityCards`，表读不到就是空数组：
矩阵只显示**占位卡**、`AbilityEffects` 一条效果都不加、日志写 `能力卡：… 0 张（来源：代码默认值（表读不到））`，
玩法其余部分（节点布置 / Pressure / 端到端）不受影响；`meta_quota` 行读不到时 `QuotaPerLevel` 兜底为 1（与表里一致）。
要主动验证：把 `ConfigIndex.tres` 的 `AbilityCards` 数组项与顶部对应的 `[ext_resource]` **一起**删掉再跑一轮自测
（⚠️ **只删数组项不删 `ext_resource`、或只删行文件不删引用，都会让整份 `ConfigIndex.tres` 加载失败**）。

## 用户 A2 / A3 + 需求 #6 实测（2026-10-12）

需求文档：`docs/requirements-ability-system.md` 的「用户拍板与新增需求（2026-10-12 第二轮）」A2 / A3 与 §五 #6。

**编译**：`& build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**（每改一步都跑，全程 0/0）。

### 一、三项各自改了什么

| # | 内容 | 落点 |
|---|---|---|
| **A2** | 跳跃**去掉倍率运算**、改成**直接改固定值**（起跳速度要快、过程不要漫长） | 角色表**删两列**（`JumpHeightMultiplier` / `JumpSpeedMultiplier`）**加两列**（`JumpVelocity = 13.0` / `JumpGravityScale = 3.6`，三行都填）+ `CharacterMovement.ApplyJumpConfigDeferred` 改成直接赋值。见上面「跳跃参数」一节 |
| **A3** | 「武器池」不存在 → 它就是**进化池**，且算**高价值**（4 波） | `Config/Tables/Nodes/node_weapon.tres`：`Title`/`DisplayName` → **进化池**、`Layer = 2`、`WaveCount = 4`、`ValueTags = tag_high`；`RewardService.FallbackTitle(Weapon)` → `"进化池"`；代码注释与文档里的"武器池"字样一并清理。**目录 `UpgradePools/` 与类 `UpgradePoolConfig` 刻意没动**（那是第三批刚落地的"池"的通用名） |
| **#6** | 关卡内**定期刷怪** + **每 120 秒增强** | 新增 4 个全局标量（`AmbientSpawnInterval` / `AmbientSpawnEscalateSeconds` / `AmbientSpawnStrengthStep` / `AmbientSpawnMaxAlive`）+ `StageController.UpdateAmbientSpawns` / `SpawnAmbientNow` / `PruneAmbient` + `EncounterController.SpawnAmbient`（复用波次刷怪的落点链路）。见 [stages.md](stages.md) 的「关卡内的定期刷怪」一节 |

### 二、硬指标逐位对照（`run_20261012_221413.log`）

```
配置表比对·索引：武器 3/角色 3/敌人 10/首领 1/召唤物 1/技能 25/Pressure 7/效果 9/节点 9/局外 6/升级池 3/AI 档案 11/能力卡 8   ← 逐位不变
节点布置：启用 10 个（高 2 / 中 5 / 低 3）→ 期望 10（2/5/3）        ← A3 之后**没有漂**（原因见下）
Pressure 测试（进化 150×1.25 + 异化 150×1.5）：… pressure=3 pmods=3 bank=0   ← 逐位不变
端到端：按 F 之后 奖励界面打开 = True  Paused = True                ← 逐位不变
🔴 白板首领实例：生命 315（首领行 900 × 0.35 = 315，期望 315）护甲 0（期望 0）  ← 逐位不变
[Audio] 配置表：音效 13（来源：表 Audio）                            ← 逐位不变
配置表比对·跳跃：起跳速度 13 / 重力缩放 3.6 / 空中加速 15 / 最大下落 60   ← A2 的新期望（旧的是 7.69 / 30 / 120）
```

**为什么「节点布置 10（高 2 / 中 5 / 低 3）」没漂**：`Main.tscn` 的那 10 个实例里**没有 `Kind = Weapon` 的节点**
（4 强化 + 进化 + 异化 + 商店 + 材料 + 恢复 + 特殊），所以把 `node_weapon` 那一行的 `Layer` 从 1 改成 2
不会改变任何一层的**实际数量**。真正变了的是 **`价值标签表 → 节点 [Weapon=3]` → `Weapon=4`**（已同步期望值），
以及自测的高/中/低改成**按价值层**（`NodePlacement.LayerOf`）数而不是按 `RewardNodeKind` 枚举数 ——
这样"标签说它值几波"与"它被数进哪一层"从此不可能互相矛盾。

### 三、A2 的取值与理由

- `JumpVelocity` **6.5 → 13**（起跳速度 ×2：用户「起跳速度也要快」）。
- `JumpGravityScale` **1.8 → 3.6**（重力 ×2）：因为滞空时间 = `2v/g`，**只抬起跳速度会让滞空翻倍**
  （那正是用户抱怨的"过程太漫长"）。两列一起给 → 高度 ×2（1.2 m → 2.4 m）、**滞空 0.87 s → 0.74 s（更短）**。
- 空中加速（15）/ 最大下落（60）**不再乘任何倍率**（上一批的 `JumpSpeedMultiplier` 已删）。
- ⚠️ 与需求文档 A2 里那句"(高度会随之 ×4，这是可接受的)"的差别：那是"只改 `JumpVelocity`"的算法后果，
  与用户同一句话里的"**过程太漫长**"直接矛盾，所以这里按"两个固定值"落地；数值本身按 A1 属占位、表里随时可改。

### 四、整轮无异常

`ERROR / Exception / warning` **0 条**；`selftest.flag` 已删（`Test-Path` = False）。
三轮自测逐位一致（`run_20261012_221308.log` / `run_20261012_221413.log` / `run_20261012_221803.log`，
随机敌种与帧率除外）。另外把 `AmbientSpawnInterval` / `AmbientSpawnEscalateSeconds` 临时改成 6 / 10
跑了一轮**自然计时路径**的验证（`run_20261012_221654.log`：每隔 6~7 秒刷一只、每 10 秒档位 +1，
全部硬指标照样逐位一致、自测跑完整），跑完已还原成 30 / 120。

---

## 护甲无上限 + 进化池合并实测（2026-10-12 用户第三轮 C1 / C2）

> 用户原话见 [requirements-ability-system.md](requirements-ability-system.md) 的「用户第三轮澄清（2026-10-12）」。
> **构建**：`& build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**（每改一步都跑）。
> **自测**：命令行直跑（`selftest.flag` + `res://Scenes/Main.tscn`），跑完 `selftest.flag` 已删。

### 一、C1：删掉 `MaxArmor`（护甲没有上限）

| 问题 | 结论 |
|---|---|
| **删了什么** | `Unit.MaxArmor` → **`Unit.StartingArmor`**（`[Export]` 改名，**值不变**）；`UnitConfig.MaxArmor` → **`UnitConfig.StartingArmor`**（配置列改名）；`MaxArmorMultiplier` / `MaxArmorAdd` 两个 StatSheet 键**整对删除**（`Unit.SeedStats` 的两行 `SetBase` + `RefreshStats` 里那段"抬高上限并补甲"的逻辑）；`Unit._baseMaxArmor` → `_startingArmor`。**`Armor` / `ArmorDamageReduction` / `ArmorBroke` / `DamageResult.ArmorLost` 一个都没动**（C1 明确要求减伤那条保持原样）。 |
| **为什么是"改名"而不是"删列"** | 单位表里必须有一列说"这个单位进场带多少甲"（玩家 40/140/60、守卫 60、冲锋兵 30、技能精英 90）。删掉就没有任何途径给初始护甲了。所以删的是**"上限"这个概念**（`MaxArmor*` 一族），留下的是**"开局护甲"这个事实**（`StartingArmor`）。 |
| **加甲走哪条路** | 新增 `Unit.GrantArmor(amount)`：`Armor += amount`，并累计到 `_armorBonus`（进树前调用也成立 —— 精英组件是子节点，`_Ready` 早于单位本身的 `_Ready`）。`EliteModifier` 从"加 `MaxArmorAdd` 修正器"改成"调 `GrantArmor(ArmorAdd)`"。 |
| **显示判据** | `MaxArmor > 0` → **`Armor > 0`**，共 **3 处**（任务书说"各一处"，实际是 3 处同样写法的 `SetArmor(...)`）：`PlayerHud.UpdateArmorBar`、`WorldOverlay.UpdateBar`、`WorldOverlay.CreateBar`（建条那一帧的初值，必须一起改否则编译不过）。**甲打光后连空壳都不显示**。 |
| **甲条的比例从哪来** | `Unit.ArmorPeak`（**运行期派生**：这个单位见过的最高护甲，只增不减，**不来自任何配置、不能外部设置、不参与结算**）。护甲没有上限之后 `Armor` 自己算不出"还剩几成"，而甲条要显示进度。**这是本轮唯一一处需要判断的地方**：另一个选择是"有甲就整条铺满"（比例恒为 1），那会让 §五 #8 的护甲进度彻底消失；选 `ArmorPeak` 的结果是**除"空壳不再显示"之外，画面与改动前逐位一致**。 |
| **`ArmorBroke`（碎甲）有没有受影响** | **没有**。触发点仍是 `Unit.ReceiveHit` 的 Armor 段"`Armor` 从 >0 掉到 0 那一次"，判据一个字没改；`WorldOverlay` / `PlayerHud` / `ArmorBreakFx` 三处消费方也没动。 |
| **行为变化（必须知道的一条）** | `EliteModifier.ArmorAdd = 120` 现在是**纯粹的加甲**：本来 0 甲的敌种（追猎者 / 快速近战 / 自爆 / 跳跃 / 远程 / 尖啸）精英化后**真的多出 120 甲**。改动前那条 `MaxArmorAdd` 只把它们的护甲上限抬到 120、`Armor` 仍是 0（一层不吸收伤害、比例恒 0 的空壳），所以这是"语义修正"带来的数值变化，属 A1 的占位范围。有甲敌种（守卫 60→180、冲锋兵 30→150）与改动前一致。 |
| **`BarBoss.tscn` / 首领** | 一行没动：首领本来就没有 `StartingArmor`（0），判据改成 `Armor > 0` 之后它依然"天然不显示"，`BarBoss.tscn` 里本来就没有护甲节点。 |
| **改了哪些数据文件** | 行文件 **14 个**（`Enemies/` 9 个带甲行 —— `enemy_global` 是纯倍率行、本来就没有这一列；`Bosses/boss_final`；`Summons/summon_drone`；`Characters/chr_gunner|heavy|summoner`）+ 场景 **4 个**（`Guard.tscn` 60 / `Charger.tscn` 30 / `SkillElite.tscn` 90 / `TrainingDummy.tscn` 60）。三个角色行的 `DescriptionTemplate` 占位符 `{MaxArmor:0}` → `{StartingArmor:0}`（`ConfigText` 按**字段名**取，不改就是 `[ConfigText] 模板里的 {MaxArmor:0} 在配置行里找不到` 警告）。全部 **LF + 无 BOM**（逐个二进制校验过）。 |
| **`ConfigIndex.tres` 存了吗** | **没有** `MaxArmor` 字段（它是全局标量表，护甲从来只在行文件/场景里），所以那次改动没有碰它 —— 也就没有"整份加载失败"的风险。⚠️ **行文件是原地改列名、不是删文件/改名**，所以 `[ext_resource]` 与数组项一个都没动。 |

### 二、C2：池的候选集时机 + 合并重复的进化池节点行

| 问题 | 结论 |
|---|---|
| **原来是什么时候算的** | **四个交互点的惰性分支**：`RunController` 的 `OfferRareRewards`(:549) / `OfferRewards`(:575) / `PoolPreview`(:618) / `OpenRewardForNode`(:693) 各写了一段 `if (!_poolReady) { _pool.Initialize(_weapon); _poolReady = true; }` —— **谁先被交互谁建池**。候选集本身（`RewardCatalog.All` 按武器三层过滤）建完之后不再变，但"什么时候建"是**第一次交互时**，不是进关卡时。⚠️ 其中两个分支（:549 / :693）**忘了设 `SummonerAllowed`**：某局若第一个被交互的是特殊奖励节点，召唤控制角色整局抽不到「亡者随从」。 |
| **改成什么时机** | 新增 `RunController.InitializePool()`（武器层 + 角色限定层一次定完），**只在 `ApplyBalance()` 里调一次** —— `ApplyBalance` 就是"角色/武器都定了、即将进关卡"的那一刻（`BeginRunWithCharacter` → `ApplyBalance()` → `ApplyStage(0)`）。四个交互点改成 `EnsurePool()`（已定过就是空操作，只作兜底）。交互时仍然要按**当前构筑**筛（互斥 / 层数上限 / `ConflictsWith` / 节点 `Kind`）—— 这几条依赖"你现在已经有什么"，天然必须每次交互算；**变的是"候选项从哪来"**。 |
| **留痕** | `[Reward] 奖励池按当前角色/武器定下来了（进关卡初始化）：角色 Gunner · 武器 Pistol（PoolId pool_pistol）→ 候选 19 条（武器层之后；角色限定项在抽取时再筛 SummonerAllowed=False）`。⚠️ 19 = **武器层过滤后**的候选数（含 `mut_summon`），枪手**实际可抽** 18 条（硬指标不变）。 |
| **合并了哪两行** | 删 **`node_weapon.tres`**（`Kind = Weapon` / Slot 1），保留 **`node_evolution.tres`**（`Kind = Evolution` / Slot 3）。理由：两行的 `Title`/`DisplayName`/`RewardKind`/`Layer`/`WaveCount`/`ValueTags` **逐列相同**（都是「进化池」+ `Evolution` + Layer 2 + 4 波 + `tag_high`），唯一区别是 `Kind`；而**场景里的进化池实例是 `Kind = Evolution`**（`Main.tscn` 的 `NodeEvolution`），**`Kind = Weapon` 一个实例都没有**；另外 `node_weapon` 的 `BossPressurePoints` 是 **0**，保留它会让进化节点丢掉 150 点 Pressure（`Pressure=3` 那条硬指标会漂）。**`Id` / 类 / 目录 / 枚举一律没改名**（C2 明令）。 |
| **`Rule`/字段怎么合并** | `ValueTags` 与 `Layer` 两边**本来就一样**（`tag_high` / 2），没有冲突；`BossPressurePoints = 150` 取的是 `node_evolution` 的（`node_weapon` 是 0，即"没表态"）；`node_weapon` 的 Note 里那句"这是武器（攻击方式）的限定池"**折进 `node_evolution.tres` 的新 Note**，并指明按攻击方式分的三个池在 `Config/Tables/UpgradePools/`。 |
| **`UpgradePools/` 三个池** | **保留不动**（C2 明令）：`pool_pistol` / `pool_sniper` / `pool_rifle` 就是"进化池的选项由角色持有的攻击方式决定"这条链路的落点（武器行 `Pool` 正向指过去；选项行的 `Pool` / `RequiredWeaponTags` 决定谁能抽到）。 |
| **"节点布置 10（高 2 / 中 5 / 低 3）"数的是表行还是场景实例** | **场景实例**（`SelfTest.ReportNodes` 遍历 `Stage` 的子节点里的 `RewardNode`，按 `NodePlacement.LayerOf(kind)` 分层）。所以删一行表**不会**改这个数字 —— 实测**仍然是 10（2/5/3）**。改的是 `配置表比对·索引` 里的 **`节点 9` → `节点 8`**（少一个行文件，必然结果，已同步期望值），以及开局日志 `节点波数（…）：…`（少一项）与 `Pressure 节点驱动：节点表 9 行里 2 行配了` → **`8 行里 2 行配了`**（配了的那两行仍然是 `node_evolution` / `node_mutation`，功能部分逐字不变）。 |
| **`Kind = Weapon` 的兜底** | `NodePlacement.LayerOf` 的代码兜底补上 `RewardNodeKind.Weapon => 2`（与 `Evolution` / `Mutation` 同层）。表里再没有这一行之后，它就是 `Kind = Weapon` 唯一的价值层来源；不补的话 `WavesFor(Weapon)` 会从 4 掉到 1，自测的 `节点 [Weapon=4 …]` 那条硬指标会漂，而且与 A3「武器限定池 = 进化池 = 高价值」自相矛盾。 |
| **`node_weapon` 的删除姿势** | `git rm` 行文件（该目录没有 `.uid`）+ **同一个改动里**删掉 `ConfigIndex.tres` 的 `[ext_resource ... id="22_node_weapon"]` 与 `Nodes = [...]` 里那一项 —— 正是 config.md 顶部那条纪律要求的姿势（只删行文件会让整份 `ConfigIndex.tres` 加载失败）。 |

### 三、自测实测（C1 与 C2 各跑一轮，最终代码一轮）

```
[Config] 配置表：武器 3 / 角色 3 / 敌人 10 / 首领 1 / 召唤物 1 / 技能 25 / Pressure 7 / 效果 9 / 节点 8 / 局外成长 6 / AI 档案 11 / 能力卡 8（ConfigIndex.tres 已加载）
[Reward] 奖励池按当前角色/武器定下来了（进关卡初始化）：角色 Gunner · 武器 Pistol（PoolId pool_pistol）→ 候选 19 条（武器层之后；角色限定项在抽取时再筛 SummonerAllowed=False）
[SelfTest] 节点布置：启用 10 个（高 2 / 中 5 / 低 3）→ 期望 10（2/5/3）
[SelfTest] 配置表比对·玩家：生命 270（期望 250 + 元进度加成 20） 护甲 40（期望 40） 移速 7（期望 7） 减伤 0（期望 0）
[SelfTest] 配置表比对·索引：… 节点 8/局外 6/升级池 3/AI 档案 11/能力卡 8（期望 3/3/10/1/1/25/7/9/8/6/3/11/8）
[SelfTest] 配置表比对·节点表：进化层 2/波 4（期望 2/4） 强化层 1/波 3（期望 1/3） 恢复层 0/波 1（期望 0/1） 商店价目 5/12/18 标题 异化池
[SelfTest] 配置表比对·价值标签：… → 节点 [Weapon=4 Enhancement=3 Evolution=4 Mutation=4 Resource=1 Heal=1 Special=1 Shop=3 Portal=1]
[SelfTest] 配置表比对·增幅项：共 19 条（强化 10 / 进化 4 / 异化 5）用了表=True 当前池子=18 条（枪手期望 18 条）
[SelfTest] 配置表比对·升级池：武器 PoolId「pool_pistol」（期望 pool_pistol） 池表 3 行 ｜ 专属层 0 条 · 类别层 4 条（命中 4）· 通用层 15 条 ｜ 召唤控制池 19 条（期望 19）
[Node] Pressure 节点驱动：节点表 8 行里 2 行配了 → node_evolution 基础 150 / 额外抽 0 次 / 标签 [不限制] · node_mutation 基础 150 …
[SelfTest] 端到端：… pressure=3 pmods=3 bank=0 victory=False all=10         ← 逐位不变
[Audio] 配置表：音效 13（来源：表 Audio）                                    ← 逐位不变
[Audio] 音效统计：共 47 次 …（缺失 id 0 个）
[SelfTest] === 自测结束 ===
```

**逐位不变**：`节点布置 10（2/5/3）`、`Pressure=3`、`端到端 F`（奖励界面打开 = True）、`音效 13`、
`增幅项 19 条 / 池子 18 条`、`价值标签 → 节点 [Weapon=4 …]`、升级池三层与召唤控制池 19 条、玩家护甲 40 / 移速 7 / 减伤 0。
**唯一变的一个数字**：`配置表比对·索引` 的 **`节点 9 → 8`**（合并两个节点行 = 少一个行文件，必然结果，已同步期望值）。
日志里**没有** `ConfigIndex.tres` 加载失败 / 读表回退的迹象（`（ConfigIndex.tres 已加载）` + 各表条数与表在时一致）。

