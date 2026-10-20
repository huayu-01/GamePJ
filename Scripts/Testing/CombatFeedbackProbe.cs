using System;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>结算结果、真实命中、局部时钟、未停世界与相机瞄准隔离验收。</summary>
public partial class CombatFeedbackProbe : Node3D
{
	private int _pass, _fail;
	private Camera3D _camera = null!;
	private CombatFeedbackProfile _saved = null!;
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[Feedback] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
	private async Task Wait(int count) { for (int i = 0; i < count; i++) await Frame(); }
	private async Task Until(Func<bool> condition, int count = 120) { for (int i = 0; i < count && !condition(); i++) await Frame(); }
	private Unit Make(Vector3 position, Faction faction = Faction.Enemy, float armor = 0)
	{
		var unit = new Unit { Faction = faction, MaxHealth = 1000, StartingArmor = armor };
		unit.AddChild(new CollisionShape3D { Name = "Body", Position = Vector3.Up * .9f, Shape = new CapsuleShape3D { Radius = .4f, Height = 1.8f } });
		var model = new Node3D { Name = "Model" }; model.AddChild(new MeshInstance3D { Position = Vector3.Up * .9f,
			Mesh = new CapsuleMesh { Radius = .4f, Height = 1.8f, Material = new StandardMaterial3D { AlbedoColor = new Color(.45f, .5f, .6f) } } });
		unit.AddChild(model); unit.AddChild(new UnitAnimator { Name = UnitAnimator.NodeName });
		unit.AddChild(new UnitPresentation { Name = "Presentation", RemoveOnDeath = false }); AddChild(unit); unit.GlobalPosition = position; return unit;
	}
	private static HitInstance Hit(Unit target, Unit source, float damage = 10, bool dot = false, bool secondary = false, bool contact = false, bool negated = false, bool parry = false) => new()
	{
		Target = target, Attacker = source, Damage = damage, Position = target.GlobalPosition + Vector3.Up * .9f + Vector3.Back * .4f,
		Direction = Vector3.Forward, Context = EventContext.Create(source, target), ContactFeedback = contact, Negated = negated, Parried = parry,
		Attack = new AttackInstance { Attacker = source, Ability = "FeedbackProbe", IsDamageOverTime = dot, IsSecondary = secondary },
	};
	public override async void _Ready()
	{
		await Wait(3); _saved = (CombatFeedbackProfile)CombatFeedback.Profile.Duplicate();
		try
		{
			var floor = new StaticBody3D { Position = Vector3.Down * .5f, CollisionLayer = CombatLayers.World, CollisionMask = 0 };
			floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(60, 1, 60) } });
			floor.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(60, 1, 60), Material = new StandardMaterial3D { AlbedoColor = new Color(.1f, .15f, .2f) } } }); AddChild(floor);
			_camera = new Camera3D { Position = new Vector3(0, 7, 12), Current = true }; AddChild(_camera); _camera.LookAt(Vector3.Up);
			AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -30, 0) });
			await Results(); await Deliveries(); await Camera(); await Clock();
			if (DisplayServer.GetName() != "headless") await Capture();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		finally
		{
			// 只恢复本探针改过的进程内字段，不触碰真实存档。
			var p = CombatFeedback.Profile; p.Enabled = _saved.Enabled; p.LocalPauseEnabled = _saved.LocalPauseEnabled;
			p.LightPauseSeconds = _saved.LightPauseSeconds; p.HeavyPauseSeconds = _saved.HeavyPauseSeconds; p.MaxPauseSeconds = _saved.MaxPauseSeconds; p.CameraStrength = _saved.CameraStrength;
		}
		GD.Print($"[Feedback] === 打击反馈验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task Results()
	{
		var source = Make(new Vector3(0, 0, 3), Faction.Player); var target = Make(Vector3.Zero, armor: 30);
		var result = target.ReceiveHit(Hit(target, source));
		Check(Mathf.IsEqualApprox(result.ArmorLost, 7) && target.Health == 1000 && CombatFeedback.LastKind == ImpactKind.Armor, "护甲保留30%减伤，10伤耗7甲，走蓝色碰甲结果");
		Check(target.Reactions!.HitStunCount == 0, "有甲命中没有普通受击硬直");
		await Wait(4); result = target.ReceiveHit(Hit(target, source, 40));
		Check(result.ArmorBroken && CombatFeedback.LastKind == ImpactKind.ArmorBreak && target.Armor == 0, "破甲事实来自本击结算快照");
		Check(target.Reactions.HitStunCount == 0, "破甲当击仍免普通受击硬直");
		await Wait(4); target.ReceiveHit(Hit(target, source));
		Check(CombatFeedback.LastKind == ImpactKind.Flesh && target.Reactions.HitStunCount == 1, "下一击打肉才产生原有普通硬直");
		await Wait(4); target.IsInvulnerable = true; int count = CombatFeedback.PresentedCount; result = target.ReceiveHit(Hit(target, source));
		Check(result.Invulnerable && CombatFeedback.LastKind == ImpactKind.None && CombatFeedback.PresentedCount == count, "无敌擦过不播放成功命中反馈");
		target.IsInvulnerable = false; target.Reactions.Reset(); target.ReceiveHit(Hit(target, source, negated: true));
		Check(CombatFeedback.LastKind == ImpactKind.Ward && !target.Reactions.IsStaggered, "效果护罩有防御反馈，无格挡者硬直");
		await Wait(4); target.ReceiveHit(Hit(target, source, negated: true, parry: true));
		Check(CombatFeedback.LastKind == ImpactKind.Parry, "技能招架事实与效果护罩分开");
		await Wait(4); target.RotationDegrees = new Vector3(0, 180, 0); var block = new BlockComponent { Name = "Block", BlockDamageMultiplier = 0 }; target.AddChild(block);
		float hp = target.Health; result = target.ReceiveHit(Hit(target, source));
		Check(result.Blocked && target.Health == hp && CombatFeedback.LastKind == ImpactKind.Block && target.Reactions.BlockStunCount == 1, "持盾正面完全格挡，黄色反馈与自身硬直保留");
		block.Blocking = false; await Wait(4); count = CombatFeedback.PresentedCount; hp = target.Health; target.ReceiveHit(Hit(target, source, dot: true));
		Check(target.Health == hp - 10 && CombatFeedback.PresentedCount == count, "DoT照常扣血，不闪烁／停顿／震屏／刷音效");
		await Wait(10); int sounds = CombatFeedback.SoundCount, cameras = CombatFeedback.CameraCount;
		for (int i = 0; i < 12; i++)
		{
			var crowd = Make(new Vector3((i % 4 - 1.5f) * .8f, 0, -i / 4)); crowd.ReceiveHit(Hit(crowd, source)); crowd.QueueFree();
		}
		Check(CombatFeedback.SoundCount - sounds <= 1 && CombatFeedback.CameraCount - cameras <= 1, "同帧群体12命中最多一个音效／镜头脉冲");
		await Wait(6); count = CombatFeedback.PresentedCount; target.ReceiveHit(Hit(target, source)); target.ReceiveHit(Hit(target, source));
		Check(CombatFeedback.PresentedCount == count + 1, "同目标密集命中限频，伤害仍独立结算");
		await Wait(6); sounds = CombatFeedback.SoundCount; cameras = CombatFeedback.CameraCount; target.ReceiveHit(Hit(target, source, secondary: true, contact: true));
		Check(CombatFeedback.SoundCount == sounds && CombatFeedback.CameraCount == cameras && !source.ImpactPause!.IsHolding, "派生伤害轻反馈，不抢镜头／停顿预算");
		var shield = Make(new Vector3(3, 0, 0)); shield.ApplyBaseStats(1000, 0, 30, 0); shield.ReceiveHit(Hit(shield, source));
		Check(CombatFeedback.LastKind == ImpactKind.Shield && shield.Shield == 20, "资源护盾使用紫色防御材质反馈");
		var p = CombatFeedback.Profile; p.Enabled = false; hp = target.Health; count = CombatFeedback.PresentedCount; target.ReceiveHit(Hit(target, source));
		Check(target.Health == hp - 10 && CombatFeedback.PresentedCount == count, "关闭反馈后伤害与规则完全照常"); p.Enabled = true;
		source.QueueFree(); target.QueueFree(); shield.QueueFree(); await Wait(5);
	}
	private async Task Camera()
	{
		var effect = CameraImpact.Ensure(_camera); Vector3 position = _camera.Position; Basis basis = _camera.Basis; float fov = _camera.Fov;
		Vector2 center = GetViewport().GetVisibleRect().Size * .5f; Vector3 ray = _camera.ProjectRayNormal(center); Vector3 origin = CameraImpact.AimOrigin(_camera, center);
		effect.Kick(Vector3.Right, 2); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		Check(effect.Offset.Length() > 0 && effect.Offset.Length() <= CombatFeedback.Profile.CameraMaxOffset, "镜头实际产生有上限的短促方向冲击");
		Check(_camera.Position == position && _camera.Basis == basis && _camera.Fov == fov, "冲击不写相机跟随位置／旋转／ADS视野");
		Check(ray.DistanceTo(_camera.ProjectRayNormal(center)) < .00001f && origin.DistanceTo(CameraImpact.AimOrigin(_camera, center)) < .0001f, "准星射线方向与求交原点不受镜头冲击影响");
		await Wait(15); Check(effect.Offset == Vector2.Zero && _camera.HOffset == 0 && _camera.VOffset == 0, "镜头归零，无累积漂移");
		effect.Kick(Vector3.Right, 2); CombatFeedback.Profile.CameraStrength = 0; await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		Check(effect.Offset == Vector2.Zero, "震动强度0即时关闭"); CombatFeedback.Profile.CameraStrength = _saved.CameraStrength;
	}
	private async Task Deliveries()
	{
		var source = Make(new Vector3(-8, 0, 4), Faction.Player); var target = Make(new Vector3(-3, 0, 0));
		var projectile = new Projectile { Speed = 20 }; AddChild(projectile); projectile.GlobalPosition = new Vector3(-6, .9f, 0);
		projectile.Launch(new AttackInstance { Attacker = source, BaseDamage = 10, Lifetime = 3 }, Vector3.Right);
		await Until(() => target.Health < 1000);
		Check(target.Health == 990 && !source.ImpactPause!.IsHolding && source.ImpactPause.AcceptedCount == 0, "真实投射物结算一次，远程命中不暂停射手");
		var explosion = RuntimeResources.Load<PackedScene>("res://Scenes/Combat/Explosion.tscn").Instantiate<Explosion>(); AddChild(explosion); explosion.GlobalPosition = target.GlobalPosition + Vector3.Up * .9f;
		explosion.Detonate(source, 10, 2, Array.Empty<string>()); await Until(() => target.Health < 990);
		Check(target.Health == 980 && source.ImpactPause!.AcceptedCount == 0, "真实爆炸不按附着近战处理，不暂停施法者");
		await Wait(6); int cameras = CombatFeedback.CameraCount; source.Tags = new[] { "Summon" }; target.ReceiveHit(Hit(target, source, contact: true));
		Check(CombatFeedback.CameraCount == cameras && source.ImpactPause!.AcceptedCount == 0, "召唤物攻击不冒领玩家镜头／局部停顿");
		int count = CombatFeedback.PresentedCount; target.ReceiveHit(Hit(target, source, 2000));
		Check(target.IsDead && CombatFeedback.PresentedCount == count + 1, "击杀反馈可以越过普通目标限频，不延迟死亡结算");
		source.QueueFree(); target.QueueFree(); await Wait(5);
	}
	private async Task Clock()
	{
		var p = CombatFeedback.Profile; p.LightPauseSeconds = p.HeavyPauseSeconds = .09f; p.MaxPauseSeconds = .1f;
		var player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(player); player.GlobalPosition = new Vector3(10, .05f, 4); player.IsInvulnerable = true;
		new PlayerLoadout().Apply(player, this, PlayerCharacterKind.WeaponMaster, WeaponPresetKind.SpiritSword);
		_camera.MakeCurrent(); player.GetNode<ActRoguelike.Camera.CameraRig>("CameraRig").CaptureMouseOnStart = false;
		var target = Make(new Vector3(10, .05f, 2)); target.GrantArmor(1000);
		await Wait(15); var ability = player.GetNode<MeleeComboAbility>("AbilityWeapon4Skill1"); var animator = player.GetNode<UnitAnimator>(UnitAnimator.NodeName);
		Check(ability.TryActivate(target), "真实灵剑连招起手成功");
		await Until(() => player.ImpactPause!.IsHolding);
		Check(player.ImpactPause!.IsHolding && target.Armor < 1000, "真实ATK→命中盒→扣甲→出手者局部停顿");
		Check(!target.ImpactPause!.IsHolding && target.Reactions!.HitStunCount == 0, "命中顿帧不暂停有甲防御者，不冒充受击打断");
		double timeline = ability.ActionTimelineTime; int strikes = ability.StrikeCount; Vector3 pos = player.GlobalPosition;
		var projectile = new Projectile { Speed = 10 }; AddChild(projectile); projectile.GlobalPosition = new Vector3(-10, 4, 0);
		projectile.Launch(new AttackInstance { Attacker = player, Lifetime = 5 }, Vector3.Right); Vector3 bulletPos = projectile.GlobalPosition;
		await Wait(3);
		Check(Math.Abs(ability.ActionTimelineTime - timeline) < .0001 && ability.StrikeCount == strikes, "停顿期间动画事件位置不前进、不重复ATK");
		Check(player.GlobalPosition.DistanceTo(pos) < .0001, "动作突进与动画共用停顿，不出现滑步");
		Check(projectile.GlobalPosition.DistanceTo(bulletPos) > .15f && Engine.TimeScale == 1, "外部弹道照常飞行，全局TimeScale未改");
		int accepted = player.ImpactPause.AcceptedCount; player.ImpactPause.Request(.09f, .12f); Check(player.ImpactPause.AcceptedCount == accepted, "同刀多目标命中不叠加停顿");
		await Wait(8); Check(!player.ImpactPause.IsHolding && ability.ActionTimelineTime > timeline, "到期后动画／动作时钟同步续播");
		await Wait(10); player.ImpactPause.Request(.09f, .12f); ability.InterruptByControl();
		Check(!player.ImpactPause.IsHolding && !ability.IsBusy, "动作中断清理局部停顿，不冻结下一动作");
		await Wait(10); player.ImpactPause.Request(.09f, .12f); p.LocalPauseEnabled = false; await Wait(2);
		Check(!player.ImpactPause.IsHolding, "运行中关闭顿帧可恢复"); p.LocalPauseEnabled = true;
		await Wait(10); player.ImpactPause.Request(.09f, .12f); float remaining = player.ImpactPause.Remaining;
		GetTree().Paused = true;
		await ToSignal(GetTree().CreateTimer(.05, processAlways: true), SceneTreeTimer.SignalName.Timeout);
		Check(player.ImpactPause.Remaining == remaining, "打开暂停界面期间局部时钟不偷偷流逝"); GetTree().Paused = false;
		await Wait(10); Check(!player.ImpactPause.IsHolding, "恢复游戏后局部停顿正常结束");
		int tickRate = Engine.PhysicsTicksPerSecond;
		try
		{
			await Wait(10); Engine.PhysicsTicksPerSecond = 30; player.ImpactPause.Request(.05f, .12f); await Wait(5);
			Check(!player.ImpactPause.IsHolding && Engine.TimeScale == 1, "30Hz物理步下短停顿能结束，不改全局时间");
			Engine.PhysicsTicksPerSecond = 120; await Wait(16);
			int accepted120 = player.ImpactPause.AcceptedCount; player.ImpactPause.Request(.05f, .12f);
			Check(player.ImpactPause.AcceptedCount == accepted120 + 1, "切换物理频率并等冷却后接受新停顿"); await Wait(3);
			Check(player.ImpactPause.IsHolding, "120Hz物理步下按秒保持，不固定成一帧"); await Wait(8);
			Check(!player.ImpactPause.IsHolding, "120Hz物理步下按秒恢复");
		}
		finally { Engine.PhysicsTicksPerSecond = tickRate; }
		await Wait(10); player.ImpactPause.Request(.09f, .12f); player.ForceKill(); await Wait(2);
		Check(!player.ImpactPause.IsHolding, "死亡清理停顿，不挂死死亡动画");
		p.LightPauseSeconds = _saved.LightPauseSeconds; p.HeavyPauseSeconds = _saved.HeavyPauseSeconds; p.MaxPauseSeconds = _saved.MaxPauseSeconds;
		player.QueueFree(); target.QueueFree(); projectile.QueueFree(); await Wait(4);
	}
	private async Task Capture()
	{
		_camera.MakeCurrent(); _camera.Position = new Vector3(0, 4, 10); _camera.Projection = Camera3D.ProjectionType.Perspective; _camera.Fov = 35; _camera.LookAt(Vector3.Up); _camera.ResetPhysicsInterpolation();
		// 清理此前真实战斗火花的视觉寿命，再等待相机渲染状态就位，避免截到旧投影和残留粒子。
		await Wait(120);
		var layer = new CanvasLayer(); AddChild(layer); var label = new Label { Position = new Vector2(26, 24), Text = "战斗反馈：蓝色碰甲 / 金色格挡 / 亮蓝破甲\n特效位于真实命中表面；预警、碰撞与伤害独立" }; label.AddThemeFontSizeOverride("font_size", 24); layer.AddChild(label);
		for (int i = 0; i < 3; i++)
		{
			var target = Make(new Vector3((i - 1) * 2.5f, 0, 0), armor: i == 0 ? 30 : 0);
			var kind = new[] { ImpactKind.Armor, ImpactKind.Block, ImpactKind.ArmorBreak }[i]; target.GetNode<UnitPresentation>("Presentation").ShowImpact(kind, 1);
			CombatImpactFx.Spawn(this, target.GlobalPosition + new Vector3(0, 1, .4f), Vector3.Forward, kind, 2);
			target.AddChild(new Label3D { Position = Vector3.Up * 2.5f, Text = new[] { "碰甲", "格挡", "破甲" }[i], FontSize = 44, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled });
		}
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng("res://docs/screenshots/j38_combat_feedback.png") == Error.Ok, "渲染反馈截图保存成功");
	}
}
