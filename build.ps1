# 构建 C# 工程。
# 用途：在 agent / CI 这类缺少 SystemDrive 等环境变量的 shell 里构建。
# 用户自己在 Godot 编辑器里构建（右上角构建按钮）不需要这个脚本。
$env:SystemDrive = 'C:'
${env:ProgramFiles(x86)} = 'C:\Program Files (x86)'
$env:ProgramFiles = 'C:\Program Files'
$env:ProgramData = 'C:\ProgramData'
$env:HOMEDRIVE = 'C:'
$env:HOMEPATH = "\Users\$env:USERNAME"
$env:DOTNET_ROOT = 'C:\Program Files\dotnet'
dotnet build "$PSScriptRoot\ActRoguelike.csproj" -c Debug @args
