param([string]$Round='round-1',[switch]$SkipBuild)
$ErrorActionPreference='Stop'
$root='F:\Godot项目\项目a'
Set-Location -LiteralPath $root
. "$root\_tools\Validation.Common.ps1"
$out="$root\Logs\validation\foliage-zones\$Round"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$lease=Open-ProjectValidationLease $root 180
$profile=Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$saves=Get-ValidationSaveSnapshot $profile
$engine='F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
try {
    if(-not $SkipBuild){foreach($configuration in @('Debug','ExportRelease')){$null=Invoke-ValidationBuild $root $configuration "$out\build-$configuration.log"}}
    $mirror=New-ValidationMirror $root 'foliage_zones' -ShareAssembly
    $mirror | Select-Object Project,Profile | ConvertTo-Json | Set-Content -LiteralPath "$out\mirror.json" -Encoding utf8
    $settingsPath=Join-Path $mirror.Project 'project.godot'
    $settings=[regex]::Replace([IO.File]::ReadAllText($settingsPath),'(?ms)(\[editor_plugins\]\s*)enabled\s*=\s*PackedStringArray\([^\r\n]*\)','$1enabled=PackedStringArray()')
    [IO.File]::WriteAllText($settingsPath,$settings,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $mirror.Profile 'settings.cfg'),"[graphics]`nwindow_mode=0`nwidth=1920`nheight=1080`n",[Text.UTF8Encoding]::new($false))
    $null=Invoke-ValidationEngine $engine @('--headless','--path',$mirror.Project,'--editor','--import') $root "$out\import.log" "$out\import.err" 150
    foreach($mode in @('headless','render')){
        $runArgs=@('--path',$mirror.Project,'res://Scenes/Debug/GrassShaderLab.tscn')
        if($mode -eq 'headless'){$runArgs+='--headless'}
        $runArgs+=@('--','--foliage-zones')
        $result=Invoke-ValidationEngine $engine $runArgs $root "$out\$mode.log" "$out\$mode.err" 180
        if($result.Output -notmatch 'SUMMARY pass=\d+ fail=0'){throw "树叶禁草区$mode未通过"}
        Assert-ValidationSaves $profile $saves
        Write-Output "PASS $mode"
    }
    $result=Invoke-ValidationEngine $engine @('--path',$mirror.Project,'res://Scenes/Debug/GrassShaderLab.tscn','--','--grass-playcheck') $root "$out\playable.log" "$out\playable.err" 60
    if($result.Output -notmatch 'SUMMARY pass=\d+ fail=0'){throw '丛林试玩检查未通过'}
    Write-Output 'PASS playable'
    Copy-Item -Path "$root\Logs\evidence\foliage-zones\*" -Destination $out
    [IO.File]::WriteAllText("$out\saves-unchanged.txt",'五份真实存档逐字节不变。')
} finally {try{Assert-ValidationSaves $profile $saves}finally{$lease.Dispose()}}
