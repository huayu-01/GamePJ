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
| 准星接在哪？ | `Assets/UI/Textures/tex_ui_crosshair_default.png`（Kenney 200 个里挑的第 112 号，`Outline` 描边版，148×148）→ `Scenes/UI/Hud.tscn` 的 **`Root/Crosshair`**（`TextureRect`，屏幕中央 56×56）。**原来那 3 个 `ColorRect`（`CrosshairH`/`CrosshairV`/`CrosshairDot`）已删除**；它们没有任何代码引用，删掉是安全的。 |
| glow 开了吗？ | 开了：`Scenes/Levels/TestArena.tscn` 的 `WorldEnvironment` → **`glow_enabled = true`、`glow_hdr_threshold = 2.0`、`glow_intensity = 0.5`、`glow_blend_mode = 1`（Screen）、只开 4/5/6 三档半径**。为什么阈值这么高见 §7。 |
| 改了几个界面？ | **外观全部 8 个（主题级）+ 2 个重点做了完整示范**（`BaseScreen` 加了标题带 / 分隔条 / 主按钮，`HelpScreen` 验证长中文正文）。没有把 7 个弹窗逐个"铺满"——这是刻意的，见 §3。 |
| 想一次看全部证据？ | [art-integration-log.md](art-integration-log.md) 的 **第五轮** 一节：逐项改动 + 全部截图 + 自测输出。 |

---

## 1. 文件清单（本轮新增）

```
Assets/UI/                                   ← 本轮新建（assets.md §1 的 UI/ 目录）
  Textures/tex_ui_panel_frame.png            128×128  大面板底（切角九宫格）
  Textures/tex_ui_panel_frame_small.png       64×64   小面板底 / 焦点框（同上，1×）
  Textures/tex_ui_panel_glass.png            128×128  圆角玻璃面板底（备选，本轮未接线）
  Textures/tex_ui_panel_glass_small.png       64×64   按钮底（所有普通按钮）
  Textures/tex_ui_header_band.png            192×64   标题带 / 主按钮（蓝色横带那条）
  Textures/tex_ui_bar_frame.png              192×48   分隔条 / 血条边框
  Textures/tex_ui_bar_fill.png                24×24   血条填充（平铺，会跟着代码染色）
  Textures/tex_ui_crosshair_default.png      148×148  准星
  Fonts/font_ui_kenney_future.ttf                      HUD 数字字体（Kenney Future, CC0）
  Themes/ui_theme.tres                                 主题（唯一入口）
  Licenses/license_kenney_{ui_pack_scifi,crosshair_pack,fonts}.txt   CC0 原文（照抄进仓库）

Scenes/UI/_UiShowcase.tscn + _ui_shot.gd     ← 验证工具（见 §8）：一次运行拍完 7 个界面 + HUD + glow 前后
```

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
| `UiPrimaryButton/styles/*` | `StyleBoxTexture_primary_*` | 同一块蓝色横带铺满整个按钮 = 主行动按钮 |
| `UiCaption/colors/font_color`、`font_size = 15` | — | 次要说明文字（灰、无描边） |

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

- 文件：`Assets/UI/Textures/tex_ui_crosshair_default.png`（Kenney **Crosshair Pack 1.1**，CC0；200 个里挑的
  **`PNG/Outline (2×)/crosshair-112.png`**，148×148 —— 白色形状 + 半透明黑描边，**在亮地板和暗天空上都看得见**）。
- 接在：`Scenes/UI/Hud.tscn` → **`Root/Crosshair`**（`TextureRect`，锚在屏幕中心，`offset ±28` = 56×56，
  `expand_mode = 1` + `stretch_mode = 5`（保持比例居中），`modulate = (1,1,1,0.9)`）。
- **替掉了什么**：原来 3 个 `ColorRect`（`CrosshairH` / `CrosshairV` / `CrosshairDot`）拼的 `+`。
  已确认**全项目没有任何 C# 引用**这三个名字（`grep Crosshair Scripts/**/*.cs` 只命中 `AimUtil.GetCrosshair*` 两个方法名，那是"准星方向"不是节点）。
- **换一个准星**：从 `_art_staging/extract/kenney_crosshair-pack/PNG/<变体>/crosshair-NNN.png` 里挑一张，
  覆盖 `tex_ui_crosshair_default.png` 即可（**零改动**）。变体 4 类：`Light` / `Dark` / `Outline` / `Glow`，
  每类有 1× 和 2×（retina）两套、各 200 张。挑选方法：把 PNG 目录拼成一张带编号的缩略图看（本轮就是这么挑的）。
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
- **没动**：`BarEnemy.tscn`（敌人脚下血条，本来就是 `ColorRect`，换贴图要动 `Fill` 的 `stretch_mode` 与尺寸换算）、
  `BarBoss.tscn`（首领血条，`ProgressBar` + 预制体里的 `StyleBoxFlat` 填充色，且它的 `Icon` 现在是 Godot 图标占位）。
  三者结构一致，照 `BarPlayer` 的做法抄即可。

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

## 10. 还没做的（可以接着推，按性价比排序）

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
