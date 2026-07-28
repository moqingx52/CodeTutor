using Microsoft.Data.Sqlite;
using CodeTutor.Application.Abstractions;
using CodeTutor.Domain.Sessions;

namespace CodeTutor.Infrastructure.Persistence;

public sealed class SqliteSessionRepository : ISessionRepository
{
    private readonly string _connectionString;

    public SqliteSessionRepository(SqliteDatabaseInitializer initializer) =>
        _connectionString = initializer.ConnectionString;

    public async Task<StudySession> CreateAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var session = new StudySession(
            Guid.NewGuid(),
            now,
            now,
            SessionStatus.Active,
            string.Empty,
            false,
            [],
            null,
            []);

        await SaveAsync(session, ct);
        return session;
    }

    public async Task SaveAsync(StudySession session, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        try
        {
            await UpsertSessionAsync(connection, transaction, session, ct);
            await DeleteCapturesAsync(connection, transaction, session.Id, ct);
            await InsertCapturesAsync(connection, transaction, session, ct);
            await DeleteChatMessagesAsync(connection, transaction, session.Id, ct);
            await InsertChatMessagesAsync(connection, transaction, session, ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<StudySession?> GetAsync(Guid id, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var sessionCmd = connection.CreateCommand();
        sessionCmd.CommandText = """
            SELECT id, created_at, updated_at, status, working_question_text, is_manually_edited, solution_json
            FROM sessions WHERE id = $id;
            """;
        sessionCmd.Parameters.AddWithValue("$id", id.ToString("N"));

        await using var reader = await sessionCmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var sessionId = Guid.Parse(reader.GetString(0));
        var createdAt = DateTimeOffset.Parse(reader.GetString(1));
        var updatedAt = DateTimeOffset.Parse(reader.GetString(2));
        var status = (SessionStatus)reader.GetInt32(3);
        var workingText = reader.GetString(4);
        var manuallyEdited = reader.GetInt32(5) != 0;
        var solution = SessionJsonSerializer.DeserializeSolution(reader.IsDBNull(6) ? null : reader.GetString(6));

        await reader.CloseAsync();

        var captures = await LoadCapturesAsync(connection, sessionId, ct);
        var chatMessages = await LoadChatMessagesAsync(connection, sessionId, ct);

        return new StudySession(
            sessionId,
            createdAt,
            updatedAt,
            status,
            workingText,
            manuallyEdited,
            captures,
            solution,
            chatMessages);
    }

    public async Task<IReadOnlyList<SessionSummary>> GetRecentAsync(int limit, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.id, s.created_at, s.updated_at,
                   (SELECT COUNT(*) FROM captures c WHERE c.session_id = s.id) AS capture_count,
                   s.working_question_text
            FROM sessions s
            ORDER BY s.updated_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var results = new List<SessionSummary>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var preview = reader.GetString(4);
            if (preview.Length > 80)
                preview = preview[..80] + "…";

            results.Add(new SessionSummary(
                Guid.Parse(reader.GetString(0)),
                DateTimeOffset.Parse(reader.GetString(1)),
                DateTimeOffset.Parse(reader.GetString(2)),
                reader.GetInt32(3),
                preview));
        }

        return results;
    }

    public async Task<StudySession?> GetActiveAsync(CancellationToken ct)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id FROM sessions
            WHERE status = $active
            ORDER BY updated_at DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$active", (int)SessionStatus.Active);

        var result = await command.ExecuteScalarAsync(ct);
        if (result is not string id)
            return null;

        return await GetAsync(Guid.Parse(id), ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM sessions WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("N"));
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAllAsync(CancellationToken ct)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM sessions;";
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpsertSessionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        StudySession session,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO sessions (id, created_at, updated_at, status, working_question_text, is_manually_edited, solution_json)
            VALUES ($id, $created, $updated, $status, $text, $edited, $solution)
            ON CONFLICT(id) DO UPDATE SET
                updated_at = excluded.updated_at,
                status = excluded.status,
                working_question_text = excluded.working_question_text,
                is_manually_edited = excluded.is_manually_edited,
                solution_json = excluded.solution_json;
            """;
        command.Parameters.AddWithValue("$id", session.Id.ToString("N"));
        command.Parameters.AddWithValue("$created", session.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated", session.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$status", (int)session.Status);
        command.Parameters.AddWithValue("$text", session.WorkingQuestionText);
        command.Parameters.AddWithValue("$edited", session.IsQuestionTextManuallyEdited ? 1 : 0);
        command.Parameters.AddWithValue("$solution", SessionJsonSerializer.SerializeSolution(session.Solution) ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task DeleteCapturesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid sessionId,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM captures WHERE session_id = $id;";
        command.Parameters.AddWithValue("$id", sessionId.ToString("N"));
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertCapturesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        StudySession session,
        CancellationToken ct)
    {
        foreach (var capture in session.Captures)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO captures (
                    id, session_id, sequence, captured_at, image_path, thumbnail_path,
                    perceptual_hash, ocr_status, ocr_json, merge_json, error_message)
                VALUES ($id, $sessionId, $seq, $captured, $image, $thumb, $hash, $status, $ocr, $merge, $error);
                """;
            command.Parameters.AddWithValue("$id", capture.Id.ToString("N"));
            command.Parameters.AddWithValue("$sessionId", session.Id.ToString("N"));
            command.Parameters.AddWithValue("$seq", capture.Sequence);
            command.Parameters.AddWithValue("$captured", capture.CapturedAt.ToString("O"));
            command.Parameters.AddWithValue("$image", capture.ImagePath);
            command.Parameters.AddWithValue("$thumb", capture.ThumbnailPath);
            command.Parameters.AddWithValue("$hash", capture.PerceptualHash);
            command.Parameters.AddWithValue("$status", (int)capture.OcrStatus);
            command.Parameters.AddWithValue("$ocr", SessionJsonSerializer.SerializeOcr(capture.Ocr) ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$merge", SessionJsonSerializer.SerializeMerge(capture.MergeDecision) ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$error", capture.ErrorMessage ?? (object)DBNull.Value);
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task DeleteChatMessagesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid sessionId,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM chat_messages WHERE session_id = $id;";
        command.Parameters.AddWithValue("$id", sessionId.ToString("N"));
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertChatMessagesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        StudySession session,
        CancellationToken ct)
    {
        var sequence = 0;
        foreach (var message in session.ChatMessages)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO chat_messages (id, session_id, sequence, role, type, content, created_at)
                VALUES ($id, $sessionId, $seq, 0, $type, $content, $created);
                """;
            command.Parameters.AddWithValue("$id", message.Id.ToString("N"));
            command.Parameters.AddWithValue("$sessionId", session.Id.ToString("N"));
            command.Parameters.AddWithValue("$seq", sequence++);
            command.Parameters.AddWithValue("$type", (int)message.Type);
            command.Parameters.AddWithValue("$content", message.Content);
            command.Parameters.AddWithValue("$created", message.CreatedAt.ToString("O"));
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task<List<CaptureRecord>> LoadCapturesAsync(
        SqliteConnection connection,
        Guid sessionId,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, session_id, sequence, captured_at, image_path, thumbnail_path,
                   perceptual_hash, ocr_status, ocr_json, merge_json, error_message
            FROM captures WHERE session_id = $id ORDER BY sequence;
            """;
        command.Parameters.AddWithValue("$id", sessionId.ToString("N"));

        var captures = new List<CaptureRecord>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            captures.Add(new CaptureRecord(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                reader.GetInt32(2),
                DateTimeOffset.Parse(reader.GetString(3)),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                (OcrStatus)reader.GetInt32(7),
                SessionJsonSerializer.DeserializeOcr(reader.IsDBNull(8) ? null : reader.GetString(8)),
                SessionJsonSerializer.DeserializeMerge(reader.IsDBNull(9) ? null : reader.GetString(9)),
                reader.IsDBNull(10) ? null : reader.GetString(10)));
        }

        return captures;
    }

    private static async Task<List<Domain.Common.ChatMessage>> LoadChatMessagesAsync(
        SqliteConnection connection,
        Guid sessionId,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, type, content, created_at
            FROM chat_messages WHERE session_id = $id ORDER BY sequence;
            """;
        command.Parameters.AddWithValue("$id", sessionId.ToString("N"));

        var messages = new List<Domain.Common.ChatMessage>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            messages.Add(new Domain.Common.ChatMessage(
                Guid.Parse(reader.GetString(0)),
                (Domain.Common.FeedbackMessageType)reader.GetInt32(1),
                reader.GetString(2),
                DateTimeOffset.Parse(reader.GetString(3))));
        }

        return messages;
    }
}
