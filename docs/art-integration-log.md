# 美术素材接入记录（CC0 下载清单 + 实测轴心/朝向）

> **本次做了什么**：下载了一批 **CC0** 免费素材，按 [assets.md](assets.md) 的目录/命名约定放进 `Assets/`，
> 并按 [art-sources.md](art-sources.md) §5「最小可玩示例」把其中 5 项接进场景，**每一项都做了验证**。
> **采集/接入时间**：2026-10 · Godot 4.7.2 stable mono · 项目 `F:\Godot项目\项目a`
> **红线遵守**：**只下 CC0**。清单里标 **QAL v1.0** 的 `C2 Bestiary` **没有下载**。

---

## 0. 30 秒结论

| 问题 | 答案 |
|---|---|
| 下载成功几个？ | **16 个包**（Kenney 12 + Quaternius 1 + Binbun 3），全部 **CC0**，全部**免费、可商用、免署名**。合计约 **86 MB**。 |
| 跳过了什么？ | `C2 Bestiary`（**QAL v1.0，红线，不下**）；`C1 Sci-Fi Essentials Kit`（免费版 **159 MB > 100 MB** 阈值，按规则跳过，见 §3）。 |
| 接进去几个？ | **天空盒 ✅ / 竞技场 ✅ / 玩家模型 ✅ / 枪口火光 ✅ / 子弹拖尾 ⚠️（功能已验证，未拿到截图）**。 |
| 实测轴心 | **脚底（min.y = 0）**，Kenney 的 GLB 全部如此。玩家模型 → `Model.transform.y = 0`。 |
| 实测朝向 | **正面朝 +Z**（glTF 规范），导入 Godot 后**背对镜头 → 必须绕 Y 转 180°**。已用截图双向验证。 |

---

## 1. 下载清单（来源 / 许可 / 体积 / 放置路径）

所有包都用 `pwsh` 的 `Invoke-WebRequest` 下载。Kenney 的直链从素材页 HTML 里正则取
（`https://kenney.nl/media/pages/assets/<slug>/<hash>/<file>.zip`）；itch.io 的免费包走
`download_url` → `file/<upload_id>` 两步 POST（见 §4.2）。

### 1.1 Kenney（全部 CC0，包内自带 `License.txt`，已逐个打开确认写着 *Creative Commons Zero, CC0*）

| # | 包 | 来源 URL | 许可 | 压缩包体积 | 放置 |
|---|---|---|---|---|---|
| 1 | **Skyboxes Space** | https://kenney.nl/media/pages/assets/skyboxes-space/b3d0ebe0d4-1790850383/kenney_skyboxes-space.zip | CC0 | **7.92 MB** | `Assets/Textures/tex_level_sky_space.png`（取 `Skyboxes/skybox-space-nebula.png`，4096×2048，2.10 MB） |
| 2 | **Particle Pack** | https://kenney.nl/media/pages/assets/particle-pack/f8fe0f8cb8-1677578741/kenney_particle-pack.zip | CC0 | **15.00 MB** | `Assets/Textures/tex_combat_trail.png`（取 `PNG (Transparent)/trace_01.png`，512×512，38 KB） |
| 3 | **Mini Arena** | https://kenney.nl/media/pages/assets/mini-arena/88f977a0cb-1709220730/kenney_mini-arena.zip | CC0 | 0.73 MB | **未采用**（风格偏"角斗场"，被 Space Station Kit 取代） |
| 4 | **Mini Characters** | https://kenney.nl/media/pages/assets/mini-characters/bfc7e272b4-1774770718/kenney_mini-characters.zip | CC0 | 2.40 MB | `Assets/Models/player/mesh_player_body.glb`（取 `character-male-a.glb`，246 KB）+ `Assets/Models/player/Textures/colormap.png` |
| 5 | **Space Station Kit** | https://kenney.nl/media/pages/assets/space-station-kit/6475288f2e-1712749919/kenney_space-station-kit.zip | CC0 | 1.84 MB | `Assets/Models/level/mesh_level_*.glb`（10 个）+ `Assets/Models/level/Textures/colormap.png` |
| 6 | Blaster Kit 2.1 | https://kenney.nl/media/pages/assets/blaster-kit/261d80a716-1753959510/kenney_blaster-kit_2.1.zip | CC0 | 1.72 MB | **未接入**（手枪仍是 BoxMesh；`Assets/Models/` 里没放） |
| 7 | Sci-fi Sounds | https://kenney.nl/media/pages/assets/sci-fi-sounds/6b296f9ecf-1677589334/kenney_sci-fi-sounds.zip | CC0 | 5.88 MB | **未接入**（音效不在本次 5 步内） |
| 8 | Impact Sounds | https://kenney.nl/media/pages/assets/impact-sounds/87b4ddecda-1677589768/kenney_impact-sounds.zip | CC0 | 0.80 MB | 同上 |
| 9 | Interface Sounds | https://kenney.nl/media/pages/assets/interface-sounds/fa43c1dd4d-1677589452/kenney_interface-sounds.zip | CC0 | 0.83 MB | 同上 |
| 10 | Kenney Fonts | https://kenney.nl/media/pages/assets/kenney-fonts/8d5435c213-1677661710/kenney_kenney-fonts.zip | CC0 | 0.06 MB | 同上（U4，UI 目录正被另一个代理改，本次不碰） |
| 11 | UI Pack - Sci-Fi | https://kenney.nl/media/pages/assets/ui-pack-sci-fi/b67c2acd31-1724181109/kenney_ui-pack-space-expansion.zip | CC0 | 0.77 MB | 同上（页面 slug 与包名不同，实名 `ui-pack-space-expansion`） |
| 12 | Crosshair Pack | https://kenney.nl/media/pages/assets/crosshair-pack/5ef74bd405-1785950072/kenney_crosshair-pack.zip | CC0 | 3.12 MB | 同上 |

> Kenney 素材页**不需要**捐赠中间页 —— 直链就在页面 HTML 里（`media/pages/assets/.../*.zip`），
> 直接 GET 即可（不是 art-sources.md §2.5 提到的那个"点 Continue without donating"路径）。

### 1.2 Quaternius（CC0）

| # | 包 | 来源 URL | 许可 | 体积 | 放置 |
|---|---|---|---|---|---|
| 13 | **Modular SciFi MegaKit (Standard 免费版)** | https://quaternius.itch.io/modular-sci-fi-megakit | **CC0**（itch "Asset license" = *Creative Commons Zero v1.0 Universal*；包内 `License_Standard.txt` 亦写 CC0 1.0） | **46.31 MB** | **本次未接入**（纯 PBR：一个柱子模块就要 ~10 MB 的 BaseColor/Normal/ORM 贴图，风格也更"写实"；见 §5 记录） |

### 1.3 Binbun（Godot 原生 3D 特效，CC0）

| # | 包 | 来源 URL | 许可 | 体积 | 放置 |
|---|---|---|---|---|---|
| 14 | **Muzzle Flash VFX** | https://binbun3d.itch.io/muzzle-flash-vfx | **CC0**（itch "Asset license" 栏 = *Creative Commons Zero v1.0 Universal*） | **1.79 MB** | ✅ 接入：`Assets/Effects/fx_weapon_muzzle.tscn` + `Assets/Effects/muzzle/*`（2 shader / 2 gd / 5 material）+ `Assets/Textures/tex_weapon_muzzle_{long,side,front}.png` |
| 15 | Hit FX（免费版 6 预设） | https://binbun3d.itch.io/hit-fx | CC0（包内 `license.txt` 明写） | 0.04 MB | 未接入（本次 5 步内没有"命中火花"） |
| 16 | Explosion FX（免费版 3 预设） | https://binbun3d.itch.io/explosion-fx | CC0（包内 `license.txt` 明写） | 0.95 MB | 未接入（`Explosion.tscn` 的 `SphereShape3D` 是玩法基准，本次不动） |

### 1.4 落盘体积（`Assets/` 实际新增）

```
Assets/Effects                     1 文件      8.2 KB   (fx_weapon_muzzle.tscn)
Assets/Effects/muzzle              9 文件     17.7 KB   (2 shader + 2 gd + 5 material)
Assets/Models/level               10 文件     72.6 KB   (mesh_level_*.glb)
Assets/Models/level/Textures       1 文件      7.3 KB   (colormap.png)
Assets/Models/player               1 文件    241.1 KB   (mesh_player_body.glb, 含 32 段动画 + 骨骼)
Assets/Models/player/Textures      1 文件      8.5 KB   (colormap.png)
Assets/Particles                   1 文件      0.6 KB   (ps_combat_trail.tres)
Assets/Textures                    5 文件   2408.6 KB   (天空盒 2.10 MB + 枪口 3 张 + 拖尾 1 张)
─────────────────────────────────────────────────────
合计新增约 2.7 MB（未接入的包留在 F:\Godot项目\_art_staging\，没有塞进仓库）
```

---

## 2. ⚠️ 接入时必须处理的两个坑 —— **实测结论（不是照抄假设）**

### 2.1 轴心：**全部在脚底**（`min.y = 0.000`）

用 art-sources.md §4.1 的"量 AABB"GDScript（带 `ResourceLoader.CACHE_MODE_IGNORE`）逐包实测，
**Space Station Kit 的 10 个模块 + 玩家角色，min.y 全部 = 0.000**（pivot 在底面），没有一个在几何中心。

| 模型 | AABB（本地空间） | 轴心 | 结论 |
|---|---|---|---|
| `mesh_level_floor.glb` | min `(-0.5, 0, -0.5)` max `(0.5, 0.3, 0.5)`、1×0.3×1 | **脚底** | 顶面在 `y = +0.3` |
| `mesh_level_wall.glb` | 1×1×0.3 | **脚底** | |
| `mesh_level_pillar.glb` | 1×1×0.5 | **脚底** | |
| `mesh_level_ramp.glb` | 1×0.3×1 | **脚底** | |
| `mesh_level_structure.glb` | 0.85×0.125×0.85 | **脚底** | |
| `mesh_level_container.glb` | 0.575×0.6×0.575 | **脚底** | |
| `mesh_level_pipe.glb` | 0.246×0.5×0.285 | **脚底** | |
| **`mesh_player_body.glb`** | min `(-0.384, 0, -0.172)` max `(0.384, 0.6713, 0.168)`、**高 0.6713** | **脚底** | 见下 |

**因此（本项目约定，写死）：**

> **`Model.transform.y = 0`** —— 因为轴心在脚底，脚底对齐单位原点（单位原点本来就在脚底）。
> **`Model` 的 `y` 不再是 `0.9`**（那是"轴心在几何中心"时代的补偿值，只对旧的 `CapsuleMesh` 成立）。

**玩家模型还多一步：原生高度只有 0.6713 m**（Kenney Mini Characters 是 chibi 比例），
而碰撞胶囊是 1.8 m。所以要**缩放**：`scale = 1.8 / 0.6713 = 2.6814`。

实测结果（编辑器脚本，实例化 `Player.tscn` 后量 `Model` 子树的世界 AABB）：

```
Model world AABB: min=(-1.029, 0.0, -0.45) max=(1.029, 1.8, 0.462)
height=1.8   feet_on_ground(dy=0.0)
```

→ **`Model.transform = Transform3D(±2.6814, 0, 0, 0, 2.6814, 0, 0, 0, ±2.6814, 0, 0, 0)`**
（x/z 的符号见 §2.2；`y` 分量 = 2.6814 是缩放，节点原点仍是 0，**不要**再加 0.9。）

> **注意 `Body`（碰撞胶囊）一个字节都没动**：仍是 `CapsuleShape3D radius=0.4 height=1.8`、`y=0.9`。
> 视觉与碰撞现在**同高同底**（都从 `y=0` 到 `y=1.8`），手感不变。

### 2.2 朝向：**glTF 正面朝 +Z → 必须绕 Y 转 180°**（逐包实测）

**规范级推论**（art-sources.md §4.2）：glTF 2.0 规定资产正面朝 **+Z**，Godot 前方是 **-Z**，
两者天然差 180°。**本次实测确认了这个推论在 Kenney 的 GLB 上成立** —— 而且是用截图**双向验证**的：

| 步骤 | 做法 | 截图 | 看到了什么 |
|---|---|---|---|
| ① 不转 | `Model.transform` 只加缩放（无旋转） | [art_02_player_facing_BEFORE_fix.png](screenshots/art_02_player_facing_BEFORE_fix.png) | **角色的正脸**（眼镜、鼻子、嘴）对着镜头 → 说明模型正面朝 **+Z**，与 Godot 前方相反 |
| ② 绕 Y 转 180° | x/z 轴取负：`Transform3D(-2.6814,0,0, 0,2.6814,0, 0,0,-2.6814, 0,0,0)` | [art_03_player_facing_AFTER_fix.png](screenshots/art_03_player_facing_AFTER_fix.png) | **后脑勺**（深色头发，看不到脸）→ 与镜头同向，即朝 **-Z** ✅ |

**本项目约定（写死）：**

> **`.glb` 角色导入后一律绕 Y 转 180°**：`Model` 的 basis 里 **x 与 z 轴取负**、y 轴保持正
> （`Transform3D(-s,0,0, 0,s,0, 0,0,-s, 0,0,0)`）。
> 判据：**basis 的行列式必须 > 0**（两个轴取负 = 绕 Y 转 π，不是镜像）。
> **不要**用"缩放为负"的手法，`det < 0` 会让法线/背面剔除出错。

**枪械同理（更硬）**：`Pistol.tscn` 的 `Muzzle` 在 `(0,0,-0.24)`，即**枪口朝 -Z**。本次实测：
`VFX.global_transform.basis * Vector3(1,0,0)`（Binbun 特效的火光长轴）= `(0, 0, -1)` = **-Z** ✅
与 `Pistol` 的前方一致，子弹与火光同向。

---

## 3. 被跳过 / 失败的（连同原因）

| 项 | 状态 | 原因 |
|---|---|---|
| **Quaternius · Bestiary - Dungeon Monsters Kit（C2）** | **主动不下（红线）** | 许可是 **QAL v1.0**，**禁止再分发素材本身**。任务红线：只下 CC0。 |
| **Quaternius · Sci-Fi Essentials Kit（C1）** | **跳过（体积阈值）** | itch 页面列出的免费版（Standard）是 **159 MB**，**> 100 MB 阈值**，按任务规则"先跳过并记录"。它本来是 art-sources.md 推荐的玩家模型首选；本次用 **Kenney Mini Characters**（CC0、GLB、自带 32 段动画）替代，玩家模型这一步照样接成了。想要它就把 https://quaternius.itch.io/sci-fi-essentials-kit 的 `Sci-Fi Essentials Kit[Standard].zip`（159 MB，CC0）单独下一遍。 |
| **Quaternius · Modular SciFi MegaKit（E1）** | 下载成功，**本轮未接入** | 见 §5。 |
| **Quaternius · Ultimate Modular Men / Ultimate Monsters / Animated Robot（C3/C4/C5）** | **跳过（下不到）** | `quaternius.com` 的这三页下载按钮是坏的：`Itch.attachBuyButton({user:"quaternius", game:""})` —— **`game` 是空字符串**（`modularscifimegakit.html` 同一段脚本里 `game` 是有值的，这三页没有）。页面里也没有任何 `.zip` 直链。**没有卡在这里**，直接换 Kenney。 |
| **Binbun · Impact VFX（F5）** | 未下 | 付费（$4.49），不在"免费 CC0"范围内。 |
| **Kenney 的 9 个包**（Blaster Kit / 3 个音效包 / 字体 / UI Pack / 准星 / Mini Arena） | 下载成功，**本轮未接入** | 不在 §5 的 5 步里；其中 UI/音效还涉及**正在被另一个代理修改**的 `Scenes/UI/`，本次不碰。 |

**没有任何一条下载失败**（16/16 成功）。itch.io 的两步 POST 流程见 §4.2。

---

## 4. 复现用的命令（下次直接用）

### 4.1 Kenney（一条正则搞定直链）

```powershell
$r = Invoke-WebRequest -Uri "https://kenney.nl/assets/skyboxes-space" -UseBasicParsing
$zip = [regex]::Match($r.Content, 'https://kenney\.nl/media/pages/assets/skyboxes-space/[^"'']+\.zip').Value
Invoke-WebRequest -Uri $zip -OutFile "$stage\kenney_skyboxes-space.zip" -UseBasicParsing
```

> **坑**：页面 slug 与包名不一定一致 —— `ui-pack-sci-fi` 的包实际叫
> `kenney_ui-pack-space-expansion.zip`。**按正则抓，别拼文件名。**

### 4.2 itch.io 免费包（名字自定价 / 免费）—— 三步 POST

```powershell
$ua = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36'
$s  = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$r  = Invoke-WebRequest -Uri $base -UseBasicParsing -WebSession $s -Headers @{'User-Agent'=$ua}
$csrf = [regex]::Match($r.Content, 'name="csrf_token" value="([^"]+)"').Groups[1].Value

# ① 拿下载页（注意：这一步要 **form-encoded**，发 JSON 会失败）
$dl = (Invoke-WebRequest -Uri "$base/download_url" -Method POST -Body @{csrf_token=$csrf} `
        -WebSession $s -Headers @{'User-Agent'=$ua;'X-Requested-With'='XMLHttpRequest';'Referer'=$base} `
        -UseBasicParsing).Content | ConvertFrom-Json | Select-Object -ExpandProperty url

# ② 下载页里拿 upload_id（**记得 @() 包起来**，否则单元素会被当字符串、$uids[0] 只取到首字符）
$p     = Invoke-WebRequest -Uri $dl -UseBasicParsing -WebSession $s -Headers @{'User-Agent'=$ua}
$csrf2 = [regex]::Match($p.Content, 'name="csrf_token" value="([^"]+)"').Groups[1].Value
$uids  = @([regex]::Matches($p.Content, 'data-upload_id="(\d+)"') | ForEach-Object { [string]$_.Groups[1].Value })

# ③ 换签名直链（这一步必须发 **JSON**，发 form 会得到 "upload_id: expected database ID integer"）
$file = (Invoke-WebRequest -Uri "$base/file/$($uids[0])?source=game_download" -Method POST `
          -Body (@{csrf_token=$csrf2} | ConvertTo-Json -Compress) -ContentType 'application/json' `
          -WebSession $s -Headers @{'User-Agent'=$ua;'X-Requested-With'='XMLHttpRequest';'Referer'=$dl} `
          -UseBasicParsing).Content | ConvertFrom-Json | Select-Object -ExpandProperty url

Invoke-WebRequest -Uri $file -OutFile "$stage\pack.zip" -UseBasicParsing   # 链接 60 秒过期，别缓存
```

---

## 5. 目录约定的两处**必要偏离**（照实记录）

[assets.md](assets.md) §1 是"按资源类型分目录"。本次有两处做不到、只能加一层，原因都是 glTF 的硬约束：

1. **`Assets/Models/level/` 与 `Assets/Models/player/` 两个子目录**
   —— Kenney 的 `.glb` **不内嵌贴图**，它的 `images[].uri` 是**相对路径 `Textures/colormap.png`**
   （解析基准是 `.glb` 自己所在目录）。而 **Mini Arena 与 Mini Characters 的两张 `colormap.png`
   文件名相同、内容不同**（9106 B vs 8706 B）。放在同一个目录里必然互相覆盖。
   → 只能按包各给一个目录，让各自的 `Textures/colormap.png` 就近解析。
   **文件本身的名字仍严格照 §2 的 `<类别>_<分组>_<变体>` 约定**（`mesh_level_*.glb`、`mesh_player_body.glb`）。

2. **`Assets/Effects/muzzle/`**
   —— Binbun 的 `.tscn` / `.tres` 里 `ext_resource` 全是 `res://assets/BinbunVFX/...` 的绝对 res:// 路径，
   搬目录必须**改文件内容**。为了不把整套 `BinbunVFX` 厂商树原样搬进项目，只抽了枪口火光这一条链
   （2 shader + 1 gd 控制器 + 1 gd 灯光 + 5 material），**路径全部改写成 `res://Assets/...`**，
   并**按 §5 规矩 4 去掉了 `ext_resource` 上的 `uid=`**。贴图按类型归到 `Assets/Textures/`。

---

## 6. 接入的 5 项：改了什么 + 验证证据

### 6.1 天空盒 ✅

- **改**：`Scenes/Levels/TestArena.tscn` 的 `WorldEnvironment`。
  `ProceduralSkyMaterial` + `Sky` 两个 `sub_resource` 换成
  `[sub_resource type="PanoramaSkyMaterial"] panorama = ExtResource("1_sky")`（指向 `tex_level_sky_space.png`），
  `Environment` 加 `ambient_light_energy = 0.55`（默认 1.0 时整个白色地板过曝）。
- **证据**：`art_01_skybox_arena.png` —— 整屏从灰变紫色星云，零代码、零节点名风险。
- `background_mode = 2`（Sky）/ `ambient_light_source = 3`（Sky）/ `tonemap_mode = 2` 保持原值。

### 6.2 竞技场（地面 / 柱×3 / 墙 / 斜坡 / 斜坡底座）✅

- **改**：`Scenes/Levels/TestArena.tscn`。把 7 个 `MeshInstance3D` 的内联 `BoxMesh`
  换成 **Space Station Kit 的 GLB 实例**：
  `Ground/Mesh` 与 `Ramp/Mesh`、`RampBase/Mesh` 共用 `mesh_level_floor.glb`，
  `Pillar1/2/3/Mesh` 共用 `mesh_level_pillar.glb`（**改一处管 3 根**），`Wall/Mesh` 用 `mesh_level_wall.glb`。
- ⚠️ **踩到的坑（值得记）**：`.tscn` 里 `Transform3D` 的 12 个浮点数是 **行主序（rows）**，
  不是 `Transform3D(x_axis, y_axis, z_axis, origin)` 那种"列"直觉。用
  `str_to_var("Transform3D(0,0,1,0,1,0,-1,0,0,0,0,0)")` 让引擎自己解析可以一次问清楚
  （实测得到 `x_axis=(0,0,-1)`、`z_axis=(1,0,0)`）。
  另外 **父节点的旋转会再作用一次到子节点的 basis 上**：斜坡的 `Mesh` 子节点**只能写缩放 + 平移**，
  不能把父节点的旋转再乘一遍（我第一版就是乘重了，AABB 中心偏了 0.13 m）。
- **验证（数值）**：编辑器脚本逐件比"视觉世界 AABB 中心 vs 碰撞盒 AABB 中心"：

```
Ground    | vis_center=(0.0,  -0.5, 0.0)  col_center=(0.0,  -0.5, 0.0)  corner_delta=0.0  OK
Pillar1   | vis_center=(6.0,   3.0, -6.0) col_center=(6.0,   3.0, -6.0) corner_delta=0.0  OK
Pillar2   | vis_center=(-7.0,  3.0, -9.0) col_center=(-7.0,  3.0, -9.0) corner_delta=0.0  OK
Pillar3   | vis_center=(10.0,  3.0,  5.0) col_center=(10.0,  3.0,  5.0) corner_delta=0.0  OK
Wall      | vis_center=(0.0,   2.5,-16.0) col_center=(0.0,   2.5,-16.0) corner_delta=0.0  OK
Ramp      | vis_center=(-8.0,  1.3, -5.0) col_center=(-8.0,  1.3, -5.0) corner_delta=0.0  OK
RampBase  | vis_center=(-8.0,  1.2, -5.0) col_center=(-8.0,  1.2, -5.0) corner_delta=0.0  OK
```

  **7/7 全部 0 误差** —— 视觉和碰撞仍然严格重合（斜坡 15° 倾斜也没跑偏）。
  **所有 `CollisionShape3D` / `BoxShape3D` 一个字节没改**（[assets.md](assets.md) §7 红线）。
- **证据**：`art_01_skybox_arena.png`、`art_04_arena_player_final.png`。

### 6.3 玩家模型 ✅（顺手把 §2 的轴心/朝向约定定死）

- **改**：`Scenes/Player/Player.tscn` 的 `Model`：
  从 `MeshInstance3D + CapsuleMesh` 换成 **`mesh_player_body.glb` 的场景实例**
  （`Model` 类型变了 —— [art-onboarding.md](art-onboarding.md) §五 明确 `Model` 无代码引用，可以换类型）。
  `load_steps` 顺手改对（原来是 12，实际该是 22）。
  另外**修了一处会穿模的地方**：`WeaponMount` 从 `(0.36, 1.15, -0.25)` 改到 `(0.45, 0.70, -0.34)` ——
  chibi 角色的头占 `y ∈ [0.92, 1.8]`，原来 1.15 m 的枪是**插在脑袋里**的（实测数据见 §2.1）。
- **动画（零 C#）**：`.glb` 自带 32 段动画（`idle / walk / sprint / jump / die / attack-*` …）。
  在 `Player.tscn` 里给实例化子节点加了一条属性覆盖：
  `[node name="AnimationPlayer" parent="Model" index="1"] autoplay = "idle"`。
  > **坑**：`index` 是**兄弟节点序号**（GLB 根下 `character-male-a` 是 0、`AnimationPlayer` 是 1），
  > 写成 `index="0"` 会静默不生效。
  > 另：导入的 `idle` 是 `LOOP_NONE`，所以它是"播一遍停在最后一帧"（自然站姿）；要真正循环需要代码。
- **验证**：见 §2.1 的 AABB 输出（1.8 m、脚在 y=0）+ §2.2 的两张朝向截图。

### 6.4 枪口火光 ✅（含 1 行 C#）

- **改**：
  1. `Assets/Effects/fx_weapon_muzzle.tscn`（Binbun `muzzle_flash_01` 改名 + 改路径），
     根节点 `one_shot = true` / `autoplay = false`；
     根 transform 写死 `Transform3D(0,0,0.3, 0,0.3,0, -0.3,0,0, 0,0,0)`
     （**朝向**：把特效长轴转到 -Z；**尺寸**：0.3 缩到"手枪"比例）。
  2. `Scenes/Weapons/Pistol.tscn`：`Muzzle` 下加 `[node name="VFX" parent="Muzzle" instance=ExtResource("3_muzzleflash")]`。
     **`Muzzle` 这个名字没动**（`Weapon.cs:95` 按名取）。
     ⚠️ **不要在这个实例上再写 `transform = ...`** —— 在 .tscn 里给"实例根节点"写 transform 是**覆盖**（不是叠加），
     会把特效自己的朝向/尺寸抹掉。要改就改 `fx_weapon_muzzle.tscn` 的根节点。
  3. `Scripts/Weapons/Weapon.cs`（**这个文件不在冲突列表里**，用 `edit` 工具改的）：
     `_Ready` 里 `_muzzleVfx = Muzzle?.GetNodeOrNull<Node>("VFX");`，
     `TryFire` 成功那一支里加一行 `_muzzleVfx?.Call("play");`。
- **验证（运行时，一次表达式里"开火 + 立刻读状态"）**：

```
[Pistol.TryFire(Vector3(0,1.2,-40)), VFX/Flash_Long.emitting, VFX/Flash_Side.emitting,
 VFX/AnimationPlayer.current_animation, ...]
=> [ true, true, true, "main", ... ]        # 开火成功 → 3 个粒子发射器全部 emitting=true、动画=main
0.10 s 之后再读               => emitting=false, anim_pos=0.175   # 单发 0.2 s 的一次性闪光，符合预期
```

- **证据**：`art_05_muzzle_flash_rendering.png`（把特效设为循环后拍到的火光本体）、
  `art_06_muzzle_flash_ingame.png`（正常游戏里 0.2 s 的一闪：火光把地面照亮了一圈）、
  `art_07_pistol_isolated.png`（把手枪单独移到空中，确认枪模型本身在渲染）。
- ⚠️ **已知残留问题（诚实记录）**：`VFX/Glow`（那一颗 `SphereMesh` 光核）的 `material_override`
  **在运行时变成 `null`** —— 被 `AnimationPlayer` 里那条
  `Glow:material_override:shader_parameter/alpha_multiplier` 轨道弄丢的
  （编辑器里实例化时是好的，跑起来就没了；把 material 设成 `resource_local_to_scene = true` 也**没修好**，
  已改回）。影响很小：那颗球缩放后只有 **0.045 m** 半径，没了它只是少一个高光核，
  三个粒子发射器（`Flash_Long/Side/Front`）全部正常。**要修的话**在 `Weapon.cs` 之外动
  `Assets/Effects/fx_weapon_muzzle.tscn`：删掉那条 Glow 轨道，改成在控制器 `_ready` 里直接写 alpha。
  日志里**没有任何 ERROR/WARNING**（自测日志已确认），所以不影响构建与自测。

### 6.5 子弹拖尾 ⚠️（功能已验证，**没拿到截图**）

- **改**：`Scenes/Combat/Bullet.tscn` 在 `Mesh` 下加 `GPUParticles3D`（**名字 `Trail`**）：
  `emitting=true / amount=60 / lifetime=0.35 / fixed_fps=0 / local_coords=false`（世界空间 → 自然拖尾）+
  `visibility_aabb = AABB(-20,-20,-20, 40,40,40)`（不然 26 m 长的拖尾会被视锥剔除）。
  新增 3 个资源（按 [assets.md](assets.md) §2）：
  `Assets/Particles/ps_combat_trail.tres`（`ParticleProcessMaterial`，方向 +Z 即弹道反方向、金色渐变 alpha）、
  `Assets/Models/mesh_combat_trail.tres`（`QuadMesh` 0.16×0.55）、
  `Assets/Materials/mat_combat_trail.tres`（**unshaded + additive** + `billboard_mode=3`，
  与 `Mesh` 那身"强自发光 ×4.0"协调）。**零代码。**
- **红线**：`Bullet.tscn` 的 `Speed = 75.0` / `MaxLifetime = 2.2` **原样未动**（已验证）。
- **验证（运行时）**：`TryFire` 后读活着的弹丸：

```
[TryFire(...), projectiles.size(), Mesh/Trail.emitting, Mesh/Trail.draw_pass_1.material]
=> [ true, 1, "true", "(res://Assets/Materials/mat_combat_trail.tres):<StandardMaterial3D>" ]
```

  → 拖尾节点在、`emitting = true`、`QuadMesh` 上挂到了正确的 `StandardMaterial3D`。**功能是通的。**
- ❌ **没拿到的证据**：**一张能看清拖尾的截图**。原因不是素材，是**工具节拍**：
  弹速 75 m/s，`gdmcp runtime screenshot` 一次往返 ~0.1–0.2 s，等截图落盘时弹丸已经飞出画面/撞墙消失
  （`local_coords=false` 的粒子是弹丸的子节点，弹丸 `QueueFree` 时拖尾一起消失）。
  试过的办法（都没成）：连拍 3–4 张、打满一梭子 `FireFullMagazine`、把枪抬到空中、
  抬高镜头（`CameraRig.PitchDegrees` 只在初始化时被读，运行时不重算，改了不生效）、
  试图用 `Engine.set_time_scale` 放慢（`Expression` 沙箱里拿不到 `Engine`）。
  **这一项请当作"代码/资源级已验证、视觉未确认"**，别当成"看过了没问题"。
  下次要拍：把 `Bullet.tscn` 的 `Speed` 临时改成 6（**只是拍图，记得改回来**），或者干脆用编辑器跑一个空场景。

---

## 7. 验证汇总（本次实际跑过的）

| 项 | 命令/方式 | 结果 |
|---|---|---|
| 编译 | `& 'F:\Godot项目\项目a\build.ps1' -v minimal` | **0 warning / 0 error** |
| 游戏内自测 | `selftest.flag` + `gdmcp tool-call run_project`（`res://Scenes/Main.tscn`） | ✅ `第 N 波开始` / `Encounter 清场完成` / `端到端：按 F 之后 奖励界面打开 = True` / `=== 自测结束 ===` |
| `selftest.flag` | `Remove-Item` | ✅ **已删除**（复查 `Test-Path` = False） |
| 日志 | 最新 `run_*.log` 匹配 `ERROR\|Exception\|WARNING` | **0 命中** |
| 资源级 | 编辑器脚本 + `ResourceLoader.CACHE_MODE_IGNORE`：AABB 逐件比对、节点/材质/网格逐项读回 | 7/7 竞技场件 0 误差；玩家 1.8 m/脚在 y=0；枪口/拖尾节点与材质全部指对 |
| 视觉 | `gdmcp runtime screenshot`（`res://Scenes/TestLab.tscn` = TestArena + Player + HUD） | `docs/screenshots/art_01..07`（唯一没拍到的是子弹拖尾，见 §6.5） |
| 未动的目录 | `Scripts/UI/`、`Scenes/UI/`、`Scenes/Interactables/`、`Scripts/Interaction/`、`Scripts/Run/` | **一个文件都没碰**（本次只改 `Scenes/Levels/TestArena.tscn`、`Scenes/Player/Player.tscn`、`Scenes/Weapons/Pistol.tscn`、`Scenes/Combat/Bullet.tscn`、`Scripts/Weapons/Weapon.cs` + 新增 `Assets/**` + 本文档） |

改 C# 一律用 `edit` 工具（**没有**用 pwsh 做多行替换）。
手写 `.tscn`/`.tres` 全部 **UTF-8 无 BOM + LF**，`ext_resource` 只写 `path=` 不写 `uid=`。

---

## 8. 下一步（按投入产出比）

1. **9 个敌人 + Boss + 木桩**：与玩家同一套包换色/缩放。⚠️ 记得
   [art-sources.md](art-sources.md) §4.3 那条：`EliteModifier` 的精英染色**只看直接子节点**，
   `.glb` 挂深了精英怪不会变紫 —— 把 `.glb` 实例直接挂成单位的直接子节点、或放在 `Model` 这一层。
2. **给玩家加 walk/sprint 动画**：✅ **已在「第四轮」做完**（见文末 §11）—— `.glb` 里现成的 `walk` / `sprint` /
   `die` 现在由 `Scripts/Combat/UnitAnimator.cs` 按移速切换，10 个敌人 + 首领与玩家共用同一个组件。
   仍**没做**的是 `jump` / `fall` / `attack-*`（§11.3）。
3. **子弹拖尾的截图**（§6.5），顺手决定要不要把拖尾改成"弹丸销毁后仍存活一小会儿"
   （现在拖尾是弹丸的子节点，命中即整个消失）。
4. **命中火花（Binbun Hit FX）/ 爆炸（Explosion FX）** —— 包已经下好了，在 `F:\Godot项目\_art_staging\`。
   接爆炸时**别碰 `Explosion.tscn` 的 `SphereShape3D`**。
5. **UI / 音效 / 字体** —— 等 `Scenes/UI/` 那个代理收工；素材（Kenney UI Pack Space Expansion / Fonts /
   3 个音效包）也都已经在 `_art_staging\` 里了。
6. **Modular SciFi MegaKit**（46 MB，CC0）如果要用来做"更写实的科幻关"，
   注意它的 glTF 是**外部 PBR 三件套**（BaseColor / Normal / ORM，单个模块 ~10 MB），
   且 `images[].uri` 只写裸文件名（`T_Trim_01_Normal.png`），**必须把 `Textures/*.png` 复制到 `.gltf` 旁边**才认得。

---

# 第二轮：9 敌 + Boss 换模型 + Binbun 命中/爆炸特效（2026-10）

> **本轮范围**：`Scenes/Enemies/**`、`Scenes/Combat/Explosion.tscn`、`Assets/**`、`docs/art-*.md`。
> **没有改任何 `Scripts/**`、`Config/**`**（边界要求；确实需要的一处改动见 §9.5「必须改代码的地方」）。
> 素材全部来自上一轮已下载的 CC0 包（**没有新下载任何东西**）。

## 9.0 30 秒结论

| 问题 | 答案 |
|---|---|
| 换了几个模型？ | **10 个**（9 敌 + Boss），全部换完并逐个验证。 |
| 用了哪个包？ | **Kenney Mini Characters**（CC0，与玩家同一个包）。`character-male-a` 已被玩家占用，敌人用其余 11 个角色里的 10 个。 |
| 实测轴心 | **脚底**：10 个 `.glb` 的 `min.y = 0.0000`（与玩家一致）→ **`Model.transform.y = 0`**。 |
| 实测朝向 | **正面朝 +Z**（与玩家一致）→ **绕 Y 转 180°**：`Transform3D(-s,0,0, 0,s,0, 0,0,-s, 0,0,0)`。**双向截图验证**（`art_10/11` 看到正脸 → `art_20/21/22` 看到后脑勺）。 |
| 缩放怎么定的？ | `s = 碰撞胶囊高度 / 模型原生高度`，**让视觉与碰撞同高同底**。逐单位数值见 §9.2。 |
| 自带动画？ | **每个 32 段**，零 C#：给实例化子节点加 `[node name="AnimationPlayer" parent="Model" index="1"] autoplay = "idle"`。 |
| 特效接了几个？ | **爆炸 ✅ 接进 `Explosion.tscn`**；**命中 ✅ 资源已就位并验证渲染**（真正挂到"打中的那一刻"需要 1 行 C#，见 §9.5）。 |
| 玩法有没有被碰？ | **没有**。`Explosion.tscn` 的 `SphereShape3D radius=1`、`Explosion.Lifetime=0.12`、所有 `Body` 胶囊、`Bullet` 速度全部一个字节没动。 |

## 9.1 素材落盘（新增）

```
Assets/Models/enemies/            11 文件  2.7 MB  (10 个 mesh_enemy_*.glb + colormap.png)
Assets/Effects/fx_combat_explosion.tscn     24 KB  (Binbun ExplosionFX「地面爆炸」预设，已改路径)
Assets/Effects/fx_combat_hit.tscn           12 KB  (Binbun HitFX vfx_hit_02，已改路径)
Assets/Effects/fx_detach_lifetime.gd         1 KB  (自写：一次性特效的独立生命周期壳)
Assets/Effects/binbun/**                    18 文件 (2 个包用到的 shader / gd / license，路径全部改写成 res://Assets/...)
Assets/Effects/binbun/*/license.txt                 (CC0 原文，照抄进仓库)
```

- 命名照 [assets.md](assets.md) §2：`mesh_enemy_<变体>.glb`（类别 `mesh_` / 分组 `enemy`）。
- **一个目录装 10 个 `.glb` 是因为 Kenney 的 glb 不内嵌贴图**：它的 `images[].uri` 是相对路径 `Textures/colormap.png`，
  而 Mini Characters 的 12 个角色**共用同一张 8706 B 的 colormap**（已核对 hash 与玩家那份一致），所以共用一份即可。
- 厂商文件放 `Assets/Effects/binbun/` 是延续上一轮 `Assets/Effects/muzzle/` 的做法（见 §5.2 的偏离说明）；
  `ext_resource` 上的 `uid=` 已按 §5 规矩 4 去掉，`res://assets/BinbunVFX_Vol2/` 全部改写成 `res://Assets/Effects/binbun/`。

## 9.2 实测值总表（轴心 / 朝向 / 缩放）

测量方法：编辑器脚本 `ResourceLoader.load(..., CACHE_MODE_IGNORE)` → `instantiate()` → 遍历子树里的 `MeshInstance3D`，
用**手动父链相乘**（`c.transform * c.get_aabb()` 只对直接子节点成立，深一层的网格会算错）求出相对单位根节点的 AABB。

| 场景 | 模型文件（源角色） | 模型原生高 | 胶囊高 | **缩放 s** | **Model.y** | Basis |
|---|---|---|---|---|---|---|
| Chaser | `mesh_enemy_chaser.glb`（male-b） | 0.6613 | 1.8 | **2.72191** | **0** | `(-s,0,0, 0,s,0, 0,0,-s)` |
| Charger | `mesh_enemy_charger.glb`（male-c） | 0.7928 | 2.0 | **2.52270** | **0** | 同上 |
| Exploder | `mesh_enemy_exploder.glb`（male-d） | 0.7218 | 1.5 | **2.07814** | **0** | 同上 |
| FastMelee | `mesh_enemy_fast_melee.glb`（female-e） | 0.7165 | 1.6 | **2.23308** | **0** | 同上 |
| Guard | `mesh_enemy_guard.glb`（male-e） | 0.6760 | 2.1 | **3.10651** | **0** | 同上 |
| JumpingMelee | `mesh_enemy_jumping_melee.glb`（female-f） | 0.6713 | 1.8 | **2.68137** | **0** | 同上 |
| RangedEnemy | `mesh_enemy_ranged.glb`（male-f） | 0.6713 | 1.8 | **2.68137** | **0** | 同上 |
| Screamer | `mesh_enemy_screamer.glb`（female-a） | 0.7755 | 2.0 | **2.57898** | **0** | 同上 |
| SkillElite | `mesh_enemy_skill_elite.glb`（female-b） | 0.7234 | 2.1 | **2.90296** | **0** | 同上 |
| Boss | `mesh_enemy_boss.glb`（female-c） | 0.7755 | 2.8 | **3.61057** | **0** | 同上 |

**逐单位回读证据**（脚本输出，10/10 全部命中）：

```
Chaser       Model=Node3D meshes=2 vis_min_y=0.0 vis_max_y=1.8001 vis_H=1.8001 capsule_h=1.8  y=0.9  autoplay=idle anims=32 has_idle=true
Charger      ... vis_min_y=0.0 vis_max_y=2.0    vis_H=2.0    capsule_h=2.0  ...
Exploder     ... vis_min_y=0.0 vis_max_y=1.4999 vis_H=1.4999 capsule_h=1.5  ...
FastMelee    ... vis_min_y=0.0 vis_max_y=1.5999 vis_H=1.5999 capsule_h=1.6  ...
Guard        ... vis_min_y=0.0 vis_max_y=2.0999 vis_H=2.0999 capsule_h=2.1  ...
JumpingMelee ... vis_min_y=0.0 vis_max_y=1.8001 vis_H=1.8001 capsule_h=1.8  ...
RangedEnemy  ... vis_min_y=0.0 vis_max_y=1.8001 vis_H=1.8001 capsule_h=1.8  ...
Screamer     ... vis_min_y=0.0 vis_max_y=2.0001 vis_H=2.0001 capsule_h=2.0  ...
SkillElite   ... vis_min_y=0.0 vis_max_y=2.0999 vis_H=2.0999 capsule_h=2.1  ...
Boss         ... vis_min_y=0.0 vis_max_y=2.8001 vis_H=2.8001 capsule_h=2.8  ...
```

→ **`vis_min_y` 全 0**（脚底正好在单位原点=地面）、**`vis_max_y` 与胶囊高度在 0.0001 内相等**（视觉与碰撞同高同底）。
**所有 `Body` / `CapsuleShape3D` / 命中盒一个字节没改。**

### 9.2.1 方向：双向截图验证（这是"逐包实测"，不是照抄规范）

| 步骤 | 截图 | 看到了什么 |
|---|---|---|
| ① 不转（identity basis） | [art_10_models_raw_front.png](screenshots/art_10_models_raw_front.png)、[art_11_models_raw_front_right.png](screenshots/art_11_models_raw_front_right.png) | 相机在 +Z 朝 -Z 看，**10 个角色全部把正脸对着镜头** → 模型正面朝 **+Z**，与 Godot 前方（-Z）相反 |
| ② 绕 Y 转 180°（x/z 轴取负） | [art_20_enemies_all.png](screenshots/art_20_enemies_all.png)、[art_21_enemies_left.png](screenshots/art_21_enemies_left.png)、[art_22_enemies_right.png](screenshots/art_22_enemies_right.png) | **全是后脑勺**、看不到脸 → 与单位前方一致 ✅ |

> **判据仍是 `det > 0`**：x、z 轴同时取负 = 绕 Y 转 π（不是镜像）。写成 scale 负值会让法线/背面剔除出错。

### 9.2.2 玩家 vs 敌人 的模型分配（同一包里挑不重样的角色）

玩家 = `character-male-a`（上一轮）。敌人按"体型/气质"分配：
Chaser=male-b（橙红大胡子）、Charger=male-c（蓝制服壮汉）、Exploder=male-d（黑西装）、
FastMelee=female-e（白衣轻装）、Guard=male-e（白+橙）、JumpingMelee=female-f（深发蓝衣）、
RangedEnemy=male-f（棕发）、Screamer=female-a（黑发紫衣）、SkillElite=female-b（金发黄衣）、Boss=female-c（蓝衣灰发，放大 3.61 倍）。

## 9.3 每个场景具体改了什么

以 `Chaser.tscn` 为例（其余 9 个完全同构）：

1. `[gd_scene load_steps=11]` → **按 `ext_resource + sub_resource + 1` 重算**（顺手修掉了原来不准的 load_steps）。
2. 新增 `[ext_resource type="PackedScene" path="res://Assets/Models/enemies/mesh_enemy_chaser.glb" id="9_model"]`
   —— 位置在**所有 `[sub_resource]` 之前**（.tscn 格式硬要求）。
3. **删掉已经没人引用的** `StandardMaterial3D_body` 与 `CapsuleMesh_body` 两个 `sub_resource`（网格换掉了，纯色材质不再需要）。
4. `Model` 节点：`MeshInstance3D + CapsuleMesh` → **`.glb` 的实例根节点**
   ```
   [node name="Model" parent="." instance=ExtResource("9_model")]
   transform = Transform3D(-2.72191, 0, 0, 0, 2.72191, 0, 0, 0, -2.72191, 0, 0, 0)
   ```
   ⚠️ **给实例根节点写 transform 是"覆盖"**：缩放、180°、位移必须**一次性写全**（上一轮踩过）。
5. 紧跟一条属性覆盖（**零 C# 让模型动起来**）：
   ```
   [node name="AnimationPlayer" parent="Model" index="1"]
   autoplay = "idle"
   ```
   ⚠️ `index` 是**兄弟序号**（glb 根下 `character-xxx` = 0、`AnimationPlayer` = 1），写错**静默不生效**。

## 9.4 特效接入

### 9.4.1 爆炸（Binbun Explosion FX → `Scenes/Combat/Explosion.tscn`）

**红线条目全部遵守**：

| 项 | 处理 |
|---|---|
| `SphereShape3D radius = 1` | **原样未动**（`[sub_resource]` 一行没改） |
| `Detonate()` 靠 `Scale = radius` | **未动**（`Explosion.cs` 没碰） |
| `Explosion.Lifetime = 0.12s` | **未动**。它是**玩法窗口**（`AttackHitbox._PhysicsProcess` 在这 0.12s 内持续判定命中），不是美术参数 |

新增结构：

```
Explosion (Area3D, Explosion.cs)
├── Shape  (CollisionShape3D + SphereShape3D radius=1)   ← 红线，未动
├── Mesh   (MeshInstance3D, 原来的半透明球)               ← 保留：它就是"半径=判定半径"的那次闪光
└── FX     (Node3D, fx_detach_lifetime.gd, fx_scale=2.5)  ← 新增
    └── Vfx (Binbun fx_combat_explosion.tscn, emission=8, light_energy=8)
```

**为什么需要 `fx_detach_lifetime.gd`（自写，21 行）**：爆炸节点只活 0.12 s，而 Binbun 的 `main` 动画有 2.6 s
——做成普通子节点会在第 0.12 s 被连根 `QueueFree`，只剩一帧闪光。
这个壳在**出生当帧**（`call_deferred`，此时宿主已经摆好位置/缩放）把自己 `reparent()` 到场景根并**显式搬运世界变换**，
于是宿主被删掉时特效不受影响，播完再自毁。**零 C# 改动、零玩法参数改动。**

> ⚠️ **踩到的坑（值得记）**：`Node.reparent(new_parent, true)`（keep_global_transform）在本项目
> （`physics_interpolation = true`）下**实测会把缩放丢掉** —— 三档缩放 1× / 3× / 6× 渲染出来一模一样。
> 修法：自己存 `global_transform` → `reparent(root, false)` → `global_transform = gt` → 再乘美术倍率。

**实测结论（诚实记录，不算"完美"）**：

- Binbun 的爆炸粒子**尺寸不跟随节点缩放**（`GPUParticles3D` 的 shader 直接写 `POSITION`，绕过 `MODEL_MATRIX`），
  这与素材页评论区的抱怨（"why cant this be scaled at all"）一致。
- 本项目的 `${TestArena}` `Environment` **没有开 glow**（只有 `background_mode=2` / `ambient_light_energy=0.55` / `tonemap_mode=2`），
  Binbun 这套特效是按 HDR/glow 环境调的 → 在这里表现为**短暂的橙色火球 + 灰烟 + 一记橙色灯光闪**，比作者 demo 里暗。
- **所以本轮的做法是"叠加"而不是"替换"**：原来的半透明球（0.12 s、半径正好等于判定半径）留着当核心闪光，
  Binbun 爆炸提供粒子、烟、碎片与光照。想要更炸裂，需要在 `TestArena.tscn` 的 Environment 里打开 glow
  —— 那个文件不在本轮允许改的范围内。

### 9.4.2 命中（Binbun Hit FX → `Assets/Effects/fx_combat_hit.tscn`）

- `Assets/Effects/fx_combat_hit.tscn` = Binbun `vfx_hit_02`（**白色的四角星火花 + 橙色辐射条纹 + 地面光斑**），
  根节点设成 `one_shot = true, autoplay = true` —— **实例化即播**，调用方只要 `Instantiate + AddChild + 设 GlobalPosition`。
- **零代码的挂接点不存在**：命中发生在 `Scripts/Combat/Projectile.cs`（子弹）/ `UnitPresentation`（受击闪色）里，
  而这两个文件不在本轮允许改的范围内 → **只做到"资源就位 + 渲染已验证"**，接线见 §9.5。
- 证据：[art_33_hit_t0.png](screenshots/art_33_hit_t0.png)（火花本体）、[art_34_hit_t1.png](screenshots/art_34_hit_t1.png)（余晖）。

> **避开了 `fx_weapon_muzzle.tscn` 那个已知坑**：那个坑来自 `AnimationPlayer` 里一条
> `Glow:material_override:shader_parameter/...` 轨道（运行时把 `material_override` 弄成 null）。
> 本轮的命中特效**只有 `Glow:scale` 轨道**，且**所有材质都是场景内的 `sub_resource`（不是共享 `.tres`）**
> → 既不会互相影响，也不会有那条轨道。爆炸同理。

## 9.5 ⚠️ 必须改代码的地方（本轮没有动 `Scripts/`，交接给下一轮）

### (1) `EliteModifier` 的精英染色**对蒙皮模型失效**（重要）

`Scripts/Combat/EliteModifier.cs:55-61` 是**只看直接子节点**的：

```csharp
foreach (Node child in unit.GetChildren())
    if (child is MeshInstance3D mesh) mesh.MaterialOverride = material;
```

换模型前，`Model` 本身就是 `MeshInstance3D`（胶囊）→ 染色生效。
换模型后，`Model` 是 **`.glb` 的实例根节点（`Node3D`）**，真正的网格在 `Model/character-xxx/Skeleton3D/...` 下
→ **直接子节点里一个 `MeshInstance3D` 都没有**，染色**静默失效**（不报错）。

**实测证据**（脚本回读 10/10）：`direct_mesh_child=false`。
副作用范围：
- `SkillElite.tscn` 里那个手挂的 `Elite` 节点 → 不再变紫（**但 `unit.Scale = 1.2` 仍然生效，精英还是大一圈**）。
- `EncounterController.cs:448` 运行时 `enemy.AddChild(new EliteModifier())` 随机点名的精英 → 同样只有放大、没有变紫。

**建议改法（1 行，改 `Scripts/Combat/` 不在本轮边界内）**：把上面那段换成递归（照抄
`UnitPresentation` 的收集方式），或额外接受 `Node3D` 并向下找网格。`UnitPresentation` 的**受击闪色是递归的，不受影响**。

### (2) 命中特效接线 —— ✅ **已在「第三轮」做完，见文末 §10**（这里保留原始交接文字）

`Scripts/Combat/Projectile.cs` 命中那一支（或 `UnitPresentation.Flash()`）加 3 行即可：

```csharp
if (GD.Load<PackedScene>("res://Assets/Effects/fx_combat_hit.tscn") is { } hitFx
    && hitFx.Instantiate<Node3D>() is { } fx)
{
    GetTree().CurrentScene.AddChild(fx);
    fx.GlobalPosition = hitPosition;     // 命中点
}
```
（`fx_combat_hit.tscn` 已经 `autoplay = true`，**不需要再调 `play()`**。）

### (3) 精英/高个模型的其它已知假设（本轮核对结果：**不需要改**）

- `CameraRig.TargetOffset.y = 1.5` 是**玩家**的胸口高；玩家模型没换 → 不用动。
- `RangedEnemy` / `TrainingShooter` 的 `Muzzle` 是空 `Marker3D`，模型换了但枪口点没动 → 子弹仍从模型前方射出，**无需改**。
- 伤害跳字高度 `WorldOverlay.cs:73` 的固定 `+1.5` 与小怪身高不匹配是**既有现象**（Boss 3.6 米更高）→ 未动。

## 9.6 验证证据

| 项 | 命令/方式 | 结果 |
|---|---|---|
| 编译 | `build.ps1 --no-incremental -v minimal` | **0 warning / 0 error** |
| 游戏内自测 | `selftest.flag` + 手动跑 `res://Scenes/Main.tscn` | ✅ `Encounter 清场完成：共生成 4，用时 3s` / `端到端：按 F 之后 奖励界面打开 = True` / `=== 自测结束 ===`；stderr 里**只有 1 条** NavigationMesh cell height 警告（既有问题），**没有 ERROR/Exception** |
| `selftest.flag` | `Remove-Item` + `Test-Path` | ✅ **已删除**（复查 False） |
| 资源级 | 编辑器脚本逐个 `instantiate` 量 AABB / 读 `AnimationPlayer` | **10/10**：脚在 y=0、视觉高 == 胶囊高、`autoplay=idle`、`anims=32`、`has_idle=true` |
| 视觉 | 自建展示场景 `Scenes/Enemies/_ArtShowcase.tscn` + `_art_shot.gd`（自己存 viewport PNG，不依赖 gdmcp 运行时桥） | `art_20`~`art_23`（10 个单位）、`art_30`~`art_34`（爆炸/命中特效） |

### 9.6.1 新增的两个"验证工具"（留在仓库里，以后换美术直接用）

| 文件 | 用途 |
|---|---|
| `Scenes/Enemies/_ArtShowcase.tscn` | 把 10 个敌人预制体**按真实场景**排成两排放在一块带碰撞的地面上（含太阳阴影、Label3D 名牌）。**这就是"换完模型长什么样"的对照台。** |
| `Scenes/Enemies/_art_shot.gd` | 挂在展示场景根上：等若干帧 → 移动相机 → `get_viewport().get_texture().get_image().save_png()` → 退出。**一次运行拍 9 张图**，完全不依赖 gdmcp 运行时桥（本机 gdmcp 的 debugger session 起不来时也能拍）。 |

跑法（**不用编辑器、不用 MCP**）：

```powershell
& 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe' `
  --path 'F:\Godot项目\项目a' 'res://Scenes/Enemies/_ArtShowcase.tscn'
```

## 9.7 本轮明确"试过但没采用"的

| 试过的 | 结果 | 为什么放弃 |
|---|---|---|
| **Quaternius Modular SciFi MegaKit 的 3 只外星怪**（`Alien_Scolitex` / `Alien_Cyclop` / `Alien_Oculichrysalis`，CC0，自带 1 段 idle）当 Boss | 导入成功、能量 AABB，但**轴心不在脚底**（`Alien_Scolitex` 的 `min.y = -1.0817`，AABB 高 3.36 / 宽 3.43 / 深 3.23），且**风格与 chibi 角色冲突** | 换成"和玩家同一个包、轴心在脚底、32 段动画"的 `character-female-c` 放大 3.61 倍。外星怪的两个结论留在 [art-sources.md](art-sources.md) 的"逐包实测"补记里，见 §9.8 |
| Binbun **地面爆炸**换成**空中爆炸/蘑菇云**预设 | 没试（时间） | 三个预设共用同一套 shader 家族，烟的问题一样；下次要更好效果优先开 glow 而不是换预设 |
| 给爆炸加 `ParticleProcessMaterial.scale_min/max` 倍率 | 没试（时间） | 粒子尺寸由 shader 的 `POSITION` 决定，改 material scale 未必有效；**更靠谱的下一步是在 `TestArena.tscn` 的 Environment 开 glow** |

## 9.8 给 [art-sources.md](art-sources.md) 的"逐包实测"补记（本轮新增的事实）

> art-sources.md §4.1 / §7 当时写的是"**任何包的轴心都没验证过**"。本轮把能测的都测了，补两条：

1. **Kenney Mini Characters（12 个角色）**：`min.y = 0.0000`（**轴心在脚底**），`max.y` 在 0.6613 ~ 0.7928 之间
   → **`Model.y = 0`**，缩放按需。**正面朝 +Z**（截图双向验证）。
2. **Quaternius Modular SciFi MegaKit 的 Alien_*（glTF，外部 `.bin`）**：**轴心不在脚底**，
   `Alien_Scolitex` 的本地 AABB 是 `min(-1.7162, -1.0817, -0.7662) → max(1.7162, 2.2806, 2.4603)`（高 3.3622 米），
   要在脚底对齐得写 `Model.y = +1.0817 × s`。**逐包实测这句话对 Quaternius 是成立的、对 Kenney 不成立。**
   另外它的 `.gltf` 里 `buffers[0].uri` 写的是**原始文件名**（`Alien_Scolitex.bin`）——
   **改文件名导入必失败**（`.import` 会停在 `valid=false`，`ResourceLoader.load` 返回 null），
   要么别改名，要么同步改 JSON 里的 `uri`。

---

# 第三轮：命中 / 击中特效**真的接线了**（2026-10）

> **本轮范围**：`Scripts/Combat/{HitEffect.cs(新),Projectile.cs,AttackHitbox.cs}`、`Scripts/Config/BalanceConfig.cs`、
> `Config/Balance.tres`、`docs/config.md`、`docs/art-integration-log.md`。
> **没有动** `Explosion.tscn` 的 `SphereShape3D radius=1` 与 `Explosion.Lifetime=0.12`（红线），
> **没有动** `Assets/Effects/fx_combat_hit.tscn` 一个字节（美术资源保持原样）。

## 10.0 30 秒结论

| 问题 | 答案 |
|---|---|
| 上一轮留下的"§9.5 (2) 命中特效接线"做完了吗？ | ✅ **做完了**，而且**两条命中路径都接了**。 |
| 为什么是"两条"？ | grep `ReceiveHit` 全项目只有**两个**会造成伤害的命中入口：<br>① **投射物命中** `Projectile.ResolveImpact`（子弹/箭矢/气刃，手上有**精确射线命中点**）<br>② **命中体击中** `AttackHitbox._PhysicsProcess`（近战挥砍 / AOE / 冲锋 / **爆炸**——`Explosion` 继承它）<br>（`RewardCatalog` 的连锁闪电也走 `ReceiveHit`，但它是奖励效果不是"命中"，没接。） |
| 接线代码在哪？ | 两个调用点各 **1 行** + 一个新文件 `Scripts/Combat/HitEffect.cs`（出生逻辑只有一处）。 |
| 参数写死了吗？ | **没有**。开关 / 预制体路径 / 寿命 / 高度偏移四个都进了 `Config/Balance.tres` 的「表现 · 命中特效」组（见 [config.md](config.md)）。 |
| 截图 | [fx_hit_01_bullet_projectile.png](screenshots/fx_hit_01_bullet_projectile.png)（子弹命中敌人胸口）、[fx_hit_02_enemy_melee_hitbox.png](screenshots/fx_hit_02_enemy_melee_hitbox.png)（敌人近战砍中玩家） |

## 10.1 ⚠️ 上一轮那句"加 3 行就行"**不够** —— 会漏节点（本轮实测）

上一轮交接写的接法是 `Instantiate + AddChild + GlobalPosition` 三行。照做能出效果，但会**泄漏**：

1. **`fx_combat_hit.tscn` 自己不会销毁。** 根节点的 `one_shot = true` 只表示"动画不循环"——
   `VFXControllerBB.play()` 播完只 `finished.emit()`，**没有任何 `queue_free()`**。
   打一梭子（步枪 8 发/秒）就会在场景树里留下十几个常驻节点，一直留到本局结束。
2. **`fx_detach_lifetime.gd` 那个现成的壳在这里用不了**：它的 `_detach()` 开头有一句
   `if holder == null or root == null or holder == root: return` —— 它设计上是"**从一个会死的宿主里脱身**"，
   而命中特效**正是**直接挂在场景根（`CurrentScene`）下的，会**提前 return、根本不走自毁那条路**。

**本轮做法**：寿命由配置值 `HitEffectLifetime`（默认 1.8s）接管，用 `SceneTreeTimer` 定时 `QueueFree`
（`HitEffect.Spawn` 里）。于是同时拿到三件事：不泄漏、寿命可调、**不需要新场景文件、不需要 reparent**。

## 10.2 顺带避开的两个已知坑

| 坑（上一轮实测的） | 本轮怎么绕开的 |
|---|---|
| `reparent(..., true)` 在 `physics_interpolation = true` 下**会丢缩放** | **全程没有 reparent**：特效出生时就 `AddChild` 到最终父节点，再写一次 `GlobalPosition` |
| Binbun 的粒子**尺寸不跟随节点缩放**（shader 里直接写 `POSITION`） | 所以**没有**开"特效缩放"配置列 —— 那会是一列改了没用的死配置；要改尺寸就改 `fx_combat_hit.tscn` 的材质/网格 |
| 命中体那一路**没有表面命中点**（判定的是"重叠"），而单位原点在**脚底** | **当时**用 `GlobalPosition + Up × HitEffectHeightOffset`（配置项，默认 1.0 = 1.8m 单位的胸口）；投射物那一路直接用射线命中点。⚠️ **后续一轮按用户要求改成"落在实际命中点"**：那一列配置**已删除**，命中体那一路改用 `AttackHitbox.ResolveHitPoint`（**目标碰撞体表面上离命中体最近的那一点**，几何算法，不读物理空间）—— 为什么不用射线见 [config.md](config.md)「用户三项修正实测」§三 |
| `fx_weapon_muzzle.tscn` 那条 `alpha_multiplier` 轨道（运行期把 `material_override` 弄成 null，会刷警告） | **与命中特效无关**：`fx_combat_hit.tscn` 里 `alpha_multiplier` 出现 **0 次**（`Select-String` 实测），本轮也没有往枪口那套里加东西 |

## 10.3 实测证据（可复现，不用编辑器、不用 MCP）

**功能**：一个一次性探针场景（跑完即删）真的走那两条路径，并数场景根下的特效节点：

```
[FxProbe] 开局：FX 数 = 0（期望 0）
[FxProbe] ① Pistol.TryFire -> true（期望 true）
[FxProbe] ① 子弹命中后 FX 数 = 1（期望 >= 1）        ← Projectile.ResolveImpact 那条路
[FxProbe] ③ 2.4s 后 FX 数 = 0（期望 0：配置寿命到了就自毁，不泄漏）
[FxProbe] ② 敌人砍中玩家后 FX 数 = 1（期望 >= 1）    ← AttackHitbox._PhysicsProcess 那条路（敌人 AI 真的砍玩家）
[FxProbe] 结论：①=1 ②=1 ③=1
```

跑法（与 `Scenes/Enemies/_art_shot.gd` 同一套路：自己造场景 + 自己存 PNG，不依赖 gdmcp 运行时桥）：

```powershell
& 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe' `
  --path 'F:\Godot项目\项目a' --windowed --resolution 1600x900 'res://_fx_probe.tscn'
```

**视觉**：`fx_hit_01_bullet_projectile.png` 是子弹打在 Chaser **胸口**上的一团白色四角星火花（地面有光斑）；
`fx_hit_02_enemy_melee_hitbox.png` 是敌人近战砍中玩家时、在**两人之间的胸口高度**爆出的火花
（证明"高度偏移"真的生效 —— 直接用单位原点会打在地上）。

> ⚠️ 记一条工具现状（本轮踩的）：**本机 gdmcp 的 runtime 桥起不来**
> （`get_runtime_screenshot` 返回 `{"status":"no_active_sessions"}`，`get_debugger_sessions` 里
> 唯一那个 session 是 `debuggable: false`），所以运行时读数/截图都得靠"游戏自己存 PNG"这条路。

## 10.4 全流程回归

- 编译：`& build.ps1 --no-incremental -v minimal` → **0 警告 / 0 错误**。
- 自测：`Main.tscn -- --selftest`（**windowed**，见下面的坑）→ `=== 自测开始 ===` … `Encounter 清场完成` /
  `白板首领降临（血量倍率 0.35（来源：表 boss_final））` / `端到端：按 F 之后 奖励界面打开 = True` /
  `=== 自测结束 ===`，stderr 里 **0 个 SCRIPT ERROR、0 个 Exception**（只有既有那条 NavigationMesh cell height 警告）。
- ⚠️ **顺带发现的别人家的坑（不是本轮的改动，记在这里免得下次再查一遍）**：
  `--headless` 跑自测时，`SelfTest.Shot()` 每帧调 `Texture2D.GetImage()`，而 headless 用的是 dummy 渲染器
  → `ERROR: Parameter "t" is null` + `NullReferenceException` **每帧刷一次**（实测一次运行刷了 3.8 万条、
  stderr 长到 **142 MB**）。**跑自测请用 windowed**，或者让那个截图步骤在 `DisplayServer.get_name() == "headless"` 时跳过。

---

# 第四轮：单位动画（`UnitAnimator`）真的接线了（2026-10）

> **本轮范围**：`Scripts/Combat/UnitAnimator.cs`（新建）、`Scripts/Combat/UnitPresentation.cs`、
> `Scripts/Config/UnitConfig.cs`（只加列）、`Scenes/Player/Player.tscn`、`Scenes/Enemies/*.tscn`、本文档 + `docs/config.md`。
> **没有动** `Assets/**`（`.glb` 一个字节没改）、`Scenes/UI/**`、`Scenes/Levels/TestArena.tscn`。
> 上一轮（§8 下一步第 2 条）留的"给玩家加 walk/sprint 动画"这一项**本轮做完了**，而且敌人与玩家共用同一个组件。

## 11.0 30 秒结论

| 问题 | 答案 |
|---|---|
| 谁在动？ | **玩家 + 10 个敌人 + 首领**（`Model` 子树里有 `AnimationPlayer` 的单位全都会动；木桩/无人机这类没有动画的单位静默跳过）。 |
| 怎么切的？ | 一个组件 `Scripts/Combat/UnitAnimator.cs` 按**移动速度**切 `idle` / `walk` / `sprint`，死亡切 `die`。 |
| 位置占在哪？ | 每个单位场景里一个 `Animator` 子节点（`[node name="Animator" type="Node" parent="."]`），**但漏挂也不会坏** —— `UnitPresentation._Ready` 会 `Ensure()` 补一个。 |
| 参数可配吗？ | 可配。`UnitConfig` 新增 6 列（4 个动画名 + 2 个速度阈值，**空 / 0 = 不覆盖**），落在单位表的「表现 · 动画」组里；一行都不填 = 组件默认值（`idle`/`walk`/`sprint`/`die`，阈值 0.15 / 4）。 |
| 循环从哪来？ | **代码里设的**。`.glb` 导入的 32 段全是 `LOOP_NONE`（§6.3 记过：`autoplay="idle"` 只是"播一遍停在最后一帧"）→ `Bind()` 把 idle/walk/跑 设成 `Linear`、`die` 保持 `None`。 |
| 有没有破坏既有表现？ | **没有**。受击闪色（`UnitPresentation`，递归收集 mesh + `MaterialOverride`）与精英染色（`EliteModifier.TintRecursive`）走的是材质，动画走的是 `AnimationPlayer` 的骨骼轨道，互不干扰；两处代码本轮都只动了"多一行 Ensure / 注释"。 |
| 证据 | 日志（绑定行 + 每次切换行，含速度）+ 两张截图（同一批单位间隔 0.4 秒、腿部姿势不同）+ 一次"表列真的是旋钮"的临时实验。 |

## 11.1 改了哪些文件

| 文件 | 改了什么 |
|---|---|
| `Scripts/Combat/UnitAnimator.cs` | **新增**（约 300 行）：按速度切动画、死亡动画、约定位查找 `Model/**/AnimationPlayer`、循环模式修正、名字解析与回退、`Ensure(unit)` 自动补挂、每种场景各一行日志 + 每次切换一行日志。 |
| `Scripts/Combat/UnitPresentation.cs` | `_Ready` 里多一行 `UnitAnimator.Ensure(_unit);`（新敌人不挂节点也能动）+ 类注释补一句。 |
| `Scripts/Config/UnitConfig.cs` | **只加列**：`[ExportGroup("表现 · 动画…")]` 下 6 个新字段 + `ApplyAnimationTo(Unit)`（`ApplyTo` 末尾调用）。既有字段的名字/类型/默认值一个没动。 |
| `Scenes/Player/Player.tscn` + `Scenes/Enemies/{Chaser,Charger,Exploder,FastMelee,Guard,JumpingMelee,RangedEnemy,Screamer,SkillElite,Boss}.tscn` | 各加一个 `Animator` 子节点（`ext_resource` 指向 `UnitAnimator.cs`，`load_steps` 相应 +1，11/11 逐个复核过）。 |
| `Scenes/Enemies/_AnimShowcase.tscn` | **新增的展示台**（对照 `_ArtShowcase.tscn` 的定位）：一块地面 + 固定侧视相机 + 4 个单位 + 一个玩家阵营的木桩目标 → 单位自己走过去，用来拍"走/跑"的截图。 |

> ⚠️ 11 个场景的 `AnimationPlayer` 那条 `autoplay = "idle"` **原样保留** —— 它现在是"动画组件没接上时的兜底姿势"，
> 不去动它更安全（本轮没有删任何一行既有属性）。

## 11.2 实测证据

**编译**：`& build.ps1 --no-incremental -v minimal` → **0 warning / 0 error**（改一步跑一次，全程 0/0）。

**绑定 + 按速度切换**（`run_20261012_010245.log`，展示场景；同一批单位、同一帧的数据）：

```
[Combat] 单位动画：FastMelee 共 32 段 · idle=idle walk=walk run=sprint die=die · 阈值 0.15/4（来源：组件默认值）
[Combat] 单位动画：Chaser / Guard / Screamer …（同上，各自一行）
[Combat] 单位动画切换：FastMelee idle → walk（当前速度 1，阈值 0.15/4）
[Combat] 单位动画切换：Chaser   idle → walk（当前速度 1，阈值 0.15/4）
[Combat] 单位动画切换：Guard    idle → walk（当前速度 1，阈值 0.15/4）
[Combat] 单位动画切换：Screamer idle → walk（当前速度 1，阈值 0.15/4）
[Combat] 单位动画切换：FastMelee walk → sprint（当前速度 4，阈值 0.15/4）
[Combat] 单位动画切换：Chaser   walk → sprint（当前速度 4，阈值 0.15/4）
[Combat] 单位动画切换：FastMelee sprint → walk（当前速度 3.5，阈值 0.15/4）   ← 到达后减速，退回走
[Combat] 单位动画切换：Chaser   sprint → walk（当前速度 3.2，阈值 0.15/4）
[Combat] 单位动画切换：FastMelee walk → idle（当前速度 0，阈值 0.15/4）
```

**死亡动画**（自测整轮里抓到的三种）：`Chaser idle → die`、`Boss idle → die`、`Exploder idle → die`，
以及首领召唤出来的小怪 `@CharacterBody3D@5 idle → die`（**自动补挂那条路真的走到了** —— 那个单位是运行期
`Instantiate` 出来的，没有任何人给它挂节点）。

**表里的列真的是旋钮**（一次临时实验，做完已还原）：给 `enemy_chaser.tres` 临时填 `AnimRunMinSpeed = 1.0` →
日志变成 `单位动画：Chaser … 阈值 0.15/1（来源：表 enemy_chaser）`，并且 `Chaser idle → sprint（当前速度 1）`
（默认阈值 4 时这一步只会是 `→ walk`）；同一帧其它单位仍是 `阈值 0.15/4（来源：组件默认值）`。
删掉那一列后立刻回到 `0.15/4（来源：组件默认值）`。

**截图**（对照台 `Scenes/Enemies/_AnimShowcase.tscn`，4 个单位 + 固定侧视相机）：

| 图 | 看到什么 |
|---|---|
| [anim_01_walk_showcase.png](screenshots/anim_01_walk_showcase.png) | `Screamer`（紫衣）**迈步中**：一条腿在前、一条在后，影子也跟着分开 —— 与"`autoplay="idle"` 停在最后一帧"的旧状态明显不同。 |
| [anim_02_walk_showcase_next.png](screenshots/anim_02_walk_showcase_next.png) | **0.4 秒后**的同一批单位：腿部姿势换了一档 ⇒ **动画真的在播**（不是定格）。 |

> 截图方式（本机 gdmcp runtime 桥与别的代理抢 session，所以走这条路）：直接
> `Godot_v4.7.2-stable_mono_win64.exe --path F:\Godot项目\项目a --resolution 1920x1080 --position 0,0 res://Scenes/Enemies/_AnimShowcase.tscn`，
> 用 `user32!SetWindowPos` 把窗口钉在 (0,0) 置顶，再用 `System.Drawing` 的 `CopyFromScreen` 连拍。

**自测**：整轮自测（`selftest.flag`）跑完，日志里 `=== 自测开始 ===` … `=== 自测结束 ===`、
`Encounter 清场完成`、`端到端：按 F 之后 奖励界面打开 = True` 全在，`ERROR/WARNING` **0 条**；
`selftest.flag` 跑完即删（复查 `Test-Path` = False）。

## 11.3 留给下一轮

1. **玩家角色行（`chr_*.tres`）的动画列当前不生效**：`RunController.ApplyBalance` 不调 `UnitConfig.ApplyTo`，
   所以玩家读的是 `Player.tscn` 上 `Animator` 节点的默认值。**一行修法**：在它的 `character != null` 分支里加
   `character.ApplyAnimationTo(_player);`（本轮边界不含 `RunController.cs`）。细节见 `docs/config.md`。
2. **`jump` / `fall` / `attack-*` 还没接**：32 段里还剩 28 段没用。接入点只有一处（`UnitAnimator`），
   加两个状态分支 + 两列即可。
3. **播放速率没跟随移速**（`SpeedScale` 固定 1）：要更贴地就加 `SpeedScale = clamp(speed / 参考速度)`。

---

# 第五轮：UI 素材 + 主题 + 字体 + 准星 + 竞技场 glow（2026-10）

> **本轮范围**：`Scenes/UI/**`、`Assets/UI/**`（新建）、`Scenes/Levels/TestArena.tscn`（**只动 `WorldEnvironment` 的 `Environment`**）、`docs/ui.md`（新建）。
> **没有动** `Scripts/**`（一个字节都没改）、`Config/**`、`Scenes/Enemies/**`、`Scenes/Player/**`、`Assets/Audio/**`、`Assets/Fonts/**`。
> **界面逻辑一行没改**：`OverlayScreen` 的分层（`Hud`/`Panel`/`Modal`）、`UiRouter`、各 `*Screen.cs` 全部原样；只改外观。
> **完整操作说明见 [ui.md](ui.md)** —— 本节只记"改了什么 + 证据"。

## 12.0 30 秒结论

| 问题 | 答案 |
|---|---|
| UI 用了哪个包？ | **Kenney UI Pack - Sci-Fi 2.0**（切角面板 + 蓝色标题带 + 血条，CC0）+ **Kenney Crosshair Pack 1.1**（准星，CC0）+ **Kenney Fonts**（HUD 数字，CC0）。三个包的 `License.txt` 都是 **CC0**，已照抄进 `Assets/UI/Licenses/`。 |
| 主题怎么组织的？ | `Assets/UI/Themes/ui_theme.tres` **一个文件**统管字体 / 字号 / 颜色 / 面板 / 按钮 / 焦点框；挂在 **8 个界面场景的根 `Control`**（7 弹窗 + `Hud`）。另加 3 个**类型变体**（`UiTitle` / `UiPrimaryButton` / `UiCaption`），场景里一行 `theme_type_variation = &"UiTitle"` 就能用。 |
| 后续加界面要做什么？ | ① 贴图放 `Assets/UI/Textures/`；② 新样式就在主题里加一个变体；③ 节点上写一行 `theme_type_variation`。**零 C#**。四种常见动作见 [ui.md](ui.md) §3。 |
| 中文字体？ | **能显示**，而且是**显式**的：`default_font` = `SystemFont`，按优先级取「微软雅黑 UI → 微软雅黑 → Noto Sans SC → 思源黑体…」。实测 `中/文/·/…/◆/◇/A/7` 全部 `has_char=true`，实解析到 **Microsoft YaHei**。⚠️ **系统字体只是开发期占位，发行前必须换自带字体**（候选 + 许可 + 体积见 §12.4）。 |
| 准星接在哪？ | `Assets/UI/Textures/tex_ui_crosshair_default.png`（Kenney 200 个里挑的第 112 号描边版）→ `Hud.tscn` 的 **`Root/Crosshair`**（`TextureRect`，屏幕中心 56×56）。**原 3 个 `ColorRect` 已删**（无代码引用）。 |
| glow？ | `TestArena.tscn` 的 `WorldEnvironment` 开了：**`glow_hdr_threshold = 2.0`** / `glow_intensity = 0.5` / `glow_bloom = 0` / `glow_blend_mode = 1`(Screen) / 只开 4-6 档。**为什么阈值不是常见的 0.9** 见 §12.5。 |

## 12.1 素材落盘（`Assets/UI/`，全部 CC0）

| 文件 | 来源（包内路径） | 尺寸 | 用在哪 |
|---|---|---|---|
| `Textures/tex_ui_panel_frame.png` | UI Pack `PNG/Extra/Double/panel_glass_notches.png` | 128² | 大面板底（九宫格 34px 切角） |
| `Textures/tex_ui_panel_frame_small.png` | 同上 `Extra/Default/` | 64² | 焦点框（九宫格 17px） |
| `Textures/tex_ui_panel_glass.png` / `_small` | `Extra/{Double,Default}/panel_glass.png` | 128²/64² | 备选圆角面板（本轮未接线） |
| `Textures/tex_ui_panel_glass_small.png` | 同 `_small` | 64² | **所有普通按钮**的底（靠 `modulate_color` 区分 normal/hover/pressed/disabled） |
| `Textures/tex_ui_header_band.png` | `PNG/Blue/Default/button_square_header_large_rectangle.png` | 192×64 | 标题带 / 主按钮（用 `region_rect` 裁顶部 33px 蓝带） |
| `Textures/tex_ui_bar_frame.png` | `PNG/Blue/Default/bar_square_large.png` | 192×48 | 分隔条 / **玩家血条边框** |
| `Textures/tex_ui_bar_fill.png` | `PNG/Blue/Default/bar_square_large_m.png` | 24² | **血条填充**（平铺，跟着代码染色） |
| `Textures/tex_ui_crosshair_default.png` | Crosshair Pack `PNG/Outline (2×)/crosshair-112.png` | 148² | 准星 |
| `Fonts/font_ui_kenney_future.ttf` | Kenney Fonts `Fonts/Kenney Future.ttf` | 34 KB | HUD 数字（`HP 120/120`） |
| `Licenses/license_kenney_*.txt` | 三个包的 `License.txt` | — | CC0 原文（举证用） |

> **两处目录偏离（照实记录）**：① UI 贴图放 `Assets/UI/Textures/` 而不是 `Assets/Textures/`
> （"改界面只看一个目录"）；② 字体放 `Assets/UI/Fonts/` 而不是 `Assets/Fonts/`（后者本轮划给了别的代理），
> 以后要统一，**只改主题里一行引用**。
> **命名**：贴图 `tex_ui_*.png`（照 [assets.md](assets.md) §2）；字体是新类别，用了 `font_ui_*.ttf`（建议给 assets.md 补一行）。

## 12.2 改了哪些文件（逐项）

**新建**

| 文件 | 内容 |
|---|---|
| `Assets/UI/Themes/ui_theme.tres` | 主题：`SystemFont`（中文）+ 13 个 `StyleBoxTexture` + 3 个类型变体 |
| `Assets/UI/**`（贴图/字体/许可，见 §12.1） | — |
| `Scenes/UI/_UiShowcase.tscn` + `_ui_shot.gd` | 拍摄台（见 §12.6） |
| `docs/ui.md` | UI 外观的唯一入口文档 |

**改（全部只动外观）**

| 文件 | 改了什么 |
|---|---|
| `Scenes/UI/{Base,BossPreview,Choose,Help,Loading,Reward,Start}Screen.tscn` | 各加 1 行 `ext_resource(Theme)` + 根 `Control` 上 1 行 `theme = ...`；`load_steps` 同步 +1。带标题的 5 个界面把 `Title` 的 `theme_override_font_sizes/font_size = 30~34` 换成 `theme_type_variation = &"UiTitle"`，`Hint` 换成 `&"UiCaption"` |
| `Scenes/UI/BaseScreen.tscn`（**完整示范**） | 另加一个 `Divider`（`NinePatchRect` + `tex_ui_bar_frame.png`），`Start` 按钮换成 `&"UiPrimaryButton"`（蓝色实心） |
| `Scenes/UI/Hud.tscn` | 加主题；**删 3 个 `ColorRect` 准星 → 1 个 `Root/Crosshair`（贴图）**；`Health` 标签加 `theme_override_fonts/font`（Kenney Future） |
| `Scenes/UI/Bars/BarPlayer.tscn` | `BarBg`：`ColorRect` → `NinePatchRect` + 血条边框贴图（**名字保持 `BarBg`**，代码找的是 `Bg`）；`Fill`/`DelayBar` 贴图：`res://icon.svg`（**平铺的 Godot 图标**）→ `tex_ui_bar_fill.png` |
| `Scenes/Levels/TestArena.tscn` | **只加 glow 那一段**（`Environment` 的 11 行），其它节点一个字节没动 |

**没碰**：`Scripts/**`（0 改动）、`Config/**`、`Scenes/Enemies/**`、`Scenes/Player/**`、`Assets/Audio/**`、`Assets/Fonts/**`、
`OverlayScreen.ScreenLayer`、任何节点名（除删掉的那 3 个无引用的准星 `ColorRect`）。

## 12.3 界面截图（7 弹窗 + HUD）

| 图 | 看什么 |
|---|---|
| [ui_01_basescreen.png](screenshots/ui_01_basescreen.png) | **完整示范**：切角面板 + 蓝色标题带（`UiTitle`）+ 青色分隔条 + 5 个贴图按钮 + 蓝色主按钮（`UiPrimaryButton`）+ 灰色说明文字（`UiCaption`），中文正文正常 |
| [ui_02_helpscreen.png](screenshots/ui_02_helpscreen.png) | 同一套主题下的**长中文正文**（30+ 行），换行 / 对齐 / 描边都正常 —— 这张同时是"中文字体可用"的证据 |
| [ui_03_startscreen.png](screenshots/ui_03_startscreen.png) | 武器选择：标题带 + 2 张卡片按钮 |
| [ui_04_rewardscreen.png](screenshots/ui_04_rewardscreen.png) | 三选一奖励：标题带 + 3 张卡片按钮 + 说明文字 |
| [ui_05_choosescreen.png](screenshots/ui_05_choosescreen.png) | 池子选择（中文长卡片文案不溢出） |
| [ui_06_bosspreview.png](screenshots/ui_06_bosspreview.png) | 首领状况面板（多行正文 + `◆`/`◇` 两个符号有字形） |
| [ui_07_loadingscreen.png](screenshots/ui_07_loadingscreen.png) | 加载界面（标题 + 说明两行） |
| [ui_08_hud_ingame.png](screenshots/ui_08_hud_ingame.png) | **HUD 实机**（拍摄台挂**真的** `Player.tscn` + `Hud.tscn`）：左上 `HP 120/120` 用 **Kenney Future**、**绿色血条 + 九宫格金属边框**、屏幕中心 **Kenney 准星**、右下 `8 / 8` 弹药 |
| [ui_09_hud_crosshair.png](screenshots/ui_09_hud_crosshair.png) | 同一次运行的下一帧（相机/玩家位置略有变化，用于核对准星是贴在屏幕中心的） |

> ⚠️ 卡片按钮的文字是拍摄台**手动填的样本文案**（真实文案由 C# 运行时写入）。
> 因为拍摄台**不调各界面的 `Open()`**（那要真实数据、还会触发分层暂停），只把根 `Control` 点亮 ——
> **拍的是外观，一行界面逻辑都没走**。

## 12.4 字体：结论（含"为什么没自带"）

- **能显示中文**：`SystemFont.font_names` 按优先级列了 8 个候选 + `sans-serif`，`allow_system_fallback = true`。
  探针实测（`--headless --script`）：`has_char` 对 `中` `文` `·` `…` `◆` `◇` `A` `7` **全部 true**，
  `get_font_name()` = **Microsoft YaHei**；本机 `OS.get_system_fonts()` = **115** 个，其中
  `Microsoft YaHei UI` / `Microsoft YaHei` / `SimHei` / `DengXian` / `SimSun` 都在。
- **英文/数字另配**：Kenney Future（CC0）用在 HUD 的 `Health` 标签。
- ⚠️ **`SystemFont` 是开发期占位**（玩家机器上不一定有雅黑）→ **发行前换成自带字体**。
- **本轮没有下载任何中文字体**（任务约束"不要下大文件"）。**实测体积**（读 GitHub API，不是估的）：

  | 候选 | 许可（项目声明） | 单字重体积 |
  |---|---|---|
  | **Noto Sans SC** | SIL OFL 1.1（`ofl/notosanssc/OFL.txt`） | `NotoSansSC[wght].ttf` = **17,772,300 B ≈ 17.0 MB** |
  | **霞鹜文楷 LXGW WenKai** | SIL OFL 1.1 | `LXGWWenKai-Regular.ttf` = **25,575,676 B ≈ 24.4 MB** |
  | **得意黑 Smiley Sans** | SIL OFL 1.1 | 发布包 5,781,344 B≈5.5 MB（含 2 字重）→ 单字重 ≈ **2.7 MB** |
  | 微软雅黑 / 黑体 / 宋体 | **专有，不可再分发** | 只能像现在这样"按名字引用"，**不能入库** |

  > 诚实交代：**我没有逐字读完许可原文**（抓 `LICENSE`/`OFL.txt` 原文时网络超时），
  > 上面"许可"一列来自各自仓库/发布页的声明与目录结构（`ofl/` + `OFL.txt`）。真要发行**请自己再确认一次**。
- **换法 3 步**（+ 强烈建议 `pyftsubset` 裁剪到 1~2 MB，Godot 原生支持 `.woff2`）见 [ui.md](ui.md) §4.3。

## 12.5 glow：参数与"为什么阈值 2.0"

- **改**：`Scenes/Levels/TestArena.tscn` 的 `Environment`（只这一段）：
  `glow_enabled=true` / `glow_normalized=true` / `glow_intensity=0.5` / `glow_strength=1.0` /
  `glow_bloom=0.0` / `glow_blend_mode=1`(Screen) / **`glow_hdr_threshold=2.0`** / `glow_hdr_scale=2.0` /
  `glow_hdr_luminance_cap=12.0` / `glow_levels/4,5,6 = 0.8,0.6,0.4`（1/2/3/7 = 0）。
- **为什么不是常见的 0.9~1.2**：这张竞技场的地面（Space Station Kit 地板 + `ambient_light_energy=0.55` + filmic）
  **本来就接近纯白**。实测 4 档参数（同一机位、同一帧数、同一发爆炸）：

  | 档 | 阈值 / 强度 / bloom / blend | 空场 | 爆炸 |
  |---|---|---|---|
  | `z_off` | 关 | 基线 | 火球是一块**平黄**、火星是**平线** |
  | `a` | 0.9 / 0.9 / 0.15 / Additive | **整屏泛白、天空褪成淡紫** = 过曝 | 火球爆成白团，画面糊 |
  | `b` | 1.4 / 0.6 / 0.05 / Screen | 天空仍明显变亮 | 火球有光晕，但整体仍偏亮 |
  | **`c`（采用）** | **2.0 / 0.5 / 0 / Screen** | **与基线基本一致**（只有天空亮一点点） | **白热核心 + 橙色外焰 + 光晕**，火星带光带 |
  | `d` | 3.0 / 0.5 / 0 / Screen | 与基线几乎相同 | 只对最亮的火星有一点点作用（太弱） |

- **证据**：4 档合成图 [ui_glow_trials_arena.png](screenshots/ui_glow_trials_arena.png) /
  [ui_glow_trials_explosion.png](screenshots/ui_glow_trials_explosion.png) /
  [ui_glow_trials_muzzle.png](screenshots/ui_glow_trials_muzzle.png)；
  **定稿前后对比（用文件里那组参数，不在运行时注入）**：
  [ui_glow_00_before_after.png](screenshots/ui_glow_00_before_after.png)
  （每行左 ON / 右 OFF：爆炸 t0、枪口火光 t0、空场），单张为 `ui_glow_01..06`。
- **结论**：**爆炸与枪口火光明显变亮**（火球有白热核心与外焰、枪口从"小白点"变成"带暖色光晕的亮点"），
  **空场几乎不变**（地板没被吃进 bloom）。唯一副作用：**天空比原来略亮一点**（星云亮部在阈值附近）。
- 想要更强：把 `glow_hdr_threshold` 降到 1.4（天空会明显泛白）；**更彻底的办法是先把地板材质压暗**（它接近纯白才是瓶颈）。

## 12.6 验证证据（本轮实际跑过的）

| 项 | 命令 / 方式 | 结果 |
|---|---|---|
| 编译 | `build.ps1 --no-incremental -v minimal` | **0 warning / 0 error**（本轮没改 C#，只是复核） |
| 资源导入 | `Godot.exe --headless --path <项目> --import` | 新 PNG / TTF 全部生成 `.import`，无报错 |
| 主题 / 字体探针 | `Godot.exe --headless --path <项目> --script res://_ui_probe.gd`（临时脚本，跑完已删） | 主题加载 ✓；`default_font` 解析到 Microsoft YaHei ✓；7 个字形 `has_char` 全 true ✓；`UiTitle/UiPrimaryButton/UiCaption` 的 `base_type` 与 `StyleBoxTexture.region_rect=(6,0,180,33)` 回读正确 ✓ |
| 界面 / HUD / glow 截图 | `Godot.exe --path <项目> res://Scenes/UI/_UiShowcase.tscn [-- ui\|glow\|final\|hud]` | 7 个界面 + 2 张 HUD + 6 张定稿 glow + 4 档试拍，全部 `1920×1080 err=0` |
| 游戏内自测 | `selftest.flag` + 直接跑 `res://Scenes/Main.tscn` | ✅ `Encounter 清场完成：共生成 4，用时 3s` / `端到端：按 F 之后 奖励界面打开 = True Paused = True` / `=== 自测结束 ===`；进程 **25 s 自动退出，exit code 0** |
| 日志 | 最新 `run_*.log` 匹配 `ERROR\|Exception` | **0 命中**；stderr 只剩 2 条**既有** WARNING（`MapManager 导航网格为空`、navmesh `cell_height` 不一致） |
| `selftest.flag` | `Remove-Item` + `Test-Path` | ✅ 已删除（复查 False） |
| 未动的目录 | `Scripts/**`（0 改动）、`Config/**`、`Scenes/Enemies/**`、`Scenes/Player/**`、`Assets/Audio/**`、`Assets/Fonts/**` | 一个文件都没碰 |

**拍摄台用法**（留在仓库里，以后换 UI 素材直接复用）：

```powershell
$godot='F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe'
& $godot --path 'F:\Godot项目\项目a' 'res://Scenes/UI/_UiShowcase.tscn'              # 全部
& $godot --path 'F:\Godot项目\项目a' 'res://Scenes/UI/_UiShowcase.tscn' -- ui        # 只拍 7 个界面
& $godot --path 'F:\Godot项目\项目a' 'res://Scenes/UI/_UiShowcase.tscn' -- hud       # 只拍 HUD 实机
& $godot --path 'F:\Godot项目\项目a' 'res://Scenes/UI/_UiShowcase.tscn' -- glow      # glow 4 档试拍
& $godot --path 'F:\Godot项目\项目a' 'res://Scenes/UI/_UiShowcase.tscn' -- final     # glow 定稿 ON/OFF 对比
```

> **两个坑（都实测踩到）**：
> ① 拍摄台**直接实例化 `TestArena.tscn`** → 拍到的 glow 永远是"游戏里真正那个环境"，
> 不会出现"展示场景自己开了 glow、游戏里没开"的假证据；
> ② **不要把 Godot 窗口最小化 / 完全遮挡**：不渲染时 `RenderingServer.frame_post_draw` 永不触发，
> 拍摄脚本会**永久卡死**（我用 `Start-Process -WindowStyle Minimized` 卡了 100 秒以上才定位到）。
> 另外：本机的 **gdmcp runtime 桥与其它代理抢 session**（`run_project` 报 *no debugger session became active*），
> 所以本轮**没用 gdmcp 截图**，全部走"自己存 viewport PNG"这条路 —— 与上一轮 `_art_shot.gd` 的做法一致。

## 12.7 本轮明确"试过但没采用"的

| 试过的 | 结果 | 为什么放弃 |
|---|---|---|
| glow 阈值 0.9（网上常见值） | 整屏泛白、天空褪成淡紫 | **过曝**，与"更亮而不是过曝"的目标相反（合成图里 `a_thr090_i090` 就是这一档） |
| `glow_bloom = 0.15` | 给全屏加了一层白雾 | 这张场地地板接近纯白，基础 bloom 只会让画面发灰 |
| `glow_blend_mode = 0`（Additive，Godot 默认） | 亮部更容易顶到死白 | 换 Screen（1）后高光仍有层次 |
| 把血条 `Fill` 换成"带端帽的完整血条图" | 没采用 | 代码会把 `Fill.Size.X` 按血量比例改小 → 端帽会被**横向压扁**；改用**中间段平铺**（`tex_ui_bar_fill.png` + `stretch_mode=1`）才对 |
| 给全部 7 个弹窗逐个"精修" | 没做 | 任务明确"**不求做满，但每个位置都要占住、且有一个做得好的示范**" —— 主题已让 7 个界面统一换皮，示范做了 `BaseScreen`（完整）+ `HelpScreen`（长文本），剩下的是体力活 |

## 12.8 给后续的交接（下一步按性价比）

1. **中文自带字体**（发行前必做）：Noto Sans SC（17 MB）或得意黑（2.7 MB）或裁字到 1~2 MB，换主题里一行。见 [ui.md](ui.md) §4.3。
2. **`BarEnemy.tscn` / `BarBoss.tscn` 换贴图**（照 `BarPlayer.tscn` 抄）；`BarBoss` 的 `Icon` 现在还是 Godot 图标占位。
3. **加"危险/警示"色变体**（红）：主题里加 `UiDangerButton` 即可，界面里一行引用。
4. **面板装饰件**：Kenney 包里还有 `panel_glass_tab`（标题凸舌）/ `panel_glass_screws`（螺丝）/ 单角切角件，
   可以叠在面板上把"科技感"再做足一点。
5. **UI 音效**：`Assets/Audio/` 里已有别人放的 `ui_click.ogg` / `ui_open.ogg`（Kenney Interface Sounds, CC0）→
   挂到主题的 `Button` 音效或 `UiRouter` 上（要碰 `Scripts/`）。
6. **准星随散布/瞄准变化**：要写 C#（改 `Crosshair` 的 `scale`/`modulate`）。
