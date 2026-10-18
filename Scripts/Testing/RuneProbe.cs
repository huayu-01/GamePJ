using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>
/// **J11 符文系统专项验收**（真实玩家 + 真实表行 + 真实结算）。
///
/// <para>八段：</para>
/// <list type="number">
/// <item><b>表 → 索引</b>：11 枚符文全部读到、Id 与 `UnlocksRequires`（`Array&lt;string&gt;` 列）逐项对；
///   每条 `ApplyHandler` 都已注册（拼错会在这里红）；</item>
/// <item><b>默认（不带符文）逐位不变</b>：生命 220 / 护甲 20 / 移速 6.5 / 各乘区 1 / 0 条符文规则，
///   一次受击的最终伤害与"没有任何符文修正器"时相等；</item>
/// <item><b>⭐ 雷之印</b>：携带后玩家最大生命值 **220 → 30**（真实读数）；</item>
/// <item><b>⭐ 战神印</b>：同样一次受击 10 → 最终伤害 10（不带）× **40（携带）** = 真 4 倍；
///   暴击率 +10%（`CritChanceAdd`）；</item>
/// <item><b>风之印 / 行军 / 大力符 / 飞翼</b>：冷却 ×1.5、移速 ×2、伤害 ×1.2、起跳 ×1.3 + 下落上限 ×0.8；</item>
/// <item><b>⭐ 符文 → 增幅前置</b>：用**真实** `RewardPool.Initialize` + `Roll`（候选表里临时加一条
///   `Requires = ["雷之印"]` 的探针行）—— 不带符文时它**进不了池**，携带雷之印时才进；换战神印仍进不了
///   （派系不串）；池子其余候选数与真实目录长度一位都没变；</item>
/// <item><b>带行为的 5 枚</b>：死铠（+20 甲 / 每 10 杀 +1 甲 / 禁疗）、生死（致命伤无效化 + 恢复 30%）、
///   狙神（1.5m ×0.5 / 9m ×1.0 / 20m ×1.5）、巨人（`RollHit` 多出 10% 最大生命值）、
///   小精灵（绕行 + 每 5 秒打一次且不在任何单位分组里）；</item>
/// <item><b>摘下符文后回到逐位不变</b>：所有读数回到第 2 段的基线。</item>
/// </list>
/// </summary>
public partial class RuneProbe : Node3D
{
#if DEBUG
	private int _pass, _fail;
	private Unit _player = null!;
	private Weapon _weapon = null!;
	private RunController _run = null!;
	private Camera3D _camera = null!;
	private readonly PlayerLoadout _loadout = new();

	private int _baselineHealth;
	private float _baselineArmor;
	private float _baselineMoveSpeed;
	private float _baselineIncomingDamage;
	private float _baselineCatalogLength;
	private int _baselineCandidates;

	/// <summary>基线（不带符文）时一次受击的最终伤害。</summary>
	private float _baselineHit;

	public override async void _Ready()
	{
		try
		{
			await Wait(0.2);
			RuneCarry.UseInMemoryStoreForSelfTest();
			RunSetup.HasSelection = true;
			RunSetup.Character = PlayerCharacterKind.Hunter;
			RunSetup.Weapon = WeaponPresetKind.Bow;

			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>();
			AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true };
			AddChild(_camera);
			_player.GlobalPosition = new Vector3(20f, 0.05f, 20f);
			_player.Velocity = Vector3.Zero;
			_camera.GlobalPosition = new Vector3(20f, 6f, 30f);
			await Wait(0.15);

			_loadout.Apply(_player, this, PlayerCharacterKind.Hunter, WeaponPresetKind.Bow);
			_weapon = NodeSearch.FindInSubtree<Weapon>(_player)!;

			// 真·RunController：`SetCarriedRune` 是本批唯一的"携带"写入口（UI 下一批再做）。
			_run = new RunController { Name = "RuneProbeRun", AutoStartStage = false };
			AddChild(_run);
			await Wait(0.2);

			await TableToIndex();
			await BaselineUnchanged();
			await LeiSeal();
			await WarSeal();
			await StatOnlyRunes();
			await PrerequisiteUnlock();
			await BehaviorRunes();
			await RestoreToBaseline();
		}
		catch (Exception ex)
		{
			Check(false, ex.ToString());
		}

		GD.Print($"[RuneProbe] === J11 符文系统验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}

	// ── ① 表 → 索引 ────────────────────────────────────────────────────────
	private async Task TableToIndex()
	{
		await Wait(0.1);
		int count = ConfigRows.Count<RuneConfig>();
		Check(count == 11, $"① 符文表读到 {count} 枚（期望 11 = 文档《后续构筑.txt》的 11 条）");

		string[] expected =
		{
			"rune_lei_seal", "rune_war_seal", "rune_wind_seal", "rune_death_armor", "rune_march",
			"rune_life_death", "rune_strength_talisman", "rune_flying_wing", "rune_sniper", "rune_giant", "rune_pixie",
		};
		var missing = expected.Where(id => ConfigRows.ById<RuneConfig>(id) == null).ToArray();
		Check(missing.Length == 0, $"① 11 个 Id 全在表里（缺 {missing.Length} 个{(missing.Length == 0 ? string.Empty : "：" + string.Join(",", missing))}）");

		var unknownHandler = new List<string>();
		foreach (RuneConfig row in ActRoguelike.Config.ConfigIndex.Settings.Runes)
		{
			if (row == null || !row.HasKnownHandler)
			{
				unknownHandler.Add(row?.Id ?? "null");
			}
		}

		Check(unknownHandler.Count == 0, $"① 每条 ApplyHandler 都已注册（未注册 {unknownHandler.Count} 条{(unknownHandler.Count == 0 ? string.Empty : "：" + string.Join(",", unknownHandler))}）");

		// UnlocksRequires 是 `Godot.Collections.Array<string>`（architecture.md 第 12 条）——
		// 这里逐项核对"项数 + 每一项的值"，证"能写盘、能读回、不是一整串文本"。
		Check(UnlockOf("rune_lei_seal") == "雷之印", $"① 雷之印的 UnlocksRequires = [{UnlockOf("rune_lei_seal")}]（期望 雷之印）");
		Check(UnlockOf("rune_war_seal") == "战神刻印", $"① 战神印的 UnlocksRequires = [{UnlockOf("rune_war_seal")}]（期望 战神刻印）");
		Check(UnlockOf("rune_wind_seal") == "风之印", $"① 风之印的 UnlocksRequires = [{UnlockOf("rune_wind_seal")}]（期望 风之印）");
		int emptyUnlocks = expected.Count(id => ConfigRows.ById<RuneConfig>(id) is { } row && row.UnlocksRequires.Count == 0);
		Check(emptyUnlocks == 8, $"① 其余 8 枚不解锁前置（实测 {emptyUnlocks} 枚为空）");

		GD.Print("[RuneProbe] INFO ① 符文表清单：" + string.Join("；", expected.Select(id =>
		{
			RuneConfig row = ConfigRows.ById<RuneConfig>(id)!;
			return $"{row.Id}（{row.DisplayName}）{row.ApplyHandler}｜解锁 [{string.Join(",", row.UnlockList())}]";
		})));
	}

	private static string UnlockOf(string id)
		=> ConfigRows.ById<RuneConfig>(id) is { } row ? string.Join(",", row.UnlockList()) : "（读不到）";

	// ── ② 默认（不带符文）逐位不变 ──────────────────────────────────────────
	private async Task BaselineUnchanged()
	{
		Check(_run.CarriedRuneId.Length == 0, $"② 开局没有携带符文（CarriedRuneId「{_run.CarriedRuneId}」）");
		Check(_run.Build.RuneUnlocks.Count == 0, $"② 前置解锁名单为空（{_run.Build.DescribeRuneUnlocks()}）");

		_baselineHealth = Mathf.RoundToInt(_player.MaxHealth);
		_baselineArmor = _player.StartingArmor;
		_baselineMoveSpeed = _player.MoveSpeed;
		Check(_baselineHealth == 220, $"② 玩家生命 {_player.MaxHealth:0}（期望 220 = 角色 200 + 元进度 20，与加符文之前逐字一致）");
		Check(Mathf.IsEqualApprox(_baselineArmor, 20f), $"② 玩家初始护甲 {_player.StartingArmor:0}（期望 20）");
		Check(Mathf.IsEqualApprox(_baselineMoveSpeed, 6.5f), $"② 玩家移速 {_player.MoveSpeed:0.##}（期望 6.5）");

		float[] multipliers =
		{
			_player.Stats.Get("DamageMultiplier"), _player.Stats.Get("IncomingDamageMultiplier"),
			_player.Stats.Get("CooldownMultiplier"), _player.Stats.Get("MoveSpeedMultiplier"),
			_player.Stats.Get("JumpVelocityMultiplier"), _player.Stats.Get("MaxFallSpeedMultiplier"),
			_player.Stats.Get("HealMultiplier"),
		};
		Check(multipliers.All(v => Mathf.IsEqualApprox(v, 1f)),
			$"② 七条乘区全是中性值 1（实测 {string.Join(" / ", multipliers.Select(v => v.ToString("0.###")))}）");
		Check(Mathf.IsEqualApprox(_player.Stats.Get("MaxHealthCap"), 0f), $"② 生命上限夹子 {_player.Stats.Get("MaxHealthCap"):0.##}（期望 0 = 不夹）");
		Check(_player.DeathAvoidRuleCount == 0, $"② 玩家身上 0 条死亡避免规则（实测 {_player.DeathAvoidRuleCount}）");
		Check(CountRuneNodes() == 0, $"② 玩家身上 0 个符文规则节点（实测 {CountRuneNodes()}）");

		_baselineHit = TakeCleanHit(10f);
		Check(Mathf.IsEqualApprox(_baselineHit, 7f),
			$"② 基线受击：10 点伤害 → 最终 {_baselineHit:0.##}（= 10 × (1 − 护甲减伤 30%)，玩家身上没有任何符文加成；这个 7 就是后面「战神印 4 倍」的对照值）");

		// 池子基线（猎手 + 弓）：候选数 + 真实目录条数 —— 后面证明"符文不改池子"。
		var pool = new RewardPool();
		pool.Initialize(_weapon, "chr_hunter");
		_baselineCandidates = pool.CandidateCount;
		_baselineCatalogLength = RewardCatalog.All.Length;
		Check(_baselineCatalogLength == 70, $"② 真实增幅项目录 {RewardCatalog.All.Length} 条（期望 J24后70 = 17/23/30，新增八条强化等级，符文**不进**这张表）");
		Check(_baselineCandidates > 0, $"② 猎手+弓 的候选池 {_baselineCandidates} 条（不带符文时的基线）");
		await Wait(0.05);
	}

	// ── ③ 雷之印（⭐ 用户点名要读数） ──────────────────────────────────────
	private async Task LeiSeal()
	{
		RuneConfig? row = _run.SetCarriedRune("rune_lei_seal");
		await Wait(0.1);
		Check(row?.Id == "rune_lei_seal", $"③ SetCarriedRune 返回「{row?.Id}」（期望 rune_lei_seal）");
		Check(_run.CarriedRuneId == "rune_lei_seal", $"③ 运行期字段 CarriedRuneId = 「{_run.CarriedRuneId}」");

		GD.Print($"[RuneProbe] ⭐ ③ **雷之印**：玩家最大生命值 {_baselineHealth} → {_player.MaxHealth:0}；当前生命 {_player.Health:0}");
		Check(Mathf.IsEqualApprox(_player.MaxHealth, 30f), $"③ 雷之印：最大生命值从 {_baselineHealth} 变成 {_player.MaxHealth:0}（期望 30 —— 用户确认「真的限制为 30」）");
		Check(_player.Health <= 30f, $"③ 雷之印：当前生命被同时夹到 {_player.Health:0}（不会留在 220）");
		Check(Mathf.IsEqualApprox(_player.Stats.Get("MaxHealthCap"), 30f), $"③ StatSheet 的 MaxHealthCap = {_player.Stats.Get("MaxHealthCap"):0.##}（期望 30）");
		Check(_run.Build.RuneUnlocks.Contains("雷之印") && _run.Build.RuneUnlocks.Contains("rune_lei_seal"),
			$"③ 前置解锁名单 {_run.Build.DescribeRuneUnlocks()}（同时含派系名与符文行 Id）");
		Check(ConfigRows.ById<RuneConfig>("rune_lei_seal")!.UnlocksRequires.Count == 1, "③ 雷之印只解锁一条前置");

		// 回到满血再进下一段（雷之印会一直夹着 30，所以先摘掉）。
		_run.SetCarriedRune(string.Empty);
		Refill();
		await Wait(0.1);
		Check(Mathf.IsEqualApprox(_player.MaxHealth, _baselineHealth), $"③ 摘下雷之印后生命上限回到 {_player.MaxHealth:0}（期望 {_baselineHealth}）");
		Check(_run.Build.RuneUnlocks.Count == 0, $"③ 摘下后前置解锁名单被清空（{_run.Build.DescribeRuneUnlocks()}）");
	}

	// ── ④ 战神印（⭐ 用户点名要读数） ──────────────────────────────────────
	private async Task WarSeal()
	{
		float critBefore = _player.Stats.Get("CritChanceAdd");
		float hitBefore = TakeCleanHit(10f);
		Refill();

		RuneConfig? row = _run.SetCarriedRune("rune_war_seal");
		await Wait(0.1);
		Check(row?.Id == "rune_war_seal", $"④ SetCarriedRune 返回「{row?.Id}」（期望 rune_war_seal）");

		float critAfter = _player.Stats.Get("CritChanceAdd");
		float incoming = _player.Stats.Get("IncomingDamageMultiplier");
		float hitAfter = TakeCleanHit(10f);
		await Wait(0.05);

		GD.Print(
			$"[RuneProbe] ⭐ ④ **战神印**：暴击率加成 {critBefore:0.###} → {critAfter:0.###}（+{critAfter - critBefore:0.###}）；"
			+ $"受击倍率 ×{incoming:0.###}；同样 10 点伤害的最终伤害 {hitBefore:0.##} → {hitAfter:0.##}（比值 {(hitBefore > 0f ? hitAfter / hitBefore : 0f):0.####}）");

		Check(Mathf.IsEqualApprox(critAfter - critBefore, 0.1f), $"④ 战神印：暴击率加成 +{critAfter - critBefore:0.###}（期望 +0.1 = 10%）");
		Check(Mathf.IsEqualApprox(incoming, 4f), $"④ 战神印：受击倍率 ×{incoming:0.###}（期望 ×4 = 「受到伤害增加 300%」）");
		Check(Mathf.IsEqualApprox(hitAfter, hitBefore * 4f),
			$"④ 战神印：同一次 10 点伤害 → 最终 {hitAfter:0.##}，正好是不带符文时 {hitBefore:0.##} 的 {hitAfter / Mathf.Max(0.0001f, hitBefore):0.####} 倍（期望 4）");
		Check(_player.Health > 0f, $"④ 战神印：挨了这一下还活着（生命 {_player.Health:0.##}）");

		_run.SetCarriedRune(string.Empty);
		Refill();
		await Wait(0.1);
		Check(Mathf.IsEqualApprox(_player.Stats.Get("IncomingDamageMultiplier"), 1f), $"④ 摘下后受击倍率回到 ×{_player.Stats.Get("IncomingDamageMultiplier"):0.###}");
	}

	// ── ⑤ 纯数值符文（风之印 / 行军 / 大力符 / 飞翼 / 死铠的禁疗与加甲） ──────
	private async Task StatOnlyRunes()
	{
		Ability skill = PlayerAbilityInput.FindByInputAction(_player, "weapon_skill_1")!;
		float cooldownBase = skill.EffectiveCooldown;

		_run.SetCarriedRune("rune_wind_seal");
		await Wait(0.1);
		float cooldownSeal = skill.EffectiveCooldown;
		Check(Mathf.IsEqualApprox(cooldownSeal / cooldownBase, 1.5f),
			$"⑤ 风之印：技能 1 实际冷却 {cooldownBase:0.###}s → {cooldownSeal:0.###}s（×{cooldownSeal / Mathf.Max(0.0001f, cooldownBase):0.###}，期望 ×1.5 = +50%）");

		_run.SetCarriedRune("rune_march");
		await Wait(0.1);
		Check(Mathf.IsEqualApprox(_player.Stats.Get("MoveSpeedMultiplier"), 2f),
			$"⑤ 行军：移速倍率 ×{_player.Stats.Get("MoveSpeedMultiplier"):0.###}（期望 ×2 = +100%）");

		_run.SetCarriedRune("rune_strength_talisman");
		await Wait(0.1);
		Check(Mathf.IsEqualApprox(_player.Stats.Get("DamageMultiplier"), 1.2f),
			$"⑤ 大力符：伤害倍率 ×{_player.Stats.Get("DamageMultiplier"):0.###}（期望 ×1.2 = +20%）");

		_run.SetCarriedRune("rune_flying_wing");
		await Wait(0.1);
		float jump = _player.Stats.Get("JumpVelocityMultiplier");
		float fall = _player.Stats.Get("MaxFallSpeedMultiplier");
		float jumpVelocity = _player.Movement?.EffectiveJumpVelocity ?? 0f;
		Check(Mathf.IsEqualApprox(jump, 1.3f) && Mathf.IsEqualApprox(fall, 0.8f),
			$"⑤ 飞翼：起跳 ×{jump:0.###}（期望 ×1.3）/ 正常下落上限 ×{fall:0.###}（期望 ×0.8）");
		Check(jumpVelocity > 13f, $"⑤ 飞翼：真实起跳速度 {jumpVelocity:0.###} m/s（角色行固定值 13 × 1.3 = 16.9；「下落攻击的下落速度」那一半本批**未实现**，见行 Note）");

		_run.SetCarriedRune("rune_death_armor");
		await Wait(0.1);
		Check(Mathf.IsEqualApprox(_player.Armor, 20f + 20f), $"⑤ 死铠：护甲 20 → {_player.Armor:0.##}（期望 +20 = 40）");
		Check(Mathf.IsEqualApprox(_player.Stats.Get("HealMultiplier"), 0f), $"⑤ 死铠：治疗倍率 ×{_player.Stats.Get("HealMultiplier"):0.###}（期望 0 = 无法恢复生命值）");
		float healed = _player.Heal(50f);
		Check(Mathf.IsEqualApprox(healed, 0f), $"⑤ 死铠：真实调用 Heal(50) 恢复 {healed:0.##} 点（期望 0）");

		// 每 10 杀 +1 甲：走真实 Kill 事件（ForceKill 是既有的脚本击杀入口，抛的是同一个 Kill 事件）。
		var armorRule = _player.GetNodeOrNull<RuneArmorOnKillRule>("Rune_rune_death_armor_ArmorOnKill");
		Check(armorRule != null, $"⑤ 死铠：击杀加甲规则挂上了（{(armorRule == null ? "没找到" : armorRule.DebugState())}）");
		if (armorRule != null)
		{
			float armorBefore = _player.Armor;
			var victims = new List<Unit>();
			for (int i = 0; i < 9; i++)
			{
				victims.Add(MakeTarget($"Kill{i}", new Vector3(30f + i, 0.9f, 30f), 100f));
			}

			await Wait(0.1);
			foreach (Unit victim in victims)
			{
				victim.ForceKill(_player);
			}

			await Wait(0.15);
			Check(Mathf.IsEqualApprox(_player.Armor, armorBefore), $"⑤ 死铠：9 杀之后护甲 {_player.Armor:0.##}（还不到 10 杀，期望不变 = {armorBefore:0.##}）");

			Unit tenth = MakeTarget("Kill9", new Vector3(30f, 0.9f, 32f), 100f);
			await Wait(0.1);
			tenth.ForceKill(_player);
			await Wait(0.15);
			Check(Mathf.IsEqualApprox(_player.Armor, armorBefore + 1f),
				$"⑤ 死铠：第 10 杀 → 护甲 {armorBefore:0.##} → {_player.Armor:0.##}（期望 +1）；规则读数：{armorRule.DebugState()}");
			foreach (Unit victim in victims)
			{
				if (IsInstanceValid(victim))
				{
					victim.QueueFree();
				}
			}

			if (IsInstanceValid(tenth))
			{
				tenth.QueueFree();
			}
		}

		_run.SetCarriedRune(string.Empty);
		Refill();
		await Wait(0.1);
		Check(Mathf.IsEqualApprox(_player.Stats.Get("HealMultiplier"), 1f) && Mathf.IsEqualApprox(_player.Stats.Get("MoveSpeedMultiplier"), 1f),
			$"⑤ 摘下后治疗倍率 {_player.Stats.Get("HealMultiplier"):0.###} / 移速 {_player.Stats.Get("MoveSpeedMultiplier"):0.###}（期望都回到 1）");
	}

	// ── ⑥ ⭐ 符文 → 增幅前置（本系统的核心价值） ───────────────────────────
	private async Task PrerequisiteUnlock()
	{
		// 探针合成行：`Requires = ["雷之印"]` —— 文档里那些"雷系特化增幅"就是这么写的。
		// ⚠️ 它**只在本次调用期间**临时插进运行期目录，用来证明前置通路；
		//   表里那 21 条雷系/战神系增幅属于**后续批次**（本批不建），所以真实表里现在一条都没有，
		//   —— 这一点在报告里明确登记，不当成"已经能抽到"。
		RewardDefinition[] original = RewardCatalog.All;
		RewardDefinition probe = MakeProbeRow("雷之印", "probe_lei_gated");
		RewardDefinition probeWar = MakeProbeRow("战神刻印", "probe_war_gated");
		RewardDefinition[] withProbes = original.Concat(new[] { probe, probeWar }).ToArray();
		RewardCatalog.UseTable(withProbes);
		try
		{
			var pool = new RewardPool();
			pool.Initialize(_weapon, "chr_hunter");

			_run.SetCarriedRune(string.Empty);
			await Wait(0.05);
			List<string> none = RollAll(pool);
			Check(!none.Contains("probe_lei_gated") && !none.Contains("probe_war_gated"),
				"⑥ 不携带符文：两条 Requires 探针行**都不进池**（前置未满足 → 根本不出现）");
			Check(none.Count > 0, $"⑥ 不携带符文时可用候选 {none.Count} 条（两条探针已被前置拦掉）");

			_run.SetCarriedRune("rune_lei_seal");
			await Wait(0.05);
			List<string> lei = RollAll(pool);
			Check(lei.Contains("probe_lei_gated"), "⑥ ⭐ 携带雷之印：`Requires = [雷之印]` 的增幅**进了候选池**（= 用户那句「携带了这个雷之印等于满足它们的前置要求」）");
			Check(!lei.Contains("probe_war_gated"), "⑥ 携带雷之印：`Requires = [战神刻印]` 的那条**仍然不进**（派系不串）");
			Check(lei.Count == none.Count + 1, $"⑥ 携带雷之印让可用候选 {none.Count} → {lei.Count}（正好多 1 条）");

			_run.SetCarriedRune("rune_war_seal");
			await Wait(0.05);
			List<string> war = RollAll(pool);
			Check(war.Contains("probe_war_gated"), "⑥ ⭐ 携带战神印：`Requires = [战神刻印]` 的增幅进了候选池");
			Check(!war.Contains("probe_lei_gated"), "⑥ 携带战神印：`Requires = [雷之印]` 的那条仍不进");
			Check(war.Count == none.Count + 1, $"⑥ 携带战神印让可用候选 {none.Count} → {war.Count}（正好多 1 条）");

			_reportRuneUnlockSummary = $"不携带 {none.Count} 条 → 雷之印 {lei.Count} 条（多 {string.Join(",", lei.Except(none))}）→ 战神印 {war.Count} 条（多 {string.Join(",", war.Except(none))}）";
			GD.Print($"[RuneProbe] ⭐ ⑥ 前置解锁实测：{_reportRuneUnlockSummary}");
		}
		finally
		{
			RewardCatalog.UseTable(original);
		}

		_checkCatalogRestored = RewardCatalog.All.Length;
		Check(Mathf.IsEqualApprox(_checkCatalogRestored, _baselineCatalogLength),
			$"⑥ 探针结束后运行期目录恢复成 {RewardCatalog.All.Length} 条（期望 {_baselineCatalogLength}，没有残留探针行）");

		_run.SetCarriedRune(string.Empty);
		await Wait(0.1);
	}

	private string _reportRuneUnlockSummary = string.Empty;
	private float _checkCatalogRestored;

	private static RewardDefinition MakeProbeRow(string requires, string id)
		=> new()
		{
			Id = id,
			Name = $"（探针）{requires} 限定",
			Description = $"J11 探针合成行：Requires = [{requires}]",
			Kind = RewardKind.Mutation,
			Requires = new[] { requires },
			MaxStacks = 1,
			Weight = 1f,
			Apply = _ => { },
		};

	/// <summary>
	/// 用**真实**的 `RewardPool.Roll` 把"这一局当前可抽到的全部候选"列出来（count = 999 = 要全部）。
	/// ⚠️ 必须传 `_run.Build` —— 符文解锁的前置就在那个构筑实例上（传一个新的 `RunBuild` 等于"没带符文"）。
	/// </summary>
	private List<string> RollAll(RewardPool pool)
		=> pool.Roll(_run.Build, 999).ConvertAll(d => d.Id);

	// ── ⑦ 带行为的 5 枚符文 ────────────────────────────────────────────────
	private async Task BehaviorRunes()
	{
		// ⑦-1 狙神（按距离加伤）
		_run.SetCarriedRune("rune_sniper");
		await Wait(0.1);
		var sniper = _player.GetNodeOrNull<RuneDistanceDamageRule>("Rune_rune_sniper_DistanceDamage");
		Check(sniper != null, $"⑦ 狙神：按距离加伤规则挂上了（{(sniper == null ? "没找到" : sniper.DebugState())}）");
		if (sniper != null)
		{
			Unit near = MakeTarget("SniperNear", Vector3.Zero, 100000f);
			Unit mid = MakeTarget("SniperMid", Vector3.Zero, 100000f);
			Unit far = MakeTarget("SniperFar", Vector3.Zero, 100000f);
			await Wait(0.05);
			near.GlobalPosition = _player.GlobalPosition + new Vector3(1.5f, 0f, 0f);
			mid.GlobalPosition = _player.GlobalPosition + new Vector3(9f, 0f, 0f);
			far.GlobalPosition = _player.GlobalPosition + new Vector3(20f, 0f, 0f);
			await Wait(0.05);
			var attack = new AttackInstance { Attacker = _player, Ability = "probe_sniper", BaseDamage = 100f, CanCrit = false };
			float nearScale = sniper.ScaleAgainst(near, attack);
			float midScale = sniper.ScaleAgainst(mid, attack);
			float farScale = sniper.ScaleAgainst(far, attack);
			Check(Mathf.IsEqualApprox(nearScale, 0.5f), $"⑦ 狙神：1.5m → ×{nearScale:0.###}（期望 ×0.5 = 文档「近距离只有 50%」）");
			Check(midScale > 0.9f && midScale < 1.1f, $"⑦ 狙神：9m → ×{midScale:0.###}（期望 ≈×1.0，线性插值中点）");
			Check(Mathf.IsEqualApprox(farScale, 1.5f), $"⑦ 狙神：20m → ×{farScale:0.###}（期望 ×1.5 = 远端预设值）");
			near.QueueFree();
			mid.QueueFree();
			far.QueueFree();
		}

		// ⑦-2 巨人（按最大生命值加伤，走真实 RollHit）
		_run.SetCarriedRune("rune_giant");
		await Wait(0.1);
		var giant = _player.GetNodeOrNull<RuneMaxHealthDamageRule>("Rune_rune_giant_MaxHealthDamage");
		Check(giant != null, $"⑦ 巨人：按最大生命值加伤规则挂上了（{(giant == null ? "没找到" : giant.DebugState())}）");
		if (giant != null)
		{
			Unit target = MakeTarget("GiantTarget", _player.GlobalPosition + new Vector3(0f, 0f, 4f), 100000f);
			await Wait(0.05);
			var attack = new AttackInstance { Attacker = _player, Ability = "probe_giant", BaseDamage = 10f, CanCrit = false };
			HitInstance hit = attack.RollHit(target, Vector3.Forward, target.GlobalPosition);
			float expected = 10f + (_player.MaxHealth * 0.1f);
			Check(Mathf.IsEqualApprox(hit.Damage, expected),
				$"⑦ 巨人：`RollHit` 基础 10 点的这一击 → 伤害 {hit.Damage:0.##}（期望 {expected:0.##} = 10 + 最大生命值 {_player.MaxHealth:0} × 10%）；规则读数：{giant.DebugState()}");
			target.QueueFree();
		}

		// ⑦-3 生死（致命伤无效化 + 恢复 30%）
		_run.SetCarriedRune("rune_life_death");
		await Wait(0.1);
		var avoid = _player.GetNodeOrNull<RuneDeathAvoidRule>("Rune_rune_life_death_DeathAvoid");
		Check(avoid != null && _player.DeathAvoidRuleCount == 1,
			$"⑦ 生死：死亡避免规则挂上了（{(avoid == null ? "没找到" : avoid.DebugState())}；玩家身上 {_player.DeathAvoidRuleCount} 条）");
		_checkAvoidCharges = avoid?.Charges ?? -1;
		Check(_checkAvoidCharges == 1, $"⑦ 生死：剩余次数 {_checkAvoidCharges}（期望 1 —— 文档写 {{n}} 未给数，预设 1）");

		// 机制本体在一个**独立靶子**上量（玩家真的会死，会把 RunController 拖进结算，污染后面的读数）。
		var rig = new Node3D { Name = "DeathAvoidRig" };
		AddChild(rig);
		var dummy = new Unit { Name = "AvoidDummy", Faction = Faction.Neutral, MaxHealth = 100f, StartingArmor = 0f };
		dummy.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.4f } });
		rig.AddChild(dummy);
		dummy.GlobalPosition = new Vector3(60f, 1f, 60f);
		var dummyAvoid = new RuneDeathAvoidRule { Name = "DummyAvoid", Charges = 1, HealFraction = 0.3f };
		dummy.AddChild(dummyAvoid);
		await Wait(0.1);
		float lethal = dummy.ApplyDirectDamage(999f);
		Check(!dummy.IsDead && Mathf.IsEqualApprox(dummy.Health, 30f),
			$"⑦ 生死：999 点致命伤 → 无效化（未死={!dummy.IsDead}），生命恢复到 {dummy.Health:0.##}（期望 30 = 100 × 30%），这次扣血读数 {lethal:0.##}");
		Check(dummyAvoid.Charges == 0, $"⑦ 生死：次数用完（剩余 {dummyAvoid.Charges}）");
		float second = dummy.ApplyDirectDamage(999f);
		Check(dummy.IsDead, $"⑦ 生死：次数用完后再吃致命伤就真的死了（这次扣血 {second:0.##}）");
		rig.QueueFree();
		await Wait(0.05);

		// ⑦-4 小精灵（绕行 + 每 5 秒打一次）
		_run.SetCarriedRune("rune_pixie");
		await Wait(0.1);
		var pixie = _player.GetNodeOrNull<PixieCompanion>("Rune_rune_pixie_Pixie");
		Check(pixie != null, $"⑦ 小精灵：实体挂上了（{(pixie == null ? "没找到" : pixie.DebugState())}）");
		if (pixie != null)
		{
			Check(!pixie.IsInGroup("player_units") && !pixie.IsInGroup("enemy_units") && !pixie.IsInGroup("summon_units"),
				"⑦ 小精灵：不在任何单位分组里（清场 / 目标选择 / 单位计数都碰不到它）");
			Check(pixie.GetChildren().OfType<CollisionObject3D>().Count() == 0, "⑦ 小精灵：子树 0 个物理体（只是一颗占位小球）");

			Unit prey = MakeTarget("PixiePrey", _player.GlobalPosition + new Vector3(2f, 0f, 0f), 100000f);
			await Wait(0.1);
			float before = prey.Health;
			bool struck = await Until(() => pixie.AttackTally > 0, 7.5);
			float lost = before - prey.Health;
			Check(struck, $"⑦ 小精灵：7.5 秒内真的出手了一次（间隔表值 5 秒）→ 第 {pixie.AttackTally} 次，目标 {pixie.LastTargetName}，伤害 {lost:0.##}（期望 ≈20）；规则读数：{pixie.DebugState()}");
			Check(Mathf.Abs(lost - 20f) < 0.01f, $"⑦ 小精灵：这一次扣了目标 {lost:0.##} 点生命（期望 20 = 表值）");
			prey.QueueFree();
		}

		_run.SetCarriedRune(string.Empty);
		Refill();
		await Wait(0.1);
	}

	private int _checkAvoidCharges;

	// ── ⑧ 摘下符文后回到逐位不变 ────────────────────────────────────────────
	private async Task RestoreToBaseline()
	{
		Check(_run.CarriedRuneId.Length == 0, $"⑧ 结束时 CarriedRuneId「{_run.CarriedRuneId}」（期望空）");
		Check(Mathf.IsEqualApprox(_player.MaxHealth, _baselineHealth), $"⑧ 生命上限 {_player.MaxHealth:0}（期望回到 {_baselineHealth}）");
		Check(Mathf.IsEqualApprox(_player.Stats.Get("IncomingDamageMultiplier"), 1f), $"⑧ 受击倍率 ×{_player.Stats.Get("IncomingDamageMultiplier"):0.###}（期望 1）");
		Check(Mathf.IsEqualApprox(_player.Stats.Get("MoveSpeedMultiplier"), 1f), $"⑧ 移速 ×{_player.Stats.Get("MoveSpeedMultiplier"):0.###}（期望 1）");
		Check(Mathf.IsEqualApprox(_player.Stats.Get("DamageMultiplier"), 1f), $"⑧ 伤害 ×{_player.Stats.Get("DamageMultiplier"):0.###}（期望 1）");
		Check(Mathf.IsEqualApprox(_player.Stats.Get("MaxHealthCap"), 0f), $"⑧ 生命夹子 {_player.Stats.Get("MaxHealthCap"):0.##}（期望 0）");
		Check(CountRuneNodes() == 0, $"⑧ 符文规则节点 {CountRuneNodes()} 个（期望 0）");
		Check(_player.DeathAvoidRuleCount == 0, $"⑧ 死亡避免规则 {_player.DeathAvoidRuleCount} 条（期望 0）");

		float hit = TakeCleanHit(10f);
		Check(Mathf.IsEqualApprox(hit, _baselineHit), $"⑧ 再吃一次同样的 10 点伤害 → 最终 {hit:0.##}（期望与基线 {_baselineHit:0.##} 逐位相等）");

		var pool = new RewardPool();
		pool.Initialize(_weapon, "chr_hunter");
		Check(pool.CandidateCount == _baselineCandidates, $"⑧ 候选池回到 {pool.CandidateCount} 条（期望基线 {_baselineCandidates}）");
		Check(RewardCatalog.All.Length == 70, $"⑧ 增幅项目录仍是 {RewardCatalog.All.Length} 条（期望 J24后70，符文不混入）");

		Check(RuneCarry.Current.Length == 0, $"⑧ 存档层 RuneCarry.Current 为空（自测内存模式，不写 user://rune.cfg）");
		await Wait(0.05);
	}

	// ── 工具 ──────────────────────────────────────────────────────────────
	/// <summary>打一次干净的量测：先把血/甲回满、再受一次固定伤害，返回 <c>FinalDamage</c>。</summary>
	private float TakeCleanHit(float amount)
	{
		Refill();
		DamageResult result = _player.ReceiveHit(new HitInstance
		{
			Target = _player,
			Attacker = null,
			Context = EventContext.Create(null, _player),
			Damage = amount,
			Direction = Vector3.Up,
			Position = _player.GlobalPosition,
		});
		return result.FinalDamage;
	}

	/// <summary>回满生命/护甲/护盾（走真实装备应用器，与开局同一条路）。</summary>
	private void Refill()
	{
		_loadout.Apply(_player, this, PlayerCharacterKind.Hunter, WeaponPresetKind.Bow);
		// ⚠️ 装备应用器会 `Stats.RemoveBySource(owner)`，符文修正器的 Source 是 RunController 的
		// `_runeBuild`（不是 owner），所以它们还在；但生命已被回满 —— 带「雷之印」时再夹一次才准。
		_player.RefreshStats();
	}

	private int CountRuneNodes()
		=> _player.GetChildren().Count(child => child.Name.ToString().StartsWith("Rune_", StringComparison.Ordinal));

	private Unit MakeTarget(string name, Vector3 position, float health)
	{
		var unit = new Unit
		{
			Name = name,
			Faction = Faction.Enemy,
			MaxHealth = health,
			KnockbackResistance = 1f,
		};
		unit.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.5f } });
		AddChild(unit);
		unit.GlobalPosition = position;
		return unit;
	}

	private void Check(bool ok, string text)
	{
		if (ok)
		{
			_pass++;
		}
		else
		{
			_fail++;
		}

		GD.Print($"[RuneProbe] {(ok ? "PASS" : "FAIL")} {text}");
	}

	private async Task Wait(double seconds)
		=> await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

	private async Task<bool> Until(Func<bool> predicate, double seconds)
	{
		ulong start = Time.GetTicksMsec();
		while (!predicate() && Time.GetTicksMsec() - start < (ulong)(seconds * 1000))
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		return predicate();
	}
#else
	// Release（ExportRelease）构建里整套探针不参与编译：`RuneCarry.UseInMemoryStoreForSelfTest`
	// 与 `MetaProgress.UseInMemoryStoreForSelfTest` 一样只在 `#if DEBUG` 下存在
	// （与 `SelfTest` / `_FeelProbe` 同一条纪律）。
	public override void _Ready() => SetProcess(false);
#endif
}
