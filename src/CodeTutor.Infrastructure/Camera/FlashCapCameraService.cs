using FlashCap;
using CodeTutor.Application.Abstractions;

namespace CodeTutor.Infrastructure.Camera;

/// <summary>
/// FlashCap 真实相机实现（Windows DirectShow / Linux V4L2）。
/// </summary>
public sealed class FlashCapCameraService : ICameraService
{
    private readonly CaptureDevices _devices = new();
    private readonly object _frameLock = new();
    private readonly object _deviceLock = new();

    private CaptureDevice? _device;
    private CaptureDeviceDescriptor? _activeDescriptor;
    private VideoCharacteristics? _activeCharacteristics;
    private CameraFrame? _latestFrame;
    private Func<CameraFrame, ValueTask>? _onFrame;
    private bool _disposed;

    public Task<IReadOnlyList<CameraDescriptor>> EnumerateAsync(CancellationToken ct)
    {
        var result = new List<CameraDescriptor>();

        foreach (var descriptor in _devices.EnumerateDescriptors())
        {
            var modes = descriptor.Characteristics
                .Where(c => c.PixelFormat != PixelFormats.Unknown)
                .Select(ToVideoMode)
                .OrderBy(m => m.Score)
                .ToList();

            if (modes.Count == 0)
                continue;

            result.Add(new CameraDescriptor(
                CreateDeviceId(descriptor),
                FormatDeviceName(descriptor),
                modes));
        }

        return Task.FromResult<IReadOnlyList<CameraDescriptor>>(result);
    }

    public async Task StartAsync(CameraSelection selection, Func<CameraFrame, ValueTask> onFrame, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await StopAsync(ct);

        var descriptor = FindDescriptor(selection.DeviceId)
                         ?? throw new InvalidOperationException($"未找到摄像头：{selection.DeviceId}");

        var characteristics = FindCharacteristics(descriptor, selection.Mode)
                              ?? throw new InvalidOperationException("所选视频模式不可用。");

        _onFrame = onFrame;
        _activeDescriptor = descriptor;
        _activeCharacteristics = characteristics;

        var device = await descriptor.OpenAsync(
            characteristics,
            OnPixelBufferArrivedAsync);

        lock (_deviceLock)
            _device = device;

        await device.StartAsync();
    }

    public async Task StopAsync(CancellationToken ct)
    {
        CaptureDevice? device;
        lock (_deviceLock)
        {
            device = _device;
            _device = null;
        }

        if (device is not null)
        {
            try
            {
                if (device.IsRunning)
                    await device.StopAsync();
            }
            catch
            {
                // 忽略停止时的设备错误。
            }

            await device.DisposeAsync();
        }

        _activeDescriptor = null;
        _activeCharacteristics = null;
        _onFrame = null;

        lock (_frameLock)
            _latestFrame = null;
    }

    public CameraFrame? TryGetLatestFrameCopy()
    {
        lock (_frameLock)
        {
            if (_latestFrame is null)
                return null;

            return new CameraFrame(
                (byte[])_latestFrame.Data.Clone(),
                _latestFrame.Width,
                _latestFrame.Height,
                _latestFrame.PixelFormat,
                _latestFrame.CapturedAt);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await StopAsync(CancellationToken.None);
        GC.SuppressFinalize(this);
    }

    private async Task OnPixelBufferArrivedAsync(PixelBufferScope bufferScope)
    {
        if (_onFrame is null)
            return;

        try
        {
            var image = bufferScope.Buffer.ExtractImage();
            var format = MapPixelFormat(_activeCharacteristics?.PixelFormat ?? PixelFormats.JPEG);
            var width = _activeCharacteristics?.Width ?? 0;
            var height = _activeCharacteristics?.Height ?? 0;

            var frame = new CameraFrame(image, width, height, format, DateTimeOffset.UtcNow);

            lock (_frameLock)
                _latestFrame = frame;

            await _onFrame(frame);
        }
        catch
        {
            // 预览回调中不抛异常，避免中断采集。
        }
    }

    private CaptureDeviceDescriptor? FindDescriptor(string deviceId) =>
        _devices.EnumerateDescriptors().FirstOrDefault(d => CreateDeviceId(d) == deviceId);

    private static VideoCharacteristics? FindCharacteristics(CaptureDeviceDescriptor descriptor, VideoMode mode)
    {
        foreach (var characteristics in descriptor.Characteristics)
        {
            if (characteristics.PixelFormat == PixelFormats.Unknown)
                continue;

            var scored = ToVideoMode(characteristics);
            if (scored.Width == mode.Width
                && scored.Height == mode.Height
                && scored.Fps == mode.Fps
                && scored.PixelFormat.Equals(mode.PixelFormat, StringComparison.OrdinalIgnoreCase))
            {
                return characteristics;
            }
        }

        var best = descriptor.Characteristics
            .Where(c => c.PixelFormat != PixelFormats.Unknown)
            .Select(c => (Characteristic: c, Mode: ToVideoMode(c)))
            .OrderBy(x => x.Mode.Score)
            .FirstOrDefault();

        return best.Characteristic;
    }

    private static VideoMode ToVideoMode(VideoCharacteristics characteristics)
    {
        var fps = characteristics.FramesPerSecond.Denominator == 0
            ? 0
            : (int)Math.Round(
                (double)characteristics.FramesPerSecond.Numerator / characteristics.FramesPerSecond.Denominator);
        var format = MapPixelFormat(characteristics.PixelFormat);
        return CameraModeScorer.CreateScoredMode(
            characteristics.Width,
            characteristics.Height,
            fps,
            format);
    }

    private static string MapPixelFormat(PixelFormats format) => format switch
    {
        PixelFormats.JPEG => "MJPEG",
        PixelFormats.PNG => "PNG",
        PixelFormats.YUYV => "YUYV",
        PixelFormats.UYVY => "UYVY",
        PixelFormats.RGB24 => "RGB24",
        PixelFormats.RGB32 => "RGB32",
        PixelFormats.ARGB32 => "ARGB32",
        PixelFormats.NV12 => "NV12",
        _ => format.ToString()
    };

    private static string CreateDeviceId(CaptureDeviceDescriptor descriptor)
    {
        var identity = descriptor.Identity?.ToString() ?? descriptor.Name;
        return $"flashcap:{identity}";
    }

    private static string FormatDeviceName(CaptureDeviceDescriptor descriptor)
    {
        var name = string.IsNullOrWhiteSpace(descriptor.Name) ? "未知摄像头" : descriptor.Name;
        return name;
    }
}
