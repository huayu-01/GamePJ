# Shader草地实机试做

历史记录：同一场景已更新为[全场丛林草地版](grass-jungle-trial.md)。下文的初版参数、按键和2103簇性能只对应当时版本，媒体与原始数据保留；当前场景以新版报告为准。

2026-10-29。用户授权先做一块样板，检查效果与性能。当前独立场景已完成，没有将草地铺入正式关卡，也没有改变原场景、存档、模型、碰撞、导航或战斗规则。

## 看效果与试玩

- [实机风动GIF](../../../Logs/evidence/grass-shader/wind.gif)、[MP4](../../../Logs/evidence/grass-shader/wind.mp4)
- [原地面／草地并排](../../../Logs/evidence/grass-shader/comparison.png)
- [普通密度原图](../../../Logs/evidence/grass-shader/batched.png)、[高密度原图](../../../Logs/evidence/grass-shader/dense.png)
- Godot打开`Scenes/Debug/GrassShaderLab.tscn`，运行当前场景。数字1原地面、2推荐密度、3同量独立草簇、4高密度；WASD／鼠标沿用正式移动与视角，左键出手，Esc设置，F10退出样板。样板包含3个静态真实敌人靶。

采用4片实体草叶组成的低面数草簇，每簇12个三角面；根梢渐变、不同位置的风波和角色靠近时压弯均由共享Shader计算。叶片没有透明混合。分块4×4米，草不投射动态阴影，接收原场景光影；36米距离剔除。主路径和预警示例附近留空，边缘用概率渐稀，原预警保留正常深度测试。

普通密度2103簇，高密度3269簇，49个分块。普通与独立草簇使用相同网格、材质、位置和世界坐标风动／色差，不靠额外材质或脚本故意放大对照成本。草簇颜色与原场景底色的整体融合仍属于后续美术定稿，本轮保留原地面，以便看清实际新增效果。

## 实测

RTX4070Ti、Godot4.7.2、D3D12 Forward+；实际输出1920×1080，3D比例1，MSAA2x，关闭帧率上限与VSync后采样。固定镜头、场景、种子29102026、角色和3个静态靶；每阶段暖场1.5秒、采样3秒，按正序和逆序各运行一次，下面是两次阶段均值的平均。

| 方案 | 整帧均值ms | GPU均值ms | 渲染CPU均值ms | 全场景draw calls |
| --- | ---: | ---: | ---: | ---: |
| 原地面 | 1.964 | 1.066 | 0.564 | 720 |
| 分块草地，2103簇 | 2.046 | 1.113 | 0.604 | 763 |
| 同量独立草簇 | 2.162 | 1.120 | 0.710 | 721 |
| 高密草地，3269簇 | 2.050 | 1.135 | 0.606 | 763 |

本机此次试验中，推荐草地相对原地面增加约0.048ms GPU时间、0.083ms整帧时间。与同量独立草簇相比，GPU时间接近，渲染CPU时间低约0.106ms。结果支持“较小开销换取草叶轮廓与风动”，不支持“铺草比空地更快”。短样本的小差值也不作为所有设备的固定提升幅度。

独立草簇的绘制调用没有变成上千次：Forward+会自动合批同网格、同不透明材质的MeshInstance3D，见[Godot官方说明](https://docs.godotengine.org/en/latest/tutorials/performance/optimizing_3d_performance.html#use-automatic-instancing)。本例独立草簇总draw calls反而少于49块MultiMesh，因此不宣称MultiMesh在这里减少了draw calls。MultiMesh的节点规模更小：默认组件仅建立分块节点；独立草簇由样板显式开启`BuildReference=true`才生成。

基准为公平切换预先建立全部对照，采样只比较可见绘制成本，未计入生成／销毁、常驻内存与全场AI战斗。录帧在所有性能采样之后单独进行；不将录制时的帧率作为性能证据。未测移动端、低端集显及上百敌人实战。

## 验证与迭代

- Debug和ExportRelease：0警告、0错误。
- 无头11项、实渲染20项通过；涵盖生成规模、无碰撞／导航、正常深度预警、真实数字键切换、共享时钟、暂停冻结及八个有效GPU采样阶段。
- 最终导入、无头和实渲染均退出0、stderr为空；五份真实存档逐字节不变。所有构建与运行持有共享验证锁，使用隔离镜像和独立用户目录，保留用户编辑器。
- 第一次迭代修正导航入场点与实体地面的高度差；第二次取景避开前景岩石。对照中逐实例uniform达到引擎缓冲上限，现全部改为世界坐标计算颜色，没有扩大工程全局缓冲或忽略错误。
- 最后一轮扩大中央路径留空，并让草地外沿渐稀；键盘验收等待场景进入可输入状态并分开按下／抬起。截图、并排图和四帧风动抽样均已打开检查；GIF为960×540／40帧，MP4为1280×720。

原始数据：[benchmark.json](../../../Logs/evidence/grass-shader/benchmark.json)、[聚合数据](../../../Logs/evidence/grass-shader/summary.json)。最终日志：`Logs/validation/grass-shader/final/`；早期迭代与错误日志保留在`round-1`、`round-2`、`round-3`。

## 文件入口

- `Scripts/Visual/ShaderGrassPatch.cs`：生成、地面采样、分块、密度与对照；加入世界后调用一次Build。默认不建立独立草簇对照。
- `Assets/Effects/grass_patch.gdshader`：实体草叶的风动、渐变与压弯。
- `Scripts/Testing/GrassShaderLab.cs`与`Scenes/Debug/GrassShaderLab.tscn`：默认可试玩；`--grass-benchmark`自动验证与采样。
- `_tools/verify_grass_shader.ps1`：共享锁、双构建、隔离导入／运行、五存档检查。
- `_tools/present_grass_shader.py`：采样结束后生成对照图、MP4／GIF及聚合表。

CodeGraph已强制重建。本轮没有改动任何已有生产C#脚本或正式场景；新增源码已统一UTF-8无BOM、LF。
