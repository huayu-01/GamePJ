using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// 角色A后撤：真实Dodge事件起步，当前默认在0.3秒内向后移动12米。
/// 只覆盖水平速度，保留重力、地形碰撞、外力与起手朝向；动画不重新制作。
/// 无敌仍由Dodge/Dodger开关，动画先结束时等待位移完成再收招；取消或换装立即释放。
/// BackwardDistance设为0可兼容旧冲量模式。弓的免费后撤仍复用同一能力与层数规则。
/// </summary>
public partial class BackdashAbility : Ability
{
	[ExportGroup("定距后撤")]
	/// <summary>正值启用定距移动；设为0可兼容旧冲量配置。碰撞仍由MoveAndSlide处理。</summary>
	[Export] public float BackwardDistance { get; set; } = 12f;
	[Export] public float TravelSeconds { get; set; } = .3f;
	[ExportGroup("后撤（向后的力）")]
	/// <summary>向后的力（冲量，m/s）。文档写「{100 或 N}」—— 表里给一个能跑起来的预设值。</summary>
	[Export] public float BackwardForce { get; set; } = 20f;

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
	private bool _traveling, _finishAfterTravel;

	protected override void OnActivate(Unit? target)
	{
		InvulnerableOwned = false;
		_invulnerableTime = 0f;
		LastImpulse = Vector3.Zero;
		_finishAfterTravel = false;
		Movement?.LockFacing(this);

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
			if (_traveling) _finishAfterTravel = true; else Finish();
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

		if (BackwardDistance > 0)
		{
			LastImpulse = back * (BackwardDistance / Mathf.Max(.01f, TravelSeconds));
			if (!Movement.BeginVelocityOverride(this, LastImpulse, Mathf.Max(.01f, TravelSeconds), isDash: true, planarOnly: true)) { Cancel(); return; }
			_traveling = true;
			if (UpwardForce != 0) Movement.AddImpulse(Vector3.Up * UpwardForce);
		}
		else
		{
			LastImpulse = (back * BackwardForce) + (Vector3.Up * UpwardForce);
			Movement.AddImpulse(LastImpulse);
		}

		OwnerUnit.IsInvulnerable = true;
		InvulnerableOwned = true;
		_invulnerableTime = 0f;

		GameLog.Write(
			"Combat",
			$"后撤：{AbilityId} 距离 {BackwardDistance:0.##}m / {TravelSeconds:0.##}s（速度 {LastImpulse}，不改变朝向，保留物理碰撞）｜无敌帧开（{InvulnerableFromEvent}）");
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
		if (_traveling && Movement?.VelocityOverrideFinished(this) == true)
		{
			Movement.EndVelocityOverride(this); _traveling = false;
			if (_finishAfterTravel) { Finish(); return; }
		}
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
		Movement?.EndVelocityOverride(this);
		Movement?.UnlockFacing(this);
		base._ExitTree();
	}

	protected override void OnFinishOrCancel()
	{
		EndInvulnerable();
		Movement?.EndVelocityOverride(this);
		_traveling = _finishAfterTravel = false;
		Movement?.UnlockFacing(this);
	}

	protected override void OnLoadoutReset() => OnFinishOrCancel();
}
