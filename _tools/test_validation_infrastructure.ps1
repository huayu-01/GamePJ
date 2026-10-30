# 验证真实拒绝路径，防止共用设施把错误数量、stderr或存档变化吞掉。
param([string]$MainLog = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Validation.Common.ps1')
$tag = Get-Date -Format 'yyyyMMdd_HHmmss'
$scratch = Join-Path $projectRoot "Logs/validation/infrastructure_$tag"
New-Item -ItemType Directory -Path $scratch -Force | Out-Null
$results = [Collections.Generic.List[object]]::new()
function Check([bool]$Condition, [string]$Name) { $results.Add([pscustomobject]@{ Name=$Name; Passed=$Condition }); if (-not $Condition) { throw "共用设施测试失败：$Name" }; Write-Output "PASS $Name" }
function Rejects([scriptblock]$Action) { try { $null = & $Action; return $false } catch { return $true } }
$counts = Get-ContentInventoryCounts $projectRoot
$centralLock = Get-ProjectValidationLockPath $projectRoot
Check ($centralLock -eq (Join-Path $projectRoot '.ai/locks/validation.lock')) '验证锁集中到.ai，不再在根目录重建散落文件'
$centralLease = Open-ProjectValidationLease $projectRoot 1
try { Check ($centralLease.Name -eq $centralLock) '默认互斥锁实际使用统一目录' } finally { $centralLease.Dispose() }
$mirrorProbe = New-ValidationMirror $projectRoot 'infrastructure' -ShareAssembly
Check ($mirrorProbe.Project.StartsWith((Join-Path $projectRoot '.ai/workspaces/') ,[StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath (Join-Path $mirrorProbe.Project 'Scenes/Main.tscn'))) '新验证镜像位于.ai且正式场景联接有效'
Check ($mirrorProbe.Lock -eq $centralLock -and (Test-Path -LiteralPath (Join-Path $projectRoot '.ai/.gdignore'))) '镜像返回统一锁且Godot不扫描历史镜像'
Check ($counts.ContainsKey('Weapons') -and $counts.ContainsKey('Enemies') -and $counts.ContainsKey('Effects')) '读取显式基线而非当前运行数量'
$validLine = "配置表：武器 $($counts.Weapons) / 敌人 $($counts.Enemies) / 效果 $($counts.Effects)（ConfigIndex.tres 已加载）"
Check ((Convert-VerifiedInventoryLine $validLine $counts -Verify).Contains('<内容基线>')) '合法规模字段经过核对才归一化'
Check (Rejects { Convert-VerifiedInventoryLine ($validLine.Replace("敌人 $($counts.Enemies)","敌人 $($counts.Enemies - 1)")) $counts -Verify }) '错误敌人数在归一化前拒绝'
Check (Rejects { Convert-VerifiedInventoryLine ($validLine.Replace("效果 $($counts.Effects)","效果 $($counts.Effects - 1)")) $counts -Verify }) '错误效果数不能被历史兼容替换吞掉'
$save = Join-Path $scratch 'settings.cfg'; [IO.File]::WriteAllText($save,'before')
$snapshot = Get-ValidationSaveSnapshot $scratch
Assert-ValidationSaves $scratch $snapshot; Check $true '五份存档含不存在状态均受保护'
[IO.File]::WriteAllText($save,'changed')
Check ((Rejects { Assert-ValidationSaves $scratch $snapshot }) -and [IO.File]::ReadAllText($save) -eq 'changed') '检测到变化后保留现场，不自动恢复'
$lock = Join-Path $scratch 'test.lock'; $lease = Open-ProjectValidationLease $projectRoot 1 $lock
try { Check (Rejects { Open-ProjectValidationLease $projectRoot 1 $lock }) '互斥锁拒绝第二个验证者' } finally { $lease.Dispose() }
$shell = Join-Path $PSHOME 'pwsh.exe'
function Engine([string]$Source, [double]$Timeout = 5) {
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($Source))
    Invoke-ValidationEngine $shell @('-NoLogo','-NoProfile','-NonInteractive','-EncodedCommand',$encoded) $projectRoot (Join-Path $scratch 'process.log') (Join-Path $scratch 'process.err') $Timeout
}
Check ((Engine '[Console]::WriteLine("ok")').Output.Trim() -eq 'ok') '正常进程返回真实输出与退出状态'
Check (Rejects { Engine '[Console]::Error.WriteLine("warning"); exit 0' }) '即使退出码为零也拒绝stderr'
Check (Rejects { Engine 'exit 7' }) '拒绝非零退出状态'
Check (Rejects { Engine 'Start-Sleep -Seconds 8' .25 }) '超时只终止本次创建的进程'
$beforeBuild = (Get-Item -LiteralPath (Join-Path $projectRoot '.godot/mono/temp/bin/Debug/ActRoguelike.dll')).LastWriteTimeUtc
Check ((Rejects { & (Join-Path $PSScriptRoot 'verify_gameplay_fixes.ps1') -Only 'invalid_selector' -SkipBuild }) -and (Rejects { & (Join-Path $PSScriptRoot 'verify_gameplay_fixes.ps1') -Only '([^' -SkipBuild })) '无效专项和正则片段必须显式失败'
Check ((Get-Item -LiteralPath (Join-Path $projectRoot '.godot/mono/temp/bin/Debug/ActRoguelike.dll')).LastWriteTimeUtc -eq $beforeBuild) '选择器失败发生在构建之前'
if (-not $MainLog) { $MainLog = (Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Logs/validation') -Filter '*.main.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName }
if (-not $MainLog) { throw '需要先运行Main专项，或显式传入MainLog。' }
$main = [IO.File]::ReadAllText($MainLog)
Check (@(Assert-MainValidation $projectRoot $main).Count -eq 8) '原八项硬指标全部比较'
Check (Rejects { Assert-MainValidation $projectRoot ($main.Replace('all=12（','all=11（')) }) '新增护甲候选必须真实登记，旧候选数量不能被归一化吞掉'
Check (Rejects { Assert-MainValidation $projectRoot ($main.Replace("敌人 $($counts.Enemies) /","敌人 $($counts.Enemies - 1) /")) }) 'Main实际规模错误仍失败'
Check (Rejects { Assert-MainValidation $projectRoot ($main + "`n[log] 00:00:00 [SelfTest] 🔴 新增错误") }) 'Main拒绝新增红标'
$report = Join-Path $projectRoot "Logs/evidence/validation-infrastructure-$tag.json"
[IO.File]::WriteAllText($report,($results | ConvertTo-Json -Depth 3),[Text.UTF8Encoding]::new($false))
Write-Output "验证设施完成：$($results.Count)项；$report"
