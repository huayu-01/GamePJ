# A 批可重复验收：编译、完整自测、真实 GUI 与下落实拍。仅管理自己启动的进程。
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
$godotExe = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$selftestFlag = Join-Path $env:APPDATA 'Godot\app_userdata\ACT Roguelike\selftest.flag'

function Invoke-BatchProcess([string]$Name, [string[]]$RunArgs) {
    $stdoutPath = Join-Path $projectDir ".ai_a_$Name.log"
    $stderrPath = Join-Path $projectDir ".ai_a_$Name.err"
    $ownedProcess = Start-Process -FilePath $godotExe -ArgumentList $RunArgs -WindowStyle Hidden `
        -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
    $deadline = (Get-Date).AddSeconds(150)
    while (-not $ownedProcess.HasExited -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
        $ownedProcess.Refresh()
    }
    if (-not $ownedProcess.HasExited) {
        Stop-Process -Id $ownedProcess.Id
        throw "$Name 超过150秒，已只关闭本脚本启动的 PID $($ownedProcess.Id)"
    }
    "$Name 退出码=$($ownedProcess.ExitCode) stdout=$stdoutPath stderr=$stderrPath"
    if ($ownedProcess.ExitCode -ne 0) { throw "$Name 失败，请查看日志" }
}

Push-Location $projectDir
try {
    & (Join-Path $projectDir 'build.ps1') --no-incremental -v minimal
    if ($LASTEXITCODE -ne 0) { throw '编译失败' }
    Invoke-BatchProcess 'selftest' @('--headless', '--path', $projectDir, '--quit-after', '40000', 'res://Scenes/Main.tscn', '--', '--selftest')
    $selftestOutput = Get-Content -LiteralPath (Join-Path $projectDir '.ai_a_selftest.log') -Raw
    if ($selftestOutput -notmatch '=== 自测结束 ===') { throw '完整自测没有走到结束标记' }
    Invoke-BatchProcess 'probe_final' @('--path', $projectDir, '--resolution', '1920x1080', '--quit-after', '3000', 'res://Scenes/UI/_BatchAProbe.tscn')
    $probeOutput = Get-Content -LiteralPath (Join-Path $projectDir '.ai_a_probe_final.log') -Raw
    if ($probeOutput -notmatch 'A 批验收结束：失败 0') { throw 'A 批验收未完整通过' }
    $saveDir = Join-Path $env:APPDATA 'Godot\app_userdata\ACT Roguelike'
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
