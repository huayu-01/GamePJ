$ErrorActionPreference = 'Stop'
$projectRoot = 'F:\Godot项目\项目a'
Set-Location -LiteralPath $projectRoot
. "$projectRoot\_tools\Validation.Common.ps1"
$lease = Open-ProjectValidationLease $projectRoot 45
try {
    $mirror = New-ValidationMirror $projectRoot 'visual_047_048' -ShareAssembly
    Copy-Item -LiteralPath "$projectRoot\outputs\visual-047-048-20261028\support\capture.gd" -Destination "$($mirror.Project)\audit_capture.gd"
    $out = "$projectRoot\outputs\visual-047-048-20261028\evidence"
    $mirror | Select-Object Project,Profile,Player | ConvertTo-Json | Set-Content -LiteralPath "$out\capture-environment.json" -Encoding utf8
    $snapshot = Get-ValidationSaveSnapshot $mirror.Player
    $result = Invoke-ValidationEngine 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' @('--path',$mirror.Project,'--script','res://audit_capture.gd','--resolution','1920x1080','--position','0,0','--log-file',"$out\capture-engine.log") $projectRoot "$out\capture-stdout.log" "$out\capture-stderr.log" 150
    Assert-ValidationSaves $mirror.Player $snapshot
    [IO.File]::WriteAllText("$out\save-integrity.txt",'真实五份存档与运行前逐字节一致。')
    $result | Select-Object ExitCode,Seconds
}
finally { $lease.Dispose() }
