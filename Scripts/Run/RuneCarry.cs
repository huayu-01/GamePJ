using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Run;

/// <summary>
/// **当前携带的符文**（J11）—— 跨场景传递 + 局外存档。
///
/// <para>用户 2026-10-17 的需求：「符文（12 个装备位）…… 携带了这个东西之后，会产生些效果」，
/// 以及第 ⑤ 步的要求「一个'当前携带的符文 Id'字段 / 开局应用到玩家 / 存档」。</para>
///
/// <para><b>为什么单独一个 `user://rune.cfg` 而不是塞进 `meta.cfg`</b>：</para>
/// <list type="number">
/// <item>四个既有存档（`meta` / `ability_cards` / `settings` / `loadout`）是每批验收的
///   **逐字哈希基线**（`docs/DEEPSEEK_HANDOFF.md` §存档）。往 `meta.cfg` 加一个键会让
///   `meta` 的 SHA256 在**每一次**验收里都变，从此再也分不清"是这一批改坏的还是基线本身变了"；</item>
/// <item>格式完全相同（`ConfigFile`，一个 section 一个键），所以"照现有存档格式"这条要求成立；</item>
/// <item>不携带符文（文件不存在）时**不产生任何文件**：默认路径上 userdata 一个字节都不多。</item>
/// </list>
///
/// <para>基地 RuneScreen 保存出击配置，RunController 在进关时读取并施效。</para>
/// </summary>
public static class RuneCarry
{
	private const string SavePath = "user://rune.cfg";
	private const string Section = "rune";
	private const string Key = "carried";

	private static string _current = string.Empty;
	private static bool _loaded;

#if DEBUG
	/// <summary>自测：不读盘、不写盘，且**恒为空**（整套硬指标必须在"没带符文"的状态下跑）。</summary>
	private static bool _selfTestMemoryOnly;
#endif

	/// <summary>当前携带的符文行 Id（空 = 不携带）。第一次访问会读一次盘。</summary>
	public static string Current
	{
		get
		{
			Load();
			return _current;
		}
	}

	/// <summary>当前携带的符文行（读不到 = null；整份符文表没加载也返回 null，不抛异常）。</summary>
	public static RuneConfig? Row => ConfigRows.ById<RuneConfig>(Current);

	/// <summary>改携带项并落盘（空 = 不带）。</summary>
	public static void Set(string? id)
	{
		Load();
		_current = id ?? string.Empty;
		Save();
	}

	public static void Load()
	{
		if (_loaded)
		{
			return;
		}

		_loaded = true;
#if DEBUG
		if (_selfTestMemoryOnly)
		{
			_current = string.Empty;
			return;
		}
#endif
		var config = new ConfigFile();
		if (config.Load(SavePath) != Error.Ok)
		{
			return;
		}

		Variant value = config.GetValue(Section, Key, string.Empty);
		_current = value.VariantType is Variant.Type.String or Variant.Type.StringName ? value.AsString() : string.Empty;
	}

	public static void Save()
	{
#if DEBUG
		if (_selfTestMemoryOnly)
		{
			return;
		}
#endif
		var config = new ConfigFile();
		config.SetValue(Section, Key, _current);
		Error error = config.Save(SavePath);
		if (error != Error.Ok)
		{
			GD.PushWarning($"[Rune] 携带符文写盘失败（{error}）—— 下一局不会记住这次选择。");
		}
	}

#if DEBUG
	/// <summary>自测专用（与 <c>MetaProgress.UseInMemoryStoreForSelfTest</c> 同一条纪律）。</summary>
	internal static void UseInMemoryStoreForSelfTest()
	{
		_loaded = true;
		_current = string.Empty;
		_selfTestMemoryOnly = true;
	}
#endif
}
