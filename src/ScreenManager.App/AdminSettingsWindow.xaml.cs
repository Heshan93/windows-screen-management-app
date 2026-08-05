using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using OrderBuddy.ScreenManager.App.Lifecycle;
using OrderBuddy.ScreenManager.App.ViewModels;
using OrderBuddy.ScreenManager.Core.Config;
using OrderBuddy.ScreenManager.Core.Display;
using OrderBuddy.ScreenManager.Core.Health;
using Serilog;

namespace OrderBuddy.ScreenManager.App;

public partial class AdminSettingsWindow : Window
{
    private readonly AdminSettingsViewModel _vm;
    private readonly IServiceProvider _services;
    private bool _saveInProgress;

    public AdminSettingsWindow(
        IConfigService configService,
        IDisplayManager displayManager,
        IHealthMonitor healthMonitor,
        KioskSupervisor kioskSupervisor,
        IAutoStartService autoStartService,
        IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        _vm = new AdminSettingsViewModel(configService, displayManager, healthMonitor, kioskSupervisor, autoStartService);
        DataContext = _vm;

        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Clear the PasswordBox whenever the selected screen changes to avoid
        // accidentally carrying a typed password over to a different screen.
        if (e.PropertyName == nameof(AdminSettingsViewModel.SelectedScreen))
        {
            if (CredentialPasswordBox is not null)
            {
                CredentialPasswordBox.Password = string.Empty;
            }
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_saveInProgress)
        {
            return;
        }

        _saveInProgress = true;
        // Force all delayed/pending WPF TextBox bindings to commit their current
        // text to the view-model BEFORE saving. Without this, a Delay=400 binding
        // may not have pushed the typed value yet, causing stale data to be saved.
        try
        {
            FlushPendingBindings();
            await _vm.SaveAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Admin settings save failed.");
            System.Windows.MessageBox.Show(this, "Save failed. Please check logs and try again.", "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _saveInProgress = false;
        }
    }

    /// <summary>
    /// Walks the visual tree and forces every TextBox whose binding has a pending
    /// Delay update to push its current text to the source immediately.
    /// </summary>
    private void FlushPendingBindings()
    {
        FlushBindingsRecursive(this);

        static void FlushBindingsRecursive(System.Windows.DependencyObject parent)
        {
            var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.TextBox textBox)
                {
                    var binding = textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                    binding?.UpdateSource();
                }

                FlushBindingsRecursive(child);
            }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ExitApp_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "Are you sure you want to exit OrderBuddy Screen Manager?\n\nAll kiosk screens will close. The app will restart automatically if auto-start is enabled.",
            "Exit Application",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            Close();
            System.Windows.Application.Current.Shutdown();
        }
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

    private async void ClearCacheNow_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            this,
            $"Clear cache now using scope '{_vm.CacheMaintenanceScope}'?" +
            (_vm.CacheMaintenanceScope == "cacheAndSession" ? "\n\nThis will log the kiosk out. Auto-login will re-authenticate if configured." : string.Empty),
            "Clear cache",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            await _vm.ClearCacheNowAsync();
        }
    }

    private void CredentialPassword_Changed(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedScreen is not null && sender is System.Windows.Controls.PasswordBox pb)
        {
            _vm.SelectedScreen.PlainTextPassword = pb.Password;
            _vm.MarkDirty();
        }
    }

    private void ClearCredentialPassword_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedScreen is null)
        {
            return;
        }

        var result = System.Windows.MessageBox.Show(
            this,
            "Remove the saved password for this screen? Auto-login will stop working until a new password is entered.",
            "Clear saved password",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _vm.SelectedScreen.PlainTextPassword = string.Empty;
            _vm.SelectedScreen.HasStoredPassword = false;
            // Persist immediately so ToConfig writes an empty EncryptedPassword.
            _vm.MarkDirty();
            if (CredentialPasswordBox is not null)
            {
                CredentialPasswordBox.Password = string.Empty;
            }
        }
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
