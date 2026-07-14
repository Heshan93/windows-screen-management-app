namespace OrderBuddy.ScreenManager.Core.Config;

public sealed class PlaceholderConfigService : IConfigService
{
    public string GetLogLevel() => "Information";
}
