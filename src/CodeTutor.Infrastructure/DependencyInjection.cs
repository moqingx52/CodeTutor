using Microsoft.Extensions.DependencyInjection;
using CodeTutor.Application.Abstractions;
using CodeTutor.Application.State;
using CodeTutor.Infrastructure.Ocr;

namespace CodeTutor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCodeTutorInfrastructure(this IServiceCollection services, string ocrBaseUrl)
    {
        services.AddSingleton<IQuestionTextMerger, QuestionTextMerger>();

        services.AddHttpClient<IOcrService, RapidOcrHttpService>(client =>
        {
            client.BaseAddress = new Uri(ocrBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // TODO (Agent): FlashCapCameraService, SqliteSessionRepository,
        // DeepSeekTextAnswerProvider, VisionAnswerProvider, ImageStore, SecretStore

        return services;
    }
}
