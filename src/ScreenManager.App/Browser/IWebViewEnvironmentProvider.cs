using Microsoft.Web.WebView2.Core;

namespace OrderBuddy.ScreenManager.App.Browser;

public interface IWebViewEnvironmentProvider
{
    Task<CoreWebView2Environment> GetEnvironmentAsync();
}
