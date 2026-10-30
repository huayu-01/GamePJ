# C批验收。构建/运行独占验证锁；只管理自己的进程；GM落盘测试前备份、后精确恢复存档。
param([switch]$ProbeOnly)
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
. (Join-Path $projectDir '_tools/LogPaths.ps1')
Initialize-ProjectLogPaths -ProjectRoot $projectDir
$godotExe = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profileDir = Join-Path $env:APPDATA 'Godot\app_userdata\ACT Roguelike'
. (Join-Path $PSScriptRoot 'Validation.Legacy.ps1')
Initialize-LegacyValidation 'c'
$evidence = [Collections.Generic.List[string]]::new()
$probeSavePath = Join-Path $profileDir 'batch_c_loadout_test.cfg'
$probeSaveBackup = if (Test-Path -LiteralPath $probeSavePath) { [IO.File]::ReadAllBytes($probeSavePath) } else { $null }

Push-Location $projectDir
try {
    Invoke-CheckedBuild 'Debug'
    if (-not $ProbeOnly) {
    Invoke-OwnedProcess 'selftest_final' @('--headless', '--path', $projectDir, '--quit-after', '40000', 'res://Scenes/Main.tscn', '--', '--selftest')
    $selftest = Get-Content 'Logs/validation/.ai_c_selftest_final.log'
    if (($selftest -join "`n") -notmatch '=== 自测结束 ===') { throw '完整自测没有走到结束标记' }
    $baseline = Get-Content 'Logs/evidence/e-batch-evidence.log'
    # 八项硬指标与历史红标由Assert-MainValidation统一核对。
    }
    Invoke-OwnedProcess 'probe_final' @('--path', $projectDir, '--resolution', '1920x1080', '--quit-after', '10000', 'res://Scenes/Debug/_BatchCProbe.tscn', '--', '--loadout-test')
    $probe = Get-Content 'Logs/validation/.ai_c_probe_final.log'
    if (($probe -join "`n") -notmatch 'C批验收结束：通过 \d+，失败 0') { throw 'C批渲染探针未完整通过' }
    foreach ($line in $probe | Where-Object { $_.StartsWith('[BatchC]') }) { $evidence.Add($line) }
    Invoke-OwnedProcess 'memory' @('--headless', '--path', $projectDir, '--quit-after', '3000', 'res://Scenes/Debug/_BatchCProbe.tscn', '--', '--loadout-test', '--memory-check')
    foreach ($line in Get-Content 'Logs/validation/.ai_c_memory.log' | Where-Object { $_.StartsWith('[BatchC]') }) { $evidence.Add($line) }
    if (-not $ProbeOnly) {
    Invoke-OwnedProcess 'b_regression' @('--headless', '--path', $projectDir, '--quit-after', '10000', 'res://Scenes/Debug/_BatchBProbe.tscn') $true
    $b = Get-Content 'Logs/validation/.ai_c_b_regression.log'
    if (($b -join "`n") -notmatch 'B 批验收结束：通过 \d+，失败 0') { throw 'B批回归未通过' }
    $evidence.Add(($b | Where-Object { $_ -match 'B 批验收结束' } | Select-Object -First 1))
    Invoke-OwnedProcess 'a_regression' @('--headless', '--path', $projectDir, '--resolution', '1920x1080', '--quit-after', '6000', 'res://Scenes/UI/_BatchAProbe.tscn')
    $a = Get-Content 'Logs/validation/.ai_c_a_regression.log'
    if (($a -join "`n") -notmatch 'A 批验收结束：失败 0') { throw 'A批回归未通过' }
    $evidence.Add("A批回归：$(@($a | Where-Object { $_.StartsWith('[BatchA] PASS') }).Count) PASS / 0 FAIL")
    Invoke-OwnedProcess 'e_regression' @('--headless', '--path', $projectDir, '--resolution', '1920x1080', '--quit-after', '10000', 'res://Scenes/UI/_BatchEProbe.tscn')
    $e = Get-Content 'Logs/validation/.ai_c_e_regression.log'
    if (($e -join "`n") -notmatch 'E 批验收结束：通过 \d+，失败 0') { throw 'E批回归未通过' }
    $evidence.Add(($e | Where-Object { $_ -match 'E 批验收结束' } | Select-Object -First 1))
    Invoke-CheckedBuild 'ExportRelease'
    }
    foreach ($name in @('meta.cfg', 'ability_cards.cfg', 'settings.cfg', 'loadout.cfg')) {
        $path = Join-Path $profileDir $name
        $hash = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { '不存在' }
        $evidence.Add("存档恢复 $name=$hash")
    }
    foreach ($name in @('c_base_combat', 'c_base_melee', 'c_character_stands', 'c_scythe_selected', 'c_sword_equipped', 'c_base_dive', 'c_enter_level')) {
        $path = Join-Path $projectDir "Logs/evidence/screenshots/$name.png"
        if (-not (Test-Path -LiteralPath $path)) { throw "缺少截图：$name" }
        $evidence.Add("截图 $name.png SHA256=$((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash)")
    }
}
finally {
    if ($null -eq $probeSaveBackup) { if (Test-Path -LiteralPath $probeSavePath) { Remove-Item -LiteralPath $probeSavePath } }
    else { [IO.File]::WriteAllBytes($probeSavePath, $probeSaveBackup) }
    $evidence.Add("selftest.flag 不存在=$(-not (Test-Path -LiteralPath (Join-Path $profileDir 'selftest.flag')))")
    [IO.File]::WriteAllText((Join-Path $projectDir 'Logs/evidence/c-batch-evidence.log'), ($evidence -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
    Pop-Location
}
