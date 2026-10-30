# 真实内容校验路径的GC缓存竞态回归；屏障只存在于专用测试进程。
param([switch]$SkipBuild, [switch]$Render, [ValidateRange(1, 10)][int]$Repeats = 3)
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Validation.Common.ps1')
$godotExe = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profileDir = Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$tag = Get-Date -Format 'yyyyMMdd_HHmmss'
$lease = Open-ProjectValidationLease $projectDir 90
$saves = Get-ValidationSaveSnapshot $profileDir
$runs = [Collections.Generic.List[object]]::new()
$saveHashes = @{}
foreach ($name in $saves.Keys) { $saveHashes[$name] = if ($null -eq $saves[$name]) { 'ABSENT' } else { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Convert]::FromBase64String($saves[$name]))) } }
$report = [ordered]@{ Date=(Get-Date -Format 's'); Passed=$false; Runs=$runs; SaveSHA256=$saveHashes; SavesUnchanged=$false; Failure='' }
function Run-GcProbe([string]$Name, [string[]]$Mode, [switch]$Visual) {
    $stem = Join-Path $projectDir "Logs/validation/gc_$tag.$Name"
    $runArgs = @('--path',$projectDir,'--log-file',"$stem.engine.log",'res://Scenes/Debug/_ResourceLifetimeProbe.tscn')
    if ($Visual) { $runArgs += @('--resolution','1600x900') } else { $runArgs += '--headless' }
    if ($Mode.Count) { $runArgs += '--'; $runArgs += $Mode }
    $result = Invoke-ValidationEngine $godotExe $runArgs $projectDir "$stem.log" "$stem.err" 90
    if ($Name -eq 'content_stress') {
        if ($result.Output -notmatch '内容压力完成：8轮' -or ([regex]::Matches($result.Output,'内容压力轮次\d+/8').Count -ne 8)) { throw '内容压力未完成全部轮次' }
    } else {
        if ($result.Output -notmatch '托管对象存活=False' -or $result.Output -notmatch '已覆盖收集后、终结前') { throw "$Name 未覆盖回收窗口" }
        if ('--content-validator' -in $Mode -and $result.Output -notmatch '实际ContentValidator完成') { throw "$Name 未调用真实内容校验器" }
    }
    Assert-ValidationSaves $profileDir $saves
    $runs.Add([ordered]@{ Name=$Name; Exit=$result.ExitCode; Seconds=$result.Seconds; Stdout="$stem.log"; Stderr="$stem.err"; StdoutSHA256=(Get-FileHash -LiteralPath "$stem.log").Hash; StderrBytes=(Get-Item -LiteralPath "$stem.err").Length })
    Write-Output "PASS $Name"
}
try {
    $active = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -like 'Godot*' -and $_.CommandLine -notmatch '--editor' })
    if ($active.Count) { throw '已有Godot游戏进程，未停止；请在当前游戏结束后验证。' }
    if (-not $SkipBuild) {
        foreach ($configuration in @('Debug','ExportRelease')) {
            $null = Invoke-ValidationBuild $projectDir $configuration (Join-Path $projectDir "Logs/validation/gc_$tag.$configuration.log")
        }
    }
    for ($round = 1; $round -le $Repeats; $round++) { Run-GcProbe "content_$round" @('--content-validator') }
    Run-GcProbe 'content_stress' @('--content-stress')
    Run-GcProbe 'resource_safe' @()
    Run-GcProbe 'native_safe' @('--native-wrapper')
    if ($Render) { Run-GcProbe 'content_render' @('--content-validator') -Visual }
    $report.Passed = $true
} catch { $report.Failure = $_.ToString(); throw }
finally {
    try { Assert-ValidationSaves $profileDir $saves; $report.SavesUnchanged=$true }
    finally {
        $path = Join-Path $projectDir "Logs/evidence/content-gc-$tag.json"
        $report | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $path -Encoding utf8
        $lease.Dispose()
        Write-Output "GC回归记录：$path"
    }
}
