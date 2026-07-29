namespace CodeTutor.Application.Abstractions;

/// <summary>
/// 相对相机帧的归一化矩形选区（0~1）。
/// </summary>
public sealed record NormalizedRectangle(double X, double Y, double Width, double Height)
{
    public NormalizedRectangle Clamp()
    {
        var x = Math.Clamp(X, 0, 1);
        var y = Math.Clamp(Y, 0, 1);
        var w = Math.Clamp(Width, 0, 1 - x);
        var h = Math.Clamp(Height, 0, 1 - y);
        return new NormalizedRectangle(x, y, w, h);
    }

    public bool IsLargeEnough(double minFraction = 0.02) =>
        Width >= minFraction && Height >= minFraction;
}

public interface ICaptureRegionProvider
{
    NormalizedRectangle? Region { get; set; }
}

public interface IImageCropper
{
    byte[] Crop(byte[] imageData, NormalizedRectangle region);
}
