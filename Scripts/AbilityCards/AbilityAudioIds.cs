namespace ActRoguelike.AbilityCards;

/// <summary>
/// 能力卡界面用到的**音效 id**（值必须与 `Config/Tables/Audio/audio_*.tres` 的 `Id` 一致）。
///
/// <para>为什么单独一个常量类而不是加到 <c>AudioIds</c>：本轮的可改范围是
/// `Scripts/AbilityCards/**` 与 `Scripts/UI/**`，`Scripts/Audio/AudioIds.cs` 不在其中
/// （那里已经有一批调用点，动它有并线冲突的风险）。三条纪律与 `AudioIds` 完全一致：
/// ① 真源永远是音效表；② 调用点写错只会**静默不播 + 一条 warning**，绝不崩；
/// ③ 想换音色只改那一行 `.tres`，代码一行不动。</para>
///
/// <para>这 4 条音效是**项目自制**（`_tools/make_ui_card_sfx.ps1` 生成的 PCM WAV，
/// 非外部素材），所以它们不涉及任何第三方许可。</para>
/// </summary>
public static class AbilityAudioIds
{
	/// <summary>解锁成功（左键长按读条完成 + 资源够）。</summary>
	public const string Unlock = "ui_card_unlock";

	/// <summary>升级成功（右键单击）。</summary>
	public const string Upgrade = "ui_card_upgrade";

	/// <summary>任何失败（碎片不够 / 额度不够 / 未解锁 / 已到上限 / 没得重置）→ 配元素震动。</summary>
	public const string Fail = "ui_card_fail";

	/// <summary>重置升级进度（右键长按读条完成，归还碎片）。</summary>
	public const string Reset = "ui_card_reset";

	/// <summary>激活 / 关闭的点击音（复用已有的按钮点击音，不另建文件）。</summary>
	public const string Click = "ui_click";
}
