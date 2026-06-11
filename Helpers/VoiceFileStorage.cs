namespace MeBio.Helpers;

public static class VoiceFileStorage
{
    private const string ProjectFileName = "MeBio.csproj";
    private static readonly string[] VoicesPathSegments = ["Data", "voices"];

    public static string VoicesDirectory
    {
        get
        {
            var projectRoot = FindProjectRootDirectory();
            return Path.Combine([projectRoot, .. VoicesPathSegments]);
        }
    }

    public static async Task<string> SaveAsync(int userId, byte[] wavBytes)
    {
        Directory.CreateDirectory(VoicesDirectory);
        var path = Path.Combine(VoicesDirectory, $"{userId}.wav");
        await File.WriteAllBytesAsync(path, wavBytes);
        return path;
    }

    public static string? GetPath(int userId)
    {
        var path = Path.Combine(VoicesDirectory, $"{userId}.wav");
        return File.Exists(path) ? path : null;
    }

    private static string FindProjectRootDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, ProjectFileName)))
                return dir.FullName;

            dir = dir.Parent;
        }

        return AppContext.BaseDirectory;
    }
}
