using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using OrderBuddy.ScreenManager.App.ViewModels;
using OrderBuddy.ScreenManager.Core.Config;
using OrderBuddy.ScreenManager.Core.Display;
using OrderBuddy.ScreenManager.Core.Health;

namespace OrderBuddy.ScreenManager.App;

public partial class AdminSettingsWindow : Window
{
    private readonly AdminSettingsViewModel _vm;
    private readonly IServiceProvider _services;

    public AdminSettingsWindow(
        IConfigService configService,
        IDisplayManager displayManager,
        IHealthMonitor healthMonitor,
        KioskSupervisor kioskSupervisor,
        IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        _vm = new AdminSettingsViewModel(configService, displayManager, healthMonitor, kioskSupervisor);
        DataContext = _vm;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await _vm.SaveAsync();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void IdentifyScreens_Click(object sender, RoutedEventArgs e)
    {
        _vm.IdentifyScreens();
    }

    private async void TestUrl_Click(object sender, RoutedEventArgs e)
    {
        await _vm.TestSelectedUrlAsync();
    }

    private void ApplyPreset_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedScreen is null)
        {
            return;
        }

        _vm.ApplyPresetForSelectedScreen(_vm.SelectedScreen.AppType);
    }

    private void MarkDirty_TextChanged(object sender, TextChangedEventArgs e)
    {
        _vm.MarkDirty();
    }

    private void MarkDirty_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _vm.MarkDirty();
    }

    private void MarkDirty_Toggled(object sender, RoutedEventArgs e)
    {
        _vm.MarkDirty();
    }

    private void RefreshDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        _vm.RefreshDiagnostics();
        _vm.RefreshDisplayTiles();
    }

    private async void ConnectivityTest_Click(object sender, RoutedEventArgs e)
    {
        await _vm.RunConnectivityTestAsync();
    }

    private void RestartAll_Click(object sender, RoutedEventArgs e)
    {
        _vm.RestartAllScreens();
        _vm.RefreshDiagnostics();
    }

    private void ExportBundle_Click(object sender, RoutedEventArgs e)
    {
        var outputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "OrderBuddySupport");
        _vm.ExportSupportBundle(outputDir);
    }

    private void RunWizard_Click(object sender, RoutedEventArgs e)
    {
        var wizard = ActivatorUtilities.CreateInstance<SetupWizardWindow>(_services);
        wizard.Owner = this;
        wizard.ShowDialog();
        _vm.ReloadFromConfig();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_vm.HasUnsavedChanges)
        {
            var answer = System.Windows.MessageBox.Show(this, "Unsaved changes will be lost. Close anyway?", "Unsaved changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes)
            {
                e.Cancel = true;
            }
        }

        base.OnClosing(e);
    }
}
