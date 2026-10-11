using System;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Core;

/// <summary>
/// 游戏日志：把局内交互全部落盘，方便游玩后分析问题（用户新重点需求）。
///
/// - 文件：``user://logs/run_年月日_时分秒.log``，同时 ``GD.Print`` 一份便于 MCP/控制台抓取。
/// - 战斗事件通过 ``GameplayEventBus`` 自动记录（死亡/击杀/暴击/破甲，以及涉及玩家的伤害）。
/// - 关键流程（选角色、进关、节点互动、奖励选择、生成、清场、结算）由各系统显式 ``GameLog.Write``。
/// - 分类：Flow 流程 / Node 节点 / Reward 奖励 / Combat 战斗 / Spawn 生成 / Input 输入。
/// </summary>
public partial class GameLog : Node
{
	public static GameLog? Instance { get; private set; }

	/// <summary>本次日志文件路径（user:// 相对路径）。</summary>
	public static string Path { get; private set; } = string.Empty;

	private FileAccess? _file;
	private float _flushTimer;

	/// <summary>写一条日志。任何地方都可以直接调用（没有实例时只打印到控制台）。</summary>
	public static void Write(string category, string message)
	{
		string line = $"{Time.GetTimeStringFromSystem()} [{category}] {message}";
		GD.Print($"[log] {line}");
		Instance?.Append(line);
	}

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;

		DirAccess.MakeDirRecursiveAbsolute("user://logs");
		string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
		Path = $"user://logs/run_{stamp}.log";
		_file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
		if (_file == null)
		{
			GD.PushWarning($"[GameLog] 无法创建日志文件 {Path}");
		}

		GameplayEventBus.Raised += OnGameplayEvent;
		Write("Flow", "=== 新的一局 ===");
		Write("Flow", $"日志文件：{Path}");
	}

	public override void _ExitTree()
	{
		GameplayEventBus.Raised -= OnGameplayEvent;
		_file?.Flush();
		_file?.Close();
		_file = null;
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public override void _Process(double delta)
	{
		_flushTimer += (float)delta;
		if (_flushTimer >= 1f)
		{
			_flushTimer = 0f;
			_file?.Flush();
		}
	}

	private void Append(string line) => _file?.StoreLine(line);

	private void OnGameplayEvent(GameplayEvent gameplayEvent)
	{
		switch (gameplayEvent.Kind)
		{
			case GameplayEventKind.Died:
				Write("Combat", $"死亡 {Describe(gameplayEvent.Target)} 击杀者={Describe(gameplayEvent.Source)}");
				break;

			case GameplayEventKind.Kill:
				Write("Combat", $"击杀 {Describe(gameplayEvent.Target)} by {Describe(gameplayEvent.Source)}");
				break;

			case GameplayEventKind.Critical:
				Write("Combat", $"暴击 {Describe(gameplayEvent.Source)} → {Describe(gameplayEvent.Target)} {gameplayEvent.Value:0.#}");
				break;

			case GameplayEventKind.ArmorBroken:
				Write("Combat", $"破甲 {Describe(gameplayEvent.Target)}");
				break;

			case GameplayEventKind.Damaged:
				// 只记与玩家相关的伤害，避免刷屏。
				if (gameplayEvent.Target?.Faction == Faction.Player || gameplayEvent.Source?.Faction == Faction.Player)
				{
					Write("Combat", $"伤害 {Describe(gameplayEvent.Source)} → {Describe(gameplayEvent.Target)} {gameplayEvent.Value:0.#}");
				}

				break;
		}
	}

	private static string Describe(Unit? unit)
		=> unit == null ? "-" : $"{unit.Name}({unit.Health:0.#})";
}
