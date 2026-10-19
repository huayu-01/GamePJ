# 身体、受击与受力约定

2026-10-19用户确认：单位之间需要实体碰撞。快乐小子以奔跑接触轻推普通敌人代替穿透；其他机制审查先搁置，完成本轮后优先画面和模型设计。后续有飞行单位、压低高度、打落及受力表现需求。

## 当前分层

Layer表示自己属于什么类别，Mask表示主动检测什么类别；同一个单位可以同时属于身体层和阵营受击层。敌人无需加入地形层。

| 对象 | Layer | Mask |
|---|---|---|
| 地形 | World（第1层） | 按场景设置 |
| 玩家／友方召唤物身体 | UnitBody（第10层）＋PlayerUnit（第2层） | World＋UnitBody |
| 敌人／精英／首领身体 | UnitBody＋EnemyUnit（第3层） | World＋UnitBody |
| 玩家攻击盒 | PlayerHitbox（第4层） | EnemyUnit |
| 敌人攻击盒 | EnemyHitbox（第5层） | PlayerUnit |
| 玩家子弹 | Projectile（第6层） | World＋EnemyUnit |
| 敌人子弹 | Projectile | World＋PlayerUnit |
| 镜头、落点及地面查询 | 各自用途 | 只查World |

Unit._Ready统一配置身体层和Mask，AttackHitbox与Projectile按攻击来源统一配置。死亡清空单位Layer。已有“闪”的显式穿单位例外仍只保留World查询，结束／打断／换装恢复原Mask；普通移动与剑突受身体阻挡。子弹的敌对目标与地形是两个查询位，并非放在同一层。

快乐小子没有额外伤害或无敌，不再改碰撞层／虚化：奔跑真实接触普通敌人时走Unit.ReceiveKnockback轻推，同一敌人有独立间隔，不推动首领。10秒无实际受伤后奔跑每秒恢复1生命；护甲／护盾受伤也重置等待，治疗尊重禁疗。推速5m/s、间隔0.5秒为表内可调预设。

## 飞行与受力（J34）

- 阵营与地面／飞行移动方式分开。飞行单位保留身体和阵营受击层，是否与其他身体接触由真实三维位置和碰撞形状决定，不把飞行层当作伤害免疫。
- UnitPhysicsProfile独立配置CanFly、Mass、ControlImmune、CanBeKnockedDown和悬停／升力／恢复参数。单位表PhysicsProfile有值时覆盖Movement上的场景回退；当前生产敌种没有自动改成飞行，新增敌种可直接引用Config/Physics/flight_standard.tres。
- ReceiveKnockback是已有战斗冲量入口，现在按质量与抗性计算，默认质量1保留地面水平手感。Movement.AddImpulse仍表示旧的速度增量，ApplyPhysicalImpulse才是除以质量的冲量；SetForce／RemoveForce提供按来源叠加的持续环境力。ReceiveForce走无敌、控制免疫、击退乘区和抗性，不制造伤害或命中事件。
- Flying根据World探地平滑追踪脚底悬停高度，单位身体不当作地面。找不到地面时沿用最后参考地面，初始回退到生成高度。地形、单位和天花板依旧参与真实MoveAndSlide。
- ReceiveHeightSuppression临时压低目标高度，多个来源取最低值，各自到期或移除后平滑回升。普通向下冲量只扰动位置，升力随后恢复；ReceiveKnockDown则关闭升力并结束主动动作，经实际碰撞着地进入恢复期，之后再起飞。悬崖中未触地不会凭空恢复。恢复期间主动技能／跳跃／移动锁定，外力仍生效。
- 打落通过能力原有Finish路径收尾，释放速度、无敌和穿单位Mask等所有权；KnockdownLanded提供实际着地速度，FlightStateChanged提供表现切换事实。两者不自动附加伤害、眩晕或主动跳跃事件。后续攻击效果按需求调用这些入口，不把普通击退自动升级为打落。
- 质量、抗性和控制免疫分开。首领不靠名字或Boss标签获得隐式免疫；具体档案显式设置ControlImmune或CanBeKnockedDown。环境力与战斗控制免疫独立。当前为CharacterBody的可控游戏物理，没有实现布娃娃、动态刚体相撞或导航寻路。
- 画面／模型设计时同步确定身体体积、脚底原点、受击高度、悬停高度和落地动作；纯视觉缩放与实际碰撞尺寸分开验收。

验证见[J33记录](character-design-j33.md)与[J34记录](character-design-j34.md)。交互验证场为Scenes/Debug/_FlightPhysicsLab.tscn：方向键移动；1向下冲量、2压低、3打落、4相同力推动轻／重单位、5复位。旧D批的“所有单位可互穿”契约已由用户裁定替换，历史证据保留。
