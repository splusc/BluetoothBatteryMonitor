# 将原始剪影预绘制到实际托盘尺寸，避免运行时反复解码大图和缩放。
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/BluetoothBatteryMonitor/Assets/DeviceIcons'
foreach ($source in Get-ChildItem -LiteralPath (Join-Path $assetDirectory 'Source') -Filter '*.png') {
    $image = [System.Drawing.Image]::FromFile($source.FullName)
    $bitmap = New-Object System.Drawing.Bitmap(32, 32)
    $bitmap.SetResolution(96, 96)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        # 按实际可见像素裁去素材留白，保持原始宽高比，在托盘画布中居中。
        $minX = $image.Width; $minY = $image.Height; $maxX = -1; $maxY = -1
        for ($y = 0; $y -lt $image.Height; $y++) {
            for ($x = 0; $x -lt $image.Width; $x++) {
                if ($image.GetPixel($x, $y).A -gt 16) {
                    $minX = [Math]::Min($minX, $x); $maxX = [Math]::Max($maxX, $x)
                    $minY = [Math]::Min($minY, $y); $maxY = [Math]::Max($maxY, $y)
                }
            }
        }
        if ($maxX -lt 0) { throw "素材为空：$($source.Name)" }
        $width = $maxX - $minX + 1; $height = $maxY - $minY + 1
        $maxHeight = if ($source.BaseName -in @('DualShock4', 'DualSense', 'Xbox', 'Gamepad')) { 32.0 } else { 28.0 }
        $scale = [Math]::Min(32.0 / $width, $maxHeight / $height)
        $bounds = [System.Drawing.RectangleF]::new((32 - $width * $scale) / 2, (32 - $height * $scale) / 2, $width * $scale, $height * $scale)
        $crop = [System.Drawing.RectangleF]::new($minX, $minY, $width, $height)
        $graphics.DrawImage($image, $bounds, $crop, [System.Drawing.GraphicsUnit]::Pixel)
        $bitmap.Save((Join-Path $assetDirectory $source.Name), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $graphics.Dispose()
        $bitmap.Dispose()
        $image.Dispose()
    }
}
