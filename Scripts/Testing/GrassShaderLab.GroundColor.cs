using System;
using System.Text.Json;
using System.Threading.Tasks;
using ActRoguelike.Core;
using ActRoguelike.Visual;
using Godot;

namespace ActRoguelike.Testing;

public partial class GrassShaderLab
{
	private async Task CheckGroundColor()
	{
		string palette=FileAccess.GetFileAsString("res://Assets/Effects/meadow_palette.gdshaderinc");
		// 保留故障函数作为GPU对照，只存在于验证代码，不改写资源文件。
		string oldPalette="float meadow_hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }\n"+palette[palette.IndexOf("float meadow_noise",StringComparison.Ordinal)..];
		var probe=new SubViewport {Size=new Vector2I(1024,1024),RenderTargetUpdateMode=SubViewport.UpdateMode.Always,Disable3D=true};
		AddChild(probe);
		var rect=new ColorRect {Size=new Vector2(1024,1024)}; probe.AddChild(rect);
		var measurements=new System.Collections.Generic.List<object>();
		foreach(bool before in new[]{true,false})
		{
			var shader=new Shader {Code="shader_type canvas_item; render_mode unshaded;\n"+(before?oldPalette:palette)+"\nvoid fragment() { COLOR=vec4(vec3(meadow_noise(UV*16.0-vec2(8.0))),1.0); }"};
			rect.Material=new ShaderMaterial {Shader=shader}; await Wait(.1);
			await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
			using var image=probe.GetTexture().GetImage();
			float maxBoundaryJump=0; int largeJumps=0,samples=0;
			for(int boundary=64;boundary<1024;boundary+=64)
			for(int i=0;i<1024;i++)
			{
				float dx=Mathf.Abs(image.GetPixel(boundary,i).R-image.GetPixel(boundary-1,i).R);
				float dy=Mathf.Abs(image.GetPixel(i,boundary).R-image.GetPixel(i,boundary-1).R);
				maxBoundaryJump=Mathf.Max(maxBoundaryJump,Mathf.Max(dx,dy));
				if(dx>.04f) largeJumps++; if(dy>.04f) largeJumps++; samples+=2;
			}
			string name=before?"before":"after";
			Check(image.SavePng(ValidationArtifacts.Path($"res://Logs/evidence/ground-color/noise-{name}.png"))==Error.Ok,"GPU噪声对照 "+name);
			GD.Print($"[GroundColor] {name}: boundaryMax={maxBoundaryJump:0.000000}, largeJumps={largeJumps}/{samples}");
			measurements.Add(new {variant=name,maxBoundaryJump,largeJumps,samples});
			if(!before) Check(maxBoundaryJump<=.04f&&largeJumps==0,"正负坐标网格边界连续，无明显颜色跳变");
		}
		probe.QueueFree(); await Wait(.1);
		System.IO.File.WriteAllText(ValidationArtifacts.Path("res://Logs/evidence/ground-color/continuity.json"),JsonSerializer.Serialize(new {renderer=RenderingServer.GetVideoAdapterName(),measurements},new JsonSerializerOptions {WriteIndented=true}));
		var ground=Mode.GetNode<MeshInstance3D>("Arena/Ground/Mesh");
		var current=(ShaderMaterial)ground.MaterialOverride!;
		string groundCode=current.Shader!.Code.Replace("#include \"res://Assets/Effects/meadow_palette.gdshaderinc\"",oldPalette,StringComparison.Ordinal);
		var legacy=new ShaderMaterial {Shader=new Shader {Code=groundCode}};
		_caption.Visible=false; _grass.SetProcess(false);
		foreach(bool overview in new[]{false,true})
		{
			SetView(overview);
			if(!overview) { _camera.GlobalPosition=new Vector3(-18,6,23); _camera.LookAt(new Vector3(-17,.2f,14)); _camera.ResetPhysicsInterpolation(); }
			Apply(ShaderGrassPatch.Variant.Ground);
			foreach(bool before in new[]{true,false})
			{
				ground.MaterialOverride=before?legacy:current; await Wait(.25);
				await GroundShot((overview?"overview":"detail")+(before?"-before":"-after"));
			}
			Apply(ShaderGrassPatch.Variant.Batched); await Wait(.25); await GroundShot((overview?"overview":"detail")+"-grass");
		}
		ground.MaterialOverride=current;
	}
	private async Task GroundShot(string name)
	{
		await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
		using var image=GetViewport().GetTexture().GetImage();
		Check(image.SavePng(ValidationArtifacts.Path($"res://Logs/evidence/ground-color/{name}.png"))==Error.Ok,"真实地表取证 "+name);
	}
}
