param([switch]$SkipBuild,[switch]$RenderOnly,[string[]]$Only=@('VisualRevision','BuildRewards','EnemyWarning','Codex','ContentLocalization','ContentDecoupling','RewardRules'))
$ErrorActionPreference='Stop'
$projectRoot='F:\Godot项目\项目a'
Set-Location -LiteralPath $projectRoot
. "$projectRoot\_tools\Validation.Common.ps1"
$engine='F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$out="$projectRoot\outputs\visual-047-048-20261028\implementation"
$lease=Open-ProjectValidationLease $projectRoot 45
$profile=Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$saves=Get-ValidationSaveSnapshot $profile
try {
    if(-not $SkipBuild) { foreach($configuration in @('Debug','ExportRelease')) { $null=Invoke-ValidationBuild $projectRoot $configuration "$out\build-$configuration.log" } }
    $mirror=New-ValidationMirror $projectRoot 'visual_revision' -ShareAssembly
    $mirror | Select-Object Project,Profile | ConvertTo-Json | Set-Content -LiteralPath "$out\mirror.json" -Encoding utf8
    $settingsPath=Join-Path $mirror.Project 'project.godot'
    $settings=[IO.File]::ReadAllText($settingsPath)
    $settings=[regex]::Replace($settings,'(?ms)(\[editor_plugins\]\s*)enabled\s*=\s*PackedStringArray\([^\r\n]*\)','$1enabled=PackedStringArray()')
    [IO.File]::WriteAllText($settingsPath,$settings,[Text.UTF8Encoding]::new($false))
    $null=Invoke-ValidationEngine $engine @('--headless','--path',$mirror.Project,'--editor','--import') $projectRoot "$out\import.log" "$out\import.err" 150
    foreach($case in $Only) {
        $modes=if($RenderOnly){@('render')}elseif($case -in @('VisualRevision','BuildRewards','EnemyWarning','Codex')){@('headless','render')}else{@('headless')}
        foreach($mode in $modes) {
            $runArgs=@('--path',$mirror.Project,'--resolution','1920x1080',"res://Scenes/Debug/_${case}Probe.tscn")
            if($mode -eq 'headless'){$runArgs+='--headless'}
            $result=Invoke-ValidationEngine $engine $runArgs $projectRoot "$out\$case-$mode.log" "$out\$case-$mode.err" 180
            Write-Output "$case $mode exit=$($result.ExitCode) seconds=$($result.Seconds)"
            Assert-ValidationSaves $profile $saves
        }
    }
    Assert-ValidationSaves $profile $saves
    [IO.File]::WriteAllText("$out\saves-unchanged.txt",'五份真实存档逐字节不变。')
}
finally {$lease.Dispose()}
