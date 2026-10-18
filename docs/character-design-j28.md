# J28：细弹射线与粗弹形状扫掠（2026-10-18）

按用户确认的方案保留逐帧飞行：先处理追踪、下坠和全局限速，再沿实际速度×物理帧时长查询。投射物仍为不监测重叠、没有碰撞形状的Area3D，伤害来自主动查询。

查询几何与命中预算已分开：

| 配置入口 | 当前值／语义 |
|---|---|
| `Scenes/Combat/Bullet.tscn`、`Arrow.tscn` | `QueryGeometry=0`，线段射线 |
| `Scenes/Combat/EnergyWave.tscn` | `QueryGeometry=1`，半径0.14世界米 |
| `Scenes/Combat/SwordQi.tscn` | `QueryGeometry=1`，半径0.08世界米 |
| `Projectile.SweepMaxHits` | 正数为每帧最多处理的目标数；≤0迁移为默认8 |
| `ArrowVolleyAbility.QueryGeometry` | -1沿用场景，0射线，1形状；技能Params可覆盖 |
| `ArrowVolleyAbility.SweepRadius` | 形状路径的世界米半径，独立于模型缩放 |
| `SkillEnhancementTuning.ProjectileSweepRadius` | 大拔刀术继续明确配置0.16，不依赖模型放大 |

未配预算不再表示保留单次射线缺陷。射线每次穿透后从命中点查询余程，排除已处理的身体RID，不用前移偏移绕出身体。两种几何共用同一命中处理；预算耗尽停在当前命中点，下帧从该处继续飞行。穿透0命中一个、穿透1命中两个。箭矢原来同时把同一穿透数写到弹体和攻击快照、Launch再相加，本批改为只写攻击快照；`PierceBudgetAtLaunch`公开实际初始预算，龙之箭探针据此严格验证99而不是只验证表面字段。

命中、穿透、预算停点、撞墙与无命中均通过同一移动函数累计真实已飞距离，达到下坠距离后按原有逐帧规则开始下坠。弹射从真实命中点改向，下帧续飞；移除原先0.6米瞬移，防止跳过近墙／目标并漏记距离。下一目标到真正命中时才进入已命中集合。

形状路径保留J12胶囊候选扩容、单位中心沿弹道投影排序、多形状RID去重，以及World同半径球CastMotion和起点CollideShape。单位接触位置仍是中心投影近似，并未改成每个单位精确接触面的连续碰撞；本批不宣称性能提升。模型放大不会自动放大查询半径；龙之箭当前仍是放大外观的细弹射线，需要厚度时通过明确的技能／场景配置调整。

## 反例与验证

[修复前实测](character-design-j28-failure-20261018_221220.log)：生产代码未改时，同一3.333333米线段内Near与Far只命中Near，命中帧TravelledDistance=0，0通过／2失败。原BowSweepProbe的“缺省路径只中最近一靶”断言据此迁移为“两种几何都同帧命中两靶”；39条数量保持，原反例证据保留。

新`_ProjectileQueryProbe`56/0，在真实Godot物理空间中按1/60秒调用被测物理回调，隔离计时相位；预算跨帧用真实PhysicsFrame隔开。弓／龙之箭回归另外覆盖真实按键→动画ATK→引擎自动逐帧飞行→Unit.Damaged，不用手调回调代替整条输入链。

- 两种几何：预算1停点与下帧续飞、多形状去重、穿透0／1、枪口与目标重叠、终止距离累计。
- 同帧前靶→薄墙→后靶、墙内出生、长墙近面、弹射近墙不穿透。
- 命中帧距离触发后续下坠、逐帧追踪保留速度与位移、弹射真实二次命中、穿透只生成一次爆炸及侧靶真实爆炸伤害。
- 四种场景几何配置、能量波0.14半径、视觉放大与判定独立、粗弹半径改值后的偏轴目标命中。

完整验收入口：`& .\_tools\verify_projectile_capsule.ps1 -ProjectileQuery`。结果与存档指纹见[完整输出](character-design-j28-evidence.log)。外部美术／场景／角色显示修改及旧模型删除均保留，不混提交；验证基于当前脏工作区，结束指纹见[工作区记录](character-design-j28-workspace.log)。

双构建均0警告／0错误，弹道56、弓39、灵剑71、蓄力80、原文115、龙之箭42、符文83均0失败；完整8进程退出0、stderr 0字节。Main耗时68.81秒、八条硬指标与J11逐字一致，原5条历史红标保持，不称Main全绿。四存档跑前=跑后（meta 6C29…7EA7、ability_cards 594B…99CA、settings 9FD4…02B4、loadout CD92…5462），rune.cfg仍不存在，selftest.flag已清。没有新的表现修改或截图；验收脚本保留打印既有J12截图指纹。CodeGraph强制索引239文件、7014节点、6775边。
