namespace OrderBuddy.ScreenManager.Core.Display;

public interface IDisplayManager
{
    event EventHandler<DisplaysChangedEventArgs>? DisplaysChanged;

    IReadOnlyList<DisplayInfo> Enumerate();

    DisplayInfo? GetPrimaryDisplay();

    IReadOnlyList<string> GetConnectedDisplayNames();
}

public sealed record DisplayInfo(
    string StableKey,
    string DeviceName,
    int X,
    int Y,
    int Width,
    int Height,
    int WorkAreaWidth,
    int WorkAreaHeight,
    string Orientation,
    double DpiScaleX,
    double DpiScaleY,
    bool IsPrimary);

public sealed class DisplaysChangedEventArgs : EventArgs
{
    public DisplaysChangedEventArgs(IReadOnlyList<DisplayInfo> displays)
    {
        Displays = displays;
    }

    public IReadOnlyList<DisplayInfo> Displays { get; }
}
