namespace OrderBuddy.ScreenManager.Core.Config;

public sealed class PlaceholderConfigService : IConfigService
{
    public AppConfig CurrentConfig { get; private set; } = AppConfig.CreateDefault();

    public IReadOnlyList<string> LastDiagnostics => [];

    public event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

    public AppConfig Load() => CurrentConfig;

    public Task SaveAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CurrentConfig = config;
        ConfigChanged?.Invoke(this, new ConfigChangedEventArgs(CurrentConfig));
        return Task.CompletedTask;
    }

    public string GetLogLevel() => "Information";
}
