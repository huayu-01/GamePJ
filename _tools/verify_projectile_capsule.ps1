# J12：胶囊扫掠、全局实际弹速上限与五探针回归，验证独占且保护真实存档。
param([switch]$ProbeOnly, [switch]$SpiritSword, [switch]$Scythe, [switch]$Affection, [switch]$Rainbow, [switch]$GoldenRoad, [switch]$LightLeap, [switch]$GroundBurst, [switch]$ActionMovement, [switch]$ChargeEvolution, [switch]$JumpStomp, [switch]$BowExitAudit, [switch]$RewardRules, [switch]$RewardTiers, [switch]$ChargeInputAudit, [switch]$KillJumps, [switch]$KillFacts, [switch]$WingDive, [switch]$ProjectileQuery, [switch]$AttackSnapshot, [switch]$DotAttribution, [switch]$RuneUi, [switch]$Specialization, [switch]$BodyCollision, [switch]$MainOnly, [switch]$FlightPhysics, [switch]$NavigationFoundation, [switch]$EnemyRoster, [switch]$LabOnly, [switch]$EnemyWarnings, [switch]$CombatFeedback, [switch]$LoadoutSlots, [switch]$AimAndThrow, [switch]$EarlyCharge, [switch]$SpiritSwords, [switch]$SpiritSwordRush, [switch]$PlayerExperience, [switch]$FlowRobustness, [switch]$KnifeVolley, [switch]$InteractionComponents, [switch]$InteractionModels, [switch]$ChallengeFeedback)
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
. (Join-Path $projectDir '_tools/LogPaths.ps1')
Initialize-ProjectLogPaths -ProjectRoot $projectDir
. (Join-Path $PSScriptRoot 'Validation.Common.ps1')
$godotExe = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profileDir = Join-Path $env:APPDATA 'Godot\app_userdata\ACT Roguelike'
$evidence = [Collections.Generic.List[string]]::new()
$lease = $null
$ownedProcess = $null
$original = @{}
$runTag = Get-Date -Format 'yyyyMMdd_HHmmss'
$batchId = if ($WingDive) { 'j27' } elseif ($KillFacts) { 'j26' } elseif ($KillJumps) { 'j25' } elseif ($RewardTiers) { 'j24' } elseif ($RewardRules) { 'j23' } elseif ($JumpStomp) { 'j22' } elseif ($ChargeEvolution) { 'j21' } elseif ($ActionMovement) { 'j20' } elseif ($GroundBurst) { 'j19' } elseif ($LightLeap) { 'j18' } elseif ($GoldenRoad) { 'j17' } elseif ($Rainbow) { 'j16' } elseif ($Affection) { 'j15' } elseif ($Scythe) { 'j14' } elseif ($SpiritSword) { 'j13' } else { 'j12' }
$SpiritSwords = $SpiritSwords -or $SpiritSwordRush
$RewardRules = $RewardRules -or $RewardTiers -or $KillJumps
$InteractionModels = $InteractionModels -or $ChallengeFeedback
$InteractionComponents = $InteractionComponents -or $InteractionModels
$FlowRobustness = $FlowRobustness -or $InteractionComponents
$PlayerExperience = $PlayerExperience -or $FlowRobustness
$EnemyRoster = $EnemyRoster -or $EnemyWarnings -or $CombatFeedback -or $LoadoutSlots -or $AimAndThrow -or $EarlyCharge -or $SpiritSwords -or $PlayerExperience -or $KnifeVolley
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
if ($EarlyCharge) { $batchId = 'j41' }
if ($SpiritSwords) { $batchId = if ($SpiritSwordRush) { 'j43' } else { 'j42' } }
if ($PlayerExperience) { $batchId = 'j44' }
if ($FlowRobustness) { $batchId = 'j45' }
if ($KnifeVolley) { $batchId = 'knife-volley' }
if ($InteractionComponents) { $batchId = 'j46' }
if ($InteractionModels) { $batchId = 'j47' }
if ($ChallengeFeedback) { $batchId = 'j48' }
$saveNames = @('meta.cfg','ability_cards.cfg','settings.cfg','loadout.cfg','rune.cfg')
$utf8 = [Text.UTF8Encoding]::new($false)

function Save-Fingerprint([string]$Phase) {
    foreach ($name in $saveNames) {
        $path = Join-Path $profileDir $name
        $hash = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { 'ABSENT' }
        $evidence.Add("存档 $Phase $name SHA256=$hash")
    }
}

function Assert-Saves { Assert-ValidationSaves $profileDir $original }

function Invoke-Probe([string]$RunName, [string]$Scene, [string]$Expected, [switch]$Render, [switch]$SelfTest, [switch]$EditorScene, [string[]]$UserArgs = @()) {
    $stdoutPath = Join-Path $projectDir "Logs/validation/.ai_$($batchId)_$runTag.$RunName.log"
    $stderrPath = Join-Path $projectDir "Logs/validation/.ai_$($batchId)_$runTag.$RunName.err"
    $runArgs = @('--path',$projectDir,'--log-file',(Join-Path $projectDir "Logs/validation/.ai_$($batchId)_$runTag.$RunName.engine.log"),'--quit-after','40000',$Scene)
    if ($Render) { $runArgs += @('--resolution','1600x900') } else { $runArgs += '--headless' }
    if ($EditorScene) {
        # 在隔离工程中验证相同源码和程序集，避免启用用户编辑器插件或争用其MCP端口。
        $editorMirror = New-ValidationMirror $projectDir 'interaction_editor' -ShareAssembly
        $editorProject = $editorMirror.Project
        Copy-Item -LiteralPath (Join-Path $projectDir '_tools/interaction_editor_probe.gd') -Destination (Join-Path $editorProject '_tools')
        $editorConfig = "config_version=5`n[application]`nconfig/name=`"Interaction Editor Probe`"`nconfig/features=PackedStringArray(`"4.7`",`"C#`")`n[dotnet]`nproject/assembly_name=`"ActRoguelike`"`n"
        [IO.File]::WriteAllText((Join-Path $editorProject 'project.godot'),$editorConfig,$utf8)
        $runArgs = @('--headless','--editor','--path',$editorProject,$Scene)
    }
    if ($SelfTest) { $runArgs += @('--','--selftest') }
    elseif ($UserArgs.Count -gt 0) { $runArgs += '--'; $runArgs += $UserArgs }
    try {
        $result = Invoke-ValidationEngine $godotExe $runArgs $projectDir $stdoutPath $stderrPath 180
        $out = $result.Output
        $evidence.Add("$RunName exit=$($result.ExitCode) seconds=$($result.Seconds) stderrBytes=0")
        $evidence.Add("=== $RunName ==="); $evidence.Add($out)
        if ($out -notmatch $Expected) { throw "$RunName 未满足完成断言：$stdoutPath" }
        Write-Output "PASS $RunName"
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
            $evidence.AddRange([string[]](Assert-MainValidation $projectDir $out))
            Write-Output 'Main八条硬指标、内容基线和历史红标核对通过'

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
    $lease = Open-ProjectValidationLease $projectDir 900
    $active = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -like 'Godot*' -and $_.CommandLine -notmatch '--editor' })
    if ($active.Count -gt 0) { throw "发现已有 Godot 游戏进程：$($active.ProcessId -join ',')，未停止它们。" }
    if (Test-Path -LiteralPath (Join-Path $profileDir 'selftest.flag')) { throw '已有 selftest.flag，需先核实归属。' }
    foreach ($name in $saveNames) {
        $path = Join-Path $profileDir $name
        $original[$name] = if (Test-Path -LiteralPath $path) { [Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) } else { $null }
    }
    [IO.File]::WriteAllText((Join-Path $projectDir "Logs/validation/.ai_$($batchId)_$runTag.saves.json"),($original | ConvertTo-Json),$utf8)
    Save-Fingerprint 'BEFORE'
    foreach ($configuration in @('Debug','ExportRelease')) {
        $evidence.Add((Invoke-ValidationBuild $projectDir $configuration (Join-Path $projectDir "Logs/validation/.ai_$($batchId)_$runTag.$configuration.log")))
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
        if ($EnemyRoster -and -not $SpiritSwords -and -not $KnifeVolley) { Invoke-Probe 'roster_lab' 'res://Scenes/Debug/_EnemyRosterLab.tscn' '截图=Ok／闪色=Ok' -Render -UserArgs '--roster-lab-capture' }
        if ($FlightPhysics) { Invoke-Probe 'flight' 'res://Scenes/Debug/_FlightPhysicsProbe.tscn' '飞行与受力验收结束：通过 \d+，失败 0' }
        Invoke-Probe 'main' 'res://Scenes/Main.tscn' '=== 自测结束 ===' -SelfTest
    }
    elseif ($KnifeVolley) {
        Invoke-Probe 'knife_volley' 'res://Scenes/Debug/_KnifeVolleyProbe.tscn' '飞刀散射验收结束：通过 \d+，失败 0'
        if (-not $ProbeOnly) {
            Invoke-Probe 'charge_evolution' 'res://Scenes/Debug/_ChargeEvolutionProbe.tscn' '蓄力进化验收结束：通过 151，失败 0'
            Invoke-Probe 'aim_throw' 'res://Scenes/Debug/_AimAndThrowProbe.tscn' '立体瞄准与投掷验收结束：通过 33，失败 0'
            Invoke-Probe 'slots' 'res://Scenes/Debug/_LoadoutSlotsProbe.tscn' '换装与技能槽验收结束：通过 57，失败 0'
            Invoke-Probe 'snapshot' 'res://Scenes/Debug/_AttackSnapshotProbe.tscn' '攻击快照验收结束：通过 45，失败 0'
            Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0'
        }
    }
    elseif ($PlayerExperience) {
        if ($ChallengeFeedback) { Invoke-Probe 'challenge_feedback' 'res://Scenes/Debug/_ChallengeFeedbackProbe.tscn' '挑战状态与拒绝反馈验收结束：通过 \d+，失败 0' -UserArgs '--challenge-feedback' }
        if ($InteractionModels) { Invoke-Probe 'interaction_editor' 'res://Scenes/Debug/_InteractionEditorProbe.tscn' '编辑器模型验收结束：通过 6，失败 0' -EditorScene -UserArgs '--interaction-editor-check' }
        if ($InteractionComponents) { Invoke-Probe 'interaction_component' 'res://Scenes/Debug/_InteractionComponentProbe.tscn' '交互组件验收结束：通过 \d+，失败 0' }
        if ($FlowRobustness) { Invoke-Probe 'spawn_flow' 'res://Scenes/Debug/_SpawnContinuityProbe.tscn' '增援与召唤验收结束：通过 \d+，失败 0' }
        $experienceArgs = if ($ChallengeFeedback) { @('--challenge-feedback','--interaction-models','--interaction-components') } elseif ($InteractionModels) { @('--interaction-models','--interaction-components') } elseif ($InteractionComponents) { @('--interaction-components') } elseif ($FlowRobustness) { @('--flow-robustness') } else { @() }
        Invoke-Probe 'run_flow' 'res://Scenes/Debug/_RunFlowContinuityProbe.tscn' '整局与离场验收结束：通过 \d+，失败 0'
        Invoke-Probe 'player_flow' 'res://Scenes/Debug/_PlayerExperienceProbe.tscn' '玩家流程验收结束：通过 \d+，失败 0' -UserArgs $experienceArgs
        if ($InteractionModels) {
            Invoke-Probe 'base_input' 'res://Scenes/Debug/_BatchCProbe.tscn' 'C批验收结束：通过 \d+，失败 0' -UserArgs '--loadout-test'
            Invoke-Probe 'character_visual' 'res://Scenes/Debug/_CharacterReplacementProbe.tscn' '完成：通过 \d+，失败 0' -UserArgs '--loadout-test'
        }
        if (-not $ProbeOnly) {
            if ($ChallengeFeedback) { Invoke-Probe 'challenge_feedback_render' 'res://Scenes/Debug/_ChallengeFeedbackProbe.tscn' '挑战状态与拒绝反馈验收结束：通过 \d+，失败 0' -Render -UserArgs '--challenge-feedback' }
            if ($InteractionComponents) { Invoke-Probe 'interaction_lab' 'res://Scenes/Debug/InteractionComponentLab.tscn' '组件示例验收：组件 9／后端 9／新模型 9／触发器 2／截图 Ok' -Render -UserArgs (@('--interaction-lab-capture') + $experienceArgs) }
            Invoke-Probe 'player_flow_render' 'res://Scenes/Debug/_PlayerExperienceProbe.tscn' '玩家流程验收结束：通过 \d+，失败 0' -Render -UserArgs $experienceArgs
            Invoke-Probe 'navigation' 'res://Scenes/Debug/_NavigationFoundationProbe.tscn' '导航基础验收结束：通过 \d+，失败 0'
            Invoke-Probe 'roster' 'res://Scenes/Debug/_EnemyRosterProbe.tscn' '敌人清单验收结束：通过 \d+，失败 0'
            Invoke-Probe 'rune_ui' 'res://Scenes/Debug/_RuneUiProbe.tscn' '符文界面验收结束：通过 \d+，失败 0'
            if ($FlowRobustness) { Invoke-Probe 'rune_ui_render' 'res://Scenes/Debug/_RuneUiProbe.tscn' '符文界面验收结束：通过 43，失败 0' -Render -UserArgs $experienceArgs }
            Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0'
        }
    }
    elseif ($SpiritSwords) {
        Invoke-Probe 'spirit_swords' 'res://Scenes/Debug/_SpiritSwordsProbe.tscn' '三剑协同验收结束：通过 \d+，失败 0'
        if (-not $ProbeOnly) {
            Invoke-Probe 'spirit_swords_render' 'res://Scenes/Debug/_SpiritSwordsProbe.tscn' '三剑协同验收结束：通过 \d+，失败 0' -Render
            Invoke-Probe 'sword_evolution' 'res://Scenes/Debug/_SpiritSwordEvolutionProbe.tscn' '灵剑进化验收结束：通过 \d+，失败 0'
            Invoke-Probe 'snapshot' 'res://Scenes/Debug/_AttackSnapshotProbe.tscn' '攻击快照验收结束：通过 45，失败 0'
            Invoke-Probe 'early_charge' 'res://Scenes/Debug/_EarlyChargeProbe.tscn' '未蓄满释放验收结束：通过 37，失败 0'
            Invoke-Probe 'slots' 'res://Scenes/Debug/_LoadoutSlotsProbe.tscn' '换装与技能槽验收结束：通过 57，失败 0'
            Invoke-Probe 'action' 'res://Scenes/Debug/_ActionInterruptionProbe.tscn' '动作交接验收结束：通过 38，失败 0'
            Invoke-Probe 'specialization' 'res://Scenes/Debug/_SpecializationProbe.tscn' '特化验收结束：通过 221，失败 0'
            Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0'
        }
    }
    elseif ($EarlyCharge) {
        Invoke-Probe 'early_charge' 'res://Scenes/Debug/_EarlyChargeProbe.tscn' '未蓄满释放验收结束：通过 \d+，失败 0'
        if (-not $ProbeOnly) {
            Invoke-Probe 'aim_throw' 'res://Scenes/Debug/_AimAndThrowProbe.tscn' '立体瞄准与投掷验收结束：通过 33，失败 0'
            Invoke-Probe 'slots' 'res://Scenes/Debug/_LoadoutSlotsProbe.tscn' '换装与技能槽验收结束：通过 57，失败 0'
            Invoke-Probe 'action' 'res://Scenes/Debug/_ActionInterruptionProbe.tscn' '动作交接验收结束：通过 38，失败 0'
            Invoke-Probe 'snapshot' 'res://Scenes/Debug/_AttackSnapshotProbe.tscn' '攻击快照验收结束：通过 45，失败 0'
            Invoke-Probe 'charge_evolution' 'res://Scenes/Debug/_ChargeEvolutionProbe.tscn' '蓄力进化验收结束：通过 151，失败 0'
            Invoke-Probe 'specialization' 'res://Scenes/Debug/_SpecializationProbe.tscn' '特化验收结束：通过 221，失败 0'
            Invoke-Probe 'spear' 'res://Scenes/Debug/_SpearRecallProbe.tscn' '气贯长虹验收结束：通过 89，失败 0'
            Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0'
        }
    }
    elseif ($AimAndThrow) {
        Invoke-Probe 'aim_throw' 'res://Scenes/Debug/_AimAndThrowProbe.tscn' '立体瞄准与投掷验收结束：通过 \d+，失败 0'
        if (-not $ProbeOnly) {
            Invoke-Probe 'aim_throw_render' 'res://Scenes/Debug/_AimAndThrowProbe.tscn' '立体瞄准与投掷验收结束：通过 \d+，失败 0' -Render
            Invoke-Probe 'slots' 'res://Scenes/Debug/_LoadoutSlotsProbe.tscn' '换装与技能槽验收结束：通过 57，失败 0'
            Invoke-Probe 'snapshot' 'res://Scenes/Debug/_AttackSnapshotProbe.tscn' '攻击快照验收结束：通过 45，失败 0'
            Invoke-Probe 'scythe' 'res://Scenes/Debug/_ScytheEvolutionProbe.tscn' '镰刀末刀进化验收结束：通过 36，失败 0'
            Invoke-Probe 'seek' 'res://Scenes/Debug/_ScytheSeekProbe.tscn' '飞镰喜爱验收结束：通过 43，失败 0'
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
            Invoke-Probe 'specialization' 'res://Scenes/Debug/_SpecializationProbe.tscn' '特化验收结束：通过 221，失败 0'
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
            Invoke-Probe 'specialization' 'res://Scenes/Debug/_SpecializationProbe.tscn' '特化验收结束：通过 221，失败 0'
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
            Invoke-Probe 'specialization' 'res://Scenes/Debug/_SpecializationProbe.tscn' '特化验收结束：通过 221，失败 0'
            Invoke-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0'
        }
    }
    elseif ($BodyCollision) {
        Invoke-Probe 'body' 'res://Scenes/Debug/_BodyCollisionProbe.tscn' '身体分层验收结束：通过 \d+，失败 0'
        if (-not $ProbeOnly) {
            Invoke-Probe 'collision' 'res://Scenes/Debug/_BatchDProbe.tscn' 'D批验收结束：通过 91，失败 0'
            Invoke-Probe 'specialization' 'res://Scenes/Debug/_SpecializationProbe.tscn' '特化验收结束：通过 221，失败 0'
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
        $null = Invoke-ValidationEngine $godotExe @('--headless','--path',$projectDir,'--editor','--import') $projectDir (Join-Path $projectDir "Logs/validation/.ai_$($batchId)_$runTag.import.log") (Join-Path $projectDir "Logs/validation/.ai_$($batchId)_$runTag.import.err") 60
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
        if ($Rainbow -or $GoldenRoad -or $LightLeap -or $GroundBurst -or $ActionMovement -or $ChargeEvolution -or $JumpStomp -or $RewardRules) { Invoke-Probe 'seek' 'res://Scenes/Debug/_ScytheSeekProbe.tscn' '飞镰喜爱验收结束：通过 43，失败 0' }
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
    $shotNames = if ($BowExitAudit -or $ChargeInputAudit -or $KillJumps -or $KillFacts -or $WingDive) { @() } elseif ($RewardTiers) { @('j24_reward_tiers') } elseif ($RewardRules) { @() } elseif ($JumpStomp) { @('j22_jump_stomp') } elseif ($ChargeEvolution) { @('j21_distinct_homing','j21_flow') } elseif ($ActionMovement) { @('j20_action_movement') } elseif ($GroundBurst) { @('j19_ground_burst') } elseif ($LightLeap) { @('j18_light_leap') } elseif ($GoldenRoad) { @('j17_golden_road') } elseif ($Rainbow) { @('j16_rainbow') } elseif ($Affection) { @('j48_affection','j48_affection_world') } elseif ($Scythe) { @('j14_mercy','j14_salvation') } elseif ($SpiritSword) { @('j13_golden_qi','j13_great_draw','j13_divine_descent') } else { @('j12_capsule_sweep') }
    foreach ($shotName in $shotNames) {
        if ($Specialization -or $RuneUi -or $BodyCollision -or $FlightPhysics -or $NavigationFoundation -or $EnemyRoster) { break }
        $shotPath = Join-Path $projectDir "docs\screenshots\$shotName.png"
        if (-not (Test-Path -LiteralPath $shotPath)) { throw "$shotName 截图缺失" }
        $evidence.Add("截图 $shotName SHA256=$((Get-FileHash -LiteralPath $shotPath -Algorithm SHA256).Hash)")
    }
    $currentShots = if ($PlayerExperience) { if ($ProbeOnly) { @() } else { @("screenshots/$($batchId)_base_guide.png","screenshots/$($batchId)_expedition_summary.png","screenshots/$($batchId)_summary_settings.png","screenshots/$($batchId)_forest_node.png","screenshots/$($batchId)_forest_reward.png","screenshots/$($batchId)_forest_combat.png") } } elseif ($SpiritSwordRush) { if ($ProbeOnly) { @() } else { @('screenshots/j43_swords_hover.png','screenshots/j43_swords_thrust.png') } } elseif ($SpiritSwords) { if ($ProbeOnly) { @() } else { @('screenshots/j42_swords_hover.png','screenshots/j42_swords_attack.png') } } elseif ($EarlyCharge) { @() } elseif ($AimAndThrow) { if (-not $ProbeOnly) { @('screenshots/j40_spear_aim.png','screenshots/j40_crossbow_aim.png') } else { @() } } elseif ($LoadoutSlots -and (-not $ProbeOnly -or $LabOnly)) { @('screenshots/j39_contact_visibility.png','screenshots/j39_spear_preview.png') } elseif ($CombatFeedback -and (-not $ProbeOnly -or $LabOnly)) { @('screenshots/j38_combat_feedback.png') } elseif ($EnemyWarnings -and (-not $ProbeOnly -or $LabOnly)) { @('screenshots/j37_enemy_warning.png') } elseif ($EnemyRoster -and (-not $ProbeOnly -or $LabOnly)) { @('screenshots/j36_enemy_roster.png','screenshots/j36_attack_warning.png') } elseif ($Specialization) { @('character-design-j32-wind.png','character-design-j32-rune-ui.png') } elseif ($RuneUi) { @('character-design-j31-rune-ui.png') } else { @() }
    # 旧入口读取本次新产物，不能把docs中保留的历史预警图当成本轮截图。
    if ($EnemyWarnings) { $currentShots = @($currentShots | ForEach-Object { if ($_ -eq 'screenshots/j37_enemy_warning.png') { '../Logs/evidence/enemy-warning.png' } else { $_ } }) }
    if ($Specialization) { $currentShots = @($currentShots | ForEach-Object { if ($_ -eq 'character-design-j32-wind.png') { '../Logs/evidence/specialization-wind.png' } else { $_ } }) }
    if ($InteractionComponents -and -not $ProbeOnly) { $currentShots += @("character-design-$batchId-rune-ui.png","screenshots/$($batchId)_interaction_lab.png") }
    elseif ($FlowRobustness -and -not $ProbeOnly) { $currentShots += 'character-design-j45-rune-ui.png' }
    if ($ChallengeFeedback -and -not $ProbeOnly) { $currentShots += @('screenshots/j48_challenge_active.png','screenshots/j48_challenge_rejected.png','screenshots/j48_challenge_cleared.png') }
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
    [IO.File]::WriteAllText((Join-Path $projectDir "Logs/evidence/$outputName"),(($evidence -join "`n").Replace("`r`n","`n").TrimEnd([char[]]"`r`n") + "`n"),$utf8)
    Write-Output "$batchId 验收完成：Logs/evidence/$outputName"
}
catch {
    $evidence.Add($_.ToString())
    foreach ($configuration in @('Debug','ExportRelease')) {
        $buildPath = Join-Path $projectDir "Logs/validation/.ai_$($batchId)_$runTag.$configuration.log"
        if (Test-Path -LiteralPath $buildPath) {
            $evidence.Add("=== 失败现场构建 $configuration ===")
            $evidence.Add((Get-Content -LiteralPath $buildPath -Raw))
        }
    }
    [IO.File]::WriteAllText((Join-Path $projectDir "Logs/evidence/character-design-$batchId-failure-$runTag.log"),(($evidence -join "`n").Replace("`r`n","`n").TrimEnd([char[]]"`r`n") + "`n"),$utf8)
    throw
}
finally {
    if ($lease) { $lease.Dispose() }
    Pop-Location
}
