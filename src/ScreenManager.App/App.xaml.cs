using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderBuddy.ScreenManager.App.Browser;
using OrderBuddy.ScreenManager.App.Display;
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
	private IHost? _host;
	private KioskSupervisor? _supervisor;

	protected override async void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		var bootstrap = BootstrapConfiguration.Load();
		ConfigureSerilog(bootstrap);
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
				services.AddSingleton<IHealthMonitor, PlaceholderHealthMonitor>();
				services.AddSingleton<Serilog.ILogger>(Log.Logger);
				services.AddSingleton<KioskSupervisor>();
				services.AddTransient<KioskWindow>();
			})
			.Build();

		await _host.StartAsync();

		_supervisor = _host.Services.GetRequiredService<KioskSupervisor>();
		_supervisor.Start();
		Log.Information("OrderBuddy Screen Manager started. Environment initialized.");
	}

	protected override async void OnExit(ExitEventArgs e)
	{
		_supervisor?.Dispose();

		if (_host is not null)
		{
			await _host.StopAsync();
			_host.Dispose();
		}

		Log.CloseAndFlush();
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

