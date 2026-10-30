# 只读数值审计；空白隔离档不带入玩家成长，真实档案前后逐字节校验。
param([switch]$SkipBuild,[ValidateSet('EnergyPalm','Crossbow','Bow','SpiritSword','Scythe','Spear')][string[]]$Weapons=@())
$ErrorActionPreference='Stop'
$projectDir=Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Validation.Common.ps1')
. (Join-Path $PSScriptRoot 'LogPaths.ps1')
Initialize-ProjectLogPaths -ProjectRoot $projectDir
New-Item -ItemType Directory -Path (Join-Path $projectDir 'Logs/evidence/balance-audit') -Force|Out-Null
$engine='F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profile=Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$tag=Get-Date -Format 'yyyyMMdd_HHmmss'
$lease=Open-ProjectValidationLease $projectDir 60
$saves=Get-ValidationSaveSnapshot $profile
try {
 if(-not $SkipBuild){foreach($configuration in @('Debug','ExportRelease')){$null=Invoke-ValidationBuild $projectDir $configuration (Join-Path $projectDir "Logs/validation/balance_audit_$tag.$configuration.log")}}
 $mirror=New-ValidationMirror $projectDir 'balance_audit' -ShareAssembly
 # 只改刚创建的隔离用户目录副本，真实存档不写入、不恢复覆盖。
 $emptyProfiles=@{'meta.cfg'="[meta]`nshards = 0`n";'ability_cards.cfg'="[cards]`ncard_vitality_level = 0`ncard_vitality_active = false`n`n[quota]`nquota_level = 0`n";'rune.cfg'="[rune]`nid = `"`"`n";'loadout.cfg'="[loadout]`n"}
 foreach($name in $emptyProfiles.Keys){[IO.File]::WriteAllText((Join-Path $mirror.Profile $name),$emptyProfiles[$name],[Text.UTF8Encoding]::new($false))}
 $arguments=@('--headless','--path',$mirror.Project,'res://Scenes/Debug/_BalanceAuditProbe.tscn'); if($Weapons.Count){$arguments+=@('--',('--audit-weapons='+($Weapons -join ',')))}
 $result=Invoke-ValidationEngine $engine $arguments $projectDir (Join-Path $projectDir "Logs/validation/balance_audit_$tag.log") (Join-Path $projectDir "Logs/validation/balance_audit_$tag.err") 540
 if($result.Output -notmatch '\[BalanceAudit\] COMPLETE resources=\d+ enemies=\d+ samples=\d+'){throw '审计没有完成'}
 Assert-ValidationSaves $profile $saves
 $validation=@{ValidatedAt=(Get-Date).ToString('o');EngineExitCode=$result.ExitCode;StderrEmpty=$true;Seconds=$result.Seconds;Weapons=$Weapons;SaveComparison='五份真实存档逐字节相同';ProductionBalanceChanged=$false;Stdout="Logs/validation/balance_audit_$tag.log";Stderr="Logs/validation/balance_audit_$tag.err"}
 $validation | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $projectDir "Logs/evidence/balance-audit/validation-$tag.json") -Encoding utf8
 Write-Output 'PASS 平衡审计和真实动作采样，生产数值及真实存档未改。'
}finally{try{Assert-ValidationSaves $profile $saves}finally{$lease.Dispose()}}
