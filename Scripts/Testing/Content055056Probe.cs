using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实弹体引爆、空间波生命周期，以及暂停中鼠标/键盘跨保护期输入。</summary>
public partial class Content055056Probe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "Content055056";
	private static IEnumerable<Node> Walk(Node node) { yield return node; foreach (Node child in node.GetChildren()) foreach (Node item in Walk(child)) yield return item; }
	private async Task Until(Func<bool> ready, double timeout = 4)
	{
		ulong end = Time.GetTicksMsec() + (ulong)(timeout * 1000);
		while (!ready() && Time.GetTicksMsec() < end) await Wait(.01);
		if (!ready()) throw new InvalidOperationException("等待真实条件超时");
	}
	private void Mouse(bool pressed, Vector2 point) => Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = pressed, Position = point, GlobalPosition = point });
	private async Task Tap(Key key)
	{
		Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true }); await Wait(.02);
		Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false }); await Wait(.02);
	}
	protected override async Task RunChecks()
	{
		Player.IsInvulnerable = true;
		await Pulse(); await Opening();
	}
	private async Task Pulse()
	{
		var run = Mode.GetNode<RunController>("RunController");
		Check(run.GrantReward("evo_blast_palm"), "055通过正式奖励入口获得爆裂火球");
		float radius = Player.Stats.Get("ExplosionRadiusAdd"), scale = Player.Stats.Get("ExplosionDamageScale");
		Check(Mathf.IsEqualApprox(radius, 3.5f) && Mathf.IsEqualApprox(scale, .6f), "055保留原3.5米与60%伤害参数");
		Vector3 center = Player.GlobalPosition + Vector3.Forward * 7;
		var target = MakeEnemy(center); var neighbour = MakeEnemy(center + Vector3.Right * 1.6f);
		await Wait(.05); float hp = neighbour.Health;
		var scene = RuntimeResources.Load<PackedScene>("res://Scenes/Combat/EnergyWave.tscn");
		var shot = scene.Instantiate<Projectile>(); Mode.AddChild(shot);
		Check(shot.ExplosionScene?.ResourcePath == "res://Scenes/Combat/EnergyPulseExplosion.tscn", "055正式能量波选择独立空间波爆炸预制体");
		shot.GlobalPosition = center + Vector3.Up * .9f + Vector3.Back * 2;
		shot.Speed = 20;
		shot.Launch(new AttackInstance { Attacker = Player, Ability = "055Probe", BaseDamage = 20, ExplosionDamage = 20 * scale, ExplosionRadius = radius, Policy = HitPolicy.OncePerTarget, Tags = new[] { "Projectile" } }, Vector3.Forward);
		await Until(() => Walk(GetTree().CurrentScene).OfType<EnergyPulseFx>().Any());
		var fx = Walk(GetTree().CurrentScene).OfType<EnergyPulseFx>().Single();
		Check(!Walk(fx).Any(n => n is CollisionObject3D || n is Unit || n.GetGroups().Count > 0), "055表现子树不含单位、碰撞或分组");
		var other = RuntimeResources.Load<PackedScene>("res://Assets/Effects/fx_energy_pulse.tscn").Instantiate<EnergyPulseFx>(); Mode.AddChild(other); other.Position = center + Vector3.Right * 8;
		Check(fx.GetNode<MeshInstance3D>("Shell").MaterialOverride != other.GetNode<MeshInstance3D>("Shell").MaterialOverride, "055并发空间波独享材质，不串生命周期");
		var camera = GetViewport().GetCamera3D(); var rig = Player.GetNode<Node3D>("CameraRig"); rig.SetPhysicsProcess(false); rig.SetProcessInput(false);
		camera.GlobalPosition = center + new Vector3(6, 4, 7); camera.LookAt(center + Vector3.Up * .8f); camera.ResetPhysicsInterpolation();
		other.QueueFree(); await Wait(.05); await Shot("055-energy-pulse-early");
		await Wait(.10);
		Check(IsInstanceValid(fx) && fx.GetParent() == GetTree().CurrentScene && !Walk(Mode).OfType<Explosion>().Any(), "0550.12秒判定结束后空间波独立播放");
		Check(Mathf.Abs((hp - neighbour.Health) - 12) < .01f, "055相邻目标仅受到原60%爆炸伤害一次");
		Check(fx.Scale.X <= radius && fx.Scale.X > radius * .5f, "055视觉外壳在真实范围以内，不再使用旧2.5倍放大");
		Check(!Walk(fx).Any(n => n is Light3D || n is GpuParticles3D), "055使用单网格轻微折射，不叠高亮灯或火烟粒子");
		await Shot("055-energy-pulse-late");
		await Wait(.5); Check(!IsInstanceValid(fx) && Mathf.Abs((hp - neighbour.Health) - 12) < .01f, "055表现按时回收，延长视觉不延长伤害窗口");
		var legacy = RuntimeResources.Load<PackedScene>("res://Scenes/Combat/Bullet.tscn").Instantiate<Projectile>();
		Check(legacy.ExplosionScene == null, "055其他弹体仍使用原爆炸表现"); legacy.Free(); target.QueueFree(); neighbour.QueueFree();
	}
	private async Task Opening()
	{
		var reward = Mode.GetNode<RewardScreen>("RunController/RewardScreen");
		RewardDefinition[] options = { RewardCatalog.Find("upg_power")!, RewardCatalog.Find("evo_divergence")! };
		var chosen = new List<int>(); reward.RewardChosen += chosen.Add;
		Mouse(true, new Vector2(3, 3)); reward.Open(options, "056 · 连续攻击中的奖励");
		Check(reward.OpenGuardSeconds >= 1 && reward.OpenFadeSeconds == 1 && reward.FadeSeconds < .3f, "056默认1秒保护和1秒打开缓动，关闭仍迅速");
		await Wait(.65); await Tap(Key.Key1);
		Check(reward.InputBlocked && chosen.Count == 0 && GetTree().Paused, "0560.65秒仍拒绝数字键，暂停不冻结真实时钟保护");
		await Shot("056-reward-opening");
		await Wait(.5); Check(reward.InputBlocked && chosen.Count == 0, "056左键从战斗持续按住，1秒后也不能直接选中奖励");
		var card = reward.GetNode<Container>("Root/Panel/VBox/ChoicesScroll/Cards").GetChildren().OfType<Button>().Last(b => b.Visible);
		Vector2 point = card.GetGlobalRect().Position + new Vector2(card.Size.X * .5f, 90);
		Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point }); await Wait(.03);
		Mouse(false, point); await Wait(.05);
		Check(!reward.InputBlocked && chosen.Count == 0, "056跨过保护期的松手被消费，必须重新点击");
		Mouse(true, point); await Wait(.02); Mouse(false, point); await Wait(.03);
		// Dummy显示器不分发GUI鼠标，补走正式按钮信号；渲染模式必须通过完整鼠标链。
		if (DisplayServer.GetName() == "headless") card.EmitSignal(Button.SignalName.Pressed);
		Check(chosen.SequenceEqual(new[] { 1 }), $"056松手后新点击正常选择第二项（{DisplayServer.GetName()}）：chosen={string.Join(',', chosen)}");
		await Tap(Key.Enter); Check(chosen.Count == 1, "056点击后Enter不重复发奖");
		await Shot("056-reward-protected"); reward.Close(); await Wait(.2);
		reward.Open(options, "056 · 再次打开");
		await Wait(.65); card = reward.GetNode<Container>("Root/Panel/VBox/ChoicesScroll/Cards").GetChildren().OfType<Button>().Last(b => b.Visible);
		point = card.GetGlobalRect().GetCenter(); Mouse(true, point); Mouse(false, point); await Tap(Key.Enter);
		Check(reward.InputBlocked && chosen.Count == 1, "056新一页重置1秒保护，实际鼠标和Enter均无效");
		await Until(() => !reward.InputBlocked); await Tap(Key.Key1);
		Check(chosen.SequenceEqual(new[] { 1, 0 }), "056保护结束后数字键正常选择当前页第一项"); reward.Close(); await Wait(.2);
		Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = true });
		reward.Open(options, "056 · 按住确认键时打开"); await Wait(1.1);
		Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = true, Echo = true });
		Check(reward.InputBlocked && chosen.Count == 2, "056开窗前按住Enter，跨过1秒和重复事件仍不能确认");
		Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = false }); await Wait(.03);
		Check(!reward.InputBlocked && chosen.Count == 2, "056旧确认键松开不选取");
		await Tap(Key.Space); Check(chosen.SequenceEqual(new[] { 1, 0, 0 }), "056重新按Space正常确认当前焦点且仅一次"); reward.Close(); await Wait(.2);
		var settings = UiService.Instance!.Screen<SettingsScreen>()!;
		Check(settings.OpenGuardSeconds >= 1 && settings.OpenFadeSeconds >= 1, "056设置继承共用保护参数，重绑输入仍经过基类过滤");
		Check(!UiRouter.Instance!.AnyOpen && !GetTree().Paused, "056选择结束后界面栈、暂停正常恢复");
	}
}
