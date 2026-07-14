using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderBuddy.ScreenManager.Core.Config;
using OrderBuddy.ScreenManager.Core.Display;
using OrderBuddy.ScreenManager.Core.Health;

namespace ScreenManager.Tests;

/// <summary>
/// P0 bootstrap smoke tests — verify DI wiring and service contracts.
/// </summary>
public class P0BootstrapTests
{
    private static IHost BuildTestHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IConfigService, PlaceholderConfigService>();
                services.AddSingleton<IDisplayManager, PlaceholderDisplayManager>();
                services.AddSingleton<IHealthMonitor, PlaceholderHealthMonitor>();
            })
            .Build();
    }

    [Fact]
    public void DiContainer_ResolvesIConfigService()
    {
        using var host = BuildTestHost();
        var svc = host.Services.GetRequiredService<IConfigService>();
        svc.Should().NotBeNull();
        svc.Should().BeAssignableTo<IConfigService>();
    }

    [Fact]
    public void DiContainer_ResolvesIDisplayManager()
    {
        using var host = BuildTestHost();
        var svc = host.Services.GetRequiredService<IDisplayManager>();
        svc.Should().NotBeNull();
        svc.Should().BeAssignableTo<IDisplayManager>();
    }

    [Fact]
    public void DiContainer_ResolvesIHealthMonitor()
    {
        using var host = BuildTestHost();
        var svc = host.Services.GetRequiredService<IHealthMonitor>();
        svc.Should().NotBeNull();
        svc.Should().BeAssignableTo<IHealthMonitor>();
    }

    [Fact]
    public void PlaceholderConfigService_GetLogLevel_ReturnsNonEmptyString()
    {
        var svc = new PlaceholderConfigService();
        var level = svc.GetLogLevel();
        level.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void PlaceholderDisplayManager_GetConnectedDisplayNames_ReturnsCollection()
    {
        var svc = new PlaceholderDisplayManager();
        var names = svc.GetConnectedDisplayNames();
        names.Should().NotBeNull();
    }

    [Fact]
    public void PlaceholderHealthMonitor_IsSystemHealthy_ReturnsBool()
    {
        var svc = new PlaceholderHealthMonitor();
        // Just confirm it returns without throwing
        var act = () => svc.IsSystemHealthy();
        act.Should().NotThrow();
    }
}