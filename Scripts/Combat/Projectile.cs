using ActRoguelike.Audio;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 统一投射物（子弹、箭矢、气刃、投掷物）。
/// 每个物理帧沿本帧位移做一次射线检测（战斗规则说明 §7），避免高速穿透；
/// 命中后建立 HitInstance 交给目标 Unit 结算，并按 Hit Policy 决定是否继续飞行（穿透）。
/// 弹道下坠只需把 <see cref="GravityScale"/> 设为非 0。
/// </summary>
public partial class Projectile : Node3D
{
	[Export] public float Speed { get; set; } = 70f;
	[Export] public float GravityScale { get; set; }
	[Export] public float MaxLifetime { get; set; } = 2.5f;

	/// <summary>
	/// **下坠起点**（米；0 = 一发射就受重力，与改动前逐位一致）。
	///
	/// 文档（角色 A 武器 2「短弩」）：「该子弹在飞行一段距离后会下坠，**射速决定了什么时候开始下坠**」
	/// —— 所以"什么时候开始掉"用**飞过的距离**表达：弹速越高，飞完这段距离越快、下坠来得越早/越晚
	/// 由表里的这个距离与弹速共同决定。表列 = 技能行的 <c>GravityStartDistance</c>。
	/// </summary>
	[Export] public float GravityStartDistance { get; set; }

	/// <summary>
	/// **追踪的标记状态 Id**（空 = 不追踪，与改动前逐位一致）。文档（弓 武技2「刹那之眼」）：
	/// 「你射出的箭会追踪被打上「标记」的最近目标」—— 这里填 <c>Mark</c>，飞行中每帧朝
	/// **最近的一个带该状态的敌对单位**转向。
	///
	/// 它读的是<b>单位身上的状态</b>（`Unit.GetStatusStacks`，可叠加、不限单目标），所以"标记谁"完全由
	/// 施法那一侧决定，投射物这边没有硬编码任何目标类型。
	/// </summary>
	[Export] public string HomingStatusId { get; set; } = string.Empty;

	/// <summary>追踪转向速度（度/秒；≤0 = 不追踪）。配 <see cref="HomingStatusId"/> 用。</summary>
	[Export] public float HomingTurnRateDegrees { get; set; }

	/// <summary>追踪搜索半径（米；≤0 = 不限）。</summary>
	[Export] public float HomingRange { get; set; }

	/// <summary>可穿透的目标数量，0 = 命中即消失。</summary>
	[Export] public int Pierce { get; set; }

	[Export] public uint HitMask { get; set; } = CombatLayers.World | CombatLayers.UnitMask;

	/// <summary>弹射时搜索下一个目标的半径。</summary>
	[Export] public float RicochetRange { get; set; } = 9f;

	/// <summary>已经飞过的距离（米）—— <see cref="GravityStartDistance"/> 的判据，也是自测的取证量。</summary>
	public float TravelledDistance { get; private set; }

	/// <summary>当前**速度方向**（自测 / 排障读它；没发射时是零向量）。</summary>
	public Vector3 VelocityDirection => _velocity.LengthSquared() > 0.0001f ? _velocity.Normalized() : Vector3.Zero;

	/// <summary>最后一次追踪锁定的目标名（空 = 这一帧没在追踪任何东西）。</summary>
	public string HomingTargetName { get; private set; } = string.Empty;

	/// <summary>最近一次追踪用的插值权重与夹角（自测 / 排障用：能直接看出"转了多少"）。</summary>
	public float LastHomingWeight { get; private set; }

	/// <summary>最近一次追踪时"速度方向 ↔ 目标方向"的夹角（度）。</summary>
	public float LastHomingAngleDegrees { get; private set; }

	private readonly HitTracker _tracker = new();
	private readonly System.Collections.Generic.HashSet<ulong> _hitTargets = new();
	private AttackInstance? _attack;
	private Vector3 _velocity;
	private float _lifetime;
	private int _pierceLeft;
	private int _ricochetLeft;
	private bool _exploded;
	private Rid _ownerRid;

	/// <summary>
	/// **斩断"场景名匹配"的投射物**（P4 新增，只加不删）。
	///
	/// <para>文档（角色 B 武器 1「灵剑」武技1「破剑式」）：「每段都可以摧毁碰撞盒内的子弹
	/// （**包括友军的**，但**无法斩断"能量波"**）」。所以判据不能是"敌对与否"，也不是"投射物的
	/// 碰撞形状名"（实测：项目里的投射物预制体**没有** <c>CollisionShape3D</c> —— 弹道走射线、
	/// 不靠形状），而是**投射物的来源场景名**：近战命中体把 <c>CutSceneNames</c>（表列）里的名字
	/// 对应的投射物清掉，能量波的场景名（`EnergyWave`）不写进名单 → 斩不断。
	/// **是数据，不是代码特例。**</para>
	///
	/// <para>名字来自 <see cref="SourceSceneName"/> = 投射物场景文件的基名
	/// （`res://Scenes/Combat/Bullet.tscn` → `Bullet`）。名单里写基名即可。</para>
	/// </summary>
	/// <returns>真的清掉几个。</returns>
	public static int CutBySceneName(string sceneName, Vector3 center, float radius)
		=> CutBySceneName(sceneName, center, radius, null);

	/// <summary>
	/// 同上，并**按名单排除**（文档：「无法斩断"能量波"」→ 排除名单里写 `EnergyWave`）。
	/// 排除名单是**表列**（`CutExcludeSceneNames`），不是代码里的特例分支。
	/// </summary>
	public static int CutBySceneName(string sceneName, Vector3 center, float radius, string[]? excludeSceneNames)
	{
		if (string.IsNullOrEmpty(sceneName) || radius <= 0f)
		{
			return 0;
		}

		float radiusSquared = radius * radius;
		int cut = 0;
		foreach (Node node in _live.ToArray())
		{
			if (!GodotObject.IsInstanceValid(node))
			{
				continue;
			}

			if (node is not Projectile projectile || projectile.SourceSceneName != sceneName)
			{
				continue;
			}

			if (excludeSceneNames != null && System.Array.IndexOf(excludeSceneNames, projectile.SourceSceneName) >= 0)
			{
				continue;
			}

			// 文档要的是"**碰撞盒内**的子弹"：以命中盒中心为球心做一次距离过滤。
			if (projectile.GlobalPosition.DistanceSquaredTo(center) > radiusSquared)
			{
				continue;
			}

			cut++;
			projectile.Cut();
		}

		return cut;
	}

	/// <summary>还活着（还没被清掉）的投射物数量 —— 自测取证用。</summary>
	public static int LiveCount => _live.Count;

	/// <summary>场上每个投射物的"场景名 @ 位置"（自测排障：一眼看出斩断半径够不够）。</summary>
	public static string DebugSummary()
	{
		var parts = new System.Collections.Generic.List<string>();
		foreach (Node node in _live)
		{
			if (node is Projectile projectile && GodotObject.IsInstanceValid(projectile))
			{
				parts.Add($"{projectile.SourceSceneName}@{projectile.GlobalPosition}");
			}
		}

		return parts.Count == 0 ? "（无）" : string.Join(" / ", parts);
	}

	/// <summary>场内所有投射物的来源场景名（自测 / 排障：一眼看出"斩断名单该写什么"）。</summary>
	public static string SourceSceneSummary()
	{
		var names = new System.Collections.Generic.List<string>();
		foreach (Node node in _live)
		{
			if (node is Projectile projectile && !string.IsNullOrEmpty(projectile.SourceSceneName) && !names.Contains(projectile.SourceSceneName))
			{
				names.Add(projectile.SourceSceneName);
			}
		}

		return names.Count == 0 ? "（场上没有投射物）" : string.Join("、", names);
	}

	/// <summary>
	/// **本投射物的来源场景名**（`Bullet` / `EnergyWave` / `Arrow` / `SwordQi`…）。
	/// 斩断判定认它 —— 见 <see cref="CutBySceneName"/>。
	/// </summary>
	public string SourceSceneName => _sourceSceneName ??= SceneFilePath.GetFile().GetBaseName();

	/// <summary>被近战斩断时的一次性收尾（<see cref="CutBySceneName"/> 走它）。</summary>
	private void Cut()
	{
		CutDown?.Invoke();
		QueueFree();
	}

	/// <summary>被斩断时触发（自测取证：证明是"被斩断"而不是自己到期）。</summary>
	public event System.Action? CutDown;

	private static readonly System.Collections.Generic.List<Projectile> _live = new();
	private string? _sourceSceneName;

	public override void _Ready()
	{
		AddToGroup("projectiles");
		_live.Add(this);

	}

	public override void _ExitTree() => _live.Remove(this);

	public void Launch(AttackInstance attack, Vector3 direction)
	{
		_attack = attack;
		_velocity = direction.Normalized() * Speed;
		_pierceLeft = Pierce + attack.Pierce;
		_ricochetLeft = attack.Ricochet;
		_ownerRid = attack.Attacker?.GetRid() ?? default;
		TravelledDistance = 0f;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_attack == null)
		{
			return;
		}

		float dt = (float)delta;
		_lifetime += dt;
		if (_lifetime > _attack.Lifetime)
		{
			QueueFree();
			return;
		}

		// 追踪（弓 武技2「刹那之眼」）：朝**最近的一个带标记的敌人**转向。
		// 默认（HomingStatusId 空 / 转向速度 ≤0）**完全不进这一段** —— 弹道与改动前逐位一致。
		if (!string.IsNullOrEmpty(HomingStatusId) && HomingTurnRateDegrees > 0f)
		{
			SteerTowardsMark();
		}

		// 下坠：GravityStartDistance ≤ 0 时与改动前同一条路（一发射就受重力）。
		if (GravityScale != 0f && TravelledDistance >= Mathf.Max(0f, GravityStartDistance))
		{
			float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
			_velocity.Y -= gravity * GravityScale * dt;
		}

		Vector3 from = GlobalPosition;
		Vector3 to = from + _velocity * dt;

		var query = PhysicsRayQueryParameters3D.Create(from, to, HitMask);
		if (_ownerRid.IsValid)
		{
			query.Exclude = new Godot.Collections.Array<Rid> { _ownerRid };
		}

		Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count > 0)
		{
			ResolveImpact(hit, to);
			return;
		}

		GlobalPosition = to;
		TravelledDistance += from.DistanceTo(to);
		Orient();
	}

	/// <summary>
	/// 朝**最近的、身上带着 <see cref="HomingStatusId"/> 这个状态的敌对单位**把速度方向转过去
	/// （每帧最多转 <see cref="HomingTurnRateDegrees"/> × dt 度）。
	///
	/// 目标筛选口径与 <c>Targeting.FindNearestHostile</c> 一致（按阵营的组），
	/// 但额外要求"带标记"—— 这就是文档里"追踪被打上标记的最近目标"。
	/// </summary>
	private void SteerTowardsMark()
	{
		Unit? attacker = _attack?.Attacker;
		HomingTargetName = string.Empty;
		if (attacker == null)
		{
			return;
		}

		Unit? best = null;
		float bestDistance = HomingRange > 0f ? HomingRange * HomingRange : float.MaxValue;
		string group = attacker.Faction == Faction.Player ? "enemy_units" : "player_units";
		foreach (Node node in GetTree().GetNodesInGroup(group))
		{
			if (node is not Unit candidate || !candidate.IsValidTarget || !attacker.IsHostileTo(candidate))
			{
				continue;
			}

			if (!candidate.HasStatus(HomingStatusId))
			{
				continue;
			}

			float distance = GlobalPosition.DistanceSquaredTo(candidate.GlobalPosition);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = candidate;
			}
		}

		if (best == null || _velocity.LengthSquared() < 0.0001f)
		{
			return;
		}

		HomingTargetName = best.Name;

		Vector3 wanted = (best.GlobalPosition - GlobalPosition).Normalized() * _velocity.Length();
		float angle = _velocity.AngleTo(wanted);
		float maxRadians = Mathf.DegToRad(HomingTurnRateDegrees) * (float)GetPhysicsProcessDeltaTime();
		float weight = angle > 1e-4f ? Mathf.Min(1f, maxRadians / angle) : 1f;
		LastHomingWeight = weight;
		LastHomingAngleDegrees = Mathf.RadToDeg(angle);
		_velocity = _velocity.Slerp(wanted, weight);
	}

	private void ResolveImpact(Godot.Collections.Dictionary hit, Vector3 to)
	{
		var collider = hit["collider"].As<GodotObject>();
		Vector3 position = hit["position"].AsVector3();

		if (_attack!.ExplosionRadius > 0f && !_exploded)
		{
			_exploded = true;
			Detonate(position);
		}

		if (collider is Unit target && _attack.Attacker != null && _attack.Attacker.IsHostileTo(target))
		{
		float now = Time.GetTicksMsec() / 1000f;
			if (target.IsValidTarget && _tracker.TryRegister(target, _attack.Policy, _attack.HitInterval, now, 0))
			{
				_hitTargets.Add(target.GetInstanceId());
				target.ReceiveHit(_attack.RollHit(target, _velocity.Normalized(), position));

				// 命中特效（Binbun Hit FX）：用**射线命中点**，子弹打在身上哪就炸在哪。
				// 开关 / 生命周期 / 预制体路径都在 `Config/ConfigIndex.tres` 的「表现 · 命中特效」组里（不写死）。
				HitEffect.Spawn(this, position);

				// 命中音效：与命中特效**同一个位置、同一个判定**，按 id 播（表里说了算）。
				AudioManager.PlaySfx(AudioIds.CombatHit);
			}

			if (_pierceLeft > 0)
			{
				_pierceLeft--;
				GlobalPosition = to;
				Orient();
				return;
			}

			if (_ricochetLeft > 0 && TryRicochet(target))
			{
				return;
			}
		}

		QueueFree();
	}

	/// <summary>弹射：转向附近还没命中过的敌人继续飞。</summary>
	private bool TryRicochet(Unit justHit)
	{
		Unit? next = null;
		float bestDistance = RicochetRange * RicochetRange;

		foreach (Node node in GetTree().GetNodesInGroup("enemy_units"))
		{
			if (node is not Unit candidate || candidate == justHit || !candidate.IsValidTarget)
			{
				continue;
			}

			if (_hitTargets.Contains(candidate.GetInstanceId()))
			{
				continue;
			}

			float distance = GlobalPosition.DistanceSquaredTo(candidate.GlobalPosition);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				next = candidate;
			}
		}

		if (next == null)
		{
			return false;
		}

		_hitTargets.Add(next.GetInstanceId());
		_ricochetLeft--;
		Vector3 direction = (next.GlobalPosition - GlobalPosition).Normalized();
		_velocity = direction * Speed;
		GlobalPosition = GlobalPosition + (direction * 0.6f);
		Orient();
		return true;
	}

	private void Detonate(Vector3 position)
	{
		var scene = GD.Load<PackedScene>("res://Scenes/Combat/Explosion.tscn");
		if (scene?.Instantiate<Explosion>() is not { } explosion)
		{
			return;
		}

		Node parent = GetTree().CurrentScene ?? GetTree().Root;
		parent.AddChild(explosion);
		explosion.GlobalPosition = position;
		explosion.Detonate(_attack!.Attacker, _attack.ExplosionDamage, _attack.ExplosionRadius, _attack.Tags);
	}

	private void Orient()
	{
		if (_velocity.LengthSquared() < 0.0001f)
		{
			return;
		}

		Vector3 direction = _velocity.Normalized();
		Vector3 up = Mathf.Abs(direction.Dot(Vector3.Up)) > 0.99f ? Vector3.Forward : Vector3.Up;
		LookAt(GlobalPosition + direction, up);
	}
}




