using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.Audio;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// **蓄力时的身体表现 + 蓄力完成反馈**（P7 表现层，用户本轮提的第 ③ 条；设计文档的蓄力机制见
/// `docs/abilities.md`）。用户原话：
///
/// <blockquote>
/// 「**所有武器的蓄力表现，需要在每次完成蓄力时都有特效 + 音效的表现**，比如**角色闪一下**或者
/// **有一圈扩大的透明残影或描边**」
/// </blockquote>
///
/// <para><b>触发点只有一个</b>：某个 <see cref="Ability"/> 的 <see cref="Ability.ChargeBaseComplete"/>
/// **由 false 变 true** 的那一帧 —— **每次完成基础蓄力**都给一次反馈，
/// 所有蓄力武器（能量掌武技2 / 弓武技1 武技2 / 短弩武技2 / 长枪武技2）共用这一份实现，
/// **加武器不用改代码**（它是遍历单位身上的全部 <see cref="Ability"/>，不认类名、不认技能行 Id）。</para>
///
/// <para><b>一次反馈由三样东西组成</b>（用户给的三个可选形态全用上了，都是 <c>[Export]</c>）：</para>
/// <list type="number">
/// <item><b>角色闪一下</b>：把 <c>Assets/Effects/charge_rim_placeholder.gdshader</c> 包成
///   <c>ShaderMaterial</c>，挂到模型每个 <c>MeshInstance3D</c> 的 <b><c>MaterialOverlay</c></b>
///   （**叠加通道**：不改模型资源、不动 <c>MaterialOverride</c>，所以与受击闪色 / 精英染色 /
///   残影互不干扰），把 <c>flash</c> 从 <see cref="FlashStrength"/> 线性衰减到 0。</item>
/// <item><b>一圈扩大的透明圈</b>：**直接复用 P6c 的 <see cref="LandingShockwave"/>**（P6d 给它加的
///   <c>PlayOnce(位置, 半径)</c> 外部入口正是为这种"不是落地"的场合准备的）——
///   本类按自己的 <c>[Export]</c> 半径 / 颜色 / 时长配置它，不为"一圈"再写第二份实现。</item>
/// <item><b>音效</b>：<see cref="ChargeCompleteSfxId"/>（默认 <see cref="AudioIds.ChargeComplete"/>
///   = 音效表里的 <c>sfx_charge_complete</c>）。**代码里没有文件路径** ——
///   素材 / 音量 / 总线都在 `Config/Tables/Audio/`。</item>
/// </list>
///
/// <para><b>「基础蓄力完成」与「进入完美窗口」怎么区分</b>（这是本轮要判断的地方）：在本项目的状态机里
/// 这两件事**发生在同一帧** —— <see cref="Ability.IsPerfectWindow"/> 的窗口就是
/// <c>[ChargeBaseSeconds, +PerfectWindowSeconds]</c>，它的起点正是基础蓄力完成的那一刻。
/// 所以**不做两次叠加反馈**（同一帧叠两次只会看着像一次更乱的），而是：**同一份反馈里换档** ——
/// 走完美窗口的那一档用 <see cref="PerfectFlashColor"/>（金）/ <see cref="PerfectStrengthScale"/> 倍强度 /
/// <see cref="PerfectRingColor"/> 的圈 / <see cref="PerfectRingScale"/> 倍的半径，
/// 音效仍是同一拍（它就是"蓄力完成"这一个事件）。</para>
///
/// <para>⚠️ <b>它只读运行期状态，不参与判定</b>：读的全是 P2 就有的
/// <see cref="Ability.IsCharging"/> / <see cref="Ability.ChargeBaseComplete"/> /
/// <see cref="Ability.IsPerfectWindow"/>，**没有**给战斗层加任何东西，`Ability` 基类一个字没动。</para>
///
/// <para>⚠️ <b>纯表现要做减法</b>（`docs/vfx.md` §3）：本类**不复制模型子树**（那是残影的做法，
/// 要摘碰撞体 / 摘 Unit / 退分组），只做两件事 —— 往已有网格上挂一个**叠加材质**、
/// 让 <see cref="LandingShockwave"/> 在 <c>CurrentScene</c> 下造一个**临时**的圈节点
/// （它自己没有碰撞体、不进任何分组、tween 完自己 <c>QueueFree</c>）。<see cref="DebugState"/> 会把
/// 这几项打出来。</para>
///
/// <para>挂载：<c>Scenes/Player/Player.tscn</c> → <c>Player/ChargeBodyFx</c>。所有几何量 / 颜色 /
/// 时长 / 音效 id 都是 <c>[Export]</c>。</para>
/// </summary>
public partial class ChargeBodyFx : Node3D
{
	[ExportGroup("数据来源（只读运行期状态）")]
	/// <summary>模型节点的约定名（与 <see cref="AfterimageTrail"/> / <see cref="UnitAnimator"/> 同一个约定）。</summary>
	[Export] public string ModelNodeName { get; set; } = "Model";

	/// <summary>找不到模型时用的兜底：直接从单位子树里找全部 <c>MeshInstance3D</c>。</summary>
	[Export] public bool FallbackToUnitMeshes { get; set; } = true;

	[ExportGroup("占位描边（第 ④ 条 · ⚠️ 美术后期替换）")]
	/// <summary>
	/// 菲涅尔边缘光的着色器（**占位版**）。换成正式版就是把这个 <c>.gdshader</c> 文件替换掉 /
	/// 或者在这里换一个路径 —— **C# 一个字都不用改**。加载失败时写一条警告并退回代码里的内联兜底。
	/// </summary>
	[Export] public string RimShaderPath { get; set; } = "res://Assets/Effects/charge_rim_placeholder.gdshader";

	/// <summary>边缘光的颜色（蓄力期间）。</summary>
	[Export] public Color RimColor { get; set; } = new(0.45f, 0.86f, 1f, 1f);

	/// <summary>菲涅尔次方：越大，亮的那一圈越贴着轮廓（越小越"整个身体发亮"）。</summary>
	[Export(PropertyHint.Range, "0.5,8,0.1")] public float RimPower { get; set; } = 2.6f;

	/// <summary>蓄力期间边缘光的强度（0 = 关掉描边这一条）。</summary>
	[Export(PropertyHint.Range, "0,4,0.05")] public float RimStrength { get; set; } = 1.2f;

	/// <summary>描边的淡入 / 淡出时长（秒）—— 蓄力开始/结束不该"啪"地跳出来。</summary>
	[Export] public float RimFadeSeconds { get; set; } = 0.12f;

	[ExportGroup("蓄力完成反馈（第 ③ 条）")]
	/// <summary>完成基础蓄力时播的音效 id（**必须与 `Config/Tables/Audio/` 里的行 Id 一致**）。</summary>
	[Export] public string ChargeCompleteSfxId { get; set; } = AudioIds.ChargeComplete;

	/// <summary>"闪一下"的时长（秒）。</summary>
	[Export] public float FlashSeconds { get; set; } = 0.22f;

	/// <summary>"闪一下"的强度（1 = 满亮；衰减到 0 就是闪完）。</summary>
	[Export(PropertyHint.Range, "0,2,0.05")] public float FlashStrength { get; set; } = 0.85f;

	/// <summary>闪光的颜色（**基础蓄力完成**那一档）。</summary>
	[Export] public Color FlashColor { get; set; } = new(0.85f, 0.95f, 1f, 1f);

	/// <summary>闪光的颜色（**同时进了完美窗口**那一档 —— 只有能量掌武技2 这类有窗口的技能会用）。</summary>
	[Export] public Color PerfectFlashColor { get; set; } = new(1f, 0.86f, 0.35f, 1f);

	/// <summary>完美窗口那一档的强度 / 半径倍率（"用颜色 + 强度区分"的那一半）。</summary>
	[Export] public float PerfectStrengthScale { get; set; } = 1.8f;

	[ExportGroup("那一圈扩大的透明圈（复用 LandingShockwave）")]
	/// <summary>圈的基础半径（米）。</summary>
	[Export] public float RingRadius { get; set; } = 1.5f;

	/// <summary>完美窗口那一档的半径倍率。</summary>
	[Export] public float PerfectRingScale { get; set; } = 1.35f;

	/// <summary>圈的离地高度（米）—— 蓄力反馈挂在**身体中段**，不是脚下。</summary>
	[Export] public float RingHeight { get; set; } = 1.0f;

	/// <summary>圈的扩散时长（秒）。</summary>
	[Export] public float RingDuration { get; set; } = 0.45f;

	/// <summary>圈的起始缩放（小一点才有"扩大"感）。</summary>
	[Export(PropertyHint.Range, "0.01,1,0.01")] public float RingStartScale { get; set; } = 0.35f;

	/// <summary>圈的颜色（基础蓄力完成）。</summary>
	[Export] public Color RingColor { get; set; } = new(0.5f, 0.9f, 1f, 0.9f);

	/// <summary>圈的颜色（完美窗口那一档）。</summary>
	[Export] public Color PerfectRingColor { get; set; } = new(1f, 0.86f, 0.35f, 0.9f);

	/// <summary>圈里那个薄盘的不透明度（色相跟着圈走）。</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float RingDiscAlpha { get; set; } = 0.16f;

	/// <summary>圈的自发光强度。</summary>
	[Export] public float RingEmissionEnergy { get; set; } = 2.4f;

	[ExportGroup("运行期可读状态（探针 / 自测）")]
	/// <summary>现在有没有人在蓄力（= 描边该不该亮）。</summary>
	public bool Charging { get; private set; }

	/// <summary>现在实际画出来的描边强度（0 = 关着）。</summary>
	public float RimShown { get; private set; }

	/// <summary>现在实际画出来的闪光强度（0 = 没在闪）。</summary>
	public float FlashShown { get; private set; }

	/// <summary>一共完成过几次基础蓄力（= 反馈次数 = 音效次数）。</summary>
	public int ChargeCompleteCount { get; private set; }

	/// <summary>最近一次完成的是哪个技能行 / 有没有走完美那一档。</summary>
	public string LastChargeAbilityId { get; private set; } = "（还没完成过）";

	/// <summary>最近一次走的是不是完美窗口那一档。</summary>
	public bool LastWasPerfect { get; private set; }

	/// <summary>最近一次播的音效 id（自测取证：证明表里有这一条、真的播了）。</summary>
	public string LastSfxId { get; private set; } = "（还没播过）";

	/// <summary>着色器是从文件加载的还是用的代码内联兜底（探针用：证明那个 .gdshader 真的生效了）。</summary>
	public string ShaderSource { get; private set; } = "（还没加载）";

	/// <summary>挂上叠加材质的网格数（0 = 找不到模型，看着就是"没效果"）。</summary>
	public int OverlayMeshCount { get; private set; }

	/// <summary>最近一次生成的那圈在世界里的节点名残留数（探针用：圈自己会 tween 完回收）。</summary>
	public int LiveRingCount
	{
		get
		{
			Node? scene = GetTree()?.CurrentScene;
			if (scene == null || !IsInstanceValid(scene))
			{
				return 0;
			}

			return scene.FindChildren($"*{RingNodeName}*", string.Empty, true, false).Count;
		}
	}

	private const string RingNodeName = "LandingShockwaveFx";

	private sealed class MeshBinding
	{
		public MeshInstance3D Mesh = null!;
		public ShaderMaterial Material = null!;
		public Material? PreviousOverlay;
	}

	private readonly List<MeshBinding> _bindings = new();
	private readonly List<Ability> _abilities = new();
	private readonly List<bool> _wasComplete = new();
	private Unit? _unit;
	private Node3D? _model;
	private LandingShockwave? _ring;
	private Shader? _shader;
	private bool _warnedNoShader;
	private bool _warnedNoMeshes;
	private float _rimShown;
	private float _flashShown;
	private Color _flashColor = Colors.White;

	/// <summary>代码里的**内联兜底着色器**：`RimShaderPath` 读不到时用它（效果相同，只是不便于美术替换）。</summary>
	private const string FallbackShaderCode = @"
shader_type spatial;
render_mode unshaded, blend_add, cull_back, depth_draw_never;
uniform vec4 rim_color : source_color = vec4(0.45, 0.86, 1.0, 1.0);
uniform float rim_power : hint_range(0.5, 8.0) = 2.6;
uniform float rim_strength : hint_range(0.0, 4.0) = 0.0;
uniform vec4 flash_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float flash : hint_range(0.0, 2.0) = 0.0;
void fragment() {
	float facing = clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
	float fresnel = pow(1.0 - facing, rim_power);
	ALBEDO = rim_color.rgb * fresnel * rim_strength + flash_color.rgb * flash * flash_color.a;
	ALPHA = clamp((fresnel * rim_strength * rim_color.a) + (flash * flash_color.a), 0.0, 1.0);
}
";

	public override void _Ready()
	{
		_unit = GetParentOrNull<Unit>();
	}

	public override void _ExitTree()
	{
		// 退出时把叠加材质**还给原样**（我们只借了这个通道，不能留下痕迹）。
		foreach (MeshBinding binding in _bindings)
		{
			if (binding.Mesh != null && IsInstanceValid(binding.Mesh))
			{
				binding.Mesh.MaterialOverlay = binding.PreviousOverlay;
			}
		}

		_bindings.Clear();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		if (_unit == null || !IsInstanceValid(_unit))
		{
			_unit = GetParentOrNull<Unit>();
			_abilities.Clear();
			_wasComplete.Clear();
		}

		EnsureBindings();
		EnsureAbilities();

		bool charging = ScanCharging(dt);
		Charging = charging;

		// ── 描边（第 ④ 条）：蓄力期间亮、结束就淡出；**它只是"叠加"**，不改任何原有材质。
		float rimTarget = charging ? Mathf.Max(0f, RimStrength) : 0f;
		float fade = Mathf.Max(0.01f, RimFadeSeconds);
		float step = dt / fade;
		_rimShown = Mathf.MoveToward(_rimShown, rimTarget, step);

		// ── 闪光（第 ③ 条）：完成那一帧置满，之后线性衰减到 0。
		if (_flashShown > 0f)
		{
			_flashShown = Mathf.Max(0f, _flashShown - (dt / Mathf.Max(0.02f, FlashSeconds)));
		}

		RimShown = _rimShown;
		FlashShown = _flashShown;
		ApplyUniforms();
	}

	/// <summary>
	/// 每帧扫一遍单位的全部能力，返回"有没有人在蓄力"，并在
	/// <see cref="Ability.ChargeBaseComplete"/> 的**上升沿**上发一次反馈（见类注释）。
	/// </summary>
	private bool ScanCharging(float dt)
	{
		bool charging = false;
		for (int i = 0; i < _abilities.Count; i++)
		{
			Ability ability = _abilities[i];
			bool isCharging = IsInstanceValid(ability) && ability.IsCharging;
			charging |= isCharging;

			bool complete = isCharging && ability.ChargeBaseComplete;
			bool before = i < _wasComplete.Count && _wasComplete[i];
			if (i < _wasComplete.Count)
			{
				_wasComplete[i] = complete;
			}

			if (complete && !before)
			{
				FireChargeComplete(ability);
			}
		}

		return charging;
	}

	/// <summary>**每次完成基础蓄力**走一次：换档 → 闪一下 + 一圈 + 音效。</summary>
	private void FireChargeComplete(Ability ability)
	{
		// 「进入完美窗口」与「基础蓄力完成」在本项目的状态机里是**同一帧**（窗口就从这一刻起算），
		// 所以不叠两次反馈：同一份反馈换一档（颜色 / 强度 / 半径），音效仍是同一拍。
		bool perfect = ability.IsPerfectWindow;
		float scale = perfect ? Mathf.Max(0.01f, PerfectStrengthScale) : 1f;

		_flashColor = perfect ? PerfectFlashColor : FlashColor;
		_flashShown = Mathf.Clamp(FlashStrength * scale, 0f, 2f);

		SpawnRing(perfect, scale);

		LastChargeAbilityId = ability.AbilityId;
		LastWasPerfect = perfect;
		ChargeCompleteCount++;

		if (!string.IsNullOrEmpty(ChargeCompleteSfxId))
		{
			AudioManager.PlaySfx(ChargeCompleteSfxId);
			LastSfxId = ChargeCompleteSfxId;
		}

		GameLog.Write(
			"Combat",
			$"蓄力完成反馈：{ability.AbilityId} 基础蓄力完成{(perfect ? "（同时在完美时机窗口里 → 金色 / ×" + Mathf.Max(0.01f, PerfectStrengthScale).ToString("0.##") + " 档）" : string.Empty)}"
			+ $" → 角色闪一下 {FlashSeconds:0.##}s（强度 {_flashShown:0.##}）+ 一圈扩大 {RingRadius * scale:0.##}m（{RingDuration:0.##}s）+ 音效「{ChargeCompleteSfxId}」"
			+ $"（第 {ChargeCompleteCount} 次；纯表现：不改战斗状态 / 无碰撞体 / 不进分组）");
	}

	/// <summary>在角色身体中段生成一圈（**复用 <see cref="LandingShockwave"/>**，不为"一圈"写第二份实现）。</summary>
	private void SpawnRing(bool perfect, float scale)
	{
		if (_ring == null || !IsInstanceValid(_ring))
		{
			_ring = new LandingShockwave
			{
				Name = "ChargeRing",
				PlayOnceSourceLabel = "蓄力完成",
				DrawDisc = RingDiscAlpha > 0f,
				AdditiveBlend = false,
				GroundOffset = 0f,
			};
			AddChild(_ring);
		}

		Color ring = perfect ? PerfectRingColor : RingColor;
		_ring.Duration = Mathf.Max(0.05f, RingDuration);
		_ring.StartScale = Mathf.Clamp(RingStartScale, 0.01f, 1f);
		_ring.RingColor = ring;
		_ring.DiscColor = new Color(ring.R, ring.G, ring.B, RingDiscAlpha);
		_ring.EmissionEnergy = RingEmissionEnergy;
		_ring.ExtraHeight = RingHeight;

		Vector3 at = _unit != null && IsInstanceValid(_unit) ? _unit.GlobalPosition : GlobalPosition;
		_ring.PlayOnce(at, Mathf.Max(0.1f, RingRadius) * scale);
	}

	/// <summary>把"该给谁挂叠加材质"备好：模型子树里的全部 <c>MeshInstance3D</c>（**每个网格一份材质**）。</summary>
	private void EnsureBindings()
	{
		if (_bindings.Count > 0)
		{
			return;
		}

		if (_shader == null)
		{
			_shader = LoadShader();
		}

		if (_shader == null)
		{
			return;
		}

		if (_model == null || !IsInstanceValid(_model))
		{
			_model = _unit?.GetNodeOrNull<Node3D>(ModelNodeName);
		}

		List<MeshInstance3D> meshes = new();
		if (_model != null && IsInstanceValid(_model))
		{
			CollectMeshes(_model, meshes);
		}
		else if (FallbackToUnitMeshes && _unit != null && IsInstanceValid(_unit))
		{
			CollectMeshes(_unit, meshes);
		}

		if (meshes.Count == 0)
		{
			if (!_warnedNoMeshes && _unit != null)
			{
				_warnedNoMeshes = true;
				GameLog.Write("Combat", $"⚠ 蓄力身体表现：在 {ModelNodeName} 里找不到 MeshInstance3D —— 描边与闪光这一轮不会出现（音效与那一圈照常）。");
			}

			return;
		}

		foreach (MeshInstance3D mesh in meshes)
		{
			var material = new ShaderMaterial { Shader = _shader };
			_bindings.Add(new MeshBinding
			{
				Mesh = mesh,
				Material = material,
				PreviousOverlay = mesh.MaterialOverlay,   // 借通道：退出时原样还回去
			});
		}

		OverlayMeshCount = _bindings.Count;
		GameLog.Write("Combat", $"蓄力身体表现：模型「{ModelNodeName}」上挂了 {_bindings.Count} 份叠加材质（{ShaderSource}；占位描边，美术后期替换）");
	}

	/// <summary>读着色器：先按 <see cref="RimShaderPath"/> 读文件，读不到就用代码里的内联兜底（**不静默**）。</summary>
	private Shader? LoadShader()
	{
		if (!string.IsNullOrEmpty(RimShaderPath))
		{
			if (ResourceLoader.Exists(RimShaderPath) && GD.Load<Shader>(RimShaderPath) is { } fromFile)
			{
				ShaderSource = $"文件 {RimShaderPath}";
				return fromFile;
			}
		}

		if (!_warnedNoShader)
		{
			_warnedNoShader = true;
			GameLog.Write("Combat", $"⚠ 蓄力身体表现：读不到着色器「{RimShaderPath}」→ 用代码里的内联兜底（效果相同，只是不便于美术替换）。");
		}

		ShaderSource = "代码内联兜底";
		var inline = new Shader { Code = FallbackShaderCode };
		return inline;
	}

	private static void CollectMeshes(Node root, List<MeshInstance3D> into)
	{
		foreach (Node child in root.GetChildren())
		{
			if (child is MeshInstance3D mesh)
			{
				into.Add(mesh);
			}

			CollectMeshes(child, into);
		}
	}

	/// <summary>把两个可调量写进每一份叠加材质（描边颜色 / 次方也一起，方便运行期调）。</summary>
	private void ApplyUniforms()
	{
		bool active = _rimShown > 0.001f || _flashShown > 0.001f;
		foreach (MeshBinding binding in _bindings)
		{
			if (binding.Mesh == null || !IsInstanceValid(binding.Mesh) || !IsInstanceValid(binding.Material))
			{
				continue;
			}

			// 只在"要画"的时候占着叠加通道；已经归零就把通道还回去（不影响别人用 MaterialOverlay）。
			binding.Mesh.MaterialOverlay = active ? binding.Material : binding.PreviousOverlay;

			binding.Material.SetShaderParameter("rim_color", RimColor);
			binding.Material.SetShaderParameter("rim_power", Mathf.Max(0.5f, RimPower));
			binding.Material.SetShaderParameter("rim_strength", _rimShown);
			binding.Material.SetShaderParameter("flash_color", _flashColor);
			binding.Material.SetShaderParameter("flash", _flashShown);
		}
	}

	/// <summary>重新收集单位身上的全部能力（**不认类名 / 技能行 Id**：加武器不用改这里）。</summary>
	private void EnsureAbilities()
	{
		for (int i = 0; i < _abilities.Count; i++)
		{
			if (!IsInstanceValid(_abilities[i]))
			{
				_abilities.Clear();
				_wasComplete.Clear();
				break;
			}
		}

		if (_abilities.Count > 0 || _unit == null || !IsInstanceValid(_unit))
		{
			return;
		}

		NodeSearch.CollectInSubtree(_unit, _abilities);
		for (int i = 0; i < _abilities.Count; i++)
		{
			_wasComplete.Add(false);
		}
	}

	// ───────────────── 拍摄台 / 探针（只摆视觉，不进战斗逻辑） ─────────────────

	/// <summary>拍摄台用：强制把描边 / 闪光摆成给定强度（不依赖真的按住右键）。传 0/0 = 撤掉。</summary>
	public void DebugPreviewChargeFx(float rim, float flash, bool perfect = false)
	{
		_rimShown = Mathf.Clamp(rim, 0f, 4f);
		_flashShown = Mathf.Clamp(flash, 0f, 2f);
		_flashColor = perfect ? PerfectFlashColor : FlashColor;
		RimShown = _rimShown;
		FlashShown = _flashShown;
		ApplyUniforms();
	}

	/// <summary>拍摄台用：不走按键，直接"完成一次基础蓄力"（音效 + 圈 + 闪，判据与画法一个字都不变）。</summary>
	public void DebugFireChargeComplete(bool perfect = false)
	{
		_flashColor = perfect ? PerfectFlashColor : FlashColor;
		_flashShown = Mathf.Clamp(FlashStrength * (perfect ? Mathf.Max(0.01f, PerfectStrengthScale) : 1f), 0f, 2f);
		SpawnRing(perfect, perfect ? Mathf.Max(0.01f, PerfectStrengthScale) : 1f);
		LastChargeAbilityId = "（拍摄台强制触发）";
		LastWasPerfect = perfect;
		ChargeCompleteCount++;
		if (!string.IsNullOrEmpty(ChargeCompleteSfxId))
		{
			AudioManager.PlaySfx(ChargeCompleteSfxId);
			LastSfxId = ChargeCompleteSfxId;
		}
	}

	/// <summary>探针用：一行的实际状态（截图与自检共用一份判据）。</summary>
	public string DebugState()
		=> $"蓄力身体表现：蓄力中={Charging} 描边={RimShown:0.##}（上限 {RimStrength:0.##} 淡入 {RimFadeSeconds:0.##}s）"
			+ $" 闪光={FlashShown:0.##}（{FlashSeconds:0.##}s 强度 {FlashStrength:0.##} 完美档 ×{PerfectStrengthScale:0.##}）"
			+ $"｜完成次数={ChargeCompleteCount} 最近={LastChargeAbilityId}（完美档={LastWasPerfect}）"
			+ $" 最近音效={LastSfxId}"
			+ $"｜叠加材质 {OverlayMeshCount} 份（{ShaderSource}） 场上那圈 {LiveRingCount} 个"
			+ $"｜只读源 能力 {_abilities.Count} 个 IsCharging/ChargeBaseComplete/IsPerfectWindow（**没有**新的战斗层属性）"
			+ $"｜纯表现 不改 MaterialOverride / 无碰撞体 / 不进任何分组";
}
