# 角色/武器/增幅 重构需求（权威记录）

> 用户的设计文档：**`F:\Godot项目\3d肉鸽雨中哈迪斯\角色设计.txt`**（243 行）—— 那是**唯一权威**。
> 本文件记录**用户对设计文档的口头澄清与决策**，两者冲突时**以本文件（更新的澄清）为准**。
> 记录时间：2026-10-12

---

## 一、术语与架构澄清

### 1.1 限定词的层级（⚠️ 容易理解错）

| 标签 | 含义 | 谁能拿到 |
|---|---|---|
| **`<A限定>`** | **限定角色 A，不限其武器** | **A 的 3 把武器（A1/A2/A3）都能拿到** |
| **`<A1限定>`** | 限定角色 A **+ 武器 1** | 只有 A 用武器 1 时 |
| **`<B限定>` / `<B1限定>` …** | 同理 | 同理 |

**用户原话**：
> 「武器1的进化池其实**只有 `<A1限定>` 里的选项**，**`<A限定>` 是这个角色的**，也就是**武器2和3也可以拿到**；反正就是 `<A限定>` 就是**限定角色A但不限定其武器**，`<B限定>` 也是」

### 1.2 池的本质 = 哈迪斯的祝福清单

**用户原话**：
> 「`<A限定>` 它**也是进化池的，不需要额外列**，这个池它**就像哈迪斯里那些祝福选项一样**，都是**有概率被刷新在玩家的选择祝福列表上的**；**选 A1 进关卡的时候能选到，选 A2 也能，选 A3 也能**」

- **"进化池"包含两类**：**角色限定（`<A>`/`<B>`）+ 武器限定（`<A1>`/`<A2>`/`<A3>`…）**
- **候选集 = 按"当前角色 + 当前武器"过滤**：拿 A1 → 候选取 `<A>` + `<A1>`；拿 A2 → `<A>` + `<A2>`
- **实现上"每把武器的池里都列一遍"或"角色池 + 武器池合并"都可行**（用户明确说两种都行）
- ⚠️ **后期的图鉴**：**从"角色"维度显示该角色所有能选到的选项**，**武器限定的选项上标注「需持有：xx」**

### 1.3 术语 vs 标签组 —— 本质是"逻辑判断"

**用户原话**：
> 「之前是使用的**标签组**，后面也是需要**把标签组换成这个了或把术语变成标签**，术语实际只是描述，**本质是要做好逻辑判断，确保不冲突**」
> 「（比如**角色A拿了角色B限定的增幅，那么不会产生任何效果，这并不正确**，又比如**拿了弹射又再拿穿透，那么逻辑上又会变得奇怪**，**有的互斥也可能是为了不太强而考虑的**）」

**由此确定的正确行为**：

| 术语 | 正确行为 | ❌ 错误行为 |
|---|---|---|
| **限定** | **过滤候选列表** —— 不满足条件的增幅**根本不该出现**在列表里 | A 拿了 B 限定的增幅、"没有任何效果" |
| **互斥** | **真的拦住**（不能同时持有） | 拿了"弹射"又拿"穿透" |
| **前置** | 没满足就不出现在列表里 | — |

⚠️ **互斥有两个来源**：
1. **逻辑冲突**（弹射 ↔ 穿透：子弹行为互斥）
2. **平衡限制**（"为了不太强" —— 例如"专一"↔"花心"）

### 1.4 动画：所有技能都有动画 + 严格要求

**用户原话**：
> 「其实**所有技能都有动画**，只是我没写入，因为基本会跟一开始写的重复」
> 「模型没有动画，但是你提议的后者有一个问题，**文档里有加速动画释放的效果，这可能使得动画和逻辑帧对不齐**，所以**严格要求有相应动画才会执行相应技能**，后面有条件了再去找一些带有 attack 动画的模型将它改成 Skill 动画即可」

**决策**：
- **没有对应动画 → 该技能/武技不执行**（**不做"时间驱动兜底"**）
- **"加速动画释放"的实现 = 改 `AnimationPlayer` 的 `speed_scale`** —— 这样**逻辑帧自动跟着动画走，天然对齐**
- **落地路径**（用户给的）：「模型普查如果没找到满足的，**可以复制里面一些动画然后改名 + 加帧**」
- ⚠️ 文档只写了几处动画（`Skill_1` / `ATK` / `Dodge`→`Dodger` / `Action`），**其余技能动画由开发时补全**（要产出一份完整的"技能 → 动画"映射）

### 1.5 维护性（硬要求）

**用户原话**：
> 「**不止**（角色），但目前只先写了 2 个，并且**武器也不一定限制只有 3 把，永远要考虑维护性**」

- **加一个新角色 / 新武器 / 新技能 / 新增幅 → 不能改代码**（只能加表行 / 加资源）
- **选角色/选武器界面要能容纳任意数量**（不能写死 2 个或 3 个）
- ⚠️ **代码里不许写死角色名/武器名/技能名**

### 1.6 `n` 的含义

**用户原话**：
> 「`n` 是因为**我不知道具体填什么**，比如施加力、范围参数，我**脑补不出具体参数的具象表现**，很多设计的技能招式**它们的参数人类是要实际开发了才知道有哪些**」

**做法**：**先留位置（表列 + 类型）+ 填一个"能跑起来的最小值" + 开发过程中把实际需要的参数补出来**（**文档里没写的参数，开发时发现也要加**）。

### 1.7 被动与 CD

**用户原话**：
> 「**没写明按键的基本是被动**，只是有些被动他**是有触发间隔的**，比如落剑式的 cd 是 5，那么当玩家连续使用下落攻击时，哪怕是每秒一次，也**只会是在第一次和 5 秒后的那一次下落攻击触发这个技能**」

- **没写按键 = 被动**；被动**可以有 CD**（= 触发间隔，标准 CD 语义）

---

## 二、决策汇总（Q1–Q9）

| # | 问题 | 用户决策 |
|---|---|---|
| **Q1** | 模型没有 `Skill_*`/`ATK` 动画怎么办 | **严格要求有动画才执行**（不做时间驱动兜底）；**可以从现有动画复制、改名、加帧** |
| **Q2** | 只有 A/B 两个角色吗 | **不止**，目前只先写 2 个；武器也不限 3 把；**永远考虑维护性** |
| **Q3** | 旧角色清到什么程度 | **表和代码都清** |
| **Q4** | `n` 占位值怎么填 | 先留位置 + 填最小可跑值，**参数在实际开发中补** |
| **Q5** | 过渡期能否不可玩 | **一次性重构**（允许中间不可玩，但每步必须能编译） |
| **Q6** | 武技3 是纯被动吗 | **是**（没写按键 = 被动；被动可以有 CD） |
| **Q7** | `<A限定>` 在池里怎么列 | **它也是进化池的，不需要额外列**；行为 = 按角色+武器过滤候选（同哈迪斯祝福）；**两种实现都可行**；图鉴从角色维度显示、武器限定标「需持有」 |
| **Q8** | `AbilityCards`（8 张能力卡）保留吗 | **保留** |
| **Q9** | 旧 3 角色清掉后选角色流程 | **先的先上，后面肯定会再加**（界面要能容纳任意数量） |

---

## 三、实施约束

1. **一次性重构**：清旧 + 建新一次做完（允许中间玩法不可玩）
2. **每步必须能编译**：`build.ps1 --no-incremental` **0 warning / 0 error**
3. **参数全进表**：包括文档里填 `n` 的、以及文档没写的（开发中发现就补）
4. **不许硬编码**：⚠️ 用户点名要审计「**可以配置但是被写成硬编码的变量**」（因为大量代码由子代理写）
5. **维护性优先**：加角色/武器/技能/增幅 = **只加表行，不改代码**
6. **动画驱动**：效果在**动画事件帧**触发；"加速释放"用 `speed_scale`；**没动画不执行**
7. ⚠️ **清旧时要小心共用**：`UnitConfig` / `AbilityConfig` / `Skills` 是**敌人、首领、召唤物与玩家共用**的 —— `Skills/` 那 25 行里 **4 行是首领行为词条（`bhv_*`）、16 行是敌人技能**，**不是"旧角色的"，不能一起清**
8. ⚠️ **`ConfigIndex.tres` 是所有表的持有者**（17 个数组指向 130 个行文件）—— 动它要格外小心（项目栽过"改它导致整份加载失败 → 全数值静默回退"）

---

## 四、实施阶段（初步，P0 调研后再细化）

| 阶段 | 内容 | 并行性 |
|---|---|---|
| **P0** | **调研**：动画普查 + 硬编码审计 + 战斗管线盘点 + 差距对照 + 清除清单 + 工作量估算 | **单个，只读**（产出 `docs/refactor-survey.md`） |
| **P1** | **数据层**：清旧表 + 建新表（2 角色 / 6 武器 / Moves / Passives / ~60 条增幅 + 限定·互斥·前置） | **必须独占**（改全部配置表 + `ConfigIndex.tres`） |
| **P2** | **机制层**：蓄力·完美时机 · 技能层数 · 滞空 · 奔跑/空中跳跃 · 子弹行为 · 标记 · 格挡 · 下落攻击 | 可拆 2 个 |
| **P3** | **角色 A**（3 把远程武器 + 技能 + 被动） | 1 个 |
| **P4** | **角色 B**（3 把近战武器 + 技能 + 被动，含"武技1 统一驱动"架构） | 1 个 |
| **P5** | **增幅接入 + 池重构**（限定/互斥/前置三层判定 + 图鉴数据基础） | 1 个 |
| **P6** | **表现**：准星蓄力指示器 · 路径可视化 · 滞空光环 · 各类特效 | 1 个 |

---

## 五、能力卡与"弹匣"的裁定（用户 2026-10-12 答复）

| # | 问题 | 用户裁定 |
|---|---|---|
| **Q10-a** | `card_control` / `card_summon`（两张 `GrantSkill` 卡，指向将被清的 `skl_control`/`skl_summon`） | **删** |
| **Q10-b** | `meta_reload`（局外成长：装填速度） | **下线** |
| **Q10-c** | `card_magazine`（弹匣容量）/ `card_reload`（装填速度） | **下线** —— 用户原话：**「没有弹匣的概念了」** |

### ⚠️ 由此确定的"横向清理清单"（**P6 清旧阶段执行**）

**"没有弹匣的概念"不只是下线两张卡，凡是与弹匣/装填相关的都要清**：

| 层面 | 要清的东西（待 P6 逐项确认） |
|---|---|
| **配置表** | `WeaponConfig` 的弹匣/装填字段（如 `MagazineSize` / `ReloadSeconds` / `ReloadSpeedMultiplier`）；`ConfigIndex` 里相关标量；`card_magazine` / `card_reload` / `meta_reload` 三个行文件 + 索引 |
| **代码** | `Weapon` 的装填逻辑、弹药计数、开火前的弹匣判定 |
| **UI** | **HUD 的弹药显示**（现在右下角是 `15 / 15` 那种） |
| **自测** | `配置表比对·武器` 里的「弹匣 12 / 装填 0.7」等断言 |
| **文档** | `docs/config.md` / `docs/combat.md` / `docs/rewards.md` 等提到弹匣装填的地方 |

⚠️ **注意**：本清单**只在 P6 执行**。P1（动画管线 + 最小闭环）**只加不删**，旧武器与旧卡暂时保留作参照。
**✅ P6a 已执行**（2026-10-15）—— 见下面 §六；**✅ P6b 已把"弹匣/装填"这一条横向清理做完**（2026-10-16）——
见下面 §七。

---

## 六、P6a 清旧记录（2026-10-15 执行完毕）

> "只加不删"是 **P1→P5 的阶段纪律**；**P6 起解禁**。本批把攒下的旧内容一次清完，并收尾三个遗留问题。
> 提交：`feat(P6a): 清旧（角色/武器/技能/增幅/池/能力卡）+ 基地 UI 改造 + 三个遗留问题`

### 6.1 删掉了什么（每个都先 grep 确认"没有别的引用"）

| 类别 | 数量 | 具体 |
|---|---|---|
| 旧角色 | 3 | `chr_gunner` / `chr_heavy` / `chr_summoner` |
| 旧武器 | 3 | `wpn_pistol` / `wpn_sniper` / `wpn_rifle` |
| 旧技能行 | 5 | `skl_dash` / `skl_burst` / `skl_melee` / `skl_summon` / `skl_control` |
| 旧增幅行 | 17 | 强化 10 + 进化 4（`evo_twin` / `evo_ap` / `evo_ricochet` / `evo_blast`）+ 异化 3（`mut_lifesteal` / `mut_summon` / `mut_supply`） |
| 旧升级池 | 3 | `pool_pistol` / `pool_sniper` / `pool_rifle`（旧武器删掉后**无人引用**才删） |
| 能力卡 | 2 | `card_summon` / `card_control`（Q10-a；它们是 `skl_summon` / `skl_control` 的唯一引用者） |
| 能力类 | 2 | `DashAbility` / `BurstFireAbility`（`P0` 判定的"仅旧角色"两个） |
| 场景节点 | 5 | `Player.tscn` 的 AbilityDash / AbilityBurst / AbilityMelee / AbilitySummon / AbilityControl |
| 按键动作 | 4 | `project.godot` 的 `dash` / `burst` / `control` / `summon` |

**没删的（容易误删）**：`Pistol.tscn`（6 把新武器的 `ScenePath` 全指着它）· 7 个共用 `Ability` 子类 ·
敌人/首领/召唤物的数据行与技能行 · `Weapon.cs` · 角色 A/B 与所有新武器/新技能/新增幅/6 个新池。

**`ConfigIndex.tres` 三处同改**（`[ext_resource]` + 数组项 + `load_steps`），每批改完都实跑验证
`（ConfigIndex.tres 已加载）`。索引 **9/5/10/1/1/44/7/13/8/6/9/11/8 → 6/2/10/1/1/39/7/13/8/6/6/11/6**
（差额只有：武器 −3 / 角色 −3 / 技能 −5 / 升级池 −3 / 能力卡 −2；其余**一条没动**）。
增幅项 **74 → 57**（= 强化 9 / 进化 23 / 异化 25）。

### 6.2 ⚠️ 角色 A 的生命 / 护甲 / 移速 **是预设值，不是设计值**

设计文档（`3d肉鸽雨中哈迪斯/角色设计.txt`）**没有**写角色 A［猎手］的生命 / 护甲 / 移速，
现在表里的 **200 / 20 / 6.5 是 P3 代理自拟的预设值**（角色 B 的 220 / 25 / 6.8 同理）。
**等用户后续确认** —— 在那之前不要把它们当成"经过设计的平衡值"，
也不要拿"换了角色所以读数变了"当成平衡结论。自测那一行已经把这个口径写进去了。

### 6.3 连带改动（漏一处就会静默坏掉）

- `ConfigIndexData` 的 `WeaponPresetKind` / `PlayerCharacterKind`：删掉旧成员，剩下 6 个武器 / 2 个角色
  **显式写死数值**（= 表行的 `Slot - 1`）→ **删旧武器没有让其余武器的序号位移**，武器行与升级池行的
  `Slot`（4…9）一个都不用改。
- 选人/选武器界面改成**遍历表行**（不再遍历枚举、也不再写死三把旧枪）→ 以后加一把武器 = 加一行 `.tres`。
- `RunController`：删掉 `BeginConfigure` / `ChooseCharacter` / `DefaultCharacterTitle`（手写角色中文标题
  那张"写死角色名"的映射表）与"没选过就先弹基地面板"那条路；开局一律按 `RunSetup` 直接开。
- `CharacterConfig.WeaponEnabled` → 删；"左键归谁"归到 **`WeaponConfig.UsesWeaponComponent`**（见 §6.4）。
- `RewardConfig.RequiresSummoner` / `RewardDefinition.RequiresSummoner` / `RewardPool.SummonerAllowed` → 删
  （唯一用户 `mut_summon` 已清；角色限定统一走 `RequiredCharacterIds`）。
- `SelfTest` / `_FeelProbe` / `SettingsSections` / `UiIcons` 里引用旧角色的地方全部同步。

### 6.4 用户 2026-10-15 的两条澄清（本批已落地）

1. **「开始探索」按钮删掉** —— 用户原话：「那个本身按了没意义……后来要求的进关卡就是去交互一个对象来进关卡，
   基地里是有做了一个的」。**交互对象已核实能进关卡**：`Base.tscn` 的 `TerminalGate`
   （`BaseTerminal.Kind = Gate`，`TargetScene = res://Scenes/Main.tscn`）→ 按 **F** →
   写 `RunSetup` → `LoadingScreen.BeginLoad` → `RunController` 直接开局。
2. **「出征时按角色行把枪收起来」删掉** —— 用户原话：「我并没有这么设计过……**相当于只是切场景**」。
   归属改成**武器行**（`UsesWeaponComponent`）：6 把新武器都是"武技驱动"（不走 `Weapon` 组件的开火），
   基地预览与关卡用**同一个来源**，所以不再是"出征时另做一步"。
   ⚠️ 顺带修掉一处不一致：原来基地按角色行把枪模型藏起来、关卡里又出现。

### 6.5 「永久强化」旧页 → 能力卡

基地里那页旧的局外强化（`BaseScreen`：碎片 + `Health/Damage/Speed/Crit/Reload` + `Key1~5`）
**整份删除**，局外成长台现在直接开**能力卡界面**（`AbilityMatrixScreen`，K 键同一个界面）。
`meta_quota`（额度扩增）与 `MetaProgress.Shards` 一行没动。

### 6.6 三个遗留问题的结论

| # | 问题 | 结论 |
|---|---|---|
| 1 | `MaxAimDistance` 表/类不一致（3 条 `已跳过` 警告） | **删参数项**（`MeleeComboAbility` 根本没有瞄准射线，补一列就是死配置） |
| 2 | `ArmorBroken` / `ShieldBroken` / `Critical` 全项目无抛点 | **补上了**，都唯一：`Unit.ReceiveHit` 里护甲/护盾归零那两处 + 结算后的暴击那一处。顺手把「能量护盾」的"护甲破碎"那一路接上（`Trigger` 支持逗号分隔 → `Parried,ArmorBroken`） |
| 3 | `VFXControllerBB` 加载失败 | **复现不出来**：`class_name` 存在、全局类缓存里有它、整轮自测 stdout/stderr 一行都没有。属"全局类缓存过期"，已自愈；删缓存可强制重建，**不需要改代码或场景** |

### 6.7 顺手修掉一个被暴露出来的真 bug

`践踏死亡` 原来是 `SetExtraAirJumps(TriggerTally * ExtraAirJumps, …)` —— **覆盖**而不是累加，
会把角色 A 被动「灵动」已经授予的 1 次空中跳跃**吃掉**。旧 3 个角色都没有这个被动，所以一直没显形；
自测基准换成猎手后立刻暴露（`1 → 1`）。改成按**当前值**累加（文档原话：「每次击杀都会获得额外跳跃的次数
（额外跳跃次数最多 N 次）」），自测回到 `1 → 2`。

---

## 七、P6b 记录（2026-10-16 执行）

> 这一轮是 **P1→P6 的最后一批**：弹匣清理 + 补异化 + 表现层 + UI 尾巴 + 两个可选清理。

### 7.1 「没有弹匣的概念了」——清的 5 层（逐层清单）

用户原话（2026-10-12）：`card_control` / `card_summon` **删**（P6a 已删）· `meta_reload` **下线** ·
`card_magazine` / `card_reload` **下线** —— **「没有弹匣的概念了」**。

| 层 | 清了什么 | 落在哪 |
|---|---|---|
| ① 配置表 | 行文件 `card_magazine.tres` / `card_reload.tres` / `meta_reload.tres` **删除**；`ConfigIndex.tres` **三处同改**（3 条 `[ext_resource]` + `MetaUpgrades`/`AbilityCards` 数组项 + `load_steps` 190→187） | `Config/Tables/…`、`Config/ConfigIndex.tres` |
| ② 代码 | `Weapon.cs` 整组装填逻辑（`MagazineSize`/`ReloadTime`/`AutoReloadDelay`/`Ammo`/`IsReloading`/`EffectiveMagazineSize`/`EffectiveReloadTime`/`TryReload`/`RefillMagazine`/`FireFullMagazine`/`BeginInfiniteFire`/`InfiniteAmmo` + `ReloadStarted`/`ReloadCompleted`/`AmmoChanged` 三个事件）；`WeaponConfig` 三列 + `ApplyTo` 的 `RefillMagazine`；`PlayerWeaponInput` 的 reload 键；`MetaProgress` 的 `BonusReloadSpeed`/`_reloadLevel`/`ReloadCost`/`ReloadLevel`/存档键 `reload_level`；`RunController` 的 `ReloadSpeedMultiplier` 修饰符；`Unit.SeedStats` 的两个键；`GameplayEventKind.ReloadStarted/ReloadCompleted`；`RewardCatalog.AmmoOnKillRule` + `RewardEffectRegistry` 的 `AmmoOnKill` handler | `Scripts/…` |
| ③ UI | `PlayerHud` 的弹药显示 + `Hud.tscn` 的 `Root/Ammo` 节点（连只服务它的 `_weapon` 字段与 `FindWeapon`）；`HelpScreen.tscn` 里「装填 / 火力倾泻」两行说明（顺带把已删的 Q 滑铲 / E 火力倾泻也改对了）；`Pistol.tscn` / `RangedEnemy.tscn` / `TrainingShooter.tscn` 三个场景里**已不存在的** `MagazineSize`/`ReloadTime` 属性行 | `Scripts/UI/`、`Scenes/` |
| ④ 自测 | `SelfTest`「配置表比对·武器」去掉「弹匣 N / 装填 X」两项与相关期望串 | `Scripts/Testing/SelfTest.cs` |
| ⑤ 输入 + 文档 | `project.godot` 的 `reload` 动作（确认无引用后删）+ `SettingsSections` 的绑定行 + `UiIcons` 两个卡图标名；本文档 + `upgrades.md`/`config.md`/`ability-cards.md`/`architecture.md`/`rewards.md` 的相关说明 | — |

⚠️ **副作用（必须知道）**：`RangedEnemy` 原来靠 `MagazineSize 3` + `ReloadTime 2.5` 每 3 发停一下；
概念删除后它变成"每 `FireInterval` 2 秒一发、永不装填"。**自测硬指标一位没变**（首领线不受它影响）。

### 7.2 补的两条通用异化（5 条里做了 2 条）

| 行 | id | 新机制 | 落在哪 | 预设值 |
|---|---|---|---|---|
| 杀戮践踏 | `mut_stomp` | 落地那一刻对周围敌人造成范围伤害 | `StompOnLandRule`（`Scripts/Rewards/RewardCatalog.cs`）：**轮询** `CharacterMovement.IsGrounded`/`FallHeight` 做落地边沿，范围伤害与「能量护盾」共用 `PulseDamage.Around` | 半径 3m / 伤害 15 / 最小落高 0（**文档只给了 `n`**） |
| 巡飞弹 | `mut_homing_missile` | 暴击 → 发射追踪飞弹 | `HomingMissileOnCritRule`：给最近的敌人挂**专用信标状态** `MissileBeacon`，再发一枚 `HomingStatusId` = 它的飞弹（**复用 P3 的追踪弹通路**） | 伤害 50 来自文档；弹速 60 / 索敌 18m / 存活 4s / 信标 1.5s / 转向 360°/s 是预设 |

**没做的 3 条**：护卫（要"奔跑态"，全项目还没有 sprint 输入/状态）、灵魂收割（要掉落物拾取系统）、
落日剑（命中施加延迟落剑，机制量最大）。逐条理由见 [upgrades.md](upgrades.md) §7。

### 7.3 表现层：准星处的蓄力指示器（用户描述最具体的那一条）

`Scripts/UI/ChargeIndicator.cs` + `Scenes/UI/Hud.tscn` 的 `Root/ChargeIndicator` 节点。
正方形四条边、每个角留缺口，缺口随 `Ability.ChargeProgress` 收到 0 = "闭合"；
闭合 / 完美时机（`Ability.IsPerfectWindow`）时换成完美色 + 罩一圈光晕 + **把准星本体也染色**
（`Crosshair` 的 `Modulate`）—— 正是文档第 44 行「闭合时准星会有个变色」。
**只读运行期状态，一行战斗逻辑都不改**。几何量（半径/线宽/缺口/三个颜色）全是导出属性。
截图：`docs/screenshots/p6b_charge_open_zoom.png` / `p6b_charge_closing_zoom.png` / `p6b_charge_perfect_zoom.png`。

### 7.4 UI 两个尾巴（**顺带查出并修掉两个真缺陷**）

1. **额度进度条看不出已占用部分**：给 `QuotaBar` 配了 `StyleBoxFlat`（fill 白底会被运行期改成三档色 /
   background 深底 + 圆角 + 边框），并按占用比例三档变色（正常蓝 / ≥0.8 黄 / 满红）。
   颜色与阈值是**场景导出值**。截图：`p6b_quota_bar_warn.png` / `p6b_quota_bar_full.png`。
2. **自适应列数**（用户要求"确认列数跟着变、卡牌比例不变"）—— **实测它没在工作**，两个真缺陷：
   - **A**：`RightPanel` 也是 `ExpandFill`，多余宽度被它分走 → 矩阵区几乎不随窗口变（1920→2592 只涨 26px）。
     改成不参与扩张，矩阵独占剩余宽度。
   - **B**：`OnMatrixResized` 读 `_matrix.Size.X`，而 GridContainer 的尺寸被"当前列数 × 卡宽"这个最小尺寸
     顶住 → **窗口变窄时它根本不发 `Resized`**，列数只涨不跌（1920→11 列、拉宽→18 列、缩回 16:9 仍然 18 列、
     矩阵还溢出右栏）。改成从 `Root`（锚满视口、不受子节点最小尺寸影响）算可用宽，
     触发源也从 Matrix 换成 **Root 的 `Resized`**。
   - **C**：页脚文案不刷新（重建后没调 `RefreshAll`）→ 出现过"矩阵真 18 列、页脚还写 10×3"。
   - 实测（同一实例只改窗口大小）：1920×1080 → **12 列**（每格 110×176）· 2560×1000 → **19 列** ·
     缩回 1920×1080 → **12 列**（两向都成立）· 3200×1000 → **24 列**。卡牌宽高比恒 0.625。
     截图：`p6b_matrix_12col_narrow.png` / `p6b_matrix_19col_wide.png`。

### 7.5 两个可选的裁定

- ✅ **删掉 `MetaProgress.BuyHealth/BuyDamage/BuySpeed/BuyCrit/BuyReload`**（连同只服务它们的私有
  `Buy(id, ref level)`）—— `BaseScreen` 删掉之后它们是死 API。**`Bonus*` 全部保留**
  （`RunController.ApplyBalance` 每局开局仍在读）。`BuyReload` 与弹匣清理一起删。
  ⚠️ `*Cost` / `*Level` 那几个只读访问器本轮**没有调用者也没被裁定删**，先留着（已在代码注释里写明）。
- ✅ **`ApplyAbilityMatrix` 的顺序问题：只改注释，没动代码**。真实顺序是"矩阵先跑、技能行后跑"，
  与注释写的相反；后果是**第一次套用**时 `Ability.AbilityId` 还是空串 → 只能退到类名匹配 →
  与角色行里按**技能行 Id** 写的名单对不上 → 这一趟矩阵实际什么都没改（现状无害，
  真正决定开关的是技能行自己的 `AbilityConfig.Enabled`）。
  **为什么不改顺序**：矩阵内部的 `ResetAbilityBindings` 会连 `InputAction` 一起恢复场景基线，
  它必须排在**所有**"行 → 能力"的写入之前；挪到技能行之后会把技能行刚写上的按键绑定清掉（实打实的回归）。
  真要修得拆成三步（先 reset → 再技能行 → 再矩阵），留给后续。
