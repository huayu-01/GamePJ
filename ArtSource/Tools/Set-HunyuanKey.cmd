@echo off
setlocal
powershell.exe -NoLogo -NoProfile -STA -File "%~dp0Set-HunyuanKey.ps1"
if errorlevel 1 (
    echo Hunyuan key setup could not start.
    pause
    exit /b 1
)
