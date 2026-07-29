using CodeTutor.Application.Abstractions;
using SkiaSharp;

namespace CodeTutor.Infrastructure.Storage;

public static class ImageCropper
{
    public static byte[] Crop(byte[] imageData, NormalizedRectangle region, string format = "png")
    {
        using var source = SKBitmap.Decode(imageData)
                         ?? throw new InvalidOperationException("无法解码截图。");

        var normalized = region.Clamp();
        if (!normalized.IsLargeEnough())
            return imageData;

        var x = (int)Math.Floor(normalized.X * source.Width);
        var y = (int)Math.Floor(normalized.Y * source.Height);
        var w = (int)Math.Ceiling(normalized.Width * source.Width);
        var h = (int)Math.Ceiling(normalized.Height * source.Height);

        w = Math.Min(w, source.Width - x);
        h = Math.Min(h, source.Height - y);
        if (w <= 0 || h <= 0)
            return imageData;

        var subset = new SKRectI(x, y, x + w, y + h);
        using var cropped = new SKBitmap(subset.Width, subset.Height, source.ColorType, source.AlphaType);
        if (!source.ExtractSubset(cropped, subset))
            throw new InvalidOperationException("裁剪选区失败。");

        using var image = SKImage.FromBitmap(cropped);
        using var data = image.Encode(format.Equals("jpeg", StringComparison.OrdinalIgnoreCase)
            ? SKEncodedImageFormat.Jpeg
            : SKEncodedImageFormat.Png, 95);

        return data.ToArray();
    }
}
