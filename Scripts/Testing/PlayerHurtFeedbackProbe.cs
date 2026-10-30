using System.Threading.Tasks;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Testing;

public partial class PlayerHurtFeedbackProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "PlayerHurt";
	protected override async Task RunChecks()
	{
		var enemy=MakeEnemy(Player.GlobalPosition+Vector3.Back*3);
		// 固定验收相机，排除正式跟随镜头尚未收敛的平移对瞄准断言的影响。
		var camera=new Camera3D(); Mode.AddChild(camera); camera.GlobalPosition=Player.GlobalPosition+new Vector3(5,4,5);
		camera.LookAt(Player.GlobalPosition+Vector3.Up); camera.Current=true;
		await Wait(.06);
		Vector3 aim=CameraImpact.AimOrigin(camera,GetViewport().GetVisibleRect().Size*.5f);
		HitInstance Damage(bool secondary=false,bool dot=false)=>new(){Attacker=enemy,Target=Player,Context=EventContext.Create(enemy,Player),Damage=100,Position=Player.GlobalPosition+Vector3.Back*4,Direction=Vector3.Forward,
			Attack=new AttackInstance{Attacker=enemy,BaseDamage=100,IsSecondary=secondary,IsDamageOverTime=dot}};
		Player.ReceiveHit(Damage(true)); var feedback=Player.GetNodeOrNull<PlayerHurtFeedback>("HurtFeedback");
		Check(feedback?.PresentedCount==1 && feedback.ScreenAlpha>0,"背后／衍生伤害实际扣血也有屏幕受击提示");
		Player.ReceiveHit(Damage(true)); Check(feedback!.PresentedCount==1,"同帧多弹不会重复叠加受击屏幕与镜头");
		await Wait(.04); Check(CameraImpact.Ensure(camera).Offset.Length()>0,"玩家受伤确实产生细微投影抖动");
		float aimDrift=CameraImpact.AimOrigin(camera,GetViewport().GetVisibleRect().Size*.5f).DistanceTo(aim);
		Check(aimDrift<.0001f,$"屏幕反馈不改变准星实际瞄准原点：偏差{aimDrift:0.######}m");
		await Shot("player-hurt-edge");
		float alpha=feedback.ScreenAlpha; GetTree().Paused=true; await Wait(.12);
		Check(Mathf.IsEqualApprox(alpha,feedback.ScreenAlpha),"暂停时受击淡出与冷却不推进"); GetTree().Paused=false;
		await Wait(.3); Check(feedback.ScreenAlpha==0,"受击提示到期完整恢复，无残留红边");
		Player.IsInvulnerable=true; Player.ReceiveHit(Damage()); Check(feedback.PresentedCount==1,"无敌抵消不播放玩家受伤反馈"); Player.IsInvulnerable=false;
		Player.ReceiveHit(Damage(true,true)); Check(feedback.PresentedCount==2,"持续区域造成实际损失也有受击提示，仍受独立限频");
		await Wait(.3); float strength=CombatFeedback.Profile.CameraStrength; CombatFeedback.Profile.CameraStrength=0;
		int cameras=CombatFeedback.CameraCount; Player.ReceiveHit(Damage());
		Check(feedback.PresentedCount==3 && CombatFeedback.CameraCount==cameras,"关闭镜头抖动后仍保留屏幕提示且不启动新抖动");
		CombatFeedback.Profile.CameraStrength=strength;
	}
}
