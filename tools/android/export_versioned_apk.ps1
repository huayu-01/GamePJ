param(
    [string]$GodotPath = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$presetPath = Join-Path $projectRoot "export_presets.cfg"
$projectPath = Join-Path $projectRoot "project.godot"
$versionMatch = Select-String -Path $presetPath -Pattern '^version/name="([^"]+)"$' | Select-Object -First 1
if (-not $versionMatch) {
    throw "Android version/name was not found in export_presets.cfg."
}

$versionName = $versionMatch.Matches[0].Groups[1].Value
$outputDirectory = Join-Path $projectRoot "build\android"
$outputPath = Join-Path $outputDirectory "GamePJ-$versionName.apk"
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

if ([string]::IsNullOrWhiteSpace($GodotPath)) {
    $godotDirectory = Join-Path (Split-Path $projectRoot -Parent) "Godot_v4.6.3-stable_mono_win64"
    $GodotPath = Join-Path $godotDirectory "Godot_v4.6.3-stable_mono_win64_console.exe"
}

if (-not (Test-Path $GodotPath)) {
    throw "Godot console was not found: $GodotPath"
}

$projectContent = [System.IO.File]::ReadAllText($projectPath)
$pluginSetting = 'enabled=PackedStringArray("res://addons/godot_mcp/plugin.cfg")'
$exportProjectContent = $projectContent.Replace($pluginSetting, 'enabled=PackedStringArray()')

try {
    # MCP 运行探针只用于编辑器调试，不能写入发布包的自动加载列表。
    [System.IO.File]::WriteAllText($projectPath, $exportProjectContent, [System.Text.UTF8Encoding]::new($false))
    & $GodotPath --headless --path $projectRoot --export-debug "Android Test" $outputPath
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $outputPath)) {
        throw "Android APK export failed."
    }
}
finally {
    [System.IO.File]::WriteAllText($projectPath, $projectContent, [System.Text.UTF8Encoding]::new($false))
}

$hash = (Get-FileHash $outputPath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Exported: $outputPath"
Write-Host "SHA256: $hash"
