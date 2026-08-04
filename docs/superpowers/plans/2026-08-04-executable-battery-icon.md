# Executable Battery Icon Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Embed a crisp, static half-charge battery icon in the Windows executable while leaving the dynamic tray icon unchanged.

**Architecture:** Commit one multi-frame ICO asset and reference it through MSBuild `ApplicationIcon`. Generate the binary asset once with Windows `System.Drawing`; keep generation code temporary so the shipped application and repository gain no dependency or tool project.

**Tech Stack:** .NET 10, WinForms, MSBuild, Windows `System.Drawing`, existing console checks

---

### Task 1: ICO asset and build configuration

**Files:**
- Create: `src/TrayMonitor/Assets/TrayMonitor.ico`
- Create: `tests/TrayMonitor.Checks/AppIconChecks.cs`
- Modify: `tests/TrayMonitor.Checks/Program.cs`
- Modify: `src/TrayMonitor/TrayMonitor.csproj`

- [ ] **Step 1: Write the failing ICO configuration check**

Create `tests/TrayMonitor.Checks/AppIconChecks.cs`:

```csharp
using System.Xml.Linq;

namespace TrayMonitor.Checks;

internal static class AppIconChecks
{
    private static readonly int[] RequiredSizes = [16, 24, 32, 48, 64, 128, 256];

    public static void Run()
    {
        var root = FindRepositoryRoot();
        var projectPath = Path.Combine(root, "src", "TrayMonitor", "TrayMonitor.csproj");
        var iconPath = Path.Combine(root, "src", "TrayMonitor", "Assets", "TrayMonitor.ico");
        var configuredIcon = XDocument.Load(projectPath)
            .Descendants("ApplicationIcon")
            .SingleOrDefault()?.Value;

        Check.Equal(@"Assets\TrayMonitor.ico", configuredIcon, "application icon configuration");
        Check.True(File.Exists(iconPath), "application icon file");

        using var reader = new BinaryReader(File.OpenRead(iconPath));
        Check.Equal((ushort)0, reader.ReadUInt16(), "ICO reserved field");
        Check.Equal((ushort)1, reader.ReadUInt16(), "ICO image type");
        var count = reader.ReadUInt16();
        Check.True(count >= RequiredSizes.Length, "ICO image count");

        var sizes = new HashSet<int>();
        for (var index = 0; index < count; index++)
        {
            var width = reader.ReadByte();
            var height = reader.ReadByte();
            sizes.Add(width == 0 ? 256 : width);
            Check.Equal(width, height, $"ICO square frame {index}");
            reader.BaseStream.Seek(14, SeekOrigin.Current);
        }

        foreach (var size in RequiredSizes)
            Check.True(sizes.Contains(size), $"ICO contains {size}px frame");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "TrayMonitor", "TrayMonitor.csproj")))
                return directory.FullName;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
```

Add `AppIconChecks.Run();` to `tests/TrayMonitor.Checks/Program.cs` before the registry-mutating startup check.

- [ ] **Step 2: Run the check and verify RED**

Run in the normal-user context:

```powershell
C:\Users\SC\AppData\Local\Microsoft\dotnet\dotnet.exe run --project tests\TrayMonitor.Checks\TrayMonitor.Checks.csproj -c Release
```

Expected: FAIL at `application icon configuration` because `ApplicationIcon` and the ICO asset do not exist.

- [ ] **Step 3: Generate the multi-frame ICO asset**

Use this temporary PowerShell script from the repository root. It renders every size separately and packs the PNG frames into a Windows ICO container; do not commit the script itself.

```powershell
Add-Type -AssemblyName System.Drawing

$iconOutputPath = Join-Path $PWD 'src\TrayMonitor\Assets\TrayMonitor.ico'
$iconDirectory = Split-Path -Parent $iconOutputPath
[System.IO.Directory]::CreateDirectory($iconDirectory) | Out-Null
$iconSizes = @(16, 24, 32, 48, 64, 128, 256)
$iconImages = [System.Collections.Generic.List[byte[]]]::new()

foreach ($iconSize in $iconSizes) {
    $bitmap = [System.Drawing.Bitmap]::new(
        $iconSize,
        $iconSize,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $stream = [System.IO.MemoryStream]::new()
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.SmoothingMode = if ($iconSize -le 32) {
            [System.Drawing.Drawing2D.SmoothingMode]::None
        } else {
            [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        }

        $stroke = [Math]::Max(1, [int][Math]::Round($iconSize * 0.08))
        $bodyX = [Math]::Max(1, [int][Math]::Round($iconSize * 0.08))
        $bodyY = [Math]::Max(2, [int][Math]::Round($iconSize * 0.20))
        $bodyWidth = [int][Math]::Round($iconSize * 0.75)
        $bodyHeight = [int][Math]::Round($iconSize * 0.60)
        $terminalWidth = [Math]::Max(1, [int][Math]::Round($iconSize * 0.09))
        $terminalHeight = [Math]::Max(2, [int][Math]::Round($bodyHeight * 0.38))
        $terminalY = $bodyY + [int][Math]::Floor(($bodyHeight - $terminalHeight) / 2)
        $innerX = $bodyX + $stroke
        $innerY = $bodyY + $stroke
        $innerWidth = [Math]::Max(1, $bodyWidth - 2 * $stroke)
        $innerHeight = [Math]::Max(1, $bodyHeight - 2 * $stroke)
        $fillWidth = [Math]::Max(1, [int][Math]::Round($innerWidth * 0.5))

        $outline = [System.Drawing.Color]::FromArgb(255, 32, 33, 36)
        $empty = [System.Drawing.Color]::FromArgb(255, 250, 250, 250)
        $halfCharge = [System.Drawing.Color]::FromArgb(255, 245, 183, 0)
        $outlinePen = [System.Drawing.Pen]::new($outline, $stroke)
        $outlineBrush = [System.Drawing.SolidBrush]::new($outline)
        $emptyBrush = [System.Drawing.SolidBrush]::new($empty)
        $chargeBrush = [System.Drawing.SolidBrush]::new($halfCharge)
        try {
            $graphics.FillRectangle($emptyBrush, $innerX, $innerY, $innerWidth, $innerHeight)
            $graphics.FillRectangle($chargeBrush, $innerX, $innerY, $fillWidth, $innerHeight)
            $graphics.DrawRectangle($outlinePen, $bodyX, $bodyY, $bodyWidth, $bodyHeight)
            $graphics.FillRectangle(
                $outlineBrush,
                $bodyX + $bodyWidth,
                $terminalY,
                $terminalWidth,
                $terminalHeight)
        }
        finally {
            $outlinePen.Dispose()
            $outlineBrush.Dispose()
            $emptyBrush.Dispose()
            $chargeBrush.Dispose()
        }

        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $iconImages.Add($stream.ToArray())
    }
    finally {
        $stream.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$fileStream = [System.IO.File]::Create($iconOutputPath)
$writer = [System.IO.BinaryWriter]::new($fileStream)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$iconSizes.Count)
    $imageOffset = 6 + 16 * $iconSizes.Count

    for ($index = 0; $index -lt $iconSizes.Count; $index++) {
        $encodedSize = if ($iconSizes[$index] -eq 256) { 0 } else { $iconSizes[$index] }
        $writer.Write([byte]$encodedSize)
        $writer.Write([byte]$encodedSize)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$iconImages[$index].Length)
        $writer.Write([uint32]$imageOffset)
        $imageOffset += $iconImages[$index].Length
    }

    foreach ($iconImage in $iconImages) {
        $writer.Write($iconImage)
    }
}
finally {
    $writer.Dispose()
    $fileStream.Dispose()
}
```

- [ ] **Step 4: Configure the Windows application icon**

Add this property to the existing `PropertyGroup` in `src/TrayMonitor/TrayMonitor.csproj`:

```xml
<ApplicationIcon>Assets\TrayMonitor.ico</ApplicationIcon>
```

- [ ] **Step 5: Run checks and verify GREEN**

Run in the normal-user context:

```powershell
C:\Users\SC\AppData\Local\Microsoft\dotnet\dotnet.exe run --project tests\TrayMonitor.Checks\TrayMonitor.Checks.csproj -c Release
```

Expected: `All checks passed.`

- [ ] **Step 6: Build Release**

```powershell
C:\Users\SC\AppData\Local\Microsoft\dotnet\dotnet.exe build src\TrayMonitor\TrayMonitor.csproj -c Release
```

Expected: 0 warnings and 0 errors.

- [ ] **Step 7: Commit the icon**

```powershell
git add src/TrayMonitor/Assets/TrayMonitor.ico src/TrayMonitor/TrayMonitor.csproj tests/TrayMonitor.Checks/AppIconChecks.cs tests/TrayMonitor.Checks/Program.cs
git commit -m "feat: add executable battery icon"
```

### Task 2: Visual verification and release

**Files:**
- Replace generated artifact: `artifacts/win-x64/TrayMonitor.exe`
- Create ignored preview: `artifacts/app-icon-preview.png`

- [ ] **Step 1: Extract the compiled EXE icon**

```powershell
Add-Type -AssemblyName System.Drawing
$builtExe = (Resolve-Path 'src\TrayMonitor\bin\Release\net10.0-windows10.0.22621.0\TrayMonitor.exe').Path
$associatedIcon = [System.Drawing.Icon]::ExtractAssociatedIcon($builtExe)
if ($null -eq $associatedIcon) { throw 'Compiled EXE has no associated icon.' }
$preview = $associatedIcon.ToBitmap()
try {
    $preview.Save(
        (Join-Path $PWD 'artifacts\app-icon-preview.png'),
        [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $preview.Dispose()
    $associatedIcon.Dispose()
}
```

- [ ] **Step 2: Inspect the preview**

Open `artifacts/app-icon-preview.png` with the local image viewer. Confirm transparent surroundings, a dark battery outline, and yellow fill covering about half of the interior without clipping.

- [ ] **Step 3: Publish after the icon commit**

```powershell
C:\Users\SC\AppData\Local\Microsoft\dotnet\dotnet.exe publish src\TrayMonitor\TrayMonitor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts\win-x64
```

Expected: `artifacts\win-x64\TrayMonitor.exe` is recreated successfully.

- [ ] **Step 4: Verify the published artifact**

Confirm the file exists, has a non-null associated icon, and its ProductVersion ends with the current Git commit SHA. Run the full checks and Release build once more, then confirm `git status --short` is empty.
