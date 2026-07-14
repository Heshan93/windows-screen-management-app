namespace OrderBuddy.ScreenManager.Core.Config;

public interface IConfigService
{
    AppConfig CurrentConfig { get; }
    IReadOnlyList<string> LastDiagnostics { get; }

    event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

    AppConfig Load();
    Task SaveAsync(AppConfig config, CancellationToken cancellationToken = default);

    string GetLogLevel();
}
