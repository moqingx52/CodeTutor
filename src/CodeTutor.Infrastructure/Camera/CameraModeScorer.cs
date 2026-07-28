using CodeTutor.Application.Abstractions;

namespace CodeTutor.Infrastructure.Camera;

public static class CameraModeScorer
{
    private const int TargetWidth = 1280;
    private const int TargetHeight = 720;
    private const int TargetFps = 30;

    public static int Score(int width, int height, int fps, string pixelFormat)
    {
        var resolutionDistance = Math.Abs(width - TargetWidth) + Math.Abs(height - TargetHeight);
        var fpsDistance = Math.Abs(fps - TargetFps);
        var formatPenalty = GetFormatPenalty(pixelFormat);

        if (formatPenalty == int.MaxValue)
            return int.MaxValue;

        return resolutionDistance * 10 + fpsDistance * 4 + formatPenalty;
    }

    public static VideoMode SelectBestMode(IReadOnlyList<VideoMode> modes) =>
        modes.OrderBy(m => m.Score).ThenByDescending(m => m.Width * m.Height).First();

    public static VideoMode CreateScoredMode(int width, int height, int fps, string pixelFormat) =>
        new(width, height, fps, pixelFormat, Score(width, height, fps, pixelFormat));

    private static int GetFormatPenalty(string pixelFormat)
    {
        var fmt = pixelFormat.ToUpperInvariant();
        if (fmt.Contains("JPEG", StringComparison.Ordinal) || fmt.Contains("MJPEG", StringComparison.Ordinal))
            return 0;
        if (fmt.Contains("YUY", StringComparison.Ordinal))
            return 10;
        if (fmt.Contains("RGB", StringComparison.Ordinal))
            return 20;
        if (fmt.Contains("UNKNOWN", StringComparison.Ordinal))
            return int.MaxValue;
        return 15;
    }
}
