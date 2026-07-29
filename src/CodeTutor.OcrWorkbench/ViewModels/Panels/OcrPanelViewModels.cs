using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodeTutor.Application.Abstractions;

namespace CodeTutor.OcrWorkbench.ViewModels.Panels;

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
    private object? _selectionPreviewBitmap;

    [ObservableProperty]
    private string _cameraStatus = "未连接";

    [ObservableProperty]
    private bool _showPreviewPlaceholder = true;

    [ObservableProperty]
    private bool _hasThumbnails;

    [ObservableProperty]
    private bool _hasSelectionPreview;

    [ObservableProperty]
    private NormalizedRectangle? _captureRegion;

    public IRelayCommand? RefreshCameraCommand { get; set; }
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
