using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CodeTutor.Desktop.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CodeTutor.Infrastructure;

namespace CodeTutor.Desktop;

public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        var ocrUrl = config["Ocr:BaseUrl"] ?? "http://127.0.0.1:18180";
        services.AddCodeTutorInfrastructure(ocrUrl);
        services.AddSingleton<MainWindowViewModel>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = services.BuildServiceProvider().GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new Views.MainWindow { DataContext = vm };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
