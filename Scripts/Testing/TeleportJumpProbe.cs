using System;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>浮光跃金：真实跳跃输入、额外次数换完整基础跳高、瞬移与World净空。</summary>
public partial class TeleportJumpProbe : Node3D
{
	private int _pass, _fail;
	private double _elapsed;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private readonly PlayerLoadout _loadout = new();
	private CharacterMovement Movement => _player.Movement!;
	public override void _Process(double delta) => _elapsed += delta;
	private void Press(bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = "jump", Pressed = pressed });
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private async Task<bool> Until(Func<bool> predicate, double seconds = 4)
	{
		double start = _elapsed;
		while (!predicate() && _elapsed - start < seconds) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		return predicate();
	}
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[TeleportJump] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Equip(bool grant = false, int extra = 0)
	{
		Press(false);
		_loadout.Apply(_player, this, PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Spear);
		_player.GlobalPosition = new Vector3(20, 0.05f, 20); _player.Velocity = Vector3.Zero; _player.ResetPhysicsInterpolation();
		Movement.SetExtraAirJumps(extra, 3);
		_camera.GlobalPosition = new Vector3(20, 3, 28); _camera.LookAt(new Vector3(20, 2, 20)); _camera.ResetPhysicsInterpolation();
		if (grant)
		{
			var row = ConfigRows.ById<RewardConfig>("evo_light_leap");
			var def = RewardCatalog.Find("evo_light_leap");
			Check(row?.HasKnownHandler == true && def != null, "浮光跃金真实表行与处理器有效");
			var build = new RunBuild();
			if (def != null) build.Add(def, new RewardContext { Player = _player, Weapon = NodeSearch.FindInSubtree<Weapon>(_player)!, Build = build });
			Check(build.Has("evo_light_leap") && Movement.TeleportJumpEnabled, "真实入构筑后开启瞬移跳跃");
		}
		await Wait(0.2);
		Check(Movement.IsGrounded, "准备完成，真实物理身体在地面");
	}
	private async Task Jump()
	{
		int before = Movement.TeleportJumpCount;
		Press(true);
		Check(await Until(() => Movement.TeleportJumpCount > before, 0.5), "真实跳跃按键触发瞬移");
		Press(false);
	}
	public override async void _Ready()
	{
		try
		{
			await Wait(0.2);
			RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.WeaponMaster; RunSetup.Weapon = WeaponPresetKind.Spear;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera);
			await NormalJump();
			foreach (int extra in new[] { 0, 1, 2, 3 }) await ConvertedJump(extra);
			await DynamicAndCeiling();
			await Equip(extra: 1);
			Check(!Movement.TeleportJumpEnabled && Movement.TeleportJumpCount == 0 && Movement.AirJumpsLeft == 1, "换装关闭瞬移并恢复真实空中跳次数");
			float y = _player.GlobalPosition.Y;
			Press(true); await Wait(0.03); Press(false);
			Check(_player.Velocity.Y > 0f && _player.GlobalPosition.Y - y < 1f, "换装后恢复普通连续上升，没有瞬移残留");
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[TeleportJump] === 浮光跃金验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task NormalJump()
	{
		await Equip(extra: 1);
		float y = _player.GlobalPosition.Y;
		Press(true); await Wait(0.03); Press(false);
		Check(!Movement.TeleportJumpEnabled && Movement.TeleportJumpCount == 0 && _player.Velocity.Y > 0f && _player.GlobalPosition.Y - y < 1f, "默认真实按键仍是普通地面跳，不瞬移");
		await Wait(0.1); Press(true); await Wait(0.02); Press(false);
		Check(Movement.AirJumpsLeft == 0 && _player.Velocity.Y > 0f && Mathf.IsEqualApprox(Movement.AirJumpHeightRatio, 0.5f), "默认额外空中跳仍消耗一次、采用原0.5高度比例");
	}
	private async Task ConvertedJump(int extra)
	{
		await Equip(true, extra);
		float y = _player.GlobalPosition.Y;
		float expected = Movement.StandardJumpHeight * (1 + extra);
		await Jump();
		Check(Mathf.Abs(Movement.LastTeleportJumpHeight - expected) < 0.005f, $"额外{extra}次换成{1 + extra}份完整基础跳高，实际{Movement.LastTeleportJumpHeight:0.###}m／期望{expected:0.###}m");
		Check(Mathf.Abs(_player.GlobalPosition.Y - y - expected) < 0.08f && _player.Velocity.Y <= 0.01f, "一个输入周期直接到目标高度，没有继续向上的弹道速度");
		Check(Movement.AirJumpsLeft == 0 && Movement.ExtraAirJumps == extra, "额外次数只保留为高度来源，不能再作为空中跳使用");
		int count = Movement.TeleportJumpCount;
		await Wait(0.08); Press(true); await Wait(0.02); Press(false);
		Check(Movement.TeleportJumpCount == count && _player.Velocity.Y <= 0f, "空中再次真实按跳不重复瞬移、不额外起跳");
		if (extra == 2) await Screenshot();
		Check(await Until(() => Movement.IsGrounded, 4), "瞬移后自然重力落地");
		await Wait(0.04); await Jump();
		Check(Movement.TeleportJumpCount == count + 1, "真实落地后可再次瞬移，不被地面旧状态锁住");
	}
	private async Task DynamicAndCeiling()
	{
		await Equip(true, 0);
		Movement.SetExtraAirJumps(2, 3);
		float expected = Movement.StandardJumpHeight * 3;
		await Jump();
		Check(Mathf.Abs(Movement.LastTeleportJumpHeight - expected) < 0.005f, "获得进化后再增加额外次数，下一跳按新次数折算，非获取时冻结");
		await Equip(true, 2);
		var ceiling = new StaticBody3D { CollisionLayer = CombatLayers.World, CollisionMask = 0 };
		var size = new Vector3(5, 0.5f, 5);
		ceiling.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
		ceiling.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.SlateGray } });
		AddChild(ceiling); ceiling.GlobalPosition = new Vector3(20, 3.5f, 20);
		await Wait(0.1); float y = _player.GlobalPosition.Y;
		await Jump();
		Check(Movement.LastTeleportJumpHeight > 0f && Movement.LastTeleportJumpHeight < expected && _player.GlobalPosition.Y - y < 1.46f,
			$"真实身体向上碰World截断净空，实际位移{Movement.LastTeleportJumpHeight:0.###}m，不进入天花板");
		ceiling.QueueFree(); await Wait(0.1);
		await Equip(true);
		Movement.BeginVelocityOverride(this, Vector3.Zero);
		int count = Movement.TeleportJumpCount;
		Press(true); await Wait(0.03); Press(false);
		Check(Movement.TeleportJumpCount == count, "其他动作持有速度覆盖时，跳跃仍被拦截");
		Movement.EndVelocityOverride(this);
	}
	private async Task Screenshot()
	{
		if (DisplayServer.GetName() == "headless") return;
		Transform3D before = _camera.GlobalTransform;
		var label = new Label3D { Text = $"Light leap: 2 extra jumps -> 3 full base heights\nInstant rise {Movement.LastTeleportJumpHeight:0.###}m | no air jumps", Position = _player.GlobalPosition + Vector3.Up * 2f, FontSize = 44, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled };
		AddChild(label);
		_camera.GlobalPosition = new Vector3(28, 10, 29); _camera.LookAt(new Vector3(20, 4, 20)); _camera.ResetPhysicsInterpolation();
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		using var screenshot = GetViewport().GetTexture().GetImage();
		Check(screenshot.SavePng("res://docs/screenshots/j18_light_leap.png") == Error.Ok, "截图 j18_light_leap");
		label.QueueFree(); _camera.GlobalTransform = before; _camera.ResetPhysicsInterpolation();
	}
}
