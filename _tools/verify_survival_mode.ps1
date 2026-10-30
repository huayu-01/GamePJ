# 独立用户目录验证生存模式，不写玩家真实进度，不关闭正在使用的编辑器。
param([switch]$SkipBuild,[switch]$HeadlessOnly,[switch]$RenderOnly,[int]$LockWaitSeconds=120)
$ErrorActionPreference='Stop'
if($HeadlessOnly -and $RenderOnly){throw 'HeadlessOnly与RenderOnly不能同时使用。'}
$projectDir=Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Validation.Common.ps1')
. (Join-Path $PSScriptRoot 'LogPaths.ps1')
Initialize-ProjectLogPaths -ProjectRoot $projectDir
New-Item -ItemType Directory -Path (Join-Path $projectDir 'Logs/evidence/survival') -Force | Out-Null
$engine='F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profile=Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$tag=Get-Date -Format 'yyyyMMdd_HHmmss'
$lease=Open-ProjectValidationLease $projectDir $LockWaitSeconds
$saves=Get-ValidationSaveSnapshot $profile
$runs=[Collections.Generic.List[object]]::new()
$report=[ordered]@{Date=(Get-Date -Format 's');Passed=$false;Runs=$runs;SavesUnchanged=$false;Failure=''}
try {
    if(-not $SkipBuild){foreach($configuration in @('Debug','ExportRelease')){$null=Invoke-ValidationBuild $projectDir $configuration (Join-Path $projectDir "Logs/validation/survival_$tag.$configuration.log")}}
    $mirror=New-ValidationMirror $projectDir 'survival' -ShareAssembly
    foreach($mode in @('headless','render')) {
        if(($mode -eq 'headless' -and $RenderOnly) -or ($mode -eq 'render' -and $HeadlessOnly)){continue}
        $out=Join-Path $projectDir "Logs/validation/survival_$tag.$mode.log"
        $err=Join-Path $projectDir "Logs/validation/survival_$tag.$mode.err"
        $runArgs=@('--path',$mirror.Project,'--resolution','1600x900','--log-file',(Join-Path $projectDir "Logs/validation/survival_$tag.$mode.engine.log"),'res://Scenes/Debug/_SurvivalModeProbe.tscn')
        if($mode -eq 'headless'){$runArgs+='--headless'}
        $result=Invoke-ValidationEngine $engine $runArgs $projectDir $out $err 150
        if($result.Output -notmatch '\[Survival\] SUMMARY pass=\d+ fail=0'){throw "$mode未完成全部机制断言"}
        $runs.Add([ordered]@{Name=$mode;Exit=$result.ExitCode;Seconds=$result.Seconds;Stdout=$out;Stderr=$err;SHA256=(Get-FileHash -LiteralPath $out).Hash})
        Assert-ValidationSaves $profile $saves
        Write-Output "PASS survival $mode"
    }
    $report.Passed=$true
}catch{$report.Failure=$_.ToString();throw}
finally {
    try{Assert-ValidationSaves $profile $saves;$report.SavesUnchanged=$true}
    finally{
        $path=Join-Path $projectDir "Logs/evidence/survival/run-$tag.json"
        try{$report|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $path -Encoding utf8}
        finally{$lease.Dispose()}
        Write-Output "生存模式验证：$path"
    }
}
