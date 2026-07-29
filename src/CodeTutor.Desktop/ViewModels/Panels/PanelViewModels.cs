using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodeTutor.Application.Abstractions;
using CodeTutor.Application.Ai;

namespace CodeTutor.Desktop.ViewModels.Panels;

public sealed class CameraListItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required VideoMode SelectedMode { get; init; }
    public required string ModeDescription { get; init; }

    public override string ToString() => Name;
}

public partial class CaptureThumbnailItem : ObservableObject, IDisposable
{
    public required int Sequence { get; init; }
    public required string ThumbnailPath { get; init; }

    [ObservableProperty]
    private object? _image;

    public void Dispose()
    {
        if (Image is Avalonia.Media.Imaging.Bitmap bitmap)
            bitmap.Dispose();
        Image = null;
    }
}

public partial class CameraPanelViewModel : ObservableObject
{
    public ObservableCollection<CameraListItem> Cameras { get; } = [];
    public ObservableCollection<CaptureThumbnailItem> Thumbnails { get; } = [];

    [ObservableProperty]
    private CameraListItem? _selectedCamera;

    [ObservableProperty]
    private object? _previewBitmap;

    [ObservableProperty]
    private string _cameraStatus = "未连接";

    [ObservableProperty]
    private bool _showPreviewPlaceholder = true;

    [ObservableProperty]
    private bool _hasThumbnails;

    [ObservableProperty]
    private NormalizedRectangle? _captureRegion;

    [ObservableProperty]
    private bool _hasCaptureRegion;

    public IRelayCommand? RefreshCameraCommand { get; set; }
    public IRelayCommand? ClearCaptureRegionCommand { get; set; }

    partial void OnCaptureRegionChanged(NormalizedRectangle? value) =>
        HasCaptureRegion = value is not null;
}

public partial class QuestionPanelViewModel : ObservableObject
{
    [ObservableProperty]
    private string _workingQuestionText = string.Empty;

    [ObservableProperty]
    private string _statsText = "已截取 0 张";

    [ObservableProperty]
    private string _mergeWarningText = string.Empty;

    [ObservableProperty]
    private bool _hasMergeWarning;

    [ObservableProperty]
    private bool _canCapture = true;

    [ObservableProperty]
    private bool _canUndo;

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
    private string _followUpText = string.Empty;

    [ObservableProperty]
    private bool _hasFollowUp;

    [ObservableProperty]
    private string _codeText = string.Empty;

    [ObservableProperty]
    private bool _isProgramming;

    [ObservableProperty]
    private bool _canSolveText;

    [ObservableProperty]
    private bool _canSolveImages;

    [ObservableProperty]
    private string _imageSolveToolTip = "DeepSeek 不支持图片识别，请切换到火山方舟";

    public IRelayCommand? SolveTextCommand { get; set; }
    public IRelayCommand? SolveImagesCommand { get; set; }
    public IRelayCommand? CopyCodeCommand { get; set; }
}

public partial class FeedbackPanelViewModel : ObservableObject
{
    public ObservableCollection<string> Messages { get; } = new();

    [ObservableProperty]
    private string _userMessage = string.Empty;

    [ObservableProperty]
    private bool _canSendFollowUp;

    public IRelayCommand? SendFollowUpCommand { get; set; }
}

public partial class HistoryViewModel : ObservableObject
{
    public IRelayCommand? ShowHistoryCommand { get; set; }
}

public sealed class AiProviderItem
{
    public required AiProviderKind Kind { get; init; }
    public required string DisplayName { get; init; }

    public override string ToString() => DisplayName;
}

public partial class SettingsBarViewModel : ObservableObject
{
    public ObservableCollection<AiProviderItem> Providers { get; } =
    [
        new() { Kind = AiProviderKind.DeepSeek, DisplayName = "DeepSeek" },
        new() { Kind = AiProviderKind.VolcanoArk, DisplayName = "火山方舟" }
    ];

    [ObservableProperty]
    private AiProviderItem? _selectedProvider;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _modelDisplay = "deepseek-v4-pro";

    [ObservableProperty]
    private bool _isModelReadOnly = true;

    [ObservableProperty]
    private bool _isBalanceVisible = true;

    [ObservableProperty]
    private string _testStatus = string.Empty;

    [ObservableProperty]
    private string _balanceText = "余额：—";

    public IRelayCommand? SaveAndTestCommand { get; set; }

    public SettingsBarViewModel() => SelectedProvider = Providers[0];
}
