using CodeTutor.Application.Abstractions;
using CodeTutor.Infrastructure.Storage;

namespace CodeTutor.Infrastructure.Imaging;

public sealed class SkiaImageCropper : IImageCropper
{
    public byte[] Crop(byte[] imageData, NormalizedRectangle region) =>
        ImageCropper.Crop(imageData, region);
}
