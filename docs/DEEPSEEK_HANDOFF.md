# DeepSeek接管执行单（2026-10-17）

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

各批`docs/character-design-j*.md`、`*-evidence.log`、`*-failures.log`和截图均保留。更早A/E/B/C/D和F/G/H只代表当轮范围，仍有原文遗漏。

J6最终：专项无头80/0、渲染82/0；完整20进程回归后仅扩展探针再跑2进程，22进程均退出0且stderr空，Debug／ExportRelease均0警告0错误，Main八条硬指标逐字一致，四存档保持，selftest.flag不存在。实测普通波30/250、完美波40/400、短弩九箭；变奏两轮1,2,3,1,2,3，表内0.08秒在60Hz物理下为0.0833秒。详细输出见J6证据。

**J6边界：Main仍有6条旧同步蓄力探针红色输出**，不是已全绿。独立新探针的真实输入／动画／物理验证通过；旧探针同帧手动Tick，无法等待真实释放ATK，需迁移。原始红色行已附J6证据，不能删日志伪装通过。

## 下一步顺序

1. **先迁移Main旧同步蓄力探针**。文件`Scripts/Testing/SelfTest.cs`：`ProbeChargeMechanism`、`ReportCrossbowProbe`、`BeginBowProbe`里的武技1段、`ReportSwordQiProbe`。对应状态机case30、33、34、44。旧`SettleShot`/`WaitUntilIdle`只是手动Tick，不推进动画。可把这些段改成Task或分帧小状态，等真实ATK／自然收招后读结果，状态机必须等任务结束再Next，缺事件用明确超时失败；不要伪造ATK、不要给生产能力加计时出弹兜底。J5的`ProbeBlinkDash`/`ProbeGuardStance`已是分帧范例。独立`ChargeReleaseProbe`是真实输入范例。然后Main退出0、8指标、这6条新检查逐项核验，双构建0/0。
2. **发散／心眼**：旧`evo_divergence`和`evo_mindseye`复用BurnOnHit只加Mark，未实现按目标标记增伤；甚至来源过滤尚不完整。发散必须只由能量掌武技1波波加标，并仅提高波波；心眼只由箭加标，玩家对已标记目标的各种伤害提高50%。用户刚确认按本次命中前已有标记结算，第一次加标这次不加伤。必须走真实奖励获取与命中，覆盖其他技能／其他单位／多个目标／层数及过期，不直接Execute冒充接入。发散原文N未定，可配置预设并明确标注。新参数用强类型资源／列，不再增加Godot.Get/Set字符串反射。
3. **龙之箭**：`evo_dragon_arrow`当前只把ChargeMaxSeconds设3，未实现蓄力≥2秒更大＋全穿透。用表配置阈值／尺寸／穿透行为，按松手结果冻结，真实释放ATK发射；射向多靶及World验证。注意当前延长3秒会继续每0.1秒加档，这一上限解释如有歧义及时告知用户。
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

## 工程铁律与验收命令

- 改C#只能使用edit／apply_patch定点编辑，新C#也一样；禁止PowerShell多行替换或Python写C#。UTF8无BOM、LF。
- 编译严格：`& 'F:\Godot项目\项目a\build.ps1' --no-incremental -v minimal`，必须0 warning／0 error；另跑`dotnet build ActRoguelike.csproj --no-incremental -v minimal -c ExportRelease`。
- 构建／Godot验证独占`.ai_validation.lock`（FileShare.None）；Git独占`.ai_git.lock`，stage前必须空、精准清单、禁止git add -A。每批docs＋commit＋push。
- 增删ConfigIndex必须ext_resource＋数组项＋load_steps三处同改。现6武器／2角／39技能／62增幅，不能静默回退。
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

`project.godot`有用户／编辑器未提交变化，**保留、不混进提交**。大量未跟踪`.uid`、图片`.import`和csv翻译导入副产物也不能一锅端。验证会重拍旧批次截图，精准恢复自己生成的旧图，保留本批新图；禁止全量git restore。另一个窗口新增SkillIndicator四样式及文档（46f9988、456e14e、b69188d、e40e3bf）已在基线，不能回滚。

J5修复两个特效GDScript深载时找不到VFXOmniLightBB：改为显式preload依赖，各3次深载／GC可实例化。音频退出必须先杀淡出Tween、卸载流、提前释放声部，再真实异步Task.Delay80ms；旧游戏计时器.08秒在无头只经过1.693ms，不能改回。修后3次Main均stderr空。旧SetGodotObjectPtr的GCHandle变体仍未单独复现，不能宣称引擎永久修复。

原生AnimationPlayer同名Play不会自动从头，必须Play后Seek(0)；**不要Stop再Play**，4.7.2有旧访问违规崩溃史。事件轨道同一时刻两个方法会覆盖，架已改GuardOn@.15、GuardOff@.9。不要重生成整个50段动画库来改一段；全部单位事件走实例＋播放序号隔离。

## 联络与续做方式

用户专用Harness会话：`session-f70b9230-26cc-42e8-bda1-189470dab697`。本机桥接在仓库外`F:\Godot项目\.codex-dsh-bridge`，是原生`dsh headless --session-id`，不是已经注册的Codex Worker。桥接只输出公开final，不输出模型thinking、不读取／复制host.lock凭据。

用户现在授权该专用对话执行项目任务。先读本单和四份权威文档，检查git状态和锁，先处理Main旧同步探针，再逐项兑现剩余计划。每批给真实输出和截图、更新文档、提交推送；有歧义立即告知用户。不要等待Codex继续指挥，也不要把“已接管”当成任务已完成。
