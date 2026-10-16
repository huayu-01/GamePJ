# 界面（UI）素材接入说明

> 给"要给面板 / 按钮 / 字体 / 准星 / 血条换外观"的人看。**UI 外观的唯一入口文档。**
> 相关：[art-onboarding.md](art-onboarding.md)（美术总说明）、[assets.md](assets.md)（目录与命名规范）、
> [art-integration-log.md](art-integration-log.md) 的「第五轮」（本轮的逐项实测证据与截图）。
> **本文只讲外观**：界面逻辑、分层、节点名约定见 [todo-handoff.md](todo-handoff.md) 与 [architecture.md](architecture.md)。

---

## 0. 30 秒结论

| 问题 | 答案 |
|---|---|
| 用了哪个包？ | **Kenney UI Pack - Sci-Fi 2.0**（CC0）+ **Kenney Crosshair Pack 1.1**（CC0）+ **Kenney Fonts**（CC0）。三者包内 `License.txt` 均写明 *Creative Commons Zero, CC0*，**可商用、免署名、可入库**。 |
| 主题资源在哪？ | `Assets/UI/Themes/ui_theme.tres`（一个文件统管字体 / 字号 / 颜色 / 面板 / 按钮），挂在 **8 个界面场景的根 `Control`**（7 个弹窗 + `Hud.tscn`）的 `theme` 属性上。 |
| 后续加界面素材要做什么？ | **三步**：① 贴图丢进 `Assets/UI/Textures/`（`tex_ui_*.png`）；② 需要新样式就在主题里加一个**类型变体**（`UiXxx/base_type`）；③ 场景节点上写 `theme_type_variation = &"UiXxx"`。**不用碰任何 C#**。细节见 §3。 |
| 中文能显示吗？ | **能**，而且是**受控**的：主题的 `default_font` 是一个 **`SystemFont`**（按 `font_names` 优先级从系统里取「微软雅黑 UI → 微软雅黑 → Noto Sans SC → 思源黑体 …」）。实测 `中/文/·/…/◆/◇/A/7` 全部 `has_char = true`，截图见 `ui_01`~`ui_09`。 |
| 为什么中文不是自带字体？ | **体积**：一个能覆盖简中的开源字体最小也是 **17 MB**（Noto Sans SC 可变体）/ **24 MB**（霞鹜文楷单字重）。本轮任务明确"不要下大文件"，所以**先调研 + 用系统字体占位**，换法见 §4。**注意：系统字体是"开发期占位"，发行前必须换成自带字体**（玩家机器上不一定有雅黑）。 |
| 准星接在哪？ | `Assets/UI/Textures/tex_ui_crosshair_dot.png`（**一个点**，48×48 实心白圆点 + 羽化边，2026-10 用户要求改回点）→ `Scenes/UI/Hud.tscn` 的 **`Root/Crosshair`**（`TextureRect`，屏幕中央 20×20）。改动前是 Kenney 的描边十字 + 圆圈（`tex_ui_crosshair_default.png`，已删除）。 |
| glow 开了吗？ | 开了：`Scenes/Levels/TestArena.tscn` 的 `WorldEnvironment` → **`glow_enabled = true`、`glow_hdr_threshold = 2.0`、`glow_intensity = 0.5`、`glow_blend_mode = 1`（Screen）、只开 4/5/6 三档半径**。为什么阈值这么高见 §7。 |
| 改了几个界面？ | **外观全部 9 个（主题级）+ 2 个新界面**（`SettingsScreen` 全屏设置菜单、`BuildScreen` 角色持有面板）。**7 个旧界面已全部改成"全屏类界面"**（面板铺满、内容居中），见 §11。 |
| 按键提示栏在哪？ | **在 UI 基类里**：`OverlayScreen.SetKeyHints(...)`，屏幕底部一栏「键 + 说明」。**结构永远存在**（`Root/KeyHintBar`），界面可以不填、不显示。**第三批写能力矩阵界面时照 §10 抄。** |
| 能力矩阵（局外卡牌）长什么样？ | `AbilityMatrixScreen.tscn`，**K** 打开。每页最多 **6×3**、只铺真实卡、空位留白；按钮或 **PgUp / PgDn** 翻页。卡面比例 0.625、间隔 28px、左侧额度条 64px 自下向上。三状态与四种交互继续由 `AbilityCardTile` 负责。当前实拍见 [A 批验收](a-batch-verification.md)。 |
| 设置菜单？ | **Esc / O** 打开 `SettingsScreen`；画面 / 控制 / 音量 / **帮助** 四页，分类来自注册表。**H** 直达帮助分页，`UiService.OpenHelp()` 兼容到这里。见 §12。 |
| 图标？ | `Assets/UI/Icons/ic_ui_*.svg`（13 张，**项目自制 SVG**，Godot 原生导入）+ `Scripts/UI/UiIcons.cs`（**唯一映射表**）。构筑元素 / 奖励卡片 / 设置分类 Tab 都用图标；说明只在**选取界面**与**鼠标悬停**（`tooltip_text`）。见 §13。 |
| 场景里的节点还写名字吗？ | **不写了**：`RewardNode.tscn` 的 `Label3D` 名牌已删，基类 `Interactable` 还会无条件把任何 `Label3D` 子孙藏掉。见 §13。 |
| 想一次看全部证据？ | [art-integration-log.md](art-integration-log.md) 的 **第五轮** 一节：逐项改动 + 全部截图 + 自测输出。**本轮（第六轮）的证据在 §10~§13 各自的表里**（截图 `ui_10` ~ `ui_17`、探针输出、自测硬指标）。 |
| **第六轮（表现批）改了什么？** | **§16**：#9 跳字分类（伤害/甲/盾/格挡/无敌/其他伤害/恢复，共 7 类）、#8 护甲进度 + 碎甲（§16.2，**2026-10 用户 A6 改成"血条上罩一层半透明塑料"**）、#13 交互表现（描边 + 按键图标 + 失败抖动/音效/击退）、#4 基地实时换装（模型 + 数值 + 枪械）。**判定与伤害管线一行没改**（见 §16.4 的逐条核对表）。#3 死亡流程单独一篇：[death-flow.md](death-flow.md)。 |

---

## 1. 文件清单（本轮新增）

```
Assets/UI/                                   ← 第五轮新建（assets.md §1 的 UI/ 目录）
  Textures/tex_ui_panel_frame.png            128×128  大面板底（切角九宫格）
  Textures/tex_ui_panel_frame_small.png       64×64   小面板底 / 焦点框（同上，1×）
  Textures/tex_ui_panel_glass.png            128×128  圆角玻璃面板底（备选，本轮未接线）
  Textures/tex_ui_panel_glass_small.png       64×64   按钮底（所有普通按钮）
  Textures/tex_ui_header_band.png            192×64   标题带 / 主按钮（蓝色横带那条）
  Textures/tex_ui_bar_frame.png              192×48   分隔条 / 血条边框
  Textures/tex_ui_bar_fill.png                24×24   血条填充（平铺，会跟着代码染色）
  Textures/tex_ui_crosshair_dot.png           48×48   准星（一个点；2026-10 改回，原来用的是 Kenney 描边十字）
  Fonts/font_ui_kenney_future.ttf                      HUD 数字字体（Kenney Future, CC0）
  Themes/ui_theme.tres                                 主题（唯一入口）
  Licenses/license_kenney_{ui_pack_scifi,crosshair_pack,fonts}.txt   CC0 原文（照抄进仓库）

Assets/UI/Icons/                             ← 第六轮新建（13 张**自制 SVG**，见 §13）
  ic_ui_reward_{enhance,evolution,mutation}.svg        奖励类型（强化 / 进化 / 异化）
  ic_ui_node_{weapon,resource,heal,special,shop,portal}.svg   地面节点类型
  ic_ui_set_{graphics,controls,audio}.svg              设置菜单分类 Tab
  ic_ui_build_holdings.svg                             「角色持有」面板标题

Scenes/UI/_UiShowcase.tscn + _ui_shot.gd     ← 验证工具（见 §8）：一次运行拍完 9 个界面 + HUD + glow 前后
```

> **SVG 是新类别**：`Assets/UI/Icons/ic_ui_<分组>_<名字>.svg`。Godot 原生导入 SVG（无需图片工具链），
> **改图标 = 改一个文本文件**；`.import` 要一起入库（`Import` 一次即可，`--headless --import` 也行）。

> **命名**照 [assets.md](assets.md) §2：`tex_ui_<变体>.png`（类别 `tex_` / 分组 `ui`）。
> 字体是**新类别**，本轮用 `font_ui_<变体>.ttf`（`assets.md` 的表里还没有 `font_` 前缀，建议补一行）。

**两处刻意的目录选择**（照实记录）：

1. **贴图放 `Assets/UI/Textures/` 而不是 `Assets/Textures/`**：`assets.md` 说贴图归 `Assets/Textures/`，
   但 UI 贴图**只给界面用**、和 3D 贴图（天空盒 / 枪口）混在一起后，改 UI 的人要在一堆 3D 贴图里找；
   放 `Assets/UI/` 下意味着"改界面只看一个目录"。
2. **字体放 `Assets/UI/Fonts/` 而不是 `Assets/Fonts/`**：`Assets/Fonts/` 这一轮被划给了别的代理。
   以后若要统一到 `Assets/Fonts/`，**只需要改主题里的 `1 行` 引用**（§4 第 3 步）。

---

## 2. 主题是怎么组织的

`ui_theme.tres` 里有**三层**，从"最省事"到"最具体"：

| 层 | 是什么 | 用在哪 | 例子 |
|---|---|---|---|
| **① 默认项** | `default_font` / `default_font_size` + `Button/*` / `Label/*` / `Panel/*` / `PanelContainer/*` | **所有**控件自动生效，**场景一个字节都不用改** | 所有 `Button` 拿到同一套贴图按钮；所有 `Label` 拿到中文字体 |
| **② 类型变体** | `UiTitle` / `UiPrimaryButton` / `UiCaption`（各自有 `base_type`） | 想让某个控件"长得不一样"时，在节点上写一行 `theme_type_variation = &"UiTitle"` | 标题（蓝色横带 + 28px）、主按钮（蓝色实心）、说明文字（15px 灰） |
| **③ 场景 override** | `theme_override_font_sizes/font_size = ...` 等 | 单个节点的一次性微调 | `Hud.tscn` 的 `Health` 用 Kenney Future 数字字体 |

**主题里的每一项（照抄/改这一张表就够）**：

| 主题项 | 值 | 说明 |
|---|---|---|
| `default_font` | `SystemFont_ui_cjk` | 中文字体（§4） |
| `default_font_size` | 18 | 全局基准字号 |
| `Panel/styles/panel`、`PanelContainer/styles/panel` | `StyleBoxTexture_panel` | `tex_ui_panel_frame.png`，九宫格 34px，`modulate (0.22,0.26,0.36,0.96)`（把浅灰原图压成深蓝灰板） |
| `Button/styles/{normal,hover,pressed,disabled}` | `StyleBoxTexture_btn_*` | 全部用 `tex_ui_panel_glass_small.png`，靠 **`modulate_color` 区分状态**（normal 0.26/0.31/0.42 → hover 0.42/0.54/0.70 → pressed 更暗 + 内容下移 2px） |
| `Button/styles/focus` | `StyleBoxTexture_btn_focus` | 小面板图 + `expand_margin 4` + 青色 → 一圈青色焦点光 |
| `Button/colors/font_*`、`Button/constants/outline_size = 2` | — | 文字自带 2px 描边（在 3D 场景上可读） |
| `Label/colors/font_color`、`Label/constants/outline_size = 2` | — | 同上 |
| `UiTitle/styles/normal` | `StyleBoxTexture_title_band` | **`region_rect = Rect2(6,0,180,33)` 把蓝色横带从按钮图里裁出来**，所以标题天生带 Kenney 那种"标题条" |
| `UiPrimaryButton/styles/*` | `StyleBoxTexture_primary_*` | 同一块蓝色横带铺满整个按钮 = 主行动按钮，**也用在设置菜单选中的那个分类 Tab 上** |
| `UiCaption/colors/font_color`、`font_size = 15` | — | 次要说明文字（灰、无描边） |
| **`UiKeyCapBox/styles/panel`** | `StyleBoxTexture_keycap` | **按键提示栏的键帽**（浅色小底板，让"键"看起来像键）；配 `UiKeyCap` 深色字 |
| **`UiKeyCap/font_size = 16`、`font_color` 深色** | — | 键帽里的字（不描边：底板本身是浅色） |
| **`UiTile/styles/panel`** | `StyleBoxTexture_tile` | **构筑图标格 / HUD 图标格**（112×112 与 44×44 都用它） |

> **为什么要用 `modulate_color` 而不是多存几张图**：Kenney 的每个状态都有现成 PNG（`*_screws` 等），
> 但**同一张图 + 不同 modulate** 改起来只要动一个数字，而且**换主题色只需要改这一列**。
> 想彻底换皮（比如换成 Red 变体）：把 `Assets/UI/Textures/` 里的 PNG 换成
> `_art_staging/extract/kenney_ui-pack-space-expansion/PNG/Red/...` 里的同名文件即可，**主题一行都不用改**。

---

## 3. 后续怎么加界面素材（4 个动作）

### 动作 1：新界面自动继承（0 步）

新建的界面场景**只要把 `theme` 指向 `ui_theme.tres`**（或挂在已成主题的父节点下），按钮 / 面板 / 字体自动就是新样式。

具体做法（`Root` 是那个界面的根 `Control`）：

```
[ext_resource type="Theme" path="res://Assets/UI/Themes/ui_theme.tres" id="2_theme"]
...
[node name="Root" type="Control" parent="."]
theme = ExtResource("2_theme")
```

⚠️ `theme` 是挂在 **`CanvasLayer` 下面的 `Root`（`Control`）** 上的，**不是挂在 `CanvasLayer` 上** ——
`CanvasLayer` 不是 `Control`，没有 `theme` 属性。也别去动 `ScreenLayer`（`Hud`/`Panel`/`Modal` 分层是逻辑）。

### 动作 2：换一张贴图（改 0~1 行）

把新 PNG 丢进 `Assets/UI/Textures/`，然后**只改主题里对应 `StyleBoxTexture` 的 `texture = ExtResource("...")`**。
**场景、C# 都不用碰。** 九宫格边距（`texture_margin_*`）按新图的边角大小改。

### 动作 3：加一个新样式（类型变体，3 行）

例：加一个"危险按钮"（红）：

```
[sub_resource type="StyleBoxTexture" id="StyleBoxTexture_danger_normal"]
texture = ExtResource("3_glass_small")
texture_margin_left = 10.0      ... （同 btn_normal 的九个边距）
modulate_color = Color(0.62, 0.2, 0.22, 1)

[resource]
UiDangerButton/base_type = &"Button"
UiDangerButton/font_sizes/font_size = 18
UiDangerButton/styles/normal = SubResource("StyleBoxTexture_danger_normal")
... hover / pressed
```

场景里：`theme_type_variation = &"UiDangerButton"`。**一个主题文件管所有界面**，不复制样式。

### 动作 4：只改一个节点的字号 / 颜色（场景里一行）

`theme_override_font_sizes/font_size = 26`、`theme_override_colors/font_color = Color(...)`。
**能用变体就别用 override** —— override 是散的，变体是集中的。

### ⚠️ 改动不许碰的东西

| 不许碰 | 为什么 |
|---|---|
| `OverlayScreen.ScreenLayer`（`Hud`/`Panel`/`Modal`） | 决定**是否暂停游戏**，是逻辑不是外观 |
| `Root/PlayerBar`、`BarPlayer/Fill`、`BarPlayer/DelayBar`、`BarEnemy/Content/{Fill,DelayBar,Label}`、`BarBoss/Panel/{Icon,NameLabel,Bar,StateLabel}` 这些**节点名** | C# 按名字取（改名**静默失效，不报错**），完整清单见 [art-onboarding.md](art-onboarding.md) 第五节 |
| 血条的**颜色** | 仍由代码按血量写（`PlayerHud.HealthColor()` / `WorldOverlay.HealthColor()`，绿→黄→红）。换成贴图条没问题（代码对 `TextureRect` 走 `Modulate`），**但颜色会被代码覆盖** |
| `Hud.tscn` 的 `Crosshair` 想加"跟随散布放大" | 要写 C#；本轮只占位 |

---

## 4. 字体：结论 + 换法（**发行前必做**）

### 4.1 已经做到的

- **中文能显示**，而且是**显式指定**的，不再依赖 Godot 默认字体的隐式 fallback：
  `ui_theme.tres` 的 `default_font` 是 `SystemFont`，`font_names` 按优先级列了
  `Microsoft YaHei UI → Microsoft YaHei → Noto Sans SC → Source Han Sans SC/CN → PingFang SC → WenQuanYi Micro Hei → Noto Sans CJK SC → sans-serif`，
  `allow_system_fallback = true`。
- **实测**（`--headless --script` 探针 + 运行期日志）：`has_char('中'/'文'/'·'/'…'/'◆'/'◇'/'A'/'7')` 全 `true`，
  解析到的实际字体是 **Microsoft YaHei**；本机 `OS.get_system_fonts()` 有 **115** 个字体，其中
  `Microsoft YaHei UI` / `Microsoft YaHei` / `SimHei` / `DengXian` / `SimSun` 都在。
- **英文/数字另配**：`Assets/UI/Fonts/font_ui_kenney_future.ttf`（Kenney Fonts, **CC0**）用在
  `Hud.tscn` 的 `Health` 标签（`theme_override_fonts/font`）—— HUD 的 `HP 120/120` 就是它，科幻方块感。
  想再给别的节点用，复制这一行即可。

### 4.2 ⚠️ 为什么**没有**自带中文字体（诚实的取舍）

任务约束是"**不要下大文件**"。而"能覆盖简中的开源字体"**没有小的**：

| 候选 | 许可 | 单字重体积（**实测**，GitHub API 读的） | 备注 |
|---|---|---|---|
| **Noto Sans SC** | **SIL OFL 1.1**（仓库目录 `ofl/notosanssc` 下带 `OFL.txt`） | `NotoSansSC[wght].ttf` = **17,772,300 B ≈ 17.0 MB**（可变字重，一个文件覆盖全部字重） | 最稳的选择；[google/fonts 目录](https://github.com/google/fonts/tree/main/ofl/notosanssc) |
| **霞鹜文楷 LXGW WenKai** | **SIL OFL 1.1**（仓库声明） | `LXGWWenKai-Regular.ttf` = **25,575,676 B ≈ 24.4 MB** | 楷体气质，与科幻 UI 不搭；[仓库](https://github.com/lxgw/LxgwWenKai) |
| **得意黑 Smiley Sans** | **SIL OFL 1.1**（仓库声明） | 发布包 `smiley-sans-v2.0.1.zip` = **5,781,344 B ≈ 5.5 MB**，**内含 2 个字重** → 单字重 ≈ **2.7 MB** | **体积最小**、风格"斜切动感"，适合标题；正文略吃力。[仓库](https://github.com/atelier-anchor/smiley-sans) |
| 微软雅黑 / 黑体 / 宋体（本机 `C:\Windows\Fonts`） | **专有，不可再分发** | 9~21 MB | **只能像现在这样"按名字引用系统字体"，绝不能拷进仓库** |

> **我没有下载任何一个中文字体**（任务约束）。上面的体积是**读 GitHub release/contents API 得到的真实字节数**；
> 许可结论来自各自仓库的声明与目录（`ofl/` + `OFL.txt`），**我没有逐字读完许可原文**（抓 `LICENSE` 原文时网络超时）。
> 真要发行，下载后**请自己再打开 `OFL.txt` 确认一次**（OFL 1.1 的义务：随附版权声明与许可文本）。

### 4.3 换成自带字体的 3 步（以后谁做都照这个）

1. 下载 `.ttf`（建议 **Noto Sans SC** 可变体，17 MB，一个文件覆盖所有字重）→ 放到 `Assets/UI/Fonts/font_ui_cjk.ttf`
   （或统一挪到 `Assets/Fonts/`，那就把下面第 3 步的路径一起改）。许可原文一并放 `Assets/UI/Licenses/`。
2. **强烈建议先裁剪**：游戏里真正用到的汉字通常不到 1500 个，用 `fonttools` 裁一遍能压到 **1~2 MB**：
   ```powershell
   pip install fonttools brotli
   pyftsubset font_ui_cjk.ttf --text-file=game_text.txt --flavor=woff2 --output-file=font_ui_cjk.woff2
   # --text-file 里放"游戏里出现过的所有字符"（可从 .tscn/.cs 里 grep text = " 得到）
   ```
   Godot **原生支持 `.woff2`**，体积比 ttf 还小。
3. 改 `ui_theme.tres` 的**一行**：把 `default_font = SubResource("SystemFont_ui_cjk")` 换成
   `default_font = ExtResource("N_cjk")`（`N_cjk` 指向新的字体文件）。**`SystemFont` 那个 sub_resource 就可以删了。**

> 裁剪的代价：**以后新增文案出现没裁进去的字会变成豆腐块**。要么把裁剪范围放大（常用 3500 字，约 3~4 MB），
> 要么在 CI 里加一个"检查所有 UI 文案的字符都在字体里"的脚本。

---

## 5. 准星

> **2026-10 用户改回"一个点"**（需求包 §五 #11：「准星改回一个点」）—— 原来的 Kenney 描边十字 + 圆圈已下线。

- 文件：`Assets/UI/Textures/tex_ui_crosshair_dot.png`（**自带**，非外部素材：48×48 实心白圆点，
  半径 2.6px + 1.1px 羽化边，四周透明；用 `System.Drawing` 生成，277 字节）。
- 接在：`Scenes/UI/Hud.tscn` → **`Root/Crosshair`**（`TextureRect`，锚在屏幕中心，`offset ±10` = 20×20，
  `stretch_mode = 5`（保持比例居中），`modulate = (1,1,1,0.9)`）。
- **替掉了什么**：`tex_ui_crosshair_default.png`（Kenney Crosshair Pack 1.1 的 `crosshair-112`，148×148，
  描边十字 + 圆圈）—— 已**整份删除**（含它的 `.import`）。全项目只有 `Hud.tscn` 引过它，删掉是安全的。
  再往前那 3 个 `ColorRect`（`CrosshairH` / `CrosshairV` / `CrosshairDot`）更早就删了。
- **换一个准星**：覆盖 `tex_ui_crosshair_dot.png` 即可（**零改动**，按路径引用）。
  想回到 Kenney 那套就从 `_art_staging/extract/kenney_crosshair-pack/PNG/<变体>/crosshair-NNN.png` 里挑一张
  （变体 4 类：`Light` / `Dark` / `Outline` / `Glow`，每类 1× 与 2× 各 200 张；挑选方法：把 PNG 目录拼成一张带编号的缩略图看）。
- **还没做**：准星不随**散布/瞄准**变化（要写 C# 改 `Crosshair` 的 `scale`/`modulate`），也没有"命中反馈"变体。

---

## 6. 血条（顺手做了一半，剩下的是有意的）

- `Scenes/UI/Bars/BarPlayer.tscn`（左上角玩家血条）：
  - `BarBg`：`ColorRect` → **`NinePatchRect` + `tex_ui_bar_frame.png`**（`patch_margin 20/8/20/8` + `modulate` 压暗）
    = 一条带金属边和端帽的**科幻血条框**。
    ⚠️ **节点名保持 `BarBg` 没动**：代码找的是 `Bg`（`WorldOverlay.HealthBarWidget.BgNode`），
    `BarBg ≠ Bg` 所以它**不会**被代码按尺寸重算 —— 改名会立刻改变行为，**别顺手改**。
  - `Fill` / `DelayBar`：贴图从 `res://icon.svg`（**Godot 图标，而且 `stretch_mode=1` 是平铺** ——
    等于把 Godot 机器人脑袋铺满血条，这是个既有观感 bug）换成 **`tex_ui_bar_fill.png`**（Kenney 血条的中间段，24×24，平铺无缝，带顶部高光）。
    代码对 `TextureRect` 走 `Modulate`，所以**绿→黄→红仍然生效**（截图 `ui_08` 里是满血绿条）。
- **没动（换贴图这件事）**：`BarEnemy.tscn`（敌人脚下血条，本来就是 `ColorRect`，换贴图要动 `Fill` 的 `stretch_mode` 与尺寸换算）、
  `BarBoss.tscn`（首领血条，`ProgressBar` + 预制体里的 `StyleBoxFlat` 填充色，且它的 `Icon` 现在是 Godot 图标占位）。
  三者结构一致，照 `BarPlayer` 的做法抄即可。
  **第六轮起 `BarEnemy` / `BarPlayer` 各多了一层"护甲塑料"（`ArmorBg` / `ArmorFill`，罩在血条上、比血条大 2px/边）**——见 §16.2；
  `BarBoss.tscn` 仍然**一个护甲节点都没有**（首领 0 甲，所以天然不显示任何护甲视觉 —— 这正是 A6 要的"有甲才显示"）。

---

## 7. glow（爆炸 / 枪口火光变亮，不过曝）

**改哪**：`Scenes/Levels/TestArena.tscn` 的 `WorldEnvironment` → `Environment`（**只动这一块**，其它节点一个字节没碰）。

| 属性 | 值 | 为什么 |
|---|---|---|
| `glow_enabled` | `true` | Binbun 的爆炸/枪口特效是**按 HDR + glow 环境调的**，不开 glow 时爆炸"以烟为主"、火光偏暗（上一轮 [art-integration-log.md](art-integration-log.md) §9.4.1 记录的现象） |
| `glow_hdr_threshold` | **2.0** | ⚠️ **关键参数**。这张竞技场的地面（Space Station Kit 地板 + `ambient_light_energy = 0.55` + filmic tonemap）**本来就接近纯白**，阈值 0.9 会把整块地板一起吃进 bloom（实测：整屏泛白、天空褪色 = 过曝）。2.0 只让真正 HDR 更亮的东西（火光、枪口）发光 |
| `glow_intensity` / `glow_strength` | `0.5` / `1.0` | 光晕强度，够看出"在发光"但不糊掉画面 |
| `glow_bloom` | `0.0` | **不加基础 bloom** —— 加了等于给整屏加一层白雾（又一次过曝） |
| `glow_blend_mode` | `1`（Screen） | 比默认的 Additive 温和，亮部不容易顶到死白 |
| `glow_levels/4,5,6` | `0.8 / 0.6 / 0.4` | 只开中/大半径三档 → 光晕是"一团"，不是"整屏发亮" |
| `glow_normalized` | `true` | 多档叠加时按总能量归一，避免越加越亮 |
| 其余 | `glow_hdr_scale = 2.0`、`glow_hdr_luminance_cap = 12.0`、levels 1/2/3/7 = 0 | — |

**试拍过程（4 档参数、同一机位同一帧数）**：`docs/screenshots/ui_glow_trials_{arena,explosion,muzzle}.png`
（左 = glow 关，其余三格 = 0.9 / 1.4 / 2.0 三档阈值）；
**定稿前后对比**：`docs/screenshots/ui_glow_00_before_after.png`（每行左 ON / 右 OFF：爆炸、枪口火光、空场）。

**结论**：爆炸的火球从"一块平黄"变成"白热核心 + 橙色外焰 + 光晕"，枪口火光从"一个小白点"变成"带暖色光晕的亮点"；
空场对比**地板亮度基本不变**（没有整体过曝）。**天空比原来略亮一点点**（星云亮部也在阈值附近），这是这套参数唯一的副作用。

**想再强一点**：把 `glow_hdr_threshold` 降到 1.4（更亮，但天空会明显泛白）；
**想更强又不泛白**：**先把地板材质压暗**（它现在接近纯白才是瓶颈），再降阈值。

---

## 8. 验证（本轮实际跑过的）

| 项 | 命令 / 方式 | 结果 |
|---|---|---|
| 编译 | `& 'F:\Godot项目\项目a\build.ps1' --no-incremental -v minimal` | **0 warning / 0 error** |
| 资源导入 | `Godot.exe --headless --path <项目> --import` | 新 PNG / TTF 全部生成 `.import`，无报错 |
| 主题自检 | `Godot.exe --headless --path <项目> --script res://_ui_probe.gd`（探针脚本，跑完已删） | 主题加载 ✓、`default_font = Microsoft YaHei` ✓、7 个字形 `has_char` 全 true ✓、3 个类型变体的 `base_type` 与 `region_rect` 回读正确 ✓ |
| 界面截图 | `Godot.exe --path <项目> res://Scenes/UI/_UiShowcase.tscn -- ui` | `ui_01_basescreen` ~ `ui_07_loadingscreen`（7 个界面，1920×1080） |
| HUD 截图 | 同上，`-- hud`（挂**真的** `Player.tscn` + `Hud.tscn`） | `ui_08_hud_ingame.png`（`HP 120/120` + 绿色血条框 + 准星）、`ui_09_hud_crosshair.png` |
| glow 前后 | 同上，`-- final`（**用 `TestArena.tscn` 文件里那组参数**，不在运行时注入） | `ui_glow_01..06` + 合成图 `ui_glow_00_before_after.png` |
| glow 试拍 | 同上，`-- glow`（4 档参数 × 空场/爆炸/枪口） | `ui_glowtrial_*` + 3 张合成图 `ui_glow_trials_*` |
| 游戏内自测 | `selftest.flag` + 直接跑 `res://Scenes/Main.tscn` | ✅ `Encounter 清场完成：共生成 4，用时 3s` / `端到端：按 F 之后 奖励界面打开 = True` / `=== 自测结束 ===`，进程 25s 自动退出（code 0）；日志里 **0 条 ERROR / Exception**，只剩 2 条既有 WARNING（导航网格为空、cell_height 不一致） |
| `selftest.flag` | `Remove-Item` + `Test-Path` | ✅ 已删除（复查 False） |

**拍摄台 `Scenes/UI/_UiShowcase.tscn` + `_ui_shot.gd`**（留在仓库里，以后换 UI 素材直接复用）：

```powershell
# 全部：7 个界面 + HUD + glow
& 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe' `
  --path 'F:\Godot项目\项目a' 'res://Scenes/UI/_UiShowcase.tscn'
# 只拍界面 / 只拍 glow 试拍 / 只拍定稿对比 / 只拍 HUD
... 'res://Scenes/UI/_UiShowcase.tscn' -- ui
... 'res://Scenes/UI/_UiShowcase.tscn' -- glow
... 'res://Scenes/UI/_UiShowcase.tscn' -- final
... 'res://Scenes/UI/_UiShowcase.tscn' -- hud
```

> ⚠️ **两个坑（都踩过）**：
> ① 这个拍摄台**直接实例化 `TestArena.tscn`**，所以拍到的 glow 永远是"游戏里真正那一个环境"，
> 不会出现"展示场景自己开了 glow、游戏里没开"的假证据；
> ② **别把 Godot 窗口最小化**（`Start-Process -WindowStyle Minimized` / 窗口被完全遮挡）——
> 不渲染时 `RenderingServer.frame_post_draw` **永远不触发**，拍摄脚本会**卡死**（实测卡了 100 秒以上）。
> 用 `Start-Process` 时不要加 `-WindowStyle`。

---

## 9. 坑与红线（本轮新增的，都是实测）

1. **`SystemFont` 只是开发期占位**：它按 `font_names` 从**玩家机器**上找字体。发行前必须换成自带字体（§4.3），
   否则在没装雅黑的机器上是**豆腐块**。
2. **`theme` 挂在 `Control` 上，不是 `CanvasLayer`**；`ScreenLayer` 那条属性**不许动**（暂停策略）。
3. **血条节点名一个都不能改**（`Fill` / `DelayBar` / `Bg` 而不是 `BarBg`）—— 改名静默失效。
4. **血条的配色仍由代码写**（绿→黄→红），换贴图可以，换配色要改 C#。
5. **`StyleBoxTexture` 的 `texture_margin_*` 是"切角尺寸"**：Kenney 的切角面板 128×128 需要 34px 边距，
   1× 版本（64×64）需要 17px。**边距大于控件高度的一半时会被压缩**，所以按钮（48px）用 1×、
   大面板（460px）用 2× —— 这就是为什么目录里同一个图案有两个尺寸。
6. **`region_rect` 是裁图的利器**：标题带只是按钮图顶部那条 33px 的蓝色横带，用 `region_rect` 裁出来就能当"标题条"用。
7. **改 `.tscn` 手写规矩**（[art-onboarding.md](art-onboarding.md)）：`ext_resource` 在所有 `[node]` 之前、
   `load_steps = ext + sub + 1`、`ext_resource` 只写 `path=` 不写 `uid=`。
8. **glow 的阈值必须按场景调**：这张竞技场的地板接近纯白，阈值照抄网上"0.9~1.2"会直接过曝。见 §7。

---

## 10. 按键提示栏（**写在 UI 基类里，第三批能力系统照着这一节做**）

用户原话：「大部分界面给一栏位置做按键提示（**层级要高且背景透明**，要避免会被遮挡和遮挡其他元素），**写在 UI 的基类里**……多数面板是需要按键提示的，它们**可以不用编辑或显示但不能没有**」。

### 10.1 它是什么 / 在哪

| 问题 | 答案 |
|---|---|
| 类 | `OverlayScreen`（`Scripts/UI/OverlayScreen.cs`）—— **所有弹层的基类**，所以每个界面天生就有 |
| 节点 | `Root/KeyHintBar`（由基类在 `InitializeOverlay()` 里**无条件建出来**；场景里若手写了同名节点就用场景那个） |
| 位置 | 屏幕**底部整条**、水平居中（`anchor_top/bottom = 1`，高 62px、距底 18px） |
| 层级 | ① 它是 `Root` 的**最后一个子节点**；② `ZIndex = 100`。两条独立保证 → 同屏任何元素都压不过它 |
| 背景 | **全透明**：这一栏自己没有任何 `Panel` / `ColorRect`，只有几个小键帽（键帽有底，说明文字没有底） |
| 鼠标 | 整条 + 每个子节点都是 `MouseFilter = Ignore` → **绝不吃鼠标事件**（不会重演"准星挡住镜头"那个坑，见 architecture.md 第 7 条） |
| 默认可见性 | **不填就 `Visible = false`**（结构在树里，但不占视觉）。填了内容才出现 |

### 10.2 API（`OverlayScreen` 的成员，子类直接用）

```csharp
// ① 一次性替换整栏（最常用）。KeyHint 有三种形态 + 两种隐式转换：
SetKeyHints(
    KeyHint.Of(Key.F, "使用"),              // 键盘键 → "F"
    KeyHint.Mouse(MouseButton.Right, "升级"), // 鼠标键 → "鼠标右键"
    KeyHint.Text("1 ~ 5", "选择"),           // 任意文本（组合键 / 长按）
    (Key.Escape, "关闭"));                   // 元组写法： (Key, string) 或 (string, string)

// ② 追加 / 清空 / 整栏显隐
AddKeyHint(Key.Tab, "切换");     // 也有 AddKeyHint(string keys, string caption) 与 AddKeyHint(KeyHint)
ClearKeyHints();                 // 清空（结构仍在）
ShowKeyHints(false);             // 整栏藏起来（比如过场动画时）

// ③ 只读查询（自检 / GDScript 探针直接读）
bool HasKeyHints          // 有没有内容
bool KeyHintsVisible      // 现在画没画
int  KeyHintCount         // 条目数
bool HasKeyHintBar        // 结构在不在树里      ← "不能没有"的机器可判定形式
bool KeyHintBarTopmost    // 是不是 Root 最后一个子节点
int  KeyHintBarZIndex     // 100
bool KeyHintBarIgnoresMouse
protected HBoxContainer UiKeyHintBar   // 想自己塞控件时用（正常情况用 SetKeyHints 就够）

// ④ 验证 / 拍图用（任何界面一行就能看到提示栏长什么样）
DebugSetSampleKeyHints();
```

**什么时候调**：在 `Open()` 里、`BeginOpen()` **之前**（和 `CloseOnCancel` 一起设置最清楚）。
在 `_Ready()` 里 `InitializeOverlay()` **之前**调也不会丢 —— 内容先存着，初始化时统一建。

### 10.3 第三批（能力矩阵界面）该写什么

```csharp
public void Open()
{
    CloseOnCancel = true;
    SetKeyHints(
        KeyHint.Mouse(MouseButton.Left, "激活 / 长按解锁"),
        KeyHint.Mouse(MouseButton.Right, "升级 / 长按重置"),
        (Key.Escape, "关闭"));
    BeginOpen();
}
```

### 10.4 现状（每个界面实际填了什么）

| 界面 | 提示栏内容 |
|---|---|
| `BaseScreen` | `1 ~ 5` 购买永久强化 ｜ `Enter` 出征 ｜ `O` 设置 ｜ `H` 说明 |
| `StartScreen` | `1 / 2 / 3` 选择武器 ｜ `Enter` 默认手枪 ｜ `悬停` 看武器数值 |
| `RewardScreen` | `1 / 2 / 3` 选择奖励 ｜ `悬停` 看说明（结算提示时是 `Enter` 确定） |
| `ChooseScreen` | `1 ~ 4` 选择（允许放弃时多一条 `Escape` 放弃） |
| 帮助分页 | `H` 直达设置的帮助分页或关闭本页 ｜ `Escape` 关闭设置 ｜ `Q / E` 切换分类 |
| `BossPreviewScreen` | `B` 关闭 ｜ `Escape` 关闭 |
| `SettingsScreen` | `O` 关闭 ｜ `Escape` 关闭 ｜ `Q` 上一类 ｜ `E` 下一类 |
| `BuildScreen` | `C` 关闭 ｜ `Escape` 关闭 ｜ `悬停` 看说明 |
| **`AbilityMatrixScreen`** | **鼠标左键 激活 / 长按解锁 ｜ 鼠标右键 升级 / 长按重置升级 ｜ `M` 扩增额度 ｜ 鼠标悬停 看效果与价格 ｜ `Escape` 关闭**（5 条，实测 `条目=5 可见=true ZIndex=100 鼠标穿透=true`） |
| `LoadingScreen` | **没有**（它刻意不继承 `OverlayScreen`：不走界面栈、自己暂停整棵树，见类注释）；加载页也没有可按键的动作 |

### 10.5 验证（本轮实测，探针逐界面输出）

```
[UiShot] 按键提示栏 BaseScreen：节点=true 在树里=true 最后子节点=true ZIndex=100 鼠标穿透=true 条目=4 可见=true
[UiShot] 按键提示栏 SettingsScreen：…（同上，条目=4）
[UiShot] 按键提示栏 LoadingScreen：节点=false（它不继承基类，预期）
[Probe] ① OpenSettings=true … 提示栏条目=4 提示栏可见=true 提示栏最后子节点=true ZIndex=100
```
截图：`ui_01`~`ui_06`、`ui_10`~`ui_13` 每张底部那一栏就是它（`ui_15`/`ui_16` 是**真的 `Open()` 打开后**的内容）。

---

## 11. 全屏化：7 个旧界面全部铺满

用户原话：「其他的 ui 界面**尽可能做成全屏类的界面**」。改动**只动布局，不动逻辑**（节点路径一个都没改，所以 C# 只多了一个 `Settings` 按钮的引用）。

| 界面 | 改前 | 改后 |
|---|---|---|
| `BaseScreen` | 760×460 居中窗 | 面板铺满（左右 48 / 上下 32），标题带横贯整屏，内容垂直居中；永久强化从单列改成 **2 列网格**；按钮 852px 居中；多了「设置」按钮 |
| `StartScreen` | 800×440 | 面板铺满，卡片 1000×96 居中排列 |
| `RewardScreen` | 640×380 | 面板铺满，卡片 1000×92 居中 |
| `ChooseScreen` | 800×420 | 面板铺满，卡片 1000×92 居中 |
| `HelpScreen` | 760×500 | 面板铺满，正文 `size_flags_vertical = 3` 撑满高度、字号 20 |
| `BossPreviewScreen` | 920×660 | 面板铺满，正文撑满 |
| `LoadingScreen` | 已经是全屏 | 没动（本来就是整屏黑底 + 居中文字） |
| `SettingsScreen` / `BuildScreen` | 新界面 | 从第一版就是全屏（左分类栏 + 右内容 / 图标矩阵） |

**两条硬约束**（别踩）：
1. **底部要给按键提示栏留位置**：内容会到屏幕底部的界面（Help / BossPreview / Settings / Build），面板 `offset_bottom = -96`（提示栏占底部 62px + 余量）。改这几个界面时**别把 `offset_bottom` 改回 -32**，否则正文会被提示栏压住。
2. **节点路径不变**：`Root/Panel/VBox/...` 是 C# 与拍摄台按名字取的；`Upgrades` 从 `VBoxContainer` 换成 `GridContainer` 是安全的（代码只按名字取它下面的 5 个按钮），但**名字一个都不能改**。

---

## 12. 全屏设置菜单（`SettingsScreen`）

用户原话：「需要一个**占用全屏设置菜单**，菜单最少应该包含**画面/控制/音量**这几类的设置（**不写死**，避免后面要加的时候大改动）」。

| 项 | 做法 |
|---|---|
| 界面 | `Scenes/UI/SettingsScreen.tscn` + `Scripts/UI/Settings/SettingsScreen.cs`（继承 `OverlayScreen`，`UiLayer.Panel`） |
| 打开 | **Esc / O** ｜ `UiService.Instance.OpenSettings()`；H / `OpenHelp()` 直达帮助分页。无界面时 Esc 的 `OpenMenu` 回调由设置页注册，退树时成对清理。 |
| 结构 | 面板铺满 → 左 `Tabs`（分类） + 右 `PageScroll/Page`（内容，**带滚动**：控制页 13 行在 1080p 下正好装不下） |
| 分类 | **注册表驱动**，见下 |
| 存哪 | `user://settings.cfg`（`ConfigFile`，**玩家机器上的东西不进仓库**）；启动时由 `UiRouter._Ready → SettingsStore.ApplyOnStartup()` 套用音量 / 窗口 / 按键重绑；**画质与 glow 由 `SettingsScreen._Ready → ApplySceneQuality()` 每次进场景套一次**（glow 在 `WorldEnvironment` 上，autoload 阶段还没有场景） |

### 12.1 「分类可扩展」到底怎么加一页（**这是本节的重点**）

```csharp
// 1) 写一个分类（SettingsSections.cs 里有三个现成的例子：画面 / 控制 / 音量）
public sealed class AbilitySettingsSection : ISettingsSection
{
    public string Id => "ability";      // 也是 settings.cfg 的节名
    public string Title => "能力";
    public void Build(VBoxContainer page, SettingsScreen screen)
    {
        SettingsUi.AddTitle(page, "能力");
        SettingsUi.AddOption(page, "默认排序", new[] { "按额度", "按名字" }, 0, index => { /* 落盘 + 生效 */ });
        SettingsUi.AddCheck(page, "显示未解锁", true, value => { });
        SettingsUi.AddSlider(page, "预览规模", 1, 5, 1, 3, v => $"{v:0}", v => { });
    }
}

// 2) 注册一行（放在任何静态构造 / 初始化里；id 相同会覆盖，热重载不会冒两页）
SettingsSections.Register(() => new AbilitySettingsSection());
```

`SettingsScreen` **不知道有哪几个分类**：它只 `SettingsSections.CreateAll()` → 每个分类建一个 Tab（图标 + 标题）→ 切到哪页就调那页的 `Build(page, screen)`。
所以"加一个 section 就多一页"是**真的**，界面代码一行都不用改。自检里可以直接读 `SettingsScreen.SectionCount` / `SectionIds` 证明（本轮实测 `分类数=3 ｜ ids=graphics / input / audio`）。

### 12.2 三页里各有什么（都是**立刻生效 + 落盘**）

| 页 | 内容 |
|---|---|
| **画面** | 显示模式（窗口化 / 全屏 / 无边框全屏）、分辨率（1280×720 ~ 3840×2160）、垂直同步、最大帧率（30~240）、**画质预设**（低 = 渲染 75% / 中 = 默认 / 高 = 2× MSAA）、**glow 开关 + glow 强度**（0~200%，基于场景文件里的原值乘） |
| **控制** | **13 个玩法动作逐条列出当前键位 + 「重绑」**（点重绑 → 按任意键；Esc 取消；另有「恢复默认键位」）。只列 `project.godot` 的玩法动作，**不列 `ui_*` 内置动作**（把 Esc 绑掉会把自己锁在界面里） |
| **音量** | **循环 `AudioServer.BusCount`** 逐条总线（现在 4 条：`Master` 总音量 / `Music` 音乐 / `SFX` 音效 / `UI` 界面音效）各一条滑条 + 静音 + 「恢复默认音量」。**加一条总线这里自动多一组**，只有中文名要在这张表里补一行 |

⚠️ **重绑只写 `user://settings.cfg`**（`InputMap.ActionEraseEvents/AddEvent` 是运行期的），**项目文件不动**。跑自测前若你自己在设置里改过键位，记得删掉 `user://settings.cfg`，否则自测按的键会打不准（自测用 `Input.ParseInputEvent` 合成真实按键）。

### 12.3 证据

| 项 | 结果 |
|---|---|
| 真实打开路径（`UiService.OpenSettings()`） | `[Probe] ① OpenSettings=true IsOpen=true Paused=true MouseMode=0 分类数=3 分类=graphics / input / audio 栈深=1` |
| Esc 关闭（`UiRouter` 路径） | `[Probe] ② Esc 之后 IsOpen=false Paused=false 栈深=0` |
| 截图 | `ui_10_settings.png`（画面）、`ui_12_settings_controls.png`（控制）、`ui_13_settings_audio.png`（音量）、`ui_15_settings_opened.png`（**真的打开**后的样子） |

---

## 13. 图标化（构筑元素 / 卡片 / 分类）

用户原话：「**尽量使用图标去代替文字**，比如**构筑元素都用图标**（这需要**一个面板来显示角色持有**），**只有选取界面和鼠标悬停时显示说明**，**场景里的那些什么强化池这些不需要显示**」。

### 13.1 图从哪来

`Assets/UI/Icons/ic_ui_*.svg`（**13 张，手写 SVG**：矢量、可改、零依赖、Godot 原生导入；颜色写在文件里，换色 = 改一个十六进制）。
名字带 `reward_`（奖励类型）/ `node_`（地面节点类型）/ `set_`（设置分类）/ `build_holdings`。

**唯一映射表 = `Scripts/UI/UiIcons.cs`**：

| 方法 | 用途 |
|---|---|
| `UiIcons.ForReward(RewardDefinition)` | 奖励类型（强化 / 进化 / 异化）→ 图标 |
| `UiIcons.ForNodeKind(RewardNodeKind)` | 地面节点 9 种 → 图标 |
| `UiIcons.ForWeapon(WeaponPresetKind)` | 武器卡片 |
| `UiIcons.ForTitle(string)` | **只有标题字符串**的通用多选一（`ChooseScreen`）：拿 `RewardService.TitleFor` 那张表反查 |
| `UiIcons.ForSection(string)` | 设置分类 Tab 图标（新增分类在这里加一行） |
| `UiIcons.Named(string)` / `DebugLoadedCount()` | 按名字取 / 自检 |

**读不到图返回 `null`**（界面只显示文字，不报错、不抛异常）—— 与"表读不到就用自身默认值"同一条纪律。

### 13.2 三处落地 + 说明什么时候出现

| 位置 | 图标化后 | 说明文字出现在哪 |
|---|---|---|
| **HUD**（`Hud.tscn` 的 `Root/BuildIcons`） | 本局构筑 = **一行小图标**（每格 44×44，叠层数写 `×N`）；**原来那行「构筑：名字／名字」的文字已经删掉** | 不显示（HUD 鼠标是捕获的，悬停没意义） || **角色持有面板**（`BuildScreen`，快捷键 **C**） | 全屏 **图标矩阵**（7 列，112×112 的格子），`GridContainer` 居中 | **鼠标悬停**：`tooltip_text =「名字 ×N（类型）」+ 说明`。面板是 `UiLayer.Panel`（打开即暂停 + 放开鼠标），所以悬停真的能悬停 |
| **选取界面**（`RewardScreen` / `StartScreen` / `ChooseScreen`） | `Button.Icon` = 对应图标（卡片左侧 64×64），文字保留 | **就在卡片上**（用户明确要求"选取界面"可以显示说明） |

**实机证据（走的是真实 `RunBuild`，不是示例数据）**：`ui_14_ingame_hud_real_build.png` ——
`Main.tscn` 跑自测，第 9 阶段（真的走过去按 F、真的选了一条奖励）之后，HUD 左下角就出现那一格构筑图标。
拍摄台 / 探针里的 `DebugSetSampleBuildIcons(count)` 只是"不打通一局也能拍"的捷径，**渲染走的是同一个 `BuildIcon()`**。

### 13.3 场景交互物**不再显示文字**

- `Scenes/Interactables/RewardNode.tscn` 的 `Label`（`Label3D`，"强化池"）**已删除**。
- 基类 `Interactable._Ready()` 里加了 `HideNameplates(this)`：**任何** `Label3D` 子孙都会被置为不可见 —— 以后谁在交互物上挂名牌都不会露出来，而**交互判定 / 范围检测 / 消耗 / 复位一行没动**（`RewardNode.cs` 一行没改，它找不到名牌节点会直接 return）。
- 玩家仍然知道那是哪种池子：**走近按 F**（HUD 的 `按 F 使用：强化池` 提示还在）+ 节点自身的颜色。
- 自检：`[UiShot] 交互物名牌：RewardNode 里的 Label3D 共 0 个（可见 0 个）—— 期望 0 / 0`；
  截图 `ui_08_hud_ingame.png` 里地面节点上方**没有任何浮空文字**。

---

## 14. 还没做的（可以接着推，按性价比排序）

| 项 | 说明 |
|---|---|
| **中文自带字体**（§4.3） | 17 MB（或裁剪到 1~2 MB）。发行前必做，本轮按约束只做了调研 + 占位 |
| `BarEnemy.tscn` / `BarBoss.tscn` 换贴图 | 照 `BarPlayer.tscn` 抄；`BarBoss` 的 `Icon` 还是 Godot 图标占位，可以换成奖励节点的图标 |
| 面板上的**装饰件**没用 | Kenney 包里还有 `panel_glass_tab`（标题凸舌）、`panel_glass_screws`（螺丝）、`panel_glass_notch_{tl,tr,bl,br}`（单角切角）—— 可以叠在面板上做"更花"的界面 |
| 一套**红色/警示**变体 | 现在只有蓝（主）+ 深灰（普通）。加 `UiDangerButton` / `UiWarningLabel` 就能表达"危险/失败"（§3 动作 3） |
| cursor 贴图没用 | 包里 `cursor_a..h` 可以换鼠标指针（`Input.set_custom_mouse_cursor`，要 1 行 C#） |
| UI 音效 | `Assets/Audio/` 有别人放进去的 `ui_click.ogg` / `ui_open.ogg`（Kenney Interface Sounds, CC0）→ 挂到主题的 `Button` 音效或 `UiRouter` 上 |
| 准星随散布变化 | 要写 C#：`Crosshair` 的 `scale` 跟着武器散布走 |
| `_ArtShowcase` 那套"展示台" | UI 也有了自己的展示台（`_UiShowcase.tscn`），建议以后**每个界面改完都跑一次 `-- ui`** 留证据 |

---

## 15. 能力矩阵界面（第三批：能力卡 = 局外装备层，2026-10）

> 用户需求 §二：「大概就是像**卡牌一样展开矩阵排序**，比如 **5×5 / 6×6 / 3×6 / 6×4** 这样的**全屏矩阵**，
> 这样每个元素视觉上都比较大」+ 元素三状态 / 四种交互 / 边框色表等级 / 光圈在边框外 / 预留间隔。
> **数值与状态层怎么工作的看 [ability-cards.md](ability-cards.md)（那一篇是能力系统的唯一入口）；本节只讲外观与交互。**

### 15.1 两个场景

| 场景 | 是什么 |
|---|---|
| `Scenes/UI/AbilityMatrixScreen.tscn` | **满屏矩阵界面**（`CanvasLayer` layer 16 / `ScreenLayer = Panel` → 打开即暂停 + 放开鼠标）。挂在 **`Scenes/Main.tscn`**（与 `BuildScreen` / `SettingsScreen` 并列），快捷键 **K**，或 `UiService.OpenAbilityMatrix()` |
| `Scenes/UI/AbilityCardTile.tscn` | **元素基类组件**（一格 = 一张卡）。界面只负责建格子 / 判定 / 额度栏 / 图例；三状态、交互、读条、震动、缓动、音效**全在元素里**（**占位卡也是这个组件**，见 §15.6） |

**行列不写死**：`Columns = 6`、`Rows = 3`（导出属性，`Rows` 是**最少**行数）= 18 格。
1920×1080 下每格实测 **284×220**（旧版是写死的 232×214）—— 格子尺寸 = 矩阵区 ÷ 行列，**随屏幕/容器自适应**（§15.6）。
**卡不满时补的是"占位卡"**（不是空框）：矩阵永远是**填满的矩形、不翻页**（加到 19 张就自动变 6×4 = 24 格）。

### 15.2 三状态 / 边框色 / 光圈（外观部分）

| 状态 / 元素 | 表现 |
|---|---|
| **未解锁** | 内容 `Modulate = (0.40,0.42,0.48)`（**变暗**）+ 更深的底板 + 等级 0 的**灰**边框 + 状态行写"未解锁" |
| **解锁未激活** | 内容恢复**白色**，边框按等级着色 |
| **激活** | `Glow`（`Panel` + `StyleBoxFlat`：`DrawCenter=false` + 边框 3px + `ShadowSize=10`）**比 `Frame` 大 9px** → **光圈落在边框外**；颜色 = 边框色；`modulate:a` 在 0.55↔1.0 之间**呼吸**（`Tween` 循环） |
| **边框颜色 = 等级** | 灰 0 `#6b7080` → 白 1 `#ebf0ff` → 绿 2 `#59eb6b` → 蓝 3 `#57a3ff` → 紫 4 `#b86bff` → 红 5 `#ff5747`（**唯一一处**：`AbilityCardTile.LevelColor`，图例也从它取色，不抄第二遍） |
| **预留间隔** | 格尺寸随容器自适应（1920×1080 下实测 **284×220**）+ **14px** 行列间隔；光圈往外扩 9px 也不会互相压住 |

**没有用任何新贴图**：底板、边框、光圈全是代码建的 `StyleBoxFlat`（`AddThemeStyleboxOverride`），
所以"换一个等级色"只改 `LevelColor` 一个 `switch`。

### 15.3 四种交互（行为部分，与 [ability-cards.md](ability-cards.md) §5.4 同一张表）

| 操作 | 行为 |
|---|---|
| **左键长按**（未解锁） | 底部出现**读条**（0.7s，`LongPressSeconds` 导出）→ 到 1.0 才判定资源 → 不够**震动元素** / 够则**解锁**并播"略微放大 + 闪一下"（`Tween`） |
| **左键单击**（已解锁） | 激活 / 关闭（占用额度） |
| **右键单击** | 升级（资源不够 → 震动） |
| **右键长按**（等级 > 1） | 读条 → 重置升级进度并**归还升级费** |
| 失败（任意原因） | **震动元素**（绕中心来回摆 + 红闪）+ `ui_card_fail` |
| 音效 | 解锁 `ui_card_unlock` / 升级 `ui_card_upgrade` / 重置 `ui_card_reset` / 激活 `ui_click` / 失败 `ui_card_fail`（都是表驱动，见 §6.3） |
| 说明文字 | **只在鼠标悬停**（`TooltipText`：名字 / 等级 / 状态 / 额度占用 / 下一级效果 / 价格） |
| **占位卡（A5）** | 四种操作**全部**判为 `NoCard` → 走**同一条**失败分支（`FailCount++` + `PlayFailShake()` + `ui_card_fail`）；左键长按照样读满 0.7 秒的条。见 §15.6 |

### 15.4 按键提示栏（照 §10 写的实例）

```csharp
SetKeyHints(
    KeyHint.Mouse(MouseButton.Left, "激活 / 长按解锁"),
    KeyHint.Mouse(MouseButton.Right, "升级 / 长按重置升级"),
    KeyHint.Of(QuotaKey, "扩增额度"),          // M
    KeyHint.Text("鼠标悬停", "看效果与价格"),
    (Key.Escape, "关闭"));
```
实测：`结构在树里=true 条目=5 可见=true ZIndex=100 鼠标穿透=true`。

### 15.5 截图与自检（复现命令见 [ability-cards.md](ability-cards.md) §7）

| 截图 | 内容 |
|---|---|
| `ability_01_matrix_states.png` | **满屏 18 格矩阵**全貌：**六档等级色 + 三状态 + 10 张占位卡**（4 张激活带光圈、3 张解锁未激活、1 张未解锁变暗、10 张占位卡）+ 额度栏 + 图例 + 提示栏 |
| `ability_02_charging.png` | 左键长按**读条中**（进度条可见，`ChargeProgress = 0.40`） |
| `ability_03_unlocked.png` | 读条结束**刚解锁**（Lv1、内容恢复颜色、白边框、播放放大闪一下） |
| `ability_04_active_glow.png` | 右键升级两次 + 左键激活后：**等级色边框 + 边框外光圈** |
| `ability_05_after_interactions.png` | 四种交互 + 故意失败 + 额度扩增之后 |
| `ability_06_ingame_opened.png` | **真的 `Main.tscn`** 里（基地界面之上）打开的能力矩阵 —— `UiService.OpenAbilityMatrix()` 找到界面 = 接线成功 |
| `ability_07_ingame_toggle_key.png` | 合成按键 **K** 打开的那一张（走的是界面自己的 `ToggleKey` 边沿检测） |
| `ability_08_placeholder_feedback.png` | **占位卡被点/长按之后**：那一格正在**抖动 + 红闪**（四种操作全部 `NoCard` → 震动 + `ui_card_fail`，状态一个字节不变） |

拍摄台：`Scenes/UI/_AbilityShowcase.tscn`（摆状态 + 四种交互 + 占位卡反馈 + 音效/缓动，全程沙箱不留痕）、
`Scenes/UI/_AbilityInGame.tscn`（在真的 Main.tscn 里验接线与按键）。
两个都是**留在仓库里**的工具，"以后改能力矩阵再跑一次"即可。

### 15.6 占位卡与自适应（用户 A5）

> **历史规格，已由第二轮 A 批取代**：当前空位不生成节点、每页上限 6×3 并支持翻页。
> `PlaceholderAt` / `PlaceholderCount` / `DebugRunPlaceholderFeedback` 已删除，旧拍摄台也已改用分页测试。
> `AbilityCardTile` 的空行防御仍保留，正常矩阵不会绑定空行。

用户原话：「直接做**满屏矩阵不翻页**，**空的位置也有卡牌占着**，但是会**因为没有设定解锁条件而处于一直无法解锁**，
**自适应大小**，保证后续**永远是填满的矩阵**」。

| 要做的 | 怎么做的（外观/交互视角；数据与判定的完整说明在 [ability-cards.md](ability-cards.md) §5.6 / §6.1） |
|---|---|
| **满屏矩阵、不翻页** | `Matrix` 是 `GridContainer` 且 `size_flags = Fill+Expand`；格数 = `Columns × max(Rows, ceil(卡数/Columns))` —— 永远是**填满的矩形**，不存在第二页。实测算式：`8 张 → 6×3 = 18 格`、`19 张 → 6×4 = 24 格`、`25 张 → 6×5 = 30 格` |
| **空位也是"卡"** | 占位卡是**真的 `AbilityCardTile`**（`Card == null`）：灰边框（压暗一档）+ 底板 + 图标 `ic_ui_card_placeholder.svg`（卡框 + 加号）+ 名字「占位卡」+ 状态「占位卡 · 不可解锁」+ 提示「未设定解锁条件」+ tooltip 教你把它变成真卡 |
| **一直无法解锁** | 卡表里**没有对应行** → 宿主对四种操作一律返回 `NoCard`；它没有等级、不进 `UnlockedCount` / `ActiveCount` / `QuotaUsed` 任何一个统计 |
| **但交互反馈照旧** | 点它 / 长按它照常读条、**震动**（`PlayFailShake`）、**失败音效**（`ui_card_fail`）—— 走的是**同一条** `RunAction` 失败分支，不是为占位卡写的第二套 |
| **自适应大小** | 场景里**不再有写死的格子尺寸**：元素 `custom_minimum_size = 120×110` 只是"能缩到多小"，靠 `Fill+Expand` 铺满；实测 1920×1080 下 `矩阵区 1772×686 → 每格 284×220px`（= 矩阵区按 6 列 3 行分摊，含 14px 间隔） |

机器可判定证据（拍摄台逐行打印，见 [ability-cards.md](ability-cards.md) §7.3）：
`列 6 × 行 3 → 18 格（真卡 8 + 占位卡 10）`、`满屏矩阵算式 … → 全部满足「填满矩形、不翻页」 = True`、
`占位卡交互：Unlock → NoCard ｜ ToggleActive → NoCard ｜ Upgrade → NoCard ｜ ResetUpgrades → NoCard
｜ 反馈：失败（震动 + 失败音效）4 次 / 成功 0 次 ｜ 失败音效 last=ui_card_fail`。

---

## 16. 表现批（第六轮：跳字分类 / 护甲表现 / 交互表现 / 基地换装）

> 本轮做的是需求包 §五的 **#9 伤害数字分类**、**#8 护甲表现**、**#13 交互表现**、**#4 基地实时换装**
> （**#3 死亡流程单独一篇：[death-flow.md](death-flow.md)**）。
> 原则：**只加表现，不碰判定**。伤害管线（`Scripts/Combat/Unit.ReceiveHit`）、交互判定（`Interactable`）、
> 数值计算**一行都没有改**（`RunController` 动了两处，逐行说明见 [death-flow.md](death-flow.md) §3）。

### 16.1 #9 跳字分类（**伤害 / 甲 / 盾 / 格挡 / 无敌 / 其他伤害 / 恢复**）

用户原话：「对敌人的跳数字要有不同的显示效果：**伤害 / 甲 / 恢复 / 其他伤害（电/燃烧）/ 格挡 / 无敌**等」。

**每一类的数据来源都是既有事实**（`Scripts/UI/DamageNumbers.cs` 的类注释里也写了同一张表）：

| 类别 | 数据来源（**不是新造的一套**） | 颜色 / 前缀 |
|---|---|---|
| 伤害 | `DamageResult.HealthLost`（`Unit.Damaged` 事件带出来的那次结算结果） | 暖黄 `#ffd959` |
| 暴击 | 同一处的 `DamageResult.Critical` | 橙 + 字号 ×1.35 + 更粗描边 |
| 甲 | `DamageResult.ArmorLost` | 冷钢蓝 `#b8dbff`，文字「甲 N」 |
| 盾 | `DamageResult.ShieldLost` | 蓝 `#8cb8ff`，文字「盾 N」 |
| 格挡 | `DamageResult.Blocked`（数值 = 甲+盾+血的实际总损失） | 淡蓝白 `#d9e6ff`，文字「格挡 N」 |
| 无敌 | `GameplayEventKind.Hit` **之后**读 `Unit.IsInvulnerable`（命中事件在无敌判定之前抛出，而这时它还是 true；无敌时**不会**走 `Damaged`） | 灰白 `#c7d1eb`，文字「无敌」（**节流 0.35s**，冲刺无敌帧会被连续命中） |
| 其他伤害（电/燃烧） | `HitInstance.Attack == null && Attacker == null`（状态效果的每秒伤害与环境直伤就是这样造的实例；普通攻击一定带 `AttackInstance`），认得出 `Burn` 就写「燃 N」 | 洋红 `#ff80d9` |
| 恢复 | **`Unit.Heal` 不发任何事件**（Combat 只读），所以由已经在轮询血量的 `PlayerHud`（20Hz）发现"血量比上次高"时调用 `DamageNumbers.ShowHeal` | 绿 `#73ff8c`，文字「＋N」 |

**同一次命中最多同时出 3 个数字**（甲 + 盾 + 伤害 / 格挡），靠 `DamageNumbers.SlotOffsetOf()` 上下错开 —— 不会叠在一个像素上。
"我方打出的"与"敌人打出的"区别从字体颜色挪到了**描边色**（因为字体颜色已经被"类别"占用了）。

⚠️ **为什么必须订阅每个单位的 `Unit.Damaged`**：`GameplayEventBus` 的 `Damaged` 事件里 `Value` **只有 `HealthLost`** ——
「甲 / 盾 / 格挡 / 暴击 / 这次伤害是不是燃烧」全都在 `DamageResult` 上。订阅是在**第一次命中**时建立的
（`GameplayEventKind.Hit` 先于结算抛出，所以连第一次命中的甲数字都不会漏），死亡 / 退树时成对退订。

### 16.2 #8 护甲进度与碎甲（血条 + 模型）—— **2026-10 按用户 A6 改版：那层"塑料"**

用户原话（A6）：「关于护甲条，**要有护甲才会被显示出来**，期望上是**有一圈半透明遮罩遮住血条**，
就像**血条包裹了一层塑料**，且**比血条大一点点**」。
（改版前是在血条**上方**加一对独立的 `ArmorBg` / `ArmorFill` 甲条 —— 那个形式已经删掉。）

**两个节点，一层"塑料"**（`Scenes/UI/Bars/BarEnemy.tscn` 的 `Content/ArmorBg` + `Content/ArmorFill`，
`BarPlayer.tscn` 的 `ArmorBg` + `ArmorFill`；**都排在 `Fill` / `DelayBar` 之后**，所以真的盖在血条上）：

| 节点 | 类型 | 是什么 | 尺寸 |
|---|---|---|---|
| `ArmorBg` | `Panel` + `StyleBoxFlat` | **那层塑料壳**：半透明冷钢蓝底（alpha 0.14）+ **1px 高亮边框**（alpha 0.55）+ 圆角 | **比血条大 2px/边**（玩家 268×24 vs 血条框 264×20；敌人 70×13 vs 血条底 66×9） |
| `ArmorFill` | `ColorRect`（冷钢蓝 alpha 0.45） | 壳里那层**磨砂的"甲"**：宽度 = 剩余甲比例，**从右往左退**，甲退掉的地方露出血条本色 | 满宽同 `ArmorBg` |

- **有护甲才显示**（A6 的核心）：`Armor > 0` 才 `SetArmor(..., visible: true)` ——
  **首领（0 甲，`BarBoss.tscn` 根本没有护甲节点）/ 白板怪 / 0 甲角色，一点护甲视觉都没有**（连壳都不给）。
  两个节点在预制体里默认 `visible = false`，所以"还没有玩家/单位"的那几帧也不会闪出一条空壳。
  实测：`无甲单位：Chaser 生命 60 甲 0/0 … 壳 隐藏(70×13) 甲填充 隐藏(70px)`。
- ⚠️ **判据在 2026-10-12 第三轮 C1 改过一次**：原来是 `MaxArmor > 0`（"有护甲上限才显示"），
  现在是 **`Armor > 0`**（用户：「**有甲了才会显示**，**护甲没有上限额度**，所以可以删除掉 `MaxArmor`」）。
  比例的分母也从 `MaxArmor` 换成**运行期派生的 `Unit.ArmorPeak`**（这个单位见过的最高甲，不是配置项）。
- **甲打光之后整条隐藏（C1 的结论）**：`Armor == 0` 时 `ArmorFill` 与 `ArmorBg` **一起隐藏**、**不留空壳** ——
  这正是用户要的"甲打光了就不显示"。⚠️ 与 C1 之前的行为不同（以前有甲上限的单位甲打光会留一层空壳）。
- **碎甲**：`Unit.ArmorBroke`（只在甲从 >0 掉到 0 时触发）→ 那层塑料**闪白**（`FlashArmorBreak`）
  + 模型上 `ArmorBreakFx`（水平环 + 亮壳，0.42s 自回收）。**改版没有动它**（C1 也没动）。
- ⚠️ `FlashArmorBreak` 的还原色读的是节点的 **`Modulate`**（不是 `Color`）：新样式的 `Color` 自带染色与透明度，
  还原成 `Color` 会把染色乘两遍（`ColorRect` 型的护甲条会越闪越暗）。
- ⚠️ **"只掉甲、没掉血"也会出血条**：`GameplayEventBus.Damaged` 的 `Value` 是 `HealthLost`（甲吸收时为 0），
  所以登记血条多了一条"甲/盾也掉了"的判据；**无甲单位行为与改动前逐位一致**。
- 细节与信号来源见 [combat.md](combat.md) 的「护甲的表现层」，判定（扣甲顺序 / 减伤）**一行没动**。

**截图**（`_FeelShowcase.tscn`，mode=arena）：

| 截图 | 内容 |
|---|---|
| `feel_01_armor_progress.png` | 有甲敌人（甲 45/60）脚下的血条：罩着半透明塑料、磨砂"甲"占了 75% |
| `feel_01b_armor_none.png` | **同屏对照**：有甲木桩（有塑料）vs 无甲追猎者（`ArmorPeak = 0`，**一条护甲视觉都没有**） |
| `feel_01c_player_armor.png` | 玩家 HUD 血条（甲 39/60、血满）：血一点没掉也看得见护甲进度 |
| `feel_02_armor_break.png` | 碎甲：模型上一圈扩散环 + 血条上的空壳 |
| **`armor_a6_zoom.png`** | **放大对照图**（从上面几张真实截图裁剪 + 最近邻放大，不是新渲染）：有甲敌人血条 / 无甲敌人血条 / 玩家血条 |

机器可判定输出（实测）：

```
[Feel] 无甲单位：Chaser 生命 60 甲 0/0（ArmorPeak = 0 → 期望任何护甲视觉都隐藏）
[Feel] 护甲视觉[有甲木桩 vs 无甲追猎者（血条按登记顺序）] 血条 2 条：
       壳 显示(70×13) 甲填充 显示(52.5px) 血条底 66×9 血 64px ／
       壳 隐藏(70×13) 甲填充 隐藏(70px) 血条底 66×9 血 51.2px
[Feel] 玩家血条护甲遮罩：壳 显示(268×24) 甲填充 显示(174.2px) 血条框 264×20 血 260px（壳比血条框每边大 2px）
[Feel] 碎甲模型特效：ArmorBreakFx 节点 1 个（0.42 秒后自己回收） → 0 个
[Feel] 敌人血条[碎甲（下一帧更新后）]：护甲遮罩 0px / 整条 70px（比例 0） 遮罩可见=True 血条 52.8px
```

### 16.3 #13 交互表现（描边 + 按键图标 + 失败响应）

- **组件**：`Scripts/Interaction/InteractablePresentation.cs`（约定节点名 `Presentation`）。
  与 `UnitAnimator.Ensure` 同一条做法：`Interactable._Ready()` 里 `InteractablePresentation.Ensure(this)` ——
  **任何交互物（现有的/新写的）不需要改预制体就自动有表现**。
- **描边**：给外观网格挂一个"反向外壳"子网格（同一份 `Mesh` + 只画背面的发光材质 + `Grow`），
  **不需要后处理、不需要改预制体**；只在"目标"身上可见。
- **按键图标**：`Sprite3D` + `Assets/UI/Icons/ic_ui_key_f.svg`（手写键帽 + 「F」，项目自制），
  始终朝向镜头、`no_depth_test`、浮在网格顶部（高度按 `Mesh` 的 AABB 算）并轻轻上下浮动。
- **只看最近的那一个**：目标由 `PlayerInteractInput.Current` 决定（玩家端每帧挑的"范围内最近的可交互物"，
  只读属性）。两个节点挨在一起时不会同时高亮。
- **失败响应**：`Interactable.Failed` / `Interactable.Rejected` → `AudioManager.Play(AudioIds.InteractFail)`
  + 抖外观网格（**不是根节点**：根节点的 scale 被出现/消失补间占用）+ 描边闪红 + 玩家被略微击退
  （`CharacterMovement.AddImpulse`，默认冲量 4.5 → 实测位移 ≈ **0.64 米**，方向背离交互物）。
- **音效复用**：`AudioIds.InteractFail = "ui_card_fail"`（能力卡那条"操作被拒"的低频嗡）——
  **不新增素材、不新增音效表行、不动 `ConfigIndex.tres`**；想换成专门的一条，照 [audio.md](audio.md) §5 加行再改这个字符串。

### 16.4 怎么保证"没动交互判定"（可逐条核对）

| 判定/流程 | 这轮有没有碰 |
|---|---|
| `Interactable._PhysicsProcess` 的范围检测（10Hz、`PlayerInteractRange`） | **一行没改** |
| `CanInteract`（`!Used && !Vanished && PlayerInRange && Enabled`） | **一行没改** |
| `Interact()` 的挑战者拒绝分支（`StageController.ChallengerActive`） | 判定一行没改，只在**返回 false 之前**多抛了一条 `Failed` 通知 |
| `Interact()` 的失败分支 `if (!CanInteract \|\| !OnInteract())` | 判定与顺序一行没改，只在 `return false` 前多抛了一条 `Failed` 通知 |
| 消耗（`SingleUse` → `Used = true` → `OnUsed()`） | **一行没改** |
| `ResetNode()` / `Appear()` / `Disappear()` / `FinishDisappear` | **一行没改** |
| 新增的东西 | ① `public event Action? Failed`；② `Failed?.Invoke()` 一行；③ `_Ready()` 里 `Ensure` 一行；④ `VisualMesh` 从 `protected` 放宽到 `public`（表现层要拿它做描边与抖动）；⑤ `PlayerInteractInput.Current` 只读属性（选择逻辑没改） |

机器可判定证据（拍摄台打印）：`交互返回=False`（场景里没有 StageController）→ `失败计数=1`
+ `失败音效：last=ui_card_fail 累计=1 缺失 id=[]` + `玩家被击退：0.64 米` —— 而 `当前目标/高亮/描边可见/图标可见` 全程不乱。

### 16.5 #4 基地里选装备/角色 → 玩家实时切换

用户原话：「在基地时选取了什么装备/角色时，**玩家就切换到相应的目标**，方便进关卡前熟悉角色」。

| 环节 | 做法 |
|---|---|
| 选取界面 | 装备配置台（`BaseTerminal.Kind = Loadout`）→ **先选角色**（`ChooseScreen`，基地场景新挂了它）→ **再选武器**（`StartScreen`）。角色卡文案复用 `RunController.CharacterCardText`（与关卡 Configure 步**同一套来源**） |
| 切换本体 | `Scripts/UI/LoadoutPreview.cs`（挂在 `Scenes/Levels/Base.tscn` 的玩家下）：盯 `RunSetup.Character/Weapon` 变化 → 换模型 / 套数值 / 收放枪械 |
| 模型从哪来 | 角色表 `Config/Tables/Characters/chr_*.tres` 的 `ModelPath`（**新增两行**：重装 → `Assets/Models/player/mesh_player_body_heavy.glb`，召唤 → `..._summoner.glb`）；留空 = 场景自带那个（枪手就是这一档） |
| 为什么不调 `UnitConfig.ApplyTo` | 它里面的换模型是"`Free()` 旧模型 + `AddChild` 新实例"，而 `UnitAnimator` 已经绑在旧的 `Model/AnimationPlayer` 上 → 运行期会留下指向已释放节点的引用（每帧报错）。这里改成**每个角色各留一份预览模型、靠 `Visible` 切换**，不释放任何既有节点 |
| 数值 | `Unit.ApplyBaseStats`（生命/护甲/护盾/移速）+ `DamageReduction`，与关卡里 `RunController.ApplyBalance` 同一个入口；**不叠加局外强化**（那是出征才算的） |
| 枪械 | 角色行 `WeaponEnabled = false`（近战重装）→ 收起基地玩家的 `WeaponMount/Pistol`；武器表那一行也套到基地玩家的 `Weapon` 组件上 |
| ⚠️ 一个真实时序坑 | `LoadoutPreview._Ready` 可能**早于配置表建好**（实测 `ConfigIndex` 的索引晚约 1 秒就绪）→ 那一次读不到角色行，所以组件记下 `_rowMissing` 并在下一帧自动补一次（否则基地玩家永远是场景默认数值 120/0） |

⚠️ 模型复用说明（**照实记录**）：`mesh_player_body_heavy.glb` = Kenney Mini Characters 的 `character-male-e`（与守卫同一个模型）、
`..._summoner.glb` = `character-female-d`（原包里唯一没被任何敌人用掉的那个）。素材来自**项目已经用着的那一包**（CC0），
要换专用模型时**只改角色表的 `ModelPath` 一列**，代码一行不用动。

### 16.6 验证：三个拍摄台 + 逐条探针输出

> 与 `_UiShowcase.tscn` 同一套思路（命令行直跑 + 截图 + 打印），**不依赖 MCP**。
> 拍摄台本身留在仓库里：`Scripts/Testing/_FeelProbe.cs`（`Mode = arena / base / death`）+ 三个场景。

```powershell
$godot = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe'
& $godot --path 'F:\Godot项目\项目a' 'res://Scenes/UI/_FeelShowcase.tscn'   # #8 #9 #13
& $godot --path 'F:\Godot项目\项目a' 'res://Scenes/UI/_FeelBase.tscn'       # #4（真的 Base.tscn + 真的交互台）
& $godot --path 'F:\Godot项目\项目a' 'res://Scenes/UI/_FeelDeath.tscn'      # #3（继承自 Main.tscn，见 death-flow.md）
```

实测输出（关键几行；`[Feel]` 行是这三个场景**直接跑时的控制台输出**（它们走 `GD.Print`，不写进 `user://logs`），
第六轮原版与 **A6 改版后**的两批都在下面，三个场景 stderr 里 **0 条 ERROR**）：

```
[Feel] 木桩：生命 600 甲 60/60 减伤 0.25 ／ 无甲单位：Chaser 生命 60 甲 0/0（ArmorPeak = 0 → 期望任何护甲视觉都隐藏）
[Feel] 命中「普通命中（甲吸收）」：原始 20 → 最终 15（甲 15 / 盾 0 / 血 0 格挡=False 无敌=False）
[Feel] 跳字[甲吸收] 1 个：「甲 15」@b8dbff
[Feel] 敌人血条[甲吸收]：护甲遮罩 52.5px / 整条 70px（比例 0.75） 遮罩可见=True 血条 64px｜壳 70×13 vs 血条底 66×9   ← 血一点没掉也有遮罩
[Feel] 护甲视觉[有甲木桩 vs 无甲追猎者] 血条 2 条：壳 显示(70×13) 甲填充 显示(52.5px) … ／ 壳 隐藏(70×13) 甲填充 隐藏(70px) …
[Feel] 跳字[碎甲] 2 个：「甲 45」@b8dbff 「105」@ffd959
[Feel] 碎甲模型特效：ArmorBreakFx 节点 1 个（0.42 秒后自己回收） → 0 个
[Feel] 敌人血条[碎甲（下一帧更新后）]：护甲遮罩 0px（比例 0） 遮罩可见=True 血条 52.8px     ← 空壳还在
[Feel] 跳字[格挡] 3 个：「无敌」@c7d1eb 「甲 7」@b8dbff 「格挡 7」@d9e6ff
[Feel] 跳字[燃烧] 1 个：「燃 6」@ff80d9 ／ [Feel] 跳字[直伤（有攻击者）] 2 个：「燃 6」「30」@ffd959
[Feel] 恢复跳字（HUD 层）：1 个：「＋35」@73ff8c
[Feel] 交互表现[靠近]：当前目标=RewardNode 同一个=True 高亮=True 描边可见=True 按键图标有=True 图标可见=True 失败计数=0
[Feel] 交互表现[失败瞬间]：… 失败计数=1  ／ 失败音效：last=ui_card_fail 累计=1 缺失 id=[]
[Feel] 玩家被击退：0.636 米（方向背离交互物，冲量 4.5）  ／ 图标表：22 张可加载（含按键图标 ic_ui_key_f）
[Feel] 玩家护甲：39/60（血 120/120 —— 血没掉也要看得见护甲进度）
[Feel] 玩家血条护甲遮罩：壳 显示(268×24) 甲填充 显示(174.2px) 血条框 264×20 血 260px（壳比血条框每边大 2px）
[Feel] 装备配置台：找到=True 玩家在范围内=True 交互接受=True ／ 选角色界面打开=True 按键提示=2 条
[Feel] 按 2 选角色后：RunSetup.Character=Heavy 武器界面打开=True
[Feel] 基地预览[重装 + 狙击枪]：模型=[.../mesh_player_body_heavy.glb] 预览实例=1 枪可见=False 玩家生命=380 甲=140
[Feel] 基地预览[召唤 + 手枪]：模型=[.../mesh_player_body_summoner.glb] 预览实例=2 枪可见=True 玩家生命=210 甲=60
```

| 截图 | 内容 |
|---|---|
| `feel_01_armor_progress.png` | 有甲敌人血条：**罩着一层半透明塑料**（甲 45/60 → 磨砂"甲"占 75%）+ 「甲 15」（近景） |
| `feel_01b_armor_none.png` | **有甲 vs 无甲同屏对照**：木桩（有塑料）/ 追猎者（`ArmorPeak = 0`，**一条护甲视觉都没有**） |
| `feel_01c_player_armor.png` | 玩家 HUD 血条（甲 39/60、血满）：血一点没掉也看得见护甲进度 |
| `armor_a6_zoom.png` | **放大对照图**（真实截图裁剪 + 最近邻放大）：有甲敌人 / 无甲敌人 / 玩家 三条血条 |
| `feel_02_armor_break.png` | 碎甲：模型上一圈扩散环 + 「甲 45」「105」 |
| `feel_03b_invuln_block.png` | 「无敌」（木桩）+「格挡 7」（守卫） |
| `feel_03_number_kinds.png` | 「燃 6」（其他伤害）+「60」（伤害）+「＋35」（恢复） |
| `feel_04b_outline_off.png` / `feel_04_interact_highlight.png` | 同一机位的**对照**：关掉 / 打开描边 + 按键图标 |
| `feel_05_interact_fail.png` | 失败响应：描边闪红 + 护盾式的抖动（同一瞬间还播了 `ui_card_fail`） |
| `feel_06_base_choose_character.png` | 基地配置台第一步：选角色（全屏卡片） |
| `feel_07_base_choose_weapon.png` | 第二步：选武器 |
| `feel_08_base_heavy_preview.png` | 选完「近战重装 + 狙击枪」：基地玩家**换成重装模型、枪收起**、生命 380 / 甲 140 |
| `feel_09_base_summoner_preview.png` | 再选「召唤控制 + 手枪」：模型再换、枪回来、生命 210 / 甲 60 |
| `feel_10`~`feel_13` | 死亡流程四张（见 [death-flow.md](death-flow.md) §4） |



