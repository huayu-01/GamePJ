param(
    [string]$GodotPath = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$presetPath = Join-Path $projectRoot "export_presets.cfg"
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

& $GodotPath --headless --path $projectRoot --export-debug "Android Test" $outputPath
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $outputPath)) {
    throw "Android APK export failed."
}

$hash = (Get-FileHash $outputPath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Exported: $outputPath"
Write-Host "SHA256: $hash"
