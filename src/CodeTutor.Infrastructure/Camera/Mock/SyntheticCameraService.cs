using SkiaSharp;
using CodeTutor.Application.Abstractions;

namespace CodeTutor.Infrastructure.Camera.Mock;

/// <summary>
/// 无外部文件依赖的合成相机，用于 Linux CI 和默认 mock-video 模式。
/// </summary>
public class SyntheticCameraService : MockCameraServiceBase
{
    private const int Width = 1280;
    private const int Height = 720;

    protected override string DeviceId => "mock:synthetic";
    protected override string DeviceName => "模拟摄像头（合成画面）";

    protected override Task<CameraFrame> GenerateFrameAsync(int frameIndex, CancellationToken ct)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(30, 34, 48));

        var scrollOffset = (frameIndex * 4) % 600;
        using var titlePaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 36,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Sans", SKFontStyle.Bold)
        };
        using var bodyPaint = new SKPaint
        {
            Color = new SKColor(200, 210, 230),
            TextSize = 24,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Sans")
        };

        canvas.DrawText("CodeTutor Mock Camera", 40, 60 - scrollOffset, titlePaint);
        canvas.DrawText($"Frame #{frameIndex}  |  1280×720@30 MJPEG", 40, 110 - scrollOffset, bodyPaint);
        canvas.DrawText("题目：编写函数计算列表中偶数之和。", 40, 180 - scrollOffset, bodyPaint);
        canvas.DrawText("输入第一行是整数 n。", 40, 220 - scrollOffset, bodyPaint);
        canvas.DrawText("第二行包含 n 个整数。", 40, 260 - scrollOffset, bodyPaint);
        canvas.DrawText("输出所有偶数的和。", 40, 300 - scrollOffset, bodyPaint);
        canvas.DrawText("示例输入：5", 40, 340 - scrollOffset, bodyPaint);
        canvas.DrawText("1 2 3 4 5", 40, 380 - scrollOffset, bodyPaint);
        canvas.DrawText("示例输出：6", 40, 420 - scrollOffset, bodyPaint);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90)
                      ?? throw new InvalidOperationException("Failed to encode mock frame.");

        var bytes = data.ToArray();
        return Task.FromResult(new CameraFrame(bytes, Width, Height, "MJPEG", DateTimeOffset.UtcNow));
    }
}
