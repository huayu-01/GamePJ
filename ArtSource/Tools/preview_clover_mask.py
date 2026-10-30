"""同一实际角色的正面、侧面、三分之四面具检查图。"""
import bpy
import math
import sys
from mathutils import Vector

base=bpy.data.scenes['CloverWayfarer']
review=bpy.data.scenes.new('CW_MaskCurvatureReview')
bpy.context.window.scene=review
review.world=base.world
for name,x,angle in [('Front',-1.32,0),('Side',0,-math.pi/2),('Quarter',1.32,.70)]:
    obj=bpy.data.objects.new('CW_MASKREVIEW_'+name,None)
    obj.instance_type='COLLECTION'
    obj.instance_collection=bpy.data.collections['CW_Character']
    obj.location.x=x;obj.rotation_euler.z=angle
    review.collection.objects.link(obj)
for source in bpy.data.collections['CW_Studio'].objects:
    if source.type=='LIGHT':review.collection.objects.link(source.copy())
data=bpy.data.cameras.new('CW_MASKREVIEW_Camera')
camera=bpy.data.objects.new('CW_MASKREVIEW_Camera',data)
review.collection.objects.link(camera)
camera.location=(0,-9,1.33)
camera.rotation_euler=(Vector((0,0,1.33))-camera.location).to_track_quat('-Z','Y').to_euler()
data.type='ORTHO';data.ortho_scale=4.10
review.camera=camera
review.render.engine='BLENDER_EEVEE'
review.eevee.taa_render_samples=64
review.render.resolution_x=1800
review.render.resolution_y=650
review.render.resolution_percentage=100
review.view_settings.view_transform='AgX'
review.view_settings.look='AgX - Medium High Contrast'
review.render.image_settings.file_format='PNG'
review.frame_set(1)
filename='mask-flat-before.png' if '--before' in sys.argv else 'mask-curvature-review.png'
review.render.filepath='F:/Godot项目/项目a/ArtSource/Exports/clover-wayfarer/'+filename
bpy.ops.render.render(write_still=True)
print('CW_MASK_REVIEW_READY')
