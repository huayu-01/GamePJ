# J12：胶囊扫掠、全局实际弹速上限与五探针回归，验证独占且保护真实存档。
param([switch]$ProbeOnly, [switch]$SpiritSword, [switch]$Scythe)
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
$godotExe = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profileDir = Join-Path $env:APPDATA 'Godot\app_userdata\ACT Roguelike'
$evidence = [Collections.Generic.List[string]]::new()
$lease = $null
$ownedProcess = $null
$original = @{}
$runTag = Get-Date -Format 'yyyyMMdd_HHmmss'
$batchId = if ($Scythe) { 'j14' } elseif ($SpiritSword) { 'j13' } else { 'j12' }
$saveNames = @('meta.cfg','ability_cards.cfg','settings.cfg','loadout.cfg','rune.cfg')
$utf8 = [Text.UTF8Encoding]::new($false)

function Save-Fingerprint([string]$Phase) {
    foreach ($name in $saveNames) {
        $path = Join-Path $profileDir $name
        $hash = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { 'ABSENT' }
        $evidence.Add("存档 $Phase $name SHA256=$hash")
    }
}

function Assert-Saves {
    foreach ($name in $saveNames) {
        $path = Join-Path $profileDir $name
        $current = if (Test-Path -LiteralPath $path) { [Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) } else { $null }
        if ($current -cne $original[$name]) { throw "存档变化：$name；保留现场，不覆盖用户可能的手动改动。" }
    }
}

function Invoke-Probe([string]$RunName, [string]$Scene, [string]$Expected, [switch]$Render, [switch]$SelfTest) {
    $stdoutPath = Join-Path $projectDir ".ai_$($batchId)_$runTag.$RunName.log"
    $stderrPath = Join-Path $projectDir ".ai_$($batchId)_$runTag.$RunName.err"
    $runArgs = @('--path',$projectDir,'--quit-after','40000',$Scene)
    if ($Render) { $runArgs += @('--resolution','1600x900') } else { $runArgs += '--headless' }
    if ($SelfTest) { $runArgs += @('--','--selftest') }
    try {
        $script:ownedProcess = Start-Process -FilePath $godotExe -ArgumentList $runArgs -WorkingDirectory $projectDir -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
        $timer = [Diagnostics.Stopwatch]::StartNew()
        while (-not $ownedProcess.HasExited -and $timer.Elapsed.TotalSeconds -lt 180) {
            Start-Sleep -Milliseconds 300
            $ownedProcess.Refresh()
        }
        if (-not $ownedProcess.HasExited) { throw "$RunName 超时；仅清理本脚本 PID $($ownedProcess.Id)。" }
        $out = Get-Content -LiteralPath $stdoutPath -Raw
        $err = Get-Content -LiteralPath $stderrPath -Raw
        $result = "$RunName exit=$($ownedProcess.ExitCode) seconds=$([Math]::Round($timer.Elapsed.TotalSeconds,2)) stderrBytes=$((Get-Item -LiteralPath $stderrPath).Length)"
        Write-Output $result
        $evidence.Add($result)
        $evidence.Add("=== $RunName ===")
        $evidence.Add($out)
        if ($err) { $evidence.Add($err) }
        if ($ownedProcess.ExitCode -ne 0 -or $out -notmatch $Expected -or $err.Length -gt 0) { throw "$RunName 未通过：$stdoutPath / $stderrPath" }
        if ($SelfTest) {
            if (($SpiritSword -or $Scythe) -and $out -notmatch '✅ P5b·实测表参数落到机制上') { throw 'Main专属技能增强契约探针未通过' }
            $markers = @('配置表：武器 6','节点布置：启用','配置表比对·玩家：','配置表比对·索引：','Pressure 测试（进化','白板首领实例：','端到端：按 F 之后','最小闭环·链路：')
            $baseline = Get-Content -LiteralPath (Join-Path $projectDir 'docs\character-design-j11-evidence.log')
            foreach ($marker in $markers) {
                $before = ($baseline | Where-Object { $_.StartsWith('[log] ') -and $_.Contains($marker) } | Select-Object -First 1) -replace '^\[log\] \S+ ', ''
                $after = ($out -split "`n" | Where-Object { $_.StartsWith('[log] ') -and $_.Contains($marker) } | Select-Object -First 1) -replace '^\[log\] \S+ ', ''
                if ([string]::IsNullOrWhiteSpace($before) -or $before.TrimEnd() -cne $after.TrimEnd()) { throw "硬指标变化：$marker" }
                $evidence.Add("[J11逐字一致] $($after.TrimEnd())")
            }
            Write-Output 'Main 八条硬指标逐字一致（索引14个数、末尾符文11）'
        }
    }
    finally {
        if ($ownedProcess -and -not $ownedProcess.HasExited) { Stop-Process -Id $ownedProcess.Id }
        $script:ownedProcess = $null
        $flag = Join-Path $profileDir 'selftest.flag'
        if (Test-Path -LiteralPath $flag) { Remove-Item -LiteralPath $flag }
        Assert-Saves
    }
}

Push-Location $projectDir
try {
    $lease = [IO.File]::Open((Join-Path $projectDir '.ai_validation.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    $active = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -like 'Godot*' -and $_.CommandLine -notmatch '--editor' })
    if ($active.Count -gt 0) { throw "发现已有 Godot 游戏进程：$($active.ProcessId -join ',')，未停止它们。" }
    if (Test-Path -LiteralPath (Join-Path $profileDir 'selftest.flag')) { throw '已有 selftest.flag，需先核实归属。' }
    foreach ($name in $saveNames) {
        $path = Join-Path $profileDir $name
        $original[$name] = if (Test-Path -LiteralPath $path) { [Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) } else { $null }
    }
    [IO.File]::WriteAllText((Join-Path $projectDir ".ai_$($batchId)_$runTag.saves.json"),($original | ConvertTo-Json),$utf8)
    Save-Fingerprint 'BEFORE'
    & (Join-Path $projectDir 'build.ps1') --no-incremental -v minimal | Tee-Object -FilePath ".ai_$($batchId)_$runTag.Debug.log"
    if ($LASTEXITCODE -ne 0) { throw 'Debug 构建失败' }
    dotnet build ActRoguelike.csproj --no-incremental -v minimal -c ExportRelease | Tee-Object -FilePath ".ai_$($batchId)_$runTag.ExportRelease.log"
    if ($LASTEXITCODE -ne 0) { throw 'ExportRelease 构建失败' }
    foreach ($configuration in @('Debug','ExportRelease')) {
        $build = Get-Content -LiteralPath ".ai_$($batchId)_$runTag.$configuration.log" -Raw
        $evidence.Add("=== $configuration ===")
        $evidence.Add($build)
        if ($build -notmatch '0 个警告' -or $build -notmatch '0 个错误') { throw "$configuration 未满足 0/0" }
    }
    if ($Scythe) {
        Invoke-Probe 'scythe' 'res://Scenes/Debug/_ScytheEvolutionProbe.tscn' '镰刀末刀进化验收结束：通过 \d+，失败 0'
        Invoke-Probe 'scythe_render' 'res://Scenes/Debug/_ScytheEvolutionProbe.tscn' '镰刀末刀进化验收结束：通过 \d+，失败 0' -Render
    }
    elseif ($SpiritSword) {
        Invoke-Probe 'sword' 'res://Scenes/Debug/_SpiritSwordEvolutionProbe.tscn' '灵剑进化验收结束：通过 \d+，失败 0'
        Invoke-Probe 'sword_render' 'res://Scenes/Debug/_SpiritSwordEvolutionProbe.tscn' '灵剑进化验收结束：通过 \d+，失败 0' -Render
    }
    else {
        Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 \d+，失败 0'
        Invoke-Probe 'bow_render' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 \d+，失败 0' -Render
    }
    if (-not $ProbeOnly) {
        if ($SpiritSword -or $Scythe) { Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0' }
        if ($Scythe) { Invoke-Probe 'sword' 'res://Scenes/Debug/_SpiritSwordEvolutionProbe.tscn' '灵剑进化验收结束：通过 71，失败 0' }
        Invoke-Probe 'charge' 'res://Scenes/Debug/_ChargeReleaseProbe.tscn' '蓄力释放验收结束：通过 80，失败 0'
        Invoke-Probe 'character' 'res://Scenes/Debug/_CharacterDesignProbe.tscn' '原始设计验收结束：通过 115，失败 0'
        Invoke-Probe 'dragon' 'res://Scenes/Debug/_DragonArrowProbe.tscn' '验收结束：通过 42，失败 0'
        Invoke-Probe 'rune' 'res://Scenes/Debug/_RuneProbe.tscn' '验收结束：通过 83，失败 0'
        Invoke-Probe 'main' 'res://Scenes/Main.tscn' '=== 自测结束 ===' -SelfTest
    }
    $shotNames = if ($Scythe) { @('j14_mercy','j14_salvation') } elseif ($SpiritSword) { @('j13_golden_qi','j13_great_draw','j13_divine_descent') } else { @('j12_capsule_sweep') }
    foreach ($shotName in $shotNames) {
        $shotPath = Join-Path $projectDir "docs\screenshots\$shotName.png"
        if (-not (Test-Path -LiteralPath $shotPath)) { throw "$shotName 截图缺失" }
        $evidence.Add("截图 $shotName SHA256=$((Get-FileHash -LiteralPath $shotPath -Algorithm SHA256).Hash)")
    }
    Save-Fingerprint 'AFTER'
    $evidence.Add("selftest.flag AFTER=$(Test-Path -LiteralPath (Join-Path $profileDir 'selftest.flag'))")
    $outputName = if ($ProbeOnly) { "character-design-$batchId-probe-$runTag.log" } else { "character-design-$batchId-evidence.log" }
    [IO.File]::WriteAllText((Join-Path $projectDir "docs\$outputName"),(($evidence -join "`n").Replace("`r`n","`n").TrimEnd([char[]]"`r`n") + "`n"),$utf8)
    Write-Output "$batchId 验收完成：docs/$outputName"
}
catch {
    $evidence.Add($_.ToString())
    foreach ($configuration in @('Debug','ExportRelease')) {
        $buildPath = Join-Path $projectDir ".ai_$($batchId)_$runTag.$configuration.log"
        if (Test-Path -LiteralPath $buildPath) {
            $evidence.Add("=== 失败现场构建 $configuration ===")
            $evidence.Add((Get-Content -LiteralPath $buildPath -Raw))
        }
    }
    [IO.File]::WriteAllText((Join-Path $projectDir "docs\character-design-$batchId-failure-$runTag.log"),(($evidence -join "`n").Replace("`r`n","`n").TrimEnd([char[]]"`r`n") + "`n"),$utf8)
    throw
}
finally {
    if ($lease) { $lease.Dispose() }
    Pop-Location
}
