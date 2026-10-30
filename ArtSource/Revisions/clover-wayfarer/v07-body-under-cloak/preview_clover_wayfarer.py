"""在Blender中用同一份真实模型渲染正背面对照，不生成或绘制二维替代图。"""
import bpy
import math
from mathutils import Vector

base=bpy.data.scenes['CloverWayfarer']
previous=bpy.data.scenes.get('CloverWayfarer_Preview')
if previous:
    bpy.context.window.scene=base
    for obj in list(previous.objects): bpy.data.objects.remove(obj,do_unlink=True)
    bpy.data.scenes.remove(previous)
preview=bpy.data.scenes.new('CloverWayfarer_Preview')
bpy.context.window.scene=preview
preview.world=base.world
for name,x,angle in [('Front',-.82,.24),('Back',.82,math.pi-.28)]:
    obj=bpy.data.objects.new('CWPREVIEW_'+name,None)
    obj.instance_type='COLLECTION'
    obj.instance_collection=bpy.data.collections['CW_Character']
    obj.location.x=x; obj.rotation_euler.z=angle
    preview.collection.objects.link(obj)
    plinth=bpy.data.objects['CW_Plinth'].copy()
    plinth.name='CWPREVIEW_Plinth_'+name
    plinth.location.x=x; plinth.scale=(.82,.82,1)
    preview.collection.objects.link(plinth)
for source in bpy.data.collections['CW_Studio'].objects:
    if source.type=='LIGHT' or source.name=='CW_Ground':
        obj=source.copy(); obj.name='CWPREVIEW_'+source.name
        preview.collection.objects.link(obj)
data=bpy.data.cameras.new('CWPREVIEW_Camera')
camera=bpy.data.objects.new('CWPREVIEW_Camera',data); preview.collection.objects.link(camera)
camera.location=(0,-8,3.1)
camera.rotation_euler=(Vector((0,0,.90))-camera.location).to_track_quat('-Z','Y').to_euler()
data.type='ORTHO'; data.ortho_scale=3.90
preview.camera=camera
preview.render.engine='BLENDER_EEVEE'
preview.render.resolution_x=1800; preview.render.resolution_y=1100; preview.render.resolution_percentage=100
preview.render.image_settings.file_format='PNG'
preview.view_settings.view_transform='AgX'
preview.view_settings.look='AgX - Medium High Contrast'
preview.frame_set(1)
preview.render.filepath='F:/Godot项目/项目a/ArtSource/Exports/clover-wayfarer/front-back-preview.png'
bpy.ops.render.render(write_still=True)
bpy.context.window.scene=base
base.frame_set(1)

# 同一份模型生成单视角与身体结构检查图，不修改最终斗篷可见状态。
camera=base.camera
for filename,position,target,scale in [('front-three-quarter.png',(3,-5.6,2.8),(0,0,.91),2.5),('back-three-quarter.png',(2.8,5.5,2.8),(0,0,.91),2.5),('head-detail.png',(.8,-5,1.9),(0,-.1,1.405),1.34),('head-side-profile.png',(4.5,0,1.43),(0,0,1.43),1.30)]:
    camera.location=position
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale=scale
    base.render.filepath='F:/Godot项目/项目a/ArtSource/Exports/clover-wayfarer/'+filename
    bpy.ops.render.render(write_still=True)
camera.location=(3,-5.6,2.8)
camera.data.ortho_scale=2.5
camera.rotation_euler=(Vector((0,0,.91))-camera.location).to_track_quat('-Z','Y').to_euler()
lower_cloth=[o for o in bpy.data.collections['CW_Character'].objects if o.name in ('CW_Cloak','CW_CloakFrontTrimLeft','CW_CloakFrontTrimRight','CW_CloakTurnedHem','CW_CloverStem') or o.name.startswith('CW_CloverLeaf')]
old_hidden={o:o.hide_render for o in lower_cloth}
for obj in lower_cloth: obj.hide_render=True
base.render.filepath='F:/Godot项目/项目a/ArtSource/Exports/clover-wayfarer/body-under-cloak-preview.png'
bpy.ops.render.render(write_still=True)
for obj,value in old_hidden.items(): obj.hide_render=value

# 正面和正侧面共用相同高度，便于核对帽沿与后帽顶的弧线。
review=bpy.data.scenes.new('CW_HeadReview')
bpy.context.window.scene=review
review.world=base.world
for name,x,rotation in [('Front',-.65,0),('Side',.64,-math.pi/2)]:
    obj=bpy.data.objects.new('CW_REVIEW_'+name,None)
    obj.instance_type='COLLECTION'
    obj.instance_collection=bpy.data.collections['CW_Character']
    obj.location.x=x
    obj.rotation_euler.z=rotation
    review.collection.objects.link(obj)
for obj in bpy.data.collections['CW_Studio'].objects:
    if obj.type=='LIGHT': review.collection.objects.link(obj.copy())
data=bpy.data.cameras.new('CW_REVIEW_Camera')
review_camera=bpy.data.objects.new('CW_REVIEW_Camera',data)
review.collection.objects.link(review_camera)
review_camera.location=(0,-8,1.43)
review_camera.rotation_euler=(Vector((0,0,1.43))-review_camera.location).to_track_quat('-Z','Y').to_euler()
data.type='ORTHO'
data.ortho_scale=2.75
review.camera=review_camera
review.render.engine='BLENDER_EEVEE'
review.render.resolution_x=1800
review.render.resolution_y=1100
review.render.resolution_percentage=100
review.view_settings.view_transform='AgX'
review.view_settings.look='AgX - Medium High Contrast'
review.render.image_settings.file_format='PNG'
review.frame_set(1)
review.render.filepath='F:/Godot项目/项目a/ArtSource/Exports/clover-wayfarer/head-front-side-preview.png'
bpy.ops.render.render(write_still=True)
bpy.context.window.scene=base
for obj in list(review.objects): bpy.data.objects.remove(obj,do_unlink=True)
bpy.data.scenes.remove(review)

for obj in base.objects: obj.select_set(False)
bpy.data.objects['CW_OwlMask'].select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects['CW_OwlMask']
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D': area.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.wm.save_as_mainfile(filepath='F:/Godot项目/项目a/ArtSource/Blender/clover-wayfarer/clover-wayfarer.blend')
print('CLOVER_ALL_PREVIEWS_READY')
