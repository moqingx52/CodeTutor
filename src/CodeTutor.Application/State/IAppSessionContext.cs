using CodeTutor.Domain.Sessions;

namespace CodeTutor.Application.State;

public interface IAppSessionContext
{
    StudySession Current { get; }
    event EventHandler? SessionChanged;
    Task InitializeAsync(CancellationToken ct);
    void ApplySession(StudySession session);
    Task PersistCurrentAsync(CancellationToken ct);
}
