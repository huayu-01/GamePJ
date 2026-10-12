using System;
using System.Collections.Generic;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Audio;

/// <summary>
/// **状态音乐**（需求包 §五 #14）：按"关卡现在是什么状态"在两套 BGM 曲库之间切。
///
/// <list type="bullet">
/// <item>**关卡默认曲库** = 音效表里 id 以 <see cref="LevelPrefix"/> 开头的行；</item>
/// <item>**挑战中曲库** = id 以 <see cref="ChallengePrefix"/> 开头的行；</item>
/// <item>状态从 <see cref="StageController.ChallengerActive"/> 读（**节点挑战与 Boss 战都算挑战**，与
///   <c>Interactable</c> 的挑战者闸门是同一个判据 —— 不另造一套状态）；</item>
/// <item>一首放完自动轮播同一曲库的下一首（**表内顺序 = 轮播顺序**）。</item>
/// </list>
///
/// <para><b>为什么是"轮询一个 bool"而不是订阅信号</b>：<see cref="StageController.ChallengerActive"/>
/// 是**派生属性**（<c>_encounterRunning || Boss 还活着</c>），流程层没有为此发信号；而本批的改动边界
/// 只允许动 <c>Scripts/Audio/**</c>。每帧读一个 bool + 一次比较的成本可以忽略，
/// 也不需要在战斗/流程代码里插钩子。</para>
///
/// <para><b>为什么会有淡入淡出</b>：<c>Music</c> 总线只有 1 个声部（后一首顶掉前一首），
/// 直接 <c>Stop()</c> 一个正在响的音频会在波形中途切断 → **爆音**。所以切换时先 0.35s 淡出、再播新的一首
/// （并让新的那首从 -60dB 淡入到行里的 <c>VolumeDb</c>）。</para>
///
/// <para><b>加一首曲子要做什么</b>（零 C#）：把 <c>.ogg</c> 放进 <c>Assets/Audio/music/</c>，
/// 命名 <c>music_level_0X.ogg</c> / <c>music_challenge_0X.ogg</c>，加一行 <c>Config/Tables/Audio/audio_*.tres</c>
/// （<c>Bus = Music</c>），再挂进 <c>Config/Balance.tres</c> 的 <c>Audio</c> 数组。见 docs/audio.md §九。</para>
/// </summary>
public partial class MusicDirector : Node
{
	/// <summary>关卡默认曲库的 id 前缀。</summary>
	public const string LevelPrefix = "music_level_";

	/// <summary>挑战中曲库的 id 前缀。</summary>
	public const string ChallengePrefix = "music_challenge_";

	/// <summary>切换时的淡出 / 淡入时长（秒）。</summary>
	private const float FadeSeconds = 0.35f;

	/// <summary>淡出终点（-60dB ≈ 听不见）。</summary>
	private const float SilentDb = -60f;

	private readonly List<string> _level = new();
	private readonly List<string> _challenge = new();

	/// <summary>两套曲库各自的轮播游标（各自记住播到第几首，来回切换不会每次都从第 1 首开始）。</summary>
	private int _levelIndex;
	private int _challengeIndex;

	private bool _inStage;
	private bool _challengeMode;
	private bool _fading;
	private bool _warnedEmptyLibrary;

	private string? _currentId;
	private string? _pendingId;
	private Tween? _fade;

	public override void _Ready()
	{
		foreach (string id in ConfigRows.Ids<AudioConfig>())
		{
			if (id.StartsWith(LevelPrefix, StringComparison.Ordinal))
			{
				_level.Add(id);
			}
			else if (id.StartsWith(ChallengePrefix, StringComparison.Ordinal))
			{
				_challenge.Add(id);
			}
		}

		// 曲库构成写一行日志：这一行同时证明"表里加的音乐行真的被当曲库读到了"。
		GameLog.Write(
			"Audio",
			$"音乐曲库：关卡 {_level.Count} 首[{string.Join("、", _level)}] / 挑战 {_challenge.Count} 首[{string.Join("、", _challenge)}]（前缀 {LevelPrefix}* / {ChallengePrefix}*，表内顺序 = 轮播顺序）");

		if (AudioManager.Instance is { } manager)
		{
			manager.MusicEnded += OnMusicEnded;
		}
	}

	public override void _ExitTree()
	{
		_fade?.Kill();

		if (AudioManager.Instance is { } manager)
		{
			manager.MusicEnded -= OnMusicEnded;
		}
	}

	public override void _Process(double delta)
	{
		// 挑战者状态是派生属性（见 StageController.ChallengerActive 的注释），这里只读它、不改它。
		bool inStage = StageController.Instance != null;
		bool challenge = StageController.Instance is { ChallengerActive: true };

		if (inStage != _inStage)
		{
			// 进出关卡（基地里没有 StageController）：进关起音乐，离关（回基地 / 结算）停音乐。
			_inStage = inStage;
			_challengeMode = challenge;

			if (inStage)
			{
				PlayNext(fade: false);
			}
			else
			{
				Stop(fade: true);
			}

			return;
		}

		if (inStage && challenge != _challengeMode)
		{
			// 挑战开始 / 结束 —— 换一套曲库。
			_challengeMode = challenge;
			PlayNext(fade: true);
		}
	}

	/// <summary>一首放完 → 同一曲库接着放下一首（曲库只有 1 首时会再放它一遍）。</summary>
	private void OnMusicEnded()
	{
		if (_inStage && !_fading)
		{
			PlayNext(fade: false);
		}
	}

	/// <summary>播当前曲库的下一首（<paramref name="fade"/> = 是否先淡出正在响的那一首）。</summary>
	private void PlayNext(bool fade)
	{
		List<string> library = _challengeMode ? _challenge : _level;
		if (library.Count == 0)
		{
			WarnEmptyLibrary();
			return;
		}

		int index = _challengeMode ? _challengeIndex : _levelIndex;
		string id = library[index % library.Count];

		if (_challengeMode)
		{
			_challengeIndex = (index + 1) % library.Count;
		}
		else
		{
			_levelIndex = (index + 1) % library.Count;
		}

		if (fade && AudioManager.Instance is { MusicVoice: { Playing: true } voice })
		{
			// 记下"想要的那一首"，淡出结束后再播（淡出期间又切状态的话，用最新那一首）。
			_pendingId = id;
			if (_fading)
			{
				return;
			}

			_fading = true;
			_fade?.Kill();
			_fade = CreateTween();
			_fade.TweenProperty(voice, "volume_db", SilentDb, FadeSeconds);
			_fade.TweenCallback(Callable.From(FinishFadeOut));
			return;
		}

		Start(id);
	}

	/// <summary>
	/// 切换留痕（"关卡曲库 / 挑战曲库各切到了哪一首"就是这一行）。
	/// **只在真的开始播这一首时才写** —— 淡出那 0.35 秒里如果状态又变了一次，
	/// 被顶掉的"意图"不写（否则日志里的"切到 X"会多于实际播放次数，与退出合计对不上）。
	/// </summary>
	private void LogSwitch(string id)
	{
		List<string> library = _challengeMode ? _challenge : _level;
		int index = library.IndexOf(id);
		GameLog.Write("Audio", $"音乐：播{(_challengeMode ? "挑战" : "关卡")}曲库「{id}」（{(index < 0 ? 1 : index + 1)}/{library.Count} 首）");
	}

	/// <summary>淡出结束：停掉旧的一首，播上"想要的那一首"（没有就只是停下来）。</summary>
	private void FinishFadeOut()
	{
		_fading = false;
		AudioManager.Instance?.MusicVoice?.Stop();

		string? next = _pendingId;
		_pendingId = null;
		if (next != null)
		{
			Start(next);
		}
	}

	/// <summary>
	/// 真的播一首：走 <see cref="AudioManager.PlayMusic"/>（**唯一入口**，日志 / 首次播放 / 计数都在那里），
	/// 然后从 <see cref="SilentDb"/> 淡入到行里的基准音量 —— 音量**不写死在代码里**，仍以表为准。
	/// </summary>
	private void Start(string id)
	{
		if (!AudioManager.PlayMusic(id))
		{
			return;
		}

		_currentId = id;
		LogSwitch(id);

		if (AudioManager.Instance is { MusicVoice: { } voice })
		{
			voice.VolumeDb = SilentDb;
			float target = ConfigRows.ById<AudioConfig>(id)?.VolumeDb ?? 0f;
			_fade?.Kill();
			_fade = CreateTween();
			_fade.TweenProperty(voice, "volume_db", target, FadeSeconds);
		}
	}

	/// <summary>停音乐（回基地 / 结算）。<paramref name="fade"/> = 淡出后再停。</summary>
	private void Stop(bool fade)
	{
		bool wasPlaying = AudioManager.Instance is { MusicVoice.Playing: true };
		_currentId = null;
		_pendingId = null;

		if (AudioManager.Instance is not { MusicVoice: { } voice } || !voice.Playing)
		{
			return;
		}

		if (wasPlaying)
		{
			GameLog.Write("Audio", "音乐：离开关卡 → 淡出停播（基地里没有 BGM）");
		}

		if (!fade)
		{
			voice.Stop();
			return;
		}

		_fading = true;
		_fade?.Kill();
		_fade = CreateTween();
		_fade.TweenProperty(voice, "volume_db", SilentDb, FadeSeconds);
		_fade.TweenCallback(Callable.From(() =>
		{
			_fading = false;
			voice.Stop();
		}));
	}

	/// <summary>曲库是空的时候说一次（只影响"有没有音乐"，绝不影响玩法）。</summary>
	private void WarnEmptyLibrary()
	{
		if (_warnedEmptyLibrary)
		{
			return;
		}

		_warnedEmptyLibrary = true;
		GameLog.Write("Audio", $"⚠ 音乐曲库是空的（表里没有 id 以 {LevelPrefix} / {ChallengePrefix} 开头的行）—— 这局没有 BGM，玩法不受影响。");
	}

#if DEBUG
	/// <summary>自测取证用：当前这一首的 id（没在播就是 "-"）。</summary>
	public string DebugCurrentMusic => _currentId ?? "-";

	/// <summary>自测取证用：现在用的是哪套曲库（关卡 / 挑战）。</summary>
	public string DebugLibrary => _challengeMode ? "挑战" : "关卡";

	/// <summary>自测取证用：两套曲库各几首。</summary>
	public string DebugLibraries => $"关卡 {_level.Count}[{string.Join("、", _level)}] / 挑战 {_challenge.Count}[{string.Join("、", _challenge)}]";
#endif
}
