# H专项验收。构建/运行独占验证锁；只管理自己的进程；GM落盘测试前备份、后精确恢复存档。
param([switch]$ProbeOnly)
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
. (Join-Path $projectDir '_tools/LogPaths.ps1')
Initialize-ProjectLogPaths -ProjectRoot $projectDir
$godotExe = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profileDir = Join-Path $env:APPDATA 'Godot\app_userdata\ACT Roguelike'
. (Join-Path $PSScriptRoot 'Validation.Legacy.ps1')
Initialize-LegacyValidation 'h'
$evidence = [Collections.Generic.List[string]]::new()
$probeSavePath = Join-Path $profileDir 'batch_c_loadout_test.cfg'
$probeSaveBackup = if (Test-Path -LiteralPath $probeSavePath) { [IO.File]::ReadAllBytes($probeSavePath) } else { $null }

Push-Location $projectDir
try {
    Invoke-CheckedBuild 'Debug'
    if (-not $ProbeOnly) {
        Invoke-OwnedProcess 'selftest_final' @('--headless', '--verbose', '--path', $projectDir, '--quit-after', '40000', 'res://Scenes/Main.tscn', '--', '--selftest')
        $selftest = Get-Content 'Logs/validation/.ai_h_selftest_final.log'
        if (($selftest -join "`n") -notmatch '=== 自测结束 ===') { throw '完整自测未结束' }
        $baseline = Get-Content 'Logs/evidence/e-batch-evidence.log'
        # 八项硬指标与历史红标由Assert-MainValidation统一核对。
    }
    Invoke-OwnedProcess 'probe_final' @('--path', $projectDir, '--resolution', '1920x1080', '--quit-after', '20000', 'res://Scenes/Debug/_BalanceProbe.tscn')
    $probe = Get-Content 'Logs/validation/.ai_h_probe_final.log'
    if (($probe -join "`n") -notmatch 'H专项验收结束：通过 \d+，失败 0') { throw 'H专项未完整通过' }
    if (($probe -join "`n") -match '等不到动画事件帧') { throw '循环连招出现错误的等待ATK超时' }
    foreach ($line in $probe | Where-Object { $_ -match '^\[(Balance|BalanceSample|DefenseSample)\]' }) { $evidence.Add($line) }
    foreach ($file in @('docs/balance-samples.csv','docs/balance-defense.csv')) {
        if (@(Import-Csv -LiteralPath $file).Count -ne 12) { throw "$file 不是12份样本" }
        $evidence.Add("数据 $file SHA256=$((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash)")
    }
    if (-not $ProbeOnly) {
        foreach ($case in @(
            @('feedback','res://Scenes/Debug/_FeedbackProbe.tscn','G专项验收结束：通过 \d+，失败 0'),
            @('characters','res://Scenes/Debug/_CharacterReplacementProbe.tscn','完成：通过 \d+，失败 0'),
            @('d_regression','res://Scenes/Debug/_BatchDProbe.tscn','D批验收结束：通过 \d+，失败 0'),
            @('c_regression','res://Scenes/Debug/_BatchCProbe.tscn','C批验收结束：通过 \d+，失败 0'),
            @('b_regression','res://Scenes/Debug/_BatchBProbe.tscn','B 批验收结束：通过 \d+，失败 0'),
            @('a_regression','res://Scenes/UI/_BatchAProbe.tscn','A 批验收结束：失败 0'),
            @('e_regression','res://Scenes/UI/_BatchEProbe.tscn','E 批验收结束：通过 \d+，失败 0')
        )) {
            $runArgs = @('--headless','--path',$projectDir,'--quit-after','10000',$case[1])
            if ($case[0] -eq 'c_regression') { $runArgs += @('--','--loadout-test') }
            Invoke-OwnedProcess $case[0] $runArgs ($case[0] -eq 'b_regression')
            $text = Get-Content "Logs/validation/.ai_h_$($case[0]).log" -Raw
            if ($text -notmatch $case[2]) { throw "$($case[0])回归未完整通过" }
            $evidence.Add(($text -split "`n" | Where-Object { $_ -match $case[2] } | Select-Object -First 1))
        }
        Invoke-OwnedProcess 'memory' @('--headless','--path',$projectDir,'--quit-after','3000','res://Scenes/Debug/_BatchCProbe.tscn','--','--loadout-test','--memory-check')
        if ((Get-Content 'Logs/validation/.ai_h_memory.log' -Raw) -notmatch 'C批验收结束：通过 5，失败 0') { throw '跨进程读档回归未通过' }
        $evidence.Add('跨进程读档5/0')
        Invoke-CheckedBuild 'ExportRelease'
    }
    foreach ($name in @('meta.cfg','ability_cards.cfg','settings.cfg','loadout.cfg')) {
        $path = Join-Path $profileDir $name
        $hash = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { '不存在' }
        $evidence.Add("存档恢复 $name=$hash")
    }
    $shot = 'Logs/evidence/screenshots/h_balance_samples.png'
    if (-not (Test-Path -LiteralPath $shot)) { throw '缺少H截图' }
    $evidence.Add("截图 h_balance_samples.png SHA256=$((Get-FileHash -LiteralPath $shot -Algorithm SHA256).Hash)")
}
finally {
    if ($null -eq $probeSaveBackup) { if (Test-Path -LiteralPath $probeSavePath) { Remove-Item -LiteralPath $probeSavePath } }
    else { [IO.File]::WriteAllBytes($probeSavePath, $probeSaveBackup) }
    $evidence.Add("selftest.flag 不存在=$(-not (Test-Path -LiteralPath (Join-Path $profileDir 'selftest.flag')))")
    [IO.File]::WriteAllText((Join-Path $projectDir 'Logs/evidence/followup-h-evidence.log'), (($evidence -join "`n") + "`n").Replace("`r", ''), [Text.UTF8Encoding]::new($false))
    Pop-Location
}
