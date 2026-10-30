"""为现有木面具增加浅球面脸罩弧度，保留轮廓、绑定与动作。"""
import bpy
import bmesh
import math
import json

ROOT='F:/Godot项目/项目a/'
RADIUS=.95
CENTER_Z=1.30
VERTICAL_SCALE=.80
CENTER_PUSH=.09
MAX_SAG=.18
scene=bpy.data.scenes['CloverWayfarer']
bpy.context.window.scene=scene
rig=bpy.data.objects['CW_Rig']


def displacement(x,z):
    radius_squared=x*x+(VERTICAL_SCALE*(z-CENTER_Z))**2
    sag=RADIUS-math.sqrt(max(.001,RADIUS*RADIUS-radius_squared))
    return -CENTER_PUSH+min(MAX_SAG,sag)


names=['CW_OwlMask','CW_JadeEyes','CW_ForeheadClover']
for name in names:
    obj=bpy.data.objects[name]
    if not obj.data.attributes.get('cw_flat_mask_base'):
        if name=='CW_OwlMask':
            # 只细分跨度大的面，给球面弯曲足够的控制点，避免全身细分。
            bm=bmesh.new();bm.from_mesh(obj.data)
            bmesh.ops.triangulate(bm,faces=list(bm.faces))
            for iteration in range(2):
                edges=[e for e in bm.edges if e.calc_length()>.20]
                bmesh.ops.subdivide_edges(bm,edges=edges,cuts=1,use_grid_fill=True)
                bmesh.ops.triangulate(bm,faces=list(bm.faces))
            bm.to_mesh(obj.data);bm.free()
        base=obj.data.attributes.new('cw_flat_mask_base','FLOAT_VECTOR','POINT')
        for item,vertex in zip(base.data,obj.data.vertices):item.vector=vertex.co
    base=obj.data.attributes['cw_flat_mask_base']
    for item,vertex in zip(base.data,obj.data.vertices):
        vertex.co=item.vector
        vertex.co.y+=displacement(vertex.co.x,vertex.co.z)
    # 三个部件共用连续弧面，图案与眼睛不会留在旧平面里。
    head=obj.vertex_groups.get('Head')
    head.add(list(range(len(obj.data.vertices))),1,'REPLACE')
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data);bm.free()
    obj.data.update()
    obj['mask_curve_revision']='v10-shallow-spherical-mask'

mask=bpy.data.objects['CW_OwlMask']
mask['surface']='shallow spherical carved wooden face shell with bevel and sparse chisel cuts'
scene['cw_mask_curve']=json.dumps({'revision':'v10-shallow-spherical-mask','sphere_radius_m':RADIUS,
    'center_z_m':CENTER_Z,'vertical_scale':VERTICAL_SCALE,'center_push_m':CENTER_PUSH,
    'max_sag_m':MAX_SAG,'outline_xz_preserved':True,'preserved_animations':[a.name for a in bpy.data.actions]})
scene['cw_geometry_revision']='v10-shallow-spherical-mask'
scene.frame_set(1)
for obj in scene.objects:obj.select_set(False)
mask.select_set(True);bpy.context.view_layer.objects.active=mask
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'ArtSource/Blender/clover-wayfarer/clover-wayfarer.blend')
print('CW_MASK_CURVED '+scene['cw_mask_curve'])
