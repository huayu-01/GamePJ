using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Encounter;
using ActRoguelike.Interaction;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>交互组件真实输入、物理触发器、宿主移动与嵌套关卡专项；不调用收益或写存档。</summary>
public partial class InteractionComponentProbe : Node3D
{
	private int _pass, _fail;
	private Unit _player = null!;
	private PlayerInteractInput _input = null!;
	private readonly List<Node> _fixtures = new();
	private void Check(bool ok, string label)
	{
		if (ok) _pass++; else _fail++;
		GD.Print($"[InteractionComponent] {(ok ? "PASS" : "FAIL")} {label}");
	}
	private async Task Frames(int count = 12)
	{
		for (int frame = 0; frame < count; frame++)
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}
	private static void KeyF(bool pressed, bool echo = false)
		=> Input.ParseInputEvent(new InputEventKey { Keycode = Key.F, PhysicalKeycode = Key.F, Pressed = pressed, Echo = echo });
	private async Task TapF()
	{
		KeyF(true); await Frames(3); KeyF(false); await Frames(3);
	}
	private void MovePlayer(Vector3 point)
	{
		_player.GlobalPosition = point;
		_player.Velocity = Vector3.Zero;
		_player.ResetPhysicsInterpolation();
	}
	private Node3D Host(string name, Vector3? position = null)
		=> new() { Name = name, Position = position ?? Vector3.Zero };
	private void Mount(Node fixture)
	{
		_fixtures.Add(fixture);
		AddChild(fixture);
	}
	private async Task ClearFixtures()
	{
		KeyF(false);
		foreach (Node fixture in _fixtures)
			if (IsInstanceValid(fixture)) fixture.QueueFree();
		_fixtures.Clear();
		await Frames(3);
		Check(Interactable.All.Count == 0, "释放宿主后后端退出公共交互列表");
	}
	private static Area3D Area(string name = "Trigger")
	{
		var area = new Area3D
		{
			Name = name, Position = Vector3.Up, CollisionLayer = 0,
			CollisionMask = CombatLayers.UnitBody, Monitoring = true,
		};
		area.AddChild(new CollisionShape3D { Name = "Shape", Shape = new BoxShape3D { Size = new Vector3(20, 3, 6) } });
		return area;
	}
	private static InteractionComponent Signal(InteractionUseMode use = InteractionUseMode.Repeatable)
		=> new() { Name = "InteractionConfig", Kind = InteractionKind.Signal, UseMode = use, InteractRadius = 3.2f, Prompt = "探针交互" };

	public override async void _Ready()
	{
		try
		{
			await Frames(3);
			var ground = new StaticBody3D { Name = "Ground", Position = Vector3.Down * .5f, CollisionLayer = CombatLayers.World, CollisionMask = 0 };
			ground.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(240, 1, 240) } });
			AddChild(ground);
			_player = new Unit { Name = "Player", Faction = Faction.Player, Position = new Vector3(70, .05f, 0) };
			_player.AddChild(new CollisionShape3D { Position = Vector3.Up * .9f, Shape = new CapsuleShape3D { Radius = .35f, Height = 1.8f } });
			_input = new PlayerInteractInput { Name = "InteractInput" };
			_player.AddChild(_input); AddChild(_player);
			AddChild(new Camera3D { Position = new Vector3(0, 10, 18), RotationDegrees = new Vector3(-25, 0, 0), Current = true });
			PlayerLocator.Invalidate();
			await Frames(3);
			Check(ReferenceEquals(PlayerLocator.Player, _player), "使用真实Unit及玩家公共定位服务");
			await Serialization();
			await BackendKinds();
			await RadiusAndPhysicalInput();
			await OnceAndReset();
			await TriggerAndMovement();
			await ParentAreaAndInvalidConfiguration();
			await DuplicateHosts();
			await ReplacementAppearance();
			await NestedStage();
			await PlacedExitLifecycle();
		}
		catch (Exception exception) { Check(false, exception.ToString()); }
		finally
		{
			KeyF(false);
			GetTree().Paused = false;
			PlayerLocator.Invalidate();
		}
		GD.Print($"[InteractionComponent] === 交互组件验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}

	private async Task Serialization()
	{
		var host = Host("SerializedProp");
		var area = Area(); host.AddChild(area); area.Owner = host;
		area.GetChild(0).Owner = host;
		var mesh = new MeshInstance3D { Name = "ImportedModel", Mesh = new BoxMesh() }; host.AddChild(mesh); mesh.Owner = host;
		var component = Signal();
		component.InteractRadius = 6.75f; component.Prompt = "保存后的提示";
		component.TriggerArea = area; component.HostRoot = host;
		component.TargetScene = "res://Scenes/Expedition.tscn";
		host.AddChild(component); component.Owner = host;
		var kind = component.GetPropertyList().FirstOrDefault(row => row["name"].AsString() == "Kind");
		Check(kind != null && kind["hint"].AsInt32() == (int)PropertyHint.Enum
			&& Enum.GetNames<InteractionKind>().All(name => kind["hint_string"].AsString().Contains(name))
			&& (kind["usage"].AsInt64() & (long)PropertyUsageFlags.Editor) != 0
			&& (kind["usage"].AsInt64() & (long)PropertyUsageFlags.Storage) != 0,
			"Inspector导出Kind枚举，具备编辑和存储标志");
		var scene = new PackedScene();
		Check(scene.Pack(host) == Error.Ok, "组件及Area／宿主引用能打包为内存PackedScene");
		Node3D copy = scene.Instantiate<Node3D>();
		var restored = copy.GetNode<InteractionComponent>("InteractionConfig");
		Check(restored.Kind == InteractionKind.Signal && restored.UseMode == InteractionUseMode.Repeatable
			&& Mathf.IsEqualApprox(restored.InteractRadius, 6.75f) && restored.Prompt == "保存后的提示"
			&& restored.TargetScene == "res://Scenes/Expedition.tscn", "场景往返保留类型、复用模式、半径、提示和目标场景");
		Check(ReferenceEquals(restored.TriggerArea, copy.GetNode<Area3D>("Trigger"))
			&& ReferenceEquals(restored.HostRoot, copy), "序列化的Area与宿主引用指向新实例，未残留原对象");
		host.Free(); Mount(copy); await Frames();
		Check(restored.Backend is SignalInteraction && ReferenceEquals(restored.Backend.TriggerArea, restored.TriggerArea), "场景实例就绪后创建正确后端并安装真实Area");
		Check(restored.GetPropertyList().All(row => row["name"].AsString() is not ("HighlightMesh" or "HideOnUse"))
			&& typeof(InteractionComponent).GetProperty("ModelRoot")?.SetMethod?.IsPublic != true,
			"新版Inspector不再导出旧模型／隐藏目标，ModelRoot为只读结果");
		Check(restored.ModelRoot != null && !copy.GetNode<MeshInstance3D>("ImportedModel").Visible,
			"序列化场景就绪后生成新模型并隐藏携带的旧模型");
		await ClearFixtures();
	}

	private async Task BackendKinds()
	{
		var cases = new (InteractionKind Kind, Type Backend)[]
		{
			(InteractionKind.Enhancement, typeof(RewardNode)), (InteractionKind.Evolution, typeof(RewardNode)),
			(InteractionKind.Mutation, typeof(RewardNode)), (InteractionKind.Resource, typeof(RewardNode)),
			(InteractionKind.Heal, typeof(RewardNode)), (InteractionKind.Special, typeof(RewardNode)),
			(InteractionKind.Shop, typeof(RewardNode)), (InteractionKind.Exit, typeof(RewardNode)),
			(InteractionKind.BossDoor, typeof(BossDoor)), (InteractionKind.Expedition, typeof(BaseTerminal)),
			(InteractionKind.AbilityCards, typeof(BaseTerminal)), (InteractionKind.Loadout, typeof(BaseTerminal)),
			(InteractionKind.Help, typeof(BaseTerminal)), (InteractionKind.Runes, typeof(RuneTerminal)),
			(InteractionKind.Character, typeof(CharacterStand)), (InteractionKind.Signal, typeof(SignalInteraction)),
		};
		var shapes = new Dictionary<InteractionKind, string>();
		foreach (var item in cases)
		{
			// 只检验接线，不执行界面、配装、奖励或存档动作。
			var host = Host("Kind_" + item.Kind, new Vector3(90, 0, 0)); host.ProcessMode = ProcessModeEnum.Disabled;
			var component = new InteractionComponent { Kind = item.Kind, SpawnsEncounter = false };
			if (item.Kind == InteractionKind.Character) component.Character = new CharacterConfig { Id = "probe_character", DisplayName = "探针角色" };
			host.AddChild(component); Mount(host);
			Check(component.Backend?.GetType() == item.Backend, $"类型{item.Kind}连接{item.Backend.Name}");
			if (component.Backend is RewardNode reward) Check(!reward.SpawnsEncounter, $"{item.Kind}保留不生成战斗的配置");
			Check(component.ModelRoot != null && component.IsAncestorOf(component.ModelRoot)
				&& component.Backend?.VisualMesh?.Mesh != null
				&& (ReferenceEquals(component.ModelRoot, component.Backend.VisualMesh) || component.ModelRoot.IsAncestorOf(component.Backend.VisualMesh)),
				$"类型{item.Kind}自动生成归组件所有的新模型及主网格");
			shapes[item.Kind] = ModelShape(component.Backend?.VisualMesh);
		}
		Check(DifferentShapes(shapes, InteractionKind.Enhancement, InteractionKind.Evolution, InteractionKind.Mutation), "强化／进化／异化具有不同几何形状");
		Check(DifferentShapes(shapes, InteractionKind.Heal, InteractionKind.Resource, InteractionKind.Shop), "恢复／材料／商店具有不同几何形状");
		Check(DifferentShapes(shapes, InteractionKind.BossDoor, InteractionKind.Exit, InteractionKind.Expedition), "首领门／离场／出击的门体几何可区分");
		Check(DifferentShapes(shapes, InteractionKind.AbilityCards, InteractionKind.Loadout, InteractionKind.Runes, InteractionKind.Help), "能力卡／装备／符文／说明使用不同结构");
		Check(DifferentShapes(shapes, InteractionKind.Character, InteractionKind.Signal), "角色展台与信号机关具有不同结构");
		await ClearFixtures();
	}

	private static bool DifferentShapes(Dictionary<InteractionKind, string> shapes, params InteractionKind[] kinds)
		=> kinds.All(kind => shapes.TryGetValue(kind, out string? shape) && !string.IsNullOrEmpty(shape))
			&& kinds.Select(kind => shapes[kind]).Distinct().Count() == kinds.Length;

	private static string ModelShape(MeshInstance3D? visual)
	{
		if (visual?.Mesh is not { } mesh) return string.Empty;
		// 主模型由多个primitive合成ArrayMesh；比较实际三角形，不把颜色、材质或节点名称当形状。
		return $"{mesh.GetType().Name}:{mesh.GetAabb().Size}:" + string.Join("|", mesh.GetFaces().Select(vertex => vertex.ToString()));
	}

	private async Task RadiusAndPhysicalInput()
	{
		var host = Host("ArbitraryImportedProp"); var component = Signal();
		host.AddChild(new Node3D { Name = "VisualContainer" }); host.AddChild(component);
		int triggered = 0; component.Triggered += () => triggered++;
		Mount(host); MovePlayer(new Vector3(2, .05f, 0)); await Frames();
		Check(component.Backend is SignalInteraction && component.Backend.TriggerArea == null
			&& component.Backend.CanInteract && ReferenceEquals(PlayerInteractInput.Current, component.Backend), "任意Node3D子挂载后，半径Signal被真实输入组件选中");
		KeyF(true); await Frames(4);
		Check(triggered == 1 && Input.IsPhysicalKeyPressed(Key.F), "真实F按下只转发一次Triggered信号");
		await Frames(18); KeyF(true, echo: true); await Frames(4);
		Check(triggered == 1, "按住F及键盘重复事件不会重复触发");
		KeyF(false); await Frames(3); await TapF();
		Check(triggered == 2 && component.Backend is { Used: false }, "松开再按可再次使用Repeatable交互");
		MovePlayer(new Vector3(15, .05f, 0));
		Check(component.Backend != null && !component.Backend.Interact() && triggered == 2, "按下时重检半径，旧范围缓存不能跨距离触发");
		MovePlayer(new Vector3(2, .05f, 0)); await Frames(); host.Visible = false;
		Check(component.Backend != null && !component.Backend.Interact() && !component.Backend.CanInteract, "隐藏宿主立即拒绝交互，不等待范围缓存刷新");
		await ClearFixtures();
	}

	private async Task OnceAndReset()
	{
		var host = Host("OnceProp"); var component = Signal(InteractionUseMode.Once);
		host.AddChild(component); int triggered = 0; component.Triggered += () => triggered++;
		Mount(host); MovePlayer(new Vector3(1, .05f, 0)); await Frames();
		await TapF();
		Check(triggered == 1 && component.Backend is { Used: true, SingleUse: true } && component.ModelRoot is { Visible: false }, "Once真实交互后消耗后端并收起新生成模型");
		Check(component.Backend != null && !component.Backend.Interact() && triggered == 1, "一次性节点不能重复触发");
		component.Backend!.ResetNode(); await Frames(30);
		Check(component.ModelRoot is { Visible: true } && component.Backend is { Used: false, Vanished: false, CanInteract: true }, "ResetNode恢复新模型及交互状态");
		await TapF(); Check(triggered == 2, "复位后真实F可再次触发一次");
		await ClearFixtures();
	}

	private async Task TriggerAndMovement()
	{
		var host = Host("AreaProp"); var area = Area(); host.AddChild(area);
		var visual = new MeshInstance3D { Name = "ImportedMesh", Position = new Vector3(1, 1, 0), Mesh = new BoxMesh() }; host.AddChild(visual);
		var mount = new Node3D { Name = "Mount", Position = new Vector3(1, 0, 0) }; host.AddChild(mount);
		var component = Signal(); component.HostRoot = host; component.TriggerArea = area;
		mount.AddChild(component); int triggered = 0; component.Triggered += () => triggered++;
		Mount(host); MovePlayer(new Vector3(6, .05f, 0)); await Frames();
		Check(area.OverlapsBody(_player) && component.Backend is { CanInteract: true }
			&& _player.GlobalPosition.DistanceTo(component.Backend.GlobalPosition) > component.InteractRadius,
			"真实Area包含半径外玩家，范围判定采用物理重叠");
		Check(ReferenceEquals(PlayerInteractInput.Current, component.Backend), "PlayerInteractInput不再以旧3.2米半径二次裁掉Area目标");
		await TapF(); Check(triggered == 1, "旧半径之外的真实F仍能触发Area交互");
		MovePlayer(new Vector3(13, .05f, 0)); await Frames(3);
		Check(!area.OverlapsBody(_player) && component.Backend != null && !component.Backend.Interact() && triggered == 1, "离开Area后直接Interact仍拒绝");
		MovePlayer(new Vector3(6, .05f, 0)); await Frames(); host.Visible = false;
		Check(area.OverlapsBody(_player) && component.Backend != null && !component.Backend.Interact(), "仍在Area时隐藏宿主也立即拒绝");
		host.Visible = true; area.Monitoring = false; await Frames(3);
		Check(component.Backend != null && !component.Backend.Interact(), "关闭真实Area的Monitoring失败关闭，不回退半径");
		// 先等负向用例触发的外观抖动结束，避免把表现补间误判成MoveTo相对位移错误。
		area.Monitoring = true; await Frames(24);
		Vector3 hostBefore = host.GlobalPosition, areaBefore = area.GlobalPosition, visualBefore = visual.GlobalPosition;
		Vector3 modelBefore = component.ModelRoot!.GlobalPosition;
		Vector3 backendBefore = component.Backend!.GlobalPosition, destination = new(30, .05f, 0);
		Vector3 delta = destination - backendBefore;
		component.Backend.MoveTo(destination); await Frames(3);
		Check(ReferenceEquals(component.Backend.PlacementTarget, host)
			&& component.Backend.GlobalPosition.DistanceTo(destination) < .01f
			&& host.GlobalPosition.DistanceTo(hostBefore + delta) < .01f
			&& area.GlobalPosition.DistanceTo(areaBefore + delta) < .01f
			&& visual.GlobalPosition.DistanceTo(visualBefore + delta) < .01f, "MoveTo移动整个指定祖先宿主，触发器、挂点与外观保持相对位置");
		Check(component.ModelRoot.GlobalPosition.DistanceTo(modelBefore + delta) < .01f && !visual.Visible,
			"新模型随宿主MoveTo整体移动，旧模型保持隐藏");
		Check(!area.OverlapsBody(_player) && !component.Backend.Interact(), "宿主移走后旧位置不会继续触发");
		MovePlayer(area.GlobalPosition + new Vector3(6, -.95f, 0)); await Frames();
		await TapF(); Check(area.OverlapsBody(_player) && triggered == 2, "移动后触发器在新位置继续接受真实F");
		await ClearFixtures();
	}

	private async Task ParentAreaAndInvalidConfiguration()
	{
		var host = Host("AutoAreaHost"); var area = Area(); host.AddChild(area);
		var component = Signal(); host.AddChild(component); Mount(host);
		Check(component.Backend != null && ReferenceEquals(component.Backend.TriggerArea, area), "未显式指定Area时识别宿主唯一直接Area");
		var parentArea = Area("ParentArea"); var child = Signal(); parentArea.AddChild(child); Mount(parentArea);
		Check(child.Backend != null && ReferenceEquals(child.Backend.TriggerArea, parentArea), "直接挂到Area下时采用父Area");
		await ClearFixtures();
		int before = Interactable.All.Count;
		var noneHost = Host("NoneHost"); var none = new InteractionComponent { Kind = InteractionKind.None }; noneHost.AddChild(none); Mount(noneHost);
		var missingHost = Host("MissingCharacterHost"); var missing = new InteractionComponent { Kind = InteractionKind.Character }; missingHost.AddChild(missing); Mount(missingHost);
		var ambiguousHost = Host("AmbiguousAreas"); ambiguousHost.AddChild(Area("FirstArea")); ambiguousHost.AddChild(Area("SecondArea"));
		var ambiguous = Signal(); ambiguousHost.AddChild(ambiguous); Mount(ambiguousHost);
		Check(none.Backend == null && none._GetConfigurationWarnings().Length > 0, "None不生成后端，编辑器能说明原因");
		Check(missing.Backend == null && missing._GetConfigurationWarnings().Length > 0, "角色缺Character失败关闭并给配置提示");
		Check(ambiguous.Backend == null && ambiguous._GetConfigurationWarnings().Length > 0, "多个Area未指定时失败关闭，不擅自选取或退回半径");
		Check(Interactable.All.Count == before, "无效配置不污染公共交互注册列表");
		await ClearFixtures();
	}

	private async Task DuplicateHosts()
	{
		var host = Host("DuplicateSiblings"); var first = Signal(); var second = Signal(); second.Name = "SecondConfig";
		host.AddChild(first); host.AddChild(second); Mount(host);
		Check(new[] { first, second }.Count(item => item.Backend != null) == 1
			&& Interactable.InSubtree(host).Count() == 1, "同宿主的重复兄弟组件只注册一个有效后端");
		await ClearFixtures();
		var shared = Host("SharedAncestorHost");
		var components = new List<InteractionComponent>();
		for (int index = 0; index < 2; index++)
		{
			var branch = new Node3D { Name = "Mount" + index }; shared.AddChild(branch);
			var component = Signal(); component.HostRoot = shared; branch.AddChild(component); components.Add(component);
		}
		Mount(shared);
		Check(components.Count(item => item.Backend != null) == 1 && Interactable.InSubtree(shared).Count() == 1,
			"不同挂点显式共享HostRoot时也只生成一个有效后端");
		await ClearFixtures();
	}

	private async Task ReplacementAppearance()
	{
		var host = Host("CustomRewardModel", new Vector3(25, 0, 0));
		var material = new StandardMaterial3D { AlbedoColor = new Color(.7f, .25f, .13f) };
		var shape = new BoxMesh { Size = new Vector3(2, 1, 3), Material = material };
		var mesh = new MeshInstance3D { Name = "ExternalModel", Mesh = shape }; host.AddChild(mesh);
		var component = new InteractionComponent { Kind = InteractionKind.Enhancement, SpawnsEncounter = false };
		host.AddChild(component); Mount(host); await Frames();
		Check(component.Backend is RewardNode && component.ModelRoot != null && component.Backend.VisualMesh?.Mesh != null
			&& !ReferenceEquals(component.Backend.VisualMesh, mesh) && !ReferenceEquals(component.Backend.VisualMesh.Mesh, shape) && !mesh.Visible,
			"奖励组件隐藏旧模型并自动生成全新的类型主模型");
		var presentation = component.Backend!.GetNode<InteractablePresentation>(InteractablePresentation.NodeName);
		Check(presentation.Outline != null && ReferenceEquals(presentation.Outline.GetParent(), component.Backend.VisualMesh)
			&& ReferenceEquals(presentation.Outline.Mesh, component.Backend.VisualMesh!.Mesh), "高亮描边只附着新模型主网格，未引用旧模型");
		string generatedShape = ModelShape(component.Backend.VisualMesh);
		component.Backend.ResetNode(); await Frames(30);
		Check(!mesh.Visible && component.ModelRoot is { Visible: true } && ModelShape(component.Backend.VisualMesh) == generatedShape,
			"Reset恢复新模型及其类型造型，旧模型不会重新出现");
		component.Backend.Enabled = false; await Frames(3);
		Check(component.ModelRoot is { Visible: false } && !mesh.Visible, "Enabled关闭会收起新模型，旧模型继续隐藏");
		component.Backend.Enabled = true; component.Backend.Visible = false; await Frames(3);
		Check(component.ModelRoot is { Visible: false } && !mesh.Visible, "Visible关闭会收起新模型，旧模型继续隐藏");
		component.Backend.ResetNode(); await Frames(30); component.Backend.Disappear(); await Frames(36);
		Check(component.Backend.Vanished && component.ModelRoot is { Visible: false } && !mesh.Visible, "Vanished后新旧模型都不可见");
		component.Backend.ResetNode(); await Frames(30);
		Check(component.ModelRoot is { Visible: true } && !mesh.Visible && ModelShape(component.Backend.VisualMesh) == generatedShape,
			"消失后Reset只恢复当前类型新模型，不复活历史外观");
		await ClearFixtures();
	}

	private async Task NestedStage()
	{
		MovePlayer(new Vector3(70, .05f, 0));
		var stage = new StageController { Name = "NestedStage", ProcessMode = ProcessModeEnum.Disabled };
		var collection = new Node3D { Name = "ImportedProps" }; stage.AddChild(collection);
		for (int index = 0; index < 10; index++)
		{
			var host = Host("Prop" + index, new Vector3((index % 5) * 12 - 24, 0, (index / 5) * 24 + 24));
			var wrapper = new Node3D { Name = "Mount" }; host.AddChild(wrapper);
			wrapper.AddChild(new InteractionComponent
			{
				Kind = index < 2 ? InteractionKind.Evolution : index < 7 ? InteractionKind.Enhancement : InteractionKind.Heal,
				HostRoot = host,
			});
			collection.AddChild(host);
		}
		stage.AddChild(new EncounterController { Name = "Combat", AutoStart = false });
		stage.AddChild(new EncounterController { Name = "BossEncounter", AutoStart = false, BossScene = RuntimeResources.Load<PackedScene>("res://Scenes/Enemies/Boss.tscn") });
		Mount(stage); await Frames(3);
		RewardNode[] nodes = Interactable.InSubtree(stage).OfType<RewardNode>().ToArray();
		Check(nodes.Length == 10 && stage.RemainingNodes == 10, "Stage实际收集多层宿主中的10个组件后端");
		Check(nodes.Count(node => node.Enabled && NodePlacement.LayerOf(node.Kind) == 2) == 2
			&& nodes.Count(node => node.Enabled && NodePlacement.LayerOf(node.Kind) == 1) == 5
			&& nodes.Count(node => node.Enabled && NodePlacement.LayerOf(node.Kind) == 0) == 3, "嵌套组件按高2／中5／低3启用");
		if (nodes.Length > 0)
		{
			nodes[0].Enabled = false;
			Check(stage.RemainingNodes == 9, "关卡剩余数读取嵌套后端的真实启用状态");
			stage.StartRun();
			Check(stage.RemainingNodes == 10, "关卡重新启用能操作同一批嵌套后端");
		}
		await ClearFixtures();
	}

	private async Task PlacedExitLifecycle()
	{
		MovePlayer(new Vector3(70, .05f, 0));
		var stage = new StageController
		{
			Name = "PlacedExitStage", ProcessMode = ProcessModeEnum.Disabled,
			PortalScene = RuntimeResources.Load<PackedScene>("res://Scenes/Interactables/RewardNode.tscn"),
		};
		var collection = new Node3D { Name = "SceneProps" }; stage.AddChild(collection);
		for (int index = 0; index < 10; index++)
		{
			var host = Host("RewardProp" + index, new Vector3((index % 5) * 12 - 24, 0, (index / 5) * 24 + 24));
			var mount = new Node3D { Name = "ImportedMount" }; host.AddChild(mount);
			mount.AddChild(new InteractionComponent
			{
				Kind = index < 2 ? InteractionKind.Evolution : index < 7 ? InteractionKind.Enhancement : InteractionKind.Heal,
				HostRoot = host,
			});
			collection.AddChild(host);
		}
		var doorHost = Host("NestedBossGate", new Vector3(15, 0, -20));
		var doorMesh = new MeshInstance3D { Name = "GateModel", Mesh = new BoxMesh { Size = new Vector3(4, 4, 1) } };
		doorHost.AddChild(doorMesh);
		var doorMount = new Node3D { Name = "GateMount" }; doorHost.AddChild(doorMount);
		var doorComponent = new InteractionComponent
		{
			Kind = InteractionKind.BossDoor, HostRoot = doorHost,
			// 关卡停用自动战斗，但新类型模型的显隐同步仍走真实组件轮询。
			ProcessMode = ProcessModeEnum.Always,
		};
		doorMount.AddChild(doorComponent); collection.AddChild(doorHost);
		var exitHost = Host("DesignedExit", new Vector3(-12, .05f, -20));
		var exitMaterial = new StandardMaterial3D { AlbedoColor = new Color(.2f, .8f, .7f) };
		var exitShape = new BoxMesh { Size = new Vector3(2, 3, 1), Material = exitMaterial };
		var exitMesh = new MeshInstance3D { Name = "ExitModel", Position = new Vector3(0, 1.5f, 0), Mesh = exitShape };
		exitHost.AddChild(exitMesh);
		var exitMount = new Node3D { Name = "ExitMount", Position = Vector3.Right * 2 }; exitHost.AddChild(exitMount);
		var exitComponent = new InteractionComponent
		{
			Kind = InteractionKind.Exit, HostRoot = exitHost,
			ProcessMode = ProcessModeEnum.Always,
		};
		exitMount.AddChild(exitComponent); collection.AddChild(exitHost);
		stage.AddChild(new EncounterController { Name = "Combat", AutoStart = false });
		stage.AddChild(new EncounterController { Name = "BossEncounter", AutoStart = false, BossScene = RuntimeResources.Load<PackedScene>("res://Scenes/Enemies/Boss.tscn") });
		int victories = 0, triggered = 0;
		stage.Victory += () => victories++;
		exitComponent.Triggered += () => triggered++;
		Mount(stage); await Frames();
		var portal = (RewardNode)exitComponent.Backend!;
		Node3D exitModel = exitComponent.ModelRoot!;
		ulong portalId = portal.GetInstanceId();
		Vector3 portalPosition = portal.GlobalPosition, hostPosition = exitHost.GlobalPosition, meshPosition = exitMesh.GlobalPosition;
		Check(stage.RemainingNodes == 10 && Interactable.InSubtree(stage).OfType<RewardNode>().Count() == 11,
			"预置Exit不占10个普通奖励节点配额");
		Check(doorComponent.Backend is BossDoor && ReferenceEquals(doorComponent.Backend.PlacementTarget, doorHost),
			"首领门可以位于容器与挂点下的嵌套宿主");
		Check(portal.Kind == RewardNodeKind.Portal && !portal.Enabled && !portal.Visible && !portal.SpawnsEncounter,
			"预置出口开局禁用且隐藏后端，不刷怪或提供普通奖励");
		Check(!exitMesh.Visible && !exitModel.Visible && !exitModel.IsVisibleInTree()
			&& portal.VisualMesh?.Mesh != null && !ReferenceEquals(portal.VisualMesh.Mesh, exitShape),
			"出口开局收起新类型模型，旧外观已隐藏且不会作为当前模型");
		MovePlayer(portalPosition + Vector3.Right); await Frames();
		Check(!portal.Interact() && victories == 0 && triggered == 0 && !stage.IsVictory,
			"首领未结束前，走近预置出口也不能离场");

		// 只控制首领结束边界，不启动战斗、不创建RunController、不执行任何收益路径。
		const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
		typeof(StageController).GetField("_bossCleared", privateInstance)!.SetValue(stage, true);
		typeof(StageController).GetField("_bossFight", privateInstance)!.SetValue(stage, true);
		typeof(StageController).GetMethod("SpawnPortal", privateInstance)!.Invoke(stage, new object[] { new Vector3(40, 0, 40) });
		await Frames(40);
		Check(stage.AwaitingExit && portal.Enabled && portal.Visible && !stage.IsVictory && portal.Prompt == stage.ExitPrompt,
			"首领结束后激活预置出口并采用当前离场提示");
		typeof(StageController).GetMethod("UpdateExitMarker", privateInstance)!.Invoke(stage, null);
		Node3D? exitMarker = stage.GetNodeOrNull<Node3D>("ExitMarker");
		Check(exitMarker != null && exitMarker.Visible
			&& ActRoguelike.Core.PlayerLocator.Player is { } markerPlayer
			&& exitMarker.GlobalPosition.DistanceTo(markerPlayer.GlobalPosition) < 1f,
			"首领结束后玩家脚下出现指向出口的角标");
		Check(ReferenceEquals(exitComponent.Backend, portal) && portal.GetInstanceId() == portalId
			&& Interactable.InSubtree(stage).OfType<RewardNode>().Count(node => node.Kind == RewardNodeKind.Portal) == 1,
			"激活原预置后端，没有因配置PortalScene再生成第二个出口");
		Check(portal.GlobalPosition.DistanceTo(portalPosition) < .01f && exitHost.GlobalPosition.DistanceTo(hostPosition) < .01f
			&& exitMesh.GlobalPosition.DistanceTo(meshPosition) < .01f, "出口保留设计位置，未搬到首领门或传入的死亡位置");
		Check(exitModel.Visible && exitModel.IsVisibleInTree() && !exitMesh.Visible
			&& portal.VisualMesh?.Mesh != null && !ReferenceEquals(portal.VisualMesh.Mesh, exitShape),
			"出口激活时只展示新类型模型，旧外观仍保持隐藏");
		Check(doorComponent.Backend is { Vanished: true } && doorComponent.ModelRoot is { Visible: false } && !doorMesh.Visible,
			"首领结束找到嵌套门并收起其后端与新模型，旧外观不复现");
		MovePlayer(portal.GlobalPosition + Vector3.Right); await Frames();
		Check(portal.CanInteract && ReferenceEquals(PlayerInteractInput.Current, portal) && !stage.ChallengerActive,
			"离场阶段真实玩家输入能选中同一个预置出口");
		await TapF();
		Check(victories == 1 && triggered == 1 && stage.IsVictory && portal.Used && RunController.Instance == null,
			"真实F通过预置出口触发一次Victory，无RunController或收益写入");
		await Frames(36); await TapF();
		Check(victories == 1 && triggered == 1 && !exitModel.Visible && !exitMesh.Visible, "已使用出口收起新模型，旧外观不复现，再按F不会重复离场");

		// 正式复位走ApplyStage→ResetStage；StartRun的旧兼容配额语义留给原75项验证。
		stage.ApplyStage(new StageDefinition { Id = "probe_reset", DisplayName = "复位验证" }, 1);
		await Frames();
		Check(IsInstanceValid(portal) && portal.IsInsideTree() && portal.GetInstanceId() == portalId
			&& ReferenceEquals(exitComponent.Backend, portal) && !portal.Used,
			"正式换关复位保留同一预置出口后端并清除一次性使用状态");
		Check(!portal.Enabled && !portal.Visible && !exitModel.Visible && !exitMesh.Visible && !stage.IsVictory && stage.RemainingNodes == 10
			&& Interactable.InSubtree(stage).OfType<RewardNode>().Count(node => node.Kind == RewardNodeKind.Portal) == 1
			&& exitHost.GlobalPosition.DistanceTo(hostPosition) < .01f,
			"复位后出口原位隐藏／禁用，普通节点仍为10个且没有出口副本");
		await ClearFixtures();
	}
}
