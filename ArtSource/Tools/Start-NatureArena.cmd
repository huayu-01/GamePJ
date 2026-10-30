@echo off
chcp 65001 >nul
dotnet build "%~dp0..\..\ActRoguelike.csproj" --nologo -v quiet
if errorlevel 1 exit /b 1
start "" "F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path "%~dp0..\.." res://Scenes/Debug/NatureCombatLab.tscn
