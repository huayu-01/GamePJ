using System;
using System.Collections.Generic;
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
/// 外观两条约定：
/// ① 按 Kind 上色走 **Surface Material Override**（见 Palette）—— 一局 10 个节点共用同一份网格 / 材质，
///    直接改共享材质会把它们一起改掉；
/// ② 头顶挂 Label3D 名牌，走近前就能认出是哪种池子。
/// </summary>
public partial class RewardNode : Interactable
{
	/// <summary>头顶名牌的节点名（`RewardNode.tscn` 里的 Label3D）。</summary>
	private const string LabelNodeName = "Label";

	/// <summary>
	/// 每种类型一份材质：`Assets/Materials/mat_node_<枚举名小写>.tres`。
	/// 场景里网格自带的 `mat_node_reward.tres` 是所有节点的默认色，这里只是按实例覆盖。
	/// </summary>
	private static readonly Dictionary<RewardNodeKind, Material> Palette = BuildPalette();

	/// <summary>节点类型。改它会即时换色并刷新头顶名牌。</summary>
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

	public override void _Ready()
	{
		base._Ready();
		OnNameChanged();
		Appear();
	}

	/// <summary>按类型上色（Surface Material Override，见 docs/assets.md 的「依赖方向」一节）。</summary>
	protected override void ApplyIdleVisual()
	{
		if (VisualMesh is { } mesh && Palette.TryGetValue(_kind, out Material? material))
		{
			mesh.SetSurfaceOverrideMaterial(0, material);
		}
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
}
