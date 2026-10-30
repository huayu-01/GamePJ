using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Interaction;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Testing;

public partial class ContentDecouplingProbe
{
	private void NodeRewards()
	{
		Check(ConfigIndex.Settings.Nodes.All(row => row.RewardBehavior != null), "全部生产节点明确引用收益策略");
		var player = new Unit { MaxHealth = 100 }; AddChild(player); player.SetPhysicsProcess(false);
		var row = new NodeConfig { Kind = RewardNodeKind.Shop, ShopHealCost = 3, ShopRewardCost = 7, ShopWeaponCost = 11 };
		int balance = 100, completed = 0, offered = 0; Action<int>? choice = null; Action? chosen = null; bool accept = true, active = true, throws = false;
		NodeRewardContext Context() => new()
		{
			Player = player, Config = row, IsActive = () => active, OnCompleted = () => completed++,
			Balance = () => balance, Spend = cost => { if (balance < cost) return false; balance -= cost; return true; }, Refund = amount => balance += amount,
			Choice = (_, _, callback) => { choice = callback; return true; }, Offer = (_, callback) => { offered++; chosen = callback; if (throws) throw new InvalidOperationException("预期界面异常"); return accept; }
		};
		var shop = new ShopNodeReward(); var context = Context(); Check(shop.Begin(context), "独立商店策略使用界面服务打开选择"); choice!(1); choice(1);
		Check(balance == 93 && offered == 1 && completed == 0 && context.IsPending, "重复购买回调只扣一次，奖励完成前不提前触发挑战");
		chosen!(); chosen(); Check(completed == 1 && !context.IsPending, "奖励领取后节点只完成一次");
		context = Context(); shop.Begin(context); choice!(2); context.Cancel(); chosen!();
		Check(balance == 93 && completed == 1, "取消待领取购买立即退还其花费，迟到选择不能发奖或完成");
		accept = false; context = Context(); shop.Begin(context); choice!(1);
		Check(balance == 93 && completed == 2 && !context.IsPending, "奖励池无候选或窗口拒绝时退款，并按商店规则完成节点挑战");
		accept = true; throws = true; context = Context(); shop.Begin(context); bool failed = false; try { choice!(2); } catch (InvalidOperationException) { failed = true; }
		Check(failed && balance == 93 && !context.IsPending && completed == 2, "收益窗口异常时退还本次花费并取消会话"); throws = false;
		balance = 0; context = Context(); shop.Begin(context); choice!(1); Check(balance == 0 && completed == 3, "余额不足不扣款，商店不购买仍完成节点");
		context = Context(); context.Cancel(); Check(!shop.Begin(context), "已取消会话不能重新打开商店");
		context = Context(); shop.Begin(context); active = false; choice!(0); Check(balance == 0 && completed == 3, "死亡或退出后迟到购买无副作用"); context.Cancel(); active = true;
		var resource = new ResourceNodeReward { Shards = 8 }; context = Context(); resource.Begin(context); resource.Begin(context);
		Check(balance == 8 && completed == 4, "资源节点直接按定义发放，重复调用同会话不重复收益");
		Check(NodePlacement.WavesFor(new NodeConfig { Layer = 0, WaveCount = 7, ValueTags = Array.Empty<string>() }, RewardNodeKind.Enhancement) == 7, "自定义节点直接消费自己的波数，不被相同旧Kind覆盖");
		NodeRewardContext? reentrant = null; balance = 30; int beforeOffers = offered;
		reentrant = new NodeRewardContext { Player = player, Config = row, IsActive = () => true, OnCompleted = () => completed++, Balance = () => balance,
			Choice = (_, _, callback) => { choice = callback; return true; }, Offer = (_, _) => { offered++; return true; },
			Spend = cost => { balance -= cost; reentrant!.Cancel(); return true; }, Refund = amount => balance += amount };
		shop.Begin(reentrant); choice!(1);
		Check(balance == 30 && offered == beforeOffers && !reentrant.IsPending, "扣款服务同步重入取消时立即退款，不继续发奖");
		Check(new NodeConfig { ShopHealCost = -1 }.Validate().Any(), "节点定义在制作期拒绝负数价目");
		player.QueueFree();
	}
	private async Task CustomInteractions()
	{
		foreach (InteractionKind kind in Enum.GetValues<InteractionKind>().Where(k => k != InteractionKind.None))
			Check(InteractionDefinitionCatalog.For(kind) is { } definition && !definition.Validate().Any(), $"内置交互{kind}的后端与外观定义可校验");
		var shape = new Node3D(); var mesh = new MeshInstance3D { Name = "Mesh", Mesh = new BoxMesh() }; shape.AddChild(mesh); mesh.Owner = shape;
		var model = new PackedScene(); Check(model.Pack(shape) == Error.Ok, "新交互外观可独立打包"); shape.Free();
		var config = new InteractionDefinition { BackendScene = RuntimeResources.Load<PackedScene>("res://Scenes/Interactables/Backends/Signal.tscn"), ModelScene = model };
		var host = new Node3D(); var mount = new InteractionComponent { Kind = InteractionKind.None, Definition = config }; host.AddChild(mount); AddChild(host); await Wait();
		Check(mount.Backend is SignalInteraction && mount.ModelRoot?.GetNodeOrNull<MeshInstance3D>("Mesh") != null && mount._GetConfigurationWarnings().Length == 0, "无需新增Kind即可用定义安装后端和独立模型");
		Check(mount.Backend!.VisualMesh?.Mesh is BoxMesh, "交互反馈读取自定义模型实际主网格");
		host.QueueFree(); await Wait(); Check(!Interactable.All.Any(i => IsInstanceValid(i) && i.PlacementTarget == host), "自定义交互退树后清理公共注册");
		var wrong = new PackedScene(); var node = new Node(); wrong.Pack(node); node.Free();
		Check(new InteractionDefinition { BackendScene = wrong }.Validate().Any() && new InteractionDefinition { BackendScene = config.BackendScene, ModelScene = wrong }.Validate().Any(), "制作期拒绝错误后端和错误模型根类型");
	}
}
