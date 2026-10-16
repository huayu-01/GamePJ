# 2026-10-16 美术来源与许可审计

所有文件均通过作者页面的零价下载流程取得；未购买、未访问付费 upload、未绕过限制。
原包只存放在已忽略的 `_art_staging/art_refresh/`，不提交整包。下载文件大小、SHA-256 和包内许可位置见
[art-refresh-downloads.json](art-refresh-downloads.json)。页面首次并发请求遇到 429 后停止并发，正常零价顺序请求成功。

| 来源 | 免费内容 / 包内核实 | 许可及采用决定 |
|---|---|---|
| [Hit FX](https://binbun3d.itch.io/hit-fx) | Hit FX Free，40,460 B；6 个预设及 shader/脚本。包内 `StylizedHitFX/license.txt` 明示 CC0 | 复用项目已有命中特效和依赖，补实例材质隔离；不重复导入其他5个预设。Full未取得 |
| [Muzzle Flash](https://binbun3d.itch.io/muzzle-flash-vfx) | 免费主包，1,875,967 B；没有独立 LICENSE。页面 Licensing 和 Asset license 均 CC0 | 复用项目已有枪口场景，修复 shader/粒子过程材质的实例共享；不导入重复主包及付费 ExtraTexturesForSupporters |
| [Loot VFX](https://binbun3d.itch.io/loot-vfx) | 免费主包，60,904 B；没有独立 LICENSE。页面 Licensing 和 Asset license 均 CC0 | 采用四个 shader、三个 include、两个脚本和一个 Rare 地面预设，组成独立 FxLootBeacon；不导入演示和 Extra |
| [Battle FX](https://binbun3d.itch.io/battle-fx) | Free，1,009,313 B；包内 `BattleFX/license.txt` 明示 CC0。免费版含五类空白模板 | 只采用免费 swing 模板、控制脚本、shader、许可；调成青蓝剑弧 FxBattleSwing。其余模板、HDR和演示未导入。Full未取得 |
| [Explosion FX](https://binbun3d.itch.io/explosion-fx) | Free，999,784 B；包内 `ExplosionFX/license.txt` 明示 CC0；3类免费预设 | 复用已有地面爆炸，材质隔离、减少重复 autoplay 和过亮发光；不导入重复内容，Full未取得 |
| [Toon Nature](https://kyrusgames.itch.io/toon-nature) | 免费 Toon Trees Lite 插件包1.0.1，1,306,643 B；读过 CREDITS、README、两个 LICENSE。Godot≥4.4，作者列出4.7.2测试 | **未采用其文件**。树木许可允许游戏内商业使用、修改、随引擎导出，但明确禁止把源文件发布到公共仓库；风模块另为MIT。树包还要求全局 shader 参数及插件配置。本轮自建低模树/山体，留 PackedScene 替换口，不引入全局依赖 |

Binbun 本次 ZIP 未提供统一语义版本号，不能把网页最新 devlog 版本当成包内确定版本；用下载日期、免费 upload 与 SHA-256 固定来源。
既有 Hit/Explosion/Muzzle 只做定点安全与视觉修正，没有整包覆盖。新增 Battle 复用已有 `VFXControllerBB`，不产生同名全局类。

CC0 规范：[Creative Commons Zero 1.0](https://creativecommons.org/publicdomain/zero/1.0/)。本次采用的 Binbun 素材没有付费依赖、外部音频或运行期网络请求。
原包内音效节点没有附带可用音频资源；没有导入 ROHHSA 或其他独立音效包。

项目自建内容：天空参数、石地 shader、橡树/松树几何、山体/岩柱、云朵、基地边框和可选屏幕像素滤镜。
没有 AI 生成图片或来源不明贴图。现有角色和敌人模型来源保持原项目记录，本轮没有重新授权或改造它们。
