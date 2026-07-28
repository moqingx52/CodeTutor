namespace CodeTutor.Infrastructure.Paths;

public static class AppPaths
{
    public static string DataRoot
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CodeTutor");
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local",
                "share",
                "CodeTutor");
        }
    }

    public static string DatabasePath => Path.Combine(DataRoot, "codetutor.db");
    public static string SessionsRoot => Path.Combine(DataRoot, "sessions");
    public static string LogsRoot => Path.Combine(DataRoot, "logs");
    public static string TrashRoot => Path.Combine(DataRoot, ".trash");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(SessionsRoot);
        Directory.CreateDirectory(LogsRoot);
        Directory.CreateDirectory(TrashRoot);
    }

    public static string SessionDirectory(Guid sessionId, DateTimeOffset? createdAt = null)
    {
        var date = (createdAt ?? DateTimeOffset.UtcNow).ToString("yyyy-MM-dd");
        return Path.Combine(SessionsRoot, date, sessionId.ToString("N"));
    }
}
