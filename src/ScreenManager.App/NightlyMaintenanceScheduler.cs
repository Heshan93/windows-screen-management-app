using OrderBuddy.ScreenManager.Core.Config;

namespace OrderBuddy.ScreenManager.App;

public interface INightlyMaintenanceScheduler
{
    void Start();
    void Stop();
}

public sealed class NightlyMaintenanceScheduler : INightlyMaintenanceScheduler, IDisposable
{
    private readonly IConfigService _configService;
    private readonly KioskSupervisor _kioskSupervisor;
    private readonly Serilog.ILogger _logger;
    private readonly object _sync = new();

    private System.Threading.Timer? _timer;
    private bool _started;

    public NightlyMaintenanceScheduler(
        IConfigService configService,
        KioskSupervisor kioskSupervisor,
        Serilog.ILogger logger)
    {
        _configService = configService;
        _kioskSupervisor = kioskSupervisor;
        _logger = logger;

        _configService.ConfigChanged += OnConfigChanged;
    }

    public void Start()
    {
        lock (_sync)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            RescheduleUnsafe(_configService.CurrentConfig.General.NightlyMaintenance);
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            _started = false;
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
    {
        lock (_sync)
        {
            if (!_started)
            {
                return;
            }

            RescheduleUnsafe(e.NewConfig.General.NightlyMaintenance);
        }
    }

    private void OnTimerTick(object? state)
    {
        try
        {
            var maintenance = _configService.CurrentConfig.General.NightlyMaintenance;
            if (maintenance.Enabled && maintenance.Action.Equals("reloadAll", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Information("Nightly maintenance started. Action: {Action}", maintenance.Action);
                _kioskSupervisor.ReloadAllWindows("nightly-maintenance");
            }
            else if (maintenance.Enabled)
            {
                _logger.Warning("Nightly maintenance action {Action} is not implemented; skipping.", maintenance.Action);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Nightly maintenance execution failed.");
        }
        finally
        {
            lock (_sync)
            {
                if (_started)
                {
                    RescheduleUnsafe(_configService.CurrentConfig.General.NightlyMaintenance);
                }
            }
        }
    }

    private void RescheduleUnsafe(NightlyMaintenanceSettings maintenance)
    {
        _timer?.Dispose();
        _timer = null;

        if (!maintenance.Enabled)
        {
            _logger.Information("Nightly maintenance disabled.");
            return;
        }

        if (!TimeOnly.TryParse(maintenance.Time, out var configuredTime))
        {
            _logger.Warning("Nightly maintenance time {Time} is invalid. Falling back to 03:30.", maintenance.Time);
            configuredTime = new TimeOnly(3, 30);
        }

        var now = DateTimeOffset.Now;
        var next = new DateTimeOffset(now.Year, now.Month, now.Day, configuredTime.Hour, configuredTime.Minute, 0, now.Offset);
        if (next <= now)
        {
            next = next.AddDays(1);
        }

        var due = next - now;
        _timer = new System.Threading.Timer(OnTimerTick, null, due, Timeout.InfiniteTimeSpan);

        _logger.Information("Nightly maintenance scheduled for {NextRunLocal} (in {Delay}).", next, due);
    }

    public void Dispose()
    {
        _configService.ConfigChanged -= OnConfigChanged;
        Stop();
    }
}
