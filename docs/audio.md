# 音频系统（总线 / 音频表 / AudioManager）

> **给"要往项目里加一个音效"的人看。** 读完这篇你不需要看任何其它文档。
> 相关：[assets.md](assets.md)（目录与命名规范）、[config.md](config.md)（配置表体系与纪律）、
> [art-sources.md](art-sources.md) §2.5（音效素材从哪下）。
> **本次之前的项目状态**：全项目 **0 个音频** —— 没有 `AudioStreamPlayer`、没有总线配置、没有音效文件。

---

## 一、30 秒结论

| 问题 | 答案 |
|---|---|
| 音频分成哪几块？ | **4 条总线**：`Master` → `Music` / `SFX` / `UI`（`default_bus_layout.tres`） |
| 音效写在哪？ | **一张表**：`Config/Tables/Audio/audio_*.tres`，**一行一条音效** |
| 怎么播？ | `AudioManager.PlaySfx("sfx_xxx")` —— **按 id 播，调用方不知道文件在哪** |
| 加一个音效要改几个文件？ | **2 个**（放 `.ogg` + 加一行 `.tres`）—— **零 C#**（除非是新触发点） |
| 表读不到 / id 写错会怎样？ | **静默不播 + 写一次 warning**，玩法完全不受影响（已主动实测，见 §七） |

```
Assets/Audio/sfx/*.ogg ─┐
Assets/Audio/ui/*.ogg  ─┤  被引用
                        ▼
Config/Tables/Audio/audio_*.tres  ──索引──►  Config/Balance.tres 的 [Export] AudioConfig[] Audio
                        ▲                                        │
                        │ 读                                       ▼
                   AudioManager（autoload）◄── ConfigRows.Rebuild / ById
                        │
      PlaySfx(id) / PlayUi(id) / PlayMusic(id)
                        │
        ┌───────────────┼───────────────┐
        ▼               ▼               ▼
   16 个声部        4 个声部        1 个声部
   总线 SFX        总线 UI        总线 Music
        └───────────────┴───────────────┘
                        ▼
                    总线 Master
```

---

## 二、总线结构（`default_bus_layout.tres`）

| 索引 | 总线名 | 谁往里播 | 声部数（同时能响几个） |
|---|---|---|---|
| 0 | `Master` | ——（不直接播） | — |
| 1 | `Music` | `PlayMusic(id)`（表里 `Bus = Music` 的行） | 1（后一首顶掉前一首） |
| 2 | `SFX` | `PlaySfx(id)`：枪声 / 命中 / 死亡 | **16** |
| 3 | `UI` | `PlayUi(id)`：按钮 / 界面开合 | **4** |

- 三组都 **`send = Master`**，音量默认 **0 dB**（**每个文件自己的响度写在表行的 `VolumeDb` 上**，不要动总线音量 —— 动总线会把那一类音效一起改掉）。
- **为什么分这三组**：玩家以后要能单独关音乐（`Music` 静音）而不动音效；UI 音效贴着耳朵、战斗音效要能被"战斗音量"单独压。
- ⚠️ **`default_bus_layout.tres` 必须在项目根目录、不能改名**：`project.godot` 里**看不到** `audio/buses/default_bus_layout` 这一行 —— 因为它的值就是这个路径，**Godot 保存项目设置时会剔除"等于默认值"的项**。换句话说：**这个文件放在根目录就是"配好了"**，不需要（也无法保留）显式的一行。删掉它 = 所有声音掉回 Master。

**已验证总线真的生效**（不是"文件放了但没被读"）：运行期读 `AudioServer` 拿到的总线名就是
`Master/Music/SFX/UI`，启动日志里也会把每条总线的声部数写出来（见 §六）。

---

## 三、音频表（`Config/Tables/Audio/`）

**一行 = 一条音效**，一行一个 `.tres`；索引挂在 `Config/Balance.tres` 的 `[Export] AudioConfig[] Audio` 上。
行类：`Scripts/Config/AudioConfig.cs`（继承 `ConfigRow`，所以每行都有 `Id` / `DisplayName` / `Note` 等通用列）。

| 列 | 类型 | 含义 |
|---|---|---|
| `Id` | string | **按 id 播放用的键**（小写 snake_case，**与 `.ogg` 文件名一致**）。调用点只认它 |
| `Stream` | **音频资源引用** | 把 `Assets/Audio/` 下的 `.ogg` 拖进来。**留空 = 这条 id 只占位、不发声**（会写 warning） |
| `Bus` | enum | `Sfx` / `Ui` / `Music` → 播到哪条总线 |
| `VolumeDb` | float | 这条音效的**基准音量**（0 = 原样，负数变小）—— 用它拉平素材之间的响度差 |
| `VolumeRandomDb` | float | **音量随机范围**：每次 ±这个值（分贝）。0 = 不抖 |
| `PitchScale` | float | 基准音高（1 = 原速；0.5 慢一倍、2 快一倍） |
| `PitchRandom` | float | **音高随机范围**：每次 ±这个值（相对 1.0）。0.06 ≈ ±6%，**一个文件当三五个用** |

**为什么 `Stream` 是资源引用而不是路径字符串**：与 `UnitConfig.AiProfile` / `WeaponConfig.Pool` 同一条纪律 ——
行文件被删/改名时 Godot 把引用**置 null**（可判定、可警告），而字符串拼错一个字符只会**静默没声音**。
Inspector 里它也是**类型过滤过的资源选择器**。

> ⚠️ **操作纪律（与其它表完全一样）**：不要单独删/改 `Balance.tres` 引用到的行文件。
> 只要引用的**任何一个**行文件缺失/改名，Godot 会**整份 `Balance.tres` 加载失败**，于是**所有**数值静默回到代码默认值。
> 要下线一行，就把 `Balance.tres` 里的数组项与顶部对应的 `[ext_resource]` **一起**删。

**当前 13 行**（5 条示范 + 4 条能力卡音效 + **4 条 BGM**）：

| 行文件 | Id | 总线 | 文件 | 触发点 |
|---|---|---|---|---|
| `audio_sfx_weapon_fire.tres` | `sfx_weapon_fire` | SFX | `Assets/Audio/sfx/sfx_weapon_fire.ogg` | `Weapon.TryFire` |
| `audio_sfx_combat_hit.tres` | `sfx_combat_hit` | SFX | `Assets/Audio/sfx/sfx_combat_hit.ogg` | `Projectile.ResolveImpact` + `AttackHitbox._PhysicsProcess` |
| `audio_sfx_enemy_die.tres` | `sfx_enemy_die` | SFX | `Assets/Audio/sfx/sfx_enemy_die.ogg` | `GameplayEventBus` 的 `Died`（`Faction.Enemy`） |
| `audio_ui_click.tres` | `ui_click` | UI | `Assets/Audio/ui/ui_click.ogg` | 所有 `BaseButton.Pressed`（运行期自动挂） |
| `audio_ui_open.tres` | `ui_open` | UI | `Assets/Audio/ui/ui_open.ogg` | `OverlayScreen.BeginOpen` |
| `audio_ui_card_unlock/upgrade/fail/reset.tres` | `ui_card_*` | UI | `Assets/Audio/ui/ui_card_*.wav` | 能力卡矩阵的 4 个操作（第六轮加的，见 [ability-cards.md](ability-cards.md)） |
| `audio_music_level_01/02.tres` | `music_level_01/02` | **Music** | `Assets/Audio/music/music_level_*.ogg` | `MusicDirector`：**关卡默认曲库**（§九） |
| `audio_music_challenge_01/02.tres` | `music_challenge_01/02` | **Music** | `Assets/Audio/music/music_challenge_*.ogg` | `MusicDirector`：**挑战中曲库**（§九） |

素材：前 5 条是 **Kenney CC0**（Sci-fi Sounds / Impact Sounds / Interface Sounds），来源对照见 `Assets/Audio/SOURCES.txt`；
`ui_card_*` 是项目自制波形；4 条 BGM 来自 **AlkaKrab Music《10 Ambient RPG Tracks》**（素材包与许可见 `Assets/Audio/SOURCES.txt`）。

---

## 四、`AudioManager`（autoload，`Scripts/Audio/AudioManager.cs`）

### 接口

| 接口 | 说明 |
|---|---|
| `bool AudioManager.PlaySfx(string id)` | 播一条**战斗**音效（= `Play`）。返回"真的播出去了" |
| `bool AudioManager.PlayUi(string id)` | 播一条**界面**音效（= `Play`） |
| `bool AudioManager.PlayMusic(string id)` | 播**音乐**（表里这一行的 `Bus` 应为 `Music`）；单声部，后一首顶掉前一首 |
| `bool AudioManager.Play(string id)` | 唯一实现：查表 → 按行的 `Bus` 选总线与声部池 |
| `void AudioManager.StopMusic()` | 停当前音乐（音效不受影响） |
| `AudioManager.MusicEnded`（事件） | 一首音乐**自然播完**时触发（`Stop()` 不触发）—— 曲库轮播用它 |
| `AudioManager.MusicVoice`（只读，`internal`） | 音乐声部本体，**只给 `MusicDirector` 做淡入淡出**；要播音乐一律走 `PlayMusic` |
| `MusicDirector`（`AudioManager` 的子节点，自动创建） | **状态音乐**：两套 BGM 曲库 + 挑战切换（§九），零 C# 配置 |
| `AudioIds.*` | 代码里用到的 id 常量（`AudioIds.WeaponFire` …）；**不是第二份真源**，写错会被 warning 抓出来 |

> `PlaySfx` / `PlayUi` 只是**让调用点读起来更清楚的别名** —— 真正决定走哪条总线的是**表行的 `Bus` 列**，
> 不存在"函数名一套、表里又一套"的两个真源。

### 声部池（为什么不是每次 new 一个播放器）

`_Ready` 里一次性建好 **SFX 16 个 / UI 4 个 / Music 1 个** `AudioStreamPlayer` 子节点。
播放时挑一个空闲的；**全忙就轮转抢占最老的那个**（连打时听得见最后一发，比"丢掉新声音"更符合直觉）。
一一梭子子弹 12 发 + 12 次命中会在一瞬间要 20 多个播放器，每次建节点既抖又难回收。

### 三条必知的行为

1. **`ProcessMode = Always`**（代码里写死的）：界面打开时 `UiRouter` 会把 `SceneTree.Paused` 置 true，
   而 `AudioStreamPlayer` **会跟着暂停** —— 那正好是 UI 音效该响的时候。声部是它的子节点，继承这个设置。
2. **回退语义**：表读不到 / id 不存在 / `Stream` 为空 / 总线不存在 → **静默不播**（玩法绝不受影响），
   但每个 id **只写一次** warning（`GD.PushWarning` + 日志分类 `Audio`），不会刷屏、也不会"以为接好了其实没声音"。
3. **日志三条线**（这是"音频没法用截图验证"的答案）：
   - 启动一行：`配置表：音效 N（来源：表 Audio）—— 总线 Master(Master) / SFX(16 声部) / UI(4 声部) / Music(1 声部)；sfx_weapon_fire(Sfx)、…`
     （**N = 表里几行**；2026-10 第七轮起是 **13** —— 5 示范 + 4 能力卡 + 4 BGM，加音频行这个数字就会变，属于预期）
   - **每个 id 第一次真的播出来时一行**：`首次播放「sfx_weapon_fire」→ 总线 SFX / res://Assets/Audio/sfx/sfx_weapon_fire.ogg`
   - 退出时一行合计：`音效统计：共 30 次 —— ui_open×2、sfx_combat_hit×20、sfx_weapon_fire×1、sfx_enemy_die×7（缺失 id 0 个）`

### 已接好的 5 个示范（各自只动了 1~2 行，玩法逻辑一行没改）

| 类别 | id | 在哪挂的 | 改动量 |
|---|---|---|---|
| **射击** | `sfx_weapon_fire` | `Scripts/Weapons/Weapon.cs` 的 `TryFire`，紧挨 `_muzzleVfx?.Call("play")` | 1 行调用（+1 行 using） |
| **命中** | `sfx_combat_hit` | `Scripts/Combat/Projectile.cs` 的 `ResolveImpact`、`Scripts/Combat/AttackHitbox.cs` 的 `_PhysicsProcess` —— **都紧挨着已有的 `HitEffect.Spawn(...)`**，与命中特效**同一个位置、同一个判定** | 每处 1 行（+1 行 using） |
| **UI · 界面打开** | `ui_open` | `Scripts/UI/OverlayScreen.cs` 的 `BeginOpen`（**六个弹窗共用这一处**） | 1 行（+1 行 using） |
| **UI · 按钮点击** | `ui_click` | **不在任何场景里接线**：`AudioManager` 订阅 `SceneTree.NodeAdded`，**运行期**给所有 `BaseButton` 挂 `Pressed` | **0 行**（新界面/新按钮自动有声） |
| **敌人死亡** | `sfx_enemy_die` | `AudioManager` 订阅 `GameplayEventBus.Raised` 的 `Died`（`Faction.Enemy` 才播） | **0 行**（订阅方在 `AudioManager` 自己身上） |

> **刻意没有为了接音效去改玩法**：`Unit.ReceiveHit` 的伤害管线、`Weapon` 的射击判定、`AttackHitbox` 的命中判定
> 都**一行未改**，全部是"在既有事件的旁边挂一行播个音效"。

---

## 五、✅ 后续加一个音效要做什么

### A. 用**已有**触发点（最常见，**零 C#**，2 步）

1. 把 `.ogg` 拷进 `Assets/Audio/sfx/`（或 `ui/`），命名 `<类别>_<分组>_<变体>`：
   - `sfx_*` = 战斗音（`sfx_weapon_reload.ogg`、`sfx_enemy_hurt.ogg`…）
   - `ui_*` = 界面音（`ui_hover.ogg`、`ui_confirm.ogg`…）
   - **全小写、下划线分词**；Godot 会自动生成同名 `.import`（**`.import` 要一起入库**）。
2. 在 `Config/Tables/Audio/` 新建一行 `.tres`（照抄任意一行改），填：
   - `Id` = **文件名去掉扩展名**（例：`sfx_weapon_reload`）
   - `Stream` = 把刚放进来的 `.ogg` **拖到这个槽**
   - `Bus` / `VolumeDb` / `VolumeRandomDb` / `PitchScale` / `PitchRandom`
   - `DisplayName` 写中文名、`Note` 写"触发点 + 素材来源"（方便下一个人）
3. 把这一行**加进 `Config/Balance.tres` 的 `Audio` 数组**（在 `Audio = [ ... ]` 里追加一个 `ExtResource`，
   并在文件顶部加一条 `[ext_resource type="Resource" path="res://Config/Tables/Audio/audio_xxx.tres" id="12N_audio_xxx"]`，
   顺手把 `load_steps` +1）。

> ⚠️ 第 3 步不能省：行文件**只被目录里放着是不会生效的** —— 索引只认 `Balance.tres` 的数组。
> 忘了加的表现是"**文件在、表在、就是没声音**"（启动日志里的条数与候选列表会立刻告诉你少了几条）。

**如果你要的是一个"全新的触发点"**，再加一步（1 行 C#）：

```csharp
using ActRoguelike.Audio;            // ← 每个调用点 +1 行
...
AudioManager.PlaySfx("sfx_weapon_reload");   // ← 就这一行
```

然后把 id 常量加到 `Scripts/Audio/AudioIds.cs`（避免在调用点写字符串字面量）。

### B. 想改某条音效的音量 / 随机 / 换文件

**只改那一行 `.tres`**，不用动 C#、不用动场景。改完**重新运行游戏**才生效（表在启动时读一次）。

### C. 想加一整类（例如"环境音"）

1. 在 `default_bus_layout.tres` 里加一条总线（编辑器：底部 **音频** 面板 → 添加总线 → 改名 → 设 `send = Master`），
2. 在 `AudioConfig.cs` 的 `AudioBusKind` 里加一个取值 + `AudioManager` 里加一组声部池（约 5 行）。

### 常见错误对照表

| 症状 | 原因 | 看哪里 |
|---|---|---|
| **完全没声音，日志里也没有 `首次播放`** | `Balance.tres` 的 `Audio` 数组里没加这一行 | 启动日志 `配置表：音效 N` 的 N 对不对 |
| 日志有 `⚠ 音效「xxx」跳过了：音效表里没有这一行` | `Id` 拼错 / 大小写不符 / 行文件没加进数组 | 表里的 `Id` 与调用点那个字符串 |
| 日志有 `⚠ … 但 Stream 是空的` | 那一行没把 `.ogg` 拖进 `Stream` | 该行 `.tres` 的 `Stream` 列 |
| 声音被"截断"或和上一次重叠得很怪 | 同一帧播太多次、声部被抢占 | 把 `VolumeRandomDb` / `PitchRandom` 打开会自然一些；或调大声部数 |
| 界面打开时没声音（其它战斗音正常） | 声部的 `ProcessMode` 被改了 | `AudioManager._Ready` 里的 `ProcessModeEnum.Always` |
| 有个 id 从没出现过 `首次播放` | 那条触发线没被走到（不是音频的问题） | 用 `首次播放` 行反查触发点 |

---

## 六、怎么在**没有声音**的情况下证明它真的播了

音频**没法用截图验证**。本项目的做法是**三条日志线 + 只读计数器**：

> ⚠️ 下面 §六 / §七 的样例是**当初 5 行音效时**抓的（数字是当时的真实值，没有改写）；
> **现在表里 13 行**，最新一轮的完整证据在 **§九**。

1. **启动**（证明表和总线都到位）：
   ```
   [Audio] 配置表：音效 5（来源：表 Audio）—— 总线 Master(Master) / SFX(16 声部) / UI(4 声部) / Music(1 声部)；
           sfx_weapon_fire(Sfx)、sfx_combat_hit(Sfx)、sfx_enemy_die(Sfx)、ui_click(Ui)、ui_open(Ui)
   ```
   这一行同时给出：**读到几条** / **来源是表还是代码默认值** / **每条走哪条总线** / **`Stream` 有没有空** /
   **`AudioServer` 上真实存在的总线名**（`_sfxBus` 若因布局没加载而退回 Master，这里会显示 `Master(16 声部)` 且上方有一条 ⚠）。
2. **首次播放**（证明"这条路真的被走到了"）：
   ```
   [Audio] 首次播放「sfx_weapon_fire」→ 总线 SFX / res://Assets/Audio/sfx/sfx_weapon_fire.ogg
   ```
3. **退出合计**（证明"量级对不对"）：
   ```
   [Audio] 音效统计：共 30 次 —— ui_open×2、sfx_combat_hit×20、sfx_weapon_fire×1、sfx_enemy_die×7（缺失 id 0 个）
   ```

另外 `#if DEBUG` 下 `AudioManager` 暴露了几个**只读**属性，可以用运行时表达式（`await_runtime_condition` /
脚本探针）直接断言：`DebugPlayCount` / `DebugLastId` / `DebugRowCount` / `DebugPlayingVoices`
（当前 `playing == true` 的声部数 = "真的在播"而不是"只记了个数"）/ `DebugBuses` / `DebugMissingIds` / `DebugSummary`。

⚠️ 本机实测：**gdmcp 的 `run_project` 在这个工程上目前会失败**
（`Play was requested but no debugger session became active` —— 编辑器里 C# 的"运行前构建"回调在
deferred 上下文创建 ProgressDialog 报错，与本次改动无关）。所以上面的取数是用
**命令行直接跑游戏 + 读 `user://logs/…`** 拿到的；`DebugSummary` 那套留给编辑器桥恢复正常后用。

---

## 七、本次实测证据（2026-10）

**编译**：`& build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**。

**自测（`selftest.flag` + 直跑 `res://Scenes/Main.tscn`，`run_20261012_010518.log`，0 条 ERROR/Exception）**：

```
[Audio] 配置表：音效 5（来源：表 Audio）—— 总线 Master(Master) / SFX(16 声部) / UI(4 声部) / Music(1 声部)；
        sfx_weapon_fire(Sfx)、sfx_combat_hit(Sfx)、sfx_enemy_die(Sfx)、ui_click(Ui)、ui_open(Ui)
[Audio] 首次播放「ui_open」→ 总线 UI / res://Assets/Audio/ui/ui_open.ogg
[Audio] 首次播放「sfx_combat_hit」→ 总线 SFX / res://Assets/Audio/sfx/sfx_combat_hit.ogg
[Audio] 首次播放「sfx_weapon_fire」→ 总线 SFX / res://Assets/Audio/sfx/sfx_weapon_fire.ogg
[Audio] 首次播放「sfx_enemy_die」→ 总线 SFX / res://Assets/Audio/sfx/sfx_enemy_die.ogg
[SelfTest] === 自测结束 ===
[Audio] 音效统计：共 30 次 —— ui_open×2、sfx_combat_hit×20、sfx_weapon_fire×1、sfx_enemy_die×7（缺失 id 0 个）
```

`ui_click` 另跑一次**运行期探针**取证（用一个临时场景在运行期新建 `Button` 再合成一次真实鼠标点击，
`run_20261012_010416.log`；探针文件用完即删）：

```
[probe] Button.Pressed 连接数 = 1
[Audio] 首次播放「ui_click」→ 总线 UI / res://Assets/Audio/ui/ui_click.ogg
[Audio] 音效统计：共 1 次 —— ui_click×1（缺失 id 0 个）
```

**回退路径主动验证**（把 `Balance.tres` 的 `Audio` 数组整条删掉再跑一整轮自测，`run_20261012_010445.log`）：

| 观察点 | 实测 |
|---|---|
| 表读不到留痕 | `配置表：音效 0（来源：代码默认值（表读不到））—— 总线 Master(Master) / SFX(16 声部) / UI(4 声部) / Music(1 声部）` |
| **每个 id 只警告一次** | 4 条 `⚠ 音效「ui_open / sfx_combat_hit / sfx_enemy_die / sfx_weapon_fire」跳过了：音效表里没有这一行 —— 静默不播，玩法不受影响。`（`ui_click` 没被触发所以没有它的警告） |
| **不崩、玩法照旧** | 自测全程跑到 `=== 自测结束 ===`；端到端按 F 仍能打开奖励界面（`奖励界面打开 = True Paused = True`） |
| 结算留痕 | `音效统计：本局一次都没播（表里 0 条；检查音效表与触发点）。` |

---

## 八、已知边界 / 下一步（都是"故意没做"，不是没接完）

| 项 | 现状 | 要做的话 |
|---|---|---|
| **没有 3D 定位音** | 全部是 2D（`AudioStreamPlayer`），没有距离衰减/声道定位 | 加一条 `PlaySfxAt(id, position)` + 一组 `AudioStreamPlayer3D` 声部；**表结构不用动**（`Bus` 已经分流） |
| **音乐素材已接（2026-10 第七轮）** | ✅ `Music` 总线 + `PlayMusic` + **两套曲库**（关卡默认 / 挑战中）都在用，见 §九 | 想换曲子：把 `.ogg` 放进 `Assets/Audio/music/`（命名 `music_level_0X` / `music_challenge_0X`）+ 加一行表 + 挂进 `Balance.tres`，**零 C#** |
| **没有音量设置界面** | ✅ **2026-10 第六轮已做**：全屏设置菜单（`SettingsScreen`，快捷键 O）的**音量**页循环 `AudioServer.BusCount` 逐条总线给滑条 + 静音，落 `user://settings.cfg`，启动时由 `UiRouter._Ready → SettingsStore.ApplyOnStartup()` 套用。见 [ui.md](ui.md) §12 | 想加"按类别"的音量曲线就在 `AudioSettingsSection` 里加一行 |
| **音效表没有"变体数组"** | 一条 id = 一个文件（靠 `PitchRandom` 制造变化） | 要轮播多个变体就加一列 `Variants`（`AudioStream[]`），`AudioManager` 里按顺序取 |
| **`ui_click` 是"全按钮一个音"** | 所有 `BaseButton.Pressed` 共用 `ui_click` | 想按按钮分类就加一个 `Button` 的 meta/组约定，或在 `OverlayScreen` 子类里覆盖 |

---

## 九、状态音乐（BGM 曲库与挑战切换，2026-10 第七轮）

> 用户原话（需求包 §五 #14）：「**状态音乐**，先随便搞点来占位，**关卡有默认 bgm 曲库**，**开启挑战时切换挑战时 bgm 曲库**」。

### 两套曲库怎么定义（**约定优于配置：只看 id 前缀**）

| 曲库 | 判据 | 现在有几首 | `MusicDirector` 什么时候用 |
|---|---|---|---|
| **关卡默认** | 音效表里 `Id` 以 **`music_level_`** 开头的行 | 2（`music_level_01/02`） | `StageController.Instance != null`（**在关卡里**）且**不在挑战中** |
| **挑战中** | `Id` 以 **`music_challenge_`** 开头 | 2（`music_challenge_01/02`） | `StageController.ChallengerActive == true` |

- **表内顺序 = 轮播顺序**：一首放完（`AudioStreamPlayer.Finished` → `AudioManager.MusicEnded`）就播同一曲库的下一首，到末尾回到第一首。
- **两套曲库各自记住播到第几首**：挑战开始 / 结束来回切时，不会每次都从第 1 首重放。
- **状态判据直接借 `StageController.ChallengerActive`**（与 `Interactable` 的"挑战中拒绝交互"闸门**同一个判据**）：
  它 = `!IsVictory && (节点挑战进行中 || Boss 还活着)`，所以**节点挑战和 Boss 战都会切挑战 BGM**，
  Boss 被打死的那一刻立刻切回关卡曲库（Boss 战不是节点挑战，语义见 `StageController.cs:47-64`）。
- **没有为它加任何新状态 / 新信号**：`ChallengerActive` 是派生属性、流程层没有对应信号，
  而本批改动边界只允许动 `Scripts/Audio/**` → `MusicDirector` 每帧读一个 bool（成本可忽略），
  不在战斗 / 流程代码里插钩子。
- **基地里没有 BGM**（`StageController.Instance == null` → 淡出停播）；关卡结束回基地时音乐淡出。
  **这是刻意的**（"关卡有默认 bgm 曲库"，基地曲库还没有需求）。

### 为什么不爆音（1 个声部的现实）

`Music` 总线只有 **1 个声部**（后一首顶掉前一首）：直接 `Stop()` 一个正在响的音频会在波形中途切断 → **爆音**。
所以切换路径是「**0.35s 淡出 → Stop → 播新的一首 → 0.35s 淡入**」（淡出终点 -60dB，淡入到**表行里的 `VolumeDb`**，
音量仍然只由表决定，代码里不写死音量）。曲库内部轮播（上一首自然放完）不需要淡出，只有"换曲库/进离关卡"才淡。

### 加一首曲子要做什么（**零 C#**，与加音效同一条纪律）

1. 把 `.ogg` 放进 `Assets/Audio/music/`，命名 `music_level_03.ogg` / `music_challenge_03.ogg`
   （**前缀决定它进哪套曲库**，序号只影响轮播顺序）。
2. 在 `Config/Tables/Audio/` 加一行 `.tres`（照抄 `audio_music_level_01.tres`）：`Id` 同文件名、`Stream` 拖进来、**`Bus = 2`（Music）**、
   `VolumeDb ≈ -10`、`PitchRandom = 0`（音乐不要音高抖动）。
3. 把它挂进 `Config/Balance.tres` 的 `Audio` 数组（`[ext_resource]` + 数组项 + `load_steps` +1）。
4. `MusicDirector` 是**自动创建**的（`AudioManager._Ready` 里 `AddChild(new MusicDirector …)`）—— **不需要动 `project.godot` 的 autoload 表**。

> ⚠️ **`.ogg` 的 Import 里 `loop` 必须是 false（默认就是）**：曲库轮播靠"播完"这件事（`Finished` 信号），
> 勾了 Loop 就永远播不完，一首曲子会一直响下去（`MusicDirector` 只在切曲库时换歌）。

### 怎么在没有声音的情况下证明它真的切了

三条日志线（音频无法截图，本项目的既有做法）：

```
[Audio] 音乐曲库：关卡 2 首[music_level_01、music_level_02] / 挑战 2 首[music_challenge_01、music_challenge_02]（前缀 music_level_* / music_challenge_*，表内顺序 = 轮播顺序）
[Audio] 首次播放「music_level_01」→ 总线 Music / res://Assets/Audio/music/music_level_01.ogg      ← AudioManager 那三条线里的"首次播放"
[Audio] 音乐：切到挑战曲库「music_challenge_01」（1/2 首）                                          ← 状态切换留痕
```

`MusicDirector` 还暴露了 `#if DEBUG` 只读值：`DebugCurrentMusic`（当前在播的 id）/ `DebugLibrary`（关卡 / 挑战）/ `DebugLibraries`（两套曲库内容）。

### 本轮实测证据（`run_20261012_200351.log`，一整轮自测，0 条 ERROR / Exception）

```
[Audio] 配置表：音效 13（来源：表 Audio）—— 总线 Master(Master) / SFX(16 声部) / UI(4 声部) / Music(1 声部)；…、music_level_01(Music)、music_level_02(Music)、music_challenge_01(Music)、music_challenge_02(Music)
[Audio] 音乐曲库：关卡 2 首[music_level_01、music_level_02] / 挑战 2 首[music_challenge_01、music_challenge_02]（前缀 music_level_* / music_challenge_*，表内顺序 = 轮播顺序）
[Audio] 音乐：切到关卡曲库「music_level_01」（1/2 首）        ← 进关（StageController 出现）
[Audio] 首次播放「music_level_01」→ 总线 Music / res://Assets/Audio/music/music_level_01.ogg
[Audio] 音乐：切到挑战曲库「music_challenge_01」（1/2 首）    ← 开启挑战（ChallengerActive=false→true）
[Audio] 首次播放「music_challenge_01」→ 总线 Music / …/music_challenge_01.ogg
[Audio] 音乐：切到关卡曲库「music_level_02」（2/2 首）        ← 挑战结束（回到关卡曲库，且轮播游标停在第 2 首）
[Audio] 首次播放「music_level_02」→ 总线 Music / …/music_level_02.ogg
[Audio] 音乐：切到挑战曲库「music_challenge_02」（2/2 首）    ← Boss 战
[Audio] 音乐：切到关卡曲库「music_level_01」（1/2 首）        ← Boss 阵亡，挑战者状态解除
[Audio] 音乐：切到挑战曲库「music_challenge_01」（1/2 首）    ← 第 2 关的节点挑战
[Audio] 音效统计：共 46 次 —— music_level_01×2、ui_open×3、music_challenge_01×2、sfx_combat_hit×18、sfx_weapon_fire×1、sfx_enemy_die×17、music_level_02×2、music_challenge_02×1（缺失 id 0 个）
```

退出合计里的 7 次音乐播放**与 7 条"切到…曲库"日志逐条对得上**（没有多出来的、也没有漏掉的），
`缺失 id 0 个` 说明 4 个音乐 id 全部在表里、`Stream` 都不为空。

**素材**：`Assets/Audio/music/` 只放了 4 首（**从 10 首的包里挑的**：Ambient 6 / 8 给关卡，Ambient 5 / 7 给挑战，
共 ~15 MB；**整个 382 MB 的包没有进仓库**）。来源与许可记在 `Assets/Audio/SOURCES.txt`。
