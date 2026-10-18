# GCHandle：可复现资源缓存竞态与项目侧规避

2026-10-17，Godot 4.7.2 mono，实际版本 `ed1daf0bf`。

## 复现方法与证据

`ResourceLifetimeProbe` 在独立进程中用一个有超时的托管终结器屏障，控制以下时序：

1. 加载真实磁盘资源；保留弱引用并离开加载函数。
2. 强制GC，确认`托管对象存活=False`。此时原生对象还没有被终结器释放。
3. 再次加载同一路径；然后放开终结器。未手工释放句柄，也未调用引擎私有接口。

默认`ResourceLoader.CacheMode.Reuse`在本机稳定触发：

```text
[Lifetime] 托管对象存活=False；终结器阻塞期间再次加载
ERROR: FATAL: Condition "gchandle.is_released()" is true.
at: mono_object_disposed_baseref (modules/mono/csharp_script.cpp:1788)
Godot.GodotObject.Dispose(bool) → Godot.GodotObject.Finalize()
退出码=-1073741795
```

相同时序下，`RuntimeResources.Load`能重新加载，旧包装器正常终结，进程退出0。弱引用回收断言证明不是用永久强引用把问题藏起来。

上游依据：[Godot #83762](https://github.com/godotengine/godot/issues/83762)。该记录确认资源缓存复活窗口及深度绕过缓存的规避方式；本项目另用可控时序验证。

⚠️ 这不代表修改了Godot引擎。旧C批`SetGodotObjectPtr / Handle is not initialized`与本次C#资源FATAL的堆栈不同，原始变体还未单独复现，不能仅凭相关上游记录认定完全同源。原生Animation包装器使用相同时序的默认缓存对照退出0，说明并非所有资源类型都会触发此FATAL；已另覆盖新入口的原生包装器加载。

## 读取入口契约

- 运行期显式加载统一用`RuntimeResources.Load<T>`。仅在`WeakReference.TryGetTarget`取得仍存活且有效的托管对象时复用。
- 包装器已失效时使用`IgnoreDeep`，连同依赖资源一起绕过原生缓存；只忽略根资源无法覆盖依赖复活。
- 缓存只保存弱引用，并定期移除死键；不会阻止资源回收。没有禁用GC，没有吞掉终结器异常。
- 场景切换及基地死亡重载统一走`RuntimeResources.ChangeScene`，仍由引擎`ChangeSceneToPacked`负责退出与延迟加入。
- 深度加载的资源图彼此独立；不要用两个独立图的对象地址判断“同一个字体文件”。字体回归改为核查实际控件使用的字体、字重、字形与资源路径。
- 编辑器生成工具保留原加载方式；此项规避范围是游戏运行期。专用负例中的直接`ResourceLoader.Load`用于复现，不能挪到正常流程。

代价：失去跨独立资源图的原生依赖共享，资源失效后重新加载可能增加读取和内存开销；完整关卡、基地往返、六配装及场景卸载回归用于检查实际影响，不能宣称引擎级永久修复。

## 验收与复现

常规验收脚本：`_tools/verify_resource_lifetime.ps1`。每次构建/运行独占验证锁；每个子进程前把四份玩家存档备份到本地`.ai_i_<名称>.profile.json`。C探针在脚本与C#入口两处强制要求`--loadout-test`。

负例独立启动参数：`res://Scenes/Debug/_ResourceLifetimeProbe.tscn -- --unsafe-cache`，会刻意触发进程崩溃；不能把其非零退出当作常规回归结果。

最终构建、八条硬指标、回归计数与截图哈希见[实测证据](resource-lifetime-evidence.log)。

最终结果：默认缓存负例3次均按预期FATAL；新入口正例3次均退出0、stderr为空，另有原生包装器正例和真实基地渲染截图。Debug/ExportRelease均0警告/0错误；Main完整自测退出0、8条指标逐字一致。F渲染33/0、F无头31/0（均12轮场景卸载与GC）、C52/0、读档5/0、D90/0、G50/0、角色63/0、H48/0、B74/0、A23/0、E39/0，正向运行stderr为空。

负例原文见[三次崩溃证据](resource-lifetime-negative.log)。当前没有调整角色基础数值，也没有以GC测试代替武器原始设计验收。

## 中间问题记录

- J16气贯长虹首轮渲染专项90/0后，退出时临时查询／截图资源报unsafe reference并在GodotObject.Finalize中原生访问违规。长枪高频预览与实际投掷、AttackHitbox扫掠查询参数改为及时Dispose，截图Image同样及时释放；后续先行与完整复验均正常退出。完整失败堆栈和真实读数见 [J16](character-design-j16.md)。⚠️ 仅为项目侧资源释放和该场景复验，不据此认定旧变体同源或引擎问题根治。
- 一次临时C验收漏传`--loadout-test`，误写装备偏好，随后因已有选中项导致连锁失败（37通过/22失败），未计为通过。
- 该次运行前没有落盘完整备份，不能宣称装备文件逐字未变。按用户最近正常游玩日志`run_20261017_122410.log`末尾的武器大师/镰刀，以及C首次读取到的猎手/弓、武器大师/镰刀，恢复选择。恢复后装备SHA256为`CD92D4BD7DE10BF7D3B76DC4DF11E440FDC0F3C00174AA40068A771097A95462`。另外三份玩家存档未改。后续验证均比较这一恢复点，并新增入口硬检查与磁盘备份。
- 一次额外F无头运行31/0但退出有4个ObjectDB对象告警；未作为无告警通过。随后同一测试加verbose重跑31/0，stderr为空，没有复现该退出告警。
- 渲染F增加verbose后，D3D12为`tex_ui_bar_fill.png`报告RGB8→RGBA8兼容转换。已核对引擎`texture_storage.cpp`：RGB格式转换仅在verbose模式报告；常规渲染参数沿用既有F验收，未压制日志或改GPU检测。此图像转换不等于GCHandle错误，额外输出保留在本地`.ai_i_f.err`。
