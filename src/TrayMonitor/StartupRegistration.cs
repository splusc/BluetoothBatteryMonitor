using Microsoft.Win32;
using System.Security;

namespace TrayMonitor;

internal sealed class StartupRegistration
{
    private const string DefaultKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TrayMonitor";

    private readonly string _keyPath;
    private readonly string? _executablePath;

    public StartupRegistration(string? keyPath = null, string? executablePath = null)
    {
        _keyPath = keyPath ?? DefaultKeyPath;
        _executablePath = executablePath ?? Environment.ProcessPath;
    }

    public bool IsEnabled()
    {
        var command = Command();
        if (command is null) return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
            return key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is string value
                && string.Equals(value, command, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return false;
        }
    }

    public bool SetEnabled(bool enabled)
    {
        if (Command() is not { } command) return false;

        try
        {
            if (enabled)
            {
                using var key = Registry.CurrentUser.CreateSubKey(_keyPath, writable: true);
                key.SetValue(ValueName, command, RegistryValueKind.String);
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

    private string? Command() => string.IsNullOrWhiteSpace(_executablePath) ? null : $"\"{_executablePath}\"";
}
