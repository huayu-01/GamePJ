using System.Collections.Generic;
using ActRoguelike.Core;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Interaction;

/// <summary>
/// 地面上可交互的物件（策划文档 §6.2 的 Node）。
/// 每局随机布置在候选位置、生成后固定、通常只能用一次；玩家自由探索走近后按键互动。
/// 子类只决定「互动后做什么」，提示与范围检测都在这里。
/// </summary>
public abstract partial class Interactable : Node3D
{
	/// <summary>外观网格的节点名。场景里必须叫这个名字（见 docs/visual-inventory.md §8.3），改名会静默失效。</summary>
	protected const string MeshNodeName = "Mesh";

	/// <summary>动画占位节点的名字（场景里的 AnimationPlayer，可以留空动画）。</summary>
	private const string AnimationNodeName = "Animation";

	/// <summary>出现 / 消失动画的名字：美术往 AnimationPlayer 里放同名动画即可自动接管（见 TryPlayAnimation）。</summary>
	private const string AppearAnimation = "appear";
	private const string DisappearAnimation = "disappear";

	/// <summary>
	/// **状态动画**（用户需求 §五 #15：节点要有「待机 / 激活 / 失效」三种状态动画）。
	/// 三个名字与 <see cref="AnimationNodeName"/> 那个 AnimationPlayer 里的动画一一对应；
	/// **没有对应动画的交互物（Boss 门、基地交互台…）什么都不播**，与 <see cref="AppearAnimation"/> 同一套回退语义。
	/// </summary>
	private const string IdleAnimation = "idle";
	private const string ActiveAnimation = "active";
	private const string DeadAnimation = "dead";

	/// <summary>代码补间版的出现 / 消失时长（秒），只在 AnimationPlayer 里没有对应动画时使用。</summary>
	private const float AppearSeconds = 0.35f;
	private const float DisappearSeconds = 0.45f;

	/// <summary>场景里所有**可用**的交互物（玩家端用它找最近的一个）。
	/// 用掉的物件播完消失动画后会退出这张表，切关 ResetNode() 时登记回来。</summary>
	public static readonly List<Interactable> All = new();

	/// <summary>交互被拒时触发（挑战者状态），供表现层做反馈。</summary>
	public event System.Action? Rejected;

	/// <summary>
	/// 交互**失败**时触发（范围外 / 已用完 / 子类当场不接受…），供表现层做失败反馈
	/// （用户需求 §五 #13：失败响应 + 抖动 + 音效 + 玩家略微击退）。
	///
	/// <para>⚠️ 它只是**多了一条通知**：判定顺序、消耗与复位逻辑一行都没有改
	/// （原来 `if (!CanInteract || !OnInteract()) return false;` 的语义完全不变）。</para>
	/// </summary>
	public event System.Action? Failed;

	/// <summary>提示文字，例如「强化池」。改它会让头顶名牌跟着刷新（见 OnNameChanged）。</summary>
	[Export]
	public string Prompt
	{
		get => _prompt;
		set
		{
			_prompt = value;
			OnNameChanged();
		}
	}

	[Export] public float InteractRadius { get; set; } = 3.2f;

	/// <summary>是否只能用一次（策划文档 §6.2：节点通常只能使用一次）。</summary>
	[Export] public bool SingleUse { get; set; } = true;

	public bool Used { get; private set; }
	public bool PlayerInRange { get; private set; }

	/// <summary>已经消失收场（不可见、不在 Interactable.All 里），连动画播到一半时也算。</summary>
	public bool Vanished { get; private set; }

	public bool CanInteract => !Used && !Vanished && PlayerInRange && Enabled;

	/// <summary>被关卡临时启用的节点才可交互（每局随机布置时关掉没抽中的）。</summary>
	public bool Enabled { get; set; } = true;

	public string CurrentPrompt => Used ? $"{Prompt}（已用）" : $"按 F 使用：{Prompt}";

	/// <summary>
	/// 外观网格（场景里必须叫 <c>Mesh</c>）。**public** 是为了让"只加表现"的
	/// <see cref="InteractablePresentation"/> 能拿到它做描边与抖动 —— 判定逻辑不读它。
	///
	/// <para>先按直接子节点找（老场景零开销）；没有再**递归**找一次 —— 这样"网格节点"可以挂在一层
	/// 中间节点下面（例如 §五 #15 的节点模型：上下浮动做在 <c>Float</c> 上、网格挂在 <c>Float/Mesh</c>，
	/// 免得浮动动画和失败抖动的补间抢同一个 <c>position</c> 轨道）。**名字仍然是唯一契约**，
	/// 只是不再限定"必须是第一层子节点"（见 docs/art-onboarding.md §三.1 与 §五）。</para>
	/// </summary>
	public MeshInstance3D? VisualMesh
		=> GetNodeOrNull<MeshInstance3D>(MeshNodeName) ?? FindChild(MeshNodeName, recursive: true, owned: false) as MeshInstance3D;

	public override void _Ready()
	{
		HideNameplates(this);
		Register();
		ApplyIdleVisual();

		// 表现（#13）：描边 + 按键图标 + 失败响应。**没挂也会自动补一个**，
		// 所以新增交互物不需要改任何预制体（与 UnitAnimator.Ensure 同一条做法）。
		InteractablePresentation.Ensure(this);
	}

	public override void _ExitTree() => All.Remove(this);

	private string _prompt = "互动";
	private float _rangeCheckAccum;
	private bool _vanishing;
	private Tween? _visualTween;

	/// <summary>当前正在播的状态动画（<c>idle</c> / <c>active</c> / <c>dead</c>；没有动画时是 null）。</summary>
	private string? _stateAnimation;

	/// <summary>这个场景里**没有**的状态动画名（查过一次就不再查：Boss 门 / 基地交互台不接状态动画，零开销）。</summary>
	private readonly HashSet<string> _missingStateAnimations = new();

	/// <summary>场景里的动画占位节点（只查一次，见 <see cref="AnimationPlayerNode"/>）。</summary>
	private AnimationPlayer? _animation;
	private bool _animationSearched;

	public override void _PhysicsProcess(double delta)
	{
		// 状态动画（待机 / 激活 / 失效）完全由下面这些**已有的**状态推导出来，放在最前面：
		// 关掉（Enabled=false）的节点也要能切到「失效」，所以必须在下面那个提前 return 之前。
		RefreshStateAnimation();

		if (!Enabled || Used || Vanished)
		{
			PlayerInRange = false;
			return;
		}

		// 交互判定不需要 60Hz：10Hz 足够，而且玩家引用走缓存服务（避免每帧遍历组）。
		_rangeCheckAccum += (float)delta;
		if (_rangeCheckAccum < 0.1f)
		{
			return;
		}

		_rangeCheckAccum = 0f;
		PlayerInRange = PlayerLocator.Player is { } player
			&& player.GlobalPosition.DistanceTo(GlobalPosition) <= InteractRadius;
	}

	/// <summary>玩家按下交互键时调用。返回 true 表示这次互动被接受（会消耗节点）。</summary>
	public bool Interact()
	{
		// 挑战者状态：挑战进行中拒绝一切交互（用户要求：表现上就是「按 F 但被拒绝」）。
		// 判定放在这里，所有交互物共用一份，节点也不会被消耗。
		if (StageController.Instance is { ChallengerActive: true })
		{
			Rejected?.Invoke();
			ActRoguelike.Core.GameLog.Write("Node", $"「{Prompt}」拒绝交互：挑战尚未完成（挑战者状态）");
			return false;
		}

		if (!CanInteract || !OnInteract())
		{
			// 表现层失败反馈（#13）：音效 + 抖动 + 玩家略微击退。判定结果与返回值不变。
			Failed?.Invoke();
			return false;
		}

		if (SingleUse)
		{
			Used = true;
			OnUsed();
		}

		return true;
	}

	protected abstract bool OnInteract();

	/// <summary>切关时复位：重新变成可用状态、恢复原本外观、并重新登记回 Interactable.All。</summary>
	public virtual void ResetNode()
	{
		Used = false;
		PlayerInRange = false;
		_vanishing = false;
		Appear();
	}

	/// <summary>
	/// 出现表现：恢复可见与可交互，然后从缩小的状态弹回原大小。
	/// 以后接美术动画：给节点加 AnimationPlayer 并放一个名为 "appear" 的动画即可接管（见 TryPlayAnimation）。
	/// </summary>
	public void Appear()
	{
		StopVisualTween();
		_vanishing = false;
		Vanished = false;
		Visible = true;
		Scale = Vector3.One;
		Register();
		ApplyIdleVisual();

		// 出现动画（美术接管时）优先：先放它；状态动画留给状态机在它播完之后接上（RefreshStateAnimation 会避让）。
		_stateAnimation = null;

		if (TryPlayAnimation(AppearAnimation) > 0f)
		{
			return;
		}

		// 没有美术动画时的兜底：20% → 100%，带一点回弹。
		Scale = Vector3.One * 0.2f;
		_visualTween = CreateTween();
		_visualTween.TweenProperty(this, "scale", Vector3.One, AppearSeconds)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	/// <summary>
	/// 消失表现（用户要求：用掉的节点直接消失，不再只是"变暗"）：缩小 + 淡出，播完隐藏并退出可交互列表，
	/// 玩家再也选不到它；ResetNode() 能把它恢复成可用状态。
	/// 以后接美术动画：AnimationPlayer 里放一个名为 "disappear" 的动画即可接管。
	/// </summary>
	public void Disappear()
	{
		if (Vanished || _vanishing)
		{
			return;
		}

		_vanishing = true;
		PlayerInRange = false;
		StopVisualTween();

		float animation = TryPlayAnimation(DisappearAnimation);
		if (animation > 0f)
		{
			FinishDisappearAfter(animation);
			return;
		}

		// 淡出走材质的**副本**：一局 10 个节点共用同一份网格材质，动共享资源会把它们一起改掉。
		StandardMaterial3D? fade = FadeCopyOfActiveMaterial();
		_visualTween = CreateTween();
		_visualTween.SetParallel(true);
		_visualTween.TweenProperty(this, "scale", Vector3.One * 0.05f, DisappearSeconds)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		if (fade != null)
		{
			_visualTween.TweenProperty(fade, "albedo_color:a", 0f, DisappearSeconds)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
			_visualTween.TweenProperty(fade, "emission_energy_multiplier", 0f, DisappearSeconds)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		}

		_visualTween.Chain().TweenCallback(Callable.From(FinishDisappear));
	}

	/// <summary>用掉之后的收场（原来只是"变暗"）。子类可以覆盖成别的表现。</summary>
	protected virtual void OnUsed() => Disappear();

	/// <summary>把外观恢复成"可用"状态（子类覆盖，例如奖励节点按类型上色）。</summary>
	protected virtual void ApplyIdleVisual() => VisualMesh?.SetSurfaceOverrideMaterial(0, null);

	/// <summary>名字 / 类型变化后的表现刷新（子类要更新视觉时覆盖它；**头顶不再有名牌**，见 HideNameplates）。</summary>
	protected virtual void OnNameChanged()
	{
	}

	/// <summary>
	/// 用户需求（图标化）：「场景里的那些什么强化池这些不需要显示」——
	/// 交互物**不再在场景里写名字**：名字只在走近后的 HUD 提示、选取界面与鼠标悬停时出现。
	///
	/// 做法是"基类无条件藏掉 <see cref="Label3D"/> 子孙节点"，而不是删掉某一个场景里的节点：
	/// 这样任何交互物（现有的 / 以后新写的）只要挂了名牌就不会露出来，
	/// 而**交互判定、范围检测、消耗与复位逻辑一行都没有动**。
	/// </summary>
	private static void HideNameplates(Node root)
	{
		foreach (Node child in root.GetChildren())
		{
			if (child is Label3D label)
			{
				label.Visible = false;
			}
			else if (child is Node3D)
			{
				HideNameplates(child);
			}
		}
	}

	/// <summary>登记为可交互目标（消失时摘掉，复位 / 出现时登记回来）。</summary>
	private void Register()
	{
		if (!All.Contains(this))
		{
			All.Add(this);
		}
	}

	/// <summary>消失动画播完的收尾：不可见 + 退出可交互列表。</summary>
	private void FinishDisappear()
	{
		// 动画播到一半就被 ResetNode() / Appear() 复位时，这次收尾作废。
		if (!_vanishing)
		{
			return;
		}

		_vanishing = false;
		_visualTween = null;
		Vanished = true;
		Visible = false;
		All.Remove(this);
	}

	/// <summary>美术动画没有回调，用计时器在动画时长后收尾。</summary>
	private void FinishDisappearAfter(float seconds)
	{
		if (GetTree() is { } tree)
		{
			tree.CreateTimer(seconds).Timeout += FinishDisappear;
			return;
		}

		FinishDisappear();
	}

	private void StopVisualTween()
	{
		if (_visualTween != null && IsInstanceValid(_visualTween))
		{
			_visualTween.Kill();
		}

		_visualTween = null;
	}

	/// <summary>
	/// 播 AnimationPlayer 里的同名动画（接美术动画的入口），返回动画时长（秒）。
	/// 没有动画节点 / 没有这个动画时返回 0，调用方走代码补间兜底。
	/// </summary>
	private float TryPlayAnimation(string name)
	{
		if (AnimationPlayerNode() is not { } player || !player.HasAnimation(name))
		{
			return 0f;
		}

		player.Play(name);
		return (float)player.GetAnimation(name).Length;
	}

	/// <summary>场景里的动画占位节点（节点名 <see cref="AnimationNodeName"/>），只查一次。</summary>
	private AnimationPlayer? AnimationPlayerNode()
	{
		if (!_animationSearched)
		{
			_animationSearched = true;
			_animation = GetNodeOrNull<AnimationPlayer>(AnimationNodeName);
		}

		return _animation;
	}

	/// <summary>
	/// **状态动画**（用户需求 §五 #15「有待机 / 激活 / 失效的状态动画」）：
	/// 三种状态**全部由已有的交互状态推导**（没有新增任何状态，也没有新增判定）——
	///
	/// <list type="table">
	/// <item><term>dead 失效</term><description><see cref="Enabled"/> 为 false（本关没抽中的节点 / 降临物下落中）
	///   或已经用完（<see cref="Used"/> / <see cref="Vanished"/>）→ 下沉 + 缩小 + 半透明（"熄灭"）；</description></item>
	/// <item><term>active 激活</term><description><see cref="CanInteract"/>（玩家在范围内、这一刻真的能按 F）→
	///   浮动更高更快 + 自转更快 + 微微胀缩（"被唤醒"）；</description></item>
	/// <item><term>idle 待机</term><description>其余情况 → 慢速上下浮动 + 慢速自转。</description></item>
	/// </list>
	///
	/// <para>⚠️ 它**只读状态、只播动画**：<see cref="Interact"/> 的判定顺序、范围检测、消耗与复位逻辑一行没动。
	/// 动画里刻意**不碰根节点的 scale**（那是 <see cref="Appear"/> / <see cref="Disappear"/> 补间的地盘），
	/// 也不碰共享材质（10 个节点共用一份材质，动它会把同类节点一起改掉）——
	/// 只动节点自己的 <c>Float</c> / <c>Mesh</c> 两个子节点。</para>
	/// </summary>
	private void RefreshStateAnimation()
	{
		if (AnimationPlayerNode() is not { } player)
		{
			return;
		}

		// 出现 / 消失动画播放期间不抢播放器 —— 那两条是"美术接管"的钩子，优先级高于状态循环。
		if (player.IsPlaying() && (player.CurrentAnimation == AppearAnimation || player.CurrentAnimation == DisappearAnimation))
		{
			return;
		}

		string wanted = !Enabled || Used || Vanished
			? DeadAnimation
			: (CanInteract ? ActiveAnimation : IdleAnimation);

		if (wanted == _stateAnimation || _missingStateAnimations.Contains(wanted))
		{
			return;
		}

		if (TryPlayAnimation(wanted) > 0f)
		{
			_stateAnimation = wanted;
		}
		else
		{
			// 这个场景没做这条动画（老节点 / Boss 门 / 基地交互台）→ 记下来，别再每帧查一次。
			_missingStateAnimations.Add(wanted);
			_stateAnimation = null;
		}
	}

#if DEBUG
	/// <summary>自测 / 探针取证用：当前的状态动画名（没有对应动画时是 "-"）。</summary>
	public string DebugStateAnimation => _stateAnimation ?? "-";
#endif

	/// <summary>复制一份当前外观材质并允许透明，用于淡出（复制 = 不会改到共享材质）。</summary>
	private StandardMaterial3D? FadeCopyOfActiveMaterial()
	{
		if (VisualMesh is not { } mesh || mesh.GetActiveMaterial(0) is not StandardMaterial3D source)
		{
			return null;
		}

		var fade = (StandardMaterial3D)source.Duplicate();
		fade.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		mesh.SetSurfaceOverrideMaterial(0, fade);
		return fade;
	}
}
