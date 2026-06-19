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

$adb = Join-Path $mappedRoot "tools\android-sdk\platform-tools\adb.exe"
$presetPath = Join-Path $projectRoot "export_presets.cfg"
$versionMatch = Select-String -Path $presetPath -Pattern '^version/name="([^"]+)"$' | Select-Object -First 1
if (-not $versionMatch) {
    throw "Android version/name was not found in export_presets.cfg."
}
$versionName = $versionMatch.Matches[0].Groups[1].Value
$apk = Join-Path $mappedRoot "build\android\GamePJ-$versionName.apk"

if (-not (Test-Path $adb)) {
    throw "adb was not found: $adb"
}

if (-not (Test-Path $apk)) {
    throw "Test APK was not found: $apk"
}

& $adb wait-for-device
$deadline = (Get-Date).AddMinutes(4)
do {
    Start-Sleep -Seconds 3
    $bootCompleted = (& $adb shell getprop sys.boot_completed 2>$null).Trim()
} while ($bootCompleted -ne "1" -and (Get-Date) -lt $deadline)

if ($bootCompleted -ne "1") {
    throw "Android Emulator did not finish booting within four minutes."
}

& $adb install -r $apk
if ($LASTEXITCODE -ne 0) {
    throw "APK installation failed."
}

& $adb shell monkey -p com.huayu.gamepj -c android.intent.category.LAUNCHER 1 | Out-Null
Write-Host "GamePJ $versionName APK was installed and launched."
