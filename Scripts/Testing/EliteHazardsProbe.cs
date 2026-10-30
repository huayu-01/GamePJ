using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Combat.Elites;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实导航场景中的12种精英危险及共通边界；没有用假伤害代替生产判定。</summary>
public partial class EliteHazardsProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "EliteHazards";
	private Vector3 _origin;
	private EliteTraitWave? _wave;
	private readonly List<Unit> _enemies = new();
	private EliteTraitDefinition Settings(string key, float radius = 2)
	{
		var def = Definition(key); def.WarningSeconds = .35f; def.Cooldown = 10; def.Duration = 1.1f; def.Damage = 25; def.Radius = radius; def.Count = 1; return def;
	}
	private Unit Enemy(Vector3 offset) { var enemy = MakeEnemy(_origin + offset); _enemies.Add(enemy); return enemy; }
	private async Task Reset()
	{
		_wave?.Stop(); _wave?.World.Clear();
		if (_wave != null && IsInstanceValid(_wave)) { _wave.World.QueueFree(); _wave.QueueFree(); }
		_wave = null;
		foreach (var enemy in _enemies) if (IsInstanceValid(enemy)) enemy.QueueFree(); _enemies.Clear();
		Player.GlobalPosition = _origin + Vector3.Up * .06f; Player.Velocity = Vector3.Zero;
		Player.Movement?.SetPhysicsProcess(true); await Wait(.16); Player.Movement?.SetPhysicsProcess(false);
		Player.FillVitals();
	}
	protected override async Task RunChecks()
	{
		_origin = Player.GlobalPosition; Player.Movement?.SetPhysicsProcess(true); await Wait(.18); Player.Movement?.SetPhysicsProcess(false); _origin = Player.GlobalPosition;
		await Burner(); await Vines(); await Link(); await Slicer(); await Spinner(); await Arcane(); await Frozen(); await Molten(); await Tidal(); await Aftershock(); await InterruptedAftershock(); await Brand(); await Thorns(); await WallClipping(); await CommonBoundaries(); await Reset();
	}
	private async Task Burner()
	{
		await Reset(); _wave = MakeWave(Settings("burner"), Enemy(Vector3.Right * 5)); float health = Player.Health;
		await Wait(.16); Check(_wave.World.AcceptedCount == 1 && Player.Health == health, "燃烧先展示预警，尚未伤害玩家");
		await Wait(.35);
		Check(Player.Health < health && _wave.World.DamageCount > 0, "燃烧地面区域真实造成伤害");
		await Shot("elite-burner");
	}
	private async Task Vines()
	{
		await Reset(); _wave = MakeWave(Settings("burrower"), Enemy(Vector3.Right * 5));
		await Wait(.53); Check(Player.HasStatus("EliteVines") && Player.Stats.Get("MoveSpeedMultiplier") == 0, "蔓生接地后实际束缚移动");
		float remaining = Player.Statuses.FirstOrDefault(s => s.Effect.Id == "EliteVines")?.Remaining ?? 0;
		await Wait(.52); float later = Player.Statuses.FirstOrDefault(s => s.Effect.Id == "EliteVines")?.Remaining ?? 0;
		Check(later < remaining, "藤蔓重复跳伤不持续刷新束缚锁死玩家");
		_wave.World.Clear(); Check(!Player.HasStatus("EliteVines"), "清场撤销该世界附加的藤蔓状态");
	}
	private async Task Link()
	{
		await Reset(); var left = Enemy(Vector3.Left * 3); var right = Enemy(Vector3.Right * 3);
		_wave = MakeWave(Settings("linker", 5), left, right); float health = Player.Health;
		await Wait(.55); Check(Player.Health < health && _wave.World.ActiveCount == 1, "同波两敌之间实际连线命中中间玩家");
		await Shot("elite-linker"); right.GlobalPosition += Vector3.Right * 30; await Wait(.1); health = Player.Health;
		await Wait(.55); Check(_wave.World.ActiveCount == 0 && Player.Health == health, "连接目标离开距离后立即断线并停止伤害");
	}
	private async Task Slicer()
	{
		await Reset(); var def = Settings("slicer", 5); def.Magnitude = 15;
		_wave = MakeWave(def, Enemy(Vector3.Left * 2)); float health = Player.Health;
		await Wait(.6); Check(Player.Health < health, "扫射旋转光束通过生产线段判定造成伤害");
		Check(_wave.World.AcceptedCount == 1, "扫射单束在存续期内不会按帧重复创建");
	}
	private async Task Spinner()
	{
		await Reset(); _wave = MakeWave(Settings("spinner", 1), Enemy(Vector3.Left)); float health = Player.Health;
		await Wait(.55); Check(_wave.World.AcceptedCount == 3, "环星确实生成三颗独立环绕球");
		Check(Player.Health < health, "接触环绕球产生真实伤害");
		await Shot("elite-spinner");
	}
	private async Task Arcane()
	{
		await Reset(); var def = Settings("arcane", 4); def.Duration = 2;
		var owner = Enemy(Vector3.Left * 4); _wave = MakeWave(def, owner);
		await Wait(.08); Check(_wave.World.ActiveCount == 1, "秘法生成固定节点与预警光束");
		var beam = _wave.World.Hazards.FirstOrDefault(); if (beam == null) return;
		Player.GlobalPosition = beam.Center + (beam.End - beam.Center).Normalized() * 1.2f; float health = Player.Health;
		await Wait(.45); Check(Player.Health < health, "秘法固定节点激光造成真实伤害");
		Vector3 center = beam.Center, end = beam.End; owner.ApplyDirectDamage(100000, Player);
		await Wait(.3); Check(!beam.IsDisposed && beam.Center == center && beam.End.DistanceTo(end) > .05f, "已启动秘法节点在本体死亡后仍固定原地继续旋转");
		await Shot("elite-arcane");
	}
	private async Task Frozen()
	{
		await Reset(); var def = Settings("frozen", 1.2f); def.Magnitude = .8f;
		_wave = MakeWave(def, Enemy(Vector3.Right * 4)); float health = Player.Health;
		await Wait(.53);
		Check(Player.Health < health && Player.HasStatus("EliteFrozen") && Player.HasActionLock, "冰晶延迟爆炸后实际冻结并锁定行动");
		await Wait(.85); Check(!Player.HasActionLock, "冰冻到期释放行动锁");
	}
	private async Task Molten()
	{
		await Reset(); var def = Settings("molten", 2.5f); def.Duration = 2;
		var owner = Enemy(Vector3.Right * 4); _wave = MakeWave(def, owner); owner.GlobalPosition = _origin;
		float health = Player.Health; await Wait(.5);
		Check(_wave.World.AcceptedCount >= 1 && Player.Health < health, "熔火按移动距离留下会伤害玩家的火路");
		int accepted = _wave.World.AcceptedCount; owner.ApplyDirectDamage(100000, Player); await Wait(.08);
		Check(_wave.World.AcceptedCount == accepted + 1, "真实死亡只生成一次熔火遗爆");
		var explosion = _wave.World.Hazards.LastOrDefault(); Check(explosion != null && !explosion.IsActive, "熔火遗爆保留完整延迟预警");
		health = Player.Health; await Wait(1.1); Check(Player.Health < health, "本体死亡后遗留危险仍真实结算伤害");
	}
	private async Task Tidal()
	{
		await Reset(); _wave = MakeWave(Settings("tidal", 5), Enemy(Vector3.Left * 4)); float health = Player.Health;
		await Wait(.85); Check(_wave.World.AcceptedCount == 2 && Player.Health == health, "潮汐内圈爆发时外侧保留安全区域");
		await Wait(1.1); Check(Player.Health < health, "随后外环爆发会伤害仍停留外侧的玩家");
	}
	private async Task Aftershock()
	{
		await Reset(); var owner = Enemy(Vector3.Left); _wave = MakeWave(Settings("aftershock", 2), owner);
		var ability = owner.GetNode<MeleeAttackAbility>("AbilityMelee"); ability.Enabled = true; ability.Windup = .18f; ability.Recover = .12f;
		Check(ability.TryActivate(Player), "真实近战能力发动用于余震路径记录");
		await Wait(.25); owner.GlobalPosition += Vector3.Forward * .7f;
		for (int i = 0; i < 40 && _wave.World.AcceptedCount == 0; i++) await Wait(.02);
		Check(_wave.World.AcceptedCount >= 1, "真实动作结束后才生成实际路径余震");
		var trail = _wave.World.Hazards.FirstOrDefault();
		if (trail != null)
		{
			Player.GlobalPosition = trail.Center; float health = Player.Health; await Wait(.55);
			Check(Player.Health < health, "余震路径在预警后实际伤害玩家");
		}
	}
	private async Task InterruptedAftershock()
	{
		await Reset(); var owner = Enemy(Vector3.Left); _wave = MakeWave(Settings("aftershock", 2), owner);
		var ability = owner.GetNode<MeleeAttackAbility>("AbilityMelee");
		ability.Enabled = true; ability.CanBeInterrupted = true; ability.InterruptLockSeconds = 0; ability.Windup = 5;
		Check(ability.TryActivate(Player), "传统近战进入长前摇，用真实受击检验余震中断反例");
		await Wait(.12); owner.ApplyDirectDamage(1, Player); await Wait(.35);
		Check(!ability.IsBusy && ability.ExecutedAttackCount == 0 && _wave.World.AcceptedCount == 0, "前摇被真实受击打断且未打开命中窗口，不产生余震");
	}
	private async Task Brand()
	{
		await Reset(); var def = Settings("trail_brand", 1); def.Duration = 2;
		_wave = MakeWave(def, Enemy(Vector3.Right * 5)); float health = Player.Health;
		await Wait(.55); Check(Player.Health < health && _wave.World.AcceptedCount == 1, "疾行烙印在经过的位置延迟爆炸");
		Player.GlobalPosition += Vector3.Right * 1.1f; await Wait(.6);
		Check(_wave.World.AcceptedCount >= 2 && _wave.World.AcceptedCount <= 4, "烙印按距离和采样间隔布点，不按帧堆积");
	}
	private async Task Thorns()
	{
		await Reset(); var def = Settings("thorn_grid", 3); def.Count = 5; def.Duration = 3;
		_wave = MakeWave(def, Enemy(Vector3.Right * 6)); float health = Player.Health;
		await Wait(.1); Check(_wave.World.AcceptedCount == 5, "荆棘棋盘生成有限的交错地板区");
		Player.Movement?.SetPhysicsProcess(true); Player.Movement?.BeginJump(7);
		await Wait(.3);
		Check(Player.GlobalPosition.Y > _origin.Y + .4f && Player.Health == health, "真实跳跃离地可以躲过激活的荆棘地板");
		await Shot("elite-thorn-jump"); await Wait(1.1); Player.Movement?.SetPhysicsProcess(false);
		Check(Player.Health < health, "回落接触荆棘地板后才产生伤害");
	}
	private async Task WallClipping()
	{
		await Reset(); var owner = Enemy(Vector3.Left * 4); _wave = MakeWave(Settings("burner"), owner); _wave.Stop(); _wave.World.Clear();
		var world = _wave.World;
		var wall = new StaticBody3D { Name = "EliteBeamWorldWall", CollisionLayer = CombatLayers.World, CollisionMask = 0 };
		wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(.35f, 3, 4) } });
		wall.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(.35f, 3, 4) }, MaterialOverride = EliteTraitVisual.Material(new Color(.32f, .4f, .45f, .8f)) });
		Mode.AddChild(wall); wall.GlobalPosition = _origin + Vector3.Up;
		Player.GlobalPosition = _origin + Vector3.Right * 2; await Wait(.08);
		var beam = world.Line(owner, _origin + Vector3.Left * 4, _origin + Vector3.Right * 4, .6f, 25, .15f, 3);
		Check(beam != null, "创建穿过真实World碰撞墙的测试光束");
		if (beam == null) { wall.QueueFree(); return; }
		float health = Player.Health; await Wait(.4);
		Check(Player.Health == health && world.DamageCount == 0, "墙后的玩家不受直线光束伤害");
		Check(beam.End.X < wall.GlobalPosition.X && beam.RequestedEnd.X > Player.GlobalPosition.X && beam.Visual.Mesh!.GetAabb().End.X < wall.GlobalPosition.X, "预警网格与命中线段共用墙前裁切终点，不残留越墙长束");
		// 真实经过的绕墙折线应保留折线，不从总起点直线裁掉后半段。
		Vector3[] points = { _origin + Vector3.Left * 4, _origin + Vector3.Left + Vector3.Back * 3, _origin + Vector3.Right * 2 + Vector3.Back * 3, _origin + Vector3.Right * 2 };
		var path = world.Polyline(owner, points, .7f, 25, .15f, .2f); await Wait(.4);
		Check(path != null && Player.Health < health && path.Points?.Length == 4, "绕墙实际折线路径仍能在墙后末段命中，不被直线裁切破坏");
		await Shot("elite-wall-clipped"); health = Player.Health; wall.QueueFree(); await Wait(.65);
		Check(beam.End.DistanceTo(beam.RequestedEnd) < .02f && Player.Health < health, "墙移除后恢复期望长度及伤害，不永久遗失原始终点");
		world.Clear();
		var enclosing = new StaticBody3D { Name = "EliteBeamInsideWall", CollisionLayer = CombatLayers.World, CollisionMask = 0 };
		enclosing.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2, 3, 2) } }); Mode.AddChild(enclosing); enclosing.GlobalPosition = owner.GlobalPosition + Vector3.Up;
		await Wait(.08); beam = world.Line(owner, owner.GlobalPosition, Player.GlobalPosition, .6f, 25, .1f, 1); health = Player.Health; await Wait(.25);
		Check(beam != null && beam.Center.DistanceTo(beam.End) < .001f && !beam.Visual.Visible && Player.Health == health, "光束起点在World内部时裁为零长，无残留长束或伤害");
		enclosing.QueueFree();
	}
	private async Task CommonBoundaries()
	{
		await Reset(); var owner = Enemy(Vector3.Right * 4); _wave = MakeWave(Settings("burner"), owner);
		_wave.Stop(); _wave.World.Clear(); var world = _wave.World; float health = Player.Health;
		var pulse = world.Circle(owner, Player.GlobalPosition, 2, 25, .1f, .02f); await Wait(.2);
		Check(pulse != null && Player.Health < health, "短于单帧的脉冲经过激活窗口仍会结算一次");
		world.Clear(); health = Player.Health;
		world.Circle(owner, Player.GlobalPosition, 2, 25, .1f, 2); world.SuspendDamage(true); float time = world.Time;
		await Wait(.3); Check(world.Time == time && Player.Health == health, "奖励暂停可冻结危险计时和伤害");
		world.SuspendDamage(false); GetTree().Paused = true; time = world.Time;
		await Wait(.25); Check(world.Time == time && Player.Health == health, "Esc树暂停不推进危险"); GetTree().Paused = false;
		await Wait(.2); Check(Player.Health < health, "解除暂停后恢复危险时钟"); world.Clear();
		for (int i = 0; i < 15; i++) world.Circle(owner, Player.GlobalPosition + Vector3.Right * 15, 1, 1, 1, 3);
		Check(world.ActiveCount == world.PerSourceCapacity && world.RejectedCount > 0, "每来源危险硬上限真实拒绝超量创建");
		world.Clear(); health = Player.Health; await Wait(.6); Check(world.ActiveCount == 0 && Player.Health == health, "清场撤销所有残留危险与后续伤害");
	}
}
