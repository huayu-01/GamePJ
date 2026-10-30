using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.UI;
using ActRoguelike.Visual;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>动作实例归属、真实模型与只读表现、单次奖励会话及迟到按钮回调。</summary>
public partial class ContentDecouplingProbe : Node3D
{
	private int _pass, _fail;
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[Decoupling] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Wait(double seconds = .1) => await ToSignal(GetTree().CreateTimer(seconds, true), SceneTreeTimer.SignalName.Timeout);
	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		try
		{
#if DEBUG
			MetaProgress.UseInMemoryStoreForSelfTest(); RuneCarry.UseInMemoryStoreForSelfTest();
#endif
			await Wait(); Actions(); await Models(); Sessions(); await LateCards(); NodeRewards(); await CustomInteractions();
		}
		catch (Exception error) { Check(false, error.ToString()); }
		finally { UiRouter.Instance?.ResetForSceneChange(); }
		GD.Print($"[Decoupling] SUMMARY pass={_pass} fail={_fail}"); GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private void Actions()
	{
		var owner = new Unit(); owner.AddChild(new CharacterMovement { Name = "Movement" }); AddChild(owner); owner.SetPhysicsProcess(false); owner.Movement!.SetPhysicsProcess(false);
		var guard = RuntimeResources.Load<GuardEnemyAction>("res://Config/EnemyActions/guard.tres"); int a = 0, b = 0;
		using var first = guard.Create(new(owner, () => null, _ => a++));
		using var second = guard.Create(new(owner, () => null, _ => b++)); first.Tick(.1f); second.Tick(.1f); second.Tick(.1f);
		Check(a == 1 && b == 2 && !ReferenceEquals(first, second), "共享动作定义创建独立执行实例");
		var hover = RuntimeResources.Load<HoverEnemyAction>("res://Config/EnemyActions/hover.tres");
		var active = hover.Create(new(owner, () => null, _ => { })); active.Begin();
		Check(owner.Movement.HasVelocityOverride, "滞空执行器取得移动所有权"); active.Dispose(); active.Dispose();
		Check(!owner.Movement.HasVelocityOverride && active.IsDisposed, "重复释放滞空幂等，归还移动所有权");
		object other = new(); owner.Movement.BeginVelocityOverride(other, Vector3.Right); active = hover.Create(new(owner, () => null, _ => { })); active.Begin(); active.Dispose();
		Check(owner.Movement.HasVelocityOverride, "执行器不会释放其他动作持有的移动覆盖"); owner.Movement.EndVelocityOverride(other);
		var plan = RuntimeResources.Load<EnemyActionPlan>("res://Config/EnemyPlans/spear_guard.tres");
		Check(plan.Opening[0].Action is GuardEnemyAction, "生产持盾计划已引用动作定义");
		Check(RuntimeResources.Load<EnemyActionPlan>("res://Config/EnemyPlans/hover_diver.tres").Opening.Any(s => s.Action is HoverEnemyAction)
			&& RuntimeResources.Load<EnemyActionPlan>("res://Config/EnemyPlans/burrow_hunter.tres").Closing.Any(s => s.Action is WaitEnemyAction), "生产滞空与等待计划已迁移");
		owner.QueueFree();
	}
	private async Task Models()
	{
		foreach (CharacterConfig character in ConfigIndex.Settings.Characters)
		{
			var profile = character.VisualProfile!;
			Check(profile != null && !profile.Validate().Any(), $"{character.Id} 模型、骨架、挂点完整");
			Node model = profile!.ModelScene!.Instantiate();
			GD.Print($"[Decoupling] PROFILE {character.Id} skeleton={model.GetPathTo(profile.ResolveSkeleton(model)!)} player={model.GetPathTo(profile.ResolveAnimationPlayer(model)!)}"); model.Free();
			var broken = (UnitVisualProfile)profile.Duplicate(); broken.SkeletonPath = "MissingSkeleton";
			Check(broken.Validate().Any(s => s.Contains("SkeletonPath")), "显式错误骨架路径不能回退成成功"); broken.Dispose();
			var copy = (CharacterConfig)character.Duplicate(); string path = $"res://Logs/validation/{character.Id}_profile_roundtrip.tres";
			Error saved = ResourceSaver.Save(copy, path); string text = FileAccess.GetFileAsString(path);
			var restored = ResourceLoader.Load<CharacterConfig>(path, cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
			Check(saved == Error.Ok && restored.VisualProfile != null && restored.ModelPath == character.ModelPath
				&& !System.Text.RegularExpressions.Regex.IsMatch(text, @"(?m)^(ModelPath|ModelTransform|WeaponGripScene|AnimLibraryDir) ="), "表现档案保存往返正确，不产生第二份旧字段参数"); copy.Dispose();
		}
		var unit = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>();
		var source = new ProbeWeaponPresentation(); unit.AddChild(source); AddChild(unit); unit.SetPhysicsProcess(false); unit.Movement!.SetPhysicsProcess(false);
		var rig = unit.GetNode<Node3D>("CameraRig"); rig.SetPhysicsProcess(false); rig.SetProcessInput(false);
		var loadout = new ActRoguelike.Player.PlayerLoadout(); loadout.Apply(unit, this, PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Spear); await Wait();
		var display = unit.GetNode<CharacterDisplay>("Appearance"); int count = display.ModelCount; Vector3 bodyScale = unit.Scale;
		Node3D weapon = display.EquippedWeapon!; Transform3D rest = weapon.Transform;
		source.State = new(true, false, Vector3.Zero); await Wait(); Check(!weapon.Visible, "新只读提供者能隐藏手持模型，无需显示组件识别能力类型");
		source.State = new(false, true, new Vector3(0, -1, -1).Normalized()); await Wait();
		Check(display.DivePoseActive && weapon.TopLevel && weapon.GlobalBasis.Y.Normalized().Dot(source.State.WorldDirection) > .999f, "只读方向驱动真实模型姿态，不修改攻击和移动");
		source.State = default; await Wait(); Check(weapon.Visible && !weapon.TopLevel && weapon.Transform.IsEqualApprox(rest), "表现状态结束恢复握持变换");
		source.State = new(false, true, Vector3.Zero); await Wait(); Check(!display.DivePoseActive && weapon.Transform.IsEqualApprox(rest), "无效表现方向不污染模型变换"); source.State = default;
		for (int i = 0; i < 3; i++) { loadout.Apply(unit, this, PlayerCharacterKind.Hunter, WeaponPresetKind.Bow); loadout.Apply(unit, this, PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Spear); }
		Check(display.ModelCount == count && unit.Scale.IsEqualApprox(bodyScale), "重复换模复用缓存，表现缩放不改变角色身体");
		unit.QueueFree(); await Wait();
	}
	private void Sessions()
	{
		var row = RewardCatalog.Find("upg_power")!; var input = new List<RewardDefinition> { row }; var flow = new RewardOfferFlow(); int applied = 0, completed = 0;
		Check(flow.TryBegin(input, () => completed++), "开始独立奖励会话"); var offer = flow.Current!; input.Clear();
		Check(offer.Options.Count == 1 && !flow.TryBegin(new[] { row }), "候选快照固定，待完成会话不能被新选择覆盖");
		Check(!offer.TryApply(-1, _ => applied++) && !offer.Complete(), "无效下标不应用也不触发完成");
		bool reentrant = true;
		Check(offer.TryApply(0, _ => { applied++; reentrant = offer.TryApply(0, _ => applied++); }), "一次合法选择成功应用");
		Check(!reentrant && !offer.TryApply(0, _ => applied++) && applied == 1, "同步重入和重复信号都不会重复应用");
		Check(offer.Complete() && !offer.Complete() && completed == 1, "完成回调只执行一次");
		flow.TryBegin(new[] { row }, () => completed++); offer = flow.Current!;
		bool threw = false; try { offer.TryApply(0, _ => throw new InvalidOperationException("expected")); } catch (InvalidOperationException) { threw = true; }
		Check(threw && offer.TryApply(0, _ => applied++), "应用失败保留原候选，可重试且未误完成"); flow.Cancel();
		Check(!offer.Complete() && completed == 1, "取消可阻止已应用但尚未交还流程的完成回调");
		flow.TryBegin(new[] { row }); offer = flow.Current!; Check(!offer.TryApply(0, _ => flow.Cancel()) && !offer.Complete(), "应用期间取消后不再推进节点");
		flow.TryBegin(new[] { row }, () => Check(flow.TryBegin(new[] { row }), "完成回调可安全打开下一次选择")); offer = flow.Current!; offer.TryApply(0, _ => { }); offer.Complete(); flow.Cancel();
	}
	private async Task LateCards()
	{
		var screen = RuntimeResources.Load<PackedScene>("res://Scenes/UI/RewardScreen.tscn").Instantiate<RewardScreen>(); AddChild(screen);
		var row = RewardCatalog.Find("upg_power")!; screen.Open(new[] { row }, "旧会话");
		var cards = screen.GetNode<Control>("Root/Panel/VBox/ChoicesScroll/Cards");
		var old = cards.GetChildren().OfType<Button>().First(b => b.Name != "CardTemplate");
		Callable[] delayed = old.GetSignalConnectionList(Button.SignalName.Pressed).Select(c => c["callable"].AsCallable()).ToArray();
		int selected = 0; screen.RewardChosen += _ => selected++;
		screen.Open(new[] { row }, "新会话");
		ulong deadline = Time.GetTicksMsec() + 2000;
		while (screen.InputBlocked && Time.GetTicksMsec() < deadline) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		Check(!screen.InputBlocked, "按真实时钟等待界面保护期结束");
		foreach (Callable callback in delayed) callback.Call();
		Check(delayed.Length > 0 && selected == 0, "旧按钮迟到回调不能领取新会话奖励");
		cards.GetChildren().OfType<Button>().First(b => b.Name != "CardTemplate").EmitSignal(Button.SignalName.Pressed);
		Check(selected == 1, "新会话按钮仍正常分发选择"); screen.Close(); screen.QueueFree(); await Wait();
	}
}

internal partial class ProbeWeaponPresentation : Node, IWeaponPresentationSource
{
	public WeaponPresentationState State { get; set; }
	public bool TryReadWeaponPresentation(out WeaponPresentationState state) { state = State; return state.Hidden || state.AlignToWorldDirection; }
}
