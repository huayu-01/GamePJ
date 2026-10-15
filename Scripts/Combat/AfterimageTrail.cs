using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// **后撤残影**（P6c 表现层；设计文档 `角色设计.txt` 第 26 行，技能 1「后撤」）：
///
/// <blockquote>
/// 「技能1[后撤]{CD:10}:角色快速向后移动（**不改变角色朝向**，只向后移动，表现相当于给角色一个向后的力……），
/// **有无敌帧**.」
/// </blockquote>
///
/// <para>机制侧 P3 已经做完了（<see cref="BackdashAbility"/>：<c>Dodge</c> 帧给向后的力 + 开无敌，
/// <c>Dodger</c> 帧关无敌）。本类只补**视觉**：无敌帧那一段时间里按 <see cref="SpawnInterval"/>
/// 在角色当前位置留下一个**角色模型的半透明副本**，每个副本在 <see cref="GhostLifetime"/> 秒里
/// 从 <see cref="GhostStartAlpha"/> 淡到 0 然后回收。</para>
///
/// <para><b>它只读运行期状态，不参与判定</b>：唯一的数据来源是
/// <see cref="BackdashAbility.InvulnerableOwned"/>（P3 就有的**只读**属性 =「Dodge→Dodger 这段
/// 无敌帧现在开着」）—— 所以残影的起止与无敌帧**同起同落**，战斗代码一行没动。</para>
///
/// <para>⚠️⚠️ <b>残影必须是"纯视觉"</b>（分层见 `architecture.md`）。本类做到这一点的手段是
/// **造出来之后立刻拆**（见 <see cref="Sanitize"/>）：把复制出来的子树里所有
/// <see cref="CollisionObject3D"/>（含 <c>Area3D</c>）、所有 <see cref="Unit"/>、所有
/// <see cref="AnimationTree"/> 从树上摘掉再 <c>QueueFree</c>；再把整棵子树从**所有分组**里移除；
/// 最后逐个点数核对（<see cref="LastGhostCollisionCount"/> / <see cref="LastGhostUnitCount"/> /
/// <see cref="LastGhostGroups"/> —— <see cref="DebugState"/> 会把它们打出来）。
/// 残影**只被 <c>AddChild</c> 到 <c>CurrentScene</c>**，从不挂到玩家/敌人下、从不进
/// <c>enemy_units</c> / <c>player_units</c> / <c>projectiles</c>，因此不吃清场、不计入数量、不吃伤害。</para>
///
/// <para><b>"那一刻的姿势"</b>：复制出来的 <c>Skeleton3D</c> 要先把源骨架的骨骼姿势逐个抄过来，
/// 再把复制出来的 <c>AnimationPlayer</c> 摘掉 —— 否则它会从 <c>autoplay</c> 的 <c>idle</c> 第 0 帧重播，
/// 残影就和角色对不上（见 <see cref="SyncPose"/>）。</para>
///
/// <para>挂载：<c>Scenes/Player/Player.tscn</c> 的 <c>Player/Afterimage</c>（<c>Node3D</c>）。
/// 所有几何量 / 颜色 / 时长都是 <c>[Export]</c>。</para>
/// </summary>
public partial class AfterimageTrail : Node3D
{
	[ExportGroup("数据来源（只读运行期状态）")]
	/// <summary>哪一条技能行是「后撤」（表里的技能行 Id）。留空 = 按类型找第一个。</summary>
	[Export] public string AbilityId { get; set; } = "skl_a1_skill_1";

	/// <summary>模型节点的约定名（与 <see cref="UnitAnimator"/> 用同一个约定）。</summary>
	[Export] public string ModelNodeName { get; set; } = "Model";

	[ExportGroup("残影（几何 / 颜色 / 时长）")]
	/// <summary>
	/// 两次留影的间隔（秒）。0.09 ≈ 0.35s 的无敌帧里留 4 个 —— 后撤总共只走 ~1.8m，
	/// 留太多会糊成一团（实测 0.045s / 8 个时 8 层轮廓叠在一起，看起来只是一片光晕）。
	/// </summary>
	[Export] public float SpawnInterval { get; set; } = 0.09f;

	/// <summary>每个残影活多久（秒）—— 淡出的时长就是它。</summary>
	[Export] public float GhostLifetime { get; set; } = 0.34f;

	/// <summary>残影刚出现时的不透明度。</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float GhostStartAlpha { get; set; } = 0.62f;

	/// <summary>残影颜色（不受光的纯色剪影，明暗只由这个颜色 + alpha 决定）。
	/// 取饱和一点的青蓝 —— 竞技场地面很白，淡色会看不见。</summary>
	[Export] public Color GhostColor { get; set; } = new(0.36f, 0.78f, 1f, 1f);

	/// <summary>同时最多留几个（保险丝；平时永远到不了）。</summary>
	[Export(PropertyHint.Range, "1,64,1")] public int MaxGhosts { get; set; } = 12;

	[ExportGroup("运行期可读状态（探针 / 自测）")]
	/// <summary>现在场上还活着几个残影。</summary>
	public int GhostCount { get; private set; }

	/// <summary>本次后撤一共留了几个影。</summary>
	public int TotalSpawned { get; private set; }

	/// <summary>现在是不是"无敌帧开着"（= 正在留影）。</summary>
	public bool TrailActive { get; private set; }

	/// <summary>最近一个残影里剩下的碰撞体数量（**必须恒为 0** —— 这是"没有碰撞"的证据）。</summary>
	public int LastGhostCollisionCount { get; private set; }

	/// <summary>最近一个残影里剩下的 <see cref="Unit"/> 数量（**必须恒为 0**）。</summary>
	public int LastGhostUnitCount { get; private set; }

	/// <summary>最近一个残影身上残留的分组名（**必须为空**）。</summary>
	public string LastGhostGroups { get; private set; } = string.Empty;

	/// <summary>最近一个残影的网格数量（探针用：0 = 这个模型复制不出来，看着就是"没效果"）。</summary>
	public int LastGhostMeshCount { get; private set; }

	/// <summary>最近一个残影抄过来的骨骼数量（探针用：0 = 没有骨架 / 没抄成功，姿势可能不对）。</summary>
	public int LastSyncedBones { get; private set; }

	private readonly List<Node3D> _ghosts = new();
	private Unit? _unit;
	private BackdashAbility? _dash;
	private Node3D? _model;
	private float _timer;
	private bool _wasActive;
	private bool _warnedNoModel;

	public override void _Ready()
	{
		_unit = GetParentOrNull<Unit>();
	}

	public override void _PhysicsProcess(double delta)
	{
		// 物理帧：残影是"位置快照"，与角色的实际位移同频取样才不会错拍
		// （`architecture.md` 第 11 条：渲染帧率 ≠ 物理帧率）。
		if (_unit == null || !IsInstanceValid(_unit))
		{
			_unit = GetParentOrNull<Unit>();
			_dash = null;
			_model = null;
		}

		Prune();

		if (_unit == null)
		{
			SetActive(false);
			return;
		}

		if (_dash == null || !IsInstanceValid(_dash))
		{
			_dash = ResolveDash();
		}

		// ⭐ 唯一的输入：战斗层给的只读量（P3 就有）。后撤没装备 / 不在无敌帧 → 恒 false。
		bool active = _unit is { IsDead: false } && _dash is { InvulnerableOwned: true };
		SetActive(active);

		if (!active)
		{
			return;
		}

		_timer -= (float)delta;
		if (_timer <= 0f)
		{
			_timer = Mathf.Max(0.01f, SpawnInterval);
			SpawnGhost();
		}
	}

	private void SetActive(bool active)
	{
		if (active == _wasActive)
		{
			return;
		}

		_wasActive = active;
		TrailActive = active;
		if (active)
		{
			_timer = 0f;   // 无敌帧一开立刻留第一个影
			GameLog.Write(
				"Combat",
				$"后撤残影：{AbilityId} 无敌帧开（{_dash?.InvulnerableFromEvent}）→ 每 {SpawnInterval:0.###}s 留一个残影，"
				+ $"每个淡出 {GhostLifetime:0.##}s（纯视觉：无碰撞 / 不进单位列表）");
		}
		else
		{
			GameLog.Write(
				"Combat",
				$"后撤残影：{AbilityId} 无敌帧关（{_dash?.InvulnerableToEvent}）—— 本次共留 {TotalSpawned} 个影，"
				+ $"现在场上还剩 {GhostCount} 个（会自己淡完回收）");
		}
	}

	/// <summary>按表里的行 Id 找「后撤」；填了 <see cref="AbilityId"/> 就只认它，否则按类型找第一个。</summary>
	private BackdashAbility? ResolveDash()
	{
		if (_unit == null)
		{
			return null;
		}

		if (!string.IsNullOrEmpty(AbilityId)
			&& Ability.FindByAbilityId(_unit, AbilityId) is BackdashAbility byId)
		{
			return byId;
		}

		foreach (Node node in _unit.FindChildren("*", string.Empty, true, false))
		{
			if (node is BackdashAbility found)
			{
				return found;
			}
		}

		return null;
	}

	private void Prune()
	{
		for (int i = _ghosts.Count - 1; i >= 0; i--)
		{
			if (_ghosts[i] == null || !IsInstanceValid(_ghosts[i]))
			{
				_ghosts.RemoveAt(i);
			}
		}

		GhostCount = _ghosts.Count;
	}

	/// <summary>
	/// 留一个影：复制 <c>Model</c> 子树 → <b>拆掉一切非视觉的东西 + 抄骨架姿势</b> →
	/// 挂到 <c>CurrentScene</c>（**不是**挂到玩家下）→ 摆到角色当前的世界变换 →
	/// 换成半透明纯色材质 → 淡出后回收。
	/// </summary>
	private void SpawnGhost()
	{
		if (_ghosts.Count >= Mathf.Max(1, MaxGhosts))
		{
			return;
		}

		if (_model == null || !IsInstanceValid(_model))
		{
			_model = _unit?.GetNodeOrNull<Node3D>(ModelNodeName);
		}

		if (_model == null)
		{
			if (!_warnedNoModel)
			{
				_warnedNoModel = true;
				GameLog.Write("Combat", $"⚠ 后撤残影：找不到模型节点「{ModelNodeName}」（模型名换过？）—— 本次不留影。");
			}

			return;
		}

		Node? host = GetTree()?.CurrentScene;
		if (host == null || !IsInstanceValid(host))
		{
			return;
		}

		if (_model.Duplicate() is not Node3D ghost)
		{
			return;
		}

		ghost.Name = $"Afterimage_{TotalSpawned}";
		Sanitize(ghost);
		host.AddChild(ghost);
		ghost.GlobalTransform = _model.GlobalTransform;

		// 半透明纯色剪影：不受光、不投影、只保留轮廓。
		var material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = new Color(GhostColor.R, GhostColor.G, GhostColor.B, GhostStartAlpha),
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		};

		int meshes = 0;
		foreach (Node node in Snapshot(ghost))
		{
			if (node is MeshInstance3D mesh)
			{
				mesh.MaterialOverride = material;
				mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
				meshes++;
			}
		}

		LastGhostMeshCount = meshes;
		TotalSpawned++;
		_ghosts.Add(ghost);
		GhostCount = _ghosts.Count;

		Tween tween = ghost.CreateTween();
		tween.TweenMethod(
				Callable.From<float>(a =>
				{
					if (IsInstanceValid(material))
					{
						material.AlbedoColor = new Color(GhostColor.R, GhostColor.G, GhostColor.B, a);
					}
				}),
				GhostStartAlpha,
				0f,
				Mathf.Max(0.02f, GhostLifetime))
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.In);   // 先亮着、尾巴收得快 → 看着像"拖出去的一条"
		tween.TweenCallback(Callable.From(() =>
		{
			if (IsInstanceValid(ghost))
			{
				_ghosts.Remove(ghost);
				GhostCount = _ghosts.Count;
				ghost.QueueFree();
			}
		}));
	}

	/// <summary>
	/// **把"复制出来的模型"变成"纯视觉"**：摘掉一切能参与物理 / 战斗 / 逻辑的东西，退出所有分组，
	/// 再把"那一刻的姿势"抄过来。
	///
	/// <para>做的是减法而不是"小心不要复制" —— <c>Model</c> 子树将来可能长出新东西，减法对
	/// "将来新增的组件"同样成立。每个动作都留了一个可数的取证量。</para>
	/// </summary>
	private void Sanitize(Node3D ghost)
	{
		List<Node> all = Snapshot(ghost);
		int collisions = 0;
		int units = 0;

		// ① 物理：CollisionObject3D 覆盖 Area3D / CharacterBody3D / StaticBody3D / RigidBody3D。
		//    ⚠️ 必须 RemoveChild（立刻离开物理世界）再 QueueFree —— 只 QueueFree 的话本帧还在。
		// ② 逻辑：任何 Unit —— 残影绝不能是个"单位"。
		// ③ AnimationTree：状态机会驱动骨骼，残影要的是"定格"。
		// ④ AnimationPlayer：同理，它会从 autoplay 的 idle 第 0 帧重播（姿势随后由 SyncPose 抄好）。
		foreach (Node node in all)
		{
			bool doomed = node is CollisionObject3D || node is Unit || node is AnimationTree || node is AnimationPlayer;
			if (!doomed)
			{
				continue;
			}

			if (node is CollisionObject3D)
			{
				collisions++;
			}
			else if (node is Unit)
			{
				units++;
			}

			node.GetParent()?.RemoveChild(node);
			node.QueueFree();
		}

		// ⑤ 姿势：在摘掉 AnimationPlayer 之后把源骨架的骨骼姿势逐个抄过来（否则是 bind pose）。
		SyncPose(ghost);

		// ⑥ 分组：残影不属于任何分组（`enemy_units` / `player_units` / `projectiles` 都不进）。
		var groups = new List<string>();
		foreach (Node node in all)
		{
			if (node is CollisionObject3D or Unit || !IsInstanceValid(node))
			{
				continue;
			}

			foreach (StringName group in node.GetGroups())
			{
				groups.Add($"{node.Name}:{group}");
				node.RemoveFromGroup(group);
			}
		}

		LastGhostCollisionCount = collisions;
		LastGhostUnitCount = units;
		LastGhostGroups = groups.Count == 0 ? string.Empty : string.Join("、", groups);
	}

	/// <summary>把源骨架的骨骼姿势抄到刚复制出来的骨架上（"定格在那一刻"）。</summary>
	private void SyncPose(Node3D ghost)
	{
		LastSyncedBones = 0;
		if (_model == null || !IsInstanceValid(_model) || !IsInstanceValid(ghost))
		{
			return;
		}

		List<Skeleton3D> source = Skeletons(_model);
		List<Skeleton3D> target = Skeletons(ghost);
		int pairs = Mathf.Min(source.Count, target.Count);
		for (int s = 0; s < pairs; s++)
		{
			Skeleton3D from = source[s];
			Skeleton3D to = target[s];
			int bones = Mathf.Min(from.GetBoneCount(), to.GetBoneCount());
			for (int i = 0; i < bones; i++)
			{
				to.SetBonePose(i, from.GetBonePose(i));
			}

			LastSyncedBones += bones;
		}
	}

	/// <summary>子树里的骨架（先序）。</summary>
	private static List<Skeleton3D> Skeletons(Node root)
	{
		var list = new List<Skeleton3D>();
		foreach (Node node in Snapshot(root))
		{
			if (node is Skeleton3D skeleton)
			{
				list.Add(skeleton);
			}
		}

		return list;
	}

	/// <summary>包含自己在内的子树快照（先序）—— 先取快照再改树，避免边遍历边删子节点。</summary>
	private static List<Node> Snapshot(Node root)
	{
		var all = new List<Node>();
		Collect(root, all);
		return all;
	}

	private static void Collect(Node node, List<Node> into)
	{
		into.Add(node);
		foreach (Node child in node.GetChildren())
		{
			Collect(child, into);
		}
	}

	public override void _ExitTree()
	{
		foreach (Node3D ghost in _ghosts)
		{
			if (ghost != null && IsInstanceValid(ghost))
			{
				ghost.QueueFree();
			}
		}

		_ghosts.Clear();
		GhostCount = 0;
	}

	/// <summary>探针用：一行的实际状态（截图与自检共用一份判据）。</summary>
	public string DebugState()
		=> $"后撤残影：无敌帧={TrailActive} 场上残影={GhostCount} 累计={TotalSpawned}"
			+ $"（间隔 {SpawnInterval:0.###}s 存活 {GhostLifetime:0.##}s 起始 alpha {GhostStartAlpha:0.##} 上限 {MaxGhosts}）"
			+ $"｜只读源 技能行={AbilityId} 无敌帧拥有={_dash?.InvulnerableOwned ?? false}"
			+ $"｜安全性 最近残影 碰撞体={LastGhostCollisionCount}（必须 0） 单位={LastGhostUnitCount}（必须 0）"
			+ $" 分组={(string.IsNullOrEmpty(LastGhostGroups) ? "（无）" : LastGhostGroups)}（必须无）"
			+ $" 网格={LastGhostMeshCount} 抄骨骼={LastSyncedBones}";
}
