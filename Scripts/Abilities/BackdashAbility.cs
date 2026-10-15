using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// **技能 1「后撤」**（角色 A，设计文档第 26/28 行）：
///
/// <list type="bullet">
/// <item>「角色快速向后移动（**不改变角色朝向**，只向后移动，表现相当于给角色一个**向后的力**，
///   产生碰撞存在被挤开或停下的情况），**有无敌帧**」 —— 所以这里给的是
///   <see cref="CharacterMovement.AddImpulse"/>（外力），不是 <c>BeginDash</c>（位移技能会强制覆盖
///   输入速度、也不会被挡住）。外力会被 <c>MoveAndSlide</c> 正常挤开/停下，且**不参与朝向计算**
///   （`CharacterMovement` 的转身只读输入速度，见 <see cref="CharacterMovement.FaceDirection"/> 的调用处）。</item>
/// <item>「动画：帧 <c>Dodge</c> 到 <c>Dodger</c> 期间无敌（相当于 <c>Dodge</c> 开无敌、<c>Dodger</c> 关无敌）」
///   —— 两个事件帧夹一个区间，靠 <see cref="Ability.AnimEventEnd"/>（本类读名字分支）。</item>
/// </list>
///
/// <para><b>「不消耗层数的后撤」</b>（弓 武技2「刹那之眼」的"基础蓄力期间被击中会强制触发一次"）：
/// 调用方用 <c>RefreshCooldown() → TryActivate() → RefreshCooldown()</c> 达成（技能层数立刻补满），
/// 本类不需要知道"谁在强制触发它"。</para>
///
/// <para>所有数值（后撤力、动画名、两个事件帧、CD）都在技能行里；代码里没有力的字面量。</para>
/// </summary>
public partial class BackdashAbility : Ability
{
	[ExportGroup("后撤（向后的力）")]
	/// <summary>向后的力（冲量，m/s）。文档写「{100 或 N}」—— 表里给一个能跑起来的预设值。</summary>
	[Export] public float BackwardForce { get; set; } = 12f;

	/// <summary>额外抬高一点的垂直分量（0 = 纯水平后撤）。</summary>
	[Export] public float UpwardForce { get; set; }

	[ExportGroup("无敌帧（按动画帧开关）")]
	/// <summary>开无敌的那一帧名（文档：<c>Dodge</c>）—— 与行里的 <c>AnimEvent</c> 同一列。</summary>
	[Export] public string InvulnerableFromEvent { get; set; } = "Dodge";

	/// <summary>关无敌的那一帧名（文档：<c>Dodger</c>）—— 与行里的 <c>AnimEventEnd</c> 同一列。</summary>
	[Export] public string InvulnerableToEvent { get; set; } = "Dodger";

	/// <summary>这一帧的超时兜底（秒；≤0 = 关）：事件帧没来也要把无敌关掉，否则玩家会永久无敌。</summary>
	[Export] public float InvulnerableSafetySeconds { get; set; } = 3f;

	[ExportGroup("运行期可读状态（自测 / UI）")]
	/// <summary>本次施法是不是已经开了无敌（自测取证用）。</summary>
	public bool InvulnerableOwned { get; private set; }

	/// <summary>最后一次后撤实际用的力（含垂直分量），自测取证用。</summary>
	public Vector3 LastImpulse { get; private set; }

	private float _invulnerableTime;

	protected override void OnActivate(Unit? target)
	{
		InvulnerableOwned = false;
		_invulnerableTime = 0f;
		LastImpulse = Vector3.Zero;

		// 动画必须存在（基类 TryActivate 已经查过），这里开播；效果等 Dodge 事件帧（= 动画开头那一帧）。
		PlaySkillAnimation();
		if (ActRoguelike.Debug.SelfTest.DebugSkipSkillAnimation)
		{
			GameLog.Write("Combat", $"⚠ 技能 {AbilityId} 按排障开关跳过了动画事件帧，直接后撤。");
			BeginDash();
		}
	}

	/// <summary>
	/// 两个事件帧：<c>Dodge</c>（开无敌 + 给向后的力）、<c>Dodger</c>（关无敌 + 收招）。
	/// ⚠️ 后撤力与无敌**都挂在动画帧上**（用户裁定：效果随动画事件帧走）。
	/// </summary>
	protected override void OnAnimEvent(string name)
	{
		if (name == InvulnerableToEvent)
		{
			EndInvulnerable();
			Finish();
			return;
		}

		BeginDash();
	}

	private void BeginDash()
	{
		if (OwnerUnit == null || Movement == null)
		{
			Cancel();
			return;
		}

		// "向后" = 角色自身朝向的反方向。**不调用 SetFacing**，所以朝向一点都不变。
		Vector3 back = OwnerUnit.GlobalTransform.Basis.Z;
		back.Y = 0f;
		back = back.LengthSquared() > 0.0001f ? back.Normalized() : Vector3.Back;

		LastImpulse = (back * BackwardForce) + (Vector3.Up * UpwardForce);
		Movement.AddImpulse(LastImpulse);

		OwnerUnit.IsInvulnerable = true;
		InvulnerableOwned = true;
		_invulnerableTime = 0f;

		GameLog.Write(
			"Combat",
			$"后撤：{AbilityId} 后撤力 {BackwardForce:0.##}（方向 {LastImpulse}，**不改变朝向**）｜无敌帧开（{InvulnerableFromEvent}）");
	}

	private void EndInvulnerable()
	{
		if (!InvulnerableOwned)
		{
			return;
		}

		InvulnerableOwned = false;
		if (OwnerUnit != null && IsInstanceValid(OwnerUnit))
		{
			OwnerUnit.IsInvulnerable = false;
		}

		GameLog.Write("Combat", $"后撤：{AbilityId} 无敌帧关（{InvulnerableToEvent}）");
	}

	/// <summary>超时兜底：动画事件帧没来（生成表配错）时也要收招并**把无敌关掉**。</summary>
	protected override void Tick(float delta)
	{
		if (!InvulnerableOwned)
		{
			if (IsBusy && !HoldToCharge && StateTime > Windup + Recover + 1.5f)
			{
				GameLog.Write("Combat", $"⚠ 技能 {AbilityId} 等不到动画事件帧「{AnimEvent}」，本次施法放弃（检查 SkillAnimations 表的事件帧名）。");
				Cancel();
			}

			return;
		}

		_invulnerableTime += delta;
		if (InvulnerableSafetySeconds > 0f && _invulnerableTime > InvulnerableSafetySeconds)
		{
			GameLog.Write("Combat", $"⚠ 技能 {AbilityId} 的无敌帧超过安全时长 {InvulnerableSafetySeconds:0.##}s（没等到「{InvulnerableToEvent}」帧），强制关闭。");
			EndInvulnerable();
		}
	}

	public override void _ExitTree()
	{
		EndInvulnerable();
		base._ExitTree();
	}
}
