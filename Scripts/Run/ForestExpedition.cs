using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Interaction;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Run;

/// <summary>正式森林的入场校验；配装、奖励与战斗继续由原有流程负责。</summary>
public partial class ForestExpedition : Node3D
{
	private SceneTreeTimer? _preparationTimer;
	private readonly Dictionary<Interactable, Vector3> _anchors = new();
	private CanvasLayer? _preparation;
	private Unit _player = null!;
	private Marker3D _start = null!;
	private int _attempts;
	public bool EntryReady { get; private set; }

	public override void _Ready()
	{
		_player = GetNode<Unit>("Player");
		_start = GetNode<Marker3D>("ExpeditionForest/NatureArena/CombatMarkers/PlayerStart");
		_start.AddToGroup("run_spawn");
		foreach (Interactable anchor in Interactable.InSubtree(GetNode<StageController>("Stage")))
			if (anchor is RewardNode or BossDoor)
				_anchors.Add(anchor, anchor.GlobalPosition);

		// MapManager 的烘焙计时器始终运行；道路就绪前不让正式流程计时或接受互动。
		ProcessMode = ProcessModeEnum.Disabled;
		_preparation = new CanvasLayer { Name = "EntryPreparation" };
		AddChild(_preparation);
		_preparation.AddChild(new Label { Text = "正在准备出征…", Position = new Vector2(24, 24) });
		SchedulePreparation();
	}

	private void SchedulePreparation()
	{
		_preparationTimer = GetTree().CreateTimer(0.1, processAlways: true);
		_preparationTimer.Timeout += PrepareEntry;
	}

	private void PrepareEntry()
	{
		_preparationTimer = null;
		if (!IsInsideTree()) return;
		_attempts++;
		if (MapManager.Instance is not { IsReady: true } map
			|| !map.TryValidPoint(_start.GlobalPosition, _start.GlobalPosition, out Vector3 start, _player))
		{
			SchedulePreparation();
			return;
		}

		var positions = new List<Vector3>();
		foreach (var entry in _anchors)
		{
			bool found = map.TryValidPoint(entry.Value, start, out Vector3 point, _player);
			if (!found || TooClose(point, positions))
			{
				found = false;
				for (int attempt = 0; attempt < 12; attempt++)
				{
					if (!map.TryRingPoint(entry.Value, 1f, 8f, out point, _player, connectedTo: start)
						|| TooClose(point, positions)) continue;
					found = true;
					break;
				}
			}
			if (!found)
			{
				if (_attempts % 50 == 0) GameLog.Write("Map", $"森林入场等待有效节点位置：{entry.Key.Name}");
				SchedulePreparation();
				return;
			}
			// 组合式交互移动整个宿主，避免触发范围和模型留在旧位置。
			entry.Key.MoveTo(point);
			positions.Add(point);
		}

		_player.GlobalPosition = start;
		_start.GlobalPosition = start;
		_player.ResetPhysicsInterpolation();
		EntryReady = true;
		_preparation?.QueueFree();
		_preparation = null;
		ProcessMode = ProcessModeEnum.Inherit;
		int rewards = 0, portals = 0, doors = 0;
		foreach (Interactable anchor in _anchors.Keys)
		{
			if (anchor is BossDoor) doors++;
			else if (anchor is RewardNode { Kind: RewardNodeKind.Portal }) portals++;
			else rewards++;
		}
		GameLog.Write("Map", $"正式森林入场：120米环境，{rewards}个奖励节点、{portals}个预置离场节点与{doors}道首领门通过导航／连通性／净空检查；使用正式玩家及配装流程");
	}

	private static bool TooClose(Vector3 point, List<Vector3> chosen)
	{
		float spacing = ConfigIndex.Settings.MinNodeSpacing;
		foreach (Vector3 existing in chosen)
			if (existing.DistanceTo(point) < spacing) return true;
		return false;
	}

	public override void _ExitTree()
	{
		if (_preparationTimer != null && IsInstanceValid(_preparationTimer))
			_preparationTimer.Timeout -= PrepareEntry;
		_preparationTimer = null;
	}
}
