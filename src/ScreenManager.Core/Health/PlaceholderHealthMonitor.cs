namespace OrderBuddy.ScreenManager.Core.Health;

public sealed class PlaceholderHealthMonitor : IHealthMonitor
{
    public bool IsSystemHealthy() => true;
}
