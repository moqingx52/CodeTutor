using CodeTutor.Infrastructure.Camera;
using FluentAssertions;

namespace CodeTutor.Infrastructure.Tests;

public class CameraModeScorerTests
{
    [Fact]
    public void Score_PrefersMjpeg720p30()
    {
        var target = CameraModeScorer.Score(1280, 720, 30, "MJPEG");
        var yuyv = CameraModeScorer.Score(1280, 720, 30, "YUYV");
        var lowRes = CameraModeScorer.Score(640, 480, 30, "MJPEG");

        target.Should().BeLessThan(yuyv);
        target.Should().BeLessThan(lowRes);
    }

    [Fact]
    public void SelectBestMode_PicksLowestScore()
    {
        var modes = new[]
        {
            CameraModeScorer.CreateScoredMode(640, 480, 30, "MJPEG"),
            CameraModeScorer.CreateScoredMode(1280, 720, 30, "MJPEG"),
            CameraModeScorer.CreateScoredMode(1280, 720, 30, "YUYV"),
        };

        var best = CameraModeScorer.SelectBestMode(modes);
        best.Width.Should().Be(1280);
        best.PixelFormat.Should().Be("MJPEG");
    }
}
