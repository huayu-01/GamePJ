# 083 飞刀·刀舞冷却 0.5 秒

2026-10-31。按验收表 083 落地；未提交/推送。

## 改动

- `Scenes/Abilities/Builds/KnifeDance.tscn`：`Cooldown 1→0.5`。
- 伤害 12、追击 3×8、追击间隔、弹速、动画和替换前置均未改。

## 验证

- Debug / ExportRelease：0 警告 0 错误。
- `_KnifeVolleyProbe`：**218/1**；唯一失败为“专一保留少八把的 80% 逐把伤害补偿”，与刀舞冷却无关。
- 场景配置读取 `Cooldown=0.5`；未新增独立冷却专项读数。

## 未完成

- 玩家验收未填；未 commit/push。
- 该专项既有 1 项失败未修（不属于 083）。
