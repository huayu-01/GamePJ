using Godot;

namespace ActRoguelike.Interaction;

/// <summary>
/// 玩家交互输入：每帧找范围内最近的可交互物件，按交互键触发，并把提示交给 HUD。
/// 挂在玩家下，和 Ability/Weapon 输入分开，互不干扰。
/// </summary>
public partial class PlayerInteractInput : Node
{
	[Export] public Key InteractKey { get; set; } = Key.F;

	/// <summary>当前可交互提示（HUD 读；没有可交互物时为空字符串）。</summary>
	public static string Prompt { get; private set; } = string.Empty;

	private bool _held;
	private Node3D? _player;

	public override void _Ready() => _player = GetParent<Node3D>();

	public override void _Process(double delta)
	{
		if (_player == null)
		{
			Prompt = string.Empty;
			return;
		}

		Interactable? nearest = FindNearest();
		Prompt = nearest == null ? string.Empty : nearest.CurrentPrompt;

		// 按住不重复触发。
		bool held = Input.IsPhysicalKeyPressed(InteractKey);
		if (held && !_held && nearest != null)
		{
			nearest.Interact();
		}

		_held = held;
	}

	private Interactable? FindNearest()
	{
		Interactable? best = null;
		float bestDistance = float.MaxValue;

		foreach (Interactable item in Interactable.All)
		{
			if (item == null || !IsInstanceValid(item) || !item.CanInteract)
			{
				continue;
			}

			float distance = item.GlobalPosition.DistanceSquaredTo(_player!.GlobalPosition);
			if (distance <= item.InteractRadius * item.InteractRadius && distance < bestDistance)
			{
				bestDistance = distance;
				best = item;
			}
		}

		return best;
	}
}
