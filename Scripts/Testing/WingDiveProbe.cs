using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>飞翼下落攻击速度：真实输入、Action、物理位移和落地；正常下落倍率独立。</summary>
public partial class WingDiveProbe : Node3D
{
	private int _pass, _fail, _landings;
	private double _elapsed;
	private Unit _player = null!;
	private Unit? _target;
	private Camera3D _camera = null!;
	private RunBuild _build = new();
	private readonly PlayerLoadout _loadout = new();
	private FallAttackAbility Fall => _player.GetNode<FallAttackAbility>("AbilityFallAttack");
	private CharacterMovement Movement => _player.Movement!;
	public override void _Process(double delta) => _elapsed += delta;
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[WingDive] {(ok ? "PASS" : "FAIL")} {text}"); }
	private void Press(string action, bool down) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = down });
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private async Task InputFrame() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
	private async Task<bool> Until(Func<bool> predicate, double seconds = 4)
	{
		double start = _elapsed;
		while (!predicate() && _elapsed - start < seconds) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		return predicate();
	}
	private async Task Equip(WeaponPresetKind weapon, RewardConfig? rune, bool angled = false)
	{
		Press("fall_attack", false); Press("skill_1", false);
		foreach (var rule in _build.Triggers) if (IsInstanceValid(rule)) { rule.GetParent()?.RemoveChild(rule); rule.QueueFree(); }
		_player.Stats.RemoveBySource(_build); _build = new RunBuild();
		if (_target != null && IsInstanceValid(_target)) _target.QueueFree();
		_target = null; _landings = 0;
		RunSetup.Weapon = weapon; _loadout.Apply(_player, this, PlayerCharacterKind.WeaponMaster, weapon);
		_player.GlobalPosition = new Vector3(20, 12, 20); _player.Velocity = Vector3.Zero; _player.ResetPhysicsInterpolation();
		_camera.GlobalPosition = angled ? new Vector3(20, 16, 29) : new Vector3(20, 18, 20);
		_camera.LookAt(angled ? new Vector3(20, 0, 15) : new Vector3(20, 0, 20), Vector3.Forward); _camera.ResetPhysicsInterpolation();
		if (rune != null)
		{
			var definition = RewardEffectRegistry.Compile(rune)!;
			_build.Add(definition, new RewardContext { Player = _player, Weapon = NodeSearch.FindInSubtree<Weapon>(_player)!, Build = _build });
		}
		await Wait(0.04);
	}
	private RewardConfig Wing => ConfigRows.ById<RuneConfig>("rune_flying_wing")!;
	private RewardConfig CopyWing(float scale)
	{
		var copy = (RewardConfig)Wing.Duplicate(true);
		var modifier = copy.Modifiers.FirstOrDefault(value => value.Stat == "DiveSpeedMultiplier");
		if (modifier != null) modifier.Value = scale;
		else copy.Modifiers = copy.Modifiers.Concat(new[] { new StatOverride { Stat = "DiveSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = scale } }).ToArray();
		return copy;
	}
	private void OnLanding() => _landings++;
	public override async void _Ready()
	{
		try
		{
			RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.WeaponMaster; RunSetup.Weapon = WeaponPresetKind.SpiritSword;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera); Fall.Landed += OnLanding;
			await Wait(0.2);
			foreach (var weapon in new[] { WeaponPresetKind.SpiritSword, WeaponPresetKind.Scythe, WeaponPresetKind.Spear })
			{
				var baseline = await Dive(weapon, null, 1);
				var wing = await Dive(weapon, Wing, 1.3f);
				Check(Mathf.IsEqualApprox(wing.Speed / baseline.Speed, 1.3f) && wing.Seconds < baseline.Seconds,
					$"{weapon} 飞翼垂直速度×1.3且落地更快：{baseline.Speed:0.###}→{wing.Speed:0.###}m/s，{baseline.Seconds:0.###}→{wing.Seconds:0.###}s");
			}
			using var copy = CopyWing(2);
			await Dive(WeaponPresetKind.Spear, copy, 2, angled: true, removeAfterAction: true);
			string text = RewardEffectRegistry.Compile(copy)!.Description;
			Check(text.Contains("2") && !text.Contains('{'), $"只改飞翼表倍率2后说明同步：{text}");
			await NormalFall(null, 1); await NormalFall(Wing, 0.8f);
			await CancelAndRemove();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		finally { if (_player != null && IsInstanceValid(_player)) Fall.Landed -= OnLanding; }
		GD.Print($"[WingDive] === 飞翼下落验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task<(float Speed, double Seconds)> Dive(WeaponPresetKind weapon, RewardConfig? rune, float scale, bool angled = false, bool removeAfterAction = false)
	{
		await Equip(weapon, rune, angled);
		Check(Mathf.IsEqualApprox(_player.Stats.Get("DiveSpeedMultiplier"), scale), $"{weapon} 实际单位下冲倍率{_player.Stats.Get("DiveSpeedMultiplier"):0.###}，期望{scale}");
		if (!angled)
		{
			_target = new Unit { Faction = Faction.Enemy, MaxHealth = 10000, KnockbackResistance = 1 };
			_target.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.2f } }); AddChild(_target);
			_target.GlobalPosition = new Vector3(22, 0.9f, 20);
		}
		Press("fall_attack", true); await InputFrame(); Press("fall_attack", false);
		Check(await Until(() => Fall.IsDiving), $"{weapon} 真实按键→动画Action启动下落攻击");
		double started = _elapsed;
		float expected = Mathf.Abs(Fall.DiveSpeed) * scale;
		float vertical = Fall.LastDownwardSpeed;
		Check(Mathf.Abs(vertical - expected * -Fall.LastDiveDirection.Y) < 0.005f && !Fall.HasLanded,
			$"{weapon} 起动快照垂直速{vertical:0.###}，表内速度{Fall.DiveSpeed}×{scale}后保留方向垂直分量");
		if (angled) Check(Fall.HasGroundAim && Fall.LastDiveDirection.Y < 0 && Mathf.Abs(Fall.LastDiveDirection.Z) > 0.1f, "长枪真实准星斜向下冲，倍率不改变方向");
		if (removeAfterAction)
		{
			_player.Stats.RemoveBySource(_build);
			Check(Mathf.IsEqualApprox(_player.Stats.Get("DiveSpeedMultiplier"), 1), "Action后移除符文，属性恢复默认1");
		}
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		Vector3 before = _player.GlobalPosition; ulong frame = Engine.GetPhysicsFrames();
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		float actualSpeed = _player.GlobalPosition.DistanceTo(before) * Engine.PhysicsTicksPerSecond / (Engine.GetPhysicsFrames() - frame);
		Check(Mathf.Abs(actualSpeed - expected) < 0.03f && Movement.HasVelocityOverride,
			$"{weapon} 真实物理位移速度{actualSpeed:0.###}m/s，期望{expected:0.###}；不被正常下落夹速覆盖");
		if (removeAfterAction) Check(Mathf.Abs(-_player.Velocity.Y - vertical) < 0.005f, "飞行中的速度保持Action快照，移除只影响下一次下冲");
		Check(await Until(() => Fall.HasLanded) && _landings == 1, $"{weapon} 真实World落地且只派发一次落地事件");
		double duration = _elapsed - started;
		await Until(() => !Fall.IsActive);
		Check(!Movement.HasVelocityOverride && Movement.IsGrounded, "落地收招归还普通移动，不残留下冲速度覆盖");
		if (_target != null) Check(_target.Health < _target.MaxHealth, $"{weapon} 加速前后均真实命中地面敌人，伤害{_target.MaxHealth - _target.Health:0.###}");
		GD.Print($"[WingDiveSample] {weapon} scale={scale} height={Fall.LastFallHeight:0.###} vertical={vertical:0.###} physical={actualSpeed:0.###} duration={duration:0.###} damage={Fall.LastDamage:0.###}");
		return (vertical, duration);
	}
	private async Task NormalFall(RewardConfig? rune, float scale)
	{
		await Equip(WeaponPresetKind.Scythe, rune);
		_player.GlobalPosition = new Vector3(20, 120, 20); _player.Velocity = Vector3.Zero;
		float cap = Movement.MaxFallSpeed * scale;
		Check(await Until(() => -_player.Velocity.Y >= cap - 0.005f, 4) && !Movement.IsGrounded,
			$"真实自然下落达到独立速度上限{cap:0.###}m/s");
		Check(Mathf.Abs(-_player.Velocity.Y - cap) < 0.005f && !Movement.HasVelocityOverride && !Fall.IsDiving,
			$"自然下落速度{ -_player.Velocity.Y:0.###}只读正常下落倍率{scale}，不读下冲倍率");
	}
	private async Task CancelAndRemove()
	{
		await Equip(WeaponPresetKind.Scythe, Wing);
		Press("fall_attack", true); await InputFrame(); Press("fall_attack", false);
		Check(await Until(() => Fall.IsDiving), "打断用例先经真实Action开始下冲");
		Press("skill_1", true); await InputFrame(); Press("skill_1", false);
		Check(!Fall.IsDiving && !Fall.HasLanded && _landings == 0, "真实闪打断下冲，不伪造落地攻击");
		await Equip(WeaponPresetKind.Scythe, null);
		Check(Mathf.IsEqualApprox(_player.Stats.Get("DiveSpeedMultiplier"), 1) && !Movement.HasVelocityOverride, "卸除／换装后倍率与速度覆盖均复原");
	}
}
