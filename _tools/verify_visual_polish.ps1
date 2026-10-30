param([string]$Round='round-1',[switch]$SkipBuild,[switch]$Release,[switch]$Regression,[string]$Resolution='1920x1080')
$ErrorActionPreference='Stop'
$projectRoot='F:\Godot项目\项目a'
Set-Location -LiteralPath $projectRoot
. "$projectRoot\_tools\Validation.Common.ps1"
$engine='F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$out="$projectRoot\outputs\visual-polish-20261029\$Round"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$lease=Open-ProjectValidationLease $projectRoot 180
$profile=Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$saves=Get-ValidationSaveSnapshot $profile
try {
    if(-not $SkipBuild){$null=Invoke-ValidationBuild $projectRoot 'Debug' "$out\build-Debug.log"}
    if($Release){$null=Invoke-ValidationBuild $projectRoot 'ExportRelease' "$out\build-ExportRelease.log"}
    $mirror=New-ValidationMirror $projectRoot 'visual_polish' -ShareAssembly
    $mirror | Select-Object Project,Profile | ConvertTo-Json | Set-Content -LiteralPath "$out\mirror.json" -Encoding utf8
    $settingsPath=Join-Path $mirror.Project 'project.godot'
    $settings=[IO.File]::ReadAllText($settingsPath)
    $settings=[regex]::Replace($settings,'(?ms)(\[editor_plugins\]\s*)enabled\s*=\s*PackedStringArray\([^\r\n]*\)','$1enabled=PackedStringArray()')
    [IO.File]::WriteAllText($settingsPath,$settings,[Text.UTF8Encoding]::new($false))
    $size=$Resolution.Split('x')
    [IO.File]::WriteAllText((Join-Path $mirror.Profile 'settings.cfg'),"[graphics]`nwindow_mode=0`nwidth=$($size[0])`nheight=$($size[1])`n",[Text.UTF8Encoding]::new($false))
    $null=Invoke-ValidationEngine $engine @('--headless','--path',$mirror.Project,'--editor','--import') $projectRoot "$out\import.log" "$out\import.err" 150
    $result=Invoke-ValidationEngine $engine @('--path',$mirror.Project,'--resolution',$Resolution,'res://Scenes/Debug/_VisualRevisionProbe.tscn') $projectRoot "$out\visual.log" "$out\visual.err" 180
    if($result.Output -notmatch 'SUMMARY pass=\d+ fail=0'){throw '视觉专项未完成'}
    foreach($name in @('048-reward-glass','048-build-glass','048-codex-glass','048-codex-creature','047-brush-current')){Copy-Item -LiteralPath "$projectRoot\Logs\evidence\elites\$name.png" -Destination "$out\$name.png"}
    if($Regression) {
        foreach($case in @('BuildRewards','Codex','ContentLocalization','RewardRules','BatchE')) {
            $scene=if($case -eq 'BatchE'){'res://Scenes/UI/_BatchEProbe.tscn'}else{"res://Scenes/Debug/_${case}Probe.tscn"}
            $result=Invoke-ValidationEngine $engine @('--headless','--path',$mirror.Project,$scene) $projectRoot "$out\$case.log" "$out\$case.err" 180
            if($result.Output -notmatch 'SUMMARY pass=\d+ fail=0|验收结束：通过 \d+，失败 0'){throw "没有完成：$case"}
        }
    }
    Assert-ValidationSaves $profile $saves
    [IO.File]::WriteAllText("$out\saves-unchanged.txt",'五份真实存档逐字节不变。')
    Write-Output "PASS $Round $Resolution"
} finally {$lease.Dispose()}
