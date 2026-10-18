# J12：胶囊扫掠、全局实际弹速上限与五探针回归，验证独占且保护真实存档。
param([switch]$ProbeOnly, [switch]$SpiritSword, [switch]$Scythe, [switch]$Affection, [switch]$Rainbow, [switch]$GoldenRoad, [switch]$LightLeap, [switch]$GroundBurst, [switch]$ActionMovement, [switch]$ChargeEvolution, [switch]$JumpStomp, [switch]$BowExitAudit, [switch]$RewardRules, [switch]$RewardTiers, [switch]$ChargeInputAudit, [switch]$KillJumps, [switch]$KillFacts, [switch]$WingDive, [switch]$ProjectileQuery)
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
$godotExe = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profileDir = Join-Path $env:APPDATA 'Godot\app_userdata\ACT Roguelike'
$evidence = [Collections.Generic.List[string]]::new()
$lease = $null
$ownedProcess = $null
$original = @{}
$runTag = Get-Date -Format 'yyyyMMdd_HHmmss'
$batchId = if ($WingDive) { 'j27' } elseif ($KillFacts) { 'j26' } elseif ($KillJumps) { 'j25' } elseif ($RewardTiers) { 'j24' } elseif ($RewardRules) { 'j23' } elseif ($JumpStomp) { 'j22' } elseif ($ChargeEvolution) { 'j21' } elseif ($ActionMovement) { 'j20' } elseif ($GroundBurst) { 'j19' } elseif ($LightLeap) { 'j18' } elseif ($GoldenRoad) { 'j17' } elseif ($Rainbow) { 'j16' } elseif ($Affection) { 'j15' } elseif ($Scythe) { 'j14' } elseif ($SpiritSword) { 'j13' } else { 'j12' }
$RewardRules = $RewardRules -or $RewardTiers -or $KillJumps
if ($ProjectileQuery) { $batchId = 'j28' }
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
            if (($SpiritSword -or $Scythe -or $Affection -or $Rainbow -or $GoldenRoad -or $LightLeap -or $GroundBurst -or $ActionMovement -or $ChargeEvolution -or $JumpStomp -or $RewardRules) -and $out -notmatch '✅ P5b·实测表参数落到机制上') { throw 'Main专属技能增强契约探针未通过' }
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
    if ($ProjectileQuery) {
        Invoke-Probe 'projectile_query' 'res://Scenes/Debug/_ProjectileQueryProbe.tscn' '弹道查询验收结束：通过 56，失败 0'
        if (-not $ProbeOnly) {
            Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0'
            Invoke-Probe 'sword' 'res://Scenes/Debug/_SpiritSwordEvolutionProbe.tscn' '灵剑进化验收结束：通过 71，失败 0'
        }
    }
    elseif ($ChargeInputAudit) {
        if (-not $RewardTiers -or -not $ProbeOnly) { throw '输入审计必须指定-RewardTiers -ProbeOnly，证据独立保存。' }
        foreach ($attempt in 1..2) { Invoke-Probe "charge_input_$attempt" 'res://Scenes/Debug/_ChargeReleaseProbe.tscn' '蓄力释放验收结束：通过 80，失败 0' }
    }
    elseif ($BowExitAudit) {
        if (-not $JumpStomp -or -not $ProbeOnly) { throw '退出审计必须指定-JumpStomp -ProbeOnly，证据独立保存。' }
        foreach ($attempt in 1..3) { Invoke-Probe "bow_exit_$attempt" 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0' }
    }
    elseif ($WingDive) {
        Invoke-Probe 'wing_dive' 'res://Scenes/Debug/_WingDiveProbe.tscn' '飞翼下落验收结束：通过 62，失败 0'
    }
    elseif ($KillFacts) {
        Invoke-Probe 'kill_facts' 'res://Scenes/Debug/_KillFactProbe.tscn' '逐杀事实验收结束：通过 40，失败 0'
    }
    elseif ($KillJumps) {
        Invoke-Probe 'kill_jumps' 'res://Scenes/Debug/_KillJumpProbe.tscn' '击杀跳跃验收结束：通过 78，失败 0'
    }
    elseif ($RewardRules) {
        Invoke-Probe 'reward_rules' 'res://Scenes/Debug/_RewardRulesProbe.tscn' '获取规则验收结束：通过 78，失败 0'
        if ($RewardTiers) { Invoke-Probe 'reward_rules_render' 'res://Scenes/Debug/_RewardRulesProbe.tscn' '获取规则验收结束：通过 79，失败 0' -Render }
    }
    elseif ($JumpStomp) {
        Invoke-Probe 'jump_stomp' 'res://Scenes/Debug/_JumpStompProbe.tscn' '起跳践踏验收结束：通过 39，失败 0'
        Invoke-Probe 'jump_stomp_render' 'res://Scenes/Debug/_JumpStompProbe.tscn' '起跳践踏验收结束：通过 40，失败 0' -Render
    }
    elseif ($ChargeEvolution) {
        Invoke-Probe 'charge_evolution' 'res://Scenes/Debug/_ChargeEvolutionProbe.tscn' '蓄力进化验收结束：通过 151，失败 0'
        Invoke-Probe 'charge_evolution_render' 'res://Scenes/Debug/_ChargeEvolutionProbe.tscn' '蓄力进化验收结束：通过 153，失败 0' -Render
    }
    elseif ($ActionMovement) {
        Invoke-Probe 'movement' 'res://Scenes/Debug/_ActionMovementProbe.tscn' '动作移动验收结束：通过 74，失败 0'
        Invoke-Probe 'movement_render' 'res://Scenes/Debug/_ActionMovementProbe.tscn' '动作移动验收结束：通过 75，失败 0' -Render
    }
    elseif ($GroundBurst) {
        Invoke-Probe 'landing' 'res://Scenes/Debug/_LandingKnockbackProbe.tscn' '地爆验收结束：通过 53，失败 0'
        Invoke-Probe 'landing_render' 'res://Scenes/Debug/_LandingKnockbackProbe.tscn' '地爆验收结束：通过 54，失败 0' -Render
    }
    elseif ($LightLeap) {
        Invoke-Probe 'teleport' 'res://Scenes/Debug/_TeleportJumpProbe.tscn' '浮光跃金验收结束：通过 64，失败 0'
        Invoke-Probe 'teleport_render' 'res://Scenes/Debug/_TeleportJumpProbe.tscn' '浮光跃金验收结束：通过 65，失败 0' -Render
    }
    elseif ($GoldenRoad) {
        Invoke-Probe 'cleave' 'res://Scenes/Debug/_SpearCleaveProbe.tscn' '金光大道验收结束：通过 31，失败 0'
        Invoke-Probe 'cleave_render' 'res://Scenes/Debug/_SpearCleaveProbe.tscn' '金光大道验收结束：通过 32，失败 0' -Render
    }
    elseif ($Rainbow) {
        Invoke-Probe 'rainbow' 'res://Scenes/Debug/_SpearRecallProbe.tscn' '气贯长虹验收结束：通过 89，失败 0'
        Invoke-Probe 'rainbow_render' 'res://Scenes/Debug/_SpearRecallProbe.tscn' '气贯长虹验收结束：通过 90，失败 0' -Render
    }
    elseif ($Affection) {
        Invoke-Probe 'seek' 'res://Scenes/Debug/_ScytheSeekProbe.tscn' '飞镰喜爱验收结束：通过 \d+，失败 0'
        Invoke-Probe 'seek_render' 'res://Scenes/Debug/_ScytheSeekProbe.tscn' '飞镰喜爱验收结束：通过 \d+，失败 0' -Render
    }
    elseif ($Scythe) {
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
        if ($WingDive) { Invoke-Probe 'landing' 'res://Scenes/Debug/_LandingKnockbackProbe.tscn' '地爆验收结束：通过 53，失败 0' }
        if ($KillFacts -or $WingDive) { Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0' }
        if ($KillJumps) { Invoke-Probe 'reward_rules' 'res://Scenes/Debug/_RewardRulesProbe.tscn' '获取规则验收结束：通过 78，失败 0' }
        if ($RewardRules) { Invoke-Probe 'jump_stomp' 'res://Scenes/Debug/_JumpStompProbe.tscn' '起跳践踏验收结束：通过 39，失败 0' }
        if ($JumpStomp -or $RewardRules) { Invoke-Probe 'charge_evolution' 'res://Scenes/Debug/_ChargeEvolutionProbe.tscn' '蓄力进化验收结束：通过 151，失败 0' }
        if ($ChargeEvolution -or $JumpStomp -or $RewardRules) { Invoke-Probe 'movement' 'res://Scenes/Debug/_ActionMovementProbe.tscn' '动作移动验收结束：通过 74，失败 0' }
        if ($SpiritSword -or $Scythe -or $Affection -or $Rainbow -or $GoldenRoad -or $LightLeap -or $GroundBurst -or $ActionMovement -or $ChargeEvolution -or $JumpStomp -or $RewardRules) { Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0' }
        if ($Scythe -or $Affection -or $Rainbow -or $GoldenRoad -or $LightLeap -or $GroundBurst -or $ActionMovement -or $ChargeEvolution -or $JumpStomp -or $RewardRules) { Invoke-Probe 'sword' 'res://Scenes/Debug/_SpiritSwordEvolutionProbe.tscn' '灵剑进化验收结束：通过 71，失败 0' }
        if ($Affection -or $Rainbow -or $GoldenRoad -or $LightLeap -or $GroundBurst -or $ActionMovement -or $ChargeEvolution -or $JumpStomp -or $RewardRules) { Invoke-Probe 'scythe' 'res://Scenes/Debug/_ScytheEvolutionProbe.tscn' '镰刀末刀进化验收结束：通过 36，失败 0' }
        if ($Rainbow -or $GoldenRoad -or $LightLeap -or $GroundBurst -or $ActionMovement -or $ChargeEvolution -or $JumpStomp -or $RewardRules) { Invoke-Probe 'seek' 'res://Scenes/Debug/_ScytheSeekProbe.tscn' '飞镰喜爱验收结束：通过 42，失败 0' }
        if ($GoldenRoad -or $LightLeap -or $GroundBurst -or $ActionMovement -or $ChargeEvolution -or $JumpStomp -or $RewardRules) { Invoke-Probe 'rainbow' 'res://Scenes/Debug/_SpearRecallProbe.tscn' '气贯长虹验收结束：通过 89，失败 0' }
        if ($LightLeap -or $GroundBurst -or $ActionMovement -or $ChargeEvolution -or $JumpStomp -or $RewardRules) { Invoke-Probe 'cleave' 'res://Scenes/Debug/_SpearCleaveProbe.tscn' '金光大道验收结束：通过 31，失败 0' }
        if ($GroundBurst -or $ActionMovement -or $ChargeEvolution -or $JumpStomp -or $RewardRules) { Invoke-Probe 'teleport' 'res://Scenes/Debug/_TeleportJumpProbe.tscn' '浮光跃金验收结束：通过 64，失败 0' }
        if ($ActionMovement -or $ChargeEvolution -or $JumpStomp -or $RewardRules) { Invoke-Probe 'landing' 'res://Scenes/Debug/_LandingKnockbackProbe.tscn' '地爆验收结束：通过 53，失败 0' }
        if ($ActionMovement -or $ChargeEvolution -or $JumpStomp -or $RewardRules) { Invoke-Probe 'collision' 'res://Scenes/Debug/_BatchDProbe.tscn' 'D批验收结束：通过 90，失败 0' }
        Invoke-Probe 'charge' 'res://Scenes/Debug/_ChargeReleaseProbe.tscn' '蓄力释放验收结束：通过 80，失败 0'
        Invoke-Probe 'character' 'res://Scenes/Debug/_CharacterDesignProbe.tscn' '原始设计验收结束：通过 115，失败 0'
        Invoke-Probe 'dragon' 'res://Scenes/Debug/_DragonArrowProbe.tscn' '验收结束：通过 42，失败 0'
        Invoke-Probe 'rune' 'res://Scenes/Debug/_RuneProbe.tscn' '验收结束：通过 83，失败 0'
        Invoke-Probe 'main' 'res://Scenes/Main.tscn' '=== 自测结束 ===' -SelfTest
    }
    $shotNames = if ($BowExitAudit -or $ChargeInputAudit -or $KillJumps -or $KillFacts -or $WingDive) { @() } elseif ($RewardTiers) { @('j24_reward_tiers') } elseif ($RewardRules) { @() } elseif ($JumpStomp) { @('j22_jump_stomp') } elseif ($ChargeEvolution) { @('j21_distinct_homing','j21_flow') } elseif ($ActionMovement) { @('j20_action_movement') } elseif ($GroundBurst) { @('j19_ground_burst') } elseif ($LightLeap) { @('j18_light_leap') } elseif ($GoldenRoad) { @('j17_golden_road') } elseif ($Rainbow) { @('j16_rainbow') } elseif ($Affection) { @('j15_affection','j15_world') } elseif ($Scythe) { @('j14_mercy','j14_salvation') } elseif ($SpiritSword) { @('j13_golden_qi','j13_great_draw','j13_divine_descent') } else { @('j12_capsule_sweep') }
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
