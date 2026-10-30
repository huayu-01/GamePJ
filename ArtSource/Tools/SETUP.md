# Blender 美术产线环境

安装与验证日期：2026-10-19。

## 已安装和启用

| 组件 | 版本与来源 | 用途 |
|---|---|---|
| Blender | 5.2.2 LTS，`E:\steam\steamapps\common\Blender\blender.exe` | 建模、材质、绑定与动画 |
| Rigify | 随 Blender 提供，已启用 | 生成人形控制骨架，制作和调整动作 |
| Node Wrangler | 随 Blender 提供，已启用 | 整理贴图及材质节点 |
| Retarget | 5.2.0，[Blender Extensions](https://extensions.blender.org/add-ons/retarget/) | Mixamo 等动作重定向及 Rigify 转换 |
| MCP for Blender | 服务端包 2.2.0，[作者仓库](https://github.com/ahujasid/mcp-for-blender) | 本机 Blender 与 Codex 通信 |

GLB 和 FBX 导入导出使用 Blender 自带模块，已经启用。
Retarget 从 Blender 官方扩展目录下载，安装包 SHA256 与目录元数据一致。
MCP 使用 PyPI 发布的固定版本及其随包提供的 Blender 插件；下载文件也完成 SHA256 校验。
具体来源与哈希见 [安装记录](installed-plugins.json)。

## 启动

可以直接在资源管理器中双击 [Start-Blender.cmd](Start-Blender.cmd)。
`.ps1` 文件在 Windows 中双击可能只会打开编辑器，不能据此判断 Blender 已经启动。
也可以在 PowerShell 中执行：

```powershell
& 'F:\Godot项目\项目a\ArtSource\Tools\Start-Blender.ps1'
```

启动器会检查四个插件、登记 `GamePJ ArtSource` 素材库，并启动本机 MCP 连接。
已存在有效 MCP 连接时复用当前 Blender，不重复打开实例。
新的启动记录写入本目录 `blender-launch-*.log` 和 `*.stderr.log`；启动失败会显示错误并保留控制台。
普通启动 Blender 也会读取已保存的插件设置；启动器额外保证这些设置在旧窗口覆盖偏好后仍能恢复。
三维视图按 `N`，打开 `MCP for Blender` 面板可查看连接；插件默认自动启动本机服务。

Codex 的 `blender` MCP 已写入用户配置 `C:\Users\huayu\.codex\config.toml`，
命令为 `C:\Users\huayu\.local\bin\mcp-for-blender.exe`。
当前 Codex 会话需要重新加载 MCP 配置；工具仍未出现时，保存工作后重启 Codex。
该接入方式采用 [Codex 官方 MCP 配置](https://developers.openai.com/codex/mcp)。

连接使用 `127.0.0.1:9876`，服务端遥测关闭，插件的详细数据收集选项关闭。
代码检查模式已启用，正常建模、材质、导入导出和保存操作可用。
本次没有连接生成服务账号或调用生成额度。

后续单 API Key 混元接入见 [混元配置说明](HUNYUAN.md)，与插件内 SecretId／SecretKey 入口分开。

## 素材存放

所有新素材源文件放在项目的 `ArtSource/` 下。Blender 素材库登记到 `ArtSource/Blender/`。
目录用途见 [目录说明](../README.md)。`.gdignore` 阻止 Godot 导入源工程和工具文件。
验收后的运行资源再按项目约定交付到 `Assets/`。

## 实测结果

已在独立的 Blender 图形实例上完成：

- 标准 MCP 初始化、工具枚举、插件状态查询、场景读取及代码执行。
- Rigify 人形元骨架创建和控制骨架生成。
- 临时 1 米立方体 GLB 导出及回读，尺寸误差小于 0.0001 米。
- Retarget、Node Wrangler 注册成功；另起后台实例确认四个插件的启用状态已保存。

完整 MCP 返回值见 [验证记录](blender-validation.json)，环境信息见 [环境记录](blender-environment.json)。
本次仅验证安装和连接；真实角色权重、Mixamo 重定向效果与战斗动作需使用后续样件验收。
验证实例使用独立端口 9877，结束后已关闭；安装前打开的用户 Blender 进程保留。
临时 GLB 位于被 Git 忽略的 `Tools/cache/`，不属于正式模型资产。

偏好设置与 Codex 配置的安装前备份位于被 Git 忽略的 `Tools/backups/`。
恢复前应先对比用户后续改动，不要直接覆盖。
