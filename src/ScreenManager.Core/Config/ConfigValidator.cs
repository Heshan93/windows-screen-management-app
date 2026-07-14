namespace OrderBuddy.ScreenManager.Core.Config;

public sealed record ConfigValidationResult(AppConfig NormalizedConfig, IReadOnlyList<string> Diagnostics)
{
    public bool HasDiagnostics => Diagnostics.Count > 0;
}

internal static class ConfigValidator
{
    private static string? NormalizeHttpUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();

        // Already a valid absolute http(s) URL: keep the operator's exact text.
        if (Uri.TryCreate(value, UriKind.Absolute, out var direct) &&
            (direct.Scheme == Uri.UriSchemeHttp || direct.Scheme == Uri.UriSchemeHttps))
        {
            return value;
        }

        // Friendly normalization for scheme-less input like "mypos.local/app" or "localhost:8080".
        // Only treat it as a URL if it actually looks like a host (contains a dot or is localhost),
        // so genuinely invalid text such as "not-a-url" is still rejected.
        if (!value.Contains("://", StringComparison.Ordinal))
        {
            var hostPart = value.Split('/', '?', '#')[0];
            var looksLikeHost = hostPart.Contains('.', StringComparison.Ordinal)
                || hostPart.StartsWith("localhost", StringComparison.OrdinalIgnoreCase);

            if (looksLikeHost &&
                Uri.TryCreate("https://" + value, UriKind.Absolute, out var withHttps) &&
                (withHttps.Scheme == Uri.UriSchemeHttp || withHttps.Scheme == Uri.UriSchemeHttps))
            {
                return "https://" + value;
            }
        }

        return null;
    }

    public static ConfigValidationResult Normalize(AppConfig input)
    {
        var diagnostics = new List<string>();

        var device = input.Device ?? new DeviceInfo();
        var general = input.General ?? new GeneralSettings();

        var retryBackoff = (general.RetryBackoffSec ?? [5, 10, 30, 60])
            .Where(v => v >= 1 && v <= 600)
            .Distinct()
            .OrderBy(v => v)
            .ToList();
        if (retryBackoff.Count == 0)
        {
            retryBackoff = [5, 10, 30, 60];
            diagnostics.Add("general.retryBackoffSec was invalid and reset to defaults.");
        }

        var offlineTimeoutSec = general.OfflineTimeoutSec;
        if (offlineTimeoutSec < 5 || offlineTimeoutSec > 3600)
        {
            offlineTimeoutSec = 20;
            diagnostics.Add("general.offlineTimeoutSec out of range and reset to 20.");
        }

        var screens = new List<ScreenConfig>();
        foreach (var screen in input.Screens ?? [])
        {
            var fixedScreen = screen;

            var allowedDomains = (screen.AllowedDomains ?? [])
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            fixedScreen = fixedScreen with
            {
                AllowedDomains = allowedDomains
            };

            var normalizedUrl = NormalizeHttpUrl(screen.Url);
            if (normalizedUrl is null)
            {
                fixedScreen = fixedScreen with { Url = "https://example.org" };
                diagnostics.Add($"screens[{screen.Id}].url invalid; set to https://example.org.");
            }
            else if (!string.Equals(normalizedUrl, screen.Url, StringComparison.Ordinal))
            {
                fixedScreen = fixedScreen with { Url = normalizedUrl };
                diagnostics.Add($"screens[{screen.Id}].url normalized to {normalizedUrl}.");
            }

            var zoom = screen.ZoomPercent;
            if (zoom < 25 || zoom > 500)
            {
                fixedScreen = fixedScreen with { ZoomPercent = 100 };
                diagnostics.Add($"screens[{screen.Id}].zoomPercent out of range; set to 100.");
            }

            var rotation = screen.RotationDegrees;
            if (rotation is not (0 or 90 or 180 or 270))
            {
                fixedScreen = fixedScreen with { RotationDegrees = 0 };
                diagnostics.Add($"screens[{screen.Id}].rotationDegrees invalid; set to 0.");
            }

            screens.Add(fixedScreen);
        }

        var normalized = input with
        {
            SchemaVersion = AppConfig.CurrentSchemaVersion,
            Device = device,
            General = general with
            {
                RetryBackoffSec = retryBackoff,
                OfflineTimeoutSec = offlineTimeoutSec
            },
            Screens = screens
        };

        return new ConfigValidationResult(normalized, diagnostics);
    }
}
