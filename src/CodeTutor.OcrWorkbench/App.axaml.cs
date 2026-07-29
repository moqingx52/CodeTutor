using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CodeTutor.Infrastructure;
using CodeTutor.OcrWorkbench.ViewModels;
using CodeTutor.OcrWorkbench.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeTutor.OcrWorkbench;

public partial class App : global::Avalonia.Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override async void OnFrameworkInitializationCompleted()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        var ocrUrl = config["Ocr:BaseUrl"] ?? "http://127.0.0.1:18180";
        var maxCheckpoints = int.TryParse(config["Storage:MaxCheckpointCount"], out var mc) ? mc : 20;

        services.AddCodeTutorCaptureInfrastructure(
            ocrUrl,
            AppLaunchOptions.Current.CameraMode,
            AppLaunchOptions.Current.SourcePath,
            maxCheckpoints);

        services.AddSingleton<OcrWorkbenchViewModel>();

        _serviceProvider = services.BuildServiceProvider();
        await _serviceProvider.InitializeDatabaseAsync();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = _serviceProvider.GetRequiredService<OcrWorkbenchViewModel>();
            desktop.MainWindow = new OcrWorkbenchWindow { DataContext = vm };
            await vm.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
