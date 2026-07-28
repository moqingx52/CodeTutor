using CodeTutor.Application.Abstractions;
using CodeTutor.Infrastructure.Camera.Mock;

namespace CodeTutor.Infrastructure.Camera;

public static class CameraServiceFactory
{
    public static ICameraService Create(string cameraMode, string? sourcePath) =>
        cameraMode.ToLowerInvariant() switch
        {
            "mock-images" or "mock-image-sequence" => new ImageSequenceCameraService(sourcePath),
            "mock-video" => new VideoFileCameraService(sourcePath),
            "mock" or "synthetic" => new SyntheticCameraService(),
            "real" or "flashcap" => new FlashCapCameraService(),
            "auto" => new AdaptiveCameraService(),
            _ => new AdaptiveCameraService()
        };
}
