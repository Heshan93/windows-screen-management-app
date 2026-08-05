using OrderBuddy.ScreenManager.Core.Config;

namespace OrderBuddy.ScreenManager.App;

public interface ICacheMaintenanceScheduler
{
    void Start();
    void Stop();
}

public sealed class CacheMaintenanceScheduler : ICacheMaintenanceScheduler, IDisposable
{
    private readonly IConfigService _configService;
    private readonly KioskSupervisor _kioskSupervisor;
    private readonly Serilog.ILogger _logger;
    private readonly object _sync = new();

    private System.Threading.Timer? _timer;
    private bool _started;

    public CacheMaintenanceScheduler(
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
            RescheduleUnsafe(_configService.CurrentConfig.General.CacheMaintenance);
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

            RescheduleUnsafe(e.NewConfig.General.CacheMaintenance);
        }
    }

    private void OnTimerTick(object? state)
    {
        try
        {
            var maintenance = _configService.CurrentConfig.General.CacheMaintenance;
            if (maintenance.Enabled)
            {
                _logger.Information(
                    "Scheduled cache maintenance started. Scope: {Scope}, ReloadAfter: {ReloadAfter}",
                    maintenance.Scope, maintenance.ReloadAfterClear);

                _ = _kioskSupervisor.ClearCacheAllAsync(maintenance.Scope, maintenance.ReloadAfterClear);
            }
            else
            {
                _logger.Debug("Scheduled cache maintenance tick: disabled in config, skipping.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Scheduled cache maintenance execution failed.");
        }
        finally
        {
            lock (_sync)
            {
                if (_started)
                {
                    RescheduleUnsafe(_configService.CurrentConfig.General.CacheMaintenance);
                }
            }
        }
    }

    private void RescheduleUnsafe(CacheMaintenanceSettings maintenance)
    {
        _timer?.Dispose();
        _timer = null;

        if (!maintenance.Enabled)
        {
            _logger.Information("Cache maintenance scheduler disabled.");
            return;
        }

        var intervalMinutes = Math.Max(5, maintenance.IntervalMinutes);
        var due = TimeSpan.FromMinutes(intervalMinutes);
        _timer = new System.Threading.Timer(OnTimerTick, null, due, System.Threading.Timeout.InfiniteTimeSpan);

        _logger.Information(
            "Cache maintenance scheduled. Interval: {IntervalMinutes} min, Scope: {Scope}, NextRunIn: {Due}",
            intervalMinutes, maintenance.Scope, due);
    }

    public void Dispose()
    {
        _configService.ConfigChanged -= OnConfigChanged;
        Stop();
    }
}
