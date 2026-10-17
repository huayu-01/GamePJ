using System;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Run;
using ActRoguelike.Visual;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>下落规则的真实输入、动画和地面验收，不写玩家存档。</summary>
public partial class FallAttackProbe : Node3D
{
	private int _pass, _fail;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private int _lands;
	private FallAttackAbility Fall => _player.GetNode<FallAttackAbility>("AbilityFallAttack");
	private readonly PlayerLoadout _loadout = new();
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[Fall] {(ok ? "PASS" : "FAIL")} {text}"); }
	private void Press(string action, bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });
	private async Task<bool> Until(Func<bool> predicate, double seconds = 3)
	{
		double elapsed = 0;
		while (!predicate() && elapsed < seconds)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			elapsed += GetProcessDeltaTime();
		}
		return predicate();
	}
	private async Task Shot(string name)
	{
		if (DisplayServer.GetName() == "headless") return;
		// 方向已由真实Action锁定后切到旁观机位，确保截图确实拍到角色与枪尖。
		_camera.GlobalPosition = _player.GlobalPosition + new Vector3(4, 2, 6);
		_camera.LookAt(_player.GlobalPosition + Vector3.Up * 0.6f);
		_camera.ResetPhysicsInterpolation();
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng($"res://docs/screenshots/{name}.png") == Error.Ok, $"截图{name}");
	}
	private async Task StartFall()
	{
		Press("fall_attack", true);
		// 等输入被实际消费，不能把上一次保留的伤害／落地快照误当成本次结果。
		Check(await Until(() => Fall.IsActive && !Fall.HasLanded), "本次真实按键已进入下落起手");
		Press("fall_attack", false);
	}
	private async Task Equip(WeaponPresetKind weapon, PlayerCharacterKind character = PlayerCharacterKind.WeaponMaster)
	{
		foreach (string action in new[] { "fall_attack", "jump", "skill_1", "skill_2", "weapon_skill_1", "weapon_skill_2" }) Press(action, false);
		RunSetup.Character = character; RunSetup.Weapon = weapon;
		_loadout.Apply(_player, this, character, weapon);
		await Wait(0.1);
	}
	private async Task Lift(float height, float x = 20)
	{
		_player.GlobalPosition = new Vector3(x, height, 20);
		_player.Velocity = Vector3.Zero;
		_player.ResetPhysicsInterpolation();
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		_player.GlobalPosition = new Vector3(x, height, 20);
		_player.Velocity = Vector3.Zero;
		_camera.GlobalPosition = new Vector3(x + 5, height + 3, 27);
		_camera.LookAt(_player.GlobalPosition + Vector3.Up);
		_camera.ResetPhysicsInterpolation();
	}
	private Unit Target(Vector3 position, float radius = 0.1f, Faction faction = Faction.Enemy)
	{
		var unit = new Unit { Faction = faction, MaxHealth = 10000, KnockbackResistance = 1 };
		unit.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = radius } });
		AddChild(unit); unit.GlobalPosition = position;
		return unit;
	}
	public override async void _Ready()
	{
		try
		{
			await Wait(0.2);
			RunSetup.Character = PlayerCharacterKind.WeaponMaster;
			RunSetup.Weapon = WeaponPresetKind.Scythe;
			RunSetup.HasSelection = true;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>();
			AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true };
			AddChild(_camera);
			Fall.Landed += () => _lands++;
			_loadout.Apply(_player, this, RunSetup.Character, RunSetup.Weapon);
			await Wait(0.2);
			CharacterMovement movement = _player.Movement!;
			float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle() * movement.GravityScale;
			float standardHeight = movement.JumpVelocity * movement.JumpVelocity / (2 * gravity);
			var fall = _player.GetNode<FallAttackAbility>("AbilityFallAttack");
			Check(Mathf.IsEqualApprox(fall.DamageMultiplierForHeight(standardHeight), 1), $"普通单跳基准高度{standardHeight:0.####}m为1倍，实际{fall.DamageMultiplierForHeight(standardHeight):0.####}");
			Check(Mathf.IsEqualApprox(fall.DamageMultiplierForHeight(standardHeight * 1.5f), 1.5f), $"二段跳多50%高度为1.5倍，实际{fall.DamageMultiplierForHeight(standardHeight * 1.5f):0.####}");
			_player.GlobalPosition = new Vector3(20, 8, 20);
			_player.Velocity = Vector3.Zero;
			await Wait(0.02);
			Input.ParseInputEvent(new InputEventAction { Action = "fall_attack", Pressed = true });
			await Wait(0.02);
			Check(fall.IsActive && !fall.IsDiving, "按下后等待真实Action，不提前下冲");
			Check(fall.LastFallHeight > 7, $"记录起手脚下真实离地高度，实际{fall.LastFallHeight:0.####}m");
			Input.ParseInputEvent(new InputEventAction { Action = "fall_attack", Pressed = false });
			await Wait(0.8);
			Check(fall.HasLanded && movement.IsGrounded, "真实World落地");
			await DamageCases();
			await AimCases();
			await JumpCases();
			await DoubleJumpFall();
			await CancellationCases();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[Fall] === 下落专项验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task DamageCases()
	{
		foreach (var spec in new[] { (1f, 34f, 1f), (1.5f, 34f, 1.5f), (1.5f, 17f, 0.75f), (1f, 17f, 0.5f), (5f, 34f, 2f) })
		{
			await Equip(WeaponPresetKind.Scythe);
			float h = _player.Movement!.StandardJumpHeight * spec.Item1;
			await Lift(h);
			Fall.DiveSpeed = spec.Item2;
			var target = Target(new Vector3(20, 1, 20));
			var friendly = Target(new Vector3(20, 1, 20), faction: Faction.Player);
			int hits = 0; float damage = 0;
			target.HitReceived += hit => { if (hit.Attack?.Ability == Fall.AbilityId) { hits++; damage = hit.Damage; } };
			int before = _lands;
			await StartFall();
			Check(await Until(() => Fall.LastDamage > 0), $"{spec.Item1}份高度/{spec.Item2}m/s等待真实Action起动");
			Press("fall_attack", false);
			Check(Mathf.Abs(Fall.LastDamageMultiplier - spec.Item3) < 0.02f, $"实际倍率{Fall.LastDamageMultiplier:0.####}，目标{spec.Item3}（起手{Fall.LastFallHeight:0.####}m，参考{Fall.LastReferenceFallTime:0.####}s）");
			Check(Fall.LastFallHeight > _player.GlobalPosition.Y + 0.5f, "伤害保留按下时高度，未改用前摇结束后高度");
			Check(await Until(() => Fall.HasLanded), "真实落地派发事件");
			await Wait(0.03);
			Check(_lands == before + 1 && !_player.Movement.HasVelocityOverride, "落地事件仅一次并释放速度覆盖");
			Check(hits == 1 && Mathf.Abs(damage - Fall.LastDamage) < 0.001f && Mathf.Abs(target.Health - (10000 - damage)) < 0.002f,
				$"Area与扫掠共享间隔：命中{hits}次，伤害{damage:0.####}，生命{target.Health:0.####}");
			Check(friendly.Health == 10000, "下冲扫掠不伤同阵营");
			target.QueueFree(); friendly.QueueFree();
		}
		await Equip(WeaponPresetKind.SpiritSword);
		await Lift(20);
		Fall.DiveSpeed = 600;
		var narrow = Target(new Vector3(20, 13, 20), 0.025f);
		int narrowHits = 0;
		narrow.HitReceived += hit => { if (hit.Attack?.Ability == Fall.AbilityId) narrowHits++; };
		await StartFall();
		Check(await Until(() => Fall.HasLanded), "600m/s仍真实落地");
		Press("fall_attack", false);
		Check(narrowHits == 1, $"600m/s两帧之间窄目标扫掠命中一次：{narrowHits}");
		narrow.QueueFree();
		await Equip(WeaponPresetKind.Scythe);
		var modifierSource = new object();
		_player.Stats.AddModifier(new StatModifier { Stat = "DamageMultiplier", Operation = ModifierOperation.Multiply, Value = 1.4f, Source = modifierSource });
		await Lift(_player.Movement!.StandardJumpHeight * 1.5f);
		await StartFall();
		Check(await Until(() => Fall.LastDamage > 0), "全局增伤样本真实起动");
		Press("fall_attack", false);
		Check(Mathf.Abs(Fall.LastDamage - Fall.GetNode<AttackHitbox>("Attack").Damage * Fall.LastDamageMultiplier * 1.4f) < 0.001f,
			$"全局增伤只乘一次：{Fall.LastDamage:0.####}");
		_player.Stats.RemoveBySource(modifierSource);
		await Until(() => Fall.HasLanded);
	}
	private async Task AimCases()
	{
		foreach (float distance in new[] { 2f, 15f })
		{
			await Equip(WeaponPresetKind.Spear);
			await Lift(8);
			Check(Fall.HomingMode == FallAttackAbility.DiveMode.Crosshair, "长枪从武器表接定向覆盖");
			_camera.LookAt(new Vector3(20 + distance, 0, 20));
			_camera.ResetPhysicsInterpolation();
			await StartFall();
			Check(await Until(() => Fall.IsDiving), "长枪真实Action开始斜冲");
			Press("fall_attack", false);
			float angle = Mathf.RadToDeg(Fall.LastDiveDirection.AngleTo(Vector3.Down));
			Vector3 destination = Fall.LastAimPoint;
			Check(Fall.HasGroundAim && Fall.LastDiveDirection.X > 0.1f && angle <= Fall.MaxDiveAngleDegrees + 0.01f,
				$"同方向选地面，夹角{angle:0.###}°，落点{destination}");
			Check(Mathf.Abs(Fall.LastDownwardSpeed - Fall.DiveSpeed * -Fall.LastDiveDirection.Y) < 0.001f,
				$"伤害仅取垂直速度{Fall.LastDownwardSpeed:0.####}，总速{Fall.DiveSpeed}");
			if (distance > 10) Check(Mathf.Abs(angle - Fall.MaxDiveAngleDegrees) < 0.01f, "超范围按最大角度截到同方向");
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			var display = _player.GetNode<CharacterDisplay>("Appearance");
			Check(display.DivePoseActive && display.EquippedWeapon!.GlobalBasis.Y.Normalized().Dot(Fall.LastDiveDirection) > 0.999f,
				$"握持长枪枪尖对齐真实下冲方向，表现只读：活动={display.DivePoseActive}，点积={display.EquippedWeapon!.GlobalBasis.Y.Normalized().Dot(Fall.LastDiveDirection):0.####}");
			await Shot("j4_spear_dive");
			Check(await Until(() => Fall.HasLanded), "斜冲真实落地");
			Check(new Vector2(_player.GlobalPosition.X - destination.X, _player.GlobalPosition.Z - destination.Z).Length() < 0.45f,
				$"实际落点接近目标：{_player.GlobalPosition}");
		}
		await Equip(WeaponPresetKind.Spear);
		await Lift(8);
		_camera.LookAt(_camera.GlobalPosition + new Vector3(0, 2, -1));
		await StartFall(); await Until(() => Fall.IsDiving);
		Check(!Fall.HasGroundAim && Fall.LastDiveDirection.IsEqualApprox(Vector3.Down), "准星指天空时垂直下落");
		await Until(() => Fall.HasLanded);
		await Equip(WeaponPresetKind.Spear); await Lift(8);
		var wall = new StaticBody3D { CollisionLayer = CombatLayers.World };
		wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.2f, 8, 8) } });
		AddChild(wall); wall.GlobalPosition = new Vector3(23, 5, 20);
		_camera.GlobalPosition = new Vector3(26, 8, 20); _camera.LookAt(new Vector3(23, 6, 20));
		await StartFall(); await Until(() => Fall.IsDiving);
		Check(!Fall.HasGroundAim && Fall.LastDiveDirection.IsEqualApprox(Vector3.Down), "准星命中World墙面不误作地面");
		await Until(() => Fall.HasLanded); wall.QueueFree();
		foreach (WeaponPresetKind weapon in new[] { WeaponPresetKind.Scythe, WeaponPresetKind.SpiritSword })
		{
			await Equip(weapon);
			Check(Fall.HomingMode == FallAttackAbility.DiveMode.Straight, $"换{weapon}恢复共通直落，无长枪方向残留");
			Check(!_player.GetNode<CharacterDisplay>("Appearance").DivePoseActive, "换装恢复握持姿态");
		}
	}
	private async Task CancellationCases()
	{
		await Equip(WeaponPresetKind.SpiritSword); await Lift(20);
		int before = _lands;
		Fall.AnimEvent = "MissingAction";
		await StartFall();
		Check(await Until(() => !Fall.IsActive), "缺少Action时取消，不以计时兜底下冲");
		Check(!Fall.HasLanded && _lands == before && Fall.LastDamage == 0 && !_player.Movement!.HasVelocityOverride, "缺事件无伤害、无落地附加圈、无速度残留");
		await Equip(WeaponPresetKind.SpiritSword); await Lift(20);
		before = _lands; Fall.MaxDiveSeconds = 0.01f;
		await StartFall();
		Check(await Until(() => !Fall.IsActive), "下冲超时正确取消");
		Check(_lands == before && !Fall.HasLanded && !_player.Movement!.HasVelocityOverride, "超时不能伪造落地事件");
		await Equip(WeaponPresetKind.Scythe); await Lift(20);
		await StartFall(); await Until(() => Fall.IsDiving);
		_player.Movement!.EndVelocityOverride(new object());
		Check(_player.Movement.HasVelocityOverride, "其他来源不能释放本次下冲速度");
		before = _lands;
		Press("skill_2", true); await Wait(0.03); Press("skill_2", false);
		Check(!Fall.IsActive && !_player.Movement.HasVelocityOverride && _lands == before, "后来架势打断下冲，释放速度且不派发落地");
		await Equip(WeaponPresetKind.Scythe); await Lift(20);
		await StartFall(); await Until(() => Fall.IsDiving);
		await Equip(WeaponPresetKind.Spear);
		Check(!Fall.IsActive && !_player.Movement.HasVelocityOverride, "换装清理下冲速度与命中体");
		await Equip(WeaponPresetKind.Scythe); await Lift(8, 60);
		Check(!Fall.TryPress(), "脚下无真实World地面时拒绝下落攻击，不用空点编造高度");
		await Equip(WeaponPresetKind.Scythe); await Lift(20);
		await StartFall(); await Until(() => Fall.IsDiving);
		_player.ReceiveHit(new HitInstance { Target = _player, Context = EventContext.Create(null, _player), Damage = 99999 });
		await Wait(0.06);
		Check(_player.IsDead && !Fall.IsActive && !_player.Movement!.HasVelocityOverride, "死亡释放下冲速度和命中状态");
	}
	private async Task JumpCases()
	{
		await Equip(WeaponPresetKind.Bow, PlayerCharacterKind.Hunter);
		await Lift(0.05f, -20);
		Check(await Until(() => _player.Movement!.IsGrounded), "普通二段跳样本从真实地面开始");
		var movement = _player.Movement!;
		Check(Mathf.IsEqualApprox(movement.AirJumpHeightRatio, 0.5f)
			&& Mathf.IsEqualApprox(movement.EffectiveAirJumpVelocity / movement.EffectiveJumpVelocity, Mathf.Sqrt(0.5f)),
			$"角色表额外跳高50%，速度比例{movement.EffectiveAirJumpVelocity / movement.EffectiveJumpVelocity:0.#####}");
		float ground = _player.GlobalPosition.Y;
		Press("jump", true); await Wait(0.03); Press("jump", false);
		Check(await Until(() => _player.Velocity.Y <= 0), "普通单跳经过真实重力到最高点");
		float first = _player.GlobalPosition.Y;
		Press("jump", true); await Wait(0.03); Press("jump", false);
		Check(await Until(() => _player.Velocity.Y <= 0), "额外跳经过真实重力到最高点");
		float second = _player.GlobalPosition.Y;
		float ratio = (second - first) / (first - ground);
		Check(Mathf.Abs(ratio - 0.5f) < 0.08f, $"实际单跳高{first - ground:0.####}m，额外高{second - first:0.####}m，比值{ratio:0.####}（含物理帧离散误差）");
		Check(Mathf.Abs(Fall.DamageMultiplierForHeight(second - ground) - 1.5f) < 0.08f,
			$"真实二段跳总高度折算标准下落倍率{Fall.DamageMultiplierForHeight(second - ground):0.####}");
		await Shot("j4_double_jump");
		string path = $"user://j4_tuning_{Time.GetTicksUsec()}.tres";
		try
		{
			var row = (AbilityConfig)ConfigRows.ById<AbilityConfig>("skl_b_fall")!.Duplicate(true);
			row.FallAttack!.DiveSpeed = 17; row.FallAttack.MaxDamageMultiplier = 1.25f;
			Check(ResourceSaver.Save(row, path) == Error.Ok, "强类型下落参数可保存成表资源");
			RuntimeResources.Load<AbilityConfig>(path).ApplyTo(Fall);
			Check(Fall.DiveSpeed == 17 && Fall.MaxDamageMultiplier == 1.25f
				&& Fall.DamageMultiplierForDive(movement.StandardJumpHeight * 10, 34) == 1.25f,
				"实际读回表资源改变速度与倍率上限，无字符串反射");
		}
		finally { DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path)); }
		path = $"user://j4_jump_{Time.GetTicksUsec()}.tres";
		try
		{
			var character = (CharacterConfig)ConfigRows.ById<CharacterConfig>("chr_hunter")!.Duplicate(true);
			character.AirJumpHeightRatio = 0.25f;
			Check(ResourceSaver.Save(character, path) == Error.Ok, "角色表空中跳高比例可写盘");
			RuntimeResources.Load<CharacterConfig>(path).ApplyJumpTo(movement);
			Check(movement.AirJumpHeightRatio == 0.25f && Mathf.IsEqualApprox(movement.EffectiveAirJumpVelocity / movement.EffectiveJumpVelocity, 0.5f),
				"真实读回0.25跳高比例，经生产接线得到0.5起跳速度比例");
		}
		finally { DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path)); }
	}
	private async Task DoubleJumpFall()
	{
		await Equip(WeaponPresetKind.Scythe); await Lift(0.05f);
		// 明示夹具授予一次空中跳；本专项不冒充已经验收“获取该次数”的增幅链。
		_player.Movement!.SetExtraAirJumps(1, 3);
		await Until(() => _player.Movement.IsGrounded);
		Press("jump", true); await Wait(0.03); Press("jump", false);
		await Until(() => _player.Velocity.Y <= 0);
		Press("jump", true); await Wait(0.03); Press("jump", false);
		await Until(() => _player.Velocity.Y <= 0);
		var target = Target(new Vector3(20, 1, 20));
		float damage = 0;
		target.HitReceived += hit => { if (hit.Attack?.Ability == Fall.AbilityId) damage = hit.Damage; };
		await StartFall(); await Until(() => Fall.LastDamage > 0);
		Check(Mathf.Abs(Fall.LastDamageMultiplier - 1.5f) < 0.08f, $"武器大师真实二段跳后按Ctrl：起手{Fall.LastFallHeight:0.####}m，倍率{Fall.LastDamageMultiplier:0.####}");
		await Shot("j4_double_jump_fall");
		Check(await Until(() => Fall.HasLanded), "二段跳后真实下冲落地");
		Check(damage > 0 && Mathf.Abs(damage - Fall.LastDamage) < 0.001f, $"二段跳下落伤害实际进入目标：{damage:0.####}");
		target.QueueFree();
	}
}
