using FluentAssertions;
using OrderBuddy.ScreenManager.Core.Lifecycle;

namespace ScreenManager.Tests;

public sealed class P8StartupTaskXmlTests
{
    [Fact]
    public void Build_IncludesRestartOnFailurePolicy()
    {
        var xml = StartupTaskXmlBuilder.Build(
            "OrderBuddy.ScreenManager.v2",
            "OrderBuddy startup",
            "OrderBuddy",
            "S-1-5-21-test",
            @"C:\Program Files\OrderBuddy\ScreenManager.App.exe",
            string.Empty);

        xml.Should().Contain("<RestartOnFailure>");
        xml.Should().Contain("<Interval>PT1M</Interval>");
        xml.Should().Contain("<Count>3</Count>");
    }

    [Fact]
    public void Build_ContainsCommandAndArguments()
    {
        var xml = StartupTaskXmlBuilder.Build(
            "OrderBuddy.ScreenManager.v2",
            "OrderBuddy startup",
            "OrderBuddy",
            "S-1-5-21-test",
            @"C:\Program Files\dotnet\dotnet.exe",
            "\"C:\\Program Files\\OrderBuddy\\ScreenManager.App.dll\"");

        xml.Should().Contain("<Command>C:\\Program Files\\dotnet\\dotnet.exe</Command>");
        xml.Should().Contain("&quot;C:\\Program Files\\OrderBuddy\\ScreenManager.App.dll&quot;");
    }

    [Fact]
    public void Build_UsesLeastPrivilegeWhenRequested()
    {
        var xml = StartupTaskXmlBuilder.Build(
            "OrderBuddy.ScreenManager.v2",
            "OrderBuddy startup",
            "OrderBuddy",
            "S-1-5-21-test",
            @"C:\Program Files\OrderBuddy\ScreenManager.App.exe",
            string.Empty,
            useHighestPrivileges: false);

        xml.Should().Contain("<RunLevel>LeastPrivilege</RunLevel>");
    }
}
