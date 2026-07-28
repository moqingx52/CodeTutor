using CodeTutor.Application.Abstractions;
using CodeTutor.Infrastructure.Camera.Mock;

namespace CodeTutor.Infrastructure.Camera;

/// <summary>
/// 优先使用真实摄像头；无可用设备时回退到模拟摄像头。
/// </summary>
public sealed class AdaptiveCameraService : ICameraService
{
    private readonly FlashCapCameraService _real = new();
    private readonly SyntheticCameraService _fallback = new();
    private ICameraService _active = null!;
    private bool _initialized;

    public async Task<IReadOnlyList<CameraDescriptor>> EnumerateAsync(CancellationToken ct)
    {
        var realDevices = await _real.EnumerateAsync(ct);
        if (realDevices.Count > 0)
        {
            _active = _real;
            return realDevices;
        }

        _active = _fallback;
        return await _fallback.EnumerateAsync(ct);
    }

    private async Task EnsureActiveAsync(CancellationToken ct)
    {
        if (_initialized)
            return;

        await EnumerateAsync(ct);
        _initialized = true;
    }

    public async Task StartAsync(CameraSelection selection, Func<CameraFrame, ValueTask> onFrame, CancellationToken ct)
    {
        await EnsureActiveAsync(ct);
        await _active.StartAsync(selection, onFrame, ct);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_active is not null)
            await _active.StopAsync(ct);
    }

    public CameraFrame? TryGetLatestFrameCopy()
    {
        return _active?.TryGetLatestFrameCopy();
    }

    public async ValueTask DisposeAsync()
    {
        await _real.DisposeAsync();
        await _fallback.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
