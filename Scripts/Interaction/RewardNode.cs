using System;
using System.Collections.Generic;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Interaction;

/// <summary>节点类型（策划文档 §6.2；⚠️ 没有「武器池」这个类型 —— 那个叫法指的就是**进化池**，用户 2026-10-12 拍板 A3）。</summary>
public enum RewardNodeKind
{
	Weapon,
	Enhancement,
	Evolution,
	Mutation,
	Resource,
	Heal,
	Special,

	/// <summary>商店：花局外碎片买局内收益。</summary>
	Shop,

	/// <summary>降临节点（Boss 死后出现）：纯关卡逻辑，不给奖励也不刷怪。</summary>
	Portal,
}

/// <summary>
/// 地面上的奖励节点。玩家走近按 F → 领取对应类型的奖励 → 该节点消耗掉 → 触发一场 Encounter。
/// 生成后固定、只能用一次（策划文档 §6.2）。
///
/// 外观三条约定：
/// ① **形状**按 Kind 换网格（`mesh_node_&lt;kind&gt;.tres`，9 种类型 9 颗不同形状的宝石）——
///    用户 2026-10 纠正过：「我的期望是使用**不同形状**的模型而不是**一个模型去换色**」；
/// ② **颜色**按 Kind 上色走 **Surface Material Override**（见 Palette）—— 一局 10 个节点共用同一份网格 / 材质，
///    直接改共享材质会把它们一起改掉；
/// ③ 脚下那圈**周身光圈**的颜色 = **价值程度**，从价值标签配置表读（见 ApplyAuraColor）——
///    用户原话：「加入一些周身特效来做信息展示的，比如光圈，光圈颜色来表示价值程度」。
/// </summary>
public partial class RewardNode : Interactable
{
	/// <summary>头顶名牌的节点名（`RewardNode.tscn` 里的 Label3D）。</summary>
	private const string LabelNodeName = "Label";

	/// <summary>周身光圈的节点名（`RewardNode.tscn` 里的 `Aura`）。</summary>
	private const string AuraNodeName = "Aura";

	/// <summary>光圈三个环的子节点名：色带 / 暗描边 / 外发光。</summary>
	private const string AuraRingName = "Ring";
	private const string AuraRimName = "Rim";
	private const string AuraGlowName = "Glow";

	/// <summary>
	/// 每种类型一份**形状**：`Assets/Models/nodes/mesh_node_<枚举名小写>.tres`
	/// （12 颗宝石里挑 9 颗，挑法与逐颗实测量见 docs/art-onboarding.md §三.1.3）。
	/// 与 <see cref="Palette"/> 同一条做法：**文件名由枚举名自动映射**，加新类型只要按规则放一份网格。
	/// </summary>
	private static readonly Dictionary<RewardNodeKind, Mesh> Shapes = BuildShapes();

	/// <summary>
	/// 每种类型一份材质：`Assets/Materials/mat_node_<枚举名小写>.tres`。
	/// 场景里网格自带的 `mat_node_<kind>.tres` 是它的兜底，这里按实例再覆盖一次
	/// （"共享材质从不被修改"这条约束靠 Surface Override 实现）。
	/// </summary>
	private static readonly Dictionary<RewardNodeKind, Material> Palette = BuildPalette();

	/// <summary>
	/// 每种类型的**光圈颜色**（按需从价值标签配置读、读一次缓存一份，见 <see cref="AuraColorFor"/>）。
	/// **刻意不做成静态字段初始化**：它要读 `ConfigIndex` 的配置索引，那必须等 autoload 就绪（首次 `_Ready`）。
	/// </summary>
	private static readonly Dictionary<RewardNodeKind, Color> AuraColors = new();

	/// <summary>一个价值标签都没命中时的中性色（**不是**"配好的颜色"，只是别让光圈整个消失）。</summary>
	private static readonly Color DefaultAuraColor = new(0.85f, 0.9f, 1.0f);

	/// <summary>光圈外圈那条**暗描边**的颜色（准星那套"亮色 + 黑描边"：亮色负责暗背景，描边负责白地板）。</summary>
	private static readonly Color AuraRimColor = new(0.04f, 0.04f, 0.05f);

	/// <summary>已经为哪个类型报过"读不到光圈颜色"（只为不刷日志）。</summary>
	private static readonly HashSet<RewardNodeKind> AuraColorWarned = new();

	/// <summary>按颜色缓存的共享光圈材质（一档一份，绝不逐节点新建；状态动画只动**每实例**的 transparency）。</summary>
	private static readonly Dictionary<(Color Color, float Brightness, float Alpha), StandardMaterial3D> AuraMaterials = new();

	/// <summary>节点类型。改它会即时换形状 / 换色 / 刷新光圈颜色与头顶名牌。</summary>
	[Export]
	public RewardNodeKind Kind
	{
		get => _kind;
		set
		{
			_kind = value;
			ApplyIdleVisual();
			OnNameChanged();
		}
	}

	/// <summary>这个节点在使用后是否触发一场 Encounter（降临节点 / 纯奖励节点设为 false）。</summary>
	[Export] public bool SpawnsEncounter { get; set; } = true;

	private RewardNodeKind _kind = RewardNodeKind.Enhancement;
	/// <summary>表现层共用的价值色，仍以价值标签表为权威。</summary>
	public Color PresentationColor => AuraColorFor(_kind);

	public override void _Ready()
	{
		base._Ready();
		OnNameChanged();
		Appear();
	}

	/// <summary>
	/// 按类型换**形状** + 上**颜色** + 定**光圈颜色**。
	/// 放在这里（而不是只放在 <see cref="Kind"/> 的 setter 里）是因为它必须覆盖**所有**进入路径：
	/// `_Ready` 的 `base._Ready()`、`Appear()`（切关复位）、以及运行时改 `Kind`。
	/// </summary>
	protected override void ApplyIdleVisual()
	{
		if (VisualMesh is { } mesh)
		{
			// ① 形状（用户：不同形状，不是一个模型换色）
			if (Shapes.TryGetValue(_kind, out Mesh? shape))
			{
				mesh.Mesh = shape;

				// 描边（`InteractablePresentation`）在建的时候**抄了一份**当时的网格做反向外壳，
				// 所以换形状要把它一起换掉 —— 否则"高亮时的描边"还是上一颗宝石的形状。
				if (GetNodeOrNull<InteractablePresentation>(InteractablePresentation.NodeName)
					is { Outline: { } outline })
				{
					outline.Mesh = shape;
				}
			}

			// ② 颜色（Surface Material Override，见 docs/assets.md 的「依赖方向」一节）
			if (Palette.TryGetValue(_kind, out Material? material))
			{
				mesh.SetSurfaceOverrideMaterial(0, material);
			}
		}

		// ③ 周身光圈的颜色 = 价值程度。
		// ⚠️ 必须**再延后执行一次**：颜色要从 `ConfigIndex` 的**表索引**读，而那张索引是
		// `ConfigIndex.CallDeferred(BuildTableIndex)` 建的 —— Main.tscn 里节点的 `_Ready` **早于**它
		// （自测实测踩到：那时索引还是空的 → 颜色被"读不到"的结果定住，整局光圈都是中性白）。
		// 延后队列先进先出，而 `ConfigIndex` 的 defer 排在整个主场景之前，所以这次执行时索引一定已就绪。
		// 先立刻执行一次，是为了"索引本来就已就绪"的常见情形（切关复位 / 运行期改 Kind）不慢一帧。
		ApplyAuraColor();
		CallDeferred(nameof(ApplyAuraColor));
	}

	/// <summary>
	/// 周身光圈（用户需求：「加入一些周身特效来做信息展示的，比如光圈，**光圈颜色来表示价值程度**」）。
	///
	/// <list type="bullet">
	/// <item>颜色**从价值标签配置表读**（`ValueTagConfig.AuraColor`），代码里一个档位色都没写死 ——
	/// 取值链与 `NodePlacement.WavesFor` 一致：节点行的 `ValueTags` → 标签行，多个标签取**波数最大**的一个；</item>
	/// <item>三个环各自的材质是**按颜色缓存的共享资源**，这里只写**每实例**的 Surface Override
	/// （与按类型上色同一条做法：动共享材质会把同档位的其它节点一起改掉）；</item>
	/// <item>只加表现：判定 / 范围 / 消耗 / 复位一行没动（见 <see cref="Interactable"/>）。</item>
	/// </list>
	/// </summary>
	private void ApplyAuraColor()
	{
		if (GetNodeOrNull<Node3D>(AuraNodeName) is not { } aura)
		{
			return;
		}

		Color color = AuraColorFor(_kind);

		// 色带：Unshaded + albedo 亮度 > 2（HDR）→ 亮地板上比地板更亮、暗天空下也是亮的，而且带一点光晕。
		aura.GetNodeOrNull<MeshInstance3D>(AuraRingName)?.SetSurfaceOverrideMaterial(0, AuraMaterial(color, 2.2f, 1f));
		// 暗描边：只负责"在白地板上画出边界"，所以压得很暗（不参与光照，颜色稳定）。
		aura.GetNodeOrNull<MeshInstance3D>(AuraRimName)?.SetSurfaceOverrideMaterial(0, AuraMaterial(AuraRimColor, 0.1f, 1f));
		// 外发光：半透明的一大圈，让"周身"这件事读得出来（不发光、不抢色带的色相）。
		aura.GetNodeOrNull<MeshInstance3D>(AuraGlowName)?.SetSurfaceOverrideMaterial(0, AuraMaterial(color, 1.5f, 0.32f));
	}

	/// <summary>
	/// 这个类型的价值档位色 = 节点行命中的价值标签里 <see cref="ValueTagConfig.WaveCount"/> 最大的那一行的
	/// <see cref="ValueTagConfig.AuraColor"/>（与"这个节点值几波"是同一条规则、同一个真源）。
	///
	/// <para><b>读不到时不缓存</b>：首次调用可能早于 `ConfigIndex` 建好表索引（见 <see cref="ApplyAuraColor"/>
	/// 上面的说明），把"读不到"的结果缓存下来会让整局都是中性白 —— 那是自测实测踩到的真 bug。
	/// 只有**真的读到**才写进 <see cref="AuraColors"/>。</para>
	///
	/// <para>节点表里**没有**这个类型的行（例如 `Kind = Weapon`：A3 之后它已并入进化池，只是枚举还留着）
	/// 不算脏数据，不写日志；有行却解析不出标签才是要报的，会给一次 warning + 一行日志（不静默）。</para>
	/// </summary>
	private static Color AuraColorFor(RewardNodeKind kind)
	{
		if (AuraColors.TryGetValue(kind, out Color cached))
		{
			return cached;
		}

		NodeConfig? row = ConfigRows.ByKind<NodeConfig, RewardNodeKind>(kind);
		Color resolved = DefaultAuraColor;
		string bestTag = string.Empty;
		int bestWaves = -1;

		foreach (string tag in row?.ValueTags ?? Array.Empty<string>())
		{
			if (ConfigRows.ById<ValueTagConfig>(tag) is { } tagRow && tagRow.WaveCount > bestWaves)
			{
				bestWaves = tagRow.WaveCount;
				bestTag = tag;
				resolved = tagRow.AuraColor;
			}
		}

		if (bestWaves < 0)
		{
			if (row != null && AuraColorWarned.Add(kind))
			{
				GameLog.Write("Node",
					$"⚠ 奖励节点类型 {kind} 的光圈颜色读不到（节点行 {row.Id} 的 ValueTags 没命中任何标签行），暂用中性白");
			}

			return DefaultAuraColor;
		}

		AuraColors[kind] = resolved;
		GameLog.Write("Node",
			$"奖励节点光圈颜色：{kind} → 价值标签「{bestTag}」（{bestWaves} 波）的 AuraColor {resolved}（来源：表 ValueTags）");
		return resolved;
	}

	/// <summary>
	/// 光圈材质：**Unshaded**（不参与光照 → 颜色在亮地板与暗天空下一致，这是"都看得清"的前提）+
	/// 可选 HDR 亮度（> 2 就会吃到 `TestArena` 那盏 glow 阈值 2.0 的光晕）。
	/// 按 (颜色, 亮度, 不透明度) 缓存，全项目一档一份。
	/// </summary>
	private static StandardMaterial3D AuraMaterial(Color color, float brightness, float alpha)
	{
		var key = (color, brightness, alpha);
		if (AuraMaterials.TryGetValue(key, out StandardMaterial3D? cached))
		{
			return cached;
		}

		var material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(color.R * brightness, color.G * brightness, color.B * brightness, alpha),
			Transparency = alpha < 1f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
			// 环带是**贴身平放**的，单面剔除会在低角度看出一条缺边 → 两面都画。
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			DisableReceiveShadows = true,
		};

		AuraMaterials[key] = material;
		return material;
	}

	/// <summary>
	/// 头顶名牌的文字 = 奖励分派表里的类型名（"强化池" / "进化池" …）。
	/// RunController 拿同一张表当界面标题，所以改名字只需要改 `RewardService.TitleFor` 一处。
	/// 降临节点不在那张表里（它没有奖励池），用关卡给的提示文字。
	/// </summary>
	protected override void OnNameChanged()
	{
		if (GetNodeOrNull<Label3D>(LabelNodeName) is not { } label)
		{
			return;
		}

		label.Text = _kind == RewardNodeKind.Portal
			? (string.IsNullOrEmpty(Prompt) ? "降临节点" : Prompt)
			: RewardService.TitleFor(_kind);
	}

	protected override bool OnInteract() => StageController.Instance?.RequestRewardNode(this) ?? false;

	/// <summary>
	/// 载入 9 份类型材质。少一份就退回网格上的默认色，并在日志里说一声 ——
	/// 这里的失败是"颜色不对"而不是报错，不写日志就会变成难查的静默失效。
	/// </summary>
	private static Dictionary<RewardNodeKind, Material> BuildPalette()
	{
		var palette = new Dictionary<RewardNodeKind, Material>();
		foreach (RewardNodeKind kind in Enum.GetValues<RewardNodeKind>())
		{
			string path = $"res://Assets/Materials/mat_node_{kind.ToString().ToLowerInvariant()}.tres";
			if (ResourceLoader.Exists(path) && GD.Load<Material>(path) is { } material)
			{
				palette[kind] = material;
			}
			else
			{
				GameLog.Write("Node", $"奖励节点类型 {kind} 缺少材质 {path}，退回默认蓝");
			}
		}

		return palette;
	}

	/// <summary>
	/// 载入 9 份类型**形状**（`Assets/Models/nodes/mesh_node_&lt;kind&gt;.tres`）—— 与 <see cref="BuildPalette"/>
	/// 同一条"文件名由枚举名自动映射"的规则。少一份就退回场景自带的那颗网格（**不变成空壳**），并写日志。
	///
	/// ⚠️ 每个 `.tres` 都是**从 FBX 抽出来的独立 ArrayMesh**，不是 `gemN.fbx::ArrayMesh_xxxxx` 那种子资源路径
	/// （后缀是导入器现生成的，重新导入就会变 → 引用静默失效、节点变成看不见的空壳）。抽取工具：
	/// `Scenes/Interactables/_ExtractGem.tscn`。
	/// </summary>
	private static Dictionary<RewardNodeKind, Mesh> BuildShapes()
	{
		var shapes = new Dictionary<RewardNodeKind, Mesh>();
		foreach (RewardNodeKind kind in Enum.GetValues<RewardNodeKind>())
		{
			string path = $"res://Assets/Models/nodes/mesh_node_{kind.ToString().ToLowerInvariant()}.tres";
			if (ResourceLoader.Exists(path) && GD.Load<Mesh>(path) is { } mesh)
			{
				shapes[kind] = mesh;
			}
			else
			{
				GameLog.Write("Node", $"奖励节点类型 {kind} 缺少形状 {path}，退回场景自带网格");
			}
		}

		return shapes;
	}
}
