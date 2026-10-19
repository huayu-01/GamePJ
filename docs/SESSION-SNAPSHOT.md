# 当前开发快照（2026-10-19）

唯一的接续入口；专题见[文档目录](README.md)。不要从头重读历史交接或重做已完成批次。最新机制为J33（身体碰撞与快乐小子），提交哈希以`git log -1`为准。

## 当前执行顺序

**最新用户裁定（2026-10-19）**：J33补回单位实体碰撞，身体共用阻挡层、敌我受击层分开；快乐小子改为奔跑接触轻推普通敌人，保留10秒安全期／每秒1生命回血。其他几项机制审查先搁置，完成本轮后优先画面和模型设计。未来需飞行单位、压低高度／打落与受力表现，本轮仅验证三维分层和统一受力入口，不自动开展完整飞行系统。详见[碰撞约定](collision-layers.md)与[J33记录](character-design-j33.md)。

此前的**清理多余文档 → 做符文 → 接特化**已完成，下面三项是交付摘要。全部留在当前对话，完成后也不要自动新建对话。平衡重测后置，数值n用表内可调预设，不逐项打断询问；当前优先级以顶端最新用户裁定为准。

1. 文档清理：重复交接和旧路线图已归档，原路径保留跳转；不删原始需求、技术专题、验证证据、失败现场和外部美术文档。
2. 符文：J31已完成基地符文台、选择面板与11枚SVG图标，统一“携带”，一次一枚，可不携带。复用J11的效果、存档和前置解锁；专项42/43均0失败，见[交付记录](character-design-j31.md)。
3. 特化：J32已接[23项清单](followup-build-updates.md)，22项有符文前置、暴风无前置；B2两项同时限定角色／武器／符文。23项真实行替换旧三项后生产目录90=23／33／34。专项194／195零失败；见[交付记录](character-design-j32.md)。数值平衡与最终特效美术后置。

原文权威：`F:\Godot项目\3d肉鸽雨中哈迪斯\角色设计.txt`、`后续构筑.txt`，结合用户最新裁定。原文只有11枚符文，第12枚缺内容，不编造。快乐小子穿透／虚化已被最新“轻推”裁定替代。其余小精灵最终表现、独立动作和美术验收留给后续画面／模型设计；派生爆炸与触发伤害审查暂缓。

## 最近已完成

| 批次 | 内容与证据 |
|---|---|
| J33 | [身体碰撞与快乐小子](character-design-j33.md)：共用身体层、独立阵营命中、奔跑轻推／回血与飞行受力设计边界 |
| J32 | [23项特化](character-design-j32.md)：候选、前置、真实派生伤害、属性转换、死斗、旋风与B2技能；194/195零失败 |
| J31 `42ce447` | [基地符文界面](character-design-j31.md)：11枚统一携带、图标、暂停／输入与存档；42/43零失败 |
| J28 `3aab844` | [弹道](character-design-j28.md)：细弹逐帧射线／粗弹形状，几何与预算分离，同帧穿透余程和距离累计修复；56/0 |
| J29 `cc62fe4` | [玩家主攻击快照](character-design-j29.md)：共读武器行5%／2倍基础及局外／构筑，投掷周期／回程、落剑式和额外剑气继承出手属性；45/0 |
| J30 `e920cc7` | [DoT归属](character-design-j30.md)：燃烧正确产生本人Kill／灵魂／死铠／践踏死亡，未知与已释放来源不冒领，不自续烧，保留原招架与固定DPS；26/0 |

J13～J27的进化、动作移动许可、强化等级、逐杀记录和飞翼加速已交付；详细记录在各批`character-design-j*.md`，不要重复实现。

## 已确认规则

- 践踏死亡：空中击杀立即补次数；容量已满时仍回填已消耗次数，不再询问。
- `+`／`++`必须有上一级；只有原文带`*`的增幅可重复，其他获得后退出候选。
- 符文独立于局内奖励表；携带满足前置并施效，一次一枚。
- 实际弹速上限来自`Config/ConfigIndex.tres.ProjectileMaxSpeed=200`。QueryGeometry：0射线／1形状；SweepMaxHits≤0取默认8，与几何独立；视觉缩放不自动放大判定半径。
- 未完成的美术不能因机制验证通过而称已验收。不要为凑全绿删断言或降低数量。

## 工程与协作约定

- 所有shell显式使用`F:\Godot项目\项目a`。上级`F:\Godot项目`不是Git工程。全程简体中文，代码标识符英文、注释中文；结构探索先CodeGraph，不委托探索，不自行开子代理。
- C#只用apply_patch定点改，UTF8无BOM／LF。CodeGraph写后有防抖，勿立即重查；交付前force index。
- 构建与验证独占`.ai_validation.lock`，Git独占`.ai_git.lock`；不可并发编译测试。不要关闭Godot编辑器或全局stop_project，仅清理自己创建的子进程。
- 每批精准暂存、核对集合、commit＋push，不用add-A或全量restore。纯文档整理只做链接／内容检查，不机械跑游戏回归。
- `ConfigIndex.tres`新增行须同步资源引用、数组和load_steps三处。数值与玩家说明共读表／资源。

外部改动必须保留且不混机制提交：`Assets/ArtRefresh/env_level_garden.tres`、四个旧heavy／summoner模型及import删除、`Scenes/Interactables/CharacterStand.tscn`、`Scenes/Levels/Base.tscn`／`TestArena.tscn`、`Scenes/Main.tscn`、`Scenes/Player/Player.tscn`、`Scripts/Visual/CharacterDisplay.cs`、`project.godot`，以及未跟踪shader／ToonPass／模型、图和所有.uid／.import。J33对project.godot仅精准提交新增第10层UnitBody，其余外部重排保持未暂存。需接已有脏场景时只改明确必要节点并精准暂存自己的补丁，或优先选择新资源／运行期挂载。参考[J30外部指纹](character-design-j30-workspace.log)；当前验收基于脏工作区，不称干净HEAD独立通过。

## 验证约定

同一PowerShell会话中依次执行，不能并发：

```powershell
& .\build.ps1 --no-incremental -v minimal
dotnet build ActRoguelike.csproj --no-incremental -v minimal -c ExportRelease
```

复用`_tools/verify_projectile_capsule.ps1`，按影响选择专项；已有`-ProjectileQuery`／`-AttackSnapshot`／`-DotAttribution`／`-RuneUi`／`-Specialization`等入口。新批补对应开关，双构建、适当真实输入／物理探针、五原回归、Main八指标与存档保护，保存实际日志。Godot4.7.2 mono路径：`F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`。Main必须指定`res://Scenes/Main.tscn -- --selftest`。

五原回归：蓄力80／原文115／龙之箭42／弓39（渲染40）／符文83，全部0失败；数量变动须说明真实新增／机制迁移原因，不能随手改绿。

Main八指标对照`character-design-j11-evidence.log`逐字一致：

| 项 | 基线 |
|---|---|
| 配置计数 | 武器6／角色2／敌人10／首领1／召唤物1／技能39／连招3／Pressure7／效果14／节点8／局外5／AI11／能力卡4，ConfigIndex已加载 |
| 布置节点 | 10（高2／中5／低3） |
| 猎手 | 220／20／6.5 |
| 索引14数 | 6／2／10／1／1／39／7／14／8／5／6／11／4／11 |
| Pressure | 3／3／0 |
| 白板首领 | 315／0 |
| 奖励UI | True／Paused True |
| 最小链路 | 1动画事件→1弹体→命中 |

原5条Main历史红标保持如实记录，不称Main全绿。奖励目录当前91=强化23／进化33／异化35（J32目录90补快乐小子）；符文仍独立为11枚。旧三项资源保留在Config/Retired/Upgrades供历史机制回归，不进入生产池。

存档每批跑前／跑后完整SHA256比较，不覆盖用户后来的手动修改。最近基线：meta `6C29…7EA7`、ability_cards `594B…99CA`、settings `9FD4…02B4`、loadout `CD92…5462`；完整值见J30证据。`rune.cfg`原来不存在，符文UI自测用内存隔离；必须另验证真实存档格式而不写用户目录。结束清理自己创建的selftest.flag。设置现为1920×1080、skill_1=R，不用更旧快照覆盖。
