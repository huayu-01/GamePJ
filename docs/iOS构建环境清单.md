# iOS 构建环境清单

## 当前结论

- 当前 Windows 环境可以继续开发、测试游戏逻辑并维护跨平台资源，但不能完成 iOS 应用的最终编译、签名和真机安装。
- 本机已安装 Godot 4.6.3 Mono 的 iOS 导出模板 `ios.zip`，这只能用于准备导出内容，不能替代 macOS 与 Xcode。
- 项目使用 C#，并由 `global.json` 固定到 .NET SDK 9.0.315。Mac 构建机应安装相同功能带的 .NET 9 SDK。

## 必需环境

1. 一台可运行当前 Xcode 的 Mac，建议 Apple Silicon Mac mini 或 MacBook。
2. Xcode、Xcode Command Line Tools，以及一台用于真机验证的 iPhone 或 iPad。
3. Godot 4.6.3 Mono 和匹配版本的导出模板。
4. .NET SDK 9.0.315；如果该精确版本不可用，先更新并验证 `global.json`，不要静默使用不同主版本。
5. Apple ID。通过 TestFlight 或 App Store 分发时需要加入 Apple Developer Program。
6. 唯一 Bundle ID，建议沿用 Android 标识语义并使用 `com.huayu.gamepj`，最终值应在 Apple Developer 后台注册后固定。
7. Development Team、开发/发布证书和对应 Provisioning Profile。

## Windows 侧可提前完成

- 保持玩法、资源、网络协议和 UI 代码不依赖 Windows/Android 专有 API。
- 为 iOS 准备应用图标、启动画面、隐私用途说明文本和版本号。
- 所有远程更新地址使用 HTTPS；不要把平台证书、私钥或 Provisioning Profile 提交到 Git。
- 为触摸输入、竖屏安全区、刘海和底部 Home Indicator 预留布局空间。
- 维护 Android 与 iOS 共用的协议版本，确保不同平台客户端能拒绝不兼容版本。

## Mac 首次构建流程

1. 克隆仓库并安装 Git LFS，然后执行 `git lfs pull`。
2. 安装 Godot 4.6.3 Mono、匹配导出模板和 .NET SDK 9.0.315。
3. 在 Godot 中导入项目，等待 C# 与资源首次构建完成。
4. 新建 iOS 导出预设，填写 Bundle ID、版本号、Development Team 和签名配置。
5. 从 Godot 导出 Xcode 工程，在 Xcode 中选择签名团队与真机目标。
6. 首次使用 Debug 配置安装到真机，验证启动、竖屏、安全区、触摸、音频和联网。
7. 使用 Product > Archive 生成归档，通过 Xcode Organizer 上传 TestFlight。
8. TestFlight 至少完成创建房间、加入房间、断线重连、完整牌局、亮牌、聊天和对局记录测试后，再考虑正式发布。

## iOS 专项验证

- 检查所有界面在 9:19.5、9:20 和带 Dynamic Island 的安全区内无裁切或重叠。
- 检查切换后台再返回时，网络会话、行动倒计时和牌局状态能正确恢复。
- 检查蜂窝网络、家庭 Wi-Fi 和不同 NAT 环境下的公网连接；P2P 不可达时必须有中继或服务器回退。
- 如果恢复局域网自动搜房，需要配置并解释 iOS 本地网络权限；当前公网流程不应依赖该权限。
- 检查热更新只更新允许的资源和数据，不下载或执行可绕过 App Review 的原生代码或 C# 程序集。
- 检查日志中不记录其他玩家未公开手牌、牌堆或签名凭据。

## 推荐实施顺序

1. 先准备 Mac 构建机、Apple 开发者账号和测试设备。
2. 创建 iOS 导出预设并完成空签名工程导出。
3. 修复首次真机编译问题，再做安全区和后台恢复适配。
4. 接入 TestFlight 内测，建立 Android 与 iOS 双端联机测试矩阵。
5. 最后配置正式签名、隐私资料、商店素材和发布流水线。
