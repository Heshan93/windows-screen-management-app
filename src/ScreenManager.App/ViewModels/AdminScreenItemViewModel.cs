using OrderBuddy.ScreenManager.Core.Config;

namespace OrderBuddy.ScreenManager.App.ViewModels;

public sealed class AdminScreenItemViewModel : ObservableObject
{
    private string _id = string.Empty;
    private bool _enabled = true;
    private string _name = string.Empty;
    private string _displayKey = string.Empty;
    private AppType _appType;
    private string _url = string.Empty;
    private int _rotationDegrees;
    private int _zoomPercent = 100;
    private int _reloadIntervalSec;
    private int _startupDelaySec;
    private bool _fullScreen = true;
    private string _fallbackMode = "branded";
    private string _fallbackImagePath = string.Empty;
    private string _fallbackHtmlPath = string.Empty;

    // Credentials (P13)
    private bool _autoLoginEnabled;
    private string _credentialUsername = string.Empty;
    private string _kioskId = string.Empty;

    /// <summary>
    /// Plain-text password as entered by the admin. Kept in memory only while the
    /// settings window is open; encrypted to EncryptedPassword on save.
    /// NEVER persisted or logged in plain text.
    /// </summary>
    public string PlainTextPassword { get; set; } = string.Empty;

    public string Id { get => _id; set => SetProperty(ref _id, value); }
    public bool Enabled { get => _enabled; set => SetProperty(ref _enabled, value); }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string DisplayKey { get => _displayKey; set => SetProperty(ref _displayKey, value); }
    public AppType AppType { get => _appType; set => SetProperty(ref _appType, value); }
    public string Url { get => _url; set => SetProperty(ref _url, value); }
    public int RotationDegrees { get => _rotationDegrees; set => SetProperty(ref _rotationDegrees, value); }
    public int ZoomPercent { get => _zoomPercent; set => SetProperty(ref _zoomPercent, value); }
    public int ReloadIntervalSec { get => _reloadIntervalSec; set => SetProperty(ref _reloadIntervalSec, value); }
    public int StartupDelaySec { get => _startupDelaySec; set => SetProperty(ref _startupDelaySec, value); }
    public bool FullScreen { get => _fullScreen; set => SetProperty(ref _fullScreen, value); }
    public string FallbackMode { get => _fallbackMode; set => SetProperty(ref _fallbackMode, value); }
    public string FallbackImagePath { get => _fallbackImagePath; set => SetProperty(ref _fallbackImagePath, value); }
    public string FallbackHtmlPath { get => _fallbackHtmlPath; set => SetProperty(ref _fallbackHtmlPath, value); }

    public bool AutoLoginEnabled { get => _autoLoginEnabled; set => SetProperty(ref _autoLoginEnabled, value); }
    public string CredentialUsername { get => _credentialUsername; set => SetProperty(ref _credentialUsername, value); }
    public string KioskId { get => _kioskId; set => SetProperty(ref _kioskId, value); }

    /// <summary>True when the screen already has an encrypted password stored in config.</summary>
    public bool HasStoredPassword { get; set; }

    public static AdminScreenItemViewModel FromConfig(ScreenConfig screen)
    {
        return new AdminScreenItemViewModel
        {
            Id = screen.Id,
            Enabled = screen.Enabled,
            Name = screen.Name,
            DisplayKey = screen.DisplayKey,
            AppType = screen.AppType,
            Url = screen.Url,
            RotationDegrees = screen.RotationDegrees,
            ZoomPercent = screen.ZoomPercent,
            ReloadIntervalSec = screen.ReloadIntervalSec,
            StartupDelaySec = screen.StartupDelaySec,
            FullScreen = screen.FullScreen,
            FallbackMode = screen.Fallback.Mode,
            FallbackImagePath = screen.Fallback.ImagePath,
            FallbackHtmlPath = screen.Fallback.CustomHtmlPath,
            AutoLoginEnabled = screen.Credentials.AutoLoginEnabled,
            CredentialUsername = screen.Credentials.Username,
            KioskId = screen.Credentials.KioskId,
            HasStoredPassword = !string.IsNullOrEmpty(screen.Credentials.EncryptedPassword),
            PlainTextPassword = string.Empty   // never populated from config
        };
    }

    public ScreenConfig ToConfig(ScreenConfig original)
    {
        // Determine the encrypted password to persist:
        // - if the admin typed a new password, encrypt it
        // - if they left it blank, keep the existing encrypted blob
        var encryptedPassword = string.IsNullOrEmpty(PlainTextPassword)
            ? original.Credentials.EncryptedPassword
            : OrderBuddy.ScreenManager.Core.Security.AdminCredentialsService.Protect(PlainTextPassword);

        return original with
        {
            Id = Id,
            Enabled = Enabled,
            Name = Name,
            DisplayKey = DisplayKey,
            AppType = AppType,
            Url = Url,
            RotationDegrees = RotationDegrees,
            ZoomPercent = ZoomPercent,
            ReloadIntervalSec = ReloadIntervalSec,
            StartupDelaySec = StartupDelaySec,
            FullScreen = FullScreen,
            Fallback = original.Fallback with
            {
                Mode = FallbackMode,
                ImagePath = FallbackImagePath,
                CustomHtmlPath = FallbackHtmlPath
            },
            Credentials = original.Credentials with
            {
                AutoLoginEnabled = AutoLoginEnabled,
                Username = CredentialUsername,
                KioskId = KioskId,
                EncryptedPassword = encryptedPassword
            }
        };
    }
}
