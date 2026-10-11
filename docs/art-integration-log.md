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
2. **给玩家加 walk/sprint 动画**：`.glb` 里现成有 `walk` / `sprint` / `jump` / `fall` / `die`，
   缺的只是一个按 `CharacterMovement` 速度切片的 `UnitAnimator`（新代码，本次没做）。
3. **子弹拖尾的截图**（§6.5），顺手决定要不要把拖尾改成"弹丸销毁后仍存活一小会儿"
   （现在拖尾是弹丸的子节点，命中即整个消失）。
4. **命中火花（Binbun Hit FX）/ 爆炸（Explosion FX）** —— 包已经下好了，在 `F:\Godot项目\_art_staging\`。
   接爆炸时**别碰 `Explosion.tscn` 的 `SphereShape3D`**。
5. **UI / 音效 / 字体** —— 等 `Scenes/UI/` 那个代理收工；素材（Kenney UI Pack Space Expansion / Fonts /
   3 个音效包）也都已经在 `_art_staging\` 里了。
6. **Modular SciFi MegaKit**（46 MB，CC0）如果要用来做"更写实的科幻关"，
   注意它的 glTF 是**外部 PBR 三件套**（BaseColor / Normal / ORM，单个模块 ~10 MB），
   且 `images[].uri` 只写裸文件名（`T_Trim_01_Normal.png`），**必须把 `Textures/*.png` 复制到 `.gltf` 旁边**才认得。
