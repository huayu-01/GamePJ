# 美术源文件与模型制作产线

本目录保存概念图、生成模型、Blender 工程、动作源文件和导出候选。
`.gdignore` 使 Godot 跳过整个源文件目录；验收后的运行资源另行放入项目 `Assets/`。

| 目录 | 用途 |
|---|---|
| `Concepts/` | 设计稿、统一风格参考及一致的多视图 |
| `Generated/` | Tripo、混元等工具的原始输出，保留生成信息 |
| `Blender/` | 整理后的 `.blend` 主工程 |
| `Animations/` | Mixamo 下载、重定向结果及自制动作源文件 |
| `Exports/` | 待验收的 GLB、贴图与预览 |
| `Tools/` | 安装记录、环境检查及制作辅助脚本 |

每件资产建议使用固定英文标识作为子目录名，并保留输入图、原始输出与可编辑源文件。
既有游戏资产不作为新造型参考。方向为 Toon Nature 所示的明亮自然、日式动画风格。

初期工作流：概念设计 → 三维生成 → 拓扑及材质整理 → 绑定及动作 → GLB → Godot 验收。

开始角色建模前先读[参考方法与制作经验](modeling-notes.md)。用户要求多找参照、先学准比例和结构关系，再替换角色识别元素；后续反馈持续归入该记录。当前[三叶旅童候选](Exports/clover-wayfarer/README.md)保留了本轮造型、比例、布料及绑定迭代。

动画制作前可先看[首批基础动作参考册](Animations/References/clover-wayfarer-basics/README.md)：12段人形源动作，附原速合集、慢放GIF与具体适配提示。参考允许包含完整手部动作。

Blender 插件安装和验证完成后，具体版本、来源及启动方法记录在 `Tools/SETUP.md`。

首个人形样件已跑通混元生成、Blender 自动权重与三个自制测试动作、GLB 导出及独立 Godot 导入。
查看 [样件说明](Exports/humanoid-pilot/README.md)，或直接打开 `Blender/humanoid-pilot/humanoid-pilot.blend`。

纯岩块敌人已沿同一条线完成混元、Blender、17 骨动作和森林样板接入，见[石头人 Plus 产线记录](../docs/stone-golem-plus-pipeline.md)。
