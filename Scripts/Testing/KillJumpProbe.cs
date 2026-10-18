using System;
using System.Collections.Generic;
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

/// <summary>践踏死亡须逐个计入本人真实击杀，覆盖同帧多杀、连锁、上限和真实跳跃。</summary>
public partial class KillJumpProbe : Node3D
{
	private int _pass, _fail, _successfulJumps;
	private double _elapsed;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private readonly PlayerLoadout _loadout = new();
	private RunBuild _build = new();
	private readonly List<Unit> _targets = new();
	private readonly List<GameplayEvent> _kills = new();
	private readonly List<ulong> _killFrames = new();
	private readonly HashSet<AttackInstance> _attacks = new();
	private CharacterMovement Movement => _player.Movement!;
	private int OwnKills => _kills.Count(kill => kill.Source == _player);
	public override void _Process(double delta) { _elapsed += delta; _build.TickTriggers((float)delta); }
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[KillJump] {(ok ? "PASS" : "FAIL")} {text}"); }
	private void Press(string action, bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private async Task<bool> Until(Func<bool> predicate, double seconds = 4)
	{
		double start = _elapsed;
		while (!predicate() && _elapsed - start < seconds) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		return predicate();
	}
	private async Task InputFrame()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}
	private void OnKill(GameplayEvent fact)
	{
		if (fact.Kind != GameplayEventKind.Kill) return;
		_kills.Add(fact);
		if (fact.Source == _player) _killFrames.Add(Engine.GetPhysicsFrames());
	}
	private void OnJump(Vector3 origin) => _successfulJumps++;
	private async Task Reset(PlayerCharacterKind character = PlayerCharacterKind.WeaponMaster, WeaponPresetKind weapon = WeaponPresetKind.SpiritSword)
	{
		foreach (string action in new[] { "jump", "weapon_skill_1", "weapon_skill_2", "skill_1", "skill_2" }) Press(action, false);
		foreach (var rule in _build.Triggers) if (IsInstanceValid(rule)) { rule.GetParent()?.RemoveChild(rule); rule.QueueFree(); }
		_player.Stats.RemoveBySource(_build); _build = new RunBuild();
		foreach (var target in _targets) if (IsInstanceValid(target)) target.QueueFree();
		await Wait(0.05); _targets.Clear(); _kills.Clear(); _killFrames.Clear(); _attacks.Clear(); _successfulJumps = 0;
		RunSetup.Character = character; RunSetup.Weapon = weapon;
		_loadout.Apply(_player, this, character, weapon);
		_player.GlobalPosition = new Vector3(20, 0.05f, 20); _player.Velocity = Vector3.Zero; _player.ResetPhysicsInterpolation();
		_camera.GlobalPosition = new Vector3(20, 2, 26); _camera.LookAt(new Vector3(20, 1, 12)); _camera.ResetPhysicsInterpolation();
		await Wait(0.2); Check(Movement.IsGrounded, "独立用例玩家身体已站地");
	}
	private void Grant(RewardConfig? row = null)
	{
		var definition = row == null ? RewardCatalog.Find("mut_stomp_kill") : RewardEffectRegistry.Compile(row);
		Check(definition != null, "真实奖励行可编译／查得践踏死亡");
		if (definition == null) return;
		_build.Add(definition, new RewardContext { Player = _player, Weapon = NodeSearch.FindInSubtree<Weapon>(_player)!, Build = _build });
		Check(_build.Has("mut_stomp_kill"), "真实入构筑践踏死亡");
	}
	private Unit Target(float x, float health = 1)
	{
		var target = new Unit { Faction = Faction.Enemy, MaxHealth = health, KnockbackResistance = 1 };
		target.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.16f } });
		AddChild(target); target.GlobalPosition = new Vector3(x, 0.9f, 18.8f); _targets.Add(target);
		target.HitReceived += hit => { if (hit.Attack != null) _attacks.Add(hit.Attack); };
		return target;
	}
	private void BurstTargets(int count)
	{
		for (int i = 0; i < count; i++) Target(20 + (i - (count - 1) * 0.5f) * 0.3f);
	}
	private async Task MeleeBurst(int count)
	{
		var ability = PlayerAbilityInput.FindByInputAction(_player, "weapon_skill_1")!;
		Check(await Until(() => !ability.IsActive && ability.CooldownRemaining <= 0.0001f), "等待真实武技可用，不手动缩冷却");
		int before = OwnKills;
		Press("weapon_skill_1", true); await InputFrame(); Press("weapon_skill_1", false);
		Check(await Until(() => OwnKills == before + count), $"真实按键→动画ATK→物理靶死亡，新增本人击杀{OwnKills - before}/{count}");
		await Until(() => !ability.IsActive);
	}
	private async Task Jump()
	{
		Press("jump", false); await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		Press("jump", true); await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		Press("jump", false); await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
	}
	private static void Hit(Unit source, Unit target, float damage, AttackInstance? attack = null)
	{
		attack ??= new AttackInstance { Attacker = source, BaseDamage = damage, CanCrit = false };
		target.ReceiveHit(attack.RollHit(target, Vector3.Forward, target.GlobalPosition));
	}
	private static RewardConfig Copy(int amount, int cap)
	{
		var copy = (RewardConfig)ConfigRows.ById<RewardConfig>("mut_stomp_kill")!.Duplicate(true);
		foreach (var parameter in copy.HandlerParams)
		{
			if (parameter.Key == "ExtraAirJumps") parameter.Value = amount;
			if (parameter.Key == "JumpCap") parameter.Value = cap;
			if (parameter.Key == "Cooldown") parameter.Value = 0;
		}
		return copy;
	}
	public override async void _Ready()
	{
		try
		{
			RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.WeaponMaster; RunSetup.Weapon = WeaponPresetKind.SpiritSword;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera);
			GameplayEventBus.Raised += OnKill; Movement.JumpStarted += OnJump;
			await Wait(0.2);
			await WithoutAugment(); await OrdinaryBurst(); await SourcesAndRemoval(); await SharedChain(); await TableParameters(); await ExistingPassive(); await AirRefill();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		finally
		{
			GameplayEventBus.Raised -= OnKill;
			if (_player != null && IsInstanceValid(_player)) Movement.JumpStarted -= OnJump;
		}
		GD.Print($"[KillJump] === 击杀跳跃验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task WithoutAugment()
	{
		await Reset(); BurstTargets(3); await Wait(0.05); await MeleeBurst(3);
		Check(_killFrames.Count == 3 && _killFrames.Distinct().Count() == 1 && _attacks.Count == 1, "无增幅真实一刀同帧三杀，共用一个AttackInstance");
		Check(Movement.ExtraAirJumps == 0, "无增幅本人击杀不增加额外跳跃");
	}
	private async Task OrdinaryBurst()
	{
		await Reset(); Grant(); BurstTargets(3); await Wait(0.05); await MeleeBurst(3);
		Check(_killFrames.Count == 3 && _killFrames.Distinct().Count() == 1, "已获得增幅后仍是真实同帧三杀");
		Check(Movement.ExtraAirJumps == 3, $"每次击杀均计入，B从0到3，实读{Movement.ExtraAirJumps}");
		BurstTargets(3); await Wait(0.05); await MeleeBurst(3);
		Check(Movement.ExtraAirJumps == 3, "继续本人多杀保持表内封顶3");
		await Wait(0.05); Check(Movement.AirJumpsLeft == 3, "站地后可用额外空中跳次数为3");
		await Jump(); Check(_successfulJumps == 1 && !Movement.IsGrounded, "真实地面跳起动");
		for (int i = 1; i <= 3; i++)
		{
			await Jump(); Check(_successfulJumps == i + 1 && Movement.AirJumpsLeft == 3 - i, $"真实第{i}次空中跳消耗一次，剩余{Movement.AirJumpsLeft}");
		}
		await Jump(); Check(_successfulJumps == 4 && Movement.AirJumpsLeft == 0, "第四次空中跳失败，不绕过次数限制");
		bool landed = await Until(() => Movement.IsGrounded);
		int atLanding = Movement.AirJumpsLeft;
		// MoveAndSlide在本帧末尾更新落地，容量回满在下一次移动物理处理开头。
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		GD.Print($"[KillJumpLanding] 落地采样可用{atLanding}→完整物理帧后{Movement.AirJumpsLeft}，容量{Movement.ExtraAirJumps}");
		Check(landed && Movement.IsGrounded && Movement.AirJumpsLeft == 3, "真实落地后经过完整物理处理按已获得容量回满三次");
	}
	private async Task SourcesAndRemoval()
	{
		await Reset(); Grant();
		var other = new Unit { Faction = Faction.Player, MaxHealth = 100 }; AddChild(other);
		Hit(other, Target(20), 100);
		Check(OwnKills == 0 && _kills.Count == 1 && Movement.ExtraAirJumps == 0, "他人真实击杀不授予本人次数");
		Hit(_player, Target(20.5f, 10000), 1);
		Check(OwnKills == 0 && Movement.ExtraAirJumps == 0, "本人非致死命中不计击杀"); other.QueueFree();
		await Reset(); Hit(_player, Target(20), 100);
		Check(OwnKills == 1 && Movement.ExtraAirJumps == 0, "卸除旧构筑规则后退订，之后本人真实击杀不残留授予");
	}
	private async Task SharedChain()
	{
		await Reset(); using var copy = Copy(1, 8); Grant(copy); BurstTargets(5); await Wait(0.05); await MeleeBurst(5);
		Check(OwnKills == 5 && _attacks.Count == 1 && _killFrames.Distinct().Count() == 1,
			"真实一刀同帧五杀；RollHit按每个目标建立上下文，不误称这些自然命中属于同链");
		Check(Movement.ExtraAirJumps == 5, $"复制表设上限8／冷却0后，独立命中上下文的五杀实际计入{Movement.ExtraAirJumps}次");
		await Reset(); Grant(copy);
		var root = EventContext.Create(_player, null);
		var attack = new AttackInstance { Attacker = _player, BaseDamage = 100, CanCrit = false };
		for (int i = 0; i < 5; i++)
		{
			var target = Target(19.4f + i * 0.3f);
			ContextHit(target, attack, root.Child(_player, target)!);
		}
		Check(OwnKills == 5 && _attacks.Count == 1 && _killFrames.Distinct().Count() == 1 && _kills.Select(kill => kill.Context?.ChainId).Distinct().Count() == 1,
			"另用同一根上下文的五次真实ReceiveHit致死命中验证同链，不伪造Kill事件");
		Check(Movement.ExtraAirJumps == 5, $"纯事实计数同链五次均计入，实读{Movement.ExtraAirJumps}，不受攻击连锁四次门影响");
		await Reset(); Grant(copy);
		var lastTarget = Target(20);
		EventContext edge = EventContext.Create(_player, lastTarget);
		for (int i = 0; i < EventContext.MaxDepth; i++) edge = edge.Child(_player, lastTarget)!;
		ContextHit(lastTarget, attack, edge);
		Check(OwnKills == 1 && _kills.Single().Context?.Depth == EventContext.MaxDepth, "合法链深度末层真实造成死亡与Kill事实");
		Check(Movement.ExtraAirJumps == 1, "纯事实计数记录合法末层击杀，不把攻击递归深度门套在跳数上");
	}
	private void ContextHit(Unit target, AttackInstance attack, EventContext context)
	{
		target.ReceiveHit(new HitInstance { Target = target, Attacker = _player, Attack = attack, Context = context,
			Damage = attack.BaseDamage, Direction = Vector3.Forward, Position = target.GlobalPosition });
	}
	private async Task TableParameters()
	{
		await Reset(); using var copy = Copy(2, 5); Grant(copy); BurstTargets(3); await Wait(0.05); await MeleeBurst(3);
		Check(Movement.ExtraAirJumps == 5, "只改表每杀增加2／上限5后，真实三杀依次增加并封顶5");
		string description = RewardEffectRegistry.Compile(copy)!.Description;
		Check(description.Contains('2') && description.Contains('5') && !description.Contains('{'), $"增量与封顶说明同步表内参数：{description}");
	}
	private async Task ExistingPassive()
	{
		await Reset(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
		Check(Movement.ExtraAirJumps == 1, "A灵动原有一次额外空中跳跃保持"); Grant();
		var attack = new AttackInstance { Attacker = _player, BaseDamage = 100, CanCrit = false };
		Hit(_player, Target(19.8f), 100, attack); Hit(_player, Target(20.2f), 100, attack);
		Check(OwnKills == 2 && _attacks.Count == 1, "A同一真实伤害快照造成两次本人击杀");
		Check(Movement.ExtraAirJumps == 3 && Movement.AirJumpsLeft == 3, $"保留灵动并逐杀立即叠加1→3，容量{Movement.ExtraAirJumps}／可用{Movement.AirJumpsLeft}");
	}
	private async Task AirRefill()
	{
		await Reset(); Grant(); BurstTargets(3); await Wait(0.05); await MeleeBurst(3);
		await Jump(); for (int i = 0; i < 3; i++) await Jump();
		Check(!Movement.IsGrounded && Movement.ExtraAirJumps == 3 && Movement.AirJumpsLeft == 0 && _successfulJumps == 4, "空中用尽三次额外跳跃，最大容量仍3");
		Hit(_player, Target(20), 100);
		Check(!Movement.IsGrounded && Movement.ExtraAirJumps == 3 && Movement.AirJumpsLeft == 1, "已达容量上限时空中击杀仍立即补回一次，不等落地");
		int before = _successfulJumps; await Jump();
		Check(_successfulJumps == before + 1 && Movement.AirJumpsLeft == 0 && !Movement.IsGrounded, "补回的次数立刻可由真实空中跳消耗");
		for (int i = 0; i < 4; i++) Hit(_player, Target(19.5f + i * 0.3f), 100);
		Check(!Movement.IsGrounded && Movement.ExtraAirJumps == 3 && Movement.AirJumpsLeft == 3, "空中同帧四杀逐次补回并封顶3，不溢出");
		await Jump(); await Jump(); Hit(_player, Target(20), 100);
		Check(!Movement.IsGrounded && Movement.AirJumpsLeft == 2, "剩一次时击杀只增加一次到2，不直接回满");
		before = _successfulJumps; await Jump(); await Jump();
		Check(_successfulJumps == before + 2 && Movement.AirJumpsLeft == 0, "补充后两次真实空中跳正确消耗");
		before = _successfulJumps; await Jump();
		Check(_successfulJumps == before && Movement.AirJumpsLeft == 0, "补回次数耗尽后仍不能无限空中跳");

		await Reset(); using var copy = Copy(2, 5); Grant(copy);
		_player.GlobalPosition += Vector3.Up * 4; _player.Velocity = Vector3.Zero; await Until(() => !Movement.IsGrounded);
		Hit(_player, Target(20), 100);
		Check(!Movement.IsGrounded && Movement.ExtraAirJumps == 2 && Movement.AirJumpsLeft == 2, "只改表每杀2／上限5，空中首次击杀立即增加容量与可用次数2");
		Hit(_player, Target(19.8f), 100); Hit(_player, Target(20.2f), 100);
		Check(Movement.ExtraAirJumps == 5 && Movement.AirJumpsLeft == 5, "副本空中三杀同帧补充到5，容量／剩余共同封顶");

		await Reset(); _player.GlobalPosition += Vector3.Up * 4; _player.Velocity = Vector3.Zero; await Until(() => !Movement.IsGrounded);
		Hit(_player, Target(20), 100);
		Check(!Movement.IsGrounded && OwnKills == 1 && Movement.ExtraAirJumps == 0 && Movement.AirJumpsLeft == 0, "没有增幅时空中真实击杀也不凭空补跳");
	}
}
