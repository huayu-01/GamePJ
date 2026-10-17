using System;
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

/// <summary>闪／架及其两条冷却进化的真实输入验收。</summary>
public partial class BlinkGuardProbe : Node3D
{
	private int _pass, _fail;
	private Unit _player = null!, _enemy = null!;
	private Camera3D _camera = null!;
	private readonly PlayerLoadout _loadout = new();
	private readonly RunBuild _build = new();
	private BlinkDashAbility Blink => _player.GetNode<BlinkDashAbility>("AbilityBSkill1");
	private GuardStanceAbility Guard => _player.GetNode<GuardStanceAbility>("AbilityBSkill2");
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private void Press(string action, bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[BlinkGuard] {(ok ? "PASS" : "FAIL")} {text}"); }
	public override void _Process(double delta) => _build.TickTriggers((float)delta);
	private async Task<bool> Until(Func<bool> predicate, double seconds = 3)
	{
		double elapsed = 0;
		while (!predicate() && elapsed < seconds) { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); elapsed += GetProcessDeltaTime(); }
		return predicate();
	}
	private async Task Shot(string name)
	{
		if (DisplayServer.GetName() == "headless") return;
		_camera.GlobalPosition = _player.GlobalPosition + new Vector3(4, 2, 6);
		_camera.LookAt(_player.GlobalPosition + Vector3.Up * 0.8f);
		_camera.ResetPhysicsInterpolation();
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng($"res://docs/screenshots/{name}.png") == Error.Ok, $"截图{name}");
	}
	private async Task Equip()
	{
		Press("skill_1", false); Press("skill_2", false);
		_loadout.Apply(_player, this, PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Scythe);
		_player.GlobalPosition = new Vector3(20, 0.05f, 20); _player.Velocity = Vector3.Zero;
		await Wait(0.1);
		_camera.GlobalPosition = _player.GlobalPosition + new Vector3(0, 1, 6);
		_camera.LookAt(_player.GlobalPosition + new Vector3(0, 1, -5));
		_camera.ResetPhysicsInterpolation();
	}
	private Unit Target(Vector3 position, float health)
	{
		var target = new Unit { Faction = Faction.Enemy, MaxHealth = health, KnockbackResistance = 1 };
		target.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.1f } });
		AddChild(target); target.GlobalPosition = position;
		return target;
	}
	private DamageResult Hit(bool energy = false) => _player.ReceiveHit(new HitInstance
	{
		Target = _player, Attacker = _enemy, Context = EventContext.Create(_enemy, _player), Damage = 10, BypassBlock = energy
	});
	private void Grant(string id)
	{
		var definition = RewardCatalog.Find(id)!;
		_build.Add(definition, new RewardContext { Player = _player, Weapon = NodeSearch.FindInSubtree<Weapon>(_player)!, Build = _build });
		Check(_build.Has(id), $"真实奖励链获得{id}");
	}
	public override async void _Ready()
	{
		try
		{
			await Wait(0.2);
			RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.WeaponMaster; RunSetup.Weapon = WeaponPresetKind.Scythe;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera);
			_enemy = Target(new Vector3(-30, 1, -30), 10000);
			await BlinkStartup();
			await BlinkGeometry();
			await GuardEvents();
			await GuardHits();
			await GuardDuration();
			await CooldownRewards();
			await ReloadVfxScripts();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[BlinkGuard] === 闪架专项验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task BlinkStartup()
	{
		await Equip();
		_camera.LookAt(_player.GlobalPosition + new Vector3(0, 8, -5));
		ulong before = _player.GetNode<UnitAnimator>("Animator").SkillPlaybackId;
		Press("skill_1", true); await Wait(0.03); Press("skill_1", false);
		Check(_player.GetNode<UnitAnimator>("Animator").SkillPlaybackId > before, "闪起手实际播放对应动画");
		Check(Blink.IsActive && !_player.Movement!.IsDashing && !_player.Movement.HasVelocityOverride, "闪未到Action不能提前冲刺");
		Check(await Until(() => _player.Movement!.IsDashing), "真实Action后闪进入三维冲刺");
		await Shot("j5_blink_air");
		await Until(() => !Blink.IsActive);
		Check(_player.GlobalPosition.Y > 2, $"准星朝空中，闪实际升空：y={_player.GlobalPosition.Y:0.####}");
		Check(!_player.Movement!.HasVelocityOverride && !_player.Movement.IsDashing, "闪自然结束归还普通移动与重力");
	}
	private async Task BlinkGeometry()
	{
		await Equip();
		Vector3 start = _player.GlobalPosition;
		Press("skill_1", true); await Wait(0.03); Press("skill_1", false);
		await Until(() => !Blink.IsActive);
		float distance = new Vector2(_player.GlobalPosition.X - start.X, _player.GlobalPosition.Z - start.Z).Length();
		Check(Mathf.Abs(distance - Blink.Distance) < 0.02f, $"最后不足一帧按剩余时长移动，实际水平距离{distance:0.####}m，表值{Blink.Distance}");
		await Equip();
		var wall = new StaticBody3D { CollisionLayer = CombatLayers.World };
		wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(8, 8, 0.2f) } });
		AddChild(wall); wall.GlobalPosition = new Vector3(20, 4, 16);
		await Wait(0.03);
		Press("skill_1", true); await Wait(0.03); Press("skill_1", false);
		await Until(() => !Blink.IsActive);
		Check(_player.GlobalPosition.Z > 16.4f && _player.GlobalPosition.Z < 17, $"三维闪仍被World地形阻挡：z={_player.GlobalPosition.Z:0.####}");
		wall.QueueFree();
		await Equip();
		Blink.Distance = 30; Blink.Duration = 0.05f;
		var narrow = Target(new Vector3(20, 0.9f, 14), 10000);
		int hits = 0;
		narrow.HitReceived += hit => { if (hit.Attack?.Ability == Blink.AbilityId) hits++; };
		Press("skill_1", true); await Wait(0.03); Press("skill_1", false);
		await Until(() => !Blink.IsActive);
		Check(hits == 1, $"600m/s闪扫过帧间窄目标只命中一次：{hits}");
		narrow.QueueFree();
		await Equip();
		Blink.AnimEvent = "MissingAction";
		start = _player.GlobalPosition;
		Press("skill_1", true); await Wait(0.03); Press("skill_1", false);
		Check(await Until(() => !Blink.IsActive), "闪缺少Action只取消");
		Check(_player.GlobalPosition.DistanceTo(start) < 0.05f && !_player.Movement!.HasVelocityOverride && Blink.LastPathPoints == 0, "缺事件无位移、无命中体或路径残留");
	}
	private async Task GuardEvents()
	{
		await Equip();
		Guard.GuardOnEvent = "MissingGuardOn"; Guard.AnimEvent = "MissingGuardOn";
		Press("skill_2", true); await Wait(0.25); Press("skill_2", false);
		Check(!Guard.StanceActive, "缺少GuardOn不能按时间兜底开架");
	}
	private async Task GuardHits()
	{
		await Equip();
		Press("skill_2", true); await Wait(0.03); Press("skill_2", false);
		Check(await Until(() => Guard.StanceActive), "真实GuardOn开启架势");
		await Shot("j5_guard_window");
		DamageResult first = Hit(energy: true);
		DamageResult second = Hit();
		Check(first.Invulnerable && first.FinalDamage == 0, "架可规避无视普通格挡的能量波");
		Check(second.FinalDamage > 0 && !second.Invulnerable && !_player.IsInvulnerable, "只规避一次，同帧第二次命中正常结算");
	}
	private async Task GuardDuration()
	{
		foreach (float scale in new[] { 0.5f, 2f })
		{
			await Equip(); Guard.AnimSpeedScale = scale;
			Press("skill_2", true); await Wait(0.03); Press("skill_2", false);
			Check(!Guard.StanceActive, $"{scale}倍动画前摇内尚未开架");
			Check(await Until(() => Guard.StanceActive), "倍速动画的真实GuardOn仍可开架");
			double elapsed = 0;
			while (Guard.StanceActive && elapsed < 2)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				elapsed += GetPhysicsProcessDeltaTime();
			}
			Check(Mathf.Abs((float)elapsed - Guard.StanceSeconds) < 0.05f, $"开架后窗口独立保持{elapsed:0.####}游戏秒，不因动画倍速截短");
		}
		await Equip();
		_player.IsInvulnerable = true;
		Press("skill_2", true); await Wait(0.03); Press("skill_2", false); await Until(() => Guard.StanceActive);
		int parries = Guard.ParryCount;
		Hit();
		Check(Guard.ParryCount == parries && Guard.StanceActive && _player.IsInvulnerable, "已有其他来源无敌时不消耗架势，也不清除该无敌");
		Guard.ResetForLoadout();
		Check(_player.IsInvulnerable, "架势取消仍不清除其他来源无敌");
		_player.IsInvulnerable = false;
	}
	private async Task CooldownRewards()
	{
		await Equip(); Grant("evo_parry_refresh");
		Press("skill_2", true); await Wait(0.03); Press("skill_2", false);
		await Until(() => Guard.StanceActive);
		Hit();
		Check(Guard.IsReady && Guard.CooldownRemaining == 0, $"招架反击真实架住后立即可再用，CD={Guard.CooldownRemaining:0.####}");
		await Equip(); Grant("evo_dash_reset");
		Press("skill_1", true); await Wait(0.03); Press("skill_1", false);
		await Until(() => !Blink.IsActive);
		float before = Blink.CooldownRemaining;
		var other = Target(new Vector3(-25, 1, 25), 1);
		var attack = new AttackInstance { Attacker = _player, Ability = "probe_other_skill", BaseDamage = 10 };
		other.ReceiveHit(attack.RollHit(other, Vector3.Forward, other.GlobalPosition));
		Check(Mathf.IsEqualApprox(Blink.CooldownRemaining, before), $"其他技能击杀不触发闪身步：{before:0.####}→{Blink.CooldownRemaining:0.####}");
		other.QueueFree();
		await Equip();
		var victim = Target(_player.GlobalPosition + Vector3.Forward * 3 + Vector3.Up * 0.9f, 1);
		Press("skill_1", true); await Wait(0.03); Press("skill_1", false);
		Check(await Until(() => victim.IsDead), "闪真实命中击杀目标");
		await Until(() => !Blink.IsActive);
		Check(Blink.CooldownRemaining > 0 && Blink.CooldownRemaining < 1.1f, $"闪施法中击杀的80%减CD保留到收招：{Blink.CooldownRemaining:0.####}");
		victim.QueueFree();
	}
	private async Task ReloadVfxScripts()
	{
		foreach (string path in new[] { "res://Assets/Effects/binbun/StylizedHitFX/script/VFXImpactBB.gd", "res://Assets/Effects/binbun/ExplosionFX/script/VFXExplosionBB.gd" })
		{
			bool valid = true;
			for (int i = 0; i < 3; i++)
			{
				var script = ResourceLoader.Load<GDScript>(path, cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
				valid &= script != null && script.CanInstantiate();
				GC.Collect(); GC.WaitForPendingFinalizers();
				await Wait(0.02);
			}
			Check(valid, $"特效脚本显式依赖经三次深载／强制GC仍可实例化：{path}");
		}
	}
}
