# Start With Windows Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a current-user “开机自动启动” tray-menu option backed by the native Windows Run registry key.

**Architecture:** A focused `StartupRegistration` class owns registry access and treats the current executable path as the desired state. `TrayApplicationContext` renders and toggles that state; the JSON settings model remains unchanged.

**Tech Stack:** .NET 10, WinForms, `Microsoft.Win32.Registry`, existing console checks

---

### Task 1: Current-user startup registration

**Files:**
- Create: `src/TrayMonitor/StartupRegistration.cs`
- Create: `tests/TrayMonitor.Checks/StartupRegistrationChecks.cs`
- Modify: `tests/TrayMonitor.Checks/Program.cs`

- [ ] **Step 1: Write the failing startup registration check**

Create `StartupRegistrationChecks.cs` with an isolated key below `HKCU\Software\TrayMonitor.Tests`, then add `StartupRegistrationChecks.Run();` to the checks program:

```csharp
using Microsoft.Win32;
using TrayMonitor;

namespace TrayMonitor.Checks;

internal static class StartupRegistrationChecks
{
    public static void Run()
    {
        var keyPath = $@"Software\TrayMonitor.Tests\{Guid.NewGuid():N}";
        const string executablePath = @"C:\Program Files\TrayMonitor\TrayMonitor.exe";

        try
        {
            var registration = new StartupRegistration(keyPath, executablePath);
            Check.Equal(false, registration.IsEnabled(), "startup defaults off");
            Check.True(registration.SetEnabled(true), "startup enable");
            Check.True(registration.IsEnabled(), "startup enabled state");

            using (var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true))
            {
                Check.Equal($"\"{executablePath}\"", key?.GetValue("TrayMonitor") as string, "quoted startup command");
                key?.SetValue("TrayMonitor", "\"C:\\Old\\TrayMonitor.exe\"");
            }

            Check.Equal(false, registration.IsEnabled(), "stale path is disabled");
            Check.True(registration.SetEnabled(false), "startup disable");
            Check.Equal(false, registration.IsEnabled(), "startup disabled state");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }
    }
}
```

- [ ] **Step 2: Run the check and verify RED**

Run:

```powershell
C:\Users\SC\AppData\Local\Microsoft\dotnet\dotnet.exe run --project tests\TrayMonitor.Checks\TrayMonitor.Checks.csproj -c Release
```

Expected: compilation fails because `StartupRegistration` does not exist.

- [ ] **Step 3: Add the minimal registry implementation**

Create `StartupRegistration.cs`:

```csharp
using Microsoft.Win32;
using System.Security;

namespace TrayMonitor;

internal sealed class StartupRegistration
{
    private const string DefaultKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TrayMonitor";
    private readonly string _keyPath;
    private readonly string? _command;

    public StartupRegistration(string? keyPath = null, string? executablePath = null)
    {
        _keyPath = keyPath ?? DefaultKeyPath;
        var path = executablePath ?? Environment.ProcessPath;
        _command = string.IsNullOrWhiteSpace(path) ? null : $"\"{path}\"";
    }

    public bool IsEnabled()
    {
        if (_command is null) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
            return string.Equals(key?.GetValue(ValueName) as string, _command, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return false;
        }
    }

    public bool SetEnabled(bool enabled)
    {
        if (_command is null) return false;
        try
        {
            if (enabled)
            {
                using var key = Registry.CurrentUser.CreateSubKey(_keyPath, writable: true);
                key.SetValue(ValueName, _command, RegistryValueKind.String);
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true);
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return false;
        }
    }
}
```

- [ ] **Step 4: Run the checks and verify GREEN**

```powershell
C:\Users\SC\AppData\Local\Microsoft\dotnet\dotnet.exe run --project tests\TrayMonitor.Checks\TrayMonitor.Checks.csproj -c Release
```

Expected: `All checks passed.`

- [ ] **Step 5: Commit the registration behavior**

```powershell
git add src/TrayMonitor/StartupRegistration.cs tests/TrayMonitor.Checks/StartupRegistrationChecks.cs tests/TrayMonitor.Checks/Program.cs
git commit -m "feat: add current-user startup registration"
```

### Task 2: Tray menu option

**Files:**
- Modify: `src/TrayMonitor/TrayApplicationContext.cs`

- [ ] **Step 1: Add the registration dependency and menu item**

Add this field beside the other service fields:

```csharp
private readonly StartupRegistration _startupRegistration = new();
```

In `BuildMenu`, add this block immediately before the existing `立即刷新` item:

```csharp
var startup = new ToolStripMenuItem("开机自动启动")
{
    Checked = _startupRegistration.IsEnabled(),
    CheckOnClick = true
};
startup.Click += (_, _) =>
{
    var succeeded = _startupRegistration.SetEnabled(startup.Checked);
    startup.Checked = _startupRegistration.IsEnabled();
    if (!succeeded)
    {
        MessageBox.Show(
            "无法修改开机自动启动设置。",
            "TrayMonitor",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
};
_menu.Items.Add(startup);
```

- [ ] **Step 2: Run all checks and build Release**

```powershell
C:\Users\SC\AppData\Local\Microsoft\dotnet\dotnet.exe run --project tests\TrayMonitor.Checks\TrayMonitor.Checks.csproj -c Release
C:\Users\SC\AppData\Local\Microsoft\dotnet\dotnet.exe build src\TrayMonitor\TrayMonitor.csproj -c Release
```

Expected: checks print `All checks passed.`; build has 0 warnings and 0 errors.

- [ ] **Step 3: Commit the menu integration**

```powershell
git add src/TrayMonitor/TrayApplicationContext.cs
git commit -m "feat: add startup tray option"
```

### Task 3: Documentation and release artifact

**Files:**
- Modify: `README.md`
- Replace generated artifact: `artifacts/win-x64/TrayMonitor.exe`

- [ ] **Step 1: Document the option**

Add this Usage bullet:

```markdown
- Use `开机自动启动` (Start with Windows) to launch TrayMonitor when the current Windows user signs in.
```

- [ ] **Step 2: Run final verification**

```powershell
C:\Users\SC\AppData\Local\Microsoft\dotnet\dotnet.exe run --project tests\TrayMonitor.Checks\TrayMonitor.Checks.csproj -c Release
C:\Users\SC\AppData\Local\Microsoft\dotnet\dotnet.exe build src\TrayMonitor\TrayMonitor.csproj -c Release
```

Expected: checks print `All checks passed.`; build has 0 warnings and 0 errors.

- [ ] **Step 3: Publish the self-contained executable**

```powershell
C:\Users\SC\AppData\Local\Microsoft\dotnet\dotnet.exe publish src\TrayMonitor\TrayMonitor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts\win-x64
```

Expected: `artifacts\win-x64\TrayMonitor.exe` is recreated successfully.

- [ ] **Step 4: Commit documentation**

```powershell
git add README.md
git commit -m "docs: document startup option"
```
