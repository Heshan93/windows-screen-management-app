using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using OrderBuddy.ScreenManager.App.Browser;
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
    private readonly KioskBrowserOptions _browserOptions;
    private readonly IWebViewEnvironmentProvider _environmentProvider;
    private readonly Serilog.ILogger _logger;
    private readonly Stopwatch _coldStartStopwatch = new();

    private WebView2? _webView;
    private int _processFailureCount;
    private ScreenConfig? _activeScreen;
    private DisplayInfo? _activeDisplay;
    private bool _isLoaded;
    private bool _allowClose;

    public KioskWindow(
        KioskBrowserOptions browserOptions,
        IWebViewEnvironmentProvider environmentProvider,
        Serilog.ILogger logger,
        IConfigService configService,
        IDisplayManager displayManager,
        IHealthMonitor healthMonitor)
    {
        _browserOptions = browserOptions;
        _environmentProvider = environmentProvider;
        _logger = logger;

        // Keep these dependencies in the constructor to prove P0 DI contracts remain valid.
        _ = configService;
        _ = displayManager;
        _ = healthMonitor;

        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
        Closing += OnClosing;
    }

    public void RequestClose()
    {
        _allowClose = true;
        Close();
    }

    public void ApplyBinding(ScreenConfig screen, DisplayInfo display)
    {
        _activeScreen = screen;
        _activeDisplay = display;
        WaitingOverlay.Visibility = Visibility.Collapsed;

        ApplyDisplayPlacement(display);

        if (_isLoaded)
        {
            _ = InitializeOrReinitializeWebViewAsync();
        }
    }

    public void ShowWaitingForDisplay(ScreenConfig screen, DisplayInfo fallbackDisplay)
    {
        _activeScreen = screen;
        _activeDisplay = fallbackDisplay;

        ApplyDisplayPlacement(fallbackDisplay);
        CleanupWebView();

        WaitingTitle.Text = $"Waiting for assigned display ({screen.Name})";
        WaitingSubtitle.Text = $"Display key '{screen.DisplayKey}' not detected. Reconnect the monitor to auto-start.";
        WaitingOverlay.Visibility = Visibility.Visible;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;

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
            WaitingOverlay.Visibility = Visibility.Collapsed;

            var webView = new WebView2();
            RootGrid.Children.Add(webView);
            _webView = webView;

            var environment = await _environmentProvider.GetEnvironmentAsync();
            await webView.EnsureCoreWebView2Async(environment);

            ConfigureWebViewSettings(webView.CoreWebView2.Settings);
            HookWebViewEvents(webView.CoreWebView2);

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
        }
    }

    private static void ApplyZoomAndRotation(WebView2 webView, ScreenConfig screen)
    {
        webView.ZoomFactor = Math.Clamp(screen.ZoomPercent / 100.0, 0.25, 5.0);

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
    }

    private void UnhookWebViewEvents(CoreWebView2 core)
    {
        core.ProcessFailed -= OnWebViewProcessFailed;
        core.NavigationCompleted -= OnNavigationCompleted;
        core.NavigationStarting -= OnNavigationStarting;
        core.NewWindowRequested -= OnNewWindowRequested;
        core.PermissionRequested -= OnPermissionRequested;
        core.ServerCertificateErrorDetected -= OnServerCertificateErrorDetected;
    }

    private async void OnWebViewProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        _processFailureCount++;
        _logger.Warning(
            "WebView2 process failure detected. ScreenId: {ScreenId}, Kind: {FailureKind}, Reason: {Reason}, Count: {Count}. Reinitializing.",
            _activeScreen?.Id,
            e.ProcessFailedKind,
            e.Reason,
            _processFailureCount);

        await InitializeOrReinitializeWebViewAsync();
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            _logger.Information(
                "Navigation completed successfully. Source: {Source}, HttpStatusCode: {HttpStatusCode}",
                _webView?.CoreWebView2?.Source,
                e.HttpStatusCode);
            return;
        }

        _logger.Warning(
            "Navigation failed. Source: {Source}, WebErrorStatus: {WebErrorStatus}, HttpStatusCode: {HttpStatusCode}",
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

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        _logger.Warning("Blocked manual close attempt for kiosk window.");
    }

    private void OnClosed(object? sender, EventArgs e)
    {
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
