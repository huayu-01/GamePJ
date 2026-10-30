param([switch]$SkipBuild,[switch]$PrepareOnly,[int]$LockWaitSeconds=120)
$ErrorActionPreference='Stop'
$projectDir=Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'LogPaths.ps1')
. (Join-Path $PSScriptRoot 'Validation.Common.ps1')
Initialize-ProjectLogPaths -ProjectRoot $projectDir
$engine='F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profile=Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$tag=Get-Date -Format 'yyyyMMdd_HHmmss'
$lease=$null;$snapshot=$null
try {
    $lease=Open-ProjectValidationLease $projectDir $LockWaitSeconds
    $snapshot=Get-ValidationSaveSnapshot $profile
    if(-not $SkipBuild) {
        Invoke-ValidationBuild $projectDir 'Debug' (Join-Path $projectDir "Logs/validation/clover_$tag.build.log") | Out-Null
        Invoke-ValidationBuild $projectDir 'ExportRelease' (Join-Path $projectDir "Logs/validation/clover_$tag.release.log") | Out-Null
    }
    $mirror=New-ValidationMirror $projectDir 'clover' -ShareAssembly
    $mirrorProject=Join-Path $mirror.Project 'project.godot'
    $mirrorSettings=[IO.File]::ReadAllText($mirrorProject)
    $mirrorSettings=[regex]::Replace($mirrorSettings,'(?ms)(\[editor_plugins\]\s*)enabled\s*=\s*PackedStringArray\([^\r\n]*\)','$1enabled=PackedStringArray()')
    [IO.File]::WriteAllText($mirrorProject,$mirrorSettings,[Text.UTF8Encoding]::new($false))
    Copy-Item -LiteralPath (Join-Path $projectDir '_tools/build_clover_skills.gd') -Destination (Join-Path $mirror.Project '_tools/build_clover_skills.gd')
    [IO.File]::AppendAllText((Join-Path $mirror.Project 'override.cfg'),"`n[editor_plugins]`nenabled=PackedStringArray()`n",[Text.UTF8Encoding]::new($false))
    Invoke-ValidationEngine $engine @('--headless','--path',$mirror.Project,'--editor','--import') $projectDir (Join-Path $projectDir "Logs/validation/clover_$tag.import.log") (Join-Path $projectDir "Logs/validation/clover_$tag.import.err") 120 | Out-Null
    $build=Invoke-ValidationEngine $engine @('--headless','--path',$mirror.Project,'--script','res://_tools/build_clover_skills.gd') $projectDir (Join-Path $projectDir "Logs/validation/clover_$tag.skills.log") (Join-Path $projectDir "Logs/validation/clover_$tag.skills.err") 60
    if($build.Output -notmatch '\[CloverBuild\] PASS') { throw '旅童技能适配资源生成未通过' }
    if(-not $PrepareOnly) {
        $result=Invoke-ValidationEngine $engine @('--path',$mirror.Project,'--resolution','1280x720','--log-file',(Join-Path $projectDir "Logs/validation/clover_$tag.engine.log"),'res://Scenes/Debug/CloverCombatLab.tscn','--','--clover-verify') $projectDir (Join-Path $projectDir "Logs/validation/clover_$tag.probe.log") (Join-Path $projectDir "Logs/validation/clover_$tag.probe.err") 120
        if($result.Output -notmatch '\[CloverCombat\] SUMMARY pass=\d+ fail=0') { throw '旅童战斗试装专项未通过' }
    }
    Assert-ValidationSaves $profile $snapshot
    Write-Output "PASS clover-combat $tag"
} finally {
    try { if($null -ne $snapshot) { Assert-ValidationSaves $profile $snapshot } }
    finally { if($lease) {$lease.Dispose()} }
}
