using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderBuddy.ScreenManager.App.Browser;
using OrderBuddy.ScreenManager.App.Display;
using OrderBuddy.ScreenManager.App.Security;
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
				services.AddSingleton<Serilog.ILogger>(Log.Logger);
				services.AddSingleton<KioskSupervisor>();
				services.AddSingleton<INightlyMaintenanceScheduler, NightlyMaintenanceScheduler>();
				services.AddTransient<KioskWindow>();
			})
			.Build();

		await _host.StartAsync();

		_configService = _host.Services.GetRequiredService<IConfigService>();
		_keyboardLockService = _host.Services.GetRequiredService<IKeyboardLockService>();
		var healthMonitor = _host.Services.GetRequiredService<IHealthMonitor>();
		healthMonitor.ApplySettings(
			_configService.CurrentConfig.General.RetryBackoffSec,
			_configService.CurrentConfig.General.OfflineTimeoutSec,
			_configService.CurrentConfig.General.OfflineTimeoutSec);

		ApplyKeyboardLock(_configService.CurrentConfig.General.LockSystemKeys);
		_configService.ConfigChanged += OnConfigChanged;

		_supervisor = _host.Services.GetRequiredService<KioskSupervisor>();
		_nightlyMaintenanceScheduler = _host.Services.GetRequiredService<INightlyMaintenanceScheduler>();
		_supervisor.Start();
		_nightlyMaintenanceScheduler.Start();
		Log.Information("OrderBuddy Screen Manager started. Environment initialized.");
	}

	private void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
	{
		ApplyKeyboardLock(e.NewConfig.General.LockSystemKeys);

		if (_host is not null)
		{
			var healthMonitor = _host.Services.GetRequiredService<IHealthMonitor>();
			healthMonitor.ApplySettings(
				e.NewConfig.General.RetryBackoffSec,
				e.NewConfig.General.OfflineTimeoutSec,
				e.NewConfig.General.OfflineTimeoutSec);
		}
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

