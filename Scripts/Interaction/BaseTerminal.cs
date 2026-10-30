using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
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
		/// <summary>局外成长台（碎片 → 能力卡：解锁 / 升级 / 激活 / 额度扩增）。</summary>
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
	[Export] public string TargetScene { get; set; } = "res://Scenes/Expedition.tscn";
	[Export] public ExpeditionDestination? Destination { get; set; }
	private bool _departing;

	public override void _Ready()
	{
		base._Ready();

		// 基地里的交互物不消耗。
		SingleUse = false;
		Prompt = Kind switch
		{
			TerminalKind.Upgrades => "局外成长台（能力卡）",
			TerminalKind.Loadout => "装备配置台",
			TerminalKind.Help => "操作说明台",
			_ => "出击门",
		};
	}

	protected override bool OnInteract()
	{
		if (Kind == TerminalKind.Gate)
		{
			if (_departing || !ResourceLoader.Exists(Destination?.ScenePath ?? TargetScene)) return false;
			_departing = true; Depart(); return true;
		}
		UiService? ui = UiService.Instance;
		if (ui == null)
		{
			return false;
		}

		// 重构：这里只说"我要开哪个台子"，界面从哪来交给 UiService，不再硬编码节点路径。
		switch (Kind)
		{
			case TerminalKind.Upgrades:
				GameLog.Write("Flow", "基地：打开局外成长台（能力卡）");
				return ui.OpenAbilityMatrix();

			case TerminalKind.Loadout:
				GameLog.Write("Flow", "基地：打开装备配置台");
				return OpenLoadoutFlow(ui);

			case TerminalKind.Help:
				GameLog.Write("Flow", "基地：打开说明台");
				return ui.OpenHelp();

			default:
				return false;
		}
	}

	private void Depart()
	{
		// 入口交互即出击；在任何本局改动前捕获资源与配装，取消时仍能完整回退。
		AdventureCheckpoint.Capture(PlayerLocator.Player);
		RunSetup.HasSelection = true;
		string target = Destination?.ScenePath ?? TargetScene;
		GameLog.Write("Flow", $"基地出击 → {target}（角色 {RunSetup.Character} / 武器 {RunSetup.Weapon}）");
		if (GetTree().CurrentScene?.GetNodeOrNull<LoadingScreen>("LoadingScreen") is { } loading)
			loading.BeginLoad(target);
		else
			RuntimeResources.ChangeScene(GetTree(), target);
	}

	/// <summary>兼容旧配置台预制体；角色由独立交互对象决定，这里只选当前角色的武器。</summary>
	private static bool OpenLoadoutFlow(UiService ui)
	{
		CharacterConfig? character = RunController.CharacterRow(RunSetup.Character);
		if (character == null || !LoadoutSelection.SelectCharacter(character)) return false;
		return ui.OpenLoadout(kind => LoadoutSelection.SelectWeapon(RunSetup.Character, kind));
	}
}
