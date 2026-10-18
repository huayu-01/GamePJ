# J31 · 基地符文携带界面

已接基地符文台，靠近按F打开。11枚符文统一使用“携带”，一次一枚，也可不携带；列表支持鼠标与焦点选择，效果与特化前置详情直接读表。11个独立SVG图标由RuneConfig.IconPath引用。

基地保存出击配置，进关沿用RunController既有施效逻辑。符文不进入奖励池；界面只在基地挂载。新节点由BaseCamp延迟挂载，未修改外部正在编辑的Base.tscn。沿用OverlayScreen的暂停、鼠标、开启保护期和Esc关闭。

验证：[完整日志](character-design-j31-evidence.log)，双构建0警告／0错误；界面专项无渲染42/0、渲染43/0，逐枚选择、替换、不携带、真实F／Esc、暂停／鼠标、重复交互、ConfigFile独立临时路径往返均通过。[截图](character-design-j31-rune-ui.png)已目视检查。原回归四项80／115／42／83均0失败；弓专项在J32补验39/0。Main八指标与J11逐字一致，原5条历史红标仍保留。真实四份存档哈希不变，rune.cfg未创建，selftest.flag已清理。

首次专项因新增SVG尚未导入失败，保留[失败现场](character-design-j31-failure-20261018_225823.log)，补充导入步骤后重验通过。验收基于保留外部美术改动的工作区。
