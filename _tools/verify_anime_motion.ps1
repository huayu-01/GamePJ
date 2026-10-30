param([switch]$SkipBuild, [switch]$Movie, [int]$LockWaitSeconds = 45)
# 复用当前公共设施；独立场景不创建玩家，也不写玩家存档。
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'LogPaths.ps1')
. (Join-Path $PSScriptRoot 'Validation.Common.ps1')
Initialize-ProjectLogPaths -ProjectRoot $projectDir
$engine = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profile = Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$snapshot = $null
$lease = $null
$tag = Get-Date -Format 'yyyyMMdd_HHmmss'
$evidence = [Collections.Generic.List[string]]::new()
try {
    $lease = Open-ProjectValidationLease $projectDir $LockWaitSeconds
    $snapshot = Get-ValidationSaveSnapshot $profile
    if (-not $SkipBuild) {
        Invoke-ValidationBuild $projectDir 'Debug' (Join-Path $projectDir "Logs/validation/anime_motion_$tag.build.log") | Out-Null
        Invoke-ValidationBuild $projectDir 'ExportRelease' (Join-Path $projectDir "Logs/validation/anime_motion_$tag.release.log") | Out-Null
    }
    [IO.Directory]::CreateDirectory((Join-Path $projectDir 'Logs/evidence/anime-motion')) | Out-Null
    foreach ($view in @('dash','action')) {
        $stdout = Join-Path $projectDir "Logs/validation/anime_motion_$tag.$view.log"
        $stderr = Join-Path $projectDir "Logs/validation/anime_motion_$tag.$view.err"
        $runArgs = @('--path', $projectDir, '--resolution', '1280x720', '--log-file', (Join-Path $projectDir "Logs/validation/anime_motion_$tag.$view.engine.log"))
        if ($Movie) { $runArgs += @('--write-movie', (Join-Path $projectDir "Logs/evidence/anime-motion/$view.avi"), '--fixed-fps', '30', '--disable-vsync') }
        $runArgs += @('res://Scenes/Debug/AnimeMotionLab.tscn', '--', '--motion-smear-check', "--motion-demo=$view")
        try {
            $result = Invoke-ValidationEngine $engine $runArgs $projectDir $stdout $stderr 120
            if ($result.Output -notmatch '\[AnimeMotion\] SUMMARY pass=\d+ fail=0') { throw "动作拖抹未满足完成断言：$stdout" }
            $evidence.Add("=== $view exit=$($result.ExitCode) stderr=0 ===")
            $evidence.Add($result.Output)
            Write-Output "PASS anime-motion $view"
        }
        finally { Assert-ValidationSaves $profile $snapshot }
    }
    [IO.File]::WriteAllText((Join-Path $projectDir "Logs/evidence/anime-motion/validation-$tag.log"), ($evidence -join "`n"), [Text.UTF8Encoding]::new($false))
}
finally {
    try { if ($null -ne $snapshot) { Assert-ValidationSaves $profile $snapshot } }
    finally { if ($lease) { $lease.Dispose() } }
}
