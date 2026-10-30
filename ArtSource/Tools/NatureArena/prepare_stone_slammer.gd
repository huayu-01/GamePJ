@tool
extends RefCounted

const ASSET = "res://Assets/Enemies/StoneSlammer/"
const SCENE = "res://Scenes/Enemies/Art/StoneSlammer.tscn"

func save_resource(resource: Resource, path: String) -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(path.get_base_dir()))
	assert(ResourceSaver.save(resource,path) == OK)
	resource.take_over_path(path)

func build() -> Dictionary:
	var visual = ResourceLoader.load(ASSET+"stone_slammer.glb","PackedScene",ResourceLoader.CACHE_MODE_IGNORE_DEEP).instantiate()
	var player: AnimationPlayer = visual.find_children("*","AnimationPlayer",true,false)[0]
	assert(player.has_animation("slam"))
	var clip = player.get_animation("slam").duplicate(true) as Animation
	assert(abs(clip.length - 0.9) < 0.01,"模型导入缓存尚未更新，请先重新导入 GLB")
	clip.resource_name = "EnemyAction"
	clip.loop_mode = Animation.LOOP_NONE
	var track = clip.add_track(Animation.TYPE_METHOD)
	clip.track_set_path(track,NodePath("."))
	clip.track_insert_key(track,0.55,{"method":&"anim_event","args":["ATK"]})
	# 正式 UnitAnimator 读取方法轨道的位置事件，关闭引擎直接方法回调。
	clip.track_set_enabled(track,false)
	save_resource(clip,ASSET+"Animations/EnemyAction.tres")
	var enemy = load("res://Scenes/Enemies/HeavySlammer.tscn").instantiate()
	var old_model = enemy.get_node("Model")
	enemy.remove_child(old_model)
	old_model.free()
	visual.name = "Model"
	visual.rotation.y = PI
	enemy.add_child(visual)
	visual.owner = enemy
	enemy.move_child(visual,1)
	enemy.name = "StoneSlammer"
	var collision: CollisionShape3D = enemy.get_node("Body")
	var shape = CapsuleShape3D.new()
	shape.height = 2.95
	shape.radius = 0.72
	collision.shape = shape
	collision.position.y = 1.475
	enemy.get_node("Animator").set("SkillLibraryDirectory",ASSET+"Animations/")
	enemy.set_meta("enemy_art_source","stone-slammer-geometric-v1")
	var packed = PackedScene.new()
	assert(packed.pack(enemy) == OK)
	save_resource(packed,SCENE)
	var row = load("res://Config/Tables/Enemies/enemy_heavy_slammer.tres").duplicate(true)
	row.set("Id","enemy_heavy_slammer_art_preview")
	row.set("DisplayName","重击敌人（几何样件）")
	row.set("ScenePath",SCENE)
	row.set("AnimLibraryDir",ASSET+"Animations/")
	row.set("Note","美术样板用；沿用重击敌人机制，使用自制几何模型和真实砸地事件。")
	save_resource(row,"res://Config/Testing/stone_slammer_preview.tres")
	var skeleton: Skeleton3D = visual.find_children("*","Skeleton3D",true,false)[0]
	var report = {"scene":SCENE,"bones":skeleton.get_bone_count(),"animations":player.get_animation_list(),"skill_length":clip.length,"impact_time":0.55,"collision_height":shape.height,"collision_radius":shape.radius}
	enemy.free()
	return report
