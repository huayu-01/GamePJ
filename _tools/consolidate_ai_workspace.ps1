# 只迁移项目根目录.ai*项；不遍历联接目标、不删除历史资料，移动前逐项验证绝对路径。
param([switch]$Apply)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\','/')
. (Join-Path $PSScriptRoot 'Validation.Common.ps1')
$container=Join-Path $projectRoot '.ai'
$lease=Open-ProjectValidationLease $projectRoot 30
$saves=Get-ValidationSaveSnapshot (Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike')
$manifest=[ordered]@{Date=(Get-Date).ToString('o');Project=$projectRoot;Destination=$container;Applied=$false;Passed=$false;Entries=@();Links=@();SavesUnchanged=$false;Failure=''}
$record=Join-Path $container 'migration-20261027.json'
function Inside([string]$Path,[string]$Parent) { return $Path.StartsWith($Parent.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) }
function Persist { $manifest|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $record -Encoding utf8 }
try {
    $items=@(Get-ChildItem -LiteralPath $projectRoot -Force | Where-Object {$_.Name -like '.ai*' -and $_.Name -ne '.ai'})
    foreach($item in $items){
        $source=[IO.Path]::GetFullPath($item.FullName)
        if((Split-Path -Parent $source) -ine $projectRoot -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw "拒绝异常根目录项目：$source"}
        $area=if($item.PSIsContainer){'workspaces'}else{'files'}
        $target=[IO.Path]::GetFullPath((Join-Path (Get-ProjectAiDirectory $projectRoot $area) $item.Name))
        if(-not (Inside $target $container) -or $source -ieq $projectRoot -or (Test-Path -LiteralPath $target)){throw "目标越界或已经存在：$target"}
        $entry=[pscustomobject]@{Source=$source;Target=$target;Directory=[bool]$item.PSIsContainer;SHA256=$null;Moved=$false}
        if(-not $item.PSIsContainer){
            $probe=[IO.File]::Open($source,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None);$probe.Dispose()
            $entry.SHA256=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        }
        $manifest.Entries+=@($entry)
    }
    # 不触碰其他进程；有编辑器或验证正在使用这些目录时直接停止。
    $godot=@(Get-CimInstance Win32_Process | Where-Object {$_.Name -like '*Godot*'})
    foreach($process in $godot){foreach($entry in $manifest.Entries){if($process.CommandLine -and $process.CommandLine.Contains($entry.Source)){throw "目录仍由PID $($process.ProcessId)使用：$($entry.Source)"}}}
    foreach($entry in $manifest.Entries | Where-Object Directory){
        $pending=[Collections.Generic.Stack[string]]::new();$pending.Push($entry.Source)
        while($pending.Count){
            foreach($child in Get-ChildItem -LiteralPath $pending.Pop() -Force){
                if($child.Attributes -band [IO.FileAttributes]::ReparsePoint){
                    $targets=@($child.Target)
                    if($targets.Count -ne 1 -or -not $targets[0]){throw "无法识别联接：$($child.FullName)"}
                    $target=[string]$targets[0]
                    if(-not [IO.Path]::IsPathRooted($target)){$target=Join-Path $child.DirectoryName $target}
                    $target=[IO.Path]::GetFullPath($target);$updated=$target
                    foreach($mapping in $manifest.Entries){if($target -ieq $mapping.Source -or (Inside $target $mapping.Source)){$updated=$mapping.Target+$target.Substring($mapping.Source.Length);break}}
                    $movedLink=$entry.Target+$child.FullName.Substring($entry.Source.Length)
                    $manifest.Links+=@([pscustomobject]@{Source=$child.FullName;Path=$movedLink;Type=$child.LinkType;OldTarget=$target;Target=$updated;Repointed=$false})
                }elseif($child.PSIsContainer){$pending.Push($child.FullName)}
            }
        }
    }
    Write-Output "预检通过：$($manifest.Entries.Count)项，$($manifest.Links.Count)个联接；正式编辑器不在迁移目标内。"
    Persist
    if(-not $Apply){Write-Output "预览清单：$record";return}
    $manifest.Applied=$true
    foreach($entry in $manifest.Entries){
        # 同卷Move-Item仅移动目录入口，绝不递归复制/删除联接指向的正式项目。
        Move-Item -LiteralPath $entry.Source -Destination $entry.Target
        $entry.Moved=$true
        if(-not $entry.Directory -and (Get-FileHash -LiteralPath $entry.Target -Algorithm SHA256).Hash -cne $entry.SHA256){throw "文件校验不同：$($entry.Target)"}
    }
    foreach($link in $manifest.Links){
        $current=Get-Item -LiteralPath $link.Path -Force
        if(-not ($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -or -not (Inside $link.Path $container)){throw "联接验证失败：$($link.Path)"}
        if($link.Target -ine $link.OldTarget){
            if($link.Type -eq 'Junction'){[IO.Directory]::Delete($link.Path);New-Item -ItemType Junction -Path $link.Path -Target $link.Target|Out-Null}
            elseif($link.Type -eq 'SymbolicLink'){Remove-Item -LiteralPath $link.Path -Force;New-Item -ItemType SymbolicLink -Path $link.Path -Target $link.Target|Out-Null}
            else{throw "不支持迁移的联接类型：$($link.Type)"}
            $link.Repointed=$true
        }
        $current=Get-Item -LiteralPath $link.Path -Force
        if([IO.Path]::GetFullPath([string]$current.Target) -ine $link.Target){throw "联接目标不一致：$($link.Path)"}
    }
    if(@(Get-ChildItem -LiteralPath $projectRoot -Force | Where-Object {$_.Name -like '.ai*' -and $_.Name -ne '.ai'}).Count){throw '项目根目录仍有未迁移的.ai项目'}
    $manifest.Passed=$true
    Write-Output "PASS 已集中$($manifest.Entries.Count)项，文件哈希与联接目标核对通过。"
}catch{$manifest.Failure=$_.ToString();throw}
finally{
    try{Assert-ValidationSaves (Join-Path $env:APPDATA 'Godot/app_userdata/ACT Roguelike') $saves;$manifest.SavesUnchanged=$true}
    finally{Persist;$lease.Dispose()}
}
