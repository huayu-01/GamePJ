using Godot;

namespace ActRoguelike.Visual;

/// <summary>动作给表现层的只读事实；显示组件不识别投掷、下落或未来的新能力类型。</summary>
public readonly record struct WeaponPresentationState(bool Hidden, bool AlignToWorldDirection, Vector3 WorldDirection);

public interface IWeaponPresentationSource
{
	bool TryReadWeaponPresentation(out WeaponPresentationState state);
}
