using System.IO;

namespace OrderBuddy.ScreenManager.App.Browser;

public sealed record KioskBrowserOptions(string StartUrl, string UserDataDirectory, string AdditionalBrowserArguments)
{
    public static KioskBrowserOptions Create(string programDataRoot)
    {
        var userDataDirectory = Path.Combine(programDataRoot, "webview2");
        Directory.CreateDirectory(userDataDirectory);

        return new KioskBrowserOptions(
            StartUrl: "https://example.org",
            UserDataDirectory: userDataDirectory,
            AdditionalBrowserArguments: "--autoplay-policy=no-user-gesture-required");
    }
}
