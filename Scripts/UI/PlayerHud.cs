using ActRoguelike.Boss;
using ActRoguelike.Combat;
using ActRoguelike.Encounter;
using ActRoguelike.Interaction;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.Core;
using System;
using System.Collections.Generic;
using Godot;

namespace ActRoguelike.UI;

/// <summary>最小 HUD：玩家生命条/数值、准星、弹药、节点进度与构筑。只读 Gameplay 状态。</summary>
public partial class PlayerHud : CanvasLayer
{
	/// <summary>玩家血条预制体：由 Hud.tscn 实例化（节点名 PlayerBar），代码不建它。</summary>
	private const string PlayerBarNode = "Root/PlayerBar";

	/// <summary>首领血条预制体：代码实例化一份挂到 HUD 层上。</summary>
	private static readonly PackedScene BossBarScene = GD.Load<PackedScene>("res://Scenes/UI/Bars/BarBoss.tscn");

	/// <summary>延迟条跟随速度（比例/秒）。</summary>
	private const float DelayFollowSpeed = 0.6f;

	/// <summary>血条栏上最多显示几个**不同**的首领强化条目（技能 + 词条；超出的折叠成 "…+N"）—— 多了不能把血条挤爆。</summary>
	private const int BossBarPerkLimit = 5;

	/// <summary>顶部那一行状态文字更窄，强化条目上限再收一档。</summary>
	private const int BossStatusPerkLimit = 4;

	/// <summary>交互提示里顺带写出的「首领状况 / 角色持有 / 设置」快捷键（取自各自界面的默认键，避免两处漂移）。</summary>
	private static readonly string BossPreviewHint =
		$"[{OS.GetKeycodeString(BossPreviewScreen.DefaultToggleKey)} 首领状况] " +
		$"[{OS.GetKeycodeString(BuildScreen.DefaultToggleKey)} 角色持有] " +
		$"[{OS.GetKeycodeString(Key.O)} 设置]";

	private Label _health = null!;
	private Label _status = null!;
	private Control _root = null!;
	/// <summary>首领血条是否展开为完整形态（验证用只读状态）。</summary>
	public bool BossBarExpanded { get; private set; }

	/// <summary>与首领的当前距离（验证用）。</summary>
	public float BossDistance { get; private set; }

	private Control? _bossPanel;
	private ProgressBar _bossBar = null!;
	private Label _bossName = null!;
	private Label _bossState = null!;

	/// <summary>血条栏上的首领强化行（**技能** ◆ + **词条** ◇，见 <see cref="BossPerkText"/>）。</summary>
	private Label _bossPerks = null!;
	private Label _build = null!;
	private HBoxContainer _buildIcons = null!;
	private HealthBarWidget _healthBar = null!;
	private Unit? _player;
	private EncounterController? _encounter;
	private RunController? _run;
	private StageController? _stage;
	private float _healthRatio = 1f;

	public override void _Ready()
	{
		_health = GetNode<Label>("Root/Health");
		_status = GetNode<Label>("Root/Status");
		_build = GetNode<Label>("Root/Build");
		_buildIcons = GetNode<HBoxContainer>("Root/BuildIcons");
		_root = GetNode<Control>("Root");

		// 血条只按名字取节点、只当 Control 用 —— 美术可以把填充换成贴图/NinePatch，类型不再被写死。
		_healthBar = HealthBarWidget.Bind(GetNode<Control>(PlayerBarNode));
	}

	public override void _ExitTree() => BindPlayer(null);

	private float _hudAccum;
	private string _lastStatus = string.Empty;
	private string _lastVitals = string.Empty;

	/// <summary>构筑图标条当前的内容签名（只有变了才重建节点）。</summary>
	private string _buildSignature = string.Empty;

	/// <summary>拍图 / 探针塞了示例图标（这时不再被真实构筑状态覆盖）。</summary>
	private bool _debugSampleBuild;

	/// <summary>HUD 上画了几个构筑图标（验证用只读属性）。</summary>
	public int BuildIconCount => _buildIcons != null && IsInstanceValid(_buildIcons) ? _buildIcons.GetChildCount() : 0;

	public override void _Process(double delta)
	{
		// HUD 不需要 60Hz：20Hz 足够，文本只在变化时写（Godot 的 Text setter 有开销）。
		_hudAccum += (float)delta;
		if (_hudAccum < 0.05f)
		{
			return;
		}

		_hudAccum = 0f;

		if (_player == null || !IsInstanceValid(_player))
		{
			_player = PlayerLocator.Player;
			BindPlayer(_player);
		}

		if (_player == null)
		{
			return;
		}

		// 关卡 / 遭遇 / 一局的引用先解析：首领血条栏（含词条）与下面几行都要用它们。
		_encounter ??= GetTree().GetFirstNodeInGroup("encounters") as EncounterController;
		_run ??= GetTree().GetFirstNodeInGroup("run_controller") as RunController;
		_stage ??= GetTree().GetFirstNodeInGroup("stage") as StageController;
		_encounter = _stage?.CurrentNode ?? _encounter;

		// 首领血条面板：放在所有分支之前，保证任何状态下都会更新。
		if (_bossPanel == null)
		{
			BuildBossPanel();
		}

		UpdateBossPanel();

		string extra = string.Empty;
		if (_player.Armor > 0f)
		{
			extra += $"  甲 {_player.Armor:0}";
		}

		if (_player.Shield > 0f)
		{
			extra += $"  盾 {_player.Shield:0}";
		}

		_health.Text = $"HP {_player.Health:0}/{_player.MaxHealth:0}{extra}";

		UpdateHealthBar(Mathf.Clamp(_player.Health / Mathf.Max(1f, _player.MaxHealth), 0f, 1f));

		// 护甲进度（用户需求 §五 #8）+ 恢复跳字（§五 #9）：都挂在 HUD 每 0.05 秒的那一次刷新里。
		UpdateArmorBar(_player);
		MaybeShowHealNumber(_player);

		// ⚠️ P6b：**弹药显示整条删除**（用户 2026-10-12 裁定「没有弹匣的概念了」）——
		// 原来这里写的是 `装填中…` / `Ammo / EffectiveMagazineSize`（屏幕右下角那个 `1 / 1`），
		// 配套的 `Root/Ammo` Label 节点也已从 `Hud.tscn` 删除。

		string prompt = PlayerInteractInput.Prompt;

		// 站在可交互物旁边（= "交互时"）顺带提示首领状况 / 角色持有 / 设置的快捷键。
		string interactHint = prompt.Length > 0 ? $"   {BossPreviewHint}" : string.Empty;
		_build.Text = prompt.Length > 0 ? $"{prompt}{interactHint}" : string.Empty;

		// 构筑**不再写成一串文字**（用户需求：构筑元素都用图标）：
		// HUD 只放一行小图标，名字与说明去 C 面板（那里鼠标能用，悬停才有意义）。
		RefreshBuildIcons(_run?.Build);

		if (_player.IsDead)
		{
			_status.Text = "已阵亡";
		}
		else if (_encounter?.Boss is { } boss && IsInstanceValid(boss) && !boss.IsDead)
		{
			var phases = boss.GetNodeOrNull<BossPhaseController>("Phases");
			var pressure = boss.GetNodeOrNull<BossPressure>("Pressure");

			// 近距离（血条面板展开）时强化条目由**面板那一条**负责显示，这里不再重复 —— 否则同一串名字出现两次。
			string perkLine = BossBarExpanded ? string.Empty : PerkLine(BossStatusPerkLimit);
			string perks = perkLine.Length > 0 ? $"   {perkLine}" : string.Empty;
			_status.Text = $"BOSS {boss.Health:0}/{boss.MaxHealth:0}   阶段 {(phases?.Phase ?? 0) + 1}   压力 {pressure?.Level ?? 0}{perks}";
		}
		else if (_encounter == null)
		{
			_status.Text = string.Empty;
		}
		else
		{
			// 现在是自由探索：显示关卡、剩余节点与隔离次数（策划文档 §6.2/§6.3）。
			string stageInfo = _stage != null
				? $"{_stage.Definition?.DisplayName ?? "Stage"}   " +
				  $"剩余节点 {_stage.RemainingNodes}   隔离次数 {_stage.FreeQuotaLeft}   用时 {_stage.ElapsedSeconds:0}s   "
				: string.Empty;

			// 召唤物数量（角色 3 的召唤与控制，策划文档 §7.3/§13）。
			int summons = GetTree().GetNodesInGroup("summon_units").Count;
			string summonInfo = summons > 0 ? $"召唤 {summons}   " : string.Empty;
			_status.Text = $"{stageInfo}{summonInfo}敌人 {_encounter.AliveCount}";
		}
	}

	// === 构筑图标条（用户需求：「构筑元素都用图标」「说明只在选取界面和鼠标悬停时显示」）===

	/// <summary>
	/// 本局构筑在 HUD 上只放**一行图标**（不写名字）：名字与说明在 <see cref="BuildScreen"/>（快捷键 C）里，
	/// 那里界面是暂停的、鼠标是放开的，悬停提示才有意义。
	/// 内容签名没变就不重建节点（HUD 每 0.05 秒跑一次，重建节点太贵）。
	/// </summary>
	private void RefreshBuildIcons(RunBuild? build)
	{
		if (_debugSampleBuild)
		{
			return;   // 拍图 / 探针塞的示例图标，不要被真实状态覆盖
		}

		string signature = BuildSignature(build);
		if (signature == _buildSignature)
		{
			return;
		}

		_buildSignature = signature;
		foreach (Node child in _buildIcons.GetChildren())
		{
			_buildIcons.RemoveChild(child);
			child.QueueFree();
		}

		if (build == null)
		{
			return;
		}

		foreach (RewardDefinition definition in build.Acquired)
		{
			_buildIcons.AddChild(BuildIcon(definition, build.StacksOf(definition.Id)));
		}
	}

	private static string BuildSignature(RunBuild? build)
	{
		if (build == null || build.Acquired.Count == 0)
		{
			return string.Empty;
		}

		var parts = new List<string>(build.Acquired.Count);
		foreach (RewardDefinition definition in build.Acquired)
		{
			parts.Add($"{definition.Id}×{build.StacksOf(definition.Id)}");
		}

		return string.Join("|", parts);
	}

	/// <summary>一格：图标 + 叠层数（与 <see cref="BuildScreen"/> 同一套图标表）。</summary>
	private static Control BuildIcon(RewardDefinition definition, int stacks)	{
		var tile = new PanelContainer
		{
			ThemeTypeVariation = "UiTile",
			CustomMinimumSize = new Vector2(44, 44),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

		var icon = new TextureRect
		{
			Texture = UiIcons.ForReward(definition),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

		var count = new Label
		{
			Text = stacks > 1 ? $"×{stacks}" : string.Empty,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Bottom,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

		// ⚠️ 外层是普通 Control（不是容器）：子节点**必须自己锚满**，否则尺寸是 0×0 → 图标看不见
		//（踩过一次：HUD 上只看到 6 个空底板）。
		var stack = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		stack.AddChild(icon);
		stack.AddChild(count);
		icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		count.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		tile.AddChild(stack);
		return tile;
	}

	/// <summary>
	/// 验证 / 拍图用：用**真实的奖励目录**填几个构筑图标（不依赖本局状态）。
	/// 拍摄台拿它拍到"HUD 上的构筑图标条长什么样"，不需要真去打通一局。
	/// </summary>
	public void DebugSetSampleBuildIcons(int count = 6)
	{
		_debugSampleBuild = true;
		foreach (Node child in _buildIcons.GetChildren())
		{
			_buildIcons.RemoveChild(child);
			child.QueueFree();
		}

		RewardDefinition[] all = RewardCatalog.All;
		int shown = Mathf.Min(count, all.Length);
		for (int i = 0; i < shown; i++)
		{
			_buildIcons.AddChild(BuildIcon(all[i], i % 3 == 0 ? 2 : 1));
		}
	}

	// === 首领血条（用户需求：唯一的首领不用头顶血条，改在屏幕上方；近距离展开完整血条，远距离只留头像） ===

	/// <summary>实例化 BarBoss 预制体。位置/尺寸/头像/字号都在场景里，这里只按名字抓节点。</summary>
	private void BuildBossPanel()
	{
		Control panel = BossBarScene.Instantiate<Control>();

		// 预制体的根节点名是 BarBoss，挂到 HUD 上时沿用旧代码的节点名 BossPanel（树结构不变）。
		panel.Name = "BossPanel";

		// 按名字取一次并缓存（节点名改了要同步这里，别让它静默失效）。
		_bossBar = RequireChild<ProgressBar>(panel, "Panel/Bar");
		_bossName = RequireChild<Label>(panel, "Panel/NameLabel");
		_bossState = RequireChild<Label>(panel, "Panel/StateLabel");
		_bossPerks = RequireChild<Label>(panel, "Panel/PerkLabel");

		// ⚠️ 预制体里的 `Panel` 自带 `visible = false`（自首个提交起），而代码只切根节点的可见性 ——
		// 于是头像/名字/血条/强化行**从来没有显示过**（只有 BossBarExpanded / BossDistance 两个只读属性在动）。
		// 这里明确打开它：开合由根节点（_bossPanel.Visible）负责，内部 Panel 常显。
		RequireChild<Control>(panel, "Panel").Visible = true;

		_bossPanel = panel;
		AddChild(_bossPanel);
	}

	private static T RequireChild<T>(Node parent, string path)
		where T : Node
		=> parent.GetNodeOrNull<T>(path) ?? throw new System.InvalidOperationException($"Boss 血条预制体缺少 {path} 节点");

	/// <summary>数值更新：只改填充比例，不碰预制体给的宽度/位置。</summary>
	private void UpdateHealthBar(float ratio)
	{
		bool hit = ratio < _healthRatio;
		_healthRatio = ratio;
		_healthBar.SetRatio(ratio, HealthColor(ratio), hit);

		// 表现更新：残影条跟随（独立方法，以后换成动画只改这里）。
		_healthBar.AdvanceDelay(ratio, DelayFollowSpeed, (float)GetProcessDeltaTime());
	}

	/// <summary>玩家血量配色：绿 → 黄 → 红。换配色改这里（或改成按预制体主题色）。</summary>
	private static Color HealthColor(float ratio)
		=> ratio > 0.5f ? new Color(0.4f, 0.85f, 0.4f) : ratio > 0.25f ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.35f, 0.3f);

	// === 护甲进度 / 碎甲 / 恢复（用户需求 §五 #8 与 #9）===

	/// <summary>当前 HUD 绑定的玩家（<see cref="Unit.ArmorBroke"/> 是 C# 事件，退订要还同一批委托）。</summary>
	private Unit? _boundPlayer;

	private Action? _armorBroke;

	/// <summary>上一次刷新的生命值（用来发现"恢复"——<c>Unit.Heal</c> 不发任何事件，见 <see cref="DamageNumbers"/>）。</summary>
	private float _lastHealth;

	/// <summary>玩家引用变化时成对订阅 / 退订（HUD 每 0.05 秒跑一次，这里只在真的换了人才动）。</summary>
	private void BindPlayer(Unit? player)
	{
		if (ReferenceEquals(player, _boundPlayer))
		{
			return;
		}

		if (_boundPlayer != null && IsInstanceValid(_boundPlayer) && _armorBroke != null)
		{
			_boundPlayer.ArmorBroke -= _armorBroke;
		}

		_boundPlayer = player;
		_armorBroke = null;
		_lastHealth = player != null && IsInstanceValid(player) ? player.Health : 0f;

		if (player == null || !IsInstanceValid(player))
		{
			return;
		}

		_armorBroke = () => OnPlayerArmorBroke(player);
		player.ArmorBroke += _armorBroke;
	}

	/// <summary>
	/// 护甲进度（用户 A6 + C1）：判据是 **<c>Armor &gt; 0</c>**（有甲才罩那层塑料），
	/// 甲打光（<c>Armor == 0</c>）**连空壳都不留** —— 用户 C1 拍板："有甲了才会显示"、"护甲没有上限额度"。
	///
	/// 比例的分母是**运行期派生的 <see cref="Unit.ArmorPeak"/>**（这个单位见过的最高甲），
	/// 不是配置（<c>MaxArmor</c> 已按 C1 删除）。
	/// </summary>
	private void UpdateArmorBar(Unit player)
		=> _healthBar.SetArmor(
			player.Armor > 0f ? Mathf.Clamp(player.Armor / Mathf.Max(1f, player.ArmorPeak), 0f, 1f) : 0f,
			player.Armor > 0f);

	/// <summary>碎甲：那层塑料闪白 + 玩家模型上炸一圈（<see cref="Unit.ArmorBroke"/> 只在甲从 &gt;0 掉到 0 时触发）。</summary>
	private void OnPlayerArmorBroke(Unit player)
	{
		_healthBar.FlashArmorBreak();
		ArmorBreakFx.Spawn(this, player.GlobalPosition);
	}

	/// <summary>
	/// 恢复跳字（§五 #9 的「恢复」）：全项目只有玩家会回血，而 <c>Unit.Heal</c> 不发事件
	/// （Combat 本轮只读，不能加钩子），所以在这里用"血量比上次刷新时高了多少"来发现它。
	/// </summary>
	private void MaybeShowHealNumber(Unit player)
	{
		float gained = player.Health - _lastHealth;
		_lastHealth = player.Health;

		if (gained <= 0.5f || !ActRoguelike.Config.ConfigIndex.Settings.ShowDamageNumbers)
		{
			return;
		}

		if (GetViewport().GetCamera3D() is not { } camera)
		{
			return;
		}

		Vector3 world = player.GlobalPosition + (Vector3.Up * 1.5f);
		if (camera.IsPositionBehind(world))
		{
			return;
		}

		ActRoguelike.Config.ConfigIndexData config = ActRoguelike.Config.ConfigIndex.Settings;
		DamageNumbers.ShowHeal(_root, camera.UnprojectPosition(world), gained,
			config.DamageNumberFontSize, config.DamageNumberRise, config.DamageNumberLifetime);
	}

	private void UpdateBossPanel()
	{
		var boss = GetTree().GetFirstNodeInGroup("boss_units") as Unit;
		if (boss == null || !IsInstanceValid(boss) || boss.IsDead)
		{
			_bossPanel!.Visible = false;
			BossBarExpanded = false;
			return;
		}

		// 只在玩家**靠近首领**时显示整个面板（含头像）。
		// 用户反馈：远离时留一个头像会在打小怪时造成"到处都是首领"的错觉。
		var player = GetTree().GetFirstNodeInGroup("player_units") as Unit;
		float distance = player != null && IsInstanceValid(player) ? player.GlobalPosition.DistanceTo(boss.GlobalPosition) : 0f;
		bool near = distance <= ActRoguelike.Config.ConfigIndex.Settings.BossBarFullRange;
		BossBarExpanded = near;
		BossDistance = distance;
		_bossPanel!.Visible = near;
		if (!near)
		{
			return;
		}

		// 数值更新（与表现分开：以后加动画只动这里，不动可见性与距离逻辑）。
		_bossBar.Value = boss.MaxHealth > 0f ? boss.Health / boss.MaxHealth : 0f;
		_bossName.Text = $"{boss.Name}   {boss.Health:0} / {boss.MaxHealth:0}";
		_bossState.Text = $"{_stage?.Definition?.DisplayName}   距离 {distance:0}";

		// 强化行：技能（Boss Build）+ 词条（Pressure）都要显示，并带标记区分来源（§7.1）。
		string perkLine = PerkLine(BossBarPerkLimit);
		_bossPerks.Text = perkLine;
		_bossPerks.Visible = perkLine.Length > 0;
	}

	/// <summary>
	/// 本关首领强化的一行文案 —— **两套来源都显示**，而且**叫法分开**（用户拍板：行为型是技能、数值型才是词条）：
	/// 技能来自这一场遭遇（<see cref="EncounterController.BossPerks"/>，Boss 出生时抽的 Boss Build 技能/行为），
	/// 词条来自关卡（<see cref="StageController.PressurePerks"/>，Pressure 阈值转换抽到的数值强化）。
	/// 计数名词、标记与折叠规则都在 <see cref="BossPerkText"/> 里（HUD 与预览面板共用一份）。
	/// </summary>
	private string PerkLine(int limit)
	{
		IReadOnlyList<BossPerk>? skills = _encounter?.BossPerks;
		IReadOnlyList<BossPerk>? entries = _stage?.PressurePerks;
		string list = BossPerkText.FormatLine(skills, entries, limit);
		return list.Length == 0 ? string.Empty : $"{BossPerkText.Summary(skills, entries)} {list}";
	}
}