namespace CodeTutor.Infrastructure.Ai;

public sealed class DeepSeekApiCallTracker
{
    private const int RefreshInterval = 5;
    private int _count;
    private readonly object _lock = new();

    public event EventHandler? BalanceRefreshNeeded;

    public void RecordSuccessfulCall()
    {
        lock (_lock)
        {
            _count++;
            if (_count % RefreshInterval == 0)
                BalanceRefreshNeeded?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Reset()
    {
        lock (_lock)
            _count = 0;
    }

    public int CurrentCount
    {
        get
        {
            lock (_lock)
                return _count;
        }
    }
}
