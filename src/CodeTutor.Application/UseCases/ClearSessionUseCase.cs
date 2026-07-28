using CodeTutor.Application.Abstractions;
using CodeTutor.Application.State;
using CodeTutor.Domain.Sessions;

namespace CodeTutor.Application.UseCases;

public sealed class ClearSessionUseCase : IClearSessionUseCase
{
    private readonly IAppSessionContext _session;
    private readonly ISessionRepository _repository;

    public ClearSessionUseCase(IAppSessionContext session, ISessionRepository repository)
    {
        _session = session;
        _repository = repository;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var current = _session.Current;
        if (current.Captures.Count > 0 || !string.IsNullOrWhiteSpace(current.WorkingQuestionText))
        {
            var archived = current with
            {
                Status = SessionStatus.Archived,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await _repository.SaveAsync(archived, ct);
        }

        var fresh = await _repository.CreateAsync(ct);
        _session.ApplySession(fresh);
    }
}
