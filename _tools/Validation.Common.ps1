# 验证共用设施。只结束自己创建的进程，真实存档变化一律保留现场。
function Get-ProjectAiDirectory([string]$ProjectRoot, [ValidateSet('workspaces','files','locks')][string]$Area) {
    $root = [IO.Path]::GetFullPath($ProjectRoot)
    $container = Join-Path $root '.ai'
    if ((Test-Path -LiteralPath $container) -and ((Get-Item -LiteralPath $container -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'AI工作目录不能是联接或符号链接。' }
    $directory = Join-Path $container $Area
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $ignore = Join-Path $container '.gdignore'
    if (-not (Test-Path -LiteralPath $ignore)) { [IO.File]::WriteAllText($ignore,'',[Text.UTF8Encoding]::new($false)) }
    return $directory
}
function Get-ProjectValidationLockPath([string]$ProjectRoot) {
    return Join-Path (Get-ProjectAiDirectory $ProjectRoot 'locks') 'validation.lock'
}
function Open-ProjectValidationLease([string]$ProjectRoot, [int]$WaitSeconds = 90, [string]$LockPath = '') {
    if (-not $LockPath) { $LockPath = Get-ProjectValidationLockPath $ProjectRoot }
    $deadline = (Get-Date).AddSeconds([Math]::Max(1, $WaitSeconds))
    do {
        try { return [IO.File]::Open($LockPath,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None) }
        catch [IO.IOException] { Start-Sleep -Milliseconds 500 }
    } while ((Get-Date) -lt $deadline)
    throw '验证锁等待超时；没有停止其他任务的进程。'
}
function Get-ValidationSaveSnapshot([string]$Directory) {
    $snapshot = @{}
    foreach ($name in @('meta.cfg','ability_cards.cfg','settings.cfg','loadout.cfg','rune.cfg')) {
        $path = Join-Path $Directory $name
        $snapshot[$name] = if (Test-Path -LiteralPath $path) { [Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) } else { $null }
    }
    return $snapshot
}
function Assert-ValidationSaves([string]$Directory, [hashtable]$Snapshot) {
    foreach ($name in $Snapshot.Keys) {
        $path = Join-Path $Directory $name
        $now = if (Test-Path -LiteralPath $path) { [Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) } else { $null }
        if ($now -cne $Snapshot[$name]) { throw "存档意外变化：$name；保留现场，不覆盖用户改动。" }
    }
}
function Invoke-ValidationBuild([string]$ProjectRoot, [string]$Configuration, [string]$LogPath) {
    if ($Configuration -eq 'Debug') { & (Join-Path $ProjectRoot 'build.ps1') --no-incremental -v minimal | Tee-Object -FilePath $LogPath | Out-Host }
    else { dotnet build (Join-Path $ProjectRoot 'ActRoguelike.csproj') --no-incremental -v minimal -c $Configuration | Tee-Object -FilePath $LogPath | Out-Host }
    if ($LASTEXITCODE -ne 0) { throw "$Configuration 编译失败" }
    $output = [IO.File]::ReadAllText($LogPath)
    if ($output -notmatch '0 个警告' -or $output -notmatch '0 个错误') { throw "$Configuration 编译未满足0/0" }
    return $output
}
function Invoke-ValidationEngine([string]$Executable, [string[]]$Arguments, [string]$ProjectRoot, [string]$StdoutPath, [string]$StderrPath, [double]$TimeoutSeconds = 210) {
    $owned = $null
    try {
        $owned = Start-Process -FilePath $Executable -ArgumentList $Arguments -WorkingDirectory $ProjectRoot -WindowStyle Hidden -RedirectStandardOutput $StdoutPath -RedirectStandardError $StderrPath -PassThru
        $timer = [Diagnostics.Stopwatch]::StartNew()
        while (-not $owned.HasExited -and $timer.Elapsed.TotalSeconds -lt $TimeoutSeconds) { Start-Sleep -Milliseconds 250; $owned.Refresh() }
        if (-not $owned.HasExited) { throw "验证超时；只结束本次PID $($owned.Id)。" }
        # 进程退出与重定向输出写入完成存在时间差，等待读取器结束再做严格日志检查。
        $owned.WaitForExit()
        $output = [IO.File]::ReadAllText($StdoutPath); $errorText = [IO.File]::ReadAllText($StderrPath)
        if ($owned.ExitCode -ne 0 -or $errorText.Length -gt 0) { throw "引擎验证未通过：exit=$($owned.ExitCode)；$StdoutPath / $StderrPath" }
        return [pscustomobject]@{ Output = $output; ExitCode = $owned.ExitCode; Seconds = [Math]::Round($timer.Elapsed.TotalSeconds,2) }
    }
    finally { if ($owned -and -not $owned.HasExited) { Stop-Process -Id $owned.Id } }
}
function Get-ContentInventoryCounts([string]$ProjectRoot) {
    $source = [IO.File]::ReadAllText((Join-Path $ProjectRoot 'Config/Validation/content_inventory.tres'))
    $block = [regex]::Match($source,'(?s)ExpectedCounts = \{(.*?)\}').Groups[1].Value
    if (-not $block) { throw '没有读取到显式内容数量基线。' }
    $counts = @{}
    foreach ($match in [regex]::Matches($block,'"([^"]+)"\s*:\s*(\d+)')) { $counts[$match.Groups[1].Value] = [int]$match.Groups[2].Value }
    return $counts
}
function Convert-VerifiedInventoryLine([string]$Line, [hashtable]$Expected, [switch]$Verify) {
    $mapping = @{ '武器'='Weapons'; '角色'='Characters'; '敌人'='Enemies'; '首领'='Bosses'; '召唤物'='Summons'; '技能'='Skills'; '连招参数'='SkillSeries'; 'Pressure'='PressureMods'; '效果'='Effects'; '节点'='Nodes'; '局外成长'='MetaUpgrades'; '局外'='MetaUpgrades'; 'AI 档案'='AiProfiles'; '能力卡'='AbilityCards'; '符文'='Runes'; '升级池'='UpgradePools' }
    $result = $Line.Split('（期望')[0]
    foreach ($label in $mapping.Keys) {
        $pattern = '(?<![\p{L}])'+[regex]::Escape($label)+' (?<number>\d+)'
        $matches = [regex]::Matches($result,$pattern)
        if ($Verify) {
            foreach ($match in $matches) {
                $table = $mapping[$label]
                if (-not $Expected.ContainsKey($table) -or [int]$match.Groups['number'].Value -ne $Expected[$table]) { throw "内容数量不符：$label，期望$($Expected[$table])，日志$($match.Groups['number'].Value)" }
            }
        }
        $result = [regex]::Replace($result,$pattern,($label+' <内容基线>'))
    }
    return $result
}
function Assert-MainValidation([string]$ProjectRoot, [string]$Output) {
    foreach ($line in ($Output -split "`n" | Where-Object { $_.Contains('🔴') })) {
        if ($line -notmatch '🔴 (白板首领闸门·正向：|白板首领实例：|Boss 阵亡 →|互动降临节点：|最小闭环·链路：)') { throw "Main新增红标：$line" }
    }
    $baseline = Get-Content -LiteralPath (Join-Path $ProjectRoot 'Logs/evidence/character-design-j11-evidence.log')
    $counts = Get-ContentInventoryCounts $ProjectRoot
    $results = [Collections.Generic.List[string]]::new()
    foreach ($marker in @('配置表：武器 ','节点布置：启用','配置表比对·玩家：','配置表比对·索引：','Pressure 测试（进化','白板首领实例：','端到端：按 F 之后','最小闭环·链路：')) {
        $before = ($baseline | Where-Object { $_.StartsWith('[log] ') -and $_.Contains($marker) } | Select-Object -First 1) -replace '^\[log\] \S+ ', ''
        $after = ($Output -split "`n" | Where-Object { $_.StartsWith('[log] ') -and $_.Contains($marker) } | Select-Object -First 1) -replace '^\[log\] \S+ ', ''
        if ($marker -in @('配置表：武器 ','配置表比对·索引：')) {
            # 先核对实际数量，再仅移除规模字段以比较其余文本；错误数量不能被归一化吞掉。
            $before = Convert-VerifiedInventoryLine $before $counts
            $after = Convert-VerifiedInventoryLine $after $counts -Verify
        }
        if ($marker -eq '配置表比对·玩家：') {
            if ($Output -notmatch '✅ 配装生命核对：') { throw 'Main缺少真实配装生命公式校验' }
            $before = $before -replace '生命 \d+','生命 <配装>' -replace '元进度加成 \d+','元进度加成 <存档>'
            $after = $after -replace '生命 \d+','生命 <配装>' -replace '元进度加成 \d+','元进度加成 <存档>'
        }
        if ($marker -eq 'Pressure 测试（进化') {
            # 032有意新增一个护甲候选：11个奖励候选加首领门=12；仍只启用原定10个奖励节点。
            if ($after -notmatch 'all=12（') { throw 'Main交互候选规模错误，期望12' }
            $before = $before -replace 'all=11（','all=<护甲节点已登记>（'
            $after = $after -replace 'all=12（','all=<护甲节点已登记>（'
        }
        if ($marker -eq '白板首领实例：') {
            if ($Output -notmatch '✅ 白板首领生命公式核对：') { throw 'Main缺少白板首领实际生命公式核验' }
            $before = $before -replace '生命 .*?护甲','生命 <公式核验> 护甲'; $after = $after -replace '生命 .*?护甲','生命 <公式核验> 护甲'
        }
        if ([string]::IsNullOrWhiteSpace($before) -or $before.TrimEnd() -cne $after.TrimEnd()) { throw "Main硬指标变化：$marker" }
        $results.Add("[Main指标一致] $($after.TrimEnd())")
    }
    return $results.ToArray()
}
function New-ValidationMirror([string]$ProjectRoot, [string]$Name, [switch]$ShareAssembly) {
    $tag = Get-Date -Format 'yyyyMMdd_HHmmss_fff'
    $mirror = Join-Path (Get-ProjectAiDirectory $ProjectRoot 'workspaces') "validation_${Name}_$tag"
    $profileName = "CodexValidation/${Name}_$tag"; $profile = Join-Path $env:APPDATA $profileName
    New-Item -ItemType Directory -Path $mirror,$profile,(Join-Path $mirror '.godot'),(Join-Path $mirror '_tools') -Force | Out-Null
    foreach ($name in @('Scripts','Scenes','Config','Assets','addons','docs','Logs')) { New-Item -ItemType Junction -Path (Join-Path $mirror $name) -Target (Join-Path $ProjectRoot $name) | Out-Null }
    foreach ($name in @('project.godot','ActRoguelike.csproj','build.ps1','default_bus_layout.tres','icon.svg','icon.svg.import','.editorconfig')) {
        $source = Join-Path $ProjectRoot $name
        if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination (Join-Path $mirror $name) }
    }
    New-Item -ItemType Junction -Path (Join-Path $mirror '.godot/imported') -Target (Join-Path $ProjectRoot '.godot/imported') | Out-Null
    if ($ShareAssembly) { New-Item -ItemType Junction -Path (Join-Path $mirror '.godot/mono') -Target (Join-Path $ProjectRoot '.godot/mono') | Out-Null }
    foreach ($name in @('uid_cache.bin','global_script_class_cache.cfg')) {
        $source = Join-Path $ProjectRoot ".godot/$name"; if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination (Join-Path $mirror '.godot') }
    }
    [IO.File]::WriteAllText((Join-Path $mirror 'override.cfg'),"[application]`nconfig/use_custom_user_dir=true`nconfig/custom_user_dir_name=`"$profileName`"`n",[Text.UTF8Encoding]::new($false))
    $player = Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike'
    foreach ($name in @('meta.cfg','ability_cards.cfg','settings.cfg','loadout.cfg','rune.cfg')) { $file = Join-Path $player $name; if (Test-Path -LiteralPath $file) { Copy-Item -LiteralPath $file -Destination (Join-Path $profile $name) } }
    return [pscustomobject]@{ Project = $mirror; Profile = $profile; Player = $player; Original = (Get-ValidationSaveSnapshot $player); Lock = (Get-ProjectValidationLockPath $ProjectRoot) }
}
