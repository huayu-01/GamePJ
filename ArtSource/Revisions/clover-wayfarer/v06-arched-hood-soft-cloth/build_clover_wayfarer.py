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
    "Clover": "e1d49d", "Jade": "2bc98e", "Clasp": "c1a66c",
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

# 布料由颈部悬挂：肩部先包圆，再松垂，不使用等距贯穿全身的锥形褶。
steps=44
cape_rings=[(1.10,.245,.215),(1.025,.365,.258),(.915,.405,.285),(.785,.425,.305),(.61,.455,.325),(.43,.485,.34),(.245,.535,.365)]
folds=[(.42,.17,-.080,.22),(1.25,.29,.075,-.16),(2.18,.18,-.075,.13),(3.18,.26,-.085,-.14),(4.18,.20,-.090,.18),(5.13,.30,.070,-.13),(5.86,.16,-.070,.15)]

def angular_distance(a,b):
    return (a-b+math.pi)%math.tau-math.pi

def cloth_fold(t,drop):
    displacement=0
    for k,(center,width,amplitude,lean) in enumerate(folds):
        center+=lean*(drop-.25)+.045*math.sin(drop*math.pi+k*.8)
        q=angular_distance(t,center)/width
        growth=(.10+.90*min(1,drop/(.55+.20*(k%3))))*(.75+.25*math.sin(drop*math.pi+k*.7))
        displacement+=amplitude*growth*(1-q*q)*math.exp(-.5*q*q)
    for center,lean in ((.87,.8),(3.05,-.45),(5.30,-.7)):
        q=angular_distance(t,center+lean*drop)/.13
        displacement-=.034*math.exp(-.5*q*q)*math.exp(-((drop-.22)/.20)**2)
    return displacement

vertices=[]
cape_angles=[]
for j,(z,rx,ry) in enumerate(cape_rings):
    drop=j/(len(cape_rings)-1)
    left_open=.012+.22*drop**1.65
    right_open=.025+.18*drop**1.55
    for i in range(steps+1):
        u=i/steps
        t=left_open+(math.tau-left_open-right_open)*u
        cape_angles.append(t)
        crease=cloth_fold(t,drop)
        front_drape=math.exp(-.5*(angular_distance(t,.65)/.55)**2)+math.exp(-.5*(angular_distance(t,5.72)/.50)**2)
        crease+=.029*math.sin(2.1*math.pi*drop+.9*math.sin(t))*math.exp(-((drop-.45)/.27)**2)*front_drape
        hem=.034*math.sin(t*2.2+.5)+.016*math.sin(t*5+1.2)
        sag=-.027*math.sin(t)**2*math.sin(drop*math.pi)
        sag+=.014*math.sin(t+.6)*math.sin(drop*math.pi)
        curl=.042*math.exp(-.5*(angular_distance(t,.4)/.34)**2)-.032*math.exp(-.5*(angular_distance(t,5.9)/.31)**2)
        radial_curl=curl*drop**6
        xx=(rx+crease+radial_curl)*math.sin(t)+.012*math.sin(drop*math.pi)
        yy=-(ry+crease+radial_curl)*math.cos(t)
        yy-=.018*math.exp(-.5*(angular_distance(t,.22)/.28)**2)*math.sin(drop*math.pi)
        zz=z+sag+hem*drop**3
        vertices.append((xx,yy,zz))
faces=[]
for j in range(len(cape_rings)-1):
    for i in range(steps):
        a=j*(steps+1)+i
        faces.extend([(a,a+steps+2,a+1),(a,a+steps+1,a+steps+2)])
cape=mesh_obj("Cloak",vertices,faces,["Moss","MossLight","MossShade"],cape=True)
for poly in cape.data.polygons:
    poly.material_index=0
cape["surface_only"]=True
cape["cloth_design"]="rounded shoulders, drifting uneven gravity folds, loose asymmetric front and turned hem"

# 前襟是窄幅回折布边，不使用亮色硬边；左右前襟随长度自然松开。
for side,index in (("Left",0),("Right",steps)):
    verts=[]
    for j in range(len(cape_rings)):
        outer=Vector(vertices[j*(steps+1)+index])
        inside=Vector(vertices[j*(steps+1)+(index+1 if index==0 else index-1)])
        direction=(inside-outer).normalized()
        verts.extend((outer+Vector((0,-.003,0)),outer+direction*.008+Vector((0,-.010,0)),outer+direction*.018+Vector((0,-.003,0))))
    fs=[]
    for j in range(len(cape_rings)-1):
        for k in range(2): fs.append((j*3+k,j*3+k+1,(j+1)*3+k+1,(j+1)*3+k))
    trim=mesh_obj("CloakFrontTrim"+side,verts,fs,["Moss","MossShade"],cape=True)
    for p in trim.data.polygons: p.material_index=p.index%2

# 轻薄的下摆向内回折，只增加一条窄布面，保留不规则高低。
vv=[]
for i in range(steps+1):
    edge=Vector(vertices[(len(cape_rings)-1)*(steps+1)+i])
    previous=Vector(vertices[(len(cape_rings)-2)*(steps+1)+i])
    up=(previous-edge).normalized()
    inward=Vector((-edge.x,-edge.y,0)).normalized()
    vv.extend((edge,edge+up*.014+inward*.005))
ff=[(i*2,i*2+1,i*2+3,i*2+2) for i in range(steps)]
mesh_obj("CloakTurnedHem",vv,ff,["MossShade"],cape=True)

# 兜帽前沿略低于木面具上缘，向后抬起形成弧顶，下侧布角保留垂褶。
hood_half=[(0,.46),(.18,.44),(.35,.38),(.47,.27),(.51,.12),(.525,-.14),(.56,-.32),(.50,-.405),(.35,-.455),(.17,-.50),(0,-.54)]
hood_outline=hood_half+[(-x,z) for x,z in reversed(hood_half[1:-1])]
segments=len(hood_outline)
hood_rings=[(-.285,1,1,1.435),(-.15,1.035,.99,1.45),(.045,1,.95,1.46),(.235,.80,.87,1.46),(.39,.44,.75,1.44)]
hood_crown_profile=[(-.335,1.755),(-.285,1.765),(-.15,1.825),(.045,1.870),(.235,1.835),(.39,1.735)]

def crown_height(y):
    if y<=hood_crown_profile[0][0]: return hood_crown_profile[0][1]
    for (ya,za),(yb,zb) in zip(hood_crown_profile,hood_crown_profile[1:]):
        if y<=yb:
            t=(y-ya)/(yb-ya)
            return za+(zb-za)*t
    return hood_crown_profile[-1][1]

def hood_point(index,y,sx,sz,cz):
    x,z=hood_outline[index]
    low=max(0,-z/.54)
    fold=low*(-.024*math.exp(-.5*((abs(x)-.48)/.075)**2)+.020*math.exp(-.5*((abs(x)-.29)/.09)**2))
    xx=x*sx
    zz=cz+z*sz
    if zz>1.35:
        upper=min(1,(zz-1.35)/.52)
        xx*=1-.14*upper
        zz=1.35+(zz-1.35)*(crown_height(y)-1.35)/(cz+.46*sz-1.35)
    zz+=.010*math.sin(index*.8)*low
    return (xx,y-.065*low+fold,zz)

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
    center.y+=.008+.035*math.sin(math.pi*i/middle)**2
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
    p.material_index=0

# 深色来自紧贴兜帽布面的内衬，眼孔后保持空腔，没有占位头球。
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

# 兜帽内沿是一圈向内卷的布边，眼睛只用小色片表示，不放实体头球。
rim_rings=[(-.285,1,1,1.435),(-.315,.985,.985,1.435),(-.305,.952,.95,1.435),(-.265,.930,.93,1.435)]
v=[]
for y,sx,sz,cz in rim_rings:
    for i in range(segments):
        v.append(hood_point(i,y,sx,sz,cz))
f=[]
for j in range(len(rim_rings)-1):
    for i in range(segments):
        ni=(i+1)%segments
        f.append((j*segments+i,(j+1)*segments+i,(j+1)*segments+ni,j*segments+ni))
rim=mesh_obj("HoodDrapedFold",v,f,["Moss","MossShade","HoodInside"],bone="Head")
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
mask["facial_features"]="two small jade eyes and matching forehead clover"
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
for j,(x,top,bottom,width) in enumerate([(-.28,.33,.125,.012),(.185,.39,.215,.009),(.29,.33,.105,.012),(-.12,-.385,-.22,.010)]):
    tri=[(x-width,top),(x+width,top+.025),(x+.003,bottom)]
    vv=[(xx,y,1.405+zz) for y in (-.70,-.439) for xx,zz in tri]
    cutter=mesh_obj("TEMP_Chisel"+str(j),vv,[(0,2,1),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)],["MaskEdge"],bone="Head")
    carve(cutter,"Shallow chisel groove")

# 两只小眼睛使用同一网格、同一碧绿材质，凹在原有的深色眼孔中。
eye_outline=[(-.014,.050),(.014,.050),(.026,.034),(.026,-.034),(.014,-.050),(-.014,-.050),(-.026,-.034),(-.026,.034)]
vv=[]; ff=[]
for sign in (1,-1):
    start=len(vv)
    for y in (-.397,-.389):
        vv.extend((sign*.175+x,y,1.38+z) for x,z in eye_outline)
    ff.append(tuple(start+i for i in range(8)))
    ff.append(tuple(start+8+i for i in reversed(range(8))))
    for i in range(8):
        ni=(i+1)%8
        ff.append((start+i,start+ni,start+8+ni,start+8+i))
eyes=mesh_obj("JadeEyes",vv,ff,["Jade"],bone="Head")
eyes["style"]="two small flat jade eyes recessed in the carved sockets"

# 围巾式领围是绕颈的开放布带，前侧叠成低垂的V形，不填充头部空腔。
scarf_steps=32
scarf_rings=[(.355,.270,1.145,.140),(.405,.315,1.095,.160),(.450,.350,1.035,.180),(.425,.325,.990,.160),(.370,.280,.955,.130)]
vv=[]
for row,(rx,ry,z,drop) in enumerate(scarf_rings):
    for i in range(scarf_steps):
        t=math.tau*i/scarf_steps
        front=max(0,1-abs(math.sin(t))) if math.cos(t)>0 else 0
        pull=.016*math.sin(t+.35)*front
        vv.append((rx*math.sin(t),-.055-ry*math.cos(t)+.009*math.sin(t*2)*front,z-drop*front+pull))
ff=[]
for row in range(len(scarf_rings)-1):
    for i in range(scarf_steps):
        ni=(i+1)%scarf_steps
        ff.append((row*scarf_steps+i,row*scarf_steps+ni,(row+1)*scarf_steps+ni,(row+1)*scarf_steps+i))
scarf=mesh_obj("ScarfCowl",vv,ff,["MossShade","Moss","MossLight"],bone="Chest")
for p in scarf.data.polygons: p.material_index=[0,1,1,0][p.index//scarf_steps]
scarf["surface_only"]=True

# 背面三叶草是一层贴着斗篷的平面布印，不是背包或凸起装备。
bpy.context.view_layer.update()
def decal_polygon(name, polygon):
    points=[Vector((x,z,0)) for x,z in polygon]
    tris=tessellate_polygon([points])
    vertices=[]; faces=[]
    def cross2(a,b,c):
        return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    def clip_edge(subject,a,b,sign):
        result=[]
        if not subject: return result
        for p,q in zip(subject,subject[1:]+subject[:1]):
            dp=sign*cross2(a,b,p); dq=sign*cross2(a,b,q)
            inside_p=dp>=-1e-9; inside_q=dq>=-1e-9
            if inside_p: result.append(p)
            if inside_p!=inside_q:
                t=dp/(dp-dq)
                result.append((p[0]+t*(q[0]-p[0]),p[1]+t*(q[1]-p[1])))
        return result
    # 将图案沿布面三角形精确裁分，避免深褶处悬空或穿插，也减少细分面数。
    for cloth_poly in cape.data.polygons:
        cloth_vertices=[cape.data.vertices[i] for i in cloth_poly.vertices]
        a,b,c=[v.co for v in cloth_vertices]
        normal=(b-a).cross(c-a)
        if normal.y<=1e-7: continue
        triangle=[(p.x,p.z) for p in (a,b,c)]
        orientation=cross2(*triangle)
        if abs(orientation)<1e-10: continue
        sign=1 if orientation>0 else -1
        for tri in tris:
            subject=[(points[item].x,points[item].y) if isinstance(item,int) else (item.x,item.y) for item in tri]
            for p,q in zip(triangle,triangle[1:]+triangle[:1]): subject=clip_edge(subject,p,q,sign)
            if len(subject)<3: continue
            clean=[]
            for p in subject:
                if not clean or (Vector(p)-Vector(clean[-1])).length>1e-7: clean.append(p)
            if len(clean)>1 and (Vector(clean[0])-Vector(clean[-1])).length<1e-7: clean.pop()
            if len(clean)<3: continue
            start=len(vertices)
            for x,z in clean:
                y=a.y-(normal.x*(x-a.x)+normal.z*(z-a.z))/normal.y
                vertices.append((x,y+.003,z))
            faces.append(tuple(start+i for i in range(len(clean))))
    obj=mesh_obj(name,vertices,faces,["Clover"],cape=True)
    return obj

heart=[(0,0),(-.043,.033),(-.077,.085),(-.081,.13),(-.052,.157),(-.021,.155),(0,.127),(.021,.155),(.052,.157),(.081,.13),(.077,.085),(.043,.033)]
for index,angle in enumerate((0,2*math.pi/3,-2*math.pi/3)):
    poly=[]
    for x,z in heart:
        poly.append((x*math.cos(angle)-z*math.sin(angle),.79+x*math.sin(angle)+z*math.cos(angle)))
    decal_polygon("CloverLeaf"+str(index+1),poly)
decal_polygon("CloverStem",[(-.008,.79),(.009,.79),(.015,.709),(.038,.65),(.02,.641),(-.002,.703)])

# 额头三叶草与眼睛共享碧绿材质，贴着木板表面，保留简易三心叶与短茎。
mask_bvh=BVHTree.FromPolygons([vertex.co for vertex in mask.data.vertices],[list(poly.vertices) for poly in mask.data.polygons],all_triangles=False)
front_vertices=[]; front_faces=[]
def forehead_polygon(polygon):
    points=[Vector((x,z,0)) for x,z in polygon]
    for tri in tessellate_polygon([points]):
        start=len(front_vertices)
        for item in tri:
            p=points[item] if isinstance(item,int) else item
            hit=mask_bvh.ray_cast(Vector((p.x,-2,p.y)),Vector((0,1,0)),3)
            if hit[0] is None: raise ValueError("额头图案超出木面具表面")
            front_vertices.append((p.x,hit[0].y-.004,p.y))
        front_faces.append((start,start+1,start+2))

glyph_scale=.58
glyph_z=1.645
for angle in (0,2*math.pi/3,-2*math.pi/3):
    forehead_polygon([(glyph_scale*(x*math.cos(angle)-z*math.sin(angle)),glyph_z+glyph_scale*(x*math.sin(angle)+z*math.cos(angle))) for x,z in heart])
forehead_polygon([(x*glyph_scale,glyph_z+(z-.79)*glyph_scale) for x,z in [(-.008,.79),(.009,.79),(.015,.709),(.038,.65),(.02,.641),(-.002,.703)]])
glyph=mesh_obj("ForeheadClover",front_vertices,front_faces,["Jade"],bone="Head")
glyph["pattern"]="three-leaf clover matching the small jade eyes"

# 统一法线并固化对象变换，方便编辑、权重与引擎导出。
for obj in objects:
    bpy.context.view_layer.objects.active=obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    bm=bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data); bm.free()
    if obj.name in ("CW_Cloak","CW_ScarfCowl"):
        for p in obj.data.polygons: p.use_smooth=True
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
        "design":{"mask_shape":"angular owl face traced from user outline","mask_surface":"carved wooden board","mask_features":"two small jade eyes and matching forehead clover","jade_color_srgb":"#2bc98e","no_head_filler":True,"hands":False,"hood":"front lip aligned just below the mask, raised rear crown forming a curved side profile","scarf_cowl":True,"cloak_surface_only":True,"cloak_folds":"uneven drifting gravity folds with rounded shoulders and loose asymmetric front","cloak_hem":"irregular turned fabric edge above ankle","cloth_shading":"soft body cloth and scarf; faceted wooden mask","backpack":False,"back_pattern":"three-leaf clover clipped to cloth triangles"},
        "references":["https://oricutstudios.itch.io/low-poly-forest-spirit-nori","https://oricutstudios.itch.io/low-poly-campfire-scout-niko"],
        "stage":"sixth model candidate; arched hood and softer draped cloth with uneven folds and turned edges; animation and game integration pending review"}
bpy.ops.wm.save_as_mainfile(filepath=SOURCE+"clover-wayfarer.blend")
print("CLOVER_MODEL_READY "+json.dumps(report))
