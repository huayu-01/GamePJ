@echo off
setlocal
powershell.exe -NoLogo -NoProfile -File "%~dp0Start-Blender.ps1"
if errorlevel 1 (
    echo.
    echo Blender startup failed. See the message and launch logs above.
    pause
    exit /b 1
)
