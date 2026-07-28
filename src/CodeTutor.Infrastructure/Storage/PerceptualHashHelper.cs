using SkiaSharp;

namespace CodeTutor.Infrastructure.Storage;

public static class PerceptualHashHelper
{
    public static string ComputeFromBytes(byte[] imageData)
    {
        using var bitmap = SKBitmap.Decode(imageData);
        if (bitmap is null)
            return string.Empty;

        return ComputeFromBitmap(bitmap);
    }

    public static string ComputeFromFile(string imagePath)
    {
        using var bitmap = SKBitmap.Decode(imagePath);
        if (bitmap is null)
            return string.Empty;

        return ComputeFromBitmap(bitmap);
    }

    private static string ComputeFromBitmap(SKBitmap source)
    {
        const int size = 9;
        using var resized = source.Resize(new SKImageInfo(size, size), SKSamplingOptions.Default)
                        ?? throw new InvalidOperationException("Failed to resize for pHash.");

        var gray = new byte[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var color = resized.GetPixel(x, y);
            gray[y * size + x] = (byte)((color.Red * 299 + color.Green * 587 + color.Blue * 114) / 1000);
        }

        var hash = new ulong[size - 1];
        for (var y = 0; y < size - 1; y++)
        for (var x = 0; x < size; x++)
        {
            if (gray[y * size + x] < gray[(y + 1) * size + x])
                hash[y] |= 1UL << x;
        }

        return string.Join("", hash.Select(h => h.ToString("x16")));
    }

    public static bool AreSimilar(string a, string b, int maxHammingDistance = 5)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return false;

        if (a == b)
            return true;

        if (a.Length != b.Length)
            return false;

        var distance = 0;
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                distance++;
                if (distance > maxHammingDistance)
                    return false;
            }
        }

        return distance <= maxHammingDistance;
    }
}
