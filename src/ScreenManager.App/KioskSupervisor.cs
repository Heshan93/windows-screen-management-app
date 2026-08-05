using Microsoft.Extensions.DependencyInjection;
using System.Windows.Threading;
using System.Threading;
using OrderBuddy.ScreenManager.Core;
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
    private int _cacheClearInProgress; // 0 = idle, 1 = running (Interlocked flag)

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

    public void ReconcileNow(string reason)
    {
        ReconcileWindows(reason);
    }

    public void ReloadAllWindows(string reason)
    {
        if (_disposed)
        {
            return;
        }

        foreach (var (screenId, window) in _windows)
        {
            window.RequestReload(reason);
            _logger.Information("Requested reload for screen {ScreenId}. Reason: {Reason}", screenId, reason);
        }
    }

    /// <summary>
    /// Clears the shared WebView2 profile cache on all live windows (one clear suffices
    /// since the profile is shared) and optionally reloads all windows afterward.
    /// A re-entrancy guard prevents concurrent timer/manual clears from overlapping.
    /// When <paramref name="idleThresholdMinutes"/> is &gt; 0, the clear is skipped and
    /// returns <c>false</c> if the system has received user input more recently than the
    /// threshold — the caller should retry later.
    /// Returns <c>true</c> when the cache was actually cleared, <c>false</c> when deferred.
    /// </summary>
    public async Task<bool> ClearCacheAllAsync(string scope, bool reloadAfter, int idleThresholdMinutes = 0)
    {
        if (_disposed)
        {
            return false;
        }

        // Idle check — skip if user is actively using the screen.
        if (idleThresholdMinutes > 0)
        {
            var threshold = TimeSpan.FromMinutes(idleThresholdMinutes);
            if (!KioskIdleDetector.IsIdle(threshold))
            {
                var idleDuration = KioskIdleDetector.GetIdleDuration();
                _logger.Information(
                    "Cache clear deferred: screen is active. " +
                    "IdleThreshold: {Threshold} min, CurrentIdle: {CurrentIdle:mm\\:ss}. Will retry.",
                    idleThresholdMinutes, idleDuration);
                return false;
            }
        }

        if (Interlocked.CompareExchange(ref _cacheClearInProgress, 1, 0) != 0)
        {
            _logger.Warning("ClearCacheAllAsync: another clear is already in progress. Skipping.");
            return false;
        }

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _logger.Information(
                "Cache clear started. Scope: {Scope}, ReloadAfter: {ReloadAfter}, Windows: {Count}",
                scope, reloadAfter, _windows.Count);

            // The WebView2 profile is shared — clearing via any one window clears all.
            // We attempt the first live window found; if none is live, log and skip.
            KioskWindow? target = null;
            foreach (var window in _windows.Values)
            {
                target = window;
                break;
            }

            bool cleared = false;
            if (target is not null)
            {
                cleared = await target.Dispatcher.InvokeAsync(
                    () => target.ClearCacheAsync(scope)).Task.Unwrap();
            }
            else
            {
                _logger.Warning("ClearCacheAllAsync: no live kiosk windows. Cache not cleared.");
            }

            sw.Stop();
            _logger.Information(
                "Cache clear finished. Scope: {Scope}, Cleared: {Cleared}, ElapsedMs: {ElapsedMs}",
                scope, cleared, sw.ElapsedMilliseconds);

            if (cleared && reloadAfter)
            {
                ReloadAllWindows("cache-clear");
            }

            return cleared;
        }
        finally
        {
            Interlocked.Exchange(ref _cacheClearInProgress, 0);
        }
    }

    public void ReassertTopmostWindows(string reason)
    {
        if (_disposed)
        {
            return;
        }

        foreach (var (screenId, window) in _windows)
        {
            window.ReassertTopmost();
            _logger.Information("Reasserted top-most state for screen {ScreenId}. Reason: {Reason}", screenId, reason);
        }
    }

    /// <summary>
    /// Temporarily removes the Topmost flag from all kiosk windows so modal dialogs
    /// (PIN pad, admin settings) are not hidden behind them.
    /// </summary>
    public void SuspendTopmost()
    {
        if (_disposed) return;
        foreach (var (_, window) in _windows)
        {
            window.Dispatcher.Invoke(() => window.Topmost = false);
        }
    }

    /// <summary>
    /// Restores the Topmost flag on all kiosk windows after admin mode ends.
    /// </summary>
    public void RestoreTopmost()
    {
        if (_disposed) return;
        foreach (var (_, window) in _windows)
        {
            window.ReassertTopmost();
        }
    }

    public void ShowIdentifyOverlays()
    {
        if (_disposed)
        {
            return;
        }

        var overlays = new List<IdentifyOverlayWindow>();
        var displays = _displayManager.Enumerate().OrderBy(d => d.X).ThenBy(d => d.Y).ToList();

        for (var i = 0; i < displays.Count; i++)
        {
            var overlay = ActivatorUtilities.CreateInstance<IdentifyOverlayWindow>(_services, displays[i], i + 1);
            overlays.Add(overlay);
            overlay.Show();
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            foreach (var overlay in overlays)
            {
                overlay.Close();
            }
        };
        timer.Start();
    }

    private void OnDisplaysChanged(object? sender, DisplaysChangedEventArgs e)
    {
        _ = e;
        RunOnUi(() => ReconcileWindows("display-changed"));
    }

    private void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
    {
        _logger.Information(
            "Supervisor received config change. Source: {Source}, ChangeId: {ChangeId}, EnabledScreens: {EnabledScreens}, Diagnostics: {Diagnostics}",
            e.Source,
            e.ChangeId,
            e.NewConfig.Screens.Count(s => s.Enabled),
            string.Join(" | ", e.Diagnostics));
        RunOnUi(() => ReconcileWindows("config-changed"));
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(action);
            return;
        }

        action();
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
