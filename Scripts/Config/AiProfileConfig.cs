using ActRoguelike.AI;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// **AI 行为表**的一行 —— 一个 `.tres` 文件就是一行（`Config/Tables/AiProfiles/ai_*.tres`）。
/// 由单位表的 <see cref="UnitConfig.AiProfile"/> 以**资源引用**指过来（不是字符串 id）。
///
/// <para><b>为什么是表</b>：AI 参数原来**每个敌人场景各配一份**（`Brain` 子树 + 各 Behavior 的 <c>[Export]</c>），
/// 10 个敌种 × 8–15 个参数 —— 想让"所有敌人更激进"要开 10 个场景改 30 处。
/// 收成"一行一个 AI 档案"之后，在 <c>Edit Resources as Table</c> 里按一列排序/批量改即可。</para>
///
/// <para><b>列的形状（为什么是扁平列而不是"行为数组"）</b>：
/// 每一列 = 「节点名 + 参数名」（<c>ChaseStopDistance</c> = <c>Brain/Chase</c> 的 <c>StopDistance</c>）。
/// 不用 <c>Array[行为]</c> 的理由与阶段 4 选"一个选项占一栏"完全相同：
/// **一列 = 一个标量属性**才拿得到编辑器下拉/排序/批量改；数组列在表插件里既不能排序也不稳定
/// （阶段 4 实测过 <c>Array[Object]</c> 那条报错）。节点名写在 <c>[ExportGroup]</c> 标题里。</para>
///
/// <para><b>两条语义约定</b>：
/// <list type="number">
/// <item><b>0 / 负数 = 不覆盖</b>：数值列留 0（<c>RetreatHealthThreshold</c> 留 -1）就**保留场景里的值** ——
/// 与 <see cref="UnitConfig"/>「0 = 不覆盖」、<see cref="AbilityConfig.Cooldown"/>「&lt;= 0 = 不覆盖」同一条纪律。
/// 唯一的例外是 <c>KeepDistanceStrafe</c>（bool）：槽位生效时**照写**，与 <see cref="AbilityConfig.Enabled"/> 一致。</item>
/// <item><b><c>Priority &lt;= 0 = 整个槽位不生效</b>：该节点在本档案里"不管"，连它下面的参数一起跳过。
/// 所以每个要管的槽位都要把它的 <c>XxxPriority</c> 填上（现有档案全部照抄场景里的值）。</b></item>
/// </list></para>
///
/// <para><b>边界（刻意不做的两件事）</b>：
/// ① **不增删行为节点** —— 档案只调数值，节点树（谁有 Chase / 谁有 Retreat）仍在场景里。
///    这与"<c>Main.tscn</c> 的 <c>SpawnXxx</c> 提供预制体与位置、敌人表覆盖成本与权重"是同一条分工。</para>
/// <para>② **不放"能力节点名"（<c>AbilityNodeName</c>）列** —— 它是身份不是数值，场景里已经写死；
/// 抄一份到表里就是第二个真源（项目文档反复点名的坑）。要改它、以及要调
/// <c>MinRange</c> / <c>MaxRange</c> 这类没有独立列的参数，走基类的
/// <see cref="ConfigRow.Params"/> 逃生口，键写成 <c>节点名.属性名</c>（例：<c>Chase.MaxRange = 8</c>）。</para>
/// </summary>
[Tool]
[GlobalClass]
public partial class AiProfileConfig : ConfigRow
{
	[ExportGroup("决策 · Brain（0 = 不覆盖）")]
	/// <summary>索敌半径（写进 <c>EnemyBrain.SearchRange</c>）。目标刷新间隔。</summary>
	[Export] public float SearchRange { get; set; }

	/// <summary>重新选目标的间隔（秒），写进 <c>EnemyBrain.TargetRefreshInterval</c>。</summary>
	[Export] public float TargetRefreshInterval { get; set; }

	[ExportGroup("行为 · 近战攻击（节点名 Attack；0 = 不覆盖）")]
	[Export] public int AttackPriority { get; set; }

	[ExportGroup("行为 · 追击（节点名 Chase）")]
	[Export] public int ChasePriority { get; set; }

	/// <summary>追到多近就停下（交给攻击行为）。</summary>
	[Export] public float ChaseStopDistance { get; set; }

	/// <summary>追击速度倍率（乘单位的 MoveSpeed）。</summary>
	[Export] public float ChaseSpeedScale { get; set; }

	[ExportGroup("行为 · 远程攻击（节点名 Shoot；0 = 不覆盖）")]
	[Export] public int ShootPriority { get; set; }

	[ExportGroup("行为 · 保持距离（节点名 KeepDistance）")]
	[Export] public int KeepDistancePriority { get; set; }

	/// <summary>舒适圈半径。</summary>
	[Export] public float KeepDistancePreferredDistance { get; set; }

	/// <summary>舒适圈的容差（在这个宽度里站定）。</summary>
	[Export] public float KeepDistanceTolerance { get; set; }

	[Export] public float KeepDistanceSpeedScale { get; set; }

	/// <summary>是否绕圈（bool 列在槽位生效时照写，见类注释）。</summary>
	[Export] public bool KeepDistanceStrafe { get; set; }

	[Export] public float KeepDistanceStrafeScale { get; set; }

	[ExportGroup("行为 · 撤退（节点名 Retreat）")]
	[Export] public int RetreatPriority { get; set; }

	/// <summary>血量低于它就开始撤退。<b>&lt; 0 = 不覆盖</b>；填 0 = 永不撤退（所以这里不能用"0 = 不覆盖"）。</summary>
	[Export(PropertyHint.Range, "-1,1,0.05")] public float RetreatHealthThreshold { get; set; } = -1f;

	[Export] public float RetreatSpeedScale { get; set; }

	/// <summary>撤到多远之外就停下。</summary>
	[Export] public float RetreatKeepDistance { get; set; }

	[ExportGroup("行为 · 通用技能槽（节点名 Explode / Leap / Scream / Charge / Summon / Buff；0 = 不覆盖）")]
	/// <summary>自爆兵的 Explode、跳兵的 Leap、尖啸者的 Scream、冲锋兵的 Charge、首领的 Summon / Buff。</summary>
	[Export] public int ExplodePriority { get; set; }

	[Export] public int LeapPriority { get; set; }

	[Export] public int ScreamPriority { get; set; }

	[Export] public int ChargePriority { get; set; }

	[Export] public int SummonPriority { get; set; }

	[Export] public int BuffPriority { get; set; }

	/// <summary>
	/// 把这一行套到单位的 AI 上（<c>EnemyBrain</c> + 它下面的行为节点）。
	///
	/// ⚠️ <b>调用时机</b>：必须在 `AddChild` **之前**（也就是 <c>EnemyBrain._Ready</c> 之前）——
	/// `_Ready` 会按 <c>Priority</c> 给行为列表排序，晚套用这一帧会按旧优先级决策。
	/// 现在由 <see cref="UnitConfig.ApplyTo"/> 统一调用，与"先 ApplyTo 再 AddChild"的既有顺序一致。
	///
	/// 读不到行时**根本不会调用它** —— 单位保留场景里的 AI 参数（"读不到则使用自身的默认值"）。
	/// </summary>
	public override void ApplyTo(Node target)
	{
		if (target is not Unit unit)
		{
			return;
		}

		if (NodeSearch.FindInSubtree<EnemyBrain>(unit) is not { } brain)
		{
			GD.PushWarning($"[AiProfileConfig] {Id} 要套到 {unit.Name} 上，但这个单位的子树里没有 EnemyBrain，已跳过。");
			return;
		}

		if (SearchRange > 0f)
		{
			brain.SearchRange = SearchRange;
		}

		if (TargetRefreshInterval > 0f)
		{
			brain.TargetRefreshInterval = TargetRefreshInterval;
		}

		// 按**节点名**认领行为槽位（与 AbilityConfig.NodeName 同一套寻址方式）：
		// 引擎自带的 Set/Get 按属性名写值，所以不需要为每个行为建映射表。
		foreach (Node child in brain.GetChildren())
		{
			if (child is not Behavior behavior)
			{
				continue;
			}

			switch (behavior.Name.ToString())
			{
				case "Attack":
					ApplyPriority(behavior, AttackPriority);
					break;

				case "Chase":
					if (ApplyPriority(behavior, ChasePriority))
					{
						SetPositive(behavior, "StopDistance", ChaseStopDistance);
						SetPositive(behavior, "SpeedScale", ChaseSpeedScale);
					}

					break;

				case "Shoot":
					ApplyPriority(behavior, ShootPriority);
					break;

				case "KeepDistance":
					if (ApplyPriority(behavior, KeepDistancePriority))
					{
						SetPositive(behavior, "PreferredDistance", KeepDistancePreferredDistance);
						SetPositive(behavior, "Tolerance", KeepDistanceTolerance);
						SetPositive(behavior, "SpeedScale", KeepDistanceSpeedScale);
						SetRaw(behavior, "Strafe", KeepDistanceStrafe);
						SetPositive(behavior, "StrafeScale", KeepDistanceStrafeScale);
					}

					break;

				case "Retreat":
					if (ApplyPriority(behavior, RetreatPriority))
					{
						// HealthThreshold 允许 0（= 永不撤退），只有负数才是"不覆盖"。
						SetNotNegative(behavior, "HealthThreshold", RetreatHealthThreshold);
						SetPositive(behavior, "SpeedScale", RetreatSpeedScale);
						SetPositive(behavior, "KeepDistance", RetreatKeepDistance);
					}

					break;

				case "Explode":
					ApplyPriority(behavior, ExplodePriority);
					break;

				case "Leap":
					ApplyPriority(behavior, LeapPriority);
					break;

				case "Scream":
					ApplyPriority(behavior, ScreamPriority);
					break;

				case "Charge":
					ApplyPriority(behavior, ChargePriority);
					break;

				case "Summon":
					ApplyPriority(behavior, SummonPriority);
					break;

				case "Buff":
					ApplyPriority(behavior, BuffPriority);
					break;
			}
		}

		ApplyExtraParams(brain);
	}

	/// <summary>
	/// <c>Priority &lt;= 0</c> = 这个槽位在本档案里"不管"，返回 false 让调用方把该槽位的参数整体跳过。
	/// </summary>
	private static bool ApplyPriority(Behavior behavior, int priority)
	{
		if (priority <= 0)
		{
			return false;
		}

		behavior.Priority = priority;
		return true;
	}

	/// <summary><c>&lt;= 0</c> = 不覆盖（距离 / 倍率这类 0 没有意义的值都用这条）。</summary>
	private static void SetPositive(GodotObject target, string property, float value)
	{
		if (value > 0f)
		{
			SetRaw(target, property, value);
		}
	}

	/// <summary><c>&lt; 0</c> = 不覆盖（给"0 是有意义的值"那一列用，例如撤退阈值 0 = 永不撤退）。</summary>
	private static void SetNotNegative(GodotObject target, string property, float value)
	{
		if (value >= 0f)
		{
			SetRaw(target, property, value);
		}
	}

	/// <summary>只在目标真的有这个属性时才写 —— 避免把参数静默写进一个不存在的属性。</summary>
	private static void SetRaw(GodotObject target, string property, Variant value)
	{
		if (target.Get(property).VariantType == Variant.Type.Nil)
		{
			GD.PushWarning($"[AiProfileConfig] {target.GetType().Name} 上没有属性 {property}，已跳过。");
			return;
		}

		target.Set(property, value);
	}

	/// <summary>
	/// 逃生口：<see cref="ConfigRow.Params"/> 的键写成 <c>节点名.属性名</c>（例：<c>Chase.MaxRange = 8</c>）。
	/// 给"没有独立列的参数"（<c>MinRange</c> / <c>MaxRange</c> / <c>AbilityNodeName</c> /
	/// 将来新增的行为类）留一条不用改 C# 就能调的路。
	/// </summary>
	private void ApplyExtraParams(EnemyBrain brain)
	{
		foreach (ParamEntry entry in Params)
		{
			if (string.IsNullOrEmpty(entry.Key))
			{
				continue;
			}

			int dot = entry.Key.IndexOf('.');
			if (dot <= 0 || dot >= entry.Key.Length - 1)
			{
				GD.PushWarning($"[AiProfileConfig] {Id} 的参数键「{entry.Key}」格式不对，应为「节点名.属性名」，已跳过。");
				continue;
			}

			string nodeName = entry.Key[..dot];
			string property = entry.Key[(dot + 1)..];
			if (brain.GetNodeOrNull<Node>(nodeName) is not { } node)
			{
				GD.PushWarning($"[AiProfileConfig] {Id} 的参数 {entry.Key}：Brain 下面没有节点 {nodeName}，已跳过。");
				continue;
			}

			SetRaw(node, property, entry.Value);
		}
	}
}
