using Avalonia;

namespace CodeTutor.OcrWorkbench;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppLaunchOptions.Parse(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
