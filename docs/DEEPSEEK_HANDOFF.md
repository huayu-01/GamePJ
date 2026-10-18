# DeepSeek接管执行单（2026-10-17）

> **2026-10-18 最新 J17**：金光大道已接原index2下劈，独立伤害×1.5／前向距离×1.3，宽高与全局乘区保持，真实武技2不增伤。专项31/32、全部回归0失败，双构建0/0、Main0／八指标一致、存档保持，见 [J17](character-design-j17.md)。**下一项浮光跃金，不重做J7～J17，不新建对话。**以下为历史。

> **2026-10-18 最新 J16**：气贯长虹已补召回与总蓄力上限，强类型WeaponRecall，真实各档／落地／往返伤害／换装还原通过。专项89/90与全部回归0失败；双构建0/0、Main0／八指标一致、存档保持，见 [J16](character-design-j16.md)。首轮渲染退出崩溃现场保留，项目侧释放临时查询后复验通过。**下一项金光大道，不重做J7～J16，不新建对话。**以下为历史。

> **2026-10-18 最新 J15**：喜爱已完成，真实离手后2／4／6秒按玩家距离索敌、真实伤害与墙前阻挡均已验。专项42/44均0失败、灵剑71/0、镰刀36/0与五回归全绿；双构建0/0、Main0／八指标一致、存档保持，见 [J15](character-design-j15.md)。**下一项气贯长虹，不重做J7～J15，不自动新建对话。**以下为历史。

> **2026-10-18 最新 J14**：怜悯／普渡已完成，见 [J14](character-design-j14.md)。真实末刀49.4→74.1；前两刀击杀不回血、末刀双杀195→211，攻击／Kill段快照过滤已验。专项36/38均0失败、灵剑71/0、五回归全绿；双构建0/0、Main0／八指标一致、存档保持。**下一项喜爱，不重做J7～J14，不自动新建对话。**以下为历史。

> **2026-10-18 最新 J13**：下面第4项里的灵剑三项已完成，见 [J13](character-design-j13.md)。真实金光五段20/23/26/29/36；大拔刀只增强自身，伤害26→39、模型1.4→2.8、胶囊0.16；神降术半径4.8、×1.6、CD2真实落地。专项71/74全0失败、五回归全绿、双构建0/0、Main0/八指标一致、存档保持。下一步从怜悯／普渡／喜爱继续。**用户已改定在当前对话继续，不再自动新建。**以下J12及旧交接状态均为历史。

> **2026-10-18 最新接续 J12**：用户已明确授权 Codex 读快照后继续执行，本文件旧“停工／等 DeepSeek”状态不再约束当前任务。胶囊扫掠与可配置全局实际弹速200已交付；完整验收、原始输出、截图和边界见 [J12](character-design-j12.md)。五探针80/0、115/0、42/0、**39/0**、83/0，弓渲染40/0；双构建0/0、Main0、八指标逐字一致、存档保持。**下一步为下面第4项（补进化）／[SESSION-SNAPSHOT](SESSION-SNAPSHOT.md) §5.3**，J7～J12不要重复做。全局限速调 `Config/ConfigIndex.tres.ProjectileMaxSpeed`，当前200；原始发射快照与实际速度分开，后者读 `Projectile.ActualSpeed`。

## 授权与入口

用户最新明确要求：“额度快要用完了，当前测试结束时将任务交接给DeepSeek，写下来让它干”。当前测试已全部退出，Codex完成本批提交后停止新开发，不再并发修改。**原专用联络对话只联络、不改代码的临时分工已由这次用户授权替换；DeepSeek现在接管项目执行。** 不要仅回报计划，按下面顺序继续做、实测、更新文档、每批commit＋push。歧义及时向用户提出。

工作目录：`F:\Godot项目\项目a`，PowerShell，Godot4.7.2 mono、C# net8.0、3D动作Roguelike。远端`https://github.com/huayu-01/GamePJ.git`，分支main。原始需求权威：`F:\Godot项目\3d肉鸽雨中哈迪斯\角色设计.txt`。

先依次读：`docs/HANDOFF.md` → `docs/roadmap-2.md` → `docs/architecture.md` → `docs/todo-handoff.md`，再读原始角色设计和`docs/original-design-audit.md`。不要把早期批次绿灯理解成原文全部完成；文档顶部当前状态优先于旧历史快照。全程简体中文汇报，未验证明确标⚠️。

## 当前已完成，可复用而非重做

| 批次 | 提交 | 本批内容 |
|---|---|---|
| I | f6694d5 | 默认资源缓存复活已收集包装器的竞态，弱缓存＋IgnoreDeep规避，负例3次崩溃／正例3次退出0；不禁GC、不无限保活 |
| J1 | e5b157c | 六配装准星朝向、后撤锁朝向、长枪四段循环、只有灵剑有额外落剑式 |
| J2 | ae75c31 | 镰刀前飞停转、周期命中、再次或靠近收；长枪准星落点、真实落地留枪、仅再次收，回手才解锁武技1；高速往返扫掠及同源飞出模型 |
| J3 | 8bffdf9 | 默认后按动作打断、可配动画前摇锁／全程例外、每单位实例事件＋播放序号隔离；残余位移修复 |
| J4 | 2d34771 | 下落按起手高度与垂直速度倍率、空中跳高比例0.5、长枪定向突击、真实落地、高速球体扫掠与枪尖只读对齐 |
| J5 | 7b020fb | 闪真实Action才三维冲刺；架真实GuardOn开窗、单次命中无效化；闪身步来源过滤及收招CD1、招架反击CD0 |
| J6 | 包含本文的提交 | 松手冻结结果后真实释放ATK出弹；变奏快射1→2→3循环、逐箭表内间隔、打断取消待发箭 |
| J7 | 本批提交 | Main 旧同步蓄力探针跨帧迁移（等释放段真ATK与自然收招再读数）；生产代码零改动 |
| J8 | 包含本文的提交 | 发散／心眼按**命中前**标记结算：攻击者侧`IOutgoingDamageScale`＋`MarkedBonus`处理器；一个类服务两条行，加标/加伤来源都进表 |
| J9 | ba709fc…2282b80 | 龙之箭：松手蓄力≥2秒的大箭（尺寸×2）＋全穿透（额外99个），阈值/尺寸/穿透全进**强类型资源列**，**按松手快照冻结**、**真实释放ATK发射**；一箭穿3靶与撞World停下均有实测 |
| J10 | 89002f5／a73f840／2d4950e／5220431 | 弓：`ChargeStepSeconds 0.1→0.3`＋`ChargeMaxSeconds 1.0→1.4`（**基础1档+后置3档=4个威力档，×1.0/1.3/1.6/1.9 加法**）；弹速 `220→160`／每档 `30→20`（**预设待确认**）；`Projectile` 新增 `SweepMaxHits`（默认0=逐位不变）让**每个物理帧把本帧整条线段（长度=弹速×帧时长，逐帧重算）上的所有敌对单位都结算**、不重复、撞World停；只给弓武技1配 `SweepMaxHits=8`。修了共用基类一个真浮点缺陷（`(1.4−0.5)/0.3=2.9999998` → Floor 少算一档）；实测**贴脸双怪**关扫掠只中1只、开扫掠同一物理帧两只全中；`_BowSweepProbe` 23/0 |
| **J11** | 本批提交 | **符文系统（哈迪斯信物 = 一个效果 + 解锁一批增幅前置）**，按用户划定的 5 步拆成 5 个提交：① 新表 `Config/Tables/Runes/`（**11 枚**，`RuneConfig : RewardConfig` + `UnlocksRequires:Array<string>` + `IconPath`）② `ConfigIndex.tres` 三处同改（id 283…293 / `Runes` 数组 / `load_steps 197→208`，**索引串 13→14 个数**）③ 施效走**既有** handler 通路（6 枚纯数值用 `StatSet`，5 枚用新 `RuneEffect`；StatSheet 新增 `MaxHealthCap`/`IncomingDamageMultiplier`/`MaxFallSpeedMultiplier`/`HealMultiplier` 四个键）④ 前置对接（`RunBuild.SatisfiesPrerequisite`，`RewardPool.HasPrerequisites` 改调它；**不带符文时逐位不变**）⑤ 携带入口（`RuneCarry`→`user://rune.cfg` + `RunController.SetCarriedRune`/`ApplyCarriedRune`，**无 UI**）。实测：**雷之印 220→30**、**战神印同样10点伤害 7→28（正好4倍）**、风之印冷却 ×1.5、死铠 +20甲/每10杀+1甲/禁疗、生死 999致命伤→活下来回到30%、狙神 1.5m×0.5/9m×1.0/20m×1.5、巨人 10基础→32、小精灵 7.5s内出手1次打20；**前置解锁用真实 `RewardPool.Roll` 证**（不带符文两条探针行都不进池；带雷之印正好+1条且派系不串）。`_RuneProbe` **83/0**；回归 80/0、115/0、42/0、23/0；双构建 0/0；Main退出0、8硬指标**只有索引串**按预期变14个数、其余7条逐字一致；四存档SHA256跑前=跑后、**`rune.cfg` 不存在**（自测内存模式）。见[rune-system.md](rune-system.md)（契约）与[character-design-j11.md](character-design-j11.md) |

各批`docs/character-design-j*.md`、`*-evidence.log`、`*-failures.log`和截图均保留。更早A/E/B/C/D和F/G/H只代表当轮范围，仍有原文遗漏。

J6最终：专项无头80/0、渲染82/0；完整20进程回归后仅扩展探针再跑2进程，22进程均退出0且stderr空，Debug／ExportRelease均0警告0错误，Main八条硬指标逐字一致，四存档保持，selftest.flag不存在。实测普通波30/250、完美波40/400、短弩九箭；变奏两轮1,2,3,1,2,3，表内0.08秒在60Hz物理下为0.0833秒。详细输出见J6证据。

**J6边界（已被J7关闭）：Main原有6条旧同步蓄力探针红色输出**，当时不是已全绿。独立新探针的真实输入／动画／物理验证通过；旧探针同帧手动Tick，无法等待真实释放ATK，需迁移。原始红色行已附J6证据，不能删日志伪装通过。→ **2026-10-17 J7 已迁移成跨帧**：6条转✅（完美40/400、过窗30/250、后置蓄力75/625、短弩3/9、弓24-45.6、剑气≥1），Main连续三次退出0（第三次stderr=0字节）、8硬指标逐字一致、双构建0/0、四存档保持；生产代码零改动。见[J7验收](character-design-j7.md)与[证据](character-design-j7-evidence.log)。

## 下一步顺序

1. **✅（2026-10-17 J7 已完成，见[character-design-j7.md](character-design-j7.md)）先迁移Main旧同步蓄力探针**。文件`Scripts/Testing/SelfTest.cs`：`ProbeChargeMechanism`、`ReportCrossbowProbe`、`BeginBowProbe`里的武技1段、`ReportSwordQiProbe`。对应状态机case30、33、34、44。旧`SettleShot`/`WaitUntilIdle`只是手动Tick，不推进动画。可把这些段改成Task或分帧小状态，等真实ATK／自然收招后读结果，状态机必须等任务结束再Next，缺事件用明确超时失败；不要伪造ATK、不要给生产能力加计时出弹兜底。J5的`ProbeBlinkDash`/`ProbeGuardStance`已是分帧范例。独立`ChargeReleaseProbe`是真实输入范例。然后Main退出0、8指标、这6条新检查逐项核验，双构建0/0。→ **实际做法**：4个探针改成`async Task`＋每帧真实delta的`OnInputHeld`＋`WaitSettle`等自然收招＋`ReleaseAtkFired`（`Finish`写冷却/`Cancel`不写）作为"真ATK到达"判据；删`SettleShot`；状态机`ProbeTaskDone`后才`Next`。仍🔴的只剩改前就有的5条历史读数标记。
2. **✅（2026-10-17 J8 已完成，见[character-design-j8.md](character-design-j8.md)与[证据](character-design-j8-evidence.log)）发散／心眼**：旧`evo_divergence`和`evo_mindseye`复用BurnOnHit只加Mark，未实现按目标标记增伤；甚至来源过滤尚不完整。发散必须只由能量掌武技1波波加标，并仅提高波波；心眼只由箭加标，玩家对已标记目标的各种伤害提高50%。用户刚确认按本次命中前已有标记结算，第一次加标这次不加伤。必须走真实奖励获取与命中，覆盖其他技能／其他单位／多个目标／层数及过期，不直接Execute冒充接入。发散原文N未定，可配置预设并明确标注。新参数用强类型资源／列，不再增加Godot.Get/Set字符串反射。→ **实际做法**：新增攻击者侧查询口`IOutgoingDamageScale` + `Unit.OutgoingDamageScaleAgainst`，在`AttackInstance.RollHit`（**生成HitInstance之前**，所以这一击自己加的标不算）读目标层数；新handler`MarkedBonus` + `MarkedBonusRule`（一个类服务两条行），加标来源=`RequiredEventAbility`、加伤来源=新强类型资源列`BonusAgainstAbility`；发散预设每层10%（文档N未定，表行Note写明）。自测`RunMarkedBonusProbe`（case54/55，P6d退出挪到55）三段23条读数全绿：同目标连打10/11/12（0/1/2层）、心眼54→36（×1→×1.5）、多目标、封顶5层、过期、后撤0命中、敌方单位打标记目标×1、能量掌武技2×1.5。双构建0/0、Main退出0、stderr 0字节、8硬指标逐字一致、四存档SAME。
3. **✅（2026-10-17 J9 已完成，见[character-design-j9.md](character-design-j9.md)与[证据](character-design-j9-evidence.log)）龙之箭**：`evo_dragon_arrow`原来只把`ChargeMaxSeconds`设3、没实现"蓄力≥2秒更大＋全穿透"。已改成：新增强类型资源列`RewardConfig.ChargedShot`（`ChargedShotTuning`：目标技能／蓄力上限3s／档数上限／阈值2s／尺寸×2／穿透99）＋新处理器`ChargedShot`→公开强类型方法`ArrowVolleyAbility.ConfigureChargedShot`（**没有`Godot.Get/Set`字符串反射**）；`OnChargeReleased`把蓄力秒数**快照一次**，`FireVolley`只读快照判定"大箭"，发射仍只由释放段**真实ATK**触发；不配阈值时逐字退化成改动前表达式。⚠️ **本批唯一碰到的共用子类是`ArrowVolleyAbility`（弓／短弩共用）**，只加不删（3个`[Export]`默认0＋1公开方法＋1快照字段＋`FireVolley`一段"阈值≤0不进"分支）；"按松手时长切换弹型"没有别的拿得到那一刻的类，若要求完全不碰需另立发射事件通路（那要改`Ability.cs`）——**请裁定**。探针`_DragonArrowProbe`42/0：**一箭穿3靶**（同一`AttackInstance`，帧390/392/393，132/264暴击/132，尺寸×2、穿透99）、**射墙**（墙前132、墙后100000/100000、箭停在墙内z=8.2339后消失、场上投射物0）、**阈值边界**（0.6s/1.9s小箭、2.05s大箭）、**不锁存**（大箭后0.6s又小箭）；独立`_ChargeReleaseProbe`80/0逐字不变、`_CharacterDesignProbe`115/0逐字不变；双构建0/0、Main退出0且8硬指标逐字一致、四存档SAME、flag不存在。⚠️ **口径待用户裁定**：`floor((3-0.5)/0.1)=`**25档**——每0.1秒仍在加档，2.0s=15档×5.5(670弹速)、3.0s=25档×8.5(970弹速)，即**"可以蓄力3秒"确实让伤害继续长到3秒**。本批按解释A落地（3秒=能按多久，2秒=叠加的离散大箭门槛）；若用户认为×8.5过大→解释B：表里`ChargedShot.MaxSteps=15`即可（2秒封顶），**不改C#**。另两条**既有**行为未动、供裁定：①穿透预算被算两遍（`FireProjectile`写`arrow.Pierce`＋`Launch`再加`attack.Pierce`→实际2×）；②单帧11.17m内间距小于它的两个目标一箭只结算最近1个。→ **J10 已关闭 ②**（每物理帧把本帧整条线段上的所有敌对单位都结算；实测贴脸双怪关扫掠只中1只、开扫掠同一物理帧两只全中；默认 `SweepMaxHits=0` 逐位不变，目前只给弓武技1开）。① 仍未动。**J10 留下三条待裁定**：B 弹速 160/20 是否合适；龙之箭 `step=0.3` 后已自动降到 8档 ×3.4（81.6伤害/320弹速），要收敛就表里 `ChargedShot.MaxSteps=5`；长枪武技2 现为"基础1+后置4=5档"（×1.8），按新口径是否要改成 4 档（`maxSec 2.0→1.75`）。
4. **逐项补其余进化并实测，不能只改Note**：
   - 金光术`evo_golden_qi`：灵剑武技1每段真实ATK额外剑气，当前无handler。
   - 大拔刀术：先核实真实表行Id，审计临时写`evo_big_draw`不是可靠文件名。收窄到拔刀式自身，不能全局伤害。
   - 神降术：灵剑额外落剑式范围／伤害／CD2；真实落地与其他武器隔离。
   - 怜悯`evo_mercy`：只出镰刀最后一刀、该刀+50%，旧全局倍率错误。
   - 普渡`evo_salvation`：仅镰刀末刀击杀回血，不能所有击杀；可能需给攻击／击杀事件加段序号。
   - 喜爱`evo_affection`：飞镰每2秒移向离玩家最近敌，当前未接。
   - 气贯长虹`evo_rainbow`：禁手动召回、落地立刻自动收、全部蓄力时间减半；J2已有RecallOnSecondPress与AutoRecallOnSettle。旧行只改base.5/step.125，max也应同步1秒。
   - 金光大道`evo_golden_road`：只长枪index2下劈，范围与伤害增强，旧全局修饰错误。
   - 浮光跃金`evo_light_leap`：额外次数换基础跳高并直接瞬移，旧JumpVelocityMultiplier1.25错误；用户换算见下。
   - 地爆`evo_ground_burst`：共通落地击退，必须覆盖镰刀／长枪，旧只改灵剑落剑式。
   - 专注、专一、花心、心流、乱金柝等也需真实链路验证；心流要求基础／后置蓄力0.1并完成自动发1箭，不能只改时间参数。
5. **异化也回原文逐条审**。特别是原文杀戮践踏写起跳（含空中跳）伤周围；旧类叫StompOnLandRule、Main按落地取证，不能据旧绿灯认定对齐。其他来源／武器限定、互斥、前置同样核对。
6. **统一玩家伤害与暴击快照**。原文5%暴击、2倍爆伤；很多近战／落／闪／投掷AttackInstance.CanCrit默认false且未合元进度。箭／能量波已加CritChanceAdd，不能重复叠加。做玩家统一入口，敌人不能默认吃玩家5%。真实100%暴击靶和基础.05＋玩家meta9×.02＝.23都验。J4/J5只补下落／闪DamageMultiplier一次，不代表暴击已修。
7. **独立动作与美术**。长枪四种挥枪仍共享阶段原生攻击，投掷复用末刀，落／闪／架／拉弓也只是适配。用户期望卡通＋低多面体、艺术品质；参考https://kyrusgames.itch.io/toon-nature，未来像素风只能作为可选热插拔镜头渲染层。占位素材后期要换；只能下载免费且许可允许的资源。Binbun五类特效已有来源审计，先读`docs/art-refresh-sources.md`及交接，不重复乱下载。表现只读战斗状态。
8. 六配装平衡重测：旧H长枪是五段，不能当现在四段DPS证据；扩展移动靶、群怪、完整构筑、两角色防御及整局胜率。角色基础数值保留现值，不再问同样问题。

## 用户已明确裁定，不重复问

- 镰刀保留共通落，没有灵剑额外落地圈、没有长枪准星定向。
- 能量掌武技2松右键判普通／完美，废弃原文闭合期按左键。
- 长枪基础1秒＋额外1秒＝总2秒，额外每.25四档；按基础加算，四档1.8倍；弓五档2.5倍。
- 抛枪准星选落点，超当前蓄力射程同方向截断；默认真实落地后不动，第二次才收，回手才解锁武技1。镰刀前飞停转、再次或靠近收。
- 发散提高的是波波，原文漏“武”字，不提高后撤。发散／心眼按命中前已有标记结算，从下一击增伤。
- 变奏免0.5秒点按快射、1→2→3循环；二／三箭短间隔依次射出，间隔进表，长按仍蓄力。
- 默认后来的动作打断前一个；未明确不可打断者均如此。可配动画时间轴前摇锁；后台纯效果、已离手武器可并用。刹那之眼保留0.3秒无动画防御软启动。
- 架让一次敌方命中无效，包括能量波；普通Block仍被能量波穿透。同帧第二击不能共享无敌。
- 下落倍率=`min(表上限, 起手脚下到World地面高度/普通单跳高度 × 垂直下落速度/标准速度)`。标准单跳同速1倍；普通额外跳高默认增加50%，同速二段跳1.5倍；低高度／低速可低于1，不另加基础1。长枪斜冲只计垂直速度。默认上限2，可表改。
- 普通额外跳高比例在CharacterConfig.AirJumpHeightRatio=.5；浮光跃金独立裁定每个额外跳次数增加1份地面基础跳高：1次2倍，2次3倍，不能按.5折算它。
- 猎手200生命／20甲／6.5速、武器大师220／25／6.8，用户选择保留现值先实测建议。
- **弓武技1（J10）**：后置蓄力间隔 **0.3s**（旧填 0.1 是笔误）＋「**基础蓄力 + 3 个后置档 = 总共 4 个威力档**」→ `ChargeMaxSeconds = 1.4`；原文「最多蓄力 1 秒」按此改成 1.4 秒。威力公式是**加法**（`1 + 0.3×档`），与旧裁定「弓五档2.5倍／长枪四档1.8倍」同一条，**不要改成乘法**。
- **基础蓄力期间不加档**（J10）：`Ability.RecountChargeSteps` 本来就是"基础蓄力没完成 = 0 档、威力×1"，本批只做实测确认（0.352s → 档0/×1；刚过基础那一发伤害不含档位加成），**代码没改**。
- **快弹不漏怪（J10）**：漏怪的真因不是"碰撞体太短"，而是既有投射物**每物理帧只取一个最近命中**、穿透后直接跳到帧末。修法是"本帧线段（长度=弹速×帧时长）上的所有敌对单位都结算"；**默认 0=不启用**，只有弓武技1 配了 `SweepMaxHits=8`。

## 工程铁律与验收命令

- 改C#只能使用edit／apply_patch定点编辑，新C#也一样；禁止PowerShell多行替换或Python写C#。UTF8无BOM、LF。
- 编译严格：`& 'F:\Godot项目\项目a\build.ps1' --no-incremental -v minimal`，必须0 warning／0 error；另跑`dotnet build ActRoguelike.csproj --no-incremental -v minimal -c ExportRelease`。
- 构建／Godot验证独占`.ai_validation.lock`（FileShare.None）；Git独占`.ai_git.lock`，stage前必须空、精准清单、禁止git add -A。每批docs＋commit＋push。
- 增删ConfigIndex必须ext_resource＋数组项＋load_steps三处同改。现6武器／2角／39技能／62增幅／**11符文**，不能静默回退。
  ⚠️ 索引串那行（`配置表比对·索引`）在 J11 之后是 **14 个数**（`6/2/10/1/1/39/7/14/8/5/6/11/4/11`）；8硬指标里只有它是"预期会变"的。
  ⚠️ `[Config] 配置表：…` 那条总览行**仍是 13 项、逐字未变** —— 符文有自己的独立日志行，不要再往那条总览里加表。
- 禁stop_project（会杀别人的自测），禁关Godot编辑器（MCP宿主）。只结束自己启动的PID。自测结束必须清userdata的selftest.flag。
- 表现层只读，不依特效长度发伤害；残影／VFX不能进单位列表或带物理碰撞。无真实动画事件只取消，不加时间执行兜底。
- 结构查询优先CodeGraph context→一次explore，已初始化，不再让用户init；字面文本再rg。每批低成本codegraph index --force，不能阻塞计划。
- 完整当前复验：`& .\_tools\verify_charge_release.ps1`。`-ProbeOnly`只跑本专项且会重写证据文件，先另存完整证据再合并，不能覆盖丢掉回归结果。
- Runner包含Debug、专项无头／渲染、闪架／落／动作／原文、Main、F/C/D/G/角色/B/A/E/读档及Release；每进程保存存档字节、检查stderr、清flag。不要同时再跑别的实例或编译。
- Godot console：`F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`。主自测必须Main场景＋`-- --selftest`，不能不带场景而进基地。
- `.gdmcp/bin/gdmcp.exe doctor`确认编辑器连接；用户已启动编辑器和MCP。编辑器不可关，run失败可用独立console，但别用全局stop。
- C探针必须`-- --loadout-test`；不能使用普通玩家存档。独立调用runner header跑C缺outer fixture还原，禁止这样做。
- 8硬指标基线在`docs/e-batch-evidence.log`，使用runner逐字比较：配置计数、10节点2/5/3、猎手220/20/6.5（meta加20）、索引、Pressure3/3/0、白板boss315/0、奖励UI True/Paused True、最小链路1动画事件→1弹体→命中。

## 存档、工作区与退出坑

userdata：`C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike`。交接点SHA256：

```
meta.cfg          6C295F398153205ABAFD39F186979AD7034D04EF7CD99EC6B976AF3202647EA7
ability_cards.cfg 594BD027B7E68FFA16D2F45A6702339E5A5B69185C49B3AB944A7A9EAE7399CA
settings.cfg      3389FC70EFEFFDD5745ABB3EC53418C2E49D06823F698B50DB94247928470864
loadout.cfg       CD92D4BD7DE10BF7D3B76DC4DF11E440FDC0F3C00174AA40068A771097A95462
```

meta碎片20、health_level2、crit_level9；当前武器大师镰刀、猎手记弓。不要盲目恢复旧H的长枪或覆盖用户新手动设置。历史I曾无字节备份污染loadout，已按用户真实日志恢复上述点，不能声称任务最初逐字无变化。自测Meta收益已内存化。

⚠️ **J11 起多了一个 `user://rune.cfg`**（符文携带，`RuneCarry`）：**刻意独立于上面四个存档**，所以 `meta.cfg` 的 SHA256 基线**没有被改**。
自测走内存模式（`RuneCarry.UseInMemoryStoreForSelfTest`，与 `MetaProgress` 同一条纪律），跑完 `rune.cfg` **不存在**；
将来若有人手动带了符文再跑 Main，8硬指标会变（玩家生命 220→30 等），那是**预期**行为，不是回归。

`project.godot`有用户／编辑器未提交变化，**保留、不混进提交**。大量未跟踪`.uid`、图片`.import`和csv翻译导入副产物也不能一锅端。验证会重拍旧批次截图，精准恢复自己生成的旧图，保留本批新图；禁止全量git restore。另一个窗口新增SkillIndicator四样式及文档（46f9988、456e14e、b69188d、e40e3bf）已在基线，不能回滚。

J5修复两个特效GDScript深载时找不到VFXOmniLightBB：改为显式preload依赖，各3次深载／GC可实例化。音频退出必须先杀淡出Tween、卸载流、提前释放声部，再真实异步Task.Delay80ms；旧游戏计时器.08秒在无头只经过1.693ms，不能改回。修后3次Main均stderr空。旧SetGodotObjectPtr的GCHandle变体仍未单独复现，不能宣称引擎永久修复。

原生AnimationPlayer同名Play不会自动从头，必须Play后Seek(0)；**不要Stop再Play**，4.7.2有旧访问违规崩溃史。事件轨道同一时刻两个方法会覆盖，架已改GuardOn@.15、GuardOff@.9。不要重生成整个50段动画库来改一段；全部单位事件走实例＋播放序号隔离。

## 联络与续做方式

用户专用Harness会话：`session-f70b9230-26cc-42e8-bda1-189470dab697`。本机桥接在仓库外`F:\Godot项目\.codex-dsh-bridge`，是原生`dsh headless --session-id`，不是已经注册的Codex Worker。桥接只输出公开final，不输出模型thinking、不读取／复制host.lock凭据。

用户现在授权该专用对话执行项目任务。先读本单和四份权威文档，检查git状态和锁，先处理Main旧同步探针，再逐项兑现剩余计划。每批给真实输出和截图、更新文档、提交推送；有歧义立即告知用户。不要等待Codex继续指挥，也不要把“已接管”当成任务已完成。

## 实际接管回执与环境阻塞

J6源码已推送`ef9bad5`。DeepSeek原专用对话实际读到本交接单并返回`ACK ef9bad5`，正确复述Main四处方法与case30/33/34/44。随后发现DeepSeek自己的pwsh工具在真正执行git前被Windows沙箱初始化阻断：`SetNamedSecurityInfoW failed (Win32 5): grantWrite(F:\Godot项目)`。收窄工作目录需新会话（旧会话绑定父目录）；临时项目目录诊断会话同样报`grantWrite(F:\Godot项目\项目a)`，因此不是git命令失败或代码构建失败。

两次只读接管检查均未改代码、未运行构建，也未修改ACL或全局沙箱配置。**首个执行前置项改为在DeepSeek侧只读诊断这个沙箱权限问题；不能宣称它已经能编译或跑测试。** 可按其已有`diagnose-windows-sandbox-acl`流程只读查原因；若需要用户设置／提升权限，直接在DeepSeek对话向用户报告，不绕过限制、不擅自改ACL或全局安全策略。权限恢复后从Main探针迁移接着执行上述计划。

Codex已停止新开发。接管确认及续做派发记录在仓库外`F:\Godot项目\.codex-dsh-bridge`；临时项目目录检查会话`session-c93fc9a5-df0d-4292-8648-25f4b1ddcc89`仅用于诊断，正式联络仍用上面的原专用会话，避免两边并发改代码。
