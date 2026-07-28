using CodeTutor.Application.Abstractions;
using CodeTutor.Application.State;
using CodeTutor.Domain.Sessions;

namespace CodeTutor.Application.UseCases;

public sealed class LoadSessionUseCase : ILoadSessionUseCase
{
    private readonly IAppSessionContext _session;
    private readonly ISessionRepository _repository;

    public LoadSessionUseCase(IAppSessionContext session, ISessionRepository repository)
    {
        _session = session;
        _repository = repository;
    }

    public async Task ExecuteAsync(Guid sessionId, CancellationToken ct)
    {
        var current = _session.Current;
        if (current.Id != sessionId && (current.Captures.Count > 0 || !string.IsNullOrWhiteSpace(current.WorkingQuestionText)))
        {
            await _repository.SaveAsync(current with
            {
                Status = SessionStatus.Archived,
                UpdatedAt = DateTimeOffset.UtcNow
            }, ct);
        }

        var loaded = await _repository.GetAsync(sessionId, ct)
                     ?? throw new InvalidOperationException($"会话 {sessionId} 不存在。");

        var active = loaded with
        {
            Status = SessionStatus.Active,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _session.ApplySession(active);
        await _session.PersistCurrentAsync(ct);
    }
}
