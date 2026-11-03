# 将原始剪影预绘制到实际托盘尺寸，避免运行时反复解码大图和缩放。
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/BluetoothBatteryMonitor/Assets/DeviceIcons'
foreach ($source in Get-ChildItem -LiteralPath (Join-Path $assetDirectory 'Source') -Filter '*.png') {
    $image = [System.Drawing.Image]::FromFile($source.FullName)
    $bitmap = New-Object System.Drawing.Bitmap(32, 32)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        # 保持已确认的索尼手柄大小补偿及设备图形位置。
        $bounds = if ($source.BaseName -in @('DualShock4', 'DualSense')) {
            New-Object System.Drawing.RectangleF(-2, 0, 34, 34)
        } else {
            New-Object System.Drawing.RectangleF(0, 4.48, 28.16, 28.16)
        }
        $graphics.DrawImage($image, $bounds)
        $bitmap.Save((Join-Path $assetDirectory $source.Name), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $graphics.Dispose()
        $bitmap.Dispose()
        $image.Dispose()
    }
}
