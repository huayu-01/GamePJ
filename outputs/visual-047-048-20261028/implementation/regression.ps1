param([switch]$SkipBuild,[switch]$Resume)
$ErrorActionPreference='Stop'
$projectRoot='F:\Godot项目\项目a'
Set-Location -LiteralPath $projectRoot
. "$projectRoot\_tools\Validation.Common.ps1"
$engine='F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$out="$projectRoot\outputs\visual-047-048-20261028\implementation"
$lease=Open-ProjectValidationLease $projectRoot 90
$profile=Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$saves=Get-ValidationSaveSnapshot $profile
$runs=[Collections.Generic.List[object]]::new()
$failures=[Collections.Generic.List[string]]::new()
try {
    if(-not $SkipBuild) { foreach($configuration in @('Debug','ExportRelease')) { $null=Invoke-ValidationBuild $projectRoot $configuration "$out\build-$configuration.log" } }
    $mirror=New-ValidationMirror $projectRoot 'visual_regression' -ShareAssembly
    $settingsPath=Join-Path $mirror.Project 'project.godot'
    $settings=[IO.File]::ReadAllText($settingsPath)
    $settings=[regex]::Replace($settings,'(?ms)(\[editor_plugins\]\s*)enabled\s*=\s*PackedStringArray\([^\r\n]*\)','$1enabled=PackedStringArray()')
    [IO.File]::WriteAllText($settingsPath,$settings,[Text.UTF8Encoding]::new($false))
    $null=Invoke-ValidationEngine $engine @('--headless','--path',$mirror.Project,'--editor','--import') $projectRoot "$out\regression-import.log" "$out\regression-import.err" 150
    $cases=@(
        @{Name='ChargeRelease';Scene='Debug/_ChargeReleaseProbe';Pattern='蓄力释放验收结束：通过 80，失败 0'},
        @{Name='CharacterDesign';Scene='Debug/_CharacterDesignProbe';Pattern='原始设计验收结束：通过 115，失败 0'},
        @{Name='DragonArrow';Scene='Debug/_DragonArrowProbe';Pattern='验收结束：通过 42，失败 0'},
        @{Name='BowSweep';Scene='Debug/_BowSweepProbe';Pattern='验收结束：通过 39，失败 0'},
        @{Name='Rune';Scene='Debug/_RuneProbe';Pattern='验收结束：通过 83，失败 0'},
        @{Name='BatchE';Scene='UI/_BatchEProbe';Pattern='E 批验收结束：通过 \d+，失败 0'},
        @{Name='SurvivalMode';Scene='Debug/_SurvivalModeProbe';Pattern='SUMMARY pass=\d+ fail=0'},
        @{Name='Main';Scene='Main';Pattern='=== 自测结束 ==='},
        @{Name='CharacterReplacement';Scene='Debug/_CharacterReplacementProbe';Pattern='完成：通过 \d+，失败 0';Render=$true},
        @{Name='ActionInterruption';Scene='Debug/_ActionInterruptionProbe';Pattern='动作交接验收结束：通过 38，失败 0'}
    )
    foreach($case in $cases) {
        try {
            $log="$out\$($case.Name)-regression.log"; $err="$out\$($case.Name)-regression.err"
            if($Resume -and (Test-Path -LiteralPath $log) -and (Test-Path -LiteralPath $err)) {
                $result=[pscustomobject]@{Output=[IO.File]::ReadAllText($log);ExitCode=0;Seconds=0}
                if((Get-Item -LiteralPath $err).Length -gt 0){throw "保留上次失败：$($case.Name)；stderr非空"}
            } else {
                $args=@('--path',$mirror.Project,'--resolution','1920x1080',"res://Scenes/$($case.Scene).tscn")
                if(-not $case.Render){$args+='--headless'}
                if($case.Name -eq 'Main'){$args+=@('--','--selftest')}
                $result=Invoke-ValidationEngine $engine $args $projectRoot $log $err 210
            }
            if($result.Output -notmatch $case.Pattern){throw "专项未完成：$($case.Name)"}
            if($case.Name -eq 'Main'){Assert-MainValidation $projectRoot $result.Output | Set-Content -LiteralPath "$out\main-indicators.txt" -Encoding utf8}
            $runs.Add(@{Name=$case.Name;Passed=$true;Exit=$result.ExitCode;Seconds=$result.Seconds;StderrBytes=0})
            Write-Output "PASS $($case.Name)"
        } catch {
            $failures.Add("$($case.Name)：$_")
			$runs.Add(@{Name=$case.Name;Passed=$false;Failure=$_.ToString()})
            Write-Output "FAIL $($case.Name)"
        } finally {Assert-ValidationSaves $profile $saves}
    }
    $runs | ConvertTo-Json | Set-Content -LiteralPath "$out\regression-results.json" -Encoding utf8
    Assert-ValidationSaves $profile $saves
    if($failures.Count){throw ($failures -join "`n")}
} finally {$lease.Dispose()}
