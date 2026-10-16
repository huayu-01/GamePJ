# Loot VFX 免费主包

作者：Binbun。来源：https://binbun3d.itch.io/loot-vfx ，核实/下载：2026-10-16。

作者产品页 Licensing 与 Asset license 均声明 **CC0 1.0**，允许个人、教育、商业项目使用，无须署名。
许可规范：https://creativecommons.org/publicdomain/zero/1.0/ 。免费 ZIP 内没有单独的许可文本，此处准确记录许可声明来源，不冒充包内文件。
原始 ZIP SHA-256 见 `docs/art-refresh-downloads.json`。仅采用免费主包，不含付费 Extra。

最小采用内容：4 个 shader、3 个 include、2 个脚本、地面 Rare 预设（移至 Scenes/ArtRefresh/FxLootBeacon.tscn）。
改动：去掉导入脚本全局类名并使用显式继承路径；重写资源路径、去外部 UID；材质和粒子过程材质按场景实例隔离；
限制 ALBEDO 非负，修正 glow_core 对负数做分数幂产生 NaN 的风险。未导入演示场景、额外预设或贴图。

本目录脚本不创建碰撞、不注册单位组、不接管拾取或奖励逻辑。
