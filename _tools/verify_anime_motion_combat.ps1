param([switch]$SkipBuild, [switch]$CaptureOnly, [switch]$Movie, [int]$LockWaitSeconds = 120)
# 实际渲染性能与图片录制分开；所有运行共用验证锁并核对真实存档。
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'LogPaths.ps1')
. (Join-Path $PSScriptRoot 'Validation.Common.ps1')
Initialize-ProjectLogPaths -ProjectRoot $projectDir
$engine = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profile = Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$tag = Get-Date -Format 'yyyyMMdd_HHmmss'
$relative = "Logs/evidence/anime-motion-combat/run-$tag/"
$destination = Join-Path $projectDir $relative
$snapshot = $null
$lease = $null
try {
    $lease = Open-ProjectValidationLease $projectDir $LockWaitSeconds
    $snapshot = Get-ValidationSaveSnapshot $profile
    [IO.Directory]::CreateDirectory($destination) | Out-Null
    if (-not $SkipBuild) {
        Invoke-ValidationBuild $projectDir 'Debug' (Join-Path $projectDir "Logs/validation/motion_combat_$tag.build.log") | Out-Null
        Invoke-ValidationBuild $projectDir 'ExportRelease' (Join-Path $projectDir "Logs/validation/motion_combat_$tag.release.log") | Out-Null
    }
    $mode = if ($CaptureOnly) { 'capture' } else { 'benchmark' }
    $runArgs = @('--path', $projectDir, '--resolution', '1920x1080', '--disable-vsync', '--log-file', (Join-Path $projectDir "Logs/validation/motion_combat_$tag.engine.log"))
    if ($Movie -and $CaptureOnly) { $runArgs += @('--write-movie', (Join-Path $destination 'combat.avi'), '--fixed-fps', '30') }
    if ($Movie -and -not $CaptureOnly) { throw '录屏只能用于CaptureOnly，不能污染性能数据。' }
    $runArgs += @('res://Scenes/Debug/AnimeMotionCombatLab.tscn', '--', "--motion-combat-$mode", "--motion-combat-output=res://$relative")
    $stdout = Join-Path $projectDir "Logs/validation/motion_combat_$tag.$mode.log"
    $stderr = Join-Path $projectDir "Logs/validation/motion_combat_$tag.$mode.err"
    $result = Invoke-ValidationEngine $engine $runArgs $projectDir $stdout $stderr 240
    $expected = if ($CaptureOnly) { '\[MotionCombat\] CAPTURE PASS' } else { '\[MotionCombat\] BENCHMARK PASS' }
    if ($result.Output -notmatch $expected) { throw "战斗测试未通过：$stdout" }
    Assert-ValidationSaves $profile $snapshot
    [IO.File]::WriteAllText((Join-Path $destination 'validation.log'), $result.Output, [Text.UTF8Encoding]::new($false))
    Write-Output "PASS motion-combat $mode $destination"
}
finally {
    try { if ($null -ne $snapshot) { Assert-ValidationSaves $profile $snapshot } }
    finally { if ($lease) { $lease.Dispose() } }
}
