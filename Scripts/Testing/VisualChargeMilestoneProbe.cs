using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Audio;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Player;
using ActRoguelike.Run;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>061/065正式模型、真实输入、实际档位与材质归还专项；只使用隔离存档。</summary>
public partial class VisualChargeMilestoneProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "VisualChargeMilestone";
	protected override WeaponPresetKind TestWeapon => WeaponPresetKind.Crossbow;
	private readonly PlayerLoadout _loadout = new();
	private ChargeBodyFx _fx = null!;
	private static IEnumerable<Node> Walk(Node root) { yield return root; foreach(Node child in root.GetChildren()) foreach(var node in Walk(child)) yield return node; }
	private void Press(string action, bool down) => Input.ParseInputEvent(new InputEventAction { Action=action, Pressed=down });
	private int Feedbacks => _fx.ChargeCompleteCount + _fx.ChargeStepCount;
	private static int AudioPlayed
	{
		get
		{
#if DEBUG
			return AudioManager.Instance!.DebugPlayCount;
#else
			return 0;
#endif
		}
	}
	private async Task<bool> Until(Func<bool> ready, double seconds=8)
	{
		for(int frame=0;frame<seconds*100&&!ready();frame++) await Wait(.01);
		return ready();
	}
	private void FrameCamera()
	{
		Player.GetNode<Node3D>("CameraRig").ProcessMode=ProcessModeEnum.Disabled;
		var camera=GetViewport().GetCamera3D();
		camera.GlobalPosition=Player.GlobalPosition+new Vector3(0,2.2f,5.2f);
		camera.LookAt(Player.GlobalPosition+Vector3.Up*1.0f); camera.ResetPhysicsInterpolation();
	}
	private async Task Equip(WeaponPresetKind weapon)
	{
		foreach(var entry in Player.AbilitySlots.Equipped.ToArray()) { Press(entry.InputSlot,false); entry.Ability.ResetForLoadout(); }
		await Wait(.05);
		RunSetup.Character=PlayerCharacterKind.Hunter; RunSetup.Weapon=weapon;
		_loadout.Apply(Player,Mode,RunSetup.Character,weapon);
		Player.Movement?.SetPhysicsProcess(false); Player.Velocity=Vector3.Zero;
		FrameCamera(); await Wait(.10);
	}
	protected override async Task RunChecks()
	{
		_fx=Player.GetNode<ChargeBodyFx>("ChargeBodyFx");
		FrameCamera(); await AimHint();
		var identities=new HashSet<string>();
		foreach(var weapon in Enum.GetValues<WeaponPresetKind>())
		{
			await Equip(weapon);
			string[] slots=Player.AbilitySlots.Equipped.Where(e=>e.Ability.HoldToCharge&&!string.IsNullOrEmpty(e.InputSlot)).Select(e=>e.InputSlot).ToArray();
			foreach(string slot in slots)
			{
				await Equip(weapon);
				var ability=Player.AbilitySlots.Resolve(slot)!; identities.Add(ability.AbilityId);
				int bases=_fx.ChargeCompleteCount, steps=_fx.ChargeStepCount;
				int played=AudioPlayed;
				Press(slot,true);
				Check(await Until(()=>ability.IsCharging),$"{weapon}/{slot}真实按住开始蓄力");
				Check(await Until(()=>_fx.ChargeCompleteCount>bases),$"{ability.AbilityId}基础完成产生一次事件反馈");
				await Wait(.015);
				Check(_fx.ChargeCompleteCount==bases+1&&_fx.LastChargeStage==0,$"{ability.AbilityId}基础完成没有重复声光");
				Check(AudioPlayed==played+1,$"{ability.AbilityId}基础完成实际音频只播放一次");
				Check(_fx.OverlayMeshCount>0&&_fx.FlashShown>0,$"{ability.AbilityId}正式角色网格收到短闪");
				Check(_fx.LastWasPerfect==ability.IsPerfectWindow,$"{ability.AbilityId}完美窗口沿用真实状态");
				if(weapon==WeaponPresetKind.Crossbow) await Shot("065-knife-base");
				int max=ability.ChargeStepSecondsEffective>0?ability.ChargeMaxStepsEffective:0;
				for(int stage=1;stage<=max;stage++)
				{
					int expectedStage=stage;
					Check(await Until(()=>ability.ChargeSteps>=expectedStage),$"{ability.AbilityId}到达真实强化档{stage}");
					await Wait(.015);
					Check(_fx.ChargeStepCount==steps+stage&&_fx.LastChargeStage==stage,$"{ability.AbilityId}强化档{stage}只反馈一次");
					Check(AudioPlayed==played+1+stage,$"{ability.AbilityId}强化档{stage}实际音频数量对应真实档位");
					Check(_fx.LastSfxPitch>1&&_fx.LastFlashColor==_fx.StepFlashColor,$"{ability.AbilityId}强化档使用独立色相与递增音高");
					if(weapon==WeaponPresetKind.Crossbow&&(stage==1||stage==max)) await Shot($"065-knife-step-{stage}");
				}
				int feedbacks=Feedbacks;
				await Wait(.30);
				Check(Feedbacks==feedbacks&&_fx.FlashShown==0,$"{ability.AbilityId}封顶长按不重复闪，短闪按固定时长消退");
				ability.InterruptByControl(); Press(slot,false); await Wait(.04);
				Check(_fx.RimShown==0&&_fx.FlashShown==0&&_fx.LiveRingCount==0&&!_fx.IsProcessing(),$"{ability.AbilityId}取消即清理身体与自有扩张圈并停止空闲更新");
			}
		}
		Check(identities.Count>=3,$"六武器当前槽位中的全部蓄力技能已覆盖：{string.Join(',',identities)}");
		await Lifecycle();
	}
	private async Task AimHint()
	{
		var hud=Walk(Mode).OfType<PlayerHud>().First();
		var hint=hud.GetNode<Label>("Root/AimAssistState");
		var assist=Player.GetNode<AimAssistController>("AimAssist");
		var target=MakeEnemy(Player.GlobalPosition+Vector3.Forward*5); await Wait(.10);
		Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex=MouseButton.Middle,Pressed=true });
		Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex=MouseButton.Middle,Pressed=false }); await Wait(.25);
		var viewport=GetViewport().GetVisibleRect(); var rect=hint.GetGlobalRect();
		Check(assist.Enabled&&hint.Visible,"061真实中键开启后显示辅助瞄准提示");
		Check(rect.Position.X<viewport.Size.X*.1f&&rect.Position.Y>viewport.Size.Y*.75f&&!rect.HasPoint(viewport.Size*.5f),"061提示位于左下HUD边缘，屏幕中央保持空白");
		Check(hint.MouseFilter==Control.MouseFilterEnum.Ignore&&rect.End.Y<=hud.GetNode<Control>("Root/PlayerBar").GetGlobalRect().Position.Y,"061提示不拦鼠标且不叠在血条上");
		Check(assist.Selected==target&&hud.GetNode<Control>("Root/AimAssistTarget").Visible,"061原目标选取与标记仍正常显示");
		await Shot("061-aim-hud-edge");
		Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex=MouseButton.Middle,Pressed=true });
		Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex=MouseButton.Middle,Pressed=false }); await Wait(.10);
		Check(!assist.Enabled&&!hint.Visible,"061中键再次关闭时提示同步隐藏"); target.QueueFree();
	}
	private async Task Lifecycle()
	{
		await Equip(WeaponPresetKind.Crossbow);
		var ability=Player.AbilitySlots.Resolve("weapon_skill_2")!;
		Press("weapon_skill_2",true);
		await Until(()=>ability.ChargeBaseComplete); await Wait(.02);
		float charge=ability.ChargeTime, flash=_fx.FlashShown;
		int count=Feedbacks, audio=AudioPlayed;
		GetTree().Paused=true; await Wait(.25);
		Check(ability.ChargeTime==charge&&_fx.FlashShown==flash&&Feedbacks==count&&AudioPlayed==audio,"065暂停冻结时长、身体短闪与阶段计数，不补播声音");
		GetTree().Paused=false; ability.InterruptByControl(); Press("weapon_skill_2",false); await Wait(.05);
		Check(_fx.LiveRingCount==0&&_fx.RimShown==0,"065暂停恢复后取消可立即清理冻结中的自有圈");

		await Equip(WeaponPresetKind.Crossbow);
		ability=Player.AbilitySlots.Resolve("weapon_skill_2")!;
		Press("weapon_skill_2",true); await Until(()=>ability.IsCharging); await Wait(.1);
		await Equip(WeaponPresetKind.Bow);
		Check(_fx.FlashShown==0&&_fx.RimShown==0&&_fx.LiveRingCount==0,"065真实换装清除旧武器蓄力表现");
		int before=Feedbacks;
		ability.Enabled=true; ability.RefreshCooldown(); ability.TryPress(); ability.OnInputHeld(10);
		Check(Feedbacks==before,"065仓库中旧武器能力即使被调用也不触发当前角色表现"); ability.ResetForLoadout();

		await Equip(WeaponPresetKind.Crossbow);
		ability=Player.AbilitySlots.Resolve("weapon_skill_2")!;
		var mesh=Walk(_fx.BoundModel!).OfType<MeshInstance3D>().First();
		Material? original=mesh.MaterialOverlay, originalOverride=mesh.MaterialOverride;
		var eliteOwner=new object(); var hitOwner=new object();
		var elite=UnitRimOverlay.Create(Colors.Purple,.5f,1); var hit=UnitRimOverlay.Create(Colors.Red,.5f,1);
		UnitRimOverlay.Set(mesh,eliteOwner,elite,10); UnitRimOverlay.Set(mesh,hitOwner,hit,30);
		Press("weapon_skill_2",true); await Until(()=>ability.ChargeBaseComplete); await Wait(.02);
		Check(mesh.MaterialOverlay==hit&&mesh.MaterialOverride==originalOverride,"065短闪不抢受击材质，不修改模型MaterialOverride");
		UnitRimOverlay.RemoveSource(hitOwner);
		Check(mesh.MaterialOverlay==elite,"065受击退出后仍保留精英材质优先级");
		UnitRimOverlay.RemoveSource(eliteOwner); await Wait(.02);
		Check(mesh.MaterialOverlay is ShaderMaterial material&&material.GetShaderParameter("rim_strength").AsSingle()>0,"065更高优先级退出后可恢复仍在进行的蓄力边缘");
		ability.InterruptByControl(); Press("weapon_skill_2",false); await Wait(.04);
		Check(mesh.MaterialOverlay==original,"065取消后归还原叠加材质");
		await Shot("065-cancel-clean");

		await Equip(WeaponPresetKind.Crossbow); ability=Player.AbilitySlots.Resolve("weapon_skill_2")!;
		ability.ConfigureChargeBehavior(new ChargeBehaviorTuning { AutoReleaseOnFullCharge=true,BaseSecondsOverride=.08f,StepSecondsOverride=.08f });
		before=Feedbacks; Press("weapon_skill_2",true);
		Check(await Until(()=>ability.ChargeReleased),"065自动松手能力到达真实满蓄力");
		Check(Feedbacks>before&&_fx.LastChargeStage==ability.ChargeMaxStepsEffective,"065自动释放同帧的最后真实档位不会丢失");
		Press("weapon_skill_2",false); await Wait(.55);
		Check(_fx.FlashShown==0&&_fx.LiveRingCount==0,"065正常释放后的短闪和圈按时回收");

		await Equip(WeaponPresetKind.Crossbow); ability=Player.AbilitySlots.Resolve("weapon_skill_2")!;
		Press("weapon_skill_2",true); await Until(()=>ability.ChargeBaseComplete); await Wait(.02);
		Player.AbilitySlots.Clear(); await Wait(.04); Press("weapon_skill_2",false);
		Check(_fx.RimShown==0&&_fx.FlashShown==0&&_fx.LiveRingCount==0,"065当前槽位直接撤销时也清理，不依赖松手事件");
		ability.ResetForLoadout();

		await Equip(WeaponPresetKind.Crossbow); ability=Player.AbilitySlots.Resolve("weapon_skill_2")!;
		Press("weapon_skill_2",true); await Until(()=>ability.ChargeBaseComplete); await Wait(.02);
		var previousModel=_fx.BoundModel;
		var otherCharacter=Enum.GetValues<PlayerCharacterKind>().First(character=>character!=PlayerCharacterKind.Hunter);
		Press("weapon_skill_2",false); RunSetup.Character=otherCharacter;
		_loadout.Apply(Player,Mode,otherCharacter,WeaponPresetKind.Crossbow); await Wait(.04);
		Check(_fx.BoundModel!=previousModel&&_fx.RimShown==0&&_fx.FlashShown==0&&_fx.LiveRingCount==0,"065真实角色换模清掉旧模型短闪和自有圈");

		await Equip(WeaponPresetKind.Crossbow); ability=Player.AbilitySlots.Resolve("weapon_skill_2")!;
		Press("weapon_skill_2",true); await Until(()=>ability.ChargeBaseComplete); await Wait(.02);
		Player.IsInvulnerable=false;
		Player.ReceiveHit(new HitInstance { Target=Player,Context=EventContext.Create(null,Player),Damage=1_000_000,BypassBlock=true });
		Press("weapon_skill_2",false);
		Check(Player.IsDead&&_fx.RimShown==0&&_fx.FlashShown==0&&_fx.LiveRingCount==0,"065真实死亡事件立即清掉身体、扩张圈与更新");
	}
}
