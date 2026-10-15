namespace ActRoguelike.Audio;

/// <summary>
/// 代码里会用到的那几个**音效 id**（值必须与 `Config/Tables/Audio/audio_*.tres` 的 `Id` 一致，
/// 也与 `.ogg` 的文件名一致）。
///
/// <para>为什么要有这个常量类，而不是在调用点写字符串字面量：调用点散在 `Weapon` / `Projectile` /
/// `AttackHitbox` / `OverlayScreen` 里，拼错一个字符的后果是**静默没声音**（不报错）。
/// 集中在这里之后，"代码引用了哪些 id" 一次 grep 就能看全。</para>
///
/// <para>⚠️ 它**不是**第二份真源：真源永远是音效表。id 写错 / 表里没有那一行时，
/// <see cref="AudioManager"/> 会写一条 warning（只警告一次）并静默跳过 —— 不会崩、也不会以为接好了。</para>
/// </summary>
public static class AudioIds
{
	/// <summary>枪械开火（触发点：<c>Weapon.TryFire</c>）。</summary>
	public const string WeaponFire = "sfx_weapon_fire";

	/// <summary>命中 / 击中（触发点：<c>Projectile.ResolveImpact</c> 与 <c>AttackHitbox._PhysicsProcess</c>，
	/// 与命中特效同一个位置）。</summary>
	public const string CombatHit = "sfx_combat_hit";

	/// <summary>敌人死亡（触发点：<c>GameplayEventBus</c> 的 <c>Died</c> 事件）。</summary>
	public const string EnemyDie = "sfx_enemy_die";

	/// <summary>按钮点击（触发点：<c>AudioManager</c> 自动挂到所有 <c>BaseButton.Pressed</c> 上，零场景改动）。</summary>
	public const string UiClick = "ui_click";

	/// <summary>界面打开（触发点：<c>OverlayScreen.BeginOpen</c>，所有弹窗共用一处）。</summary>
	public const string UiOpen = "ui_open";

	/// <summary>
	/// **蓄力完成**（触发点：<c>Scripts/Combat/ChargeBodyFx.cs</c> —— 任意蓄力技能的
	/// <c>Ability.ChargeBaseComplete</c> 由 false 变 true 的那一帧，**每次完成基础蓄力都响一声**）。
	///
	/// <para>它是一条**独立的新表行**（P7 ③）：源文件目前是**临时借用**项目自制的
	/// <c>ui_card_unlock.wav</c>（"能力卡·解锁"那一声，语义上最接近"蓄力就绪"），
	/// 总线走 <c>SFX</c>（战斗音效，而不是 UI 总线）。美术出了专门的素材之后，
	/// 把 <c>Config/Tables/Audio/audio_sfx_charge_complete.tres</c> 的 <c>Stream</c> 指过去即可 ——
	/// **代码一个字都不用改**（调用方只认这个 id，永远不知道文件在哪）。</para>
	/// </summary>
	public const string ChargeComplete = "sfx_charge_complete";

	/// <summary>
	/// 交互物**失败响应**的音效（触发点：<c>Interactable`Failed</c>/<c>Rejected</c> → 表现层，
	/// 用户需求 §五 #13「失败增加一个失败响应、抖动效果、音效、玩家被略微击退」）。
	///
	/// <para>它**刻意复用**能力卡那一条 <c>ui_card_fail</c>（同一类"操作被拒"的低频嗡声）：
	/// 不新增素材、不新增音效表行、也不动 <c>ConfigIndex.tres</c> 的音频索引 ——
	/// 想换成专门的一条，就照 docs/audio.md §5 加一个 <c>sfx_*</c> 行，再把这里的值改掉（改一行字符串）。</para>
	/// </summary>
	public const string InteractFail = "ui_card_fail";

	// 音乐（BGM）刻意**不在这里列常量**：曲库是 MusicDirector 按音效表的 id 前缀
	// （music_level_ / music_challenge_）扫出来的，加一首曲子不用改任何 C#（见 docs/audio.md §九）。
}
