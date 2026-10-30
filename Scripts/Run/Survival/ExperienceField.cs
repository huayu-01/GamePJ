using System;
using System.Collections.Generic;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Run;

/// <summary>集中绘制经验，达到上限时合并数值，不为每颗经验建立物理体。</summary>
public partial class ExperienceField : Node3D
{
	private sealed class Mote { public Vector3 Position; public int Value; }
	private readonly List<Mote> _motes = new();
	private MultiMesh _mesh = null!;
	private MultiMesh _trails = null!;
	public Unit? Player { get; set; }
	public int Capacity { get; set; } = 384;
	public float PickupRadius { get; set; } = 4;
	public float PickupSpeedScale { get; set; } = 1;
	public int Count => _motes.Count;
	public int StoredExperience { get { int total = 0; foreach (var mote in _motes) total += mote.Value; return total; } }
	public event Action<int>? Collected;
	public override void _Ready()
	{
		var material = new StandardMaterial3D { AlbedoColor = new Color(.15f, 1, .8f), EmissionEnabled = true, Emission = new Color(.1f, .9f, .6f), EmissionEnergyMultiplier = 2, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
		_mesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new SphereMesh { Radius = .15f, Height = .3f, RadialSegments = 8, Rings = 4, Material = material }, InstanceCount = Capacity, VisibleInstanceCount = 0 };
		AddChild(new MultiMeshInstance3D { Multimesh = _mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
		_trails = new MultiMesh { TransformFormat=MultiMesh.TransformFormatEnum.Transform3D, Mesh=new BoxMesh { Size=new Vector3(.025f,.025f,.4f), Material=material }, InstanceCount=Capacity, VisibleInstanceCount=0 };
		AddChild(new MultiMeshInstance3D { Multimesh=_trails, CastShadow=GeometryInstance3D.ShadowCastingSetting.Off });
	}
	public void Drop(Vector3 position, int value)
	{
		if (value <= 0) return;
		if (_motes.Count >= Capacity)
		{
			Mote nearest = _motes[0]; float distance = float.MaxValue;
			foreach (var mote in _motes) { float next = mote.Position.DistanceSquaredTo(position); if (next < distance) { nearest = mote; distance = next; } }
			nearest.Value += value; return;
		}
		_motes.Add(new Mote { Position = position + Vector3.Up * .35f, Value = value });
	}
	public void Clear() { _motes.Clear(); if (_mesh != null) _mesh.VisibleInstanceCount = 0; if (_trails != null) _trails.VisibleInstanceCount=0; }
	public override void _PhysicsProcess(double delta)
	{
		if (Player == null || !IsInstanceValid(Player) || Player.IsDead) return;
		Vector3 target = Player.GlobalPosition + Vector3.Up * .6f;
		for (int i = _motes.Count - 1; i >= 0; i--)
		{
			var mote = _motes[i]; float distance = mote.Position.DistanceTo(target);
			if (distance <= PickupRadius) mote.Position = mote.Position.MoveToward(target, (float)delta * PickupSpeedScale * (9 + 3 * (PickupRadius - distance)));
			if (mote.Position.DistanceSquaredTo(target) < .45f * .45f)
			{
				_motes.RemoveAt(i); Collected?.Invoke(mote.Value);
				if (_motes.Count == 0 || IsQueuedForDeletion()) break;
				i = Mathf.Min(i, _motes.Count);
			}
		}
		_mesh.VisibleInstanceCount = _motes.Count;
		int trails=0;
		for (int i = 0; i < _motes.Count; i++)
		{
			_mesh.SetInstanceTransform(i, new Transform3D(Basis.Identity, ToLocal(_motes[i].Position)));
			Vector3 direction=target-_motes[i].Position;
			if(PickupSpeedScale>1 && direction.LengthSquared()>.01f && direction.LengthSquared()<PickupRadius*PickupRadius)
				_trails.SetInstanceTransform(trails++,new Transform3D(Basis.LookingAt(direction,Mathf.Abs(direction.Normalized().Dot(Vector3.Up))>.99f?Vector3.Right:Vector3.Up),ToLocal(_motes[i].Position)));
		}
		_trails.VisibleInstanceCount=trails;
	}
}
