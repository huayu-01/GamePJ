using ActRoguelike.Core;
using System.Collections.Generic;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// **命中 / 击中特效**的统一出生点（`Assets/Effects/fx_combat_hit.tscn` = Binbun Hit FX `vfx_hit_02`，CC0）。
///
/// <para><b>为什么要有一个统一入口，而不是在两处各写三行</b>：项目里有**两条**会造成伤害的命中路径 ——
/// <list type="number">
/// <item><b>命中（投射物）</b>：<see cref="Projectile.ResolveImpact"/> —— 子弹 / 箭矢 / 气刃打到单位，
///   手上有一个**精确的射线命中点**，特效就炸在那儿。</item>
/// <item><b>击中（命中体）</b>：<see cref="AttackHitbox._PhysicsProcess"/> —— 近战挥砍 / AOE / 冲锋 /
///   爆炸的判定窗口（<see cref="Explosion"/> 继承自它，所以爆炸打到的单位也走这条）。
///   这条路径的判定是"重叠"，重叠检测**不给接触点**，所以由 <c>AttackHitbox.ResolveHitPoint</c>
///   补一条射线取**目标碰撞体表面的实际命中点**（用户要求：「实际应该在命中的位置发生特效」）。</item>
/// </list>
/// 两处共用同一个特效资源、同一个全局开关与同一套生命周期，所以出生逻辑只写一份。</para>
///
/// <para><b>为什么生命周期要自己管（两个坑连着踩）</b>：
/// ① <c>fx_combat_hit.tscn</c> 根节点上的 <c>one_shot</c> 只表示"动画不循环"，
///    <b>它不会自毁</b>（`VFXControllerBB.play()` 播完只发一个 <c>finished</c> 信号，没人 queue_free）；
/// ② 爆炸用的那个壳 <c>Assets/Effects/fx_detach_lifetime.gd</c> 在"父节点就是场景根"时会提前
///    <c>return</c>（它设计上是"从一个会死的宿主里脱身"），而我们的特效**正是**直接挂在场景根下的。
/// 于是不自己计时的话，**每次命中都会在场景树里留下一个常驻节点**（打一梭子就多十几个）。
/// 这里用配置里的 <see cref="ConfigIndexData.HitEffectLifetime"/> 显式销毁（默认 1.8s = 素材 <c>main</c>
/// 动画 1.6s + 0.2s 尾巴）。</para>
///
/// <para>⚠️ <b>刻意没有"特效缩放"配置列</b>：Binbun 这套粒子的尺寸**不跟随节点缩放**
/// （shader 里直接写 <c>POSITION</c>，绕过 <c>MODEL_MATRIX</c>），加一列"缩放"就是加一列改了没用的死配置
/// —— 与配置表阶段 3 清理死字段是同一条纪律。尺寸要调就改 <c>fx_combat_hit.tscn</c> 里的材质/网格。</para>
///
/// <para>⚠️ 另一条已知坑**被这个做法绕开了**：`reparent(new_parent, true)` 在
/// <c>physics_interpolation = true</c> 下会丢缩放（美术代理实测）。这里从头到尾没有 reparent ——
/// 特效出生时就挂在最终父节点下，只写一次世界坐标。</para>
/// </summary>
public static class HitEffect
{
	/// <summary>表里没填路径时的兜底（与 <see cref="ConfigIndexData.HitEffectScenePath"/> 的默认值一致）。</summary>
	public const string DefaultScenePath = "res://Assets/Effects/fx_combat_hit.tscn";

	/// <summary>已解析的预制体缓存（命中每秒会发生好几次，不能每次都 GD.Load）。</summary>
	private static PackedScene? _cached;
	private static string _cachedPath = string.Empty;
	private static bool _resolved;

	/// <summary>
	/// 在 <paramref name="position"/> 播一次命中特效。找不到场景 / 开关关闭 / 不在场景树里时**静默跳过**
	/// （表现层的问题绝不能影响伤害结算）。
	/// </summary>
	/// <param name="context">调用方自己（只用来取 <see cref="Node.GetTree"/> 与 <c>CurrentScene</c>）。</param>
	/// <param name="position">世界坐标下的命中点。</param>
	public static void Spawn(Node context, Vector3 position, float scale = 1)
	{
		ConfigIndexData settings = ConfigIndex.Settings;

		// 全局开关（`Config/ConfigIndex.tres` 的「表现 · 命中特效」组）—— 关掉之后两条路径一起停。
		if (!settings.ShowHitEffects)
		{
			return;
		}

		SceneTree? tree = context.GetTree();
		Node? parent = tree?.CurrentScene ?? tree?.Root;
		if (tree == null || parent == null)
		{
			return;
		}

		if (ResolveScene(settings)?.Instantiate<Node3D>() is not { } fx)
		{
			return;
		}

		// 直接挂在最终父节点下再写世界坐标：**不做 reparent**（见类注释里的缩放坑）。
		parent.AddChild(fx);
		fx.GlobalPosition = position;
		fx.Scale *= Mathf.Clamp(scale, .5f, 1.6f);

#if DEBUG
		// 自测取证用（正式逻辑从不读它；Release 构建里这些成员不存在）。
		RecordSpawn(position);
#endif

		// 自己计时销毁（素材不自毁，见类注释）。<= 0 = 不接管（交给特效自己，慎用）。
		if (settings.HitEffectLifetime > 0f)
		{
			tree.CreateTimer(settings.HitEffectLifetime).Timeout += () =>
			{
				if (GodotObject.IsInstanceValid(fx))
				{
					fx.QueueFree();
				}
			};
		}
	}

#if DEBUG
	/// <summary>
	/// 自测取证用：**最近 8 次**出生的世界坐标（= `AttackHitbox` / `Projectile` 真的传进来的那个点）。
	///
	/// <para>为什么不是"最近一次"：一次近战挥砍在人堆里会同时打中好几只，**每只都生一份特效** ——
	/// 只留最后一次，自测就分不清"哪一份是这一只怪的"。自测拿它跟"回读场景里的节点"互为佐证。</para>
	/// </summary>
	public static IReadOnlyList<Vector3> RecentSpawns => _recentSpawns;

	private static readonly List<Vector3> _recentSpawns = new();

	private static void RecordSpawn(Vector3 position)
	{
		if (_recentSpawns.Count >= 8)
		{
			_recentSpawns.RemoveAt(0);
		}

		_recentSpawns.Add(position);
	}
#endif

	/// <summary>读特效预制体（表里的路径优先，读不到就警告一次并跳过）。</summary>
	private static PackedScene? ResolveScene(ConfigIndexData settings)
	{
		string path = string.IsNullOrEmpty(settings.HitEffectScenePath) ? DefaultScenePath : settings.HitEffectScenePath;
		if (_resolved && _cachedPath == path)
		{
			return _cached;
		}

		_resolved = true;
		_cachedPath = path;
		_cached = ResourceLoader.Exists(path) ? RuntimeResources.Load<PackedScene>(path) : null;
		if (_cached == null)
		{
			GD.PushWarning($"[HitEffect] 命中特效 {path} 读不到，本次跳过（只警告一次）。");
		}

		return _cached;
	}
}
