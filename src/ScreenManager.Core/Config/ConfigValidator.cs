namespace OrderBuddy.ScreenManager.Core.Config;

public sealed record ConfigValidationResult(AppConfig NormalizedConfig, IReadOnlyList<string> Diagnostics)
{
    public bool HasDiagnostics => Diagnostics.Count > 0;
}

internal static class ConfigValidator
{
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

            if (!Uri.TryCreate(screen.Url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                fixedScreen = fixedScreen with { Url = "https://example.org" };
                diagnostics.Add($"screens[{screen.Id}].url invalid; set to https://example.org.");
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
