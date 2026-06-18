# 项目本地 Android Emulator

Android SDK、Emulator、API 35/36 系统镜像和虚拟设备数据安装在本项目目录中，但因体积较大被 `.gitignore` 排除，不会提交到 Git。

## 启动

```powershell
powershell -ExecutionPolicy Bypass -File tools/android/start_emulator.ps1
```

如需清除模拟器数据并冷启动：

```powershell
powershell -ExecutionPolicy Bypass -File tools/android/start_emulator.ps1 -WipeData
```

## 安装测试 APK

```powershell
powershell -ExecutionPolicy Bypass -File tools/android/install_test_apk.ps1
```

脚本会使用 `R:` 作为临时英文路径映射，以规避 Android Emulator 对中文路径支持不完整的问题。项目文件仍实际保存在原目录中。
