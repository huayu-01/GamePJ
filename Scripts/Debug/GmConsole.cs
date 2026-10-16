using System;
using System.Collections.Generic;
using System.Globalization;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Interaction;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Debug;

/// <summary>可插拔开发控制台：输入、UI和命令均随本节点生命周期存在，不注册autoload或InputMap。</summary>
public partial class GmConsole : OverlayScreen
{
	[Export] public bool Enabled { get; set; } = true;
	protected override Control Root => GetNode<Control>("Root");
	public bool Available { get; private set; }
	public bool GodMode { get; private set; }
	public bool LastCommandSucceeded { get; private set; }
	public string LastResult { get; private set; } = string.Empty;
	public int CommandCount { get; private set; }
	private LineEdit _input = null!;
	private RichTextLabel _output = null!;
	private readonly List<string> _history = new();
	private readonly Queue<string> _lines = new();
	private int _historyIndex;
	private Unit? _godPlayer;
	private bool _previousInvulnerability;
	private static bool DevelopmentBuild
	{
		get
		{
#if DEBUG
			return true;
#else
			return false;
#endif
		}
	}
	public override void _Ready()
	{
		Available = Enabled && DevelopmentBuild && OS.IsDebugBuild();
		if (!Available)
		{
			SetProcessInput(false);
			QueueFree();
			return;
		}
		_input = GetNode<LineEdit>("Root/Panel/Margin/Layout/Command/Input");
		_input.KeepEditingOnTextSubmit = true;
		_output = GetNode<RichTextLabel>("Root/Panel/Margin/Layout/Output");
		_input.TextSubmitted += Submit;
		_input.GuiInput += InputKeys;
		GetNode<Button>("Root/Panel/Margin/Layout/Command/Run").Pressed += () => Submit(_input.Text);
		GetNode<Button>("Root/Panel/Margin/Layout/Header/Close").Pressed += Close;
		BindQuick("Resources", "resource 100");
		BindQuick("Heal", "heal");
		BindQuick("Refill", "refill");
		BindQuick("God", "invincible");
		BindQuick("Skills", "list skills");
		BindQuick("Upgrades", "list upgrades");
		BindQuick("Enemies", "list enemies");
		BindQuick("Interactions", "list interactions");
		InitializeOverlay();
		SetKeyHints(KeyHint.Text("Enter", "执行"), KeyHint.Text("Tab", "补全"), KeyHint.Text("↑ / ↓", "历史"), (Key.Escape, "关闭"));
		Log("GM 测试台就绪 · G 打开 / Esc 关闭。输入 help 查看命令，list 可查询当前表中的真实ID。");
		Log("碎片会保存到玩家进度；增幅、CD倍率、层数等只影响当前实例。give 为GM直接发放，跳过抽取限定。");
	}
	private void BindQuick(string node, string command)
		=> GetNode<Button>($"Root/Panel/Margin/Layout/Quick/{node}").Pressed += () => Submit(command);
	public override void _Input(InputEvent @event)
	{
		if (!Available || @event is not InputEventKey { Pressed: true, Echo: false } key
			|| key.CtrlPressed || key.AltPressed || key.MetaPressed) return;
		if (IsOpen)
		{
			// 先于LineEdit处理Esc，避免第一次Esc仅退出文本编辑、第二次才关控制台。
			if (key.Keycode == Key.Escape || key.PhysicalKeycode == Key.Escape)
			{
				if (!InputBlocked) Close();
				GetViewport().SetInputAsHandled();
			}
			return;
		}
		if (key.PhysicalKeycode != Key.G && key.Keycode != Key.G) return;
		Open();
		GetViewport().SetInputAsHandled();
	}
	public void Open()
	{
		if (!Available || IsOpen) return;
		BeginOpen();
		_input.Clear();
		_input.GrabFocus();
		_input.Edit();
		_historyIndex = _history.Count;
	}
	public override void Close()
	{
		if (!Available || !IsOpen) return;
		_input.ReleaseFocus();
		BeginClose();
	}
	public override void _PhysicsProcess(double delta)
	{
		if (!GodMode) return;
		Unit? player = PlayerLocator.Player;
		if (!ReferenceEquals(player, _godPlayer)) BindGodPlayer(player);
		if (_godPlayer != null && IsInstanceValid(_godPlayer)) _godPlayer.IsInvulnerable = true;
	}
	public override void _ExitTree()
	{
		UnbindGodPlayer();
		if (Available) UiRouter.Instance?.Pop(this);
	}
	private void BindGodPlayer(Unit? player)
	{
		UnbindGodPlayer();
		_godPlayer = player;
		if (player == null) return;
		_previousInvulnerability = player.IsInvulnerable;
		player.IsInvulnerable = true;
		player.HitReceived += KeepGodMode;
	}
	private void UnbindGodPlayer()
	{
		if (_godPlayer != null && IsInstanceValid(_godPlayer))
		{
			_godPlayer.HitReceived -= KeepGodMode;
			_godPlayer.IsInvulnerable = _previousInvulnerability;
		}
		_godPlayer = null;
	}
	private void KeepGodMode(HitInstance hit)
	{
		if (_godPlayer != null && IsInstanceValid(_godPlayer)) _godPlayer.IsInvulnerable = true;
	}
	private void InputKeys(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true } key) return;
		if (key.Keycode == Key.Up || key.Keycode == Key.Down)
		{
			_historyIndex = Mathf.Clamp(_historyIndex + (key.Keycode == Key.Up ? -1 : 1), 0, _history.Count);
			_input.Text = _historyIndex < _history.Count ? _history[_historyIndex] : string.Empty;
			_input.CaretColumn = _input.Text.Length;
			_input.AcceptEvent();
		}
		else if (key.Keycode == Key.Tab)
		{
			Complete();
			_input.AcceptEvent();
		}
	}
	private void Complete()
	{
		string[] parts = _input.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		string prefix = _input.Text.EndsWith(' ') ? string.Empty : parts.Length == 0 ? string.Empty : parts[^1];
		var candidates = new List<string>();
		if (parts.Length <= 1 && !_input.Text.EndsWith(' ')) candidates.AddRange(new[] { "help", "list", "resource", "give", "cdr", "charges", "refill", "invincible", "heal", "spawn", "interact", "run", "base" });
		else if (parts[0] == "give") candidates.AddRange(Array.ConvertAll(RewardCatalog.All, r => r.Id));
		else if (parts[0] == "spawn") candidates.AddRange(ConfigRows.Ids<EnemyConfig>());
		else if (parts[0] is "charges" or "refill") candidates.AddRange(Abilities().ConvertAll(a => a.AbilityId));
		else if (parts[0] == "list") candidates.AddRange(new[] { "skills", "upgrades", "enemies", "interactions" });
		candidates.RemoveAll(c => !c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
		candidates.Sort(StringComparer.Ordinal);
		if (candidates.Count == 1)
		{
			int start = _input.Text.Length - prefix.Length;
			_input.Text = _input.Text[..start] + candidates[0] + " ";
			_input.CaretColumn = _input.Text.Length;
		}
		else Log(candidates.Count == 0 ? "没有匹配项。" : string.Join("  ", candidates));
	}
	private void Submit(string command)
	{
		if (string.IsNullOrWhiteSpace(command)) return;
		_history.Add(command.Trim());
		if (_history.Count > 64) _history.RemoveAt(0);
		_historyIndex = _history.Count;
		Log($"> {command}");
		Log(ExecuteCommand(command));
		_input.Clear();
		if (IsOpen)
		{
			_input.GrabFocus();
			_input.Edit();
		}
	}
	private void Log(string line)
	{
		_lines.Enqueue(line);
		while (_lines.Count > 120) _lines.Dequeue();
		_output.Text = string.Join("\n", _lines);
	}
	private static Unit Player() => PlayerLocator.Player ?? throw new ArgumentException("当前没有存活玩家。");
	private static List<Ability> Abilities()
	{
		var result = new List<Ability>();
		foreach (Node node in Player().FindChildren("*", string.Empty, true, false))
			if (node is Ability { Enabled: true } ability) result.Add(ability);
		return result;
	}
	private static Ability Skill(string id) => Abilities().Find(a => a.AbilityId.Equals(id, StringComparison.OrdinalIgnoreCase))
		?? throw new ArgumentException($"当前装备没有启用技能 {id}；输入 list skills 查询。");
	private static int Integer(string[] parts, int index, int min, int max)
	{
		if (parts.Length <= index || !int.TryParse(parts[index], out int number) || number < min || number > max)
			throw new ArgumentException($"参数{index}须为 {min}～{max} 的整数。");
		return number;
	}
	public string ExecuteCommand(string command)
	{
		LastCommandSucceeded = false;
		if (!Available || !DevelopmentBuild) return LastResult = "GM仅在开发构建且节点启用时可用。";
		CommandCount++;
		string[] parts = command.Trim().TrimStart('/').Split(' ', StringSplitOptions.RemoveEmptyEntries);
		try
		{
			LastResult = Dispatch(parts);
			LastCommandSucceeded = true;
		}
		catch (ArgumentException ex) { LastResult = $"未执行：{ex.Message}"; }
		catch (InvalidOperationException ex) { LastResult = $"未执行：{ex.Message}"; }
		return LastResult;
	}
	private string Dispatch(string[] p)
	{
		if (p.Length == 0) throw new ArgumentException("输入 help 查看命令。");
		switch (p[0].ToLowerInvariant())
		{
			case "help":
				return "list skills/upgrades/enemies/interactions [搜索词]\nresource 数量：增加碎片并保存　give 增幅ID：直接发放\ncdr 倍率：设基础CD倍率（0.01～10；0.5=减半）\ncharges 技能ID 层数：设最大层数并补满　refill [技能ID]：补满/清CD\ninvincible [on/off]：开关无敌　heal：恢复生命/护甲/护盾\nspawn 敌人ID [数量]：按基表召唤　interact 序号：传送并执行交互\nrun [角色ID 武器ID]：直接出征　base：返回基地\nG打开，Esc关闭；Tab补全命令/ID，↑↓选择历史。";
			case "list": return Catalogue(p);
			case "resource":
				if (p.Length != 2) throw new ArgumentException("用法：resource 数量");
				int amount = Integer(p, 1, 1, 1000000);
				MetaProgress.Load();
				MetaProgress.AddShards(amount);
				return $"碎片 +{amount} → {MetaProgress.Shards}（已保存）。";
			case "give":
				if (p.Length != 2) throw new ArgumentException("用法：give 增幅ID");
				if (RunController.Instance?.GrantReward(p[1]) != true) throw new ArgumentException("增幅不存在或当前不在局内；list upgrades 可查ID。");
				return $"已发放 {p[1]}，当前 {RunController.Instance.Build.StacksOf(p[1])} 层（GM直接发放）。";
			case "cdr":
				if (p.Length != 2 || !float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float multiplier)
					|| !float.IsFinite(multiplier) || multiplier < 0.01f || multiplier > 10) throw new ArgumentException("用法：cdr 倍率，范围0.01～10。");
				Player().Stats.SetBase("CooldownMultiplier", multiplier);
				return $"基础CD倍率={multiplier:0.##}，增幅修正保留；下次技能收尾时生效。";
			case "charges":
				if (p.Length != 3) throw new ArgumentException("用法：charges 技能ID 层数");
				int charges = Integer(p, 2, 1, 20);
				Ability charged = Skill(p[1]);
				charged.MaxCharges = charges;
				charged.RefreshCooldown();
				return $"{charged.AbilityId} 最大层数={charges}，已补满。";
			case "refill":
				if (p.Length > 2) throw new ArgumentException("用法：refill [技能ID]");
				List<Ability> abilities = p.Length == 1 ? Abilities() : new List<Ability> { Skill(p[1]) };
				foreach (Ability ability in abilities) ability.RefreshCooldown();
				return $"已补满/清除冷却：{abilities.Count} 个启用技能。";
			case "invincible":
				if (p.Length > 2 || p.Length == 2 && p[1] is not ("on" or "off")) throw new ArgumentException("用法：invincible [on/off]");
				bool on = p.Length == 1 ? !GodMode : p[1] == "on";
				if (on && !GodMode) BindGodPlayer(Player());
				if (!on && GodMode) UnbindGodPlayer();
				GodMode = on;
				return $"GM无敌：{(on ? "开启" : "关闭")}。移除控制台会清理GM持有的无敌。";
			case "heal": Player().FillVitals(); return "生命、护甲、护盾已按当前属性补满。";
			case "spawn": return Spawn(p);
			case "interact": return Interact(p);
			case "base":
				RunSetup.Clear();
				ChangeScene("res://Scenes/Levels/Base.tscn");
				return "返回基地。";
			case "run":
				if (p.Length != 1 && p.Length != 3) throw new ArgumentException("用法：run [角色ID 武器ID]");
				if (p.Length == 3)
				{
					if (!RunController.TryResolveCharacter(p[1], out var character) || RunController.CharacterRow(character) == null
						|| !RunController.TryResolveWeapon(p[2], out var weapon) || RunController.WeaponRow(weapon) == null)
						throw new ArgumentException("角色或武器ID不存在。");
					RunSetup.Character = character;
					RunSetup.Weapon = weapon;
				}
				RunSetup.HasSelection = true;
				ChangeScene("res://Scenes/Main.tscn");
				return "按所选角色/武器进入关卡。";
			default: throw new ArgumentException($"未知命令 {p[0]}。输入 help 查看命令。");
		}
	}
	private void ChangeScene(string path)
	{
		Close();
		UiRouter.Instance?.ResetForSceneChange();
		SceneTree tree = GetTree();
		Callable.From(() => tree.ChangeSceneToFile(path)).CallDeferred();
	}
	private string Catalogue(string[] p)
	{
		if (p.Length < 2 || p.Length > 3) throw new ArgumentException("用法：list skills/upgrades/enemies/interactions [搜索词]");
		var lines = new List<string>();
		switch (p[1])
		{
			case "skills":
				foreach (Ability ability in Abilities()) lines.Add($"{ability.AbilityId} · {ConfigRows.ById<AbilityConfig>(ability.AbilityId)?.DisplayName} · {AbilityHud.KeyCaption(ability.InputAction)} · CD {ability.EffectiveCooldown:0.##} · {ability.ChargesRemaining}/{ability.MaxCharges}");
				break;
			case "upgrades": foreach (RewardDefinition row in RewardCatalog.All) lines.Add($"{row.Id} · {row.Name} · {row.Kind}"); break;
			case "enemies":
				foreach (string id in ConfigRows.Ids<EnemyConfig>()) if (ConfigRows.ById<EnemyConfig>(id) is { IsGlobal: false } row) lines.Add($"{id} · {row.DisplayName} · 生命{row.MaxHealth}");
				break;
			case "interactions":
				List<Interactable> items = Interactions();
				for (int i = 0; i < items.Count; i++) lines.Add($"{i} · {items[i].Name} · {items[i].Prompt}");
				break;
			default: throw new ArgumentException("可查：skills / upgrades / enemies / interactions");
		}
		if (p.Length == 3) lines.RemoveAll(line => !line.Contains(p[2], StringComparison.OrdinalIgnoreCase));
		return lines.Count == 0 ? "没有匹配项。" : string.Join("\n", lines);
	}
	private string Spawn(string[] p)
	{
		if (p.Length < 2 || p.Length > 3) throw new ArgumentException("用法：spawn 敌人ID [数量1～20]");
		EnemyConfig row = ConfigRows.ById<EnemyConfig>(p[1]) ?? throw new ArgumentException("敌人ID不存在；list enemies可查询。");
		if (row.IsGlobal || string.IsNullOrEmpty(row.ScenePath)) throw new ArgumentException("这不是可召唤的敌人行。");
		int count = p.Length == 3 ? Integer(p, 2, 1, 20) : 1;
		PackedScene scene = GD.Load<PackedScene>(row.ScenePath) ?? throw new InvalidOperationException("敌人场景读取失败。");
		Unit player = Player();
		Vector3 forward = -(GetViewport().GetCamera3D()?.GlobalBasis.Z ?? player.GlobalBasis.Z);
		forward.Y = 0;
		forward = forward.Normalized();
		for (int i = 0; i < count; i++)
		{
			Unit unit = scene.Instantiate<Unit>();
			row.ApplyTo(unit);
			GetTree().CurrentScene.AddChild(unit);
			unit.GlobalPosition = player.GlobalPosition + forward * (5 + i / 5 * 2) + forward.Cross(Vector3.Up) * (i % 5 - Math.Min(4, count - 1) * 0.5f) * 1.5f;
		}
		return $"已召唤 {row.DisplayName} ×{count}（基表数值，未计入挑战波次）。";
	}
	private static List<Interactable> Interactions()
	{
		var result = Interactable.All.FindAll(item => IsInstanceValid(item) && item.Enabled && !item.Used && !item.Vanished);
		result.Sort((a, b) => string.Compare(a.GetPath(), b.GetPath(), StringComparison.Ordinal));
		return result;
	}
	private string Interact(string[] p)
	{
		List<Interactable> items = Interactions();
		if (p.Length != 2 || items.Count == 0) throw new ArgumentException("用法：interact 序号；先list interactions查看可用交互。");
		int index = Integer(p, 1, 0, items.Count - 1);
		Interactable target = items[index];
		if (StageController.Instance is { ChallengerActive: true }) throw new ArgumentException("挑战进行中，交互仍遵守挑战者限制。");
		Player().GlobalPosition = target.GlobalPosition + Vector3.Right * target.InteractRadius * 0.5f;
		target._PhysicsProcess(0.11);
		if (!target.Interact()) throw new InvalidOperationException("当前交互未接受，请查看节点条件。");
		Close();
		return $"已传送并交互：{target.Name}。";
	}
}
