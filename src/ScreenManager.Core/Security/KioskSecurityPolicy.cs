using OrderBuddy.ScreenManager.Core.Config;

namespace OrderBuddy.ScreenManager.Core.Security;

public enum PopupAction
{
    Block,
    RedirectSameWindow,
    AllowNewWindow
}

public sealed record NavigationDecision(bool Allow, string Reason);

public static class KioskSecurityPolicy
{
    public static NavigationDecision EvaluateNavigation(ScreenConfig screen, string? targetUrl)
    {
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            return new NavigationDecision(false, "Empty URL");
        }

        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            return new NavigationDecision(false, "Invalid URL");
        }

        if (uri.Scheme.Equals("about", StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase))
        {
            return new NavigationDecision(false, "Blocked scheme");
        }

        if (screen.AllowedDomains.Count == 0)
        {
            return new NavigationDecision(true, "No domain allow-list configured");
        }

        var host = uri.Host.ToLowerInvariant();
        var allowed = screen.AllowedDomains.Any(domain =>
            host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase));

        return allowed
            ? new NavigationDecision(true, "Domain allowed")
            : new NavigationDecision(false, "Domain not in allow-list");
    }

    public static PopupAction EvaluatePopupPolicy(ScreenConfig screen)
    {
        if (screen.AllowPopups)
        {
            return PopupAction.AllowNewWindow;
        }

        return screen.RedirectPopupsToSameWindow
            ? PopupAction.RedirectSameWindow
            : PopupAction.Block;
    }
}
