# Lyra / Samurai 角色接入

2026-10-17。按用户指定，角色一Lyra对应猎手，角色二Samurai对应武器大师；基地展台、基地战斗玩家和正式关卡共用角色表。
本批未替换首领或普通敌人。

## 素材与实际内容

- [Elf Archer Lyra](https://grigory-petrov.itch.io/elf-archer-lyra)：24骨、20,596三角形，6段原生动作（idle/walk/sprint/jump/block/die）。没有原生射箭攻击。
- [Samurai Warrior Pack](https://grigory-petrov.itch.io/samurai-warrior-pack)：24骨、20,044三角形，实际8段动作（idle/walk/sprint/attack/double_attack/block/die/push）。作者页面列7段，压缩包额外含push。
- 原生GLB内嵌的是4096×4096颜色纹理。接入时将它提取成共享引用的PNG，避免Godot提取图片后又把同一图片留在GLB里重复存储；像素数据不变。作者说明中的“2K”“Lyra约5K三角形”等数字与文件实测有差异；不把页面宣传数字当性能统计。
- 武士原包每段动画重复附带整套网格/纹理，接入工具只合并动画buffer/accessor，最终每角色只保留一份GLB与一张颜色图，合计约28/29MB。未把整份171MB/373MB ZIP、重复FBX和其它散图加入项目。
- 原始README分别保存在Assets/Characters/Lyra和Samurai目录；允许商业游戏与项目，禁止将原始模型原样转售，Samurai另禁NFT/AI训练。署名Grigory Petrov。此项目中的合并与适配用于游戏，未将素材声明为CC0。
- 原ZIP哈希与动作清单见Assets/Characters/sources.json；下载包在忽略目录_art_staging/selected_characters，原下载文件未修改。

## 接线

1. 两角色行ModelPath分别指向新GLB。ModelTransform把原生1.7m身高调整到1.8m并朝向角色前方；只变换显示模型，单位根尺度仍为1，玩家身体仍为半径0.4/高1.8的胶囊。
2. 新骨架以厘米记录骨骼、Armature缩放0.01。WeaponGripScene指向MeshyWeaponGrip，挂RightHand并换算Socket尺度；六把武器仍使用项目已有可替换低模场景。
3. idle/walk/sprint/die使用原生动作。两个角色各有25段独立技能动画资源；不把旧Kenney轨道按名称硬塞进新骨架。
4. 离线适配按“源全局pose相对源全局rest的差值→目标全局rest→目标父骨局部空间”换算，固定目标骨长，根位置只保留垂直动作。额外前臂、手、脚等骨保留父级关系。
5. Samurai灵剑五段改用原生attack/double_attack的分段，由skill-motions.json配置前摇、命中、收招采样点，并对齐既有ATK事件时刻。其它技能使用已适配动作；Lyra射箭属于项目技能适配，不声称是包内自带射箭。
6. 近战仍由真实动画ATK事件激活AttackHitbox，沿用SkillSeries逐段形状、前伸、有效时间与倍率；不通过模型碰撞网格造成伤害。

## 本轮发现并修复的旧问题

灵剑第五段原生成表EventTimes为0.4687/0.485s，而源动作只有约0.4167s。
原生成器把两项都缩放并夹到目标片段末尾0.5044s；同一方法轨道同一时刻的后一个Combo覆盖了ATK，因此第五段只摆动作、不造成伤害。
现把源时间修为0.35/0.4s，生成结果为ATK@0.423696、Combo@0.484224；同步修正原技能库并重新生成两套新骨架库。
数值表、连招段数、逐段倍率和伤害盒尺寸不因换模改变。

## 验证方式与边界

运行Assets/Characters/tools/verify_characters.ps1：非增量Debug构建、真实渲染/物理帧验收。
探针走InputEventAction和实际动画播放，不调用DebugFireAnimEvent；覆盖新身体碰墙、敌方命中、六武器换装、猎手三武器投射物命中、灵剑5段/镰刀3段/长枪5段与循环、同段不重复伤害和换装清理。
`-SkipBuild -SelfTest`运行正式Main完整自测；所有构建/运行均独占.ai_validation.lock，只退出自己的PID。

复现资产准备：先将作者两份免费ZIP放在忽略的_art_staging/selected_characters，再运行prepare_selected_characters.py；Godot导入后运行generate_character_skills.gd。
角色切换采用既有PlayerLoadout，测试不写正式角色选择记忆。

| 最终验收 | 结果 |
|---|---|
| Debug / ExportRelease非增量构建 | 均0 warning / 0 error |
| 去重后的实际角色/战斗渲染探针 | 68 PASS / 0 FAIL，退出0；含三把近战逐段命中及长枪循环 |
| 基地C批回归 | 52 PASS / 0 FAIL，退出0；无头模式跳过7张截图断言 |
| Main完整自测 | 退出0并到达结束标记；8条硬指标与E批基线逐字一致 |
| 原生攻击姿态实拍 | attack / double_attack六时刻对照，退出0 |
| 存档 | 各验收运行中meta.cfg、ability_cards.cfg、settings.cfg、loadout.cfg哈希不变 |

原始判定摘要、关键自测日志与截图哈希见[验收证据](character-replacement-evidence.log)。
截图：characters_native_idle.png（两模型正面）、characters_samurai_sword.png（实际基地）、characters_combo_*.png（三近战命中）。

导入工具的环境问题也保留：另开headless编辑器会报告共享MCP的9080端口占用；recovery-mode导入时则出现GodotTools的HotReloadAssemblyWatcher计时器错误，两个导入进程均退出0。
这些编辑器工具问题未通过关闭宿主或修改插件配置处理。最终实际模型加载、原生姿态、68项战斗探针和52项基地回归没有ERROR或未绑定动画轨道；原有导航网格/单元高度告警仍见日志。

模型没有手指骨骼，握持采用手掌挂点；现有六武器几何仍是阶段性素材。当前工作不包含新增手指蒙皮、为每把武器制作独立整套原生动作或替换首领。
