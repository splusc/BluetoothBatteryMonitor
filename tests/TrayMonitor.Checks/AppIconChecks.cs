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
