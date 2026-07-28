using SkiaSharp;
using CodeTutor.Application.Abstractions;
using CodeTutor.Infrastructure.Paths;

namespace CodeTutor.Infrastructure.Storage;

public sealed class FileImageStore : IImageStore
{
    public async Task<(string ImagePath, string ThumbnailPath)> SaveCaptureAsync(
        Guid sessionId,
        int sequence,
        Stream image,
        CancellationToken ct)
    {
        var sessionDir = AppPaths.SessionDirectory(sessionId);
        Directory.CreateDirectory(sessionDir);

        var imageName = $"capture_{sequence:D3}.png";
        var thumbName = $"capture_{sequence:D3}_thumb.jpg";
        var imagePath = Path.Combine(sessionDir, imageName);
        var thumbPath = Path.Combine(sessionDir, thumbName);

        await WriteAtomicallyAsync(imagePath, image, ct);
        await CreateThumbnailAsync(imagePath, thumbPath, ct);

        return (imagePath, thumbPath);
    }

    public Task MoveToTrashAsync(string imagePath, CancellationToken ct)
    {
        if (!File.Exists(imagePath))
            return Task.CompletedTask;

        var trashDir = Path.Combine(AppPaths.TrashRoot, DateTime.UtcNow.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(trashDir);

        var dest = Path.Combine(trashDir, Path.GetFileName(imagePath));
        if (File.Exists(dest))
            dest = Path.Combine(trashDir, $"{Guid.NewGuid():N}_{Path.GetFileName(imagePath)}");

        File.Move(imagePath, dest);

        var thumbPath = imagePath.Replace(".png", "_thumb.jpg", StringComparison.OrdinalIgnoreCase);
        if (File.Exists(thumbPath))
        {
            var thumbDest = Path.Combine(trashDir, Path.GetFileName(thumbPath));
            File.Move(thumbPath, thumbDest);
        }

        return Task.CompletedTask;
    }

    private static async Task WriteAtomicallyAsync(string targetPath, Stream source, CancellationToken ct)
    {
        var tempPath = targetPath + ".tmp";

        using var bitmap = SKBitmap.Decode(source);
        if (bitmap is null)
            throw new InvalidOperationException("Failed to decode capture image.");

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100)
                      ?? throw new InvalidOperationException("Failed to encode PNG.");

        await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            data.SaveTo(fs);
            await fs.FlushAsync(ct);
        }

        if (File.Exists(targetPath))
            File.Delete(targetPath);

        File.Move(tempPath, targetPath);
    }

    private static Task CreateThumbnailAsync(string imagePath, string thumbPath, CancellationToken ct)
    {
        using var input = SKBitmap.Decode(imagePath);
        if (input is null)
            throw new InvalidOperationException($"Failed to decode image: {imagePath}");

        const int thumbWidth = 160;
        var scale = (float)thumbWidth / input.Width;
        var thumbHeight = Math.Max(1, (int)(input.Height * scale));

        using var resized = input.Resize(new SKImageInfo(thumbWidth, thumbHeight), SKSamplingOptions.Default)
                        ?? throw new InvalidOperationException("Failed to resize thumbnail.");

        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        if (data is null)
            throw new InvalidOperationException("Failed to encode thumbnail.");

        var tempPath = thumbPath + ".tmp";
        using (var fs = File.Create(tempPath))
            data.SaveTo(fs);

        if (File.Exists(thumbPath))
            File.Delete(thumbPath);

        File.Move(tempPath, thumbPath);
        return Task.CompletedTask;
    }
}
