using System.Collections.Generic;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 单位动画（本轮新增）：按**移动速度**在 <c>idle</c> / <c>walk</c> / 跑 之间切换，死亡时播倒地动画。
///
/// <b>为什么是一个独立组件</b>：10 个敌人 + 首领 + 玩家已经是 Kenney Mini Characters 的 `.glb`
/// （每个 32 段动画），但场景里只有 <c>autoplay = "idle"</c>，所以全部停在静止站姿。
/// 移动/奔跑了该换段动画，这件事与"受击闪色"（<see cref="UnitPresentation"/>）一样属于**表现层**，
/// 不参与任何战斗结算，所以单独一个组件、自己订阅 <see cref="Unit"/> 的事件。
///
/// <b>接入契约（后续加敌人照这个抄，不需要写代码）</b>：
/// <list type="number">
/// <item>场景里：单位下有 <c>Model</c> 节点，<c>Model</c> 子树里有 <c>AnimationPlayer</c>
///   （`.glb` 实例根下 <c>index="1"</c> 那个）—— 这两个都是**约定节点名**，由本组件自动查找。</item>
/// <item>单位节点下的 <c>Animator</c> 子节点：**可以不挂** —— <see cref="UnitPresentation"/> 在
///   <c>_Ready</c> 里会补一个（<see cref="Ensure"/>）。显式挂在场景里的好处是编辑器里能直接改
///   <c>[Export]</c>（右侧 Inspector 有中文分组）。</item>
/// <item>要改动画名 / 速度阈值：优先改**单位表**里那几列（`Config/Tables/Enemies/enemy_*.tres`
///   的「表现 · 动画」组，见 <see cref="UnitConfig.ApplyAnimationTo"/>）——表是权威；
///   留空 / 填 0 就走本组件自身的 <c>[Export]</c> 默认值（表读不到也照样有动画）。</item>
/// </list>
///
/// ⚠️ <b>不碰两处已有的递归表现</b>：受击闪色（<see cref="UnitPresentation"/>）与精英染色
/// （<see cref="EliteModifier.TintRecursive"/>）改的是 <c>MeshInstance3D.MaterialOverride</c>，
/// 本组件改的是 <c>AnimationPlayer</c> 的骨骼轨道，两者互不影响（实测见 docs/art-integration-log.md）。
/// </summary>
public partial class UnitAnimator : Node
{
	/// <summary>约定节点名（场景里显式挂、或由 <see cref="Ensure"/> 自动补的那个名字）。</summary>
	public const string NodeName = "Animator";

	/// <summary>模型所在节点的约定名（`.glb` 实例根，`AnimationPlayer` 在它的子树里）。</summary>
	private const string ModelNodeName = "Model";

	[ExportGroup("动画名（Kenney Mini Characters 里「跑」叫 sprint；换其它模型时改这里或改单位表）")]
	/// <summary>待机动画名（速度低于 <see cref="WalkMinSpeed"/>）。</summary>
	[Export] public string AnimIdle { get; set; } = "idle";

	/// <summary>移动动画名（速度在 <see cref="WalkMinSpeed"/> 与 <see cref="RunMinSpeed"/> 之间）。</summary>
	[Export] public string AnimWalk { get; set; } = "walk";

	/// <summary>奔跑动画名（速度不低于 <see cref="RunMinSpeed"/>）。Kenney 素材里的「跑」就是 <c>sprint</c>。</summary>
	[Export] public string AnimRun { get; set; } = "sprint";

	/// <summary>死亡动画名（播一遍停在倒地那一帧，正好撑到 <see cref="UnitPresentation.DeathLinger"/> 到期）。</summary>
	[Export] public string AnimDeath { get; set; } = "die";

	[ExportGroup("速度阈值（m/s；单位表里填 0 = 用这里的值）")]
	/// <summary>「开始移动」的阈值：低于它算站住。0.15 ≈ 加速起步的那一两帧。</summary>
	[Export] public float WalkMinSpeed { get; set; } = 0.15f;

	/// <summary>
	/// 「开始奔跑」的阈值。默认 4：单位表里的移速大多是 2.6~3.6（走）与 4.2~7（跑），
	/// 所以**不填任何一列**也能得到"慢的走、快的跑"；被减速（`Slow` ×0.55）时会从跑退回走。
	/// </summary>
	[Export] public float RunMinSpeed { get; set; } = 4f;

	[ExportGroup("过渡")]
	/// <summary>切换动画时的混合时间（秒）；0 = 硬切。开跑/收脚用一小段混合看起来不跳。</summary>
	[Export] public float BlendSeconds { get; set; } = 0.15f;

	private static readonly HashSet<string> LoggedScenes = new();
	private static readonly HashSet<string> LoggedSwitches = new();
	private static readonly HashSet<string> LoggedWarnings = new();

	private Unit _unit = null!;
	private AnimationPlayer? _player;
	private string _idle = string.Empty;
	private string _walk = string.Empty;
	private string _run = string.Empty;
	private string _death = string.Empty;
	private string _current = string.Empty;
	private string _source = "组件默认值";
	private float _speed;
	private bool _dead;

	/// <summary>
	/// 找单位下的 <c>Animator</c>；**没有就补一个** —— 这样"新增一个敌人"不需要在场景里手动挂节点，
	/// 只要有 `Model/…/AnimationPlayer` 就自动有动画（读不到表也成立）。
	/// </summary>
	public static UnitAnimator Ensure(Unit unit)
	{
		if (unit.GetNodeOrNull<UnitAnimator>(NodeName) is { } existing)
		{
			return existing;
		}

		var animator = new UnitAnimator { Name = NodeName };
		unit.AddChild(animator);
		return animator;
	}

	/// <summary>
	/// 单位表的「表现 · 动画」几列 → 本组件。**空字符串 / 0 = 不覆盖**（留空就用本组件的默认值），
	/// 与攻击组"0 = 不覆盖"是同一条纪律：只想改一个字段不会把别的字段写成空。
	/// </summary>
	public void ApplyRow(UnitConfig row)
	{
		bool applied = false;

		if (!string.IsNullOrEmpty(row.AnimIdle))
		{
			AnimIdle = row.AnimIdle;
			applied = true;
		}

		if (!string.IsNullOrEmpty(row.AnimWalk))
		{
			AnimWalk = row.AnimWalk;
			applied = true;
		}

		if (!string.IsNullOrEmpty(row.AnimRun))
		{
			AnimRun = row.AnimRun;
			applied = true;
		}

		if (!string.IsNullOrEmpty(row.AnimDeath))
		{
			AnimDeath = row.AnimDeath;
			applied = true;
		}

		if (row.AnimWalkMinSpeed > 0f)
		{
			WalkMinSpeed = row.AnimWalkMinSpeed;
			applied = true;
		}

		if (row.AnimRunMinSpeed > 0f)
		{
			RunMinSpeed = row.AnimRunMinSpeed;
			applied = true;
		}

		if (applied)
		{
			_source = $"表 {row.Id}";
		}
	}

	/// <summary>当前正在播的动画名（调试 / 运行时探针用；没绑到播放器时是空串）。</summary>
	public string CurrentAnimation => _current;

	/// <summary>这一条的实际参数来源（"表 enemy_chaser" / "组件默认值"），日志与探针用。</summary>
	public string Source => _source;

	public override void _Ready()
	{
		_unit = GetParent<Unit>();
		_unit.Died += OnDied;
		Bind();
	}

	public override void _ExitTree()
	{
		if (IsInstanceValid(_unit))
		{
			_unit.Died -= OnDied;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_dead || _player == null)
		{
			return;
		}

		Vector3 velocity = _unit.Velocity;
		_speed = new Vector2(velocity.X, velocity.Z).Length();

		Play(_speed >= RunMinSpeed ? _run : _speed >= WalkMinSpeed ? _walk : _idle, BlendSeconds);
	}

	/// <summary>死亡：切成倒地动画（一次性，停在最后一帧），之后不再按速度切。</summary>
	private void OnDied(HitInstance hit)
	{
		_dead = true;
		Play(_death, 0f);
	}

	/// <summary>
	/// 绑定 <c>Model</c> 子树里的 <c>AnimationPlayer</c>，并把用到的几段都调成**循环**。
	///
	/// ⚠️ 必须自己设循环：`.glb` 导入的动画全是 `LOOP_NONE`（"播一遍停在最后一帧"），
	/// 所以场景里那句 <c>autoplay="idle"</c> 只是摆了个静止姿势（docs/art-integration-log.md §6.3）。
	/// 死亡那段**故意保持不循环** —— 停在倒地帧正好。
	/// </summary>
	private void Bind()
	{
		if (GetParent().GetNodeOrNull<Node>(ModelNodeName) is { } model)
		{
			_player = NodeSearch.FindInSubtree<AnimationPlayer>(model);
		}

		if (_player == null)
		{
			// 没有模型 / 没有动画的单位（木桩、无人机…）：静默不参与，不报错、也没有每帧开销。
			return;
		}

		_idle = Resolve(AnimIdle, null);
		_walk = Resolve(AnimWalk, _idle);
		_run = Resolve(AnimRun, _walk);
		_death = Resolve(AnimDeath, null);

		SetLoop(_idle, true);
		SetLoop(_walk, true);
		SetLoop(_run, true);
		SetLoop(_death, false);

		LogOnce();
		Play(_idle, 0f);
	}

	/// <summary>
	/// 解析一个动画名：优先精确匹配，其次**忽略大小写**（换个素材写成 `Idle` / `Run` 也能直接用），
	/// 都不命中就退到 <paramref name="fallback"/>（例如 32 段里没有 <c>sprint</c> 就退回 <c>walk</c>），
	/// 退不了就返回空串（不播那一段）。退回时会写一条警告，避免"配了名字却没生效"静默发生。
	/// </summary>
	private string Resolve(string wanted, string? fallback)
	{
		if (_player == null || string.IsNullOrEmpty(wanted))
		{
			return fallback ?? string.Empty;
		}

		if (_player.HasAnimation(wanted))
		{
			return wanted;
		}

		foreach (string name in _player.GetAnimationList())
		{
			if (string.Equals(name, wanted, System.StringComparison.OrdinalIgnoreCase))
			{
				return name;
			}
		}

		// 同一种单位只提醒一次（同一个 .glb 每生成一只都会走到这里，不收敛会刷屏）。
		if (LoggedWarnings.Add($"{_unit.SceneFilePath}|{wanted}"))
		{
			GD.PushWarning(
				$"[UnitAnimator] {_unit.Name} 的模型里没有动画「{wanted}」，已回退到「{fallback ?? "（无）"}」"
				+ $"（现有 {_player.GetAnimationList().Length} 段）。");
		}

		return fallback != null && _player.HasAnimation(fallback) ? fallback : string.Empty;
	}

	private void SetLoop(string name, bool loop)
	{
		if (_player?.GetAnimation(name) is { } animation)
		{
			// 同一个 .glb 的所有实例共享这几段 Animation，所以重复设成同一个值是幂等的。
			animation.LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
		}
	}

	/// <summary>切动画（同一段不重复 Play，避免每帧把混合时间重置）。</summary>
	private void Play(string name, float blend)
	{
		if (_player == null || string.IsNullOrEmpty(name) || name == _current)
		{
			return;
		}

		string from = _current;
		_player.Play(name, blend);
		_current = name;

		// 每次切换留一行日志（**每种单位各记一次**，不会刷屏）：调参时"为什么它没动"能直接看出来。
		if (from.Length > 0 && LoggedSwitches.Add($"{_unit.SceneFilePath}|{from}->{name}"))
		{
			GameLog.Write("Combat", $"单位动画切换：{_unit.Name} {from} → {name}（当前速度 {_speed:0.##}，阈值 {WalkMinSpeed:0.##}/{RunMinSpeed:0.##}）");
		}
	}

	/// <summary>每个场景只写一行日志：证明"这一段动画是从哪儿来的"（表 or 组件默认值）。</summary>
	private void LogOnce()
	{
		string scene = _unit.SceneFilePath;
		if (!LoggedScenes.Add($"{scene}|{_source}"))
		{
			return;
		}

		GameLog.Write(
			"Combat",
			$"单位动画：{_unit.Name} 共 {_player!.GetAnimationList().Length} 段 · idle={_idle} walk={_walk} run={_run} die={_death} · "
			+ $"阈值 {WalkMinSpeed:0.##}/{RunMinSpeed:0.##}（来源：{_source}）");
	}
}
