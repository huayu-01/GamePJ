using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 基地里的**实时换装预览**（用户需求 §五 #4：「在基地时选取了什么装备/角色时，玩家就切换到相应的目标，
/// 方便进关卡前熟悉角色」）。
///
/// <list type="bullet">
/// <item><b>换模型</b>：角色表（`Config/Tables/Characters/chr_*.tres`）的 <see cref="UnitConfig.ModelPath"/>
///   指向哪个 `.glb`，就把基地玩家身上的外观换成哪一个（留空 = 场景自带的那一个）。</item>
/// <item><b>换数值</b>：生命 / 护甲 / 护盾 / 移速 / 减伤按角色行套到基地玩家身上
///   （与 <c>RunController.ApplyBalance</c> 在关卡里做的是同一件事；基地里没有战斗，所以只是"看得见身份"）。</item>
/// <item><b>换武器</b>：枪械开关按角色行的 <see cref="CharacterConfig.WeaponEnabled"/>（近战重装收起枪），
///   并把武器表那一行套到基地玩家手上的 <c>Weapon</c> 组件上。</item>
/// </list>
///
/// <para><b>为什么不直接调 <see cref="UnitConfig.ApplyTo"/></b>：它里面的换模型是"<c>Free()</c> 掉旧模型再
/// <c>AddChild</c> 一个新实例"，而 <c>UnitAnimator</c> 已经绑在旧的 `Model/AnimationPlayer` 上了 ——
/// 运行期换模型会留下一个指向已释放节点的引用（每帧报错）。这里改成"
/// **每个角色各留一份预览模型、靠 <c>Visible</c> 切换**"，不释放任何既有节点。</para>
///
/// <para>⚠️ 模型只是"看得见的那一层"：关卡里玩家的模型由 <c>Player.tscn</c> 决定（本组件只在基地场景的
/// 玩家实例上），所以它**不会**影响关卡内的战斗单位。</para>
/// </summary>
public partial class LoadoutPreview : Node
{
	/// <summary>场景里外观模型的约定节点名（与 <c>UnitAnimator</c>/<c>UnitConfig.ApplyModel</c> 同一份约定）。</summary>
	private const string ModelNodeName = "Model";

	private readonly Dictionary<string, Node3D> _previews = new();

	private Unit _player = null!;
	private Node3D? _originalModel;
	private Transform3D _modelTransform = Transform3D.Identity;
	private PlayerCharacterKind _character = (PlayerCharacterKind)(-1);
	private WeaponPresetKind _weapon = (WeaponPresetKind)(-1);

	/// <summary>当前预览用的是哪个角色的模型路径（空 = 场景自带；验证用只读值）。</summary>
	public string PreviewModelPath { get; private set; } = string.Empty;

	/// <summary>现在有几个预览模型实例（验证用只读值）。</summary>
	public int PreviewModelCount => _previews.Count;

	/// <summary>上一次套用时角色行读不到（配置表还没建好）——下一帧会再试一次。</summary>
	private bool _rowMissing;

	public override void _Ready()
	{
		// 配置台打开时界面会把整棵树暂停（OverlayScreen 的分层策略），而"换人"必须**在选定的那一刻**发生，
		// 所以这个组件在暂停期间也照常跑（面板一关就能看到换好的角色）。
		ProcessMode = ProcessModeEnum.Always;

		_player = GetParent<Unit>();
		_originalModel = _player.GetNodeOrNull<Node3D>(ModelNodeName);
		if (_originalModel != null)
		{
			_modelTransform = _originalModel.Transform;
		}

		Apply(RunSetup.Character, RunSetup.Weapon);
	}

	public override void _Process(double delta)
	{
		// 基地里的选项来自交互台（RunSetup 是跨场景的静态配置），所以这里只做"变了就换"的边沿检测。
		// ⚠️ 另外要处理一种真实时序：本组件 `_Ready` 可能早于配置表建好（实测 `Balance` 的索引晚一秒才就绪），
		// 那一次读不到角色行 → 记下 `_rowMissing`，等表就绪后自动补一次（否则基地玩家永远是场景默认数值）。
		if (RunSetup.Character != _character || RunSetup.Weapon != _weapon || _rowMissing)
		{
			Apply(RunSetup.Character, RunSetup.Weapon);
		}
	}

	/// <summary>把基地玩家切换成这个角色 + 武器（可反复调用，同一个角色不会重复建模型）。</summary>
	public void Apply(PlayerCharacterKind character, WeaponPresetKind weapon)
	{
		_character = character;
		_weapon = weapon;

		CharacterConfig? row = RunController.CharacterRow(character);
		_rowMissing = row == null;
		PreviewModelPath = row?.ModelPath ?? string.Empty;

		ApplyPreviewModel(PreviewModelPath);
		ApplyStats(row);
		ApplyWeapon(row, weapon);

		GameLog.Write(
			"Flow",
			$"基地换装预览：角色 {character} / 武器 {weapon} —— 模型 {(PreviewModelPath.Length == 0 ? "场景自带" : PreviewModelPath)}"
			+ $"（预览实例 {_previews.Count} 个；角色行{(row == null ? "读不到，等表就绪后重试" : "已套用")}）");
	}

	/// <summary>没有 <c>ModelPath</c> 就露出场景自带的模型，有就切到对应预览实例（各自的 Visible 互斥）。</summary>
	private void ApplyPreviewModel(string path)
	{
		if (_originalModel == null)
		{
			return;
		}

		if (path.Length == 0)
		{
			_originalModel.Visible = true;
			foreach (Node3D preview in _previews.Values)
			{
				preview.Visible = false;
			}

			return;
		}

		if (!_previews.TryGetValue(path, out Node3D? model))
		{
			if (!ResourceLoader.Exists(path) || GD.Load<PackedScene>(path) is not { } scene)
			{
				GD.PushWarning($"[LoadoutPreview] 角色模型 {path} 读不到，保留场景自带的模型。");
				return;
			}

			model = scene.Instantiate<Node3D>();
			model.Name = $"Preview_{_previews.Count}";
			// 与场景里那个占位模型用**同一套变换**（.glb 是 1/2.7 的比例，抄一遍才不会大小不一）。
			model.Transform = _modelTransform;
			_player.AddChild(model);
			PlayIdle(model);
			_previews[path] = model;
		}

		foreach (KeyValuePair<string, Node3D> entry in _previews)
		{
			entry.Value.Visible = entry.Key == path;
		}

		_originalModel.Visible = false;
	}

	/// <summary>
	/// 预览模型也要有站姿：`.glb` 里的动画默认不播（`autoplay` 是场景里那个 `AnimationPlayer` 节点上的属性），
	/// 所以这里显式播一次 idle（并把常用那几段设成循环，与 <c>UnitAnimator</c> 同一套做法）。
	/// </summary>
	private static void PlayIdle(Node3D model)
	{
		if (NodeSearch.FindInSubtree<AnimationPlayer>(model) is not { } player)
		{
			return;
		}

		foreach ((string name, bool loop) in new[] { ("idle", true), ("walk", true), ("sprint", true), ("die", false) })
		{
			if (player.GetAnimation(name) is { } animation)
			{
				animation.LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
			}
		}

		if (player.HasAnimation("idle"))
		{
			player.Play("idle");
		}
	}

	/// <summary>
	/// 角色行的数值套到基地玩家身上（与关卡里的 <c>RunController.ApplyBalance</c> 用同一个入口）。
	/// 不叠加局外强化（那是出征时才算的），也不改能力开关（基地里没有战斗）。
	/// </summary>
	private void ApplyStats(CharacterConfig? row)
	{
		if (row == null)
		{
			return;
		}

		_player.ApplyBaseStats(row.MaxHealth, row.StartingArmor, row.MaxShield, row.MoveSpeed);
		_player.DamageReduction = row.DamageReduction;
	}

	/// <summary>
	/// 武器：枪械开关按角色行（近战重装收起枪 → 模型上也看得出来），
	/// 并把武器表那一行套到基地玩家手上的 <c>Weapon</c> 组件（三把枪共用同一个节点，数值才是区别）。
	/// </summary>
	private void ApplyWeapon(CharacterConfig? row, WeaponPresetKind weapon)
	{
		bool enabled = row?.WeaponEnabled ?? true;
		_player.GetNodeOrNull<Node3D>("WeaponMount/Pistol")?.SetVisible(enabled);

		if (enabled && NodeSearch.FindInSubtree<Weapons.Weapon>(_player) is { } gun)
		{
			RunController.WeaponRow(weapon)?.ApplyTo(gun);
		}
	}
}
