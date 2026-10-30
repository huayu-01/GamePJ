# 精英特性真实无头／渲染专项；共用锁、隔离档和严格错误检查。
param([switch]$SkipBuild,[switch]$SkipImport,[switch]$HeadlessOnly,[switch]$RenderOnly,[string[]]$Only=@())
$ErrorActionPreference='Stop'
if($HeadlessOnly -and $RenderOnly){throw '不能同时指定两种唯一运行模式。'}
$projectDir=Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Validation.Common.ps1')
. (Join-Path $PSScriptRoot 'LogPaths.ps1')
Initialize-ProjectLogPaths -ProjectRoot $projectDir
New-Item -ItemType Directory -Path (Join-Path $projectDir 'Logs/evidence/elites') -Force | Out-Null
$engine='F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profile=Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$cases=@('EliteDefense','EliteHazards','EliteMotion','EliteIntegration','BalanceTuning','SceneRunes','Content032037','Content039044','Codex','Content055056','VisualRevision','PauseDive','Content049054','CombatArt','Content057060','GrowthBuilds','Content062067','SurvivalMilestoneRetry','Content068070','Builds20General','Builds20Hunter','Builds20Master')
foreach($name in $Only){if($name -notin $cases){throw "未知专项：$name"}}
$tag=Get-Date -Format 'yyyyMMdd_HHmmss'
$lease=Open-ProjectValidationLease $projectDir 120
$saves=Get-ValidationSaveSnapshot $profile
$runs=[Collections.Generic.List[object]]::new()
$report=[ordered]@{Date=(Get-Date -Format 's');Passed=$false;Runs=$runs;SavesUnchanged=$false;Failure=''}
try{
    if(-not $SkipBuild){foreach($configuration in @('Debug','ExportRelease')){$null=Invoke-ValidationBuild $projectDir $configuration (Join-Path $projectDir "Logs/validation/elites_$tag.$configuration.log")}}
    $mirror=New-ValidationMirror $projectDir 'elites' -ShareAssembly
    if(-not $SkipImport){
        $settingsPath=Join-Path $mirror.Project 'project.godot'
        $settings=[IO.File]::ReadAllText($settingsPath)
        $settings=[regex]::Replace($settings,'(?ms)(\[editor_plugins\]\s*)enabled\s*=\s*PackedStringArray\([^\r\n]*\)','$1enabled=PackedStringArray()')
        [IO.File]::WriteAllText($settingsPath,$settings,[Text.UTF8Encoding]::new($false))
        $null=Invoke-ValidationEngine $engine @('--headless','--path',$mirror.Project,'--editor','--import') $projectDir (Join-Path $projectDir "Logs/validation/elites_$tag.import.log") (Join-Path $projectDir "Logs/validation/elites_$tag.import.err") 150
    }
    foreach($case in $cases){
        if($Only.Count -and $case -notin $Only){continue}
        foreach($mode in @('headless','render')){
            if(($HeadlessOnly -and $mode -eq 'render') -or ($RenderOnly -and $mode -eq 'headless')){continue}
            $out=Join-Path $projectDir "Logs/validation/elites_$tag.$case.$mode.log"; $err=Join-Path $projectDir "Logs/validation/elites_$tag.$case.$mode.err"
            $runArgs=@('--path',$mirror.Project,'--resolution','1600x900',"res://Scenes/Debug/_${case}Probe.tscn")
            if($mode -eq 'headless'){$runArgs+='--headless'}
            $result=Invoke-ValidationEngine $engine $runArgs $projectDir $out $err 240
            if($result.Output -notmatch "\[$case\] SUMMARY pass=\d+ fail=0"){throw "$case $mode缺少全部通过标记"}
            $runs.Add([ordered]@{Name=$case;Mode=$mode;Exit=$result.ExitCode;Seconds=$result.Seconds;Stdout=$out;Stderr=$err;SHA256=(Get-FileHash -LiteralPath $out).Hash})
            Assert-ValidationSaves $profile $saves; Write-Output "PASS $case $mode"
        }
    }
    $report.Passed=$true
}catch{$report.Failure=$_.ToString();throw}
finally{
    try{Assert-ValidationSaves $profile $saves;$report.SavesUnchanged=$true}
    finally{
        $path=Join-Path $projectDir "Logs/evidence/elites/run-$tag.json"
        try{$report|ConvertTo-Json -Depth 7|Set-Content -LiteralPath $path -Encoding utf8}finally{$lease.Dispose()}
        Write-Output "精英验收：$path"
    }
}
