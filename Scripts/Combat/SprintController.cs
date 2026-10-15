using ActRoguelike.Config;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// **奔跑**（设计文档 `角色设计.txt` 第 19 行，写在「所有角色通用的机制」里）：
///
/// <code>
/// 奔跑(只有在移动状态时才可以按住shift键进入,移动的更快,释放其他任何状态或技能都会退出奔跑)
/// </code>
///
/// <list type="bullet">
/// <item><b>按住 shift</b>：按键动作名来自**表**（角色表 <c>SprintInputAction</c>，默认 `sprint`），
///   不是这里写死的字符串。`project.godot` 的 `[input]` 里有这个动作（Shift）才会生效，
///   动作不存在 → 永远不算奔跑（并写一条警告，不静默）。</item>
/// <item><b>只有在移动状态</b>：读 <see cref="CharacterMovement.IntentDirection"/>（已有输入意图）——
///   站着不动按 Shift 不算奔跑。</item>
/// <item><b>移动的更快</b>：<see cref="EffectiveSpeed"/> = 基础移速 × 倍率（倍率来自角色表
///   <c>SprintSpeedMultiplier</c>）。调用方（<c>PlayerController</c>）用它设置移动意图速度 ——
///   <see cref="CharacterMovement"/> **一个字节都没改**。</item>
/// <item><b>释放其他任何状态或技能都会退出奔跑</b>：轮询**只读**量判断 ——
///   位移技能（<see cref="CharacterMovement.IsDashing"/>）与"玩家身上任何一个 <c>Ability</c> 正在出招"
///   （<c>Ability.IsBusy</c>）都会让奔跑立刻结束。这也是"必须落地"（<see cref="RequireGrounded"/>）的同一条思路。</item>
/// </list>
///
/// <para><b>为什么不做进 <see cref="CharacterMovement"/></b>：它是玩家 / 敌人 / 首领 / 召唤物共用的战斗类，
/// 加输入语义会污染所有 AI 单位。这里沿用 P6b「杀戮践踏」那条纪律（<c>StompOnLandRule</c>）——
/// **只读共用类已经公开的量，不碰它**。本组件只挂在玩家预制体上。</para>
///
/// <para>⚠️ <b>默认（读不到表 / 倍率 = 1）行为逐位不变</b>：<see cref="SpeedMultiplier"/> 的出口是
/// <see cref="EffectiveSpeed"/>，倍率为 1 时它返回 <c>baseSpeed</c> 本身。</para>
/// </summary>
public partial class SprintController : Node
{
	[ExportGroup("输入（表里填，这里是回退值）")]
	/// <summary>奔跑键的动作名（`project.godot` 的 `[input]`；设计文档第 19 行写的是 shift）。</summary>
	[Export] public string InputAction { get; set; } = "sprint";

	[ExportGroup("速度（角色表 chr_*.tres 的两列会覆盖这里）")]
	/// <summary>奔跑时的移速倍率（1 = 不加速 = 改动前行为）。文档只写"移动的更快"，没给数字。</summary>
	[Export] public float SpeedMultiplier { get; set; } = 1f;

	[ExportGroup("退出奔跑的条件")]
	/// <summary>在出招（任何一个 <c>Ability.IsBusy</c>）时退出奔跑（文档：「释放其他任何状态或技能都会退出奔跑」）。</summary>
	[Export] public bool ExitWhileAbilityBusy { get; set; } = true;

	/// <summary>必须在地面上才算奔跑（预设：Shift 在空中原本是下落攻击的键，见 `fall_attack`）。</summary>
	[Export] public bool RequireGrounded { get; set; } = true;

	[ExportGroup("运行期可读状态（自测 / 探针 / 护卫）")]
	/// <summary>现在是不是在奔跑（护卫这类"奔跑时……"的增幅读它）。</summary>
	public bool IsSprinting => DebugSprintOverride || _sprinting;

	/// <summary>
	/// **探针专用**：把奔跑状态钉成 true（不依赖真实按键注入）。
	/// 与 P6c 的 `DebugPreviewXxx` 同一条纪律 —— **判据与画法一个字都不变，只是"谁来触发"**。
	/// 正式流程里永远是 false。
	/// </summary>
	public bool DebugSprintOverride { get; set; }

	/// <summary>奔跑键这一帧是不是按住着（与 <see cref="IsSprinting"/> 分开，排障用）。</summary>
	public bool InputHeld { get; private set; }

	/// <summary>因为哪一个条件退出的（最近一次；排障用）。</summary>
	public string LastExitReason { get; private set; } = "（还没跑过）";

	/// <summary>本局累计进入奔跑几次（自测取证）。</summary>
	public int SprintCount { get; private set; }

	private Unit? _unit;
	private CharacterMovement? _movement;
	private bool _warnedMissingAction;
	private bool _warnedNoRow;
	private bool _configured;
	private bool _wasSprinting;
	private bool _sprinting;

	private static readonly System.Collections.Generic.List<Abilities.Ability> Scratch = new();

	public override void _Ready()
	{
		_unit = GetParent() as Unit;

		// ⚠️ 与 `CharacterMovement.ApplyJumpConfigDeferred` 同一个理由：归属组在 `Unit._Ready` 里登记，
		// 而**子节点的 `_Ready` 早于父节点** —— 读表那一步必须 defer 一帧。
		CallDeferred(nameof(TryApplyCharacterRowDeferred));
	}

	/// <summary>
	/// 角色表的两列覆盖（`SprintInputAction` / `SprintSpeedMultiplier`）。
	/// 与 <see cref="CharacterMovement"/> 的跳跃两列同一个口径：**只有玩家读表**，读不到就保留
	/// <see cref="InputAction"/> / <see cref="SpeedMultiplier"/> 的当前值（"读不到则使用自身的默认值"），
	/// 但必须留痕。
	///
	/// <para>⚠️ **读不到就不置 <see cref="_configured"/>**：延迟调用可能早于表就绪（实测：与
	/// <c>CharacterMovement</c> 那两个延迟调用同一帧、谁先谁后由节点顺序决定），所以没读到就
	/// 下一物理帧继续试，直到读到为止（成功只记一次日志，读不到只警告一次，不刷屏）。</para>
	/// </summary>
	private void TryApplyCharacterRowDeferred()
	{
		if (_configured || _unit == null || !IsInstanceValid(_unit))
		{
			return;
		}

		if (ConfigRows.BySlot<CharacterConfig>((int)RunSetup.Character + 1) is not { } row)
		{
			if (!_warnedNoRow)
			{
				_warnedNoRow = true;
				Core.GameLog.Write(
					"Config",
					$"奔跑：角色行暂时读不到（RunSetup.Character={RunSetup.Character} / 角色表 {ConfigRows.Count<CharacterConfig>()} 行）→ 先按「{InputAction}」/ 倍率 {SpeedMultiplier:0.##} 跑，表就绪后会再套一次");
			}

			return;
		}

		if (!string.IsNullOrEmpty(row.SprintInputAction))
		{
			InputAction = row.SprintInputAction;
		}

		if (row.SprintSpeedMultiplier > 0f)
		{
			SpeedMultiplier = row.SprintSpeedMultiplier;
		}

		_configured = true;
		Core.GameLog.Write(
			"Config",
			$"奔跑：角色行 {row.Id} → 按键「{InputAction}」（project.godot 里{(InputMap.HasAction(InputAction) ? "有" : "**没有**")}这个动作）/ 移速倍率 {SpeedMultiplier:0.##}（来源：表 Characters，读不到则用场景导出值）");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_configured)
		{
			TryApplyCharacterRowDeferred();
		}

		if (_unit == null || !IsInstanceValid(_unit))
		{
			return;
		}

		_movement ??= _unit.Movement;

		bool hasAction = InputMap.HasAction(InputAction);
		InputHeld = hasAction && Input.IsActionPressed(InputAction);
		if (!hasAction && !_warnedMissingAction)
		{
			_warnedMissingAction = true;
			GameLogWrite($"⚠ 奔跑：`project.godot` 的 [input] 里没有动作「{InputAction}」→ 奔跑永远进不去。");
		}

		_sprinting = Evaluate();
		if (_sprinting)
		{
			if (!_wasSprinting)
			{
				SprintCount++;
				GameLogWrite($"奔跑：进入（按键「{InputAction}」按住 + 有移动意图，移速 ×{SpeedMultiplier:0.##}，累计第 {SprintCount} 次）");
			}
		}
		else if (_wasSprinting)
		{
			GameLogWrite($"奔跑：退出（{LastExitReason}）");
		}

		_wasSprinting = _sprinting;
	}

	/// <summary>这一帧算不算奔跑（只读各种已公开的量，不改任何战斗状态）。</summary>
	private bool Evaluate()
	{
		if (DebugSprintOverride)
		{
			LastExitReason = "（自测强制）";
			return true;
		}

		if (_unit is not { IsDead: false } || !InputHeld || _movement == null)
		{
			LastExitReason = _unit is { IsDead: true } ? "单位已死亡" : "没按奔跑键";
			return false;
		}

		if (RequireGrounded && !_movement.IsGrounded)
		{
			LastExitReason = "在空中（预设：必须落地才算奔跑）";
			return false;
		}

		// 「只有在移动状态时才可以……进入」：读**已经存在的**移动意图，不额外造一个移动量。
		if (_movement.IntentDirection.LengthSquared() < 0.0001f)
		{
			LastExitReason = "没有移动输入";
			return false;
		}

		if (_movement.IsDashing)
		{
			LastExitReason = "位移技能正在生效";
			return false;
		}

		if (ExitWhileAbilityBusy && AnyAbilityBusy())
		{
			LastExitReason = "正在出招（释放技能/武技会退出奔跑）";
			return false;
		}

		return true;
	}

	private bool AnyAbilityBusy()
	{
		if (_unit == null)
		{
			return false;
		}

		Scratch.Clear();
		NodeSearch.CollectInSubtree(_unit, Scratch);
		foreach (Abilities.Ability ability in Scratch)
		{
			if (IsInstanceValid(ability) && ability.Enabled && ability.IsBusy)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// **本次移动意图应该用的速度**：奔跑时 = 基础移速 × <see cref="SpeedMultiplier"/>，否则**原样返回**。
	/// 调用方是 `PlayerController`（"移动的更快"就落在这一行上）。
	/// </summary>
	public float EffectiveSpeed(float baseSpeed)
	{
		if (!IsSprinting || SpeedMultiplier <= 0f)
		{
			return baseSpeed;
		}

		return baseSpeed * SpeedMultiplier;
	}

	/// <summary>探针用：一行的实际状态。</summary>
	public string DebugState()
		=> $"奔跑：奔跑中={IsSprinting} 按键「{InputAction}」按住={InputHeld} 倍率={SpeedMultiplier:0.##} 累计={SprintCount}"
			+ $"｜条件 必须落地={RequireGrounded} 出招即退出={ExitWhileAbilityBusy}｜最近一次退出原因={LastExitReason}";

	private static void GameLogWrite(string message) => Core.GameLog.Write("Combat", message);
}
