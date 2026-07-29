using CodeTutor.Application.Abstractions;
using FluentAssertions;

namespace CodeTutor.Application.Tests;

public sealed class NormalizedRectangleTests
{
    [Fact]
    public void Clamp_keeps_region_inside_unit_square()
    {
        var rect = new NormalizedRectangle(-0.1, 0.2, 1.5, 0.9).Clamp();

        rect.X.Should().Be(0);
        rect.Y.Should().Be(0.2);
        rect.Width.Should().Be(1);
        rect.Height.Should().Be(0.8);
    }

    [Fact]
    public void IsLargeEnough_rejects_tiny_regions()
    {
        new NormalizedRectangle(0, 0, 0.01, 0.5).IsLargeEnough().Should().BeFalse();
        new NormalizedRectangle(0, 0, 0.05, 0.05).IsLargeEnough().Should().BeTrue();
    }
}
