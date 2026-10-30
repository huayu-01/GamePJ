# I专项：资源缓存竞态与运行期读取入口验收；构建和进程独占验证锁。
param([switch]$ProbeOnly)
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
. (Join-Path $projectDir '_tools/LogPaths.ps1')
Initialize-ProjectLogPaths -ProjectRoot $projectDir
$godotExe = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profileDir = Join-Path $env:APPDATA 'Godot\app_userdata\ACT Roguelike'
. (Join-Path $PSScriptRoot 'Validation.Legacy.ps1')
Initialize-LegacyValidation 'i'
$evidence = [Collections.Generic.List[string]]::new()
$probeSavePath = Join-Path $profileDir 'batch_c_loadout_test.cfg'
$probeSaveBackup = if (Test-Path -LiteralPath $probeSavePath) { [IO.File]::ReadAllBytes($probeSavePath) } else { $null }

Push-Location $projectDir
try {
    Invoke-CheckedBuild 'Debug'
    Invoke-OwnedProcess 'lifetime_safe' @('--headless','--verbose','--path',$projectDir,'res://Scenes/Debug/_ResourceLifetimeProbe.tscn')
    $lifetime = Get-Content 'Logs/validation/.ai_i_lifetime_safe.log' -Raw
    if ($lifetime -notmatch '托管对象存活=False' -or $lifetime -notmatch '已覆盖收集后、终结前') { throw 'GC竞态条件未覆盖' }
    $evidence.AddRange([string[]]($lifetime -split "`n" | Where-Object { $_.StartsWith('[Lifetime]') }))
    Invoke-OwnedProcess 'native_safe' @('--headless','--verbose','--path',$projectDir,'res://Scenes/Debug/_ResourceLifetimeProbe.tscn','--','--native-wrapper')
    Invoke-OwnedProcess 'lifetime_render' @('--path',$projectDir,'--resolution','1920x1080','res://Scenes/Debug/_ResourceLifetimeProbe.tscn')
    if (-not $ProbeOnly) {
        Invoke-OwnedProcess 'selftest' @('--headless','--verbose','--path',$projectDir,'--quit-after','40000','res://Scenes/Main.tscn','--','--selftest')
        $selftest = Get-Content 'Logs/validation/.ai_i_selftest.log'
        if (($selftest -join "`n") -notmatch '=== 自测结束 ===') { throw '完整自测未结束' }
        $baseline = Get-Content 'Logs/evidence/e-batch-evidence.log'
        # 八项硬指标与历史红标由Assert-MainValidation统一核对。
        foreach ($case in @(
            @('f','res://Scenes/Debug/_FollowupProbe.tscn','F专项验收结束：通过 \d+，失败 0'),
            @('c','res://Scenes/Debug/_BatchCProbe.tscn','C批验收结束：通过 \d+，失败 0'),
            @('d','res://Scenes/Debug/_BatchDProbe.tscn','D批验收结束：通过 \d+，失败 0'),
            @('g','res://Scenes/Debug/_FeedbackProbe.tscn','G专项验收结束：通过 \d+，失败 0'),
            @('characters','res://Scenes/Debug/_CharacterReplacementProbe.tscn','完成：通过 \d+，失败 0'),
            @('b','res://Scenes/Debug/_BatchBProbe.tscn','B 批验收结束：通过 \d+，失败 0'),
            @('a','res://Scenes/UI/_BatchAProbe.tscn','A 批验收结束：失败 0'),
            @('e','res://Scenes/UI/_BatchEProbe.tscn','E 批验收结束：通过 \d+，失败 0')
        )) {
            # D3D12的RGB8→RGBA8兼容转换只在verbose时报告；渲染验收沿用F原有常规启动参数。
            $runArgs = if ($case[0] -eq 'f') { @('--path',$projectDir,'--resolution','1920x1080',$case[1]) } else { @('--headless','--verbose','--path',$projectDir,'--quit-after','20000',$case[1]) }
            if ($case[0] -eq 'c') { $runArgs += @('--','--loadout-test') }
            Invoke-OwnedProcess $case[0] $runArgs ($case[0] -eq 'b')
            $log = Get-Content "Logs/validation/.ai_i_$($case[0]).log" -Raw
            if ($log -notmatch $case[2]) { throw "$($case[0])验收未完整通过" }
            $evidence.Add(($log -split "`n" | Where-Object { $_ -match $case[2] } | Select-Object -First 1))
        }
        Invoke-OwnedProcess 'memory' @('--headless','--path',$projectDir,'res://Scenes/Debug/_BatchCProbe.tscn','--','--loadout-test','--memory-check')
        if ((Get-Content 'Logs/validation/.ai_i_memory.log' -Raw) -notmatch '通过 5，失败 0') { throw '跨进程读档未通过' }
        $evidence.Add('跨进程读档5/0')
        Invoke-CheckedBuild 'ExportRelease'
    }
    $shot = 'Logs/evidence/screenshots/i_lifetime_after_gc.png'
    if (-not (Test-Path -LiteralPath $shot)) { throw '缺少GC后基地截图' }
    $evidence.Add("截图 i_lifetime_after_gc.png SHA256=$((Get-FileHash -LiteralPath $shot -Algorithm SHA256).Hash)")
    foreach ($name in @('meta.cfg','ability_cards.cfg','settings.cfg','loadout.cfg')) {
        $path = Join-Path $profileDir $name
        $evidence.Add("存档 $name SHA256=$((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash)")
    }
}
finally {
    if ($null -eq $probeSaveBackup) { if (Test-Path -LiteralPath $probeSavePath) { Remove-Item -LiteralPath $probeSavePath } }
    else { [IO.File]::WriteAllBytes($probeSavePath, $probeSaveBackup) }
    $evidence.Add("selftest.flag 不存在=$(-not (Test-Path -LiteralPath (Join-Path $profileDir 'selftest.flag')))")
    [IO.File]::WriteAllText((Join-Path $projectDir 'Logs/evidence/resource-lifetime-evidence.log'), (($evidence -join "`n") + "`n").Replace("`r",''), [Text.UTF8Encoding]::new($false))
    Pop-Location
}
