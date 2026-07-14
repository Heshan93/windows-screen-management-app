using Microsoft.Win32;
using OrderBuddy.ScreenManager.Core.Display;
using Serilog;
using FormsScreen = System.Windows.Forms.Screen;

namespace OrderBuddy.ScreenManager.App.Display;

public sealed class WindowsDisplayManager : IDisplayManager, IDisposable
{
    private readonly Serilog.ILogger _logger;
    private bool _disposed;

    public WindowsDisplayManager(Serilog.ILogger logger)
    {
        _logger = logger;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public event EventHandler<DisplaysChangedEventArgs>? DisplaysChanged;

    public IReadOnlyList<DisplayInfo> Enumerate()
    {
        var displays = FormsScreen.AllScreens
            .Select(ToDisplayInfo)
            .OrderBy(d => d.X)
            .ThenBy(d => d.Y)
            .ToList();

        return displays;
    }

    public DisplayInfo? GetPrimaryDisplay()
    {
        return Enumerate().FirstOrDefault(d => d.IsPrimary);
    }

    public IReadOnlyList<string> GetConnectedDisplayNames()
    {
        return Enumerate().Select(d => d.DeviceName).ToList();
    }

    private DisplayInfo ToDisplayInfo(FormsScreen screen)
    {
        var bounds = screen.Bounds;
        var workArea = screen.WorkingArea;

        var orientation = bounds.Width >= bounds.Height ? "Landscape" : "Portrait";
        var stableKey = DisplayBindingPlanner.BuildStableDisplayKey(
            screen.DeviceName,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height);

        return new DisplayInfo(
            StableKey: stableKey,
            DeviceName: screen.DeviceName,
            X: bounds.X,
            Y: bounds.Y,
            Width: bounds.Width,
            Height: bounds.Height,
            WorkAreaWidth: workArea.Width,
            WorkAreaHeight: workArea.Height,
            Orientation: orientation,
            DpiScaleX: 1.0,
            DpiScaleY: 1.0,
            IsPrimary: screen.Primary);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        var displays = Enumerate();
        _logger.Information("Display settings changed. Connected displays: {Count}", displays.Count);
        DisplaysChanged?.Invoke(this, new DisplaysChangedEventArgs(displays));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }
}
