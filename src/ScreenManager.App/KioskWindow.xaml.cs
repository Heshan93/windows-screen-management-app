using System.Diagnostics;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using OrderBuddy.ScreenManager.App.Browser;
using OrderBuddy.ScreenManager.Core.Config;
using OrderBuddy.ScreenManager.Core.Display;
using OrderBuddy.ScreenManager.Core.Health;
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
    private readonly Stopwatch _coldStartStopwatch = Stopwatch.StartNew();

    private WebView2? _webView;
    private int _processFailureCount;

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
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyPrimaryDisplayFullscreen();
        await InitializeOrReinitializeWebViewAsync();
    }

    private async Task InitializeOrReinitializeWebViewAsync()
    {
        try
        {
            CleanupWebView();

            var webView = new WebView2();
            RootGrid.Children.Add(webView);
            _webView = webView;

            var environment = await _environmentProvider.GetEnvironmentAsync();
            await webView.EnsureCoreWebView2Async(environment);

            ConfigureWebViewSettings(webView.CoreWebView2.Settings);
            HookWebViewEvents(webView.CoreWebView2);

            webView.CoreWebView2.Navigate(_browserOptions.StartUrl);

            _logger.Information(
                "Kiosk WebView2 initialized. StartUrl: {StartUrl}, ColdStartMs: {ColdStartMs}",
                _browserOptions.StartUrl,
                _coldStartStopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize kiosk WebView2.");
        }
    }

    private void ApplyPrimaryDisplayFullscreen()
    {
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
        WindowState = WindowState.Normal;

        _logger.Information(
            "Kiosk window fullscreen applied on primary monitor. Width: {Width}, Height: {Height}",
            Width,
            Height);
    }

    private void ConfigureWebViewSettings(CoreWebView2Settings settings)
    {
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDevToolsEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;

        _logger.Information("WebView2 settings applied: context-menu/devtools/statusbar/zoom-control disabled.");
    }

    private void HookWebViewEvents(CoreWebView2 core)
    {
        core.ProcessFailed += OnWebViewProcessFailed;
        core.NavigationCompleted += OnNavigationCompleted;
    }

    private void UnhookWebViewEvents(CoreWebView2 core)
    {
        core.ProcessFailed -= OnWebViewProcessFailed;
        core.NavigationCompleted -= OnNavigationCompleted;
    }

    private async void OnWebViewProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        _processFailureCount++;
        _logger.Warning(
            "WebView2 process failure detected. Kind: {FailureKind}, Reason: {Reason}, Count: {Count}. Reinitializing.",
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
