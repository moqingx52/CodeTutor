using CodeTutor.Infrastructure.Ai;
using FluentAssertions;

namespace CodeTutor.Infrastructure.Tests;

public sealed class DeepSeekApiCallTrackerTests
{
    [Fact]
    public void BalanceRefreshNeeded_fires_on_every_fifth_successful_call()
    {
        var tracker = new DeepSeekApiCallTracker();
        var refreshCount = 0;
        tracker.BalanceRefreshNeeded += (_, _) => refreshCount++;

        for (var i = 0; i < 9; i++)
            tracker.RecordSuccessfulCall();

        refreshCount.Should().Be(1);

        tracker.RecordSuccessfulCall();
        refreshCount.Should().Be(2);
    }

    [Fact]
    public void Reset_clears_counter()
    {
        var tracker = new DeepSeekApiCallTracker();
        var refreshCount = 0;
        tracker.BalanceRefreshNeeded += (_, _) => refreshCount++;

        for (var i = 0; i < 4; i++)
            tracker.RecordSuccessfulCall();

        tracker.Reset();

        tracker.RecordSuccessfulCall();
        refreshCount.Should().Be(0);
        tracker.CurrentCount.Should().Be(1);
    }
}
