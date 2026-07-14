using FluentAssertions;
using OrderBuddy.ScreenManager.Core.Health;

namespace ScreenManager.Tests;

public sealed class P5HealthMonitorTests
{
    [Fact]
    public void RecordFailure_UsesConfiguredBackoff_ThenEscalatesToOfflineProbe()
    {
        var monitor = new PlaceholderHealthMonitor();
        monitor.ApplySettings([5, 10], offlineProbeSec: 20, heartbeatTimeoutSec: 20);

        var start = new DateTimeOffset(2026, 7, 14, 3, 0, 0, TimeSpan.Zero);

        var first = monitor.RecordFailure("s1", ScreenFailureType.Navigation, "http 500", start);
        var second = monitor.RecordFailure("s1", ScreenFailureType.Navigation, "http 500", start.AddSeconds(1));
        var third = monitor.RecordFailure("s1", ScreenFailureType.Navigation, "http 500", start.AddSeconds(2));

        first.State.Should().Be(ScreenHealthState.Recovering);
        first.NextRecoveryAttemptUtc.Should().Be(start.AddSeconds(5));

        second.State.Should().Be(ScreenHealthState.Recovering);
        second.NextRecoveryAttemptUtc.Should().Be(start.AddSeconds(11));

        third.State.Should().Be(ScreenHealthState.Offline);
        third.NextRecoveryAttemptUtc.Should().Be(start.AddSeconds(22));
    }

    [Fact]
    public void RecordNavigationSuccess_ResetsFailures_AndMarksOnline()
    {
        var monitor = new PlaceholderHealthMonitor();
        var now = new DateTimeOffset(2026, 7, 14, 3, 0, 0, TimeSpan.Zero);

        monitor.RecordFailure("s1", ScreenFailureType.Navigation, "timeout", now);
        var snapshot = monitor.RecordNavigationSuccess("s1", "https://example.org", now.AddSeconds(1));

        snapshot.State.Should().Be(ScreenHealthState.Online);
        snapshot.ConsecutiveFailures.Should().Be(0);
        snapshot.NextRecoveryAttemptUtc.Should().BeNull();
    }

    [Fact]
    public void EvaluateHeartbeatTimeout_WhenExceeded_TriggersRecovery()
    {
        var monitor = new PlaceholderHealthMonitor();
        monitor.ApplySettings([5, 10, 30], offlineProbeSec: 20, heartbeatTimeoutSec: 5);

        var start = new DateTimeOffset(2026, 7, 14, 3, 0, 0, TimeSpan.Zero);
        monitor.RecordHeartbeat("s1", start);

        var snapshot = monitor.EvaluateHeartbeatTimeout("s1", start.AddSeconds(8));

        snapshot.State.Should().Be(ScreenHealthState.Recovering);
        snapshot.LastReason.Should().Contain("HeartbeatTimeout");
        snapshot.NextRecoveryAttemptUtc.Should().Be(start.AddSeconds(13));
    }

    [Fact]
    public void RecordConnectivity_OfflineThenOnline_TransitionsToRecovering()
    {
        var monitor = new PlaceholderHealthMonitor();
        monitor.ApplySettings([5, 10], offlineProbeSec: 20, heartbeatTimeoutSec: 20);

        var start = new DateTimeOffset(2026, 7, 14, 3, 0, 0, TimeSpan.Zero);
        var offline = monitor.RecordConnectivity("s1", false, start, "network down");
        var online = monitor.RecordConnectivity("s1", true, start.AddSeconds(10), "network up");

        offline.State.Should().Be(ScreenHealthState.Offline);
        offline.NextRecoveryAttemptUtc.Should().Be(start.AddSeconds(20));

        online.State.Should().Be(ScreenHealthState.Recovering);
        online.NextRecoveryAttemptUtc.Should().Be(start.AddSeconds(10));
    }

    [Fact]
    public void ShouldAttemptRecovery_TrueOnlyAfterScheduledTime()
    {
        var monitor = new PlaceholderHealthMonitor();
        monitor.ApplySettings([5], offlineProbeSec: 20, heartbeatTimeoutSec: 20);

        var start = new DateTimeOffset(2026, 7, 14, 3, 0, 0, TimeSpan.Zero);
        monitor.RecordFailure("s1", ScreenFailureType.Navigation, "timeout", start);

        monitor.ShouldAttemptRecovery("s1", start.AddSeconds(4)).Should().BeFalse();
        monitor.ShouldAttemptRecovery("s1", start.AddSeconds(5)).Should().BeTrue();
    }

    [Fact]
    public void RecordMemoryPressure_RecordsFailureAndSchedulesRecovery()
    {
        var monitor = new PlaceholderHealthMonitor();
        monitor.ApplySettings([5, 10], offlineProbeSec: 20, heartbeatTimeoutSec: 20);

        var start = new DateTimeOffset(2026, 7, 14, 3, 0, 0, TimeSpan.Zero);
        var snapshot = monitor.RecordMemoryPressure("s1", 1024, 768, start);

        snapshot.State.Should().Be(ScreenHealthState.Recovering);
        snapshot.LastReason.Should().Contain("MemoryPressure");
        snapshot.ConsecutiveFailures.Should().Be(1);
    }
}
