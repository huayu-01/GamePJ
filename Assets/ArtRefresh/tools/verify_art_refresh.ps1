# 只管理本脚本的验证进程，每个构建/运行期间独占工作区验证锁。
param([switch]$ProbeOnly, [switch]$SkipImport, [switch]$Before)
$ErrorActionPreference = 'Stop'
$projectDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$enginePath = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$evidence = [System.Collections.Generic.List[string]]::new()
$script:validationStarted = $false
$saveDir = Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
$saveHashes = @{}
$comparisonScenes = [System.Collections.Generic.List[string]]::new()
foreach ($name in @('meta.cfg', 'ability_cards.cfg', 'settings.cfg')) {
    $path = Join-Path $saveDir $name
    $saveHashes[$name] = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { '不存在' }
}

function Open-ValidationLock {
    try {
        $handle = [System.IO.File]::Open((Join-Path $projectDir '.ai_validation.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
        $script:validationStarted = $true
        return $handle
    }
    catch [System.IO.IOException] { throw '验证锁正被其他任务占用；本轮不启动构建或Godot，请稍后重试。' }
}

function Invoke-OwnedProcess([string]$Name, [string[]]$RunArgs) {
    $validationLock = Open-ValidationLock
    $ownedProcess = $null
    try {
        $stdoutPath = Join-Path $projectDir ".ai_art_$Name.log"
        $stderrPath = Join-Path $projectDir ".ai_art_$Name.err"
        $ownedProcess = Start-Process -FilePath $enginePath -ArgumentList $RunArgs -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
        $deadline = (Get-Date).AddSeconds(160)
        while (-not $ownedProcess.HasExited -and (Get-Date) -lt $deadline) {
            Start-Sleep -Milliseconds 300
            $ownedProcess.Refresh()
        }
        if (-not $ownedProcess.HasExited) { throw "$Name 超过160秒" }
        $evidence.Add("$Name 退出码=$($ownedProcess.ExitCode)")
        Write-Output "$Name 退出码=$($ownedProcess.ExitCode)"
        if ($ownedProcess.ExitCode -ne 0) { throw "$Name 失败，请查看 $stderrPath" }
        $errors = @(Get-Content -LiteralPath $stderrPath | Where-Object { $_ -match '^(ERROR:|SCRIPT ERROR:)' -and $_ -notmatch '^ERROR: 2 resources still in use at exit' })
        if ($errors.Count -gt 0) {
            $evidence.Add("[未通过] $Name 新错误：$($errors -join ' / ')")
            throw "$Name 新错误：$($errors -join ' / ')"
        }
    }
    finally {
        if ($null -ne $ownedProcess -and -not $ownedProcess.HasExited) { Stop-Process -Id $ownedProcess.Id }
        $validationLock.Dispose()
    }
}

Push-Location $projectDir
try {
    if (-not $ProbeOnly) {
        $validationLock = Open-ValidationLock
        try {
            & 'F:\Godot项目\项目a\build.ps1' --no-incremental -v minimal | Tee-Object -FilePath '.ai_art_build.log'
            if ($LASTEXITCODE -ne 0) { throw '构建失败' }
            $output = Get-Content '.ai_art_build.log' -Raw
            if ($output -notmatch '0 个警告' -or $output -notmatch '0 个错误') { throw '构建必须0warning/0error' }
            $evidence.Add('build.ps1 --no-incremental -v minimal：0 warning / 0 error')
        }
        finally { $validationLock.Dispose() }
        if (-not $SkipImport) { Invoke-OwnedProcess 'import' @('--headless', '--path', $projectDir, '--import', '--mcp-port=19081') }
    }
    Invoke-OwnedProcess 'probe' @('--path', $projectDir, '--resolution', '1920x1080', '--quit-after', '2400', 'res://Scenes/ArtRefresh/ArtRefreshProbe.tscn')
    $lines = Get-Content '.ai_art_probe.log'
    if (($lines -join "`n") -notmatch '验收结束：通过 \d+，失败 0') { throw '美术探针未完整通过' }
    foreach ($line in $lines | Where-Object { $_.StartsWith('[ArtRefresh]') }) { $evidence.Add($line) }
    Invoke-OwnedProcess 'gameplay' @('--path', $projectDir, '--resolution', '1920x1080', '--quit-after', '1200', 'res://Scenes/ArtRefresh/GameplaySmoke.tscn')
    foreach ($line in Get-Content '.ai_art_gameplay.log' | Where-Object { $_.StartsWith('[ArtRefresh]') }) { $evidence.Add($line) }
    if (-not $ProbeOnly) {
        Invoke-OwnedProcess 'selftest' @('--headless', '--path', $projectDir, '--quit-after', '40000', 'res://Scenes/Main.tscn', '--', '--selftest')
        $lines = Get-Content '.ai_art_selftest.log'
        if (($lines -join "`n") -notmatch '=== 自测结束 ===') { throw '自测缺少结束标记' }
        $baseline = Get-Content 'docs/e-batch-evidence.log'
        foreach ($marker in @('配置表：武器 6', '节点布置：启用', '配置表比对·玩家：', '配置表比对·索引：', 'Pressure 测试（进化', '白板首领实例：', '端到端：按 F 之后', '最小闭环·链路：')) {
            $baselineLine = ($baseline | Where-Object { $_.StartsWith('[基线逐字一致] ') -and $_.Contains($marker) } | Select-Object -First 1) -replace '^\[基线逐字一致\] ', ''
            $after = ($lines | Where-Object { $_.Contains($marker) } | Select-Object -First 1) -replace '^\[log\] \S+ ', ''
            if ([string]::IsNullOrEmpty($baselineLine) -or $baselineLine -cne $after) { throw "硬指标改变：$marker" }
            $evidence.Add("[基线逐字一致] $after")
        }
    }
    # 正式验证全部完成后才加载历史场景；随机文件名和移除UID同时避免复用编辑器旧UID映射。
    if ($Before) {
        $comparisonId = [Guid]::NewGuid().ToString('N')
        foreach ($pair in @(@('TestArena', 'arena'), @('Base', 'base'))) {
            $source = ((& git show "d4270fc:Scenes/Levels/$($pair[0]).tscn") -join "`n") -replace ' uid="[^"]+"', ''
            if ($LASTEXITCODE -ne 0) { throw '无法读取比较基线d4270fc' }
            $comparisonName = ".ai_art_before_$($pair[1])_$comparisonId.tscn"
            $comparisonScenes.Add((Join-Path $projectDir $comparisonName))
            [System.IO.File]::WriteAllText($comparisonScenes[$comparisonScenes.Count - 1], $source + "`n", [System.Text.UTF8Encoding]::new($false))
        }
        $arenaArg = '--before-arena=res://' + [System.IO.Path]::GetFileName($comparisonScenes[0])
        $baseArg = '--before-base=res://' + [System.IO.Path]::GetFileName($comparisonScenes[1])
        Invoke-OwnedProcess 'before' @('--path', $projectDir, '--resolution', '1920x1080', '--quit-after', '2400', 'res://Scenes/ArtRefresh/ArtRefreshProbe.tscn', '--', '--before', $arenaArg, $baseArg)
    }
    foreach ($name in $saveHashes.Keys) {
        $path = Join-Path $saveDir $name
        $afterHash = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { '不存在' }
        if ($afterHash -cne $saveHashes[$name]) { throw "存档改变：$name" }
        $evidence.Add("存档不变 $name=$afterHash")
    }
    foreach ($path in Get-ChildItem -LiteralPath 'docs/screenshots' -Filter 'art_refresh_*.png') {
        $evidence.Add("截图 $($path.Name) SHA256=$((Get-FileHash -LiteralPath $path.FullName -Algorithm SHA256).Hash)")
    }
}
finally {
    foreach ($temporaryScene in $comparisonScenes) {
        if (Test-Path -LiteralPath $temporaryScene) { Remove-Item -LiteralPath $temporaryScene }
    }
    $evidence.Add("selftest.flag 不存在=$(-not (Test-Path -LiteralPath (Join-Path $saveDir 'selftest.flag')))")
    if ($script:validationStarted) {
        [System.IO.File]::WriteAllText((Join-Path $projectDir 'docs/art-refresh-evidence.log'), ($evidence -join "`n") + "`n", [System.Text.UTF8Encoding]::new($false))
    }
    Pop-Location
}
