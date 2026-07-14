using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderBuddy.ScreenManager.App.Browser;
using OrderBuddy.ScreenManager.App.Display;
using OrderBuddy.ScreenManager.App.Lifecycle;
using OrderBuddy.ScreenManager.App.Security;
using OrderBuddy.ScreenManager.App.Status;
using OrderBuddy.ScreenManager.Core.Config;
using OrderBuddy.ScreenManager.Core.Display;
using OrderBuddy.ScreenManager.Core.Health;
using Serilog;
using Serilog.Events;

namespace OrderBuddy.ScreenManager.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
	private static Mutex? _singleInstanceMutex;

	private IHost? _host;
	private KioskSupervisor? _supervisor;
	private IKeyboardLockService? _keyboardLockService;
	private IConfigService? _configService;
	private INightlyMaintenanceScheduler? _nightlyMaintenanceScheduler;
	private IAutoStartService? _autoStartService;

	protected override async void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		var bootstrap = BootstrapConfiguration.Load();
		ConfigureSerilog(bootstrap);

		_singleInstanceMutex = new Mutex(true, "Global\\OrderBuddy.ScreenManager.v2", out var isFirstInstance);
		if (!isFirstInstance)
		{
			Log.Warning("Second launch detected. Exiting due to single-instance guard.");
			Log.CloseAndFlush();
			Shutdown();
			return;
		}
		var browserOptions = KioskBrowserOptions.Create(bootstrap.ProgramDataRoot);

		_host = Host.CreateDefaultBuilder()
			.UseSerilog()
			.ConfigureServices(services =>
			{
				services.AddSingleton(bootstrap);
				services.AddSingleton(browserOptions);
				services.AddSingleton(ConfigServiceOptions.FromRootDirectory(bootstrap.ProgramDataRoot));
				services.AddSingleton<IWebViewEnvironmentProvider, WebViewEnvironmentProvider>();
				services.AddSingleton<IConfigService, ConfigService>();
				services.AddSingleton<IDisplayManager, WindowsDisplayManager>();
				services.AddSingleton<ISystemClock, SystemClock>();
				services.AddSingleton<IHealthMonitor, PlaceholderHealthMonitor>();
				services.AddSingleton<IKeyboardLockService, WindowsKeyboardLockService>();
				services.AddSingleton<IAdminAccessService, AdminAccessService>();
				services.AddSingleton<IAutoStartService, WindowsAutoStartService>();
				services.AddSingleton<ILastConnectionStore, LastConnectionStore>();
				services.AddSingleton<Serilog.ILogger>(Log.Logger);
				services.AddSingleton<KioskSupervisor>();
				services.AddSingleton<INightlyMaintenanceScheduler, NightlyMaintenanceScheduler>();
				services.AddTransient<AdminPinWindow>();
				services.AddTransient<AdminSettingsWindow>();
				services.AddTransient<SetupWizardWindow>();
				services.AddTransient<KioskWindow>();
			})
			.Build();

		await _host.StartAsync();

		_configService = _host.Services.GetRequiredService<IConfigService>();
		_keyboardLockService = _host.Services.GetRequiredService<IKeyboardLockService>();
		_autoStartService = _host.Services.GetRequiredService<IAutoStartService>();
		var healthMonitor = _host.Services.GetRequiredService<IHealthMonitor>();
		healthMonitor.ApplySettings(
			_configService.CurrentConfig.General.RetryBackoffSec,
			_configService.CurrentConfig.General.OfflineTimeoutSec,
			_configService.CurrentConfig.General.OfflineTimeoutSec);

		ApplyKeyboardLock(_configService.CurrentConfig.General.LockSystemKeys);
		_configService.ConfigChanged += OnConfigChanged;
		SystemEvents.PowerModeChanged += OnPowerModeChanged;

		if (_configService.CurrentConfig.General.ServiceWatchdogEnabled)
		{
			Log.Warning("ServiceWatchdogEnabled is on, but Windows Service watchdog is design-stub only in P8 and remains disabled.");
		}

		_supervisor = _host.Services.GetRequiredService<KioskSupervisor>();
		_nightlyMaintenanceScheduler = _host.Services.GetRequiredService<INightlyMaintenanceScheduler>();
		_supervisor.Start();
		_nightlyMaintenanceScheduler.Start();

		await ApplyAutoStartAsync(_configService.CurrentConfig);
		Log.Information("OrderBuddy Screen Manager started. Environment initialized.");
	}

	private void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
	{
		ApplyKeyboardLock(e.NewConfig.General.LockSystemKeys);
		_ = ApplyAutoStartAsync(e.NewConfig);

		if (_host is not null)
		{
			var healthMonitor = _host.Services.GetRequiredService<IHealthMonitor>();
			healthMonitor.ApplySettings(
				e.NewConfig.General.RetryBackoffSec,
				e.NewConfig.General.OfflineTimeoutSec,
				e.NewConfig.General.OfflineTimeoutSec);
		}
	}

	private async Task ApplyAutoStartAsync(AppConfig config)
	{
		if (_autoStartService is null)
		{
			return;
		}

		try
		{
			var status = await _autoStartService.ApplyAsync(config);
			Log.Information(
				"Auto-start sync complete. EnabledByConfig: {EnabledByConfig}, Registered: {Registered}, RestartPolicyEnabled: {RestartPolicyEnabled}, Detail: {Detail}",
				status.EnabledByConfig,
				status.IsRegistered,
				status.RestartPolicyEnabled,
				status.Detail);
		}
		catch (Exception ex)
		{
			Log.Warning(ex, "Auto-start sync failed.");
		}
	}

	private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
	{
		if (e.Mode != PowerModes.Resume || _supervisor is null)
		{
			return;
		}

		Dispatcher.Invoke(() =>
		{
			_supervisor.ReconcileNow("system-resume");
			_supervisor.ReloadAllWindows("system-resume");
			_supervisor.ReassertTopmostWindows("system-resume");
		});

		Log.Information("System resume handled: displays revalidated, screens reloaded, top-most state reasserted.");
	}

	private void ApplyKeyboardLock(bool enabled)
	{
		if (_keyboardLockService is null)
		{
			return;
		}

		if (enabled)
		{
			_keyboardLockService.Start();
			return;
		}

		_keyboardLockService.Stop();
	}

	protected override async void OnExit(ExitEventArgs e)
	{
		if (_configService is not null)
		{
			_configService.ConfigChanged -= OnConfigChanged;
		}

		SystemEvents.PowerModeChanged -= OnPowerModeChanged;

		_keyboardLockService?.Stop();
		_nightlyMaintenanceScheduler?.Stop();

		if (_nightlyMaintenanceScheduler is IDisposable disposableNightly)
		{
			disposableNightly.Dispose();
		}

		if (_keyboardLockService is IDisposable disposableKeyboardLock)
		{
			disposableKeyboardLock.Dispose();
		}

		_supervisor?.Dispose();

		if (_host is not null)
		{
			await _host.StopAsync();
			_host.Dispose();
		}

		Log.CloseAndFlush();

		_singleInstanceMutex?.ReleaseMutex();
		_singleInstanceMutex?.Dispose();
		_singleInstanceMutex = null;

		base.OnExit(e);
	}

	private static void ConfigureSerilog(BootstrapConfiguration bootstrap)
	{
		var loggerConfiguration = new LoggerConfiguration()
			.MinimumLevel.Is(bootstrap.ParsedLogLevel)
			.WriteTo.File(
				Path.Combine(bootstrap.LogDirectory, "screen-manager-.log"),
				rollingInterval: RollingInterval.Day,
				fileSizeLimitBytes: 10 * 1024 * 1024,
				retainedFileCountLimit: 14,
				rollOnFileSizeLimit: true,
				shared: true);

		try
		{
			loggerConfiguration = loggerConfiguration.WriteTo.EventLog(
				source: "OrderBuddy.ScreenManager",
				manageEventSource: true,
				restrictedToMinimumLevel: LogEventLevel.Error);
		}
		catch
		{
			// Event Log source creation may fail without elevated rights in some environments.
		}

		Log.Logger = loggerConfiguration.CreateLogger();
	}

	internal sealed record BootstrapConfiguration(string ProgramDataRoot, string LogDirectory, string LogLevel)
	{
		public LogEventLevel ParsedLogLevel => Enum.TryParse<LogEventLevel>(LogLevel, true, out var level)
			? level
			: LogEventLevel.Information;

		public static BootstrapConfiguration Load()
		{
			var commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
			var root = Path.Combine(commonData, "OrderBuddy", "ScreenManager");
			var logs = Path.Combine(root, "logs");
			Directory.CreateDirectory(root);
			Directory.CreateDirectory(logs);

			var configPath = Path.Combine(root, "appsettings.json");
			if (!File.Exists(configPath))
			{
				var initial = new BootstrapConfiguration(root, logs, "Information");
				var payload = JsonSerializer.Serialize(initial, new JsonSerializerOptions { WriteIndented = true });
				File.WriteAllText(configPath, payload);
				return initial;
			}

			try
			{
				var json = File.ReadAllText(configPath);
				var loaded = JsonSerializer.Deserialize<BootstrapConfiguration>(json);
				if (loaded is not null)
				{
					Directory.CreateDirectory(loaded.ProgramDataRoot);
					Directory.CreateDirectory(loaded.LogDirectory);
					return loaded;
				}
			}
			catch
			{
				// Fall back to safe defaults if the bootstrap config is unreadable.
			}

			return new BootstrapConfiguration(root, logs, "Information");
		}
	}
}

