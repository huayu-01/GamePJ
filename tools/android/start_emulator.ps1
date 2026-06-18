param(
    [switch]$WipeData
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$mappedDrive = "R:"
$mappedRoot = "$mappedDrive\"
$currentMapping = subst | Where-Object { $_ -like "$mappedDrive\:*" }

if ($currentMapping) {
    $sourceProject = Join-Path $projectRoot "project.godot"
    $mappedProject = Join-Path $mappedRoot "project.godot"
    $mappingMatches = (Test-Path $mappedProject) -and
        ((Get-FileHash $sourceProject).Hash -eq (Get-FileHash $mappedProject).Hash)
    if (-not $mappingMatches) {
        throw "$mappedDrive is already mapped to another directory."
    }
}

if (-not $currentMapping) {
    subst $mappedDrive $projectRoot
}

$env:ANDROID_SDK_ROOT = Join-Path $mappedRoot "tools\android-sdk"
$env:ANDROID_HOME = $env:ANDROID_SDK_ROOT
$env:ANDROID_AVD_HOME = Join-Path $mappedRoot "tools\android-avd"

$emulator = Join-Path $env:ANDROID_SDK_ROOT "emulator\emulator.exe"
$adb = Join-Path $env:ANDROID_SDK_ROOT "platform-tools\adb.exe"
$avdName = "GamePJ_Portrait_ASCII_API35"

if (-not (Test-Path $emulator)) {
    throw "Android Emulator was not found: $emulator"
}

& $adb start-server | Out-Null
$running = & $adb devices
if ($running -match "emulator-\d+\s+device") {
    Write-Host "Android Emulator is already running."
    exit 0
}

$arguments = @(
    "-avd", $avdName,
    "-no-boot-anim",
    "-gpu", "auto",
    "-no-metrics"
)

if ($WipeData) {
    $arguments += "-wipe-data"
}

Start-Process -FilePath $emulator -ArgumentList $arguments | Out-Null
Write-Host "Starting $avdName in 1080x2400 portrait mode."
