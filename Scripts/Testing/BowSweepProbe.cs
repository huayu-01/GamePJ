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
/// J10 弓·档位＋弹速＋"快弹不漏怪"专项验收（**真实按键蓄力 → 真实释放 ATK → 真实命中**）。
///
/// <para>五段，全部读真实事件 / 真实投射物 / 真实 <c>Unit.Damaged</c>：</para>
/// <list type="number">
/// <item><b>表 → 机制</b>：step 0.3 / 上限 1.4s（3 个后置档）/ 弹速 160＋每档 20 / 扫掠预算 8；</item>
/// <item><b>基础蓄力期间不加档</b>：按到 0.35s（&lt; 0.5s）读 <c>ChargeSteps=0</c>、<c>ChargePowerScale=1</c>，
///   且这一按松手**不释放**；再打两发真实箭：刚过基础那发一点档位加成都不吃，过了 1 档才开始涨；</item>
/// <item><b>其他蓄力技能的档数口径</b>：短弩武技2（3 后置档）与长枪武技2（4 后置档）按各自表列现算；</item>
/// <item><b>龙之箭</b>：真实入构筑后 3s 上限 = 8 个后置档；</item>
/// <item><b>贴脸双怪</b>：同一条单帧线段里的两只怪 —— 关掉扫掠（既有路径）只结算最近那只，
///   开扫掠（表值 8）两只都在**同一物理帧**结算、不重复；墙夹在两只中间时墙后那只一次都打不到。</item>
/// </list>
/// </summary>
public partial class BowSweepProbe : Node3D
{
	private int _pass, _fail;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private ArrowVolleyAbility _bow = null!;
	private readonly PlayerLoadout _loadout = new();
	private readonly RunBuild _build = new();

	private readonly List<Projectile> _spawned = new();
	private readonly List<int> _spawnSweep = new();
	private readonly List<string> _hits = new();
	private readonly List<float> _hitDamage = new();
	private readonly List<ulong> _hitFrames = new();
	private double _elapsed;

	public override void _Process(double delta) => _elapsed += delta;

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

		GD.Print($"[BowSweep] {(ok ? "PASS" : "FAIL")} {text}");
	}

	/// <summary>投射物出生时的扫掠预算（NodeAdded 时属性还没写完，推迟到本帧末尾读）。</summary>
	private void Added(Node node)
	{
		if (node is not Projectile projectile)
		{
			return;
		}

		_spawned.Add(projectile);
		Callable.From(() => _spawnSweep.Add(IsInstanceValid(projectile) ? projectile.SweepMaxHits : -1)).CallDeferred();
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
		unit.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.5f } });
		AddChild(unit);
		unit.GlobalPosition = position;
		unit.Damaged += (result, hit) =>
		{
			_hits.Add(unit.Name.ToString());
			_hitDamage.Add(result.FinalDamage);
			_hitFrames.Add(Engine.GetPhysicsFrames());
		};
		return unit;
	}

	private void MakeWall(Vector3 position, Vector3 size)
	{
		var body = new StaticBody3D
		{
			Name = "ProbeWall",
			CollisionLayer = CombatLayers.World,
			CollisionMask = 0,
		};
		body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
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
			await TableToMechanism();
			await BaseChargeNeutral();
			await OtherChargeRows();
			await GrantDragonArrow();
			await DualTargetSweep();
		}
		catch (Exception ex)
		{
			Check(false, ex.ToString());
		}

		GetTree().NodeAdded -= Added;
		GD.Print($"[BowSweep] === J10 弓·档位/弹速/扫掠 验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}

	/// <summary>① 表 → 机制（还没拿龙之箭时的默认行）。</summary>
	private async Task TableToMechanism()
	{
		float dmgMul = _player.Stats.Get("DamageMultiplier");
		int tiers = _bow.ChargeMaxStepsEffective;
		Check(
			Mathf.IsEqualApprox(_bow.ChargeBaseSeconds, 0.5f)
				&& Mathf.IsEqualApprox(_bow.ChargeStepSeconds, 0.3f)
				&& Mathf.IsEqualApprox(_bow.ChargeStepScale, 1.3f)
				&& Mathf.IsEqualApprox(_bow.ChargeMaxSeconds, 1.4f)
				&& tiers == 3,
			$"表→机制·蓄力：基础 {_bow.ChargeBaseSeconds:0.##}s / 每 {_bow.ChargeStepSeconds:0.##}s 一档 / 每档 ×{_bow.ChargeStepScale:0.##}（**加算**：1 + {_bow.ChargeStepScale - 1f:0.##}×档）/ 上限 {_bow.ChargeMaxSeconds:0.##}s → {tiers} 个后置档（基础 1 + 后置 {tiers} = {tiers + 1} 个威力档）");
		Check(
			Mathf.IsEqualApprox(_bow.Speed, 160f) && Mathf.IsEqualApprox(_bow.SpeedPerChargeStep, 20f),
			$"表→机制·弹速：基础 {_bow.Speed:0.##} m/s / 每档 +{_bow.SpeedPerChargeStep:0.##} → 满档 {_bow.Speed + (_bow.SpeedPerChargeStep * tiers):0.##}（J10 预设值，等用户确认）");
		Check(_bow.SweepMaxHits == 8, $"表→机制·扫掠：技能行 SweepMaxHits={_bow.SweepMaxHits}（期望 8）已套到技能节点上");

		// 威力阶梯（公式是**加法**：1 + (Scale-1) × 档数，与之前"弓五档 2.5 倍/长枪四档 1.8 倍"一致）。
		for (int s = 0; s <= tiers; s++)
		{
			float power = 1f + (_bow.ChargeStepScale - 1f) * s;
			GD.Print($"[BowSweep] INFO 威力阶梯：后置 {s} 档（蓄 {0.5f + (0.3f * s):0.##}s）→ 威力 ×{power:0.###} 单发伤害 {_bow.Damage * power * dmgMul:0.##} 弹速 {_bow.Speed + (_bow.SpeedPerChargeStep * s):0.##}");
		}

		await Wait(0.05);
	}

	/// <summary>真实按键蓄到 <paramref name="holdSeconds"/> 以上 → 松手 → 等真实释放 ATK 出弹 → 等自然收招。</summary>
	private async Task<bool> FireHolding(double holdSeconds)
	{
		int before = _spawned.Count;
		await Until(() => _bow.CooldownRemaining <= 0.0001f, 6);
		Press(_bow.InputAction, true);
		bool reached = await Until(() => _bow.IsCharging && _bow.ChargeTime >= holdSeconds, 6);
		Press(_bow.InputAction, false);
		bool fired = await Until(() => _spawned.Count > before, 3);
		await Until(() => !_bow.IsActive, 3);
		await Wait(0.03);
		return reached && fired;
	}

	/// <summary>② 基础蓄力期间到底加不加档（用户新要求：基础蓄力不要让后续蓄力同时生效）。</summary>
	private async Task BaseChargeNeutral()
	{
		float dmgMul = _player.Stats.Get("DamageMultiplier");

		// ⓐ 停在基础蓄力**完成之前**：档数必须是 0、威力必须是 1。
		int before = _spawned.Count;
		await Until(() => _bow.CooldownRemaining <= 0.0001f, 6);
		Press(_bow.InputAction, true);
		bool mid = await Until(() => _bow.IsCharging && _bow.ChargeTime >= 0.35f, 6);
		float held = _bow.ChargeTime;
		int stepsMid = _bow.ChargeSteps;
		float powerMid = _bow.ChargePowerScale;
		float progress = _bow.ChargeProgress;
		Press(_bow.InputAction, false);
		await Wait(0.3);
		Check(
			mid && stepsMid == 0 && Mathf.IsEqualApprox(powerMid, 1f) && progress < 1f,
			$"② 基础蓄力期间不加档：按到 {held:0.###}s（< 基础 {_bow.ChargeBaseSecondsEffective:0.##}s）→ ChargeSteps {stepsMid}（期望 0）ChargePowerScale ×{powerMid:0.###}（期望 1）进度 {progress:0.###}（期望 <1）");
		Check(
			_bow.LastReleaseAborted && _spawned.Count == before,
			$"② 没蓄满就松手：不释放（LastReleaseAborted={_bow.LastReleaseAborted}）场上新弹体 {_spawned.Count - before} 个（期望 0）");
		await Until(() => !_bow.IsActive, 3);

		// ⓑ 真实箭：**刚过基础**那一发一点档位加成都不吃。
		bool okBase = await FireHolding(0.505);
		Check(
			okBase && _bow.ChargeSteps == 0 && Mathf.IsEqualApprox(_bow.LastShotDamage, _bow.Damage * dmgMul)
				&& Mathf.IsEqualApprox(_bow.LastShotSpeed, _bow.Speed),
			$"② 刚过基础就松手（真实按键 {_bow.LastShotChargeSeconds:0.###}s，档 {_bow.ChargeSteps}）：伤害 {_bow.LastShotDamage:0.###}（期望 {_bow.Damage * dmgMul:0.###} = 表值 {_bow.Damage:0} ×玩家倍率 {dmgMul:0.##}，**不含档位加成**）弹速 {_bow.LastShotSpeed:0.##}（期望 {_bow.Speed:0}）");

		// ⓒ 过 1 个后置档才开始涨。
		bool okOne = await FireHolding(0.5f + (_bow.ChargeStepSecondsEffective * 1f) + 0.005f);
		Check(
			okOne && _bow.ChargeSteps == 1
				&& Mathf.IsEqualApprox(_bow.LastShotDamage, _bow.Damage * 1.3f * dmgMul)
				&& Mathf.IsEqualApprox(_bow.LastShotSpeed, _bow.Speed + _bow.SpeedPerChargeStep),
			$"③ 过了第 1 个后置档（真实按键 {_bow.LastShotChargeSeconds:0.###}s）：伤害 {_bow.LastShotDamage:0.###}（期望 ×1.3 = {_bow.Damage * 1.3f * dmgMul:0.###}）弹速 {_bow.LastShotSpeed:0.##}（期望 {_bow.Speed + _bow.SpeedPerChargeStep:0.##}）→ 增长从**基础蓄力之后**才开始");

		await Until(() => Projectile.LiveCount == 0, 4);
		await Wait(0.1);
	}

	/// <summary>③ 其他蓄力技能的档数口径（按各自表列现算，不改它们）。</summary>
	private async Task OtherChargeRows()
	{
		AbilityConfig? crossbow = ConfigRows.ById<AbilityConfig>("skl_a2_w1_2");
		AbilityConfig? spear = ConfigRows.ById<AbilityConfig>("skl_b3_w1_2");
		Check(crossbow != null, "表行 skl_a2_w1_2（短弩武技2）已加载");
		Check(spear != null, "表行 skl_b3_w1_2（长枪武技2）已加载");
		if (crossbow == null || spear == null)
		{
			return;
		}

		int crossbowTiers = PostTiers(crossbow);
		Check(
			Mathf.IsEqualApprox(crossbow.ChargeBaseSeconds, 0.4f) && Mathf.IsEqualApprox(crossbow.ChargeStepSeconds, 0.2f)
				&& crossbow.ChargeMaxSteps == 3 && crossbowTiers == 3,
			$"短弩武技2：基础 {crossbow.ChargeBaseSeconds:0.##}s + 每 {crossbow.ChargeStepSeconds:0.##}s × {crossbowTiers} 个后置档（ChargeMaxSteps={crossbow.ChargeMaxSteps}）→ 基础 1 + 后置 {crossbowTiers} = {crossbowTiers + 1} 个**发数**档（每档 +{crossbow.Param("CountPerChargeStep", 0f):0} 支）；行里没填 ChargeStepScale（节点上保持 1）→ 不分伤害档，**与用户口径一致、不需要改**");

		int spearTiers = PostTiers(spear);
		float spearPower = 1f + (spear.ChargeStepScale - 1f) * spearTiers;
		Check(
			Mathf.IsEqualApprox(spear.ChargeBaseSeconds, 1.0f) && Mathf.IsEqualApprox(spear.ChargeStepSeconds, 0.25f)
				&& Mathf.IsEqualApprox(spear.ChargeMaxSeconds, 2.0f) && spearTiers == 4
				&& Mathf.IsEqualApprox(spearPower, 1.8f),
			$"长枪武技2：基础 {spear.ChargeBaseSeconds:0.##}s + (上限 {spear.ChargeMaxSeconds:0.##}s − 基础)/{spear.ChargeStepSeconds:0.##}s = {spearTiers} 个后置档 → 基础 1 + 后置 {spearTiers} = {spearTiers + 1} 档，威力 ×{spearPower:0.###}（它自己的 ChargeStepScale={spear.ChargeStepScale:0.##}，加算）；⚠️ 比用户新口径「基础+后置3=4档」多 1 档，**本批没改**，等用户确认");

		GD.Print("[BowSweep] INFO 档数口径：弓（本批）基础 1 + 后置 3 = 4 档；短弩武技2 基础 1 + 后置 3 = 4 档（发数档）；长枪武技2 基础 1 + 后置 4 = 5 档（原裁定「额外每.25四档」）");
		await Wait(0.05);
	}

	/// <summary>把表列按 <c>Ability.ChargeMaxStepsEffective</c> 的同一个公式算一遍（探针侧只读）。</summary>
	private static int PostTiers(AbilityConfig row)
	{
		if (row.ChargeStepSeconds <= 0f)
		{
			return 0;
		}

		int steps = row.ChargeMaxSteps > 0 ? row.ChargeMaxSteps : int.MaxValue;
		if (row.ChargeMaxSeconds > row.ChargeBaseSeconds)
		{
			steps = Mathf.Min(steps, (int)Mathf.Floor(((row.ChargeMaxSeconds - row.ChargeBaseSeconds) / row.ChargeStepSeconds) + 0.0001f));
		}

		return steps == int.MaxValue ? 0 : Mathf.Max(0, steps);
	}

	/// <summary>④ 龙之箭：真实入构筑（生产里抽中后同一条路），上限 3s 现在 = 8 个后置档。</summary>
	private async Task GrantDragonArrow()
	{
		RewardConfig? row = ConfigRows.ById<RewardConfig>("evo_dragon_arrow");
		Check(
			row?.ChargedShot != null && row.ChargedShot.TargetAbility?.Id == "skl_a3_w1_1"
				&& Mathf.IsEqualApprox(row.ChargedShot.MaxChargeSeconds, 3f)
				&& Mathf.IsEqualApprox(row.ChargedShot.ThresholdSeconds, 2f)
				&& row.ChargedShot.PierceCount == 99,
			$"龙之箭表行：指向 {row?.ChargedShot?.TargetAbility?.Id} 上限 {row?.ChargedShot?.MaxChargeSeconds}s 阈值 {row?.ChargedShot?.ThresholdSeconds}s 穿透 {row?.ChargedShot?.PierceCount}");

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

		int tiers = _bow.ChargeMaxStepsEffective;
		Check(
			_build.Has("evo_dragon_arrow") && Mathf.IsEqualApprox(_bow.ChargeMaxSeconds, 3f) && tiers == 8
				&& _bow.ChargedShotPierce == 99,
			$"通过 RunBuild.Add 拿到龙之箭后：上限 {_bow.ChargeMaxSeconds:0.##}s → {tiers} 个后置档（威力 ×{1f + (_bow.ChargeStepScale - 1f) * tiers:0.##}，大箭穿透 {_bow.ChargedShotPierce}）");

		DragonArrowInfo(tiers);
		await Wait(0.05);
	}

	private void DragonArrowInfo(int tiers)
	{
		float full = 1f + (_bow.ChargeStepScale - 1f) * tiers;
		float speed = _bow.Speed + (_bow.SpeedPerChargeStep * tiers);
		// 旧值（J9 交付时）：step=0.1 / 上限 3s = 25 个后置档、弹速 220+30×25。
		GD.Print(
			$"[BowSweep] INFO 龙之箭满蓄：现在 {tiers} 档 ×{full:0.##} 伤害 {_bow.Damage * full:0.##} 弹速 {speed:0.##}"
			+ "（J9 交付时 25 档 ×8.5 伤害 204 弹速 970；把 step 改成 0.3 之后它自己就降下来了）");
	}

	/// <summary>⑤ 贴脸双怪：同一帧线段里两只怪，既有路径漏一只、扫掠两只都在同一帧结算。</summary>
	private async Task DualTargetSweep()
	{
		var t1 = MakeTarget("T1", new Vector3(20f, 0.9f, 17.6f));
		var t2 = MakeTarget("T2", new Vector3(20f, 0.9f, 16.0f));
		await Wait(0.25);

		int tiers = _bow.ChargeMaxStepsEffective;
		float shotSpeed = _bow.Speed + (_bow.SpeedPerChargeStep * Mathf.Min(5f, tiers));
		float segment = shotSpeed / Engine.PhysicsTicksPerSecond;
		GD.Print(
			$"[BowSweep] INFO 贴脸双怪几何：蓄 2.05s 的大箭弹速 {shotSpeed:0.##} m/s → 单帧线段 {segment:0.###}m；"
			+ $"T1 z=17.6 / T2 z=16.0（半径 0.5，跨 z∈[15.5, 18.1]）都完整落在同一条线段里（枪口 z≈19.4 → 帧末 z≈{19.4f - segment:0.###}）");

		int tableSweep = _bow.SweepMaxHits;

		// ⓐ 对照组：把新列临时置 0 = 任何**没配**这一列的武器走的那条既有路径（改动前的行为）。
		_bow.SweepMaxHits = 0;
		int start = _hits.Count;
		int spawn = _spawned.Count;
		bool okControl = await FireHolding(2.05);
		await Settle(1, start);
		Check(
			okControl && _spawnSweep.Count > spawn && _spawnSweep[spawn] == 0,
			$"⑤ 对照组：真实投射物上的扫掠预算 {ArrowSweep(spawn)}（期望 0 = 不扫掠，既有路径）");
		Check(
			_hits.Count - start == 1 && HitsSince(start) == "T1",
			$"⑤ 关掉扫掠（= 改动前）：贴脸双怪只结算**最近那只** → 命中 {HitsSince(start)}，T2 一点血都没掉（{t2.Health:0.#}/{t2.MaxHealth:0.#}）");

		// ⓑ 表值生效：同一对靶、同一条线段 → 两只都结算，而且在**同一物理帧**。
		_bow.SweepMaxHits = tableSweep;
		start = _hits.Count;
		spawn = _spawned.Count;
		bool okSweep = await FireHolding(2.05);
		await Settle(2, start);
		Check(
			okSweep && _spawnSweep.Count > spawn && _spawnSweep[spawn] == tableSweep,
			$"⑤ 真实投射物上的扫掠预算 {ArrowSweep(spawn)}（期望表值 {tableSweep}）");
		Check(
			_hits.Count - start == 2 && HitsSince(start) == "T1 → T2",
			$"⑤⭐ 开扫掠：贴脸双怪**都打到了** → 命中顺序 {HitsSince(start)}，伤害 {DamagesSince(start)}");
		Check(
			_hitFrames.Count > start + 1 && _hitFrames[start] == _hitFrames[start + 1],
			$"⑤⭐ 两次命中在**同一物理帧**（帧 {FramesSince(start)}）：不是漏怪后靠下一帧补打");
		Check(
			CountIn(start, "T1") == 1 && CountIn(start, "T2") == 1,
			$"⑤ 不重复结算：这一发里 T1 正好 {CountIn(start, "T1")} 次、T2 正好 {CountIn(start, "T2")} 次（各 1 次）");

		// ⓒ 撞 World 停下：墙夹在两靶之间，都落在一帧线段内。
		t1.GlobalPosition = new Vector3(20f, 0.9f, 17.8f);
		t2.GlobalPosition = new Vector3(20f, 0.9f, 15.4f);
		MakeWall(new Vector3(20f, 2.5f, 16.9f), new Vector3(12f, 5f, 0.4f));
		await Wait(0.25);
		float t2Before = t2.Health;
		start = _hits.Count;
		bool okWall = await FireHolding(2.05);
		await Settle(1, start);
		Check(
			okWall && _hits.Count - start == 1 && HitsSince(start) == "T1",
			$"⑤ 扫掠撞到 World 就停下：墙**前**的靶被结算、墙后的靶没被打到 → 命中 {HitsSince(start)}，伤害 {DamagesSince(start)}");
		Check(
			Mathf.IsEqualApprox(t2.Health, t2Before),
			$"⑤ 墙后靶生命 {t2.Health:0.#}（打前 {t2Before:0.#}）—— 扫掠没有穿地形");
		Check(
			await Until(() => Projectile.LiveCount == 0, 2.5),
			$"⑤ 箭撞墙后消失（场上存活投射物 {Projectile.LiveCount}）");

		GD.Print($"[BowSweep] INFO ⑤ 按靶统计命中次数：T1={TotalOf("T1")} T2={TotalOf("T2")}（T2 只在扫掠那发/墙后那发里没被打到，见上面的断言）");
		t1.QueueFree();
		t2.QueueFree();
		await Wait(0.15);
	}

	/// <summary>等这一发该结算的都结算完（或投射物自己消失）再关掉本段窗口。</summary>
	private async Task Settle(int expectedHits, int hitStart)
	{
		await Until(() => _hits.Count - hitStart >= expectedHits || Projectile.LiveCount == 0, 4);
		await Wait(0.08);
	}

	private int ArrowSweep(int index) => _spawnSweep.Count > index ? _spawnSweep[index] : -1;

	private string HitsSince(int start)
		=> _hits.Count <= start ? "（无）" : string.Join(" → ", _hits.GetRange(start, _hits.Count - start));

	private string DamagesSince(int start)
		=> _hitDamage.Count <= start ? "（无）" : string.Join(" / ", _hitDamage.GetRange(start, _hitDamage.Count - start).ConvertAll(d => d.ToString("0.##")));

	private string FramesSince(int start)
		=> _hitFrames.Count <= start ? "（无）" : string.Join(" / ", _hitFrames.GetRange(start, _hitFrames.Count - start).ConvertAll(f => f.ToString()));

	private int CountIn(int start, string name)
	{
		int n = 0;
		for (int i = start; i < _hits.Count; i++)
		{
			if (_hits[i] == name)
			{
				n++;
			}
		}

		return n;
	}

	private int TotalOf(string name)
	{
		int n = 0;
		foreach (string hit in _hits)
		{
			if (hit == name)
			{
				n++;
			}
		}

		return n;
	}
}
