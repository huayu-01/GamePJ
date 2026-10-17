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

	/// <summary>技能动画库的目录/库名（来自单位表；空 = 这个单位没有技能动画）。</summary>
	private string _skillDir = string.Empty;
	private string _skillLibrary = "skills";

	/// <summary>已经真的把技能动画库加进播放器了（惰性、只做一次）。</summary>
	private bool _skillLibraryLoaded;

	/// <summary>正在播技能动画：这段时间**不按移速切** idle/walk/sprint（否则技能第一帧就被运动覆盖掉）。</summary>
	private bool _skillPlaying;

	/// <summary>当前正在播的技能动画全名（`skills/Skill_A1_ATK1`），用于判断"播完了"。</summary>
	private string _skillName = string.Empty;

	/// <summary>当前这段技能动画上的事件帧（时刻 + 名字 + 参数）。</summary>
	private readonly List<AnimationEventKey> _skillEvents = new();

	/// <summary>上一次取的动画播放位置（用来判"这一帧跨过了哪个事件帧"）。</summary>
	private double _skillLastPosition;

	/// <summary>已经触发过的事件帧下标（每段动画只触发一次）。</summary>
	private readonly HashSet<int> _skillFiredEvents = new();

	/// <summary>
	/// **技能动画事件帧的解析**（授权格式仍是 Godot 的方法轨道 —— 生成器照旧写
	/// `anim_event` + args；运行期由这里把方法轨道上的关键帧读出来，在播放位置跨过它的那一帧触发）。
	///
	/// ⚠️ **为什么不用引擎的方法轨道回调**：实测在 Godot 4.7.2 里，技能动画的方法轨道
	/// **一次都不会落到目标节点上**，而且会在混合器内部直接让进程崩掉
	/// （`exit -1073741819` = 访问违规；同一份 `.tres` 在临时工程里、以及自测那个独立探针节点上都能正常触发）。
	/// 所以运行期改用**位置跨越**：语义与"事件帧"完全一致（文档："子弹在事件帧到 ATK 时发射"），
	/// 当帧生效、跟着 `speed_scale` 走、零引擎回调风险。授权格式没变，以后引擎修好了把开关一翻就行。
	/// </summary>
	private readonly record struct AnimationEventKey(double Time, string Name, string[] Args);

	/// <summary>这次技能动画是否循环（循环的技能动画（如蓄力）不参与"播完就退出"的判定）。</summary>
	private bool _skillLoops;

	/// <summary>
	/// 技能动画**还没接上**（<c>AnimationPlayer.Play</c> 是延迟生效的：刚调完的那一帧
	/// <c>CurrentAnimation</c> 还是上一段）。这几帧里**不能**判"播完了"，否则技能动画会在
	/// 还没开始播的时候就被这里的每帧守卫取消掉（实测：技能动画一秒都没播、事件帧永远不来）。
	/// </summary>
	private int _skillStartupGrace;

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

		// 技能动画库（空 = 这个单位没有技能动画，例如木桩/无人机）。
		if (!string.IsNullOrEmpty(row.AnimLibraryDir))
		{
			_skillDir = row.AnimLibraryDir;
			if (!string.IsNullOrEmpty(row.AnimLibraryName))
			{
				_skillLibrary = row.AnimLibraryName;
			}

			applied = true;
		}

		if (applied)
		{
			_source = $"表 {row.Id}";
		}
	}

	/// <summary>当前正在播的动画名（调试 / 运行时探针用；没绑到播放器时是空串）。</summary>
	public string CurrentAnimation => _current;
	/// <summary>每次播放／停止均换代；能力只能接收自己那次播放的事件。</summary>
	public ulong SkillPlaybackId { get; private set; }
	public double SkillPlaybackPosition => Mathf.Max(0, _skillPlaying && _player != null
		&& _player.CurrentAnimation == _skillName ? _player.CurrentAnimationPosition : _skillLastPosition);
	public event System.Action<string, ulong>? SkillEvent;

	/// <summary>
	/// 技能动画事件帧的**分发口**（用户在动画里挂的 `anim_event` 帧会走到这里）。
	///
	/// 为什么用事件而不是让技能自己轮询：事件帧必须**当帧**生效（`CallbackModeMethod = Immediate`），
	/// 而 <c>Ability._PhysicsProcess</c> 与 <c>UnitAnimator</c> 的物理回调谁先谁后由节点顺序决定 ——
	/// 走事件就等于"逻辑帧跟着动画走"，不依赖任何顺序假设。
	///
	/// 这是历史自测统计口。战斗能力使用实例SkillEvent和播放序号，不假设场上只有一个动画使用者。
	/// </summary>
	public static event System.Action<string>? AnimationEvent;

	/// <summary>场地里最后一个可用的 <see cref="UnitAnimator"/>（方法轨道的回调要静态入口）。</summary>
	private static UnitAnimator? _currentAnimator;

	/// <summary>
	/// 动画方法轨道的接收方法（名字写死在这 —— 它就是动画资源里 <c>"method": &amp;"anim_event"</c> 那一项）。
	///
	/// ⚠️ 方法轨道的 path 是**相对 <c>AnimationMixer.RootNode</c>** 的；项目的 <c>AnimationPlayer</c>
	/// 挂在 <c>Model</c>（`.glb` 实例根）子树里、<c>root_node</c> 是 <c>".."</c>，所以轨道 path 写
	/// <c>".."</c> 正好落在 <c>Unit</c> 下 —— 也就是本组件身上（见 <c>SkillAnimationConfig.EventTargetPath</c>）。
	/// </summary>
	/// <summary>
	/// **把一次动画事件帧广播出去**（由 <see cref="Unit.anim_event"/> 转发进来；方法轨道本身落在 Unit 上）。
	///
	/// ⚠️ 方法轨道的 path 是**相对 <c>AnimationMixer.RootNode</c>** 的；项目的 <c>AnimationPlayer</c>
	/// 挂在 <c>Model</c>（`.glb` 实例根）子树里、<c>root_node</c> 是 <c>".."</c>，所以轨道 path 写
	/// <c>".."</c> 时调用落在 <c>Unit</c> 上 —— 也就是本组件的父节点（见 <c>SkillAnimationConfig.EventTargetPath</c>）。
	/// </summary>
	/// <param name="name">事件名（`ATK` / `Dodge` / `Release` …，来自配置表）。</param>
	public void RaiseAnimationEvent(string name)
	{
		SkillEvent?.Invoke(name, SkillPlaybackId);
		// 历史全局口仅供旧自测统计；战斗能力订阅本单位实例事件。
		AnimationEvent?.Invoke(name);
	}

	/// <summary>这一条的实际参数来源（"表 enemy_chaser" / "组件默认值"），日志与探针用。</summary>
	public string Source => _source;

	public override void _Ready()
	{
		_unit = GetParent<Unit>();
		_unit.Died += OnDied;
		_currentAnimator = this;
		Bind();
	}

	public override void _ExitTree()
	{
		if (_currentAnimator == this)
		{
			_currentAnimator = null;
		}

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

		if (_skillPlaying)
		{

			// 技能动画不受移速影响；非循环的那几段播完就交还给运动（按移速切）。
			if (_skillStartupGrace > 0)
			{
				// Play 是延迟生效的：给两帧时间让 CurrentAnimation 真的切过去，再开始判"播完没有"。
				_skillStartupGrace--;
				return;
			}

			// 事件帧：在播放位置跨过它的这一帧触发（当帧生效，跟 speed_scale 走）。
			ulong playbackId = SkillPlaybackId;
			DriveSkillEvents();
			if (SkillPlaybackId != playbackId) return;

			// ⚠️ 判"播完"必须比的是 **_skillName**，不能比 _current：
			// `Play(name)` 内部有"同名不重复"的早退（`name == _current`），而 _current 在
			// PlaySkillAnimation 里已经被改成技能名了 —— 一旦这里把 _current 清空，同一次技能
			// 就会被"复活"成一份新的播放（实测：动画永远停在 0.0000，事件帧永远不来）。
			if (!_skillLoops && _player.CurrentAnimation != _skillName)
			{
				_skillPlaying = false;
				_current = string.Empty;
				_player.SpeedScale = 1f;
			}
			else
			{
				return;
			}
		}

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
	///
	/// ⚠️ <b>技能动画库不在这里加载</b>：角色行的 <see cref="UnitConfig.AnimLibraryDir"/> 有可能
	/// 晚于本组件的 <c>_Ready</c> 才套上来（<c>RunController.ApplyBalance</c> 在场景实例化之后跑），
	/// 所以走 <see cref="RequireAnimation"/> 惰性加载。同样地，这里只给**本实例自己的播放器**设属性，
	/// 不再去改 `.glb` 的共享 <c>Animation</c> 资源。
	/// </summary>
	/// <summary>换角色时把真实动画事件驱动切到可见模型，不释放旧播放器。</summary>
	public void BindVisual(Node model)
	{
		if (_player == NodeSearch.FindInSubtree<AnimationPlayer>(model)) return;
		StopSkillAnimation();
		_player = null;
		_skillLibraryLoaded = false;
		Bind(model);
	}

	private void Bind(Node? visual = null)
	{
		if ((visual ?? GetParent().GetNodeOrNull<Node>(ModelNodeName)) is { } model)
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

		// 场景里已经填了技能动画库目录的话，**在 _Ready 里就加载**（不要等到第一次放技能）。
		// 实测：等 AnimationPlayer 已经跑了很久、再运行期 AddAnimationLibrary + Play，
		// 会让引擎在下一帧崩掉（exit -1073741819）；在 _Ready 里一次加载好就没事
		// （自测里的独立探针也是"加载完再播"这条顺序，一直正常）。
		PrepareSkillLibrary();
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

	/// <summary>
	/// 设置"idle / walk / 跑"这几段的循环。
	///
	/// ⚠️ <b>这里确实会写共享资源</b>（`.glb` 导入的那几段 `Animation` 是 13 个模型文件各自实例共享的子资源），
	/// 但**只在值与当前不同时才写** —— 这样就永远不会有"A 单位把它设成循环、B 单位改成不循环"的互相踩踏，
	/// 因为这几个循环标记在所有单位上都是同一个常量（idle/walk/跑 循环、die 不循环）。
	///
	/// <b>技能动画不走这里</b>：它们的循环标记在生成期写进各自的 `.tres`（<c>SkillAnimationConfig.Loop</c>），
	/// 每个单位加载到的是独立资源 —— 那才是"按单位区分循环"的正确落点。
	/// </summary>
	private void SetLoop(string name, bool loop)
	{
		if (_player?.GetAnimation(name) is not { } animation)
		{
			return;
		}

		Animation.LoopModeEnum wanted = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
		if (animation.LoopMode != wanted)
		{
			animation.LoopMode = wanted;
		}
	}

	/// <summary>
	/// **技能动画的入口**（技能的能力在激活时调用它）。
	///
	/// 返回动画的**完整名**（`skills/Skill_A1_ATK1`）与加载进来的段数；段数 = 0 表示这个单位
	/// 根本没有技能动画可播 —— 调用方（技能）据此**拒绝执行**（用户裁定：没动画不执行）。
	///
	/// ⚠️ <c>speed_scale</c> 设在**播放器**上（每实例独立，实测 498ms → 262ms），
	/// 这样"加速动画释放"时事件帧仍然精确命中（实测 <c>anim_pos</c> = 0.1000 / 0.2518 / 0.4015）。
	/// </summary>
	/// <param name="animationName">技能动画名（表里的 <c>AnimName</c>，不带库名也行）。</param>
	/// <param name="speedScale">动画速度倍率（1 = 原速；蓄力类技能用 &lt;1 拉长）。</param>
	/// <param name="blend">混合时间（秒）。</param>
	public string PlaySkillAnimation(string animationName, float speedScale, float blend = 0.08f)
	{
		PrepareSkillLibrary();
		if (_player == null || string.IsNullOrEmpty(animationName))
		{
			return string.Empty;
		}

		string full = SkillAnimationLibrary.ResolveExact(_player, _skillLibrary, animationName);
		if (full.Length == 0)
		{
			return string.Empty;
		}

		// ⚠️ **不要调 `_player.Stop()`**：实测（Godot 4.7.2）在"运行期才加进来动画库"的 AnimationPlayer 上，
		// `Stop()` 之后紧接 `Seek()/Play()` 会把进程直接搞崩（exit -1073741819，访问违规）；
		// 只 `Seek(0)` + `Play()` 就足够把技能动画从头播（`Play` 本身会重置到该段的起点）。
		_player.SpeedScale = speedScale <= 0f ? 1f : speedScale;
		_player.Play(full);
		// Godot对正在播放的同名动画会继续旧位置；新动作必须重新从0开始，且不调用有崩溃历史的Stop。
		_player.Seek(0);
		SkillPlaybackId++;

		_skillName = full;
		_skillPlaying = true;
		_skillStartupGrace = 2;
		_skillLoops = _player.GetAnimation(full)?.LoopMode != Animation.LoopModeEnum.None;
		_current = full;
		ReadSkillEvents(full);
		GameLog.Write(
			"Animation",
			$"技能动画开播：{_unit.Name} → {full}（speed_scale={_player.SpeedScale:0.##}，循环={_skillLoops}，"
			+ $"事件帧 {_skillEvents.Count} 个：{DescribeSkillEvents()}）");
		return full;
	}

	/// <summary>让技能动画立刻停手（技能被取消 / 单位死亡时调用），把控制权交还运动动画。</summary>
	public void StopSkillAnimation()
	{
		SkillPlaybackId++;
		_skillPlaying = false;
		_skillStartupGrace = 0;
		_skillName = string.Empty;
		_skillEvents.Clear();
		_skillFiredEvents.Clear();
		_current = string.Empty;
		if (_player != null)
		{
			_player.SpeedScale = 1f;
		}
	}

	/// <summary>
	/// 把一段技能动画上的方法轨道事件帧读成"时刻 + 名字 + 参数"。
	///
	/// ⚠️ **为什么由本项目自己读方法轨道，而不是交给引擎回调**：实测在 Godot 4.7.2 里，
	/// 技能动画的方法轨道**一次都不会落到目标节点上**，并且会在混合器内部直接让进程崩掉
	/// （`exit -1073741819` = 访问违规）。同一份 `.tres` 在临时工程里、以及自测里那个独立探针
	/// 节点上都能正常触发，所以这是"真项目这套层级 + 该引擎版本"上的问题，不是资源写坏了。
	///
	/// 于是运行期改成**位置跨越**：授权格式（方法轨道 + `anim_event` + args）**一点没变**，
	/// 只是由这里把关键帧读出来，在播放位置跨过它的那一帧触发 —— 时机与引擎回调完全一致，
	/// 跟着 `speed_scale` 走，且零引擎回调风险。以后引擎修好了，把这里换成回调即可。
	/// </summary>
	private void ReadSkillEvents(string full)
	{
		_skillEvents.Clear();
		_skillFiredEvents.Clear();
		_skillLastPosition = 0.0;

		if (_player?.GetAnimation(full) is not { } animation)
		{
			return;
		}

		for (int track = 0; track < animation.GetTrackCount(); track++)
		{
			if (animation.TrackGetType(track) != Animation.TrackType.Method)
			{
				continue;
			}

			for (int key = 0; key < animation.TrackGetKeyCount(track); key++)
			{
				Variant raw = animation.TrackGetKeyValue(track, key);
				if (raw.VariantType != Variant.Type.Dictionary)
				{
					continue;
				}

				var dict = raw.AsGodotDictionary();
				if (!dict.TryGetValue("method", out Variant methodValue)
					|| methodValue.AsString() != "anim_event"
					|| !dict.TryGetValue("args", out Variant argsValue)
					|| argsValue.VariantType != Variant.Type.Array)
				{
					continue;
				}

				var args = argsValue.AsGodotArray();
				if (args.Count == 0)
				{
					continue;
				}

				var rest = new string[args.Count - 1];
				for (int i = 1; i < args.Count; i++)
				{
					rest[i - 1] = args[i].AsString();
				}

				_skillEvents.Add(new AnimationEventKey(animation.TrackGetKeyTime(track, key), args[0].AsString(), rest));
			}
		}

		_skillEvents.Sort((a, b) => a.Time.CompareTo(b.Time));
	}

	/// <summary>事件帧清单（日志用）。</summary>
	private string DescribeSkillEvents()
	{
		if (_skillEvents.Count == 0)
		{
			return "（无）";
		}

		var text = new System.Text.StringBuilder();
		foreach (AnimationEventKey key in _skillEvents)
		{
			text.Append(text.Length > 0 ? "、" : string.Empty);
			text.Append($"{key.Name}@{key.Time:0.###}s");
		}

		return text.ToString();
	}

	/// <summary>
	/// 每物理帧检查："播放位置从上一帧到这一帧"跨过了哪些事件帧 → 当帧触发。
	/// 未触发过的事件帧只触发一次；动画回到起点（循环或重播）时重置。
	/// </summary>
	private void DriveSkillEvents()
	{
		if (_skillEvents.Count == 0 || _player == null)
		{
			return;
		}

		ulong playbackId = SkillPlaybackId;
		double now = _player.CurrentAnimationPosition;
		if (now + 1e-6 < _skillLastPosition)
		{
			// 动画回到起点（循环段 / 重播）：允许事件帧再次触发。
			_skillFiredEvents.Clear();
		}

		for (int i = 0; i < _skillEvents.Count; i++)
		{
			if (_skillFiredEvents.Contains(i))
			{
				continue;
			}

			AnimationEventKey key = _skillEvents[i];
			if (now + 1e-6 < key.Time)
			{
				continue;
			}

			_skillFiredEvents.Add(i);
			RaiseAnimationEvent(key.Name);
			// 回调可能换段或交接动作；不能用旧播放位置继续驱动新一轮事件表。
			if (SkillPlaybackId != playbackId) return;
		}

		_skillLastPosition = now;
	}

	/// <summary>技能动画库是否已经就位（技能"没动画不执行"的判定依据）。</summary>
	public bool HasSkillLibrary => SkillAnimationLibrary.ListAnimationFiles(_skillDir).Length > 0;

	/// <summary>
	/// 按名查一段技能动画（**全路径**，忽略大小写）。返回空串 = 没这段动画。
	///
	/// ⚠️ 只有这一条路：<c>AnimationPlayer.HasAnimation("Skill_A1_ATK1")</c> 对命名库里的动画
	/// **永远返回 false**（实测），所以技能"先查动画是否存在"必须走这里。
	/// </summary>
	public string ResolveSkillAnimation(string animationName)
	{
		PrepareSkillLibrary();
		return _player == null ? string.Empty : SkillAnimationLibrary.ResolveExact(_player, _skillLibrary, animationName);
	}

	/// <summary>
	/// 惰性把技能动画库加进本单位的播放器（每实例一份）。已经加过就直接返回。
	///
	/// ⚠️ <c>SkillAnimationLibrary.LoadInto</c> 内部会设 <c>CallbackModeMethod = Immediate</c> ——
	/// 事件帧必须**当帧**生效（默认 Deferred 会晚一帧、且排在物理之后，实测确认）。
	/// </summary>
	public void PrepareSkillLibrary()
	{
		if (_skillLibraryLoaded || _player == null || string.IsNullOrEmpty(_skillDir))
		{
			return;
		}

		int loaded = SkillAnimationLibrary.LoadInto(_player, _skillLibrary, _skillDir);
		if (loaded > 0)
		{
			_skillLibraryLoaded = true;
			GameLog.Write(
				"Animation",
				$"技能动画库已加载：{_unit.Name} ← {_skillDir}（库「{_skillLibrary}」{loaded} 段；"
				+ $"回调模式 {_player.CallbackModeMethod}，查名用全路径如「{_skillLibrary}/<动画名>」）");
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


