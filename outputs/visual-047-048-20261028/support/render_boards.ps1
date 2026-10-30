$ErrorActionPreference='Stop'
$projectRoot='F:\Godot项目\项目a'
Set-Location -LiteralPath $projectRoot
. "$projectRoot\_tools\Validation.Common.ps1"
$lease=Open-ProjectValidationLease $projectRoot 45
try {
    $out="$projectRoot\outputs\visual-047-048-20261028"
    Invoke-ValidationEngine 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' @('--path',"$out\support\render_project",'--script','res://render.gd','--resolution','2048x1536','--log-file',"$out\evidence\board-engine.log") $projectRoot "$out\evidence\board-stdout.log" "$out\evidence\board-stderr.log" 90 | Select-Object ExitCode,Seconds
}
finally { $lease.Dispose() }
