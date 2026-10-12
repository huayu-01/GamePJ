using System.Collections.Generic;
using System.Text;
using ActRoguelike.Boss;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Encounter;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 首领状况预览面板（用户需求）：「在交互时可以有面板预览当前首领状况」—— 本关攒了多少 Pressure、
/// 首领学到了哪些**技能**、抽到了哪些**词条**、首领现在是什么数值，集中在一张只读面板里。
/// 后期要做词典图鉴，届时与它合并（用户原话）。
///
/// 走既有 UI 框架，不绕过任何一层：
/// <list type="bullet">
/// <item>继承 <see cref="OverlayScreen"/> —— 分层 / 防误触 / 开合动画 / Esc 关闭都由基类管。</item>
/// <item>由 <see cref="UiService.OpenBossPreview"/> 打开（业务代码不认识本类的节点路径）。</item>
/// <item>实例挂在场景里（`Main.tscn` / `Base.tscn`），与其他界面同级；快捷键在 <see cref="_Process"/> 里做边沿检测。</item>
/// </list>
///
/// <b>分层 = <see cref="OverlayScreen.UiLayer.Panel"/></b>：这是"停下来看一眼资料"的信息面板，
/// 与帮助界面（H）同类 —— 打开即暂停并放开鼠标，读的时候不该被打死。
/// `Hud` 层是留给**常驻**元素的（常驻即不暂停、不拦输入），`Modal` 层留给必须做出选择的界面
/// （三选一 / 商店）。想改成"边打边看"，只把场景里的 <c>ScreenLayer</c> 改成 Hud 即可，代码不用动。
/// </summary>
public partial class BossPreviewScreen : OverlayScreen
{
	/// <summary>默认快捷键（HUD 的交互提示也读它，避免同一个键在两处写死）。</summary>
	public const Key DefaultToggleKey = Key.B;

	[Export] public Key ToggleKey { get; set; } = DefaultToggleKey;

	protected override Control Root => _root;

	private Control _root = null!;
	private Label _body = null!;
	private bool _keyHeld;

	public override void _Ready()
	{
		_root = GetNode<Control>("Root");
		_body = GetNode<Label>("Root/Panel/VBox/Body");
		InitializeOverlay();
	}

	public override void _Process(double delta)
	{
		// 边沿检测（与 HelpScreen 同一套写法）：按住不放只切一次。
		bool held = Input.IsPhysicalKeyPressed(ToggleKey);
		if (held && !_keyHeld)
		{
			Toggle();
		}

		_keyHeld = held;

		// 面板开着时每帧刷新（此时游戏是暂停的，20Hz 之类的节流没有意义）。
		if (IsOpen)
		{
			_body.Text = Describe(StageController.Instance, StageController.Instance?.CurrentNode);
		}
	}

	private void Toggle()
	{
		if (IsOpen)
		{
			Close();
		}
		else
		{
			Open();
		}
	}

	public void Open()
	{
		if (IsOpen)
		{
			return;
		}

		_body.Text = Describe(StageController.Instance, StageController.Instance?.CurrentNode);
		SetKeyHints(KeyHint.Of(ToggleKey, "关闭"), (Key.Escape, "关闭"));
		BeginOpen();
	}

	public override void Close()
	{
		// 保护期内不响应关闭，避免「开→瞬间关」的误触（与 HelpScreen 一致）。
		if (IsOpen && !InputBlocked)
		{
			BeginClose();
		}
	}

	// ───────────────────────── 正文：纯读状态 → 字符串 ─────────────────────────

	/// <summary>
	/// 面板正文。做成**静态 + 只读入参**，所以自测不用开界面就能断言内容（`BossPreviewScreen.Describe(stage, enc)`）。
	/// 数据来源都是公开只读属性，面板自己不持有任何状态、也不改任何状态。
	/// </summary>
	public static string Describe(StageController? stage, EncounterController? encounter)
	{
		var text = new StringBuilder();
		if (stage == null)
		{
			return "（当前不在关卡里：首领状况要进入关卡后才有）";
		}

		StageDefinition? stageRow = stage.Definition;
		text.Append($"关卡：{stageRow?.DisplayName ?? $"第 {stage.StageNumber} 关"}   已用时 {stage.ElapsedSeconds:0}s\n\n");

		AppendQuota(text, stage);
		AppendPressure(text, stage, stageRow);
		AppendPerks(text, stage, encounter);
		AppendBoss(text, encounter);
		text.Append($"\n{OS.GetKeycodeString(DefaultToggleKey)} 开关 · Esc 关闭（后期与词典图鉴合并）");
		return text.ToString();
	}

	/// <summary>§8.1/§8.2：免费挑战额度与闸门状态（闸门是"这次结算加不加 Pressure"的开关）。</summary>
	private static void AppendQuota(StringBuilder text, StageController stage)
	{
		text.Append("【免费挑战额度 · 关卡核心文档 §8】\n");
		text.Append($"  本关额度 {stage.FreeChallengeQuota}（基础 {Balance.Settings.FreeChallengeBaseQuota} + 每关 1，上限 {Balance.Settings.FreeChallengeMaxQuota}）");
		text.Append($"   已挑战 {stage.ChallengesUsed} 次   剩余 {stage.FreeQuotaLeft}\n");
		text.Append(stage.CurrentChallengeFree
			? "  闸门：下一次结算算「免费挑战」→ 完成后不增加 Pressure（§8.2）\n"
			: "  闸门：额度已用完 → 挑战完成后会累积 Pressure（§8.2 第 5 条）\n");
		text.Append('\n');
	}

	/// <summary>§7.2–§7.5：攒了多少、跨了几次阈值、池子里有什么。</summary>
	private static void AppendPressure(StringBuilder text, StageController stage, StageDefinition? stageRow)
	{
		float growth = stageRow is { PressureGrowthMultiplier: > 0f } row ? row.PressureGrowthMultiplier : 1f;
		text.Append("【Pressure · 关卡核心文档 §7】\n");
		text.Append($"  阈值 {stage.PressureThresholdNow:0}   本关跨阈值 {stage.PressureRolls} 次（每跨一次立即抽 1 条数值强化）   余数 {stage.PressureBank:0.#}（转换后清零、不结转）\n");
		text.Append($"  组合顺序：基础点数（节点表 → 关卡行 → Balance 全局默认值）× 本关倍率 {growth:0.##} × 类型比例（进化 / 异化）\n");
		text.Append($"  转换池候选：{DescribePool()}\n");
		text.Append('\n');
	}

	/// <summary>数值型转换池的候选与权重（§7.5「候选内容和权重由配置定义」）。</summary>
	private static string DescribePool()
	{
		IReadOnlyList<BossPerk> pool = BossPerkCatalog.PressureOnly;
		if (pool.Count == 0)
		{
			return "0 条（空池：抽取会返回 null，不静默变强）";
		}

		var parts = new List<string>(pool.Count);
		foreach (BossPerk perk in pool)
		{
			string tags = perk.Tags.Length == 0 ? "无标签" : string.Join(",", perk.Tags);
			parts.Add($"{perk.Name}({perk.Id}) 权重 {perk.Weight:0.##} [{tags}]");
		}

		return string.Join(" · ", parts);
	}

	/// <summary>
	/// 两套来源分列显示（§7.1：行为型 Boss Build / 数值型 Pressure 必须能区分来源），
	/// 而且**叫法分开**（用户拍板）：行为型 = **技能**（它本来就是技能表里的行），数值型 = **词条**。
	/// </summary>
	private static void AppendPerks(StringBuilder text, StageController stage, EncounterController? encounter)
	{
		IReadOnlyList<BossPerk> behavior = encounter?.BossPerks ?? System.Array.Empty<BossPerk>();
		IReadOnlyList<BossPerk> numbers = stage.PressurePerks;

		text.Append("【技能】（行为型 · Boss Build：首领降临时抽，学到就是它的了）\n");
		if (behavior.Count == 0)
		{
			int expected = stage.Definition?.BossPerkCount ?? 0;
			text.Append($"  {BossPerkText.BehaviorMark} 首领降临时抽 {expected} 条（现在还没有）\n");
		}
		else
		{
			text.Append($"  {BossPerkText.BehaviorMark} 技能 {behavior.Count} 条（首领降临时抽）\n");
			text.Append(BossPerkText.Lines(behavior, BossPerkText.BehaviorMark));
		}

		string applied = stage.PressurePerksApplied ? "已套用到首领身上" : "首领降临时套用";
		text.Append("【词条】（数值型 · Pressure 阈值转换：一个参数一栏）\n");
		text.Append($"  {BossPerkText.PressureMark} 词条 {numbers.Count} 条（{applied}）\n");
		if (numbers.Count == 0)
		{
			text.Append("      （还没有跨过阈值）\n");
		}
		else
		{
			text.Append(BossPerkText.Lines(numbers, BossPerkText.PressureMark));
		}

		text.Append('\n');
	}

	/// <summary>首领当前数值（没降临就说明为什么是空的）。</summary>
	private static void AppendBoss(StringBuilder text, EncounterController? encounter)
	{
		text.Append("【首领】\n");
		Unit? boss = encounter?.Boss;
		if (boss == null || !GodotObject.IsInstanceValid(boss) || boss.IsDead)
		{
			text.Append("  尚未降临（打 Boss 门后出场；上面抽到的技能与词条都在它出生时套用）\n");
			return;
		}

		var phases = boss.GetNodeOrNull<BossPhaseController>("Phases");
		var pressure = boss.GetNodeOrNull<BossPressure>("Pressure");
		text.Append($"  生命 {boss.Health:0} / {boss.MaxHealth:0}   护甲 {boss.Armor:0}   阶段 {(phases?.Phase ?? 0) + 1}   压力等级 {pressure?.Level ?? 0}\n");
		text.Append($"  乘区：伤害×{boss.Stats.Get("DamageMultiplier"):0.###}  攻速×{boss.Stats.Get("AttackSpeedMultiplier"):0.###}  移速×{boss.Stats.Get("MoveSpeedMultiplier"):0.###}\n");
	}
}
