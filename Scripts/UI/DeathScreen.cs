using ActRoguelike.Core;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// **死亡流程的过渡面板**（用户需求 §五 #3：「玩家死亡时 → 进死亡动画 → 然后进演示动画
/// （相当于场景切换的过渡，目前没有可以给个简单的面板做占位）→ 回到基地」）。
///
/// <para>三段时间轴（都能在 Inspector 上改）：</para>
/// <list type="number">
/// <item><b>死亡动画</b>（<see cref="DeathAnimationSeconds"/>）：面板还不出现，世界照常渲染 ——
///   玩家的倒地动画由 <c>UnitAnimator</c>/<c>UnitPresentation</c> 播（它们在这段时间里不能被暂停，
///   所以这一段**不暂停**整棵树）；</item>
/// <item><b>演示过渡（占位面板）</b>（<see cref="HoldSeconds"/>）：淡入一整屏面板 + 倒计时进度条，
///   这里就是用户说的"演示动画"的占位；这一段时间树是暂停的（防止背后继续打）；</item>
/// <item><b>回基地</b>：倒计时归零（或按 Enter / 空格跳过）→ 切到 <see cref="TargetScene"/>。</item>
/// </list>
///
/// <para><b>为什么不像其它界面那样继承 <c>OverlayScreen</c></b>：与 <c>LoadingScreen</c> 同样的理由 ——
/// 它是**流程节点**而不是"弹层"：不走界面栈、要自己控制暂停时机（第一段绝不能暂停），
/// 而且必须能在暂停期间继续走自己的计时（<c>ProcessMode = Always</c>）。
/// 正因为如此，它挂在场景根下（<c>Main.tscn</c> 的 <c>DeathScreen</c>），由 <c>RunController</c> 按名字取用。</para>
///
/// <para>⚠️ 按用户要求，这里**不做结算系统**：只把结算文案原样显示出来 + 一段占位过渡。
/// 正式版把 <see cref="HoldSeconds"/> 那一段换成真正的演示/回放即可。</para>
/// </summary>
public partial class DeathScreen : CanvasLayer
{
	/// <summary>死亡动画播多久（秒）：玩家的倒地动画 + <c>UnitPresentation.DeathLinger</c> 大致就这么长。</summary>
	[Export] public float DeathAnimationSeconds { get; set; } = 1.4f;

	/// <summary>演示过渡面板停留多久（秒）。到点自动回基地；中途按 Enter / 空格可以跳过。</summary>
	[Export] public float HoldSeconds { get; set; } = 3.2f;

	/// <summary>面板淡入时长（秒）。</summary>
	[Export] public float FadeSeconds { get; set; } = 0.45f;

	/// <summary>回哪个场景（默认基地）。</summary>
	[Export] public string TargetScene { get; set; } = "res://Scenes/Levels/Base.tscn";

	private enum Phase
	{
		/// <summary>还没开始（或已经结束）。</summary>
		Idle,

		/// <summary>等死亡动画播完。</summary>
		DeathAnimation,

		/// <summary>演示过渡面板（占位）。</summary>
		Panel,
	}

	private Control _root = null!;
	private Label _title = null!;
	private Label _body = null!;
	private Label _hint = null!;
	private ProgressBar _countdown = null!;

	private Phase _phase = Phase.Idle;
	private float _timer;
	private float _fade;

	/// <summary>死亡流程正在播（验证用只读值）。</summary>
	public bool IsPlaying => _phase != Phase.Idle;

	/// <summary>当前处于哪一段（验证用只读值）："idle" / "death" / "panel"。</summary>
	public string PhaseName => _phase switch
	{
		Phase.DeathAnimation => "death",
		Phase.Panel => "panel",
		_ => "idle",
	};

	/// <summary>倒计时进度 0→1（验证用只读值）。</summary>
	public float CountdownProgress => (float)_countdown.Value;

	public override void _Ready()
	{
		// 第一段要跟世界一起走、第二段要能在暂停中继续计时 —— 只有 Always 同时满足。
		ProcessMode = ProcessModeEnum.Always;
		_root = GetNode<Control>("Root");
		_title = GetNode<Label>("Root/Panel/VBox/Title");
		_body = GetNode<Label>("Root/Panel/VBox/Body");
		_hint = GetNode<Label>("Root/Panel/VBox/Hint");
		_countdown = GetNode<ProgressBar>("Root/Panel/VBox/Countdown");

		_root.Visible = false;
		_root.Modulate = new Color(1f, 1f, 1f, 0f);
		_hint.Text = $"演示动画（占位）· 按 Enter / 空格跳过，{HoldSeconds:0.#} 秒后回基地";
	}

	/// <summary>开始死亡流程：先死亡动画，再演示过渡面板，最后回基地。</summary>
	public void BeginDeath(string title, string body)
	{
		if (IsPlaying)
		{
			return;
		}

		// 死亡时可能还压着界面（奖励三连/商店）：先放开它们，否则第一段动画会被暂停冻住。
		UiService.Instance?.CloseAll();
		GetTree().Paused = false;

		_title.Text = title;
		_body.Text = body;
		_countdown.Value = 0d;
		_root.Visible = true;
		_root.Modulate = new Color(1f, 1f, 1f, 0f);
		_phase = Phase.DeathAnimation;
		_timer = DeathAnimationSeconds;
		_fade = 0f;

		GameLog.Write("Flow", $"死亡流程：进死亡动画（{DeathAnimationSeconds:0.#}s，期间不暂停）→ 演示过渡面板（占位 {HoldSeconds:0.#}s）→ {TargetScene}");
	}

	public override void _Process(double delta)
	{
		if (_phase == Phase.Idle)
		{
			return;
		}

		float dt = (float)delta;

		if (_phase == Phase.DeathAnimation)
		{
			_timer -= dt;
			if (_timer <= 0f)
			{
				EnterPanel();
			}

			return;
		}

		// 面板段：淡入 → 倒计时（可跳过）。
		if (_fade < 1f)
		{
			_fade = FadeSeconds > 0.01f ? Mathf.Min(1f, _fade + (dt / FadeSeconds)) : 1f;
			_root.Modulate = new Color(1f, 1f, 1f, _fade);
		}

		if (Input.IsActionJustPressed("ui_accept"))
		{
			GameLog.Write("Flow", "死亡流程：玩家按 Enter / 空格跳过演示过渡");
			ReturnToBase();
			return;
		}

		_timer -= dt;
		_countdown.Value = HoldSeconds > 0.01f ? Mathf.Clamp(1f - (_timer / HoldSeconds), 0d, 1d) : 1d;
		if (_timer <= 0f)
		{
			ReturnToBase();
		}
	}

	private void EnterPanel()
	{
		_phase = Phase.Panel;
		_timer = HoldSeconds;
		_countdown.Value = 0d;

		// 这里是"场景切换的过渡"：背后不该再打，所以这一段暂停整棵树（本节点是 Always，计时照走）。
		GetTree().Paused = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		GameLog.Write("Flow", $"死亡流程：进演示过渡面板（占位）—— 本局结算已显示，{HoldSeconds:0.#}s 后自动回基地");
	}

	private void ReturnToBase()
	{
		_phase = Phase.Idle;
		_root.Visible = false;

		// 切场景前必须解除暂停（Command line 自测与正常游玩都靠这一条回到可用状态）。
		GetTree().Paused = false;
		RunSetup.Clear();
		GameLog.Write("Flow", $"死亡流程：演示过渡结束 → 回到基地场景 {TargetScene}");
			RuntimeResources.ChangeScene(GetTree(), TargetScene);
	}
}
