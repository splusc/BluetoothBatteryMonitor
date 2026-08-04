namespace TrayMonitor;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\TrayMonitor", out var firstInstance);
        if (!firstInstance) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
