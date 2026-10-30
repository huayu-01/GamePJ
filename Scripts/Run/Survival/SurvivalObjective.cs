using ActRoguelike.Core;
using ActRoguelike.Config;
using ActRoguelike.Interaction;
using Godot;

namespace ActRoguelike.Run;

/// <summary>信标、遗物和出口只请求模式服务，使用既有F互动与单次消费契约。</summary>
public partial class SurvivalObjective : Interactable
{
	public enum ObjectiveKind { Beacon, Relic, Exit }
	public SurvivalDirector Director { get; set; } = null!;
	public ObjectiveKind Kind { get; set; }
	public int Index { get; set; }
	public bool Completed { get; private set; }
	public float CaptureProgress { get; private set; }
	public bool Channeling { get; private set; }
	public string ProgressText => _progress.Text;
	public bool ProgressVisible => _progress.Visible;
	private Label3D _progress = null!;
	private StandardMaterial3D _material = null!;
	public override void _Ready()
	{
		SingleUse = Kind != ObjectiveKind.Beacon;
		Prompt = Kind switch {
			ObjectiveKind.Beacon => string.Format(ContentText.Get("ui.survival.beacon_prompt", "净化信标 {0} · 启动后在圈内停留{1:0.#}秒；恢复15%生命并选增幅；净化全部3座可削弱首领"), Index + 1, Director.Definition!.BeaconCaptureSeconds),
			ObjectiveKind.Relic => ContentText.Get("ui.survival.relic_prompt", "强敌遗物 · 选择1项增幅（需要交互，不会自动拾取）"),
			_ => "撤离 · 完成试炼返回基地" };
		Color color = Kind == ObjectiveKind.Relic ? new Color(1, .67f, .15f) : Kind == ObjectiveKind.Exit ? new Color(1, .82f, .35f) : new Color(.12f, .85f, .7f);
		_material = new StandardMaterial3D { AlbedoColor = color, EmissionEnabled = true, Emission = color, EmissionEnergyMultiplier = 1.1f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
		var mesh = new MeshInstance3D { Name = "Mesh", Mesh = Kind == ObjectiveKind.Exit ? new TorusMesh { InnerRadius = 1.4f, OuterRadius = 1.7f, Rings = 32, RingSegments = 8, Material = _material } : new CylinderMesh { TopRadius = .18f, BottomRadius = .55f, Height = 1.8f, RadialSegments = 8, Material = _material }, Position = Vector3.Up * 1.3f };
		if (Kind == ObjectiveKind.Exit) mesh.RotationDegrees = new Vector3(90, 0, 0);
		AddChild(mesh);
		if (Kind == ObjectiveKind.Beacon)
		{
			float radius = Director.Definition!.BeaconRadius;
			AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = radius - .09f, OuterRadius = radius, Rings = 48, RingSegments = 4, Material = _material }, Position = Vector3.Up * .06f, Scale = new Vector3(1, .08f, 1), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
		}
		_progress = new Label3D { Position = Vector3.Up * 3.3f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Font = RuntimeResources.Load<Font>("res://Assets/UI/Themes/ui_font.tres"), FontSize = 40, PixelSize = .005f };
		AddChild(_progress); base._Ready();
	}
	protected override bool OnInteract()
	{
		if (Kind == ObjectiveKind.Exit) return Director.Extract();
		if (Kind == ObjectiveKind.Relic) return Director.ClaimRelic(this);
		if (Channeling || Completed || Director.State != SurvivalDirector.PhaseState.Surviving) return false;
		Channeling = true; Enabled = false; _progress.Visible = true;
		Prompt = ContentText.Get("ui.survival.beacon_channel", "信标净化中 · 保持在圈内；离圈会倒退进度"); return true;
	}
	public override void _PhysicsProcess(double delta)
	{
		// 保留共用范围检测与目标描边；输入选择器依赖此缓存找到可交互物。
		base._PhysicsProcess(delta);
		if (!Channeling || Completed || Director.State != SurvivalDirector.PhaseState.Surviving || Director.Player is not { IsDead: false } player) return;
		float radius = Director.Definition!.BeaconRadius;
		bool inside = new Vector2(player.GlobalPosition.X - GlobalPosition.X, player.GlobalPosition.Z - GlobalPosition.Z).Length() <= radius;
		CaptureProgress = Mathf.Clamp(CaptureProgress + (float)delta * (inside ? 1 : -.5f), 0, Director.Definition.BeaconCaptureSeconds);
		float ratio = CaptureProgress / Director.Definition.BeaconCaptureSeconds;
		_progress.Text = string.Format(ContentText.Get("ui.survival.beacon_progress", "净化 {0:0}%{1}"), ratio * 100,
			inside ? "" : ContentText.Get("ui.survival.beacon_return", " · 返回圈内"));
		_material.EmissionEnergyMultiplier = 1 + ratio * 2;
		if (ratio >= 1 && Director.CompleteBeacon(this)) { Completed = true; Enabled = false; _progress.Text = "净化完成"; Prompt = "信标已净化"; }
	}
}
