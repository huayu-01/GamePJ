using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Visual;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>三剑协同：真实近战ATK出手，复制握持剑的外圈轨迹；完全归位后才能再次协同。</summary>
public partial class SpiritSwordsAbility : Ability
{
	[Export] public PackedScene SwordScene { get; set; } = null!;
	[Export] public float DurationSeconds { get; set; } = 10f;
	[Export] public float ReturnSeconds { get; set; } = 1.1f;
	[Export] public float SwordDamageScale { get; set; } = 0.3f;
	[Export] public float OuterReach { get; set; } = 3.4f;
	[Export] public float SwordSpacing { get; set; } = 0.65f;
	public float RemainingSeconds { get; private set; }
	public override float EffectRemainingSeconds => RemainingSeconds;
	public override bool IsReady => base.IsReady && !Summoned;
	public float KillExtensionSeconds { get; private set; }
	public bool Summoned => RemainingSeconds > 0f;
	public bool SwordsReady => Summoned && !_following && _returnTime <= 0f;
	public bool Following => _following;
	public int SwordCount => _swords.Count;
	public int FollowCount { get; private set; }
	public int ExtensionCount { get; private set; }
	public AttackInstance? LastSwordAttack { get; private set; }
	public IReadOnlyList<AttackHitbox> Swords => _swords;
	private readonly List<AttackHitbox> _swords = new();
	private readonly Transform3D[] _returnFrom = new Transform3D[3];
	private MeleeComboAbility? _source;
	private CharacterDisplay? _display;
	private bool _following, _subscribed;
	private float _returnTime;

	public void ConfigureKillExtension(float seconds) => KillExtensionSeconds = Mathf.Max(0f, seconds);
	protected override bool CanActivate(Unit? target) => !Summoned && SwordScene != null
		&& OwnerUnit?.AbilitySlots.Resolve("weapon_skill_1") is MeleeComboAbility;

	protected override void OnActivate(Unit? target)
	{
		_source = (MeleeComboAbility)OwnerUnit!.AbilitySlots.Resolve("weapon_skill_1")!;
		_display = OwnerUnit.GetNodeOrNull<CharacterDisplay>("Appearance");
		RemainingSeconds = Mathf.Max(0.01f, DurationSeconds);
		FollowCount = ExtensionCount = 0;
		LastSwordAttack = null;
		for (int i = 0; i < 3; i++)
		{
			var sword = SwordScene.Instantiate<AttackHitbox>();
			AddChild(sword);
			sword.TopLevel = true;
			sword.GlobalTransform = RestPose(i);
			sword.ResetPhysicsInterpolation();
			_swords.Add(sword);
		}
		_source.StrikeStarted += BeginFollow;
		GameplayEventBus.Raised += OnGameplayEvent;
		_subscribed = true;
		// 召唤是即时背景效果，立即完成施法并从此刻开始15秒冷却；持续期另行计时。
		Finish();
	}

	private Transform3D RestPose(int index) => new(OwnerUnit!.GlobalBasis.Orthonormalized(),
		OwnerUnit.GlobalPosition + OwnerUnit.GlobalBasis * new Vector3((index - 1) * 0.65f, 2.7f + (index == 1 ? 0.2f : 0f), 0.15f));

	private Transform3D FollowPose(int index)
	{
		// 直接读取骨骼挂点的真实挥剑方向，五段横斩／上挑／下劈均沿各自动画变化。
		Node3D weapon = _display!.EquippedWeapon!;
		Basis basis = weapon.GlobalBasis.Orthonormalized();
		Vector3 anchor = OwnerUnit!.GlobalPosition + Vector3.Up * 0.95f;
		Vector3 tip = weapon.GlobalPosition + basis.Y * 0.75f - anchor;
		Vector3 horizontal = new(tip.X, 0f, tip.Z);
		Vector3 direction = horizontal.LengthSquared() > 0.01f ? horizontal.Normalized() : -OwnerUnit.GlobalBasis.Z;
		float rangeScale = _source!.MeleeRangeMultiplier;
		// 扩大水平方向的外圈，保留真实剑尖高度；不能把抬手高度也放大成悬空数米。
		return new Transform3D(basis, anchor + direction * (OuterReach + index * SwordSpacing) * rangeScale + Vector3.Up * tip.Y);
	}

	private void BeginFollow(AttackInstance sourceAttack)
	{
		if (!SwordsReady || !Enabled || OwnerUnit is not { IsDead: false }
			|| _source is not { Enabled: true } || !OwnerUnit.AbilitySlots.Owns(_source)
			|| _display?.EquippedWeapon == null) return;
		_following = true;
		FollowCount++;
		LastSwordAttack = new AttackInstance
		{
			Attacker = OwnerUnit, Ability = AbilityId, AbilityKind = AbilityId,
			BaseDamage = sourceAttack.BaseDamage * SwordDamageScale,
			CanCrit = sourceAttack.CanCrit, CritChance = sourceAttack.CritChance, CritMultiplier = sourceAttack.CritMultiplier,
			ComboStep = sourceAttack.ComboStep, ComboStepCount = sourceAttack.ComboStepCount,
			Knockback = sourceAttack.Knockback, FeedbackWeight = 0.65f,
			Policy = HitPolicy.OncePerTarget, Tags = new[] { "Melee", "Physical", "SpiritSword" },
		};
		for (int i = 0; i < _swords.Count; i++)
		{
			_swords[i].GlobalTransform = FollowPose(i);
			_swords[i].ResetPhysicsInterpolation();
			_swords[i].Activate(OwnerUnit, LastSwordAttack);
		}
	}

	private void BeginReturn()
	{
		_following = false;
		_returnTime = Mathf.Max(0.01f, ReturnSeconds);
		for (int i = 0; i < _swords.Count; i++)
		{
			_swords[i].Deactivate();
			_returnFrom[i] = _swords[i].GlobalTransform;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (!Summoned) return;
		if (!Enabled || OwnerUnit is not { IsDead: false } || !OwnerUnit.AbilitySlots.Owns(this)) { ClearSwords(); return; }
		RemainingSeconds = Mathf.Max(0f, RemainingSeconds - (float)delta);
		if (RemainingSeconds <= 0f) { ClearSwords(); return; }
		// 三剑是附着近战：随原挥击一起停顿，但持续时间与冷却仍使用真实游戏时间。
		if (OwnerUnit.ImpactPause?.IsHolding == true) return;
		if (_following && (_source?.IsStriking != true || _display?.EquippedWeapon == null)) BeginReturn();
		if (_following)
		{
			for (int i = 0; i < _swords.Count; i++)
			{
				AttackHitbox sword = _swords[i];
				Transform3D before = sword.GlobalTransform, after = FollowPose(i);
				int steps = Mathf.Clamp(Mathf.CeilToInt(before.Origin.DistanceTo(after.Origin) / 0.15f), 1, 32);
				for (int step = 1; step <= steps; step++)
				{
					Vector3 from = sword.GlobalPosition;
					sword.GlobalTransform = before.InterpolateWith(after, (float)step / steps);
					sword.SweepBox(from, sword.GlobalPosition);
				}
			}
		}
		else
		{
			_returnTime = Mathf.Max(0f, _returnTime - (float)delta);
			float progress = 1f - _returnTime / Mathf.Max(0.01f, ReturnSeconds);
			float smooth = progress * progress * (3f - 2f * progress);
			for (int i = 0; i < _swords.Count; i++)
				_swords[i].GlobalTransform = _returnTime > 0f ? _returnFrom[i].InterpolateWith(RestPose(i), smooth) : RestPose(i);
		}
	}

	private void OnGameplayEvent(GameplayEvent evt)
	{
		if (evt.Kind != GameplayEventKind.Kill || evt.Source != OwnerUnit || !Summoned || !Enabled
			|| OwnerUnit is not { IsDead: false } || evt.Target is not { IsDead: true } target
			|| !OwnerUnit.IsHostileTo(target) || KillExtensionSeconds <= 0f) return;
		RemainingSeconds += KillExtensionSeconds;
		ExtensionCount++;
	}

	private void ClearSwords()
	{
		if (_subscribed)
		{
			if (IsInstanceValid(_source)) _source!.StrikeStarted -= BeginFollow;
			GameplayEventBus.Raised -= OnGameplayEvent;
		}
		_subscribed = false;
		foreach (AttackHitbox sword in _swords) { sword.Deactivate(); sword.QueueFree(); }
		_swords.Clear();
		_source = null;
		_display = null;
		RemainingSeconds = _returnTime = 0f;
		_following = false;
	}
	protected override void OnLoadoutReset() { ClearSwords(); KillExtensionSeconds = 0f; }
	public override void _ExitTree() { ClearSwords(); base._ExitTree(); }
}
