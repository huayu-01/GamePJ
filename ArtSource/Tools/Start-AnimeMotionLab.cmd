@echo off
cd /d "%~dp0\..\.."
start "Anime Motion Lab" "F:\Godot项目\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path "%CD%" --resolution 1280x720 res://Scenes/Debug/AnimeMotionLab.tscn
