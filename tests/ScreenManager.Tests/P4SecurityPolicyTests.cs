using FluentAssertions;
using OrderBuddy.ScreenManager.Core.Config;
using OrderBuddy.ScreenManager.Core.Security;

namespace ScreenManager.Tests;

public sealed class P4SecurityPolicyTests
{
    [Fact]
    public void EvaluateNavigation_NoAllowList_AllowsHttp()
    {
        var screen = new ScreenConfig
        {
            Id = "s1",
            Url = "https://example.org"
        };

        var decision = KioskSecurityPolicy.EvaluateNavigation(screen, "https://example.org/path");

        decision.Allow.Should().BeTrue();
    }

    [Fact]
    public void EvaluateNavigation_BlocksFileAndAboutSchemes()
    {
        var screen = new ScreenConfig { Id = "s1" };

        KioskSecurityPolicy.EvaluateNavigation(screen, "file:///c:/test.html").Allow.Should().BeFalse();
        KioskSecurityPolicy.EvaluateNavigation(screen, "about:blank").Allow.Should().BeFalse();
    }

    [Fact]
    public void EvaluateNavigation_WithAllowList_BlocksOffListDomain()
    {
        var screen = new ScreenConfig
        {
            Id = "s1",
            AllowedDomains = ["example.org"]
        };

        var allowed = KioskSecurityPolicy.EvaluateNavigation(screen, "https://sub.example.org/page");
        var blocked = KioskSecurityPolicy.EvaluateNavigation(screen, "https://evil.test/");

        allowed.Allow.Should().BeTrue();
        blocked.Allow.Should().BeFalse();
    }

    [Fact]
    public void EvaluatePopupPolicy_RespectsScreenFlags()
    {
        var block = new ScreenConfig
        {
            Id = "s1",
            AllowPopups = false,
            RedirectPopupsToSameWindow = false
        };

        var redirect = block with { RedirectPopupsToSameWindow = true };
        var allow = block with { AllowPopups = true };

        KioskSecurityPolicy.EvaluatePopupPolicy(block).Should().Be(PopupAction.Block);
        KioskSecurityPolicy.EvaluatePopupPolicy(redirect).Should().Be(PopupAction.RedirectSameWindow);
        KioskSecurityPolicy.EvaluatePopupPolicy(allow).Should().Be(PopupAction.AllowNewWindow);
    }
}
