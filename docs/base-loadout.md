# 基地战斗与角色换装（C批）

基地现在使用真实技能、动画事件、命中与伤害结算。`BaseCamp`管理练习场生命周期，基地没有RunController、StageController或奖励结算。
`PracticeTarget`复用TrainingDummy真实单位；有护甲、会死亡，2秒后重建。基地死亡会重建基地，越界回出生点，均不发碎片。

## 单一装备入口

`PlayerLoadout.Apply`供关卡RunController和基地LoadoutPreview共同调用，顺序如下：

1. 取消旧能力并执行换装清理，清空当前技能槽、停用仓库能力和旧输入绑定；移除旧灵动额外跳跃和滞空。
2. 按角色表套基础数值与跳跃/奔跑参数，应用局外成长；按owner去掉旧修饰再添加，反复换装不叠加。
3. 套角色技能、攻击/动画配置与武器技能，将当前声明的技能索引装入`Unit.AbilitySlots`；启用/禁用矩阵只作用于这些技能。
4. 应用能力卡，清理旧输入队列／按住状态，再切换角色与握持武器表现。基地能力卡改变后会重新应用。

J39：配装默认值与当前槽位由玩家实例持有，重新创建PlayerLoadout也不会把上次配装误当默认。`PlayerAbilityInput`只执行槽位唯一对象；`PlayerWeaponInput`不再重复触发技能驱动的左键。HUD同样读当前槽，不读RunSetup猜测配装。

`AbilityConfig.InputAction`用于安装默认槽，InputMap负责Q／鼠标等实际按键。`AbilitySlots.AssignToSlot(unit, "skill_1", row)`可将任意技能索引显式装入技能1；其原本的InputAction不影响该槽路由。仓库可以保留其它角色技能，只有当前装槽对象响应输入。换装前排队、按住或尚未释放的旧输入必须先松开，不能穿越到新技能。

玩家投掷路径通过当前槽位的`UpdateInputPreview(held)`消费实际输入：按住瞄准时更新，松手、换装和死亡隐藏；不能以长期保持的`IsActive/InFlight`决定玩家预览显隐。程序接口蓄力与玩家按键预览独立，详见[J40](character-design-j40.md)。

`Ability.ResetForLoadout`只在装备重配时调用，沿用取消生命周期；后撤、闪和架补齐其持有的无敌/碰撞/订阅清理。
本轮纠正原始设计归属：落剑式只在灵剑的武器技能行引用，不再放在角色B共通技能中。灵剑6格；镰刀和长枪没有额外落剑式，为5格。共通“落”仍保留，长枪专属方向改型另行核验。

## 角色和武器归属

六条WeaponConfig行新增强类型 `OwnerCharacter:CharacterConfig` 引用。列表、选择入口及实际初始化共用这份归属。
StartScreen只列当前角色的武器；当前项有✓并禁用，重复数字键也不执行；列表可滚动，图标读取武器行IconPath。
`LoadoutSelection.SelectWeapon`拒绝不合法组合且不修改装备；旧配装若已不合法，初始化会使用该角色的第一把合法武器。
GM的run命令也在写入前校验归属，避免报失败后留下半套配置。

`CharacterRoster`遍历角色表实例化CharacterStand，每个对象指向一个角色行。F交互直接切人并打开其武器页，
不再先弹出另一个角色列表。旧BaseTerminal的Loadout种类保留为当前角色武器页的兼容入口。

## 选择记忆

正式选择经LoadoutSelection写入独立 `user://loadout.cfg`，不向meta.cfg塞字段，避免MetaProgress.Save覆盖。
`[selection] character`保存最后角色Id；`[weapons] <角色Id>`保存对应武器Id。
读档按现有表和归属重新校验，失效Id回到合法默认武器；没有文件时只读默认值，不自动创建文件。
BaseCamp等索引就绪后恢复选择，避免首帧查表为空而漏掉存档。RunSetup仍是跨场景的运行期配置；直接写它不自动落盘。
GM临时配置和自动化测试不改玩家的正式选择记忆。

## 可替换的角色表现

`CharacterDisplay`是纯表现组件，玩家与CharacterStand共用。角色行ModelPath决定身体，武器行ModelPath决定握持场景；
当前猎手使用Grigory_Petrov的Lyra，武器大师使用Samurai；六个握持场景仍为本项目自建低模几何。
角色行ModelTransform单独配置模型缩放/转向，身体碰撞与单位根节点保持原尺寸；展台保留底座高度。
角色模型缓存并切换Visible，WeaponGripScene按骨架配置握持场景；新角色挂RightHand，旧骨架默认挂arm-right，不创建碰撞、单位或分组。
替换骨架时需同时配置兼容的握持骨骼/Socket和对应技能动画库，不能只换路径后假定任意骨架都兼容。

实际UnitAnimator绑定当前可见模型。技能动画在私有副本中把同名骨骼轨道重定向到实际Skeleton路径，
关键帧和方法事件不变，不改磁盘动画或共享缓存；预热在播放之前完成。
两套新骨架各自使用离线生成的技能库，灵剑5段使用作者原生攻击片段并对齐事件，其余技能按全局rest差值适配。详见[角色替换验收](character-replacement.md)。
受击闪色、蓄力身体表现与后撤残影跟随当前可见模型，旧材质通道在切换时归还。

验收、截图和未解决问题见 [c-batch-verification.md](c-batch-verification.md)。
