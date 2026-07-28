namespace CodeTutor.Desktop;

public sealed class AppLaunchOptions
{
    public string CameraMode { get; init; } = OperatingSystem.IsWindows() ? "auto" : "mock-video";
    public string? SourcePath { get; init; }

    public static AppLaunchOptions Current { get; private set; } = new();

    public static void Parse(string[] args)
    {
        var mode = OperatingSystem.IsWindows() ? "auto" : "mock-video";
        string? source = null;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--camera" && i + 1 < args.Length)
                mode = args[++i];
            else if (args[i] == "--source" && i + 1 < args.Length)
                source = args[++i];
        }

        Current = new AppLaunchOptions { CameraMode = mode, SourcePath = source };
    }
}
