using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Audio;
using ActRoguelike.Player;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.UI;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>字体、破甲环、导航与场景卸载的真实运行专项。</summary>
public partial class FollowupProbe : Node3D
{
	private int _pass;
	private int _fail;
	public override async void _Ready()
	{
		try
		{
			if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--audio-exit") >= 0) await RunAudioExit();
			else await Run();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GetTree().Paused = false;
		GD.Print($"[Followup] === F专项验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private void Check(bool ok, string message)
	{
		if (ok) _pass++; else _fail++;
		GD.Print($"[Followup] {(ok ? "PASS" : "FAIL")} {message}");
	}
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds, true), SceneTreeTimer.SignalName.Timeout);
	private async Task Shot(string name)
	{
		if (DisplayServer.GetName() == "headless") return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng($"res://docs/screenshots/{name}.png") == Error.Ok, $"截图 {name}");
	}
	private async Task RunAudioExit()
	{
		foreach (Node child in GetChildren()) child.QueueFree();
		await Wait(0.1);
		Node main = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate();
		GetTree().Root.AddChild(main);
		GetTree().CurrentScene = main;
		await Wait(1.2);
		Check(AudioManager.MusicPlaying, "退出前真实关卡音乐正在播放");
	}
	private async Task Run()
	{
		await Wait(0.25);
		RunSetup.Character = PlayerCharacterKind.Hunter;
		RunSetup.Weapon = WeaponPresetKind.EnergyPalm;
		RunSetup.HasSelection = true;
		Unit player = GD.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>();
		AddChild(player);
		player.GlobalPosition = new Vector3(0, 0.05f, 4);
		new PlayerLoadout().Apply(player, this, RunSetup.Character, RunSetup.Weapon);
		player.IsInvulnerable = true;
		await Wait(0.8);
		var map = GetNode<MapManager>("MapManager");
		Check(map.IsReady && map.PolygonCount > 0, $"生产场地递归烘焙成功：多边形={map.PolygonCount}");
		NavigationRegion3D region = GetNode<NavigationRegion3D>("NavRegion");
		NavigationMesh mesh = region.NavigationMesh;
		Rid navigationMap = region.GetNavigationMap();
		Check(Mathf.IsEqualApprox(mesh.CellSize, NavigationServer3D.MapGetCellSize(navigationMap)) && Mathf.IsEqualApprox(mesh.CellHeight, NavigationServer3D.MapGetCellHeight(navigationMap)), $"导航单元与地图一致：{mesh.CellSize}/{mesh.CellHeight}");
		Check(map.TrySnap(new Vector3(0, 0.1f, 0), out Vector3 origin) && origin.DistanceTo(Vector3.Zero) < 0.8f, $"原点附近导航点合法：{origin}");
		Check(map.IsWalkable(new Vector3(0, 0.1f, 4)), "中央地面可行走");
		Check(!map.IsWalkable(new Vector3(6, 0.1f, -6), 0.6f), "石柱占地不是可行走点");
		Check(!map.IsWalkable(new Vector3(0, 0.1f, -16), 0.6f), "墙体占地不是可行走点");
		var points = new HashSet<Vector2>();
		bool validPoints = true;
		for (int i = 0; i < 32; i++)
		{
			Vector3 spawn = map.SpawnPoint(new Vector3(0, 0.1f, 4), 6, 12);
			points.Add(new Vector2(Mathf.Snapped(spawn.X, 0.1f), Mathf.Snapped(spawn.Z, 0.1f)));
			validPoints &= map.IsWalkable(spawn, 0.8f) && new Vector2(spawn.X, spawn.Z - 4).Length() >= 4.8f;
		}
		Check(validPoints && points.Count > 20, $"32次刷怪落点有效且分散：不同点={points.Count}");
		Vector3[] route = NavigationServer3D.MapGetPath(navigationMap, new Vector3(-9, 0.3f, -14), new Vector3(9, 0.3f, -18), true);
		Check(route.Length >= 3, $"墙体两侧导航路径绕行：路径点={route.Length}");
		int before = map.PolygonCount;
		map.Rebuild();
		await Wait(0.25);
		Check(map.IsReady && map.PolygonCount == before, $"重烘多边形稳定：{before}→{map.PolygonCount}");
		int regions = 0;
		foreach (Node node in GetChildren()) if (node is NavigationRegion3D) regions++;
		Check(regions == 1 && GetNodeOrNull<KillZone>("KillZone") != null, "重烘不叠加区域且区域杀仍存在");

		var font = GD.Load<FontVariation>("res://Assets/UI/Themes/ui_font.tres");
		var textServer = TextServerManager.GetPrimaryInterface();
		var coordinates = textServer.FontGetVariationCoordinates(font.GetRids()[0]);
		Check(coordinates.TryGetValue(textServer.NameToTag("wght"), out Variant weight) && Mathf.IsEqualApprox(weight.AsSingle(), 500), $"字体实际500字重：{coordinates}");
		Check(font.BaseFont is FontFile file && !file.AllowSystemFallback && file.ResourcePath.EndsWith("NotoSansSC.ttf"), "内置FontFile且禁用系统字体回退");
		var label = new Label { Text = "猎手 武器大师 长虹贯日 灵魂收割 设置 帮助" };
		AddChild(label);
		Check(label.GetThemeFont("font") == font, "未指定主题的控件也使用项目内置字体");
		Check(GD.Load<Theme>("res://Assets/UI/Themes/ui_theme.tres").DefaultFont == font, "现有界面主题共用同一内置字体");
		string corpus = FileAccess.GetFileAsString("res://.ai_font_corpus.txt");
		var missing = new List<char>();
		foreach (char character in corpus) if (!font.HasChar(character)) missing.Add(character);
		Check(corpus.Length > 500 && missing.Count == 0, $"项目中文语料{corpus.Length}个不同字形，缺字={new string(missing.ToArray())}");
		label.QueueFree();

		ArmorBreakFx.Spawn(this, player.GlobalPosition + new Vector3(0, 0, -2));
		Node3D effect = GetNode<Node3D>("ArmorBreakFx");
		var ring = effect.GetNode<MeshInstance3D>("Ring").Mesh as TorusMesh;
		Check(ring is { Rings: 32, RingSegments: 8 }, "破甲环圆周32段/截面8段");
		Check(SafeFx(effect), "破甲特效无单位/碰撞/分组");
		await Shot("f_armor_round");
		await Wait(0.55);
		Check(!IsInstanceValid(effect), "破甲特效按既有时长自毁");
		foreach (Node child in GetChildren()) if (child != this) child.QueueFree();
		await Wait(0.2);
		Check(MapManager.Instance == null, "地图卸载后静态引用清理");

		// 提前卸载包含延时烘焙的地图，和完整加载交替；每轮强制回收托管包装。
		for (int i = 0; i < 12; i++)
		{
			string path = i % 2 == 0 ? "res://Scenes/Levels/Base.tscn" : "res://Scenes/Main.tscn";
			Node scene = GD.Load<PackedScene>(path).Instantiate();
			GetTree().Root.AddChild(scene);
			GetTree().CurrentScene = scene;
			bool early = i % 4 == 1;
			await Wait(early ? 0.04 : 0.7);
			if (i == 0)
			{
				Input.ParseInputEvent(new InputEventAction { Action = "ui_cancel", Pressed = true });
				await Wait(0.05);
				Input.ParseInputEvent(new InputEventAction { Action = "ui_cancel", Pressed = false });
				await Wait(0.4);
				await Shot("f_settings_font");
			}
			UiRouter.Instance?.ResetForSceneChange();
			scene.QueueFree();
			GetTree().CurrentScene = this;
			await Wait(0.12);
			GC.Collect();
			GC.WaitForPendingFinalizers();
			Check(!IsInstanceValid(scene) && GetTree().GetNodesInGroup("player_units").Count == 0 && MapManager.Instance == null, $"场景卸载+强制GC第{i + 1}轮，提前卸载={early}");
		}
	}
	private static bool SafeFx(Node node)
	{
		if (node is Unit or CollisionObject3D || node.GetGroups().Count > 0) return false;
		foreach (Node child in node.GetChildren()) if (!SafeFx(child)) return false;
		return true;
	}
}
