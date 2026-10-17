# 内置中文字体

- 名称：Noto Sans SC，可变字重原文件；本项目UI使用500字重。
- 上游：[Google Fonts / Noto Sans SC](https://github.com/google/fonts/tree/main/ofl/notosanssc)。
- 固定提交：`a85815a42757630ce188fdad368c2dfc444d4773`。
- [原始字体文件](https://raw.githubusercontent.com/google/fonts/a85815a42757630ce188fdad368c2dfc444d4773/ofl/notosanssc/NotoSansSC%5Bwght%5D.ttf)。下载于2026-10-17。
- 本地文件名改为NotoSansSC.ttf，字体二进制未修改；17,772,300字节。
- SHA-256：`A3041811A78C361B1DE50F953C805E0244951C21C5BD412F7232EF0D899AF0DA`。
- 版权：Adobe；SIL Open Font License 1.1，完整原文随OFL.txt保留。允许随游戏嵌入和分发，字体本身不能单独出售。

主题和Label3D默认字体共用Assets/UI/Themes/ui_font.tres，不依赖用户操作系统安装中文字体。

Godot 4.7.2使用整数OpenType tag `2003265652`设置500字重；专项探针从TextServer原生字体坐标读回500后才判通过。
