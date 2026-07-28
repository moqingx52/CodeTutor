using SkiaSharp;
using CodeTutor.Application.Abstractions;

namespace CodeTutor.Infrastructure.Camera.Mock;

/// <summary>
/// 循环播放目录中的 PNG/JPEG 图片序列。
/// </summary>
public sealed class ImageSequenceCameraService : MockCameraServiceBase
{
    private readonly string _sourceDirectory;
    private readonly List<(byte[] Data, int Width, int Height)> _frames = [];

    public ImageSequenceCameraService(string? sourceDirectory)
    {
        _sourceDirectory = sourceDirectory ?? string.Empty;
        LoadFrames();
        if (_frames.Count == 0)
            throw new InvalidOperationException(
                $"No images found in '{_sourceDirectory}'. Provide --source with a directory of PNG/JPEG files.");
    }

    protected override string DeviceId => "mock:images";
    protected override string DeviceName => $"模拟摄像头（图片序列：{Path.GetFileName(_sourceDirectory)}）";

    protected override Task<CameraFrame> GenerateFrameAsync(int frameIndex, CancellationToken ct)
    {
        var frame = _frames[frameIndex % _frames.Count];
        return Task.FromResult(new CameraFrame(
            (byte[])frame.Data.Clone(),
            frame.Width,
            frame.Height,
            "MJPEG",
            DateTimeOffset.UtcNow));
    }

    private void LoadFrames()
    {
        if (string.IsNullOrWhiteSpace(_sourceDirectory) || !Directory.Exists(_sourceDirectory))
            return;

        var files = Directory.GetFiles(_sourceDirectory)
            .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in files)
        {
            using var bitmap = SKBitmap.Decode(file);
            if (bitmap is null)
                continue;

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            if (data is null)
                continue;

            _frames.Add((data.ToArray(), bitmap.Width, bitmap.Height));
        }
    }
}
