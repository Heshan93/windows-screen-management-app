using OrderBuddy.ScreenManager.Core.Config;

namespace OrderBuddy.ScreenManager.Core.Display;

public sealed record ScreenDisplayBinding(ScreenConfig Screen, DisplayInfo? Display, bool IsWaitingForDisplay);

public static class DisplayBindingPlanner
{
    public static IReadOnlyList<ScreenDisplayBinding> Plan(
        IReadOnlyList<ScreenConfig> screens,
        IReadOnlyList<DisplayInfo> displays)
    {
        var enabledScreens = screens.Where(s => s.Enabled).ToList();
        var displayByKey = displays.ToDictionary(d => d.StableKey, StringComparer.OrdinalIgnoreCase);

        var result = new List<ScreenDisplayBinding>(enabledScreens.Count);

        foreach (var screen in enabledScreens)
        {
            if (!string.IsNullOrWhiteSpace(screen.DisplayKey) && displayByKey.TryGetValue(screen.DisplayKey, out var directMatch))
            {
                result.Add(new ScreenDisplayBinding(screen, directMatch, false));
                continue;
            }

            var hintIndex = Math.Max(0, screen.DisplayNumberHint - 1);
            if (hintIndex < displays.Count)
            {
                result.Add(new ScreenDisplayBinding(screen, displays[hintIndex], false));
                continue;
            }

            result.Add(new ScreenDisplayBinding(screen, null, true));
        }

        return result;
    }

    public static string BuildStableDisplayKey(string deviceName, int x, int y, int width, int height)
    {
        var normalizedDevice = string.IsNullOrWhiteSpace(deviceName)
            ? "unknown"
            : deviceName.Trim().ToLowerInvariant();

        return $"display:{normalizedDevice}:{x},{y}:{width}x{height}";
    }
}
