using System.Text.Json;
using System.Threading;

namespace OrderBuddy.ScreenManager.Core.Config;

public sealed class ConfigService : IConfigService, IDisposable
{
    private readonly ConfigServiceOptions _options;
    private readonly object _sync = new();
    private readonly FileSystemWatcher _watcher;
    private readonly Timer _reloadTimer;

    private bool _disposed;
    private bool _isInternalSave;
    private IReadOnlyList<string> _lastDiagnostics = [];

    public AppConfig CurrentConfig { get; private set; } = AppConfig.CreateDefault();

    public IReadOnlyList<string> LastDiagnostics => _lastDiagnostics;

    public event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

    public ConfigService(ConfigServiceOptions options)
    {
        _options = options;

        var configDir = Path.GetDirectoryName(_options.ConfigFilePath)
            ?? throw new InvalidOperationException("Config directory path is invalid.");

        Directory.CreateDirectory(configDir);

        _reloadTimer = new Timer(_ => ReloadFromWatcher(), null, Timeout.Infinite, Timeout.Infinite);

        _watcher = new FileSystemWatcher(configDir, Path.GetFileName(_options.ConfigFilePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnConfigFileChanged;
        _watcher.Created += OnConfigFileChanged;
        _watcher.Renamed += OnConfigFileChanged;

        CurrentConfig = Load();
    }

    public AppConfig Load()
    {
        lock (_sync)
        {
            var diagnostics = new List<string>();
            var loaded = LoadInternal(diagnostics);

            var validation = ConfigValidator.Normalize(loaded);
            diagnostics.AddRange(validation.Diagnostics);

            var normalized = EnsureAdminPinProtected(validation.NormalizedConfig, diagnostics);

            CurrentConfig = normalized;
            _lastDiagnostics = diagnostics;

            // Persist normalized version so config remains clean after self-heal/migration.
            SaveInternal(normalized, diagnostics);

            return CurrentConfig;
        }
    }

    public async Task SaveAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var saved = await Task.Run(() =>
        {
            lock (_sync)
            {
                var diagnostics = new List<string>();
                var validation = ConfigValidator.Normalize(config);
                diagnostics.AddRange(validation.Diagnostics);

                var normalized = EnsureAdminPinProtected(validation.NormalizedConfig, diagnostics);
                SaveInternal(normalized, diagnostics);

                CurrentConfig = normalized;
                _lastDiagnostics = diagnostics;
                return normalized;
            }
        }, cancellationToken);

        // Notify listeners deterministically so kiosk windows re-apply the new URL/zoom/etc.
        // immediately. The FileSystemWatcher is suppressed during our own writes (_isInternalSave),
        // so relying on it would make saves apply only after an unrelated event or an app restart.
        // The watcher's own reload compares JSON and no-ops when it later observes the same content.
        ConfigChanged?.Invoke(this, new ConfigChangedEventArgs(saved));
    }

    public string GetLogLevel() => CurrentConfig.General.LogLevel;

    private AppConfig LoadInternal(List<string> diagnostics)
    {
        if (!File.Exists(_options.ConfigFilePath))
        {
            diagnostics.Add("Config file missing. Created default config.");
            return AppConfig.CreateDefault();
        }

        try
        {
            var json = File.ReadAllText(_options.ConfigFilePath);
            if (ConfigMigrator.TryMigrate(json, out var migrated, out var migrationDiagnostic))
            {
                diagnostics.Add(migrationDiagnostic);
                return migrated;
            }

            var loaded = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig);
            if (loaded is not null)
            {
                return loaded;
            }

            diagnostics.Add("Config file was empty or invalid JSON payload; using default config.");
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Primary config load failed: {ex.GetType().Name}. Attempting backup restore.");
        }

        if (File.Exists(_options.BackupFilePath))
        {
            try
            {
                var backupJson = File.ReadAllText(_options.BackupFilePath);
                var backup = JsonSerializer.Deserialize(backupJson, ConfigJsonContext.Default.AppConfig);
                if (backup is not null)
                {
                    diagnostics.Add("Recovered config from backup file.");
                    return backup;
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add($"Backup config load failed: {ex.GetType().Name}. Falling back to defaults.");
            }
        }

        diagnostics.Add("Using safe default config.");
        return AppConfig.CreateDefault();
    }

    private void SaveInternal(AppConfig config, List<string> diagnostics)
    {
        var configDir = Path.GetDirectoryName(_options.ConfigFilePath)
            ?? throw new InvalidOperationException("Config directory path is invalid.");
        Directory.CreateDirectory(configDir);

        var tempFile = Path.Combine(configDir, $".{Path.GetFileName(_options.ConfigFilePath)}.{Guid.NewGuid():N}.tmp");
        var payload = JsonSerializer.Serialize(config, ConfigJsonContext.Default.AppConfig);

        try
        {
            _isInternalSave = true;

            File.WriteAllText(tempFile, payload);

            if (File.Exists(_options.ConfigFilePath))
            {
                // Atomic replace; current file is moved to backup path atomically.
                File.Replace(tempFile, _options.ConfigFilePath, _options.BackupFilePath, true);
            }
            else
            {
                File.Move(tempFile, _options.ConfigFilePath, false);
                File.Copy(_options.ConfigFilePath, _options.BackupFilePath, true);
            }

            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }

            if (OperatingSystem.IsWindows() && ConfigFileAcl.ShouldApplyAcl(_options.ConfigFilePath))
            {
                if (!ConfigFileAcl.TryApplyAdminSystemWriteAcl(_options.ConfigFilePath, out var configAclDiagnostic))
                {
                    diagnostics.Add(configAclDiagnostic);
                }

                if (!ConfigFileAcl.TryApplyAdminSystemWriteAcl(_options.BackupFilePath, out var backupAclDiagnostic))
                {
                    diagnostics.Add(backupAclDiagnostic);
                }
            }
        }
        finally
        {
            _isInternalSave = false;
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add("Config saved successfully.");
        }
    }

    private static AppConfig EnsureAdminPinProtected(AppConfig input, List<string> diagnostics)
    {
        var adminPinHash = input.General.AdminPinHash;

        if (string.IsNullOrWhiteSpace(adminPinHash))
        {
            var defaultPinHash = ConfigSecurity.HashPin("1234");
            diagnostics.Add("Admin PIN hash was empty; generated protected default PIN hash.");
            return input with
            {
                General = input.General with
                {
                    AdminPinHash = defaultPinHash
                }
            };
        }

        if (!adminPinHash.StartsWith("PBKDF2$", StringComparison.Ordinal))
        {
            var rehashed = ConfigSecurity.HashPin(adminPinHash);
            diagnostics.Add("Admin PIN value was plaintext/legacy; converted to protected PBKDF2 hash.");
            return input with
            {
                General = input.General with
                {
                    AdminPinHash = rehashed
                }
            };
        }

        return input;
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_isInternalSave)
        {
            return;
        }

        _reloadTimer.Change(250, Timeout.Infinite);
    }

    private void ReloadFromWatcher()
    {
        if (_disposed)
        {
            return;
        }

        AppConfig newConfig;
        lock (_sync)
        {
            var previous = CurrentConfig;
            newConfig = LoadInternal(new List<string>());
            var validation = ConfigValidator.Normalize(newConfig);
            newConfig = EnsureAdminPinProtected(validation.NormalizedConfig, new List<string>());

            var previousJson = JsonSerializer.Serialize(previous, ConfigJsonContext.Default.AppConfig);
            var newJson = JsonSerializer.Serialize(newConfig, ConfigJsonContext.Default.AppConfig);
            if (string.Equals(previousJson, newJson, StringComparison.Ordinal))
            {
                return;
            }

            CurrentConfig = newConfig;
        }

        ConfigChanged?.Invoke(this, new ConfigChangedEventArgs(newConfig));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher.Changed -= OnConfigFileChanged;
        _watcher.Created -= OnConfigFileChanged;
        _watcher.Renamed -= OnConfigFileChanged;
        _watcher.Dispose();
        _reloadTimer.Dispose();
    }
}
