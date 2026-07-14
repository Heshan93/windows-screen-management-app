using System.Windows;
using OrderBuddy.ScreenManager.Core.Display;

namespace OrderBuddy.ScreenManager.App;

public partial class IdentifyOverlayWindow : Window
{
    public IdentifyOverlayWindow(DisplayInfo display, int number)
    {
        InitializeComponent();

        Left = display.X + Math.Max(20, (display.Width - Width) / 2);
        Top = display.Y + Math.Max(20, (display.Height - Height) / 2);

        DisplayNumberText.Text = number.ToString();
        DisplayNameText.Text = $"{display.DeviceName}{Environment.NewLine}{display.Width}x{display.Height}";
    }
}
