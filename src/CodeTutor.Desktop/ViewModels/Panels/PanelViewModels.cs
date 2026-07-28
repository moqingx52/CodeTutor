using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeTutor.Desktop.ViewModels.Panels;

public partial class CameraPanelViewModel : ObservableObject
{
    public ObservableCollection<string> Cameras { get; } = new() { "Mock Camera" };

    [ObservableProperty]
    private string? _selectedCamera = "Mock Camera";

    [ObservableProperty]
    private object? _previewBitmap;

    [ObservableProperty]
    private string _cameraStatus = "未连接";

    public IRelayCommand? RefreshCameraCommand { get; set; }
}

public partial class QuestionPanelViewModel : ObservableObject
{
    [ObservableProperty]
    private string _workingQuestionText = string.Empty;

    [ObservableProperty]
    private string _statsText = "已截取 0 张";

    public IRelayCommand? CaptureAndOcrCommand { get; set; }
    public IRelayCommand? UndoCaptureCommand { get; set; }
    public IRelayCommand? ClearSessionCommand { get; set; }
}

public partial class SolutionPanelViewModel : ObservableObject
{
    [ObservableProperty]
    private string _finalAnswerText = string.Empty;

    [ObservableProperty]
    private string _explanationText = string.Empty;

    [ObservableProperty]
    private string _codeText = string.Empty;

    [ObservableProperty]
    private bool _isProgramming;

    public IRelayCommand? SolveTextCommand { get; set; }
    public IRelayCommand? SolveImagesCommand { get; set; }
    public IRelayCommand? CopyCodeCommand { get; set; }
}

public partial class FeedbackPanelViewModel : ObservableObject
{
    public ObservableCollection<string> Messages { get; } = new();

    [ObservableProperty]
    private string _userMessage = string.Empty;

    public IRelayCommand? SendFollowUpCommand { get; set; }
}

public partial class HistoryViewModel : ObservableObject
{
    public IRelayCommand? ShowHistoryCommand { get; set; }
}

public partial class SettingsBarViewModel : ObservableObject
{
    [ObservableProperty]
    private string _baseUrl = "https://api.deepseek.com";

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _model = "deepseek-chat";

    [ObservableProperty]
    private string _testStatus = string.Empty;

    public IRelayCommand? SaveAndTestCommand { get; set; }
}
