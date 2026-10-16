# E 批可重复验收；只管理本脚本启动的进程，不使用全局 stop_project。
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
$godotExe = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$saveDir = Join-Path $env:APPDATA 'Godot\app_userdata\ACT Roguelike'
$flagPath = Join-Path $saveDir 'selftest.flag'
$evidence = [System.Collections.Generic.List[string]]::new()
$saveHashes = @{}
foreach ($saveName in @('meta.cfg', 'ability_cards.cfg', 'settings.cfg')) {
    $savePath = Join-Path $saveDir $saveName
    $saveHashes[$saveName] = if (Test-Path -LiteralPath $savePath) { (Get-FileHash -LiteralPath $savePath -Algorithm SHA256).Hash } else { '不存在' }
}

function Invoke-BatchProcess([string]$Name, [string[]]$RunArgs) {
    $stdoutPath = Join-Path $projectDir ".ai_e_$Name.log"
    $stderrPath = Join-Path $projectDir ".ai_e_$Name.err"
    $ownedProcess = Start-Process -FilePath $godotExe -ArgumentList $RunArgs -WindowStyle Hidden `
        -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
    $deadline = (Get-Date).AddSeconds(150)
    while (-not $ownedProcess.HasExited -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
        $ownedProcess.Refresh()
    }
    if (-not $ownedProcess.HasExited) {
        Stop-Process -Id $ownedProcess.Id
        throw "$Name 超过150秒，已只退出本脚本启动的 PID $($ownedProcess.Id)"
    }
    $message = "$Name 退出码=$($ownedProcess.ExitCode)"
    Write-Output $message
    $evidence.Add($message)
    if ($ownedProcess.ExitCode -ne 0) { throw "$Name 失败：$stdoutPath" }
    # 基线已有的退出资源提示单独留档，禁止把新的运行错误算作通过。
    $unexpected = @(Get-Content -LiteralPath $stderrPath | Where-Object { $_ -match '^ERROR:' -and $_ -notmatch '^ERROR: 2 resources still in use at exit' })
    if ($unexpected.Count -gt 0) { throw "$Name 新运行错误：$($unexpected -join ' / ')" }
}

Push-Location $projectDir
try {
    & (Join-Path $projectDir 'build.ps1') --no-incremental -v minimal | Tee-Object -FilePath '.ai_e_build_final.log'
    if ($LASTEXITCODE -ne 0) { throw '编译失败' }
    $buildOutput = Get-Content '.ai_e_build_final.log' -Raw
    if ($buildOutput -notmatch '0 个警告' -or $buildOutput -notmatch '0 个错误') { throw '编译未满足0/0' }
    $evidence.Add('build --no-incremental -v minimal：0 warning / 0 error')
    Invoke-BatchProcess 'selftest_final' @('--headless', '--path', $projectDir, '--quit-after', '40000', 'res://Scenes/Main.tscn', '--', '--selftest')
    $selftestLines = Get-Content '.ai_e_selftest_final.log'
    if (($selftestLines -join "`n") -notmatch '=== 自测结束 ===') { throw '完整自测未到结束标记' }
    $baseline = Get-Content 'docs/a-batch-evidence.log'
    foreach ($marker in @('配置表：武器 6', '节点布置：启用', '配置表比对·玩家：', '配置表比对·索引：', 'Pressure 测试（进化', '白板首领实例：', '端到端：按 F 之后', '最小闭环·链路：')) {
        $before = ($baseline | Where-Object { $_.StartsWith('[基线逐字一致] ') -and $_.Contains($marker) } | Select-Object -First 1) -replace '^\[基线逐字一致\] ', ''
        $after = ($selftestLines | Where-Object { $_.Contains($marker) } | Select-Object -First 1) -replace '^\[log\] \S+ ', ''
        if ([string]::IsNullOrEmpty($before) -or $before -cne $after) { throw "硬指标改变：$marker" }
        $evidence.Add("[基线逐字一致] $after")
    }
    foreach ($line in $selftestLines | Where-Object { $_ -match 'P3·刹那之眼·瞄准与标记|P3·弓武技2 刹那之眼|=== 自测结束 ===' }) { $evidence.Add($line) }
    Invoke-BatchProcess 'probe_final' @('--path', $projectDir, '--resolution', '1920x1080', '--quit-after', '6000', 'res://Scenes/UI/_BatchEProbe.tscn')
    $probeLines = Get-Content '.ai_e_probe_final.log'
    if (($probeLines -join "`n") -notmatch 'E 批验收结束：通过 43，失败 0') { throw 'E 批探针未完整通过' }
    foreach ($line in $probeLines | Where-Object { $_.StartsWith('[BatchE]') }) { $evidence.Add($line) }
    Invoke-BatchProcess 'a_regression' @('--headless', '--path', $projectDir, '--resolution', '1920x1080', '--quit-after', '6000', 'res://Scenes/UI/_BatchAProbe.tscn')
    $regressionLines = Get-Content '.ai_e_a_regression.log'
    if (($regressionLines -join "`n") -notmatch 'A 批验收结束：失败 0') { throw 'A 批回归失败' }
    $evidence.Add("A 批无头回归：$(@($regressionLines | Where-Object { $_.StartsWith('[BatchA] PASS') }).Count) 项通过，失败0（本轮不覆盖A批原截图）")
    foreach ($saveName in $saveHashes.Keys) {
        $savePath = Join-Path $saveDir $saveName
        $afterHash = if (Test-Path -LiteralPath $savePath) { (Get-FileHash -LiteralPath $savePath -Algorithm SHA256).Hash } else { '不存在' }
        if ($afterHash -cne $saveHashes[$saveName]) { throw "玩家存档改变：$saveName" }
        $evidence.Add("存档不变 $saveName=$afterHash")
    }
    foreach ($name in @('e_eye_with_arrow', 'e_status_three', 'e_status_stacks_five', 'e_status_cleanup')) {
        $imagePath = Join-Path $projectDir "docs/screenshots/$name.png"
        if (-not (Test-Path -LiteralPath $imagePath)) { throw "缺少截图：$name" }
        $evidence.Add("截图 $name.png SHA256=$((Get-FileHash -LiteralPath $imagePath -Algorithm SHA256).Hash)")
    }
}
finally {
    if (Test-Path -LiteralPath $flagPath) { Remove-Item -LiteralPath $flagPath }
    $evidence.Add("selftest.flag 不存在=$(-not (Test-Path -LiteralPath $flagPath))")
    [System.IO.File]::WriteAllText((Join-Path $projectDir 'docs/e-batch-evidence.log'), ($evidence -join "`n") + "`n", [System.Text.UTF8Encoding]::new($false))
    Pop-Location
}
