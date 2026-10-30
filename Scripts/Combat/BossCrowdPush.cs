using System.Collections.Generic;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>首领冲锋扫过的同阵营推挤；不借敌对攻击盒、不造成伤害，每轮冲锋每目标一次。</summary>
public static class BossCrowdPush
{
	public static void Sweep(Unit owner, Vector3 from, Vector3 to, Vector3 forward, HashSet<ulong> pushed)
	{
		var settings = EnemyCrowdConfig.Current;
		if (!settings.BossPushEnabled || !owner.HasTag("Boss") || !owner.IsInsideTree()) return;
		var body = MapManager.BodySize(owner);
		Vector3 a = from, b = to; a.Y = b.Y = 0; Vector3 segment = b - a;
		float lengthSquared = segment.LengthSquared(); forward.Y = 0;
		if (forward.IsZeroApprox()) return; forward = forward.Normalized();
		foreach (Node node in owner.GetTree().GetNodesInGroup("enemy_units"))
		{
			if (node is not Unit target || target == owner || target.IsDead || target.IsQueuedForDeletion() || target.Faction != owner.Faction
				|| target.Movement == null || target.HasTag("Boss") || pushed.Contains(target.GetInstanceId())) continue;
			Vector3 position = target.GlobalPosition, flat = position; flat.Y = 0;
			float t = lengthSquared < .0001f ? 0 : Mathf.Clamp((flat - a).Dot(segment) / lengthSquared, 0, 1);
			Vector3 nearest = a + segment * t, away = flat - nearest;
			if (away.LengthSquared() > (body.Radius + 3f) * (body.Radius + 3f)) continue;
			var other = MapManager.BodySize(target);
			float radius = body.Radius + other.Radius + .08f;
			if (away.LengthSquared() > radius * radius || position.Y > Mathf.Max(from.Y, to.Y) + body.Height
				|| position.Y + other.Height < Mathf.Min(from.Y, to.Y)) continue;
			Vector3 side = away.LengthSquared() > .001f ? away.Normalized() : forward.Cross(Vector3.Up) * (target.GetInstanceId() % 2 == 0 ? 1 : -1);
			Vector3 impulse = (forward + side * settings.BossPushSideWeight).Normalized() * Mathf.Max(0, settings.BossPushSpeed);
			if (target.Movement.ReceiveCrowdImpulse(impulse)) pushed.Add(target.GetInstanceId());
		}
	}
}
