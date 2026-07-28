using Microsoft.Data.Sqlite;

namespace CodeTutor.Infrastructure.Persistence;

public sealed class SqliteDatabaseInitializer
{
    private readonly string _connectionString;

    public SqliteDatabaseInitializer(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public string ConnectionString => _connectionString;

    public async Task InitializeAsync(CancellationToken ct)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL;";
        await pragma.ExecuteNonQueryAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT PRIMARY KEY,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                status INTEGER NOT NULL,
                working_question_text TEXT NOT NULL DEFAULT '',
                is_manually_edited INTEGER NOT NULL DEFAULT 0,
                solution_json TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS captures (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                sequence INTEGER NOT NULL,
                captured_at TEXT NOT NULL,
                image_path TEXT NOT NULL,
                thumbnail_path TEXT NOT NULL,
                perceptual_hash TEXT NOT NULL,
                ocr_status INTEGER NOT NULL,
                ocr_json TEXT NULL,
                merge_json TEXT NULL,
                error_message TEXT NULL,
                FOREIGN KEY (session_id) REFERENCES sessions(id) ON DELETE CASCADE,
                UNIQUE(session_id, sequence)
            );

            CREATE TABLE IF NOT EXISTS chat_messages (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                sequence INTEGER NOT NULL,
                role INTEGER NOT NULL DEFAULT 0,
                type INTEGER NOT NULL,
                content TEXT NOT NULL,
                created_at TEXT NOT NULL,
                FOREIGN KEY (session_id) REFERENCES sessions(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS checkpoints (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                capture_count INTEGER NOT NULL,
                state_json TEXT NOT NULL,
                created_at TEXT NOT NULL,
                FOREIGN KEY (session_id) REFERENCES sessions(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_sessions_updated_at ON sessions(updated_at DESC);
            CREATE INDEX IF NOT EXISTS idx_captures_session_id ON captures(session_id);
            CREATE INDEX IF NOT EXISTS idx_checkpoints_session_id ON checkpoints(session_id, created_at DESC);
            """;

        await command.ExecuteNonQueryAsync(ct);
    }
}
