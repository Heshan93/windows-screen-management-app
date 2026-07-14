namespace OrderBuddy.ScreenManager.Core.Display;

public sealed class PlaceholderDisplayManager : IDisplayManager
{
    public IReadOnlyList<string> GetConnectedDisplayNames()
    {
        return new[] { "Display 1" };
    }
}
