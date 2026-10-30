$ErrorActionPreference='Stop'
$projectRoot='F:\Godot项目\项目a'
Set-Location -LiteralPath $projectRoot
. "$projectRoot\_tools\Validation.Common.ps1"
$engine='F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$out="$projectRoot\outputs\visual-047-048-20261028\implementation"
$lease=Open-ProjectValidationLease $projectRoot 90
$profile=Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$saves=Get-ValidationSaveSnapshot $profile
Add-Type -AssemblyName System.Drawing
try {
    $mirror=Get-Content -LiteralPath "$out\mirror.json" -Raw | ConvertFrom-Json
    foreach($resolution in @('1280x720','3840x2160')) {
        $size=$resolution.Split('x')
        # 设置启动应用会覆盖命令行尺寸；只改隔离档的画面设置。
        [IO.File]::WriteAllText((Join-Path $mirror.Profile 'settings.cfg'),"[graphics]`nwindow_mode=0`nwidth=$($size[0])`nheight=$($size[1])`n",[Text.UTF8Encoding]::new($false))
        $runArgs=@('--path',$mirror.Project,'--resolution',$resolution,'res://Scenes/Debug/_VisualRevisionProbe.tscn')
        $result=Invoke-ValidationEngine $engine $runArgs $projectRoot "$out\VisualRevision-$resolution.log" "$out\VisualRevision-$resolution.err" 180
        if($result.Output -notmatch 'SUMMARY pass=\d+ fail=0'){throw "画面专项未完成：$resolution"}
        foreach($name in @('048-reward-glass','048-build-glass','048-codex-glass','047-brush-current')) {
            $image=[Drawing.Image]::FromFile("$projectRoot\Logs\evidence\elites\$name.png")
            try{if($image.Width -ne [int]$size[0] -or $image.Height -ne [int]$size[1]){throw "实际截图尺寸不符：$resolution，得到$($image.Width)x$($image.Height)"}}finally{$image.Dispose()}
            Copy-Item -LiteralPath "$projectRoot\Logs\evidence\elites\$name.png" -Destination "$out\screenshots\$name-$resolution.png"
        }
        Assert-ValidationSaves $profile $saves
        Write-Output "PASS $resolution"
    }
    [IO.File]::WriteAllText((Join-Path $mirror.Profile 'settings.cfg'),"[graphics]`nwindow_mode=0`nwidth=1920`nheight=1080`n",[Text.UTF8Encoding]::new($false))
    $result=Invoke-ValidationEngine $engine @('--path',$mirror.Project,'--resolution','1920x1080','res://Scenes/Debug/_ContentLocalizationProbe.tscn') $projectRoot "$out\ContentLocalization-render.log" "$out\ContentLocalization-render.err" 150
    if($result.Output -notmatch 'SUMMARY pass=\d+ fail=0'){throw '英文渲染专项未完成'}
    Copy-Item -LiteralPath "$projectRoot\Logs\evidence\content-localization-en.png" -Destination "$out\screenshots\048-reward-english-1080p.png"
    Assert-ValidationSaves $profile $saves
    Write-Output 'PASS 英文界面'
} finally {$lease.Dispose()}
