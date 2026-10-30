# 界面与素材接入

2026-10-28：奖励、构筑与图鉴已接入简化毛玻璃局部主题 `GlassUi`。奖励卡中央灰透明、边缘磨砂，卡名按类别着色、右上角用类别图标，描述不随类别变色；前置和互斥集中在图鉴并以赭／紫标题区分。三页底部显示实际按键，不显示状态示例；覆盖期间隐藏战斗HUD，关闭恢复。奖励Enter／Space确认当前焦点并防重复提交，原候选和授予机制保留。截图、验证与未覆盖范围见[047／048实施](reviews/20261028/visual-047-048-current.md)。

本页是当前 UI 结构、主题和换肤契约。旧五／六轮接入过程、旧角色截图、系统字体占位方案及探针原文见 [历史原文](archive/reference-before-2026-10-21/ui.md)；原始截图、批次报告和失败记录均保留在原路径。

## 界面入口与职责

| 界面 | 当前用途／默认入口 |
|---|---|
| `StartScreen` | 当前角色允许的武器页；角色展台切人后打开 |
| `BaseGuide`／`ExpeditionScreen` | 可收起基地指引；出击门汇总当前角色、武器、符文和激活能力卡，确认后出发 |
| `RewardScreen`／`ChooseScreen` | 局内奖励、商店等必须完成的选择或消息 |
| `AbilityMatrixScreen` | 能力卡解锁、升级、激活、额度；K |
| `RuneScreen` | 基地符文台选择携带；局内不换符文 |
| `BuildScreen` | 当前持有构筑和说明；C |
| `BossPreviewScreen` | 首领状况、技能／词条、隔离次数和压力；B |
| `SettingsScreen` | 画面、控制、音量、帮助；O／Esc，H直达帮助 |
| `DeathScreen` | 死亡动作可跳过，结算展示等待玩家确认后回基地 |
| `PlayerHud`／`WorldOverlay` | 当前技能、资源、准星、交互提示、血条和跳字 |
| `ChallengeStatusPanel` | 右上挑战预算／波次／本场存活；真正清场后短暂显示完成，不抢输入 |

基地是可移动、可战斗练习的真实场景；旧 `BaseScreen` 永久强化页和关卡内额外 Configure 入口已退出正式流程。玩家技能与 HUD 共读 `Unit.AbilitySlots`，不能通过遍历库存技能或读取旧角色枚举猜测显示内容。

## 分层、输入和暂停

- `OverlayScreen` 是弹层基类。`Hud` 不暂停、不拦输入；`Panel`／`Modal` 由 `UiRouter` 统一管理暂停与鼠标。是否允许 Esc 关闭由 `CloseOnCancel` 决定。
- 业务调用 `UiService` 的语义方法，服务按类型查找并缓存实际界面；跨场景缓存要失效。
- Esc 交给路由处理有效顶层界面；保护期内消费按键，不向下关闭其他面板。进行中的冒险若顶层是必须选择的奖励／商店，Esc可在其上打开设置，关闭设置后原选择继续。设置内可确认取消本次冒险并恢复出击前状态。不要在换肤时修改 `ScreenLayer` 或绕过打开保护期。
- 出击摘要的确认、返回和 Enter 共用顶层身份检查；上面打开设置后不能触发底层出击，关闭设置后摘要继续暂停。
- `SetKeyHints` 提供底部提示栏，结构始终存在，可以不填内容。整栏与纯显示 HUD 使用 `MouseFilter=Ignore`，不能遮挡镜头和操作输入。
- 玩法键读取 InputMap 和用户设置；提示应说明当前槽的真实含义。界面快捷键当前由相应界面组件持有。

HUD左上常驻携带符文图标与名称；关卡内读取本局已应用的符文，基地读取当前携带配置。未携带时明确显示，切换后不残留旧图标。取消冒险、雷池与状态表现说明见[冒险回退与联动](adventure-synergy-20261024.md)。

## 主题、字体与贴图

| 资源 | 当前入口 |
|---|---|
| 主题 | `Assets/UI/Themes/ui_theme.tres` |
| 中文字体 | `Assets/UI/Themes/ui_font.tres` → 项目内 `Assets/Fonts/NotoSansSC/NotoSansSC.ttf` |
| 纹理 | `Assets/UI/Textures/tex_ui_*.png` |
| 通用图标映射 | `Scripts/UI/UiIcons.cs` 与 `Assets/UI/Icons/` |
| 符文图标 | `Assets/UI/Icons/Runes/`；符文配置行引用 |
| 准星 | `Hud.tscn` 的 `Root/Crosshair`，当前使用 `tex_ui_crosshair_dot.png` |

中文字体已经随项目提供，旧“发行前仍需下载中文字体／仅依赖雅黑”的段落属于历史。项目全局字体与主题字体共用当前资源；新增文字应检查字体覆盖及实际小字号可读性。

换肤步骤：

1. 将贴图、字体和许可放入约定目录，保留原始来源。
2. 优先在主题里新增／修改类型变体，场景指定 `theme_type_variation`；局部 override 仅用于明确例外。
3. 保留 C# 实际读取的节点名和数据绑定，尤其 `Root/PlayerBar`、`BarPlayer/Fill`、`DelayBar`、敌人及首领血条节点。
4. 验证不同分辨率、悬停、打开保护、顶层关闭和暂停恢复；只替换外观不能宣称业务流程已回归。

素材目录、授权和完整节点规范见 [美术入口](art-onboarding.md)、[资源规范](assets.md)。原有 Kenney UI／Crosshair／Fonts 资源的许可文件保留在项目中。

## 关键表现约定

- 战斗生命条在左下，左上为携带符文及最近6项构筑图标，超出显示数量并提示按C查看全部；中键辅助瞄准默认关闭，开启后显示状态及目标四角标记，暂停或模态界面自动隐藏。详见[049～054接入记录](content-update-049-054-20261029.md)。
- 敌人、玩家血条表现区分生命、护盾和护甲；护甲使用覆盖血条的半透明层与 `ArmorPeak` 比例，并非恢复“最大护甲”属性。
- 跳字区分伤害、护甲、护盾、格挡、无敌、其他伤害和恢复。护甲免普通打断、格挡者硬直、效果护罩等真实结算差异应与反馈一致。
- 节点形状区分类型，光圈区分价值。场景常驻名牌已隐藏，靠近后由高亮／按键提示说明互动；不要只靠颜色传达含义。
- `AbilityMatrixScreen` 每页最多6列×3行，窄屏自适应，只生成真实卡，空位留白。左键激活／长按解锁，右键升级／长按重置升级，M扩增额度，PgUp／PgDn翻页。
- 技能蓄力、瞄准、投掷路径按真实槽和实际按住输入显示。玩家松手、换装和死亡清理预览；脱手技能仍运行不代表玩家还在按住预览。
- 战斗镜头、特效、音效按 [打击反馈](combat-feel.md) 的结算类别和预算接入；展示模型不能参与命中判定。

## 仍需验证的体验

基地指引、出击摘要和节点代价提示已接入并通过真实场景切换的自动化验证。指引观察实际移动、命中和武器页使用，可由 F1 收起，不阻止直接出发；节点提示读取真实挑战波数和免费／超额规则，不提前抽具体奖励。

整局统计、首领收尾、换关／末关提示、符文预览限定、死亡确认以及挑战／游离敌人数区分均已接入。五张1920×1080实际渲染截图已核对，见 [J44](archive/development-batches/character-design-j44.md)；首次游玩的理解成本、更多分辨率和最终视觉风格仍需专门验证，见 [完整体验计划](player-experience-plan.md)。

UI改动按影响检查真实流程与不同分辨率，纯文档整理只做内容和链接检查；已有证据入口为 [A批](archive/development-batches/a-batch-verification.md)、[C批](archive/development-batches/c-batch-verification.md)、[死亡流程](death-flow.md)、[J31符文界面](archive/development-batches/character-design-j31.md)、[J39槽位](archive/development-batches/character-design-j39.md)。
