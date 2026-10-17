# H专项验收。构建/运行独占验证锁；只管理自己的进程；GM落盘测试前备份、后精确恢复存档。
param([switch]$ProbeOnly)
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
$godotExe = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profileDir = Join-Path $env:APPDATA 'Godot\app_userdata\ACT Roguelike'
$evidence = [Collections.Generic.List[string]]::new()
$probeSavePath = Join-Path $profileDir 'batch_c_loadout_test.cfg'
$probeSaveBackup = if (Test-Path -LiteralPath $probeSavePath) { [IO.File]::ReadAllBytes($probeSavePath) } else { $null }

function Open-ValidationLease {
    $deadline = (Get-Date).AddSeconds(90)
    do {
        try { return [IO.File]::Open((Join-Path $projectDir '.ai_validation.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
        catch [IO.IOException] { Start-Sleep -Milliseconds 500 }
    } while ((Get-Date) -lt $deadline)
    throw '验证锁等待超时；没有停止其他任务的进程，请稍后重跑。'
}
function Invoke-CheckedBuild([string]$Configuration) {
    $lease = Open-ValidationLease
    try {
        $logName = ".ai_h_build_$Configuration.log"
        if ($Configuration -eq 'Debug') {
            & (Join-Path $projectDir 'build.ps1') --no-incremental -v minimal | Tee-Object -FilePath $logName
        }
        else {
            # build.ps1固定写-c Debug；Release作为额外检查复用它已设置的环境，不叠加两个-c参数。
            dotnet build (Join-Path $projectDir 'ActRoguelike.csproj') --no-incremental -v minimal -c ExportRelease | Tee-Object -FilePath $logName
        }
        if ($LASTEXITCODE -ne 0) { throw "$Configuration 编译失败" }
        $buildText = Get-Content $logName -Raw
        if ($buildText -notmatch '0 个警告' -or $buildText -notmatch '0 个错误') { throw "$Configuration 编译未满足0/0" }
        $evidence.Add("$Configuration 编译：0 warning / 0 error")
    }
    finally { $lease.Dispose() }
}
function Invoke-OwnedProcess([string]$RunName, [string[]]$RunArgs, [bool]$GmWrites = $false) {
    $lease = Open-ValidationLease
    $original = @{}
    $badWrites = [Collections.Generic.List[string]]::new()
    $ownedProcess = $null
    try {
        foreach ($name in @('meta.cfg', 'ability_cards.cfg', 'settings.cfg', 'loadout.cfg')) {
            $path = Join-Path $profileDir $name
            $original[$name] = if (Test-Path -LiteralPath $path) { [IO.File]::ReadAllBytes($path) } else { $null }
        }
        $stdoutPath = Join-Path $projectDir ".ai_h_$RunName.log"
        $stderrPath = Join-Path $projectDir ".ai_h_$RunName.err"
        $ownedProcess = Start-Process -FilePath $godotExe -ArgumentList $RunArgs -WindowStyle Hidden `
            -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
        $deadline = (Get-Date).AddSeconds(150)
        while (-not $ownedProcess.HasExited -and (Get-Date) -lt $deadline) {
            Start-Sleep -Milliseconds 500
            $ownedProcess.Refresh()
        }
        if (-not $ownedProcess.HasExited) {
            Stop-Process -Id $ownedProcess.Id
            throw "$RunName 超时，仅退出本脚本启动的PID $($ownedProcess.Id)"
        }
        $message = "$RunName 退出码=$($ownedProcess.ExitCode)"
        Write-Output $message
        $evidence.Add($message)
        if ($ownedProcess.ExitCode -ne 0) { throw "$RunName 未通过：$stdoutPath" }
        $errors = @(Get-Content -LiteralPath $stderrPath | Where-Object { $_ -match '^(ERROR:|SCRIPT ERROR:)' })
        if ((Get-Content -LiteralPath $stderrPath -Raw) -match '^WARNING:|\nWARNING:') { throw "$RunName 仍有运行告警：$stderrPath" }
        if ($errors.Count -gt 0) { throw "$RunName 新运行错误：$($errors -join ' / ')" }
        if ((Get-Content -LiteralPath $stderrPath -Raw) -match "couldn't resolve track") { throw "$RunName 动画轨道没有绑定到实际模型" }
    }
    finally {
        foreach ($name in $original.Keys) {
            $path = Join-Path $profileDir $name
            $current = if (Test-Path -LiteralPath $path) { [IO.File]::ReadAllBytes($path) } else { $null }
            $beforeBase64 = if ($null -eq $original[$name]) { '' } else { [Convert]::ToBase64String($original[$name]) }
            $afterBase64 = if ($null -eq $current) { '' } else { [Convert]::ToBase64String($current) }
            if ($beforeBase64 -cne $afterBase64) {
                if (-not ($GmWrites -and $name -eq 'meta.cfg')) { $badWrites.Add($name) }
                else {
                    if ($null -eq $original[$name]) { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path } }
                    else { [IO.File]::WriteAllBytes($path, $original[$name]) }
                }
            }
        }
        $flagPath = Join-Path $profileDir 'selftest.flag'
        if (Test-Path -LiteralPath $flagPath) { Remove-Item -LiteralPath $flagPath }
        $lease.Dispose()
    }
    if ($badWrites.Count -gt 0) { throw "$RunName 检测到非预期存档变化，保留现场避免覆盖用户可能的手动设置：$($badWrites -join ', ')" }
}

Push-Location $projectDir
try {
    Invoke-CheckedBuild 'Debug'
    if (-not $ProbeOnly) {
        Invoke-OwnedProcess 'selftest_final' @('--headless', '--verbose', '--path', $projectDir, '--quit-after', '40000', 'res://Scenes/Main.tscn', '--', '--selftest')
        $selftest = Get-Content '.ai_h_selftest_final.log'
        if (($selftest -join "`n") -notmatch '=== 自测结束 ===') { throw '完整自测未结束' }
        $baseline = Get-Content 'docs/e-batch-evidence.log'
        foreach ($marker in @('配置表：武器 6', '节点布置：启用', '配置表比对·玩家：', '配置表比对·索引：', 'Pressure 测试（进化', '白板首领实例：', '端到端：按 F 之后', '最小闭环·链路：')) {
            $before = ($baseline | Where-Object { $_.StartsWith('[基线逐字一致] ') -and $_.Contains($marker) } | Select-Object -First 1) -replace '^\[基线逐字一致\] ', ''
            $after = ($selftest | Where-Object { $_.Contains($marker) } | Select-Object -First 1) -replace '^\[log\] \S+ ', ''
            if ([string]::IsNullOrEmpty($before) -or $before -cne $after) { throw "硬指标改变：$marker" }
            $evidence.Add("[基线逐字一致] $after")
        }
    }
    Invoke-OwnedProcess 'probe_final' @('--path', $projectDir, '--resolution', '1920x1080', '--quit-after', '20000', 'res://Scenes/Debug/_BalanceProbe.tscn')
    $probe = Get-Content '.ai_h_probe_final.log'
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
            $text = Get-Content ".ai_h_$($case[0]).log" -Raw
            if ($text -notmatch $case[2]) { throw "$($case[0])回归未完整通过" }
            $evidence.Add(($text -split "`n" | Where-Object { $_ -match $case[2] } | Select-Object -First 1))
        }
        Invoke-OwnedProcess 'memory' @('--headless','--path',$projectDir,'--quit-after','3000','res://Scenes/Debug/_BatchCProbe.tscn','--','--loadout-test','--memory-check')
        if ((Get-Content '.ai_h_memory.log' -Raw) -notmatch 'C批验收结束：通过 5，失败 0') { throw '跨进程读档回归未通过' }
        $evidence.Add('跨进程读档5/0')
        Invoke-CheckedBuild 'ExportRelease'
    }
    foreach ($name in @('meta.cfg','ability_cards.cfg','settings.cfg','loadout.cfg')) {
        $path = Join-Path $profileDir $name
        $hash = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { '不存在' }
        $evidence.Add("存档恢复 $name=$hash")
    }
    $shot = 'docs/screenshots/h_balance_samples.png'
    if (-not (Test-Path -LiteralPath $shot)) { throw '缺少H截图' }
    $evidence.Add("截图 h_balance_samples.png SHA256=$((Get-FileHash -LiteralPath $shot -Algorithm SHA256).Hash)")
}
finally {
    if ($null -eq $probeSaveBackup) { if (Test-Path -LiteralPath $probeSavePath) { Remove-Item -LiteralPath $probeSavePath } }
    else { [IO.File]::WriteAllBytes($probeSavePath, $probeSaveBackup) }
    $evidence.Add("selftest.flag 不存在=$(-not (Test-Path -LiteralPath (Join-Path $profileDir 'selftest.flag')))")
    [IO.File]::WriteAllText((Join-Path $projectDir 'docs/followup-h-evidence.log'), (($evidence -join "`n") + "`n").Replace("`r", ''), [Text.UTF8Encoding]::new($false))
    Pop-Location
}
