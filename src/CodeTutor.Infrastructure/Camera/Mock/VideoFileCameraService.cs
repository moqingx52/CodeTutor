namespace CodeTutor.Infrastructure.Camera.Mock;

/// <summary>
/// 视频文件 mock：当前无内置解码器时回退到合成滚动帧。
/// 保留接口供后续接入真实视频解码。
/// </summary>
public sealed class VideoFileCameraService : SyntheticCameraService
{
    private readonly string? _videoPath;

    public VideoFileCameraService(string? videoPath) => _videoPath = videoPath;

    protected override string DeviceId => "mock:video";

    protected override string DeviceName =>
        string.IsNullOrWhiteSpace(_videoPath) || !File.Exists(_videoPath)
            ? "模拟摄像头（合成视频）"
            : $"模拟摄像头（视频：{Path.GetFileName(_videoPath)}）";
}
