import bpy, math, random, json
from mathutils import Matrix, Vector
scene=bpy.data.scenes.new('NatureArena_120m')
bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'
rng=random.Random(20261020)
library=bpy.data.collections.new('AssetLibrary'); scene.collection.children.link(library)
sectors={}
for name in ['NW','NE','SW','SE']:
    group=bpy.data.collections.new('Sector_'+name); scene.collection.children.link(group); sectors[name]=group
cache={}
placements=[]
def load_asset(asset):
    if asset in cache:
        return cache[asset]
    before=set(scene.objects)
    bpy.ops.import_scene.gltf(filepath='F:/Godot项目/项目a/Assets/Environment/StylizedNature/Models/'+asset+'.gltf')
    imported=[o for o in scene.objects if o not in before]
    for obj in imported:
        for group in list(obj.users_collection): group.objects.unlink(obj)
        library.objects.link(obj)
        obj.hide_set(True); obj.hide_render=True
    cache[asset]=imported
    return imported
def place(asset,x,z,scale,kind,yaw=None):
    angle=rng.uniform(-math.pi,math.pi) if yaw is None else yaw
    sector=('N' if z<0 else 'S')+('W' if x<0 else 'E')
    record={'asset':asset,'x':round(x,3),'z':round(z,3),'y':0,'scale':round(scale,3),'yaw':round(angle,4),'kind':kind,'sector':sector}
    placements.append(record)
    for source in load_asset(asset):
        obj=source.copy(); obj.name=kind+'_'+str(len(placements)).zfill(4)+'_'+asset
        sectors[sector].objects.link(obj)
        obj.hide_render=False; obj.hide_set(False)
        obj.matrix_world=Matrix.Translation((x,-z,0))@Matrix.Rotation(-angle,4,'Z')@Matrix.Scale(scale,4)@source.matrix_world
        obj['asset']=asset; obj['layout_index']=len(placements)-1
def reserved(x,z,margin=0):
    if math.hypot(x,z)<20+margin: return True
    if abs(x)<3.4+margin or abs(z)<3.4+margin: return True
    if abs(math.hypot(x,z)-34)<3.5+margin: return True
    return any(math.hypot(x-cx,z-cz)<8+margin for cx,cz in [(-34,0),(34,0),(0,-34),(0,34)])
for gx in range(-52,53,7):
    for gz in range(-52,53,7):
        x=gx+rng.uniform(-1.8,1.8); z=gz+rng.uniform(-1.8,1.8)
        if not reserved(x,z,1.5) and rng.random()<0.92:
            family='Pine' if z<-18 else ('TwistedTree' if x>25 else 'CommonTree')
            place(family+'_'+str(rng.randint(1,5)),x,z,rng.uniform(0.85,1.25),'tree')
for x,z in [(-13,-13),(13,-13),(-13,13),(13,13)]:
    place('Rock_Medium_'+str(rng.randint(1,3)),x,z,1.1,'rock')
for i in range(36):
    x=rng.uniform(-53,53); z=rng.uniform(-53,53)
    if not reserved(x,z,2): place('Rock_Medium_'+str(rng.randint(1,3)),x,z,rng.uniform(0.65,1.6),'rock')
for i in range(100):
    x=rng.uniform(-55,55); z=rng.uniform(-55,55)
    if not reserved(x,z,0.4): place('Bush_Common_Flowers' if i%4==0 else 'Bush_Common',x,z,rng.uniform(0.65,1.25),'bush')
for i in range(1050):
    x=rng.uniform(-55,55); z=rng.uniform(-55,55)
    if not reserved(x,z,-1.2):
        assets=['Grass_Common_Short','Grass_Wispy_Short','Clover_1','Fern_1','Flower_3_Group','Flower_4_Group']
        asset=assets[rng.randrange(len(assets))]
        place(asset,x,z,rng.uniform(0.35,0.7),'foliage')
# 宽阔环路只放少量扁平踏石，碰撞由平整地面负责。
for angle in [0,math.pi/2,math.pi,math.pi*1.5]:
    for offset in range(-2,3):
        x=math.cos(angle)*(34+offset*1.4); z=math.sin(angle)*(34+offset*1.4)
        place('RockPath_Round_Small_'+str(rng.randint(1,3)),x,z,0.85,'path')
library.hide_render=True
bpy.ops.mesh.primitive_plane_add(size=120)
ground=bpy.context.object; ground.name='Ground_120x120'
material=bpy.data.materials.new('ArenaGrassReference'); material.use_nodes=True
shader=next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
shader.inputs['Base Color'].default_value=(0.3,0.46,0.16,1); shader.inputs['Roughness'].default_value=1
ground.data.materials.append(material)
sun_data=bpy.data.lights.new('ArenaSun','SUN'); sun_data.energy=2
sun=bpy.data.objects.new('ArenaSun',sun_data); scene.collection.objects.link(sun); sun.rotation_euler=(math.radians(25),math.radians(-30),math.radians(-25))
camera_data=bpy.data.cameras.new('ArenaOverview'); camera=bpy.data.objects.new('ArenaOverview',camera_data); scene.collection.objects.link(camera)
camera.location=(87,-92,100); camera.rotation_euler=(Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler()
camera_data.type=next(i.identifier for i in camera_data.bl_rna.properties['type'].enum_items if i.identifier=='ORTHO'); camera_data.ortho_scale=157; scene.camera=camera
scene.render.resolution_x=1200; scene.render.resolution_y=900; scene.render.resolution_percentage=100
bpy.ops.wm.save_as_mainfile(filepath='F:/Godot项目/项目a/ArtSource/Blender/NatureArena/nature-arena-layout.blend',copy=True)
print('LAYOUT_JSON='+json.dumps({'seed':20261020,'size':120,'central_clear_radius':10,'combat_radius':20,'loop_radius':34,'placements':placements},separators=(',',':')))
