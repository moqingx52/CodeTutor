using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodeTutor.Application.Abstractions;
using CodeTutor.Application.State;
using CodeTutor.Application.UseCases;
using CodeTutor.Domain.Ocr;
using CodeTutor.OcrWorkbench.ViewModels.Panels;

namespace CodeTutor.OcrWorkbench.ViewModels;

public partial class OcrWorkbenchViewModel : ObservableObject
{
    private static readonly TimeSpan SelectionPreviewInterval = TimeSpan.FromMilliseconds(33);

    private readonly ICameraService _camera;
    private readonly IAppSessionContext _session;
    private readonly ICaptureAndOcrUseCase _captureAndOcr;
    private readonly IUndoLastCaptureUseCase _undo;
    private readonly IClearSessionUseCase _clearSession;
    private readonly IUpdateQuestionTextUseCase _updateQuestionText;
    private readonly ICheckpointStore _checkpoints;
    private readonly ICaptureRegionProvider _captureRegionProvider;
    private readonly IImageCropper _imageCropper;

    private CancellationTokenSource? _previewCts;
    private CancellationTokenSource? _textEditDebounceCts;
    private CameraSelection? _currentSelection;
    private int _previewFrameCounter;
    private bool _syncingSession;
    private byte[]? _latestFrameData;
    private DateTime _lastSelectionPreviewUpdate = DateTime.MinValue;

    public CameraPanelViewModel CameraPanel { get; } = new();
    public QuestionPanelViewModel QuestionPanel { get; } = new();

    [ObservableProperty]
    private string _statusText = "就绪";

    public OcrWorkbenchViewModel(
        ICameraService camera,
        IAppSessionContext session,
        ICaptureAndOcrUseCase captureAndOcr,
        IUndoLastCaptureUseCase undo,
        IClearSessionUseCase clearSession,
        IUpdateQuestionTextUseCase updateQuestionText,
        ICheckpointStore checkpoints,
        ICaptureRegionProvider captureRegionProvider,
        IImageCropper imageCropper)
    {
        _camera = camera;
        _session = session;
        _captureAndOcr = captureAndOcr;
        _undo = undo;
        _clearSession = clearSession;
        _updateQuestionText = updateQuestionText;
        _checkpoints = checkpoints;
        _captureRegionProvider = captureRegionProvider;
        _imageCropper = imageCropper;

        _session.SessionChanged += (_, _) => SyncFromSession();

        CameraPanel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CameraPanelViewModel.CaptureRegion))
            {
                _captureRegionProvider.Region = CameraPanel.CaptureRegion;
                UpdateSelectionPreview();
            }
        };

        QuestionPanel.CaptureAndOcrCommand = new AsyncRelayCommand(CaptureAndOcrAsync);
        QuestionPanel.UndoCaptureCommand = new AsyncRelayCommand(UndoAsync);
        QuestionPanel.ClearSessionCommand = new AsyncRelayCommand(ClearSessionAsync);
        CameraPanel.RefreshCameraCommand = new AsyncRelayCommand(RefreshCamerasAsync);

        CameraPanel.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(CameraPanelViewModel.SelectedCamera) && CameraPanel.SelectedCamera is not null)
                await StartPreviewAsync();
        };

        QuestionPanel.PropertyChanged += async (_, e) =>
        {
            if (_syncingSession || e.PropertyName != nameof(QuestionPanelViewModel.WorkingQuestionText))
                return;

            _textEditDebounceCts?.Cancel();
            _textEditDebounceCts = new CancellationTokenSource();
            var token = _textEditDebounceCts.Token;
            try
            {
                await Task.Delay(600, token);
                await _updateQuestionText.ExecuteAsync(QuestionPanel.WorkingQuestionText, token);
            }
            catch (OperationCanceledException)
            {
                // 防抖取消。
            }
        };
    }

    public async Task InitializeAsync()
    {
        SyncFromSession();
        await RefreshCamerasAsync();
    }

    private async Task RefreshCamerasAsync()
    {
        try
        {
            var devices = await _camera.EnumerateAsync(CancellationToken.None);
            CameraPanel.Cameras.Clear();

            foreach (var device in devices)
            {
                var bestMode = device.Modes.OrderBy(m => m.Score).First();
                CameraPanel.Cameras.Add(new CameraListItem
                {
                    Id = device.Id,
                    Name = device.Name,
                    SelectedMode = bestMode,
                    ModeDescription = FormatModeDescription(bestMode)
                });
            }

            CameraPanel.SelectedCamera = CameraPanel.Cameras.FirstOrDefault();
            StatusText = devices.Count > 0
                ? $"已发现 {devices.Count} 个摄像头"
                : "未发现摄像头，请检查连接或改用模拟模式";
        }
        catch (Exception ex)
        {
            CameraPanel.CameraStatus = "枚举失败";
            StatusText = $"摄像头枚举失败：{ex.Message}";
        }
    }

    private async Task StartPreviewAsync()
    {
        if (CameraPanel.SelectedCamera is null)
            return;

        await StopPreviewAsync();

        var devices = await _camera.EnumerateAsync(CancellationToken.None);
        var device = devices.FirstOrDefault(d => d.Id == CameraPanel.SelectedCamera.Id);
        if (device is null)
        {
            CameraPanel.CameraStatus = "设备不可用";
            StatusText = "所选摄像头已断开，请刷新后重试";
            return;
        }

        var mode = CameraPanel.SelectedCamera.SelectedMode;
        _currentSelection = new CameraSelection(CameraPanel.SelectedCamera.Id, mode);
        _previewCts = new CancellationTokenSource();
        _previewFrameCounter = 0;

        try
        {
            await _camera.StartAsync(_currentSelection, OnPreviewFrameAsync, _previewCts.Token);
            CameraPanel.CameraStatus = $"预览中 · {CameraPanel.SelectedCamera.ModeDescription}";
            StatusText = "摄像头预览已启动";
        }
        catch (Exception ex)
        {
            CameraPanel.CameraStatus = "连接失败";
            StatusText = $"摄像头启动失败：{ex.Message}";
        }
    }

    private static string FormatModeDescription(VideoMode mode)
    {
        var format = mode.PixelFormat.ToUpperInvariant() switch
        {
            "MJPEG" or "JPEG" => "MJPEG",
            _ => mode.PixelFormat
        };
        return $"{mode.Width}×{mode.Height} · {mode.Fps} 帧/秒 · {format}";
    }

    private async ValueTask OnPreviewFrameAsync(CameraFrame frame)
    {
        _previewFrameCounter++;
        if (_previewFrameCounter % 2 != 0)
            return;

        try
        {
            _latestFrameData = frame.Data;
            await using var ms = new MemoryStream(frame.Data);
            var bitmap = new Bitmap(ms);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (CameraPanel.PreviewBitmap is Bitmap old)
                    old.Dispose();
                CameraPanel.PreviewBitmap = bitmap;
                CameraPanel.ShowPreviewPlaceholder = false;
                UpdateSelectionPreview(force: true);
            });
        }
        catch
        {
            // 忽略单帧解码失败。
        }
    }

    private void UpdateSelectionPreview(bool force = false)
    {
        if (!force)
        {
            var now = DateTime.UtcNow;
            if (now - _lastSelectionPreviewUpdate < SelectionPreviewInterval)
                return;
        }

        _lastSelectionPreviewUpdate = DateTime.UtcNow;

        if (_latestFrameData is null || CameraPanel.CaptureRegion is not { } region)
        {
            ClearSelectionPreview();
            return;
        }

        try
        {
            var cropped = _imageCropper.Crop(_latestFrameData, region);
            using var ms = new MemoryStream(cropped);
            var bitmap = new Bitmap(ms);

            if (CameraPanel.SelectionPreviewBitmap is Bitmap old)
                old.Dispose();

            CameraPanel.SelectionPreviewBitmap = bitmap;
            CameraPanel.HasSelectionPreview = true;
        }
        catch
        {
            ClearSelectionPreview();
        }
    }

    private void ClearSelectionPreview()
    {
        if (CameraPanel.SelectionPreviewBitmap is Bitmap old)
            old.Dispose();

        CameraPanel.SelectionPreviewBitmap = null;
        CameraPanel.HasSelectionPreview = false;
    }

    private async Task StopPreviewAsync()
    {
        if (_previewCts is null)
            return;

        await _previewCts.CancelAsync();
        await _camera.StopAsync(CancellationToken.None);
        _previewCts.Dispose();
        _previewCts = null;
    }

    private async Task CaptureAndOcrAsync()
    {
        QuestionPanel.CanCapture = false;
        StatusText = "正在截取并识别…";

        try
        {
            await _captureAndOcr.ExecuteAsync(CancellationToken.None);
            var last = _session.Current.Captures.LastOrDefault();
            if (last?.OcrStatus == Domain.Sessions.OcrStatus.Succeeded && last.Ocr is not null)
            {
                var mergeHint = FormatMergeHint(last.MergeDecision);
                StatusText = $"识别完成 · 置信度 {last.Ocr.MeanConfidence:P0} · 耗时 {last.Ocr.Elapsed.TotalMilliseconds:F0} 毫秒{mergeHint}";
            }
            else if (last?.OcrStatus == Domain.Sessions.OcrStatus.Failed)
            {
                StatusText = $"截图已保存，但 OCR 失败：{last.ErrorMessage}";
            }
            else
            {
                StatusText = "截取并识别完成";
            }
        }
        catch (DuplicateCaptureException ex)
        {
            StatusText = ex.Message;
        }
        catch (Exception ex)
        {
            StatusText = $"截取失败：{ex.Message}";
        }
        finally
        {
            QuestionPanel.CanCapture = true;
            await UpdateUndoStateAsync();
        }
    }

    private async Task UndoAsync()
    {
        try
        {
            await _undo.ExecuteAsync(CancellationToken.None);
            StatusText = "已撤销上一次截屏";
        }
        catch (Exception ex)
        {
            StatusText = $"撤销失败：{ex.Message}";
        }

        await UpdateUndoStateAsync();
    }

    private async Task ClearSessionAsync()
    {
        try
        {
            await _clearSession.ExecuteAsync(CancellationToken.None);
            StatusText = "已清空当前会话";
            SyncFromSession();
        }
        catch (Exception ex)
        {
            StatusText = $"清空失败：{ex.Message}";
        }

        await UpdateUndoStateAsync();
    }

    private void SyncFromSession()
    {
        _syncingSession = true;
        try
        {
            var session = _session.Current;
            QuestionPanel.WorkingQuestionText = session.WorkingQuestionText;

            var avgConfidence = session.Captures
                .Where(c => c.Ocr is not null)
                .Select(c => c.Ocr!.MeanConfidence)
                .DefaultIfEmpty(0)
                .Average();

            var stats = session.Captures.Count > 0
                ? $"已截取 {session.Captures.Count} 张 / 平均置信度 {avgConfidence:P0}"
                : "已截取 0 张";

            if (session.IsQuestionTextManuallyEdited)
                stats += " / 已手动编辑";

            QuestionPanel.StatsText = stats;

            var lastMerge = session.Captures.LastOrDefault()?.MergeDecision;
            if (lastMerge?.Strategy == MergeStrategy.NoOverlapWithWarning)
            {
                QuestionPanel.MergeWarningText = "未检测到重叠，请检查截图";
                QuestionPanel.HasMergeWarning = true;
            }
            else
            {
                QuestionPanel.MergeWarningText = string.Empty;
                QuestionPanel.HasMergeWarning = false;
            }

            RefreshThumbnails(session);
        }
        finally
        {
            _syncingSession = false;
        }

        _ = UpdateUndoStateAsync();
    }

    private void RefreshThumbnails(Domain.Sessions.StudySession session)
    {
        foreach (var item in CameraPanel.Thumbnails)
            item.Dispose();

        CameraPanel.Thumbnails.Clear();
        CameraPanel.HasThumbnails = session.Captures.Count > 0;

        foreach (var capture in session.Captures.OrderBy(c => c.Sequence))
        {
            var item = new CaptureThumbnailItem
            {
                Sequence = capture.Sequence,
                ThumbnailPath = capture.ThumbnailPath
            };

            if (File.Exists(capture.ThumbnailPath))
            {
                try
                {
                    item.Image = new Bitmap(capture.ThumbnailPath);
                }
                catch
                {
                    // 缩略图损坏时忽略。
                }
            }

            CameraPanel.Thumbnails.Add(item);
        }
    }

    private async Task UpdateUndoStateAsync()
    {
        QuestionPanel.CanUndo = await _checkpoints.HasAnyAsync(_session.Current.Id, CancellationToken.None);
    }

    private static string FormatMergeHint(MergeDecision? decision) => decision?.Strategy switch
    {
        MergeStrategy.First => string.Empty,
        MergeStrategy.LineOverlap => $" · 重叠 {decision.OverlapLineCount} 行",
        MergeStrategy.CharacterOverlap => $" · 字符重叠 {decision.OverlapCharCount}",
        MergeStrategy.NoOverlapWithWarning => " · 未检测到重叠，请检查截图",
        MergeStrategy.DuplicateSkipped => " · 重复截图",
        _ => string.Empty
    };
}
