using Godot;

namespace ActRoguelike.UI;

/// <summary>帮助是设置中的一整页。动作键名读取当前重绑结果，避免说明与实际操作漂移。</summary>
public sealed class HelpSettingsSection : ISettingsSection
{
	public string Id => "help";
	public string Title => "帮助";

	public void Build(VBoxContainer page, SettingsScreen screen)
	{
		SettingsUi.AddTitle(page, "移动与战斗");
		AddAction(page, "移动", "move_forward", "move_left", "move_back", "move_right");
		AddAction(page, "跳跃 · 猎手可在空中多跳一次", "jump");
		AddAction(page, "奔跑 · 按住提速", "sprint");
		AddAction(page, "技能 1 / 技能 2", "skill_1", "skill_2");
		AddAction(page, "武技 1 / 武技 2 · 部分武技可按住蓄力", "weapon_skill_1", "weapon_skill_2");
		AddAction(page, "下落攻击 · 武器大师在空中使用", "fall_attack");
		AddAction(page, "互动 · 走近节点或首领门", "interact");
		SettingsUi.AddCaption(page, "鼠标移动转动视角。Esc 打开设置或关闭最上层界面；H 直达本页；O 打开设置。\nK 能力矩阵　·　B 首领状况　·　C 角色持有");

		SettingsUi.AddTitle(page, "探索 → 构筑 → 挑战首领");
		SettingsUi.AddCaption(page, "1. 探索关卡，按颜色与图标寻找强化、进化、异化等节点。\n2. 走近互动，选择一项奖励，迎接随之发生的战斗。\n3. 打赢后继续探索；首领门随时可以挑战，也可放弃剩余节点。\n4. 免费挑战额度与首领压力以 B「首领状况」中的本关读数为准。");
		SettingsUi.AddTitle(page, "成长与胜负");
		SettingsUi.AddCaption(page, "能力矩阵中的卡可解锁、升级与激活；激活占用额度，下局开局生效。空位留白，超过一页可用按钮或 PgUp / PgDn 翻页。\n击败首领即通关，中途阵亡则本局结束；结算后按 Enter 或界面按钮继续。");
		SettingsUi.AddTitle(page, "看懂战斗反馈");
		SettingsUi.AddCaption(page, "血条内显示「当前生命/上限 +护甲」，蓝色数字为护甲。敌人受伤后显示血条与伤害数字。\n首领栏的 ◆ 表示技能强化，◇ 表示压力词条。武器大师的下落攻击带有向上掠过身体的风线，落地会扩散冲击圈。");
	}

	private static void AddAction(VBoxContainer page, string text, params string[] actions)
	{
		var keys = new string[actions.Length];
		for (int i = 0; i < actions.Length; i++)
		{
			keys[i] = ControlsSettingsSection.CurrentKeys(actions[i]);
		}
		SettingsUi.AddCaption(page, $"{string.Join(" / ", keys)}　　{text}");
	}
}
