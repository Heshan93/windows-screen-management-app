using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using OrderBuddy.ScreenManager.App.Browser;
using OrderBuddy.ScreenManager.App.Security;
using OrderBuddy.ScreenManager.App.Status;
using OrderBuddy.ScreenManager.Core.Config;
using OrderBuddy.ScreenManager.Core.Display;
using OrderBuddy.ScreenManager.Core.Health;
using OrderBuddy.ScreenManager.Core.Security;
using Serilog;

namespace OrderBuddy.ScreenManager.App;

/// <summary>
/// P1 kiosk spike window: a single full-screen WebView2 on the primary monitor.
/// </summary>
public partial class KioskWindow : Window
{
    private const string HeartbeatScript =
        "(function(){if(window.__obInstalled){return;}window.__obInstalled=true;" +
        "function post(m){if(window.chrome&&window.chrome.webview){window.chrome.webview.postMessage(m);}}" +
        "function hb(){post({type:'heartbeat',ts:Date.now()});}hb();setInterval(hb,5000);" +
        "var taps=[];" +
        "function cue(x,y){try{var d=document.createElement('div');d.style.cssText='position:fixed;z-index:2147483647;width:90px;height:90px;border-radius:50%;pointer-events:none;left:'+(x-45)+'px;top:'+(y-45)+'px;background:rgba(0,150,255,0.45);border:2px solid #fff;transition:opacity .5s,transform .5s;opacity:1;transform:scale(0.6);';(document.body||document.documentElement).appendChild(d);requestAnimationFrame(function(){d.style.opacity='0';d.style.transform='scale(1.5)';});setTimeout(function(){d.remove();},520);}catch(e){}}" +
        "function badge(n){try{var b=document.getElementById('__obAdminBadge');if(!b){b=document.createElement('div');b.id='__obAdminBadge';b.style.cssText='position:fixed;z-index:2147483647;top:8px;right:8px;pointer-events:none;background:#0096ff;color:#fff;font:600 13px sans-serif;padding:4px 10px;border-radius:12px;box-shadow:0 2px 6px rgba(0,0,0,.4);';(document.body||document.documentElement).appendChild(b);}b.textContent='Admin '+n+'/5';b.style.opacity='1';clearTimeout(b.__t);b.__t=setTimeout(function(){b.style.transition='opacity .4s';b.style.opacity='0';},1400);}catch(e){}}" +
        "function tap(x,y){var w=window.innerWidth;if(x<w-140||y>140){taps=[];return;}var n=Date.now();taps.push(n);taps=taps.filter(function(t){return n-t<=3000;});if(taps.length>=3){cue(x,y);badge(taps.length);}if(taps.length>=5){taps=[];post({type:'admin-gesture'});}}" +
        "window.addEventListener('pointerdown',function(e){tap(e.clientX,e.clientY);},true);})();";

    private readonly KioskBrowserOptions _browserOptions;
    private readonly IWebViewEnvironmentProvider _environmentProvider;
    private readonly Serilog.ILogger _logger;
    private readonly IConfigService _configService;
    private readonly IHealthMonitor _healthMonitor;
    private readonly ISystemClock _clock;
    private readonly ILastConnectionStore _lastConnectionStore;
    private readonly IAdminAccessService _adminAccessService;
    private readonly IServiceProvider _services;
    private readonly Stopwatch _coldStartStopwatch = new();
    private readonly DispatcherTimer _healthTickTimer;
    private readonly DispatcherTimer _reloadIntervalTimer;
    private readonly HttpClient _connectivityClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    private WebView2? _webView;
    private int _processFailureCount;
    private ScreenConfig? _activeScreen;
    private DisplayInfo? _activeDisplay;
    private bool _isLoaded;
    private bool _allowClose;
    private bool _recoveryInProgress;
    private DateTimeOffset _lastHealthWarningUtc = DateTimeOffset.MinValue;
    private ConnectivityFailureCause _lastFailureCause = ConnectivityFailureCause.Unknown;
    private DateTimeOffset? _lastSuccessfulConnectionUtc;
    private int _memoryWatchdogThresholdMb;
    private readonly Queue<DateTimeOffset> _cornerTapHistory = new();

    public KioskWindow(
        KioskBrowserOptions browserOptions,
        IWebViewEnvironmentProvider environmentProvider,
        Serilog.ILogger logger,
        IConfigService configService,
        IDisplayManager displayManager,
        IHealthMonitor healthMonitor,
        ISystemClock clock,
        ILastConnectionStore lastConnectionStore,
        IAdminAccessService adminAccessService,
        IServiceProvider services)
    {
        _browserOptions = browserOptions;
        _environmentProvider = environmentProvider;
        _logger = logger;
        _configService = configService;
        _healthMonitor = healthMonitor;
        _clock = clock;
        _lastConnectionStore = lastConnectionStore;
        _adminAccessService = adminAccessService;
        _services = services;

        // Keep these dependencies in the constructor to prove P0 DI contracts remain valid.
        _ = displayManager;

        _healthTickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _healthTickTimer.Tick += OnHealthTick;

        _reloadIntervalTimer = new DispatcherTimer();
        _reloadIntervalTimer.Tick += OnReloadIntervalTick;

        _memoryWatchdogThresholdMb = ResolveMemoryWatchdogThresholdMb();
        _healthMonitor.ApplySettings(
            _configService.CurrentConfig.General.RetryBackoffSec,
            _configService.CurrentConfig.General.OfflineTimeoutSec,
            _configService.CurrentConfig.General.OfflineTimeoutSec);

        _configService.ConfigChanged += OnConfigChanged;

        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
        Closing += OnClosing;
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
    }

    public void RequestClose()
    {
        _allowClose = true;
        Close();
    }

    public void RequestReload(string reason)
    {
        _ = Dispatcher.InvokeAsync(async () =>
        {
            if (_activeScreen is null)
            {
                return;
            }

            _logger.Information("Reload requested for screen {ScreenId}. Reason: {Reason}", _activeScreen.Id, reason);

            if (_webView?.CoreWebView2 is null)
            {
                await InitializeOrReinitializeWebViewAsync();
                return;
            }

            _webView.CoreWebView2.Navigate(_activeScreen.Url);
        });
    }

    public void ReassertTopmost()
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            Topmost = false;
            Topmost = true;
            Activate();
        });
    }

    public void ApplyBinding(ScreenConfig screen, DisplayInfo display)
    {
        var isSameTarget = _webView?.CoreWebView2 is not null
            && _activeScreen is not null
            && _activeScreen.Id.Equals(screen.Id, StringComparison.OrdinalIgnoreCase)
            && _activeDisplay is not null
            && string.Equals(_activeDisplay.StableKey, display.StableKey, StringComparison.OrdinalIgnoreCase);

        var previousUrl = _activeScreen?.Url;

        _activeScreen = screen;
        _activeDisplay = display;
        _lastSuccessfulConnectionUtc = _lastConnectionStore.GetLastSuccessUtc(screen.Id);

        _healthMonitor.EnsureScreen(screen.Id);
        ConfigureReloadInterval(screen);
        HideFallbackOverlay();

        ApplyDisplayPlacement(display);

        if (!_isLoaded)
        {
            return;
        }

        // If the same screen is already showing on the same display, apply changes in place
        // (instant zoom, navigate only if the URL changed) instead of a full cold restart.
        if (isSameTarget && _webView?.CoreWebView2 is not null)
        {
            ApplyZoomAndRotation(_webView, screen);
            _logger.Information(
                "Applied settings in place. ScreenId: {ScreenId}, ZoomPercent: {ZoomPercent}, RotationDegrees: {RotationDegrees}",
                screen.Id,
                screen.ZoomPercent,
                screen.RotationDegrees);

            if (!string.Equals(previousUrl, screen.Url, StringComparison.Ordinal))
            {
                _logger.Information("Applying updated URL in place. ScreenId: {ScreenId}, Url: {Url}", screen.Id, screen.Url);
                _webView.CoreWebView2.Navigate(screen.Url);
            }
            return;
        }

        _ = InitializeOrReinitializeWebViewAsync();
    }

    public void ShowWaitingForDisplay(ScreenConfig screen, DisplayInfo fallbackDisplay)
    {
        _activeScreen = screen;
        _activeDisplay = fallbackDisplay;

        ApplyDisplayPlacement(fallbackDisplay);
        CleanupWebView();

        ShowFallbackOverlay(
            title: $"Waiting for assigned display ({screen.Name})",
            message: $"Display key '{screen.DisplayKey}' not detected. Reconnect the monitor to auto-start.",
            connectivityMessage: "Cause: assigned display unavailable.",
            nextAttempt: null,
            screen: screen);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        _healthTickTimer.Start();

        if (_activeScreen is not null && _activeDisplay is not null)
        {
            await InitializeOrReinitializeWebViewAsync();
            return;
        }

        var primaryDisplay = new DisplayInfo(
            StableKey: "display:primary:fallback",
            DeviceName: "Primary",
            X: 0,
            Y: 0,
            Width: (int)SystemParameters.PrimaryScreenWidth,
            Height: (int)SystemParameters.PrimaryScreenHeight,
            WorkAreaWidth: (int)SystemParameters.WorkArea.Width,
            WorkAreaHeight: (int)SystemParameters.WorkArea.Height,
            Orientation: SystemParameters.PrimaryScreenWidth >= SystemParameters.PrimaryScreenHeight ? "Landscape" : "Portrait",
            DpiScaleX: 1.0,
            DpiScaleY: 1.0,
            IsPrimary: true);

        ShowWaitingForDisplay(
            new ScreenConfig
            {
                Id = "unassigned",
                Name = "Unassigned",
                DisplayKey = "",
                Url = _browserOptions.StartUrl
            },
            primaryDisplay);
    }

    private async Task InitializeOrReinitializeWebViewAsync()
    {
        if (_activeScreen is null || _activeDisplay is null)
        {
            return;
        }

        try
        {
            _coldStartStopwatch.Restart();
            CleanupWebView();
            HideFallbackOverlay();

            var webView = new WebView2();
            RootGrid.Children.Add(webView);
            _webView = webView;

            var environment = await _environmentProvider.GetEnvironmentAsync();
            await webView.EnsureCoreWebView2Async(environment);

            ConfigureWebViewSettings(webView.CoreWebView2.Settings);
            HookWebViewEvents(webView.CoreWebView2);
            await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(HeartbeatScript);

            ApplyZoomAndRotation(webView, _activeScreen);
            _logger.Information(
                "Applied per-screen presentation settings. ScreenId: {ScreenId}, ZoomPercent: {ZoomPercent}, RotationDegrees: {RotationDegrees}",
                _activeScreen.Id,
                _activeScreen.ZoomPercent,
                _activeScreen.RotationDegrees);
            webView.CoreWebView2.Navigate(_activeScreen.Url);

            _logger.Information(
                "Kiosk WebView2 initialized. ScreenId: {ScreenId}, DisplayKey: {DisplayKey}, StartUrl: {StartUrl}, ColdStartMs: {ColdStartMs}",
                _activeScreen.Id,
                _activeDisplay.StableKey,
                _activeScreen.Url,
                _coldStartStopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize kiosk WebView2.");
            if (_activeScreen is not null)
            {
                var snapshot = _healthMonitor.RecordFailure(
                    _activeScreen.Id,
                    ScreenFailureType.Manual,
                    "WebView2 initialization failed",
                    _clock.UtcNow);
                ShowRecoveryOverlay(snapshot, "Browser initialization failed.");
            }
        }
    }

    private static void ApplyZoomAndRotation(WebView2 webView, ScreenConfig screen)
    {
        var targetZoom = Math.Clamp(screen.ZoomPercent / 100.0, 0.25, 5.0);
        webView.ZoomFactor = targetZoom;

        webView.LayoutTransform = screen.RotationDegrees switch
        {
            90 => new RotateTransform(90),
            180 => new RotateTransform(180),
            270 => new RotateTransform(270),
            _ => Transform.Identity
        };
    }

    private void ApplyDisplayPlacement(DisplayInfo display)
    {
        Left = display.X;
        Top = display.Y;
        Width = display.Width;
        Height = display.Height;
        WindowState = WindowState.Normal;

        _logger.Information(
            "Kiosk window placement applied. DisplayKey: {DisplayKey}, X: {X}, Y: {Y}, Width: {Width}, Height: {Height}",
            display.StableKey,
            display.X,
            display.Y,
            display.Width,
            display.Height);
    }

    private void ConfigureWebViewSettings(CoreWebView2Settings settings)
    {
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDevToolsEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;

        _logger.Information("WebView2 settings applied: context-menu/devtools/accelerators/statusbar/zoom-control disabled.");
    }

    private void HookWebViewEvents(CoreWebView2 core)
    {
        core.ProcessFailed += OnWebViewProcessFailed;
        core.NavigationCompleted += OnNavigationCompleted;
        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;
        core.PermissionRequested += OnPermissionRequested;
        core.ServerCertificateErrorDetected += OnServerCertificateErrorDetected;
        core.WebMessageReceived += OnWebMessageReceived;
    }

    private void UnhookWebViewEvents(CoreWebView2 core)
    {
        core.ProcessFailed -= OnWebViewProcessFailed;
        core.NavigationCompleted -= OnNavigationCompleted;
        core.NavigationStarting -= OnNavigationStarting;
        core.NewWindowRequested -= OnNewWindowRequested;
        core.PermissionRequested -= OnPermissionRequested;
        core.ServerCertificateErrorDetected -= OnServerCertificateErrorDetected;
        core.WebMessageReceived -= OnWebMessageReceived;
    }

    private async void OnWebViewProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        _processFailureCount++;
        _logger.Warning(
            "WebView2 process failure detected. ScreenId: {ScreenId}, Kind: {FailureKind}, Reason: {Reason}, Count: {Count}.",
            _activeScreen?.Id,
            e.ProcessFailedKind,
            e.Reason,
            _processFailureCount);

        if (_activeScreen is null)
        {
            return;
        }

        var snapshot = _healthMonitor.RecordFailure(
            _activeScreen.Id,
            ScreenFailureType.ProcessCrash,
            $"{e.ProcessFailedKind}: {e.Reason}",
            _clock.UtcNow);

        ShowRecoveryOverlay(snapshot, "Browser process crashed. Waiting for recovery window.");
        await AttemptRecoveryIfDueAsync("process-failed");
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_activeScreen is null)
        {
            return;
        }

        if (e.IsSuccess)
        {
            var source = _webView?.CoreWebView2?.Source ?? _activeScreen.Url;
            var now = _clock.UtcNow;
            _healthMonitor.RecordNavigationSuccess(_activeScreen.Id, source, now);
            _lastSuccessfulConnectionUtc = now;
            _lastFailureCause = ConnectivityFailureCause.None;
            _ = PersistLastSuccessfulConnectionAsync(_activeScreen.Id, now);
            HideFallbackOverlay();

            // Re-assert the configured zoom: WebView2 can reset ZoomFactor across navigations.
            if (_webView is not null)
            {
                _webView.ZoomFactor = Math.Clamp(_activeScreen.ZoomPercent / 100.0, 0.25, 5.0);
            }

            _logger.Information(
                "Navigation completed successfully. ScreenId: {ScreenId}, Source: {Source}, HttpStatusCode: {HttpStatusCode}",
                _activeScreen.Id,
                source,
                e.HttpStatusCode);
            return;
        }

        var snapshot = _healthMonitor.RecordFailure(
            _activeScreen.Id,
            ScreenFailureType.Navigation,
            $"{e.WebErrorStatus} ({e.HttpStatusCode})",
            _clock.UtcNow);

        ShowRecoveryOverlay(snapshot, "Navigation failed. Applying backoff retry.");

        _logger.Warning(
            "Navigation failed. ScreenId: {ScreenId}, Source: {Source}, WebErrorStatus: {WebErrorStatus}, HttpStatusCode: {HttpStatusCode}",
            _activeScreen.Id,
            _webView?.CoreWebView2?.Source,
            e.WebErrorStatus,
            e.HttpStatusCode);
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (_activeScreen is null)
        {
            return;
        }

        var decision = KioskSecurityPolicy.EvaluateNavigation(_activeScreen, e.Uri);
        if (decision.Allow)
        {
            return;
        }

        e.Cancel = true;
        _logger.Warning(
            "Blocked navigation by policy. ScreenId: {ScreenId}, Source: {Source}, Reason: {Reason}",
            _activeScreen.Id,
            e.Uri,
            decision.Reason);
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (_activeScreen is null)
        {
            e.Handled = true;
            _logger.Warning("Blocked popup because no active screen is bound. Source: {Source}", e.Uri);
            return;
        }

        var action = KioskSecurityPolicy.EvaluatePopupPolicy(_activeScreen);
        switch (action)
        {
            case PopupAction.Block:
                e.Handled = true;
                _logger.Warning("Blocked popup by policy. ScreenId: {ScreenId}, Source: {Source}", _activeScreen.Id, e.Uri);
                break;

            case PopupAction.RedirectSameWindow:
                e.Handled = true;
                if (_webView?.CoreWebView2 is not null && !string.IsNullOrWhiteSpace(e.Uri))
                {
                    _webView.CoreWebView2.Navigate(e.Uri);
                    _logger.Information(
                        "Popup redirected into same window. ScreenId: {ScreenId}, Source: {Source}",
                        _activeScreen.Id,
                        e.Uri);
                }
                break;

            case PopupAction.AllowNewWindow:
                e.Handled = false;
                _logger.Information("Popup allowed by policy. ScreenId: {ScreenId}, Source: {Source}", _activeScreen.Id, e.Uri);
                break;
        }
    }

    private void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        var allow = _activeScreen?.AutoGrantPermissions == true;

        e.State = allow ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny;
        e.Handled = true;

        _logger.Information(
            "Permission request handled. ScreenId: {ScreenId}, Kind: {PermissionKind}, Origin: {Origin}, Decision: {Decision}",
            _activeScreen?.Id,
            e.PermissionKind,
            e.Uri,
            allow ? "Allow" : "Deny");
    }

    private void OnServerCertificateErrorDetected(object? sender, CoreWebView2ServerCertificateErrorDetectedEventArgs e)
    {
        var allow = _activeScreen?.AllowInsecureCertificates == true;

        e.Action = allow
            ? CoreWebView2ServerCertificateErrorAction.AlwaysAllow
            : CoreWebView2ServerCertificateErrorAction.Cancel;

        _logger.Warning(
            "Server certificate policy applied. ScreenId: {ScreenId}, Uri: {Uri}, Error: {Error}, Decision: {Decision}",
            _activeScreen?.Id,
            e.RequestUri,
            e.ErrorStatus,
            allow ? "AlwaysAllow" : "Cancel");
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_activeScreen is null)
        {
            return;
        }

        try
        {
            using var json = JsonDocument.Parse(e.WebMessageAsJson);
            if (!json.RootElement.TryGetProperty("type", out var typeElement))
            {
                return;
            }

            var messageType = typeElement.GetString();
            if (string.Equals(messageType, "heartbeat", StringComparison.OrdinalIgnoreCase))
            {
                _healthMonitor.RecordHeartbeat(_activeScreen.Id, _clock.UtcNow);
                return;
            }

            if (string.Equals(messageType, "admin-gesture", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Information("Admin corner gesture detected via WebView. ScreenId: {ScreenId}", _activeScreen.Id);
                _ = OpenAdminSettingsAsync("corner-gesture");
                return;
            }
        }
        catch (JsonException)
        {
            // Ignore non-JSON payloads from host pages.
        }
    }

    private async void OnHealthTick(object? sender, EventArgs e)
    {
        if (_activeScreen is null)
        {
            return;
        }

        var now = _clock.UtcNow;
        var heartbeatSnapshot = _healthMonitor.EvaluateHeartbeatTimeout(_activeScreen.Id, now);
        if (heartbeatSnapshot.State is ScreenHealthState.Recovering or ScreenHealthState.Offline)
        {
            ShowRecoveryOverlay(heartbeatSnapshot, "Heartbeat missing. Waiting for recovery retry.");
        }

        if (_memoryWatchdogThresholdMb > 0)
        {
            var workingSetMb = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
            if (workingSetMb >= _memoryWatchdogThresholdMb)
            {
                var memorySnapshot = _healthMonitor.RecordMemoryPressure(
                    _activeScreen.Id,
                    workingSetMb,
                    _memoryWatchdogThresholdMb,
                    now);

                ShowRecoveryOverlay(memorySnapshot, "Memory watchdog triggered. Scheduling controlled reload.");
            }
        }

        await AttemptRecoveryIfDueAsync("health-tick");
    }

    private void OnReloadIntervalTick(object? sender, EventArgs e)
    {
        if (_activeScreen is null || _webView?.CoreWebView2 is null)
        {
            return;
        }

        var snapshot = _healthMonitor.GetSnapshot(_activeScreen.Id);
        if (snapshot.State != ScreenHealthState.Online)
        {
            _logger.Debug(
                "Skipped periodic reload for screen {ScreenId} because state is {State}.",
                _activeScreen.Id,
                snapshot.State);
            return;
        }

        _webView.CoreWebView2.Navigate(_activeScreen.Url);
        _logger.Information(
            "Periodic reload triggered. ScreenId: {ScreenId}, IntervalSec: {IntervalSec}",
            _activeScreen.Id,
            _activeScreen.ReloadIntervalSec);
    }

    private void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnConfigChanged(sender, e));
            return;
        }

        _healthMonitor.ApplySettings(
            e.NewConfig.General.RetryBackoffSec,
            e.NewConfig.General.OfflineTimeoutSec,
            e.NewConfig.General.OfflineTimeoutSec);

        _memoryWatchdogThresholdMb = ResolveMemoryWatchdogThresholdMb();

        if (_activeScreen is null)
        {
            return;
        }

        var updatedScreen = e.NewConfig.Screens.FirstOrDefault(s =>
            s.Id.Equals(_activeScreen.Id, StringComparison.OrdinalIgnoreCase));

        if (updatedScreen is null)
        {
            return;
        }

        var previousUrl = _activeScreen.Url;
        _activeScreen = updatedScreen;
        ConfigureReloadInterval(updatedScreen);

        // Apply zoom/rotation changes immediately from this handler too,
        // so we don't rely solely on the supervisor's reconcile timing.
        if (_webView is not null)
        {
            ApplyZoomAndRotation(_webView, updatedScreen);

            if (!string.Equals(previousUrl, updatedScreen.Url, StringComparison.Ordinal))
            {
                _webView.CoreWebView2?.Navigate(updatedScreen.Url);
            }
        }
    }

    private async Task AttemptRecoveryIfDueAsync(string reason)
    {
        if (_activeScreen is null)
        {
            return;
        }

        if (!_healthMonitor.ShouldAttemptRecovery(_activeScreen.Id, _clock.UtcNow))
        {
            return;
        }

        await AttemptRecoveryAsync(reason);
    }

    private async Task AttemptRecoveryAsync(string reason)
    {
        if (_activeScreen is null || _recoveryInProgress)
        {
            return;
        }

        _recoveryInProgress = true;

        try
        {
            var now = _clock.UtcNow;
            var connectivity = await ProbeConnectivityAsync(_activeScreen.Url);
            var connectivitySnapshot = _healthMonitor.RecordConnectivity(
                _activeScreen.Id,
                connectivity.InternetReachable && connectivity.OriginReachable,
                now,
                connectivity.Message);

            _lastFailureCause = connectivity.FailureCause;

            if (!connectivity.InternetReachable || !connectivity.OriginReachable)
            {
                ShowRecoveryOverlay(connectivitySnapshot, connectivity.Message + " Holding on fallback with slow probe.");
                return;
            }

            if (_webView?.CoreWebView2 is null)
            {
                await InitializeOrReinitializeWebViewAsync();
            }
            else
            {
                _webView.CoreWebView2.Navigate(_activeScreen.Url);
            }

            _logger.Information(
                "Recovery attempt executed. ScreenId: {ScreenId}, Reason: {Reason}",
                _activeScreen.Id,
                reason);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Recovery attempt failed. Reason: {Reason}", reason);
            var snapshot = _healthMonitor.RecordFailure(
                _activeScreen.Id,
                ScreenFailureType.Manual,
                $"Recovery execution failed ({reason})",
                _clock.UtcNow);
            ShowRecoveryOverlay(snapshot, "Recovery attempt failed. Backing off.");
        }
        finally
        {
            _recoveryInProgress = false;
        }
    }

    private async Task<ConnectivityStatus> ProbeConnectivityAsync(string url)
    {
        if (Environment.GetEnvironmentVariable("ORDERBUDDY_FORCE_INTERNET_DOWN") == "1")
        {
            return ConnectivityClassifier.Evaluate(internetReachable: false, originReachable: false, captivePortalDetected: false);
        }

        var internetProbe = await ProbeInternetAsync();

        if (!internetProbe.InternetReachable)
        {
            return ConnectivityClassifier.Evaluate(
                internetReachable: false,
                originReachable: false,
                captivePortalDetected: internetProbe.CaptivePortalDetected);
        }

        if (Environment.GetEnvironmentVariable("ORDERBUDDY_FORCE_ORIGIN_DOWN") == "1")
        {
            return ConnectivityClassifier.Evaluate(internetReachable: true, originReachable: false, captivePortalDetected: false);
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var targetUri))
        {
            return ConnectivityClassifier.Evaluate(internetReachable: true, originReachable: false, captivePortalDetected: false);
        }

        var originReachable = await ProbeOriginAsync(targetUri);
        return ConnectivityClassifier.Evaluate(internetReachable: true, originReachable, captivePortalDetected: false);
    }

    private async Task<(bool InternetReachable, bool CaptivePortalDetected)> ProbeInternetAsync()
    {
        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            return (false, false);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "http://www.msftconnecttest.com/connecttest.txt");
            using var response = await _connectivityClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return (false, false);
            }

            var body = await response.Content.ReadAsStringAsync();
            var captive = !body.Contains("Microsoft Connect Test", StringComparison.OrdinalIgnoreCase);
            return (!captive, captive);
        }
        catch (HttpRequestException)
        {
            return (false, false);
        }
        catch (TaskCanceledException)
        {
            return (false, false);
        }
    }

    private async Task<bool> ProbeOriginAsync(Uri targetUri)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, targetUri);
            using var response = await _connectivityClient.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.MethodNotAllowed)
            {
                using var getRequest = new HttpRequestMessage(HttpMethod.Get, targetUri);
                using var getResponse = await _connectivityClient.SendAsync(getRequest);
                return getResponse.IsSuccessStatusCode;
            }

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    private void ShowRecoveryOverlay(ScreenHealthSnapshot snapshot, string message)
    {
        if (_activeScreen is null)
        {
            return;
        }

        var fallbackCauseText = _lastFailureCause switch
        {
            ConnectivityFailureCause.InternetDown => "Cause: internet connectivity unavailable.",
            ConnectivityFailureCause.OriginDown => "Cause: internet up, app endpoint unavailable.",
            ConnectivityFailureCause.CaptivePortal => "Cause: captive portal detected.",
            _ => "Cause: recovering browser session."
        };

        ShowFallbackOverlay(
            title: $"Recovering {_activeScreen.Name}",
            message,
            connectivityMessage: fallbackCauseText,
            nextAttempt: snapshot.NextRecoveryAttemptUtc,
            screen: _activeScreen);

        var now = _clock.UtcNow;
        if ((now - _lastHealthWarningUtc).TotalSeconds < 5)
        {
            return;
        }

        _lastHealthWarningUtc = now;
        _logger.Warning(
            "Screen health transition. ScreenId: {ScreenId}, State: {State}, ConsecutiveFailures: {ConsecutiveFailures}, NextAttemptUtc: {NextAttemptUtc}, Reason: {Reason}",
            snapshot.ScreenId,
            snapshot.State,
            snapshot.ConsecutiveFailures,
            snapshot.NextRecoveryAttemptUtc,
            snapshot.LastReason);
    }

    private void HideFallbackOverlay()
    {
        if (FallbackOverlay.Visibility == Visibility.Visible)
        {
            FallbackOverlay.Visibility = Visibility.Collapsed;
        }

        CustomFallbackImage.Visibility = Visibility.Collapsed;
        CustomFallbackHtmlBrowser.Visibility = Visibility.Collapsed;
    }

    private void ShowFallbackOverlay(
        string title,
        string message,
        string connectivityMessage,
        DateTimeOffset? nextAttempt,
        ScreenConfig screen)
    {
        var lastConnectedText = _lastSuccessfulConnectionUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "never";
        var retryText = nextAttempt?.ToLocalTime().ToString("HH:mm:ss") ?? "now";

        FallbackTitle.Text = title;
        FallbackSubtitle.Text = message;
        FallbackDeviceInfo.Text = $"Device: {_configService.CurrentConfig.Device.DeviceName} | Screen: {screen.Name}";
        FallbackConnectionCause.Text = connectivityMessage;
        FallbackLastConnected.Text = $"Last successful connection: {lastConnectedText}";
        FallbackRetryCountdown.Text = $"Next retry: {retryText}";
        FallbackOverlay.Visibility = Visibility.Visible;

        ApplyCustomFallback(screen);
    }

    private void ApplyCustomFallback(ScreenConfig screen)
    {
        CustomFallbackImage.Visibility = Visibility.Collapsed;
        CustomFallbackHtmlBrowser.Visibility = Visibility.Collapsed;

        var mode = screen.Fallback.Mode ?? "branded";
        if (mode.Equals("image", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(screen.Fallback.ImagePath) &&
            File.Exists(screen.Fallback.ImagePath))
        {
            try
            {
                CustomFallbackImage.Source = new BitmapImage(new Uri(screen.Fallback.ImagePath, UriKind.Absolute));
                CustomFallbackImage.Visibility = Visibility.Visible;
                return;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to load fallback image for screen {ScreenId}.", screen.Id);
            }
        }

        if (mode.Equals("customHtml", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(screen.Fallback.CustomHtmlPath) &&
            File.Exists(screen.Fallback.CustomHtmlPath))
        {
            try
            {
                var html = File.ReadAllText(screen.Fallback.CustomHtmlPath);
                CustomFallbackHtmlBrowser.NavigateToString(html);
                CustomFallbackHtmlBrowser.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to load fallback HTML for screen {ScreenId}.", screen.Id);
            }
        }
    }

    private async Task PersistLastSuccessfulConnectionAsync(string screenId, DateTimeOffset utcNow)
    {
        try
        {
            await _lastConnectionStore.SetLastSuccessUtcAsync(screenId, utcNow);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to persist last successful connection timestamp for screen {ScreenId}.", screenId);
        }
    }

    private void ConfigureReloadInterval(ScreenConfig screen)
    {
        if (screen.ReloadIntervalSec <= 0)
        {
            _reloadIntervalTimer.Stop();
            return;
        }

        _reloadIntervalTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(screen.ReloadIntervalSec, 5, 86_400));
        _reloadIntervalTimer.Start();

        _logger.Information(
            "Per-screen reload interval configured. ScreenId: {ScreenId}, IntervalSec: {IntervalSec}",
            screen.Id,
            screen.ReloadIntervalSec);
    }

    private static int ResolveMemoryWatchdogThresholdMb()
    {
        var raw = Environment.GetEnvironmentVariable("ORDERBUDDY_MEMORY_WATCHDOG_MB");
        if (!int.TryParse(raw, out var threshold))
        {
            return 0;
        }

        return Math.Clamp(threshold, 128, 16_384);
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        _logger.Warning("Blocked manual close attempt for kiosk window.");
    }

    private async void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F12 &&
            (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
            (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            e.Handled = true;
            await OpenAdminSettingsAsync("keyboard-shortcut");
        }
    }

    private async void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(this);
        if (!IsInCornerTapZone(point))
        {
            return;
        }

        await RegisterCornerTapAsync();
    }

    private async void OnAdminTapHotspotMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        await RegisterCornerTapAsync();
    }

    private async Task RegisterCornerTapAsync()
    {
        var now = DateTimeOffset.UtcNow;
        _cornerTapHistory.Enqueue(now);
        while (_cornerTapHistory.Count > 0 && (now - _cornerTapHistory.Peek()).TotalSeconds > 3)
        {
            _cornerTapHistory.Dequeue();
        }

        if (_cornerTapHistory.Count >= 5)
        {
            _cornerTapHistory.Clear();
            await OpenAdminSettingsAsync("corner-gesture");
        }
    }

    private bool IsInCornerTapZone(System.Windows.Point point)
    {
        return point.X >= ActualWidth - 140 && point.Y <= 140;
    }

    private Task OpenAdminSettingsAsync(string trigger)
    {
        return Dispatcher.InvokeAsync(() =>
        {
            // Temporarily suspend Topmost on ALL kiosk windows so the modal
            // dialogs (PIN pad, settings) are not hidden behind them.
            var supervisor = _services.GetService<KioskSupervisor>();
            supervisor?.SuspendTopmost();

            try
            {
                var pinWindow = ActivatorUtilities.CreateInstance<AdminPinWindow>(_services);
                pinWindow.Owner = this;
                pinWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                pinWindow.Topmost = true;
                var unlocked = pinWindow.ShowDialog() == true;
                if (!unlocked)
                {
                    _logger.Warning("Admin access denied. Trigger: {Trigger}", trigger);
                    return;
                }

                _logger.Information("Admin access granted. Trigger: {Trigger}", trigger);

                var settingsWindow = ActivatorUtilities.CreateInstance<AdminSettingsWindow>(_services);
                settingsWindow.Owner = this;
                settingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                settingsWindow.Topmost = true;
                settingsWindow.ShowDialog();
                _logger.Information("Admin settings closed. Trigger: {Trigger}", trigger);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to open admin settings. Trigger: {Trigger}", trigger);
            }
            finally
            {
                supervisor?.RestoreTopmost();
            }
        }).Task;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _healthTickTimer.Stop();
        _reloadIntervalTimer.Stop();
        _connectivityClient.Dispose();
        _configService.ConfigChanged -= OnConfigChanged;
        PreviewKeyDown -= OnPreviewKeyDown;
        PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        CleanupWebView();
    }

    private void CleanupWebView()
    {
        if (_webView is not null)
        {
            try
            {
                var core = _webView.CoreWebView2;
                if (core is not null)
                {
                    UnhookWebViewEvents(core);
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.Debug(ex, "Skipping CoreWebView2 unhook during cleanup because browser process already crashed.");
            }

            RootGrid.Children.Remove(_webView);
            try
            {
                _webView.Dispose();
            }
            catch (InvalidOperationException ex)
            {
                _logger.Debug(ex, "WebView2 dispose raised after crash; continuing cleanup.");
            }

            _webView = null;
        }
    }
}
