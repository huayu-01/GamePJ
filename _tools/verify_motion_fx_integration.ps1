param([switch]$SkipBuild, [int]$LockWaitSeconds = 120)
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'LogPaths.ps1')
. (Join-Path $PSScriptRoot 'Validation.Common.ps1')
Initialize-ProjectLogPaths -ProjectRoot $projectDir
$engine = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profile = Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$tag = Get-Date -Format 'yyyyMMdd_HHmmss'
$lease = $null
$snapshot = $null
try {
    $lease = Open-ProjectValidationLease $projectDir $LockWaitSeconds
    $snapshot = Get-ValidationSaveSnapshot $profile
    if (-not $SkipBuild) {
        Invoke-ValidationBuild $projectDir 'Debug' (Join-Path $projectDir "Logs/validation/motion_integration_$tag.build.log") | Out-Null
        Invoke-ValidationBuild $projectDir 'ExportRelease' (Join-Path $projectDir "Logs/validation/motion_integration_$tag.release.log") | Out-Null
    }
    $stdout = Join-Path $projectDir "Logs/validation/motion_integration_$tag.log"
    $stderr = Join-Path $projectDir "Logs/validation/motion_integration_$tag.err"
    $runArgs = @('--path', $projectDir, '--resolution', '1280x720', '--log-file', (Join-Path $projectDir "Logs/validation/motion_integration_$tag.engine.log"), 'res://Scenes/Debug/_MotionFxIntegrationProbe.tscn')
    $result = Invoke-ValidationEngine $engine $runArgs $projectDir $stdout $stderr 90
    if ($result.Output -notmatch '\[MotionFxIntegration\] SUMMARY pass=\d+ fail=0') { throw "组件接入专项未通过：$stdout" }
    Assert-ValidationSaves $profile $snapshot
    Write-Output "PASS motion-fx integration $stdout"
}
finally {
    try { if ($null -ne $snapshot) { Assert-ValidationSaves $profile $snapshot } }
    finally { if ($lease) { $lease.Dispose() } }
}
