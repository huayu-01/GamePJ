using ActRoguelike.Abilities;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// **武器不在手上 → 武技1 不能用**（P4 新增）—— 设计文档第 135 行那半句
/// 「武器飞出去这期间**武技1无法使用**」的唯一落点。
///
/// <para>它是场景里的一个普通 <see cref="Node3D"/>（挂在 <c>Player</c> 下，连同一条武器节点），
/// 把两个**互相不知道对方存在**的能力连起来：</para>
/// <code>
/// WeaponThrowAbility.InFlightChanged ──→ WeaponGate ──→ MeleeComboAbility.SetExternallyBlocked
/// </code>
///
/// <para>为什么要有这一层：连招能力（<c>MeleeComboAbility</c>）是**三把武器共用**的，
/// 它不该知道"自己属于哪把武器、那把武器飞出去没有"；而抛掷能力也不该去改别人。
/// 连线放在**场景**里 → 换武器/加武器只改表与场景，C# 一行不动。</para>
///
/// <para>不填任何 Id（或找不到节点）时**什么都不做** —— 默认行为逐位不变。</para>
/// </summary>
public partial class WeaponGate : Node3D
{
	/// <summary>**上游**：那把武器的抛掷技能行 Id（例如 `skl_b2_w2_2`）。</summary>
	[Export] public string ThrowAbilityId { get; set; } = string.Empty;

	/// <summary>**下游**：要被锁住的连招技能行 Id（例如 `skl_b2_w2_1`）。</summary>
	[Export] public string ComboAbilityId { get; set; } = string.Empty;

	/// <summary>绑定成功了吗（自测取证）。</summary>
	public bool Bound { get; private set; }

	/// <summary>现在锁着吗（自测取证）。</summary>
	public bool BlockedNow => _combo?.ExternallyBlocked ?? false;

	/// <summary>
	/// **按节点名推默认的两个技能行 Id**（`WeaponGate5` → 镰刀的武技2 / 武技1）。
	///
	/// <para>为什么要这个默认值：两个 Id 本来是表列（<c>Params</c>），但"命名一个技能行的 Id"
	/// 在连招那一套里已经有一个稳定约定 —— <c>skl_b&lt;武器号&gt;_w&lt;武技号&gt;_&lt;序号&gt;</c>。
	/// 给默认值之后，**场景里一个 WeaponGate 节点 + 一条映射就够了**，不必再依赖两列文本参数的套用时机。</para>
	/// </summary>
	private (string Throw, string Combo) DefaultIds()
	{
		return Name.ToString() switch
		{
			// 武器 1（灵剑）的武技2 是剑气（不需要门），这里只是把映射补齐。
			"WeaponGate4" => ("skl_b1_w1_2", "skl_b1_w1_1"),
			// 武器 2（镰刀）：无情 / 憎恶。
			"WeaponGate5" => ("skl_b2_w1_2", "skl_b2_w1_1"),
			// 武器 3（长枪）：长虹贯日 / 横扫千军。
			"WeaponGate6" => ("skl_b3_w1_2", "skl_b3_w1_1"),
			_ => (string.Empty, string.Empty),
		};
	}

	private WeaponThrowAbility? _throw;
	private MeleeComboAbility? _combo;

	public override void _PhysicsProcess(double delta)
	{
		TryBind();
	}

	/// <summary>
	/// 惰性绑定：**表是在第一个物理帧之后才套上来的**（`RunController.ApplyBalance` 在 idle 帧里跑），
	/// 所以 `_Ready` 里查 `AbilityId` 一定是空的 —— 每帧补一次（与 `Ability.EnsureAnimEventHooked` 同一个坑）。
	/// </summary>
	private void TryBind()
	{
		if (Bound || GetParent() is not Unit owner)
		{
			return;
		}

		(string throwId, string comboId) = DefaultIds();
		string throwAbilityId = string.IsNullOrEmpty(ThrowAbilityId) ? throwId : ThrowAbilityId;
		string comboAbilityId = string.IsNullOrEmpty(ComboAbilityId) ? comboId : ComboAbilityId;
		if (string.IsNullOrEmpty(throwAbilityId) || string.IsNullOrEmpty(comboAbilityId))
		{
			return;
		}

		_throw = Ability.FindByAbilityId(owner, throwAbilityId) as WeaponThrowAbility;
		_combo = Ability.FindByAbilityId(owner, comboAbilityId) as MeleeComboAbility;
		if (_throw == null || _combo == null)
		{
			return;
		}

		_throw.InFlightChanged += OnInFlightChanged;
		_combo.SetExternallyBlocked(_throw.InFlight);
		Bound = true;
		GameLog.Write("Combat", $"武器门：{throwAbilityId}（抛掷）在飞 → 锁住 {comboAbilityId}（武技1）—— 文档「武器飞出去这期间武技1无法使用」");
	}

	private void OnInFlightChanged(bool inFlight)
	{
		_combo?.SetExternallyBlocked(inFlight);
		GameLog.Write("Combat", $"武器门：武器{(inFlight ? "飞出去了" : "回到手上")} → 武技1 {ComboAbilityId} {(inFlight ? "锁住" : "解锁")}");
	}

	public override void _ExitTree()
	{
		if (_throw != null && IsInstanceValid(_throw))
		{
			_throw.InFlightChanged -= OnInFlightChanged;
		}

		_throw = null;
		_combo = null;
		Bound = false;
	}
}
