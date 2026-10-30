# 交互组件接入

更新日期：2026-10-24。使用 [InteractionComponent](../Scripts/Interaction/InteractionComponent.cs)，按 `Kind` 自动生成新类型模型，并接入现有 F 交互、奖励、首领门和基地界面。内置类型现在通过 `Config/Interactions/` 的资源安装后端，当前解耦状态见[实施记录](content-extension-plan.md)；J48保留为原类型模型的历史验收。

## 最快接入

1. 在场景中准备一个 `Node3D` 宿主，将 `Scenes/Interactables/InteractionComponent.tscn` 拖到它下面。也可添加 `InteractionComponent` 节点，无需另接模型。
2. 在 Inspector 的 `Kind` 下拉框选择用途，编辑器自动显示对应的新模型预览。没有自定义 `Definition` 时，`None` 不注册交互；`Prompt` 留空沿用类型提示。
3. 配置范围：有 `Area3D` 时按下一节设置；没有 Area 时用 `InteractRadius`，默认3.2米。
4. 保持 `UseMode=Default`，补齐类型参数并消除配置警告；运行后走近按 F。进入 Area 只代表在范围内，不会自动执行交互。

推荐层级：

```text
Object (Node3D)
├─ Trigger (Area3D，可选)
│  └─ Shape (CollisionShape3D)
└─ InteractionComponent
```

编辑器切换 `Kind` 会更新模型预览。业务类型与参数仍在开局读取，运行时创建一个现有交互后端；运行中修改 Inspector 配置不会重建业务，须停止并重新运行场景。

## 自定义后端和外观

新建 `InteractionDefinition`，把继承 `Interactable` 的后端场景放入 `BackendScene`，把外观场景放入 `ModelScene`，再赋给组件的 `Definition`。此入口优先于 `Kind`，可以保留 `Kind=None`；增加交互不需要修改 `InteractionComponent` 的创建分支。后端负责业务，模型只承担预览、可见状态和反馈，根类型错误会在配置检查中报告。

未指定 `ModelScene` 时，用 `VisualStyle` 选择现有几何外观。已有角色展台、出击门和奖励后端通过 `IInteractionConfigurationConsumer` 自行读取组件的角色、目标场景与挑战开关；自定义后端可实现相同接口，或直接在自己的预制体中配置参数。外观主网格命名为 `Mesh` 以接入既有描边，允许处于模型子层级。

奖励后端还可用 `RewardNode.Definition` 指向自定义 `NodeConfig`，收益由 `RewardBehavior` 策略控制；同一旧类型可安装不同收益和价值配置。各策略与单次完成／取消约定见[内容制作](content-authoring.md)。

## 范围与宿主

`TriggerArea` 可显式拖入；未指定时依次使用：组件的父 Area → `HostRoot` 自身是 Area → 宿主唯一的直接子 Area。宿主有多个直接子 Area 时须明确指定，组件不会猜选。

Area 必须同时满足：

- 开启 `Monitoring`。
- `CollisionMask` 包含 `UnitBody`（第10层，推荐）或 `Player`（第2层），能检测玩家身体。
- 有启用的 `CollisionShape3D`，且已分配 `Shape`。

未找到 Area 时才使用半径交互。Area 存在但配置错误时，应修正配置，不能依赖半径代替。

`HostRoot` 默认取父 `Node3D`；组件直接放在 `StageController` 下时默认取组件自身。可显式指定组件的祖先作为完整宿主，布点移动会带动整个宿主。组件挂在 Area 下、需要将整个装置一起布点时，将 `HostRoot` 指向完整装置的祖先物体。

一个宿主只使用一个组件；不要挂到已经具有 `Interactable` 后端的物体下面。重复宿主、模糊 Area、未指定定义的 `None` 或内置角色类型缺少角色配置时不注册后端。

## 类型与必要参数

| `Kind` | 行为／必要条件 |
|---|---|
| `None` | 不注册交互 |
| `Enhancement`／`Evolution`／`Mutation` | 原强化／进化／异化奖励，领取后按 `SpawnsEncounter` 决定是否挑战 |
| `Resource`／`Heal`／`Special`／`Shop` | 原材料／恢复／特殊奖励／商店流程；同样使用奖励侧 `SpawnsEncounter` |
| `BossDoor` | 原首领门逻辑；放在 Stage 子树内 |
| `Exit` | 首领结束后的离场节点；放在 Stage 子树内，开局隐藏，首领结束后激活 |
| `Expedition` | 出击入口；`TargetScene` 指定目标场景，默认 `res://Scenes/Expedition.tscn` |
| `AbilityCards` | 打开能力矩阵 |
| `Loadout` | 打开当前角色的武器选择页 |
| `Runes` | 打开符文携带页 |
| `Help` | 打开操作帮助 |
| `Character` | 将角色 `CharacterConfig` 资源拖入 `Character` 字段 |
| `Signal` | 不附加奖励或界面业务，交互成功发出无参 `Triggered` 信号，可连接自定义机关 |

奖励、首领门和离场组件须放在 Stage 子树内，Stage 支持嵌套收集，不要求组件直接挂在 Stage 下。奖励类型需要现有关卡／奖励运行上下文；界面类需要场景中存在对应 UI，基地已经提供。

## 一次性使用与外观

`UseMode` 提供 `Default`／`Once`／`Repeatable`。推荐保持 `Default`，继承类型原有的一次性或重复使用语义；覆盖前应确认业务允许重复领取或打开。

模型及其交互反馈由组件按 `Kind` 自动建立，制作者无需接线。只读 `ModelRoot` 用于代码检查生成结果，不是需要在 Inspector 分配的模型入口。

正式基地与出征场景的全部交互物统一迁移为新组件、新类型模型，不再使用旧交互模型。迁移会隐藏旧模型，正式场景已移除旧实例引用；这不代表全项目删除旧模型资产，历史证据保留原状。

## 挑战状态与失败反馈

奖励节点仍是先领取收益、再按 `SpawnsEncounter` 开始挑战。挑战期间显示挑战者粒子，右上角显示本场预算与波次；预算用完不代表挑战完成。

挑战中对范围内的类型组件按 F 会被拒绝：出现红色波罩、弹开玩家并播放失败音效，目标不消耗、不执行成功业务。反馈由组件自动接入，制作者无需另挂反馈脚本；在 Inspector 调整：

| 参数 | 默认值 | 用途 |
|---|---:|---|
| `FailureKnockback` | 6 | 失败时弹开强度 |
| `FailureWaveRadius` | 2.6 | 红色波罩半径 |
| `FailureWaveSeconds` | 0.55秒 | 红色波罩持续时间 |

组件将这些参数传给自动反馈层。仅本场 Encounter 真正结束并清完其计入的敌人，才退出挑战状态、播放完成绿波并显示完成提示；游离怪不参与本场清场账。预算耗尽、暂停、死亡或 Reset 均不能作为挑战完成来触发成功反馈。

## 示例与排查

可用 F6 运行 [InteractionComponentLab](../Scenes/Debug/InteractionComponentLab.tscn)：基地加 Area 机关、Area 帮助和半径符文入口。查看其层级与参数即可复用配置；最新验证证据见 J48。

没有交互提示时，先检查 `Kind`、配置警告、宿主重复、Area 的 Mask／Shape，以及是否仍处于挑战中；能交互但没有界面时，检查对应 UI 是否已挂入场景。完整收益顺序与首领离场规则见 [关卡流程](stages.md)。
