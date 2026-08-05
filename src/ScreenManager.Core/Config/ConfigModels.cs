namespace OrderBuddy.ScreenManager.Core.Config;

public enum AppType
{
    Till,
    Kiosk,
    Menu,
    Kds,
    Collection,
    Promo,
    Custom
}

public sealed record AppConfig
{
    public const int CurrentSchemaVersion = 3;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public DeviceInfo Device { get; init; } = new();
    public GeneralSettings General { get; init; } = new();
    public List<ScreenConfig> Screens { get; init; } = [];

    public static AppConfig CreateDefault() => new();
}

public sealed record DeviceInfo
{
    public string DeviceName { get; init; } = Environment.MachineName;
    public string DeviceId { get; init; } = Guid.NewGuid().ToString("N");
    public string StoreId { get; init; } = string.Empty;
    public string OrganizationId { get; init; } = string.Empty;
    public string AppVersion { get; init; } = "2.0.0";
}

public sealed record GeneralSettings
{
    public bool AutoStartOnLogin { get; init; } = true;
    public bool ServiceWatchdogEnabled { get; init; } = false;
    public bool AutoReconnect { get; init; } = true;
    public bool LockSystemKeys { get; init; } = false;
    public List<int> RetryBackoffSec { get; init; } = [5, 10, 30, 60];
    public int OfflineTimeoutSec { get; init; } = 20;
    public string LogLevel { get; init; } = "Information";
    public CacheSettings Cache { get; init; } = new();
    public NightlyMaintenanceSettings NightlyMaintenance { get; init; } = new();
    public CacheMaintenanceSettings CacheMaintenance { get; init; } = new();
    public string AdminPinHash { get; init; } = string.Empty;
}

public sealed record CacheSettings
{
    public bool Enabled { get; init; } = true;
    public bool ClearOnUpdate { get; init; } = true;
}

public sealed record CacheMaintenanceSettings
{
    public bool Enabled { get; init; } = true;
    public int IntervalMinutes { get; init; } = 60;

    /// <summary>"cacheOnly" clears DiskCache+CacheStorage (keeps session).
    /// "cacheAndSession" also clears Cookies+AllDomStorage (logs out).</summary>
    public string Scope { get; init; } = "cacheAndSession";

    public bool ReloadAfterClear { get; init; } = true;
}

public sealed record NightlyMaintenanceSettings
{
    public bool Enabled { get; init; } = true;
    public string Time { get; init; } = "03:30";
    public string Action { get; init; } = "reloadAll";
}

public sealed record ScreenConfig
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; init; } = true;
    public string Name { get; init; } = "Screen";
    public string DisplayKey { get; init; } = string.Empty;
    public int DisplayNumberHint { get; init; } = 1;
    public AppType AppType { get; init; } = AppType.Custom;
    public string Url { get; init; } = "https://example.org";
    public int StartupDelaySec { get; init; } = 0;
    public int ReloadIntervalSec { get; init; } = 0;
    public bool FullScreen { get; init; } = true;
    public bool AllowPopups { get; init; } = false;
    public bool RedirectPopupsToSameWindow { get; init; } = true;
    public bool AutoGrantPermissions { get; init; } = true;
    public bool AllowInsecureCertificates { get; init; } = false;
    public int RotationDegrees { get; init; } = 0;
    public int ZoomPercent { get; init; } = 100;
    public List<string> AllowedDomains { get; init; } = [];
    public FallbackConfig Fallback { get; init; } = new();
    public ScreenCredentials Credentials { get; init; } = new();
}

public sealed record FallbackConfig
{
    public string Mode { get; init; } = "branded";
    public string CustomHtmlPath { get; init; } = string.Empty;
    public string ImagePath { get; init; } = string.Empty;
}

/// <summary>
/// Per-screen saved credentials for kiosk auto-login after a cache clear.
/// The password is stored as a DPAPI-encrypted base64 blob — never plaintext.
/// </summary>
public sealed record ScreenCredentials
{
    public bool AutoLoginEnabled { get; init; } = false;
    public string Username { get; init; } = string.Empty;

    /// <summary>DPAPI (LocalMachine) encrypted password, base64-encoded. Empty when not set.</summary>
    public string EncryptedPassword { get; init; } = string.Empty;
}

public sealed class ConfigChangedEventArgs : EventArgs
{
    public ConfigChangedEventArgs(
        AppConfig newConfig,
        string source = "unknown",
        string? changeId = null,
        IReadOnlyList<string>? diagnostics = null)
    {
        NewConfig = newConfig;
        Source = source;
        ChangeId = string.IsNullOrWhiteSpace(changeId) ? Guid.NewGuid().ToString("N") : changeId;
        Diagnostics = diagnostics ?? [];
    }

    public AppConfig NewConfig { get; }
    public string Source { get; }
    public string ChangeId { get; }
    public IReadOnlyList<string> Diagnostics { get; }
}

public sealed record ConfigServiceOptions(string ConfigFilePath, string BackupFilePath)
{
    public static ConfigServiceOptions FromRootDirectory(string rootDirectory)
    {
        var configFilePath = Path.Combine(rootDirectory, "config.json");
        var backupFilePath = Path.Combine(rootDirectory, "config.bak");
        return new ConfigServiceOptions(configFilePath, backupFilePath);
    }
}
