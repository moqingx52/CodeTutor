using CodeTutor.Domain.Sessions;
using CodeTutor.Infrastructure.Persistence;
using FluentAssertions;

namespace CodeTutor.Infrastructure.Tests;

public class SqliteCheckpointStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteDatabaseInitializer _initializer;
    private readonly SqliteCheckpointStore _store;
    private readonly SqliteSessionRepository _repository;

    public SqliteCheckpointStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"codetutor-ckpt-{Guid.NewGuid():N}.db");
        _initializer = new SqliteDatabaseInitializer(_dbPath);
        _initializer.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        _store = new SqliteCheckpointStore(_initializer, maxCount: 3);
        _repository = new SqliteSessionRepository(_initializer);
    }

    [Fact]
    public async Task PushAndPop_RestoresCheckpointState()
    {
        var session = await _repository.CreateAsync(CancellationToken.None);
        var checkpoint = new SessionCheckpoint(
            session.Id,
            2,
            "已有题干",
            false,
            null,
            [],
            DateTimeOffset.UtcNow);

        await _store.PushAsync(checkpoint, CancellationToken.None);
        (await _store.HasAnyAsync(session.Id, CancellationToken.None)).Should().BeTrue();

        var popped = await _store.PopAsync(session.Id, CancellationToken.None);
        popped.Should().NotBeNull();
        popped!.WorkingQuestionText.Should().Be("已有题干");
        popped.CaptureCount.Should().Be(2);

        (await _store.HasAnyAsync(session.Id, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task Push_TrimsToMaxCount()
    {
        var session = await _repository.CreateAsync(CancellationToken.None);

        for (var i = 0; i < 5; i++)
        {
            await _store.PushAsync(new SessionCheckpoint(
                session.Id,
                i,
                $"text-{i}",
                false,
                null,
                [],
                DateTimeOffset.UtcNow.AddSeconds(i)), CancellationToken.None);
        }

        var count = 0;
        while (await _store.HasAnyAsync(session.Id, CancellationToken.None))
        {
            await _store.PopAsync(session.Id, CancellationToken.None);
            count++;
        }

        count.Should().Be(3);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
