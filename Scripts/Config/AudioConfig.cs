using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 一条音效播到哪条**音频总线**上（总线结构见 <c>default_bus_layout.tres</c>）。
///
/// 为什么要分总线而不是全都丢 Master：玩家要能单独关掉音乐、留着音效；
/// UI 音效又通常要比战斗音效更"贴脸"（不受 3D 距离/Master 战斗压缩影响）。
/// 三档够用且和 <c>default_bus_layout.tres</c> 里的三组一一对应。
/// </summary>
public enum AudioBusKind
{
	/// <summary>战斗音效（枪声 / 命中 / 死亡…）→ 总线 <c>SFX</c>。</summary>
	Sfx,

	/// <summary>界面音效（按钮 / 开关面板…）→ 总线 <c>UI</c>。</summary>
	Ui,

	/// <summary>音乐 → 总线 <c>Music</c>（单声部，后一首顶掉前一首）。</summary>
	Music,
}

/// <summary>
/// **音效表**的一行 —— 一个 `.tres` 文件就是一条音效（`Config/Tables/Audio/audio_*.tres`）。
/// 索引用 <see cref="ConfigIndexData.Audio"/>。运行期由 autoload <c>AudioManager</c> 读，业务代码只写
/// <c>AudioManager.PlaySfx("sfx_xxx")</c> —— **调用方永远不需要知道文件在哪**。
///
/// **一行 = id + 资源引用 + 音量 / 音高随机范围 + 总线**，所以在项目里"加一个音效"是两件事：
/// ① 把 `.ogg` 放进 `Assets/Audio/`（命名 <c>sfx_*</c> / <c>ui_*</c>）；② 在这里加一行 `.tres`，
/// 把 <see cref="Stream"/> 拖到这个文件上。**不需要改任何 C#**（除非是一个全新的触发点）。
///
/// 为什么 <see cref="Stream"/> 是**资源引用**而不是路径字符串：与 <c>UnitConfig.AiProfile</c> /
/// <c>WeaponConfig.Pool</c> 同一条纪律 —— 文件被删/改名时 Godot 会把引用**置 null**（可判定、可警告），
/// 而路径字符串拼错一个字符只会**静默没声音**。Inspector 里这一列也是类型过滤后的资源选择器。
/// </summary>
[Tool]
[GlobalClass]
public partial class AudioConfig : ConfigRow
{
	[ExportGroup("声音资源")]
	/// <summary>
	/// 要播的音频（把 <c>Assets/Audio/</c> 下的 <c>.ogg</c> 拖进来）。**留空 = 这条 id 只占位、不发声**
	/// （<c>AudioManager</c> 会写一条 warning，不会崩）。
	/// </summary>
	[Export(PropertyHint.File, "*.ogg,*.wav,*.mp3")] public AudioStream? Stream { get; set; }

	[ExportGroup("播放 · 总线与音量")]
	/// <summary>播到哪条总线上（<c>SFX</c> / <c>UI</c> / <c>Music</c>，对应 <c>default_bus_layout.tres</c> 的三组）。</summary>
	[Export] public AudioBusKind Bus { get; set; } = AudioBusKind.Sfx;

	/// <summary>
	/// 这条音效的**基准音量**（分贝；0 = 原样，负数是变小）。
	/// 素材之间的响度差靠这一列拉平，**不要**去改总线的音量（改总线会一起影响所有音效）。
	/// </summary>
	[Export] public float VolumeDb { get; set; }

	/// <summary>
	/// **音量随机范围**：每次播放时在 <c>±这个值</c>（分贝）之间随机抖动。
	/// 0 = 不抖（默认）。同一发子弹连打十次听起来一模一样就是因为没开这个 —— 开 1.5 左右就"活"了。
	/// </summary>
	[Export(PropertyHint.Range, "0,12,0.1")] public float VolumeRandomDb { get; set; }

	[ExportGroup("播放 · 音高")]
	/// <summary>基准音高（1 = 原速；2 = 快一倍、音调更高；0.5 = 慢一倍）。可用来把同一个文件"变成"几档不同的音。</summary>
	[Export(PropertyHint.Range, "0.25,4,0.01")] public float PitchScale { get; set; } = 1f;

	/// <summary>
	/// **音高随机范围**：每次播放时在 <c>±这个值</c>（相对 1.0 的比例）之间随机。
	/// 0 = 不抖（默认）。**这是"一个文件当三五个用"最省事的旋钮** —— 0.06 ≈ ±6%，人耳听得出来但不会变调。
	/// </summary>
	[Export(PropertyHint.Range, "0,0.5,0.01")] public float PitchRandom { get; set; }
}
