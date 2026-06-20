# Android APK 构建

项目不再内置 Android Emulator、系统镜像或虚拟设备数据。此目录仅保留 APK 导出工具。

导出带版本号的 APK：

```powershell
powershell -ExecutionPolicy Bypass -File tools/android/export_versioned_apk.ps1
```

输出文件格式：

```text
build/android/GamePJ-<版本号>.apk
```

使用实体 Android 设备测试时，可在手机中直接打开 APK 安装，或使用保留的 `tools/android-sdk/platform-tools/adb.exe` 手动安装。
