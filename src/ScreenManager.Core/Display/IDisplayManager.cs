namespace OrderBuddy.ScreenManager.Core.Display;

public interface IDisplayManager
{
    IReadOnlyList<string> GetConnectedDisplayNames();
}
