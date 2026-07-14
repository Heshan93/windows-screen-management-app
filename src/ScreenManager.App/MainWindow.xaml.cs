using System.Windows;
using OrderBuddy.ScreenManager.Core.Config;
using OrderBuddy.ScreenManager.Core.Display;
using OrderBuddy.ScreenManager.Core.Health;

namespace OrderBuddy.ScreenManager.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public string StatusText { get; }

    public MainWindow(IConfigService configService, IDisplayManager displayManager, IHealthMonitor healthMonitor)
    {
        InitializeComponent();

        StatusText = $"P0 bootstrap ready.\n" +
                     $"Config: {configService.GetType().Name}\n" +
                     $"Displays detected: {displayManager.GetConnectedDisplayNames().Count}\n" +
                     $"Health service: {healthMonitor.GetType().Name}";

        DataContext = this;
    }
}