# GM控制台（B-2）

`Scenes/Debug/GmConsole.tscn` 是完整可插拔预制体，自带界面、命令和G键输入。
当前基地与Main各挂一份；不注册autoload或InputMap，删除节点后G不再响应。
`Enabled=false` 会自行移除。C# `DEBUG` 与 `OS.IsDebugBuild()` 双重限制开发构建；
Godot正式配置 `ExportRelease` 中不会启用控制台。移除场景里的实例即可完全去掉入口。

按 **G** 打开，**Esc** 关闭；打开会暂停并释放鼠标，关闭恢复原有界面栈。
输入框支持连续回车提交、Tab补全命令/ID、↑↓历史。面板有常用按钮和真实ID目录，输出可复制。
控制台内输入字母g不会关闭面板；G只负责打开，避免打断 `give` 等命令。

| 命令 | 行为 |
|---|---|
| `help` | 查询用法 |
| `list skills` | 列出当前启用技能、ID、按键、冷却及层数 |
| `list upgrades [词]` | 从增幅表列出ID、名字、类型，可按ID/名称过滤 |
| `list enemies [词]` | 从敌人表列出ID、名字、基础生命 |
| `list interactions` | 列出当前可用交互物及本次列表序号 |
| `resource 100` | 增加100碎片并保存到meta.cfg |
| `give upg_power` | 通过RunController.GrantReward直接发放增幅；GM跳过抽取限定 |
| `cdr 0.5` | 基础CD倍率设为0.5，保留增幅修正，下次技能收尾采用 |
| `charges skl_a1_skill_1 3` | 最大层数设为3并补满 |
| `refill [技能ID]` | 清CD、补层；省略ID则操作全部启用技能 |
| `invincible [on/off]` | 无敌开关，省略参数切换；节点卸载退订并还原原状态 |
| `heal` | 按当前属性恢复生命、护甲与护盾 |
| `spawn enemy_chaser 2` | 按敌人基表生成两个单位，不计入挑战波次；单次1～20 |
| `interact 1` | 传送到列表中序号1的交互物附近，更新范围并走真实Interact链路 |
| `run [角色ID 武器ID]` | 使用所选配装出征；如 `run chr_hunter wpn_bow` |
| `base` | 返回基地 |

碎片使用既有货币与存档入口，不新建货币。其余调试改动仅作用于当前实例，场景重建后恢复表值。
交互仍遵守挑战者与节点条件，不通过字符串反射调用任意方法，也不执行任意脚本。
场景切换前用 `UiRouter.ResetForSceneChange()` 清除旧界面引用，避免奖励模态造成跨场景残留暂停。
卸载GM只移除本界面，若下面仍有设置页，继续保持设置页的暂停，不强行恢复游戏。

GM是调试工具，允许调用公开战斗/资源接口；它不属于只读表现层。正式HUD仍只读运行状态。
验证入口及存档恢复说明见 [B批验收](b-batch-verification.md)。

⚠️ `ExportRelease`编译与开关禁用可在本地验收，完整发行可执行文件导出/运行需另行验证。
