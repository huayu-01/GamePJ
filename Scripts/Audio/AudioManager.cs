using System.Collections.Generic;
using System.Text;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Audio;

/// <summary>
/// **音频播放的唯一入口**（autoload）。
///
/// <para><b>调用方只写 id，永远不需要知道文件在哪</b>：
/// <c>AudioManager.PlaySfx(AudioIds.WeaponFire)</c> / <c>PlayUi(...)</c> / <c>PlayMusic(...)</c>。
/// 文件路径 / 音量 / 音高随机范围 / 走哪条总线，全在 <c>Config/Tables/Audio/audio_*.tres</c> 里。</para>
///
/// <para><b>三条纪律</b>（与项目里"表是权威、代码是回退"一致）：
/// <list type="number">
/// <item><b>读不到表 / id 不存在 / Stream 为空 → 静默不播，但写一次日志</b>（<c>GD.PushWarning</c> +
///   <c>GameLog</c>，同一个 id 只警告一次）。表现层的问题绝不能影响玩法，也绝不能"以为接好了其实没声音"。</item>
/// <item><b>每个 id 第一次真的播出来时写一行日志</b>（id → 总线 → 文件）。这是"这条音效到底有没有被触发过"的唯一证据。</item>
/// <item><b>退出时写一行合计</b>（每个 id 各播了几次）—— 调参/验收时不用再去数。</item>
/// </list></para>
///
/// <para><b>为什么是声部池而不是每次 new 一个 AudioStreamPlayer</b>：一梭子子弹 12 发 + 命中 12 下
/// 会在一瞬间要 20 多个播放器，每次建节点既抖又难回收。这里在 <c>_Ready</c> 一次性建好
/// （SFX 16 个 / UI 4 个 / Music 1 个），播放时挑一个空闲的；全忙就**轮转抢占最老的那个**
/// （比"丢掉新声音"更符合直觉：连打时听得见最后一发）。</para>
///
/// <para>⚠️ <b>刻意没做 3D 定位音</b>（没有 <c>AudioStreamPlayer3D</c>）：当前 4 个示范点
/// （开火 / 命中 / 死亡 / UI）里 UI 本来就是 2D，战斗那几个"响一下"的反馈用 2D 也成立。
/// 要做距离衰减/声道定位时再按 <see cref="AudioIds"/> 的粒度加一条 <c>PlaySfxAt(id, position)</c>
/// 与一组 3D 声部即可，表结构不用动（<c>Bus</c> 那一列已经分流了）。</para>
///
/// <para>⚠️ <b>必须 <c>ProcessMode = Always</c></b>：界面打开时 <c>UiRouter</c> 会把 <c>SceneTree.Paused</c>
/// 置 true，而 <c>AudioStreamPlayer</c> 会跟着暂停 —— 那正好是 UI 音效该响的时候。</para>
/// </summary>
public partial class AudioManager : Node
{
	public static AudioManager? Instance { get; private set; }

	/// <summary>SFX 声部数（同时能响几个战斗音效）。</summary>
	private const int SfxVoiceCount = 16;

	/// <summary>UI 声部数（同时能响几个界面音效）。</summary>
	private const int UiVoiceCount = 4;

	/// <summary>总线名（必须与 <c>default_bus_layout.tres</c> 一致）。</summary>
	private const string MasterBus = "Master";
	private const string SfxBus = "SFX";
	private const string UiBus = "UI";
	private const string MusicBus = "Music";

	private readonly List<AudioStreamPlayer> _sfx = new();
	private readonly List<AudioStreamPlayer> _ui = new();
	private AudioStreamPlayer? _music;

	private int _sfxNext;
	private int _uiNext;

	/// <summary>每个 id 播了几次（退出时写进日志；也是 <c>#if DEBUG</c> 那几个只读属性的来源）。</summary>
	private readonly Dictionary<string, int> _played = new();

	/// <summary>已经警告过的缺失 id（同一个 id 只警告一次，避免刷屏）。</summary>
	private readonly HashSet<string> _warnedMissing = new();

	/// <summary>已经写过"首次播放"日志的 id。</summary>
	private readonly HashSet<string> _announced = new();

	private int _playCount;
	private string _lastId = "-";

	private string _sfxBus = SfxBus;
	private string _uiBus = UiBus;
	private string _musicBus = MusicBus;

	public override void _Ready()
	{
		Instance = this;

		// 界面打开时游戏是暂停的 —— UI 音效必须仍然能响（AudioStreamPlayer 会跟着暂停）。
		ProcessMode = ProcessModeEnum.Always;

		_sfxBus = ResolveBus(SfxBus);
		_uiBus = ResolveBus(UiBus);
		_musicBus = ResolveBus(MusicBus);

		for (int i = 0; i < SfxVoiceCount; i++)
		{
			_sfx.Add(NewVoice($"SfxVoice{i}", _sfxBus));
		}

		for (int i = 0; i < UiVoiceCount; i++)
		{
			_ui.Add(NewVoice($"UiVoice{i}", _uiBus));
		}

		_music = NewVoice("MusicVoice", _musicBus);

		// 一首放完的通知（OGG 的 Import 里 loop=false 才有）—— 曲库轮播靠它，见 MusicDirector。
		_music.Finished += OnMusicFinished;

		LoadTable();

		// **状态音乐**（需求包 §五 #14）：按"关卡当前是什么状态"切两套 BGM 曲库。
		// 这里 new 出来当子节点，而不是加进 project.godot 的 autoload —— 本批改动边界不允许动 project.godot，
		// 而且它本来就该跟着 AudioManager 的生命周期走（ProcessMode=Always 也一样继承）。
		AddChild(new MusicDirector { Name = "MusicDirector" });

		// 敌人死亡：走既有的事件总线，**不碰任何玩法代码**（`Unit.ReceiveHit` 的伤害管线一行没改）。
		GameplayEventBus.Raised += OnGameplayEvent;

		// 按钮点击：项目边界不准改 `Scenes/UI/**`，所以这里在运行期给**所有** BaseButton 自动挂上
		// 点击音 —— 新加界面/按钮不用记得接音效（这也是"位置先占好"的示范）。
		GetTree().NodeAdded += OnNodeAdded;
	}

	public override void _ExitTree()
	{
		GameplayEventBus.Raised -= OnGameplayEvent;

		if (GetTree() is { } tree)
		{
			tree.NodeAdded -= OnNodeAdded;
		}

		LogSummary();

		if (Instance == this)
		{
			Instance = null;
		}
	}

	// ─────────────────────────────────────────────────────────────────────────
	// 对外接口：按 id 播放（调用方只给 id）
	// ─────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// 按 id 播一条音效。**表里没有这个 id → 静默不播 + 写一次 warning**（表现层的问题不影响玩法）。
	/// 走哪条总线由**行里的 <c>Bus</c> 列**决定 —— <see cref="PlaySfx"/> / <see cref="PlayUi"/> 只是
	/// 让调用点读起来更清楚的别名，不是第二套分流规则。
	/// </summary>
	/// <returns>真的播出去了才返回 true。</returns>
	public static bool Play(string? id) => Instance?.PlayInternal(id) ?? false;

	/// <summary>按 id 播一条**战斗**音效（等价于 <see cref="Play"/>）。</summary>
	public static bool PlaySfx(string? id) => Play(id);

	/// <summary>按 id 播一条**界面**音效（等价于 <see cref="Play"/>）。</summary>
	public static bool PlayUi(string? id) => Play(id);

	/// <summary>按 id 播**音乐**（表里这一行的 <c>Bus</c> 应为 <c>Music</c>）。单声部：后一首顶掉前一首。</summary>
	public static bool PlayMusic(string? id) => Play(id);

	/// <summary>停掉当前音乐（音效不受影响）。</summary>
	public static void StopMusic() => Instance?._music?.Stop();

	/// <summary>一首音乐**自然播完**时触发（<see cref="MusicDirector"/> 的曲库轮播用它）。</summary>
	/// <remarks><c>Stop()</c> 不会触发它 —— 只有放到结尾才会。</remarks>
	public event System.Action? MusicEnded;

	/// <summary>
	/// 音乐声部（<b>只给同层的 <see cref="MusicDirector"/> 做淡入淡出用</b>；要播音乐一律走
	/// <see cref="PlayMusic"/>，别直接拿它 <c>Play()</c> —— 那样会绕过表、日志与计数）。
	/// </summary>
	internal AudioStreamPlayer? MusicVoice => _music;

	// ─────────────────────────────────────────────────────────────────────────
	// 内部实现
	// ─────────────────────────────────────────────────────────────────────────

	private void LoadTable()
	{
		AudioConfig[] rows = Balance.Settings.Audio;
		ConfigRows.Rebuild(rows);

		int count = ConfigRows.Count<AudioConfig>();

		// "顺藤摸瓜"入口：一行看清这张表里有什么、各走哪条总线、文件在不在。
		var sb = new StringBuilder();
		foreach (string id in ConfigRows.Ids<AudioConfig>())
		{
			AudioConfig? row = ConfigRows.ById<AudioConfig>(id);
			if (row == null)
			{
				continue;
			}

			if (sb.Length > 0)
			{
				sb.Append('、');
			}

			sb.Append($"{id}({row.Bus}{(row.Stream == null ? "，Stream 为空" : string.Empty)})");
		}

		// 与 `Balance.BuildTableIndex` 的日志同一格式：一眼能看出这张表读到几条、来自表还是代码默认值。
		GameLog.Write(
			"Audio",
			$"配置表：音效 {count}（来源：{(count > 0 ? "表 Audio" : "代码默认值（表读不到）")}）—— 总线 {MasterBus}(Master) / {_sfxBus}({SfxVoiceCount} 声部) / {_uiBus}({UiVoiceCount} 声部) / {_musicBus}(1 声部){(sb.Length > 0 ? $"；{sb}" : string.Empty)}");
	}

	/// <summary>建一个声部节点并挂到自己下面（<c>ProcessMode = Always</c> 会被子节点继承，暂停时照样出声）。</summary>
	private AudioStreamPlayer NewVoice(string name, string bus)
	{
		var voice = new AudioStreamPlayer { Name = name, Bus = bus };
		AddChild(voice);
		return voice;
	}

	/// <summary>总线的回退语义：布局文件没生效时退回 Master，并留痕（绝不静默）。</summary>
	private static string ResolveBus(string wanted)
	{
		if (AudioServer.GetBusIndex(wanted) >= 0)
		{
			return wanted;
		}

		GameLog.Write("Audio", $"⚠ 音频总线「{wanted}」不存在（default_bus_layout.tres 没被加载？），这类声音退回「{MasterBus}」。");
		return MasterBus;
	}

	private bool PlayInternal(string? id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return false;
		}

		AudioConfig? row = ConfigRows.ById<AudioConfig>(id);
		if (row == null)
		{
			WarnOnce(id!, "音效表里没有这一行（Config/Tables/Audio/）");
			return false;
		}

		if (row.Stream == null)
		{
			WarnOnce(id!, "表里有这一行，但 Stream 是空的（没把 .ogg 拖进这一列）");
			return false;
		}

		AudioStreamPlayer voice = row.Bus switch
		{
			AudioBusKind.Ui => Take(_ui, ref _uiNext),
			AudioBusKind.Music => _music ?? Take(_sfx, ref _sfxNext),
			_ => Take(_sfx, ref _sfxNext),
		};

		voice.Stream = row.Stream;
		voice.VolumeDb = row.VolumeDb + CenteredRandom(row.VolumeRandomDb);
		voice.PitchScale = Mathf.Max(0.01f, row.PitchScale + CenteredRandom(row.PitchRandom));
		voice.Play();

		Record(id!, voice.Bus);
		return true;
	}

	/// <summary>取一个声部：优先空闲的；全忙则轮转抢占（连打时听得见最新的那一下）。</summary>
	private static AudioStreamPlayer Take(List<AudioStreamPlayer> voices, ref int next)
	{
		foreach (AudioStreamPlayer voice in voices)
		{
			if (!voice.Playing)
			{
				return voice;
			}
		}

		AudioStreamPlayer stolen = voices[next % voices.Count];
		next = (next + 1) % voices.Count;
		return stolen;
	}

	private static float CenteredRandom(float range)
		=> range <= 0f ? 0f : (float)GD.RandRange(-range, range);

	private void Record(string id, string bus)
	{
		_played.TryGetValue(id, out int count);
		_played[id] = count + 1;
		_playCount++;
		_lastId = id;

		// 每个 id 只在**第一次真的播出来**时写一行：这就是"接好了"与"以为接好了"的分界线。
		if (_announced.Add(id))
		{
			string path = ConfigRows.ById<AudioConfig>(id)?.Stream?.ResourcePath ?? "(内联流)";
			GameLog.Write("Audio", $"首次播放「{id}」→ 总线 {bus} / {path}");
		}
	}

	private void WarnOnce(string id, string reason)
	{
		if (!_warnedMissing.Add(id))
		{
			return;
		}

		GD.PushWarning($"[Audio] 音效「{id}」跳过了：{reason}（同一个 id 只警告一次）。");
		GameLog.Write("Audio", $"⚠ 音效「{id}」跳过了：{reason} —— 静默不播，玩法不受影响。");
	}

	/// <summary>退出时写一行合计（"这条音效到底响过没有"不用再去数）。</summary>
	private void LogSummary()
	{
		if (_playCount == 0)
		{
			GameLog.Write("Audio", $"音效统计：本局一次都没播（表里 {ConfigRows.Count<AudioConfig>()} 条；检查音效表与触发点）。");
			return;
		}

		var sb = new StringBuilder();
		foreach (KeyValuePair<string, int> entry in _played)
		{
			if (sb.Length > 0)
			{
				sb.Append('、');
			}

			sb.Append($"{entry.Key}×{entry.Value}");
		}

		GameLog.Write("Audio", $"音效统计：共 {_playCount} 次 —— {sb}（缺失 id {_warnedMissing.Count} 个）");
	}

	private void OnGameplayEvent(GameplayEvent gameplayEvent)
	{
		if (gameplayEvent.Kind != GameplayEventKind.Died)
		{
			return;
		}

		// 只播"敌方单位死亡"。玩家/中立死亡另有剧情音，不该混用同一个死亡音（也没有那种需求）。
		if (gameplayEvent.Target == null || gameplayEvent.Target.Faction != Faction.Enemy)
		{
			return;
		}

		PlaySfx(AudioIds.EnemyDie);
	}

	private void OnNodeAdded(Node node)
	{
		if (node is BaseButton button)
		{
			button.Pressed += OnButtonPressed;
		}
	}

	private void OnButtonPressed() => PlayUi(AudioIds.UiClick);

	/// <summary>音乐声部放到结尾 → 转通知（曲库轮播）。</summary>
	private void OnMusicFinished() => MusicEnded?.Invoke();

#if DEBUG
	/// <summary>自测取证用（Release 构建里不存在）：本局成功播放的次数。</summary>
	public int DebugPlayCount => _playCount;

	/// <summary>自测取证用：最近一次成功播放的 id。</summary>
	public string DebugLastId => _lastId;

	/// <summary>自测取证用：音效表索引到几条。</summary>
	public int DebugRowCount => ConfigRows.Count<AudioConfig>();

	/// <summary>自测取证用：当前正在发声的声部数（证明"真的在播"，而不是只记了个数）。</summary>
	public int DebugPlayingVoices
	{
		get
		{
			int playing = 0;
			foreach (AudioStreamPlayer voice in _sfx)
			{
				if (voice.Playing)
				{
					playing++;
				}
			}

			foreach (AudioStreamPlayer voice in _ui)
			{
				if (voice.Playing)
				{
					playing++;
				}
			}

			if (_music is { Playing: true })
			{
				playing++;
			}

			return playing;
		}
	}

	/// <summary>自测取证用：<c>AudioServer</c> 上**真实存在**的总线名（证明 <c>default_bus_layout.tres</c> 生效了）。</summary>
	public string DebugBuses
	{
		get
		{
			var names = new List<string>(AudioServer.BusCount);
			for (int i = 0; i < AudioServer.BusCount; i++)
			{
				names.Add(AudioServer.GetBusName(i));
			}

			return string.Join("/", names);
		}
	}

	/// <summary>自测取证用：被警告过的缺失 id（逗号分隔）。</summary>
	public string DebugMissingIds => string.Join(",", _warnedMissing);

	/// <summary>自测取证用：一行摘要（<c>await_runtime_condition</c> 直接读它）。</summary>
	public string DebugSummary
		=> $"表 {ConfigRows.Count<AudioConfig>()} 条 / 播放 {_playCount} 次 / 最近「{_lastId}」/ 在响 {DebugPlayingVoices} / 缺 id {_warnedMissing.Count} / 总线 {DebugBuses}";
#endif
}
