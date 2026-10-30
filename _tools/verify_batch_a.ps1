# A 批可重复验收：编译、完整自测、真实 GUI 与下落实拍。仅管理自己启动的进程。
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
. (Join-Path $projectDir '_tools/LogPaths.ps1')
Initialize-ProjectLogPaths -ProjectRoot $projectDir
$godotExe = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
. (Join-Path $PSScriptRoot 'Validation.Legacy.ps1')
Initialize-LegacyValidation 'a'
$evidence = [Collections.Generic.List[string]]::new()

$selftestFlag = Join-Path $profileDir 'selftest.flag'

Push-Location $projectDir
try {
    Invoke-CheckedBuild 'Debug'
    Invoke-BatchProcess 'selftest' @('--headless', '--path', $projectDir, '--quit-after', '40000', 'res://Scenes/Main.tscn', '--', '--selftest')
    $selftestOutput = Get-Content -LiteralPath (Join-Path $projectDir 'Logs/validation/.ai_a_selftest.log') -Raw
    if ($selftestOutput -notmatch '=== 自测结束 ===') { throw '完整自测没有走到结束标记' }
    Invoke-BatchProcess 'probe_final' @('--path', $projectDir, '--resolution', '1920x1080', '--quit-after', '3000', 'res://Scenes/UI/_BatchAProbe.tscn')
    $probeOutput = Get-Content -LiteralPath (Join-Path $projectDir 'Logs/validation/.ai_a_probe_final.log') -Raw
    if ($probeOutput -notmatch 'A 批验收结束：失败 0') { throw 'A 批验收未完整通过' }
    $saveDir = $profileDir
    foreach ($saveName in @('meta.cfg', 'ability_cards.cfg', 'settings.cfg')) {
        $savePath = Join-Path $saveDir $saveName
        if (Test-Path -LiteralPath $savePath) {
            "存档哈希 $saveName=$((Get-FileHash -LiteralPath $savePath -Algorithm SHA256).Hash)"
        }
    }
}
finally {
    if (Test-Path -LiteralPath $selftestFlag) { Remove-Item -LiteralPath $selftestFlag }
    "selftest.flag 已删除=$(-not (Test-Path -LiteralPath $selftestFlag))"
    Pop-Location
}
