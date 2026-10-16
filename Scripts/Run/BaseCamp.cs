using ActRoguelike.Combat;
using ActRoguelike.Core;
using ActRoguelike.Config;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Run;

/// <summary>基地练习生命周期；没有关卡、奖励池、局内构筑和结算。</summary>
public partial class BaseCamp : Node
{
	[Export] public float FallResetHeight { get; set; } = -15;
	[Export] public float DeathReloadDelay { get; set; } = 0.7f;
	[Export] public Vector3 SpawnPosition { get; set; } = new(0, 1.2f, 9);
	private Unit? _player;
	private double _reloadTime = -1;
	private bool _selectionRestored;
	public override void _Process(double delta)
	{
		if (!_selectionRestored)
		{
			if (ConfigRows.Count<CharacterConfig>() == 0) return;
			if (!RunSetup.HasSelection) LoadoutMemory.Restore();
			_selectionRestored = true;
		}
		_player ??= PlayerLocator.Player;
		if (_reloadTime >= 0)
		{
			_reloadTime -= delta;
			if (_reloadTime <= 0)
			{
				SetProcess(false);
				UiRouter.Instance?.ResetForSceneChange();
				GetTree().ReloadCurrentScene();
			}
			return;
		}
		if (_player == null || !IsInstanceValid(_player)) return;
		if (_player.IsDead) _reloadTime = DeathReloadDelay;
		else if (_player.GlobalPosition.Y < FallResetHeight)
		{
			_player.GlobalPosition = SpawnPosition;
			_player.Velocity = Vector3.Zero;
			_player.ResetPhysicsInterpolation();
		}
	}
}
