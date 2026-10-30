# Clover Wayfarer

运行候选来自 [ArtSource可编辑主工程](../../../ArtSource/Blender/clover-wayfarer/clover-wayfarer.blend)。当前v10浅球面木面具，24网格、3362三角面、15骨，基础动作60fps；姿态预览与造型迭代见[源资产说明](../../../ArtSource/Exports/clover-wayfarer/README.md)。

`clover-wayfarer.glb`只含角色、材质、蒙皮与六段基础动作。`skills/`是本骨架的轻施法姿态和原猎手事件时刻，用 [_tools/build_clover_skills.gd](../../../_tools/build_clover_skills.gd)生成。

使用 [UnitVisualProfile](../../../Config/VisualProfiles/clover_wayfarer.tres)、[模型预制体](../../../Scenes/Visual/CloverWayfarer.tscn)与[悬浮武器焦点](../../../Scenes/Visual/CloverWeaponFocus.tscn)接入。2026-10-26已将正式角色A（猎手）的VisualProfile切换至该档案，基地展台、森林冒险和生存试炼共用；[森林战斗试装入口](../../../Scenes/Debug/CloverCombatLab.tscn)继续保留，详见[试玩和验证](../../../docs/clover-combat-trial.md)。

模型为本项目制作；基础动作从已有Quaternius CC0动画库适配，公开来源与本地库记录见[参考册](../../../ArtSource/Animations/References/clover-wayfarer-basics/README.md)。ORICUT公开角色只作为设计参考，没有使用其商业模型或动作文件。
