# 第二轮 A 批验收记录（2026-10-16）

范围：HANDOFF §5 A-1～A-5。基线为 `be2467d`，验收使用 Godot 4.7.2 mono / C#，原 `ConfigIndex.tres` 与所有表行均未修改。

## 实测结果

| 检查 | 实测 |
|---|---|
| `build.ps1 --no-incremental -v minimal` | **0 个警告 / 0 个错误** |
| 完整 `Main.tscn -- --selftest` | **退出码 0**，`17:06:34 === 自测结束 ===` |
| `_BatchAProbe.tscn` 实拍 | **退出码 0，32 条 PASS，失败 0** |
| 基线比对 | 下表八条日志去掉时间戳后，**逐字完全一致** |
| 存档 | `meta.cfg` / `ability_cards.cfg` 验证前后 SHA-256 不变，未创建 `settings.cfg` |
| 清理 | `selftest.flag` **不存在**；只退出本轮游戏，编辑器保持连接，未调用 `stop_project` |

原始摘录：[a-batch-evidence.log](a-batch-evidence.log)。完整终端输出位于项目本地 `.ai_a_selftest.log/.err`、
`.ai_a_probe_final.log/.err`；游戏日志为 `user://logs/run_20261016_170550.log`（完整自测），
`run_20261016_170636.log`（实拍探针）。改动前为 `run_20261016_163759.log`。

| 硬指标 | 改动前 → 改动后 |
|---|---|
| 配置加载 | `ConfigIndex.tres 已加载`，连招参数 3、音效 14 |
| 索引 | `6/2/10/1/1/39/7/14/8/5/6/11/4` → 同值 |
| 节点布置 | `10（高2/中5/低3）` → 同值 |
| 玩家 | `生命220 / 护甲20 / 移速6.5 / 减伤0` → 同值 |
| 压力 | `pressure=3 pmods=3 bank=0` → 同值 |
| 白板首领 | `生命315 / 护甲0` → 同值 |
| 端到端 F | `奖励界面打开=True Paused=True` → 同值 |
| 最小闭环 | `动画事件帧1 → 投射物1 → 命中掉血 ✓` → 同值 |

两份真实存档的 SHA-256（验证前后相同）：

```text
meta.cfg=DD0CB8EAFCDF2858016870B8ED2A65874FD55ED0620A743BE55A483059BD10C4
ability_cards.cfg=594BD027B7E68FFA16D2F45A6702339E5A5B69185C49B3AB944A7A9EAE7399CA
```

## 五项验收与截图

| 项 | 行为与证据 |
|---|---|
| A-1 血条 | `BarPlayer/Vitals` 内显示 `220/220 +20`，护甲蓝色；HUD 独立 `Health` 节点不存在。[实拍](screenshots/a_hud_vitals.png) |
| A-2 额度条 | 左侧宽 **64px**，`FillMode=3`（BottomToTop），原三档色保留。[5/6 预演实拍](screenshots/a_matrix_four.png)；该图只预演额度显示，未激活真实卡、未改变存档 |
| A-3 卡牌 | 真实 4 张只生成 4 个节点；0/1/18/19/37 张样本均通过。19 张为 **18+1 两页**；GUI 点击下一页，PgUp 回首页，升级后的 Lv2 保留；末页不会越界。[首页](screenshots/a_matrix_page1.png) / [末页](screenshots/a_matrix_page2.png) |
| A-3 自适应 | 画布宽度 1920→720→1920 时，列数 **6→3→6**，窄画布每页容量 9，始终最多三行；不会生成空卡。分页样本只存在于禁止落盘的沙箱，正式表仍 4 张 |
| A-4 设置 | 合成真实 Esc → 设置打开且暂停；四页 `graphics / input / audio / help`；H → 帮助页；Esc 关闭恢复游戏。[ESC](screenshots/a_settings_esc.png) / [帮助](screenshots/a_settings_help.png) |
| A-5 下落 | 角色 B + 灵剑，测试台把玩家摆到 12m，再按真实 Ctrl；**没有调低速度、没有延长特效、没有调用视觉预演**。[下落帧1](screenshots/a_dive_00.png) / [下落帧2](screenshots/a_dive_01.png) / [落地](screenshots/a_dive_landed.png) |

下落实测输出：

```text
真实下落帧 4 y=9.625534 只读源=True 真实下落帧=4 强度=1 可见=True 速度=(0,-36.352005,0)
真实下落帧 8 y=7.104068 只读源=True 真实下落帧=8 强度=1 可见=True 速度=(0,-38.70401,0)
预演剩余=0s 光带=10｜碰撞体=0 单位=0 分组=0
落地事件与既有落圈继续生效
落地后风线收起
特效前后单位分组计数不变：[1,0,0]
=== A 批验收结束：失败 0 ===
```

另用编辑器 MCP `runtime screenshot` 取了两张 1920×1080 视口图（先确认根节点 alpha=1，避开开场淡入）：
[关卡能力页](screenshots/a_matrix_runtime.png) / [设置帮助页](screenshots/a_help_runtime.png)。
MCP 实测 `UiService.OpenAbilityMatrix()=true`，帮助图标的 `resource_path` 为 `res://Assets/UI/Icons/ic_ui_set_help.svg`。

## 接入变化

- 能力页的新布局参数导出到场景：卡面比例 0.625、最小宽128、最大宽200、间隔28；固定画布上只铺真实卡，宽高变化时重新计算容量。
- `Main.tscn` 原来未挂能力页，服务调用实测为 false；本批补入相同预制体。基地原有入口继续使用。
- 删除独立 `HelpScreen` 场景和脚本；`UiService.OpenHelp()` 兼容到设置页。`HelpSettingsSection` 从 InputMap 读取当前重绑按键。
- `DiveWindFx` 只读状态，用一个 ImmediateMesh 绘制青蓝细长风线。没有复制模型，没有碰撞体、单位和分组，原战斗逻辑只新增一个只读属性 `IsDiving`。
- 原 `_AbilityShowcase` / `_AbilityInGame` 拍摄台同步移除旧占位卡 API；新验收台与一键脚本保留在仓库。

## 重跑

```powershell
& 'F:\Godot项目\项目a\_tools\verify_batch_a.ps1'
```

脚本顺序执行指定编译命令、完整无头自测、带渲染的 A 批探针，读取真实进程退出码，并在 finally 清理 flag。
探针截图均直接来自运行视口。无头探针会明确跳过 GUI 命中测试；GUI 点击结论以带渲染实拍为准。

## 保留的问题与边界

- ⚠️ 完整无头自测仍有**改动前已存在**的空导航网格、cell_height 不匹配、退出时 `4 ObjectDB instances` / `2 resources still in use` 日志。基线同样出现，本批未处理；不能据编译 0/0 宣称运行日志零警告。
- ⚠️ 本批统一卡牌留白、间距和信息位置，增强下落识别度；整体场景、角色、材质、光照和其余 UI 美术仍需后续艺术打磨，尚未达到终版艺术品质。
- ⚠️ 角色数值仍是已有预设值；E/B/C/D 批及字体等原待办继续保留。CodeGraph 后续维护按用户要求放在每批交付后，不能阻塞计划。
