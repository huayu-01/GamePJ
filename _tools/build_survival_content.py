"""首版生存场景与配置制作源；只在显式执行时重写本脚本拥有的新文件。"""
from pathlib import Path
import json, math

ROOT = Path(__file__).resolve().parents[1]
def write(name, text):
    path = ROOT/name
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text.strip()+'\n', encoding='utf-8')
def quote(value): return json.dumps(value, ensure_ascii=False)

phases = [
    ('薄暮 · 拾取与成长',0,2,36,18,.65,.65,0,['chaser','fast_melee']),
    ('余火 · 近远交织',.10,2.6,48,24,.75,.75,0,['chaser','fast_melee','exploder','ranged']),
    ('回廊 · 追踪与盾阵',.28,3.1,64,28,.85,.8,.03,['chaser','exploder','ranged','spear_guard','arc_thrower','burrow_hunter']),
    ('暗涌 · 重击与突围',.50,3.8,84,32,1,.9,.05,['chaser','heavy_slammer','blade_spinner','hover_diver','ranged','bomb_lobber']),
    ('长夜 · 陨落与连斩',.73,4.7,108,38,1.15,1,.08,['fast_melee','meteor_mage','tracking_charger','combo_fighter','heavy_slammer','arc_bomber']),
    ('破晓前 · 最后防线',.90,5.5,132,42,1.3,1.1,.10,['chaser','fast_melee','exploder','tracking_charger','meteor_mage','heavy_slammer']),
]
enemies=sorted({e for p in phases for e in p[-1]}|{'blade_spinner'})
parts=['[gd_resource type="Resource" script_class="SurvivalDefinition" format=3]',
       '[ext_resource type="Script" path="res://Scripts/Config/SurvivalDefinition.cs" id="definition"]',
       '[ext_resource type="Script" path="res://Scripts/Config/SurvivalPhase.cs" id="phase"]',
       '[ext_resource type="Resource" path="res://Config/Tables/Bosses/boss_final.tres" id="boss"]']
assert (ROOT/'Config/Tables/Bosses/boss_final.tres').exists()
for e in enemies: parts.append(f'[ext_resource type="Resource" path="res://Config/Tables/Enemies/enemy_{e}.tres" id="{e}"]')
for i,(title,start,budget,pressure,alive,hp,dmg,elite,pool) in enumerate(phases):
    parts += [f'\n[sub_resource type="Resource" id="phase_{i}"]','script = ExtResource("phase")',
              'Title = '+quote(title),f'StartFraction = {start}',f'BudgetPerSecond = {budget}',f'MaxAlivePressure = {pressure}',f'MaxAlive = {alive}',f'HealthScale = {hp}',f'DamageScale = {dmg}',f'EliteChance = {elite}',
              'Enemies = ['+', '.join(f'ExtResource("{e}")' for e in pool)+']']
parts += ['\n[resource]','script = ExtResource("definition")','Id = "survival_twilight"','Title = "暮光回廊 · 死战"','DurationSeconds = 720.0','FinalBoss = ExtResource("boss")','MilestoneEnemy = ExtResource("blade_spinner")',
          'Phases = ['+', '.join(f'SubResource("phase_{i}")' for i in range(len(phases)))+']']
write('Config/Modes/twilight_survival.tres','\n'.join(parts))
write('Config/Modes/survival_destination.tres','''
[gd_resource type="Resource" script_class="ExpeditionDestination" format=3]
[ext_resource type="Script" path="res://Scripts/Config/ExpeditionDestination.cs" id="script"]
[ext_resource type="Resource" path="res://Config/Modes/twilight_survival.tres" id="settings"]
[resource]
script = ExtResource("script")
Title = "暮光回廊 · 死战"
ScenePath = "res://Scenes/Survival.tscn"
ModeSettings = ExtResource("settings")
''')

# 场景静态布局可直接在Godot编辑；装饰使用已存在的自然资源预制。
scene=['[gd_scene format=3]']
prefabs=['CommonTree_3','Pine_2','Pine_5','Rock_Medium_1','Bush_Common']
for name in prefabs: scene.append(f'[ext_resource type="PackedScene" path="res://Scenes/Levels/NatureArena/Prefabs/{name}.tscn" id="{name}"]')
scene.append('[ext_resource type="Font" path="res://Assets/UI/Themes/ui_font.tres" id="font"]')
scene.append('[ext_resource type="Script" path="res://Scripts/Run/Survival/SurvivalScenery.cs" id="scenery"]')
colors={'ground':(.13,.24,.21),'stone':(.28,.34,.38),'trim':(.48,.54,.51),'path':(.32,.37,.34),'cyan':(.12,.82,.72),'amber':(1,.58,.17),'water':(.08,.35,.40)}
for name,rgb in colors.items():
    scene += [f'\n[sub_resource type="StandardMaterial3D" id="{name}"]',f'albedo_color = Color({rgb[0]}, {rgb[1]}, {rgb[2]}, 1)','roughness = 0.9']
    if name in ['cyan','amber','water']: scene += ['emission_enabled = true',f'emission = Color({rgb[0]}, {rgb[1]}, {rgb[2]}, 1)','emission_energy_multiplier = 1.3']
scene += ['''
[sub_resource type="Environment" id="environment"]
background_mode = 1
background_color = Color(0.045, 0.075, 0.11, 1)
ambient_light_source = 2
ambient_light_color = Color(0.58, 0.73, 0.82, 1)
ambient_light_energy = 0.8
tonemap_mode = 2
glow_enabled = true
fog_enabled = true
fog_light_color = Color(0.13, 0.24, 0.27, 1)
fog_density = 0.0025

[node name="SurvivalArena" type="Node3D" groups=["nav_geometry"]]
script = ExtResource("scenery")
[node name="WorldEnvironment" type="WorldEnvironment" parent="."]
environment = SubResource("environment")
[node name="Moonlight" type="DirectionalLight3D" parent="."]
rotation_degrees = Vector3(-52, -27, 0)
light_color = Color(0.78, 0.88, 1, 1)
light_energy = 1.15
shadow_enabled = true
''']
# 资源必须先声明，因此网格和形状先收集，最后再拼接节点。
resources=[]; nodes=[]; counter=0
def box(name,pos,size,material='stone',solid=False):
    global counter
    counter+=1; sid=f'box_{counter}'
    resources.extend([f'[sub_resource type="BoxMesh" id="{sid}"]',f'size = Vector3({size[0]}, {size[1]}, {size[2]})',f'material = SubResource("{material}")'])
    if solid:
        resources.extend([f'[sub_resource type="BoxShape3D" id="shape_{sid}"]',f'size = Vector3({size[0]}, {size[1]}, {size[2]})'])
        nodes.extend([f'[node name="{name}" type="StaticBody3D" parent="."]',f'position = Vector3({pos[0]}, {pos[1]}, {pos[2]})',f'[node name="Mesh" type="MeshInstance3D" parent="{name}"]',f'mesh = SubResource("{sid}")',f'[node name="Shape" type="CollisionShape3D" parent="{name}"]',f'shape = SubResource("shape_{sid}")'])
    else: nodes.extend([f'[node name="{name}" type="MeshInstance3D" parent="."]',f'position = Vector3({pos[0]}, {pos[1]}, {pos[2]})',f'mesh = SubResource("{sid}")'])
box('Ground',(0,-.5,0),(104,1,104),'ground',True)
for i,(pos,size) in enumerate([((0,2,-52),(104,4,1)),((0,2,52),(104,4,1)),((-52,2,0),(1,4,104)),((52,2,0),(1,4,104))]): box(f'Boundary{i}',pos,size,'stone',True)
for i in range(-10,11):
    for axis in [0,1]: box(f'CrossPath{axis}_{i+10}',(i*4.5 if axis==0 else 0,.015,0 if axis==0 else i*4.5),(4.35,.03,10) if axis==0 else (10,.03,4.35),'path')
for i in range(40):
    a=i*math.tau/40;x,z=38*math.cos(a),38*math.sin(a)
    box(f'OuterPath{i}',(round(x,3),.035,round(z,3)),(6,.045,6),'path')
# 四组遗迹岛让玩家换线；主干道、信标和出生区留白。
for i,(x,z) in enumerate([(-16,-13),(16,-13),(-17,14),(17,15)]):
    box(f'RuinWall{i}',(x,.9,z),(6,1.8,2.2),'stone',True)
    box(f'RuinCap{i}',(x,1.86,z),(6.4,.16,2.6),'trim')
    for side in [-1,1]: box(f'Pillar{i}_{side}',(x+side*4.3,1.5,z),(1.2,3,1.2),'stone',True)
# 月井可绕行，中央水面和碎石台座。
box('MoonWellBase',(0,.28,0),(5,.56,5),'stone',True)
box('MoonWellWater',(0,.58,0),(4.2,.04,4.2),'water')
for i in [-1,1]:
    box(f'WellEdgeX{i}',(i*2.45,.7,0),(.22,.35,5),'trim')
    box(f'WellEdgeZ{i}',(0,.7,i*2.45),(5,.35,.22),'trim')
for i,x in enumerate([-9,9]):
    box(f'NorthArchPillar{i}',(x,2.6,-33),(1.6,5.2,1.6),'stone',True)
    box(f'NorthArchTop{i}',(x*.65,5.4,-33),(7,.9,2),'trim')
for i in range(7):
    box(f'EastCrystal{i}',(31+(i%3-1)*1.6,1+(i%3)*.6,-2+(i//3)*1.4),(.75,2+(i%3)*1.2,.75),'cyan')
box('ForgeBase',(-32,.4,-1),(6,.8,6),'stone',True)
for i in range(3): box(f'ForgeEmber{i}',(-34+i*2,1.5,-1),(1.1,2.1,1.1),'amber')
for i in range(56):
    a=i*math.tau/56; radius=47.5+(i%3)*1.1; name=prefabs[i%3]
    nodes.extend([f'[node name="OuterTree{i}" parent="." instance=ExtResource("{name}")]',f'position = Vector3({radius*math.cos(a):.3f}, 0, {radius*math.sin(a):.3f})',f'rotation_degrees = Vector3(0, {i*47%360}, 0)',f'scale = Vector3({2.4+i%3*.2:.2f}, {2.4+i%3*.2:.2f}, {2.4+i%3*.2:.2f})'])
for i in range(20):
    a=i*math.tau/20;radius=44
    nodes.extend([f'[node name="EdgeRock{i}" parent="." instance=ExtResource("Rock_Medium_1")]',f'position = Vector3({radius*math.cos(a):.3f}, 0, {radius*math.sin(a):.3f})','scale = Vector3(1.8, 1.6, 1.8)'])
for name,pos,text in [('Entry',(0,3,34),'南门 · 死战'),('North',(0,6.5,-33),'北 · 破晓拱门'),('East',(32,4,1),'东 · 青辉晶庭'),('West',(-32,4,1),'西 · 余烬锻炉')]:
    nodes.extend([f'[node name="Landmark{name}" type="Label3D" parent="."]',f'position = Vector3({pos[0]}, {pos[1]}, {pos[2]})',f'text = {quote(text)}','font = ExtResource("font")','font_size = 48','pixel_size = 0.004','billboard = 1','outline_size = 6'])
first_node=next(i for i,line in enumerate(scene) if '[node ' in line)
# environment块和节点在同一多行片段里，按首个[node切开。
joined='\n'.join(scene); split=joined.index('[node ')
write('Scenes/Levels/SurvivalArena.tscn',joined[:split]+'\n'+'\n'.join(resources)+'\n'+joined[split:]+'\n'+'\n'.join(nodes))

ext=[('PackedScene','Scenes/Levels/SurvivalArena.tscn','arena'),('PackedScene','Scenes/Player/Player.tscn','player'),('Script','Scripts/World/MapManager.cs','map'),('Script','Scripts/Run/Survival/SurvivalDirector.cs','director'),('Script','Scripts/Rewards/RunController.cs','run'),('Script','Scripts/Visual/ToonPass.cs','toon')]
screens=['Hud','WorldOverlay','SettingsScreen','BuildScreen','DeathScreen','AbilityMatrixScreen']
for s in screens: ext.append(('PackedScene',f'Scenes/UI/{s}.tscn',s))
for s in ['RewardScreen','ChooseScreen','StartScreen']:ext.append(('PackedScene',f'Scenes/UI/{s}.tscn',s))
ext.append(('PackedScene','Scenes/Debug/GmConsole.tscn','gm'))
out=['[gd_scene format=3]']+[f'[ext_resource type="{t}" path="res://{p}" id="{i}"]' for t,p,i in ext]
out+=['[node name="Survival" type="Node3D"]','[node name="Arena" parent="." instance=ExtResource("arena")]','[node name="MapManager" type="Node3D" parent="."]','script = ExtResource("map")','[node name="Player" parent="." instance=ExtResource("player")]','position = Vector3(0, 0.1, 38)','[node name="Director" type="Node3D" parent="."]','script = ExtResource("director")','[node name="RunController" type="Node" parent="."]','script = ExtResource("run")','ModeControllerPath = NodePath("../Director")']
for s in ['RewardScreen','ChooseScreen','StartScreen']:out.append(f'[node name="{s}" parent="RunController" instance=ExtResource("{s}")]')
for s in screens:out.append(f'[node name="{s}" parent="." instance=ExtResource("{s}")]')
out+=['[node name="GmConsole" parent="." instance=ExtResource("gm")]','[node name="ToonPass" type="Node" parent="."]','script = ExtResource("toon")']
write('Scenes/Survival.tscn','\n'.join(out))
print('Wrote survival settings, destination, arena and mode scene.')
