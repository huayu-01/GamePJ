using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Run;
using ActRoguelike.UI;
using ActRoguelike.Visual;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>丛林草地独立样板；默认可试玩，--grass-benchmark执行自动取证。</summary>
public partial class GrassShaderLab : EliteTraitProbeSupport
{
	protected override string ProbeTag=>"GrassShaderLab";
	protected override bool SceneMeadowEnabled=>false;
	private ShaderGrassPatch _grass=null!;
	private JungleGrassScenery _scenery=null!;
	private Label _caption=null!;
	private bool _quit,_ready,_testingKeys,_overview,_playable;
	private Camera3D _camera=null!;
	private bool RecordOnly=>OS.GetCmdlineUserArgs().Contains("--grass-record-only");
	private bool PlayCheck=>OS.GetCmdlineUserArgs().Contains("--grass-playcheck");
	private bool PreviewOnly=>OS.GetCmdlineUserArgs().Contains("--grass-preview");
	private bool FoliageZones=>OS.GetCmdlineUserArgs().Contains("--foliage-zones");
	private bool GroundColor=>OS.GetCmdlineUserArgs().Contains("--ground-color");
	private bool Automated=>OS.GetCmdlineUserArgs().Contains("--grass-benchmark")||RecordOnly||PreviewOnly||FoliageZones||GroundColor;
	public override void _EnterTree()
	{
		// 自动验证仍用引擎注入按键，窗口不接管用户键盘；手动试玩保持正常输入。
		if((Automated||PlayCheck)&&DisplayServer.GetName()!="headless") GetWindow().Unfocusable=true;
	}
	private static IEnumerable<Node> Walk(Node root) { yield return root; foreach(Node child in root.GetChildren()) foreach(var node in Walk(child)) yield return node; }
	protected override async Task RunChecks()
	{
		GD.Seed(29102026);
		var window=GetTree().Root; window.Mode=Window.ModeEnum.Windowed;
		window.Size=new Vector2I(1920,1080); window.Scaling3DScale=1; window.Msaa3D=Viewport.Msaa.Msaa2X;
		if(Automated) { Engine.MaxFps=0; DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled); }
		if(MapManager.Instance!.TryValidPoint(new Vector3(0,.1f,20),Player.GlobalPosition,out var position,Player)) Player.GlobalPosition=position;
		Player.ResetPhysicsInterpolation(); Player.GetNode<Node3D>("CameraRig").ProcessMode=ProcessModeEnum.Disabled;
		_camera=new Camera3D { Current=true,Fov=62,PhysicsInterpolationMode=PhysicsInterpolationModeEnum.Off }; Mode.AddChild(_camera); FollowCamera();
		foreach(var hud in Walk(Mode).OfType<CanvasLayer>().Where(node=>node is PlayerHud || node.GetType().Name=="SurvivalHud")) hud.Visible=false;
		var indicator=RuntimeResources.Load<PackedScene>("res://Scenes/Combat/SkillIndicator.tscn").Instantiate<SkillIndicator>();
		Mode.AddChild(indicator); indicator.IsHostile=true;
		indicator.ShowAreaPoint(Player.GlobalPosition+new Vector3(2,0,-5.5f),2.1f); indicator.SetWarningProgress(.75f);
		for(int i=0;i<3;i++) MakeEnemy(Player.GlobalPosition+new Vector3(i*2.5f-2.5f,0,-5.3f-i*.7f));
		await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
		if(Automated&&!FoliageZones&&!GroundColor) await Shot("soft-before");
		var arena=Mode.GetNode<Node3D>("Arena");
		// 对比原对象、形状和变换，确保视觉替换没有悄悄改变关卡碰撞。
		var collision=Walk(arena).OfType<CollisionShape3D>().ToDictionary(shape=>shape.GetInstanceId(),shape=>(shape.GlobalTransform,shape.Shape,shape.Disabled));
		_scenery=new JungleGrassScenery { Name="JungleScenery" }; Mode.AddChild(_scenery); _scenery.Build(arena);
		Check(collision.Count==Walk(arena).OfType<CollisionShape3D>().Count()&&Walk(arena).OfType<CollisionShape3D>().All(shape=>collision[shape.GetInstanceId()]==(shape.GlobalTransform,shape.Shape,shape.Disabled)),"丛林化保留全部原碰撞对象、形状、变换与启用状态");
		Check(!Walk(_scenery).Any(node=>node is CollisionObject3D or CollisionShape3D or NavigationRegion3D),"新增岩壁、藤叶和阔叶树只负责显示，不新增碰撞或导航");
		Check(_scenery.ReplacedStructures>=14&&_scenery.BoundaryRockCount>=100&&_scenery.FernCount>=200,$"丛林形体：{_scenery.ReplacedStructures}处残墙／石柱、{_scenery.BoundaryRockCount}块边界岩、{_scenery.FernCount}组大叶植物");
		_grass=new ShaderGrassPatch { Name="ShaderGrassPatch",BuildReference=false }; Mode.AddChild(_grass);
		_grass.GlobalPosition=Vector3.Zero; _grass.RegisterSceneExclusions(arena,arena.GetNode("Ground")); _grass.Build();
		Check(_grass.NormalCount>12000&&_grass.ChunkCount>=600,$"全场草地：普通{_grass.NormalCount}簇／{_grass.ChunkCount}个分块，{ShaderGrassPatch.TrianglesPerClump}三角面/簇");
		Check(_grass.Density==6&&_grass.FieldSize==new Vector2(100,100),"100×100米全场覆盖，单位面积采样密度由12减为6，普通档保留比例仍为65%");
		Check(_grass.InstanceBounds.Position.X< -49&&_grass.InstanceBounds.Position.Z< -49&&_grass.InstanceBounds.End.X>49&&_grass.InstanceBounds.End.Z>49,"草地覆盖四个边缘，原道路不再留空");
		Check(!Walk(_grass).Any(node=>node is CollisionObject3D or CollisionShape3D or NavigationRegion3D),"草地没有新增碰撞、导航或战斗实体");
		Check(_grass.GrassMaterial.GetShaderParameter("blade_texture").AsGodotObject() is Texture2D,"十字草片已绑定草叶轮廓遮罩，使用裁切与MSAA覆盖渐隐");
		Check(!_grass.GrassMaterial.Shader!.Code.Contains("player_position")&&_grass.WindFrequency==.45f,"风动频率降为原来的45%，无角色坐标上传或靠近压弯");
		Check(indicator.GetNode<MeshInstance3D>("DrawRoot/BrushContour").MaterialOverride is StandardMaterial3D {NoDepthTest:false},"技能预警保留正常深度测试");
		var layer=new CanvasLayer { Layer=25 }; Mode.AddChild(layer);
		_caption=new Label { Position=new Vector2(32,26),MouseFilter=Control.MouseFilterEnum.Ignore };
		GlassUi.Typography(_caption,23,600); _caption.AddThemeConstantOverride("outline_size",5); _caption.AddThemeColorOverride("font_outline_color",new Color(.05f,.10f,.08f,.9f)); layer.AddChild(_caption);
		Apply(ShaderGrassPatch.Variant.Batched); _ready=true; SetProcessInput(true); await Wait(.25);
		_testingKeys=true;
		await Press(Key.Key1); Check(_grass.CurrentVariant==ShaderGrassPatch.Variant.Ground,"数字键1切换丛林地表并关闭草地更新");
		await Press(Key.Key2); Check(_grass.CurrentVariant==ShaderGrassPatch.Variant.Batched,"数字键2恢复半密度全场草地");
		await Press(Key.Key3); Check(_overview&&_camera.Projection==Camera3D.ProjectionType.Orthogonal,"数字键3显示全场俯视，取消草地距离剔除");
		await Press(Key.Key4); Check(!_overview&&_camera.Projection==Camera3D.ProjectionType.Perspective,"数字键4返回游戏镜头");
		_testingKeys=false;
		float before=_grass.WindTime; await Wait(.05); Check(_grass.WindTime>before,"风动由单个共享材质时钟推进");
		before=_grass.WindTime; GetTree().Paused=true; await Wait(.08); Check(_grass.WindTime==before,"暂停时草地风动冻结"); GetTree().Paused=false;
		Apply(ShaderGrassPatch.Variant.Ground); Check(!_grass.IsProcessing(),"无草对照停止草地更新"); Apply(ShaderGrassPatch.Variant.Batched);
		if(FoliageZones) { await CheckFoliageZones(); return; }
		if(GroundColor) { if(DisplayServer.GetName()!="headless") await CheckGroundColor(); return; }
		if(DisplayServer.GetName()=="headless") return;
		if(Automated) { if(RecordOnly) await RecordWind(); else if(PreviewOnly) await Preview(); else await Benchmark(); }
		else
		{
			new PlayerLoadout().Apply(Player,Mode,PlayerCharacterKind.Hunter,WeaponPresetKind.EnergyPalm);
			_playable=true; SetView(false);
			if(PlayCheck)
			{
				var rig=Player.GetNode<Node3D>("CameraRig"); var follow=Walk(rig).OfType<Camera3D>().First();
				Check(follow.Current&&Player.Movement?.IsPhysicsProcessing()==true,"试玩恢复真实跟随相机和玩家移动");
				await Press(Key.Key3); Check(_camera.Current&&Player.Movement?.IsPhysicsProcessing()==false,"试玩俯视切换锁定玩家位移");
				await Press(Key.Key4); Check(follow.Current&&Player.Movement?.IsPhysicsProcessing()==true,"试玩返回跟随相机并恢复移动");
				await Shot("soft-playable"); _quit=true;
			}
			else Input.MouseMode=Input.MouseModeEnum.Captured;
			while(!_quit) await Wait(.1);
		}
	}
	private async Task Press(Key key)
	{
		Input.ParseInputEvent(new InputEventKey {Keycode=key,PhysicalKeycode=key,Pressed=true}); await Wait(.05);
		Input.ParseInputEvent(new InputEventKey {Keycode=key,PhysicalKeycode=key,Pressed=false}); await Wait(.05);
	}
	private void FollowCamera()
	{
		_camera.Projection=Camera3D.ProjectionType.Perspective; _camera.Current=true;
		_camera.GlobalPosition=Player.GlobalPosition+new Vector3(0,3.4f,8.4f);
		_camera.LookAt(Player.GlobalPosition+new Vector3(0,.8f,-3)); _camera.ResetPhysicsInterpolation();
	}
	private void SetView(bool overview)
	{
		_overview=overview; _grass.SetOverview(overview); _scenery.SetOverview(overview);
		if(overview)
		{
			Player.GetNode<Node3D>("CameraRig").ProcessMode=ProcessModeEnum.Disabled; Player.Movement?.SetPhysicsProcess(false);
			_camera.Current=true; _camera.Projection=Camera3D.ProjectionType.Orthogonal; _camera.Size=134;
			_camera.GlobalPosition=new Vector3(72,98,72); _camera.LookAt(Vector3.Zero); _camera.ResetPhysicsInterpolation();
		}
		else if(_playable)
		{
			Player.Movement?.SetPhysicsProcess(true); Player.GetNode<Node3D>("CameraRig").ProcessMode=ProcessModeEnum.Inherit;
			Walk(Player.GetNode("CameraRig")).OfType<Camera3D>().First().Current=true;
		}
		else FollowCamera();
		Caption();
	}
	private void Apply(ShaderGrassPatch.Variant variant) { _grass.ApplyVariant(variant); Caption(); }
	private void Caption()
	{
		string mode=_grass.CurrentVariant==ShaderGrassPatch.Variant.Ground?"丛林地表":"全场半密草 · 慢风";
		string controls=_overview?"Esc 设置":"WASD 移动　鼠标转向　左键出手　Esc 设置";
		_caption.Text=$"柔和草地样板 · {mode} · {(_overview?"全场俯视":"游戏镜头")}\n1 丛林地表　2 半密草地　3 全场俯视　4 游戏镜头　F10 退出\n{controls}";
	}
	public override void _Input(InputEvent inputEvent)
	{
		if(!_ready||Automated&&!_testingKeys||UiRouter.Instance?.AnyOpen==true||inputEvent is not InputEventKey {Pressed:true,Echo:false} key) return;
		switch(key.Keycode)
		{
			case Key.Key1: Apply(ShaderGrassPatch.Variant.Ground); break;
			case Key.Key2: Apply(ShaderGrassPatch.Variant.Batched); break;
			case Key.Key3: SetView(true); break;
			case Key.Key4: SetView(false); break;
			case Key.F10: _quit=true; break;
			default: return;
		}
		GetViewport().SetInputAsHandled();
	}
	private async Task Benchmark()
	{
		var rid=GetViewport().GetViewportRid(); RenderingServer.ViewportSetMeasureRenderTime(rid,true);
		var results=new List<object>();
		var phases=new[]{(false,false),(false,true),(true,false),(true,true),(true,true),(true,false),(false,true),(false,false)};
		try
		{
			for(int run=0;run<phases.Length;run++)
			{
				var (overview,grass)=phases[run]; SetView(overview); Apply(grass?ShaderGrassPatch.Variant.Batched:ShaderGrassPatch.Variant.Ground);
				string phase=(overview?"overview":"follow")+(grass?"_grass":"_ground"); await Wait(1.5);
				var times=new List<double>();var gpu=new List<double>();var cpu=new List<double>();var draws=new List<double>();var primitives=new List<double>();
				ulong start=Time.GetTicksUsec(),previous=start;
				while(Time.GetTicksUsec()-start<3_000_000)
				{
					await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
					ulong now=Time.GetTicksUsec(); times.Add((now-previous)/1000.0);previous=now;
					gpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid));cpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeCpu(rid));
					draws.Add(Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame));primitives.Add(Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame));
				}
				var sorted=times.OrderBy(t=>t).ToArray();
				results.Add(new { run,variant=phase,samples=times.Count,meanFrameMs=times.Average(),medianFrameMs=sorted[sorted.Length/2],p95FrameMs=sorted[(int)(sorted.Length*.95)],
					gpuMs=gpu.Average(),renderCpuMs=cpu.Average(),drawCalls=draws.Average(),primitives=primitives.Average() });
				Check(times.Count>30&&gpu.Any(value=>value>0),$"{phase}实渲染{times.Count}帧，平均{times.Average():0.00}ms，GPU{gpu.Average():0.00}ms，draw{draws.Average():0}");
				if(run<4) await Shot("soft-"+phase);
			}
			using var screenshot=GetViewport().GetTexture().GetImage();
			Check(screenshot.GetSize()==new Vector2I(1920,1080),"基准输出1920×1080，3D比例1，MSAA2x");
			string path=ValidationArtifacts.Path("res://Logs/evidence/grass-soft/benchmark.json");
			System.IO.File.WriteAllText(path,JsonSerializer.Serialize(new { engine=Engine.GetVersionInfo()["string"].AsString(),gpu=RenderingServer.GetVideoAdapterName(),
				resolution="1920x1080",msaa="2x",grassNormal=_grass.NormalCount,chunks=_grass.ChunkCount,trianglesPerClump=ShaderGrassPatch.TrianglesPerClump,seed=_grass.Seed,density=_grass.Density,windFrequency=_grass.WindFrequency,
				note="同一丛林场景、玩家和3个静态真敌人；游戏镜头草地66至80米收缩渐隐，全景取消距离剔除；各暖场1.5秒采样3秒，正反序各一次；不代表满场AI战斗。",phases=results },new JsonSerializerOptions { WriteIndented=true }));
			SetView(false); Apply(ShaderGrassPatch.Variant.Batched); await Wait(.5); await RecordWind();
			await Details();
		}
		finally { RenderingServer.ViewportSetMeasureRenderTime(rid,false); }
	}
	private async Task Preview()
	{
		foreach(bool overview in new[]{false,true})
		foreach(bool grass in new[]{false,true})
		{
			SetView(overview); Apply(grass?ShaderGrassPatch.Variant.Batched:ShaderGrassPatch.Variant.Ground); await Wait(.3);
			await Shot("soft-"+(overview?"overview":"follow")+(grass?"_grass":"_ground"));
		}
		SetView(false); Apply(ShaderGrassPatch.Variant.Batched); await Details();
	}
	private async Task Details()
	{
		_camera.GlobalPosition=new Vector3(-18,3.4f,23); _camera.LookAt(new Vector3(-17,1.3f,14)); _camera.ResetPhysicsInterpolation();
		await Wait(.3); await Shot("soft-wall");
		_camera.GlobalPosition=Player.GlobalPosition+new Vector3(0,11,13); _camera.LookAt(Player.GlobalPosition+new Vector3(0,0,-4));
		_camera.ResetPhysicsInterpolation(); await Wait(.3); await Shot("soft-style");
		_camera.GlobalPosition=Player.GlobalPosition+new Vector3(-4,1.4f,1.6f); _camera.LookAt(Player.GlobalPosition+new Vector3(-3,.06f,-1.4f));
		_camera.ResetPhysicsInterpolation(); await Wait(.3); await Shot("soft-detail");
		Vector3 target=Player.GlobalPosition+new Vector3(-7,.06f,-1.4f);
		for(int angle=0;angle<=90;angle+=45)
		{
			_camera.GlobalPosition=target+new Vector3(-1,1.35f,3).Rotated(Vector3.Up,Mathf.DegToRad(angle)); _camera.LookAt(target);
			_camera.ResetPhysicsInterpolation(); await Wait(.3); await Shot("soft-angle-"+angle);
		}
	}
	private async Task RecordWind()
	{
		// 以固定1/30秒推进Shader时间，避免PNG写盘较慢时压缩视频而把慢风加速。
		Engine.MaxFps=30; float startWind=_grass.WindTime; ulong start=Time.GetTicksMsec(); _grass.SetProcess(false);
		for(int frame=0;frame<180;frame++)
		{
			_grass.GrassMaterial.SetShaderParameter("wind_time",startWind+frame/30.0f);
			await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
			using var image=GetViewport().GetTexture().GetImage();
			if(image.SavePng(ValidationArtifacts.Path($"res://Logs/evidence/grass-soft/frames/{frame:D3}.png"))!=Error.Ok) throw new InvalidOperationException("风动录帧保存失败");
		}
		Check(_grass.WindTime==startWind,"慢风录制固定30fps时步，不受PNG写盘耗时加速影响");
		System.IO.File.WriteAllText(ValidationArtifacts.Path("res://Logs/evidence/grass-soft/recording.json"),JsonSerializer.Serialize(new {frames=180,fps=30,simulatedSeconds=6,windFrequency=_grass.WindFrequency,wallSeconds=(Time.GetTicksMsec()-start)/1000.0}));
		_grass.GrassMaterial.SetShaderParameter("wind_time",startWind); _grass.SetProcess(true); Engine.MaxFps=0;
	}
}
