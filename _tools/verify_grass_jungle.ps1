param([string]$Round='round-1',[switch]$SkipBuild,[switch]$RecordOnly,[switch]$CheckPlayable)
# 同一场景已更新为柔和十字草片版；历史丛林数据保留在原目录。
& "$PSScriptRoot/verify_grass_soft.ps1" -Round $Round -SkipBuild:$SkipBuild -RecordOnly:$RecordOnly -CheckPlayable:$CheckPlayable
