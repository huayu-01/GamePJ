"""回读GLB，核对真实导出的动作、蒙皮、循环接缝与落地高度。"""
import bpy
import json
import math
from pathlib import Path

ROOT=Path('F:/Godot项目/项目a')
OUTPUT=ROOT/'ArtSource/Animations/clover-wayfarer/basic-trial'
manifest=json.loads((OUTPUT/'motion-manifest.json').read_text(encoding='utf-8'))
expected=json.loads((ROOT/'ArtSource/Exports/clover-wayfarer/model-report.json').read_text(encoding='utf-8'))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.context.scene.render.fps=60
bpy.ops.import_scene.gltf(filepath=str(ROOT/'ArtSource/Exports/clover-wayfarer/clover-wayfarer.glb'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers)]
report={'passed':True,'mesh_count':len(meshes),'bones':len(rig.data.bones),
        'triangles':sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons),
        'animations':[],'unbound_vertices':0,'max_weight_sum_error':0}
for obj in meshes:
    for vertex in obj.data.vertices:
        total=sum(g.weight for g in vertex.groups)
        report['unbound_vertices']+=total<.000001
        report['max_weight_sum_error']=max(report['max_weight_sum_error'],abs(1-total))
assert len(bpy.data.actions)==6,[a.name for a in bpy.data.actions]
for clip in manifest['clips']:
    action=next(a for a in bpy.data.actions if a.name==clip['name'])
    rig.animation_data.action=action
    start,end=action.frame_range
    low=100
    matrices=[]
    scene=bpy.context.scene
    for index in range(25):
        frame=start+(end-start)*index/24
        scene.frame_set(math.floor(frame),subframe=frame-math.floor(frame))
        bpy.context.view_layer.update()
        graph=bpy.context.evaluated_depsgraph_get()
        for obj in meshes:
            evaluated=obj.evaluated_get(graph)
            mesh=evaluated.to_mesh()
            low=min(low,min((evaluated.matrix_world@v.co).z for v in mesh.vertices))
            evaluated.to_mesh_clear()
        if index in (0,24):matrices.append([pb.matrix.copy() for pb in rig.pose.bones])
    seam=max(abs(a[i][j]-b[i][j]) for a,b in zip(matrices[0],matrices[1]) for i in range(4) for j in range(4))
    result={'name':clip['name'],'duration_seconds':float((end-start)/60),'sampled_frames':25,
        'minimum_mesh_z':low,'loop_seam_matrix_error':seam if clip['loop'] else None}
    assert low>-.002,result
    if clip['loop']:assert seam<.0001,result
    report['animations'].append(result)
assert report['mesh_count']==expected['meshes'] and report['triangles']==expected['triangles'] and report['bones']==expected['bones'],report
assert report['unbound_vertices']==0 and report['max_weight_sum_error']<.00001,report
(OUTPUT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('CW_VERIFIED '+json.dumps(report),flush=True)
