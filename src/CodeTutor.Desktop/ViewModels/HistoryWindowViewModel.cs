using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodeTutor.Application.Abstractions;
using CodeTutor.Application.UseCases;
using CodeTutor.Domain.Sessions;

namespace CodeTutor.Desktop.ViewModels;

public partial class HistoryWindowViewModel : ObservableObject
{
    private readonly ISessionRepository _repository;
    private readonly ILoadSessionUseCase _loadSession;
    private readonly IDeleteSessionUseCase _deleteSession;
    private readonly IClearAllHistoryUseCase _clearAllHistory;

    public ObservableCollection<HistorySessionItem> Sessions { get; } = [];

    [ObservableProperty]
    private HistorySessionItem? _selectedSession;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _confirmingClearAll;

    public bool DialogResult { get; private set; }

    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
    public IAsyncRelayCommand ClearAllCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand CloseCommand { get; }

    public HistoryWindowViewModel(
        ISessionRepository repository,
        ILoadSessionUseCase loadSession,
        IDeleteSessionUseCase deleteSession,
        IClearAllHistoryUseCase clearAllHistory)
    {
        _repository = repository;
        _loadSession = loadSession;
        _deleteSession = deleteSession;
        _clearAllHistory = clearAllHistory;

        LoadCommand = new AsyncRelayCommand(LoadSelectedAsync, () => SelectedSession is not null);
        DeleteCommand = new AsyncRelayCommand(DeleteSelectedAsync, () => SelectedSession is not null);
        ClearAllCommand = new AsyncRelayCommand(ClearAllAsync, () => Sessions.Count > 0);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        CloseCommand = new RelayCommand(() => { });

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectedSession))
            {
                LoadCommand.NotifyCanExecuteChanged();
                DeleteCommand.NotifyCanExecuteChanged();
            }
        };
    }

    public async Task RefreshAsync()
    {
        var recent = await _repository.GetRecentAsync(20, CancellationToken.None);
        Sessions.Clear();
        foreach (var item in recent)
        {
            Sessions.Add(new HistorySessionItem
            {
                Id = item.Id,
                Title = FormatTitle(item),
                PreviewText = string.IsNullOrWhiteSpace(item.PreviewText) ? "（无题干）" : item.PreviewText,
                CaptureCount = item.CaptureCount,
                UpdatedAt = item.UpdatedAt
            });
        }

        SelectedSession = Sessions.FirstOrDefault();
        StatusText = Sessions.Count > 0 ? $"共 {Sessions.Count} 条历史记录" : "暂无历史记录";
        ClearAllCommand.NotifyCanExecuteChanged();
    }

    public async Task LoadSelectedAsync()
    {
        if (SelectedSession is null)
            return;

        await _loadSession.ExecuteAsync(SelectedSession.Id, CancellationToken.None);
        DialogResult = true;
        StatusText = "已加载所选会话";
    }

    public async Task DeleteSelectedAsync()
    {
        if (SelectedSession is null)
            return;

        var id = SelectedSession.Id;
        await _deleteSession.ExecuteAsync(id, CancellationToken.None);
        await RefreshAsync();
        StatusText = "已删除所选会话";
    }

    public async Task ClearAllAsync()
    {
        if (!ConfirmingClearAll)
        {
            ConfirmingClearAll = true;
            StatusText = "⚠ 再次点击「清空全部历史」以确认删除";
            return;
        }

        ConfirmingClearAll = false;
        await _clearAllHistory.ExecuteAsync(CancellationToken.None);
        await RefreshAsync();
        DialogResult = true;
        StatusText = "已清空全部历史";
    }

    private static string FormatTitle(SessionSummary item)
    {
        var local = item.UpdatedAt.ToLocalTime();
        return $"{local:yyyy-MM-dd HH:mm} · {item.CaptureCount} 张截图";
    }
}

public sealed class HistorySessionItem
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string PreviewText { get; init; }
    public required int CaptureCount { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }

    public override string ToString() => Title;
}
