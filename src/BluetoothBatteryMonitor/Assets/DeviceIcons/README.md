# 内置设备图形

`Xbox.png`、`DualShock4.png`、`DualSense.png` 为用户确认的白色手柄剪影。`Gamepad.png` 与 Xbox 使用相同轮廓，但移除了品牌标志，用于未知型号的手柄。

`Keyboard.png`、`Mouse.png`、`Headphones.png`、`Unknown.png` 分别派生自 Lucide 的 keyboard、mouse、headphones、bluetooth 图形，许可证见 `LICENSE.txt`。

程序文件图标 `../BluetoothBatteryMonitor.ico` 使用同一蓝牙剪影的白色版本，搭配白色空心电池轮廓，电池内部和背景透明，包含 16、24、32、48、64、128、256 像素图像。电池边框与蓝牙标志采用接近的线宽，32 像素画布上约为 2.5 像素，并按各尺寸的像素网格取整。蓝牙部分沿用 Lucide 许可，电池由程序绘制。

PS4 外轮廓参考 [GamepadViewer PS4 图形](https://gamepadviewer.com/ps4-assets/base.svg)，PS5 轮廓依据用户提供的 DualSense 正面参考绘制。电池由程序绘制，不包含在图片中。

所有 PNG 都是透明背景白色图形，作为嵌入资源随单文件 EXE 发布；替换图片时需保留相应文件名。

根目录 PNG 为预绘制的 32×32 托盘资源，按透明通道裁去原图留白后保持原始宽高比、居中显示。四种手柄等比放大到 32×32 画布允许的最大尺寸，不拉伸、不裁切轮廓。电池在运行时覆盖最右侧，宽 11 像素，约占画布的 33%。每张图的解码缓冲仅为 4KB。384×384 原始剪影保留在 `Source/`，不嵌入程序集。修改原始剪影后运行 `scripts/Export-TrayDeviceIcons.ps1` 重新生成托盘资源。
