# J专项：原始角色设计验收；构建和进程独占验证锁。
param([switch]$ProbeOnly)
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
. (Join-Path $projectDir '_tools/LogPaths.ps1')
Initialize-ProjectLogPaths -ProjectRoot $projectDir
$godotExe = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profileDir = Join-Path $env:APPDATA 'Godot\app_userdata\ACT Roguelike'
. (Join-Path $PSScriptRoot 'Validation.Legacy.ps1')
Initialize-LegacyValidation 'j6'
$evidence = [Collections.Generic.List[string]]::new()
$probeSavePath = Join-Path $profileDir 'batch_c_loadout_test.cfg'
$probeSaveBackup = if (Test-Path -LiteralPath $probeSavePath) { [IO.File]::ReadAllBytes($probeSavePath) } else { $null }

Push-Location $projectDir
try {
    Invoke-CheckedBuild 'Debug'
    Invoke-OwnedProcess 'design_headless' @('--headless','--path',$projectDir,'res://Scenes/Debug/_ChargeReleaseProbe.tscn')
    Invoke-OwnedProcess 'design_render' @('--path',$projectDir,'--resolution','1920x1080','res://Scenes/Debug/_ChargeReleaseProbe.tscn')
    foreach ($runName in @('design_headless','design_render')) {
        $design = Get-Content "Logs/validation/.ai_j6_$runName.log" -Raw
        if ($design -notmatch '蓄力释放验收结束：通过 \d+，失败 0') { throw '原始设计探针未完整通过' }
        $evidence.AddRange([string[]]($design -split "`n" | Where-Object { $_.StartsWith('[ChargeRelease]') }))
    }
    if (-not $ProbeOnly) {
        Invoke-OwnedProcess 'blink_headless' @('--headless','--path',$projectDir,'res://Scenes/Debug/_BlinkGuardProbe.tscn')
        Invoke-OwnedProcess 'blink_render' @('--path',$projectDir,'--resolution','1920x1080','res://Scenes/Debug/_BlinkGuardProbe.tscn')
        foreach ($blinkCase in @('blink_headless','blink_render')) {
            $blinkLog = Get-Content "Logs/validation/.ai_j6_$blinkCase.log" -Raw
            if ($blinkLog -notmatch '闪架专项验收结束：通过 \d+，失败 0') { throw '闪架回归未通过' }
            $evidence.Add(($blinkLog -split "`n" | Where-Object { $_ -match '闪架专项验收结束' } | Select-Object -Last 1))
        }
        Invoke-OwnedProcess 'fall_headless' @('--headless','--path',$projectDir,'res://Scenes/Debug/_FallAttackProbe.tscn')
        Invoke-OwnedProcess 'fall_render' @('--path',$projectDir,'--resolution','1920x1080','res://Scenes/Debug/_FallAttackProbe.tscn')
        foreach ($fallCase in @('fall_headless','fall_render')) {
            $fallLog = Get-Content "Logs/validation/.ai_j6_$fallCase.log" -Raw
            if ($fallLog -notmatch '下落专项验收结束：通过 \d+，失败 0') { throw '下落回归未通过' }
            $evidence.Add(($fallLog -split "`n" | Where-Object { $_ -match '下落专项验收结束' } | Select-Object -Last 1))
        }
        Invoke-OwnedProcess 'actions_headless' @('--headless','--path',$projectDir,'res://Scenes/Debug/_ActionInterruptionProbe.tscn')
        Invoke-OwnedProcess 'actions_render' @('--path',$projectDir,'--resolution','1920x1080','res://Scenes/Debug/_ActionInterruptionProbe.tscn')
        foreach ($actionCase in @('actions_headless','actions_render')) {
            $actionLog = Get-Content "Logs/validation/.ai_j6_$actionCase.log" -Raw
            if ($actionLog -notmatch '动作交接验收结束：通过 \d+，失败 0') { throw '动作交接回归未通过' }
            $evidence.Add(($actionLog -split "`n" | Where-Object { $_ -match '动作交接验收结束' } | Select-Object -Last 1))
        }
        Invoke-OwnedProcess 'original_headless' @('--headless','--path',$projectDir,'res://Scenes/Debug/_CharacterDesignProbe.tscn')
        Invoke-OwnedProcess 'original_render' @('--path',$projectDir,'--resolution','1920x1080','res://Scenes/Debug/_CharacterDesignProbe.tscn')
        foreach ($originalCase in @('original_headless','original_render')) {
            $originalLog = Get-Content "Logs/validation/.ai_j6_$originalCase.log" -Raw
            if ($originalLog -notmatch '原始设计验收结束：通过 \d+，失败 0') { throw '原始设计回归未通过' }
            $evidence.Add(($originalLog -split "`n" | Where-Object { $_ -match '原始设计验收结束' } | Select-Object -Last 1))
        }
        Invoke-OwnedProcess 'selftest' @('--headless','--verbose','--path',$projectDir,'--quit-after','40000','res://Scenes/Main.tscn','--','--selftest')
        $selftest = Get-Content 'Logs/validation/.ai_j6_selftest.log'
        if (($selftest -join "`n") -notmatch '=== 自测结束 ===') { throw '完整自测未结束' }
        foreach ($skillMarker in @('✅ P4·技能1 闪：真实动画Action后','✅ P4·技能2 架：真实GuardOn后')) {
            $skillLine = $selftest | Where-Object { $_.Contains($skillMarker) } | Select-Object -First 1
            if ([string]::IsNullOrEmpty($skillLine)) { throw "真实动画验收缺失：$skillMarker" }
            $evidence.Add($skillLine)
        }
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
            $log = Get-Content "Logs/validation/.ai_j6_$($case[0]).log" -Raw
            if ($log -notmatch $case[2]) { throw "$($case[0])验收未完整通过" }
            $evidence.Add(($log -split "`n" | Where-Object { $_ -match $case[2] } | Select-Object -First 1))
        }
        Invoke-OwnedProcess 'memory' @('--headless','--path',$projectDir,'res://Scenes/Debug/_BatchCProbe.tscn','--','--loadout-test','--memory-check')
        if ((Get-Content 'Logs/validation/.ai_j6_memory.log' -Raw) -notmatch '通过 5，失败 0') { throw '跨进程读档未通过' }
        $evidence.Add('跨进程读档5/0')
        Invoke-CheckedBuild 'ExportRelease'
    }
    foreach ($shot in @('Logs/evidence/screenshots/j6_energy_release.png','Logs/evidence/screenshots/j6_variation_burst.png')) {
        if (-not (Test-Path -LiteralPath $shot)) { throw "缺少原始设计截图：$shot" }
        $evidence.Add("截图 $shot SHA256=$((Get-FileHash -LiteralPath $shot -Algorithm SHA256).Hash)")
    }
    foreach ($name in @('meta.cfg','ability_cards.cfg','settings.cfg','loadout.cfg')) {
        $path = Join-Path $profileDir $name
        $evidence.Add("存档 $name SHA256=$((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash)")
    }
}
finally {
    if ($null -eq $probeSaveBackup) { if (Test-Path -LiteralPath $probeSavePath) { Remove-Item -LiteralPath $probeSavePath } }
    else { [IO.File]::WriteAllBytes($probeSavePath, $probeSaveBackup) }
    $evidence.Add("selftest.flag 不存在=$(-not (Test-Path -LiteralPath (Join-Path $profileDir 'selftest.flag')))")
    [IO.File]::WriteAllText((Join-Path $projectDir 'Logs/evidence/character-design-j6-evidence.log'), (($evidence -join "`n") + "`n").Replace("`r",''), [Text.UTF8Encoding]::new($false))
    Pop-Location
}
