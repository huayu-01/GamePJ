using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Config;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Interaction;

public enum InteractionKind
{
	None, Enhancement, Evolution, Mutation, Resource, Heal, Special, Shop,
	BossDoor, Exit, Expedition, AbilityCards, Loadout, Runes, Help, Character, Signal,
}

public enum InteractionUseMode { Default, Once, Repeatable }

/// <summary>挂在场景物体下的交互配置；编辑器选类型，运行时复用一个现有交互后端。</summary>
[Tool]
[GlobalClass]
public partial class InteractionComponent : Node3D
{
	[Signal] public delegate void TriggeredEventHandler();
	private static readonly Dictionary<Node3D, InteractionComponent> ActiveHosts = new();
	private Node3D? _registeredHost;
	private InteractionKind _kind;
	[ExportGroup("交互行为")]
	[Export] public InteractionKind Kind
	{
		get => _kind;
		set
		{
			_kind = value;
			if (Engine.IsEditorHint() && IsInsideTree()) UpdateConfigurationWarnings();
		}
	}
	[Export] public InteractionDefinition? Definition { get; set; }
	private InteractionDefinition? ResolvedDefinition => Definition ?? InteractionDefinitionCatalog.For(Kind);
	[Export] public string Prompt { get; set; } = string.Empty;
	[Export] public InteractionUseMode UseMode { get; set; }
	[ExportGroup("触发范围")]
	[Export] public Area3D? TriggerArea { get; set; }
	[Export(PropertyHint.Range, "0.1,100,0.1,or_greater")] public float InteractRadius { get; set; } = 3.2f;
	[ExportGroup("类型参数")]
	[Export] public bool SpawnsEncounter { get; set; } = true;
	[Export] public CharacterConfig? Character { get; set; }
	[Export(PropertyHint.File, "*.tscn")] public string TargetScene { get; set; } = "res://Scenes/Expedition.tscn";
	[Export] public ExpeditionDestination? Destination { get; set; }
	[ExportGroup("宿主与表现（可选）")]
	[Export] public Node3D? HostRoot { get; set; }
	[ExportGroup("交互失败反馈")]
	[Export(PropertyHint.Range, "0,30,0.1,or_greater")] public float FailureKnockback { get; set; } = 6;
	[Export(PropertyHint.Range, "0.1,20,0.1,or_greater")] public float FailureWaveRadius { get; set; } = 2.6f;
	[Export(PropertyHint.Range, "0.1,3,0.05,or_greater")] public float FailureWaveSeconds { get; set; } = .55f;

	public Interactable? Backend { get; private set; }
	public Node3D? ModelRoot { get; private set; }
	private readonly Dictionary<GeometryInstance3D, (bool Visible, float Transparency)> _replacedGeometry = new();
	private InteractionKind _previewKind;
	private CharacterConfig? _previewCharacter;
	private InteractionDefinition? _previewDefinition;
	private PackedScene? _previewModel;
	private InteractionKind _previewStyle;
	private bool? _lastVisualState;

	private Node3D? ResolveHost() => HostRoot ?? (GetParent() is Node3D parent && parent is not StageController ? parent : this);
	private Area3D? ResolveArea(Node3D host)
	{
		if (TriggerArea != null) return TriggerArea;
		if (GetParent() is Area3D parentArea) return parentArea;
		if (host is Area3D hostArea) return hostArea;
		Area3D[] areas = host.GetChildren().OfType<Area3D>().ToArray();
		return areas.Length == 1 ? areas[0] : null;
	}
	private bool HasEarlierComponent(Node3D host)
	{
		if (host is Interactable) return true;
		if (!Engine.IsEditorHint())
			return ActiveHosts.TryGetValue(host, out InteractionComponent? active) && active != this && IsInstanceValid(active) && active.IsInsideTree();
		return ComponentsUnder(host).Any(c => c != this && (c.Definition != null || c.Kind != InteractionKind.None) && c.ResolveHost() == host);
	}
	private static IEnumerable<InteractionComponent> ComponentsUnder(Node root)
	{
		if (root is InteractionComponent component) yield return component;
		foreach (Node child in root.GetChildren())
			foreach (InteractionComponent nested in ComponentsUnder(child)) yield return nested;
	}

	public override string[] _GetConfigurationWarnings()
	{
		var warnings = new List<string>();
		if (Definition != null) warnings.AddRange(Definition.Validate());
		if (Definition == null && Kind == InteractionKind.None) warnings.Add("请选择交互类型；None 不会注册交互目标。");
		if (Definition == null && Kind == InteractionKind.Character && Character == null) warnings.Add("角色展台需要指定 Character 配置。");
		Node3D? host = ResolveHost();
		if (host != null)
		{
			if (host != this && !host.IsAncestorOf(this)) warnings.Add("HostRoot 必须是当前组件的祖先节点。");
			if (HasEarlierComponent(host)) warnings.Add("一个宿主只使用一个交互组件；不要挂到已有 Interactable 下面。");
			if (TriggerArea == null && GetParent() is not Area3D && host is not Area3D && host.GetChildren().OfType<Area3D>().Count() > 1)
				warnings.Add("宿主有多个 Area3D，请明确指定 TriggerArea。");
			if (ResolveArea(host) is { } area)
			{
				if (!area.Monitoring) warnings.Add("TriggerArea 必须开启 Monitoring 才能检测玩家。");
				if ((area.CollisionMask & Combat.CombatLayers.BodyLayerOf(Combat.Faction.Player)) == 0) warnings.Add("TriggerArea 的 CollisionMask 需要覆盖玩家身体（UnitBody 或 Player 层）。");
				if (!area.GetChildren().OfType<CollisionShape3D>().Any(s => !s.Disabled && s.Shape != null)) warnings.Add("TriggerArea 需要启用的 CollisionShape3D 和 Shape。");
			}
			else if (InteractRadius <= 0) warnings.Add("没有触发器时，InteractRadius 必须大于0。");
		}
		return warnings.ToArray();
	}
	public override void _Ready()
	{
		if (Engine.IsEditorHint()) { RefreshPreview(); return; }
		Node3D host = ResolveHost()!;
		if ((Definition == null && Kind == InteractionKind.None) || (Definition == null && Kind == InteractionKind.Character && Character == null)
			|| HasEarlierComponent(host) || (host != this && !host.IsAncestorOf(this))) return;
		if (TriggerArea == null && GetParent() is not Area3D && host is not Area3D && host.GetChildren().OfType<Area3D>().Count() > 1) return;
		Backend = ResolvedDefinition?.CreateBackend(this);
		if (Backend == null) return;
		ActiveHosts[host] = this; _registeredHost = host;
		Backend.Name = "Interaction";
		Backend.InteractRadius = InteractRadius;
		Backend.TriggerArea = ResolveArea(host);
		Backend.PlacementTarget = host;
		Backend.OwnsTypeModel = true;
		HideLegacyGeometry(host);
		ModelRoot = ResolvedDefinition!.CreateModel(Character);
		Backend.AddChild(ModelRoot);
		Backend.Succeeded += OnSucceeded;
		Backend.ResetPerformed += OnReset;
		AddChild(Backend);
		InteractablePresentation feedback = InteractablePresentation.Ensure(Backend);
		feedback.FailKnockback = FailureKnockback;
		feedback.FailureWaveRadius = FailureWaveRadius;
		feedback.FailureWaveSeconds = FailureWaveSeconds;
		if (UseMode != InteractionUseMode.Default) Backend.SingleUse = UseMode == InteractionUseMode.Once;
		if (!string.IsNullOrWhiteSpace(Prompt)) Backend.Prompt = Prompt;
	}
	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint()) { RefreshPreview(); return; }
		if (Backend == null || !IsInstanceValid(Backend)) return;
		bool visible = Backend.Enabled && Backend.Visible && !Backend.Vanished && !Backend.Used;
		if (_lastVisualState == visible) return;
		_lastVisualState = visible;
		if (ModelRoot != null && IsInstanceValid(ModelRoot)) ModelRoot.Visible = visible;
	}
	private void RefreshPreview()
	{
		var definition = ResolvedDefinition;
		if (_previewKind == Kind && _previewCharacter == Character && _previewDefinition == definition && _previewModel == definition?.ModelScene && _previewStyle == (definition?.VisualStyle ?? InteractionKind.None) && (definition == null || ModelRoot != null)) return;
		_previewDefinition = definition; _previewModel = definition?.ModelScene; _previewStyle = definition?.VisualStyle ?? InteractionKind.None;
		_previewKind = Kind; _previewCharacter = Character;
		if (ModelRoot != null && IsInstanceValid(ModelRoot)) { RemoveChild(ModelRoot); ModelRoot.QueueFree(); }
		ModelRoot = null;
		RestoreLegacyGeometry();
		Node3D host = ResolveHost()!;
		if (definition == null || (Definition == null && Kind == InteractionKind.Character && Character == null)
			|| (host != this && !host.IsAncestorOf(this))) return;
		HideLegacyGeometry(host);
		ModelRoot = ResolvedDefinition!.CreateModel(Character, preview: true);
		AddChild(ModelRoot);
	}
	private void HideLegacyGeometry(Node root)
	{
		// 只替换当前宿主的旧外观，保留触发器、碰撞、说明标签及独立交互子物体。
		if (root is Interactable || (root is InteractionComponent component && component != this)) return;
		if (root != ResolveHost() && root.GetChildren().OfType<InteractionComponent>().Any(c => c != this)) return;
		if (root is GeometryInstance3D geometry && root is not Label3D && root is not Sprite3D)
		{
			_replacedGeometry.TryAdd(geometry, (geometry.Visible, geometry.Transparency));
			// Mesh本身作为宿主时不能隐藏父树，否则新模型也会一起消失。
			if (geometry.IsAncestorOf(this)) geometry.Transparency = 1f;
			else geometry.Visible = false;
		}
		foreach (Node child in root.GetChildren()) HideLegacyGeometry(child);
	}
	private void RestoreLegacyGeometry()
	{
		foreach (var entry in _replacedGeometry)
			if (IsInstanceValid(entry.Key)) { entry.Key.Visible = entry.Value.Visible; entry.Key.Transparency = entry.Value.Transparency; }
		_replacedGeometry.Clear();
	}
	private void OnSucceeded()
	{
		if (Backend is { Used: true } && ModelRoot != null && IsInstanceValid(ModelRoot)) ModelRoot.Visible = false;
		EmitSignal(SignalName.Triggered);
	}
	private void OnReset()
	{
		_lastVisualState = null;
		if (ModelRoot != null && IsInstanceValid(ModelRoot)) ModelRoot.Visible = true;
	}
	public override void _ExitTree()
	{
		RestoreLegacyGeometry();
		if (_registeredHost != null && ActiveHosts.TryGetValue(_registeredHost, out InteractionComponent? active) && active == this)
			ActiveHosts.Remove(_registeredHost);
		_registeredHost = null;
		if (Backend == null || !IsInstanceValid(Backend)) return;
		Backend.Succeeded -= OnSucceeded;
		Backend.ResetPerformed -= OnReset;
	}
}
