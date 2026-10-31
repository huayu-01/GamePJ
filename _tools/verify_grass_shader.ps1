param([string]$Round='round-1',[switch]$SkipBuild)
# 同一场景已经更新为柔和草地版；初版证据保留作历史记录。
& "$PSScriptRoot/verify_grass_soft.ps1" -Round $Round -SkipBuild:$SkipBuild
