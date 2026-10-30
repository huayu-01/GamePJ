# 混元生 3D：单 API Key 接入

采用用户提供的[腾讯云官方文档](https://cloud.tencent.com/document/product/1804/126189)，
使用 `https://api.ai3d.cloud.tencent.com/v1/ai3d/submit` 提交任务，
使用 `https://api.ai3d.cloud.tencent.com/v1/ai3d/query` 查询任务。
这个接口的鉴权头是 `Authorization: <API Key>`，不额外添加 `Bearer`。

## 本机配置

双击 [Set-HunyuanKey.cmd](Set-HunyuanKey.cmd)，将 API Key 填入掩码输入框，点击“加密保存”。
密钥通过 Windows DPAPI 加密，保存在当前用户的
`%LOCALAPPDATA%\GamePJ\ArtPipeline\hunyuan-api-key.json`，不会保存到项目中。
配置文件只能由对应 Windows 用户正常解密；配置窗口不调用生成接口。
`Set-HunyuanKey.ps1` 使用 UTF-8 BOM，兼容 Windows PowerShell 5.1。

现装 Blender MCP 的混元入口使用 SecretId／SecretKey 签名协议，本页的单 Key 由项目适配器处理。
生成完成后通过 Blender MCP 导入整理；不需要把单 Key 填进插件的 SecretId 或 SecretKey 字段。

## 检查与调用

在项目根目录使用已经安装的 Python：

```powershell
$artPython = 'C:\Users\huayu\AppData\Roaming\uv\tools\mcp-for-blender\Scripts\python.exe'
& $artPython .\ArtSource\Tools\hunyuan_client.py status
```

`status` 只验证本机能否读取凭据，不发请求、不产生模型费用，也不等于远程鉴权已验证。
也可由启动当前进程的本机环境变量 `HUNYUAN3D_API_KEY` 提供密钥，优先于加密凭据文件。

准备好参考图后，可提交单个任务：

```powershell
& $artPython .\ArtSource\Tools\hunyuan_client.py submit --image '.\ArtSource\Concepts\humanoid-pilot\front.png'
& $artPython .\ArtSource\Tools\hunyuan_client.py query --job-id '<提交返回的 JobId>'
& $artPython .\ArtSource\Tools\hunyuan_client.py download --job-id '<提交返回的 JobId>' --name humanoid-pilot
```

提交会消耗该账号的对应生成额度。默认采用 3.0／LowPoly／混合四边面，PBR 关闭。
同一输入和参数会复用已经记录的 JobId。网络超时等结果不确定时保留记录并阻止自动重新提交，
须先核对控制台，避免重复计费。明确拒绝的请求允许修正配置后重试。
任务记录保存在 `ArtSource/Generated/_jobs/`；不会写入密钥或请求鉴权头。
下载命令只查询已有任务，将原始模型和 SHA-256 校验记录保存到 `Generated/<资产名称>/`，
不会创建生成任务，也不会向模型下载地址发送 API 鉴权头。

本次真实接入使用专业版参数文档中的 `ImageBase64` 原始 Base64 字符串并成功提交。
兼容文档所述的 `ImageUrl.Url` 嵌套对象在本次调用中返回 `Invalid param`，适配器未采用该写法。
参见[专业版任务参数](https://cloud.tencent.com/document/product/1804/123447)。

如果服务端返回 `HTTP 401 / invalid_api_key`，请在
[混元生 3D 控制台](https://console.cloud.tencent.com/ai3d/start)重新复制完整 API Key，
通过本机窗口重新保存。不要将其他产品的 Key、SecretId 或 SecretKey 填到这个单 Key 接口。
错误信息会移除服务端可能回显的密钥，项目日志不记录请求鉴权头。

## 当前样件范围

2026-10-21 已完成非人形蘑菇样件：单图、3.0／LowPoly，任务 `1504328840385544192`，服务返回消耗 25 积分；原始结果在 `ArtSource/Generated/scaredy-shroom-v1/`。Blender 整理后加入 4 骨及缩地／出土动作，详情见[本轮模型记录](../../docs/enemy-models-first-pass.md)。小法师用 Blender MCP 直接制作，未再提交混元任务。

同日新增石头人 Plus：单图、3.0／LowPoly，任务 `1504389242456629248`，服务返回消耗 25 积分；原始结果在 `ArtSource/Generated/stone-golem-plus-v1/`。使用第一张纯石块定稿生成几何、第二张作为简单黑灰平涂参考，Blender 整理为 17 骨和五段动作；见[石头人 Plus 产线记录](../../docs/stone-golem-plus-pipeline.md)。

先验证一个结构简单、关节清晰的标准双足人形，从参考图、三维生成、Blender 整理、
骨骼与基础动作一直到 GLB。飞行单位及其造型留待后续确定。
现有模型不作为造型参考，试件不直接替换生产角色。

离线校验命令：

```powershell
& $artPython .\ArtSource\Tools\test_hunyuan_client.py
```

校验使用虚构数据和网络替身，覆盖鉴权格式、图片字段、重复计费防护及 Windows 本机加密解密。
