param([int]$LockWaitSeconds=120)
$ErrorActionPreference='Stop'
$projectDir=Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'LogPaths.ps1')
. (Join-Path $PSScriptRoot 'Validation.Common.ps1')
$engine='F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profile=Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$tag=Get-Date -Format 'yyyyMMdd_HHmmss'
$lease=$null;$snapshot=$null
try {
    $lease=Open-ProjectValidationLease $projectDir $LockWaitSeconds
    $snapshot=Get-ValidationSaveSnapshot $profile
    $mirror=New-ValidationMirror $projectDir 'clover_regression' -ShareAssembly
    $results=@()
    $cases=@(
        @{Name='charge'; Scene='_ChargeReleaseProbe'; Pattern='\[ChargeRelease\] === 蓄力释放验收结束：通过 \d+，失败 0'; Headless=$true},
        @{Name='interruption'; Scene='_ActionInterruptionProbe'; Pattern='\[Action\] === 动作交接验收结束：通过 \d+，失败 0'; Headless=$true},
        @{Name='replacement'; Scene='_CharacterReplacementProbe'; Pattern='\[CharacterReplacement\] 完成：通过 \d+，失败 0'; Headless=$false}
    )
    foreach($case in $cases) {
        $runArgs=@('--path',$mirror.Project,'--resolution','1280x720')
        if($case.Headless) {$runArgs+= '--headless'}
        $runArgs+= "res://Scenes/Debug/$($case.Scene).tscn"
        $stdout=Join-Path $projectDir "Logs/validation/clover_regression_$tag.$($case.Name).log"
        $stderr=Join-Path $projectDir "Logs/validation/clover_regression_$tag.$($case.Name).err"
        $run=Invoke-ValidationEngine $engine $runArgs $projectDir $stdout $stderr 210
        if($run.Output -notmatch $case.Pattern) {throw "旧流程复验未通过：$stdout"}
        $results+=@{name=$case.Name;passed=$true;log=$stdout;seconds=$run.Seconds}
    }
    $mainOut=Join-Path $projectDir "Logs/validation/clover_regression_$tag.main.log"
    $mainErr=Join-Path $projectDir "Logs/validation/clover_regression_$tag.main.err"
    $main=Invoke-ValidationEngine $engine @('--headless','--path',$mirror.Project,'res://Scenes/Main.tscn','--','--selftest') $projectDir $mainOut $mainErr 210
    Assert-MainValidation $projectDir $main.Output | Out-Host
    $results+=@{name='Main';passed=$true;log=$mainOut;seconds=$main.Seconds;hard_metrics=8}
    Assert-ValidationSaves $profile $snapshot
    $report=@{passed=$true;checks=$results;original_saves_unchanged=$true}
    [IO.File]::WriteAllText((Join-Path $projectDir 'Logs/evidence/clover-combat/regression.json'),($report|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
    Write-Output "PASS clover-regression $tag"
} finally {
    try {if($null-ne $snapshot){Assert-ValidationSaves $profile $snapshot}}
    finally {if($lease){$lease.Dispose()}}
}
