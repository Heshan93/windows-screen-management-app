using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using OrderBuddy.ScreenManager.App.Lifecycle;
using OrderBuddy.ScreenManager.Core.Config;
using OrderBuddy.ScreenManager.Core.Display;
using OrderBuddy.ScreenManager.Core.Health;
using Serilog;

namespace OrderBuddy.ScreenManager.App.ViewModels;

public sealed class DisplayTileViewModel
{
    public required string DeviceName { get; init; }
    public required string StableKey { get; init; }
    public required string BoundsText { get; init; }
    public required string StatusText { get; init; }
    public required double Left { get; init; }
    public required double Top { get; init; }
    public required double TileWidth { get; init; }
    public required double TileHeight { get; init; }
    public required System.Windows.Media.Brush StatusBrush { get; init; }
}

public sealed class AdminSettingsViewModel : ObservableObject
{
    private readonly IConfigService _configService;
    private readonly IDisplayManager _displayManager;
    private readonly IHealthMonitor _healthMonitor;
    private readonly KioskSupervisor _kioskSupervisor;
    private readonly IAutoStartService _autoStartService;

    private bool _hasUnsavedChanges;
    private string _diagnosticsText = string.Empty;
    private string _testUrlResult = string.Empty;
    private string _autoStartStatusText = string.Empty;
    private bool _autoStartOnLogin;
    private bool _serviceWatchdogEnabled;
    private AdminScreenItemViewModel? _selectedScreen;

    public ObservableCollection<AdminScreenItemViewModel> Screens { get; } = [];
    public ObservableCollection<DisplayTileViewModel> DisplayTiles { get; } = [];

    public bool HasUnsavedChanges { get => _hasUnsavedChanges; set => SetProperty(ref _hasUnsavedChanges, value); }
    public string DiagnosticsText { get => _diagnosticsText; set => SetProperty(ref _diagnosticsText, value); }
    public string TestUrlResult { get => _testUrlResult; set => SetProperty(ref _testUrlResult, value); }
    public string AutoStartStatusText { get => _autoStartStatusText; set => SetProperty(ref _autoStartStatusText, value); }
    public bool AutoStartOnLogin { get => _autoStartOnLogin; set => SetProperty(ref _autoStartOnLogin, value); }
    public bool ServiceWatchdogEnabled { get => _serviceWatchdogEnabled; set => SetProperty(ref _serviceWatchdogEnabled, value); }
    public AdminScreenItemViewModel? SelectedScreen { get => _selectedScreen; set => SetProperty(ref _selectedScreen, value); }

    public AdminSettingsViewModel(
        IConfigService configService,
        IDisplayManager displayManager,
        IHealthMonitor healthMonitor,
        KioskSupervisor kioskSupervisor,
        IAutoStartService autoStartService)
    {
        _configService = configService;
        _displayManager = displayManager;
        _healthMonitor = healthMonitor;
        _kioskSupervisor = kioskSupervisor;
        _autoStartService = autoStartService;

        ReloadFromConfig();
    }

    private static string NormalizeUrlInput(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var value = raw.Trim();
        if (value.Contains("://", StringComparison.Ordinal))
        {
            return value;
        }

        return "https://" + value;
    }

    public void ReloadFromConfig()
    {
        Screens.Clear();
        foreach (var screen in _configService.CurrentConfig.Screens)
        {
            Screens.Add(AdminScreenItemViewModel.FromConfig(screen));
        }

        SelectedScreen = Screens.FirstOrDefault();
        AutoStartOnLogin = _configService.CurrentConfig.General.AutoStartOnLogin;
        ServiceWatchdogEnabled = _configService.CurrentConfig.General.ServiceWatchdogEnabled;
        RefreshDisplayTiles();
        RefreshDiagnostics();
        _ = RefreshAutoStartStatusAsync();
        HasUnsavedChanges = false;
    }

    public void MarkDirty() => HasUnsavedChanges = true;

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var changeId = Guid.NewGuid().ToString("N");
        var selectedScreenId = SelectedScreen?.Id ?? "(none)";

        Log.Information(
            "Admin save requested. ChangeId: {ChangeId}, SelectedScreenId: {SelectedScreenId}, ScreenCount: {ScreenCount}",
            changeId,
            selectedScreenId,
            Screens.Count);

        foreach (var screen in Screens)
        {
            screen.Url = NormalizeUrlInput(screen.Url);

            Log.Information(
                "Admin staged screen settings. ChangeId: {ChangeId}, ScreenId: {ScreenId}, Enabled: {Enabled}, Url: {Url}, ZoomPercent: {ZoomPercent}, RotationDegrees: {RotationDegrees}",
                changeId,
                screen.Id,
                screen.Enabled,
                screen.Url,
                screen.ZoomPercent,
                screen.RotationDegrees);
        }

        var current = _configService.CurrentConfig;
        var remapped = current.Screens.Select(existing =>
        {
            var vm = Screens.FirstOrDefault(s => s.Id.Equals(existing.Id, StringComparison.OrdinalIgnoreCase));
            return vm is null ? existing : vm.ToConfig(existing);
        }).ToList();

        var final = current with { Screens = remapped };
        final = final with
        {
            General = final.General with
            {
                AutoStartOnLogin = AutoStartOnLogin,
                ServiceWatchdogEnabled = ServiceWatchdogEnabled
            }
        };

        await _configService.SaveAsync(final, cancellationToken);

        var persisted = _configService.CurrentConfig.Screens
            .FirstOrDefault(s => s.Id.Equals(selectedScreenId, StringComparison.OrdinalIgnoreCase));

        Log.Information(
            "Admin save completed. ChangeId: {ChangeId}, PersistedSelectedScreenFound: {Found}, PersistedUrl: {PersistedUrl}, PersistedZoomPercent: {PersistedZoomPercent}, Diagnostics: {Diagnostics}",
            changeId,
            persisted is not null,
            persisted?.Url,
            persisted?.ZoomPercent,
            string.Join(" | ", _configService.LastDiagnostics));

        HasUnsavedChanges = false;
        RefreshDiagnostics();
        await RefreshAutoStartStatusAsync(cancellationToken);
    }

    public async Task RefreshAutoStartStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await _autoStartService.GetStatusAsync(cancellationToken);
            AutoStartStatusText =
                $"Auto-start task: {(status.IsRegistered ? "registered" : "not registered")}, restart policy: {(status.RestartPolicyEnabled ? "enabled" : "missing")}. {status.Detail}";
        }
        catch (Exception ex)
        {
            AutoStartStatusText = $"Auto-start status unavailable: {ex.Message}";
        }
    }

    public async Task<string> TestSelectedUrlAsync()
    {
        if (SelectedScreen is null)
        {
            TestUrlResult = "Select a screen first.";
            return TestUrlResult;
        }

        var normalized = NormalizeUrlInput(SelectedScreen.Url);
        SelectedScreen.Url = normalized;

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            TestUrlResult = "Invalid URL.";
            return TestUrlResult;
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var response = await client.GetAsync(uri);
            TestUrlResult = response.IsSuccessStatusCode
                ? $"URL reachable ({(int)response.StatusCode})."
                : $"URL unreachable ({(int)response.StatusCode}).";
        }
        catch (Exception ex)
        {
            TestUrlResult = $"URL test failed: {ex.Message}";
        }

        return TestUrlResult;
    }

    public void ApplyPresetForSelectedScreen(AppType appType)
    {
        if (SelectedScreen is null)
        {
            return;
        }

        SelectedScreen.AppType = appType;
        switch (appType)
        {
            case AppType.Kds:
                SelectedScreen.ZoomPercent = 100;
                SelectedScreen.ReloadIntervalSec = 0;
                SelectedScreen.RotationDegrees = 0;
                break;
            case AppType.Menu:
                SelectedScreen.ZoomPercent = 110;
                SelectedScreen.ReloadIntervalSec = 300;
                break;
            case AppType.Till:
                SelectedScreen.ZoomPercent = 100;
                SelectedScreen.ReloadIntervalSec = 0;
                break;
            default:
                break;
        }

        MarkDirty();
    }

    public void IdentifyScreens()
    {
        _kioskSupervisor.ShowIdentifyOverlays();
    }

    public string ExportSupportBundle(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var zipPath = Path.Combine(outputDirectory, $"OrderBuddy-support-{timestamp}.zip");

        var config = _configService.CurrentConfig;
        var redacted = config with
        {
            General = config.General with
            {
                AdminPinHash = "REDACTED"
            }
        };

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        var configEntry = archive.CreateEntry("config.redacted.json");
        using (var writer = new StreamWriter(configEntry.Open()))
        {
            writer.Write(System.Text.Json.JsonSerializer.Serialize(redacted, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }

        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OrderBuddy", "ScreenManager", "logs");
        if (Directory.Exists(logDir))
        {
            foreach (var logFile in Directory.GetFiles(logDir, "*.log"))
            {
                archive.CreateEntryFromFile(logFile, Path.Combine("logs", Path.GetFileName(logFile)));
            }
        }

        DiagnosticsText = $"Support bundle exported to {zipPath}";
        return zipPath;
    }

    public void RestartAllScreens()
    {
        _kioskSupervisor.ReloadAllWindows("admin-restart-all");
    }

    public void RefreshDisplayTiles()
    {
        DisplayTiles.Clear();

        var displays = _displayManager.Enumerate();
        if (displays.Count == 0)
        {
            return;
        }

        var minX = displays.Min(d => d.X);
        var minY = displays.Min(d => d.Y);
        var maxX = displays.Max(d => d.X + d.Width);
        var maxY = displays.Max(d => d.Y + d.Height);

        const double wallWidth = 560;
        const double wallHeight = 240;

        var scaleX = wallWidth / Math.Max(1, maxX - minX);
        var scaleY = wallHeight / Math.Max(1, maxY - minY);
        var scale = Math.Min(scaleX, scaleY);

        var snapshots = _healthMonitor.GetSnapshots()
            .Where(s => !string.IsNullOrEmpty(s.ScreenId))
            .GroupBy(s => s.ScreenId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var configScreens = _configService.CurrentConfig.Screens
            .Where(s => !string.IsNullOrEmpty(s.DisplayKey))
            .GroupBy(s => s.DisplayKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var display in displays)
        {
            var status = "Unassigned";
            var statusBrush = System.Windows.Media.Brushes.Gray;
            if (configScreens.TryGetValue(display.StableKey, out var screen) && snapshots.TryGetValue(screen.Id, out var snapshot))
            {
                status = $"{screen.Name} ({snapshot.State})";
                statusBrush = snapshot.State switch
                {
                    ScreenHealthState.Online => System.Windows.Media.Brushes.LimeGreen,
                    ScreenHealthState.Recovering => System.Windows.Media.Brushes.Gold,
                    ScreenHealthState.Loading => System.Windows.Media.Brushes.DodgerBlue,
                    _ => System.Windows.Media.Brushes.OrangeRed
                };
            }

            DisplayTiles.Add(new DisplayTileViewModel
            {
                DeviceName = display.DeviceName,
                StableKey = display.StableKey,
                BoundsText = $"{display.Width}x{display.Height} @ {display.X},{display.Y}",
                StatusText = status,
                Left = (display.X - minX) * scale,
                Top = (display.Y - minY) * scale,
                TileWidth = Math.Max(80, display.Width * scale),
                TileHeight = Math.Max(50, display.Height * scale),
                StatusBrush = statusBrush
            });
        }
    }

    public async Task<string> RunConnectivityTestAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

            var internet = await client.GetAsync("https://www.msftconnecttest.com/connecttest.txt");
            var internetStatus = internet.IsSuccessStatusCode ? "online" : $"http {(int)internet.StatusCode}";

            string originStatus;
            if (SelectedScreen is null || !Uri.TryCreate(SelectedScreen.Url, UriKind.Absolute, out var uri))
            {
                originStatus = "not-tested";
            }
            else
            {
                var origin = await client.GetAsync(uri);
                originStatus = origin.IsSuccessStatusCode ? "online" : $"http {(int)origin.StatusCode}";
            }

            var summary = $"Connectivity test: internet={internetStatus}, origin={originStatus}";
            DiagnosticsText = string.Concat(DiagnosticsText, Environment.NewLine, summary);
            return summary;
        }
        catch (Exception ex)
        {
            var summary = $"Connectivity test failed: {ex.Message}";
            DiagnosticsText = string.Concat(DiagnosticsText, Environment.NewLine, summary);
            return summary;
        }
    }

    public void RefreshDiagnostics()
    {
        var connectedDisplays = _displayManager.GetConnectedDisplayNames();
        var snapshots = _healthMonitor.GetSnapshots();

        var lines = new List<string>
        {
            $"Connected displays: {connectedDisplays.Count}",
            $"Health snapshots: {snapshots.Count}",
            $"Process uptime: {TimeSpan.FromMilliseconds(Environment.TickCount64):g}",
            $"Working set MB: {Environment.WorkingSet / (1024d * 1024d):F1}",
            $"Logs folder: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OrderBuddy", "ScreenManager", "logs")}",
            ""
        };

        lines.AddRange(snapshots.Select(s =>
            $"{s.ScreenId}: {s.State}, failures={s.ConsecutiveFailures}, lastSuccess={s.LastSuccessUtc:O}"));

        DiagnosticsText = string.Join(Environment.NewLine, lines);
    }
}
