using Microsoft.Extensions.DependencyInjection;
using OrderBuddy.ScreenManager.Core.Config;
using OrderBuddy.ScreenManager.Core.Display;
using Serilog;

namespace OrderBuddy.ScreenManager.App;

public sealed class KioskSupervisor : IDisposable
{
    private readonly IServiceProvider _services;
    private readonly IConfigService _configService;
    private readonly IDisplayManager _displayManager;
    private readonly Serilog.ILogger _logger;
    private readonly Dictionary<string, KioskWindow> _windows = new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;

    public KioskSupervisor(
        IServiceProvider services,
        IConfigService configService,
        IDisplayManager displayManager,
        Serilog.ILogger logger)
    {
        _services = services;
        _configService = configService;
        _displayManager = displayManager;
        _logger = logger;

        _displayManager.DisplaysChanged += OnDisplaysChanged;
        _configService.ConfigChanged += OnConfigChanged;
    }

    public void Start()
    {
        ReconcileWindows("initial-start");
    }

    private void OnDisplaysChanged(object? sender, DisplaysChangedEventArgs e)
    {
        _ = e;
        ReconcileWindows("display-changed");
    }

    private void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
    {
        _ = e;
        ReconcileWindows("config-changed");
    }

    private void ReconcileWindows(string reason)
    {
        if (_disposed)
        {
            return;
        }

        var displays = _displayManager.Enumerate();
        var config = _configService.CurrentConfig;
        var bindings = DisplayBindingPlanner.Plan(config.Screens, displays);

        _logger.Information(
            "Reconciling kiosk windows. Reason: {Reason}, EnabledScreens: {ScreenCount}, Displays: {DisplayCount}",
            reason,
            bindings.Count,
            displays.Count);

        var activeScreenIds = bindings.Select(b => b.Screen.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Close windows for removed/disabled screens.
        var staleScreens = _windows.Keys.Where(id => !activeScreenIds.Contains(id)).ToList();
        foreach (var staleScreenId in staleScreens)
        {
            _windows[staleScreenId].RequestClose();
            _windows.Remove(staleScreenId);
            _logger.Information("Closed window for stale screen binding {ScreenId}.", staleScreenId);
        }

        var primaryDisplay = _displayManager.GetPrimaryDisplay();

        foreach (var binding in bindings)
        {
            if (!_windows.TryGetValue(binding.Screen.Id, out var window))
            {
                window = ActivatorUtilities.CreateInstance<KioskWindow>(_services);
                _windows[binding.Screen.Id] = window;
                window.Show();
            }

            if (binding.Display is not null)
            {
                window.ApplyBinding(binding.Screen, binding.Display);
                continue;
            }

            if (primaryDisplay is not null)
            {
                window.ShowWaitingForDisplay(binding.Screen, primaryDisplay);
                _logger.Warning(
                    "Screen {ScreenId} has no matching display key {DisplayKey}. Showing waiting overlay.",
                    binding.Screen.Id,
                    binding.Screen.DisplayKey);
            }
            else
            {
                _logger.Warning(
                    "Screen {ScreenId} has no matching display and no primary display is available.",
                    binding.Screen.Id);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _displayManager.DisplaysChanged -= OnDisplaysChanged;
        _configService.ConfigChanged -= OnConfigChanged;

        foreach (var window in _windows.Values.ToList())
        {
            window.RequestClose();
        }

        _windows.Clear();
    }
}
