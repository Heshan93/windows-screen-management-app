using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderBuddy.ScreenManager.App.Browser;
using OrderBuddy.ScreenManager.App.Diagnostics;
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
	private ICacheMaintenanceScheduler? _cacheMaintenanceScheduler;
	private IAutoStartService? _autoStartService;

	public App()
	{
		// Register global crash handlers as the very first thing so nothing dies silently.
		StartupDiagnostics.Write("App constructor entered. Registering global exception handlers.");

		AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
		DispatcherUnhandledException += OnDispatcherUnhandledException;
		System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
	}

	private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		var ex = e.ExceptionObject as Exception;
		StartupDiagnostics.Write($"FATAL: AppDomain unhandled exception. IsTerminating={e.IsTerminating}.", ex);
		TryLogSerilog("AppDomain unhandled exception", ex);
		ShowFatalError("A fatal error occurred (AppDomain).", ex);
	}

	private void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
	{
		StartupDiagnostics.Write("FATAL: Dispatcher unhandled exception.", e.Exception);
		TryLogSerilog("Dispatcher unhandled exception", e.Exception);
		ShowFatalError("A fatal error occurred (UI thread).", e.Exception);

		// Keep the process alive so the operator can read the message instead of a silent exit.
		e.Handled = true;
	}

	private void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
	{
		StartupDiagnostics.Write("WARNING: Unobserved task exception.", e.Exception);
		TryLogSerilog("Unobserved task exception", e.Exception);
		e.SetObserved();
	}

	private static void TryLogSerilog(string message, Exception? ex)
	{
		try
		{
			Log.Fatal(ex, "{Message}", message);
			Log.CloseAndFlush();
		}
		catch
		{
			// Serilog may not be configured yet.
		}
	}

	private static void ShowFatalError(string headline, Exception? ex)
	{
		try
		{
			var logPath = StartupDiagnostics.LogPath ?? "(no writable log location)";
			var detail = ex is null ? "(no exception detail)" : ex.Message;
			var text =
				headline + Environment.NewLine + Environment.NewLine +
				detail + Environment.NewLine + Environment.NewLine +
				"A detailed log was written to:" + Environment.NewLine + logPath;

			System.Windows.MessageBox.Show(
				text,
				"OrderBuddy Screen Manager — startup error",
				System.Windows.MessageBoxButton.OK,
				System.Windows.MessageBoxImage.Error);
		}
		catch
		{
			// Never let error reporting itself crash the app.
		}
	}

	protected override async void OnStartup(StartupEventArgs e)
	{
		try
		{
			await StartupCoreAsync(e);
		}
		catch (Exception ex)
		{
			StartupDiagnostics.Write("FATAL: Unhandled exception during OnStartup.", ex);
			TryLogSerilog("Unhandled exception during OnStartup", ex);
			ShowFatalError("The application failed to start.", ex);
		}
	}

	private async Task StartupCoreAsync(StartupEventArgs e)
	{
		base.OnStartup(e);

		StartupDiagnostics.Write("Stage 1: OnStartup entered. Loading bootstrap configuration.");
		var bootstrap = BootstrapConfiguration.Load();
		StartupDiagnostics.Write($"Stage 2: Bootstrap loaded. ProgramDataRoot={bootstrap.ProgramDataRoot}, LogDirectory={bootstrap.LogDirectory}, LogLevel={bootstrap.LogLevel}.");

		ConfigureSerilog(bootstrap);
		StartupDiagnostics.Write("Stage 3: Serilog configured. Subsequent logs go to the rolling log file too.");
		Log.Information("=== OrderBuddy Screen Manager starting. ProgramData: {Root} ===", bootstrap.ProgramDataRoot);

		_singleInstanceMutex = new Mutex(true, "Global\\OrderBuddy.ScreenManager.v2", out var isFirstInstance);
		if (!isFirstInstance)
		{
			StartupDiagnostics.Write("Stage 4: Second instance detected. Exiting via single-instance guard.");
			Log.Warning("Second launch detected. Exiting due to single-instance guard.");
			Log.CloseAndFlush();
			Shutdown();
			return;
		}
		StartupDiagnostics.Write("Stage 4: Single-instance guard acquired.");

		var browserOptions = KioskBrowserOptions.Create(bootstrap.ProgramDataRoot);
		StartupDiagnostics.Write("Stage 5: Building DI host.");

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
				services.AddSingleton<ICacheMaintenanceScheduler, CacheMaintenanceScheduler>();
				services.AddTransient<AdminPinWindow>();
				services.AddTransient<AdminSettingsWindow>();
				services.AddTransient<SetupWizardWindow>();
				services.AddTransient<KioskWindow>();
			})
			.Build();

		StartupDiagnostics.Write("Stage 6: DI host built. Starting host.");
		await _host.StartAsync();
		StartupDiagnostics.Write("Stage 7: Host started. Resolving core services.");

		_configService = _host.Services.GetRequiredService<IConfigService>();
		StartupDiagnostics.Write($"Stage 8: ConfigService resolved. Screens={_configService.CurrentConfig.Screens.Count}, EnabledScreens={_configService.CurrentConfig.Screens.Count(s => s.Enabled)}.");
		Log.Information(
			"Config loaded. Screens: {Screens}, Enabled: {Enabled}. Diagnostics: {Diagnostics}",
			_configService.CurrentConfig.Screens.Count,
			_configService.CurrentConfig.Screens.Count(s => s.Enabled),
			string.Join(" | ", _configService.LastDiagnostics));

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

		StartupDiagnostics.Write("Stage 9: Starting KioskSupervisor, nightly scheduler, and cache maintenance scheduler.");
		_supervisor = _host.Services.GetRequiredService<KioskSupervisor>();
		_nightlyMaintenanceScheduler = _host.Services.GetRequiredService<INightlyMaintenanceScheduler>();
		_cacheMaintenanceScheduler = _host.Services.GetRequiredService<ICacheMaintenanceScheduler>();
		_supervisor.Start();
		_nightlyMaintenanceScheduler.Start();
		_cacheMaintenanceScheduler.Start();

		StartupDiagnostics.Write("Stage 10: Applying auto-start registration.");
		await ApplyAutoStartAsync(_configService.CurrentConfig);

		StartupDiagnostics.Write("Stage 11: Ensuring first-run experience (wizard / welcome screen).");
		await EnsureFirstRunExperienceAsync();

		StartupDiagnostics.Write("Stage 12: Startup complete.");
		Log.Information("OrderBuddy Screen Manager started. Environment initialized.");
	}


	private void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
	{
		Log.Information(
			"App received config change. Source: {Source}, ChangeId: {ChangeId}, EnabledScreens: {EnabledScreens}, Diagnostics: {Diagnostics}",
			e.Source,
			e.ChangeId,
			e.NewConfig.Screens.Count(s => s.Enabled),
			string.Join(" | ", e.Diagnostics));

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

	private void LaunchSetupWizard()
	{
		if (_host is null) return;

		try
		{
			StartupDiagnostics.Write("Wizard: creating SetupWizardWindow.");
			var wizard = ActivatorUtilities.CreateInstance<SetupWizardWindow>(_host.Services);
			wizard.Topmost = true;
			wizard.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
			StartupDiagnostics.Write("Wizard: showing dialog.");
			var result = wizard.ShowDialog();
			StartupDiagnostics.Write($"Wizard: dialog closed. Result={result}.");

			if (result == true)
			{
				Log.Information("Setup wizard completed. Reconciling kiosk windows.");
				_supervisor?.ReconcileNow("wizard-complete");
			}
			else
			{
				Log.Information("Setup wizard was closed without finishing.");
			}
		}
		catch (Exception ex)
		{
			StartupDiagnostics.Write("Wizard: FAILED to launch.", ex);
			Log.Error(ex, "Failed to launch setup wizard.");
		}
	}

	/// <summary>
	/// Guarantees the app always shows something on first run. If no screens are
	/// configured, the setup wizard is launched. If the operator dismisses the
	/// wizard without configuring anything, a built-in welcome screen is shown so
	/// the application is never an invisible background process.
	/// </summary>
	private async Task EnsureFirstRunExperienceAsync()
	{
		if (_configService is null)
		{
			return;
		}

		if (_configService.CurrentConfig.Screens.Any(s => s.Enabled))
		{
			StartupDiagnostics.Write("First-run: screens already configured; skipping wizard.");
			return;
		}

		StartupDiagnostics.Write("First-run: no enabled screens. Launching setup wizard.");
		Log.Information("No enabled screens found. Launching first-run setup wizard.");
		LaunchSetupWizard();

		if (_configService.CurrentConfig.Screens.Any(s => s.Enabled))
		{
			StartupDiagnostics.Write("First-run: screens configured via wizard.");
			return;
		}

		StartupDiagnostics.Write("First-run: still no screens. Creating built-in welcome screen.");
		Log.Warning("No screens configured after setup wizard. Showing built-in welcome screen so the app is visible.");

		try
		{
			var config = _configService.CurrentConfig;
			var welcome = new ScreenConfig
			{
				Id = "welcome",
				Name = "Setup",
				Enabled = true,
				AppType = AppType.Custom,
				Url = BuildWelcomePageUrl(),
				DisplayNumberHint = 1,
				FullScreen = true
			};

			await _configService.SaveAsync(config with { Screens = [welcome] });
			_supervisor?.ReconcileNow("welcome-screen");
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to create built-in welcome screen.");
		}
	}

	private static string BuildWelcomePageUrl()
	{
		const string html = """
			<!DOCTYPE html>
			<html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
			<style>
			html,body{height:100%;margin:0}
			body{background:#141428;color:#f5f5f7;font-family:'Segoe UI',Arial,sans-serif;display:flex;align-items:center;justify-content:center;text-align:center}
			.card{max-width:760px;padding:48px}
			h1{font-size:2.6em;margin:0 0 8px;font-weight:600}
			.sub{font-size:1.25em;color:#9aa4c4;margin-bottom:36px}
			p{font-size:1.4em;line-height:1.7;color:#e2e6f5;margin:18px 0}
			.key{display:inline-block;background:#2a2a48;border:1px solid #3d3d66;padding:6px 16px;border-radius:8px;font-weight:600;color:#fff;margin:0 4px}
			.pin{color:#7dd3fc;font-weight:700}
			</style></head><body><div class="card">
			<h1>OrderBuddy Screen Manager</h1>
			<div class="sub">This screen has not been set up yet.</div>
			<p>Press <span class="key">Ctrl</span>+<span class="key">Shift</span>+<span class="key">F12</span></p>
			<p>or tap the <b>top-right corner</b> 5 times quickly</p>
			<p>Then enter PIN <span class="key pin">1234</span> to configure your screens.</p>
			</div></body></html>
			""";

		var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(html));
		return $"data:text/html;base64,{base64}";
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
		_cacheMaintenanceScheduler?.Stop();

		if (_nightlyMaintenanceScheduler is IDisposable disposableNightly)
		{
			disposableNightly.Dispose();
		}

		if (_cacheMaintenanceScheduler is IDisposable disposableCache)
		{
			disposableCache.Dispose();
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
			var root = ResolveWritableRoot();
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

		/// <summary>
		/// Picks the first data root the current (possibly non-admin kiosk) user can actually write to.
		/// Preferred: %ProgramData%\OrderBuddy\ScreenManager. If that is not writable (e.g. it was created
		/// admin-owned by an elevated installer and never re-permissioned), fall back to a per-user location
		/// so the app still starts instead of dying with "Access to the path is denied".
		/// </summary>
		private static string ResolveWritableRoot()
		{
			var candidates = new[]
			{
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OrderBuddy", "ScreenManager"),
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OrderBuddy", "ScreenManager")
			};

			foreach (var candidate in candidates)
			{
				if (IsWritable(candidate))
				{
					StartupDiagnostics.Write($"Bootstrap: using data root '{candidate}'.");
					return candidate;
				}

				StartupDiagnostics.Write($"Bootstrap: data root '{candidate}' is NOT writable; trying next candidate.");
			}

			// Last resort: use the first candidate anyway so behaviour is unchanged if probing was misleading.
			return candidates[0];
		}

		private static bool IsWritable(string directory)
		{
			try
			{
				Directory.CreateDirectory(directory);
				var probe = Path.Combine(directory, ".write-probe-" + Guid.NewGuid().ToString("N"));
				File.WriteAllText(probe, "probe");
				File.Delete(probe);
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}

