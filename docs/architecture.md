# 架构说明

J8「按目标标记加伤」的唯一读点放在`AttackInstance.RollHit`：生成`HitInstance`之前问攻击者一次`IOutgoingDamageScale`（`Unit.OutgoingDamageScaleAgainst`），所以"这一击自己刚加的标记"不会算进这一击；规则挂在攻击者身上（增幅是玩家的），敌人／召唤物天然不吃。来源过滤与数值全在表里（`RequiredEventAbility` + 新强类型资源列`BonusAgainstAbility`），不新增`Godot.Get/Set`字符串反射。[标记加伤契约](character-design-j8.md)。

J6蓄力松手为一次性提交：基类冻结输入状态，能力保存释放结果，真实释放动画事件才起动投射物。变奏的强类型参数资源引用目标技能，不新增字符串反射赋值；真实ATK后进入可中断的逐发连射过程。表现层继续只读状态。[蓄力释放契约](character-design-j6.md)。

J5防御以`HitInstance.Negated`标记单次命中，不修改单位级无敌来代替架势；伤害入口仍先派发命中事实再执行无敌／单次无效化／普通格挡。真实Kill事件携带攻击技能Id，表中的`RewardConfig.RequiredEventAbility`资源引用编译为运行期来源限制。闪复用来源持有的三维速度覆盖和球体扫掠；收招保留施法期间已发生的冷却修正，不丢失闪击杀／招架事实。见[闪架契约](character-design-j5.md)。

J4三维技能位移由`CharacterMovement`按来源持有速度覆盖，技能结束只能释放自己的覆盖；下落的伤害倍率、World地面求解与真实落地事件归能力层。`FallAttackTuning`为技能行的强类型子资源，武器仅以资源引用覆盖共通技能的瞄准方式，不新增重复Id或隐藏技能行。`CharacterDisplay`只读下冲方向对齐长枪，临时解除骨骼父变换并成对恢复；表现变换不参与命中和落地计算。见[下落契约](character-design-j4.md)。

原则来源：`策划文档.txt` §2.3 分层、`执行文档.txt` §四。**执行文档阶段 1~7 已全部完成**：`Scenes/Main.tscn` 是一条从头打到尾的 Vertical Slice（战斗节点 → 三选一 → 精英节点 → Boss → 胜负结算与重开）。

## 模块总览

运行期磁盘资源与场景切换统一经`Core/RuntimeResources`：存活托管包装器用弱引用复用，失效时用`IgnoreDeep`创建独立资源图，避免Godot 4.7.2缓存复活GC竞态。不得另加无限强引用池或禁用GC；详见[生命周期依据与验收](resource-lifetime.md)。编辑器生成器不适用这一运行期入口。

| 层 | 内容 | 位置 |
|---|---|---|
| 战斗核心 | Unit / AttackInstance / HitInstance / HitTracker / AttackHitbox / Projectile / Explosion / Targeting / BlockComponent / EliteModifier | `Scripts/Combat/` |
| 单位能力 | CharacterMovement（重力、跳跃、位移技能、外力、朝向） | `Scripts/Combat/CharacterMovement.cs` |
| 数值与状态 | StatSheet + StatModifier、StatusEffect / StatusPresets | `Scripts/Combat/` |
| 事件 | GameplayEvent / GameplayEventBus（订阅方必须成对退订：`_Ready` += / `_ExitTree` -=） | `Scripts/Combat/GameplayEvent.cs` |
| 能力 | Ability 基类 + 近战/远程/范围(可自爆)/跳跃/冲锋/位移/火力倾泻 | `Scripts/Abilities/` |
| AI | EnemyBrain（Controller）+ Behavior 基类 + 7 种行为 + 避障 | `Scripts/AI/` |
| 交互 | Interactable（地面节点基类）/ RewardNode / BossDoor / PlayerInteractInput | `Scripts/Interaction/` |
| 关卡 | EnemySpawnEntry + EncounterController（Spawn Budget / Boss 出场 / 完成与失败判定）、StageController（节点序列与胜负） | `Scripts/Encounter/`、`Scripts/Run/` |
| Boss | BossPhaseController（阶段与技能解锁）、BossPressure（随时间加压）、BossPerkCatalog（Boss 构筑词条） | `Scripts/Boss/` |
| 召唤物 | 复用 Unit + Brain + Ability；`summon_units` 单独分组 | `Scenes/Summons/` |
| 成长 | RewardCatalog / RewardPool / RunBuild / TriggerRule / RunController | `Scripts/Rewards/` |
| 武器 | Weapon（攻击节奏、多发射击、投射物生成；⚠️ **P6b 起没有弹匣/装填概念**） | `Scripts/Weapons/` |
| 玩家 | PlayerController、PlayerWeaponInput、PlayerAbilityInput | `Scripts/Player/` |
| 界面 | OverlayScreen（弹层基类）+ UiRouter（路由）+ BaseScreen / StartScreen / ChooseScreen / RewardScreen | `Scripts/UI/` |
| 表现 | UnitPresentation、PlayerHud（生命条/准星/节点/BOSS/构筑）、**ChargeIndicator（准星蓄力指示器，P6b）**、**ScopeOverlay（开镜特效，P6c）**、**AfterimageTrail（后撤残影，P6c）**、**ProjectileTrail（剑气拖尾，P6c）**、**LandingShockwave（落圈特效，P6c）**、WorldOverlay（伤害跳字 + 敌人血条，**血条是事件驱动**：订阅 `GameplayEventBus` 的 `Damaged`/`Died` 增删，不每帧遍历单位组）、OverlayScreen/UiRouter（弹层基类与路由） | `Scripts/Combat/`、`Scripts/UI/` |
| 镜头 | CameraRig（第三人称跟随 + 碰撞 + 构图偏移 TargetScreenHeight） | `Scripts/Camera/` |

战斗细节见 [combat.md](combat.md)，成长系统见 [rewards.md](rewards.md)，Boss 见 [boss.md](boss.md)，流程与结算见 [stages.md](stages.md)，MCP 验证经验见 [testing.md](testing.md)。

## ⚠ Godot 生命周期与节点类型约定（踩过五次）

1. **子节点 `_Ready` 早于父节点**：组件不能在 `_Ready` 里读父节点在 `_Ready` 中才赋的值。
   - `Unit.Movement` → Ability/Behavior 用惰性属性读取。
   - `Unit` 的基础值（MaxHealth/StartingArmor/MaxShield）必须在 `_EnterTree` 捕获，否则被精英组件的 `RefreshStats` 提前改写。
2. **兄弟节点的 `_Ready` 顺序按子节点顺序**：`RunController` 早于 `Stage`，所以不能在 `_Ready` 里按小组找 `Stage` —— 首次查找失败后必须在首帧惰性绑定。
3. **Godot 只沿“直接父节点”解析 3D 变换**：把 `Area3D`(命中体) / `Node3D`(武器) 挂在普通 `Node` 下，它们会掉到世界原点。
   - 因此 `Ability` 必须是 `Node3D`（命中体、武器、Muzzle 都是它的子节点）。
4. **反复切换 `Area3D.monitoring` 会让静止在范围内的单位不进重叠列表**：命中体常开监测，只切换 `_active`。
5. **传送 `CharacterBody3D` 需要 `MoveAndSlide` 跑一帧**才会同步物理体（见 testing.md）。
6. **C# 方法返回值必须 Variant 兼容**（float/int/bool/string/Node…）：返回自定义 struct 的方法无法从 GDScript 调用；泛型方法 `GetNodeOrNull<T>` 也不能被脚本调用，脚本侧用 `get_node`。
7. **纯显示的 HUD 元素必须 `mouse_filter = Ignore(2)`**：`ColorRect` 这类 Control 默认是 `Stop`，会吃掉鼠标事件。准星正好在屏幕正中心，而鼠标捕获时事件坐标恒为中心 —— 结果 `_UnhandledInput` 再也收不到鼠标移动，**镜头完全不能转**（踩过一次）。
   视角这种玩家输入因此改走 `CameraRig._Input`（在 GUI 之前派发），任何 UI 都抢不走；`MouseMotionEvents` 计数可用于排查。
9. **生成预算要处理「余额买不起任何敌人」**：剩 1 点预算而所有敌人成本 ≥2 时，会既不刷怪又不满足「预算归零」的完成条件 —— 死锁。`EncounterController.RetireUnaffordableBudget()` 每帧检查：余额低于当前可生成条目的最低成本就作废余额。
10. **一个 .cs 里只能有一个能被场景挂载的类，且类名必须等于文件名**：`SupportAbilities.cs` 里放了 `BuffAlliesAbility` 与 `SummonAbility` 两个类，结果两个脚本挂载**静默失败**（节点变成裸 Node3D，属性读不到），Boss 第三阶段召唤与技能精英光环一直没生效。用代码 `new` 出来的类（如 TriggerRule 子类）不受影响。
8. **`StatSheet` 里被当作除数/乘法基数的键必须有基础值**：缺省读到 0，`0.7 / max(0.1, 0)` 会把装填时间变成 7 秒 —— 调大 `ReloadTime` 之类的数值完全不生效。新增属性键时在 `Unit.SeedStats` 补默认值。
11. **物理帧率 ≠ 渲染帧率（残影/剧烈缓动的根源，踩过一次）**：物理固定 60Hz，而本机渲染 120fps。
    - 没开 `physics/common/physics_interpolation` 时，角色的**渲染变换**只在物理帧上更新：实测 301 帧里有 **150 帧位移为 0**（49.8%），其余帧位移 0.114（等于 2 帧的路程）。画面上就是「一顿一冲」，也就是用户说的"剧烈缓动 + 残影"。
    - **凡是每帧改 `Node3D` 变换的跟随逻辑，必须写在 `_PhysicsProcess` 里**，让引擎把物理状态插值到渲染帧。写在 `_Process` 里会和被跟随对象错拍：实测相机单帧位移在 `0 ↔ 0.1167` 之间交替（σ=0.057），角色/相机相对位置每帧抖 ±4cm。
    - 排查手段：`SelfTest` 的表现采样阶段会逐帧记录「物理位置位移 / 插值后位移 / 相机位移 / 角色↔相机相位差」并打日志；`Node3D.GetGlobalTransformInterpolated()` 拿到的才是渲染器真正使用的变换（`GlobalPosition` 始终是物理值，开不开插值都一样）。
12. **数组型表参数一律用 `Godot.Collections.Array<T>` 列，不许"整串文本 + 运行期解析"、不许"按行 Id 约定路径 load"**（P8 立的规矩，实测与理由见 [abilities.md](abilities.md) §8.2 / §11）：
    - `[Export] Godot.Collections.Array<string|float|Vector3|T:Resource>`：**能编译、能导出、能写盘、能读回**（P8 探针：项数/每一项非空/值逐位都对）。属性表里带元素类型（`hintString="4/0:"` = `Array[String]`）。
    - `[Export] float[]` / `string[]` / `Vector3[]` 也**能编译**；**只有 `PackedFloat32Array` / `PackedStringArray` 没有 C# 类型**（`CS0246`）——别用。
    - ⚠️ **"`Count` 读得到、每一项都是 `null`"= 编辑器加载了旧 C# 程序集**（新导出属性在旧程序集里根本不存在）。**重启编辑器**，不要据此推翻设计。
    - 一张行需要"每项一份"的一串数据时：**要么加一个 `Array<T>` 列**，**要么老老实实加一行子表 + 一个资源引用列**（例如 `AbilityConfig.ComboSeries` → `Config/Tables/SkillSeries/ser_*.tres`）。**表里的东西要一眼看得懂**（用户本轮裁定：「该改代码的时候还是得改」，别为了"不改代码"把数据做脆）。
    - **禁止**：`ability.Get("属性名")` / `Set("属性名", 值)` 这种**字符串反射设值**（属性名写错不报错，只是静默不生效）。要写强类型属性，就用类型判断（`ability is MeleeComboAbility combo`）。

## 碰撞层与查询（第二轮 D 批）

`project.godot` 的层号从1开始；`CombatLayers` 常量与 `.tscn` 中的数值是位值。
实现位于 `Scripts/Combat/CombatTypes.cs`，不是历史交接里误写的 `CombatLayers.cs`。
`layer` 表示自身类别，`mask` 表示检测对象；单位身体的mask与战斗查询的mask分别配置。

| 层号 | 编辑器名 | 位值 | 当前用途与mask |
|---|---|---:|---|
| 1 | World | 1 | 地面、墙、坡；单位移动、镜头遮挡、AI避障、刷怪/节点落点只查此层 |
| 2 | PlayerUnit | 2 | 玩家及玩家阵营召唤物身体；mask=1 |
| 3 | EnemyUnit | 4 | 普通敌人、精英、Boss身体；mask=1 |
| 4 | PlayerHitbox | 8 | 玩家/友方攻击Area；mask=4 |
| 5 | EnemyHitbox | 16 | 敌方攻击Area；mask=2 |
| 6 | Projectile | 32 | 四种弹体Area根的类别；mask=5（玩家弹）或3（敌方弹） |
| 7 | Interactable | 64 | 预留；当前交互物是Node3D+距离判定，不人为添加物理体 |
| 8 | Pickup | 128 | 预留；当前拾取用距离判定，不人为添加物理体 |
| 9 | VfxOnly | 256 | 仅预留名字；生产纯特效不创建CollisionObject3D，不分配此层 |

- 所有 `Unit` 的CharacterBody3D只撞World，同阵营和敌阵营均可穿过；不加软推挤，不依赖身体挡住来凑近战距离。
- `AttackHitbox` 在Ready按祖先Unit预配置，在Activate按真实来源刷新；常开Monitoring，仅切换激活态。
  独立生成的Explosion也由Detonate的来源配置。保留IsHostileTo与HitTracker两道结算校验。
- `Projectile` 默认每物理帧单次射线；`SweepMaxHits>0` 时改用整段胶囊 `IntersectShape`，按弹道投影排序，World 用同半径球的最早阻挡距离裁定，预算用尽停点续扫。`SweepRadius` 默认0.08世界米，可在弹体场景调节。
  `ConfigIndex.tres.ProjectileMaxSpeed` 是全局实际弹速上限，当前200m/s；Launch、弹射与追踪/重力后均夹取速度向量。技能发射快照保留原值，`Projectile.ActualSpeed` 读实际速度，详见 [J12](character-design-j12.md)。
  Area根不设形状、Monitoring/Monitorable均关闭，避免重复重叠结算。
  Launch统一配置阵营mask，`HitMask`兼容旧导出列；命中起点在体内也有效，已命中的RID加入排除列表，防止重复消耗穿透次数。
  弹射与追踪均按敌对阵营索敌，斩弹仍用既有场景名单与运行期弹体列表。
- `KillZone` 是Area，layer=0、mask=6，只检测单位身体；死亡Unit的layer归零。
- 近战身体完全重叠、闪/剑突穿单位、地形阻挡、双阵营四种弹体与全部敌人AI必须用真实物理帧验收。
  复现及证据见 [d-batch-verification.md](d-batch-verification.md)。

## Unit 与表现的分工

**J19共通地爆**：FallAttackAbility真实Land阶段复用实际形状／变换查询敌人、容量填满扩容、单位去重；Unit.ReceiveKnockback提供纯击退入口，不追加Hit／Damage事件，抗性由移动组件处理。参数通过SkillEnhancement资源配置，默认0、换装清理，三配装见 [J19](character-design-j19.md)。用户新增的技能动画移动许可与灵动授权为下一批；平衡重测暂后置。

**J18浮光跃金**：CharacterMovement将额外跳次数作为地面瞬移高度来源，AirJumpsLeft对外为0；TestMove以真实身体裁定向上World净空，然后写入位置、清垂直上升速度并ResetPhysicsInterpolation。地面旧状态用离地锁避免重复瞬移，真实落地后恢复，PlayerLoadout清理模式。默认路径不变，见 [J18](character-design-j18.md)。

**J17金光大道**：复用近战单段模式，指定原连招index2；SkillEnhancementTuning.ForwardReachScale只缩放盒形判定Z及其前伸中心，宽高与其他技能保持，换装清理。只该刀伤害乘1.5，替换旧全局Damage／Range修饰，真实武技2隔离见 [J17](character-design-j17.md)。

**J16气贯长虹**：WeaponRecallTuning从原技能行确定性缩放三项蓄力时间，SpearThrowAbility换装恢复召回策略；真实落地自动回手沿用WeaponThrowAbility既有状态机。长枪高频预览、投掷射线及AttackHitbox扫掠查询参数及时Dispose，避免大量临时资源积压到退出终结；首轮退出失败和复验边界见 [J16](character-design-j16.md)，不等同于引擎终结器修复。

**J15喜爱**：ScytheThrowAbility在真实离手后按游戏秒周期，以拥有者位置选最近敌对单位；WeaponThrowAbility提供受保护的重定向飞行段，复用World、射程、周期伤害与回手状态机。默认无重定向不改变旧路径，回程停止索敌，换装清理。参数使用WeaponSeekTuning资源引用，见 [J15](character-design-j15.md)。

**J14末刀进化**：近战单段模式沿用原连招表的动画／几何／真实ATK，选择与伤害倍率只作用目标能力；AttackInstance冻结ComboStep／ComboStepCount，Unit.Die将其带入Kill事件。TablePulse按来源单位／技能／冻结末段过滤普渡，不读玩家当前连招段号，旧攻击延迟结算不会串段。见 [J14](character-design-j14.md)。

**J13灵剑进化**：`MeleeProjectileTuning` 配置每段真实ATK额外投射物，继承当段伤害快照；`SkillEnhancementTuning` 只改指定拔刀式／落剑式，不写全局伤害乘区。拔刀式模型放大与胶囊半径分开配置；落剑式复制独立球体、表现只读真实半径。换装还原增强配置及形状。真链路与预设边界见 [J13](character-design-j13.md)。

**原始设计J3**：`Ability`在新请求验证成功后交接前台动作；不可打断例外与动画前摇锁定由强类型技能列配置。`UnitAnimator.SkillEvent`是单位实例事件，能力以播放序号过滤；全局旧事件只留作历史自测统计。回调换段后立即停止旧事件游标。输入按真实事件顺序派发；后台效果、离手武器和落剑式被动圈不参与前台交接。

**原始设计J2**：投掷技能的尚未回手状态与动作占用分离，回程全程锁武技1；`ThrownWeaponDisplay`只读投掷位置／方向并实例化当前武器同源模型。高速投掷Box使用两帧凸包扫掠，与Area共用HitTracker。退出先撤销音乐淡出Tween，再释放声部并等待混音回收；完整自测的局外收益／消费仅保存在本进程内，不写玩家存档。

**原始设计J1**：玩家朝向在武器输入分流前统一更新；`CharacterMovement`按来源持有朝向锁，后撤负责成对获取／释放。落剑式属于灵剑武器技能列表，角色共通列表仅保留闪／架／落。一次性爆炸视觉脱离短寿命攻击节点后，用原生Timer信号回收，不持有会被深度资源重载取消的GDScript等待协程。

**后续F专项**：导航只采`nav_geometry`组内递归子节点的World静态碰撞，网格单元尺寸服从NavigationMap；烘焙计时器退树解绑，重建只管理自有区域。
正常退出统一走`GameExit.Request`，先卸载音频流并异步等待混音回收，再退出SceneTree；UI/GDScript可调用`UiRouter.RequestQuit(exitCode)`。不新增autoload。
项目默认字体与界面Theme共用自带FontVariation，禁系统回退；字重以TextServer原生坐标验收。

**第二轮 C 批补充**：PlayerLoadout是基地/关卡共用的装备应用器，不创建关卡上下文。
角色交互通过表内OwnerCharacter归属校验，LoadoutMemory只负责偏好文件，未新增autoload。
CharacterDisplay只切换表现模型/握持场景；UnitAnimator把真实动画驱动绑定当前可见模型，骨骼路径只改私有副本。
基地练习生命周期独立，不生成RunController/StageController或结算。接入见 [base-loadout.md](base-loadout.md)。

**第二轮 B 批补充**：`AbilityHud` 仅消费技能只读量、当前配装身份和表内表现列，
不会通过界面改CD、层数或伤害；HUD CanvasLayer=3，高于可选像素滤镜层1，低于弹窗。
GM是独立开发工具预制体，使用公开接口主动调用系统；不作为autoload，也不留下InputMap绑定。
主动切场景通过 `UiRouter.ResetForSceneChange()` 清空旧界面引用，防止模态页面卸载后残留暂停。
详见 [gm-console.md](gm-console.md)。

**第二轮 E 批补充**：技能的 `IsActive` 与占动作的 `IsBusy` 分离，默认配置仍等价；
后台维持类不参与角色动画驱动，收尾不会停止别的技能动画。状态表现由 `StatusEffectFx` 只读状态，
用 `EffectConfig` 的强类型形态列与 `Texture2D` 资源引用配置，不按状态 Id 写分支，不进入碰撞或单位列表。
从单位子节点 `_Ready` 添加兄弟组件须延后执行；详见 [abilities.md](abilities.md) 与 [vfx.md](vfx.md)。

**第二轮 A 批补充**：`DiveWindFx` 只读 `FallAttackAbility.IsDiving` / `Unit.Velocity`，
只创建 `MeshInstance3D`，不创建单位、物理体或分组。玩家血条的 `Vitals` 为内嵌富文本；
ESC 菜单回调由 `SettingsScreen` 注册并在退树时解除，帮助由 `HelpSettingsSection` 注册为第四页。
能力页以最多 6×3 分页、空位留白；基地与关卡均挂相同预制体。

`Unit` 只产生 Gameplay 事实（受伤、死亡、破甲）并抛 C# 事件；`UnitPresentation` 订阅事件做闪色/销毁，不参与结算。
`CharacterMovement` 订阅 `Unit.KnockbackReceived` 施加外力，因此 Unit 不需要知道移动组件的存在。

## 已实现模块

### CameraRig（`Scripts/Camera/CameraRig.cs` + `Scenes/Camera/CameraRig.tscn`）

- 结构：`CameraRig(yaw)` → `SpringArm3D(pitch、碰撞)` → `Camera3D`。执行文档 6.2 的独立 CameraRig 方案。
- 节点设为 `TopLevel`，不继承目标节点旋转：镜头朝向只由鼠标决定，目标转身不会带动镜头。位置由脚本按 `TargetOffset` 跟随。
- **跟随写在 `_PhysicsProcess`**（不是 `_Process`）：和物理同频后由物理插值补到渲染帧，镜头与角色共享同一对物理状态、相对位置恒定；写在 `_Process` 会与角色的 60Hz 步进错拍，产生持续抖动（详见上面第 11 条）。
- 对外只暴露两个查询：`FlatForward` / `FlatRight`，单位据此计算移动与瞄准方向；单位不直接读鼠标。
- 参数入口（Inspector）：`MouseSensitivity`、`Min/MaxPitchDegrees`、`PitchDegrees`、`TargetOffset`、`FollowSharpness`、`CaptureMouseOnStart`。
- 镜头遮挡：`SpringArm3D` 自身负责，并在 `_Ready` 中排除目标本体，避免镜头顶到角色自身碰撞体。

### PlayerController（`Scripts/Player/PlayerController.cs` + `Scenes/Player/Player.tscn`）

- `CharacterBody3D`，只负责“移动与跳跃”这一层能力；朝向输入来自 CameraRig。
- 参数入口：`MoveSpeed`、`SprintSpeed`、`Ground/AirAcceleration`、`JumpVelocity`、`ExtraAirJumps`、`GravityScale`、`TurnSharpness`。
- `ExtraAirJumps` 是“灵动/额外跳跃”类增益的接入点：改这个值即可获得二段跳，不需要动跳跃逻辑。
- 身体朝向跟随移动方向（执行文档 6.4 待试玩确认是否改为跟随镜头）。

## 预制体（Unity Prefab 式对象）

运行时不再用代码拼装节点，固定结构一律做成可实例化的场景：

| 预制体 | 用途 |
|---|---|
| `Scenes/Camera/CameraRig.tscn` | 镜头，可挂到任意单位上 |
| `Scenes/Player/Player.tscn` | 玩家：CameraRig + Pistol + 战术滑铲 + 火力倾泻 + 技能输入 |
| `Scenes/Weapons/Pistol.tscn` | 双手单点手枪 |
| `Scenes/Combat/Bullet.tscn` | 投射物 |
| `Scenes/Enemies/Chaser.tscn` | 近战追踪敌人（Brain：近战攻击 + 追击） |
| `Scenes/Enemies/RangedEnemy.tscn` | 远程敌人（Brain：射击 + 保持距离 + 撤退） |
| `Scenes/Training/TrainingDummy.tscn` | **纯挨打木桩**（不动、不还手、抗击退、带甲） |
| `Scenes/Training/TrainingAttacker.tscn` | **只攻击不移动的木桩**（验证近战/无敌/击退） |
| `Scenes/Training/TrainingShooter.tscn` | **只远程不移动的木桩**（验证投射物与远程行为） |
| `Scenes/TestLab.tscn` | 测试场：玩家 + 三种木桩，用于自动化验证 |
| `Scenes/Levels/TestArena.tscn` | 测试场地，含光照与天空 |
| `Scenes/UI/Hud.tscn` | 生命/护甲/护盾（⚠️ **P6b 删了弹药显示**）+ 准星 + 准星蓄力指示器 + 开镜特效 |
| `Scenes/Combat/SwordQi.tscn` | 剑气本体（`Projectile` + `Blade`/`Tail` + **拖尾 `Trail`**，P6c） |
| `Scenes/Main.tscn` | 可玩入口：场地 + 玩家 + 2 近战 + 1 远程 |

实例覆盖（在父场景里改子场景属性，不改预制体本身）是调参首选方式。
**注意**：运行时传送 `CharacterBody3D` 需要让 `MoveAndSlide` 跑一帧才能同步物理体（见 testing.md）。

## 扩展方式

- **新敌人**：复制一个敌人预制体，换模型/数值，用已有的 Behavior + Ability 组合即可；只有真正的新机制才需要新 Behavior/Ability。
- **新单位（召唤物/Boss）**：同样用 Unit + Movement(+Brain/Ability) 组合，复用死亡与伤害链路。
- **新武器/技能**：Ability 挂在单位下，通过参数与 Tag 组合；不要在 Unit 里按武器 ID 分支。
- **数值增强**：给 `Unit.Stats` 加 `StatModifier`，再 `RefreshStats()`；不要在具体脚本里写特例。
- **新镜头行为**：扩 CameraRig 的导出参数，不要在玩家脚本里直接改 Camera3D。

## 已知限制与后续专项

- 阶段1~7及第二轮A/E/B/C/D的当前状态以HANDOFF顶部与逐批证据为准，不再把历史快照当待办。
- 角色数值曲线、整体终版美术和自带中文字体仍需专项推进；占位资源允许继续替换。
- 导航仍有空网格与cell_height不一致告警，局部避障不能覆盖全部凹形地形；D批未改变导航算法。
- 破甲环八边形、旧P4探针期望偏差、退出资源告警及偶发GCHandle异常见各批验收记录。

## 单例与初始化顺序（踩过坑，必须遵守）

项目里有 8 个全局入口，**顺序错了会出现"读到 null 但不报错"的隐蔽问题**（我们为此丢过好几轮）：

| 顺序 | 名称 | 类型 | 说明 |
|---|---|---|---|
| 1 | `ConfigIndex` | autoload | 所有数值的唯一入口，谁都可以最先读 |
| 2 | `GameLog` | autoload | 日志，进场景前就可用（`_Ready` 里不要依赖任何场景节点） |
| 3 | `UiRouter` | autoload | 界面栈 → 决定暂停与鼠标模式 |
| 4 | `UiService` | autoload | 界面服务：按类型查界面，**界面本体在场景里**，所以只做惰性查找 |
| 5 | `SelfTest` | autoload（Testing） | 只在 `--selftest` 或 `user://selftest.flag` 时激活，否则 `SetProcess(false)` |
| — | `MetaProgress` | 静态类 | 首次访问时读 `user://meta.cfg`；`AddShards` 一定落盘 |
| — | `RunSetup` | 静态类 | 基地 → 关卡的出征配置（跨场景传递） |
| — | `MapManager.Instance` | 场景节点 | 导航烘焙 + 点位供给；**烘焙是延时的**（等几何体进树） |
| — | `StageController.Instance` / `RunController.Instance` | 场景节点 | 关卡流程 / 本局流程的编排者 |

**规则**：

1. **场景节点类的 `_Ready` 里不要假设别的场景节点已经就绪** —— Godot 的子节点 `_Ready` 早于父节点，兄弟节点的顺序也不保证。用到的引用要么惰性获取，要么像 `RunController` 那样**等首帧再绑**。
2. **autoload 之间可以互相读，但不要在 `_Ready` 里读场景**（那时场景还没进树）。需要场景内容就延迟到第一次使用。
3. **`MapManager` 的烘焙是异步时序的**（延时 + 同步烘焙），任何要在烘焙结果上做事的地方都必须检查 `IsReady` 并准备兜底路径。
4. **加新的全局入口前先问：能不能传参？** 项目里已经有 8 个，每多一个就多一处顺序耦合。
