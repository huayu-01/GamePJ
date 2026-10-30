using Godot;

namespace ActRoguelike.UI;

/// <summary>四角选中标，坐标由HUD投影提供；不遮挡鼠标，也不参与锁定逻辑。</summary>
public partial class AimAssistMarker : Control
{
	public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; Size = new Vector2(40, 40); }
	public override void _Draw()
	{
		Color ink = new(1, .85f, .35f, .95f);
		foreach (var corner in new[] { new Vector2(0, 0), new Vector2(40, 0), new Vector2(0, 40), new Vector2(40, 40) })
		{
			Vector2 inward = new(corner.X == 0 ? 1 : -1, corner.Y == 0 ? 1 : -1);
			DrawLine(corner, corner + new Vector2(inward.X * 10, 0), new Color(0, 0, 0, .7f), 5, true);
			DrawLine(corner, corner + new Vector2(0, inward.Y * 10), new Color(0, 0, 0, .7f), 5, true);
			DrawLine(corner, corner + new Vector2(inward.X * 10, 0), ink, 2, true);
			DrawLine(corner, corner + new Vector2(0, inward.Y * 10), ink, 2, true);
		}
	}
}
