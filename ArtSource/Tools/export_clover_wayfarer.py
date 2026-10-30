"""只导出三叶旅童角色集合，排除其他场景和摄影棚。"""
import bpy

bpy.context.window.scene=bpy.data.scenes['CloverWayfarer']
bpy.context.scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
for obj in bpy.data.collections['CW_Character'].all_objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects['CW_Rig']
bpy.ops.export_scene.gltf(
    filepath='F:/Godot项目/项目a/ArtSource/Exports/clover-wayfarer/clover-wayfarer.glb',
    export_format='GLB',use_selection=True,use_active_scene=True,
    export_animations=True,export_animation_mode='ACTIONS',export_frame_range=False,
    export_force_sampling=True,export_materials='EXPORT',export_skins=True,
    export_yup=True,export_cameras=False,export_lights=False)
print('CLOVER_ASSET_EXPORT_READY')
