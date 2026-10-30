# 历史专项保留其场景、机制断言和截图核对，共用构建、独占、进程与存档保护。
. (Join-Path $PSScriptRoot 'Validation.Common.ps1')
function Initialize-LegacyValidation([string]$Prefix) {
    $script:legacyPrefix = $Prefix
    $script:legacyMirror = New-ValidationMirror $projectDir $Prefix -ShareAssembly
    $script:profileDir = $legacyMirror.Profile
}
function Open-ValidationLease { Open-ProjectValidationLease $projectDir 900 }
function Invoke-CheckedBuild([string]$Configuration) {
    $lease = Open-ValidationLease
    try {
        $log = Join-Path $projectDir "Logs/validation/.ai_$($legacyPrefix)_build_$Configuration.log"
        $null = Invoke-ValidationBuild $projectDir $Configuration $log
        $evidence.Add("$Configuration 编译：0 warning / 0 error")
    }
    finally { $lease.Dispose(); Assert-ValidationSaves $legacyMirror.Player $legacyMirror.Original }
}
function Invoke-OwnedProcess([string]$RunName, [string[]]$RunArgs, [bool]$GmWrites = $false) {
    if (($RunArgs -join ' ') -match '_BatchCProbe.tscn' -and $RunArgs -notcontains '--loadout-test') { throw 'C探针缺少独立存档参数，拒绝启动。' }
    $lease = Open-ValidationLease
    try {
        $arguments = @($RunArgs | ForEach-Object { if ($_ -ceq $projectDir) { $legacyMirror.Project } else { $_ } })
        $stdout = Join-Path $projectDir "Logs/validation/.ai_$($legacyPrefix)_$RunName.log"
        $stderr = Join-Path $projectDir "Logs/validation/.ai_$($legacyPrefix)_$RunName.err"
        $arguments = @('--log-file', ($stdout -replace '\.log$', '.engine.log')) + $arguments
        $result = Invoke-ValidationEngine $godotExe $arguments $projectDir $stdout $stderr
        $message = "$RunName 退出码=$($result.ExitCode) stderr字节=0"
        Write-Output $message; $evidence.Add($message)
        if ($RunArgs -contains '--selftest') { $evidence.AddRange([string[]](Assert-MainValidation $projectDir $result.Output)) }
    }
    finally {
        $flag = Join-Path $profileDir 'selftest.flag'; if (Test-Path -LiteralPath $flag) { Remove-Item -LiteralPath $flag }
        $lease.Dispose(); Assert-ValidationSaves $legacyMirror.Player $legacyMirror.Original
    }
}
function Invoke-BatchProcess([string]$Name, [string[]]$RunArgs) { Invoke-OwnedProcess $Name $RunArgs }
