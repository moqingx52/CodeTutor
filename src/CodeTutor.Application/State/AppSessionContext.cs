using CodeTutor.Application.Abstractions;
using CodeTutor.Domain.Sessions;

namespace CodeTutor.Application.State;

public sealed class AppSessionContext : IAppSessionContext
{
    private readonly ISessionRepository _repository;

    public AppSessionContext(ISessionRepository repository) => _repository = repository;

    public StudySession Current { get; private set; } = null!;

    public event EventHandler? SessionChanged;

    public async Task InitializeAsync(CancellationToken ct)
    {
        Current = await _repository.GetActiveAsync(ct) ?? await _repository.CreateAsync(ct);
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplySession(StudySession session)
    {
        Current = session;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task PersistCurrentAsync(CancellationToken ct) =>
        _repository.SaveAsync(Current, ct);
}
