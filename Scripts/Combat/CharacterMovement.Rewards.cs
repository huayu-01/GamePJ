using System;
using Godot;

namespace ActRoguelike.Combat;

public partial class CharacterMovement
{
	private readonly System.Collections.Generic.List<TeleportLease> _teleportRewardSources = new();
	private (bool Enabled, float Height, float Hover) _teleportRewardBaseline;
	public IDisposable AcquireTeleportJump(float heightMultiplier = 0, float hoverSeconds = 0)
	{
		if (_teleportRewardSources.Count == 0)
		{
			_teleportRewardBaseline = (TeleportJumpEnabled, TeleportJumpHeightMultiplier, TeleportJumpHoverSeconds);
			if (!TeleportJumpEnabled) ConfigureTeleportJump(true);
		}
		var lease = new TeleportLease(this, heightMultiplier, hoverSeconds); _teleportRewardSources.Add(lease);
		TeleportJumpHeightMultiplier = heightMultiplier; TeleportJumpHoverSeconds = hoverSeconds;
		return lease;
	}
	private sealed class TeleportLease : IDisposable
	{
		private CharacterMovement? _movement;
		public readonly float Height, Hover;
		public TeleportLease(CharacterMovement movement, float height, float hover) { _movement = movement; Height = height; Hover = hover; }
		public void Dispose()
		{
			var movement = _movement; _movement = null;
			if (!IsInstanceValid(movement) || !movement!._teleportRewardSources.Remove(this)) return;
			if (movement._teleportRewardSources.Count == 0)
			{
				var baseline = movement._teleportRewardBaseline; movement.ConfigureTeleportJump(baseline.Enabled, baseline.Height, baseline.Hover);
			}
			else { var latest = movement._teleportRewardSources[^1]; movement.TeleportJumpHeightMultiplier = latest.Height; movement.TeleportJumpHoverSeconds = latest.Hover; }
		}
	}
}
