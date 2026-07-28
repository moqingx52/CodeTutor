using System.Text.Json;
using System.Text.Json.Serialization;
using CodeTutor.Domain.Common;
using CodeTutor.Domain.Ocr;
using CodeTutor.Domain.Sessions;
using CodeTutor.Domain.Solutions;

namespace CodeTutor.Infrastructure.Persistence;

internal static class SessionJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string? SerializeSolution(SolutionResult? solution) =>
        solution is null ? null : JsonSerializer.Serialize(solution, Options);

    public static SolutionResult? DeserializeSolution(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<SolutionResult>(json, Options);

    public static string? SerializeOcr(OcrResult? ocr) =>
        ocr is null ? null : JsonSerializer.Serialize(ocr, Options);

    public static OcrResult? DeserializeOcr(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<OcrResult>(json, Options);

    public static string? SerializeMerge(MergeDecision? merge) =>
        merge is null ? null : JsonSerializer.Serialize(merge, Options);

    public static MergeDecision? DeserializeMerge(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<MergeDecision>(json, Options);

    public static string SerializeCheckpointState(SessionCheckpoint checkpoint) =>
        JsonSerializer.Serialize(new CheckpointStateDto
        {
            WorkingQuestionText = checkpoint.WorkingQuestionText,
            IsQuestionTextManuallyEdited = checkpoint.IsQuestionTextManuallyEdited,
            Solution = checkpoint.Solution,
            ChatMessages = checkpoint.ChatMessages.ToList()
        }, Options);

    public static SessionCheckpoint DeserializeCheckpoint(
        Guid sessionId,
        int captureCount,
        string stateJson,
        DateTimeOffset createdAt)
    {
        var dto = JsonSerializer.Deserialize<CheckpointStateDto>(stateJson, Options)
                  ?? throw new InvalidOperationException("Invalid checkpoint state JSON.");

        return new SessionCheckpoint(
            sessionId,
            captureCount,
            dto.WorkingQuestionText,
            dto.IsQuestionTextManuallyEdited,
            dto.Solution,
            dto.ChatMessages,
            createdAt);
    }

    public static string SerializeChatMessages(IReadOnlyList<ChatMessage> messages) =>
        JsonSerializer.Serialize(messages, Options);

    public static List<ChatMessage> DeserializeChatMessages(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<ChatMessage>>(json, Options) ?? [];
}

internal sealed class CheckpointStateDto
{
    public string WorkingQuestionText { get; set; } = string.Empty;
    public bool IsQuestionTextManuallyEdited { get; set; }
    public SolutionResult? Solution { get; set; }
    public List<ChatMessage> ChatMessages { get; set; } = [];
}
