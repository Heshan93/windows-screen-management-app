using System.Collections.ObjectModel;
using System.Windows;
using OrderBuddy.ScreenManager.Core.Config;
using OrderBuddy.ScreenManager.Core.Display;

namespace OrderBuddy.ScreenManager.App;

public partial class SetupWizardWindow : Window
{
    private readonly IConfigService _configService;
    private readonly IDisplayManager _displayManager;

    private readonly ObservableCollection<WizardScreenRow> _rows = new();

    public SetupWizardWindow(IConfigService configService, IDisplayManager displayManager)
    {
        _configService = configService;
        _displayManager = displayManager;
        InitializeComponent();
        BuildRows();
    }

    private void BuildRows()
    {
        var displays = _displayManager.Enumerate().OrderBy(d => d.X).ThenBy(d => d.Y).ToList();

        // Create exactly one configurable screen per physically-detected display.
        // A single-display kiosk gets a single screen (no phantom KDS/Menu entries).
        var defaults = new (string Name, AppType Type)[]
        {
            ("Till", AppType.Till),
            ("Kitchen", AppType.Kds),
            ("Menu", AppType.Menu),
            ("Collection", AppType.Collection),
            ("Promo", AppType.Promo),
            ("Screen", AppType.Custom)
        };

        for (var i = 0; i < displays.Count; i++)
        {
            var display = displays[i];
            var preset = defaults[Math.Min(i, defaults.Length - 1)];
            _rows.Add(new WizardScreenRow
            {
                Number = i + 1,
                DisplayKey = display.StableKey,
                DisplayLabel = $"Display {i + 1}: {display.DeviceName}  ({display.Width}x{display.Height} @ {display.X},{display.Y})"
                    + (display.IsPrimary ? "  [primary]" : string.Empty),
                Name = preset.Name,
                SelectedAppType = preset.Type,
                Url = string.Empty
            });
        }

        SummaryText.Text = displays.Count == 1
            ? "1 display detected. Configure the screen below."
            : $"{displays.Count} displays detected. Configure each screen below.";

        ScreenRows.ItemsSource = _rows;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void Finish_Click(object sender, RoutedEventArgs e)
    {
        if (_rows.Count == 0)
        {
            ValidationText.Text = "No displays detected.";
            return;
        }

        var screens = new List<ScreenConfig>();
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var url = NormalizeUrl(row.Url);
            screens.Add(new ScreenConfig
            {
                Id = $"screen-{i + 1}",
                Name = string.IsNullOrWhiteSpace(row.Name) ? $"Screen {i + 1}" : row.Name.Trim(),
                Enabled = true,
                AppType = row.SelectedAppType,
                Url = url,
                DisplayKey = row.DisplayKey,
                DisplayNumberHint = i + 1
            });
        }

        FinishButton.IsEnabled = false;
        ValidationText.Text = string.Empty;

        try
        {
            await _configService.SaveAsync(_configService.CurrentConfig with { Screens = screens });
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            FinishButton.IsEnabled = true;
            ValidationText.Text = "Could not save: " + ex.Message;
        }
    }

    private static string NormalizeUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            // Left blank -> a friendly placeholder the operator can replace later.
            return "https://example.org";
        }

        var value = raw.Trim();
        return value.Contains("://", StringComparison.Ordinal) ? value : "https://" + value;
    }
}

public sealed class WizardScreenRow
{
    public int Number { get; set; }
    public string DisplayKey { get; set; } = string.Empty;
    public string DisplayLabel { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public AppType SelectedAppType { get; set; } = AppType.Custom;

    public IReadOnlyList<AppType> AppTypes { get; } =
        Enum.GetValues<AppType>();
}

