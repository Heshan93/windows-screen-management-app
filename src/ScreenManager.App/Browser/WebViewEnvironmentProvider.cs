using Microsoft.Web.WebView2.Core;
using OrderBuddy.ScreenManager.Core.Config;
using Serilog;

namespace OrderBuddy.ScreenManager.App.Browser;

public sealed class WebViewEnvironmentProvider : IWebViewEnvironmentProvider
{
    private readonly KioskBrowserOptions _options;
    private readonly IConfigService _configService;
    private readonly Serilog.ILogger _logger;
    private readonly object _sync = new();
    private Task<CoreWebView2Environment>? _environmentTask;

    public WebViewEnvironmentProvider(KioskBrowserOptions options, IConfigService configService, Serilog.ILogger logger)
    {
        _options = options;
        _configService = configService;
        _logger = logger;
    }

    internal static string BuildArguments(string baseArguments, BrowserSecurityOverrides overrides)
    {
        var args = new List<string> { baseArguments };
        if (overrides.DisableWebSecurity)
        {
            args.Add("--disable-web-security");
        }

        if (overrides.IgnoreCertificateErrors)
        {
            args.Add("--ignore-certificate-errors");
        }

        return string.Join(' ', args.Where(a => !string.IsNullOrWhiteSpace(a)));
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
        var overrides = _configService.CurrentConfig.General.BrowserSecurityOverrides;
        var arguments = BuildArguments(_options.AdditionalBrowserArguments, overrides);

        var envOptions = new CoreWebView2EnvironmentOptions(arguments);
        var environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: _options.UserDataDirectory,
            options: envOptions);

        if (overrides.DisableWebSecurity || overrides.IgnoreCertificateErrors)
        {
            _logger.Warning(
                "Browser security overrides ACTIVE for all screens. DisableWebSecurity: {DisableWebSecurity}, IgnoreCertificateErrors: {IgnoreCertificateErrors}",
                overrides.DisableWebSecurity,
                overrides.IgnoreCertificateErrors);
        }

        _logger.Information(
            "WebView2 environment created. UserDataDirectory: {UserDataDirectory}, AdditionalArgs: {AdditionalArgs}",
            _options.UserDataDirectory,
            arguments);

        return environment;
    }
}
