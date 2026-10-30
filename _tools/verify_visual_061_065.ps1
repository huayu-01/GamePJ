param([string]$Round='final',[switch]$SkipBuild,[switch]$Regression,[string]$Only='')
$ErrorActionPreference='Stop'
$root='F:\Godot项目\项目a'
Set-Location -LiteralPath $root
. "$root\_tools\Validation.Common.ps1"
$out="$root\Logs\validation\visual-061-065\$Round"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$engine='F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$lease=Open-ProjectValidationLease $root 180
$profile=Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$saves=Get-ValidationSaveSnapshot $profile
try {
    if(-not $SkipBuild) { foreach($configuration in @('Debug','ExportRelease')) { $null=Invoke-ValidationBuild $root $configuration "$out\build-$configuration.log" } }
    $mirror=New-ValidationMirror $root 'visual_charge' -ShareAssembly
    $mirror | Select-Object Project,Profile | ConvertTo-Json | Set-Content -LiteralPath "$out\mirror.json" -Encoding utf8
    $settingsPath=Join-Path $mirror.Project 'project.godot'
    $settings=[regex]::Replace([IO.File]::ReadAllText($settingsPath),'(?ms)(\[editor_plugins\]\s*)enabled\s*=\s*PackedStringArray\([^\r\n]*\)','$1enabled=PackedStringArray()')
    [IO.File]::WriteAllText($settingsPath,$settings,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $mirror.Profile 'settings.cfg'),"[graphics]`nwindow_mode=0`nwidth=1920`nheight=1080`n",[Text.UTF8Encoding]::new($false))
    $null=Invoke-ValidationEngine $engine @('--headless','--path',$mirror.Project,'--editor','--import') $root "$out\import.log" "$out\import.err" 150
    $cases=@(@{Name='VisualChargeMilestone';Mode='headless'},@{Name='VisualChargeMilestone';Mode='render'})
    if($Regression) { $cases+=@(@{Name='ChargeRelease';Mode='headless'},@{Name='VisualReadability';Mode='headless'},@{Name='Codex';Mode='render'},@{Name='ContentLocalization';Mode='headless'},@{Name='RewardRules';Mode='headless'},@{Name='BatchE';Mode='headless'}) }
    if($Only) { $cases=@($cases | Where-Object { $_.Name -in $Only.Split(',') }) }
    foreach($case in $cases) {
        $scene=if($case.Name -eq 'BatchE'){'res://Scenes/UI/_BatchEProbe.tscn'}else{"res://Scenes/Debug/_$($case.Name)Probe.tscn"}
        $runArgs=@('--path',$mirror.Project,'--resolution','1920x1080',$scene)
        if($case.Mode -eq 'headless'){$runArgs+='--headless'}
        if($case.Name -eq 'VisualReadability'){$runArgs+=@('--','--rims-only')}
        $result=Invoke-ValidationEngine $engine $runArgs $root "$out\$($case.Name)-$($case.Mode).log" "$out\$($case.Name)-$($case.Mode).err" 180
        if($result.Output -notmatch 'SUMMARY pass=\d+ fail=0|验收结束：通过 \d+，失败 0|验收结束.*失败 0'){throw "未完成：$($case.Name)"}
        Assert-ValidationSaves $profile $saves
        Write-Output "PASS $($case.Name) $($case.Mode)"
    }
    $evidence="$root\Logs\evidence\visual-061-065"
    New-Item -ItemType Directory -Path $evidence -Force | Out-Null
    Get-ChildItem -LiteralPath "$root\Logs\evidence\elites" -File | Where-Object { $_.Name -match '^(061-|065-|VisualChargeMilestone)' } | Copy-Item -Destination $evidence
    [IO.File]::WriteAllText("$out\saves-unchanged.txt",'五份真实存档逐字节不变。')
} finally {$lease.Dispose()}
