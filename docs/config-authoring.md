# 当前配置编辑入口

调参从 `Config/ConfigIndex.tres` 的引用进入，行资源放在 `Config/Tables/<表名>/`；一行一个 `.tres`。强类型字段定义以 `Scripts/Config/*.cs` 为准，运行时结果以当前日志及专项验证为准。

新增内容优先按[制作入口](content-authoring.md)接入。2026-10-24起支持组合效果资源、稳定文本键与 `ContentValidator`；修改后可运行统一验证的 `content`／`effects`／`localization` 专项。

## 常用入口

| 要改的内容 | 入口与实际规则 |
|---|---|
| 角色、武器、所属关系 | Characters / Weapons；六武器按 OwnerCharacter 归属，基地与关卡共用 PlayerLoadout |
| 技能、连招、动作事件 | Skills / SkillSeries / SkillAnimations；伤害仍等待真实 ATK 等事件，缺事件不补计时伤害 |
| 增幅、前置、互斥、次数 | Upgrades；处理器及强类型参数资源表达机制，Requires / Excludes / MaxStacks 过滤真实候选 |
| 符文 | Runes；一次携带一枚，不进奖励池，进关施效并满足对应特化前置 |
| 敌人、行为、资源成本 | Enemies / AiProfiles / Config/EnemyPlans；行为压力决定生成成本，Weight 控制抽取概率 |
| 节点价值与波数 | Nodes / ValueTags；价值标签优先，当前高4／中3／低1波 |
| 节点数量与分布 | 每关固定10个，高2／中5／低3；地图必须提供足够候选点，不在关卡表另填数量 |
| 免费挑战额度 | ConfigIndex.FreeChallengeBaseQuota / FreeChallengeMaxQuota；实际值=基础+关序增量，受上限约束；当前1／2／2 |
| 三关敌人、首领强度 | Stages；敌人白名单、预算、倍率、护卫、首领开场技能数 |
| 首领数值词条 | Pressure；与行为型首领技能分池，节点完成后抽取并在首领出生时施效 |
| 召唤数量与存活上限 | SummonAbility.Count 是单次请求数，MaxAlive=0 不限；正数统计本技能生成单位及全场 Summon 标签单位，实时检查剩余名额，死亡／待删除不占名额；无合法落点不计成功 |
| 局外成长 | AbilityCards / Meta；当前玩家入口是能力矩阵，旧存档基础加成保留兼容 |
| 命中、停顿、镜头反馈 | Config/CombatFeedback.tres；表现预算与伤害判定分离 |

## 已移除的假可调项

- StageDefinition.RewardNodeCount、StageDefinition.FreeChallengeQuota，以及 StageController.ActiveNodeCount 的旧 Inspector 入口已移除。它们曾被固定分层／全局额度规则覆盖，不能继续留作调参入口。
- 武器没有弹匣和装填机制；旧 MagazineSize / ReloadTime / AutoReloadDelay 不属于当前字段。
- 波次一次性出场与逐只生成的并发约束需区分；MaxConcurrent 不是整波硬上限。调整难度时应同时看实际波次、成本、可用预算及游离怪数量。

## 增删资源的约定

1. 修改现有行时保留稳定 Id、UID 与引用；不要只依据中文显示名判断身份。
2. 新增索引行时同步 ConfigIndex.tres 的 ext_resource、数组与 load_steps。移除行时反向检查这些位置。
3. 路径未被直接写出不代表资源没用。需检查目录遍历、枚举插值、UID、动画库扫描、测试和回退依赖，见[资源审计](resource-lifecycle-audit.md)。
4. 未知字段不会自动成为功能；新增可调项必须找到实际消费者并验证修改能改变预期结果。
5. ConfigIndex 的资源依赖损坏可能使整份索引加载失败并回退，不能把“还能启动”当作配置正确。
6. 数值说明优先读取同一行或强类型参数，不复制另一份固定值。

## 阅读范围

本页用于当前维护。原始分表设计、阶段编号、旧角色／装填字段、迁移过程和历史统计见[配置设计归档](archive/reference-before-2026-10-21/config-schema.md)，无需作为新人接手前置阅读。

## 实际参数来源与覆盖（2026-10-28复核）

- 当前六武器UsesWeaponComponent=false，武器行Damage=0表示不使用旧枪械开火；真实伤害来自Skills.Params、Player命中盒覆盖及MeleeStrike场景回退。不要调整无消费者的FireInterval或ProjectileSpeed期待武技跟随改变。
- 敌人表MoveSpeed是基础走速；全局/精英/状态在StatSheet乘算，行为SpeedScale再参与意图。冲锋/回旋/俯冲独立速度覆盖不自动吃基础走速倍率。落弹法师KeepDistance驻位在Scenes/Enemies/MeteorMage.tscn，当前11±1米；技能触发距离仍来自EnemyPlans/meteor_mage.tres的16米。
- EnemyActionStep有强类型Action时，由Action.OwnedStepProperties指定的同名字段以Action.Resolve为准，Step只负责它仍拥有的时序/分支参数。例如抛射读取Action.FlightSeconds，不按兼容Step.Speed反推飞行时间。
- UnitConfig的行值覆盖单位场景；表缺失才保留场景默认。攻击组部分0是“不覆盖”；明确导出的逐项参数和不同类型的0语义须逐个查消费者，不能统一理解为零伤害或零冷却。
- 护甲减伤由命中前有甲决定，整击先减伤再依次扣护甲、临时/普通护盾、生命；破甲余伤不重算。精英比例护甲只在出生/显式回满初始化，RefreshStats不补损甲。玩家/普通加甲/基础甲等没有一起改比例。
- 退用奖励使用既有Weight=0资格规则，统一排除随机、GM获取和图鉴。旧十二项死战专属身份仍登记，不从资源文件数推导可用奖励数；内容数量基线与本轮89项有效资源须分开。

六武器评估按距离、单体/群体、持续/爆发和暴露风险分别记录，见[046实施记录](reviews/20261028/balance-046-implementation.md)。木桩DPS不单独决定削弱，也不因后期收割而自动抬高敌方压力。

- 长枪长虹贯日当前LinearShot=true，消费SpearThrowAbility.Lance中的LanceDamage=50、LanceSpeed=40、LanceMaxTargets=2、技能CD10秒；不蓄力、无抛掷回程。旧WeaponThrow命中盒、FlightSeconds和抛物线预览字段不决定该直射分支。本轮只同步评估/采样入口，没有修改这些现值。
