using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// **被动「灵动」**（角色 A，设计文档第 24 行）：
/// 「角色能在空中额外跳跃一次（正常情况下所有角色都只会有 1 次额外的获取方法，**上限最多 3 次**）」。
///
/// <para>没有按键的技能就是被动（用户裁定 `<see cref="Ability.InputAction"/>` 留空 = 不由玩家按键触发），
/// 所以本类**不靠 <c>TryActivate</c>**：它在每帧把"这个被动应该给几次额外跳跃"写进
/// <see cref="CharacterMovement.SetExtraAirJumps"/>（幂等写入，值一样时什么都不做）。</para>
///
/// <para>⚠️ 只在 <see cref="Ability.Enabled"/> 为真时生效 —— 而 <b>Enabled 是技能行写进来的</b>
/// （`AbilityConfig.Enabled`），所以"哪个角色拿到这个被动"完全由表决定（场景里那两个能力节点默认关闭，
/// 这正是 `RunController.ResetAbilityBindings` 的场景基线语义）。</para>
///
/// <para>上限（3）是**参数**不是常量：设计文档说的是"上限最多 3 次"，而未来的
/// 异化「践踏死亡」（每次击杀获得额外跳跃次数）也要共用这条上限。</para>
/// </summary>
public partial class AirJumpPassiveAbility : Ability
{
	/// <summary>由技能表授予：允许在攻击动作期间保持主动移动。</summary>
	[Export] public bool GrantAttackMovement { get; set; }
	protected override void OnLoadoutReset() => GrantAttackMovement = false;
	[ExportGroup("额外跳跃次数")]
	/// <summary>这个被动**授予几次**额外空中跳跃（文档：1 次）。</summary>
	[Export] public int Grant { get; set; } = 1;

	/// <summary>额外跳跃次数的**上限**（文档：最多 3 次）。</summary>
	[Export] public int MaxExtraAirJumps { get; set; } = 3;

	[ExportGroup("运行期可读状态（自测 / UI）")]
	/// <summary>最近一次实际写进去的额外跳跃次数（自测取证用）。</summary>
	public int AppliedExtraAirJumps { get; private set; } = -1;

	/// <summary>场景基线上本来有几次（第一次读到时就固定下来；本被动只做"基线 + 授予"）。</summary>
	public int SceneBaseline { get; private set; } = -1;

	private bool _baselineCaptured;

	/// <summary>
	/// 被动**不会**被 <c>TryActivate</c> 触发（没有按键），所以这个抽象成员只是必须实现：
	/// 真被激活时什么都不做（效果全在 <see cref="Tick"/> 里按帧幂等地维持）。
	/// </summary>
	protected override void OnActivate(Unit? target)
	{
	}

	protected override void Tick(float delta)
	{
		if (!Enabled || OwnerUnit == null)
		{
			return;
		}

		CharacterMovement? movement = Movement;
		if (movement == null)
		{
			return;
		}

		if (!_baselineCaptured)
		{
			// 基线只抓一次：这一行**只加不减**，所以未来"践踏死亡"这类别处加的跳跃次数不会被它抹掉。
			SceneBaseline = movement.ExtraAirJumps;
			_baselineCaptured = true;
			GameLog.Write(
				"Combat",
				$"被动·灵动：{AbilityId} 生效（场景基线额外跳跃 {SceneBaseline} 次 + 授予 {Grant} 次，上限 {MaxExtraAirJumps} 次）");
		}

		int wanted = Mathf.Clamp(SceneBaseline + Grant, 0, Mathf.Max(0, MaxExtraAirJumps));
		// 只抬高、不压低：与"别处也在加跳跃次数"的机制（践踏死亡）共存。
		movement.SetExtraAirJumps(Mathf.Max(wanted, movement.ExtraAirJumps), MaxExtraAirJumps);
		AppliedExtraAirJumps = movement.ExtraAirJumps;
	}
}
