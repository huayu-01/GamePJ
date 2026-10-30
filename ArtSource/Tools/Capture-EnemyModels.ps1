param([ValidateSet('pair','mushroom','mage','mushroom-burrow','mage-cast')][string]$View = 'pair',[switch]$Original)
# 仅启动并清理本脚本的预览进程，逐次核对五份玩家存档。
$ErrorActionPreference = 'Stop'
$projectDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $projectDir '_tools/LogPaths.ps1')
Initialize-ProjectLogPaths -ProjectRoot $projectDir
$enginePath = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$profileDir = Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$saveNames = @('meta.cfg','ability_cards.cfg','settings.cfg','loadout.cfg','rune.cfg')
$before = @{}
$lease = [IO.File]::Open((Join-Path $projectDir '.ai_validation.lock'),'OpenOrCreate','ReadWrite','None')
$ownedProcess = $null
try {
    foreach ($name in $saveNames) {
        $path = Join-Path $profileDir $name
        $before[$name] = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { 'ABSENT' }
    }
    $stdout = Join-Path $projectDir "Logs/validation/.ai_enemy_models_$View.log"
    $stderr = Join-Path $projectDir "Logs/validation/.ai_enemy_models_$View.err"
    $runArgs = @('--path',$projectDir,'--resolution','1600x900','res://Scenes/Debug/NatureArtLab.tscn','--',"--enemy-art-capture=$View")
    if ($Original) { $runArgs += '--enemy-art-original' }
    $ownedProcess = Start-Process -FilePath $enginePath -ArgumentList $runArgs -WorkingDirectory $projectDir -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    Write-Output "模型预览 PID=$($ownedProcess.Id) view=$View"
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while (-not $ownedProcess.HasExited -and $timer.Elapsed.TotalSeconds -lt 70) {
        Start-Sleep -Milliseconds 250
        $ownedProcess.Refresh()
    }
    if (-not $ownedProcess.HasExited) { throw "模型预览超时，日志：$stdout / $stderr" }
    $out = [IO.File]::ReadAllText($stdout)
    $err = [IO.File]::ReadAllText($stderr)
    Write-Output "exit=$($ownedProcess.ExitCode) stderrBytes=$((Get-Item -LiteralPath $stderr).Length)"
    Write-Output ($out -split "`n" | Where-Object { $_ -like '*[[]EnemyArt[]]*' })
    if ($err) { Write-Output $err }
    if ($ownedProcess.ExitCode -ne 0 -or $err.Length -gt 0 -or $out -notmatch '"passed":true') { throw "模型预览未通过，日志：$stdout / $stderr" }
}
finally {
    if ($ownedProcess -and -not $ownedProcess.HasExited) { Stop-Process -Id $ownedProcess.Id }
    foreach ($name in $saveNames) {
        $path = Join-Path $profileDir $name
        $after = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { 'ABSENT' }
        if ($before.ContainsKey($name) -and $after -cne $before[$name]) { Write-Error "存档发生变化：$name；保留现场，不覆盖手动改动。" }
    }
    $lease.Dispose()
}
