using System.Collections.Generic;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Interaction;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 界面图标表（用户需求：「尽量使用图标去代替文字」）。
///
/// **一处映射，多处复用**：构筑面板（<see cref="BuildScreen"/>）、HUD 的构筑图标条、
/// 奖励三选一卡片、设置菜单的分类 Tab 都从这里取图，所以"换一张图"只改一个文件名。
///
/// 图是**手写 SVG**（`Assets/UI/Icons/ic_ui_*.svg`，项目自制，可商用、零依赖）：
/// SVG 走 Godot 原生导入，改一个数字就是新图标，不需要任何图片工具链。
///
/// ⚠️ 读不到图返回 <c>null</c>，调用方要么不显示图标、要么只用文字 —— <b>绝不抛异常</b>
/// （与"表读不到就用自身默认值"同一条纪律）。
/// </summary>
public static class UiIcons
{
	private const string Dir = "res://Assets/UI/Icons/";

	/// <summary>奖励类型 → 图标（强化 / 进化 / 异化）。</summary>
	public static Texture2D? ForReward(RewardDefinition? definition)
		=> definition == null ? null : ForRewardKind(definition.Kind);

	/// <summary>奖励类型 → 图标。</summary>
	public static Texture2D? ForRewardKind(RewardKind kind) => kind switch
	{
		RewardKind.Evolution => Named("ic_ui_reward_evolution"),
		RewardKind.Mutation => Named("ic_ui_reward_mutation"),
		_ => Named("ic_ui_reward_enhance"),
	};

	/// <summary>地面节点类型 → 图标（奖励节点 / 商店 / 降临门…）。</summary>
	public static Texture2D? ForNodeKind(RewardNodeKind kind) => kind switch
	{
		RewardNodeKind.Weapon => Named("ic_ui_node_weapon"),
		RewardNodeKind.Evolution => Named("ic_ui_reward_evolution"),
		RewardNodeKind.Mutation => Named("ic_ui_reward_mutation"),
		RewardNodeKind.Resource => Named("ic_ui_node_resource"),
		RewardNodeKind.Heal => Named("ic_ui_node_heal"),
		RewardNodeKind.Special => Named("ic_ui_node_special"),
		RewardNodeKind.Shop => Named("ic_ui_node_shop"),
		RewardNodeKind.Portal => Named("ic_ui_node_portal"),
		_ => Named("ic_ui_reward_enhance"),
	};

	/// <summary>武器 → 图标（三种武器共用一件，靠卡片文字区分 —— 选取界面允许有文字）。</summary>
	public static Texture2D? ForWeapon(WeaponPresetKind kind) => Named("ic_ui_node_weapon");

	/// <summary>「角色持有」面板 / HUD 图标条的标题图标。</summary>
	public static Texture2D? Holdings => Named("ic_ui_build_holdings");

	/// <summary>设置菜单的分类图标（分类 id → 图标，新增 section 时在这里加一行即可）。</summary>
	public static Texture2D? ForSection(string id) => id switch
	{
		"graphics" => Named("ic_ui_set_graphics"),
		// 控制分类的 id 是 "input"（= settings.cfg 的节名，与 project.godot 的 [input] 同名）。
		"input" or "controls" => Named("ic_ui_set_controls"),
		"audio" => Named("ic_ui_set_audio"),
		_ => Holdings,
	};

	/// <summary>
	/// 按**界面标题**取图标（通用多选一 <see cref="ChooseScreen"/> 只拿得到标题字符串）。
	/// 标题来自 <c>RewardService.TitleFor</c> 那张表，所以节点加一种类型时这里自动跟着走。
	/// </summary>
	public static Texture2D? ForTitle(string? title)
	{
		if (string.IsNullOrEmpty(title))
		{
			return null;
		}

		foreach (KeyValuePair<string, Texture2D?> entry in TitleMap())
		{
			if (title.StartsWith(entry.Key, System.StringComparison.Ordinal))
			{
				return entry.Value;
			}
		}

		return null;
	}

	/// <summary>按文件名取图（不带扩展名），找不到返回 null 并只写一次日志。</summary>
	public static Texture2D? Named(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}

		if (_cache.TryGetValue(name, out Texture2D? cached))
		{
			return cached;
		}

		string path = Dir + name + ".svg";
		Texture2D? texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		_cache[name] = texture;

		if (texture == null)
		{
			GameLog.Write("UI", $"图标缺失：{path}（界面照常显示，只是没有图标）");
		}

		return texture;
	}

	/// <summary>本类一共能解析出几张图（验证用；放在这里当作"图标表是否接上"的自检指标）。</summary>
	public static int DebugLoadedCount()
	{
		int count = 0;
		foreach (string name in Names())
		{
			if (Named(name) != null)
			{
				count++;
			}
		}

		return count;
	}

	/// <summary>本类引用到的全部图标名（自检与文档共用一份清单）。</summary>
	public static IEnumerable<string> Names() => new[]
	{
		"ic_ui_reward_enhance", "ic_ui_reward_evolution", "ic_ui_reward_mutation",
		"ic_ui_node_weapon", "ic_ui_node_resource", "ic_ui_node_heal", "ic_ui_node_special",
		"ic_ui_node_shop", "ic_ui_node_portal", "ic_ui_build_holdings",
		"ic_ui_set_graphics", "ic_ui_set_controls", "ic_ui_set_audio",
	};

	private static readonly Dictionary<string, Texture2D?> _cache = new();
	private static Dictionary<string, Texture2D?>? _titleMap;

	private static Dictionary<string, Texture2D?> TitleMap()
	{
		if (_titleMap != null)
		{
			return _titleMap;
		}

		_titleMap = new Dictionary<string, Texture2D?>();
		foreach (RewardNodeKind kind in System.Enum.GetValues<RewardNodeKind>())
		{
			string title = RewardService.TitleFor(kind);
			if (!string.IsNullOrEmpty(title) && !_titleMap.ContainsKey(title))
			{
				_titleMap[title] = ForNodeKind(kind);
			}
		}

		return _titleMap;
	}
}
