using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.Audio;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 统一蓄力表现：监听当前槽位能力的真实基础完成与强化档位事件，输出短促全身菲涅尔闪光和阶段音效。
/// 基础完成保留扩张环，强化档位只强调身体轮廓与音高，避免反复铺设范围圈。
/// 不改原材质/伤害/动画；受击等更高优先级叠加可抢占通道。空闲停止更新，取消/换装/死亡归还材质。
/// </summary>
public partial class ChargeBodyFx : Node3D
{
	[ExportGroup("数据来源（只读运行期状态）")]
	/// <summary>模型节点的约定名（与 <see cref="AfterimageTrail"/> / <see cref="UnitAnimator"/> 同一个约定）。</summary>
	[Export] public string ModelNodeName { get; set; } = "Model";

	/// <summary>找不到模型时用的兜底：直接从单位子树里找全部 <c>MeshInstance3D</c>。</summary>
	[Export] public bool FallbackToUnitMeshes { get; set; } = true;

	[ExportGroup("蓄力边缘光")]
	/// <summary>
	/// 菲涅尔与薄层身体闪光的着色器。加载失败时写一条警告并退回相同画法的内联兜底。
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
	[Export] public float FlashSeconds { get; set; } = 0.18f;

	/// <summary>"闪一下"的强度（1 = 满亮；衰减到 0 就是闪完）。</summary>
	[Export(PropertyHint.Range, "0,2,0.05")] public float FlashStrength { get; set; } = 0.85f;

	/// <summary>闪光的颜色（**基础蓄力完成**那一档）。</summary>
	[Export] public Color FlashColor { get; set; } = new(0.65f, 0.88f, 1f, 1f);

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
	[Export] public Color RingColor { get; set; } = new(0.5f, 0.9f, 1f, 0.6f);

	/// <summary>圈的颜色（完美窗口那一档）。</summary>
	[Export] public Color PerfectRingColor { get; set; } = new(1f, 0.86f, 0.35f, 0.9f);

	/// <summary>圈里那个薄盘的不透明度（色相跟着圈走）。</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float RingDiscAlpha { get; set; } = 0.08f;

	/// <summary>圈的自发光强度。</summary>
	[Export] public float RingEmissionEnergy { get; set; } = 1.4f;

	[ExportGroup("运行期可读状态（探针 / 自测）")]
	/// <summary>现在有没有人在蓄力（= 描边该不该亮）。</summary>
	public bool Charging { get; private set; }

	/// <summary>现在实际画出来的描边强度（0 = 关着）。</summary>
	public float RimShown { get; private set; }

	/// <summary>现在实际画出来的闪光强度（0 = 没在闪）。</summary>
	public float FlashShown { get; private set; }

	/// <summary>基础完成反馈次数；后置强化档位单独记入ChargeStepCount。</summary>
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
	public Node3D? BoundModel => _model;

	/// <summary>最近一次生成的那圈在世界里的节点名残留数（探针用：圈自己会 tween 完回收）。</summary>
	public int LiveRingCount
	{
		get
		{
			return _ownedRings.FindAll(ring => IsInstanceValid(ring) && !ring.IsQueuedForDeletion()).Count;
		}
	}

	private sealed class MeshBinding
	{
		public MeshInstance3D Mesh = null!;
		public ShaderMaterial Material = null!;
	}

	private readonly List<MeshBinding> _bindings = new();
	private readonly List<Ability> _abilities = new();
	private readonly List<Node3D> _ownedRings = new();
	private int _slotRevision = -1;
	private Ability? _lastPulseSource;
	private Ability? _chargingSource;
	[ExportGroup("强化档位反馈")]
	[Export] public Color StepFlashColor { get; set; } = new(.43f, .95f, .73f);
	[Export] public float StepFlashScale { get; set; } = 1.15f;
	[Export] public float StepPitchIncrement { get; set; } = .08f;
	public int ChargeStepCount { get; private set; }
	public int LastChargeStage { get; private set; } = -1;
	public float LastSfxPitch { get; private set; } = 1;
	public Color LastFlashColor => _flashColor;
	private Unit? _unit;
	private Node3D? _model;
	private LandingShockwave? _ring;
	private Shader? _shader;
	private bool _warnedNoShader;
	private bool _warnedNoMeshes;
	private float _rimShown;
	private float _flashShown;
	private float _flashPeak, _flashElapsed;
	private Color _flashColor = Colors.White;

	// 拍摄台 / 探针用的强制状态（只为截图与取证，不依赖真的按住右键）。
	private bool _previewActive;
	private float _previewRim;
	private float _previewFlash;
	private Color _previewFlashColor = Colors.White;

	/// <summary>代码里的**内联兜底着色器**：`RimShaderPath` 读不到时用它（效果相同，只是不便于美术替换）。</summary>
	private const string FallbackShaderCode = @"
shader_type spatial;
render_mode unshaded, blend_mix, cull_back, depth_draw_never;
uniform vec4 rim_color : source_color = vec4(0.45, 0.86, 1.0, 1.0);
uniform float rim_power : hint_range(0.5, 8.0) = 2.6;
uniform float rim_strength : hint_range(0.0, 4.0) = 0.0;
uniform vec4 flash_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float flash : hint_range(0.0, 2.0) = 0.0;
void fragment() {
	float facing = clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
	float fresnel = pow(1.0 - facing, rim_power);
	float total = max(0.001, rim_strength + flash);
	ALBEDO = (rim_color.rgb * rim_strength + flash_color.rgb * flash) / total;
	EMISSION = ALBEDO * 0.85;
	float body_flash = min(flash, 1.0) * flash_color.a * 0.12;
	float edge_alpha = fresnel * clamp(rim_strength * rim_color.a * 0.45 + flash * flash_color.a * 0.45, 0.0, 0.52);
	ALPHA = clamp(body_flash + edge_alpha, 0.0, 0.62);
}
";

	public override void _Ready()
	{
		_unit = GetParentOrNull<Unit>();
		SetProcess(false);
		if (_unit != null)
		{
			EnsureAbilities(); _unit.ChildEnteredTree += HookAbility; _unit.ChildExitingTree += UnhookAbility;
			_unit.Died += ClearOnDeath;
		}
	}

	public override void _ExitTree()
	{
		ClearVisuals();
		foreach (var ability in _abilities.ToArray()) UnhookAbility(ability);
		if (IsInstanceValid(_unit)) { _unit!.ChildEnteredTree -= HookAbility; _unit.ChildExitingTree -= UnhookAbility; _unit.Died -= ClearOnDeath; }
		UnitRimOverlay.RemoveSource(this);
		_bindings.Clear();
	}

	/// <summary>换角色时归还旧模型的叠加通道，下一帧绑定当前可见模型。</summary>
	public void BindVisual(Node3D model)
	{
		if (_model == model) return;
		ClearVisuals();
		_bindings.Clear();
		_model = model;
	}

	public override void _Process(double delta)
	{
		float dt = Mathf.Min((float)delta, .05f);
		if (!_previewActive && (_unit is not { IsDead: false } || _slotRevision != _unit.AbilitySlots.Revision))
		{
			ClearVisuals();
			return;
		}
		EnsureBindings();

		// 拍摄台：钉住不动（只摆视觉，判据与画法一个字都不变）。
		if (_previewActive)
		{
			ApplyPreview();
			return;
		}

		if (_unit == null || !IsInstanceValid(_unit))
		{
			_unit = GetParentOrNull<Unit>();
			_abilities.Clear();
		}

		bool charging = ScanCharging();
		Charging = charging;

		// ── 描边（第 ④ 条）：蓄力期间亮、结束就淡出；**它只是"叠加"**，不改任何原有材质。
		float rimTarget = charging ? Mathf.Max(0f, RimStrength) : 0f;
		float fade = Mathf.Max(0.01f, RimFadeSeconds);
		float step = dt / fade;
		_rimShown = Mathf.MoveToward(_rimShown, rimTarget, step);

		// 峰值不影响持续时间；短暂保持后平滑消退，完美档也不会拖成一整块白光。
		if (_flashShown > 0f)
		{
			_flashElapsed += dt;
			float progress = Mathf.Clamp(_flashElapsed / Mathf.Max(.02f, FlashSeconds), 0, 1);
			_flashShown = _flashPeak * (1 - Mathf.SmoothStep(.18f, 1, progress));
		}

		RimShown = _rimShown;
		FlashShown = _flashShown;
		ApplyUniforms();
		if (!charging && _rimShown <= 0 && _flashShown <= 0) SetProcess(false);
	}

	/// <summary>
	/// 只读取当前槽位的蓄力状态来维持边缘光；阶段声光由真实状态机事件驱动。
	/// </summary>
	private bool ScanCharging()
	{
		bool charging = false;
		for (int i = 0; i < _abilities.Count; i++)
		{
			Ability ability = _abilities[i];
			bool isCharging = IsInstanceValid(ability) && ability.Enabled && _unit?.AbilitySlots.Owns(ability) == true && ability.IsCharging;
			charging |= isCharging;
		}

		return charging;
	}

	/// <summary>**每次完成基础蓄力**走一次：换档 → 闪一下 + 一圈 + 音效。</summary>
	private void FireChargeComplete(Ability ability, int stage)
	{
		// 「进入完美窗口」与「基础蓄力完成」在本项目的状态机里是**同一帧**（窗口就从这一刻起算），
		// 所以不叠两次反馈：同一份反馈换一档（颜色 / 强度 / 半径），音效仍是同一拍。
		bool perfect = stage == 0 && ability.IsPerfectWindow;
		float scale = stage > 0 ? StepFlashScale : perfect ? Mathf.Max(0.01f, PerfectStrengthScale) : 1f;

		_flashColor = stage > 0 ? StepFlashColor : perfect ? PerfectFlashColor : FlashColor;
		_flashShown = Mathf.Clamp(FlashStrength * scale, 0f, 2f);
		_flashPeak = _flashShown; _flashElapsed = 0;

		if (stage == 0) SpawnRing(perfect, scale);

		LastChargeAbilityId = ability.AbilityId;
		LastWasPerfect = perfect;
		LastChargeStage = stage;
		if (stage == 0) ChargeCompleteCount++; else ChargeStepCount++;

		if (!string.IsNullOrEmpty(ChargeCompleteSfxId))
		{
			LastSfxPitch = Mathf.Clamp(1 + StepPitchIncrement * Mathf.Min(stage, 5), .4f, 2);
			AudioManager.PlayImpact(ChargeCompleteSfxId, LastSfxPitch);
			LastSfxId = ChargeCompleteSfxId;
		}

		GameLog.Write(
			"Combat",
			$"蓄力阶段反馈：{ability.AbilityId} 阶段{stage}{(perfect ? "（完美）" : string.Empty)}"
			+ $" → 身体薄层闪光 {FlashSeconds:0.##}s，音效「{ChargeCompleteSfxId}」×{LastSfxPitch:0.##}"
			+ $"；基础反馈{ChargeCompleteCount}次，强化反馈{ChargeStepCount}次，扩张圈={(stage == 0 ? "有" : "无")}");
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
		_ownedRings.RemoveAll(effect => !IsInstanceValid(effect) || effect.IsQueuedForDeletion());
		if (_ring.LastSpawnedEffect is { } effect)
		{
			effect.ProcessMode = ProcessModeEnum.Pausable;
			_ownedRings.Add(effect);
		}
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
			});
		}

		OverlayMeshCount = _bindings.Count;
		GameLog.Write("Combat", $"蓄力身体表现：模型「{ModelNodeName}」上绑定 {_bindings.Count} 份半透明叠加材质（{ShaderSource}）");
	}

	/// <summary>读着色器：先按 <see cref="RimShaderPath"/> 读文件，读不到就用代码里的内联兜底（**不静默**）。</summary>
	private Shader? LoadShader()
	{
		if (!string.IsNullOrEmpty(RimShaderPath))
		{
			if (ResourceLoader.Exists(RimShaderPath) && RuntimeResources.Load<Shader>(RimShaderPath) is { } fromFile)
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
		bool active = RimShown > 0.001f || FlashShown > 0.001f;
		foreach (MeshBinding binding in _bindings)
		{
			if (binding.Mesh == null || !IsInstanceValid(binding.Mesh) || !IsInstanceValid(binding.Material))
			{
				continue;
			}

			// 只在"要画"的时候占着叠加通道；已经归零就把通道还回去（不影响别人用 MaterialOverlay）。
			UnitRimOverlay.Set(binding.Mesh, this, active ? binding.Material : null, 5);

			binding.Material.SetShaderParameter("rim_color", RimColor);
			binding.Material.SetShaderParameter("rim_power", Mathf.Max(0.5f, RimPower));
			binding.Material.SetShaderParameter("rim_strength", RimShown);
			binding.Material.SetShaderParameter("flash_color", _previewActive ? _previewFlashColor : _flashColor);
			binding.Material.SetShaderParameter("flash", FlashShown);
		}
	}

	/// <summary>重新收集单位身上的全部能力（**不认类名 / 技能行 Id**：加武器不用改这里）。</summary>
	private void EnsureAbilities()
	{
		if (_unit == null) return;
		var abilities = new List<Ability>(); NodeSearch.CollectInSubtree(_unit, abilities);
		foreach (var ability in abilities) HookAbility(ability);
	}
	private void HookAbility(Node node)
	{
		if (node is not Ability ability || _abilities.Contains(ability)) return;
		_abilities.Add(ability); ability.ChargeMilestoneReached += OnMilestone;
		ability.ChargePresentationChanged += OnChargeState;
	}
	private void UnhookAbility(Node node)
	{
		if (node is not Ability ability || !_abilities.Remove(ability)) return;
		if (ReferenceEquals(_chargingSource, ability) || ReferenceEquals(_lastPulseSource, ability)) ClearVisuals();
		if (IsInstanceValid(ability)) { ability.ChargeMilestoneReached -= OnMilestone; ability.ChargePresentationChanged -= OnChargeState; }
	}
	private void OnMilestone(Ability ability, int stage)
	{
		if (_unit is not { IsDead: false } || GetTree().Paused || !ability.Enabled || !_unit.AbilitySlots.Owns(ability)) return;
		_slotRevision = _unit.AbilitySlots.Revision;
		EnsureBindings(); _lastPulseSource = ability; FireChargeComplete(ability, stage);
		SetProcess(true);
	}
	private void OnChargeState(Ability ability, bool active, bool canceled)
	{
		if (active)
		{
			if (_unit is { IsDead: false } && !GetTree().Paused && ability.Enabled && _unit.AbilitySlots.Owns(ability))
			{
				if (_slotRevision != _unit.AbilitySlots.Revision) ClearVisuals();
				_slotRevision = _unit.AbilitySlots.Revision;
				_chargingSource = ability; SetProcess(true);
			}
		}
		else
		{
			bool wasActive = ReferenceEquals(_chargingSource, ability);
			if (wasActive) _chargingSource = null;
			if (canceled && (wasActive || ReferenceEquals(_lastPulseSource, ability))) ClearVisuals();
		}
	}
	private void ClearOnDeath(HitInstance _) => ClearVisuals();
	private void ClearVisuals()
	{
		_rimShown = _flashShown = RimShown = FlashShown = 0; Charging = false; _lastPulseSource = _chargingSource = null;
		_flashPeak = _flashElapsed = 0;
		foreach (var ring in _ownedRings) if (IsInstanceValid(ring)) { ring.Visible = false; ring.QueueFree(); }
		_ownedRings.Clear();
		_previewActive = false; UnitRimOverlay.RemoveSource(this); SetProcess(false);
	}

	// ───────────────── 拍摄台 / 探针（只摆视觉，不进战斗逻辑） ─────────────────

	/// <summary>拍摄台用：**把描边 / 闪光钉住**成给定强度（不依赖真的按住右键，也不会被 `_Process` 一帧冲掉）。
	/// <paramref name="perfect"/> 只影响用哪一档的闪光颜色。撤掉用 <see cref="DebugClearChargeFxPreview"/>。</summary>
	public void DebugPreviewChargeFx(float rim, float flash, bool perfect = false)
	{
		EnsureBindings(); SetProcess(true);
		_previewActive = true;
		_previewRim = Mathf.Clamp(rim, 0f, 4f);
		_previewFlash = Mathf.Clamp(flash, 0f, 2f);
		_previewFlashColor = perfect ? PerfectFlashColor : FlashColor;
		ApplyPreview();
	}

	/// <summary>拍摄台用：撤掉强制状态，回到"跟着真实蓄力状态走"。</summary>
	public void DebugClearChargeFxPreview()
	{
		_previewActive = false;
		_previewRim = 0f;
		_previewFlash = 0f;
		ApplyPreview();
	}

	/// <summary>把强制状态写进材质（**判据与画法一个字都不变，只是"谁来触发"**）。</summary>
	private void ApplyPreview()
	{
		RimShown = _previewRim;
		FlashShown = _previewFlash;
		ApplyUniforms();
	}

	/// <summary>拍摄台用：不走按键，直接"完成一次基础蓄力"（音效 + 圈 + 闪，判据与画法一个字都不变）。</summary>
	public void DebugFireChargeComplete(bool perfect = false)
	{
		EnsureBindings(); SetProcess(true);
		_flashColor = perfect ? PerfectFlashColor : FlashColor;
		_flashShown = Mathf.Clamp(FlashStrength * (perfect ? Mathf.Max(0.01f, PerfectStrengthScale) : 1f), 0f, 2f);
		_flashPeak = _flashShown; _flashElapsed = 0;
		_slotRevision = _unit?.AbilitySlots.Revision ?? -1;
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
