using Microsoft.Data.Sqlite;
using CodeTutor.Application.Abstractions;
using CodeTutor.Domain.Sessions;

namespace CodeTutor.Infrastructure.Persistence;

public sealed class SqliteCheckpointStore : ICheckpointStore
{
    private readonly string _connectionString;
    private readonly int _maxCount;

    public SqliteCheckpointStore(SqliteDatabaseInitializer initializer, int maxCount = 20)
    {
        _connectionString = initializer.ConnectionString;
        _maxCount = maxCount;
    }

    public async Task PushAsync(SessionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        try
        {
            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO checkpoints (id, session_id, capture_count, state_json, created_at)
                    VALUES ($id, $sessionId, $count, $state, $created);
                    """;
                insert.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
                insert.Parameters.AddWithValue("$sessionId", checkpoint.SessionId.ToString("N"));
                insert.Parameters.AddWithValue("$count", checkpoint.CaptureCount);
                insert.Parameters.AddWithValue("$state", SessionJsonSerializer.SerializeCheckpointState(checkpoint));
                insert.Parameters.AddWithValue("$created", checkpoint.CreatedAt.ToString("O"));
                await insert.ExecuteNonQueryAsync(ct);
            }

            await using (var trim = connection.CreateCommand())
            {
                trim.Transaction = transaction;
                trim.CommandText = """
                    DELETE FROM checkpoints
                    WHERE session_id = $sessionId
                      AND id NOT IN (
                          SELECT id FROM checkpoints
                          WHERE session_id = $sessionId
                          ORDER BY created_at DESC
                          LIMIT $max
                      );
                    """;
                trim.Parameters.AddWithValue("$sessionId", checkpoint.SessionId.ToString("N"));
                trim.Parameters.AddWithValue("$max", _maxCount);
                await trim.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<SessionCheckpoint?> PopAsync(Guid sessionId, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        try
        {
            string? checkpointId = null;
            int captureCount = 0;
            string? stateJson = null;
            DateTimeOffset createdAt = default;

            await using (var select = connection.CreateCommand())
            {
                select.Transaction = transaction;
                select.CommandText = """
                    SELECT id, capture_count, state_json, created_at
                    FROM checkpoints
                    WHERE session_id = $sessionId
                    ORDER BY created_at DESC
                    LIMIT 1;
                    """;
                select.Parameters.AddWithValue("$sessionId", sessionId.ToString("N"));

                await using var reader = await select.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                {
                    await transaction.RollbackAsync(ct);
                    return null;
                }

                checkpointId = reader.GetString(0);
                captureCount = reader.GetInt32(1);
                stateJson = reader.GetString(2);
                createdAt = DateTimeOffset.Parse(reader.GetString(3));
            }

            await using (var delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM checkpoints WHERE id = $id;";
                delete.Parameters.AddWithValue("$id", checkpointId);
                await delete.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);

            return SessionJsonSerializer.DeserializeCheckpoint(
                sessionId,
                captureCount,
                stateJson!,
                createdAt);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> HasAnyAsync(Guid sessionId, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM checkpoints WHERE session_id = $sessionId;";
        command.Parameters.AddWithValue("$sessionId", sessionId.ToString("N"));

        var count = (long)(await command.ExecuteScalarAsync(ct) ?? 0L);
        return count > 0;
    }
}
