using CodeTutor.Application.Abstractions;
using CodeTutor.Application.State;
using CodeTutor.Domain.Sessions;

namespace CodeTutor.Application.UseCases;

public sealed class UpdateQuestionTextUseCase : IUpdateQuestionTextUseCase
{
    private readonly IAppSessionContext _session;

    public UpdateQuestionTextUseCase(IAppSessionContext session) => _session = session;

    public async Task ExecuteAsync(string text, CancellationToken ct)
    {
        var session = _session.Current;
        if (session.WorkingQuestionText == text && session.IsQuestionTextManuallyEdited)
            return;

        var updated = session with
        {
            WorkingQuestionText = text,
            IsQuestionTextManuallyEdited = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _session.ApplySession(updated);
        await _session.PersistCurrentAsync(ct);
    }
}

public sealed class DeleteSessionUseCase : IDeleteSessionUseCase
{
    private readonly IAppSessionContext _session;
    private readonly ISessionRepository _repository;

    public DeleteSessionUseCase(IAppSessionContext session, ISessionRepository repository)
    {
        _session = session;
        _repository = repository;
    }

    public async Task ExecuteAsync(Guid sessionId, CancellationToken ct)
    {
        if (_session.Current.Id == sessionId)
        {
            var fresh = await _repository.CreateAsync(ct);
            _session.ApplySession(fresh);
        }

        await _repository.DeleteAsync(sessionId, ct);
    }
}

public sealed class ClearAllHistoryUseCase : IClearAllHistoryUseCase
{
    private readonly IAppSessionContext _session;
    private readonly ISessionRepository _repository;

    public ClearAllHistoryUseCase(IAppSessionContext session, ISessionRepository repository)
    {
        _session = session;
        _repository = repository;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        await _repository.DeleteAllAsync(ct);
        var fresh = await _repository.CreateAsync(ct);
        _session.ApplySession(fresh);
    }
}
