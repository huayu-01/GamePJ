# 整局回退写盘只发生在独立工程和独立用户目录。
param([int]$LockWaitSeconds = 900)
$ErrorActionPreference = 'Stop'
$sourceDir = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Validation.Common.ps1')
$mirror = New-ValidationMirror $sourceDir 'adventure'
foreach ($name in @('verify_gameplay_fixes.ps1','Validation.Common.ps1','LogPaths.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $mirror.Project "_tools/$name")
}
Write-Output "独立验证工程：$($mirror.Project)；用户目录：$($mirror.Profile)"
try {
    & (Join-Path $mirror.Project '_tools/verify_gameplay_fixes.ps1') -Only adventure,adventure_render -LockWaitSeconds $LockWaitSeconds -ProfileDirectory $mirror.Profile -ValidationLockPath $mirror.Lock
}
finally {
    Assert-ValidationSaves $mirror.Player $mirror.Original
    Write-Output '原玩家五份存档即时指纹一致。'
}
