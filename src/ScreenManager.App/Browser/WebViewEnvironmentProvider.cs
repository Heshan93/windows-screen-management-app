using Microsoft.Web.WebView2.Core;
using Serilog;

namespace OrderBuddy.ScreenManager.App.Browser;

public sealed class WebViewEnvironmentProvider : IWebViewEnvironmentProvider
{
    private readonly KioskBrowserOptions _options;
    private readonly Serilog.ILogger _logger;
    private readonly object _sync = new();
    private Task<CoreWebView2Environment>? _environmentTask;

    public WebViewEnvironmentProvider(KioskBrowserOptions options, Serilog.ILogger logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<CoreWebView2Environment> GetEnvironmentAsync()
    {
        lock (_sync)
        {
            _environmentTask ??= CreateEnvironmentAsync();
            return _environmentTask;
        }
    }

    private async Task<CoreWebView2Environment> CreateEnvironmentAsync()
    {
        var envOptions = new CoreWebView2EnvironmentOptions(_options.AdditionalBrowserArguments);
        var environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: _options.UserDataDirectory,
            options: envOptions);

        _logger.Information(
            "WebView2 environment created. UserDataDirectory: {UserDataDirectory}, AdditionalArgs: {AdditionalArgs}",
            _options.UserDataDirectory,
            _options.AdditionalBrowserArguments);

        return environment;
    }
}
