using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Combat;

public partial class SkillIndicator
{
	private MeshInstance3D? _brushArea, _brushLine, _brushContour, _brushShadow;
	private ImmediateMesh? _brushAreaMesh, _brushContourMesh, _brushShadowMesh;
	private ShaderMaterial? _areaBrushMaterial, _lineBrushMaterial;
	private StandardMaterial3D? _brushEdgeMaterial;
	private float _warningProgress = -1;
	private Vector3 _sampleCenter = new(float.PositiveInfinity,0,0);
	private float _sampleRadius;
	private Vector3 _drawnCenter = new(float.PositiveInfinity,0,0);
	private float _drawnRadius;
	private readonly float[] _terrain = new float[9];
	public float WarningProgress => _warningProgress;
	public void SetWarningProgress(float progress)
	{
		_warningProgress=Mathf.Clamp(progress,0,1); UpdateBrushTint();
	}
	private void InitializeBrush()
	{
		_brushArea=_drawRoot?.GetNodeOrNull<MeshInstance3D>("BrushArea"); _brushAreaMesh=_brushArea?.Mesh as ImmediateMesh;
		_brushLine=_drawRoot?.GetNodeOrNull<MeshInstance3D>("BrushLine");
		_brushContour=_drawRoot?.GetNodeOrNull<MeshInstance3D>("BrushContour"); _brushContourMesh=_brushContour?.Mesh as ImmediateMesh;
		_brushShadow=_drawRoot?.GetNodeOrNull<MeshInstance3D>("BrushShadow"); _brushShadowMesh=_brushShadow?.Mesh as ImmediateMesh;
		if (_brushArea?.MaterialOverride is ShaderMaterial area) _brushArea.MaterialOverride=_areaBrushMaterial=(ShaderMaterial)area.Duplicate();
		if (_brushLine?.MaterialOverride is ShaderMaterial line) _brushLine.MaterialOverride=_lineBrushMaterial=(ShaderMaterial)line.Duplicate();
		_brushEdgeMaterial=OwnMaterial(_brushContour);
	}
	private void HideBrush()
	{
		if(_brushArea!=null)_brushArea.Visible=false;if(_brushLine!=null)_brushLine.Visible=false;
		if(_brushContour!=null)_brushContour.Visible=false;if(_brushShadow!=null)_brushShadow.Visible=false;
		_warningProgress=-1;
	}
	private void UpdateBrushTint()
	{
		void Apply(ShaderMaterial? material) {
			if(material==null) return;
			material.SetShaderParameter("ink_color",ResolvedColor); material.SetShaderParameter("opacity",IsHostile ? .55f : .45f);
			material.SetShaderParameter("progress",_warningProgress); material.SetShaderParameter("hostile",IsHostile);
		}
		Apply(_areaBrushMaterial);Apply(_lineBrushMaterial);
		if(_brushEdgeMaterial!=null) Tint(_brushEdgeMaterial,new Color(ResolvedColor.R,ResolvedColor.G,ResolvedColor.B,.92f),1);
	}
	private void DrawBrushArea(Vector3 center,float radius)
	{
		if(_brushArea==null || _brushAreaMesh==null) return;
		if(_ring!=null)_ring.Visible=false;if(_fill!=null)_fill.Visible=false;
		if(center.IsEqualApprox(_drawnCenter) && Mathf.IsEqualApprox(radius,_drawnRadius)) {
			_brushArea.Visible=true;if(_brushContour!=null)_brushContour.Visible=true;if(_brushShadow!=null)_brushShadow.Visible=true;UpdateBrushTint();return;
		}
		_drawnCenter=center;_drawnRadius=radius;
		// 小范围移动复用九点高度场；不增加每帧逐顶点射线，几何的水平范围仍由调用方给定。
		if(center.DistanceSquaredTo(_sampleCenter)>.16f || Mathf.Abs(radius-_sampleRadius)>.01f) {
			_sampleCenter=center; _sampleRadius=radius;
			for(int z=0;z<3;z++) for(int x=0;x<3;x++) {
				Vector3 point=center+new Vector3(x-1,0,z-1)*radius;
				Vector3 hit=SnapToGround(point);
				_terrain[z*3+x]=Mathf.Abs(hit.Y-center.Y)<2f ? hit.Y : center.Y;
			}
		}
		float Height(float x,float z) {
			float sx=Mathf.Clamp(x+1,0,2),sz=Mathf.Clamp(z+1,0,2);int ix=Mathf.Min(1,(int)sx),iz=Mathf.Min(1,(int)sz);
			return Mathf.Lerp(Mathf.Lerp(_terrain[iz*3+ix],_terrain[iz*3+ix+1],sx-ix),Mathf.Lerp(_terrain[(iz+1)*3+ix],_terrain[(iz+1)*3+ix+1],sx-ix),sz-iz)+center.Y-_sampleCenter.Y+GroundOffset;
		}
		Vector3 Point(float x,float z) => new(center.X+x*radius,Height(x,z),center.Z+z*radius);
		_brushAreaMesh.ClearSurfaces();_brushAreaMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
		void Vertex(float x,float z){_brushAreaMesh.SurfaceSetUV(new Vector2(x+1,z+1)*.5f);_brushAreaMesh.SurfaceAddVertex(Point(x,z));}
		for(int z=0;z<2;z++) for(int x=0;x<2;x++){float a=x-1,b=z-1;Vertex(a,b);Vertex(a+1,b);Vertex(a+1,b+1);Vertex(a,b);Vertex(a+1,b+1);Vertex(a,b+1);}
		_brushAreaMesh.SurfaceEnd();_brushArea.Visible=true;
		var outline=new List<Vector3>(65);
		for(int i=0;i<=64;i++){float a=Mathf.Tau*i/64;outline.Add(Point(Mathf.Cos(a),Mathf.Sin(a))+Vector3.Up*.012f);}
		FillBand(_brushShadowMesh,_brushShadow,outline,.085f,Vector3.Up,_brushShadow?.MaterialOverride);
		FillBand(_brushContourMesh,_brushContour,outline,.038f,Vector3.Up,_brushEdgeMaterial);
		if(_ring!=null)_ring.Visible=false;if(_fill!=null)_fill.Visible=false;
		UpdateBrushTint();
	}
	private void DrawBrushLine(Vector3 start,Vector3 forward,float length,float width)
	{
		if(_brushLine==null) return;
		_brushLine.Position=start+forward*length*.5f;_brushLine.Rotation=new Vector3(0,Mathf.Atan2(-forward.X,-forward.Z),0);
		_brushLine.Scale=new Vector3(width,1,length);_brushLine.Visible=true;
		if(_lineFill!=null)_lineFill.Visible=false;UpdateBrushTint();
	}
}
