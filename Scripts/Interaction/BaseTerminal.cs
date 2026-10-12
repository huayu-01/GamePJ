using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.UI;
using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Interaction;

/// <summary>
/// 基地里的交互台（用户需求：把几个模块系统作为交互物放进基地场景）。
/// 与关卡节点不同：基地交互物**可以反复使用**（不是一次性的）。
/// </summary>
public partial class BaseTerminal : Interactable
{
	public enum TerminalKind
	{
		/// <summary>局外强化台（碎片消费）。</summary>
		Upgrades,

		/// <summary>装备配置台（角色 / 武器）。</summary>
		Loadout,

		/// <summary>操作说明台。</summary>
		Help,

		/// <summary>出击门：切换场景进入关卡。</summary>
		Gate,
	}

	[Export] public TerminalKind Kind { get; set; } = TerminalKind.Upgrades;

	/// <summary>出击门加载的场景。</summary>
	[Export] public string TargetScene { get; set; } = "res://Scenes/Main.tscn";

	public override void _Ready()
	{
		base._Ready();

		// 基地里的交互物不消耗。
		SingleUse = false;
		Prompt = Kind switch
		{
			TerminalKind.Upgrades => "局外强化台",
			TerminalKind.Loadout => "装备配置台",
			TerminalKind.Help => "操作说明台",
			_ => "出击门",
		};
	}

	protected override bool OnInteract()
	{
		UiService? ui = UiService.Instance;
		if (ui == null)
		{
			return false;
		}

		// 重构：这里只说"我要开哪个台子"，界面从哪来交给 UiService，不再硬编码节点路径。
		switch (Kind)
		{
			case TerminalKind.Upgrades:
				GameLog.Write("Flow", "基地：打开局外强化台");
				return ui.OpenUpgrades();

			case TerminalKind.Loadout:
				GameLog.Write("Flow", "基地：打开装备配置台");
				return OpenLoadoutFlow(ui);

			case TerminalKind.Help:
				GameLog.Write("Flow", "基地：打开说明台");
				return ui.OpenHelp();

			default:
				// 出击：带着基地里的配置切到关卡场景。
				RunSetup.HasSelection = true;
				GameLog.Write("Flow", $"基地出击 → {TargetScene}（角色 {RunSetup.Character} / 武器 {RunSetup.Weapon}）");

				// 走加载页：显示"加载中"并暂停输入，最短显示时间后切场景。
				if (GetTree().CurrentScene?.GetNodeOrNull<LoadingScreen>("LoadingScreen") is { } loading)
				{
					loading.BeginLoad(TargetScene);
				}
				else
				{
					GetTree().ChangeSceneToFile(TargetScene);
				}

				return true;
		}
	}

	/// <summary>
	/// 装备配置台：**先选角色、再选武器**，两步都在选定的那一刻写进 <see cref="RunSetup"/>，
	/// 基地里的玩家立刻换成对应的目标（用户需求 §五 #4：方便进关卡前熟悉角色）。
	///
	/// <list type="bullet">
	/// <item>角色文案与关卡里的 Configure 步**同一套来源**（<c>RunController.CharacterCardText</c> +
	///   角色表的 <c>DescriptionTemplate</c>），不在这里抄第二份数字；</item>
	/// <item>真正"换人"的是常驻基地的 <c>Scripts/UI/LoadoutPreview.cs</c>（模型 + 数值 + 枪械开关），
	///   它盯着 <see cref="RunSetup"/> 变化 —— 所以这里只负责"选了什么"。</item>
	/// </list>
	/// </summary>
	private static bool OpenLoadoutFlow(UiService ui)
	{
		var options = new List<string>();
		foreach (PlayerCharacterKind kind in System.Enum.GetValues<PlayerCharacterKind>())
		{
			options.Add(RunController.CharacterCardText(kind));
		}

		return ui.OpenChoose(
			"选择角色（基地里可以直接看到换人）",
			options,
			index =>
			{
				RunSetup.Character = (PlayerCharacterKind)index;
				RunSetup.HasSelection = true;
				GameLog.Write("Flow", $"基地：选定角色 {RunSetup.Character}（基地玩家已切换）");

				// 第二步：武器。近战重装的角色行说 `WeaponEnabled = false` —— 仍然允许选，
				// 出征时按角色行把枪收起来（与关卡里 `RunController.ApplyBalance` 的规则一致）。
				ui.OpenLoadout(kind =>
				{
					RunSetup.Weapon = kind;
					RunSetup.HasSelection = true;
					GameLog.Write("Flow", $"基地：选定武器 {kind}（基地玩家已切换）");
				});
			},
			allowCancel: true,
			hint: "按数字键或点击选择角色；选定后接着选武器");
	}
}
