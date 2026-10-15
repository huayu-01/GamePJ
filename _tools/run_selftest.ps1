# P6d 自测跑法（命令行直跑 + 读退出码；不依赖编辑器 / MCP 运行时桥）
# 用法：& 'F:\Godot项目\项目a\_tools\run_selftest.ps1'
$ErrorActionPreference = 'Continue'
$godot = 'F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe'
$flag  = 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\selftest.flag'
$logs  = 'C:\Users\huayu\AppData\Roaming\Godot\app_userdata\ACT Roguelike\logs'

'1' | Set-Content $flag
try {
    $p = Start-Process -FilePath $godot `
        -ArgumentList '--path', 'F:\Godot项目\项目a', 'res://Scenes/Main.tscn' `
        -PassThru
    $deadline = (Get-Date).AddSeconds(180)
    while (-not $p.HasExited -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
        $p.Refresh()
    }
    if (-not $p.HasExited) { "TIMEOUT（>180s，进程仍在跑）"; }
    else { "退出码 = $($p.ExitCode)" }
}
finally {
    Remove-Item $flag -ErrorAction SilentlyContinue
    "selftest.flag 已删除 = $(-not (Test-Path $flag))"
}

$latest = Get-ChildItem $logs -Filter 'run_*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
"最新运行日志 = $($latest.FullName)"
