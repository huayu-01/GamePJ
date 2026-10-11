using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Run;
using ActRoguelike.UI;
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
				return ui.OpenLoadout(kind =>
				{
					RunSetup.Weapon = kind;
					RunSetup.HasSelection = true;
					GameLog.Write("Flow", $"基地：选定武器 {kind}");
				});

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
}
