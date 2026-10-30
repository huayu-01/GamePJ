using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Camera;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Run;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>低血残影、输入驱动的长枪宽带/肩部镜头及单位边缘效果专项。</summary>
public partial class VisualReadabilityProbe : Node3D
{
	private int _passed, _failed;
	private Unit _player = null!;
	private CameraRig _rig = null!;
	private void Check(bool ok, string text) { if (ok) _passed++; else _failed++; GD.Print($"[VisualReadability] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	private async Task Wait(float seconds)
	{
		float elapsed = 0;
		while (elapsed < seconds) { await Frame(); elapsed += (float)GetProcessDeltaTime(); }
	}
	private void Press(bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = "weapon_skill_2", Pressed = pressed });

	public override async void _Ready()
	{
		try
		{
			await Wait(.2f); // 等autoload完成表索引，再安装真实长枪槽位。
			HealthBars();
			var ground = new StaticBody3D { CollisionLayer = CombatLayers.World, CollisionMask = 0, Position = Vector3.Down * .5f };
			ground.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(100, 1, 100) } });
			ground.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(100, 1, 100) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(.13f, .17f, .21f) } });
			AddChild(ground); AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-65, -30, 0) });
			RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.WeaponMaster; RunSetup.Weapon = WeaponPresetKind.Spear;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_rig = _player.GetNode<CameraRig>("CameraRig");
			new PlayerLoadout().Apply(_player, this, PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Spear);
			await Wait(.15f);
			if (!OS.GetCmdlineUserArgs().Contains("--rims-only")) await SpearPreview();
			await Rims();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		finally { Press(false); }
		GD.Print($"[VisualReadability] === 表现可读性验收结束：通过 {_passed}，失败 {_failed} ===");
		GameExit.Request(GetTree(), _failed == 0 ? 0 : 1);
	}

	private void HealthBars()
	{
		foreach (string scene in new[] { "BarEnemy", "BarPlayer" })
		{
			using Control root = RuntimeResources.Load<PackedScene>($"res://Scenes/UI/Bars/{scene}.tscn").Instantiate<Control>();
			var widget = HealthBarWidget.Bind(root);
			var fill = (Control)root.FindChild("Fill", true, false);
			var delay = (Control)root.FindChild("DelayBar", true, false);
			widget.SetRatio(.08f, Colors.Red, false);
			Check(!delay.Visible && Mathf.IsEqualApprox(fill.Size.X, widget.FillWidth * .08f), $"{scene}低血初始化不闪满条");
			widget.SetRatio(.06f, Colors.Red, true);
			Check(delay.Visible && Mathf.IsEqualApprox(delay.Size.X, widget.FillWidth * .08f), $"{scene}8%→6%只残留受击前8%，不会整条变红");
			widget.SetRatio(.06f, Colors.Red, true);
			Check(Mathf.IsEqualApprox(delay.Size.X, widget.FillWidth * .08f), $"{scene}零伤害不把延迟条放大");
			widget.AdvanceDelay(.06f, .6f, .1f);
			Check(!delay.Visible && Mathf.IsEqualApprox(delay.Size.X, fill.Size.X), $"{scene}小伤害残影按比例速度快速收敛");
			widget.SetRatio(.03f, Colors.Red, true);
			widget.SetRatio(0, Colors.Red, true);
			Check(delay.Size.X <= widget.FillWidth * .061f && Mathf.IsZeroApprox(fill.Size.X), $"{scene}连续致死伤害只显示剩余真实血量残影");
			widget.AdvanceDelay(0, .6f, .2f);
			Check(!delay.Visible && Mathf.IsZeroApprox(delay.Size.X), $"{scene}归零后不残留红条");
			widget.SetRatio(.6f, Colors.Green, false); widget.SetRatio(.4f, Colors.Yellow, true); widget.SetRatio(.8f, Colors.Green, false);
			Check(!delay.Visible && Mathf.IsEqualApprox(fill.Size.X, widget.FillWidth * .8f), $"{scene}治疗超过旧残影时立即清理");
			root.Free();
		}
	}

	private async Task SpearPreview()
	{
		var spear = (SpearThrowAbility)_player.AbilitySlots.Resolve("weapon_skill_2")!;
		var path = spear.GetNode<DashPathIndicator>("Path");
		Press(true); await Wait(.35f);
		Check(path.IsShowing && path.PointCount > 2, "真实右键按住展示完整弹道采样");
		var mesh = path.GetNode<MeshInstance3D>("Path").Mesh!;
		Vector3[] vertices = mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
		Check(path.Width >= .3f && vertices.Length == path.PointCount * 2, "路径使用每点左右成对的三角带，有真实宽度");
		Check(vertices.Chunk(2).All(pair => Mathf.Abs(pair[0].DistanceTo(pair[1]) - path.Width) < .002f), "整条路径每段都保持配置宽度");
		Check(((vertices[0] + vertices[1]) * .5f).DistanceTo(path.Points[0]) < .001f
			&& ((vertices[^2] + vertices[^1]) * .5f).DistanceTo(path.Points[^1]) < .001f, "宽带覆盖出手点到完整末点");
		Check(_rig.ShoulderPreviewActive && _rig.ShoulderOffset > .8f, "按住长枪槽位输入时进入轻微侧肩镜头");
		await Capture("combat_readability_spear", "长枪：真实宽带弹道、完整路径；按住右键时镜头轻微靠右肩");
		Press(false); await Wait(.25f);
		Check(!path.IsShowing && !_rig.ShoulderPreviewActive && Mathf.Abs(_rig.ShoulderOffset) < .01f, "松手后隐藏路径并恢复镜头，不等待武器回手");
		spear.ResetForLoadout(); await Wait(.05f);
		Check(spear.TryActivate(), "无输入的程序蓄力可启动"); spear.OnInputHeld(.2f); await Wait(.1f);
		Check(!path.IsShowing && !_rig.ShoulderPreviewActive, "程序蓄力不打开玩家路径和侧肩镜头");
		spear.ResetForLoadout(); Press(true); await Wait(.2f);
		Check(path.IsShowing, "换装清理前真实预览已开启");
		new PlayerLoadout().Apply(_player, this, PlayerCharacterKind.Hunter, WeaponPresetKind.Crossbow); await Wait(.25f);
		Check(!path.IsShowing && Mathf.Abs(_rig.ShoulderOffset) < .01f, "按住中换装完整清理旧路径与镜头偏移"); Press(false); await Wait(.05f);
	}

	private async Task Rims()
	{
		var unit = new Unit { Faction = Faction.Enemy, Position = new Vector3(0, .05f, -5), MaxHealth = 1000 };
		Material original = new StandardMaterial3D { AlbedoColor = new Color(.2f, .65f, .3f) };
		var model = new Node3D { Name = "Model" };
		var body = new MeshInstance3D { Name = "BodyMesh", Position = Vector3.Up, Mesh = new CapsuleMesh { Height = 2, Radius = .5f }, MaterialOverride = original };
		model.AddChild(body); unit.AddChild(model);
		unit.AddChild(new CollisionShape3D { Name = "Body", Position = Vector3.Up, Shape = new CapsuleShape3D { Height = 2, Radius = .5f } });
		unit.AddChild(new UnitAnimator { Name = "Animator" });
		var presentation = new UnitPresentation { Name = "Presentation", RemoveOnDeath = false }; unit.AddChild(presentation);
		var charge = new ChargeBodyFx { Name = "ChargeBodyFx" }; unit.AddChild(charge);
		var eliteModifier = new EliteModifier(); AddChild(unit); unit.AddChild(eliteModifier); await Wait(.1f);
		// 美术样式在入树后延迟绑定；保全判据应比较实际运行材质，而非绑定前的原始资源。
		original = body.MaterialOverride!;
		Material? elite = body.MaterialOverlay;
		Check(body.MaterialOverride == original && elite is ShaderMaterial, "精英保留模型原材质，仅叠加半透明边缘光");
		var armor = unit.GetNode<MeshInstance3D>("ArmorAura");
		Check(armor.Visible && armor.Mesh?.SurfaceGetMaterial(0) is ShaderMaterial, "护甲使用半透明菲涅尔外壳");
		charge.DebugPreviewChargeFx(1.2f, 1f); await Wait(.05f);
		Material? chargeMaterial = body.MaterialOverlay;
		Check(chargeMaterial == elite && body.MaterialOverride == original, "蓄力边缘光不抢精英材质且主体不变色");
		presentation.ShowImpact(ImpactKind.Flesh, .4f);
		Material? impact = body.MaterialOverlay;
		await Wait(.04f);
		Check(impact is ShaderMaterial && body.MaterialOverlay == impact && impact != chargeMaterial, "受击高于蓄力，不被下一帧蓄力写回覆盖");
		presentation.FlashAttack(); Material? warning = body.MaterialOverlay;
		Check(warning is ShaderMaterial && warning != impact && body.MaterialOverride == original, "攻击预警高于受击，仍保留主体材质");
		presentation.SetProcess(false);
		var camera = GetViewport().GetCamera3D()!;
		_rig.SetPhysicsProcess(false); camera.GlobalPosition = unit.GlobalPosition + new Vector3(3, 2, 5); camera.LookAt(unit.GlobalPosition + Vector3.Up);
		await Capture("combat_readability_rim", "精英／护甲／预警：半透明边缘光，主体材质仍清楚可见");
		presentation.SetProcess(true); await Wait(.16f);
		Check(body.MaterialOverlay == impact, "预警结束后恢复仍有效的受击边缘光");
		await Wait(.3f); Check(body.MaterialOverlay == elite, "受击结束后恢复仍有效且优先的精英边缘光");
		charge.DebugClearChargeFxPreview(); await Wait(.3f);
		Check(body.MaterialOverlay == elite && body.MaterialOverride == original, "蓄力消退后恢复精英轮廓，没有实体染色或残留闪白");
		eliteModifier.QueueFree(); await Wait(.05f);
		Check(body.MaterialOverlay == null && body.MaterialOverride == original, "精英表现退出后归还原叠加通道");
		unit.QueueFree();
	}

	private async Task Capture(string name, string caption)
	{
		if (DisplayServer.GetName() == "headless") return;
		var layer = new CanvasLayer(); layer.AddChild(new Label { Text = caption, Position = new Vector2(22, 22) }); AddChild(layer);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		using var image = GetViewport().GetTexture().GetImage();
		Check(image.SavePng(ActRoguelike.Testing.ValidationArtifacts.Path($"res://Logs/evidence/screenshots/{name}.png")) == Error.Ok, $"截图 {name}");
		layer.QueueFree();
	}
}
