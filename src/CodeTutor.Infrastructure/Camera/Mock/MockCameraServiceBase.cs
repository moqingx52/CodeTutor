using System.Diagnostics;
using CodeTutor.Application.Abstractions;

namespace CodeTutor.Infrastructure.Camera.Mock;

public abstract class MockCameraServiceBase : ICameraService
{
    private readonly object _frameLock = new();
    private CameraFrame? _latestFrame;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private Func<CameraFrame, ValueTask>? _onFrame;

    protected abstract string DeviceId { get; }
    protected abstract string DeviceName { get; }

    public Task<IReadOnlyList<CameraDescriptor>> EnumerateAsync(CancellationToken ct)
    {
        var mode = CameraModeScorer.CreateScoredMode(1280, 720, 30, "MJPEG");
        return Task.FromResult<IReadOnlyList<CameraDescriptor>>([
            new CameraDescriptor(DeviceId, DeviceName, [mode])
        ]);
    }

    public Task StartAsync(CameraSelection selection, Func<CameraFrame, ValueTask> onFrame, CancellationToken ct)
    {
        _onFrame = onFrame;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;
        var frameIndex = 0;

        _loopTask = Task.Run(async () =>
        {
            var sw = Stopwatch.StartNew();
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var frame = await GenerateFrameAsync(frameIndex++, token);
                    lock (_frameLock)
                        _latestFrame = frame;

                    if (_onFrame is not null)
                        await _onFrame(frame);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }

                var targetMs = 33;
                var elapsed = (int)(sw.ElapsedMilliseconds % targetMs);
                var delay = Math.Max(1, targetMs - elapsed);
                await Task.Delay(delay, token);
            }
        }, token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_cts is null)
            return;

        await _cts.CancelAsync();
        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.WaitAsync(TimeSpan.FromSeconds(2), ct);
            }
            catch (TimeoutException)
            {
                // Best effort stop.
            }
        }

        _cts.Dispose();
        _cts = null;
        _loopTask = null;
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
        await StopAsync(CancellationToken.None);
        GC.SuppressFinalize(this);
    }

    protected abstract Task<CameraFrame> GenerateFrameAsync(int frameIndex, CancellationToken ct);
}
