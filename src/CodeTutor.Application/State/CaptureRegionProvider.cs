using CodeTutor.Application.Abstractions;

namespace CodeTutor.Application.State;

public sealed class CaptureRegionProvider : ICaptureRegionProvider
{
    public NormalizedRectangle? Region { get; set; }
}
