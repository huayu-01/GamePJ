using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.AI;
using ActRoguelike.Audio;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Interaction;
using ActRoguelike.Run;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Debug;

/// <summary>
/// 本轮五项改动（§五 #3 死亡流程 / #4 基地换装 / #8 护甲表现 / #9 跳字分类 / #13 交互表现）的**验证拍摄台**。
///
/// <para>用法（三个场景各跑一次，见 docs/ui.md §16.6 与 docs/death-flow.md）：</para>
/// <code>
/// Godot.exe --path &lt;项目&gt; res://Scenes/UI/_FeelShowcase.tscn   # #8 #9 #13（TestArena + 真的 Player/Hud/WorldOverlay）
/// Godot.exe --path &lt;项目&gt; res://Scenes/UI/_FeelBase.tscn         # #4（真的 Base.tscn，走真的交互台）
/// Godot.exe --path &lt;项目&gt; res://Scenes/UI/_FeelDeath.tscn        # #3（真的 Main.tscn，真的把玩家打死）
/// </code>
///
/// <p>它**只读/只播表现**：所有伤害都用既有入口（<c>Unit.ReceiveHit</c> / <c>ApplyDirectDamage</c> /
/// <c>StatusPresets.Create</c>），不做任何结算改动；每个场景跑完自己退出。</p>
/// </summary>
public partial class _FeelProbe : Node3D
{
#if DEBUG
	/// <summary>跑哪一套：arena（#8/#9/#13）/ base（#4）/ death（#3）。</summary>
	[Export] public string Mode { get; set; } = "arena";

	private sealed class Step
	{
		public required float Wait { get; init; }
		public required System.Action Act { get; init; }
	}

	private readonly Queue<Step> _steps = new();
	private Step? _current;
	private float _countdown;
	private bool _done;
	private string? _pendingShot;
	private float _pendingShotDelay;

	private Unit? _player;
	private Unit? _dummy;
	private Unit? _guard;

	/// <summary>无甲单位（追猎者，<c>ArmorPeak = 0</c>）：用来验用户 A6 的「要有护甲才会被显示出来」。</summary>
	private Unit? _plain;
	private CanvasLayer? _overlay;
	private Node? _hud;
	private Camera3D? _cam;
	private Interactable? _node;
	private DeathScreen? _death;
	private LoadoutPreview? _preview;

	public override void _Process(double delta)
	{
		if (_done)
		{
			return;
		}

		// 待拍（不占队列顺序，见 ShotSoon 的注释）。
		if (_pendingShot != null)
		{
			_pendingShotDelay -= (float)delta;
			if (_pendingShotDelay <= 0f)
			{
				string file = _pendingShot;
				_pendingShot = null;
				Shot(file);
			}
		}

		if (_current == null)
		{
			// ⚠️ 还有"待拍"时不能收工 —— 否则最后一张图会被 Quit 抢掉（实测踩到：feel_09 从来没写出来）。
			if (_steps.Count == 0 && _pendingShot == null)
			{
				_done = true;
				GD.Print("[Feel] === 拍摄台结束 ===");
				ActRoguelike.Core.GameExit.Request(GetTree());
				return;
			}

			if (_steps.Count > 0)
			{
				_current = _steps.Dequeue();
				_countdown = _current.Wait;
			}
		}

		_countdown -= (float)delta;
		if (_countdown > 0f)
		{
			return;
		}

		if (_current is not { } running)
		{
			return;   // 队列空了又没有待拍：上面那一支已经收工了。
		}

		_current = null;
		running.Act();
	}

	public override void _Ready()
	{
		// 界面/死亡面板都会暂停整棵树，拍摄台必须照常走。
		ProcessMode = ProcessModeEnum.Always;
		GD.Print($"[Feel] === 拍摄台开始 mode={Mode} ===");

		switch (Mode)
		{
			case "base":
				BuildBase();
				break;
			case "death":
				BuildDeath();
				break;
			default:
				BuildArena();
				break;
		}
	}

	private void Later(float seconds, System.Action act) => _steps.Enqueue(new Step { Wait = seconds, Act = act });

	/// <summary>
	/// 迟一小段再截图：<c>GetViewport().GetTexture().GetImage()</c> 拿到的是**上一帧**渲染结果，
	/// 同一帧里刚加进去的跳字 / 刚移动的相机都还没画出来（踩过一次：跳字全都不在图上）。
	///
	/// ⚠️ 用"待拍"字段 + 倒计时，**不能**再往步骤队列里塞一步 —— 那会被排到队尾，
	/// 于是这张图会在整个拍摄流程的最后才拍（实测踩到：三张图拍到的都是最后的机位）。
	/// </summary>
	private void ShotSoon(string file, float delay = 0.1f)
	{
		_pendingShot = file;
		_pendingShotDelay = delay;
	}

	private void Shot(string file)
	{
		GetTree().Paused = false;
		Image? image = GetViewport()?.GetTexture()?.GetImage();
		if (image == null)
		{
			GD.Print($"[Feel] 截图失败 {file}（拿不到视口图像）");
			return;
		}

		Error err = image.SavePng("res://docs/screenshots/" + file);
		GD.Print($"[Feel] {file}  {image.GetWidth()}x{image.GetHeight()}  err={err}");
	}

	// ───────────────────────── #8 + #9 + #13：竞技场 ─────────────────────────

	private void BuildArena()
	{
		_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>();
		AddChild(_player);
		_player.GlobalPosition = new Vector3(0f, 0.6f, 4f);

		_hud = RuntimeResources.Load<PackedScene>("res://Scenes/UI/Hud.tscn").Instantiate();
		AddChild(_hud);
		_overlay = RuntimeResources.Load<PackedScene>("res://Scenes/UI/WorldOverlay.tscn").Instantiate<CanvasLayer>();
		AddChild(_overlay);

		// 自己的机位（能精确取景；HUD/血条是 CanvasLayer，与谁当相机无关）。
		Later(0.2f, WideShot);

		Later(0.6f, () =>
		{
			// 带甲的木桩（600 生命 / 60 甲 / 护甲减伤 0.25）——用来验"护甲进度"与"碎甲"。
			_dummy = RuntimeResources.Load<PackedScene>("res://Scenes/Training/TrainingDummy.tscn").Instantiate<Unit>();
			// ⚠️ 木桩场景里没有 Animator 节点，而 UnitPresentation 会在 _Ready 里 `new` 一个补上 ——
			// 运行期在 _Ready 里 add_child 会命中 Godot 的 "Parent node is busy setting up children"（既有问题，与本轮无关）。
			// 先在**进树之前**补一个，避开那条报错。
			_dummy.AddChild(new UnitAnimator { Name = UnitAnimator.NodeName });
			AddChild(_dummy);
			_dummy.GlobalPosition = new Vector3(-2.2f, 0f, 1.2f);
			GD.Print($"[Feel] 木桩：生命 {_dummy.MaxHealth:0} 甲 {_dummy.Armor:0}/{_dummy.ArmorPeak:0} 减伤 {_dummy.ArmorDamageReduction:0.##}");

			// 守卫（正面格挡 0.25）：把 AI 与技能停掉，只当"会格挡的靶子"。
			_guard = RuntimeResources.Load<PackedScene>("res://Scenes/Enemies/Guard.tscn").Instantiate<Unit>();
			AddChild(_guard);
			_guard.GlobalPosition = new Vector3(2.4f, 0f, 1.2f);
			foreach (Node child in _guard.GetChildren())
			{
				if (child is EnemyBrain or Ability)
				{
					child.SetProcess(false);
					child.SetPhysicsProcess(false);
				}
			}

			GD.Print($"[Feel] 守卫：生命 {_guard.MaxHealth:0} 甲 {_guard.Armor:0}/{_guard.ArmorPeak:0} 格挡倍率 {_guard.Block?.BlockDamageMultiplier}");

			// 无甲单位（A6 的对照）：**一条护甲视觉都不该有** —— 连"塑料壳"都不给。
			_plain = RuntimeResources.Load<PackedScene>("res://Scenes/Enemies/Chaser.tscn").Instantiate<Unit>();
			if (_plain.GetNodeOrNull<UnitAnimator>(UnitAnimator.NodeName) == null)
			{
				_plain.AddChild(new UnitAnimator { Name = UnitAnimator.NodeName });
			}

			AddChild(_plain);
			_plain.GlobalPosition = new Vector3(4.4f, 0f, 3.4f);
			foreach (Node child in _plain.GetChildren())
			{
				if (child is EnemyBrain or Ability)
				{
					child.SetProcess(false);
					child.SetPhysicsProcess(false);
				}
			}

			GD.Print($"[Feel] 无甲单位：{_plain.Name} 生命 {_plain.MaxHealth:0} 甲 {_plain.Armor:0}/{_plain.ArmorPeak:0}（ArmorPeak = 0 → 期望任何护甲视觉都隐藏）");
		});

		// ① 甲还在：一次打不穿的伤害 → 应该出现「甲 N」，并且敌人血条上罩着那层半透明"塑料"（**血一点没掉**也要显示）。
		Later(0.8f, () =>
		{
			Hit(_dummy!, 20f, "普通命中（甲吸收）");
			PrintNumbers("甲吸收");
			PrintBars("甲吸收");
			Look(new Vector3(-2.2f, 1.5f, 4.2f), new Vector3(-2.2f, 0.55f, 1.2f));   // 拉近看护甲遮罩
		});
		Later(0.15f, () => Shot("feel_01_armor_progress.png"));

		// ①b A6 的对照：**有甲的木桩 vs 无甲的追猎者**，同屏看"有甲才显示"。
		Later(0.05f, () =>
		{
			Hit(_plain!, 12f, "无甲单位");
			PrintArmorVisual("有甲木桩 vs 无甲追猎者（血条按登记顺序）");
			Look(new Vector3(1.0f, 2.4f, 8.6f), new Vector3(1.0f, 0.9f, 1.8f));
		});
		Later(0.15f, () => Shot("feel_01b_armor_none.png"));

		// ② 打碎甲：一次超出剩余甲的伤害 → 「甲 N」+「伤害 N」+ 碎甲特效（环 + 甲条闪白）。
		Later(1.0f, () =>
		{
			Hit(_dummy!, 200f, "打碎甲");
			PrintNumbers("碎甲");
			PrintBars("碎甲");
			PrintArmorFx();
			WideShot();
		});
		Later(0.12f, () => Shot("feel_02_armor_break.png"));
		Later(0.15f, () => PrintBars("碎甲（下一帧更新后）"));
		Later(0.3f, () => PrintArmorFx());

		// ③ 四类跳字一次摆出来：无敌 / 格挡（真攻击实例）/ 燃烧（其他伤害）/ 恢复（玩家）。
		Later(1.2f, () =>
		{
			_dummy!.IsInvulnerable = true;
			Hit(_dummy!, 30f, "无敌");
			_dummy.IsInvulnerable = false;
			PrintNumbers("无敌");

			// 格挡：造一个**真的攻击实例**（Attack != null），方向朝守卫正面 → BlockComponent 判定格挡。
			var attack = new AttackInstance { Attacker = _player, Ability = "probe_melee", BaseDamage = 40f, CanCrit = false };
			_guard!.ReceiveHit(attack.RollHit(_guard, new Vector3(0f, 0f, 1f), _guard.GlobalPosition));
			PrintNumbers("格挡");
			WideShot();
			ShotSoon("feel_03b_invuln_block.png");

			// 其他伤害（电/燃烧）：状态效果的每秒伤害走的是"没有攻击实例"的 HitInstance。
			if (StatusPresets.Create("Burn", 3f) is { } burn)
			{
				_dummy.ApplyStatus(burn);
			}

			GD.Print($"[Feel] 木桩状态：{string.Join(",", _dummy.GetStatusIds())}（等 1 秒让燃烧跳一次）");
		});
		Later(1.2f, () =>
		{
			PrintNumbers("燃烧");
			_dummy!.ApplyDirectDamage(30f, _player);   // 有攻击者的直伤 → 普通「伤害」样式（不是"其他伤害"）
			PrintNumbers("直伤（有攻击者）");
		});

		// 恢复：玩家先掉血再回血 → PlayerHud 20Hz 发现"血量上升" → 「＋N」。
		Later(0.8f, () =>
		{
			_player!.ApplyDirectDamage(60f, _guard);
			GD.Print($"[Feel] 玩家掉血后：{_player.Health:0}/{_player.MaxHealth:0}");
		});
		Later(0.35f, () =>
		{
			_player!.Heal(35f);
			GD.Print($"[Feel] 玩家回血后：{_player.Health:0}/{_player.MaxHealth:0}");
		});
		Later(0.25f, () =>
		{
			PrintNumbers("无敌 / 格挡 / 燃烧 / 恢复 一起");
			WideShot();
			ShotSoon("feel_03_number_kinds.png");
		});
		Later(0.05f, () => PrintHealNumber());

		// ④ 交互表现（#13）：真的 RewardNode + 真的 PlayerInteractInput 选目标。
		Later(0.6f, () =>
		{
			_node = RuntimeResources.Load<PackedScene>("res://Scenes/Interactables/RewardNode.tscn").Instantiate<Interactable>();
			AddChild(_node);
			_node.GlobalPosition = new Vector3(0f, 0f, 2.0f);
			GD.Print($"[Feel] 交互物：{_node.Name} 提示「{_node.CurrentPrompt}」半径 {_node.InteractRadius:0.#}");
		});
		Later(0.8f, () =>
		{
			PrintHighlight("靠近");
			// 从**侧后方**看交互物：站正后方会被玩家自己的脑袋挡住（实测踩到）。
			Look(new Vector3(3.0f, 2.2f, 4.6f), new Vector3(0f, 1.15f, 2.0f));
			SetOutline(false);                                                  // 先关掉描边拍一张对照
			ShotSoon("feel_04b_outline_off.png");
		});
		Later(0.25f, () =>
		{
			SetOutline(true);
			PrintHighlight("描边打开");
			ShotSoon("feel_04_interact_highlight.png");
		});

		// ⑤ 失败响应：没有 StageController → OnInteract 返回 false → Failed → 抖动 + 音效 + 击退。
		Later(0.5f, () =>
		{
			Vector3 before = _player!.GlobalPosition;
			bool accepted = _node!.Interact();
			GD.Print($"[Feel] 交互返回={accepted}（期望 False：场景里没有 StageController）");
			PrintHighlight("失败瞬间");
			GD.Print($"[Feel] 失败音效：last={AudioManager.Instance?.DebugLastId} 累计={AudioManager.Instance?.DebugPlayCount} 缺失 id=[{AudioManager.Instance?.DebugMissingIds}]");
			_player.SetMeta("feel_before", before);
			ShotSoon("feel_05_interact_fail.png", 0.12f);
		});
		Later(0.45f, () =>
		{
			if (_player!.HasMeta("feel_before"))
			{
				Vector3 before = (Vector3)_player.GetMeta("feel_before");
				float moved = new Vector2(_player.GlobalPosition.X - before.X, _player.GlobalPosition.Z - before.Z).Length();
				GD.Print($"[Feel] 玩家被击退：{moved:0.###} 米（方向背离交互物，冲量 {(_node?.GetNodeOrNull<InteractablePresentation>("Presentation")?.FailKnockback ?? 0f):0.#}）");
			}

			PrintHighlight("击退后");
			GD.Print($"[Feel] 图标表：{UiIcons.DebugLoadedCount()} 张可加载（含按键图标 ic_ui_key_f）");
		});

		// ⑥ 玩家血条上的护甲遮罩（A6）：给玩家 60 甲、再打掉一点甲（**血一点不掉**）→ HUD 那条血条也罩上塑料。
		// ⚠️ 排在"恢复跳字"之后：护甲会吸收伤害，早给甲会把 ③ 的「＋35」恢复证据吃掉。
		Later(0.5f, () =>
		{
			_player!.ApplyBaseStats(_player.MaxHealth, 60f, 0f, _player.MoveSpeed);
			_player.ApplyDirectDamage(30f, _guard);
			GD.Print($"[Feel] 玩家护甲：{_player.Armor:0}/{_player.ArmorPeak:0}（血 {_player.Health:0}/{_player.MaxHealth:0} —— 血没掉也要看得见护甲进度）");
			WideShot();
		});

		// HUD 每 0.05 秒才刷一次，隔一会儿再读才是"屏幕上真的画出来的样子"。
		Later(0.3f, () => PrintPlayerArmor());
		Later(0.05f, () => Shot("feel_01c_player_armor.png"));

		// ⑦ 让超时/漏截图也能看到结论。
		Later(0.5f, () => PrintNumbers("收尾（跳字应已淡出）"));
	}

	/// <summary>一次普通命中（走既有入口 <see cref="Unit.ReceiveHit"/>，不经过任何新代码路径）。</summary>
	private void Hit(Unit target, float damage, string tag)
	{
		var attack = new AttackInstance { Attacker = _player, Ability = "probe", BaseDamage = damage, CanCrit = false };
		DamageResult result = target.ReceiveHit(attack.RollHit(target, _player!.GlobalPosition - target.GlobalPosition, target.GlobalPosition));
		GD.Print($"[Feel] 命中「{tag}」：原始 {damage:0} → 最终 {result.FinalDamage:0.#}（甲 {result.ArmorLost:0.#} / 盾 {result.ShieldLost:0.#} / 血 {result.HealthLost:0.#} 格挡={result.Blocked} 无敌={result.Invulnerable}）");
	}

	/// <summary>把跳字层当前的文字全打出来（每类跳字的机器可判定形式）。</summary>
	private void PrintNumbers(string tag)
	{
		var texts = new List<string>();
		foreach (Node child in _overlay!.GetNode<Control>("Numbers").GetChildren())
		{
			if (child is Label label)
			{
				texts.Add($"「{label.Text}」@{label.Modulate.ToHtml(false)}");
			}
		}

		GD.Print($"[Feel] 跳字[{tag}] {texts.Count} 个：{string.Join(" ", texts)}");
	}

	/// <summary>敌人血条上的护甲遮罩 / 血量条（读的是预制体里真实的 Control 尺寸）。</summary>
	private void PrintBars(string tag)
	{
		Control bars = _overlay!.GetNode<Control>("Bars");
		if (bars.GetChildCount() == 0)
		{
			GD.Print($"[Feel] 敌人血条[{tag}]：还没有血条（受伤后才会建）");
			return;
		}

		var root = bars.GetChild(0) as Control;
		var armorFill = root?.GetNodeOrNull<Control>("Content/ArmorFill");
		var armorBg = root?.GetNodeOrNull<Control>("Content/ArmorBg");
		var fill = root?.GetNodeOrNull<Control>("Content/Fill");
		var bg = root?.GetNodeOrNull<Control>("Content/Bg");
		float armorRatio = armorFill != null && armorBg != null && armorBg.Size.X > 0f ? armorFill.Size.X / armorBg.Size.X : 0f;
		GD.Print(
			$"[Feel] 敌人血条[{tag}]：护甲遮罩 {armorFill?.Size.X:0.#}px / 整条 {armorBg?.Size.X:0.#}px（比例 {armorRatio:0.##}）"
			+ $" 遮罩可见={armorFill?.Visible} 血条 {fill?.Size.X:0.#}px｜壳 {armorBg?.Size.X:0.#}×{armorBg?.Size.Y:0.#} vs 血条底 {bg?.Size.X:0.#}×{bg?.Size.Y:0.#}（大一点点）");
	}

	/// <summary>
	/// A6：「**要有护甲才会被显示出来**」的机器可判定形式 —— 逐条血条打印那层塑料壳/甲填充的可见性与尺寸。
	/// 血条按**登记顺序**排列（先受伤的先建），所以前面的行对应先受伤的单位。
	/// </summary>
	private void PrintArmorVisual(string tag)
	{
		Control bars = _overlay!.GetNode<Control>("Bars");
		var lines = new List<string>();
		foreach (Node child in bars.GetChildren())
		{
			if (child is not Control root)
			{
				continue;
			}

			Control? shell = root.GetNodeOrNull<Control>("Content/ArmorBg");
			Control? charge = root.GetNodeOrNull<Control>("Content/ArmorFill");
			Control? barBg = root.GetNodeOrNull<Control>("Content/Bg");
			Control? fill = root.GetNodeOrNull<Control>("Content/Fill");
			lines.Add(
				$"壳 {(shell?.Visible == true ? "显示" : "隐藏")}({shell?.Size.X:0.#}×{shell?.Size.Y:0.#})"
				+ $" 甲填充 {(charge?.Visible == true ? "显示" : "隐藏")}({charge?.Size.X:0.#}px)"
				+ $" 血条底 {barBg?.Size.X:0.#}×{barBg?.Size.Y:0.#} 血 {fill?.Size.X:0.#}px");
		}

		GD.Print($"[Feel] 护甲视觉[{tag}] 血条 {bars.GetChildCount()} 条：{string.Join(" ／ ", lines)}");
	}

	private void PrintHighlight(string tag)
	{
		InteractablePresentation? presentation = _node?.GetNodeOrNull<InteractablePresentation>("Presentation");
		GD.Print(
			$"[Feel] 交互表现[{tag}]：当前目标={PlayerInteractInput.Current?.Name} 同一个={ReferenceEquals(PlayerInteractInput.Current, _node)} "
			+ $"组件在树里={presentation != null} 高亮={presentation?.Highlighted} 描边可见={presentation?.Outline?.Visible} "
			+ $"按键图标有={presentation?.KeyIcon != null} 图标可见={presentation?.KeyIcon?.Visible} 失败计数={presentation?.FailCount}");
	}

	/// <summary>宽景（默认机位）：三个单位 + 交互物都在画面里。</summary>
	private void WideShot() => Look(new Vector3(0f, 2.6f, 9.5f), new Vector3(0f, 1.0f, 1.0f));

	private void Look(Vector3 from, Vector3 at)
	{
		_cam ??= CreateCamera();
		_cam.GlobalPosition = from;
		_cam.LookAt(at, Vector3.Up);
		_cam.MakeCurrent();
	}

	private Camera3D CreateCamera()
	{
		var camera = new Camera3D { Name = "FeelCamera", Fov = 70f };
		AddChild(camera);
		return camera;
	}

	/// <summary>拍对照图用：单独关掉 / 打开描边网格（组件只在"高亮状态变化"时才写它）。</summary>
	private void SetOutline(bool visible)
	{
		if (_node?.GetNodeOrNull<InteractablePresentation>("Presentation")?.Outline is { } outline)
		{
			outline.Visible = visible;
		}
	}

	/// <summary>碎甲特效的机器可判定形式：场景里现在有几个 <c>ArmorBreakFx</c> 节点。</summary>
	private void PrintArmorFx()
		=> GD.Print($"[Feel] 碎甲模型特效：ArmorBreakFx 节点 {CountByName(GetTree().Root, "ArmorBreakFx")} 个（0.42 秒后自己回收）");

	/// <summary>玩家 HUD 血条上的护甲遮罩（A6）：壳/甲填充的可见性与尺寸（血条框 264×20 → 壳该是 268×24，即每边大 2px）。</summary>
	private void PrintPlayerArmor()
	{
		Control? root = _hud is CanvasLayer hud ? hud.GetNodeOrNull<Control>("Root/PlayerBar") : null;
		Control? shell = root?.GetNodeOrNull<Control>("ArmorBg");
		Control? charge = root?.GetNodeOrNull<Control>("ArmorFill");
		Control? fill = root?.GetNodeOrNull<Control>("Fill");
		Control? frame = root?.GetNodeOrNull<Control>("BarBg");
		GD.Print(
			$"[Feel] 玩家血条护甲遮罩：壳 {(shell?.Visible == true ? "显示" : "隐藏")}({shell?.Size.X:0.#}×{shell?.Size.Y:0.#})"
			+ $" 甲填充 {(charge?.Visible == true ? "显示" : "隐藏")}({charge?.Size.X:0.#}px)"
			+ $" 血条框 {frame?.Size.X:0.#}×{frame?.Size.Y:0.#} 血 {fill?.Size.X:0.#}px（壳比血条框每边大 2px）");
	}

	/// <summary>恢复跳字挂在 HUD 层（<c>PlayerHud</c> 的 Root），所以单独数一次「＋N」。</summary>
	private void PrintHealNumber()
	{
		var found = new List<string>();
		if (_hud is CanvasLayer hud && hud.GetNodeOrNull<Control>("Root") is { } root)
		{
			foreach (Node child in root.GetChildren())
			{
				if (child is Label label && label.Text.StartsWith('＋'))
				{
					found.Add($"「{label.Text}」@{label.Modulate.ToHtml(false)}");
				}
			}
		}

		GD.Print($"[Feel] 恢复跳字（HUD 层，全项目只有玩家会回血）：{found.Count} 个：{string.Join(" ", found)}");
	}

	/// <summary>把界面上的卡片文案打出来（证明基地的选项与关卡里是同一套来源，不是抄的第二份）。</summary>
	private static void PrintCards(string tag, VBoxContainer? cards)
	{
		var texts = new List<string>();
		if (cards != null)
		{
			foreach (Node child in cards.GetChildren())
			{
				if (child is Button button && button.Visible)
				{
					texts.Add(button.Text);
				}
			}
		}

		GD.Print($"[Feel] {tag} {texts.Count} 张：{string.Join(" ｜ ", texts)}");
	}

	private static int CountByName(Node root, string name)	{
		int count = root.Name == name ? 1 : 0;
		foreach (Node child in root.GetChildren())
		{
			count += CountByName(child, name);
		}

		return count;
	}

	// ───────────────────────── #4：基地实时换装 ─────────────────────────

	private void BuildBase()
	{
		AddChild(RuntimeResources.Load<PackedScene>("res://Scenes/Levels/Base.tscn").Instantiate());

		Later(0.8f, () =>
		{
			_preview = FindInTree<LoadoutPreview>(GetTree().CurrentScene ?? this);
			GD.Print($"[Feel] 基地初始：角色 {RunSetup.Character} / 武器 {RunSetup.Weapon} ｜ LoadoutPreview={_preview != null} 预览模型=[{_preview?.PreviewModelPath}] 实例 {_preview?.PreviewModelCount}");
		});

		// 走**真的交互台**：装备配置台 → 先选角色（ChooseScreen）→ 再选武器（StartScreen）。
		Later(0.3f, () =>
		{
			// ⚠️ 交互判定要求玩家在范围内（`InteractRadius`），所以先把玩家挪到配置台旁边 ——
			// 这一步模拟"人走到台子前面"，判定本身一行没改。
			if (FindTerminal(BaseTerminal.TerminalKind.Loadout) is { } loadout && PlayerLocator.Player is { } walker)
			{
				walker.GlobalPosition = loadout.GlobalPosition + new Vector3(0f, 1.0f, 2.4f);
				GD.Print($"[Feel] 把玩家挪到配置台旁：距离 {walker.GlobalPosition.DistanceTo(loadout.GlobalPosition):0.##} 米（半径 {loadout.InteractRadius:0.#}）");
			}
		});
		Later(0.35f, () =>
		{
			BaseTerminal? terminal = FindTerminal(BaseTerminal.TerminalKind.Loadout);
			GD.Print($"[Feel] 装备配置台：找到={terminal != null} 玩家在范围内={terminal?.PlayerInRange} 交互接受={terminal?.Interact()}");
			ChooseScreen? choose = FindInTree<ChooseScreen>(GetTree().CurrentScene ?? this);
			GD.Print($"[Feel] 选角色界面打开={choose?.IsOpen} 按键提示={choose?.KeyHintCount} 条 目标={PlayerInteractInput.Current?.Name}");
			PrintCards("选角色卡片（与关卡 Configure 步同一套来源）", choose?.GetNodeOrNull<VBoxContainer>("Root/Panel/VBox/Cards"));
		});
		Later(0.5f, () => Shot("feel_06_base_choose_character.png"));

		Later(0.5f, () => PressKey(Key.Key2));   // 近战重装
		Later(0.15f, () =>
		{
			StartScreen? start = FindInTree<StartScreen>(GetTree().CurrentScene ?? this);
			GD.Print($"[Feel] 按 2 选角色后：RunSetup.Character={RunSetup.Character} 武器界面打开={start?.IsOpen}");
			PrintCards("武器卡片", start?.GetNodeOrNull<VBoxContainer>("Root/Panel/VBox/Cards"));
		});
		Later(0.35f, () => Shot("feel_07_base_choose_weapon.png"));

		Later(0.5f, () => PressKey(Key.Key2));   // 激光狙击枪
		Later(0.15f, () => GD.Print($"[Feel] 按 2 选武器后：RunSetup 角色={RunSetup.Character} 武器={RunSetup.Weapon} HasSelection={RunSetup.HasSelection}"));
		Later(0.45f, () =>
		{
			PrintPreview("重装 + 狙击枪");
			LookAtBasePlayer();
			ShotSoon("feel_08_base_heavy_preview.png");
		});

		// 再走一遍：召唤控制 + 手枪。
		// ⚠️ 上一步的近景机位把玩家挪到了空地（远离配置台），所以这里要先走回台子边 —— 否则交互被拒。
		Later(0.6f, () =>
		{
			if (FindTerminal(BaseTerminal.TerminalKind.Loadout) is { } loadout && PlayerLocator.Player is { } walker)
			{
				walker.GlobalPosition = loadout.GlobalPosition + new Vector3(0f, 1.0f, 2.4f);
			}
		});
		Later(0.35f, () =>
		{
			FindTerminal(BaseTerminal.TerminalKind.Loadout)?.Interact();
			GD.Print($"[Feel] 第二次开配置台：{FindInTree<ChooseScreen>(GetTree().CurrentScene ?? this)?.IsOpen}");
		});
		Later(0.5f, () => PressKey(Key.Key3));
		Later(0.4f, () => PressKey(Key.Key1));
		Later(0.6f, () =>
		{
			PrintPreview("召唤 + 手枪");
			LookAtBasePlayer();
			ShotSoon("feel_09_base_summoner_preview.png");
		});
	}

	/// <summary>
	/// 基地预览的近景机位：先把玩家挪到**台子之间的空地**（不然近景会被配置台本身挡住 —— 实测踩到），
	/// 再从正面斜上方看过去，模型换没换一眼就能看出来。
	/// </summary>
	private void LookAtBasePlayer()
	{
		if (PlayerLocator.Player is not { } player)
		{
			return;
		}

		player.GlobalPosition = new Vector3(0f, 1.0f, 6f);
		// 固定的近景机位（与竞技场那台同一个思路：站在玩家斜前方 4.5 米、抬高 1.4 米再看回来）。
		Look(new Vector3(0f, 2.4f, 10.5f), new Vector3(0f, 1.2f, 6f));
		GD.Print($"[Feel] 基地近景机位：玩家 {player.Name}@{player.GlobalPosition}（父 {player.GetParent()?.Name}）相机 {_cam?.GlobalPosition} 当前相机={GetViewport().GetCamera3D()?.Name}");
	}

	private void PressKey(Key key)
	{
		Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
		Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
	}

	private void PrintPreview(string tag)
	{
		Node3D? pistol = _preview?.GetParent().GetNodeOrNull<Node3D>("WeaponMount/Pistol");
		GD.Print(
			$"[Feel] 基地预览[{tag}]：角色 {RunSetup.Character} 武器 {RunSetup.Weapon} "
			+ $"模型=[{_preview?.PreviewModelPath}] 预览实例={_preview?.PreviewModelCount} 枪可见={pistol?.Visible} "
			+ $"玩家生命={(_preview?.GetParent() as Unit)?.MaxHealth:0} 甲={(_preview?.GetParent() as Unit)?.ArmorPeak:0}");
	}

	// ───────────────────────── #3：死亡流程 ─────────────────────────

	/// <summary>
	/// 本拍摄台**就挂在真的 <c>Main.tscn</c> 里**（场景是 `_FeelDeath.tscn` = 继承自 Main 的场景，见 docs/death-flow.md §4）。
	///
	/// <p>为什么非要这样：<c>RunController.EndRun</c> 是按
	/// <c>GetTree().CurrentScene.GetNodeOrNull&lt;DeathScreen&gt;("DeathScreen")</c> 找面板的 ——
	/// 把 Main **实例化到别的场景下**再手动把 <c>CurrentScene</c> 指过去是**不行的**（实测：C# 的
	/// <c>SceneTree.CurrentScene</c> setter 赋值不生效，实测两次都没改掉），那样验的就不是真的那条路。</p>
	/// </summary>
	private void BuildDeath()
	{
		// 走"从基地出征"那条路，跳过开局界面，让 RunController 立刻开局（它的 `_Process` 第一帧才读这个开关）。
		// ⚠️ P6a：角色/武器换成清旧之后剩下的角色 A［猎手］+ 能量掌。
		RunSetup.Character = PlayerCharacterKind.Hunter;
		RunSetup.Weapon = WeaponPresetKind.EnergyPalm;
		RunSetup.HasSelection = true;

		// 挂在当前场景下、但**挪到树根**：这样死亡流程切场景时这个拍摄台不会被一起释放，
		// 才能验证到"真的回到了基地"。
		// ⚠️ 挪节点必须**延后一帧**：`_Ready` 期间引擎正忙着装配场景，此刻 remove_child 会报
		// "Parent node is busy adding/removing children"（实测踩到）。
		string scenePath = GetTree().CurrentScene?.SceneFilePath ?? string.Empty;
		_death = GetTree().CurrentScene?.GetNodeOrNull<DeathScreen>("DeathScreen");
		GD.Print(
			$"[Feel] 当前场景={GetTree().CurrentScene?.Name} 文件={scenePath} ｜ DeathScreen 找到={_death != null} "
			+ $"死亡动画 {_death?.DeathAnimationSeconds:0.#}s / 演示面板 {_death?.HoldSeconds:0.#}s");

		Later(0.15f, () =>
		{
			if (GetTree().CurrentScene != null && GetParent() == GetTree().CurrentScene)
			{
				// ⚠️ 先把树根抓在手里：`RemoveChild` 之后本节点的 `GetTree()` 就变成 null 了。
				Node root = GetTree().Root;
				Node parent = GetParent();
				parent.RemoveChild(this);
				root.AddChild(this);
				GD.Print("[Feel] 拍摄台已挪到树根（切场景时不会被一起释放）");
			}
		});

		Later(1.6f, () =>
		{
			Unit? player = PlayerLocator.Player;
			GD.Print($"[Feel] 关卡玩家：找到={player != null} 生命={player?.Health:0}/{player?.MaxHealth:0} 暂停={GetTree().Paused}");
			player?.ForceKill();
		});

		// ① 死亡动画段：面板还没出现，世界里能看到倒地。
		Later(0.7f, () =>
		{
			GD.Print($"[Feel] 死亡流程①死亡动画：{DeathState()}（期望 正在播=True 阶段=death）");
			Shot("feel_10_death_anim.png");
		});

		// ② 演示过渡面板（占位）：淡入 + 倒计时。
		Later(1.4f, () =>
		{
			GD.Print($"[Feel] 死亡流程②演示面板：{DeathState()}（期望 阶段=panel）暂停={GetTree().Paused}");
			Shot("feel_11_death_panel.png");
		});
		Later(0.9f, () =>
		{
			GD.Print($"[Feel] 死亡流程②进行中：{DeathState()}");
			Shot("feel_12_death_panel_full.png");
		});

		// ③ 回基地：等它自己切场景（ChangeSceneToFile），再看当前场景是谁。
		Later(2.6f, () =>
		{
			Node? scene = GetTree().CurrentScene;
			GD.Print(
				$"[Feel] 死亡流程③回到基地：当前场景={scene?.Name} 文件={scene?.SceneFilePath} "
				+ $"（期望 Base / res://Scenes/Levels/Base.tscn）HasSelection={RunSetup.HasSelection}（期望 False）暂停={GetTree().Paused}");
			Shot("feel_13_back_to_base.png");
		});
	}

	/// <summary>死亡面板状态（面板可能已被场景切换释放，所以每次都要重新判有效性）。</summary>
	private string DeathState()
		=> _death != null && IsInstanceValid(_death)
			? $"正在播={_death.IsPlaying} 阶段={_death.PhaseName} 倒计时={_death.CountdownProgress:0.##}"
			: "面板已被释放（场景已切走）";

	// ───────────────────────── 工具 ─────────────────────────

	private static T? FindInTree<T>(Node root)
		where T : class
	{
		foreach (Node child in root.GetChildren())
		{
			if (child is T typed)
			{
				return typed;
			}

			if (FindInTree<T>(child) is { } deeper)
			{
				return deeper;
			}
		}

		return null;
	}

	private BaseTerminal? FindTerminal(BaseTerminal.TerminalKind kind)
	{
		foreach (BaseTerminal terminal in FindAll<BaseTerminal>(GetTree().CurrentScene ?? this))
		{
			if (terminal.Kind == kind)
			{
				return terminal;
			}
		}

		return null;
	}

	private static List<T> FindAll<T>(Node root)
		where T : class
	{
		var found = new List<T>();
		foreach (Node child in root.GetChildren())
		{
			if (child is T typed)
			{
				found.Add(typed);
			}

			found.AddRange(FindAll<T>(child));
		}

		return found;
	}
#else
	public override void _Ready() => SetProcess(false);
#endif
}
