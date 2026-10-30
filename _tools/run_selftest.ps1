# Main自测沿用统一的独占锁、存档保护和集中日志入口。
param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'verify_gameplay_fixes.ps1') -Only main -SkipBuild:$SkipBuild
