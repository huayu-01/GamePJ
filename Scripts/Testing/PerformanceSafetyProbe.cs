using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>验证优化边界：广播重入与订阅语义、空闲表现唤醒、护甲和状态生命周期。</summary>
public partial class PerformanceSafetyProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "PerformanceSafety";
	protected override async Task RunChecks()
	{
		CheckEvents();
		var enemy = MakeEnemy(Player.GlobalPosition + Vector3.Right * 8);
		await Wait(.15);
		var reactions = enemy.Reactions!;
		Check(!reactions.IsPhysicsProcessing(), "无受击／格挡硬直时停止空闲物理回调");
		reactions.BlockRecoil(.2f);
		Check(reactions.IsStaggered && reactions.IsPhysicsProcessing(), "成功格挡立即唤醒格挡者自身硬直");
		await Wait(.3);
		Check(!reactions.IsStaggered && !reactions.IsPhysicsProcessing(), "硬直正常到期并再次休眠");
		reactions.BlockRecoil(.4f); reactions.Reset();
		Check(!reactions.IsStaggered && !reactions.IsPhysicsProcessing(), "主动重置同步取消硬直和计时");
		var presentation = enemy.GetNode<UnitPresentation>("Presentation");
		Check(!presentation.IsProcessing(), "没有闪色或预警时身体表现休眠");
		presentation.FlashAttack();
		Check(presentation.IsProcessing(), "攻击预警立即唤醒身体表现");
		await Wait(.2);
		Check(!presentation.IsProcessing(), "攻击预警结束恢复休眠");
		enemy.GrantArmor(20);
		var aura = enemy.GetNode<MeshInstance3D>("ArmorAura");
		Check(aura.Visible && !presentation.IsProcessing(), "新增护甲通过事件显示，不需要常驻轮询");
		enemy.ReceiveHit(new HitInstance { Target = enemy, Attacker = Player, Context = EventContext.Create(Player, enemy), Damage = 100, Direction = Vector3.Forward, Position = enemy.GlobalPosition });
		Check(enemy.Armor <= 0 && !aura.Visible, "破甲立即隐藏护甲光效");
		Check(!reactions.IsStaggered, "这次命中前有护甲，仍不会被打入受击硬直");
		presentation.ShowImpact(ImpactKind.Flesh, .05f);
		Check(presentation.IsProcessing(), "受击闪色可再次唤醒表现");
		await Wait(.2);
		Check(!presentation.IsProcessing(), "受击闪色结束清理并休眠");
		var fx = enemy.GetNode<StatusEffectFx>("StatusEffects");
		Check(!fx.IsPhysicsProcessing(), "无状态和规则表现时不再轮询");
		var status = new StatusEffect { Id = "PerfSafetyStatus", Duration = .5f, MaxStacks = 3 };
		enemy.ApplyStatus(status); enemy.ApplyStatus(status);
		await Wait(.08);
		Check(fx.IsPhysicsProcessing() && fx.DisplayedStacks(status.Id) == 2, "状态添加与叠层立即唤醒并显示正确层数");
		enemy.RemoveStatus(status.Id); await Wait(.08);
		Check(fx.VisualCount == 0 && !fx.IsPhysicsProcessing(), "主动移除最后状态后清理并休眠");
		enemy.ApplyStatus(new StatusEffect { Id = "PerfSafetyExpire", Duration = .08f }); await Wait(.25);
		Check(fx.VisualCount == 0 && !fx.IsPhysicsProcessing(), "自然到期同样通知视觉收尾");
		var source = new PerformanceVisualSource(); enemy.AddChild(source); await Wait(.12);
		Check(fx.IsPhysicsProcessing() && fx.DisplayedStacks("PerfSafetySource") == 1, "动态附加只读来源可唤醒表现");
		source.Active = false; await Wait(.15);
		Check(fx.VisualCount == 0 && fx.IsPhysicsProcessing(), "独立来源无变化事件时仍保留原轮询");
		source.QueueFree(); await Wait(.15);
		Check(!fx.IsPhysicsProcessing(), "移除最后规则来源后停止轮询");
	}
	private void CheckEvents()
	{
		const string tag = "PerfSafetyBus";
		var trace = new List<int>(); bool changed = false;
		Action<GameplayEvent> second = f => { if (f.Tag == tag) trace.Add(2); };
		Action<GameplayEvent> late = f => { if (f.Tag == tag) trace.Add(3); };
		Action<GameplayEvent> first = f =>
		{
			if (f.Tag != tag) return; trace.Add(1);
			if (!changed) { changed = true; GameplayEventBus.Raised -= second; GameplayEventBus.Raised += late; }
		};
		try
		{
			GameplayEventBus.Raised += first; GameplayEventBus.Raised += second;
			GameplayEventBus.Raise(new GameplayEvent { Tag = tag });
			Check(string.Join(',', trace) == "1,2", "广播中增删订阅不改变本次快照");
			trace.Clear(); GameplayEventBus.Raise(new GameplayEvent { Tag = tag });
			Check(string.Join(',', trace) == "1,3", "下次广播采用新订阅顺序");
		}
		finally { GameplayEventBus.Raised -= first; GameplayEventBus.Raised -= second; GameplayEventBus.Raised -= late; }
		int calls = 0;
		Action<GameplayEvent> count = f => { if (f.Tag == tag) calls++; };
		GameplayEventBus.Raised += count; GameplayEventBus.Raised += count;
		GameplayEventBus.Raised -= count; GameplayEventBus.Raise(new GameplayEvent { Tag = tag });
		Check(calls == 1, "重复订阅移除一次只取消最后一次");
		GameplayEventBus.Raised -= count;
		trace.Clear(); Action<GameplayEvent> multicast = second + late;
		GameplayEventBus.Raised += multicast; GameplayEventBus.Raised -= multicast;
		GameplayEventBus.Raise(new GameplayEvent { Tag = tag });
		Check(trace.Count == 0, "多播委托整体移除保持标准语义");
		GameplayEventBus.Raised += count;
		try
		{
			for (int i = 0; i < 20; i++) GameplayEventBus.Raise(new GameplayEvent { Tag = tag });
			long before = GC.GetAllocatedBytesForCurrentThread();
			for (int i = 0; i < 10000; i++) GameplayEventBus.Raise(new GameplayEvent { Tag = tag });
			long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
			Check(bytes < 1024, $"10000次广播不再重复分配调用列表，实测{bytes}字节（含订阅者）");
		}
		finally { GameplayEventBus.Raised -= count; }
	}
}

public partial class PerformanceVisualSource : Node, IStatusVisualSource
{
	public bool Active = true;
	private readonly EffectConfig _row = new() { Id = "PerfSafetySource", VisualStyle = StatusVisualStyle.Orbit };
	public IEnumerable<EffectConfig> ActiveStatusVisuals() { if (Active) yield return _row; }
}
