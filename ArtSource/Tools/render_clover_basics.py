"""从实际试绑模型渲染正斜面与侧面同步动作。"""
import bpy
import json
import math
import sys
from pathlib import Path
from mathutils import Vector

ROOT=Path('F:/Godot项目/项目a')
OUTPUT=ROOT/'ArtSource/Animations/clover-wayfarer/basic-trial'
base=bpy.data.scenes['CloverWayfarer']
manifest=json.loads(base['cw_motion_manifest'])
manifest['geometry_revision']=base.get('cw_geometry_revision','v08-stocky-child-proportions')
rig=bpy.data.objects['CW_Rig']
scene=bpy.data.scenes.new('CW_BasicMotionReview')
bpy.context.window.scene=scene
scene.world=base.world
actors=[]
for name,x,rotation in [('Front',-.82,.20),('Side',.83,-math.pi/2)]:
    obj=bpy.data.objects.new('CW_MOTION_'+name,None)
    obj.instance_type='COLLECTION'
    obj.instance_collection=bpy.data.collections['CW_Character']
    obj.location.x=x
    obj.rotation_euler.z=rotation
    scene.collection.objects.link(obj)
    actors.append(obj)
for source in bpy.data.collections['CW_Studio'].objects:
    if source.type=='LIGHT':scene.collection.objects.link(source.copy())
mat=bpy.data.materials.new('CW_MotionFloor')
mat.use_nodes=True
shader=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
shader.inputs['Base Color'].default_value=(.11,.145,.135,1)
shader.inputs['Roughness'].default_value=.95
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.002))
bpy.context.object.data.materials.append(mat)
data=bpy.data.cameras.new('CW_MotionCamera')
camera=bpy.data.objects.new('CW_MotionCamera',data)
scene.collection.objects.link(camera)
camera.location=(0,-8,2.7)
camera.rotation_euler=(Vector((0,0,.95))-camera.location).to_track_quat('-Z','Y').to_euler()
data.type='ORTHO';data.ortho_scale=3.85
scene.camera=camera
scene.render.engine='BLENDER_EEVEE'
scene.eevee.taa_render_samples=24
scene.render.resolution_x=768
scene.render.resolution_y=576
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.render.fps=60
scene.view_settings.view_transform='AgX'
scene.view_settings.look='AgX - Medium High Contrast'
inspect='--inspect' in sys.argv
for clip in manifest['clips']:
    count=max(2,round(clip['duration_seconds']*24))
    clip['render_frame_count']=count
    if '--die-only' in sys.argv and clip['name']!='die':continue
    rig.animation_data.action=bpy.data.actions[clip['name']]
    camera.location=(.35,-8,2.7) if clip['name']=='die' else (0,-8,2.7)
    target=Vector((.35,0,.85)) if clip['name']=='die' else Vector((0,0,.95))
    camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
    data.ortho_scale=5.0 if clip['name']=='die' else 3.85
    indices=sorted(set([0,round((count-1)*.25),round((count-1)*.5),round((count-1)*.75),count-1])) if inspect else range(count)
    directory=OUTPUT/('inspect' if inspect else 'frames')/clip['name']
    directory.mkdir(parents=True,exist_ok=True)
    for index in indices:
        t=index/24
        frame=1+t*60
        scene.frame_set(math.floor(frame),subframe=frame-math.floor(frame))
        h=0
        if clip['name']=='jump' and .08<t<.61:
            p=(t-.08)/.53;h=4*.30*p*(1-p)
        for actor in actors:actor.location.z=h
        bpy.context.view_layer.update()
        scene.render.filepath=str(directory/f'{index:04d}.png')
        bpy.ops.render.render(write_still=True)
    clip['render_frame_count']=count
    print('CW_RENDERED '+clip['name'],flush=True)
OUTPUT.mkdir(parents=True,exist_ok=True)
(OUTPUT/'motion-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('CW_RENDER_DONE',flush=True)
