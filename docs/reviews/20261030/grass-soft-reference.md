# 参考图风格：草地融合与柔和十字草片

2026-10-30。独立`Scenes/Debug/GrassShaderLab.tscn`已改为地表同色、柔和受光的十字草片版本；草簇数量仍为19289簇／623块，100×100米，候选密度6、普通显示65%，慢风频率0.45、无角色靠近压弯。

## 参考来源与可读范围

用户给出的两段B站视频均未能读取实际播放内容：网页请求失败／412，浏览器打开超时。本轮没有声称观看过视频。实际依据为用户附件参考图、只读的本地`F:/Godot项目/3d肉鸽雨中哈迪斯/草地衔接/`中的Unity Shader Graph／HLSL／参数截图，以及用户随后补充的“法线夹角渐隐、两片合成十字”原理。

本地Graph包含TerrainTexture、TerrainSize、TerrainOffset、TerrainColorBlend、BottomColorHeight、BottomBlendIntensity和MainLight阴影相关节点。本轮移植的是世界坐标色场、根部衔接与光照控制思路；没有直接复制Unity的管线代码，也没有改动参考文件。并未从无法访问的视频中推断作者的具体阈值或全部实现。

## 当前效果

- [上一版／新版实机对照](../../../Logs/evidence/grass-soft/comparison.png)
- [参考角度取景](../../../Logs/evidence/grass-soft/style.png)、[草根与叶片近景](../../../Logs/evidence/grass-soft/detail.png)
- [慢风GIF](../../../Logs/evidence/grass-soft/wind.gif)、[MP4](../../../Logs/evidence/grass-soft/wind.mp4)
- [全场俯视](../../../Logs/evidence/grass-soft/overview_grass.png)、[正式跟随相机](../../../Logs/evidence/grass-soft/playable.png)
- [观察角度0°](../../../Logs/evidence/grass-soft/angle-0.png)、[45°](../../../Logs/evidence/grass-soft/angle-45.png)、[90°](../../../Logs/evidence/grass-soft/angle-90.png)

## 实现

每簇两张相差90°的草片，每张沿高度分三段，合计仍为12三角面。轮廓采用新建的原生SVG草叶遮罩：`Assets/Effects/grass_soft_blades.svg`。单片宽1.1米、高0.45米，再应用原有随机缩放；叶根略埋入地面，底边不露出完整矩形。

`meadow_palette.gdshaderinc`由地面和草叶同时引用，按相同世界坐标返回同一套低频颜色。草根保持地面色，向叶尖逐渐提亮，地表落叶色差压低；草的颜色锚在根点，不随风动漂移。低频色场保留明暗与干湿变化，避免融合后只剩一片均匀绿色。

几何原法线和受光法线分开使用：原法线计算`abs(dot(normal, eye))`，在0.10至0.38之间平滑渐隐接近侧对视线的草片；不是把所有朝向摄像机的草片隐藏。受光法线在片元阶段统一为世界朝上方向，避免双面反转导致突变的黑色纸片。草和地面共用柔和漫反射计算，仍接收现有灯光和阴影。

遮罩采用Alpha Scissor与MSAA Alpha-to-Coverage，保留深度测试。MSAA仍为2x，没有用全屏模糊掩盖插片感。坐标空间与覆盖抗锯齿参数参照[Godot 4.7 Spatial Shader文档](https://docs.godotengine.org/en/4.7/tutorials/shaders/shader_reference/spatial_shader.html)。

风动频率保持0.45，幅度由0.13改为0.065，避免更宽的草片整簇大幅摆动。仍使用共享时钟，暂停冻结；角色坐标和靠近压弯没有恢复。游戏镜头66至80米收缩渐隐，俯视取消距离剔除。地形、碰撞、导航与正式关卡资源不变。

## 实测与验证

RTX4070Ti、Godot4.7.2、D3D12 Forward+，1920×1080、3D比例1、MSAA2x。固定第三人称取景和全场俯视，各比较同一环境无草／有草；每段暖场1.5秒、采样3秒，正反序各一次。

| 视角／草地 | 整帧均值ms | GPU均值ms | 全场景draw calls |
| --- | ---: | ---: | ---: |
| 固定第三人称，无草 | 2.24 | 1.59 | 445 |
| 固定第三人称，柔和草 | 2.85 | 2.22 | 1145 |
| 俯视全场，无草 | 2.00 | 0.94 | 242 |
| 俯视全场，柔和草 | 3.21 | 1.42 | 1488 |

本轮铺草增量约为第三人称0.61ms整帧／0.63ms GPU、俯视1.21ms／0.48ms。三角面数相同不等于绘制成本相同：裁切草片和覆盖抗锯齿有额外开销，本轮没有宣称比上一版更省性能。跨轮硬件状态不同，不直接拿两轮绝对帧时间当严格差值；本轮也不代表满场AI或其他设备。

完整采样轮Debug和ExportRelease均0警告0错误，无头18、实渲染39、真实跟随相机试玩22项通过，退出0且stderr为空。覆盖全场分布、半密度、遮罩绑定、无角色压弯、原碰撞保留、暂停冻结、真实按键、八段GPU采样与恢复移动。五份真实存档保持逐字节不变。

记录：`Logs/validation/grass-soft/final-cross-soft/`；[原始基准](../../../Logs/evidence/grass-soft/benchmark.json)、[聚合数据](../../../Logs/evidence/grass-soft/summary.json)。慢风采用固定1/30秒Shader时步录180帧、输出6秒，录帧和编码在性能采样之后执行。

首轮实体细叶过于贴近地表色，画面偏平；按用户补充原理改为十字草片后，再调整草簇覆盖宽度、叶尖提亮与低频明暗。保留两次预览及最终日志，不覆盖上一版`grass-jungle`和初版`grass-shader`证据。当前自动验证／媒体入口为`_tools/verify_grass_soft.ps1`与`_tools/present_grass_soft.py`；旧验证入口转交新版。

侧向检查的一张原取景被玩家模型挡住，已将角度检查移到同片空地重新采集，几何和材质未再次改动。补拍验证在`Logs/validation/grass-soft/final-angle-check/`，双构建0／0、无头18、实渲染29、试玩22项通过，stderr为空。媒体核对：GIF为960×540、120帧／6秒；MP4为1280×720、30fps／6秒。CodeGraph已强制重建，本轮C#保持UTF-8无BOM和LF。
