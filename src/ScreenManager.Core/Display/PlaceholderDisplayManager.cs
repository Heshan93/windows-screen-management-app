namespace OrderBuddy.ScreenManager.Core.Display;

public sealed class PlaceholderDisplayManager : IDisplayManager
{
    private readonly IReadOnlyList<DisplayInfo> _displays =
    [
        new DisplayInfo(
            StableKey: "display:primary:0,0:1920x1080",
            DeviceName: "DISPLAY1",
            X: 0,
            Y: 0,
            Width: 1920,
            Height: 1080,
            WorkAreaWidth: 1920,
            WorkAreaHeight: 1040,
            Orientation: "Landscape",
            DpiScaleX: 1.0,
            DpiScaleY: 1.0,
            IsPrimary: true)
    ];

    public event EventHandler<DisplaysChangedEventArgs>? DisplaysChanged;

    public IReadOnlyList<DisplayInfo> Enumerate() => _displays;

    public DisplayInfo? GetPrimaryDisplay() => _displays.FirstOrDefault(d => d.IsPrimary);

    public IReadOnlyList<string> GetConnectedDisplayNames()
    {
        return _displays.Select(d => d.DeviceName).ToList();
    }

    internal void RaiseChangedForTests()
    {
        DisplaysChanged?.Invoke(this, new DisplaysChangedEventArgs(_displays));
    }
}
