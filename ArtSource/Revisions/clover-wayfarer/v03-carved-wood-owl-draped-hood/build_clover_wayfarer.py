"""三叶旅童：以纯Blender几何制作的低多边形角色候选，保留可编辑网格与骨架。"""
import bpy
import bmesh
import math
import json
from mathutils import Vector
from mathutils.geometry import tessellate_polygon
from mathutils.bvhtree import BVHTree

SOURCE = "F:/Godot项目/项目a/ArtSource/Blender/clover-wayfarer/"
OUTPUT = "F:/Godot项目/项目a/ArtSource/Exports/clover-wayfarer/"
PREFIX = "CW_"

# 仅重建本工具自己的场景，保留用户原先的场景和模型。
for scene_name in ("CloverWayfarer_Preview", "CloverWayfarer"):
    old = bpy.data.scenes.get(scene_name)
    if old:
        if bpy.context.window.scene == old:
            bpy.context.window.scene = next(s for s in bpy.data.scenes if s != old)
        for obj in list(old.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.scenes.remove(old)
for action in list(bpy.data.actions):
    if action.get("asset_id") == "clover-wayfarer" or any(slot.identifier.endswith("CW_Rig") for slot in action.slots):
        action.use_fake_user=False
        if action.users==0: bpy.data.actions.remove(action)
for blocks in (bpy.data.meshes,bpy.data.materials,bpy.data.armatures,bpy.data.cameras,bpy.data.lights,bpy.data.worlds,bpy.data.collections):
    for block in list(blocks):
        if block.name.startswith(PREFIX) and block.users==0: blocks.remove(block)
scene = bpy.data.scenes.new("CloverWayfarer")
bpy.context.window.scene = scene
character = bpy.data.collections.new(PREFIX + "Character")
scene.collection.children.link(character)
studio = bpy.data.collections.new(PREFIX + "Studio")
scene.collection.children.link(studio)
materials = {}
objects = []
bindings = {}

def material(name, hex_color, roughness=.85):
    value = hex_color.lstrip("#")
    rgb = [int(value[i:i+2], 16) / 255 for i in (0, 2, 4)]
    linear = [((c + .055) / 1.055) ** 2.4 if c > .04045 else c / 12.92 for c in rgb]
    mat = bpy.data.materials.new(PREFIX + name)
    mat.diffuse_color = (*linear, 1)
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*linear, 1)
    bsdf.inputs["Roughness"].default_value = roughness
    materials[name] = mat
    return mat

for name, color in {
    "Moss": "899648", "MossLight": "9eaa5a", "MossShade": "70813f",
    "Rim": "b1b76d", "DeepGreen": "304638", "HoodInside": "24392f",
    "Mask": "b98959", "MaskLight": "c89b6b", "MaskShade": "a77749", "MaskEdge": "795437", "Tunic": "ded1a9",
    "TunicShade": "c7bc94", "Pants": "485142", "Leather": "735c43",
    "LeatherLight": "927454", "Boots": "514738", "Sole": "897354",
    "Clover": "e1d49d", "Clasp": "c1a66c",
}.items():
    material(name, color)

def mesh_obj(name, vertices, faces, mats, collection=character, bone=None, cape=False):
    mesh = bpy.data.meshes.new(PREFIX + name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(PREFIX + name, mesh)
    collection.objects.link(obj)
    for mat in mats:
        mesh.materials.append(materials[mat] if isinstance(mat, str) else mat)
    if collection == character:
        objects.append(obj)
        bindings[obj.name] = (bone, cape)
    return obj

def uv_ellipsoid(name, center, scale, mat, bone, segments=16, rings=8):
    vertices = [(center[0], center[1], center[2] + scale[2])]
    for j in range(1, rings):
        phi = math.pi * j / rings
        for i in range(segments):
            theta = math.tau * i / segments
            vertices.append((center[0] + scale[0] * math.sin(phi) * math.cos(theta),
                             center[1] + scale[1] * math.sin(phi) * math.sin(theta),
                             center[2] + scale[2] * math.cos(phi)))
    bottom = len(vertices)
    vertices.append((center[0], center[1], center[2] - scale[2]))
    faces = [(0, 1+i, 1+(i+1)%segments) for i in range(segments)]
    for j in range(rings-2):
        a = 1+j*segments
        b = a+segments
        for i in range(segments):
            ni=(i+1)%segments
            faces.append((a+i,b+i,b+ni,a+ni))
    a = 1+(rings-2)*segments
    faces.extend((bottom,a+(i+1)%segments,a+i) for i in range(segments))
    return mesh_obj(name,vertices,faces,[mat],bone=bone)

def tube(name, points, radii, mats, bone, sides=10, flatten=1.0):
    points = [Vector(p) for p in points]
    direction=(points[-1]-points[0]).normalized()
    helper=Vector((0,1,0)) if abs(direction.y)<.95 else Vector((0,0,1))
    x=direction.cross(helper).normalized()
    y=direction.cross(x).normalized()
    vertices=[]
    for point,radius in zip(points,radii):
        for i in range(sides):
            t=math.tau*i/sides
            vertices.append(point + x*math.cos(t)*radius + y*math.sin(t)*radius*flatten)
    faces=[tuple(reversed(range(sides)))]
    for j in range(len(points)-1):
        for i in range(sides):
            ni=(i+1)%sides
            faces.append((j*sides+i,j*sides+ni,(j+1)*sides+ni,(j+1)*sides+i))
    faces.append(tuple((len(points)-1)*sides+i for i in range(sides)))
    return mesh_obj(name,vertices,faces,mats,bone=bone)

def z_loft(name, rings, sides, mats, bone, offset=0):
    vertices=[]
    for z,rx,ry,cy in rings:
        for i in range(sides):
            t=math.tau*i/sides + offset
            vertices.append((rx*math.sin(t),cy-ry*math.cos(t),z))
    faces=[tuple(reversed(range(sides)))]
    for j in range(len(rings)-1):
        for i in range(sides):
            ni=(i+1)%sides
            faces.append((j*sides+i,j*sides+ni,(j+1)*sides+ni,(j+1)*sides+i))
    faces.append(tuple((len(rings)-1)*sides+i for i in range(sides)))
    return mesh_obj(name,vertices,faces,mats,bone=bone)

# 仅保留短靴；角色没有手、袖口、手臂或斗篷内部的身体填块。
for side,sign in (("Left",1),("Right",-1)):
    shoe=z_loft(side+"Boot",[(.055,.135,.21,-.075),(.105,.151,.225,-.082),(.18,.141,.203,-.065),(.25,.109,.13,-.005),(.34,.106,.12,0)],12,["Boots"],side+"Foot")
    shoe.location.x=sign*.165
    sole=z_loft(side+"Sole",[(.03,.137,.213,-.075),(.07,.145,.219,-.075),(.09,.143,.215,-.075)],12,["Sole"],side+"Foot")
    sole.location.x=sign*.165
    cuff=z_loft(side+"BootCuff",[(.295,.113,.128,0),(.34,.117,.132,0),(.355,.111,.125,0)],12,["LeatherLight"],side+"Shin")
    cuff.location.x=sign*.165

# 长斗篷只保留双面可见的薄布面，前襟收窄，背部连续以承载图案。
steps=40
opening=.065
angles=[opening+(math.tau-2*opening)*i/steps for i in range(steps+1)]
cape_rings=[(1.10,.245,.215),(1.035,.355,.258),(.88,.415,.285),(.71,.475,.325),(.53,.535,.37),(.36,.578,.41),(.235,.605,.435)]
vertices=[]
for j,(z,rx,ry) in enumerate(cape_rings):
    for i,t in enumerate(angles):
        drop=j/(len(cape_rings)-1)
        wave=.68*math.cos(t*7+.35)+.28*math.sin(t*11+1.1)+.22*math.cos(t*4-.4)
        fold=1+(.015+.105*drop**.9)*wave
        hem=.028*math.sin(t*3+.5)+.019*math.cos(t*7-.2)+.012*math.sin(t*11+.8)
        zz=z+hem*drop**3
        vertices.append((rx*math.sin(t)*fold,-ry*math.cos(t)*fold,zz))
faces=[]
for j in range(len(cape_rings)-1):
    for i in range(steps):
        a=j*(steps+1)+i
        faces.extend([(a,a+steps+2,a+1),(a,a+steps+1,a+steps+2)])
cape=mesh_obj("Cloak",vertices,faces,["Moss","MossLight","MossShade"],cape=True)
for poly in cape.data.polygons:
    index=poly.index//2%steps
    poly.material_index = 1 if index in (1,2,12,13,26,27,37) else 2 if index in (6,7,20,21,33) else 0
cape["surface_only"]=True

# 前襟压边与底边使用几何窄带；大色块而非繁密纹理。
for side,index in (("Left",0),("Right",steps)):
    verts=[]
    for j in range(len(cape_rings)):
        outer=Vector(vertices[j*(steps+1)+index])
        inside=Vector(vertices[j*(steps+1)+(index+1 if index==0 else index-1)])
        direction=(inside-outer).normalized()
        verts.extend((outer+Vector((0,-.006,0)),outer+direction*.025+Vector((0,-.006,0))))
    fs=[(j*2,j*2+1,j*2+3,j*2+2) for j in range(len(cape_rings)-1)]
    mesh_obj("CloakFrontTrim"+side,verts,fs,["Rim"],cape=True)

# 兜帽改为松垂布罩：上部收窄，下侧布角垂到领围，后方收成垂下的帽尖。
hood_half=[(0,.46),(.18,.44),(.35,.38),(.47,.27),(.51,.12),(.53,-.12),(.575,-.32),(.56,-.42),(.40,-.48),(.20,-.49),(0,-.505)]
hood_outline=hood_half+[(-x,z) for x,z in reversed(hood_half[1:-1])]
segments=len(hood_outline)
hood_rings=[(-.285,1,1,1.435),(-.15,1.035,.99,1.45),(.045,1,.95,1.46),(.235,.80,.87,1.46),(.39,.44,.75,1.44)]

def hood_point(index,y,sx,sz,cz):
    x,z=hood_outline[index]
    low=max(0,-z/.505)
    fold=.026*math.cos(index*2.7+.4)*low
    return (x*sx,y-.075*low+fold,cz+z*sz)

verts=[]
for row,(y,sx,sz,cz) in enumerate(hood_rings):
    for i in range(segments):
        verts.append(hood_point(i,y,sx,sz,cz))
faces=[]
for j in range(len(hood_rings)-1):
    for i in range(segments):
        ni=(i+1)%segments
        a=j*segments+i; b=j*segments+ni
        faces.append((a,(j+1)*segments+i,(j+1)*segments+ni,b))
# 后帽用顺布纹的竖向面片闭合，避免所有面挤到一点形成束袋结。
last=(len(hood_rings)-1)*segments
middle=segments//2
seam=[last]
for i in range(1,middle):
    right=Vector(verts[last+i]); left=Vector(verts[last+segments-i])
    center=(right+left)*.5
    center.y+=.023
    seam.append(len(verts)); verts.append(tuple(center))
seam.append(last+middle)
for i in range(middle):
    ri=last+i; rn=last+i+1
    li=last+(segments-i)%segments; ln=last+(segments-i-1)%segments
    right_face=tuple(dict.fromkeys((seam[i],ri,rn,seam[i+1])))
    left_face=tuple(dict.fromkeys((li,seam[i],seam[i+1],ln)))
    if len(right_face)>=3: faces.append(right_face)
    if len(left_face)>=3: faces.append(left_face)
hood=mesh_obj("Hood",verts,faces,["Moss","MossLight","MossShade","HoodInside"],bone="Head")
for p in hood.data.polygons:
    i=p.index%segments
    p.material_index=1 if i in (1,2,12,13) else 2 if i in (5,6,8,14,16,18) else 0

# 深色只来自紧贴兜帽布面的内衬，眼孔后仍然是空腔，没有占位头球。
bm=bmesh.new(); bm.from_mesh(hood.data)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
radial=sum(face.normal.dot(Vector((face.calc_center_median().x,0,face.calc_center_median().z-1.435))) for face in bm.faces)
if radial<0: bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
bm.to_mesh(hood.data); bm.free()
lining=hood.modifiers.new("Thin dark hood lining","SOLIDIFY")
lining.thickness=.006; lining.offset=-1; lining.material_offset=3
bpy.context.view_layer.objects.active=hood
hood.select_set(True)
bpy.ops.object.modifier_apply(modifier=lining.name)
hood.select_set(False)

# 兜帽内沿是一圈向内卷的布边，内部不放头球或眼球。
rim_rings=[(-.285,1,1,1.435),(-.335,.975,.975,1.435),(-.328,.925,.93,1.435),(-.235,.905,.91,1.435)]
v=[]
for y,sx,sz,cz in rim_rings:
    for i in range(segments):
        v.append(hood_point(i,y,sx,sz,cz))
f=[]
for j in range(len(rim_rings)-1):
    for i in range(segments):
        ni=(i+1)%segments
        f.append((j*segments+i,(j+1)*segments+i,(j+1)*segments+ni,j*segments+ni))
rim=mesh_obj("HoodDrapedFold",v,f,["MossShade","Rim","HoodInside"],bone="Head")
for p in rim.data.polygons: p.material_index = p.index//segments

# 按用户红线制作上挑的猫头鹰脸：宽直颊、中央平眉、上挑耳角和尖下颌。
# 木板采用平面拼色、真实厚度和窄倒角，不再使用圆盘或球面形体。
half_outline=[(0,.380),(.18,.365),(.30,.315),(.355,.265),(.430,.430),(.535,.515),(.515,.300),(.492,.100),(.455,.015),(.455,-.250),(.390,-.300),(0,-.415)]
outline=half_outline+[(-x,z) for x,z in reversed(half_outline[1:-1])]
inset=.955
inner=[(x*inset,z*inset) for x,z in outline]
cuts=[-.60,-.36,-.22,-.07,.075,.235,.37,.60]
depths=[-.451,-.455,-.463,-.459,-.465,-.460,-.456,-.450]

def wood_depth(x):
    for j in range(len(cuts)-1):
        if x<=cuts[j+1]:
            ratio=(x-cuts[j])/(cuts[j+1]-cuts[j])
            return depths[j]*(1-ratio)+depths[j+1]*ratio
    return depths[-1]

def clip_x(poly,value,keep_right):
    result=[]
    for a,b in zip(poly,poly[1:]+poly[:1]):
        ina=(a[0]>=value-1e-9) if keep_right else (a[0]<=value+1e-9)
        inb=(b[0]>=value-1e-9) if keep_right else (b[0]<=value+1e-9)
        if ina: result.append(a)
        if ina!=inb:
            ratio=(value-a[0])/(b[0]-a[0])
            result.append((value,a[1]+ratio*(b[1]-a[1])))
    clean=[]
    for p in result:
        if not clean or (Vector(p)-Vector(clean[-1])).length>1e-7: clean.append(p)
    if len(clean)>1 and (Vector(clean[0])-Vector(clean[-1])).length<1e-7: clean.pop()
    return clean

v=[]; f=[]; face_mats=[]; front_indices={}
def front_vertex(point):
    x,z=point
    key=(round(x,7),round(z,7))
    if key not in front_indices:
        front_indices[key]=len(v)
        v.append((x,wood_depth(x),1.405+z))
    return front_indices[key]

for j in range(len(cuts)-1):
    panel=clip_x(clip_x(inner,cuts[j],True),cuts[j+1],False)
    if len(panel)>=3:
        f.append(tuple(front_vertex(p) for p in panel))
        face_mats.append([2,0,1,0,1,0,2][j])

# 倒角边界在木板分片处共享顶点，保持完整封闭的薄壳拓扑。
boundary=[]
for a,b in zip(inner,inner[1:]+inner[:1]):
    samples=[(0,a)]
    if abs(b[0]-a[0])>1e-8:
        for x in cuts[1:-1]:
            t=(x-a[0])/(b[0]-a[0])
            if 1e-7<t<1-1e-7: samples.append((t,(x,a[1]+t*(b[1]-a[1]))))
    boundary.extend(point for _,point in sorted(samples))
front=[front_vertex(p) for p in boundary]
edge=[]; back=[]
for x,z in boundary:
    edge.append(len(v)); v.append((x/inset,wood_depth(x)+.026,1.405+z/inset))
    back.append(len(v)); v.append((x/inset,-.345,1.405+z/inset))
for i in range(len(boundary)):
    ni=(i+1)%len(boundary)
    f.append((front[i],front[ni],edge[ni],edge[i])); face_mats.append(1)
    f.append((edge[i],edge[ni],back[ni],back[i])); face_mats.append(3)
f.append(tuple(reversed(back))); face_mats.append(3)
mask=mesh_obj("OwlMask",v,f,["Mask","MaskLight","MaskShade","MaskEdge"],bone="Head")
mask["interior_empty"]=True
mask["facial_features"]="two eye openings only"
mask["design_reference"]="user-owl-outline-v03.png"
mask["surface"]="carved flat wooden board with bevel and sparse chisel cuts"
for p,mi in zip(mask.data.polygons,face_mats): p.material_index=mi
bm=bmesh.new(); bm.from_mesh(mask.data)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.to_mesh(mask.data); bm.free()

def carve(cutter,name):
    bm=bmesh.new(); bm.from_mesh(cutter.data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(cutter.data); bm.free()
    bpy.context.view_layer.objects.active=mask
    cut=mask.modifiers.new(name,"BOOLEAN")
    cut.operation="DIFFERENCE"; cut.solver="EXACT"; cut.object=cutter
    bpy.ops.object.modifier_apply(modifier=cut.name)
    objects.remove(cutter); bindings.pop(cutter.name,None)
    bpy.data.objects.remove(cutter,do_unlink=True)

for side,sign in (("Left",1),("Right",-1)):
    cutter=tube("TEMP_Eye"+side,[(sign*.175,-.68,1.38),(sign*.175,-.475,1.38),(sign*.175,-.410,1.38),(sign*.175,-.25,1.38)],[1.28,1.28,1,1],["MaskEdge"],"Head",sides=8)
    for vertex in cutter.data.vertices:
        vertex.co.x = sign*.175+(vertex.co.x-sign*.175)*.074
        vertex.co.z = 1.38+(vertex.co.z-1.38)*.105
    carve(cutter,"Angular carved eye opening")

# 稀疏的三角凿痕只切入表面，不添加眉鼻嘴，也不把木板分成独立零件。
for j,(x,top,bottom,width) in enumerate([(-.28,.33,.125,.012),(.055,.39,.215,.009),(.29,.33,.105,.012),(-.12,-.385,-.22,.010)]):
    tri=[(x-width,top),(x+width,top+.025),(x+.003,bottom)]
    vv=[(xx,y,1.405+zz) for y in (-.70,-.439) for xx,zz in tri]
    cutter=mesh_obj("TEMP_Chisel"+str(j),vv,[(0,2,1),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)],["MaskEdge"],bone="Head")
    carve(cutter,"Shallow chisel groove")

# 围巾式领围是绕颈的开放布带，前侧叠成低垂的V形，不填充头部空腔。
scarf_steps=32
scarf_rings=[(.375,.285,1.145,.140),(.445,.345,1.075,.170),(.465,.360,1.025,.180),(.405,.300,.985,.145)]
vv=[]
for row,(rx,ry,z,drop) in enumerate(scarf_rings):
    for i in range(scarf_steps):
        t=math.tau*i/scarf_steps
        front=max(0,1-abs(math.sin(t))) if math.cos(t)>0 else 0
        vv.append((rx*math.sin(t),-.055-ry*math.cos(t),z-drop*front))
ff=[]
for row in range(len(scarf_rings)-1):
    for i in range(scarf_steps):
        ni=(i+1)%scarf_steps
        ff.append((row*scarf_steps+i,row*scarf_steps+ni,(row+1)*scarf_steps+ni,(row+1)*scarf_steps+i))
scarf=mesh_obj("ScarfCowl",vv,ff,["MossShade","Moss","MossLight"],bone="Chest")
for p in scarf.data.polygons: p.material_index=[0,2,0][p.index//scarf_steps]
scarf["surface_only"]=True

# 背面三叶草是一层贴着斗篷的平面布印，不是背包或凸起装备。
bpy.context.view_layer.update()
bvh=BVHTree.FromPolygons([vertex.co for vertex in cape.data.vertices],[list(poly.vertices) for poly in cape.data.polygons],all_triangles=False)
def project_back(x,z):
    hit=bvh.ray_cast(Vector((x,2,z)),Vector((0,-1,0)),4)
    return (x,hit[0].y+.006 if hit[0] is not None else .34,z)

def decal_polygon(name, polygon):
    points=[Vector((x,z,0)) for x,z in polygon]
    tris=tessellate_polygon([points])
    triangles=[]
    def split(a,b,c,level=0):
        if max((a-b).length,(a-c).length,(b-c).length)>.032 and level<5:
            ab=(a+b)*.5; bc=(b+c)*.5; ca=(c+a)*.5
            for tr in ((a,ab,ca),(ab,b,bc),(ca,bc,c),(ab,bc,ca)): split(*tr,level+1)
        else: triangles.append((a,b,c))
    for tri in tris:
        split(*(points[p] if isinstance(p, int) else p for p in tri))
    vertices=[]; faces=[]
    for tri in triangles:
        start=len(vertices)
        vertices.extend(project_back(p.x,p.y) for p in tri)
        faces.append((start,start+1,start+2))
    return mesh_obj(name,vertices,faces,["Clover"],cape=True)

heart=[(0,0),(-.043,.033),(-.077,.085),(-.081,.13),(-.052,.157),(-.021,.155),(0,.127),(.021,.155),(.052,.157),(.081,.13),(.077,.085),(.043,.033)]
for index,angle in enumerate((0,2*math.pi/3,-2*math.pi/3)):
    poly=[]
    for x,z in heart:
        poly.append((x*math.cos(angle)-z*math.sin(angle),.79+x*math.sin(angle)+z*math.cos(angle)))
    decal_polygon("CloverLeaf"+str(index+1),poly)
decal_polygon("CloverStem",[(-.008,.79),(.009,.79),(.015,.709),(.038,.65),(.02,.641),(-.002,.703)])

# 统一法线并固化对象变换，方便编辑、权重与引擎导出。
for obj in objects:
    bpy.context.view_layer.objects.active=obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    bm=bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data); bm.free()
    obj.select_set(False)

# 简单可编辑骨架：角色骨骼与三片斗篷独立控制，所有权重写在本资产内。
armature=bpy.data.armatures.new(PREFIX+"Skeleton")
rig=bpy.data.objects.new(PREFIX+"Rig",armature); character.objects.link(rig)
bpy.context.view_layer.objects.active=rig; rig.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
bones={}
def bone(name,head,tail,parent=None):
    eb=armature.edit_bones.new(name); eb.head=head; eb.tail=tail
    if parent: eb.parent=armature.edit_bones[parent]
    bones[name]=(Vector(head),Vector(tail))
bone("Root",(0,0,0),(0,0,.16))
bone("Hips",(0,0,.45),(0,0,.69),"Root")
bone("Spine",(0,0,.69),(0,0,.91),"Hips")
bone("Chest",(0,0,.91),(0,0,1.10),"Spine")
bone("Neck",(0,0,1.10),(0,0,1.20),"Chest")
bone("Head",(0,0,1.20),(0,0,1.73),"Neck")
for side,sign in (("Left",1),("Right",-1)):
    bone(side+"Thigh",(sign*.14,0,.53),(sign*.16,0,.31),"Hips")
    bone(side+"Shin",(sign*.16,0,.31),(sign*.165,0,.13),side+"Thigh")
    bone(side+"Foot",(sign*.165,0,.13),(sign*.165,-.23,.10),side+"Shin")
bone("CapeBack",(0,.12,1.05),(0,.40,.235),"Chest")
bone("CapeLeft",(.18,.01,1.05),(.60,.01,.235),"Chest")
bone("CapeRight",(-.18,.01,1.05),(-.60,.01,.235),"Chest")
bpy.ops.object.mode_set(mode="OBJECT")
rig.show_in_front=True
for obj in objects:
    assigned,is_cape=bindings[obj.name]
    if is_cape:
        groups={name:obj.vertex_groups.new(name=name) for name in ("Chest","CapeBack","CapeLeft","CapeRight")}
        for vertex in obj.data.vertices:
            z=vertex.co.z
            cloth=max(0,min(1,(1.045-z)/.30))
            weights={"CapeBack":max(.02,vertex.co.y+.13)*2,"CapeLeft":max(.02,vertex.co.x+.10),"CapeRight":max(.02,-vertex.co.x+.10)}
            total=sum(weights.values())
            if cloth<1: groups["Chest"].add([vertex.index],1-cloth,"REPLACE")
            for name,weight in weights.items(): groups[name].add([vertex.index],cloth*weight/total,"REPLACE")
    else:
        group=obj.vertex_groups.new(name=assigned)
        group.add(list(range(len(obj.data.vertices))),1,"REPLACE")
    mod=obj.modifiers.new("Character skin","ARMATURE"); mod.object=rig
    obj.parent=rig

# 自制两个轻动作供后续导入检查，保留低多边形玩偶的节奏。
for pb in rig.pose.bones: pb.rotation_mode="XYZ"
rig.animation_data_create()
for name,length in (("idle",60),("walk",36)):
    action=bpy.data.actions.new(name)
    action["asset_id"]="clover-wayfarer"
    rig.animation_data.action=action
    for frame in range(1,length+2,3):
        phase=(frame-1)/length*math.tau
        for pb in rig.pose.bones:
            pb.rotation_euler=(0,0,0); pb.location=(0,0,0)
        if name=="idle":
            rig.pose.bones["Chest"].rotation_euler[0]=.018*math.sin(phase)
            rig.pose.bones["Head"].rotation_euler[1]=.028*math.sin(phase+.4)
            rig.pose.bones["CapeBack"].rotation_euler[0]=.025*math.sin(phase+.8)
            rig.pose.bones["Hips"].location.z=.006*math.sin(phase)
        else:
            for side,sign in (("Left",1),("Right",-1)):
                rig.pose.bones[side+"Thigh"].rotation_euler[0]=.30*math.sin(phase)*sign
                rig.pose.bones[side+"Shin"].rotation_euler[0]=max(0,-math.sin(phase)*sign)*.32
                rig.pose.bones["Cape"+side].rotation_euler[0]=.04*math.sin(phase+.6)*sign
            rig.pose.bones["Hips"].location.z=.012*(1-math.cos(phase*2))
            rig.pose.bones["CapeBack"].rotation_euler[0]=.055*math.sin(phase*2+.8)
        for pb in rig.pose.bones:
            pb.keyframe_insert(data_path="rotation_euler",frame=frame,group=pb.name)
            if pb.name=="Hips": pb.keyframe_insert(data_path="location",frame=frame,group=pb.name)
    action.use_fake_user=True
rig.animation_data.action=bpy.data.actions.get("idle")
scene.render.fps=30
scene.frame_start=1; scene.frame_end=61
scene.frame_set(1)

# 仅展示用的干净摄影棚，不加入GLB。
ground_mat=material("StudioFloor","c2c8ad")
bpy.ops.mesh.primitive_cylinder_add(vertices=64,radius=.88,depth=.075,location=(0,0,-.035))
plinth=bpy.context.object; plinth.name=PREFIX+"Plinth"
for coll in list(plinth.users_collection): coll.objects.unlink(plinth)
studio.objects.link(plinth); plinth.data.materials.append(materials["MossShade"])
bevel=plinth.modifiers.new("Soft edge","BEVEL"); bevel.width=.018; bevel.segments=2
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.08))
ground=bpy.context.object; ground.name=PREFIX+"Ground"
for coll in list(ground.users_collection): coll.objects.unlink(ground)
studio.objects.link(ground); ground.data.materials.append(ground_mat)
world=bpy.data.worlds.new(PREFIX+"World"); world.use_nodes=True
background=next(n for n in world.node_tree.nodes if n.type=="BACKGROUND")
background.inputs[0].default_value=(.55,.61,.50,1)
background.inputs[1].default_value=.45
scene.world=world
def area(name,loc,power,size,color):
    data=bpy.data.lights.new(PREFIX+name,"AREA"); data.energy=power; data.shape="DISK"; data.size=size; data.color=color
    obj=bpy.data.objects.new(PREFIX+name,data); studio.objects.link(obj); obj.location=loc
    obj.rotation_euler=(Vector((0,0,.9))-obj.location).to_track_quat('-Z','Y').to_euler()
area("Key",(-3,-4,5),500,4.0,(1.0,.90,.73))
area("Fill",(3,-2,3),250,3.5,(.78,.89,1.0))
area("Rim",(1,3,4),450,3.0,(1.0,.94,.77))
camera_data=bpy.data.cameras.new(PREFIX+"Camera"); camera=bpy.data.objects.new(PREFIX+"Camera",camera_data); studio.objects.link(camera)
camera_data.type="ORTHO"; camera_data.ortho_scale=2.50; camera_data.lens=60
scene.camera=camera
def camera_view(position):
    camera.location=position
    camera.rotation_euler=(Vector((0,0,.91))-camera.location).to_track_quat('-Z','Y').to_euler()
camera_view((3,-5.6,2.8))
scene.render.engine="BLENDER_EEVEE"
scene.render.resolution_x=1200; scene.render.resolution_y=1200; scene.render.resolution_percentage=100
scene.render.image_settings.file_format="PNG"
scene.render.film_transparent=False
scene.view_settings.view_transform="AgX"
scene.view_settings.look="AgX - Medium High Contrast"

# 默认视图直接显示人物，保留骨架可编辑但不遮挡模型。
rig.select_set(False)
bpy.context.view_layer.objects.active=mask; mask.select_set(True)
for screen in bpy.data.screens:
    for area_ui in screen.areas:
        if area_ui.type=="VIEW_3D":
            area_ui.spaces.active.shading.type="MATERIAL"
            area_ui.spaces.active.region_3d.view_distance=3.4
            area_ui.spaces.active.region_3d.view_location=Vector((0,0,.95))

triangles=0
for obj in objects:
    obj.data.calc_loop_triangles(); triangles+=len(obj.data.loop_triangles)
report={"asset":"clover-wayfarer","triangles":triangles,"mesh_objects":len(objects),"bones":len(armature.bones),
        "animations":["idle","walk"],"height_m":max(v.co.z for obj in objects for v in obj.data.vertices),
        "design":{"mask_shape":"angular owl face traced from user outline","mask_surface":"carved wooden board","mask_features":"two eye openings only","mask_interior_empty":True,"hands":False,"hood":"draped fabric with hanging lower folds","scarf_cowl":True,"cloak_surface_only":True,"cloak_hem":"irregular folds above ankle","backpack":False,"back_pattern":"three-leaf clover"},
        "references":["https://oricutstudios.itch.io/low-poly-forest-spirit-nori","https://oricutstudios.itch.io/low-poly-campfire-scout-niko"],
        "stage":"third model candidate; angular wooden owl mask, no hands, draped hood and scarf cowl; animation and game integration pending review"}
bpy.ops.wm.save_as_mainfile(filepath=SOURCE+"clover-wayfarer.blend")
print("CLOVER_MODEL_READY "+json.dumps(report))
