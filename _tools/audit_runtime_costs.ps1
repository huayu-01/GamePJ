# 只读结构调查，使用隔离用户目录和公共验证锁；不执行性能策略或改写生产场景。
param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$projectDir=Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Validation.Common.ps1')
. (Join-Path $PSScriptRoot 'LogPaths.ps1')
Initialize-ProjectLogPaths -ProjectRoot $projectDir
New-Item -ItemType Directory -Path (Join-Path $projectDir 'Logs/evidence/performance-investigation') -Force | Out-Null
$engine='F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profile=Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$tag=Get-Date -Format 'yyyyMMdd_HHmmss'
$lease=Open-ProjectValidationLease $projectDir 120
$saves=Get-ValidationSaveSnapshot $profile
try {
 if(-not $SkipBuild){foreach($configuration in @('Debug','ExportRelease')){$null=Invoke-ValidationBuild $projectDir $configuration (Join-Path $projectDir "Logs/validation/runtime_audit_$tag.$configuration.log")}}
 $mirror=New-ValidationMirror $projectDir 'runtime_audit' -ShareAssembly
 $result=Invoke-ValidationEngine $engine @('--headless','--path',$mirror.Project,'res://Scenes/Debug/_RuntimeCostAudit.tscn') $projectDir (Join-Path $projectDir "Logs/validation/runtime_audit_$tag.log") (Join-Path $projectDir "Logs/validation/runtime_audit_$tag.err") 180
 if($result.Output -notmatch '\[RuntimeCostAudit\] COMPLETE enemies=\d+ physics=Jolt Physics'){throw '缺少调查完成标记'}
 Assert-ValidationSaves $profile $saves
 Write-Output 'PASS 真实运行节点结构调查，存档不变。'
}finally{$lease.Dispose()}
