# J12：胶囊扫掠、全局实际弹速上限与五探针回归，验证独占且保护真实存档。
param([switch]$ProbeOnly, [switch]$SpiritSword, [switch]$Scythe, [switch]$Affection, [switch]$Rainbow, [switch]$GoldenRoad, [switch]$LightLeap, [switch]$GroundBurst, [switch]$ActionMovement, [switch]$ChargeEvolution, [switch]$JumpStomp, [switch]$BowExitAudit, [switch]$RewardRules, [switch]$RewardTiers, [switch]$ChargeInputAudit, [switch]$KillJumps, [switch]$KillFacts, [switch]$WingDive, [switch]$ProjectileQuery, [switch]$AttackSnapshot, [switch]$DotAttribution, [switch]$RuneUi, [switch]$Specialization, [switch]$BodyCollision, [switch]$MainOnly, [switch]$FlightPhysics, [switch]$NavigationFoundation, [switch]$EnemyRoster, [switch]$LabOnly, [switch]$EnemyWarnings, [switch]$CombatFeedback, [switch]$LoadoutSlots, [switch]$AimAndThrow)
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
$EnemyRoster = $EnemyRoster -or $EnemyWarnings -or $CombatFeedback -or $LoadoutSlots -or $AimAndThrow
if ($ProjectileQuery) { $batchId = 'j28' }
if ($AttackSnapshot) { $batchId = 'j29' }
if ($DotAttribution) { $batchId = 'j30' }
if ($RuneUi) { $batchId = 'j31' }
if ($Specialization) { $batchId = 'j32' }
if ($BodyCollision) { $batchId = 'j33' }
if ($FlightPhysics) { $batchId = 'j34' }
if ($NavigationFoundation) { $batchId = 'j35' }
if ($EnemyRoster) { $batchId = 'j36' }
if ($EnemyWarnings) { $batchId = 'j37' }
if ($CombatFeedback) { $batchId = 'j38' }
if ($LoadoutSlots) { $batchId = 'j39' }
if ($AimAndThrow) { $batchId = 'j40' }
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

function Invoke-Probe([string]$RunName, [string]$Scene, [string]$Expected, [switch]$Render, [switch]$SelfTest, [string[]]$UserArgs = @()) {
    $stdoutPath = Join-Path $projectDir ".ai_$($batchId)_$runTag.$RunName.log"
    $stderrPath = Join-Path $projectDir ".ai_$($batchId)_$runTag.$RunName.err"
    $runArgs = @('--path',$projectDir,'--quit-after','40000',$Scene)
    if ($Render) { $runArgs += @('--resolution','1600x900') } else { $runArgs += '--headless' }
    if ($SelfTest) { $runArgs += @('--','--selftest') }
    elseif ($UserArgs.Count -gt 0) { $runArgs += '--'; $runArgs += $UserArgs }
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
            if ($EnemyRoster) {
                foreach ($line in ($out -split "`n" | Where-Object { $_.Contains('🔴') })) {
                    if ($line -notmatch '🔴 (白板首领闸门·正向：|白板首领实例：|Boss 阵亡 →|互动降临节点：|最小闭环·链路：)') {
                        throw "Main新增红标：$line"
                    }
                }
            }
            if (($BodyCollision -or $FlightPhysics -or $NavigationFoundation -or $EnemyRoster) -and $out -match '🔴 J8') { throw 'Main J8真实命中回归失败；保留日志，不以八指标代替技能断言。' }
            if (($SpiritSword -or $Scythe -or $Affection -or $Rainbow -or $GoldenRoad -or $LightLeap -or $GroundBurst -or $ActionMovement -or $ChargeEvolution -or $JumpStomp -or $RewardRules) -and $out -notmatch '✅ P5b·实测表参数落到机制上') { throw 'Main专属技能增强契约探针未通过' }
            $markers = @('配置表：武器 6','节点布置：启用','配置表比对·玩家：','配置表比对·索引：','Pressure 测试（进化','白板首领实例：','端到端：按 F 之后','最小闭环·链路：')
            $baseline = Get-Content -LiteralPath (Join-Path $projectDir 'docs\character-design-j11-evidence.log')
            foreach ($marker in $markers) {
                $before = ($baseline | Where-Object { $_.StartsWith('[log] ') -and $_.Contains($marker) } | Select-Object -First 1) -replace '^\[log\] \S+ ', ''
                $after = ($out -split "`n" | Where-Object { $_.StartsWith('[log] ') -and $_.Contains($marker) } | Select-Object -First 1) -replace '^\[log\] \S+ ', ''
                if ($EnemyRoster -and $marker -eq '配置表：武器 6') { $before = $before.Replace('敌人 10 /','敌人 21 /') }
                if ($EnemyRoster -and $marker -eq '配置表比对·索引：') {
                    # 校验全部14项实际数值；历史解释文字已改写为J36现状。
                    $before = $before.Replace('/敌人 10/','/敌人 21/').Split('（期望')[0]
                    $after = $after.Split('（期望')[0]
                }
                if ([string]::IsNullOrWhiteSpace($before) -or $before.TrimEnd() -cne $after.TrimEnd()) { throw "硬指标变化：$marker" }
                $note = if ($EnemyRoster) { 'J11基线比对；J36仅敌人数10→21' } else { 'J11逐字一致' }
                $evidence.Add("[$note] $($after.TrimEnd())")
            }
            Write-Output 'Main 八条硬指标通过（J36仅敌人数10→21，其余与J11一致；索引14项全校验）'
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
    if ($LabOnly) {
        if (-not $EnemyRoster) { throw 'LabOnly仅用于J36／J37渲染定向复核。' }
        if ($LoadoutSlots) { Invoke-Probe 'slots_render' 'res://Scenes/Debug/_LoadoutSlotsProbe.tscn' '换装与技能槽验收结束：通过 \d+，失败 0' -Render }
        elseif ($CombatFeedback) { Invoke-Probe 'feedback_render' 'res://Scenes/Debug/_CombatFeedbackProbe.tscn' '打击反馈验收结束：通过 \d+，失败 0' -Render }
        elseif ($EnemyWarnings) { Invoke-Probe 'warnings_render' 'res://Scenes/Debug/_EnemyWarningProbe.tscn' '敌人预警验收结束：通过 \d+，失败 0' -Render }
        else { Invoke-Probe 'roster_lab' 'res://Scenes/Debug/_EnemyRosterLab.tscn' '截图=Ok／闪色=Ok' -Render -UserArgs '--roster-lab-capture' }
    }
    elseif ($MainOnly) {
        if ((-not $BodyCollision -and -not $FlightPhysics -and -not $EnemyRoster) -or $ProbeOnly) { throw 'MainOnly仅用于J33／J34／J36完整回归后的定向复核。' }
        if ($EnemyRoster) { Invoke-Probe 'roster_lab' 'res://Scenes/Debug/_EnemyRosterLab.tscn' '截图=Ok／闪色=Ok' -Render -UserArgs '--roster-lab-capture' }
        if ($FlightPhysics) { Invoke-Probe 'flight' 'res://Scenes/Debug/_FlightPhysicsProbe.tscn' '飞行与受力验收结束：通过 \d+，失败 0' }
        Invoke-Probe 'main' 'res://Scenes/Main.tscn' '=== 自测结束 ===' -SelfTest
    }
    elseif ($AimAndThrow) {
        Invoke-Probe 'aim_throw' 'res://Scenes/Debug/_AimAndThrowProbe.tscn' '立体瞄准与投掷验收结束：通过 \d+，失败 0'
        if (-not $ProbeOnly) {
            Invoke-Probe 'aim_throw_render' 'res://Scenes/Debug/_AimAndThrowProbe.tscn' '立体瞄准与投掷验收结束：通过 \d+，失败 0' -Render
            Invoke-Probe 'slots' 'res://Scenes/Debug/_LoadoutSlotsProbe.tscn' '换装与技能槽验收结束：通过 57，失败 0'
            Invoke-Probe 'snapshot' 'res://Scenes/Debug/_AttackSnapshotProbe.tscn' '攻击快照验收结束：通过 45，失败 0'
            Invoke-Probe 'scythe' 'res://Scenes/Debug/_ScytheEvolutionProbe.tscn' '镰刀末刀进化验收结束：通过 36，失败 0'
            Invoke-Probe 'seek' 'res://Scenes/Debug/_ScytheSeekProbe.tscn' '飞镰喜爱验收结束：通过 42，失败 0'
            Invoke-Probe 'spear' 'res://Scenes/Debug/_SpearRecallProbe.tscn' '气贯长虹验收结束：通过 89，失败 0'
            Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0'
        }
    }
    elseif ($LoadoutSlots) {
        Invoke-Probe 'slots' 'res://Scenes/Debug/_LoadoutSlotsProbe.tscn' '换装与技能槽验收结束：通过 \d+，失败 0'
        if (-not $ProbeOnly) {
            Invoke-Probe 'slots_render' 'res://Scenes/Debug/_LoadoutSlotsProbe.tscn' '换装与技能槽验收结束：通过 \d+，失败 0' -Render
            Invoke-Probe 'feedback' 'res://Scenes/Debug/_CombatFeedbackProbe.tscn' '打击反馈验收结束：通过 41，失败 0'
            Invoke-Probe 'action' 'res://Scenes/Debug/_ActionInterruptionProbe.tscn' '动作交接验收结束：通过 \d+，失败 0'
            Invoke-Probe 'snapshot' 'res://Scenes/Debug/_AttackSnapshotProbe.tscn' '攻击快照验收结束：通过 45，失败 0'
            Invoke-Probe 'specialization' 'res://Scenes/Debug/_SpecializationProbe.tscn' '特化验收结束：通过 194，失败 0'
            Invoke-Probe 'sword' 'res://Scenes/Debug/_SpiritSwordEvolutionProbe.tscn' '灵剑进化验收结束：通过 \d+，失败 0'
            Invoke-Probe 'scythe' 'res://Scenes/Debug/_ScytheEvolutionProbe.tscn' '镰刀末刀进化验收结束：通过 36，失败 0'
            Invoke-Probe 'spear' 'res://Scenes/Debug/_SpearRecallProbe.tscn' '气贯长虹验收结束：通过 89，失败 0'
            Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0'
        }
    }
    elseif ($CombatFeedback) {
        Invoke-Probe 'feedback' 'res://Scenes/Debug/_CombatFeedbackProbe.tscn' '打击反馈验收结束：通过 \d+，失败 0'
        if (-not $ProbeOnly) {
            Invoke-Probe 'feedback_render' 'res://Scenes/Debug/_CombatFeedbackProbe.tscn' '打击反馈验收结束：通过 \d+，失败 0' -Render
            Invoke-Probe 'warnings' 'res://Scenes/Debug/_EnemyWarningProbe.tscn' '敌人预警验收结束：通过 40，失败 0'
            Invoke-Probe 'roster' 'res://Scenes/Debug/_EnemyRosterProbe.tscn' '敌人清单验收结束：通过 95，失败 0'
            Invoke-Probe 'collision' 'res://Scenes/Debug/_BatchDProbe.tscn' 'D批验收结束：通过 91，失败 0'
            Invoke-Probe 'projectile_query' 'res://Scenes/Debug/_ProjectileQueryProbe.tscn' '弹道查询验收结束：通过 56，失败 0'
            Invoke-Probe 'attack_snapshot' 'res://Scenes/Debug/_AttackSnapshotProbe.tscn' '验收结束：通过 45，失败 0'
            Invoke-Probe 'dot_attribution' 'res://Scenes/Debug/_DotAttributionProbe.tscn' '验收结束：通过 26，失败 0'
            Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0'
        }
    }
    elseif ($EnemyWarnings) {
        Invoke-Probe 'warnings' 'res://Scenes/Debug/_EnemyWarningProbe.tscn' '敌人预警验收结束：通过 \d+，失败 0'
        if (-not $ProbeOnly) {
            Invoke-Probe 'warnings_render' 'res://Scenes/Debug/_EnemyWarningProbe.tscn' '敌人预警验收结束：通过 \d+，失败 0' -Render
            Invoke-Probe 'roster' 'res://Scenes/Debug/_EnemyRosterProbe.tscn' '敌人清单验收结束：通过 95，失败 0'
            Invoke-Probe 'collision' 'res://Scenes/Debug/_BatchDProbe.tscn' 'D批验收结束：通过 91，失败 0'
            Invoke-Probe 'projectile_query' 'res://Scenes/Debug/_ProjectileQueryProbe.tscn' '弹道查询验收结束：通过 56，失败 0'
            Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0'
        }
    }
    elseif ($EnemyRoster) {
        Invoke-Probe 'roster' 'res://Scenes/Debug/_EnemyRosterProbe.tscn' '敌人清单验收结束：通过 \d+，失败 0'
        if (-not $ProbeOnly) {
            Invoke-Probe 'roster_lab' 'res://Scenes/Debug/_EnemyRosterLab.tscn' '截图=Ok／闪色=Ok' -Render -UserArgs '--roster-lab-capture'
            Invoke-Probe 'navigation' 'res://Scenes/Debug/_NavigationFoundationProbe.tscn' '导航基础验收结束：通过 \d+，失败 0'
            Invoke-Probe 'collision' 'res://Scenes/Debug/_BatchDProbe.tscn' 'D批验收结束：通过 91，失败 0'
            Invoke-Probe 'body' 'res://Scenes/Debug/_BodyCollisionProbe.tscn' '身体分层验收结束：通过 31，失败 0'
            Invoke-Probe 'flight' 'res://Scenes/Debug/_FlightPhysicsProbe.tscn' '飞行与受力验收结束：通过 42，失败 0'
            Invoke-Probe 'projectile_query' 'res://Scenes/Debug/_ProjectileQueryProbe.tscn' '弹道查询验收结束：通过 56，失败 0'
            Invoke-Probe 'specialization' 'res://Scenes/Debug/_SpecializationProbe.tscn' '特化验收结束：通过 194，失败 0'
            Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0'
        }
    }
    elseif ($NavigationFoundation) {
        Invoke-Probe 'navigation' 'res://Scenes/Debug/_NavigationFoundationProbe.tscn' '导航基础验收结束：通过 \d+，失败 0'
        if (-not $ProbeOnly) {
            Invoke-Probe 'collision' 'res://Scenes/Debug/_BatchDProbe.tscn' 'D批验收结束：通过 91，失败 0'
            Invoke-Probe 'body' 'res://Scenes/Debug/_BodyCollisionProbe.tscn' '身体分层验收结束：通过 31，失败 0'
            Invoke-Probe 'flight' 'res://Scenes/Debug/_FlightPhysicsProbe.tscn' '飞行与受力验收结束：通过 42，失败 0'
            Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0'
        }
    }
    elseif ($FlightPhysics) {
        Invoke-Probe 'flight' 'res://Scenes/Debug/_FlightPhysicsProbe.tscn' '飞行与受力验收结束：通过 \d+，失败 0'
        if (-not $ProbeOnly) {
            Invoke-Probe 'flight_lab' 'res://Scenes/Debug/_FlightPhysicsLab.tscn' '截图=Ok' -Render -UserArgs '--flight-lab-capture'
            Invoke-Probe 'body' 'res://Scenes/Debug/_BodyCollisionProbe.tscn' '身体分层验收结束：通过 31，失败 0'
            Invoke-Probe 'collision' 'res://Scenes/Debug/_BatchDProbe.tscn' 'D批验收结束：通过 91，失败 0'
            Invoke-Probe 'movement' 'res://Scenes/Debug/_ActionMovementProbe.tscn' '动作移动验收结束：通过 74，失败 0'
            Invoke-Probe 'jump_stomp' 'res://Scenes/Debug/_JumpStompProbe.tscn' '起跳践踏验收结束：通过 39，失败 0'
            Invoke-Probe 'landing' 'res://Scenes/Debug/_LandingKnockbackProbe.tscn' '地爆验收结束：通过 53，失败 0'
            Invoke-Probe 'specialization' 'res://Scenes/Debug/_SpecializationProbe.tscn' '特化验收结束：通过 194，失败 0'
            Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0'
        }
    }
    elseif ($BodyCollision) {
        Invoke-Probe 'body' 'res://Scenes/Debug/_BodyCollisionProbe.tscn' '身体分层验收结束：通过 \d+，失败 0'
        if (-not $ProbeOnly) {
            Invoke-Probe 'collision' 'res://Scenes/Debug/_BatchDProbe.tscn' 'D批验收结束：通过 91，失败 0'
            Invoke-Probe 'specialization' 'res://Scenes/Debug/_SpecializationProbe.tscn' '特化验收结束：通过 194，失败 0'
            Invoke-Probe 'projectile_query' 'res://Scenes/Debug/_ProjectileQueryProbe.tscn' '弹道查询验收结束：通过 56，失败 0'
            Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0'
        }
    }
    elseif ($Specialization) {
        Invoke-Probe 'specialization' 'res://Scenes/Debug/_SpecializationProbe.tscn' '特化验收结束：通过 \d+，失败 0'
        Invoke-Probe 'specialization_render' 'res://Scenes/Debug/_SpecializationProbe.tscn' '特化验收结束：通过 \d+，失败 0' -Render
        if (-not $ProbeOnly) {
            Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0'
            Invoke-Probe 'attack_snapshot' 'res://Scenes/Debug/_AttackSnapshotProbe.tscn' '攻击快照验收结束：通过 45，失败 0'
            Invoke-Probe 'dot_source' 'res://Scenes/Debug/_DotAttributionProbe.tscn' '持续伤害归属验收结束：通过 26，失败 0'
            Invoke-Probe 'reward_rules' 'res://Scenes/Debug/_RewardRulesProbe.tscn' '获取规则验收结束：通过 78，失败 0'
            Invoke-Probe 'rune_ui' 'res://Scenes/Debug/_RuneUiProbe.tscn' '符文界面验收结束：通过 42，失败 0'
            Invoke-Probe 'rune_ui_render' 'res://Scenes/Debug/_RuneUiProbe.tscn' '符文界面验收结束：通过 43，失败 0' -Render
        }
    }
    elseif ($RuneUi) {
        $ownedProcess = Start-Process -FilePath $godotExe -ArgumentList @('--headless','--path',$projectDir,'--editor','--import') -WorkingDirectory $projectDir -WindowStyle Hidden -RedirectStandardOutput ".ai_$($batchId)_$runTag.import.log" -RedirectStandardError ".ai_$($batchId)_$runTag.import.err" -PassThru
        if (-not $ownedProcess.WaitForExit(60000)) { Stop-Process -Id $ownedProcess.Id; throw '资源导入超时' }
        if ($ownedProcess.ExitCode -ne 0) { throw '资源导入失败' }
        $ownedProcess = $null
        Invoke-Probe 'rune_ui' 'res://Scenes/Debug/_RuneUiProbe.tscn' '符文界面验收结束：通过 42，失败 0'
        Invoke-Probe 'rune_ui_render' 'res://Scenes/Debug/_RuneUiProbe.tscn' '符文界面验收结束：通过 43，失败 0' -Render
    }
    elseif ($DotAttribution) {
        Invoke-Probe 'dot_source' 'res://Scenes/Debug/_DotAttributionProbe.tscn' '持续伤害归属验收结束：通过 26，失败 0'
        if (-not $ProbeOnly) {
            Invoke-Probe 'kill_facts' 'res://Scenes/Debug/_KillFactProbe.tscn' '逐杀事实验收结束：通过 40，失败 0'
            Invoke-Probe 'status' 'res://Scenes/UI/_BatchEProbe.tscn' 'E 批验收结束：通过 39，失败 0'
            Invoke-Probe 'attack_snapshot' 'res://Scenes/Debug/_AttackSnapshotProbe.tscn' '攻击快照验收结束：通过 45，失败 0'
            Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0'
        }
    }
    elseif ($AttackSnapshot) {
        Invoke-Probe 'attack_snapshot' 'res://Scenes/Debug/_AttackSnapshotProbe.tscn' '攻击快照验收结束：通过 45，失败 0'
        if (-not $ProbeOnly) {
            Invoke-Probe 'projectile_query' 'res://Scenes/Debug/_ProjectileQueryProbe.tscn' '弹道查询验收结束：通过 56，失败 0'
            Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0'
            Invoke-Probe 'sword' 'res://Scenes/Debug/_SpiritSwordEvolutionProbe.tscn' '灵剑进化验收结束：通过 71，失败 0'
            Invoke-Probe 'scythe' 'res://Scenes/Debug/_ScytheEvolutionProbe.tscn' '镰刀末刀进化验收结束：通过 36，失败 0'
            Invoke-Probe 'rainbow' 'res://Scenes/Debug/_SpearRecallProbe.tscn' '气贯长虹验收结束：通过 89，失败 0'
            Invoke-Probe 'landing' 'res://Scenes/Debug/_LandingKnockbackProbe.tscn' '地爆验收结束：通过 53，失败 0'
            Invoke-Probe 'wing_dive' 'res://Scenes/Debug/_WingDiveProbe.tscn' '飞翼下落验收结束：通过 62，失败 0'
        }
    }
    elseif ($ProjectileQuery) {
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
    if (-not $ProbeOnly -and -not $MainOnly) {
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
        if ($ActionMovement -or $ChargeEvolution -or $JumpStomp -or $RewardRules) { Invoke-Probe 'collision' 'res://Scenes/Debug/_BatchDProbe.tscn' 'D批验收结束：通过 91，失败 0' }
        Invoke-Probe 'charge' 'res://Scenes/Debug/_ChargeReleaseProbe.tscn' '蓄力释放验收结束：通过 80，失败 0'
        Invoke-Probe 'character' 'res://Scenes/Debug/_CharacterDesignProbe.tscn' '原始设计验收结束：通过 115，失败 0'
        Invoke-Probe 'dragon' 'res://Scenes/Debug/_DragonArrowProbe.tscn' '验收结束：通过 42，失败 0'
        Invoke-Probe 'rune' 'res://Scenes/Debug/_RuneProbe.tscn' '验收结束：通过 83，失败 0'
        Invoke-Probe 'main' 'res://Scenes/Main.tscn' '=== 自测结束 ===' -SelfTest
    }
    $shotNames = if ($BowExitAudit -or $ChargeInputAudit -or $KillJumps -or $KillFacts -or $WingDive) { @() } elseif ($RewardTiers) { @('j24_reward_tiers') } elseif ($RewardRules) { @() } elseif ($JumpStomp) { @('j22_jump_stomp') } elseif ($ChargeEvolution) { @('j21_distinct_homing','j21_flow') } elseif ($ActionMovement) { @('j20_action_movement') } elseif ($GroundBurst) { @('j19_ground_burst') } elseif ($LightLeap) { @('j18_light_leap') } elseif ($GoldenRoad) { @('j17_golden_road') } elseif ($Rainbow) { @('j16_rainbow') } elseif ($Affection) { @('j15_affection','j15_world') } elseif ($Scythe) { @('j14_mercy','j14_salvation') } elseif ($SpiritSword) { @('j13_golden_qi','j13_great_draw','j13_divine_descent') } else { @('j12_capsule_sweep') }
    foreach ($shotName in $shotNames) {
        if ($Specialization -or $RuneUi -or $BodyCollision -or $FlightPhysics -or $NavigationFoundation -or $EnemyRoster) { break }
        $shotPath = Join-Path $projectDir "docs\screenshots\$shotName.png"
        if (-not (Test-Path -LiteralPath $shotPath)) { throw "$shotName 截图缺失" }
        $evidence.Add("截图 $shotName SHA256=$((Get-FileHash -LiteralPath $shotPath -Algorithm SHA256).Hash)")
    }
    $currentShots = if ($AimAndThrow) { if (-not $ProbeOnly) { @('screenshots/j40_spear_aim.png','screenshots/j40_crossbow_aim.png') } else { @() } } elseif ($LoadoutSlots -and (-not $ProbeOnly -or $LabOnly)) { @('screenshots/j39_contact_visibility.png','screenshots/j39_spear_preview.png') } elseif ($CombatFeedback -and (-not $ProbeOnly -or $LabOnly)) { @('screenshots/j38_combat_feedback.png') } elseif ($EnemyWarnings -and (-not $ProbeOnly -or $LabOnly)) { @('screenshots/j37_enemy_warning.png') } elseif ($EnemyRoster -and (-not $ProbeOnly -or $LabOnly)) { @('screenshots/j36_enemy_roster.png','screenshots/j36_attack_warning.png') } elseif ($Specialization) { @('character-design-j32-wind.png','character-design-j32-rune-ui.png') } elseif ($RuneUi) { @('character-design-j31-rune-ui.png') } else { @() }
    foreach ($shot in $currentShots) {
        # ProbeOnly不一定运行符文渲染，只有本次完整流程要求这张界面图。
        if ($Specialization -and $ProbeOnly -and $shot -like '*rune-ui*') { continue }
        $path = Join-Path $projectDir "docs\$shot"
        if (-not (Test-Path -LiteralPath $path)) { throw "$shot 截图缺失" }
        $evidence.Add("截图 $shot SHA256=$((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash)")
    }
    Save-Fingerprint 'AFTER'
    $evidence.Add("selftest.flag AFTER=$(Test-Path -LiteralPath (Join-Path $profileDir 'selftest.flag'))")
    $outputName = if ($MainOnly) { "character-design-$batchId-main-$runTag.log" } elseif ($ProbeOnly) { "character-design-$batchId-probe-$runTag.log" } else { "character-design-$batchId-evidence.log" }
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
