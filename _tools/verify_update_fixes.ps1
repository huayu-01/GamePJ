# 更新清单的逐项真实场景验收；测试输出集中到Logs，使用独立用户目录与公共验证锁。
param([switch]$SkipBuild,[switch]$ImportAssets,[string[]]$Only=@('navigation'))
$ErrorActionPreference='Stop'
$projectDir=Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Validation.Common.ps1')
. (Join-Path $PSScriptRoot 'LogPaths.ps1')
Initialize-ProjectLogPaths -ProjectRoot $projectDir
New-Item -ItemType Directory -Path (Join-Path $projectDir 'Logs/evidence/update-fixes') -Force | Out-Null
$engine='F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profile=Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$cases=@(
 @{Name='navigation';Scene='_NavigationFoundationProbe';Pattern='导航基础验收结束：通过 \d+，失败 0'},
 @{Name='player_feedback';Scene='_PlayerHurtFeedbackProbe';Pattern='\[PlayerHurt\] SUMMARY pass=\d+ fail=0';Render=$true},
 @{Name='ui_guard';Scene='_UiChoiceGuardProbe';Pattern='\[UiChoiceGuard\] SUMMARY pass=\d+ fail=0'},
 @{Name='enemy_roster';Scene='_EnemyRosterProbe';Pattern='敌人清单验收结束：通过 \d+，失败 0'},
 @{Name='content';Scene='_ContentValidationProbe';Pattern='\[ContentValidation\] SUMMARY pass=\d+ fail=0'},
 @{Name='performance';Scene='_SurvivalPerformanceProbe';Pattern='\[SurvivalPerformance\] SUMMARY pass=\d+ fail=0';RenderOnly=$true},
 @{Name='spawn_height';Scene='_SurvivalSpawnHeightProbe';Pattern='\[SurvivalSpawnHeight\] SUMMARY pass=\d+ fail=0'},
 @{Name='reward_variety';Scene='_SurvivalRewardVarietyProbe';Pattern='\[SurvivalRewardVariety\] SUMMARY pass=\d+ fail=0'},
 @{Name='scythe_jump';Scene='_ScytheLandingJumpProbe';Pattern='\[ScytheLandingJump\] SUMMARY pass=\d+ fail=0'},
 @{Name='thunder_cooldown';Scene='_ThunderHeartCooldownProbe';Pattern='\[ThunderHeartCooldown\] SUMMARY pass=\d+ fail=0'},
 @{Name='charge_evolution';Scene='_ChargeEvolutionProbe';Pattern='蓄力进化验收结束：通过 \d+，失败 0'},
 @{Name='config_authoring';Scene='_ConfigAuthoringProbe';Pattern='\[ConfigAuthoring\] SUMMARY pass=\d+ fail=0';Render=$true},
 @{Name='fall';Scene='_FallAttackProbe';Pattern='下落专项验收结束：通过 \d+，失败 0'},
 @{Name='charge';Scene='_ChargeReleaseProbe';Pattern='蓄力释放验收结束：通过 \d+，失败 0'},
 @{Name='numeric_identity';Scene='_NumericIdentityProbe';Pattern='\[NumericIdentity\] SUMMARY pass=\d+ fail=0'},
 @{Name='throw_size';Scene='_ThrowSizeProbe';Pattern='\[ThrowSize\] SUMMARY pass=\d+ fail=0'},
 @{Name='guard_locomotion';Scene='_GuardLocomotionProbe';Pattern='\[GuardLocomotion\] SUMMARY pass=\d+ fail=0'}
)
foreach($name in $Only){if($name -notin $cases.Name){throw "未知专项：$name"}}
$tag=Get-Date -Format 'yyyyMMdd_HHmmss'
$lease=Open-ProjectValidationLease $projectDir 120
$saves=Get-ValidationSaveSnapshot $profile
$runs=[Collections.Generic.List[object]]::new()
$report=[ordered]@{Date=(Get-Date -Format 's');Passed=$false;Runs=$runs;SavesUnchanged=$false;Failure=''}
try{
 if(-not $SkipBuild){foreach($configuration in @('Debug','ExportRelease')){$null=Invoke-ValidationBuild $projectDir $configuration (Join-Path $projectDir "Logs/validation/update_fixes_$tag.$configuration.log")}}
 $mirror=New-ValidationMirror $projectDir 'update_fixes' -ShareAssembly
	if($ImportAssets){
	 $settingsPath=Join-Path $mirror.Project 'project.godot'
	 $settings=[IO.File]::ReadAllText($settingsPath)
	 $settings=[regex]::Replace($settings,'(?ms)(\[editor_plugins\]\s*)enabled\s*=\s*PackedStringArray\([^\r\n]*\)','$1enabled=PackedStringArray()')
	 [IO.File]::WriteAllText($settingsPath,$settings,[Text.UTF8Encoding]::new($false))
	 $null=Invoke-ValidationEngine $engine @('--headless','--path',$mirror.Project,'--editor','--import') $projectDir (Join-Path $projectDir "Logs/validation/update_fixes_$tag.import.log") (Join-Path $projectDir "Logs/validation/update_fixes_$tag.import.err") 180
	}
 foreach($case in $cases){
  if($case.Name -notin $Only){continue}
  $modes=if($case.RenderOnly){@('render')}elseif($case.Render){@('headless','render')}else{@('headless')}
  foreach($mode in $modes){
  $out=Join-Path $projectDir "Logs/validation/update_fixes_$tag.$($case.Name).$mode.log"; $err=Join-Path $projectDir "Logs/validation/update_fixes_$tag.$($case.Name).$mode.err"
  $runArgs=@('--path',$mirror.Project,'--resolution','1600x900',"res://Scenes/Debug/$($case.Scene).tscn"); if($mode -eq 'headless'){$runArgs+='--headless'}
  $result=Invoke-ValidationEngine $engine $runArgs $projectDir $out $err 180
  if($result.Output -notmatch $case.Pattern){throw "缺少通过标记：$($case.Name)"}
  $runs.Add([ordered]@{Name=$case.Name;Mode=$mode;Exit=$result.ExitCode;Stdout=$out;Stderr=$err;SHA256=(Get-FileHash -LiteralPath $out).Hash})
  Assert-ValidationSaves $profile $saves; Write-Output "PASS $($case.Name) $mode"
  }
 }
 $report.Passed=$true
}catch{$report.Failure=$_.ToString();throw}
finally{
 try{Assert-ValidationSaves $profile $saves;$report.SavesUnchanged=$true}
 finally{
  $path=Join-Path $projectDir "Logs/evidence/update-fixes/run-$tag.json"
  try{$report|ConvertTo-Json -Depth 7|Set-Content -LiteralPath $path -Encoding utf8}finally{$lease.Dispose()}
  Write-Output "逐项验收：$path"
 }
}
