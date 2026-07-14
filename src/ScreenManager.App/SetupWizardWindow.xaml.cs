using System.Windows;
using OrderBuddy.ScreenManager.Core.Config;
using OrderBuddy.ScreenManager.Core.Display;

namespace OrderBuddy.ScreenManager.App;

public partial class SetupWizardWindow : Window
{
    private readonly IConfigService _configService;
    private readonly IDisplayManager _displayManager;

    private readonly (string Title, string Body)[] _steps =
    {
        ("Step 1 of 6 - Welcome", "Welcome to OrderBuddy setup."),
        ("Step 2 of 6 - Device", "Confirm device details and location."),
        ("Step 3 of 6 - Assign displays", "Assign Till, KDS, and Menu to physical screens."),
        ("Step 4 of 6 - URLs & options", "Enter URLs and optional rotation/zoom settings."),
        ("Step 5 of 6 - Review", "Review and apply configuration."),
        ("Step 6 of 6 - Lock", "Apply settings and return to kiosk mode.")
    };

    private int _stepIndex;

    public SetupWizardWindow(IConfigService configService, IDisplayManager displayManager)
    {
        _configService = configService;
        _displayManager = displayManager;
        InitializeComponent();
        RenderStep();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_stepIndex == 0)
        {
            return;
        }

        _stepIndex--;
        RenderStep();
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_stepIndex < _steps.Length - 1)
        {
            _stepIndex++;
            RenderStep();
            return;
        }

        var config = _configService.CurrentConfig;

        var displays = _displayManager.Enumerate().OrderBy(d => d.X).ThenBy(d => d.Y).ToList();
        var screens = new List<ScreenConfig>
        {
            new() { Id = "wizard-till", Name = "Till", Enabled = true, AppType = AppType.Till, Url = "https://example.org/till", DisplayKey = displays.ElementAtOrDefault(0)?.StableKey ?? string.Empty, DisplayNumberHint = 1 },
            new() { Id = "wizard-kds", Name = "Kitchen", Enabled = true, AppType = AppType.Kds, Url = "https://example.org/kds", DisplayKey = displays.ElementAtOrDefault(1)?.StableKey ?? string.Empty, DisplayNumberHint = 2 },
            new() { Id = "wizard-menu", Name = "Menu", Enabled = true, AppType = AppType.Menu, Url = "https://example.org/menu", DisplayKey = displays.ElementAtOrDefault(2)?.StableKey ?? string.Empty, DisplayNumberHint = 3 }
        };

        await _configService.SaveAsync(config with { Screens = screens });
        DialogResult = true;
        Close();
    }

    private void RenderStep()
    {
        StepTitle.Text = _steps[_stepIndex].Title;
        StepBody.Text = _steps[_stepIndex].Body;
        NextButton.Content = _stepIndex == _steps.Length - 1 ? "Finish" : "Next";
    }
}
