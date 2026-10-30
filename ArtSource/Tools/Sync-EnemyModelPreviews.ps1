# 使用当前编辑器的 MCP 导入两个模型，期间持有工作区独占验证锁。
$ErrorActionPreference = 'Stop'
$projectDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$lease = [IO.File]::Open((Join-Path $projectDir '.ai_validation.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
try {
    $scanCode = @'
var files = EditorInterface.get_resource_filesystem()
files.scan()
_custom_print("asset scan requested")
'@
    $endpoint = 'http://127.0.0.1:9081/cli/v1/tools/execute_editor_script/execute'
    $payload = @{ arguments = @{ code = $scanCode }; apply_confirmed = $true; allow_open_world = $true } | ConvertTo-Json -Depth 6
    Invoke-RestMethod -Uri $endpoint -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($payload)) -TimeoutSec 55 | ConvertTo-Json -Depth 8
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        if ((Test-Path -LiteralPath (Join-Path $projectDir 'Assets/Enemies/GhostMage/ghost_mage.glb.import')) -and (Test-Path -LiteralPath (Join-Path $projectDir 'Assets/Enemies/ScaredyShroom/scaredy_shroom.glb.import'))) { break }
        Start-Sleep -Milliseconds 500
    }
    foreach ($relative in @('Assets/Enemies/GhostMage/ghost_mage.glb.import','Assets/Enemies/ScaredyShroom/scaredy_shroom.glb.import')) {
        $path = Join-Path $projectDir $relative
        $content = [IO.File]::ReadAllText($path)
        $content = $content.Replace('animation/fps=30','animation/fps=60')
        [IO.File]::WriteAllText($path,$content,[Text.UTF8Encoding]::new($false))
    }
    $importCode = @'
var paths = PackedStringArray(["res://Assets/Enemies/GhostMage/ghost_mage.glb","res://Assets/Enemies/ScaredyShroom/scaredy_shroom.glb"])
EditorInterface.get_resource_filesystem().reimport_files(paths)
var report = {}
for path in paths:
	var model = ResourceLoader.load(path,"PackedScene",ResourceLoader.CACHE_MODE_IGNORE_DEEP).instantiate()
	var player = model.find_children("*","AnimationPlayer",true,false)[0] as AnimationPlayer
	var skeleton = model.find_children("*","Skeleton3D",true,false)[0] as Skeleton3D
	report[path] = {"bones":skeleton.get_bone_count(),"animations":player.get_animation_list()}
	model.free()
for path in ["res://Scripts/Testing/EnemyVisualPreview.gd","res://Scripts/Testing/EnemyModelShowcase.gd"]:
	var script = ResourceLoader.load(path,"GDScript",ResourceLoader.CACHE_MODE_IGNORE)
	report[path] = {"resource_loaded":script != null}
_custom_print(JSON.stringify(report))
'@
    $payload = @{ arguments = @{ code = $importCode }; apply_confirmed = $true; allow_open_world = $true } | ConvertTo-Json -Depth 6
    Invoke-RestMethod -Uri $endpoint -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($payload)) -TimeoutSec 55 | ConvertTo-Json -Depth 8
}
finally {
    $lease.Dispose()
}
