# 交接文档：待办任务与项目约定

## J9：龙之箭蓄力阈值大箭 + 全穿透（2026-10-17，已完成）

交接单「下一步顺序」第3条已兑现：[J9验收与边界](character-design-j9.md)、[完整输出](character-design-j9-evidence.log)。`Weapon.cs`／7个共用`Ability`子类／敌人·首领·召唤物行与技能行**一个字没动**；`ConfigIndex.tres`**没有改**（不增删行）。

病根：`evo_dragon_arrow` 的`ApplyHandler`是`AbilityOverride`，参数只有`AbilityId=skl_a3_w1_1`＋`ChargeMaxSeconds=3.0`——原文后半「蓄力2秒后射出的箭更大且附带全穿透」**代码里不存在**，而且它不是纯数据能表达的（要按松手时长切换弹型=新行为），旧处理器还走`Godot.Set`字符串反射。

改法（三件）：① 新增强类型资源列`RewardConfig.ChargedShot`（`ChargedShotTuning`：`TargetAbility`目标技能／`MaxChargeSeconds`上限／`MaxSteps`档数上限／`ThresholdSeconds`阈值／`SizeScale`尺寸／`PierceCount`穿透数，全在表里）；② 新handler `ChargedShot`→公开强类型方法`ArrowVolleyAbility.ConfigureChargedShot(...)`，**没有`Godot.Get/Set`字符串反射**；③ `ArrowVolleyAbility`新增三个`[Export]`（阈值/尺寸/穿透）＋`_releasedChargeSeconds`：松手那一刻在`OnChargeReleased`**快照一次**，`FireVolley`只读快照判定`chargedShot`→穿透预算=表值+99、尺寸=×2；阈值≤0时逐字退化成改动前表达式（**默认行为逐位不变**）。发射入口一个没加，仍是释放段真实`ATK`。

表行（`evo_dragon_arrow.tres`）：`ApplyHandler="ChargedShot"`，`ChargedShot={skl_a3_w1_1, 上限3s, MaxSteps 0, 阈值2s, 尺寸×2, 穿透99}`。

自测（新`Scenes/Debug/_DragonArrowProbe.tscn`＋`Scripts/Testing/DragonArrowProbe.cs`）**42/0**，全走真实链路（`ConfigRows`读表→`RewardCatalog.Find`→`RunBuild.Add`真实入构筑→真实按键`Input.ParseInputEvent`→释放段真实`ATK`→`Projectile`每物理帧射线→`Unit.Damaged`读数）：**一箭穿3靶** T1(帧390,132)→T2(帧392,264暴击)→T3(帧393,132)，三次命中是**同一个`AttackInstance`**（`Pierce==99`，同一发箭），尺寸实测×2、穿透预算99；**射向World**：墙前靶132、墙**后**靶100000/100000一次没被打到、箭最后位置z=8.2339（墙∈[7.5,8.5]）、箭消失、场上投射物0；**阈值边界**：0.6s→小箭(31.2)、1.9s→小箭(124.8)、2.05s→大箭；**不锁存**：大箭后0.6s又回小箭(31.2)。冻结证据：`LastShotChargeSeconds`＝松手冻结值（0.608/1.91/2.06/0.607/2.058）。

验收：Debug／ExportRelease均0警告0错误；Main退出码0、耗时68.9s、**stderr 0字节**、到`=== 自测结束 ===`；8硬指标逐字一致（含`（ConfigIndex.tres 已加载）`与索引`6/2/10/1/1/39/7/14/8/5/6/11/4`）；红色行仍是改前就有的5条历史读数标记、P5b覆盖行仍✅；独立`_ChargeReleaseProbe`复跑**80/0**（与J6/J7/J8逐字一致=默认行为不变）、`_CharacterDesignProbe`**115/0**（原文验收历史基线）；四存档SHA256跑前=跑后=交接点；`selftest.flag`不存在；编辑器未关（`gdmcp doctor` editor_connected=true）。**本批无截图**。第一版探针有窗口缺陷（上一发的箭飞进下一段命中列表）→首轮32/10，加`Settle`后42/0，机制侧当时已全绿。

⚠️ 待用户裁定：「**3秒上限 vs 2秒阈值**」。实测`ChargeMaxStepsEffective=floor((3-0.5)/0.1)=`**25档**：2.0s=15档×5.5(670弹速)、3.0s=25档×8.5(970弹速)——**每0.1秒仍在加档，2秒不是上限**。本批按**解释A**落地（3秒只是"能按多久"，2秒是叠加的离散大箭门槛；原文明写"可以蓄力3秒"）。若认为×8.5过大→**解释B**：表里`ChargedShot.MaxSteps=15`（2秒封顶），**不用改C#**。另记两条**既有**行为未动、供裁定：穿透预算被算两遍（`FireProjectile`写`arrow.Pierce`＋`Launch`再加`attack.Pierce`→实际2×）；单帧11.17m内间距<它的两个目标一箭只会结算最近1个。下一项回交接单第4条：逐项补其余进化。

## J8：发散／心眼按命中前标记结算（2026-10-17，已完成）

交接单「下一步顺序」第2条已兑现：[J8验收与边界](character-design-j8.md)、[完整输出](character-design-j8-evidence.log)。`Weapon.cs`／7个`Ability`子类／敌人·首领·召唤物行与技能行**一个字没动**；`ConfigIndex.tres` **没有改**（两条行本来就在表里，本批不增删行，所以没有三处同改的风险）。

病根：两条行的`ApplyHandler`都是`BurnOnHit`，而那条规则的判据只有「来源是自己＋目标不是自己」→ **任何命中都挂标记**（后撤/灵剑/能量掌武技2都算，这就是交接单说的「来源过滤尚不完整」），而「按标记加伤」那一半代码里根本不存在。

改法（四件）：① 新增`Scripts/Combat/IOutgoingDamageScale.cs`＋`Unit.OutgoingDamageScaleAgainst`（攻击者侧登记，无登记恒为1）＋`AttackInstance.RollHit`里**生成`HitInstance`之前**读目标层数——所以「第一次加标这一击不加伤」是**顺序保证**的，不是特例判断；② 新handler `MarkedBonus`＋`MarkedBonusRule`（**一个类服务两条行**）：加标来源=`RequiredEventAbility`资源引用，加伤来源=新加强类型资源列`RewardConfig.BonusAgainstAbility`，数值`FlatBonus`／`PerStackBonus`；③ 发散`PerStackBonus=0.1`（**文档{N}未定→预设10%/层**，行`Note`与`ParamEntry.Note`都写明）；心眼`FlatBonus=0.5`且`BonusAgainstAbility`留空=**玩家的各种伤害**；④ **没有新增任何`Godot.Get/Set`字符串反射**。

自测（`RunMarkedBonusProbe`，case54/55，P6d的退出挪到case55）三段23条读数全✅，全部走**真实奖励获取**（`RewardPool`确认在池＋`RunController.GrantReward`→`RunBuild.Add`→真实handler）+**真实按键/真实动画事件帧/真投射物/真命中**（读数来自`Unit.Damaged`）：同目标连打 **10／11／12 = ×1／×1.1／×1.2**（0/1/2层）、心眼真箭 **54→36 = ×1→×1.5**；边界逐项——其它技能（武技2不加标不加伤、层数1→1）、后撤（0次命中）、其它单位（敌方打带2层标记目标结算20=×1）、多目标（A 3层/B 1层各记各的）、封顶5层（×1.5不再涨）、过期（推6.1s→0层→×1）、两条同时拥有（1.1×1.5=×1.65）、"各种伤害"（武技2对已标记目标×1.5）。

验收：Debug／ExportRelease均0警告0错误；Main退出码0、耗时68.1s、**stderr 0字节**、到`=== 自测结束 ===`；8硬指标逐字一致（含`（ConfigIndex.tres 已加载）`与索引`6/2/10/1/1/39/7/14/8/5/6/11/4`）；红色行仍是改前就有的5条历史读数标记、**没有新增**；四存档SHA256跑前=跑后=交接点；`selftest.flag`跑前跑后都不存在。**本批无截图**（证据是文本读数＋存档哈希）。

边界：发散N是预设10%；心眼的"箭"=弓武技1`skl_a3_w1_1`（资源引用；将来要"任何箭类武器都算"应改成攻击标签`Array<string>`列）；标记层数不区分施加者；DoT与`ApplyDirectDamage`不走`RollHit`所以不吃加成（本批未接，也不需要）。下一项回交接单第3条：龙之箭。

## J7：Main 旧同步蓄力探针跨帧迁移（2026-10-17，已完成）

交接单「下一步顺序」第1条已兑现：[J7验收与边界](character-design-j7.md)、[完整输出](character-design-j7-evidence.log)。只改`Scripts/Testing/SelfTest.cs`，**生产代码零改动**（`Ability.cs`／7个`Ability`子类／`Weapon.cs`／敌人·首领·召唤物行都没动），J6的"Main六条红灯"边界不再是边界。

改法：`ProbeChargeMechanism`／`ReportCrossbowProbe`／`BeginBowProbe`的武技1段／`ReportSwordQiProbe`全部从"同帧灌蓄力+同帧读结果"改成`async Task`跨帧——`HoldCharge`每帧把**真实帧delta**交给`OnInputHeld`，松手后`WaitSettle`等技能**自己收招**（释放段真ATK→`Tick`里的`Finish`），状态机判`ProbeTaskDone`才`Next()`。删除`SettleShot`（同帧手动Tick=假象来源）。绿行判据新增`释放段真ATK到达 = settled && CooldownRemaining>0`（只有`Finish`写冷却、缺事件的`Cancel`不写），**没伪造ATK、没给生产能力加计时出弹兜底**；缺事件=明确超时红灯。

6条从🔴变✅（逐项）：机制·完美时机40/400、机制·过完美窗口30/250、机制·后置蓄力档3×1.9与封顶5×2.5→75/625、P3·短弩武技2（0档3支/3档9支）、P3·弓武技1（24/220与45.6/310）、P4·灵剑武技2拔刀式（未蓄满0个/蓄满1个）。第7条「机制·蓄力未完成松手」改前就是绿，现在也走真实帧按住0.3s仍绿。**仍🔴的没有新增**：只剩改前就在、内容写"期望=实际"的5条历史读数标记（白板首领闸门／白板首领实例／Boss阵亡／互动降临节点／最小闭环链路）。

连带夹具修正（也在SelfTest.cs内）：武技1现在真的射箭，原来摆在弹道0.9m的60血Chaser测试靶会被打死（`Unit.ApplyStatus`对`IsDead`直接return）→ case35标记层数／追踪弹会跟着红。改成两支真箭打完再建靶、`MaxHealth`抬到4000当纯靶；修后这两条恢复✅。

验收：Debug／ExportRelease均0警告0错误；Main退出0连续三次（58.4s／58.3s／58.2s，第三次单独抓stderr=0字节）且到`=== 自测结束 ===`；8硬指标逐字一致；独立`_ChargeReleaseProbe`复跑80/0；四存档SHA256保持；`selftest.flag`不存在；`.ai_*`遗留0字节锁文件已删未重建。**本批无截图**，证据是文本读数＋独立探针计数＋存档哈希。`WaitUntilIdle`（14处）与其余探针仍是手动推物理回调，不涉及"松手→释放段ATK"，留给新批次。下一项回交接单第2条：发散／心眼按命中前标记结算。

## 原始设计重新核验（2026-10-17，当前任务）

用户因额度将尽要求当前测试结束后交接DeepSeek，Codex停止新开发；入口为[DEEPSEEK_HANDOFF.md](DEEPSEEK_HANDOFF.md)。J6最终80/0、82/0，完整20进程＋扩展2进程均退出0且stderr空，双构建0/0、Main8指标一致、四存档保持、flag不存在。Main六条旧同步蓄力探针已在**J7**迁移成跨帧（见上），不能再把Main退出0跟"旧探针全绿"混为一谈。

J3已完成：动作默认打断、可配置前摇／全程例外、实例事件与播放序号隔离、真实同帧输入顺序。闪中断后残留位移3.672m/0.1s修到0，同名重播归零、按住重复事件不重新出招。专项无头38/0、渲染40/0；原文回归115/0、120/0，双构建0/0、Main退出0且8条指标一致，F33/0、C52/0、D90/0、G51/0、角色61/0、B74/0、A失败0、E39/0、读档5/0。14进程stderr为空，四存档保持，flag不存在。见[J3完整证据](character-design-j3.md)。

J4已完成：下落倍率按`min(配置上限, 起手高度/普通单跳高度 × 垂直下落速度/标准速度)`；普通单跳1倍、二段跳额外50%高度时1.5倍，表封顶默认2。普通空中跳高比例进表默认0.5，保留浮光跃金独立裁定。长枪定向、Action后下冲、真实落地事件、高速球体扫掠与只读枪尖朝向已接。专项104/0及108/0，双构建0/0、Main0且8条指标一致；动作38/40、原文115/120均0失败，F33/0、C53/0、D90/0、G51/0、角色61/0、B74/0、A失败0、E39/0、读档5/0，16进程stderr空、四存档保持、flag不存在。[J4证据](character-design-j4.md)。实际二段跳后1.5109倍，包含物理离散误差，未改写成精确1.5实测。

J5已完成：闪真实Action驱动三维冲刺，架真实GuardOn开窗，只规避一次敌方命中（含能量波）；同帧第二击正常受伤，普通格挡仍被能量波穿透。闪身步只认闪击杀且收招CD1，招架反击CD0。专项30/0、32/0；双构建0/0、Main0及8条指标一致，下落104/108、动作38/40、原文115/120均0失败，F33/0、C53/0、D90/0、G51/0、角色61/0、B74/0、A失败0、E39/0、读档5/0。18进程stderr空、四存档保持、flag清理；音频退出改真实等待，另三轮Main退出无告警。[J5证据与截图](character-design-j5.md)。

J6已完成：能量掌／短弩／弓／剑气松手冻结与真实释放ATK、变奏快射1→2→3循环；二／三连发按表内间隔逐箭发射。接下来迁移Main旧同步蓄力探针，再做发散／心眼／龙之箭及其余进化、统一暴击与独立动作。标记增伤已获用户确认按命中前层数结算，从下一击开始。

J2已完成：镰刀前飞停留与0.5秒周期命中；长枪准星落点、五档射程／速度／伤害加算、真实落地留枪及手动召回；回程锁门到手才解锁，同源飞出模型与握持隐藏。高速Box扫掠覆盖600m/s窄目标。
无头115/0、渲染120/0，双构建0/0，Main退出0且8条指标一致；F33/0、C52/0、D90/0、G51/0、角色61/0、B74/0、A失败0、E39/0、读档5/0。12进程stderr为空，四存档保持，flag不存在。[完整证据](character-design-j2.md)。
J2结束时待办现已推进J3动作交接；下落与长枪定向转入J4。独立投掷动作、四种挥枪动作、进化、变奏快射与全局暴击仍待接线及真实验收。

J1已完成：六配装四方向朝向、后撤锁朝向、灵剑专属落剑式、长枪四段两轮循环。修后无头44/0、渲染46/0；完整自测退出0、8条指标一致，双构建0/0及全量回归见[J1验收](character-design-j1.md)。修前失败与特效协程重载告警均保留，后者已改原生Timer并验证旧特效回收。
用户补充裁定已归档：下落按起手脚下离地高度、蓄力增强按基础加算、抛枪准星选落点并截射程、发散提升波波、变奏点按1→2→3免基础蓄力。J2已完成投掷与加算；下落、进化及动作视觉继续执行。

用户要求先处理GCHandle，再核对朝向和各种武器技能；有歧义立即反馈。已确认镰刀仅共通“落”、能量掌松右键按时机释放、长枪基础1秒加额外1秒蓄力。
GC缓存竞态已稳定复现3次，运行期弱缓存＋IgnoreDeep规避通过3次正例、双构建0/0、Main及8条指标、F33/0与各项回归；[细节与边界](resource-lifetime.md)。旧SetGodotObjectPtr堆栈仍待单独定位。下一项为角色朝向、长枪四连与留枪召回、镰刀前飞停留、下落攻击及原文逐项验收，见[审计清单](original-design-audit.md)。
旧H长枪数据为五段循环，不代表用户要求的四段循环；旧测试绿灯不能作为原文需求完成的证据。

## 后续专项H（2026-10-17）

用户确认保留基础数值。12份真实六配装输出与12份承伤样本，H渲染49/0；表内200/20/6.5与220/25/6.8未改。
修复真实连点提前开窗跳过ATK、缓冲立即切换判定以及循环长枪按总施法时间误超时；修复前失败原文保留。
详见[H验收](followup-h-verification.md)与[平衡建议](balance-review.md)。
最终双构建0/0、完整自测退出0及8条指标一致；G50/0、角色63/0、A23/0、B74/0、C52/0、D90/0、E39/0、读档5/0，存档/截图/flag检查通过。F/G/H本轮范围已完成。
**仍然开着的口子（当前权威）**：整体终版美术、GCHandle根因、全局暴击对近战的适用范围，以及移动目标/群怪/完整构筑/整局胜率验证。
以下F/G与第二轮批次清单为历史状态。

## 后续专项G（2026-10-17）

Loot/Battle已生产接线；长枪改为合格蓄力松手才发射，修正近距离误收回和蓄力距离上限。Main落剑式探针正确记录起手前基线，四档上限期望为4。
真实输入/动画/物理探针53/0和截图见[G验收](followup-g-verification.md)。
**仍然开着的口子（当前权威）**：H平衡实测、整体终版美术、GCHandle根因。基础数值按用户选择保留现值。
下文F的待办为当时快照。

## 后续专项F（2026-10-17）

自带中文字体500字重/1104字形、圆周32段破甲环、73多边形导航与有序Ogg退出，详见[F验收](followup-f-verification.md)。
12轮切场景强制GC及3轮提前卸载未复现旧GCHandle错误；根因仍未定位，不算已修复。
用户确认基础数值保留现值，先做平衡实测与建议；后续按[专项清单](followup-plan.md)继续G→H。
**仍然开着的口子（当前权威）**：Loot/Battle生产接线、长枪蓄力/旧P4探针、平衡实测、整体终版美术与GCHandle根因。
以下各批清单均为历史快照。

## 第二轮 D 批及全部批次执行状态（2026-10-17）✅

- **D①②③**：project.godot九层命名，architecture.md位值/语义/mask对照；攻击盒8/16、四弹体32按实际来源阵营配置。
- **D④⑤**：全体Unit身体mask=World，敌我及同阵营均不阻挡；保留地形阻挡。三把近战体内重叠仍命中，闪/剑突真实穿透。
- 弹道略过队友、体内出生可命中；命中RID排除避免重复消耗穿透；敌方弹射也按敌对阵营选目标。
- 主自测四处动画取证改读当前可见模型；C表现验收读角色表路径，兼容美术替换，不改正式战斗或动画资源。

实测：Debug/ExportRelease均0警告/0错误；完整自测退出0、八条硬指标逐字一致。
D94/0；C52/0与新进程读档5/0，B74/0，A23/0，E39/0；四存档不变，selftest.flag不存在。
见 [D验收与4张截图](d-batch-verification.md) 和 [18条完成核对](roadmap-2-completion.md)。
**A/E/B/C/D所有计划批次已完成**。以下各轮状态保留为历史快照。
**D批结束时仍然开着的口子（历史）**：整体美术终版、角色平衡、自带中文字体、破甲环八边形、导航、旧P4期望偏差与偶发退出异常。当前以顶部专项状态为准。
美术/素材任务的并行改动独立提交，本批不混入；Loot/Battle生产接线边界见art-refresh-handoff.md。

## 第二轮 C 批执行状态（2026-10-16）✅

- **C-1**：基地与关卡共用PlayerLoadout，真实角色技能/武技/能力卡、跳跃和奔跑参数均套用；练习木桩可被击破并重建，基地死亡不结算。
- **C-2**：WeaponConfig.OwnerCharacter为强类型归属；角色表生成独立CharacterStand，F直接切角色，只列其武器；旧两步选人流程已撤下。
- **C-3**：独立loadout.cfg按角色Id记武器Id，启动/往返恢复；当前卡片有✓且禁用重复选择；角色对象与玩家均显示其当前武器。
  CharacterDisplay切换可见模型，UnitAnimator切换真实播放器，骨骼轨道在私有副本中绑定实际骨架，握持场景随手臂运动。
  换装取消旧无敌/碰撞/架势订阅、灵动额外跳跃和滞空；受击、蓄力、残影均跟随当前模型。

验收：双配置编译0/0；完整自测退出0、八条硬指标一致；C59/0、跨进程读档5/0、A/B/E回归23/0、74/0、39/0。
存档隔离与7张截图见 [c-batch-verification.md](c-batch-verification.md)，后续配置见 [base-loadout.md](base-loadout.md)。
**C批交付时仍然开着的口子（历史）**：D碰撞层（现已完成）；整体美术终版、角色平衡、自带中文字体、破甲环八边形与导航问题。
⚠️ 一次中间完整自测出现GCHandle终结器异常，未当作通过；原文保留，根因未定位。
下文各轮未完成清单为历史快照，以本段及HANDOFF为准。

---

## 第二轮 B 批执行状态（2026-10-16）✅

- **B-1**：19条玩家技能行增加强类型图标/颜色/顺序列，19个统一SVG图标；右下角技能栏只读真实CD、层数、蓄力、维持状态及InputMap按键。
  六套配装按角色/武器表过滤；猎手5格，武器大师6格。400×300可用区域能换行；HUD layer=3，位于可选像素滤镜layer=1上方。
- **B-2**：`GmConsole.tscn` 自带界面、命令与G输入，无autoload/InputMap残留；基地/Main各一份。
  资源、增幅、CD、层数、无敌、召敌、真实交互、base/run、补全/历史均实跑；卸载可恢复暂停/无敌，保留底下设置页。
  连续回车与奖励模态切场景清理均有回归；Debug开关与ExportRelease禁用入口，编辑器生成脚本以TOOLS隔离。

**验收**：Debug/ExportRelease编译0/0；完整自测退出码0；B实拍探针82通过/0失败、A回归23/0、E无头回归39/0。
八条硬指标逐字一致；8张截图；GM资源测试恢复meta原始字节，其余存档哈希保持；`selftest.flag`不存在。
详见 [B批验收](b-batch-verification.md)、[命令与接入](gm-console.md)。⚠️ 尚未导出并运行完整发行可执行文件。

**B批结束时的未完成清单（历史）**：C基地战斗与角色交互（C-1→C-2→C-3）→ D碰撞层。
独立美术任务已改善庭院、接入安全免费特效并提供可选像素滤镜；Loot/Battle生产接线见 [美术专项交接](art-refresh-handoff.md)。
角色/交互主体仍为阶段性素材，整体美术终版、角色平衡数值、自带中文字体、破甲环八边形和导航问题继续保留。
下文各轮「仍然开着」清单均为历史快照，以本段及HANDOFF为准。

---

## 第二轮 E 批执行状态（2026-10-16）✅

- **E-1**：刹那之眼不占动作、不播放动画、不走蓄力，右键按住维持开镜和标记，松手结束。
  按用户选择保留 0.3s 无动画软启动和受击免伤、免费后撤；后台收尾不停止左键动作。
  `AbilityConfig` 新增强类型三列，普通技能默认语义不变；禁用/死亡/退树还原相机，暂停期间松手不锁死。
- **E-2**：`StatusEffectFx` 通用预制体由 `UnitPresentation` 延后挂载，读取效果行图标/颜色/形态及只读单位状态。
  9 条状态有表现，Mark 显示真实层数与5层封顶，多状态并存；过期和死亡清理，无碰撞体、单位或分组。
  模型材质不变，索敌用隐形信标保留；4 个 SVG 图标、冰晶、火星及亮色火芯均已实拍检查。

**验收**：编译 0/0；完整自测退出码0，E实拍探针43通过/0失败，A无头回归23通过/0失败。
八条硬指标与A批证据逐字一致，玩家存档哈希不变，`selftest.flag` 不存在。
证据、截图、复跑脚本和既有导航/资源告警、P4旧偏差见 [e-batch-verification.md](e-batch-verification.md)。

**E批结束时的未完成清单（历史）**：B 技能 HUD / 可插拔 GM → C 基地战斗与角色交互 → D 碰撞层。
整体美术终版、角色平衡数值、自带中文字体、破甲环八边形和导航问题仍待后续专项。
CodeGraph本批仅低成本检查健康状态（167文件、4611节点、8431边），未重建索引、未阻塞计划。
下文各轮状态和「仍然开着」清单均为历史快照，以本段及 HANDOFF 为准。

---

## 第二轮 A 批执行状态（2026-10-16）✅

- **A-1**：玩家血量文本并入 `BarPlayer/Vitals`，显示 `220/220 +20`，护甲用蓝色富文本；保留护盾显示和原护甲遮罩。
- **A-2**：额度条移到卡区左侧，宽 64px，自下向上；顶栏集中资源与额度扩增。
- **A-3**：只生成真实卡，每页最多六列三行，按钮 / PgUp / PgDn 翻页，空位留白；删除矩阵占位卡集合和旧探针 API，更新旧拍摄台。
- **A-4**：Esc 打开设置，帮助为第四个大分页，H 直达该页；移除独立帮助场景，保留 `UiService.OpenHelp()` 的兼容调用。
- **A-5**：增加 `DiveWindFx`，只读新增的 `FallAttackAbility.IsDiving` 与真实速度；真实 Ctrl 下落有青蓝风线，落地后收起，原落圈继续工作。

**验收**：编译 0 warning / 0 error；完整自测与 A 批实拍探针均退出码 0，探针失败 0。
八条关键日志去时间戳后与改动前逐字一致，玩家 `meta.cfg` / `ability_cards.cfg` SHA-256 不变，
`selftest.flag` 不存在。证据与重跑脚本见 [a-batch-verification.md](a-batch-verification.md)。

**本批发现并补接**：`Main.tscn` 原来没有挂能力页（实测 `OpenAbilityMatrix()=false`），现与基地同样挂入预制体。

**A批结束时的未完成清单（历史）**：E-1 弓武技2纯效果并用、E-2 状态表现、B 技能 HUD / 可插拔 GM、
C 基地战斗及角色交互、D 碰撞层；见 HANDOFF §5。角色平衡数值、自带中文字体、破甲环八边形和导航问题仍保留。
后文 P6c 的旧「仍然开着」清单是历史快照，P6d/P7/P8 完成项以 HANDOFF 为准。
整体美术尚未达到终版艺术品质，本批完成布局秩序与下落反馈，未更换场景、角色和整套 UI 美术。

---

> 给接手工作的代理看。**这份文档是自包含的**，不需要其它对话上下文。
> 项目：`F:\Godot项目\项目a`（Godot 4.7.2 stable mono / C# / net8.0）

---

## 一、铁律（违反会浪费大量时间，都是踩过的坑）

### 1. 改 C# 代码只用这两种方式

| 方式 | 适用 |
|---|---|
| **`edit` 工具**（读文件后精确替换） | **首选**，多行结构改动必须用它 |
| `pwsh` 里的**单行** `.Replace('旧的一整行', '新的')` | 只改一行时可用 |

**绝对不要**用 pwsh 做**多行**文本替换 —— 换行符（`\r\n` vs `\n`）不一致会**静默失败**：命令返回成功、文件没变、行为照旧。本项目已经因此栽了 7 次以上。

如果必须按行块删除方法，用这个可靠套路（已验证）：
```powershell
$lines = [System.Collections.Generic.List[string]](Get-Content $file)
# 从起始行向下找第一个 "\t}" 作为方法结尾；从后往前删除避免行号漂移
$lines.RemoveRange($start, $end - $start + 1)
Set-Content $file -Value ($lines -join "`r`n")
```
删完**立刻编译**，因为范围可能吃多（曾误删过一个方法）。

### 2. 每改一步都要验证

```powershell
# 编译（必须 0 warning / 0 error）
& 'F:\Godot项目\项目a\build.ps1' -v minimal
```
```powershell
# 跑游戏内的自测（验证逻辑）
'1' | Set-Content 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'
$cli = 'F:\Godot项目\项目a\.gdmcp\bin\gdmcp.exe'
& $cli tool-call run_project --args-json '{"allow_window":true,"scene_path":"res://Scenes/Main.tscn"}' --apply
Start-Sleep -Seconds 20
Remove-Item 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'
& $cli tool-call stop_project --args-json '{"allow_window":true}' --apply
# 读日志（自测会把内部状态打进去）
$dir = 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\logs'
$f = Get-ChildItem "$dir\*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Get-Content $f.FullName | Where-Object { $_ -match '波开始|清场完成|端到端' } | Select-Object -Last 8
```

**改完必须把 `selftest.flag` 删掉**，否则玩家自己玩时会被自测接管。

### 3. 其它约定

- 数值一律放 `Config/Tables/<分组>/*.tres`（一行一个文件，索引挂在 `Config/ConfigIndex.tres` 的 `[Export] XxxConfig[]` 数组上）或 `ConfigIndex.tres` 的全局字段里，**不要硬编码在逻辑里**。见 [config.md](config.md) 的"调参去哪张表"。
- **表是权威、代码是回退**：读不到表就用组件/行类自身的 `[Export]` 默认值（`ConfigIndex.FileLoaded` + 每个系统的"来源：表/默认值"日志能证明走的是哪条路）。
- ⚠️ **不要单独删/改 `ConfigIndex.tres` 引用到的行文件** —— 一个行文件缺失会让**整份** `ConfigIndex.tres` 加载失败、所有数值静默回退。要下线一行，把数组项与对应的 `[ext_resource]` 一起删。
- 玩家可见文案改动 → 同步更新 `docs/`。
- 日志用 `ActRoguelike.Core.GameLog.Write("分类", "消息")`（分类：Flow / Node / Reward / Spawn / Combat / Map / **Config**）。
  ⚠️ **`ConfigIndex` 的 `_Ready` 里不能直接写日志** —— `GameLog` 的 autoload 排在它之后，写进去会被静默丢掉。`ConfigIndex` 用 `CallDeferred` 绕过这一点。
- 运行时读数用 CLI 的 `await_runtime_condition`（**不是** `evaluate_debug_expression`，后者只在断点暂停时可用）：
  ```powershell
  $a = @{ expression = 'get_node("/root/Main/Stage").DebugState'; timeout_ms = 1500 } | ConvertTo-Json -Compress
  & $cli tool-call await_runtime_condition --args-json $a --apply --allow-open-world
  ```

---

## 二、项目结构与关键约定

```
Scripts/
  Abilities/ AI/ Boss/ Camera/ Combat/ Config/ Core/ Encounter/
  Interaction/ Player/ Rewards/ Run/ Testing/ UI/ Weapons/ World/
```

**分层**：`Unit`（单位本体）→ `Controller/Brain`（决策）→ `Ability`（技能，必须挂 Node3D）→ `Weapon/Attack/Hit/Damage`（战斗管线）→ `Event/Trigger`（事件与触发器）。
流程层：`RunController`（一局）→ `StageController`（一关）→ `EncounterController`（一场）。

**已抽出的纯逻辑层**（改规则不要碰流程代码）：
`NodePlacement` / `SpawnPlanner` / `RewardService` / `RunSettlement`
（`KillPlane` 已删除，区域杀改为碰撞区域 `Scripts/World/KillZone.cs`）

**统一入口**（新增代码优先用它们，别自己找）：
- `PlayerLocator.Player` —— 玩家引用（缓存，别每帧查组）
- `MapManager.Instance` —— 导航烘焙 + 刷怪点/降临点/节点落点
- `UiService.Instance` —— 开界面（`OpenUpgrades/OpenLoadout/OpenHelp/OpenRewards/OpenChoose/OpenMessage`）
- `UiRouter` —— 界面栈 / Esc / 暂停策略
- `ConfigIndex.Settings` —— 所有数值（**唯一数值门面**；实际数值在 `Config/Tables/<分组>/` 的分表里，见 [config.md](config.md)）
- `ConfigRows.ById<T>()` / `BySlot<T>()` / `ByKind<T,TKind>()` —— 读配置表的唯一入口（读不到 → `null` → 调用方回退到自身 `[Export]` 默认值）

**UI 框架现状**：`OverlayScreen` 已支持分层（`UiLayer.Hud/Panel/Modal`，决定是否暂停）、防误触、开合动画（`FadeSeconds` / `OpenScaleFrom`）。
界面清单与所属层见 [config.md](config.md) 的「界面与弹窗」；**新增界面请走 `UiService` + `UiRouter`，不要自己 new / 找节点路径**
（例：首领状况预览 = `BossPreviewScreen` + `UiService.OpenBossPreview()`，快捷键 B，分层 `Panel`）。

**详细流程**见 [controller-flow.md](controller-flow.md)，架构与坑见 [architecture.md](architecture.md)。

---

## 三、待办任务（本次交接的重点）

### 任务 I：P6b —— 表现层 + UI 尾巴 + 横向清理（**P1→P6 的最后一批**）✅ 已完成（2026-10-16）

> **这一轮把 P1→P6 整条重构线收尾了。** 逐条记录见
> [character-redesign.md](character-redesign.md) §七；本段只列"做完了什么 / 还差什么"。

**① 弹匣/装填横向清理（用户裁定「没有弹匣的概念了」）—— 5 层全清**：
配置表（删 3 个行文件 + `ConfigIndex.tres` 三处同改）· 代码（`Weapon` 整组装填逻辑 + `WeaponConfig`
三列 + `PlayerWeaponInput` 的 reload 键 + `MetaProgress` 的装填成长 + `RunController` 的修饰符 +
`Unit.SeedStats` 两个键 + `GameplayEventKind` 两个成员 + `AmmoOnKillRule`/`AmmoOnKill` handler）·
UI（HUD 弹药显示 + `Hud.tscn` 的 `Root/Ammo` 节点 + 帮助页两行说明 + 三个场景里失效的属性行）·
自测（武器比对那一行的期望串）· `project.godot` 的 **`reload` 输入动作（确认无引用后删）** + 文档。

**② 五条通用异化：做了 2 条**（都是"能复用已有机制"的）：
`mut_stomp` 杀戮践踏（落地范围伤害，轮询 `CharacterMovement.IsGrounded`）×
`mut_homing_missile` 巡飞弹（暴击 → 追踪飞弹，复用 P3 追踪弹通路 + 专用信标状态 `MissileBeacon`）。
**还差 3 条**：护卫（要"奔跑态"，全项目还没有 sprint 输入/状态）· 灵魂收割（要掉落物拾取系统）·
落日剑（命中施加延迟落剑）。两条新行都有**自测行为取证**（真敌人靶子 + 真 `RewardContext`）。

**③ 表现层：做了"准星处的蓄力指示器"**（用户描述最具体的那条；`Scripts/UI/ChargeIndicator.cs`）——
缺口随 `Ability.ChargeProgress` 收缩到闭合，闭合/完美时机变色 + 准星本体染色。
**没做**：开镜特效 / 后撤残影 / 剑气拖尾 / 落圈特效。→ ✅ **4 项已在 P6c 做完，见下面「任务 J」**。

**④ UI 两个尾巴：都做了，并查出两个真缺陷**：
额度进度条配了 `StyleBoxFlat` + 三档变色（正常蓝 / ≥0.8 黄 / 满红）；
**自适应列数原来没在工作** —— ①右栏也在抢宽度、②列数只涨不跌（矩阵尺寸被自己的最小尺寸顶住，
窗口变窄时不发 `Resized`）、③页脚文案不刷新。三个都修了，实测 1920→12 列 / 宽→19、24 列，
卡牌比例恒 0.625，截图在 `docs/screenshots/`。

**⑤ 两个可选都办了**：删掉 `MetaProgress.Buy*` 五个死方法（`Bonus*` 保留 —— 开局还在读）；
`ApplyAbilityMatrix` 的**顺序问题只改注释不改代码**（改顺序会让 `ResetAbilityBindings`
把技能行刚写上的按键绑定清掉，是实打实的回归；真修要拆三步）。

> **索引**：`6/2/10/1/1/39/7/13/8/6/6/11/6`（P6a）→ **`6/2/10/1/1/39/7/14/8/5/6/11/4`**
> （效果 +1 = `eff_missile_beacon`；局外 −1 = `meta_reload`；能力卡 −2 = 两张弹匣卡）。
> **增幅项 57 → 59**（强化 9 / 进化 23 / 异化 25 → 27）。
> **硬指标逐位不变**：节点布置 10(2/5/3) · Pressure=3 pmods=3 bank=0 · 白板首领 315/甲0 ·
> 端到端 奖励界面打开 True · 最小闭环链路 ✓ · 玩家 220/20/6.5 · 日志有 `（ConfigIndex.tres 已加载）`。
> 备份：`_refactor_backup/ConfigIndex.tres.p6bbefore`。

### 任务 J：P6c —— 补齐 4 项表现层特效 ✅ 已完成（2026-10-16）

> ⭐ **本批的纪律：表现层只读运行期状态，战斗逻辑一行没改**
> （唯一例外 = 给 `HawkEyeAbility` 加了一个**只读**属性 `ZoomProgress`）。
> 加特效的完整清单 / 契约 / 踩过的坑见 **[vfx.md](vfx.md)**；逐条接入记录见
> [abilities.md](abilities.md) §9。四张实拍图在 `docs/screenshots/p6c_*.png`（各带一张放大图）。

**① 开镜特效**（弓 武技2「刹那之眼」，设计文档第 87 行「放大视角」）
`Scripts/UI/ScopeOverlay.cs`，挂 `Scenes/UI/Hud.tscn` → `Root/ScopeOverlay`（Root 的**第一个**子节点）。
一张径向渐变贴图（`GradientTexture2D`）= 暗角 + 圆形镜筒细亮环 + 环上 12 条朝内刻度；
**镜筒半径以内一个像素都不画**（中心净空 167.6px）→ 不遮挡瞄准。只读 `HawkEyeAbility.ZoomProgress`
→ 开镜淡入、还原视角淡出，与镜头完全同步。`mouse_filter = Ignore`（双保险）。

**② 后撤残影**（技能1「后撤」，设计文档第 26 行「有无敌帧」）
`Scripts/Combat/AfterimageTrail.cs`，挂 `Scenes/Player/Player.tscn` → `Player/Afterimage`。
无敌帧期间每 0.09s 留一个角色模型的半透明副本（`GhostLifetime` 内淡出）。
只读 `BackdashAbility.InvulnerableOwned`（P3 就有的只读属性）→ 起止与 `Dodge→Dodger` 同起同落。
⚠️ **安全性（做减法 + 三个可数取证量）**：复制出来立刻把子树里**全部 `CollisionObject3D`（含 `Area3D`）
/ `Unit` / `AnimationTree` / `AnimationPlayer`** 摘掉并 `QueueFree`，再把整棵子树**退出所有分组**；
只 `AddChild` 到 `CurrentScene`。实测 4 个残影在场时：`碰撞体=0 单位=0 分组=（无）`，
且 `enemy_units` / `player_units` / `projectiles` 三个分组计数**一位没变**。

**③ 剑气拖尾**（灵剑 武技2「拔刀式」，设计文档第 110/114 行）
`Scripts/Combat/ProjectileTrail.cs`，**挂在剑气本体** `Scenes/Combat/SwordQi.tscn` → `SwordQi/Trail`
（不是另做一个特效实体）。每物理帧记一个采样点，用 `ImmediateMesh` 连成一条**面向镜头**的渐细光带。
只读 `Projectile.VelocityDirection`。采样点用 `ToLocal` 换回局部空间 → 父节点的
`ProjectileSizeScale`（"剑气大小等比攻击盒大小"）自动作用在拖尾宽度上。

**④ 落圈特效**（灵剑 武技3「落剑式」+ 被动「落」，设计文档第 112/99 行）
`Scripts/Combat/LandingShockwave.cs`，挂 `Player/AbilityFallAttack/Shockwave`（订阅公开的
`FallAttackAbility.Landed` 事件）与 `Player/AbilityPassive2/ShockwaveBurst`（轮询公开只读的
`LandingBurstAbility.TriggerCount` 上升沿）。**半径读战斗层真正的那个几何量** ——
`Attack/Shape` 的 `SphereShape3D.Radius`（3.2m，= `PulseDamage.Around` / `AttackHitbox` 用的范围），
读不到才用兜底值。

> **三个实拍里发现并修掉的真问题**（都是截图看出来的，不是"我觉得"）：
> ① 暗角用"一圈套一圈 `DrawArc`"→ 抗锯齿边缘叠出**一圈圈可见的同心细线** → 换 `GradientTexture2D`；
> ② 残影 0.045s/8 个 → **糊成一片光晕**（后撤总共只走 ~1.8m）→ 改 0.09s/4 个 + 饱和一点的青蓝；
> ③ `TorusMesh.Rings` / `RingSegments` **写反了 → 圆画成菱形**；顺带把加法混合默认关
>    （白地板 + `Add` 会冲成一片白）。
>
> **硬指标逐位不变**：节点布置 `10(2/5/3)` · `pressure=3 pmods=3 bank=0` · 白板首领 `315/甲 0` ·
> 端到端 `奖励界面打开=True` · 玩家 `220/20/6.5` · 索引 `6/2/10/1/1/39/7/14/8/5/6/11/4` ·
> 最小闭环链路 ✓ · 自测 `=== 自测结束 ===` **退出码 0**。**`ConfigIndex.tres` 一行没动**
> （表现层不该新增表行）。备份：`_refactor_backup/ConfigIndex.tres.p6cbefore`。

### 📌 P1→P6 整条线现在的状态（给下一位接手的人）

| 阶段 | 内容 | 状态 |
|---|---|---|
| **P0** | 调研（动画普查 + 硬编码审计 + 战斗管线盘点） | ✅ |
| **P1** | 数据层：清旧表 + 建新表（2 角色 / 6 武器 / 39 技能行 / 59 增幅 / 6 升级池） | ✅ |
| **P2** | 机制层：蓄力·完美时机 · 技能层数 · 滞空 · 奔跑/空中跳跃 · 子弹行为 · 标记 · 格挡 · 下落攻击 | ✅（⚠️ **"奔跑"只做了数据语义，没有输入/状态** —— 见 P6b ② 的"护卫"） |
| **P3** | 角色 A（3 把远程武器 + 技能 + 被动） | ✅ |
| **P4** | 角色 B（3 把近战武器 + 技能 + 被动，武技1 统一驱动） | ✅ |
| **P5 / P5b** | 增幅接入 + 池重构（限定/互斥/前置三层判定 + 图鉴数据基础） | ✅ |
| **P6a** | 清旧（旧角色/武器/技能/增幅/池/能力卡）+ 基地 UI 改造 | ✅ |
| **P6b** | 弹匣横向清理 + 2 条异化 + 蓄力指示器 + UI 两个尾巴 + 两个可选清理 | ✅ |
| **P6c** | **4 项表现层特效**（开镜 / 后撤残影 / 剑气拖尾 / 落圈）—— 只读运行期状态，战斗逻辑零改动 | ✅ |

**P6c 之后仍然开着的口子**（都不阻塞玩法）：
1. 通用异化 3 条没做（护卫 / 灵魂收割 / 落日剑）—— 见 [upgrades.md](upgrades.md) §7 第 1 条。
2. 角色 A/B 的**生命 / 护甲 / 移速是预设值**（220/20/6.5 与 220/25/6.8），设计文档没写，**等用户确认**。
3. `ApplyAbilityMatrix` 的时机要拆三步才修得对（见 P6b ⑤）。
4. 3 条占位行 + 8 处"只接了一半"的增幅（[upgrades.md](upgrades.md) §7 第 2、3 条）。
5. `ArmorBreakFx`（破甲特效）的 `TorusMesh.Rings`/`RingSegments` 也是写反的（环是八边形），
   修法 = 两个数字对调；本轮不在边界内，见 [vfx.md](vfx.md) §7。

### 任务 H：P6a —— 清旧 + 基地 UI 改造 + 三个遗留问题 ✅ 已完成（2026-10-15）

> ⚠️ **阶段纪律变了**：**"只加不删"是 P1→P5 的纪律；P6 起解禁**。P1→P5 期间攒下的旧内容
> 已在 P6a 一次清完（见 [character-redesign.md](character-redesign.md) §六）。
>
> **删了**：旧角色 3（`chr_gunner` / `chr_heavy` / `chr_summoner`）· 旧武器 3（`wpn_pistol` / `wpn_sniper` /
> `wpn_rifle`）· 旧技能行 5（`skl_dash` / `skl_burst` / `skl_melee` / `skl_summon` / `skl_control`）·
> 旧增幅 17（强化 10 + 进化 4 + 异化 3）· 旧升级池 3（`pool_pistol` / `pool_sniper` / `pool_rifle`）·
> 能力卡 2（`card_summon` / `card_control`）· 能力类 2（`DashAbility` / `BurstFireAbility`）·
> `Player.tscn` 的 5 个旧角色专属能力节点 · `project.godot` 的 4 条旧按键动作。
>
> **索引 `9/5/10/1/1/44/7/13/8/6/9/11/8` → `6/2/10/1/1/39/7/13/8/6/6/11/6`**（差额只有
> 武器 −3 / 角色 −3 / 技能 −5 / 升级池 −3 / 能力卡 −2）；**增幅项 74 → 57**（强化 9 / 进化 23 / 异化 25）。
> 其余硬指标逐位不变：**节点布置 10（2/5/3）· Pressure=3 pmods=3 bank=0 · 白板首领 315/甲 0 ·
> 端到端 F=True · 最小闭环链路 ✓**。
>
> **⚠️ 两处读数按用户裁定换了基准**（"防回归"的意义是别的东西没被改坏，角色换代后基准本身就该换代）：
> - `配置表比对·玩家` **270/40/7 → 220/20/6.5**（自测基准角色从 `chr_gunner` 换成 `chr_hunter`）
> - `配置表比对·升级池` `pool_pistol / 9 行 → pool_energy_palm / 6 行`
> - ⚠️ **角色 A 的生命/护甲/移速（200/20/6.5）是 P3 代理自拟的预设值** —— 设计文档没写这三项，
>   **等用户确认**；不要当成经过设计的平衡值。
>
> **同时落地了用户两条澄清**：① 删掉基地的「开始探索」按钮（进关卡 = 走到 **出击门**
> `Base.tscn/TerminalGate` 按 F，已核实它真的能进关卡）；② 删掉"出征时按角色行把枪收起来"
> —— "左键归谁"改由**武器行**的 `WeaponConfig.UsesWeaponComponent` 决定（6 把新武器都是武技驱动），
> 基地预览与关卡同源（进关卡**只是切场景**）。基地那页旧的「永久强化」（`BaseScreen`）**整份删除**，
> 局外成长台现在直接开**能力卡界面**（`meta_quota` 与 `MetaProgress.Shards` 一行没动）。
>
> **三个遗留问题的结论**：① `MaxAimDistance` 三条 `已跳过` 警告 → 删参数项（那个类没有瞄准射线）；
> ② `ArmorBroken` / `ShieldBroken` / `Critical` **补上了唯一抛点**（都在 `Unit.ReceiveHit`），
> 顺手把「能量护盾」的"护甲破碎"那一路接上；③ `VFXControllerBB` **复现不出来**（全局类缓存过期，
> 已自愈，不需要改代码）。
>
> **顺手修了一个真 bug**：`践踏死亡` 原来**覆盖**而不是累加额外空中跳跃，会把被动「灵动」的 1 次吃掉。
>
> **留给 P6b（✅ 2026-10-16 已全部办完，见上面「任务 I」）**：① 通用异化还差 5 条（杀戮践踏 / 护卫 / 巡飞弹 / 灵魂收割 / 落日剑 —— 每条都要一个新机制）；
> ② 弹匣/装填那条横向清理（`card_magazine` / `card_reload` / `meta_reload` + `Weapon.cs` 的装填逻辑 ——
> 本批明令不许碰 `Weapon.cs`）；③ `docs/config.md` 等长文档里还有旧角色的读数（**只改会误导的**，没全改）。

### 任务 0：配置表体系阶段 2 —— ConfigIndex 拆成 8 张表 ✅ 已完成（2026-10）

> **做了什么**：`ConfigIndexData` 从 74 字段降到 30 个全局字段；新增 8 张表（武器 3 / 角色 3 / 敌人 1 /
> **效果 10** / 节点 9 / 增幅项 19 / 局外成长 5 / 关卡 3，共 **53 个行文件**），全部"一行一个 `.tres`"，
> 索引挂在 `ConfigIndex.tres` 的 `[Export] XxxConfig[]` 数组上。
> **没有做逐行回退**（保持资源引用数组）—— 但**不要单独删/改行文件**，见下面那条纪律。
> `ConfigIndex.Settings.Xxx` **仍然可用**（渐进迁移，没有大爆炸式重构）。
> 进度 / 实测证据 / 逐项比对 / 两条新坑见 [config-schema.md](config-schema.md) §5 阶段 2 与 §8；调参入口见 [config.md](config.md)。
>
> **留给下一轮的三件事**（按建议顺序）：
> 1. ✅ **AI 行为表（盘点 #15）已做完（2026-10 配置表阶段 5）**：新增 `Config/Tables/AiProfiles/ai_*.tres`
>    **11 行**（一行一个 AI 档案 = 一个单位的整套 AI 参数），行类 `AiProfileConfig`；`UnitConfig` 多一列
>    **`AiProfile`（资源引用）**，套用挂在 `UnitConfig.ApplyTo` 上（**没有碰 `EncounterController`**）。
>    盘到 **25 个 AI 参数**（原先散在 10 个敌人场景的 **53 处**赋值里）。
>    ⚠️ 4 处硬编码避障启发式（`Behavior.AvoidObstacles` 的 2.4 / 0.6 / 1.2s / 0.35）**没有进表**（属"AI 手感"保护值）；
>    ⚠️ 档案只调数值，**不增删行为节点**（拓扑仍在场景里）。
>    见 [config.md](config.md) 的「配置表阶段 5 实测」与 [config-schema.md](config-schema.md) §5 阶段 5。
> 2. ✅ **首领增强的"节点驱动"（T9 最后一步）已做完（2026-10 配置表阶段 6）**：
>    `NodeConfig.BossPressurePoints` / `BossModifierRolls` / `BossModifierTags` 三个字段
>    **已全部接进** `StageController.AddPressureForNode`（不是 `EncounterController.BossModifierRolls` ——
>    改成 `StageController` 排队 + `BossSpawned` 信号在 Boss 出生时套用，避免把关卡逻辑渗进遭遇层）。
>    §7.5 的 Pressure 转换池真的接线了：`PressureModConfig` 新增 `Tags`、`Weight` 真的加权；
>    **两个抽取点拆开**（Boss 开场词条 `Roll()` 只抽行为型，Pressure 转换 `RollPressure()` 只抽数值型）。
>    ⚠️ 因此**开场词条的结果会变**（候选 7 → 4，§7.1 要求的分离）；自测的 `+187.5 → 1` / `+225 → 3` **没漂**。
>    ⚠️ 两处需要平衡测试 / 后续处理：① `BossInitialPressure` 不再叠加节点累积 Pressure 折算出的压力等级
>    （那是 §7.5 的替身）；② HUD 的 `[词条]` 一栏只显示行为型，不显示 Pressure 数值强化。
>    见 [config.md](config.md) 的「配置表阶段 6 实测」与 [boss.md](boss.md)。
>    ⚠️ 关卡核心文档 §7.1 的硬约束仍然成立：**Boss Pressure（数值强化）与 Boss Build（行为/技能）必须分离**，
>    且**不要为 Pressure 设计"可被玩家增幅修改"的接口**（本轮没有加）。
>    ✅ **§8.2 的「免费挑战不加 Pressure」闸门已接（2026-10）** —— 见下面「任务 D」。
> 3. **收尾决定**：`RewardConfig.PoolTags` / `ConflictsWith` 两个死字段（接上或删掉）、
>    `WeaponConfig.ApplicableWeapons` 反向指（武器↔升级池关联）、`Pistol.tscn` 的数值（回退基线）。
>    → ✅ **`PoolTags` / `ConflictsWith` 已在阶段 4 处理**（前者删除、后者接上）；
>    `WeaponConfig.ApplicableWeapons` **全项目从未存在过**，且用户拍板改成**正向指**（武器 → 升级池表），
>    所以不再需要它；`Pistol.tscn` 的数值**仍未动**（它现在是"表读不到时的回退基线"）。
>    见 [config.md](config.md) 的「配置表阶段 4 实测」。
>
> ✅ **阶段 3 已完成（2026-10）**：统一单位表（`UnitConfig` 基类 + 敌人 10 行 / 首领 1 行 / 召唤物 1 行 +
> 角色表改继承）· 技能表 25 行（含 4 条首领行为词条）· 首领数值强化池 3 行 · 白名单改成按 Id ·
> 6 处死字段清理 · 敌人倍率静默换挡修复 · `BarEnemy` 居中修复。
> 详见 [config.md](config.md) 的「配置表阶段 3 实测」与 [config-schema.md](config-schema.md) §5 阶段 3。

### 任务 G：用户第三轮 C1（护甲无上限）+ C2（节点池规则）✅ 已完成（2026-10-12）

> **用户原话**：C1「**有甲了才会显示**，**护甲没有上限额度**，所以**可以删除掉 `MaxArmor`** 的」；
> C2「**进化池里会出什么选项是根据交互的角色持有的攻击方式（武器）**，并没有"**放哪个进化池节点**"的说法……
> 而**池里一开始有什么，应该在进入关卡初始化的时候就判断好的**」。
> 需求原文：[requirements-ability-system.md](requirements-ability-system.md) 的「用户第三轮澄清（2026-10-12）」。
>
> **做了什么**：
> 1. **C1**：`Unit.MaxArmor` / `UnitConfig.MaxArmor` → **`StartingArmor`（开局护甲）**（**改名，值一个没动**）；
>    `MaxArmorMultiplier` / `MaxArmorAdd` 两个 StatSheet 键与 `RefreshStats` 里"抬高上限并补甲"那段**删除**；
>    新增 `Unit.GrantArmor()`（精英怪 `EliteModifier.ArmorAdd = 120` 改走它 —— "纯粹的加甲"）；
>    **显示判据 `MaxArmor > 0` → `Armor > 0`**（3 处 `SetArmor(...)`：`PlayerHud.UpdateArmorBar` /
>    `WorldOverlay.UpdateBar` / `WorldOverlay.CreateBar`）→ **甲打光不再留空壳**；
>    甲条比例的分母换成运行期派生的 `Unit.ArmorPeak`（不来自配置、不参与结算）。
>    ⚠️ `ArmorDamageReduction`（减伤）与 `ArmorBroke`（碎甲，仍是"甲从 >0 掉到 0 那一次"）**一行没动**；
>    `BarBoss.tscn` / 首领没动（0 甲 → 依然天然不显示）。
>    ⚠️ **必然的行为变化**：本来 0 甲的敌种精英化后**真的多出 120 甲**（旧实现只是把上限抬到 120、甲仍是 0）。
> 2. **C2**：候选集**原来在"第一次交互时"才建**（四个惰性分支，其中两个忘了设 `SummonerAllowed`）→
>    改成 **`RunController.InitializePool()`，只在进关卡的 `ApplyBalance()` 里调一次**；
>    交互时只从定好的池里抽（仍然按当前构筑过滤互斥/层数/节点 `Kind`）。
>    **重复的两个进化池节点行合并**：删 `node_weapon.tres`（`Kind = Weapon`，场景无实例、`BossPressurePoints` 是 0），
>    保留 `node_evolution.tres`；`ConfigIndex.tres` 的 `[ext_resource]` 与 `Nodes` 数组项**同一次**删；
>    `NodePlacement.LayerOf` 兜底补 `Weapon => 2`（否则 `Weapon=4` 会掉到 1）；
>    `Config/Tables/UpgradePools/` 三个按攻击方式分的池**保留不动**。**`Id`/类/目录一律没改名。**
> 3. **硬指标**：除 **`配置表比对·索引` 的 `节点 9 → 8`**（合并 = 少一个行文件，必然结果）之外**逐位一致** ——
>    `节点布置 10（高 2/中 5/低 3）`（**它数的是场景实例，不是表行**）、`Pressure=3`、`端到端 F`、`音效 13`、
>    `增幅项 19 条 / 池子 18 条`、`价值标签 → 节点 [Weapon=4 …]` 全部照旧；日志无 `ConfigIndex.tres` 加载失败。
>
> **数据改了哪 18 个文件**：行文件 14（`Enemies/` 9 + `Bosses/` 1 + `Summons/` 1 + `Characters/` 3，
> 含三个角色行的 `DescriptionTemplate` 占位符）+ 场景 4（`Guard` / `Charger` / `SkillElite` / `TrainingDummy`），
> 外加**删除** `Config/Tables/Nodes/node_weapon.tres`。全部 **LF + 无 BOM**。
> **`ConfigIndex.tres` 里本来没有护甲字段**（C1 完全没碰它），C2 才碰了它的 `Nodes` 数组。
> 细节与两侧实测见 [config.md](config.md) 的「护甲无上限 + 进化池合并实测」。

### 任务 F：用户三项修正（护甲只给精英 / 行为型改称技能 / 命中特效落在命中点）✅ 已完成（2026-10）

> **用户原话**：②「**护甲目前就只给精英怪使用就好了，首领不需要**」、
> ④「**行为型的词条其实不太对，这个应该是算是技能**」、
> ⑤「不需要（细分开关），**实际应该在命中的位置发生特效**」。
>
> **做了什么**：
> 1. **`pmod_fortress_armor`「铁壁·护甲」整行下线**（Pressure **8 → 7 条**）：行文件 +
>    `ConfigIndex.tres` 的 `[ext_resource]`/数组项 + **`BossPerkCatalog.FallbackPressure` 里的代码兜底**
>    —— 三处同一次改（只删表行 = 表读不到时首领又吃护甲，静默换挡）。
>    ✅ **精英护甲不受影响**：它来自 `Scripts/Combat/EliteModifier.cs`（`ArmorAdd = 120`），与 Pressure 无关；
>    改完 `MaxArmorAdd` 的生产者全项目只剩它一个。自测期望串同步 `…/25/8/…` → `…/25/7/…`。
> 2. **术语统一**：**行为型 = 技能**（`bhv_*.tres`，本来就是技能表的行）、**数值型 = 词条**。
>    改了 `BossPerkText`（新增 `Summary()`，HUD 那一行变成 `技能 2 · 词条 2 ◆… ◇…`）、`PlayerHud`、
>    `BossPreviewScreen`（拆成【技能】/【词条】两节）、`BossPerkCatalog` 的日志与注释、
>    `AbilityConfig` 的 `[ExportGroup]` 标题与若干注释 + 四份文档。
>    **没有**动任何 `bhv_*` 的文件名 / 目录 / 类名 / 字段名；`◆` / `◇` 两个标记保持不变
>    （它们本来就区分来源，问题在"叫法"）。
> 3. **命中特效落在实际命中点**：`AttackHitbox` 用**目标碰撞体表面上离命中体最近的那一点**
>    （`ResolveHitPoint`：胶囊/球/盒三种形状 + 碰撞体中心兜底），取代"目标原点 + 全局高度偏移"；
>    **`HitEffectHeightOffset` 整列删除**（字段 + `ConfigIndex.tres` 那行）。投射物那条路径**一行没动**。
>    ⚠️ 第一版实现是"打一条射线取接触点"，自测里 7 次跑出 3 次**静默打空**（射线读物理空间在查询那一刻的
>    状态：目标被脚本挪动 / 刚被冻住 / 刚被打死都会打空）→ 改成几何算法，两次连跑数值逐位相同
>    （出生点到碰撞体表面偏差 **0.026 m**，旧做法基线 **0.4 m** = 埋在身体里）。
>
> **证据**：`build.ps1 --no-incremental` **0 warning / 0 error**；自测两次连跑
> （`run_20261012_003652.log` / `run_20261012_003734.log`）全程通过并自动退出、`selftest.flag` 跑完即删；
> 索引行 `Pressure 7`、池明细 7 条无护甲、概率 23.1/23.1/15.4/15.4/7.7×3；探针的合成 `StageIds` 判定仍生效。
> 详见 [config.md](config.md) 的「用户三项修正实测」与 [boss.md](boss.md) 的「Boss 构筑（技能 + 词条）」。
>
> **⚠️ 留给下一轮的两个决定**：① ~~首领**本体的 200 基甲**没动~~ → ✅ **已去掉（2026-10 再一轮，用户明确要求「去掉首领的甲」）**：
> `boss_final.tres` 的 `MaxArmor = 0` + `Boss.tscn` 那份同值导出删掉；**护甲字段全部保留在基类上**
> （用户澄清：护甲是所有单位通用的属性，首领只是"没有获取手段"）。判据/管线走向见
> [config.md](config.md) 的「首领去甲 + 单位动画实测」§一。**遗留**：首领有效血量因此明显下降，
> 要不要补偿调参（`MaxHealth` 或 `BossHealthMultiplier`）是数值决策；
> ② `Scripts/Run/StageController.cs` 里两条日志仍写着"Boss Build **行为词条**"
> （受"只改数据不改算法"的边界约束没动，建议单独一次只改字符串的提交）。
>
> ✅ **同轮还做完了**：**单位动画系统**（`Scripts/Combat/UnitAnimator.cs`）—— 玩家 + 10 敌人 + 首领
> 按移速在 `idle`/`walk`/跑 之间切、死亡播 `die`；参数是 `UnitConfig` 新增的「表现 · 动画」6 列
> （**空 / 0 = 不覆盖**），新敌人不挂节点、不写代码、不填表也能动（`UnitPresentation._Ready` 会 `Ensure()` 补挂）。
> **边界**：`RunController.ApplyBalance` 不调 `ApplyTo`，所以**玩家那 3 行（`chr_*.tres`）的动画列当前不生效**
> —— 一行修法（`character.ApplyAnimationTo(_player);`）与证据见 [art-integration-log.md](art-integration-log.md) §11。

### 任务 E：Pressure 词条"一参数一栏"（3→8 条）+ 技能参数入池 + 限定词刷新判定 ✅ 已完成（2026-10）

> **用户要求**：① 3 条应该拆成 5 条（**每个参数单独占一个词条栏**）；② 把一些**技能的参数**也加入词条；
> ③ 每个 boss 可能都不同，**限定词记得做好刷新判定**。
>
> **做了什么**：
> 1. **数值型 3 → 5 条**（`pmod_berserk` → 伤害/攻速两条，`pmod_fortress` → 生命/护甲两条，`pmod_swift` 不变）+
>    **3 条技能参数词条**（尖啸回响 / 冲锋冲击 / 召唤急速）= **8 条**。权重**继承母行**（3/3/2/2/2）+ 技能型各 1。
> 2. **技能参数进 `Pressure/`（数值型）而不是 `Skills/`**：依据 §7.5 明列「技能冷却等参数」属数值型；
>    `PressureModConfig` 多 `AbilityNode` / `AbilityScales` / `AttackScales`，`Value` 是**倍率**（可叠乘）。
> 3. **限定词 + 刷新判定**：`PerkQualifier`（`BossIds` / `StageIds` / 必须有技能节点）+ `BossPerkCatalog.ApplyWithRefresh`
>    —— 判定在 **Boss 出生时**（`perk.Apply` 里），不成立就**换抽**；`StageController` / `EncounterController` **一行没改**。
>
> **⚠️ 需要知道的两件事**：
> - **每抽的期望强度降到原来的 58.9%**（"一抽只买一个参数"的必然结果，且**调权重补不回来**）——
>   要补只能提高单条数值或降阈值，**这是一轮平衡测试的入口**。
> - 旧行文件 `pmod_berserk.tres` / `pmod_fortress.tres` **已删除**（与 `ConfigIndex.tres` 的数组项 + ext_resource 同一次改动）。
> - 行为型词条（`bhv_*`）**还没有**限定词列（要给 `AbilityConfig` 加列才行，本轮没做）。
>
> 证据：`build.ps1 --no-incremental` 0/0；自测 `+187.5 → 1` / `+225 → 3` **逐位不变**；
> 真实出生实测有"抽到铁壁·护甲 → 出生时判不成立 → 换抽狂暴·伤害 → 护甲停在 200"的完整链条；
> 回退（清空 `PressureMods`）跑满一轮，池明细与表在时逐字相同。
> 详见 [config.md](config.md) 的「配置表阶段 7 实测」与 [config-schema.md](config-schema.md) §5 阶段 7。

### 任务 D：§8.2 免费挑战闸门 + HUD 词条 + 首领状况预览面板 ✅ 已完成（2026-10）

> **做了什么**（三件事，用户一次提的）：
> 1. **§8.2「免费挑战不加 Pressure」闸门接上了**：`StageController.AddPressureForNode` 第一件事读
>    `CurrentChallengeFree`（= 挑战数 ≤ 本关免费额度，额度在 `StartEncounter` 里随挑战数扣，§8.2 第 1–2 步），
>    免费挑战直接返回 —— **不累积 Pressure、也不抽数值强化**（连节点行的"额外抽几次"一起拦）。
>    ⚠️ 顺手修了一个**真 bug**：`StageController._currentNodeKind` 从来没被赋值过，
>    所以**正常游玩时 Pressure 一次都不会累积**（只有自测直接调 `AddPressureForNode` 才有数字）。
>    现在 `StartEncounter` 里写 `_currentNodeKind = node?.Kind`（证据 = 自测端到端那行的 `node=Enhancement`）。
> 2. **HUD 血条栏显示词条名（两套来源都显示）**：新增 `Scripts/UI/BossPerkText.cs` 统一文案，
>    `◆` = 行为型（`EncounterController.BossPerks`）、`◇` = 数值型（`StageController.PressurePerks`）；
>    同名折叠成 `名字×N`、超过上限折叠成 `…+N`（血条栏 5 个 / 顶部状态行 4 个）。
>    节点在 `Scenes/UI/Bars/BarBoss.tscn` 的 `Panel/PerkLabel`。
> 3. **首领状况预览面板**（快捷键 **B**）：`BossPreviewScreen` + `Scenes/UI/BossPreviewScreen.tscn`，
>    走 `OverlayScreen`（分层 `Panel`）/ `UiService.OpenBossPreview()` / `UiRouter`，**没有绕过任何一层**。
>    内容 = 免费额度与闸门状态 / Pressure 阈值·跨阈值次数·余数·转换池候选与权重 / 两类词条逐条（带说明）/ 首领当前数值。
>    正文由静态只读的 `Describe(stage, encounter)` 生成，自测不开界面就能断言。
> 4. **自测**：`SelfTest.ReportPressureGate()` 替换了原来直接调两次 `AddPressureForNode` 的写法 ——
>    先验"免费挑战 = 0"（新增硬指标），再用 `DebugExhaustFreeQuota()` 把额度用掉，之后 `+187.5 / +225` 逐位不变。
>    细节见 [config.md](config.md) 的「免费挑战额度（§8.2）」。
>
> **还没做**：预览面板只有快捷键（用户拍板先这样，后期与词典图鉴合并）；交互时不会自动弹面板，
> 只在 HUD 的交互提示里加了 `[B 首领状况]`。

### 任务 A：血条改事件驱动（用户明确要求）✅ 已完成（2026-10）
> 实现：`Scripts/UI/WorldOverlay.cs`。已订阅 `GameplayEventBus.Raised`（`_Ready` += / `_ExitTree` -= 成对），
> `Damaged` → O(1) 登记并建血条，`Died` → 移除，`_Process` 只更新已登记血条、不再遍历单位组。
> 实测数据见 [config.md](config.md) 的「血条事件驱动」验证条目。
> **注意**：若一次伤害直接致死，`Damaged` 与 `Died` 在同一调用栈内先后触发，血条会「建好又立刻拆掉」，
> 所以用秒级轮询 `Bars` 子节点数是看不到血条的 —— 这是正确行为。

**问题**：`Scripts/UI/WorldOverlay.cs`（195 行）在 `_Process` 里**每帧遍历** `enemy_units` 和 `boss_units` 两个组来画头顶血条。

**要求**：血条应该是**事件通知** —— 扣血后通知，而不是遍历（除非用 ECS，本项目不是）。

**做法**：
1. 在 `_Ready` 订阅 `GameplayEventBus.Raised`，在 `_ExitTree` 退订（**必须配对**，项目里其它订阅都做到了）。
2. `GameplayEventKind.Damaged` → 若目标是敌方单位（`Faction != Faction.Player`），**O(1)** 登记它并创建血条。
3. `GameplayEventKind.Died` → 移除对应血条。
4. 满血的单位移除血条（保留现有的"满血不显示"设计）。
5. `_Process` 只更新**已登记**的那几个血条的位置/血量（通常 0–5 个），**不再遍历组**。
6. 保留现有能力：首领血条由 `PlayerHud` 负责（屏幕上方），`WorldOverlay` 只画普通怪**脚下**血条；`isBoss` 的单位在这里要跳过。

**验收**：
- 构建 0 warning / 0 error
- 跑自测：战斗中日志无异常；用 `await_runtime_condition` 读 `get_node("/root/Main/WorldOverlay").get_child_count()` 这类值确认没有每帧增长
- 视觉：跑起来后敌人受伤会出血条，满血/死亡后消失（可用 `runtime screenshot` 截图确认）
- `docs/architecture.md` 或 `docs/config.md` 里补一句"血条是事件驱动"的说明

### 任务 B：修「移动残影」 ✅ 已完成（2026-01）

**问题**（用户实测）：角色移动、跳跃、滑铲时**整个角色在剧烈缓动**，能看到一层不明显的残影。

**真实原因**（与交接文档给的三个方向**都不同**，靠逐帧探针测出来的）：

1. **主因 —— 物理 60Hz / 渲染 120fps 不同步，且没开物理插值。**
   角色的 `CharacterBody3D` 只在物理帧上移动，而渲染 120fps：实测 301 帧里 **150 帧（49.8%）位移为 0**，另外 150 帧每帧位移 0.114（= 两帧的路程）。肉眼就是「停一下、冲一下」。*跟 `UnitPresentation.cs` 无关*：那个文件只做受击闪色与延迟销毁，**没有任何位置插值**。
2. **帮凶 —— `CameraRig` 的跟随写在 `_Process` 里。**
   镜头每个渲染帧都在平滑移动（σ=0.057，单帧位移在 `0 ↔ 0.1167` 之间交替），而被跟随的角色只有一半的帧在动，两者错拍 → 角色/镜头相对位置每帧抖 ±4cm，角色在画面上「一进一退」，这正是"残影"的来源。
3. `CharacterMovement` 的 `_inputVelocity.MoveToward(...)` 与 `FaceDirection` 的 `LerpAngle` 是**正常的加减速/转身缓动**，不是 bug，保留。

**修复**：
- `project.godot` → `[physics] common/physics_interpolation=true`
- `Scripts/Camera/CameraRig.cs` → 跟随从 `_Process` 移到 `_PhysicsProcess`

**验证数据**（`SelfTest` 表现采样阶段，按住 W 前进 301 帧、120fps）：

| 指标 | 修复前 | 修复后 |
|---|---|---|
| 角色位移为 0 的帧 | 151 / 301（50.2%） | **0 / 301（0%）** |
| 角色单帧位移 标准差 | 0.0579 | **0.0058**（↓10×） |
| 相机单帧位移 标准差 | 0.0572（0↔0.1167 交替） | **0.0092** |
| 角色↔相机相位差 标准差 | 0.0112 | **0.0072** |

截图：`docs/screenshots/ghost_after_1..3.png`（移动中连拍三张，角色轮廓单层、屏幕位置稳定）。
复现方法：`SelfTest` 的表现采样阶段（第 11~12 阶段）会打这些数字；临时去掉 `physics_interpolation` 即可回到修复前的数值。

### 任务 C：清理与文档同步 ✅ 已完成（2026-01）

1. 已删除 `Scripts/Run/KillPlane.cs`（连 `.uid`）—— 确认全项目无引用（`KillPlaneY` 是平衡配置，仍被 `MapManager.EnsureKillZone()` 使用，保留）。
2. `docs/controller-flow.md` 已更新；`docs/config.md` 的区域杀一行也改成「所有单位进入即杀」。
3. `docs/` 与代码不一致处（本次发现并已修）：
   - `stages.md` 还写着「`ActiveNodeCount` 从 3 提到 6 / 实际启用 5 个」—— 实际 `ConfigIndex.tres` 是 `RewardNodeCount = 3/4/4`，自测实测启用 **10 个（2/5/3）**。
   - `stages.md` 的实测表还写着「剩余节点 3 / 免费挑战 2」，与当前「剩余节点 9 / 隔离次数 2」不符。
   - `config.md` 说「怪物血条改到脚下仍缺视觉确认」，实际 `docs/screenshots/healthbar_feet.png` 已有确认。


---

## 四、最近一轮已完成的工作（供参考，不要重复做）

- `PlayerLocator` 统一玩家引用；`Interactable` 交互判定 60Hz → 10Hz；`PlayerHud` 刷新 60Hz → 20Hz
- 区域杀从"每帧遍历 4 个组查 Y"改为**碰撞区域** `KillZone`（进入即杀）
- `OverlayScreen` 加分层（`UiLayer`）与开合动画；`UiRouter` 按层决定暂停
- 抽出 `RewardService` / `RunSettlement`（纯函数）
- `SelfTest` 移到 `Scripts/Testing/` 并加 `#if DEBUG`
- 基地场景 `Scenes/Levels/Base.tscn`（4 个交互台 + 出击门），主场景已切到基地

**历史导航问题，F专项已修复**：原MapManager只采组内根节点，空区域得到0多边形；现递归采World碰撞，实测73多边形并验证绕墙和刷怪落点。

---

## ⚠️ 编辑器构建失败 / MCP 起不来（2026-10-12 实测确认）

**现象**
- 编辑器 Problems 报：`无法解析 SDK "Godot.NET.Sdk/4.7.2"` / `找不到指定的 SDK`
- `run_project` 报：`Play was requested but no debugger session became active` 或
  `The scene likely failed to load — check ProjectSettings application/run/main_scene`
- `runtime info` / `runtime screenshot` 报：`no_active_sessions`

**根因（用户实测结论 —— 不是代码问题、不是 SDK 没装）**

**有子代理在同时运行时就会出现。** 多个 Godot 实例并发会抢编辑器的构建回调：
编辑器 `play_custom_scene` 触发 C#「运行前构建」，而它在 deferred 上下文里建
ProgressDialog 失败：

```
Do not use progress dialog (task) while flushing the message queue
```

→ 构建回调失败 → 没有 debugger session → 运行/截图全都拿不到。

**解法**
1. **重启编辑器**（大概率解决）—— 关掉编辑器进程后重新起：
   ```powershell
   & 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe' `
     -e --path 'F:\Godot项目\项目a' --mcp-port=9080
   ```
   ⚠️ **若当时仍有代理在跑，重启后仍会失败** —— 这是预期的，等代理收工再重启。
2. **任何时候都可靠的绕过：命令行直跑 + 读日志**（不依赖编辑器与 MCP）
   ```powershell
   $godot = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe'
   '1' | Set-Content 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'
   Start-Process $godot -ArgumentList '--path','F:\Godot项目\项目a','res://Scenes/Main.tscn'
   Start-Sleep -Seconds 27
   Remove-Item 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'  # 必须删
   # 日志：C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\logs\
   ```
   ⚠️ **必须带场景参数** —— 主场景是基地，不带会进基地（自测会因为找不到 `Main/Stage` 而输出一堆空值）。

**给并行代理的纪律**
- 跑验证**优先用命令行直跑**，不要依赖 MCP 运行时桥
- **`stop_project` 是全局的**，会杀掉别的代理正在跑的自测实例 —— 不要随便用
- 设置 `selftest.flag` 的窗口要尽量短（它会影响那几秒内**任何**启动的游戏实例）
- 若必须用编辑器：**用完把自己的编辑器实例关掉**

---

## ✅ 编辑器 / MCP 的正确用法（2026-10-15 用户实测确认，**推翻了之前的结论**）

### 真正的根因

之前记录的「编辑器构建失败 / MCP 起不来」，**根因是 Godot 编辑器被关掉了** ——
**不是代码问题、不是 SDK 没装、也不是"子代理并发"**。

**用户实测**：**编辑器开着 → MCP 自动可用**（不需要额外配置）；
编辑器关掉时，代理只能退回"命令行直跑"，于是**每次都要启一个 Godot 进程** →
**慢，而且反复弹控制台窗口**。

### 代理应该怎么做

1. **先查编辑器在不在**：
   ```powershell
   & 'F:\Godot项目\项目a\.gdmcp\bin\gdmcp.exe' doctor     # 看 editor_connected
   ```
2. **不在就自己开一个**（不要干等、也不要一上来就命令行直跑）：
   ```powershell
   & 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe' `
     -e --path 'F:\Godot项目\项目a' --mcp-port=9080
   # 等约 25 秒，再 doctor 确认 editor_connected: true
   ```
3. **然后优先用 MCP**：
   ```powershell
   & $cli tool-call run_project --args-json '{"allow_window":true,"scene_path":"res://Scenes/Main.tscn"}' --apply
   & $cli runtime screenshot --format png --save-path 'user://x.png'
   & $cli runtime info
   ```
   —— **比命令行直跑快得多，也不会反复弹控制台窗口。**
4. **若 `run_project` 仍报 `no debugger session` / 构建回调失败**：
   - 先**再试一次**；仍失败就在编辑器里手动"构建项目"一次，再试
   - **最后**才退回下面的命令行直跑
5. ⚠️ **用完不要关掉编辑器**（别的代理还要用）；⚠️ **也不要 `stop_project`**（它全局生效，会杀掉别人正在跑的自测）

### 命令行直跑（**兜底**，编辑器/MCP 实在不可用时才用）

```powershell
$godot = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe'
'1' | Set-Content 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'
Start-Process $godot -ArgumentList '--path','F:\Godot项目\项目a','res://Scenes/Main.tscn'
Start-Sleep -Seconds 40
Remove-Item 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'   # 必须删
# 日志：C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\logs\
```
⚠️ **必须带场景参数**（主场景是基地；不带会进基地，自测输出一堆空值）。
⚠️ `-- --char=xxx --weapon=yyy` 这类旁路参数只在需要绕过选题界面时用。

---

## 本轮专项后的「仍然开着的口子」（2026-10-17，覆盖上文历史清单）

- 终版美术持续内容：角色、敌人、武器、终端、场景的统一风格与替换，当前庭院/角色/免费特效是阶段性交付。
- GC缓存竞态已稳定复现并接入项目侧规避（I专项）；旧SetGodotObjectPtr堆栈未单独复现，继续保留，不能宣称引擎级永久修复。
- 原始角色设计纠偏：J1～J6范围已验，J7已把Main旧同步蓄力探针迁移成跨帧，**J8已把发散／心眼改成「加标来源进表＋按命中前层数加伤」并逐项取证**；继续龙之箭、其余进化、统一暴击、独立动作及各武器逐条证据，详细交接见DEEPSEEK_HANDOFF.md。
- 全局暴击对近战的适用范围：现有近战快照只读命中体自身CritChance，未合入元进度暴击；H已公开实际0暴击和源码边界，未擅改规则。
- 平衡扩展验证：移动目标、散射全弹/远距离、群怪、完整构筑、两角色主动防御与整局胜率。

自带字体、破甲圆环、导航/Ogg正常退出、Loot/Battle接线、长枪蓄力、旧P4探针与连点跳段均已有本轮专项处理及证据。角色基础数值按用户选择保留，不再重复询问同一确认。
