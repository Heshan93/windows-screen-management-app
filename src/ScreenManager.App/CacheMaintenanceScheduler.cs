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
            if (!maintenance.Enabled)
            {
                _logger.Debug("Scheduled cache maintenance tick: disabled in config, skipping.");
                lock (_sync)
                {
                    if (_started) RescheduleUnsafe(maintenance);
                }
                return;
            }

            _logger.Information(
                "Scheduled cache maintenance started. Scope: {Scope}, ReloadAfter: {ReloadAfter}, IdleThreshold: {IdleThreshold} min",
                maintenance.Scope, maintenance.ReloadAfterClear, maintenance.IdleThresholdMinutes);

            var clearTask = _kioskSupervisor.ClearCacheAllAsync(
                maintenance.Scope,
                maintenance.ReloadAfterClear,
                maintenance.IdleThresholdMinutes);

            clearTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.Error(t.Exception, "Scheduled cache maintenance execution failed.");
                }

                lock (_sync)
                {
                    if (!_started) return;

                    bool cleared = !t.IsFaulted && t.Result;
                    if (!cleared && maintenance.IdleThresholdMinutes > 0)
                    {
                        // Screen was active — retry in 1 minute rather than waiting the full interval.
                        _logger.Information(
                            "Cache clear deferred due to activity. Retrying in 1 minute.");
                        ScheduleRetryUnsafe();
                    }
                    else
                    {
                        RescheduleUnsafe(maintenance);
                    }
                }
            }, System.Threading.Tasks.TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Scheduled cache maintenance tick failed unexpectedly.");
            lock (_sync)
            {
                if (_started)
                {
                    RescheduleUnsafe(_configService.CurrentConfig.General.CacheMaintenance);
                }
            }
        }
    }

    private void ScheduleRetryUnsafe()
    {
        _timer?.Dispose();
        _timer = new System.Threading.Timer(OnTimerTick, null,
            TimeSpan.FromMinutes(1),
            System.Threading.Timeout.InfiniteTimeSpan);
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
