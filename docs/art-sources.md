# 免费美术素材搜集与选型（可直接照着下载）

> **用途**：给"现在就想把灰盒换成能看的东西"的人一份**可以直接下载的清单**。
> **项目**：`F:\Godot项目\项目a`（Godot 4.7 / Forward+ / C#，第三人称动作 Roguelike，**目前零美术资源**）。
> **相关**：[art-onboarding.md](art-onboarding.md)（接入说明）、[visual-inventory.md](visual-inventory.md)（88 个视觉元素清单）、[assets.md](assets.md)（目录与命名规范）。
> **本次没有下载任何资源、没有改任何 `.cs` / `.tscn` / `.tres`**；本文档是唯一产物。
> **采集时间点**：扫描时 `Scenes/Interactables/`、`Scenes/UI/`、`Scripts/Interaction/`、`Scripts/Run/` 正由**另外两个代理**改动 —— 本文档里凡涉及这四个目录的步骤都标了 **⚠️冲突**。

---

## 0. 30 秒结论

| 问题 | 答案 |
|---|---|
| 一套 CC0 就够用吗？ | **能覆盖 80%**：`Quaternius`（角色/敌人/枪/环境）+ `Binbun`（Godot 原生 3D 特效）+ `Kenney`（UI/字体/音效/粒子贴图）三家全是 CC0，且**风格互不冲突**（都是低多边形扁平色块）。 |
| 一共找到多少候选？ | **32 条已验证**（我实际打开页面、读到许可与价格条款：角色 9 + 特效 8 + 环境 6 + UI 5 + 音效 3 + 枪械 1）+ **23 条次级候选**（只在已验证页面里被提及、或只在搜索结果里出现，页面本身没验证：特效 7 + 角色/动画 9 + UI 3 + 主表里标 ⚠️ 的 4）。 |
| 哪个坑最容易踩？ | **朝向**。glTF 规范规定模型正面朝 **+Z**，Godot 规定相机前方是 **-Z** —— 这两个约定**天然差 180°**，所以绝大多数 `.glb` 角色导入后是**背对着你**的。见 §4.2。 |
| 最省事的第一步？ | 见 §5「最小可玩示例」：5 个文件，**零 C# 改动**。 |

### 0.1 本清单的"验证状态"约定（重要，别跳）

| 标记 | 含义 |
|---|---|
| ✅**已验证** | 我用工具**实际打开了该页面**，下面的许可 / 价格 / 格式是**从页面正文读到的**，不是推测。 |
| ⚠️**仅提及** | 该链接出现在**我已验证的页面正文里**（是官方推荐位/相关链接），但我**没有单独打开它**。许可与价格**未核实**。 |
| ❓**仅搜索** | 只在搜索结果里出现过，**没有打开过**。当成"线索"，不要当成事实。 |

> **我做不到的事，先说清楚**：本次**没有下载任何素材包**（任务明确禁止），所以：
> - **轴心位置（原点在脚底还是中心）**：**没有任何一个包被我验证过**。官方页面与 README **都不写这一项**（Kenney 的两篇 3D 导入文档、KayKit 的 README、Quaternius 的 FAQ 我全读了，**都没有提到 pivot/origin**）。
>   → 因此 §4.1 给的是**判据 + 一条 5 行验证脚本**，不是结论。**不要照抄别人的"通常在哪"，按 §4.1 自己量一次。**
> - **默认朝向**：同上，**逐包未验证**。但 §4.2 给了**规范级证据**（glTF 规范 + Godot 官方文档），可以推出**大概率结果**与**确定的修正量**。
> - **文件体积**：只有 itch.io 页面明确写了（我照抄了）；Kenney 的包**页面不标体积**，我不编。

---

## 1. 项目需求 → 素材映射

对照 [visual-inventory.md](visual-inventory.md) §3 / §5 / §6 / §7，本项目需要的东西与"当前实现"：

| 类别 | 需要几个 | 现在是什么 | 备注 |
|---|---|---|---|
| 低多边形角色 | 玩家 1 + 敌人 9 + Boss 1 + 木桩 3 + 无人机 1 = **15** | `CapsuleMesh` + 纯色 | 高度 1.0 ~ 2.8 米，**原点在脚底**，**必须朝 -Z** |
| 武器 | 手枪 1（3 把武器共用同一个场景） | `BoxMesh` 0.1×0.16×0.34 | `Muzzle` 在 **-Z** |
| 交互物 | 奖励节点 1 种（10 实例）+ Boss 门 1 种 + 基地 4 个台子 | 圆柱 / 方块 + 纯色 | ⚠️**冲突**：`Scenes/Interactables/` 正被改 |
| 环境 | 地面 / 柱子×3 / 墙 / 斜坡 / 斜坡底座 = **7** | `BoxMesh` + 灰材质 | 柱子 3 根**共享同一 mesh+材质**，改一处即可 |
| 天空 | 1 | 内联 `ProceduralSkyMaterial` | 换天空盒贴图是**改 1 个文件**的大收益项 |
| 特效 | 枪口 / 命中 / 爆炸 / 治疗 / 升级 / 尘土 = **6 类** | **全项目 0 个粒子** | 从零加 |
| UI | 面板 / 按钮 / 血条 / 准星 / 字体 | 纯色 `ColorRect` + 默认主题 | ⚠️**冲突**：`Scenes/UI/` 正被改；且 `HealthBarFill` 必须仍是 `ColorRect` |
| 音效 | 射击 / 命中 / 爆炸 / UI 点击 | **0 个** | 可选 |

---

## 2. 候选清单（已验证）

### 2.1 低多边形角色（玩家 + 9 敌 + Boss + 无人机）

> **选型建议**：本项目是**科幻 / 暗色竞技场**，且要 15 个造型。**最省事的是只挑一家**（风格天然统一、色板一致）：
> - 想要**科幻机器人**敌人 → **Quaternius Sci-Fi Essentials Kit**（一家全包，含枪和道具）
> - 想要**人形/怪物**主角团 → **KayKit**（骨架统一、动画最多、有免费动画库）
> - 想要**最"低多边形扁平色块"**（最贴近当前灰盒气质）→ **Kenney Mini 系列**

| # | 名称 | 链接 | 许可 | 价格 | 格式 | 含动画/骨骼 | 状态 |
|---|---|---|---|---|---|---|---|
| C1 | **Quaternius · Sci-Fi Essentials Kit** | https://quaternius.itch.io/sci-fi-essentials-kit | **CC0**（itch 页面"Asset license"栏 = *Creative Commons Zero v1.0 Universal*；正文亦写 CC0）**无需署名、可商用** | 免费版 = **39 个模型**（itch 图上标 "39 completely free assets"）；Pro = **$9.99**（全部 85 个模型）；Source = **$14.99**（+ `.blend` + **已实现的 Godot 4.3 工程** + 全息/星球/闪屏自定义 shader） | **.FBX / .OBJ / .glTF** | **含动画**（"animated robot enemies"）；蒙皮骨骼 ✅ | ✅**已验证** |
| C2 | **Quaternius · Bestiary - Dungeon Monsters Kit** | https://quaternius.itch.io/bestiary-dungeon-monsters-kit | ⚠️**不是 CC0**，是 **QAL v1.0**（新许可）：**免费商用、无需署名**，但**禁止把素材本身转售/再分发**（做成游戏卖没问题） | 免费版 45 MB（7 只怪）；Source = $20 | 页面上传 `.zip`（Quaternius 惯例为 FBX/OBJ/glTF） | **有骨骼、无动画**（页面明写 *"This kit doesn't include any animations"*）；Humanoid rig，可配 Universal Animation Library 重定向 | ✅**已验证** |
| C3 | **Quaternius · Ultimate Modular Men Pack** | https://quaternius.com/packs/ultimatemodularcharacters.html | **CC0** | 免费（Patreon 有 Source 版） | **FBX / OBJ / glTF / Blend** | **11 个角色 / 24 个动画**，已蒙皮 ✅ | ✅**已验证** |
| C4 | **Quaternius · Ultimate Monsters** | https://quaternius.com/packs/ultimatemonsters.html | **CC0** | 免费 | **FBX / OBJ / Blend / glTF** | **50 只全动画怪物** ✅ | ✅**已验证** |
| C5 | **Quaternius · Animated Robot Pack** | https://quaternius.com/packs/animatedrobot.html | **CC0** | 免费 | **FBX / OBJ / Blend**（**无 glTF**） | 1 只"可爱机器人"，**有动画** ✅ | ✅**已验证** |
| C6 | **KayKit · Character Pack : Adventurers** | https://kaylousberg.itch.io/kaykit-adventurers | **CC0**（itch"Asset license"栏 + 正文 + [GitHub README](https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0) 三处一致）。**无需署名、可商用** | 免费版 = **5 角色 + 25+ 配件**（12 MB）；**Extra = $7.95**（+3 角色 + 每角色 3 套换色贴图）；Source = **$11.95**（+ `.blend`） | **.FBX / .GLTF**（含 **1024×1024** 渐变图集，可降到 128²） | **已绑定 + 含动画**；Source 版含 **75 个动画**。另有**免费**动画包 C10 | ✅**已验证** |
| C7 | **KayKit · Character Pack : Skeletons** | https://kaylousberg.itch.io/kaykit-skeletons | **CC0** | 免费版 4 只骷髅；Extra 付费 | **.FBX / .GLTF** | **已绑定 + 90+ 动画** ✅ | ✅**已验证** |
| C8 | **Kenney · Mini Characters** | https://kenney.nl/assets/mini-characters | **CC0** | 免费 | Kenney 3D 素材统一发 **GLB**（见 [Kenney KB](https://kenney.nl/knowledge-base/game-assets-3d/importing-3d-models-into-game-engines)：*"The glTF file format is distributed as GLB in Kenney game assets"*） | 页面 Features 标 **🎞️Animation**（有动画）；**25 个文件** | ✅**已验证** |
| C9 | **Kenney · Mini Arena** | https://kenney.nl/assets/mini-arena | **CC0** | 免费 | GLB | 标 **🎞️Animation**；v1.1 专门加了 **"Character rig"**；20 个文件 | ✅**已验证** |
| C10 | **KayKit · Character Animations** | https://kaylousberg.itch.io/kaykit-character-animations | 在 C6 页面上标为 **free**；**许可未单独核实** | 免费 | — | 动画库（可重定向到 C6/C7 的骨架上） | ⚠️**仅提及** |

**逐条适配性评估**

- **C1 Sci-Fi Essentials Kit**（**本项目首选**）：它是唯一一个"**敌人 + 枪 + 道具 + 场景小件**同一套科幻风格"的免费包，直接命中本项目的题材。**注意**：① 免费版只有 39 个模型，9 种敌人可能需要用**换色 + 缩放**（`EliteModifier` 已经在做缩放，配色本来就是项目里的区分手段，见 [visual-inventory.md](visual-inventory.md) §3）；② 官方说 Source 版工程是 **Godot 4.3** 做的，并声明"**高于此版本的都可以**"，本项目 **4.7** 在范围内，但 `.tscn` 直接搬进来仍有版本差异风险；③ **免费版 159 MB 解压后更大**，别整个塞进仓库。
- **C2 Bestiary**：**7 只怪 × 3 套配色 = 21 种外观**，正好能铺满 9 种敌人 + Boss（Boss 挑最大的一只放大）。**它没有动画**，所以接进来是"静态摆件"或需要另配 C14/C15/C16 动画库。**许可不是 CC0 而是 QAL** —— 对本项目（做游戏卖）**完全够用**，但**不能**把模型原样再发布出去。
- **C3/C4/C5**：`C3` 提供人形底模（11 个），`C4` 提供怪物（50 个），`C5` 提供机器人。三者都是 CC0 且风格接近，可以**混用**。**注意 C5 没有 glTF**，只有 FBX/OBJ/Blend —— Godot 4 导入 FBX 需要 **FBX2glTF**（Kenney 的 [KB](https://kenney.nl/knowledge-base/game-assets-3d/importing-characters-and-animations) 也是这么写的："make sure to use (and/or download) FBX2glTF when prompted"）。**优先选带 glTF 的包能省一步**。
- **C6/C7 KayKit**：**美术质量最高、动画最全、骨架统一**，还配免费动画库。**唯一的问题是题材**：它是**剑与魔法/地牢**风（骑士、法师、游侠、骷髅），放进科幻竞技场会**风格打架**。若能接受"奇幻竞技场"的设定，`C6 + C7 + C10` 是**最省事的一整套**（同一个作者的骨架，动画互通）。**1024² 图集可降到 128²**，体积友好。
- **C8/C9 Kenney Mini**：**块头最小、最"扁平色块"**，与项目现状（`CapsuleMesh` + 纯色）气质最接近，**替换后违和感最低**。代价是造型偏"可爱小人"，9 种敌人的**辨识度要靠配色和体型**，不是靠造型。`C9` 自带竞技场（柱子/看台），可直接当竞技场用。

### 2.2 特效 / 粒子（枪口火光、命中火花、爆炸、治疗、升级、落地尘土）

> **重大利好**：**Binbun（binbun3d.itch.io）** 专门做 **Godot 4.x 的 3D VFX**，全部 **CC0**、**拖进场景就能用**（"Ready to use. Simply drag and drop into your scene."），带**工具脚本**可在编辑器里改颜色/亮度/速度/一次性播放。
> 本项目目前 **0 个粒子**，用这些包比自己写 `GPUParticles3D` 快得多。

| # | 名称 | 链接 | 许可 | 价格 | 格式 | 覆盖本项目哪一项 | 状态 |
|---|---|---|---|---|---|---|---|
| F1 | **Binbun · Muzzle Flash VFX - Godot 3D** | https://binbun3d.itch.io/muzzle-flash-vfx | **CC0**（页面"Licensing"段 + itch"Asset license"栏一致）**无需署名、可商用** | **免费**（Name your own price；主包 1.7 MB）；> $0.75 加 12 张额外贴图 | **Godot 4.x 原生** `.tscn` + `.gd` + 贴图 + shader | **枪口火光**（24 种：6 中 / 6 小 / 6 极小 / 6 大）+ 12 张贴图 | ✅**已验证** |
| F2 | **Binbun · Hit FX - Godot 3D Effects** | https://binbun3d.itch.io/hit-fx | **CC0** | 免费版 39 kB（**6 个预设**）；Full = **$5.94**（28 个预设） | Godot 4.x `.tscn` | **命中火花**（近战/子弹命中），带"flare / streak / explosion" shader | ✅**已验证** |
| F3 | **Binbun · Explosion FX - Godot 3D Effects** | https://binbun3d.itch.io/explosion-fx | **CC0** | 免费版 976 kB（**3 个预设**：地面爆炸 / 空中爆炸 / 蘑菇云）；Full = **$5.94**（16 个） | Godot 4.x `.tscn` | **爆炸**（替换 `Scenes/Combat/Explosion.tscn` 里那个半透明球） | ✅**已验证** |
| F4 | **Binbun · Portal VFX - Godot 3D** | https://binbun3d.itch.io/godot-portal-vfx | **CC0** | 免费版 27 kB（1 个可调 portal）；Full = **$5.99**（33 个预设） | Godot 4.x `.tscn`（用 **stencil buffer**） | **奖励节点的"降临"光圈** / Boss 门 / 传送门 | ✅**已验证** |
| F5 | **Binbun · Impact VFX - Godot 3D** | https://binbun3d.itch.io/impact-vfx | **CC0** | **付费**：$5.99 → 特价 **$4.49**；有免费 Demo（333 kB） | Godot 4.x `.tscn` | 4 大爆炸 + 3 小爆炸 + **7 地面冲击** + **9 命中**（地面冲击正好当**落地尘土**） | ✅**已验证** |
| F6 | **Brackeys' VFX Bundle** | https://brackeysgames.itch.io/brackeys-vfx-bundle | **CC0**（页面"LICENSE"段明写 *"Attribution is not required"*） | **免费**（Name your own price），**26 MB** | **粒子贴图 + flipbook 序列帧 + 预绘 spritesheet**（.png） | **原始素材**：自己搭 `GPUParticles3D` 时的弹药库。含 Kenney / Picster / Thomas Iché / CodeManu 四方贴图 | ✅**已验证** |
| F7 | **Kenney · Particle Pack** | https://kenney.nl/assets/particle-pack | **CC0** | 免费 | **512×512 PNG**，**80 个文件** | 通用粒子贴图（星火、光斑、噪声） | ✅**已验证** |
| F8 | **Kenney · Smoke Particles** | https://kenney.nl/assets/smoke-particles | **CC0** | 免费 | PNG，**70 个文件**，标签含 `smoke` / `explosion` | **落地尘土**、爆炸烟、二段跳烟 | ✅**已验证** |

**逐条适配性评估**

- **F1 是本项目性价比最高的一条**：`Scenes/Weapons/Pistol.tscn` 里已经有一个叫 **`Muzzle`** 的 `Marker3D`（位置 `(0,0,-0.24)`，**必须保持这个名字**，`Scripts/Weapons/Weapon.cs:95` 按名取），枪口火光**挂它下面**即可。
  **⚠️ 已知坑（来自该页面评论区，作者已确认）**：这批特效的 shader / 材质**默认是共享的**，多个实例会**互相影响**（"*it updates every glow mesh of every instance of the same VFX at the same time*"）。**修法（社区给出、作者认可）**：把 VFX 场景里的材质**设为 local to scene**，或把 shader 改成用 **instance uniform** 并在 `AnimationPlayer` 里给 instance 参数打关键帧。
  **⚠️ 版本**：作者说这包是 **Godot 4.6** 做的，在 4.4/4.5 测试过 —— 本项目 **4.7**，方向是"更高版本"，风险低。
- **F2 Hit FX 是最需要注意版本的一条**：评论区里有人在 **Godot 4.4.1** 上遇到 *"the animation `main` is not present on the nodes"*，**升级到 4.7 就好了**（*"In the end I migrated to Godot 4.7-stable and it works"*）。**本项目正好是 4.7，这条坑对我们已经关了**。
  **⚠️ 另一条**：有用户反馈**缩放不生效**（"*why cant this be scaled at all*"）—— 命中特效的**大小不好调**，如果嫌大，得改 shader/节点而不是改 `Scale`。
- **F3 Explosion FX 接进 `Explosion.tscn` 时的红线**：`Scenes/Combat/Explosion.tscn` 里的 **`SphereShape3D radius=1`** 是**玩法判定**（`Explosion.Detonate()` 用 `Scale = radius` 缩放），**不是美术**，别顺手改（[assets.md](assets.md) §7 也写了）。要换的是里面的 **`Mesh` 节点**（半透明球），且新视觉**基准半径必须是 1、轴心在球心**，否则爆炸范围视觉会跟判定对不上。
- **F4 Portal VFX 需要 Godot ≥ 4.5**：评论区里 4.4 用户的 `AnimationPlayer` 是空的，**升到 4.5+ 才好**（stencil 缓冲依赖）。本项目 4.7 没问题。
- **F5 Impact VFX 是唯一要花钱的**（$4.49）。**它的"7 个地面冲击"正好补项目缺的"落地尘土"**。可以先只下 333 kB 的 Demo 试。
- **F6 是"零件"不是"成品"**：给你贴图和 flipbook，**粒子系统还得自己搭**。本项目完全没有粒子经验的话，**建议 F1/F2/F3 打底 + F6/F7/F8 补贴图**。

### 2.3 环境（竞技场地面 / 墙 / 柱子 / 平台 / 天空）

| # | 名称 | 链接 | 许可 | 价格 | 格式 | 覆盖本项目哪一项 | 状态 |
|---|---|---|---|---|---|---|---|
| E1 | **Quaternius · Modular Sci-Fi MegaKit** | https://quaternius.com/packs/modularscifimegakit.html | **CC0** | 免费版 = **60~70% 的包**；Pro/Source 付费（$ 见 itch 页） | **FBX / OBJ / Blend / glTF** | **277 个模型**：`floors` / `walls` / `columns`（= 柱子）/ `doors` / `props` / `aliens`。**网格对齐（grid-based）**，正好替换 `TestArena.tscn` 的地面/柱/墙/斜坡 | ✅**已验证** |
| E2 | **Kenney · Space Station Kit** | https://kenney.nl/assets/space-station-kit | **CC0** | 免费 | GLB | 90 个文件，科幻室内（墙/地板/门/管道） | ✅**已验证** |
| E3 | **Kenney · Mini Arena** | https://kenney.nl/assets/mini-arena | **CC0** | 免费 | GLB | **20 个文件**：竞技场地面/看台/柱/围栏 —— **最适合"一个竞技场"这种需求量** | ✅**已验证** |
| E4 | **Kenney · Skyboxes Space** | https://kenney.nl/assets/skyboxes-space | **CC0** | 免费 | **贴图**，**5 个文件**（含 galaxy / band 变体） | **天空盒** —— 替换 `TestArena.tscn` 的 `WorldEnvironment` 天空，**改 1 个资源、全屏观感变化** | ✅**已验证** |
| E5 | **Kenney · Space Kit** | https://kenney.nl/assets/space-kit | **CC0** | 免费 | GLB | 150 个文件（星球/飞船/太空设施）—— 做远景装饰 | ✅**已验证** |
| E6 | **Quaternius · Sci-Fi Essentials Kit**（同 C1） | https://quaternius.itch.io/sci-fi-essentials-kit | **CC0** | 见 C1 | FBX/OBJ/glTF | 箱体 / 货架 / 控制台 / 全息屏 —— 基地那 4 个交互台的替身 | ✅**已验证** |
| E7 | **Kenney · Skyboxes** | https://kenney.nl/assets/skyboxes | **CC0**（同系列，页面未单独开） | 免费 | 贴图 | 天空盒备选（非太空题材） | ⚠️**仅提及** |
| E8 | **Kenney · Modular Cave Kit** | https://kenney.nl/assets/modular-cave-kit | **CC0**（同站惯例，页面未单独开） | 免费 | GLB | 洞穴风竞技场备选 | ⚠️**仅提及** |
| E9 | **Kenney · Factory Kit** | https://kenney.nl/assets/factory-kit | **CC0**（同上） | 免费 | GLB | 工业风竞技场备选 | ⚠️**仅提及** |

**逐条适配性评估**

- **E1 与 `TestArena.tscn` 的对应关系**（一眼看出该换哪几个）：
  | 场景里 | 现在 | 换成 E1 的 |
  |---|---|---|
  | `Ground/Mesh` | `BoxMesh` 80×1×80 墨绿 | `Floor_*`（地面模块，铺 8×8 网格） |
  | `Pillar1/2/3/Mesh` | 共享 `BoxMesh` 1.6×6×1.6 灰 | `Column_*`（**3 根柱子共享同一 mesh+材质，改一处即可**） |
  | `Wall/Mesh` | `BoxMesh` 12×5×1 灰 | `Wall_*` |
  | `Ramp/Mesh` + `RampBase/Mesh` | `BoxMesh` 6×0.6×8 绕 X 转 15° | 用 `Floor_*`/`Ramp_*` 拼，**⚠️ 倾斜角与碰撞体一致，别只改 mesh** |
  **注意**：E1 的模型是"**模块**"，不是"一整块地面"。铺 80×80 米要摆很多块 —— 如果只想**一个文件搞定地面**，E3（Kenney Mini Arena）或直接用**平铺贴图 + `Triplanar`**更省事。
- **E2/E3/E5 都是 GLB**，Godot 4 **原生导入、无需 FBX2glTF**，比 Quaternius 的 FBX 路径少一步。
- **E4 是本清单里"投入产出比"最高的一条**：一个 `.exr`/`.png` 天空盒 + `WorldEnvironment` 一行 `sky`，**整屏从灰变太空**，且**零代码、零节点名风险**。

### 2.4 UI（面板 / 按钮 / 血条贴图 / 字体 / 准星）

> ⚠️ **冲突警告**：`Scenes/UI/` 与 `Scripts/UI/` 正被**另一个代理**改动。**本节任何一步都先跟他协调**，否则必冲突。
> 且 [visual-inventory.md](visual-inventory.md) §8.3/§8.4 有三条硬约束：`Root/HealthBarFill` **被强转成 `ColorRect`**（换 `TextureRect` 会在 `_Ready` 抛异常）、卡片模板**必须是 `Button`**、`Root/Panel/VBox/...` 是**绝对路径**（中间插一层就炸）。

| # | 名称 | 链接 | 许可 | 价格 | 格式 | 覆盖本项目哪一项 | 状态 |
|---|---|---|---|---|---|---|---|
| U1 | **Kenney · UI Pack - Sci-Fi** | https://kenney.nl/assets/ui-pack-sci-fi | **CC0** | 免费 | PNG（**130 个文件**） | **科幻风**面板/按钮/滑条 —— 六个弹窗（奖励/商店/开始/基地/帮助/加载）的底板 | ✅**已验证** |
| U2 | **Kenney · UI Pack** | https://kenney.nl/assets/ui-pack | **CC0** | 免费 | PNG（**430 个文件**） | 通用 UI：按钮/面板/滑条（v2.0 完全重做） | ✅**已验证** |
| U3 | **Kenney · Crosshair Pack** | https://kenney.nl/assets/crosshair-pack | **CC0** | 免费 | PNG + **矢量**，**64×64**，**200 个文件** | **准星**（现在是 3 个 `ColorRect`：`CrosshairH`/`CrosshairV`/`CrosshairDot`） | ✅**已验证** |
| U4 | **Kenney · Kenney Fonts** | https://kenney.nl/assets/kenney-fonts | **CC0** | 免费 | 字体，**11 个文件** | HUD 与弹窗字体（现在全是**引擎默认字体**） | ✅**已验证** |
| U5 | **Oxanium**（Google Fonts） | 许可原文：https://raw.githubusercontent.com/google/fonts/main/ofl/oxanium/OFL.txt ；字体仓库：https://github.com/google/fonts/tree/main/ofl/oxanium ；官网页：https://fonts.google.com/specimen/Oxanium | **SIL OFL 1.1** —— **免费商用、可嵌入游戏**；**要求**：随附版权声明与许可文本（游戏里放个 credits/license 页最省事）；**不得单独售卖字体本身** | 免费 | **`.ttf`**（可变字重 `Oxanium[wght].ttf`） | **科幻感标题/数字字体**，Godot 原生支持 `.ttf`。比 Kenney 的像素字更"现代科幻" | ✅**已验证**（OFL 原文 + 仓库） |

**逐条适配性评估**

- **U1/U2 是"贴图"不是"主题"**：本项目**没有任何 `Theme`/`StyleBox` 资源**（[visual-inventory.md](visual-inventory.md) §6.3）。拿到 PNG 后要**自己建一个 `Theme`**：
  `Assets/UI/ui_theme_base.tres` → 挂到 `project.godot` 的 **`gui/theme/custom`**。
  **这样做的最大好处**：**完全不碰任何场景节点名**，`Root/Panel/VBox/...` 这些绝对路径**一个都不动**，绕开了 §8.3 的所有雷。
- **U3 准星可以直接用**：`CrosshairH`/`CrosshairV`/`CrosshairDot` **没有任何代码引用**（[visual-inventory.md](visual-inventory.md) §8.3 表里没有它们），可以换成 `TextureRect` 或干脆换成一张准星图。这也是**零代码**的。
- **U4 vs U5**：Kenney 字体是**像素/方块风**（CC0，最省心）；Oxanium 是**科幻无衬线**（OFL，需带许可文本）。**两者都免费商用**。想要"科技感"选 U5，想要"零义务"选 U4。
- **⚠️ 血条**：`Root/HealthBarFill` **必须是 `ColorRect`**（`PlayerHud.cs:46` 强转），且宽度被 `PlayerHud.cs:16` 的 `HealthBarWidth = 260f` **每帧覆盖**（改场景宽度不生效）。**所以血条不能直接换成贴图** —— 想换就得**同时改 C#**。**建议第一轮先不动血条**。

### 2.5 音效（可选）

| # | 名称 | 链接 | 许可 | 价格 | 格式 | 覆盖本项目哪一项 | 状态 |
|---|---|---|---|---|---|---|---|
| A1 | **Kenney · Sci-fi Sounds** | https://kenney.nl/assets/sci-fi-sounds | **CC0** | 免费 | **70 个文件**（`.ogg`，见 [Kenney KB](https://kenney.nl/knowledge-base/general/convert-ogg-files-to-other-audio-formats) 证实其音频为 OGG） | 射击、能量、引擎、科幻交互 | ✅**已验证** |
| A2 | **Kenney · Impact Sounds** | https://kenney.nl/assets/impact-sounds | **CC0** | 免费 | **130 个文件** | **命中**、碰撞、打击（130 种 Foley） | ✅**已验证** |
| A3 | **Kenney · Interface Sounds** | https://kenney.nl/assets/interface-sounds | **CC0** | 免费 | **100 个文件** | **UI 点击**、确认、取消、升级提示 | ✅**已验证** |

**适配性评估**：三家都是 CC0、`.ogg`、**正好匹配 [assets.md](assets.md) §1 的 `Assets/Audio/sfx_*.ogg` 约定**。`A3` 可直接喂给六个弹窗的 `Button`（`Button` 有 `theme_override` 音效，或统一挂在 `Theme` 上，**零代码**）。
**注意**：Kenney 下载页有一个"建议捐赠"的中间页，**点 "Continue without donating..." 才是免费直链**（`<a>` 文本会被抓成"Thanks for downloading"）。**不用付钱**。

---

## 3. 次级候选（未验证，当线索用）

> 这些**只在搜索结果里出现**，或**只在我已验证的页面正文里被推荐**。**许可与价格一律未核实**，用之前必须自己开页面确认（见 §7 的核对清单）。

### 3.1 特效 / 粒子

| 名称 | 链接 | 线索来源 | 为什么可能有用 |
|---|---|---|---|
| **Nolka's pyrotechnics \| Explosion VFX for Godot** | https://nolkaloid.itch.io/nolkas-pyro-vfx-godot | F3（Bunbun Explosion FX）页面正文推荐 | 同为 Godot 的爆炸 VFX，可对比 |
| **CodeManu · VFX Free Pack** | https://codemanu.itch.io/vfx-free-pack | F6（Brackeys）页面正文，是其素材来源之一 | 预绘 spritesheet |
| **Picster · Godot particle and VFX textures** | https://github.com/RPicster/Godot-particle-and-vfx-textures | F6（Brackeys）页面正文，是其素材来源之一 | **Godot 专用粒子贴图仓库**，免费 |
| **Binbun · Godot VFX Bundle - 700+ Assets** | https://itch.io/s/178769/godot-vfx-bundle-700-assets | F1/F4/F5 页面正文（$35，含 27 个包） | **一次性买齐**（若决定用 Binbun 路线） |
| Binbun 其它同系列包 | https://binbun3d.itch.io/smoke-vfx · /loot-vfx · /status-fx · /beam-vfx · /flame-fx · /electric-fx · /magic-projectile-vfx · /magic-area-vfx · /magic-orb-vfx · /ice-vfx · /poison-vfx · /dark-magic-fx · /elemental-magic-fx | 各已验证页面侧栏 | `smoke-vfx` = 尘土/烟；`loot-vfx` = 奖励拾取；`status-fx` = 增益/治疗光环；`beam-vfx` = 远程/激光 |
| **Pimen · Battle VFX: Hit Spark** | https://pimen.itch.io/battle-vfx-hit-spark | itch 标签页搜索结果 | 命中火花备选（**风格偏像素 2D**，未必合 3D） |
| **Brackeys · brackeys-particle-controls** | https://github.com/Brackeys/brackeys-particle-controls | F6 页面正文 | Godot 编辑器里**预览粒子**的插件，调试 `GPUParticles3D` 省时间 |

### 3.2 角色 / 动画

| 名称 | 链接 | 线索来源 | 为什么可能有用 |
|---|---|---|---|
| **Quaternius · Universal Animation Library** | https://quaternius.itch.io/universal-animation-library (120+) · https://quaternius.itch.io/universal-animation-library-2 (130+) | Quaternius 官方 itch 主页 | C2 Bestiary **没动画**，可与它重定向搭配 |
| **Quaternius · Universal Base Characters** | https://quaternius.itch.io/universal-base-characters | Quaternius 官方 itch 主页 | 人形底模 |
| **Quaternius · 50+ LowPoly Guns** | https://quaternius.itch.io/50-lowpoly-guns | Quaternius 官方 itch 主页 | **手枪模型**（现在是个方块）—— 也可用 C1 里的枪 |
| **Quaternius · LowPoly Robot** | https://quaternius.itch.io/lowpoly-robot | Quaternius 官方 itch 主页 | **无人机**候选 |
| **Quaternius · LowPoly Animated Monsters** | https://quaternius.itch.io/lowpoly-animated-monsters | Quaternius 官方 itch 主页 | 与 C4 可能同源 |
| **Quaternius · LowPoly Animated Easy Enemies** | https://quaternius.itch.io/animated-easy-enemies | Quaternius 官方 itch 主页 | 小怪 |
| **Poly Pizza**（站） | https://poly.pizza | 搜索结果 | **10,700+ 免费低模**；**逐个模型许可不同**。**已单独验证一个**：https://poly.pizza/m/QCm7qe9uNJ 『Animated Robot』by Quaternius = **FBX/GLTF + Public Domain (CC0) + 带动画** |
| **OpenGameArt**（站） | https://opengameart.org | 搜索结果 | 站点可达、高级搜索可用（[3D Art 搜索](https://opengameart.org/art-search-advanced?field_art_type_tid%5B%5D=10)）。**但没有找到能一次覆盖 15 个角色的成套 CC0 科幻包** —— 结果多是散件（[Low Poly Robot](https://opengameart.org/content/low-poly-robot) 等），逐件许可需自查。**建议只用来补单个缺口，别指望成套** |
| **Kenney · Blaster Kit** | https://kenney.nl/assets/blaster-kit | 与 E2/E3 同批 | **CC0**，**40 个文件**，**3D 枪械**（v2.1 加了箱子/消音器/投掷物/烟雾）—— **这是我在 Kenney 站内验证过的**，可直接当手枪来源 |
| **KayKit GitHub 官方镜像**（10 个包，含 `Space-Base-Bits` / `Dungeon-Remastered` / `Prototype-Bits`） | https://github.com/KayKit-Game-Assets | 搜索结果 + 我拉取了 org 仓库列表 | **不用登录 itch 就能下**（`git clone`），且每个 repo 的 `README.md` 写明 CC0 |

### 3.3 UI

| 名称 | 链接 | 线索来源 |
|---|---|---|
| Kenney · Input Prompts | https://kenney.nl/assets/input-prompts | Kenney 站内列表 |
| Kenney · Fantasy UI Borders | https://kenney.nl/assets/fantasy-ui-borders | U1 页面推荐位 |
| Kenney · UI Pack - Adventure | https://kenney.nl/assets/ui-pack-adventure | U2 页面推荐位 |

---

## 4. 三个必踩的坑（逐条核对）

### 4.1 坑一：轴心位置（原点在脚底 vs 在中心）

**项目现状**（[visual-inventory.md](visual-inventory.md) §8.2）：

- 单位是 `CharacterBody3D`，**原点在脚底**（`Body` 的 `CollisionShape3D.transform.y = 高度/2`，胶囊底面正好在 local `y = 0`）。
- `Model`（`MeshInstance3D`）的 `transform.y` **也等于 高度/2**（玩家 1.8 高 → `y = 0.9`），**因为内置 `CapsuleMesh` 的轴心在几何中心**。

**所以规则只有一条，别记混**：

| 导入模型的轴心在哪 | `Model.transform.y` 应该设成 | 理由 |
|---|---|---|
| **几何中心**（= 现在 `CapsuleMesh` 就是这样） | **`高度/2`**（保持现状，玩家 = 0.9） | 中心对齐，脚正好落在 `y=0` |
| **脚底**（大多数 game-ready 角色的惯例） | **`0`** | 脚底对齐原点，脚正好落在 `y=0` |

> ⚠️ **两份项目文档在这里的措辞会让人绕晕**：[art-onboarding.md](art-onboarding.md) 第 54 行说"换成原点在脚底的模型会**下沉**半身高，要往上抬 高度/2"，而 [visual-inventory.md](visual-inventory.md) §8.2.2 给的改法是"**把 `Model` 的 y 改成 0**"。
> **按上表来**：脚底轴心 → `y = 0`（**往下改**，不是往上抬）；中心轴心 → `y = 高度/2`（保持现状）。**物理上只有一个正确答案，用上面那张表**。§8.2.2 的改法是对的。

**⚠️ 我无法替你验证任何一个包的轴心**（没下载）。**5 行脚本自己量一次，别猜**（跑法见 [assets.md](assets.md) §6.3，记得带 `ResourceLoader.CACHE_MODE_IGNORE`）：

```gdscript
var ps = ResourceLoader.load("res://Assets/Models/mesh_player_body.glb", "", ResourceLoader.CACHE_MODE_IGNORE)
var root = ps.instantiate()
var box = AABB(); var first = true
for c in root.find_children("*", "MeshInstance3D", true, false):
    var b = c.transform * c.get_aabb()      # local → root 空间
    box = b if first else box.merge(b); first = false
print("min.y = ", box.position.y, "   max.y = ", box.end.y, "   高度 = ", box.size.y)
print("轴心在脚底?", absf(box.position.y) < 0.05, "  → Model.transform.y 应为 ",
      (0.0 if absf(box.position.y) < 0.05 else box.size.y * 0.5))
```

判读：`min.y ≈ 0` → **轴心在脚底 → `Model.y = 0`**；`min.y ≈ -高度/2`（即 `max.y ≈ +高度/2`）→ **轴心在中心 → `Model.y = 高度/2`**。

**顺带一个更省事的做法**：不管轴心在哪，**别改 `Model` 的 y**，而是在 `Model` 下面加一层空 `Node3D` 当"对齐节点"，把补偿写在那一层。这样以后换包不用重新想。`Model` **没有任何代码引用，可以随便改名**（[art-onboarding.md](art-onboarding.md) 第五节）。

**另外两个连带项**（换高个子模型时必查，[visual-inventory.md](visual-inventory.md) §8.2）：

- **相机胸口高 1.5 米写死在 `CameraRig.cs:23`** 的 `TargetOffset = (0,1.5,0)`，`Player.tscn` **没有覆盖它**。换 2.5 米模型 → 在 `Player.tscn` 里覆盖 `TargetOffset.y` 到 ~1.8，或改代码。
- **手里枪的位置**：`Player.tscn:61-64` 的 `WeaponMount` 在 `(0.36, 1.15, -0.25)` → **手部高度约 1.15 米**。人形模型的**手要对得上这个高度**，否则枪会悬空/穿模。

**交互物的轴心**（换奖励节点/Boss 门时）：节点原点在**地面上方 0.25 米**（`NodePlacement.cs:62` / `MapManager.cs:217`）。**新模型的轴心放底面即可**（现状也是悬空 0.25 米，见 `docs/screenshots/challenger_mark2.png`）；想贴地就把节点原点下移 0.25。

### 4.2 坑二：朝向（模型必须默认朝 **-Z**）

**为什么本项目对这个特别敏感**：`CharacterMovement` **只做绕 Y 的旋转**，公式是 `Mathf.Atan2(-direction.X, -direction.Z)`（`CharacterMovement.cs:187-189`）。**模型朝错 180°，整个游戏里所有单位都是倒着走的**，而且**不会报错**。

**规范级证据（这条是确定的，不是我猜的）**：

| 来源 | 原文 | 结论 |
|---|---|---|
| [Godot 官方文档 · Model export considerations](https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/importing_3d_scenes/model_export_considerations.html) | *"Godot uses a right-handed, Y-is-up coordinate system, with **the -Z axis as the camera's forward direction**."* | Godot 里"前方" = **-Z** |
| 同上 | *"The convention for 3D assets is to face the opposite direction as the camera... codified in glTF as part of the glTF 2.0 specification. This means that for oriented 3D assets (such as characters), **the +Z axis is the direction of the front**, so -Z is the rear... In Blender, this means that **+Y is rear and -Y is front** for an asset."* | **glTF 资产正面朝 +Z** |
| [Khronos · glTF 2.0 资产制作指南](https://github.com/KhronosGroup/3DC-Asset-Creation/blob/main/asset-creation-guidelines/detail-version/CoordinateSystemandScaleUnit.md) | *"In glTF **+Y is world up, and the front of a glTF asset faces +Z**. The units for all linear distances are **meters**."* | 同上，且**单位是米**（正好匹配本项目：玩家 1.8 = 1.8 米） |

**推论（这才是关键）**：

> glTF 规范要求角色**正面朝 +Z**；Godot 要求角色**正面朝 -Z**。**两者天然相差 180°。**
> 所以：**一个"正确导出为 glTF"的角色，导入 Godot 后默认是背对着你的** —— 而本项目恰好要求朝 -Z，**等于要把它转回来**。

**具体操作**（两种，选一种，**别同时用**）：

1. **场景里加 180°**（可逆、不动资源）：在 `Player.tscn` / `Scenes/Enemies/*.tscn` 的 `Model` 节点上设
   `transform = Transform3D(-1, 0, 0, 0, 1, 0, 0, 0, -1, 0, <y>, 0)`（绕 Y 转 π）。
   **注意**：转了之后 `Model.y` 的补偿值不变（Y 轴旋转不动 Y 位移）。
2. **建模里转**：在 Blender 里把模型绕 Z 转 180° 再导出（**改一次，所有场景都受益**，但要重新导入）。

**⚠️ 必须实测确认，不要盲信**：上面是**规范推论**，但**实际朝向取决于导出器有没有遵守 glTF 约定**（Blender 的 glTF 导出器默认遵守，映射 `-Y 前 → +Z 前`；但有些导出器/有些包的作者建模时朝向就不标准）。**我没有下载任何包，所以逐包未验证**。**30 秒验证法**：

1. 把 `.glb` 拖进一个空场景，`Camera3D` 放在 `(0, 1.5, +5)` **朝 -Z 看**（即默认朝向）。
2. 如果看到角色的**正脸** → 它已经朝 -Z，**不用转**；如果看到**后脑勺** → **绕 Y 转 180°**；如果看到**侧面** → 这包没遵守约定，**转 ±90°**。

**枪的朝向同理，而且更硬**：`Pistol.tscn` 的 `Muzzle` 在 **`(0, 0, -0.24)`**，也就是**枪口朝 -Z**。**枪模型的枪口必须也在 -Z**，否则子弹从枪托后面飞出来。

### 4.3 坑三：骨骼 / 动画（本项目现在完全没有）

**现状**：全项目 grep `AnimationPlayer|Skeleton3D` → **0 命中**（[visual-inventory.md](visual-inventory.md) §1.3）。**没有一行动画代码。**

**接入成本（按由低到高）**：

| 方案 | 成本 | 说明 |
|---|---|---|
| **纯静态模型**（不带动画） | **最低，零代码** | 直接把 `.glb` 的 mesh 指给 `Model`。**9 个敌人 + Boss 都能这么干**（现在也只是个胶囊在滑行）。**推荐第一轮就这么做。** |
| **带动画的 `.glb` 当子节点** | **低**，但要新增少量代码 | 把 `.glb` 实例成 `Model` 的子节点，它自带 `AnimationPlayer`。**要动起来必须有代码**：`AnimationPlayer.play("idle"/"run")` 得有人调（现在没有任何调用点）。**建议新增一个 `Scripts/Combat/UnitAnimator.cs`**，按 `CharacterMovement` 的速度切 idle/run。 |
| **动画状态机（`AnimationTree`）** | **中** | 攻击/受击/死亡要接 `Unit` 的事件。 |
| **死亡动画** | **中，且要改代码** | `CharacterMovement` 在死亡时**直接 `return`**（`:130-133`），`UnitPresentation` 只是延迟 `DeathLinger`（Boss 2.5s / 其余 1.1s）后 `QueueFree`（`:99-109`）—— **没有倒地/布娃娃**。加死亡动画要新代码。 |

**三条和动画直接相关的既有机制（会影响你的接入方式，务必先读）**：

1. **`UnitPresentation` 的受击闪色是"递归"的**（`UnitPresentation.cs:70-81`，递归收集子树里**所有** `MeshInstance3D`）。→ **好**：换成蒙皮网格也会跟着闪；**坏**：手里挂的枪（`WeaponMount/Pistol/Body`）也会一起闪。
2. **`EliteModifier` 的精英染色"不递归"**（`EliteModifier.cs:55-61`，**只看直接子节点**）。→ **如果你把 `.glb` 实例挂在 `Model` 下面（三层深），精英怪就不会被染成紫色**，看起来"精英没生效"。**对策**：把 `.glb` 实例**直接挂成单位的直接子节点**，或把它放在 `Model` 这一层。
3. **`EliteModifier` 会直接改 `unit.Scale`**（默认 1.35，`SkillElite.tscn` 写 1.2），**连碰撞体一起放大**。→ **给敌人建模时留 20~35% 的余量**，别让精英怪卡在门框里。

**其它接入注意**：

- **`physics_interpolation = true`**（`project.godot`）：可见节点按渲染帧插值 → **加挂点/特效不需要自己插值**，但**不要把视觉节点做成"每物理帧瞬移"的独立对象**（会重新抖）。
- **动画模型的 T-pose**：Godot 官方提醒 *"Make sure that the skeleton is reset to its T-pose or default rest pose before exporting"*，否则导入后是扭曲的。
- **`Model` 的节点类型会变**：从 `MeshInstance3D` 变成**实例化的 `.glb` 场景**。**这是安全的** —— `Model` / `Body` **没有任何代码引用，可以随便改名/换类型**（[art-onboarding.md](art-onboarding.md) 第五节）。
- **`Body`（碰撞体）不要动**：所有单位的碰撞胶囊与 `Model` 同高同 y（如 `Chaser.tscn:34-40`）。**视觉可以高于碰撞**（碰撞只影响手感）。
- **`.glb` 导入设置**：Godot 4 **原生支持 glTF/GLB**（[Kenney KB](https://kenney.nl/knowledge-base/game-assets-3d/importing-3d-models-into-game-engines) 推荐 GLB："*GLB (recommended)*"）。**FBX 需要 FBX2glTF**，所以**优先选带 glTF/GLB 的包**（C1/C3/C4/C6/C7/E1/E2/E3/E5/C8/C9 都带，只有 C5 没有）。
- **颜色发灰/发紫**：Kenney KB 说 Godot 里选中贴图 → **Import 面板勾 "High Quality"** 可修颜色问题。

---

## 5. 最小可玩示例（只换 5 个东西，让画面不"灰盒"）

**挑选标准**：① 屏幕占比大（一眼看出来）；② **零 C# 改动**；③ **避开正在被改的四个目录**（`Scenes/UI/`、`Scenes/Interactables/`、`Scripts/Interaction/`、`Scripts/Run/`）。
**结论**：环境 + 天空 + 玩家 + 枪口火光 + 子弹拖尾。**全部零代码。**

| 序 | 换什么 | 下载（已验证 CC0） | 放到哪（照 [assets.md](assets.md) §1/§2） | 改哪个文件 | 关键注意 |
|---|---|---|---|---|---|
| **1** | **竞技场地面 + 柱子×3 + 墙 + 斜坡** | **Quaternius · Modular Sci-Fi MegaKit**<br>https://quaternius.com/packs/modularscifimegakit.html | `Assets/Models/mesh_level_ground.glb`<br>`Assets/Models/mesh_level_pillar.glb`<br>`Assets/Models/mesh_level_wall.glb`<br>`Assets/Models/mesh_level_ramp.glb`<br>（贴图→ `Assets/Textures/`） | `Scenes/Levels/TestArena.tscn`<br>的 `Ground/Mesh`、`Pillar1..3/Mesh`、`Wall/Mesh`、`Ramp/Mesh`、`RampBase/Mesh` | **柱子 3 根共享同一个 mesh + 材质 → 改一处，3 根一起变**（最省事的一步）。**斜坡的倾斜角与碰撞体一致，别只改 mesh**。若嫌模块化摆件太麻烦，**只想铺一块地面** → 改用 **Kenney Mini Arena**（https://kenney.nl/assets/mini-arena ）或给地面加**平铺贴图 + `Triplanar`** |
| **2** | **天空盒** | **Kenney · Skyboxes Space**<br>https://kenney.nl/assets/skyboxes-space | `Assets/Textures/tex_level_sky_space.png`（或 `.exr`） | `Scenes/Levels/TestArena.tscn` 的 `WorldEnvironment`（现在内联 `ProceduralSkyMaterial`）—— 把 `Environment.sky` 换成 `PanoramaSkyMaterial` 指向该贴图 | **投入产出比最高的一条**：**1 个资源、整屏从灰变太空、零代码、零节点名风险**。建议**第一个做** |
| **3** | **玩家模型**（+ 顺手定死轴心/朝向约定） | **Quaternius · Sci-Fi Essentials Kit**<br>https://quaternius.itch.io/sci-fi-essentials-kit<br>（备选：**KayKit Adventurers** https://kaylousberg.itch.io/kaykit-adventurers ；**Kenney Mini Characters** https://kenney.nl/assets/mini-characters ） | `Assets/Models/mesh_player_body.glb` | `Scenes/Player/Player.tscn` 的 `Model` 节点（`:51-53`）—— 只改 `mesh = ExtResource(...)` 一行 | **先按 §4.1 量轴心**：脚底轴心 → `Model.transform.y = 0`；中心轴心 → 保持 `0.9`。**再按 §4.2 试朝向**：背对镜头就绕 Y 转 180°。**手部高度要对得上 `WeaponMount` 的 1.15 米**（`Player.tscn:61-64`），否则枪悬空。**换成 2.5 米模型要同步改 `CameraRig.TargetOffset.y`**（现在写死 1.5，`Player.tscn` 没覆盖） |
| **4** | **枪口火光** | **Binbun · Muzzle Flash VFX - Godot 3D**<br>https://binbun3d.itch.io/muzzle-flash-vfx | `Assets/Effects/fx_weapon_muzzle.tscn`（整包按类型归位：`.tscn`→`Effects/`，贴图→`Textures/`，shader→`Effects/`） | `Scenes/Weapons/Pistol.tscn` —— 把特效实例挂到 **`Muzzle`** 节点下（位置 `(0,0,-0.24)`） | **⚠️ `Muzzle` 这个名字不能改**（`Weapon.cs:95` 按名取）。**⚠️ 材质默认共享，多个实例会互相干扰** → 把材质设成 **local to scene** 或用 **instance uniform**（§2.2 F1 有详情）。**⚠️ 播放要有人触发** → 需要在 `Scripts/Weapons/Weapon.cs` 加 1 行 `play()`（**这个文件不在冲突列表里**） |
| **5** | **子弹拖尾**（把"1 个方块"变成"1 道光"） | **Kenney · Particle Pack**<br>https://kenney.nl/assets/particle-pack<br>（+ 想要更花哨：**Brackeys' VFX Bundle** https://brackeysgames.itch.io/brackeys-vfx-bundle ） | 贴图 → `Assets/Textures/tex_combat_trail.png`；粒子资源 → `Assets/Particles/ps_combat_trail.tres` | `Scenes/Combat/Bullet.tscn` —— 在 `Mesh` 下加一个 `GPUParticles3D`，用上面的贴图做拖尾 | **零代码**（子弹在飞，粒子自然拖尾）。**`Bullet.tscn` 的 `Speed`/`MaxLifetime` 是玩法数值，别顺手改**（[assets.md](assets.md) §7）。`Mesh` 的材质是**强自发光 ×4.0**，粒子也用 `unshaded + additive` 才协调 |

### 5.1 建议的执行顺序

```
第 1 步：天空盒（§5-2）      ← 5 分钟，1 个文件，先建立"这不是灰盒了"的信心
第 2 步：环境（§5-1）        ← 屏幕占比最大；柱子改一处管 3 根
第 3 步：玩家（§5-3）        ← 顺便把 §4.1/§4.2 的约定一次定死，后面 14 个单位照抄
第 4 步：枪口火光（§5-4）    ← 战斗反馈从 0 到 1
第 5 步：子弹拖尾（§5-5）    ← 零代码
────────────────────────────────────────
第 6 步：9 个敌人 + Boss + 木桩（同一套包换色/缩放）
第 7 步：爆炸（F3）           ← ⚠️ 别碰 Explosion.tscn 的 SphereShape3D
第 8 步：UI Theme（U1/U2/U5） ← ⚠️ 等 Scenes/UI 那个代理收工
第 9 步：奖励节点 + Boss 门   ← ⚠️ 等 Scenes/Interactables 那个代理收工
```

**每步改完都要跑 [assets.md](assets.md) §6 的验证**（编译 → 自测 → 资源级校验 → 截图）。

---

## 6. 许可速查（结论先看这个）

| 许可 | 本清单里的包 | 要署名吗 | 能商用吗 | 额外义务 |
|---|---|---|---|---|
| **CC0 1.0**（公共领域） | **C1**(Quaternius Sci-Fi Essentials)、**C3/C4/C5**、**C6/C7**(KayKit)、**C8/C9/E2/E3/E4/E5**、**A1/A2/A3**、**U1/U2/U3/U4**、**F1~F8**、Kenney Blaster Kit | **不要** | **可以** | **没有**（最干净） |
| **QAL v1.0**（Quaternius 新许可） | **C2**(Bestiary - Dungeon Monsters Kit) | **不要** | **可以** | **不得把素材本身转售/再分发**（做成游戏卖没问题）。旧包仍是 CC0，QAL 只对新包生效 |
| **SIL OFL 1.1** | **U5**(Oxanium 字体) | 不强制列入游戏 credits，但**必须随附版权声明 + 许可文本** | **可以** | **不得单独售卖字体本身**；不得用保留字名发布修改版 |
| **未核实** | §3 全部次级候选，以及 **C10**、**E7/E8/E9** | — | — | **用之前自己看页面** |

**三条与本项目直接相关的提醒**：

1. **Kenney 的"捐赠"中间页不是付费墙** —— 点 **"Continue without donating..."** 拿免费直链。
2. **itch.io 的"Asset license"栏是作者自己选的**，与正文声明一般是同一个；本清单里 **F1~F5 我两处都读了，一致**。**但 §3 的包没读**。
3. **Quaternius 的许可页在 2026-08-28 更新成了 QAL**（https://quaternius.com/license.html ）。老包页面仍标 CC0。许可页自己写了：*"the version in effect at the time you obtained the Assets governs your use of them"* —— **本项目两种许可下都完全可用**（免费商用、无需署名），**唯一的差别是"不能把素材包本身再发布出去"**。

---

## 7. 我没做的事 / 用之前必须自己核对的

**本次的任务边界**：**只写了这一个文件**，没有下载任何资源、没有改任何代码或场景。

| # | 事项 | 状态 |
|---|---|---|
| 1 | **任何包的轴心位置（脚底/中心）** | **完全未验证**。官方页面与 README 都不写这项。→ 用 §4.1 的脚本自己量 |
| 2 | **任何包的默认朝向（+Z/-Z/±X）** | **逐包未验证**。§4.2 的"glTF 正面朝 +Z"是**规范级证据**（Godot 官方 + Khronos 官方），但**实际取决于导出器**。→ 用 §4.2 的"30 秒验证法"看一次 |
| 3 | **文件体积** | 只有 itch.io 页面写了（我照抄）；**Kenney 各包页面不标体积**，所以 Kenney 那几行**没有体积数据**（我不编）。Quaternius 的 Sci-Fi Essentials 免费包 = **159 MB** |
| 4 | **Quaternius 各包的具体格式** | `C1` 的格式（FBX/OBJ/glTF）是页面明写的；`C3/C4/C5` 的格式来自 quaternius.com 的图标区（`glTF` 图标在 `C3/C4` 有、**`C5` 没有**）。**压缩包内的实际目录结构未验证** |
| 5 | **§3 的 21 条次级候选** | **许可/价格一律未核实**。包括 `C10`(KayKit Character Animations)、`E7/E8/E9`、以及 Quaternius 的一批包 |
| 6 | **OpenGameArt 是否真的没有合适成套包** | 我只做了**一次** 3D Art 搜索（关键词 `low poly robot`，14 条结果），**结论是"没找到成套的"，不是"没有"**。要更深可以按标签再搜 |
| 7 | **模型在 Godot 4.7 里的实际导入结果** | 未验证（没下载）。已知的作者侧信息：`C1` 的 Source 工程是 **Godot 4.3**，作者声明"更高版本可用"；`F2`/`F4` 有用户在 4.7 上跑通 |
| 8 | **模型与项目现有视觉的最终观感** | **未做任何渲染对比**。选型建议基于页面预览图 + 项目文档描述，**没有截图比对** |

**每条下载前的 30 秒核对清单**（照做就不会翻车）：

1. 打开页面，确认 **"Asset license"** 栏 / **"Licensing"** 段落。
2. 确认**免费版**和**付费版**的**分界线**（很多包免费版只有 60~70%）。
3. 看**格式**：有 `.glb`/`.gltf` → Godot 4 原生导入；**只有 `.fbx` → 需要 FBX2glTF**，多一步。
4. 看**是否含动画**：页面写 *"Animated"* / *"rigged"* / 或明确写 *"doesn't include any animations"*（**C2 就是后者**）。
5. 下载后先跑 §4.1 的 5 行脚本量轴心，再按 §4.2 看一眼朝向，**最后才动场景**。

---

## 附：本清单里"许可最干净（CC0）"的那几个

如果只记 5 个链接，记这 5 个 —— **全部 CC0、全部免费、全部无需署名、全部可商用**：

| 用途 | 名称 | 链接 |
|---|---|---|
| 角色 / 敌人 / 枪（科幻，一套到底） | **Quaternius · Sci-Fi Essentials Kit** | https://quaternius.itch.io/sci-fi-essentials-kit |
| 环境（科幻模块化，277 件） | **Quaternius · Modular Sci-Fi MegaKit** | https://quaternius.com/packs/modularscifimegakit.html |
| Godot 原生 3D 特效（枪口/命中/爆炸/传送门） | **Binbun · Godot 3D VFX 系列** | https://binbun3d.itch.io/muzzle-flash-vfx · https://binbun3d.itch.io/hit-fx · https://binbun3d.itch.io/explosion-fx · https://binbun3d.itch.io/godot-portal-vfx |
| UI / 字体 / 音效 / 粒子贴图（一站配齐） | **Kenney** | https://kenney.nl/assets/ui-pack-sci-fi · https://kenney.nl/assets/kenney-fonts · https://kenney.nl/assets/sci-fi-sounds · https://kenney.nl/assets/particle-pack |
| 天空盒（1 个文件换整屏） | **Kenney · Skyboxes Space** | https://kenney.nl/assets/skyboxes-space |
