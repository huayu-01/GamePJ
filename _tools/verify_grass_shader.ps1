param([string]$Round='round-1',[switch]$SkipBuild)
# 同一场景已经更新为丛林版；旧入口转交新版验证，旧草地证据保留作历史记录。
Write-Output '草地样板已更新为丛林版，验证转交 verify_grass_jungle.ps1。'
& "$PSScriptRoot/verify_grass_jungle.ps1" -Round $Round -SkipBuild:$SkipBuild
