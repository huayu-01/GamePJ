using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// **单位表**的公共基类（配置表阶段 3）：敌人 / 首领 / 召唤物 / 角色都"享有一份通用单位参数"。
///
/// 字段分组就是用户给的结构，一行一个单位：
/// <list type="bullet">
/// <item><b>攻击</b>：伤害 / 间隔 / 范围 / 击退（套到单位的**主攻击**上）。</item>
/// <item><b>生命</b>：最大生命 / 护甲 / 护盾 / 护甲减伤 / 额外减伤 / 击退抗性。</item>
/// <item><b>移速</b>。</item>
/// <item><b>行为 &amp; 类</b>：敌种类标签（供按类筛选；取代"按显示名匹配"）。</item>
/// <item><b>持有技能</b>：指向技能表（`Config/Tables/Skills/`）的**资源引用**，不是字符串 id。</item>
/// <item><b>表现</b>：模型 / 图标 / 缩放。</item>
/// </list>
///
/// 具体表只在此基础上加"这一类的私有列"：<see cref="EnemyConfig"/> 加刷怪成本与权重，
/// <see cref="BossConfig"/> 加白板副本血量，<see cref="SummonConfig"/> 加存活时间，
/// <see cref="CharacterConfig"/> 加武器/能力开关矩阵。
///
/// ⚠️ <b>调用时机</b>：必须在 `Unit._EnterTree` **之前**套用（生成时"先 ApplyTo、再 AddChild"）——
/// `Unit` 在 `_EnterTree` 里捕获基础值，晚套用会让"回满生命"算错（见 config-tables-inventory.md #10）。
///
/// 数值权威与回退：**表是权威、场景是回退**。读不到行时调用方什么都不做，单位保留场景里的值。
/// </summary>
public abstract partial class UnitConfig : ConfigRow
{
	[ExportGroup("生命")]
	[Export] public float MaxHealth { get; set; } = 100f;

	[Export] public float MaxArmor { get; set; }

	[Export] public float MaxShield { get; set; }

	/// <summary>护甲大于 0 时提供的伤害减免。</summary>
	[Export(PropertyHint.Range, "0,0.9,0.01")] public float ArmorDamageReduction { get; set; } = 0.3f;

	/// <summary>护甲之外的额外减伤（与护甲减伤按 1-(1-a)(1-b) 叠加，总量上限 90%）。</summary>
	[Export(PropertyHint.Range, "0,0.9,0.01")] public float DamageReduction { get; set; }

	/// <summary>击退抗性，0 = 全额吃击退，1 = 完全免疫。</summary>
	[Export(PropertyHint.Range, "0,1,0.05")] public float KnockbackResistance { get; set; }

	[ExportGroup("移速")]
	/// <summary>基础移动速度（增益通过 StatSheet 的 MoveSpeedMultiplier 放大）。</summary>
	[Export] public float MoveSpeed { get; set; } = 4f;

	[ExportGroup("攻击（主攻击 = 子树里第一个带命中体的能力；0 = 不覆盖）")]
	/// <summary>主攻击的伤害（写进命中体的 <c>Damage</c>；远程兵这类没有命中体的写进武器组件）。0 = 不覆盖。</summary>
	[Export] public float AttackDamage { get; set; }

	/// <summary>主攻击的间隔（写进能力的 <c>Cooldown</c>）。0 = 不覆盖。</summary>
	[Export] public float AttackInterval { get; set; }

	/// <summary>
	/// 主攻击的作用距离。按能力子类的不同属性名依次尝试：<c>AttackRange</c>（近战）→
	/// <c>TriggerRadius</c>（范围）→ <c>MaxRange</c>（远程）→ <c>MaxDistance</c>（冲锋）。0 = 不覆盖。
	/// </summary>
	[Export] public float AttackRange { get; set; }

	/// <summary>主攻击的击退（写进命中体的 <c>Knockback</c>）。0 = 不覆盖。</summary>
	[Export] public float AttackKnockback { get; set; }

	[ExportGroup("行为 & 类")]
	/// <summary>
	/// 敌种类标签（<c>Melee</c> / <c>Tank</c> / <c>Boss</c> / <c>Summon</c> …），套到 <see cref="Unit.Tags"/>。
	/// 用途是按类筛选（例如"这一关只放近战类"），**不是**拿来当身份键 —— 身份键是 <see cref="ConfigRow.Id"/>。
	/// </summary>
	[Export] public string[] Tags { get; set; } = System.Array.Empty<string>();

	[ExportGroup("持有技能（指向 Config/Tables/Skills/ 的行；资源引用不是字符串）")]
	/// <summary>
	/// 这个单位装了哪几个技能。元素指向技能表的行文件（行被删/改名时 Godot 把引用置 null，不会留下悬空 id）。
	/// 生成时按行里的 <see cref="AbilityConfig.NodeName"/> 找到对应能力节点并套用参数。
	/// </summary>
	[Export] public AbilityConfig[] Skills { get; set; } = System.Array.Empty<AbilityConfig>();

	[ExportGroup("表现")]
	/// <summary>替换占位模型用的预制体（空 = 用场景里自带的占位模型）。</summary>
	[Export(PropertyHint.File, "*.glb,*.gltf,*.tscn")] public string ModelPath { get; set; } = string.Empty;

	// ⚠️ 刻意**没有** IconPath 列：敌人/召唤物现在没有任何"图标"界面（首领面板的头像是 BarBoss.tscn 的
	// 固定色块），加一列没人读的图标路径就是死配置。要做首领头像时再加一列 + 一处 UI（`PlayerHud`）。

	/// <summary>整体缩放（含碰撞体，与 <see cref="EliteModifier"/> 的 ModelScale 同一套做法）。1 = 不缩放。</summary>
	[Export(PropertyHint.Range, "0.1,5,0.05")] public float ModelScale { get; set; } = 1f;

	/// <summary>作用距离的候选属性名，按能力子类依次尝试（见 <see cref="AttackRange"/>）。</summary>
	private static readonly string[] RangeProperties = { "AttackRange", "TriggerRadius", "MaxRange", "MaxDistance" };

	/// <summary>
	/// 把"生命 + 移速 + 类标签 + 攻击 + 表现 + 持有技能"套到单位上。
	/// 读不到行时**根本不会调用它** —— 单位保留场景里的值（"读不到则使用自身的默认值"）。
	/// </summary>
	public override void ApplyTo(Node target)
	{
		if (target is not Unit unit)
		{
			return;
		}

		unit.ApplyBaseStats(MaxHealth, MaxArmor, MaxShield, MoveSpeed);
		unit.ArmorDamageReduction = ArmorDamageReduction;
		unit.DamageReduction = DamageReduction;
		unit.KnockbackResistance = KnockbackResistance;
		if (Tags.Length > 0)
		{
			unit.Tags = Tags;
		}

		ApplyAttackTo(unit);
		ApplySkillsTo(unit);
		ApplyModel(unit);
	}

	/// <summary>
	/// 攻击组 → 单位的**主攻击**：子树里第一个"带命中体、或带武器组件"的能力。
	/// 每个字段单独判 0（0 = 不覆盖），所以"只调间隔不调伤害"不会把伤害写成 0。
	/// 玩家侧由 <c>RunController.ApplyBalance</c> 直接调用（角色行的攻击组就是近战重击的四个数）。
	/// </summary>
	public void ApplyAttackTo(Unit unit)
	{
		if (AttackDamage <= 0f && AttackInterval <= 0f && AttackRange <= 0f && AttackKnockback <= 0f)
		{
			return;
		}

		var abilities = new List<Ability>();
		NodeSearch.CollectInSubtree(unit, abilities);
		foreach (Ability ability in abilities)
		{
			// 命中体优先（近战/范围/冲锋），没有命中体就退到武器组件（远程兵走这条）。
			Node? strike = (Node?)NodeSearch.FindInSubtree<AttackHitbox>(ability)
				?? NodeSearch.FindInSubtree<Weapons.Weapon>(ability);
			if (strike == null)
			{
				continue;
			}

			if (AttackDamage > 0f)
			{
				SetIfPresent(strike, "Damage", AttackDamage);
			}

			if (AttackKnockback > 0f)
			{
				SetIfPresent(strike, "Knockback", AttackKnockback);
			}

			if (AttackInterval > 0f)
			{
				SetIfPresent(ability, "Cooldown", AttackInterval);
			}

			if (AttackRange > 0f)
			{
				foreach (string property in RangeProperties)
				{
					if (SetIfPresent(ability, property, AttackRange))
					{
						break;
					}
				}
			}

			return;
		}

		GD.PushWarning($"[UnitConfig] {Id} 填了攻击参数，但在 {unit.Name} 的子树里找不到主攻击，已跳过。");
	}

	/// <summary>
	/// 持有技能 → 按 <see cref="AbilityConfig.NodeName"/> 在单位子树里找到对应能力并套用参数。
	/// 行读不到 / 节点找不到时**跳过那一行**，能力保留场景里的值。
	/// 玩家侧由 <c>RunController.ApplyBalance</c> 调用（角色行的"持有技能"指向技能表）。
	/// </summary>
	public void ApplySkillsTo(Unit unit)
	{
		foreach (AbilityConfig skill in Skills)
		{
			if (skill == null)
			{
				continue;
			}

			if (unit.GetNodeOrNull<Ability>(skill.NodeName) is { } ability)
			{
				skill.ApplyTo(ability);
				continue;
			}

			GD.PushWarning($"[UnitConfig] {Id} 的持有技能 {skill.Id} 找不到节点 {skill.NodeName}，已跳过。");
		}
	}

	/// <summary>表现：换模型（可选）+ 整体缩放。两件都留空/为 1 时什么都不做。</summary>
	private void ApplyModel(Unit unit)
	{
		if (!string.IsNullOrEmpty(ModelPath))
		{
			if (ResourceLoader.Exists(ModelPath) && GD.Load<PackedScene>(ModelPath) is { } model)
			{
				// 单位这时还没进场景树，占位模型可以直接 Free（QueueFree 要等进树才生效）。
				unit.GetNodeOrNull<Node>("Model")?.Free();
				unit.AddChild(model.Instantiate());
			}
			else
			{
				GD.PushWarning($"[UnitConfig] {Id} 的模型 {ModelPath} 读不到，保留场景里的占位模型。");
			}
		}

		if (!Mathf.IsEqualApprox(ModelScale, 1f))
		{
			unit.Scale = new Vector3(ModelScale, ModelScale, ModelScale);
		}
	}

	/// <summary>只在目标真的有这个属性时才写 —— 避免把参数静默写进一个不存在的属性。</summary>
	private static bool SetIfPresent(GodotObject target, string property, float value)
	{
		if (target.Get(property).VariantType == Variant.Type.Nil)
		{
			return false;
		}

		target.Set(property, value);
		return true;
	}
}
