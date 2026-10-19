using System.Collections.Generic;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Visual;

/// <summary>
/// **卡通化 = 引擎内置 Toon 漫反射**（L1-a）。
///
/// <para>做法只有一件事：把场上模型的 <see cref="StandardMaterial3D"/> 复制一份，改
/// <c>DiffuseMode = Toon</c>（`BaseMaterial3D` 自带的**分档漫反射**，不是自定义 shader），
/// 按需要再把 <c>SpecularMode</c> 也换成 Toon（或关掉高光），然后写回
/// <see cref="MeshInstance3D.SetSurfaceOverrideMaterial"/> / <see cref="MeshInstance3D.MaterialOverride"/>。</para>
///
/// <para>三条纪律：</para>
/// <list type="number">
/// <item><b>不碰玩法</b>：只换材质引用，不改网格、不改材质以外任何东西；<see cref="Enabled"/> = false 时一行都不生效
///   （场上逐位等于改动前）。</item>
/// <item><b>不碰特效</b>：Unshaded（拖尾/光晕/命中特效）、透明混合、无深度测试、Billboard 的材质一律跳过 ——
///   这些是"光"不是"面"，切了 Toon 会变成一块黑面。判据只看材质本身的开关，不认节点名。</item>
/// <item><b>同源共享</b>：同一份源材质只复制一次，所有引用它的面共用一份 Toon 副本，不按实例倍增。</item>
/// </list>
///
/// <para>挂法：放进某个场景根下（本工程是 <c>Scenes/Main.tscn</c> 与 <c>Scenes/Levels/Base.tscn</c>），
/// 它会覆盖<b>自己父节点整棵子树</b>，并监听 <see cref="SceneTree.NodeAdded"/> 处理之后才出生的
/// 单位/武器/掉落物模型。移除这个节点 = 恢复原样。</para>
/// </summary>
public partial class ToonPass : Node
{
	/// <summary>总开关。false = 完全不介入（材质一个都不复制）。</summary>
	[Export] public bool Enabled { get; set; } = true;

	/// <summary>高光也换内置 Toon 高光；false = 直接关掉高光（哑光卡通，模型只有 colormap 时更干净）。</summary>
	[Export] public bool ToonSpecular { get; set; } = true;

	/// <summary>卡通材质的粗糙度（只影响高光形状；关闭高光时无意义）。</summary>
	[Export(PropertyHint.Range, "0,1,0.05")] public float Roughness { get; set; } = 0.85f;

	/// <summary>是否处理之后出生的节点（敌人、召唤物、掉落、切武器换的模型）。</summary>
	[Export] public bool WatchNewNodes { get; set; } = true;

	/// <summary>场上还活着的这次卡通化（自测/排障用；场景切换后旧实例会被替换）。</summary>
	public static ToonPass? Active { get; private set; }

	/// <summary>已经切成 Toon 的面数（自测取证：0 就等于没生效）。</summary>
	public static int ConvertedSurfaces { get; private set; }

	/// <summary>看过但因为"是特效/透明/无光照"而故意跳过的面数。</summary>
	public static int SkippedSurfaces { get; private set; }

	/// <summary>真正复制出来的 Toon 材质数量（同源共享，所以它 ≤ 面数）。</summary>
	public static int ConvertedMaterials => _cache.Count;

	/// <summary>素材里**没有**被换掉的源材质（只读，排障用）。</summary>
	public static string Summary()
		=> $"ToonPass 面 {ConvertedSurfaces}（跳过 {SkippedSurfaces}）材质 {_cache.Count}（源材质去重后）";

	private static readonly Dictionary<StandardMaterial3D, StandardMaterial3D> _cache = new();
	private SceneTree? _tree;

	public override void _Ready()
	{
		if (!Enabled)
		{
			return;
		}

		// 美术对比用：`-- --no-toon` 启动一次 = 同一场景同一机位的"改动前"对照组。
		if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--no-toon") >= 0)
		{
			return;
		}

		Active = this;
		_tree = GetTree();
		ConvertedSurfaces = 0;
		SkippedSurfaces = 0;

		// ① 先把"已经在场上"的模型一次性切掉（本节点通常写在场景最后，兄弟节点都已就位）。
		Node? scope = GetParent();
		if (scope != null)
		{
			foreach (Node child in scope.FindChildren("*", "MeshInstance3D", true, false))
			{
				if (child is MeshInstance3D mesh)
				{
					Convert(mesh);
				}
			}
		}

		// ② 之后出生的模型（敌人刷新、召唤物、切武器、掉落物）走同一条路。
		if (WatchNewNodes)
		{
			_tree.NodeAdded += OnNodeAdded;
		}

		GameLog.Write("Art", $"卡通化（内置 Toon 漫反射，不是自定义 shader）：{Summary()}"
			+ $"｜高光 {(ToonSpecular ? $"内置 Toon（粗糙度 {Roughness:0.##}）" : "关闭")}"
			+ $"｜之后出生的节点 {(WatchNewNodes ? "继续接管" : "不接管")}");
	}

	public override void _ExitTree()
	{
		if (_tree != null && WatchNewNodes)
		{
			_tree.NodeAdded -= OnNodeAdded;
		}

		if (Active == this)
		{
			Active = null;
		}
	}

	private void OnNodeAdded(Node node)
	{
		if (node is MeshInstance3D mesh && IsInstanceValid(mesh))
		{
			Convert(mesh);
		}
	}

	/// <summary>把一个网格的每个面换成本次会话里的 Toon 材质（幂等：已经是 Toon 副本就跳过）。</summary>
	private void Convert(MeshInstance3D mesh)
	{
		if (mesh.Mesh == null)
		{
			return;
		}

		// material_override（墙面/终端/门用得多）优先级最高，一挂就盖住全部面 —— 那就只换它本身。
		if (mesh.MaterialOverride is StandardMaterial3D overrideMaterial)
		{
			if (!IsShadedSurface(overrideMaterial))
			{
				SkippedSurfaces++;
				return;
			}

			StandardMaterial3D? toonOverride = ToonOf(overrideMaterial);
			if (toonOverride != null && !ReferenceEquals(mesh.MaterialOverride, toonOverride))
			{
				mesh.MaterialOverride = toonOverride;
			}

			return;
		}

		int surfaces = mesh.Mesh.GetSurfaceCount();
		for (int i = 0; i < surfaces; i++)
		{
			Material? current = mesh.GetSurfaceOverrideMaterial(i);
			Material? source = current ?? mesh.Mesh.SurfaceGetMaterial(i);
			if (source is not StandardMaterial3D standard)
			{
				// 自定义 ShaderMaterial（命中特效、能量波、拖尾）不是这一次要动的对象。
				continue;
			}

			if (!IsShadedSurface(standard))
			{
				SkippedSurfaces++;
				continue;
			}

			StandardMaterial3D? toon = ToonOf(standard);
			if (toon == null || ReferenceEquals(current, toon))
			{
				continue;
			}

			mesh.SetSurfaceOverrideMaterial(i, toon);
			ConvertedSurfaces++;
		}
	}

	/// <summary>取（必要时复制出）源材质对应的 Toon 副本；返回 null = 这份源材质不该被改。</summary>
	private StandardMaterial3D? ToonOf(StandardMaterial3D source)
	{
		if (_cache.TryGetValue(source, out StandardMaterial3D? cached) && IsInstanceValid(cached))
		{
			return cached;
		}

		// 已经是本次产出的副本（重复挂同一个网格）→ 直接用，不再套一层。
		foreach (StandardMaterial3D produced in _cache.Values)
		{
			if (ReferenceEquals(produced, source))
			{
				return source;
			}
		}

		if (source.DiffuseMode == BaseMaterial3D.DiffuseModeEnum.Toon)
		{
			// 表/场景里已经手工配成 Toon 的材质（如 mat_Model_Toon_3d.tres）保持原样，只记一笔。
			_cache[source] = source;
			return source;
		}

		var toon = (StandardMaterial3D)source.Duplicate();
		toon.DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Toon;
		toon.SpecularMode = ToonSpecular
			? BaseMaterial3D.SpecularModeEnum.Toon
			: BaseMaterial3D.SpecularModeEnum.Disabled;
		if (ToonSpecular)
		{
			toon.Roughness = Roughness;
		}

		_cache[source] = toon;
		return toon;
	}

	/// <summary>
	/// **只给真正的"受光面"上 Toon**。特效材质（Unshaded / 透明 / 无深度测试 / 朝向相机公告板）
	/// 换成分档光照会变成一块死黑，所以一律保留原样。
	/// </summary>
	private static bool IsShadedSurface(StandardMaterial3D material)
		=> material.ShadingMode == BaseMaterial3D.ShadingModeEnum.PerPixel
			&& material.Transparency == BaseMaterial3D.TransparencyEnum.Disabled
			&& !material.NoDepthTest
			&& material.BillboardMode == BaseMaterial3D.BillboardModeEnum.Disabled;
}
