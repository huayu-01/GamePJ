# 2026-10-23：十四项战斗与界面修复，串行验证并保护真实存档。
param([switch]$SkipBuild, [string[]]$Only = @(), [int]$LockWaitSeconds = 45, [string]$ProfileDirectory = '', [string]$ValidationLockPath = '')
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
. (Join-Path $projectDir '_tools/LogPaths.ps1')
Initialize-ProjectLogPaths -ProjectRoot $projectDir
. (Join-Path $PSScriptRoot 'Validation.Common.ps1')
$godotExe = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profileDir = if ($ProfileDirectory) { $ProfileDirectory } else { Join-Path $env:APPDATA 'Godot\app_userdata\ACT Roguelike' }
$tag = Get-Date -Format 'yyyyMMdd_HHmmss'
$utf8 = [Text.UTF8Encoding]::new($false)
$evidence = [Collections.Generic.List[string]]::new()
$failures = [Collections.Generic.List[string]]::new()
$original = @{}
$lease = $null
$ownedProcess = $null
function Open-ValidationLease { Open-ProjectValidationLease $projectDir $LockWaitSeconds $ValidationLockPath }
function Assert-Saves { Assert-ValidationSaves $profileDir $original }
function Run-Probe([string]$Name, [string]$Scene, [string]$Expected, [switch]$Render, [switch]$Main) {
    if ($Only.Count -gt 0 -and $Name -notin $Only) { return }
    $stdout = Join-Path $projectDir "Logs/validation/.ai_gameplay_$tag.$Name.log"
    $stderr = Join-Path $projectDir "Logs/validation/.ai_gameplay_$tag.$Name.err"
    $runArgs = @('--path',$projectDir,'--log-file',(Join-Path $projectDir "Logs/validation/.ai_gameplay_$tag.$Name.engine.log"),'--quit-after','40000',$Scene)
    if ($Render) { $runArgs += @('--resolution','1600x900') } else { $runArgs += '--headless' }
    if ($Main) { $runArgs += @('--','--selftest') }
    try {
        $result = Invoke-ValidationEngine $godotExe $runArgs $projectDir $stdout $stderr
        $evidence.Add("=== $Name exit=$($result.ExitCode) seconds=$($result.Seconds) ===")
        $evidence.Add($result.Output)
        if ($result.Output -notmatch $Expected) { throw "$Name 没有满足完成断言：$stdout" }
        if ($Main) { $evidence.AddRange([string[]](Assert-MainValidation $projectDir $result.Output)) }
        Write-Output "PASS $Name"
    }
    catch { $failures.Add($_.ToString()); Write-Output "FAIL $Name $_" }
    finally {
        $flag = Join-Path $profileDir 'selftest.flag'
        if (Test-Path -LiteralPath $flag) { Remove-Item -LiteralPath $flag }
        Assert-Saves
    }
}
# 从实际声明读取有效选择器，拼写错误应在构建前失败，不能静默跳过。
$selectorTokens = $null; $selectorErrors = $null
$selectorAst = [Management.Automation.Language.Parser]::ParseFile($PSCommandPath, [ref]$selectorTokens, [ref]$selectorErrors)
if ($selectorErrors.Count) { throw '无法解析验证专项声明。' }
$knownSelectors = @($selectorAst.FindAll({ param($node) $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Run-Probe' }, $true) | ForEach-Object { $_.CommandElements[1].Value })
foreach ($selector in $Only) { if ($selector -notin $knownSelectors) { throw "未知验证选择器：$selector" } }
Push-Location $projectDir
try {
    $lease = Open-ValidationLease
    $active = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -like 'Godot*' -and $_.CommandLine -notmatch '--editor' })
    if ($active.Count -gt 0) { throw "已有Godot游戏进程，未停止：$($active.ProcessId -join ',')" }
    if (Test-Path -LiteralPath (Join-Path $profileDir 'selftest.flag')) { throw '已有selftest.flag，先核实归属。' }
    foreach ($name in @('meta.cfg','ability_cards.cfg','settings.cfg','loadout.cfg','rune.cfg')) {
        $path = Join-Path $profileDir $name
        $original[$name] = if (Test-Path -LiteralPath $path) { [Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) } else { $null }
        $evidence.Add("存档BEFORE $name SHA256=$(if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path).Hash}else{'ABSENT'})")
    }
    [IO.File]::WriteAllText((Join-Path $projectDir "Logs/validation/.ai_gameplay_$tag.saves.json"),($original | ConvertTo-Json),$utf8)
    if (-not $SkipBuild) {
        foreach ($configuration in @('Debug','ExportRelease')) {
            $evidence.Add((Invoke-ValidationBuild $projectDir $configuration (Join-Path $projectDir "Logs/validation/.ai_gameplay_$tag.$configuration.log")))
        }
    }
    Run-Probe 'rules' 'res://Scenes/Debug/_GameplayRulesProbe.tscn' '玩法规则验收结束：通过 \d+，失败 0'
    Run-Probe 'effects' 'res://Scenes/Debug/_RewardEffectsProbe.tscn' '\[RewardEffects\] SUMMARY pass=\d+ fail=0'
    Run-Probe 'jump_stomp' 'res://Scenes/Debug/_JumpStompProbe.tscn' '起跳践踏验收结束：通过 \d+，失败 0'
    Run-Probe 'kill_jumps' 'res://Scenes/Debug/_KillJumpProbe.tscn' '击杀跳跃验收结束：通过 \d+，失败 0'
    Run-Probe 'kill_facts' 'res://Scenes/Debug/_KillFactProbe.tscn' '逐杀事实验收结束：通过 \d+，失败 0'
    Run-Probe 'body' 'res://Scenes/Debug/_BodyCollisionProbe.tscn' '身体分层验收结束：通过 \d+，失败 0'
    Run-Probe 'decoupling' 'res://Scenes/Debug/_ContentDecouplingProbe.tscn' '\[Decoupling\] SUMMARY pass=\d+ fail=0'
    Run-Probe 'interaction' 'res://Scenes/Debug/_InteractionComponentProbe.tscn' '交互组件验收结束：通过 \d+，失败 0'
    Run-Probe 'interaction_render' 'res://Scenes/Debug/_InteractionComponentProbe.tscn' '交互组件验收结束：通过 \d+，失败 0' -Render
    Run-Probe 'enemy_actions' 'res://Scenes/Debug/_EnemyActionExecutionProbe.tscn' '\[EnemyActions\] SUMMARY pass=\d+ fail=0'
    Run-Probe 'warnings' 'res://Scenes/Debug/_EnemyWarningProbe.tscn' '敌人预警验收结束：通过 \d+，失败 0'
    Run-Probe 'warnings_render' 'res://Scenes/Debug/_EnemyWarningProbe.tscn' '敌人预警验收结束：通过 \d+，失败 0' -Render
    Run-Probe 'replacement' 'res://Scenes/Debug/_CharacterReplacementProbe.tscn' '\[CharacterReplacement\] 完成：通过 \d+，失败 0'
    Run-Probe 'replacement_render' 'res://Scenes/Debug/_CharacterReplacementProbe.tscn' '\[CharacterReplacement\] 完成：通过 \d+，失败 0' -Render
    Run-Probe 'content' 'res://Scenes/Debug/_ContentValidationProbe.tscn' '\[ContentValidation\] SUMMARY pass=\d+ fail=0'
    Run-Probe 'localization' 'res://Scenes/Debug/_ContentLocalizationProbe.tscn' '\[ContentLocalization\] SUMMARY pass=\d+ fail=0'
    Run-Probe 'localization_render' 'res://Scenes/Debug/_ContentLocalizationProbe.tscn' '\[ContentLocalization\] SUMMARY pass=\d+ fail=0' -Render
    Run-Probe 'visual' 'res://Scenes/Debug/_VisualReadabilityProbe.tscn' '表现可读性验收结束：通过 \d+，失败 0'
    Run-Probe 'visual_render' 'res://Scenes/Debug/_VisualReadabilityProbe.tscn' '表现可读性验收结束：通过 \d+，失败 0' -Render
    Run-Probe 'reward_ui' 'res://Scenes/Debug/_BuildRewardsProbe.tscn' '\[BuildRewards\] SUMMARY pass=\d+ fail=0'
    Run-Probe 'reward_ui_render' 'res://Scenes/Debug/_BuildRewardsProbe.tscn' '\[BuildRewards\] SUMMARY pass=\d+ fail=0' -Render
    Run-Probe 'ballistics' 'res://Scenes/Debug/_PlayerBallisticsProbe.tscn' '玩家弹道验收结束：通过 \d+，失败 0'
    Run-Probe 'projectile_query' 'res://Scenes/Debug/_ProjectileQueryProbe.tscn' '弹道查询验收结束：通过 56，失败 0'
    Run-Probe 'movement' 'res://Scenes/Debug/_ActionMovementProbe.tscn' '动作移动验收结束：通过 74，失败 0'
    Run-Probe 'seek' 'res://Scenes/Debug/_ScytheSeekProbe.tscn' '飞镰喜爱验收结束：通过 \d+，失败 0'
    Run-Probe 'knife' 'res://Scenes/Debug/_KnifeVolleyProbe.tscn' '飞刀散射验收结束：通过 219，失败 0'
    Run-Probe 'charge_evolution' 'res://Scenes/Debug/_ChargeEvolutionProbe.tscn' '蓄力进化验收结束：通过 151，失败 0'
    Run-Probe 'aim' 'res://Scenes/Debug/_AimAndThrowProbe.tscn' '立体瞄准与投掷验收结束：通过 33，失败 0'
    Run-Probe 'slots' 'res://Scenes/Debug/_LoadoutSlotsProbe.tscn' '换装与技能槽验收结束：通过 57，失败 0'
    Run-Probe 'snapshot' 'res://Scenes/Debug/_AttackSnapshotProbe.tscn' '攻击快照验收结束：通过 45，失败 0'
    Run-Probe 'bow' 'res://Scenes/Debug/_BowSweepProbe.tscn' '验收结束：通过 39，失败 0'
    Run-Probe 'early' 'res://Scenes/Debug/_EarlyChargeProbe.tscn' '未蓄满释放验收结束：通过 37，失败 0'
    Run-Probe 'roster' 'res://Scenes/Debug/_EnemyRosterProbe.tscn' '敌人清单验收结束：通过 \d+，失败 0'
    Run-Probe 'flight' 'res://Scenes/Debug/_FlightPhysicsProbe.tscn' '飞行与受力验收结束：通过 \d+，失败 0'
    Run-Probe 'reward_rules' 'res://Scenes/Debug/_RewardRulesProbe.tscn' '获取规则验收结束：通过 78，失败 0'
    Run-Probe 'specialization' 'res://Scenes/Debug/_SpecializationProbe.tscn' '特化验收结束：通过 \d+，失败 0'
    Run-Probe 'specialization_render' 'res://Scenes/Debug/_SpecializationProbe.tscn' '特化验收结束：通过 \d+，失败 0' -Render
    Run-Probe 'landing' 'res://Scenes/Debug/_LandingKnockbackProbe.tscn' '地爆验收结束：通过 \d+，失败 0'
    # 写盘专项只能由独立用户目录入口显式选择，不加入玩家目录的默认全量运行。
    if ($Only -contains 'adventure') { Run-Probe 'adventure' 'res://Scenes/Debug/_AdventureCancelProbe.tscn' '\[AdventureCancel\] SUMMARY pass=\d+ fail=0' }
    if ($Only -contains 'adventure_render') { Run-Probe 'adventure_render' 'res://Scenes/Debug/_AdventureCancelProbe.tscn' '\[AdventureCancel\] SUMMARY pass=\d+ fail=0' -Render }
    Run-Probe 'synergy' 'res://Scenes/Debug/_SynergyMatrixProbe.tscn' '\[SynergyMatrix\] SUMMARY combinations=\d+ pass=\d+ fail=0'
    Run-Probe 'ui' 'res://Scenes/UI/_BatchEProbe.tscn' 'E 批验收结束：通过 \d+，失败 0'
    Run-Probe 'charge' 'res://Scenes/Debug/_ChargeReleaseProbe.tscn' '蓄力释放验收结束：通过 80，失败 0'
    Run-Probe 'character' 'res://Scenes/Debug/_CharacterDesignProbe.tscn' '原始设计验收结束：通过 115，失败 0'
    Run-Probe 'dragon' 'res://Scenes/Debug/_DragonArrowProbe.tscn' '验收结束：通过 42，失败 0'
    Run-Probe 'rune' 'res://Scenes/Debug/_RuneProbe.tscn' '验收结束：通过 83，失败 0'
    Run-Probe 'main' 'res://Scenes/Main.tscn' '=== 自测结束 ===' -Main
    Assert-Saves
    foreach ($name in $original.Keys) {
        $path = Join-Path $profileDir $name
        $evidence.Add("存档AFTER $name SHA256=$(if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path).Hash}else{'ABSENT'})")
    }
    if ($failures.Count -gt 0) { throw ($failures -join "`n") }
    [IO.File]::WriteAllText((Join-Path $projectDir "Logs/evidence/gameplay-fixes-evidence-$tag.log"),($evidence -join "`n"),$utf8)
    Write-Output "验收完成：Logs/evidence/gameplay-fixes-evidence-$tag.log"
}
catch {
    $evidence.Add($_.ToString())
    [IO.File]::WriteAllText((Join-Path $projectDir "Logs/evidence/gameplay-fixes-failure-$tag.log"),($evidence -join "`n"),$utf8)
    throw
}
finally { if ($lease) { $lease.Dispose() }; Pop-Location }
