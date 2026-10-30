param()

$ErrorActionPreference = 'Stop'
$blenderExecutable = 'E:\steam\steamapps\common\Blender\blender.exe'
$bootstrapScript = Join-Path $PSScriptRoot 'start_blender.py'
$logPrefix = Join-Path $PSScriptRoot ('blender-launch-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))

try {
    if (!(Test-Path -LiteralPath $blenderExecutable) -or !(Test-Path -LiteralPath $bootstrapScript)) {
        throw 'Blender executable or bootstrap script is missing.'
    }

    # 已有本机连接时复用用户窗口，避免反复启动多个实例。
    $listeners = @(Get-NetTCPConnection -LocalPort 9876 -State Listen -ErrorAction SilentlyContinue)
    foreach ($listener in $listeners) {
        $existingBlender = Get-Process -Id $listener.OwningProcess -ErrorAction SilentlyContinue
        if ($existingBlender -and $existingBlender.Path -eq $blenderExecutable) {
            Write-Host ('Blender is already running with MCP. PID: ' + $existingBlender.Id)
            return
        }
    }
    if ($listeners.Count -gt 0) {
        throw 'MCP port 9876 is occupied by another application.'
    }

    # 这是用户需要查看和操作的交互窗口；独立进程使启动器可以正常退出。
    $launchArguments = @('--python', ('"' + $bootstrapScript + '"'))
    $blenderProcess = Start-Process -FilePath $blenderExecutable -ArgumentList $launchArguments `
        -WorkingDirectory (Join-Path $PSScriptRoot '..\Blender') -WindowStyle Normal `
        -RedirectStandardOutput ($logPrefix + '.log') -RedirectStandardError ($logPrefix + '.stderr.log') -PassThru

    # 等待真实端口就绪；启动失败时保留错误，避免窗口无声关闭。
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        Start-Sleep -Milliseconds 500
        $blenderProcess.Refresh()
        if ($blenderProcess.HasExited) {
            throw ('Blender exited during startup. Exit code: ' + $blenderProcess.ExitCode)
        }
        $ready = Get-NetTCPConnection -LocalPort 9876 -State Listen -ErrorAction SilentlyContinue |
            Where-Object { $_.OwningProcess -eq $blenderProcess.Id }
        if ($ready) {
            Write-Host ('Blender is ready. MCP: 127.0.0.1:9876. PID: ' + $blenderProcess.Id)
            Write-Host ('Startup log: ' + $logPrefix + '.log')
            return
        }
    }
    throw ('Blender started, but MCP is not ready yet. Check: ' + $logPrefix + '.log')
}
catch {
    Write-Host ('Startup failed: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host ('Logs: ' + $logPrefix + '.log / .stderr.log')
    exit 1
}
