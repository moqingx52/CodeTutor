using Microsoft.Extensions.DependencyInjection;
using CodeTutor.Application.Abstractions;
using CodeTutor.Application.State;
using CodeTutor.Application.UseCases;
using CodeTutor.Infrastructure.Ai;
using CodeTutor.Infrastructure.Camera;
using CodeTutor.Infrastructure.Imaging;
using CodeTutor.Infrastructure.Ocr;
using CodeTutor.Infrastructure.Persistence;
using CodeTutor.Infrastructure.Secrets;
using CodeTutor.Infrastructure.Storage;
using CodeTutor.Infrastructure.Paths;

namespace CodeTutor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCodeTutorInfrastructure(
        this IServiceCollection services,
        string ocrBaseUrl,
        string cameraMode,
        string? cameraSource,
        int maxCheckpointCount = 20)
    {
        AppPaths.EnsureCreated();

        var dbInitializer = new SqliteDatabaseInitializer(AppPaths.DatabasePath);
        services.AddSingleton(dbInitializer);
        services.AddSingleton<ISessionRepository, SqliteSessionRepository>();
        services.AddSingleton<ICheckpointStore>(sp =>
            new SqliteCheckpointStore(sp.GetRequiredService<SqliteDatabaseInitializer>(), maxCheckpointCount));
        services.AddSingleton<IImageStore, FileImageStore>();
        services.AddSingleton<ISecretStore, EnvironmentSecretStore>();
        services.AddSingleton<IQuestionTextMerger, QuestionTextMerger>();
        services.AddSingleton<ICaptureRegionProvider, CaptureRegionProvider>();
        services.AddSingleton<IImageCropper, SkiaImageCropper>();

        services.AddSingleton<ICameraService>(_ =>
            CameraServiceFactory.Create(cameraMode, cameraSource));

        services.AddHttpClient<IOcrService, RapidOcrHttpService>(client =>
        {
            client.BaseAddress = new Uri(ocrBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddSingleton<DeepSeekApiCallTracker>();
        services.AddHttpClient<DeepSeekTextAnswerProvider>();
        services.AddSingleton<VolcanoArkStubProvider>();
        services.AddSingleton<RoutingTextAnswerProvider>();
        services.AddSingleton<ITextAnswerProvider>(sp => sp.GetRequiredService<RoutingTextAnswerProvider>());

        services.AddHttpClient<VolcanoArkVisionAnswerProvider>();
        services.AddSingleton<IVisionAnswerProvider>(sp => sp.GetRequiredService<VolcanoArkVisionAnswerProvider>());

        services.AddSingleton<IAppSessionContext, AppSessionContext>();

        services.AddSingleton<ICaptureAndOcrUseCase, CaptureAndOcrUseCase>();
        services.AddSingleton<IUndoLastCaptureUseCase, UndoLastCaptureUseCase>();
        services.AddSingleton<IClearSessionUseCase, ClearSessionUseCase>();
        services.AddSingleton<ISolveTextUseCase, SolveTextUseCase>();
        services.AddSingleton<ISolveVisionUseCase, SolveVisionUseCase>();
        services.AddSingleton<ILoadSessionUseCase, LoadSessionUseCase>();
        services.AddSingleton<ISaveAndTestApiUseCase, SaveAndTestApiUseCase>();
        services.AddSingleton<ISendFollowUpUseCase, SendFollowUpUseCase>();
        services.AddSingleton<IUpdateQuestionTextUseCase, UpdateQuestionTextUseCase>();
        services.AddSingleton<IDeleteSessionUseCase, DeleteSessionUseCase>();
        services.AddSingleton<IClearAllHistoryUseCase, ClearAllHistoryUseCase>();

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        var initializer = services.GetRequiredService<SqliteDatabaseInitializer>();
        await initializer.InitializeAsync(ct);

        var sessionContext = services.GetRequiredService<IAppSessionContext>();
        await sessionContext.InitializeAsync(ct);
    }
}
