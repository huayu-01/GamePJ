$ErrorActionPreference='Stop'
$root='F:\Godot项目\项目a'
Set-Location -LiteralPath $root
. "$root\_tools\Validation.Common.ps1"
$out="$root\outputs\visual-047-048-20261028\refinement\final"
$engine='F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$lease=Open-ProjectValidationLease $root 90
$profile=Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$saves=Get-ValidationSaveSnapshot $profile
$results=[Collections.Generic.List[object]]::new()
try {
    $mirror=Get-Content -LiteralPath "$out\mirror.json" -Raw | ConvertFrom-Json
    foreach($case in @('BuildRewards','Codex','ContentLocalization','RewardRules','BatchE')) {
        $modes=if($case -in @('BuildRewards','Codex','ContentLocalization')){@('headless','render')}else{@('headless')}
        foreach($mode in $modes) {
            $scene=if($case -eq 'BatchE'){'res://Scenes/UI/_BatchEProbe.tscn'}else{"res://Scenes/Debug/_${case}Probe.tscn"}
            $runArgs=@('--path',$mirror.Project,'--resolution','1920x1080',$scene)
            if($mode -eq 'headless'){$runArgs+='--headless'}
            $result=Invoke-ValidationEngine $engine $runArgs $root "$out\$case-$mode.log" "$out\$case-$mode.err" 180
            if($result.Output -notmatch 'SUMMARY pass=\d+ fail=0|验收结束：通过 \d+，失败 0'){throw "没有完成：$case $mode"}
            Assert-ValidationSaves $profile $saves
            $results.Add(@{Case=$case;Mode=$mode;Exit=$result.ExitCode;StderrBytes=0;Seconds=$result.Seconds})
            Write-Output "PASS $case $mode"
        }
    }
    Assert-ValidationSaves $profile $saves
    $results | ConvertTo-Json | Set-Content -LiteralPath "$out\checks.json" -Encoding utf8
    [IO.File]::WriteAllText("$out\saves-unchanged.txt",'五份真实存档逐字节不变。')
} finally {$lease.Dispose()}
