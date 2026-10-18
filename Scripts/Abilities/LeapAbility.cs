using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// **技能 2「跃升」**（角色 A，设计文档第 30/33 行）：
/// 「角色进行一次**强力的跳跃**，且获得一段时间{3s}的**滞空**」
/// 「动画：帧 <c>Action</c> 时释放效果」。
///
/// <para>实现分成两半，各管一件事：</para>
/// <list type="bullet">
/// <item><b>本类</b>：动画 → 事件帧 <c>Action</c> → 起跳（<see cref="CharacterMovement.SetVerticalVelocity"/>，
///   速度是表值 <see cref="LeapVelocity"/>，与角色的普通起跳值无关）+ 授予滞空。</item>
/// <item><b><see cref="AirHoverController"/></b>（玩家场景里的常驻组件）：持有"还剩多少秒滞空"，
///   在"空中 + 按住跳跃键"时减缓下落 90%，并在脚底显示光环 —— 因为本技能在事件帧那一刻就收招了，
///   而滞空要持续几秒，状态不能挂在技能上。</item>
/// </list>
///
/// <para>「当有跳跃次数时会先执行跳跃」由 <c>PlayerController</c>（<c>IsActionJustPressed("jump")</c> →
/// <c>TryJump</c>）与 <see cref="AirHoverController"/>（按下那一帧不插手）共同保证。</para>
/// </summary>
public partial class LeapAbility : Ability
{
	[ExportGroup("强力跳跃")]
	/// <summary>起跳的垂直速度（m/s，表值；文档只写了"强力的跳跃"，具体值是开发预设）。</summary>
	[Export] public float LeapVelocity { get; set; } = 16f;

	/// <summary>空中也能用（1 = 可以，0 = 只能在地面）。文档没写落地限制，默认允许。</summary>
	[Export] public bool AllowInAir { get; set; } = true;

	[ExportGroup("滞空（授予给脚底那个常驻组件）")]
	/// <summary>滞空时长（秒；文档：3）。</summary>
	[Export] public float HoverSeconds { get; set; } = 3f;

	/// <summary>按住跳跃键时的下落减缓比例（文档：90%）。</summary>
	[Export(PropertyHint.Range, "0,1,0.05")] public float HoverFallSlowPercent { get; set; } = 0.9f;

	/// <summary>跳跃键动作名覆盖（留空 = 用组件上的值）。表列可填，默认不填。</summary>
	[Export] public string JumpActionOverride { get; set; } = string.Empty;

	[ExportGroup("脚底光环（表现参数，一并传给组件）")]
	[Export] public float RingRadius { get; set; } = 1.15f;
	[Export] public float RingThickness { get; set; } = 0.07f;
	[Export] public float RingHeight { get; set; } = 0.12f;
	[Export] public Color RingColor { get; set; } = new(0.45f, 0.85f, 1f, 0.9f);

	[ExportGroup("运行期可读状态（自测 / UI）")]
	/// <summary>最后一次跃升实际用的起跳速度。</summary>
	public float LastLeapVelocity { get; private set; }

	/// <summary>最后一次跃升授予的滞空秒数（授予成功才有值）。</summary>
	public float LastHoverSeconds { get; private set; }

	private bool _released;

	protected override void OnActivate(Unit? target)
	{
		_released = false;
		LastLeapVelocity = 0f;
		LastHoverSeconds = 0f;
		PlaySkillAnimation();
		if (ActRoguelike.Debug.SelfTest.DebugSkipSkillAnimation)
		{
			GameLog.Write("Combat", $"⚠ 技能 {AbilityId} 按排障开关跳过了动画事件帧，直接跃升。");
			Release();
		}
	}

	/// <summary>动画播到 <c>Action</c> 帧 → 强力跳跃 + 授予滞空。</summary>
	protected override void OnAnimEvent(string name)
	{
		Release();
	}

	private void Release()
	{
		if (_released || OwnerUnit == null)
		{
			return;
		}

		_released = true;

		if (Movement != null)
		{
			if (!AllowInAir && Movement.IsGrounded == false)
			{
				GameLog.Write("Combat", $"跃升：{AbilityId} 只能在地面使用，本次不执行。");
				Cancel();
				return;
			}

			Movement.BeginJump(LeapVelocity);
			LastLeapVelocity = LeapVelocity;
		}

		if (FindInOwner<AirHoverController>() is { } hover)
		{
			hover.JumpAction = string.IsNullOrEmpty(JumpActionOverride) ? hover.JumpAction : JumpActionOverride;
			hover.ApplyRingStyle(RingRadius, RingThickness, RingHeight, RingColor);
			hover.Grant(HoverSeconds, HoverFallSlowPercent);
			LastHoverSeconds = HoverSeconds;
		}
		else
		{
			GD.PushWarning($"[LeapAbility] {AbilityId} 找不到 AirHoverController（玩家场景里少了这个组件？），滞空没有生效。");
			GameLog.Write("Combat", $"⚠ 跃升 {AbilityId}：{OwnerUnit.Name} 上没有 AirHoverController，滞空没生效。");
		}

		GameLog.Write(
			"Combat",
			$"跃升：{AbilityId} 起跳速度 {LeapVelocity:0.##} m/s + 滞空 {HoverSeconds:0.##}s（按住跳跃键下落减缓 {HoverFallSlowPercent * 100f:0.#}%，脚底光环半径 {RingRadius:0.##}）");
		Finish();
	}

	/// <summary>动画事件帧没来时的兜底（不做时间驱动兜底）。</summary>
	protected override void Tick(float delta)
	{
		if (IsBusy && !_released && StateTime > Windup + Recover + 1.5f)
		{
			GameLog.Write("Combat", $"⚠ 技能 {AbilityId} 等不到动画事件帧「{AnimEvent}」，本次施法放弃（检查 SkillAnimations 表的事件帧名）。");
			Cancel();
		}
	}
}
