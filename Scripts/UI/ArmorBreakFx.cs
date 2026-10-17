using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 破甲的**模型额外显示效果**（用户需求 §五 #8：「甚至是模型的额外显示效果」）——
/// 单位身上炸开一圈水平扩散的环 + 一层快速褪去的亮壳，0.42 秒后自己回收。
///
/// <para><b>为什么是"一次性特效"而不是给单位加节点</b>：与 <c>HitEffect</c> 同一条思路 ——
/// 不改任何预制体、不动 <c>Scripts/Combat/**</c> 的管线，任何单位（玩家 / 敌人 / 以后的新怪）
/// 只要有"破甲"这个事实就能播，新单位自动带上。</para>
///
/// <para><b>触发来源</b>：<see cref="ActRoguelike.Combat.Unit.ArmorBroke"/> 事件 —— 它**只在甲从 &gt;0 掉到 0
/// 的那一次**触发（<c>Unit.ReceiveHit</c> 的 Armor 段），所以"碎甲"与"甲被扣但还没碎"天然分开；
/// 甲为 0 的单位（首领/无甲怪）根本不会走到那一行，也就不会误播。</para>
/// </summary>
internal static class ArmorBreakFx
{
	/// <summary>护甲配色（与血条上的护甲条同色系）：冷钢蓝。</summary>
	public static readonly Color ArmorColor = new(0.72f, 0.86f, 1f);

	private const float Duration = 0.42f;

	/// <summary>在 <paramref name="worldPosition"/> 播一次破甲特效；<paramref name="context"/> 只用来挂节点。</summary>
	public static void Spawn(Node context, Vector3 worldPosition, Color? color = null)
	{
		if (context == null || !GodotObject.IsInstanceValid(context) || !context.IsInsideTree())
		{
			return;
		}

		Color tint = color ?? ArmorColor;
		Node? host = context.GetTree()?.CurrentScene ?? context;
		if (host == null || !GodotObject.IsInstanceValid(host))
		{
			return;
		}

		var holder = new Node3D { Name = "ArmorBreakFx" };
		host.AddChild(holder);
		holder.GlobalPosition = worldPosition + (Vector3.Up * 0.9f);

		// ① 水平扩散的环（TorusMesh 默认就躺在 XZ 平面上）。
		MeshInstance3D ring = Ring(tint, 0.9f);
		holder.AddChild(ring);

		// ② 一层快速褪去的亮壳：让"碎"这件事在模型本体上也看得见。
		MeshInstance3D shell = Shell(tint);
		holder.AddChild(shell);

		Tween tween = holder.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(holder, "scale", Vector3.One * 2.1f, Duration)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

		if (ring.MaterialOverride is StandardMaterial3D ringMaterial)
		{
			tween.TweenProperty(ringMaterial, "albedo_color:a", 0f, Duration);
			tween.TweenProperty(ringMaterial, "emission_energy_multiplier", 0.2f, Duration);
		}

		if (shell.MaterialOverride is StandardMaterial3D shellMaterial)
		{
			tween.TweenProperty(shellMaterial, "albedo_color:a", 0f, Duration * 0.6f);
		}

		tween.Chain().TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(holder))
			{
				holder.QueueFree();
			}
		}));
	}

	private static MeshInstance3D Ring(Color tint, float alpha)
	{
		var mesh = new TorusMesh { InnerRadius = 0.45f, OuterRadius = 0.62f, RingSegments = 8, Rings = 32 };
		return new MeshInstance3D
		{
			Name = "Ring",
			Mesh = mesh,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = Unshaded(tint, alpha, 2.2f),
		};
	}

	private static MeshInstance3D Shell(Color tint)
	{
		var mesh = new SphereMesh { Radius = 0.6f, Height = 1.2f, RadialSegments = 12, Rings = 6 };
		return new MeshInstance3D
		{
			Name = "Shell",
			Mesh = mesh,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = Unshaded(tint, 0.3f, 1.2f),
		};
	}

	/// <summary>自发光 + 半透明 + 不受光：颜色明暗只由 tint/alpha 决定，不看场景光照（竞技场很亮也能看清）。</summary>
	private static StandardMaterial3D Unshaded(Color tint, float alpha, float energy) => new()
	{
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		BlendMode = BaseMaterial3D.BlendModeEnum.Add,
		CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		AlbedoColor = new Color(tint.R, tint.G, tint.B, alpha),
		EmissionEnabled = true,
		Emission = tint,
		EmissionEnergyMultiplier = energy,
	};
}
