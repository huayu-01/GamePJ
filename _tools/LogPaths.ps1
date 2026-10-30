# 开发工具统一日志目录；不修改当前工作目录，也不接触玩家存档。
function Initialize-ProjectLogPaths {
    param([Parameter(Mandatory)][string]$ProjectRoot)
    foreach ($name in @('runtime','validation','evidence','maintenance')) {
        [IO.Directory]::CreateDirectory((Join-Path $ProjectRoot "Logs/$name")) | Out-Null
    }
}
