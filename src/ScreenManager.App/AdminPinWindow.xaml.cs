using System.Windows;
using System.Windows.Controls;
using OrderBuddy.ScreenManager.App.Security;
using OrderBuddy.ScreenManager.Core.Config;

namespace OrderBuddy.ScreenManager.App;

public partial class AdminPinWindow : Window
{
    private readonly IAdminAccessService _adminAccessService;
    private readonly IConfigService _configService;

    public AdminPinWindow(IAdminAccessService adminAccessService, IConfigService configService)
    {
        _adminAccessService = adminAccessService;
        _configService = configService;
        InitializeComponent();
    }

    private void Digit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Content: string value } && value.Length == 1)
        {
            PinBox.Password += value;
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        PinBox.Password = string.Empty;
    }

    private void Backspace_Click(object sender, RoutedEventArgs e)
    {
        if (PinBox.Password.Length > 0)
        {
            PinBox.Password = PinBox.Password[..^1];
        }
    }

    private void Unlock_Click(object sender, RoutedEventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        if (_adminAccessService.TryUnlock(PinBox.Password, _configService.CurrentConfig, now, out var retryAfter))
        {
            DialogResult = true;
            Close();
            return;
        }

        StatusText.Text = retryAfter is null
            ? "Invalid PIN"
            : $"Locked. Retry in {(int)retryAfter.Value.TotalSeconds}s";

        PinBox.Password = string.Empty;
    }
}
