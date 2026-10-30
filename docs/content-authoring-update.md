# 内容编辑入口更新

对应2026-10-26验收表014—018。旧模型、骨架、场景槽位和现有资源UID保持兼容。

## 挑选技能类型

|内容|创建的资源类型|只在该类型出现的参数|
|---|---|---|
|普通技能／被动|AbilityConfig|冷却、动作、目标等通用配置|
|蓄力技能|ChargedAbilityConfig|蓄力时长、档位、提前释放、完美窗口|
|连招|ComboAbilityConfig|AbilitySeries段落组件|
|下落攻击|FallAbilityConfig|FallAttackTuning|
|首领额外行为|BossBehaviorConfig|首领处理器与抽取权重|

这些定义均可由现有单位／武器的Skills数组引用，槽位仍决定实际执行哪个技能。专用类型没有额外逐帧处理。未来同时需要多个能力模块时，可在专用类型继续组合资源组件，不需要把参数重新塞进所有技能。

## 数字身份与命名

329项已有身份资源已分配全局唯一正整数 `NumericId`。`Config/content-id-ledger.json` 保留已分配和退役编号，不因排序、删内容而重排或复用。Inspector以数字ID作为制作入口。旧 `Id` 隐藏但保留序列化，用作已有存档、前置条件和旧脚本的兼容别名；既有业务代码仍有兼容别名调用，不要求一次性改写玩家存档。

新内容不需要描述型字符串ID，未提供旧别名时自动用数字文本兼容现有存档键。代码可直接 `ConfigRows.ByNumericId<AbilityConfig>(数字)` 查询；`ConfigRows.ById` 同时接受旧键与数字文本。派生技能仍在AbilityConfig表索引。

新增文件例子：`skill_1500.tres`、`enemy_1501.tres`、`augment_1502.tres`。已有语义路径保留以兼容外部引用，没有批量重命名旧资源。技能图标已实际采用 `Assets/UI/Icons/Skills/icon_数字.svg`，也支持同名png、webp；颜色由图片本身决定。

```powershell
# 先预览将创建的路径与自动分配的下一编号
python _tools/new_content_resource.py --type charge --name 新蓄力技能
# 确认内容类型后创建，默认不覆盖文件、不复用编号
python _tools/new_content_resource.py --type charge --name 新蓄力技能 --apply
# 编辑Godot资源，补齐节点、动画、参数并加入ConfigIndex对应数组。
# 手工创建的带旧别名资源可用此工具分配编号；不加--apply只校验。
python _tools/assign_content_ids.py --apply
python _tools/assign_content_ids.py
```

## 技能栏

技能表不再手工绑定HUD图标、颜色和排序。`AbilityHudStyle` 在HUD节点上决定槽位顺序、蓄力条、维持标志、效果倒计时与层数是否显示。实际技能提供当前冷却、层数及进度；HUD不改变输入或战斗参数。挥砍表现有自己的Tint，不再借HUD颜色。

验证入口：`_tools/verify_update_fixes.ps1 -ImportAssets -Only content,config_authoring,numeric_identity`。新增图片后需要先导入。验收表仍由玩家填写“通过／不通过”，脚本只记录修复与验证结果。
