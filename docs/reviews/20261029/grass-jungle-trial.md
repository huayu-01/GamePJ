# 丛林场景与全场半密草地

历史记录：2026-10-30同一场景已更新为[地表融合与柔和十字草片](../20261030/grass-soft-reference.md)。本页材质、几何、性能和媒体对应当时版本，保留作前后对照。

2026-10-29。按用户要求更新原草地样板，地板、围墙和边界均已丛林化，草覆盖整个100×100米地面，单位面积密度减半、风动频率降低，不做角色靠近压弯。仍在独立的`Scenes/Debug/GrassShaderLab.tscn`中预览，正式关卡资源没有直接替换。

## 效果与试玩

- [六秒慢风GIF](../../../Logs/evidence/grass-jungle/wind.gif)、[MP4](../../../Logs/evidence/grass-jungle/wind.mp4)
- [原庭院／新版丛林对照](../../../Logs/evidence/grass-jungle/comparison.png)
- [全场俯视](../../../Logs/evidence/grass-jungle/overview_grass.png)、[围墙近景](../../../Logs/evidence/grass-jungle/wall.png)
- [正式跟随相机的试玩画面](../../../Logs/evidence/grass-jungle/playable.png)
- Godot打开`Scenes/Debug/GrassShaderLab.tscn`，运行当前场景：1丛林地表、2半密草地、3全场俯视、4游戏镜头、F10退出；游戏镜头沿用WASD、鼠标、左键和Esc。俯视时停止玩家位移，返回游戏镜头后恢复；提示栏按视角显示可用操作。

灰色地砖和路面条已隐藏，地表改为苔地、土斑与落叶。四周边界由116块不规则覆苔岩块替代；内部14处残墙、柱体改为岩石和盘根，墙面增加弯曲细藤、大叶植物。针叶外圈换为阔叶树冠，内部补六组较小阔叶丛。地表、石头与树根材质为独立Shader，全部只修改样板运行时实例。

原有碰撞对象、形状资源、全局变换和启用状态逐一对比保持一致；新增树、岩石、藤叶与草均不加入碰撞或导航。井台等高出地面的设施、墙顶和树干下实际被碰撞遮挡的位置不种草，旧道路和预警示例不再主动排除草。

## 草与风动参数

- 100×100米地面、4×4米分块，623个非空分块，普通显示19289簇，每簇4片叶／12三角面。
- 每平方米候选采样密度由12改为6；普通显示比例仍为65%，自然稀密斑块与边缘渐稀保留。这里减半指单位面积配置，铺设面积扩大后总株数增加，不能与初版2103簇直接比较总量。
- 共享Shader时钟的风动频率乘0.45，即比原版降低55%；幅度仍为0.13。角色坐标uniform、每帧坐标上传与压弯分支全部删除。
- 实体叶片无透明混合、无动态投影；游戏镜头66至80米通过顶点缩小渐隐，80米外剔除；俯视取消距离剔除，完整显示全场草。全场版本不建立几万独立Mesh节点作为对照。
- 慢风媒体用固定1/30秒Shader时步录180帧，输出6秒；PNG写盘耗时不参与风动计时，避免把慢风加速播放。录帧和编码均在性能采样之后进行。

## 本机实测

RTX4070Ti、Godot4.7.2、D3D12 Forward+，1920×1080、3D比例1、MSAA2x，关闭VSync和帧率限制。对照双方使用同一新版丛林环境、相同玩家与三个静态真实敌人靶；各暖场1.5秒、采样3秒，正反序各一次。性能采样用固定第三人称取景，试玩恢复正式跟随相机。以下为两次阶段均值的平均，测的是草地增量，未对原庭院与丛林环境做整体性能比较。

| 视角／草地 | 整帧均值ms | GPU均值ms | 渲染CPU均值ms | 全场景draw calls |
| --- | ---: | ---: | ---: | ---: |
| 游戏镜头，无草 | 1.87 | 1.52 | 0.56 | 445 |
| 游戏镜头，半密草 | 2.36 | 1.67 | 0.77 | 787 |
| 俯视全场，无草 | 1.69 | 0.87 | 0.51 | 242 |
| 俯视全场，半密草 | 2.67 | 1.21 | 0.89 | 865 |

本机游戏镜头增加约0.49ms整帧、0.15ms GPU；全场俯视增加约0.98ms整帧、0.34ms GPU。俯视显示全部623个草块，额外绘制提交更明显。结果只说明此次静态样板的开销，不代表满场AI、长局战斗或其他设备性能，也不说明铺草比空地更快。

原始数据：[benchmark.json](../../../Logs/evidence/grass-jungle/benchmark.json)、[聚合结果](../../../Logs/evidence/grass-jungle/summary.json)。有效采样记录位于`Logs/validation/grass-jungle/round-3/`。

## 验证与迭代

完整采样轮Debug与ExportRelease均0警告0错误，无头18项、实渲染33项通过，退出0、stderr为空。覆盖全场规模与边缘、原碰撞保留、新增视觉无碰撞／导航、无透明草叶、正常深度预警、真实数字键切换、暂停冻结和八段有效GPU采样；真实五份存档逐字节不变。最终媒体轮再次双构建0／0，无头18、固定时步录制20、真实跟随相机试玩22项通过，所有stderr为空，五存档保持不变；日志在`Logs/validation/grass-jungle/final-media-playable/`。

最终对照图、全场图、墙体近景、试玩相机及四帧草叶局部均已打开检查。GIF为960×540、120帧／6秒，MP4为1280×720、30fps／6秒；时间记录见[recording.json](../../../Logs/evidence/grass-jungle/recording.json)。本轮C#文件均为UTF-8无BOM、LF；CodeGraph已强制重建。

实机检查后调整了偏黄亮的地表和石面，增加地表与草根的融合；将36米硬切改为66至80米收缩渐隐，减少远处明显空带；直线藤条改为细曲藤，大叶植物合成共享整丛网格。首轮Shader矩阵构造和脚本镜头插值告警已在后续严格验证中修正，历史错误日志保留。

入口文件：`Scripts/Visual/JungleGrassScenery.cs`、`Scripts/Visual/ShaderGrassPatch.cs`、`Assets/Effects/jungle_ground.gdshader`、`Assets/Effects/jungle_surface.gdshader`、`Assets/Effects/grass_patch.gdshader`；自动验证与媒体整理分别为`_tools/verify_grass_jungle.ps1`和`_tools/present_grass_jungle.py`。
