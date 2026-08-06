# BluetoothBatteryMonitor Rename Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename the complete application surface from `TrayMonitor` to `BluetoothBatteryMonitor`, delete legacy settings and startup registration on launch, and publish the renamed executable.

**Architecture:** Keep the existing WinForms tray architecture and native Windows integrations. Rename active source/test identifiers mechanically, then add one small `LegacyDataCleanup` startup component whose filesystem and registry targets can be overridden by the existing dependency-free check executable.

**Tech Stack:** .NET 10, Windows Forms, Windows Runtime Bluetooth APIs, `System.Text.Json`, `Microsoft.Win32.Registry`, custom console checks.

---

## Working Tree Constraint

`src/TrayMonitor/BluetoothMonitor.cs` already contains uncommitted GATT battery-reading work. Preserve that exact implementation while moving the file and changing only its namespace. Do not stage or commit implementation changes unless the user explicitly approves including that pre-existing work. Ignore `.claude/`; update `CLAUDE.md` only because the approved rename specification includes current repository documentation.

### Task 1: Rename Projects, Namespaces, and Runtime Identifiers

**Files:**
- Move: `src/TrayMonitor` to `src/BluetoothBatteryMonitor`
- Move: `src/BluetoothBatteryMonitor/TrayMonitor.csproj` to `src/BluetoothBatteryMonitor/BluetoothBatteryMonitor.csproj`
- Move: `src/BluetoothBatteryMonitor/Assets/TrayMonitor.ico` to `src/BluetoothBatteryMonitor/Assets/BluetoothBatteryMonitor.ico`
- Move: `tests/TrayMonitor.Checks` to `tests/BluetoothBatteryMonitor.Checks`
- Move: `tests/BluetoothBatteryMonitor.Checks/TrayMonitor.Checks.csproj` to `tests/BluetoothBatteryMonitor.Checks/BluetoothBatteryMonitor.Checks.csproj`
- Modify: every `.cs` file under the two renamed directories
- Modify: `src/BluetoothBatteryMonitor/SettingsStore.cs`
- Modify: `src/BluetoothBatteryMonitor/StartupRegistration.cs`
- Modify: `src/BluetoothBatteryMonitor/Program.cs`
- Modify: `src/BluetoothBatteryMonitor/TrayApplicationContext.cs`
- Modify: `tests/BluetoothBatteryMonitor.Checks/AppIconChecks.cs`
- Modify: `tests/BluetoothBatteryMonitor.Checks/SettingsStoreChecks.cs`
- Modify: `tests/BluetoothBatteryMonitor.Checks/StartupRegistrationChecks.cs`

- [ ] **Step 1: Move the directories and named project resources**

Use explicit paths so the existing dirty `BluetoothMonitor.cs` moves without being regenerated:

```powershell
Move-Item -LiteralPath src\TrayMonitor -Destination src\BluetoothBatteryMonitor
Move-Item -LiteralPath src\BluetoothBatteryMonitor\TrayMonitor.csproj -Destination src\BluetoothBatteryMonitor\BluetoothBatteryMonitor.csproj
Move-Item -LiteralPath src\BluetoothBatteryMonitor\Assets\TrayMonitor.ico -Destination src\BluetoothBatteryMonitor\Assets\BluetoothBatteryMonitor.ico
Move-Item -LiteralPath tests\TrayMonitor.Checks -Destination tests\BluetoothBatteryMonitor.Checks
Move-Item -LiteralPath tests\BluetoothBatteryMonitor.Checks\TrayMonitor.Checks.csproj -Destination tests\BluetoothBatteryMonitor.Checks\BluetoothBatteryMonitor.Checks.csproj
```

- [ ] **Step 2: Rename source and check namespaces**

In every source `.cs` file, replace:

```csharp
namespace TrayMonitor;
```

with:

```csharp
namespace BluetoothBatteryMonitor;
```

In every check `.cs` file, replace `namespace TrayMonitor.Checks;` and imports as follows:

```csharp
using BluetoothBatteryMonitor;

namespace BluetoothBatteryMonitor.Checks;
```

Update the check runner import:

```csharp
using BluetoothBatteryMonitor.Checks;
```

- [ ] **Step 3: Update both project files and application metadata**

Set `src/BluetoothBatteryMonitor/BluetoothBatteryMonitor.csproj` to:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.22621.0</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ApplicationIcon>Assets\BluetoothBatteryMonitor.ico</ApplicationIcon>
    <Product>蓝牙电量监视器</Product>
    <AssemblyTitle>蓝牙电量监视器</AssemblyTitle>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="BluetoothBatteryMonitor.Checks" />
  </ItemGroup>
</Project>
```

Set the project reference in `tests/BluetoothBatteryMonitor.Checks/BluetoothBatteryMonitor.Checks.csproj` to:

```xml
<ProjectReference Include="..\..\src\BluetoothBatteryMonitor\BluetoothBatteryMonitor.csproj" />
```

- [ ] **Step 4: Update persistent and runtime names**

Change the default settings directory in `SettingsStore`:

```csharp
"BluetoothBatteryMonitor",
"settings.json");
```

Change `StartupRegistration.ValueName`:

```csharp
private const string ValueName = "BluetoothBatteryMonitor";
```

Change the mutex in `Program.Main`:

```csharp
using var mutex = new Mutex(true, @"Local\BluetoothBatteryMonitor", out var firstInstance);
```

Change the error-dialog title in `TrayApplicationContext`:

```csharp
"蓝牙电量监视器"
```

- [ ] **Step 5: Update path-sensitive checks**

In `AppIconChecks`, use these names in both `Run` and `FindRepositoryRoot`:

```csharp
var projectPath = Path.Combine(root, "src", "BluetoothBatteryMonitor", "BluetoothBatteryMonitor.csproj");
var iconPath = Path.Combine(root, "src", "BluetoothBatteryMonitor", "Assets", "BluetoothBatteryMonitor.ico");
Check.Equal(@"Assets\BluetoothBatteryMonitor.ico", configuredIcon, "application icon configuration");
```

Use `BluetoothBatteryMonitor` in temporary test names and startup-test paths. The startup check must assert the new registry value:

```csharp
const string executablePath = @"C:\Program Files\BluetoothBatteryMonitor\BluetoothBatteryMonitor.exe";
const string command = "\"C:\\Program Files\\BluetoothBatteryMonitor\\BluetoothBatteryMonitor.exe\"";
Check.Equal(command, key!.GetValue("BluetoothBatteryMonitor") as string, "startup command");
```

- [ ] **Step 6: Run existing checks and build**

Run:

```powershell
dotnet run --project tests\BluetoothBatteryMonitor.Checks\BluetoothBatteryMonitor.Checks.csproj -c Release
dotnet build src\BluetoothBatteryMonitor\BluetoothBatteryMonitor.csproj -c Release
```

Expected: checks print `All checks passed.` and both commands exit with zero errors.

### Task 2: Delete Legacy Settings and Startup Entry

**Files:**
- Create: `tests/BluetoothBatteryMonitor.Checks/LegacyDataCleanupChecks.cs`
- Modify: `tests/BluetoothBatteryMonitor.Checks/Program.cs`
- Create: `src/BluetoothBatteryMonitor/LegacyDataCleanup.cs`
- Modify: `src/BluetoothBatteryMonitor/Program.cs`

- [ ] **Step 1: Write the failing cleanup check**

Create `LegacyDataCleanupChecks.cs`:

```csharp
using Microsoft.Win32;
using BluetoothBatteryMonitor;

namespace BluetoothBatteryMonitor.Checks;

internal static class LegacyDataCleanupChecks
{
    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), $"BluetoothBatteryMonitor-{Guid.NewGuid():N}");
        var legacyDirectory = Path.Combine(root, "TrayMonitor");
        var keyPath = $"Software\\BluetoothBatteryMonitor.Tests\\{Guid.NewGuid():N}";

        try
        {
            Directory.CreateDirectory(legacyDirectory);
            File.WriteAllText(Path.Combine(legacyDirectory, "settings.json"), "{}");
            using (var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true))
            {
                key.SetValue("TrayMonitor", "old-command");
                key.SetValue("KeepMe", "other-command");
            }

            LegacyDataCleanup.Run(legacyDirectory, keyPath);

            Check.Equal(false, Directory.Exists(legacyDirectory), "legacy settings removed");
            using var remaining = Registry.CurrentUser.OpenSubKey(keyPath);
            Check.Equal(null, remaining!.GetValue("TrayMonitor"), "legacy startup removed");
            Check.Equal("other-command", remaining.GetValue("KeepMe") as string, "unrelated startup preserved");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }
    }
}
```

Call it from the check runner before `StartupRegistrationChecks.Run();`:

```csharp
LegacyDataCleanupChecks.Run();
```

- [ ] **Step 2: Run the check to verify it fails**

Run:

```powershell
dotnet run --project tests\BluetoothBatteryMonitor.Checks\BluetoothBatteryMonitor.Checks.csproj -c Release
```

Expected: compilation fails because `LegacyDataCleanup` does not exist.

- [ ] **Step 3: Add the minimal cleanup implementation**

Create `LegacyDataCleanup.cs`:

```csharp
using Microsoft.Win32;
using System.Security;

namespace BluetoothBatteryMonitor;

internal static class LegacyDataCleanup
{
    private const string DefaultStartupKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void Run(string? settingsDirectory = null, string? startupKeyPath = null)
    {
        settingsDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrayMonitor");
        startupKeyPath ??= DefaultStartupKeyPath;

        try
        {
            if (Directory.Exists(settingsDirectory)) Directory.Delete(settingsDirectory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(startupKeyPath, writable: true);
            key?.DeleteValue("TrayMonitor", throwOnMissingValue: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
        }
    }
}
```

Call it after the new mutex confirms this is the first instance:

```csharp
if (!firstInstance) return;

LegacyDataCleanup.Run();
ApplicationConfiguration.Initialize();
```

- [ ] **Step 4: Run the checks and build**

Run:

```powershell
dotnet run --project tests\BluetoothBatteryMonitor.Checks\BluetoothBatteryMonitor.Checks.csproj -c Release
dotnet build src\BluetoothBatteryMonitor\BluetoothBatteryMonitor.csproj -c Release
```

Expected: `All checks passed.`, zero warnings, and zero errors.

### Task 3: Update Current Documentation

**Files:**
- Modify: `README.md`
- Modify: `CLAUDE.md`

- [ ] **Step 1: Replace current product references**

Use `BluetoothBatteryMonitor` for the product, source/test paths, executable, mutex, settings directory, startup value, namespaces, and architecture headings. Use the Chinese name where user-facing prose names the application:

```markdown
# BluetoothBatteryMonitor

Windows 11 tray application “蓝牙电量监视器” that displays Bluetooth-device battery state as a battery icon.
```

The build and publish commands must be:

```powershell
dotnet run --project tests/BluetoothBatteryMonitor.Checks/BluetoothBatteryMonitor.Checks.csproj
dotnet build src/BluetoothBatteryMonitor/BluetoothBatteryMonitor.csproj
dotnet publish src/BluetoothBatteryMonitor/BluetoothBatteryMonitor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```

- [ ] **Step 2: Confirm active files no longer use the old product name**

Run:

```powershell
rg -n "TrayMonitor|traymonitor" src tests README.md CLAUDE.md
```

Expected: only the intentional legacy cleanup directory, registry value, and related test fixtures contain `TrayMonitor`; repository root text may remain in `CLAUDE.md` only if explicitly describing the unchanged checkout path.

### Task 4: Verify and Publish the Renamed Executable

**Files:**
- Replace generated artifact: `artifacts/win-x64/TrayMonitor.exe` with `artifacts/win-x64/BluetoothBatteryMonitor.exe`

- [ ] **Step 1: Run final checks and Release build**

Run:

```powershell
dotnet run --project tests\BluetoothBatteryMonitor.Checks\BluetoothBatteryMonitor.Checks.csproj -c Release
dotnet build src\BluetoothBatteryMonitor\BluetoothBatteryMonitor.csproj -c Release
```

Expected: `All checks passed.`, zero warnings, and zero errors.

- [ ] **Step 2: Remove only the old generated executable and publish**

Resolve and verify the exact artifact directory, delete only the old EXE if present, then publish:

```powershell
$artifactDirectory = (Resolve-Path 'artifacts\win-x64').Path
if ($artifactDirectory -ne (Join-Path $PWD 'artifacts\win-x64')) { throw 'Unexpected artifact directory' }
Remove-Item -LiteralPath (Join-Path $artifactDirectory 'TrayMonitor.exe') -Force -ErrorAction SilentlyContinue
dotnet publish src\BluetoothBatteryMonitor\BluetoothBatteryMonitor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts\win-x64
```

Expected: publish succeeds and reports `BluetoothBatteryMonitor -> ...\artifacts\win-x64\`.

- [ ] **Step 3: Verify the final artifact and embedded icon**

Run:

```powershell
Get-Item artifacts\win-x64\BluetoothBatteryMonitor.exe
if (Test-Path artifacts\win-x64\TrayMonitor.exe) { throw 'Old executable still exists' }
$icon = [System.Drawing.Icon]::ExtractAssociatedIcon((Resolve-Path 'artifacts\win-x64\BluetoothBatteryMonitor.exe'))
if ($null -eq $icon) { throw 'Embedded executable icon is missing' }
$icon.Dispose()
```

Expected: the new executable exists, the old executable does not, and icon extraction succeeds.

- [ ] **Step 4: Review the final working tree**

Run:

```powershell
git status --short
git diff --check
```

Expected: the renamed implementation, tests, current docs, preserved GATT work, and generated artifact state are visible; `.claude/` remains untouched. Do not stage or commit the implementation without explicit permission because the renamed `BluetoothMonitor.cs` includes pre-existing user changes.
