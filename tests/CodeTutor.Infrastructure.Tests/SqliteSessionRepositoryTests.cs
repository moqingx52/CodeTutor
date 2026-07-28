using CodeTutor.Domain.Ocr;
using CodeTutor.Domain.Sessions;
using CodeTutor.Infrastructure.Persistence;
using FluentAssertions;

namespace CodeTutor.Infrastructure.Tests;

public class SqliteSessionRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteDatabaseInitializer _initializer;
    private readonly SqliteSessionRepository _repository;

    public SqliteSessionRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"codetutor-test-{Guid.NewGuid():N}.db");
        _initializer = new SqliteDatabaseInitializer(_dbPath);
        _initializer.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        _repository = new SqliteSessionRepository(_initializer);
    }

    [Fact]
    public async Task CreateAndGet_RoundTripsSession()
    {
        var created = await _repository.CreateAsync(CancellationToken.None);
        var loaded = await _repository.GetAsync(created.Id, CancellationToken.None);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(created.Id);
        loaded.Status.Should().Be(SessionStatus.Active);
    }

    [Fact]
    public async Task Save_PersistsCapturesAndQuestionText()
    {
        var session = await _repository.CreateAsync(CancellationToken.None);
        var capture = new CaptureRecord(
            Guid.NewGuid(),
            session.Id,
            1,
            DateTimeOffset.UtcNow,
            "/tmp/capture_001.png",
            "/tmp/capture_001_thumb.jpg",
            "abc123",
            OcrStatus.Succeeded,
            new OcrResult("题目文本", 0.95, TimeSpan.FromMilliseconds(100), []),
            new MergeDecision(MergeStrategy.First, 0, 0, 1.0, true),
            null);

        var updated = session with
        {
            WorkingQuestionText = "题目文本",
            Captures = [capture]
        };

        await _repository.SaveAsync(updated, CancellationToken.None);
        var loaded = await _repository.GetAsync(session.Id, CancellationToken.None);

        loaded!.WorkingQuestionText.Should().Be("题目文本");
        loaded.Captures.Should().HaveCount(1);
        loaded.Captures[0].Ocr!.FullText.Should().Be("题目文本");
    }

    [Fact]
    public async Task GetActive_ReturnsLatestActiveSession()
    {
        var first = await _repository.CreateAsync(CancellationToken.None);
        var second = await _repository.CreateAsync(CancellationToken.None);

        var archived = first with { Status = SessionStatus.Archived };
        await _repository.SaveAsync(archived, CancellationToken.None);
        await _repository.SaveAsync(second, CancellationToken.None);

        var active = await _repository.GetActiveAsync(CancellationToken.None);
        active!.Id.Should().Be(second.Id);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
