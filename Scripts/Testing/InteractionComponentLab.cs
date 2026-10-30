using System;
using System.Linq;
using ActRoguelike.AbilityCards;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Interaction;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>示例场景可正常F6；指定取证参数时只读检查组件并截图，不改用户配装和进度。</summary>
public partial class InteractionComponentLab : Node3D
{
	public override async void _Ready()
	{
#if DEBUG
		if (!OS.GetCmdlineUserArgs().Contains("--interaction-lab-capture")) return;
		try
		{
			MetaProgress.UseInMemoryStoreForSelfTest(); RuneCarry.UseInMemoryStoreForSelfTest();
			AbilityStore.DebugBeginSandbox();
			RunSetup.Character = PlayerCharacterKind.Hunter; RunSetup.Weapon = WeaponPresetKind.EnergyPalm; RunSetup.HasSelection = true;
			await ToSignal(GetTree().CreateTimer(1.8, true), SceneTreeTimer.SignalName.Timeout);
			InteractionComponent[] components = FindChildren("*", "Node3D", true, false).OfType<InteractionComponent>().ToArray();
			int active = components.Count(c => c.Backend != null), triggers = components.Count(c => c.Backend?.TriggerArea != null);
			int models = components.Count(c => c.ModelRoot != null && c.Backend?.VisualMesh?.Mesh != null);
			string batch = OS.GetCmdlineUserArgs().Contains("--challenge-feedback") ? "j48"
				: OS.GetCmdlineUserArgs().Contains("--interaction-models") ? "j47" : "j46";
			Error shot = GetViewport().GetTexture().GetImage().SavePng(ActRoguelike.Testing.ValidationArtifacts.Path($"res://Logs/evidence/screenshots/{batch}_interaction_lab.png"));
			bool valid = components.Length >= 9 && active == components.Length && models == components.Length && triggers == 2 && shot == Error.Ok;
			GD.Print($"[InteractionLab] 组件示例验收：组件 {components.Length}／后端 {active}／新模型 {models}／触发器 {triggers}／截图 {shot}");
			GameExit.Request(GetTree(), valid ? 0 : 1);
		}
		catch (Exception error)
		{
			GD.Print($"[InteractionLab] FAIL {error}"); GameExit.Request(GetTree(), 1);
		}
		finally { AbilityStore.DebugEndSandbox(); }
#else
		await System.Threading.Tasks.Task.CompletedTask;
#endif
	}
}
