using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CodeTutor.Desktop.ViewModels;
using CodeTutor.Desktop.Views;
using CodeTutor.Application.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CodeTutor.Infrastructure;
using CodeTutor.Infrastructure.Ai;

namespace CodeTutor.Desktop;

public partial class App : Avalonia.Application
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

        services.AddSingleton(new DeepSeekOptions
        {
            BaseUrl = AiProviderDefaults.DeepSeekBaseUrl,
            Model = config["Ai:DeepSeek:Model"] ?? AiProviderDefaults.DeepSeekDefaultModel,
            TimeoutSeconds = int.TryParse(config["Ai:DeepSeek:TimeoutSeconds"], out var ts) ? ts : 60,
            ThinkingEnabled = bool.TryParse(config["Ai:DeepSeek:ThinkingEnabled"], out var te) && te
        });

        services.AddSingleton(new VolcanoArkOptions
        {
            BaseUrl = AiProviderDefaults.VolcanoArkBaseUrl,
            Model = config["Ai:Vision:Model"] ?? AiProviderDefaults.VolcanoArkDefaultModel,
            TimeoutSeconds = int.TryParse(config["Ai:Vision:TimeoutSeconds"], out var vts) ? vts : 120,
            MaxImages = int.TryParse(config["Ai:Vision:MaxImages"], out var mi) ? mi : 8
        });

        services.AddCodeTutorInfrastructure(
            ocrUrl,
            AppLaunchOptions.Current.CameraMode,
            AppLaunchOptions.Current.SourcePath,
            maxCheckpoints);

        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<HistoryWindowViewModel>();

        _serviceProvider = services.BuildServiceProvider();
        await _serviceProvider.InitializeDatabaseAsync();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            var mainWindow = new MainWindow { DataContext = vm };
            vm.OwnerWindow = mainWindow;
            desktop.MainWindow = mainWindow;
            await vm.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
