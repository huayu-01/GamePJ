# 独占验证锁，只管理本脚本启动的PID；探针不写正式存档。
param([switch]$SkipBuild, [switch]$SelfTest, [switch]$ReleaseBuild, [switch]$BaseRegression, [switch]$NativeStudy)
$ErrorActionPreference = 'Stop'
$projectDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$engine = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
function Open-CharacterLease {
    $deadline = (Get-Date).AddSeconds(45)
    do {
        try { return [IO.File]::Open((Join-Path $projectDir '.ai_validation.lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
        catch [IO.IOException] { Start-Sleep -Milliseconds 500 }
    } while ((Get-Date) -lt $deadline)
    throw '验证锁仍被占用；没有启动新的构建或Godot。'
}
function Invoke-CharacterRun([string]$Name, [string[]]$RunArgs) {
    $lease = Open-CharacterLease
    $proc = $null
    $saveRoot = Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
    $saveHashes = @{}
    $testPath = Join-Path $saveRoot 'batch_c_loadout_test.cfg'
    $testBackup = if (Test-Path -LiteralPath $testPath) { [IO.File]::ReadAllBytes($testPath) } else { $null }
    try {
        foreach ($save in @('meta.cfg', 'ability_cards.cfg', 'settings.cfg', 'loadout.cfg')) {
            $path = Join-Path $saveRoot $save
            $saveHashes[$save] = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { 'absent' }
        }
        $proc = Start-Process -FilePath $engine -ArgumentList $RunArgs -WindowStyle Hidden -RedirectStandardOutput (Join-Path $projectDir ".ai_character_$Name.log") -RedirectStandardError (Join-Path $projectDir ".ai_character_$Name.err") -PassThru
        $deadline = (Get-Date).AddSeconds(100)
        while (-not $proc.HasExited -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300; $proc.Refresh() }
        if (-not $proc.HasExited) { throw "$Name 超时" }
        if ($proc.ExitCode -ne 0) { throw "$Name 退出码=$($proc.ExitCode)" }
        $errors = @(Get-Content (Join-Path $projectDir ".ai_character_$Name.err") | Where-Object { $_ -match '^(ERROR:|SCRIPT ERROR:)' -and $_ -notmatch '^ERROR: 2 resources still in use at exit' })
        if ($errors.Count -gt 0) { throw "$Name 存在运行错误：$($errors -join ' / ')" }
        Write-Output "$Name 退出0"
        foreach ($save in $saveHashes.Keys) {
            $path = Join-Path $saveRoot $save
            $after = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { 'absent' }
            if ($after -cne $saveHashes[$save]) { throw "正式存档被改变：$save" }
        }
        Write-Output "$Name 正式四份存档哈希不变"
    }
    finally {
        if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id }
        if ($Name -eq 'base_regression') {
            if ($null -eq $testBackup) { if (Test-Path -LiteralPath $testPath) { Remove-Item -LiteralPath $testPath } }
            else { [IO.File]::WriteAllBytes($testPath, $testBackup) }
        }
        $lease.Dispose()
    }
}
Push-Location $projectDir
try {
    if (-not $SkipBuild) {
        $lease = Open-CharacterLease
        try {
            & ./build.ps1 --no-incremental -v minimal *> .ai_character_build_Debug.log
            if ($LASTEXITCODE -ne 0) { throw 'Debug编译失败' }
            $output = Get-Content .ai_character_build_Debug.log -Raw
            if ($output -notmatch '0 个警告' -or $output -notmatch '0 个错误') { throw 'Debug编译未达到0/0' }
            Write-Output 'Debug编译0/0'
        }
        finally { $lease.Dispose() }
    }
    if ($ReleaseBuild) {
        $lease = Open-CharacterLease
        try {
            dotnet build ./ActRoguelike.csproj --no-incremental -v minimal -c ExportRelease *> .ai_character_build_ExportRelease.log
            if ($LASTEXITCODE -ne 0) { throw 'ExportRelease编译失败' }
            $output = Get-Content .ai_character_build_ExportRelease.log -Raw
            if ($output -notmatch '0 个警告' -or $output -notmatch '0 个错误') { throw 'ExportRelease编译未达到0/0' }
            Write-Output 'ExportRelease编译0/0'
        }
        finally { $lease.Dispose() }
    }
    if ($SelfTest) {
        Invoke-CharacterRun 'selftest' @('--headless', '--path', $projectDir, '--quit-after', '40000', 'res://Scenes/Main.tscn', '--', '--selftest')
        $output = Get-Content .ai_character_selftest.log
        if (($output -join "`n") -notmatch '=== 自测结束 ===') { throw '完整自测没有结束标记' }
        $baseline = Get-Content docs/e-batch-evidence.log
        foreach ($marker in @('配置表：武器 6', '节点布置：启用', '配置表比对·玩家：', '配置表比对·索引：', 'Pressure 测试（进化', '白板首领实例：', '端到端：按 F 之后', '最小闭环·链路：')) {
            $before = ($baseline | Where-Object { $_.StartsWith('[基线逐字一致] ') -and $_.Contains($marker) } | Select-Object -First 1) -replace '^\[基线逐字一致\] ', ''
            $after = ($output | Where-Object { $_.Contains($marker) } | Select-Object -First 1) -replace '^\[log\] \S+ ', ''
            if ([string]::IsNullOrEmpty($before) -or $before -cne $after) { throw "硬指标改变：$marker" }
            Write-Output "[基线逐字一致] $after"
        }
    }
    elseif ($BaseRegression) {
        Invoke-CharacterRun 'base_regression' @('--headless', '--path', $projectDir, '--quit-after', '10000', 'res://Scenes/Debug/_BatchCProbe.tscn', '--', '--loadout-test')
        $output = Get-Content .ai_character_base_regression.log
        if (($output -join "`n") -notmatch 'C批验收结束：通过 \d+，失败 0') { throw '基地回归未通过' }
        $output | Where-Object { $_.Contains('C批验收结束') }
    }
    elseif ($NativeStudy) {
        Invoke-CharacterRun 'native' @('--path', $projectDir, '--quit-after', '3600', '--script', 'res://Assets/Characters/tools/inspect_characters.gd', '--mcp-port=19084', '--', '--native-attacks')
    }
    else {
        Invoke-CharacterRun 'probe' @('--path', $projectDir, '--resolution', '1920x1080', '--quit-after', '20000', 'res://Scenes/Debug/_CharacterReplacementProbe.tscn')
        $output = Get-Content .ai_character_probe.log -Raw
        if ($output -notmatch '完成：通过 \d+，失败 0') { throw '角色探针未完整通过' }
        Get-Content .ai_character_probe.log | Where-Object { $_.Contains('[CharacterReplacement]') }
    }
}
finally { Pop-Location }
