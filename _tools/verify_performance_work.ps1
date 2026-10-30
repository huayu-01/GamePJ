# 性能工作独立验证：顺序执行、隔离存档、保留用户编辑器，基准不以采样完成宣称达标。
param([switch]$SkipBuild,[string]$Label='baseline',[string[]]$Cases=@('benchmark'),[ValidateSet(50,100)][int]$EnemyCount=100,[ValidateSet('Physical','PassThrough','Avoidance')][string]$CrowdMode='Physical')
$ErrorActionPreference='Stop'
if($Label -notmatch '^[a-zA-Z0-9_-]+$'){throw 'Label只允许英文、数字、下划线和连字符'}
$projectDir=Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Validation.Common.ps1')
. (Join-Path $PSScriptRoot 'LogPaths.ps1')
Initialize-ProjectLogPaths -ProjectRoot $projectDir
New-Item -ItemType Directory -Path (Join-Path $projectDir 'Logs/evidence/performance-investigation') -Force | Out-Null
$engine='F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profile=Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$known=@{
 benchmark=@{Scene='_SurvivalPerformanceProbe';Tag='SurvivalPerformance';Render=$true}
 regression=@{Scene='_PerformanceSafetyProbe';Tag='PerformanceSafety';Render=$false}
 crowd=@{Scene='_CrowdAvoidanceProbe';Tag='CrowdAvoidance';Render=$false}
 animation=@{Scene='_AnimationCostProbe';Tag='AnimationCost';Render=$true}
 physics=@{Scene='_FlightPhysicsProbe';Pattern='飞行与受力验收结束：通过 \d+，失败 0';Render=$false}
 body=@{Scene='_BodyCollisionProbe';Pattern='身体分层验收结束：通过 \d+，失败 0';Render=$false}
 feedback=@{Scene='_CombatFeedbackProbe';Pattern='打击反馈验收结束：通过 \d+，失败 0';Render=$true}
}
foreach($case in $Cases){if(-not $known.ContainsKey($case)){throw "未知专项：$case"}}
$tag=Get-Date -Format 'yyyyMMdd_HHmmss'
$lease=Open-ProjectValidationLease $projectDir 60
$saves=Get-ValidationSaveSnapshot $profile
try{
 if(-not $SkipBuild){foreach($configuration in @('Debug','ExportRelease')){$null=Invoke-ValidationBuild $projectDir $configuration (Join-Path $projectDir "Logs/validation/performance_$tag.$configuration.log")}}
 $mirror=New-ValidationMirror $projectDir 'performance_work' -ShareAssembly
 foreach($case in $Cases){
  $spec=$known[$case];$args=@('--path',$mirror.Project,'--resolution','1600x900',"res://Scenes/Debug/$($spec.Scene).tscn")
  if(-not $spec.Render){$args+='--headless'}
  $args+=@('--',"--perf-label=$Label","--perf-count=$EnemyCount","--perf-crowd=$CrowdMode")
  $result=Invoke-ValidationEngine $engine $args $projectDir (Join-Path $projectDir "Logs/validation/performance_$tag.$case.$Label.log") (Join-Path $projectDir "Logs/validation/performance_$tag.$case.$Label.err") 240
  $expected=if($spec.Pattern){$spec.Pattern}else{"\[$($spec.Tag)\] SUMMARY pass=\d+ fail=0"}
  if($result.Output -notmatch $expected){throw "缺少通过标记：$case"}
  Assert-ValidationSaves $profile $saves
  Write-Output "PASS $case $Label，真实存档不变。"
 }
}finally{try{Assert-ValidationSaves $profile $saves}finally{$lease.Dispose()}}
