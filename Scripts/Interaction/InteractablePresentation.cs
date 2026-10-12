using ActRoguelike.Audio;
using ActRoguelike.Core;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Interaction;

/// <summary>
/// 交互物的**表现层**（用户需求 §五 #13：「为交互对象增加各种交互表现，比如失败增加一个失败响应、
/// 抖动效果、音效、玩家被略微击退；靠近可交互对象时目标有一圈描边和一个按键图标」）。
///
/// <para><b>它不碰任何判定</b>：本组件只读 <see cref="Interactable"/> 已经算好的状态
/// （<c>PlayerInRange</c> / <c>Used</c> / <c>Vanished</c> / <c>Enabled</c>）与两条**通知**事件
/// （<see cref="Interactable.Rejected"/>、<see cref="Interactable.Failed"/>），
/// 交互范围、消耗、复位逻辑一行都没有动（见 docs/ui.md §16.4）。</para>
///
/// <list type="bullet">
/// <item><b>描边</b>：给外观网格加一个"反向外壳"子网格（同一份 Mesh + 只画背面的发光材质 + Grow）——
///   这是最省的一种描边，不需要后处理，也不需要改任何预制体；</item>
/// <item><b>按键图标</b>：一个始终朝向镜头的 <see cref="Sprite3D"/>（键帽 SVG，见 <see cref="UiIcons.InteractKey"/>），
///   浮在节点顶上轻轻上下浮动；</item>
/// <item><b>只看最近的那一个</b>：目标由 <see cref="PlayerInteractInput.Current"/> 决定
///   （玩家端每帧挑的"范围内最近的可交互物"），所以两个节点挨在一起时不会同时高亮。</item>
/// </list>
/// </summary>
public partial class InteractablePresentation : Node3D
{
	/// <summary>约定节点名（场景里显式挂、或由 <see cref="Ensure"/> 自动补的那个名字）。</summary>
	public const string NodeName = "Presentation";

	/// <summary>描边颜色。</summary>
	public static readonly Color HighlightColor = new(0.55f, 0.92f, 1f);

	/// <summary>失败响应时描边的颜色。</summary>
	public static readonly Color FailColor = new(1f, 0.45f, 0.4f);

	[ExportGroup("描边")]
	/// <summary>描边向外扩张的量（局部单位；越大越粗）。</summary>
	[Export] public float OutlineGrow { get; set; } = 0.045f;

	[ExportGroup("按键图标")]
	/// <summary>图标离"外观网格顶部"的高度（米）。</summary>
	[Export] public float IconGap { get; set; } = 0.55f;

	/// <summary>图标上下浮动的幅度（米）与频率。</summary>
	[Export] public float IconBobAmplitude { get; set; } = 0.07f;

	[Export] public float IconBobSpeed { get; set; } = 2.6f;

	/// <summary>一个像素在 3D 里的大小（64×64 的键帽 → 约 0.48 米）。</summary>
	[Export] public float IconPixelSize { get; set; } = 0.0075f;

	[ExportGroup("失败响应")]
	/// <summary>抖动幅度（米）。抖的是**外观网格**，不是根节点 —— 根节点的缩放已经被出现/消失补间占用。</summary>
	[Export] public float ShakeStrength { get; set; } = 0.14f;

	/// <summary>玩家被击退的冲量（走 <c>CharacterMovement.AddImpulse</c>，指数衰减）。</summary>
	[Export] public float FailKnockback { get; set; } = 4.5f;

	private Interactable _owner = null!;
	private MeshInstance3D? _outline;
	private Sprite3D? _keyIcon;
	private Tween? _shake;
	private Vector3 _meshHome;
	private bool _meshHomeTaken;
	private float _bob;

	/// <summary>这个交互物现在有没有描边 / 按键图标（验证与探针用只读值）。</summary>
	public bool Highlighted { get; private set; }

	/// <summary>按键图标的节点（没有图标素材时为 null）。</summary>
	public Sprite3D? KeyIcon => _keyIcon;

	/// <summary>描边网格的节点（外观网格缺失时为 null）。</summary>
	public MeshInstance3D? Outline => _outline;

	/// <summary>失败响应播了几次（验证用只读计数器）。</summary>
	public int FailCount { get; private set; }

	/// <summary>
	/// 找交互物下的 <c>Presentation</c>；**没有就补一个** —— 这样"新增一个交互物"不需要在场景里手动挂节点
	/// （与 <c>UnitAnimator.Ensure</c> 同一条做法：任何交互物都自动有表现）。
	/// </summary>
	public static InteractablePresentation Ensure(Interactable owner)
	{
		if (owner.GetNodeOrNull<InteractablePresentation>(NodeName) is { } existing)
		{
			return existing;
		}

		var presentation = new InteractablePresentation { Name = NodeName };
		owner.AddChild(presentation);
		return presentation;
	}

	public override void _Ready()
	{
		_owner = GetParent<Interactable>();
		_owner.Rejected += OnInteractFailed;
		_owner.Failed += OnInteractFailed;

		BuildOutline();
		BuildKeyIcon();
	}

	public override void _ExitTree()
	{
		if (IsInstanceValid(_owner))
		{
			_owner.Rejected -= OnInteractFailed;
			_owner.Failed -= OnInteractFailed;
		}
	}

	public override void _Process(double delta)
	{
		bool target = IsTarget();
		if (target != Highlighted)
		{
			Highlighted = target;
			if (_outline != null)
			{
				_outline.Visible = target;
			}

			if (_keyIcon != null)
			{
				_keyIcon.Visible = target;
			}
		}

		if (!Highlighted || _keyIcon == null)
		{
			return;
		}

		// 图标轻轻上下浮动（"这里能按"这件事要比静态图标更容易被注意到）。
		_bob += (float)delta * IconBobSpeed;
		Vector3 position = _keyIcon.Position;
		position.Y = IconHeight() + (Mathf.Sin(_bob) * IconBobAmplitude);
		_keyIcon.Position = position;
	}

	/// <summary>
	/// 是不是"玩家当前瞄准的那一个"：目标是玩家端每帧挑出来的最近可交互物
	/// （<see cref="PlayerInteractInput.Current"/>），所以范围里的其它节点不会一起高亮。
	/// </summary>
	private bool IsTarget()
	{
		if (_owner.Used || _owner.Vanished || !_owner.Enabled)
		{
			return false;
		}

		return ReferenceEquals(PlayerInteractInput.Current, _owner);
	}

	/// <summary>反向外壳描边：同一份 Mesh + 只画背面（<c>CullMode = Front</c>）+ 向外 Grow + 自发光。</summary>
	private void BuildOutline()
	{
		if (_owner.VisualMesh is not { } mesh || mesh.Mesh == null)
		{
			return;
		}

		var material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Front,
			Grow = true,
			GrowAmount = OutlineGrow,
			AlbedoColor = HighlightColor,
			EmissionEnabled = true,
			Emission = HighlightColor,
			EmissionEnergyMultiplier = 1.4f,
		};

		_outline = new MeshInstance3D
		{
			Name = "Outline",
			Mesh = mesh.Mesh,
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Visible = false,
		};

		// 挂在**外观网格**下面：跟着网格自己的变换走（节点的缩放/偏移不用抄第二遍）。
		mesh.AddChild(_outline);
	}

	private void BuildKeyIcon()
	{
		Texture2D? texture = UiIcons.InteractKey;
		if (texture == null)
		{
			return;
		}

		_keyIcon = new Sprite3D
		{
			Name = "KeyIcon",
			Texture = texture,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Shaded = false,
			// 不参与深度测试：指示图标不该被节点自己的模型挡住（它的作用是"告诉你按哪个键"）。
			NoDepthTest = true,
			PixelSize = IconPixelSize,
			Position = new Vector3(0f, IconHeight(), 0f),
			Visible = false,
		};

		AddChild(_keyIcon);
	}

	/// <summary>图标高度：外观网格顶部再往上 <see cref="IconGap"/>（网格大小由预制体决定，所以按 AABB 算）。</summary>
	private float IconHeight()
	{
		if (_owner.VisualMesh is not { } mesh)
		{
			return 2f;
		}

		Aabb box = mesh.GetAabb();
		float half = Mathf.Abs(box.Size.Y * mesh.Scale.Y) * 0.5f;
		return mesh.Position.Y + half + IconGap;
	}

	/// <summary>
	/// 失败响应（用户需求 §五 #13）：音效 + 抖动 + 描边红闪 + 玩家被略微击退。
	/// 由 <see cref="Interactable.Failed"/> / <see cref="Interactable.Rejected"/> 触发 ——
	/// 判定本身在 <see cref="Interactable.Interact"/> 里照旧，这里只是"知道失败了"。
	/// </summary>
	private void OnInteractFailed()
	{
		FailCount++;

		// 音效：走音效表（表里没有这一行就静默不播，玩法不受影响）。
		AudioManager.Play(AudioIds.InteractFail);

		Shake();
		FlashOutline();
		PushPlayerBack();
	}

	/// <summary>抖外观网格（不是根节点：根节点的 scale 被出现/消失补间占用，混着写会互相打断）。</summary>
	private void Shake()
	{
		if (_owner.VisualMesh is not { } mesh)
		{
			return;
		}

		if (!_meshHomeTaken)
		{
			_meshHome = mesh.Position;
			_meshHomeTaken = true;
		}

		if (_shake != null && IsInstanceValid(_shake))
		{
			_shake.Kill();
			mesh.Position = _meshHome;
		}

		Tween tween = mesh.CreateTween();
		for (int i = 0; i < 6; i++)
		{
			float offset = (i % 2 == 0 ? 1f : -1f) * ShakeStrength * (1f - (i * 0.12f));
			tween.TweenProperty(mesh, "position", _meshHome + new Vector3(offset, 0f, 0f), 0.04f);
		}

		tween.TweenProperty(mesh, "position", _meshHome, 0.05f);
		_shake = tween;
	}

	/// <summary>失败时描边闪红（成功路径不会走到这里）。</summary>
	private void FlashOutline()
	{
		if (_outline?.MaterialOverride is not StandardMaterial3D material)
		{
			return;
		}

		material.AlbedoColor = FailColor;
		material.Emission = FailColor;

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(material, "albedo_color", HighlightColor, 0.4f);
		tween.TweenProperty(material, "emission", HighlightColor, 0.4f);
	}

	/// <summary>玩家被略微击退：**背离交互物的方向**推一下，走 CharacterMovement 的既有外力入口。</summary>
	private void PushPlayerBack()
	{
		if (PlayerLocator.Player is not { } player || !IsInstanceValid(player))
		{
			return;
		}

		Vector3 direction = player.GlobalPosition - _owner.GlobalPosition;
		direction.Y = 0f;
		if (direction.LengthSquared() < 0.0001f)
		{
			direction = Vector3.Back;
		}

		player.Movement?.AddImpulse(direction.Normalized() * FailKnockback);
	}
}
