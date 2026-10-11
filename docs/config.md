# 数值配置（调参唯一入口）

**所有可调数值拆成 12 张表，全部挂在 [`Config/Balance.tres`](../Config/Balance.tres) 上；运行期由 autoload `Balance` 读取并建索引。**

> **2026-10 配置表阶段 2（重要变化）**：`Balance` 从"唯一数值**文件**"变成"唯一数值**门面 + 表索引**"。
> 业务代码**仍然写 `Balance.Settings.Xxx`**（这条约定没变），但数值的实际家在 `Config/Tables/<分组>/` 里，
> **一行一个 `.tres` 文件**。字段定义见 `Scripts/Config/*.cs`，缺字段/缺表都会回退到代码默认值。
>
> **2026-10 配置表阶段 3（单位表 + 技能表）**：新增 **单位表基类 `UnitConfig`**（攻击/生命/移速/持有技能/行为&类/表现）
> 与 **技能表 `AbilityConfig`**；敌人 9 个敌种 + 首领 + 召唤物各成一行；白名单从"按中文显示名"改成"按 Id"。
> 细节与实测见文末「配置表阶段 3」一节。

## 调参去哪张表

| 要调什么 | 打开哪个目录 | 一行是什么 | 行数 |
|---|---|---|---|
| **敌人（按敌种）**：生命/护甲/移速/攻击四项/标签/持有技能/刷怪成本与权重 | `Config/Tables/Enemies/enemy_*.tres` | 一个敌种 | **10**（1 行全局倍率 + 9 个敌种） |
| **首领**：生命/护甲/移速/攻击四项/持有技能/白板副本血量 | `Config/Tables/Bosses/boss_final.tres` | 一个首领 | 1 |
| **召唤物**：数值 + 存活时间 | `Config/Tables/Summons/summon_drone.tres` | 一个召唤物 | 1 |
| **技能**：冷却/前摇/后摇/朝向/开关/接近区间 + 子类专属参数 + 命中体参数 | `Config/Tables/Skills/skl_*.tres` | 一个技能 | **25**（5 玩家 + 16 单位 + 4 首领行为词条） |
| **首领数值强化池（Pressure）** | `Config/Tables/Pressure/pmod_*.tres` | 一条数值强化 | 3 |
| 玩家基础数值与攻击组 | `Config/Tables/Characters/chr_*.tres` | 一个角色 | 3 |
| 武器数值（伤害/射速/弹匣/装填/散布/**击退**/暴击/弹速） | `Config/Tables/Weapons/wpn_*.tres` | 一把武器 | 3 |
| 效果参数（燃烧 dps / 减速 / 眩晕 / 爆炸 / 吸血 / 召唤上限 / 连锁闪电） | `Config/Tables/Effects/eff_*.tres` | 一个效果 | 9 |
| 奖励节点（标题 / 层 / 波数 / 商店价目） | `Config/Tables/Nodes/node_*.tres` | 一种节点 | 9 |
| 增幅项（19 条强化·进化·异化） | `Config/Tables/Upgrades/*.tres` | 一条增幅 | 19 |
| 关卡（预算 / 波次 / 敌人白名单 / Boss 词条数 / Pressure 规则） | `Config/Tables/Stages/stg_*.tres` | 一关 | 3 |
| 局外成长（碎片价格 / 每级效果） | `Config/Tables/Meta/meta_*.tres` | 一项 | 5 |
| **全局开关**（敌人/Boss 倍率、节奏、Boss 压力曲线、伤害跳字、血条、节点间距、区域杀高度） | **`Config/Balance.tres`**（还在原地） | — | 28 个字段 |

- **改完要重新运行游戏才生效**（数值在开局一次性应用，不热重载）。
- 两种改法都行：Godot 编辑器里双击 `.tres` 用 Inspector 改（有中文分组），或直接用文本编辑器改。
- 字段的中文注释在对应的行类里：`Scripts/Config/{UnitConfig,EnemyConfig,BossConfig,SummonConfig,AbilityConfig,PressureModConfig,CharacterConfig,WeaponConfig,EffectConfig,NodeConfig,RewardConfig,MetaUpgradeConfig,StageDefinition}.cs`。
- **单位表是继承关系**：`EnemyConfig` / `BossConfig` / `SummonConfig` / `CharacterConfig` 都继承 `UnitConfig`，
  所以"生命 / 攻击四项 / 移速 / 持有技能 / 标签 / 表现"这六组列在所有单位表里长得一样，只有各自的私有列不同。
  **攻击四项是整个单位表通用的一组**（伤害/间隔/范围/击退，套到该单位的**主攻击**上）：
  `AttackDamage` / `AttackInterval`（→ 能力 `Cooldown`）/ `AttackRange`（按能力子类依次试
  `AttackRange`→`TriggerRadius`→`MaxRange`→`MaxDistance`）/ `AttackKnockback`。**留 0 = 不覆盖**。

> ⚠️ **操作纪律（血泪教训）**：不要单独删/改 `Balance.tres` 引用到的行文件。
> 只要引用的**任何一个**行文件缺失/改名，Godot 会**整份 `Balance.tres` 加载失败**（不是只丢那一行），
> 于是**所有**数值静默回到代码默认值（实测：玩家生命 250 → 140、武器变成 22/0.32/8/1.15）。
> 要下线一行，就把 `Balance.tres` 里的数组项与顶部对应的 `[ext_resource]` 一起删。
> 详见 [config-schema.md](config-schema.md) §8.3。

## 各表参数（当前取值）

### 角色表 `Config/Tables/Characters/`（3 行）

| 行 | 生命 | 护甲 | 移速 | 减伤 | 武器 | 能力开关 | **攻击组**（伤害/间隔/范围/击退） |
|---|---|---|---|---|---|---|---|
| `chr_gunner`（槽 1） | 250 | 40 | 7.0 | 0 | ✅ | 关 Melee/Summon/Control | 32 / 0.75 / 2.4 / 5 |
| `chr_heavy`（槽 2） | 380 | 140 | 6.2 | 12% | ❌ | 开 Melee，关 Summon/Control | 32 / 0.75 / 2.4 / 5 |
| `chr_summoner`（槽 3） | 210 | 60 | 6.8 | 0 | ✅ | 开 Summon/Control，关 Melee | 32 / 0.75 / 2.4 / 5 |

角色表**继承 `UnitConfig`**，所以"攻击组"就是**左键近战重击的四个数**（原先散在 `Balance.tres` 的
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
| `Config/Tables/Bosses/boss_final.tres` | 1 | 生命 900 / 护甲 200 / 移速 3.6 / 攻击 24·1.8·3.4·6 / 标签 Boss,Melee；**持有技能 4 行**（`skl_boss_melee` / `skl_boss_scream` / `skl_boss_charge` / `skl_boss_summon`）；`EliteCopyHealthScale = 0.35`（白板首领副本的血量倍率） |
| `Config/Tables/Summons/summon_drone.tres` | 1 | 生命 40 / 移速 6.0 / 攻击 6·0.9·1.8·1.5 / 标签 Summon / `LifetimeSeconds = 25` |
| `Config/Tables/Skills/skl_*.tres` | 21 | 技能：`NodeName`（目标能力节点）+ 通用项（Aim/Cooldown/Windup/Recover/Enabled/Approach*）+ `Params`（子类专属，Key = 属性名）+ `AttackParams`（命中体/武器组件） |
| `Config/Tables/Skills/bhv_*.tres` | 4 | **首领行为词条**（Boss Build）：`ApplyHandler` 非空的技能行，由 `BossPerkCatalog` 编译成运行期词条 |
| `Config/Tables/Pressure/pmod_*.tres` | 3 | **首领数值强化池**（Pressure）：狂暴 / 铁壁 / 疾行，内容是 `StatOverride` 数值操作 |

**⚠️ 首领强化的两套来源必须能分开（关卡核心文档 §7.1）**：
行为/技能型在**技能表**里（`ApplyHandler` 非空的那 4 行），数值型在 **Pressure 表**里（3 行）。
两张表分别编译、各自独立回退（一侧读不到不会让另一侧变空），开局日志会分开打条数：
`首领强化来源：数值型 Pressure 3 条（来源：表 Pressure）/ 行为型 Boss Build 4 条（来源：表 Skills）`。
开场词条的抽取目前仍是"两表合起来抽"（顺序 = 数值型在前 + 行为型在后，与分表前的 7 条**逐位一致**，
所以随机序列没变）；`BossPerkCatalog.PressureOnly` / `BehaviorOnly` 已经让两套来源**可独立追踪**，
将来 Pressure 转换池接线时按来源各抽各的即可。**没有**为 Pressure 设计"可被玩家增幅修改"的接口（文档明令禁止）。

**技能表的两个约定**（避免两个真源）：
1. **单位的"主攻击"四个数以单位行为准**（`ApplyAttackTo` 在技能行之后套用）。
   所以 `skl_melee` / `skl_enemy_*_melee` 这类**主攻击的技能行把 `Cooldown` 留空（0 = 不覆盖）**，
   间隔由单位行的 `AttackInterval` 给。
2. **命中体/武器组件的参数写在技能行的 `AttackParams` 里**（`Damage` / `Knockback` / `StatusId` /
   `StatusDuration` / `AttackTags` / `FireInterval` / 弹速 / 弹丸预制体…）；只有**主攻击的伤害与击退**
   由单位行的攻击组统一给。这条规则让"改文案/改标签"与"改数值"各归其位。

### 武器表 `Config/Tables/Weapons/`（3 行）

| 行 | 伤害 | 间隔 | 弹匣 | 装填 | 散布 | **击退** | 穿透 | 连发 |
|---|---|---|---|---|---|---|---|---|
| `wpn_pistol`（槽 1） | 24 | 0.26 | 12 | 0.7 | 0.5 | **2.5** | 0 | ❌ |
| `wpn_sniper`（槽 2） | 60 | 0.95 | 5 | 1.4 | 0.2 | **4.0** | 2 | ❌ |
| `wpn_rifle`（槽 3） | 13 | 0.12 | 30 | 1.2 | 1.6 | **1.2** | 0 | ✅ |

三把武器**共用同一个 `Pistol.tscn` 节点**，只换数值（`RunController.EquipWeapon` → `WeaponConfig.ApplyTo`），
所以不会重建节点、不会弄丢挂在武器下的输入组件。

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

### 节点表 `Config/Tables/Nodes/`（9 行）

| 行 | 节点类型 | 层 | 波数 | 奖励过滤 | 界面标题 |
|---|---|---|---|---|---|
| `node_weapon` | Weapon | 1 | 2 | 进化 | 武器池 |
| `node_enhance` | Enhancement | 1 | 2 | 强化 | 强化池 |
| `node_evolution` | Evolution | 2 | 3 | 进化 | 进化池 |
| `node_mutation` | Mutation | 2 | 3 | 异化 | 异化池 |
| `node_resource` | Resource | 0 | 1 | 不过滤 | 局外资源 |
| `node_heal` | Heal | 0 | 1 | 不过滤 | 恢复状态 |
| `node_special` | Special | 0 | 1 | 不过滤 | 特殊奖励 |
| `node_shop` | Shop | 1 | 2 | 不过滤 | 补给站 |
| `node_portal` | Portal | 0 | 1 | 不过滤 | 降临节点（不触发战斗） |

`node_shop` 还带三项价目：**恢复 5 / 强化 12 / 进化 18 碎片**（花局外碎片买局内收益）。
碎片不足时不会扣款、也不会产生收益，但**依然会触发本场 Encounter**（节点 = 一次收益 + 一次挑战）。

### 增幅项表 `Config/Tables/Upgrades/`（19 行）

10 条强化 + 4 条进化 + 5 条异化，数值与分表前**逐条一致**。
每条行的 `ApplyHandler` 决定用哪个 C# 处理器（`Scripts/Rewards/RewardEffectRegistry.cs`）：

| handler | 条数 | 说明 |
|---|---|---|
| `StatSet` | 14 | 纯数据：改 `StatSheet` 的某项（数值在 `Modifiers` 里） |
| `ChainLightning` / `Lifesteal` / `BurnOnHit` / `SummonOnKill` / `AmmoOnKill` | 各 1 | 需要一行 C#（加新机制仍要写一行代码，见 [config-schema.md](config-schema.md) §6.1） |

> ⚠️ `Weight` 目前 19 条**都写成 1.0** → 加权抽取仍退化成等概率（与分表前一致，**故意不改**，改了抽样分布会变）。
> 现在**改得动了**：想调某条的出率就改它的 `Weight` 一行。

### 局外成长表 `Config/Tables/Meta/`（5 行）

| 行 | 存档键 | 每级效果 | 价格 |
|---|---|---|---|
| `meta_health` | `health_level` | 最大生命 +10 | 10 × (等级+1) |
| `meta_damage` | `damage_level` | 伤害 +5% | 同上 |
| `meta_speed` | `speed_level` | 移速 +3% | 同上 |
| `meta_crit` | `crit_level` | 暴击率 +3% | 同上 |
| `meta_reload` | `reload_level` | 装填速度 +6% | 同上 |

> ⚠️ **存档兼容红线**：存档键名（`SaveKey`）必须保持 `health_level` 等原值，改了老玩家进度会归零。

## 还留在 `Balance.tres` 的全局字段

### 敌人 · 全局倍率（`Config/Tables/Enemies/enemy_global.tres` 是**优先来源**，这里是回退值）
| 参数 | 含义 |
|---|---|
| `EnemyHealthMultiplier` | 所有普通敌人生命倍率（**回退值 = 0.75，与表一致**） |
| `EnemyDamageMultiplier` | **所有普通敌人伤害倍率（难度总开关）**（**回退值 = 0.3，与表一致**） |
| `EnemyMoveSpeedMultiplier` | 所有普通敌人移速倍率（1） |
| `EliteChanceMultiplier` | 精英出现概率倍率（1 = 按各节点的配置，0 = 不出精英） |

> 敌人表的 `enemy_global` 行里同名四个值是**优先来源**，读不到才用上面这四个。
> 🔧 **阶段 3 修的静默换挡**：这两个回退值原来是 `1f` / `0.8f`，而表里是 `0.75` / `0.3` ——
> 表一读不到，敌人伤害会**静默 ×2.67 且没有任何日志**。现在两处对齐，并且 `Balance.ScaleEnemy`
> 第一次套用时会在日志里写一行「敌人倍率：…（来源：表 enemy_global / 代码默认值 BalanceConfig）」。

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
[Config] 配置表：武器 3 / 角色 3 / 敌人 1 / 效果 10 / 节点 9 / 局外成长 5（Balance.tres 已加载）
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
[Config] 配置表：武器 3 / 角色 3 / 敌人 10 / 首领 1 / 召唤物 1 / 技能 25 / Pressure 3 / 效果 9 / 节点 9 / 局外成长 5（Balance.tres 已加载）
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
| 日志留痕 | `配置表：… 敌人 0 / 首领 0 / 召唤物 0 / 技能 0 / Pressure 0 …（Balance.tres 已加载）`、`首领强化来源：数值型 Pressure 3 条（来源：代码兜底）/ 行为型 Boss Build 4 条（来源：代码兜底）`、9 条「关卡白名单里的 Id 在敌人表里找不到」的警告 |
| **不再静默换挡** | `敌人倍率：普通 生命×0.75 伤害×0.3（来源：代码默认值 BalanceConfig（表读不到））` —— 与表在时**同值** |
| 白名单 | 读不到敌人行 → **不筛**（8 个生成项全在，回退路径仍能玩），而不是"整关没怪" |
| 敌人 | 用 `Scenes/Enemies/*.tscn` 的值（实测 JumpingMelee 生命 38 = 场景 50 × 0.75），技能行的 Id 变回类名 |
| 白板首领 | `血量倍率 0.35（来源：场景导出值）` |
| 玩法可用性 | 自测全程通过、端到端按 F 仍打开奖励界面 |

> ⚠️ 一个值得记住的边界：**清空 `Skills` 索引数组不会让"某个单位行的持有技能"失效** ——
> 技能引用写在**单位行文件**里（`chr_*.tres` / `enemy_*.tres` 的 `ext_resource`），
> 数组只是"给 `ConfigRows` 建索引、给日志报数"用的。要真正回退到场景值，得清空**单位表**那一侧的数组。

## 按键映射（当前）

| 操作 | 按键 | 定义位置 |
|---|---|---|
| 移动 | W/A/S/D + 方向键 | `project.godot` → `[input]` 的 `move_*` |
| 跳跃 | 空格 | `jump` |
| 射击 / 预瞄 | 鼠标左键 / 右键 | `fire` / `aim` |
| 战术滑铲 | **Q** | `dash` |
| 火力倾泻 | E | `burst` |
| 帮助界面 | H | `HelpScreen.ToggleKey`（导出属性，非 InputMap） |
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

## 开局配置与武器池

- 开局先弹 **Configure 画面**（`Scenes/UI/StartScreen.tscn`）：按 1 选手枪 / 按 2 选狙击枪 / 按 3 选步枪 → 进入 Stage 1。
  卡片文案来自武器表的 `DescriptionTemplate`（支持 `{Damage:0}` 这类占位符）。
- **武器池节点**：互动后弹出**三选一**，选定即触发本场 Encounter。用的是通用选择界面 `ChooseScreen`，以后商店/事件节点可直接复用。

## 节点自身的数值

每个节点的预算、并发上限、生成间隔写在 `Scenes/Main.tscn` 的 `Stage/Node1`、`Node2`、`Node3Boss` 上（Inspector 里可直接改）。
每个节点的敌人种类与权重**现在来自敌人表**（`Config/Tables/Enemies/enemy_*.tres` 的
`ScenePath` / `Cost` / `Weight` / `MinElapsedSeconds` / `EliteChance`）—— 场景里的 `SpawnXxx` 子节点
仍然在（提供预制体引用与位置），但每次开战前都会由 `EncounterController.ApplyEntryFilter` **按表覆盖**；
读不到行时才用场景里的值（见上面的「敌人表」一节）。
**节点的"类型 → 层 / 波数 / 标题 / 奖励过滤"在 `Config/Tables/Nodes/node_*.tres` 里**（不在场景里）。

> **成本与预算的整除关系**：节点结束时余额若低于任何可用敌人的成本，会被当作「已花完」直接结算（不会卡住）。所以节点预算最好是成本的整数倍，否则最后会有一点余额被浪费。

## 代码里不写死数值

新增可调项时：
1. 属于某张表的一行 → 在对应行类上加 `[Export]`，在建一个 `.tres` 行文件，再加进 `Balance.tres` 的索引数组；
2. 是跨表全局开关 → 在 `BalanceConfig` 里加一个 `[Export]` → 在 `Balance.tres` 里给出默认值；
3. 套用它的地方读 `Balance.Settings` 或 `ConfigRows.ById<T>(...)`。

`EncounterController` 生成敌人后调用 `Balance.ScaleEnemy(unit, this, isBoss)` 套用倍率（优先读 `enemy_global` 行），
`RunController.ApplyBalance()` 套用角色/武器数值（读角色表与武器表）。

## 血条（用户需求，2026-01）

| 项目 | 规则 | 配置 |
|---|---|---|
| 普通怪物血条 | 放在**脚下**（沿 -Up 偏移），满血不显示 | `EnemyHealthBarTopOffset = 0.25`（现在按脚下偏移使用） |
| 触发方式 | **事件驱动**：订阅 `GameplayEventBus.Raised`，收到 `Damaged` 才 O(1) 登记该单位并建血条，`Died` 立即移除；**不再每帧遍历** `enemy_units` / `boss_units` 组 | — |
| 首领血条 | **不再画头顶血条**，改为屏幕**上方**的主血条面板（`WorldOverlay` 按 `HasTag("Boss")` 跳过） | — |
| 近距离 | 与首领距离 ≤ `BossBarFullRange` 时展开完整血条（名称 + 血量 + 关卡） | `BossBarFullRange = 22` |
| 远距离 | 只保留首领头像色块 | 同上 |

代码位置：`Scripts/UI/WorldOverlay.cs`（怪物血条）、`Scripts/UI/PlayerHud.cs`（首领面板，含验证用只读属性 `BossBarExpanded` / `BossDistance`）。

**验证状态**：
- 首领面板：**已通过** —— 无首领时 `BossBarExpanded=False`；首领出场近距离（20.7 米）`True`、面板与名称条都可见；走远到 48.5 米（>22）自动收起为 `False`、只留头像；再靠近又展开为 `True` ✓
  （踩过的坑：`UpdateBossPanel()` 一开始被插在 `PlayerHud._Process` 的**某一个状态分支末尾**，Boss 战走的是另一个分支，所以从不执行；移到所有分支之前才生效。）
- 怪物血条改到脚下：代码已改（`WorldOverlay` 用 `- Vector3.Up * offset`），**仍缺视觉确认**，建议你游玩时看一眼。
- 血条事件驱动：**机制已通过**（2026-10 自测实测日志）——满血敌人不建血条（`满血HP=60 血条数=0`）；扣 5 点（60 → 55）后 `Damaged` 事件立刻建出血条（`登记 Chaser HP=55/60 现有血条=1`），此后逐帧采样 121 帧期间**只登记一次、没有重复建条、单位存活且 HP 实时为 55**；致死 `Died` 事件立刻移除（`移除 ... 剩余血条=0`）。首领按 `HasTag("Boss")` 跳过（`跳过 Boss ... 候选=False`）。
  注意：若一次伤害直接致死，`Damaged` 与 `Died` 在同一调用栈内先后触发，血条会「建好又立刻拆掉」——本作实测几乎每次伤害都是即死，所以用 0.5 秒级轮询 `Bars` 子节点数常常看到 0，这是正确行为，不是丢事件。

## 关卡安全（用户需求，2026-01）

| 项目 | 规则 | 配置 |
|---|---|---|
| 地图下方区域杀 | **所有单位**（玩家/敌人/Boss/召唤物）进入区域即击杀 | `KillPlaneY = -8`（Balance.tres）→ 区域中心取 `KillPlaneY - 30`，由 `MapManager.EnsureKillZone()` 建 `KillZone`（Area3D，进入即杀）；旧的 `KillPlane` 每帧遍历实现已删除 |
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

## Pressure 结算（关卡核心文档 L352/L370）

> **2026-10 更新（配置表阶段 1 + 2）**：Pressure 的**基础点数 / 阈值 / 增长倍率 / 额外比例**现在优先读**当前 Stage 行**
> （`Config/Tables/Stages/stg_*.tres` 的 `PressureGrowthMultiplier` / `PressureThreshold` /
> `PressurePerNodeNormal` / `PressurePerNodeHighValue` / `PressureEvolutionBonus` / `PressureMutationBonus`）。
> **行里留 0（= 未设置）就回退到下表的全局默认值** —— 当前三行都没填，所以行为与分表前完全一致。
> 组合顺序固定为：`基础点数 × 本关增长倍率 × 挑战类型额外比例`。
> ⚠️ 硬约束：这里只管**数值型**强化（Boss Pressure）；行为/技能型走 `BossPerkCatalog`（Boss Build），两者不得合成一个池（关卡核心文档 §7.1）。

| 参数 | 含义 |
|---|---|
| `PressurePerHighValueNode` | 完成一次**高价值**节点（进化/异化）挑战增加的 Pressure 点数（150）—— 行里没填时的全局默认值 |
| `PressurePerPoint` | 每满多少点转成 1 点首领压力（100）—— 行里没填时的全局默认值 |

规则：**挑战完成后**才累积（不是随时间涨）；只有高价值节点计数；每满 100 转 1 点，**余数清零**（不满 100 的部分丢弃）。

累积结果进入 `OpenBossDoor()` 的初始压力：

```csharp
_bossEncounter.BossInitialPressure = Mathf.Max(0, ChallengesUsed - FreeChallengeQuota) + _bossPressureBonus;
```

即「超额挑战次数」+「节点累积压力」共同决定 Boss 初始压力。

实测（自测直接调 `AddPressureForNode`，2026-10 复核）：

```
Pressure 累积：+187.5（基础 150 × 本关倍率 1 × 类型比例 1.25）阈值 100 → 余数 0 / 本关首领额外压力 1
Pressure 累积：+225（基础 150 × 本关倍率 1 × 类型比例 1.5）阈值 100 → 余数 0 / 本关首领额外压力 3
再加一次中价值（强化）→ 压力仍为 3（只有高价值计数 ✓）
```

注：两次 150 得到 2 点而不是 3 点，是因为文档要求「余数清零」——每次转换后不足 100 的 50 点被丢弃，实现与文档一致。

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

## 白板首领副本（关卡核心文档 L231/L315/L317）

每个 Encounter 的**最终阶段**都要打一个「白板首领副本」——文档里叫"第 8 波"，且明确要求**不能与常规波数共用参数**；同时提醒它**不是** Boss Door 里的最终首领。

| 项目 | 实现 |
|---|---|
| 触发时机 | 常规波全部清空后（`CheckCompleted()` 里，早于"结算完成"） |
| 独立参数 | `EncounterController.EliteBossScene`（**独立于** `BossScene`/`IsBossNode`） |
| 弱化 | `boss_final.tres` 的 `EliteCopyHealthScale = 0.35`（白板首领血量只有普通首领的 35%；读不到行才用场景导出值） |
| 不带 Boss 构筑 | 不设置 `BossPerkCount`/`BossInitialPressure`，所以无词条、无压力 |
| 完成条件 | 白板首领**死亡后**才算完成（它在 `_spawned` 里，残留会阻止完成判定） |
| 复位 | `ResetEncounter()` 复位 `_eliteBossSpawned` |

实测：

```
常规波已清空 → 进入白板首领副本
白板首领降临（血量倍率 0.35，无压力 / 无词条）
死亡 Boss(0)
Encounter 清场完成：共生成 4，用时 3s      ← 首领死后才结算
交互 #1：接受=True ... encDone=True
```

**踩坑**：第一版我把这段分支写进了 `if (BossScene != null)` 里，而普通 Encounter 的 `BossScene` 是 null（只有 BossFight 才有），所以永不执行；而且它还在 `IsCompleted = true` 之后，即使执行也拦不住结算。正确位置是 `CheckCompleted()` 开头、`AliveCount > 0` 判断之后、`IsRunning = false` 之前。

### 进化 / 异化对 Pressure 的额外比例（关卡核心文档 L356）

| 参数 | 含义 |
|---|---|
| `PressureEvolutionBonus` | 进化奖励的 Pressure 比例（1.25 = +25%）—— 行里没填时的全局默认值 |
| `PressureMutationBonus` | 异化奖励的 Pressure 比例（1.5 = +50%）—— 行里没填时的全局默认值 |

`AddPressureForNode()` 按奖励类型乘比例后再走「每 100 转 1 点、余数清零」。
