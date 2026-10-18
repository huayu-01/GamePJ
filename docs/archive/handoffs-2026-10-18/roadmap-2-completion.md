> 历史归档（2026-10-18）。当前任务与约定以 [SESSION-SNAPSHOT](../../SESSION-SNAPSHOT.md) 为准；其中旧交接／新建对话授权不再生效。

# 第二轮18条需求完成核对（2026-10-17）

本表是当前状态；roadmap-2.md中的“现状侦察”与分批改法保留作历史依据。
执行顺序为A→E→B→C→D。美术终版、平衡与既有技术问题另列专项，不冒充本轮已经解决。

| 用户条目 | 完成内容 | 实现批次与证据 |
|---|---|---|
| 1 | ESC打开设置，帮助并为独立分页 | [A验收](../../a-batch-verification.md) |
| 2 | 基地实际执行技能、武技、伤害与死亡练习重建 | [C验收](../../c-batch-verification.md) |
| 3 | 每角色独立交互对象，武器强类型归属过滤并拒绝越界选择 | [C验收](../../c-batch-verification.md) |
| 4 | 展示角色模型、即时切换玩家与武技、按角色保存上次武器、当前项高亮 | [C验收](../../c-batch-verification.md) |
| 5 | 右下技能图标、真实CD扇形、层数与按键 | [B验收](../../b-batch-verification.md) |
| 6 | Shift奔跑提速、角色表参数、Ctrl下落攻击与风线 | P6d已实现；C真实输入与A表现回归 |
| 7 | 64px竖向额度条，置于卡片左侧，自下向上填充 | [A验收](../../a-batch-verification.md) |
| 8 | 每页最多6×3、无空卡占位、从左向右排列 | [A验收](../../a-batch-verification.md) |
| 9 | 血条内显示当前/上限与异色护甲 | [A验收](../../a-batch-verification.md) |
| 10 | 完美窗口与方形蓄力准星仅能量掌武技2 | P7已实现，A范围检查；[蓄力契约§10](../../abilities.md) |
| 11 | 调试GM命令、资源/增幅/技能/召敌/交互/场景调用 | [B验收](../../b-batch-verification.md) |
| 12 | 九层命名、全单位World-only、攻击盒与弹道分离、全量回归 | [D验收](../../d-batch-verification.md)与[架构对照](../../architecture.md) |
| 13 | 刹那之眼按住维持、并用技能、不占动作，保留已确认0.3s软启动与防御 | [E验收](../../e-batch-verification.md) |
| 14 | 状态表配置通用表现，Mark层数，过期/死亡清理 | [E验收](../../e-batch-verification.md) |
| 15 | GM独立预制体，自带G入口；移除节点后入口消失，ExportRelease禁用 | [B验收](../../b-batch-verification.md) |
| 16 | 超过18张卡翻页 | [A验收](../../a-batch-verification.md) |
| 17 | 基地木桩真实受击、击破与重建，无关卡资源结算 | [C验收](../../c-batch-verification.md) |
| 18 | 准星缩→闭合→变色→0.3s后恢复颜色且保持闭合；战斗完美窗口仍0.2s | P7已实现；[abilities.md §10](../../abilities.md)、历史P7截图 |

用户追加的“所有武器蓄力完成反馈与音效”已由P7的ChargeBodyFx通用实现；
刹那之眼按用户第13条定义为纯维持效果，不再冒充蓄力技能。

## 美术与后续专项边界

- 卡通低多面体方向、庭院光照/环境、Hit/Explosion/Muzzle已接入；可选像素滤镜保持独立屏幕层，HUD在上方。
- 免费来源与许可见 [art-refresh-sources.md](../../art-refresh-sources.md)；Loot/Battle当前为可复用预制体，生产接线边界见 [art-refresh-handoff.md](../../art-refresh-handoff.md)。
- 角色美术由另一个用户任务继续更换；本批不混入其素材或动画提交，回归读取角色表现表，不锁死旧占位模型文件名。
- 整体画面尚非终版；角色平衡、自带中文字体、破甲环、导航、旧P4期望偏差与偶发退出异常保留专项清单。
