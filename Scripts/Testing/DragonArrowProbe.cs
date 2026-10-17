using System;
using System.Collections.Generic;
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

/// <summary>
/// J9「龙之箭」专项验收：**真实按键蓄力 → 真实释放 ATK 发射 → 真实命中**。
///
/// <para>验四件事（全部读真实事件 / 真实投射物 / 真实 `Unit.Damaged`）：</para>
/// <list type="number">
/// <item>表里的阈值（2s）/ 尺寸（×2）/ 穿透数（99）真的落到了技能节点上（强类型资源，全在表里）；</item>
/// <item>**松手阈值**：0.6s / 1.9s 都是普通箭（只中最近 1 个靶），2.05s 才是大箭；</item>
/// <item>**全穿透**：同一发大箭的**同一个 AttackInstance** 依次打中 3 个靶（T1→T2→T3）；</item>
/// <item>**不穿 World**：同一发大箭打中墙前靶之后被墙挡住，墙后靶一次都没被打到。</item>
/// </list>
///
/// <para>另有一条"不锁存"的证据：大箭之后再来一发 0.6s → 又只是普通箭（按**松手那一刻**判定，
/// 不是"过了 2 秒就永久变强"）。</para>
/// </summary>
public partial class DragonArrowProbe : Node3D
{
	private int _pass, _fail;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private ArrowVolleyAbility _bow = null!;
	private readonly PlayerLoadout _loadout = new();
	private readonly RunBuild _build = new();

	private readonly List<Projectile> _spawned = new();
	private readonly List<float> _spawnScale = new();
	private readonly List<int> _spawnPierce = new();
	private readonly List<string> _hitOrder = new();
	private readonly List<float> _hitDamage = new();
	private readonly List<AttackInstance?> _hitAttacks = new();
	private readonly List<ulong> _hitFrames = new();

	private Projectile? _watched;
	private Vector3 _watchedLastPosition;
	private double _elapsed;

	public override void _Process(double delta)
	{
		_elapsed += delta;
		_build.TickTriggers((float)delta);
		Watch();
	}

	// 物理帧也记一次"投射物最后位置"：投射物是在它自己的 `_PhysicsProcess` 里撞墙消失的，
	// 本节点在树里排在它前面（父节点先派发），所以这里拿到的是**撞击前一帧**的位置。
	public override void _PhysicsProcess(double delta) => Watch();

	private void Watch()
	{
		if (_watched != null && IsInstanceValid(_watched))
		{
			_watchedLastPosition = _watched.GlobalPosition;
		}
	}

	private void Press(string action, bool pressed)
		=> Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });

	private async Task Wait(double seconds)
		=> await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

	private async Task<bool> Until(Func<bool> predicate, double seconds = 4)
	{
		double start = _elapsed;
		while (!predicate() && _elapsed - start < seconds)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		return predicate();
	}

	private void Check(bool ok, string text)
	{
		if (ok)
		{
			_pass++;
		}
		else
		{
			_fail++;
		}

		GD.Print($"[DragonArrow] {(ok ? "PASS" : "FAIL")} {text}");
	}

	/// <summary>投射物出生时的缩放 / 穿透预算（NodeAdded 时属性还没写完，推迟到本帧末尾读）。</summary>
	private void Added(Node node)
	{
		if (node is not Projectile projectile)
		{
			return;
		}

		_spawned.Add(projectile);
		_watched = projectile;
		_watchedLastPosition = projectile.GlobalPosition;
		Callable.From(() =>
		{
			if (IsInstanceValid(projectile))
			{
				_spawnScale.Add(projectile.Scale.X);
				_spawnPierce.Add(projectile.Pierce);
			}
			else
			{
				_spawnScale.Add(-1f);
				_spawnPierce.Add(-1);
			}
		}).CallDeferred();
	}

	private Unit MakeTarget(string name, Vector3 position)
	{
		var unit = new Unit
		{
			Name = name,
			Faction = Faction.Enemy,
			MaxHealth = 100000f,
			KnockbackResistance = 1f,
		};
		unit.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 1f } });
		AddChild(unit);
		unit.GlobalPosition = position;
		unit.Damaged += (result, hit) =>
		{
			_hitOrder.Add(unit.Name.ToString());
			_hitDamage.Add(result.FinalDamage);
			_hitAttacks.Add(hit.Attack);
			_hitFrames.Add(Engine.GetPhysicsFrames());
		};
		return unit;
	}

	private void MakeWall(Vector3 position)
	{
		var body = new StaticBody3D
		{
			Name = "ProbeWall",
			CollisionLayer = CombatLayers.World,
			CollisionMask = 0,
		};
		body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(12f, 5f, 1f) } });
		AddChild(body);
		body.GlobalPosition = position;
	}

	public override async void _Ready()
	{
		try
		{
			await Wait(0.2);
			RunSetup.HasSelection = true;
			RunSetup.Character = PlayerCharacterKind.Hunter;
			RunSetup.Weapon = WeaponPresetKind.Bow;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>();
			AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true };
			AddChild(_camera);
			_player.GlobalPosition = new Vector3(20f, 0.05f, 20f);
			_player.Velocity = Vector3.Zero;
			_camera.GlobalPosition = new Vector3(20f, 0.95f, 26f);
			_camera.LookAt(new Vector3(20f, 0.95f, 10f));
			_camera.ResetPhysicsInterpolation();
			_loadout.Apply(_player, this, PlayerCharacterKind.Hunter, WeaponPresetKind.Bow);
			_bow = (ArrowVolleyAbility)PlayerAbilityInput.FindByInputAction(_player, "weapon_skill_1")!;
			GetTree().NodeAdded += Added;
			await Wait(0.3);
			await TableAndGrant();
			await PiercingShots();
			await WorldStop();
		}
		catch (Exception ex)
		{
			Check(false, ex.ToString());
		}

		GetTree().NodeAdded -= Added;
		GD.Print($"[DragonArrow] === 龙之箭验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}

	/// <summary>① 表 → 机制：强类型资源里的四个数字真的接到了这个技能节点上。</summary>
	private async Task TableAndGrant()
	{
		// 改前的默认值（不配这条进化时行为必须逐位不变）：上限 1s = 5 档、阈值/尺寸/穿透全 0。
		Check(
			Mathf.IsEqualApprox(_bow.ChargeMaxSeconds, 1f)
				&& _bow.ChargeMaxStepsEffective == 5
				&& Mathf.IsEqualApprox(_bow.ChargedShotThresholdSeconds, 0f)
				&& Mathf.IsEqualApprox(_bow.ChargedShotSizeScale, 0f)
				&& _bow.ChargedShotPierce == 0,
			$"改前默认：蓄力上限 {_bow.ChargeMaxSeconds}s（{_bow.ChargeMaxStepsEffective} 档）阈值 {_bow.ChargedShotThresholdSeconds} 尺寸 {_bow.ChargedShotSizeScale} 穿透 {_bow.ChargedShotPierce}（期望 1s/5档/0/0/0）");

		RewardConfig? row = ConfigRows.ById<RewardConfig>("evo_dragon_arrow");
		Check(row != null, "表行 evo_dragon_arrow 已加载");
		ChargedShotTuning? tuning = row?.ChargedShot;
		Check(
			tuning != null && tuning.TargetAbility?.Id == "skl_a3_w1_1",
			$"行上的强类型 ChargedShot 指向弓武技1：{tuning?.TargetAbility?.Id}");
		Check(
			tuning != null
				&& Mathf.IsEqualApprox(tuning.MaxChargeSeconds, 3f)
				&& Mathf.IsEqualApprox(tuning.ThresholdSeconds, 2f)
				&& Mathf.IsEqualApprox(tuning.SizeScale, 2f)
				&& tuning.PierceCount == 99
				&& tuning.MaxSteps == 0,
			$"表里的数字：蓄力上限 {tuning?.MaxChargeSeconds}s 阈值 {tuning?.ThresholdSeconds}s 尺寸 ×{tuning?.SizeScale} 穿透 {tuning?.PierceCount} 档数上限 {tuning?.MaxSteps}（0=不限）");

		RewardDefinition? definition = RewardCatalog.Find("evo_dragon_arrow");
		Check(definition != null, "RewardCatalog 编译出了这一行（ApplyHandler=ChargedShot 已注册）");
		if (definition != null)
		{
			_build.Add(definition, new RewardContext
			{
				Player = _player,
				Weapon = NodeSearch.FindInSubtree<Weapon>(_player)!,
				Build = _build,
			});
		}

		Check(_build.Has("evo_dragon_arrow"), "通过真实 RunBuild.Add（生产里抽中后入构筑的同一条路）拿到龙之箭");
		Check(
			Mathf.IsEqualApprox(_bow.ChargeMaxSeconds, 3f)
				&& _bow.ChargeMaxStepsEffective == 25
				&& Mathf.IsEqualApprox(_bow.ChargedShotThresholdSeconds, 2f)
				&& Mathf.IsEqualApprox(_bow.ChargedShotSizeScale, 2f)
				&& _bow.ChargedShotPierce == 99,
			$"拿到之后技能节点：蓄力上限 {_bow.ChargeMaxSeconds}s（{_bow.ChargeMaxStepsEffective} 档）阈值 {_bow.ChargedShotThresholdSeconds}s 尺寸 ×{_bow.ChargedShotSizeScale} 穿透 {_bow.ChargedShotPierce}");

		float fullPower = 1f + (_bow.ChargeStepScale - 1f) * _bow.ChargeMaxStepsEffective;
		float atTwo = 1f + (_bow.ChargeStepScale - 1f) * TiersAt(2f);
		GD.Print(
			$"[DragonArrow] INFO 口径（交接单点名的歧义）：上限 {_bow.ChargeMaxSeconds:0.##}s → 满蓄 {_bow.ChargeMaxStepsEffective} 档 = ×{fullPower:0.##}"
			+ $"（每 {_bow.ChargeStepSecondsEffective:0.##}s 仍加一档）；2.0s 处 = {TiersAt(2f)} 档 ×{atTwo:0.##}。"
			+ (Mathf.Abs(atTwo - fullPower) > 0.001f
				? "→ 现在是「继续加档到 3 秒」，2 秒只是大箭阈值；要改成「2 秒后不再加档」把 MaxSteps 填 15 即可（不改 C#）。"
				: "→ 现在是「2 秒后不再加档」。"));
		await Wait(0.05);
	}

	private int TiersAt(float seconds)
	{
		float baseSeconds = _bow.ChargeBaseSecondsEffective;
		float stepSeconds = _bow.ChargeStepSecondsEffective;
		return stepSeconds <= 0f || seconds <= baseSeconds
			? 0
			: Mathf.FloorToInt((seconds - baseSeconds) / stepSeconds);
	}

	/// <summary>真实按键蓄力到 <paramref name="holdSeconds"/> → 松手 → 等真实释放 ATK 出弹 → 等自然收招。</summary>
	private async Task Fire(double holdSeconds, string label)
	{
		int before = _spawned.Count;
		await Until(() => _bow.CooldownRemaining <= 0.0001f, 6);
		Press(_bow.InputAction, true);
		bool reached = await Until(() => _bow.IsCharging && _bow.ChargeTime >= holdSeconds, 6);
		float pressed = _bow.ChargeTime;
		Check(reached, $"{label}：真实按键蓄到 {holdSeconds:0.00}s（实际 {pressed:0.###}s）");
		Press(_bow.InputAction, false);
		await Wait(0.03);

		// 松手之后 `ChargeReleased` 锁住了累加 → 这一刻的 ChargeTime 就是"松手结果"（冻结值）。
		float frozen = _bow.ChargeTime;
		Check(_spawned.Count == before, $"{label}：松手后仍等释放段 ATK，不立即出弹");
		bool fired = await Until(() => _spawned.Count > before, 3);
		Check(fired, $"{label}：真实释放 ATK 才发射");
		await Until(() => !_bow.IsActive, 3);
		Check(
			Mathf.IsEqualApprox(_bow.LastShotChargeSeconds, frozen),
			$"{label}：这一发的判据是**松手快照** {_bow.LastShotChargeSeconds:0.###}s（松手冻结值 {frozen:0.###}s，按住期间 {pressed:0.###}s）");
	}

	/// <summary>
	/// 等这一发该结算的都结算完（或投射物自己消失）再关掉本段窗口 ——
	/// 否则上一发的箭会飞进下一段的命中列表（大箭 670m/s ≈ 11m/物理帧，到第二个靶要好几帧）。
	/// </summary>
	private async Task Settle(int expectedHits, int hitStart)
	{
		await Until(
			() => _hitOrder.Count - hitStart >= expectedHits || _watched == null || !IsInstanceValid(_watched),
			4.5);
		await Wait(0.06);
	}

	private string HitsSince(int start)
		=> _hitOrder.Count <= start ? "（无）" : string.Join(" → ", _hitOrder.GetRange(start, _hitOrder.Count - start));

	private string DamagesSince(int start)
		=> _hitDamage.Count <= start ? "（无）" : string.Join(" / ", _hitDamage.GetRange(start, _hitDamage.Count - start).ConvertAll(d => d.ToString("0.##")));

	/// <summary>②③④ 阈值边界 + 全穿透 + 不锁存。</summary>
	private async Task PiercingShots()
	{
		// 三个靶在 x=20 这条直线上；间距 18m 远大于"一帧飞行距离"（2s 大箭 670m/s ≈ 11.2m/帧），
		// 这样每个靶都必然落在**不同**的一帧扫掠里（穿透的真实语义：一箭连续命中多个目标）。
		var t1 = MakeTarget("T1", new Vector3(20f, 0.9f, 11.4f));
		var t2 = MakeTarget("T2", new Vector3(20f, 0.9f, -6.6f));
		var t3 = MakeTarget("T3", new Vector3(20f, 0.9f, -24.6f));
		await Wait(0.15);

		// ① 0.6s：普通箭（阈值 2s 没到）→ 只中最近的 T1
		int start = _hitOrder.Count;
		int spawn = _spawned.Count;
		await Fire(0.6, "① 蓄 0.6s");
		await Settle(1, start);
		Check(
			!_bow.LastShotChargedShot && _spawnScale.Count > spawn && Mathf.IsEqualApprox(_spawnScale[spawn], 1f) && _spawnPierce[spawn] == 0,
			$"① 小箭：不是大箭={!_bow.LastShotChargedShot} 尺寸 ×{ArrowScale(spawn)} 穿透预算 {ArrowPierce(spawn)}（期望 False/×1/0）");
		Check(
			_hitOrder.Count - start == 1 && HitsSince(start) == "T1",
			$"① 小箭只中最近 1 个靶：命中顺序 {HitsSince(start)}，伤害 {DamagesSince(start)}");

		// ② 1.9s：仍然没过 2s 阈值 → 还是普通箭（证明判据是 2s，不是"超过 1s 上限就算"）
		start = _hitOrder.Count;
		spawn = _spawned.Count;
		await Fire(1.9, "② 蓄 1.9s");
		await Settle(1, start);
		Check(
			!_bow.LastShotChargedShot && _spawnScale.Count > spawn && Mathf.IsEqualApprox(_spawnScale[spawn], 1f) && _spawnPierce[spawn] == 0,
			$"② 1.9s < 阈值：仍不是大箭={!_bow.LastShotChargedShot} 尺寸 ×{ArrowScale(spawn)} 穿透预算 {ArrowPierce(spawn)}（期望 False/×1/0）");
		Check(
			_hitOrder.Count - start == 1 && HitsSince(start) == "T1",
			$"② 1.9s 只中最近 1 个靶：命中顺序 {HitsSince(start)}，伤害 {DamagesSince(start)}");

		// ③ 2.05s：过大箭阈值 → 同一发箭穿过三个靶
		start = _hitOrder.Count;
		spawn = _spawned.Count;
		await Fire(2.05, "③ 蓄 2.05s（大箭）");
		await Settle(3, start);
		Check(
			_bow.LastShotChargedShot && _spawnScale.Count > spawn && Mathf.IsEqualApprox(_spawnScale[spawn], 2f) && _spawnPierce[spawn] == 99,
			$"③ 大箭：是大箭={_bow.LastShotChargedShot} 尺寸 ×{ArrowScale(spawn)} 穿透预算 {ArrowPierce(spawn)}（期望 True/×2/99）");
		Check(
			_hitOrder.Count - start == 3 && HitsSince(start) == "T1 → T2 → T3",
			$"③ 一箭穿三个靶：命中顺序 {HitsSince(start)}，伤害 {DamagesSince(start)}");
		Check(
			_hitOrdersMatchOneAttack(start),
			"③ 三次命中是**同一个 AttackInstance**（同一发箭），不是三箭");
		Check(
			_hitFrames.Count > start + 2 && _hitFrames[start + 2] > _hitFrames[start],
			"③ 三次命中分布在不同物理帧（扫掠逐帧推进，不是同一帧重复结算）"
				+ $"（帧间隔 {(_hitFrames.Count > start + 2 ? (long)_hitFrames[start + 1] - (long)_hitFrames[start] : 0L)}/{(long)(_hitFrames.Count > start + 2 ? _hitFrames[start + 2] - _hitFrames[start + 1] : 0L)}）");
		bool survived = _watched != null && IsInstanceValid(_watched);
		GD.Print($"[DragonArrow] INFO ③ 第三个靶之后箭还活着={survived}（MaxLifetime {_bow.MaxLifetime}s，说明它没在最后一个靶上消失）");
		HitTable(start);

		// ④ 大箭之后马上再来一发 0.6s：又只是普通箭 → 判据不锁存（按松手那一刻算）
		start = _hitOrder.Count;
		spawn = _spawned.Count;
		await Fire(0.6, "④ 大箭后再蓄 0.6s");
		await Settle(1, start);
		Check(
			!_bow.LastShotChargedShot && _spawnScale.Count > spawn && Mathf.IsEqualApprox(_spawnScale[spawn], 1f) && _spawnPierce[spawn] == 0,
			$"④ 不锁存：大箭之后 0.6s 又回到小箭={!_bow.LastShotChargedShot} 尺寸 ×{ArrowScale(spawn)} 穿透预算 {ArrowPierce(spawn)}（期望 False/×1/0）");
		Check(
			_hitOrder.Count - start == 1 && HitsSince(start) == "T1",
			$"④ 小箭只中最近 1 个靶：命中顺序 {HitsSince(start)}，伤害 {DamagesSince(start)}");

		t1.QueueFree();
		t2.QueueFree();
		t3.QueueFree();
		await Wait(0.15);
	}

	private float ArrowScale(int index) => _spawnScale.Count > index ? _spawnScale[index] : -1f;

	private int ArrowPierce(int index) => _spawnPierce.Count > index ? _spawnPierce[index] : -1;

	private bool _hitOrdersMatchOneAttack(int start)
	{
		if (_hitAttacks.Count < start + 3)
		{
			return false;
		}

		AttackInstance? first = _hitAttacks[start];
		return first != null
			&& ReferenceEquals(first, _hitAttacks[start + 1])
			&& ReferenceEquals(first, _hitAttacks[start + 2])
			&& first.Pierce == 99
			&& first.Ability == "skl_a3_w1_1";
	}

	private void HitTable(int start)
	{
		for (int i = start; i < _hitOrder.Count; i++)
		{
			ulong frame = i < _hitFrames.Count ? _hitFrames[i] : 0UL;
			GD.Print($"[DragonArrow] INFO ③ 命中#{i - start + 1} 靶 {_hitOrder[i]} 伤害 {_hitDamage[i]:0.##} 暴击快照 {(_hitAttacks.Count > i && _hitAttacks[i] != null ? _hitAttacks[i]!.BaseDamage.ToString("0.##") : "?")} 帧 {frame}");
		}
	}

	/// <summary>⑤ 射向 World：大箭打中墙前靶之后被墙挡住，墙后靶一次都没被打到。</summary>
	private async Task WorldStop()
	{
		MakeWall(new Vector3(20f, 2.5f, 8f));
		var front = MakeTarget("W_Front", new Vector3(20f, 0.9f, 14f));
		var behind = MakeTarget("W_Behind", new Vector3(20f, 0.9f, 2f));
		await Wait(0.25);

		int start = _hitOrder.Count;
		int spawn = _spawned.Count;
		await Fire(2.05, "⑤ 大箭射向 World 墙");
		await Settle(1, start);
		await Until(() => !IsInstanceValid(_watched), 1.5);
		await Wait(0.05);

		Check(
			_bow.LastShotChargedShot && _spawnPierce.Count > spawn && _spawnPierce[spawn] == 99,
			$"⑤ 这一发确实是全穿透的大箭（穿透预算 {ArrowPierce(spawn)}）");
		Check(
			_hitOrder.Count - start == 1 && HitsSince(start) == "W_Front",
			$"⑤ 只打中墙**前**的靶：命中顺序 {HitsSince(start)}，伤害 {DamagesSince(start)}");
		Check(
			Mathf.IsEqualApprox(behind.Health, behind.MaxHealth),
			$"⑤ 墙**后**的靶一次都没被打到：生命 {behind.Health:0.##}/{behind.MaxHealth:0.##}（穿透没有穿过地形）");
		float wallNear = 7.5f;
		float wallFar = 8.5f;
		Check(
			_watchedLastPosition.Z >= wallNear - 0.01f && _watchedLastPosition.Z <= wallFar + 0.01f
				&& _watchedLastPosition.Z > behind.GlobalPosition.Z,
			$"⑤ 箭停在墙里：最后位置 {_watchedLastPosition}（墙 z∈[{wallNear:0.##},{wallFar:0.##}]，墙后靶 z={behind.GlobalPosition.Z:0.##}）");
		Check(!IsInstanceValid(_watched), "⑤ 箭撞墙后消失（不是穿墙继续飞）");
		GD.Print($"[DragonArrow] INFO ⑤ 场上存活投射物 {Projectile.LiveCount}");
		front.QueueFree();
		behind.QueueFree();
		await Wait(0.1);
	}
}
