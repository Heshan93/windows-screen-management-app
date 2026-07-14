using FluentAssertions;
using OrderBuddy.ScreenManager.Core.Health;

namespace ScreenManager.Tests;

public sealed class P6ConnectivityClassifierTests
{
    [Fact]
    public void Evaluate_InternetDown_ReturnsInternetCause()
    {
        var status = ConnectivityClassifier.Evaluate(
            internetReachable: false,
            originReachable: false,
            captivePortalDetected: false);

        status.FailureCause.Should().Be(ConnectivityFailureCause.InternetDown);
        status.InternetReachable.Should().BeFalse();
        status.OriginReachable.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_OriginDown_ReturnsOriginCause()
    {
        var status = ConnectivityClassifier.Evaluate(
            internetReachable: true,
            originReachable: false,
            captivePortalDetected: false);

        status.FailureCause.Should().Be(ConnectivityFailureCause.OriginDown);
        status.InternetReachable.Should().BeTrue();
        status.OriginReachable.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_CaptivePortal_ReturnsCaptivePortalCause()
    {
        var status = ConnectivityClassifier.Evaluate(
            internetReachable: false,
            originReachable: false,
            captivePortalDetected: true);

        status.FailureCause.Should().Be(ConnectivityFailureCause.CaptivePortal);
        status.CaptivePortalDetected.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_Healthy_ReturnsNoneCause()
    {
        var status = ConnectivityClassifier.Evaluate(
            internetReachable: true,
            originReachable: true,
            captivePortalDetected: false);

        status.FailureCause.Should().Be(ConnectivityFailureCause.None);
        status.InternetReachable.Should().BeTrue();
        status.OriginReachable.Should().BeTrue();
    }
}
