using ActRoguelike.Combat;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.AI;

/// <summary>地面路径跟随；只给Movement发送意图，不直接移动身体或用射线冒充路径。</summary>
public partial class UnitNavigation : Node
{
	[Export] public float RepathSeconds { get; set; } = 0.3f;
	[Export] public float WaypointTolerance { get; set; } = 0.3f;
	private Unit _unit = null!;
	private Vector3[] _path = System.Array.Empty<Vector3>();
	private Vector3 _target;
	private float _repathLeft;
	private int _index, _revision = -1;
	private MapManager? _map;
	public bool HasPath => _index < _path.Length;
	public override void _Ready() => _unit = GetParent<Unit>();
	public override void _PhysicsProcess(double delta) => _repathLeft -= (float)delta;
	public void Stop() { _path = System.Array.Empty<Vector3>(); _index = 0; _repathLeft = 0; _unit.Movement?.Stop(); }
	public bool MoveTo(Vector3 destination, float speed)
	{
		var map = MapManager.Instance;
		if (map == null || !map.IsReady || _unit.Movement == null || _unit.Movement.IsControlDisabled) { _unit.Movement?.Stop(); return false; }
		// 目标可以跳起，行走目标取其脚下的导航位置；实际攻击仍检查真实几何。
		if (!map.TrySnap(destination, out Vector3 ground)) { Stop(); return false; }
		if (_map != map || _revision != map.NavigationRevision || _repathLeft <= 0 || ground.DistanceTo(_target) > 1f)
		{
			_map = map; _revision = map.NavigationRevision; _target = ground; _repathLeft = RepathSeconds; _index = 0;
			if (!map.TryPath(_unit.GlobalPosition, ground, out _path, _unit)) { _unit.Movement.Stop(); return false; }
		}
		while (_index < _path.Length)
		{
			Vector3 offset = _path[_index] - _unit.GlobalPosition; offset.Y = 0;
			if (offset.Length() > WaypointTolerance) { _unit.Movement.SetIntent(offset.Normalized(), speed); return true; }
			_index++;
		}
		_unit.Movement.Stop(); return true;
	}
	public bool KeepRange(Vector3 center, float distance, float speed, bool strafe = false)
	{
		var map = MapManager.Instance; if (map == null) { Stop(); return false; }
		Vector3 away = _unit.GlobalPosition - center; away.Y = 0;
		away = away.LengthSquared() > 0.001f ? away.Normalized() : _unit.GlobalTransform.Basis.Z;
		if (strafe) away = away.Rotated(Vector3.Up, 0.35f);
		Vector3 desired = center + away * distance;
		if (map.TryValidPoint(desired, _unit.GlobalPosition, out Vector3 valid, _unit)) return MoveTo(valid, speed);
		if (map.TryRingPoint(center, Mathf.Max(1f, distance - 1f), distance + 1f, out valid, _unit, _unit.GlobalPosition)) return MoveTo(valid, speed);
		Stop(); return false;
	}
}
