using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodeTutor.Application.Abstractions;
using CodeTutor.Application.Ai;
using CodeTutor.Application.State;
using CodeTutor.Application.UseCases;
using CodeTutor.Desktop.ViewModels.Panels;
using CodeTutor.Desktop.Views;
using CodeTutor.Domain.Ocr;
using CodeTutor.Domain.Solutions;
using CodeTutor.Infrastructure.Ai;
using Microsoft.Extensions.DependencyInjection;

namespace CodeTutor.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ICameraService _camera;
    private readonly IAppSessionContext _session;
    private readonly ICaptureAndOcrUseCase _captureAndOcr;
    private readonly IUndoLastCaptureUseCase _undo;
    private readonly IClearSessionUseCase _clearSession;
    private readonly ISolveTextUseCase _solveText;
    private readonly ISolveVisionUseCase _solveVision;
    private readonly ISaveAndTestApiUseCase _saveAndTest;
    private readonly ISendFollowUpUseCase _sendFollowUp;
    private readonly IUpdateQuestionTextUseCase _updateQuestionText;
    private readonly ICheckpointStore _checkpoints;
    private readonly IVisionAnswerProvider _visionProvider;
    private readonly ISecretStore _secrets;
    private readonly ITextAnswerProvider _textProvider;
    private readonly RoutingTextAnswerProvider _routingProvider;
    private readonly ICaptureRegionProvider _captureRegionProvider;
    private readonly IServiceProvider _services;

    private CancellationTokenSource? _previewCts;
    private CancellationTokenSource? _textEditDebounceCts;
    private CameraSelection? _currentSelection;
    private int _previewFrameCounter;
    private bool _syncingSession;

    public CameraPanelViewModel CameraPanel { get; } = new();
    public QuestionPanelViewModel QuestionPanel { get; } = new();
    public SolutionPanelViewModel SolutionPanel { get; } = new();
    public FeedbackPanelViewModel FeedbackPanel { get; } = new();
    public HistoryViewModel History { get; } = new();
    public SettingsBarViewModel Settings { get; } = new();

    public Window? OwnerWindow { get; set; }

    [ObservableProperty]
    private string _statusText = "就绪";

    public MainWindowViewModel(
        ICameraService camera,
        IAppSessionContext session,
        ICaptureAndOcrUseCase captureAndOcr,
        IUndoLastCaptureUseCase undo,
        IClearSessionUseCase clearSession,
        ISolveTextUseCase solveText,
        ISolveVisionUseCase solveVision,
        ISaveAndTestApiUseCase saveAndTest,
        ISendFollowUpUseCase sendFollowUp,
        IUpdateQuestionTextUseCase updateQuestionText,
        ICheckpointStore checkpoints,
        IVisionAnswerProvider visionProvider,
        ISecretStore secrets,
        ITextAnswerProvider textProvider,
        RoutingTextAnswerProvider routingProvider,
        ICaptureRegionProvider captureRegionProvider,
        IServiceProvider services)
    {
        _camera = camera;
        _session = session;
        _captureAndOcr = captureAndOcr;
        _undo = undo;
        _clearSession = clearSession;
        _solveText = solveText;
        _solveVision = solveVision;
        _saveAndTest = saveAndTest;
        _sendFollowUp = sendFollowUp;
        _updateQuestionText = updateQuestionText;
        _checkpoints = checkpoints;
        _visionProvider = visionProvider;
        _secrets = secrets;
        _textProvider = textProvider;
        _routingProvider = routingProvider;
        _captureRegionProvider = captureRegionProvider;
        _services = services;

        _routingProvider.Tracker.BalanceRefreshNeeded += (_, _) =>
            _ = RefreshBalanceAsync();

        _session.SessionChanged += (_, _) => SyncFromSession();

        CameraPanel.ClearCaptureRegionCommand = new RelayCommand(() => CameraPanel.CaptureRegion = null);
        CameraPanel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CameraPanelViewModel.CaptureRegion))
                _captureRegionProvider.Region = CameraPanel.CaptureRegion;
        };

        QuestionPanel.CaptureAndOcrCommand = new AsyncRelayCommand(CaptureAndOcrAsync);
        QuestionPanel.UndoCaptureCommand = new AsyncRelayCommand(UndoAsync);
        QuestionPanel.ClearSessionCommand = new AsyncRelayCommand(ClearSessionAsync);
        SolutionPanel.SolveTextCommand = new AsyncRelayCommand(SolveTextAsync);
        SolutionPanel.SolveImagesCommand = new AsyncRelayCommand(SolveVisionAsync);
        SolutionPanel.CopyCodeCommand = new RelayCommand(CopyCode);
        Settings.SaveAndTestCommand = new AsyncRelayCommand(SaveAndTestAsync);
        FeedbackPanel.SendFollowUpCommand = new AsyncRelayCommand(SendFollowUpAsync);
        History.ShowHistoryCommand = new AsyncRelayCommand(ShowHistoryAsync);
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

        Settings.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsBarViewModel.SelectedProvider))
            {
                _routingProvider.Tracker.Reset();
                await LoadProviderSettingsAsync();
                UpdateImageSolveState();
            }
        };
    }

    public async Task InitializeAsync()
    {
        await LoadSettingsAsync();
        SyncFromSession();
        await RefreshCamerasAsync();

        if (!string.IsNullOrWhiteSpace(Settings.ApiKey)
            && Settings.SelectedProvider?.Kind == AiProviderKind.DeepSeek)
            await RefreshBalanceAsync();
    }

    private async Task LoadSettingsAsync()
    {
        var providerValue = await _secrets.GetAsync("ai.provider", CancellationToken.None);
        var providerKind = AiProviderKindExtensions.FromStorageValue(providerValue);
        Settings.SelectedProvider = Settings.Providers.FirstOrDefault(p => p.Kind == providerKind)
                                    ?? Settings.Providers[0];

        await LoadProviderSettingsAsync();
    }

    private async Task LoadProviderSettingsAsync()
    {
        var provider = Settings.SelectedProvider?.Kind ?? AiProviderKind.DeepSeek;

        if (provider == AiProviderKind.VolcanoArk)
        {
            Settings.IsModelReadOnly = false;
            Settings.IsBalanceVisible = false;
            Settings.BalanceText = "余额：—";

            Settings.ApiKey = await _secrets.GetAsync("volcano.api_key", CancellationToken.None) ?? string.Empty;
            var model = await _secrets.GetAsync("volcano.model", CancellationToken.None);
            Settings.ModelDisplay = string.IsNullOrWhiteSpace(model)
                ? AiProviderDefaults.VolcanoArkDefaultModel
                : model;
            return;
        }

        Settings.IsModelReadOnly = true;
        Settings.IsBalanceVisible = true;

        Settings.ApiKey = await _secrets.GetAsync("deepseek.api_key", CancellationToken.None) ?? string.Empty;
        var deepseekModel = await _secrets.GetAsync("deepseek.model", CancellationToken.None);
        Settings.ModelDisplay = string.IsNullOrWhiteSpace(deepseekModel)
            ? AiProviderDefaults.DeepSeekDefaultModel
            : deepseekModel;
    }

    private async Task RefreshBalanceAsync()
    {
        try
        {
            var balance = await _textProvider.GetBalanceAsync(CancellationToken.None);
            Settings.BalanceText = balance?.ToDisplayText() ?? "余额：—";
        }
        catch
        {
            Settings.BalanceText = "余额：查询失败";
        }
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
            AddFeedback("错误", $"摄像头启动失败：{ex.Message}");
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
            await using var ms = new MemoryStream(frame.Data);
            var bitmap = new Bitmap(ms);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (CameraPanel.PreviewBitmap is Bitmap old)
                    old.Dispose();
                CameraPanel.PreviewBitmap = bitmap;
                CameraPanel.ShowPreviewPlaceholder = false;
            });
        }
        catch
        {
            // 忽略单帧解码失败。
        }
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
                AddFeedback("系统", $"OCR 完成，共 {last.Ocr.Lines.Count} 行{mergeHint}");
            }
            else if (last?.OcrStatus == Domain.Sessions.OcrStatus.Failed)
            {
                StatusText = $"截图已保存，但 OCR 失败：{last.ErrorMessage}";
                AddFeedback("警告", last.ErrorMessage ?? "OCR 失败");
            }
            else
            {
                StatusText = "截取并识别完成";
                AddFeedback("系统", "截图已保存");
            }
        }
        catch (DuplicateCaptureException ex)
        {
            StatusText = ex.Message;
            AddFeedback("警告", ex.Message);
        }
        catch (Exception ex)
        {
            StatusText = $"截取失败：{ex.Message}";
            AddFeedback("错误", ex.Message);
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
            AddFeedback("系统", "撤销成功");
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
            AddFeedback("系统", "会话已清空");
        }
        catch (Exception ex)
        {
            StatusText = $"清空失败：{ex.Message}";
        }

        await UpdateUndoStateAsync();
    }

    private async Task SolveTextAsync()
    {
        QuestionPanel.CanCapture = false;
        SolutionPanel.CanSolveText = false;
        StatusText = "正在文字解答，请稍候…";
        try
        {
            await _solveText.ExecuteAsync(CancellationToken.None);
            var solution = _session.Current.Solution;
            if (solution?.NeedsMoreContext == true)
            {
                StatusText = "解答完成，但题目信息可能不完整";
                AddFeedback("提示", "模型认为题干不完整，请继续截屏或手动补充。");
            }
            else
            {
                StatusText = "文字解答完成";
                AddFeedback("系统", "文字解答完成");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"解答失败：{ex.Message}";
            AddFeedback("错误", ex.Message);
        }
        finally
        {
            QuestionPanel.CanCapture = true;
            SyncFromSession();
        }
    }

    private async Task SolveVisionAsync()
    {
        QuestionPanel.CanCapture = false;
        SolutionPanel.CanSolveImages = false;
        StatusText = "正在图片解答，请稍候…";
        try
        {
            await _solveVision.ExecuteAsync(CancellationToken.None);
            var solution = _session.Current.Solution;
            if (solution?.NeedsMoreContext == true)
            {
                StatusText = "图片解答完成，但题目信息可能不完整";
                AddFeedback("提示", "模型认为题干不完整，请继续截屏。");
            }
            else
            {
                StatusText = "图片解答完成";
                AddFeedback("系统", "图片解答完成");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"图片解答失败：{ex.Message}";
            AddFeedback("错误", ex.Message);
        }
        finally
        {
            QuestionPanel.CanCapture = true;
            SyncFromSession();
        }
    }

    private async Task SaveAndTestAsync()
    {
        var provider = Settings.SelectedProvider?.Kind ?? AiProviderKind.DeepSeek;
        try
        {
            _routingProvider.Tracker.Reset();
            await _saveAndTest.ExecuteAsync(
                provider,
                Settings.ApiKey,
                Settings.ModelDisplay,
                CancellationToken.None);
            Settings.TestStatus = "连接成功";
            StatusText = "API 测试成功";
            AddFeedback("系统", "API 测试成功");

            if (provider == AiProviderKind.DeepSeek)
                await RefreshBalanceAsync();
            else
                UpdateImageSolveState();
        }
        catch (Exception ex)
        {
            Settings.TestStatus = "连接失败";
            StatusText = $"API 测试失败：{ex.Message}";
            AddFeedback("错误", ex.Message);
        }
    }

    private async Task SendFollowUpAsync()
    {
        if (string.IsNullOrWhiteSpace(FeedbackPanel.UserMessage))
            return;

        var message = FeedbackPanel.UserMessage;
        FeedbackPanel.UserMessage = string.Empty;
        AddFeedback("用户", message);

        try
        {
            await _sendFollowUp.ExecuteAsync(message, CancellationToken.None);
            var last = _session.Current.ChatMessages.LastOrDefault(m => m.Type == Domain.Common.FeedbackMessageType.Assistant);
            if (last is not null)
                AddFeedback("助手", last.Content);
        }
        catch (Exception ex)
        {
            AddFeedback("错误", ex.Message);
        }
    }

    private async Task ShowHistoryAsync()
    {
        if (OwnerWindow is null)
            return;

        var vm = _services.GetRequiredService<HistoryWindowViewModel>();
        await vm.RefreshAsync();

        var window = new HistoryWindow { DataContext = vm };
        var result = await window.ShowDialog<bool?>(OwnerWindow);
        if (result == true)
        {
            StatusText = "已加载历史会话";
            await UpdateUndoStateAsync();
            SyncFromSession();
        }
    }

    private void CopyCode()
    {
        if (string.IsNullOrWhiteSpace(SolutionPanel.CodeText) || OwnerWindow is null)
            return;

        OwnerWindow.Clipboard?.SetTextAsync(SolutionPanel.CodeText);
        StatusText = "代码已复制到剪贴板";
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

            RefreshThumbnails(session);
            UpdateSolutionPanel(session.Solution);

            SolutionPanel.CanSolveText = !string.IsNullOrWhiteSpace(session.WorkingQuestionText);
            UpdateImageSolveState();
        }
        finally
        {
            _syncingSession = false;
        }

        _ = UpdateUndoStateAsync();
    }

    private void UpdateImageSolveState()
    {
        var session = _session.Current;
        var isVolcano = Settings.SelectedProvider?.Kind == AiProviderKind.VolcanoArk;

        SolutionPanel.CanSolveImages = isVolcano
                                       && _visionProvider.IsConfigured
                                       && session.Captures.Count > 0;

        SolutionPanel.ImageSolveToolTip = Settings.SelectedProvider?.Kind switch
        {
            AiProviderKind.DeepSeek => "DeepSeek 不支持图片识别，请切换到火山方舟",
            AiProviderKind.VolcanoArk when !_visionProvider.IsConfigured => "视觉模型尚未配置",
            _ => "OCR 不准时，将全部截图上传至视觉模型"
        };
    }

    private void UpdateSolutionPanel(SolutionResult? solution)
    {
        SolutionPanel.IsProgramming = solution?.QuestionType == QuestionType.Programming;

        if (solution is null)
        {
            SolutionPanel.FinalAnswerText = string.Empty;
            SolutionPanel.ExplanationText = string.Empty;
            SolutionPanel.CodeText = string.Empty;
            return;
        }

        if (solution.QuestionType == QuestionType.Programming)
        {
            SolutionPanel.FinalAnswerText = string.Empty;
            SolutionPanel.ExplanationText = solution.NeedsMoreContext
                ? "提示：题目信息可能不完整，请继续截屏或补充题干。"
                : string.Empty;
            SolutionPanel.CodeText = solution.Code;
            return;
        }

        SolutionPanel.FinalAnswerText = string.IsNullOrWhiteSpace(solution.FinalAnswer)
            ? string.Empty
            : $"最终答案：{solution.FinalAnswer}";

        var explanation = solution.Explanation;
        if (solution.NeedsMoreContext)
            explanation = "【题目可能不完整】" + explanation;

        SolutionPanel.ExplanationText = string.IsNullOrWhiteSpace(explanation)
            ? string.Empty
            : $"解题思路：{explanation}";
        SolutionPanel.CodeText = string.Empty;
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
        MergeStrategy.NoOverlapWithWarning => " · 未检测到重叠",
        MergeStrategy.DuplicateSkipped => " · 重复截图",
        _ => string.Empty
    };

    private void AddFeedback(string role, string message) =>
        FeedbackPanel.Messages.Add($"[{role}] {message}");
}
