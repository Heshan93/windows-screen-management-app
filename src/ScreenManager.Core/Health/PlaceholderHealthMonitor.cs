namespace OrderBuddy.ScreenManager.Core.Health;

public sealed class PlaceholderHealthMonitor : IHealthMonitor
{
    private static readonly IReadOnlyList<int> DefaultBackoff = [5, 10, 30, 60];

    private readonly object _sync = new();
    private readonly Dictionary<string, MutableScreenHealth> _screens = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<int> _retryBackoffSec = DefaultBackoff;
    private int _offlineProbeSec = 20;
    private int _heartbeatTimeoutSec = 20;

    public void ApplySettings(IReadOnlyList<int> retryBackoffSec, int offlineProbeSec, int heartbeatTimeoutSec)
    {
        lock (_sync)
        {
            var cleaned = retryBackoffSec
                .Where(v => v >= 1 && v <= 600)
                .Distinct()
                .OrderBy(v => v)
                .ToArray();

            _retryBackoffSec = cleaned.Length == 0 ? DefaultBackoff : cleaned;
            _offlineProbeSec = Math.Clamp(offlineProbeSec, 15, 30);
            _heartbeatTimeoutSec = Math.Clamp(heartbeatTimeoutSec, 5, 300);
        }
    }

    public void EnsureScreen(string screenId)
    {
        lock (_sync)
        {
            GetOrCreateUnsafe(screenId);
        }
    }

    public ScreenHealthSnapshot GetSnapshot(string screenId)
    {
        lock (_sync)
        {
            return ToSnapshot(GetOrCreateUnsafe(screenId));
        }
    }

    public IReadOnlyList<ScreenHealthSnapshot> GetSnapshots()
    {
        lock (_sync)
        {
            return _screens.Values.Select(ToSnapshot).ToList();
        }
    }

    public bool IsSystemHealthy()
    {
        lock (_sync)
        {
            return _screens.Values.All(s => s.State is ScreenHealthState.Loading or ScreenHealthState.Online);
        }
    }

    public ScreenHealthSnapshot RecordNavigationSuccess(string screenId, string source, DateTimeOffset utcNow)
    {
        lock (_sync)
        {
            var screen = GetOrCreateUnsafe(screenId);
            screen.State = ScreenHealthState.Online;
            screen.ConsecutiveFailures = 0;
            screen.LastSuccessUtc = utcNow;
            screen.NextRecoveryAttemptUtc = null;
            screen.LastReason = $"Navigation ok: {source}";
            return ToSnapshot(screen);
        }
    }

    public ScreenHealthSnapshot RecordFailure(string screenId, ScreenFailureType failureType, string reason, DateTimeOffset utcNow)
    {
        lock (_sync)
        {
            var screen = GetOrCreateUnsafe(screenId);
            screen.ConsecutiveFailures++;
            screen.LastFailureUtc = utcNow;
            screen.LastReason = $"{failureType}: {reason}";

            if (failureType == ScreenFailureType.ProcessCrash)
            {
                screen.ProcessFailureCount++;
            }

            var capped = _retryBackoffSec.Count;
            if (screen.ConsecutiveFailures > capped)
            {
                screen.State = ScreenHealthState.Offline;
                screen.NextRecoveryAttemptUtc = utcNow.AddSeconds(_offlineProbeSec);
            }
            else
            {
                screen.State = ScreenHealthState.Recovering;
                var delay = _retryBackoffSec[screen.ConsecutiveFailures - 1];
                screen.NextRecoveryAttemptUtc = utcNow.AddSeconds(delay);
            }

            return ToSnapshot(screen);
        }
    }

    public ScreenHealthSnapshot RecordHeartbeat(string screenId, DateTimeOffset utcNow)
    {
        lock (_sync)
        {
            var screen = GetOrCreateUnsafe(screenId);
            screen.LastHeartbeatUtc = utcNow;
            if (screen.State is ScreenHealthState.Loading or ScreenHealthState.Online)
            {
                screen.State = ScreenHealthState.Online;
            }

            return ToSnapshot(screen);
        }
    }

    public ScreenHealthSnapshot RecordConnectivity(string screenId, bool networkAvailable, DateTimeOffset utcNow, string reason)
    {
        lock (_sync)
        {
            var screen = GetOrCreateUnsafe(screenId);
            screen.NetworkAvailable = networkAvailable;

            if (!networkAvailable)
            {
                screen.State = ScreenHealthState.Offline;
                screen.LastFailureUtc = utcNow;
                screen.LastReason = $"Connectivity: {reason}";
                screen.NextRecoveryAttemptUtc = utcNow.AddSeconds(_offlineProbeSec);
                return ToSnapshot(screen);
            }

            if (screen.State == ScreenHealthState.Offline)
            {
                screen.State = ScreenHealthState.Recovering;
                screen.NextRecoveryAttemptUtc = utcNow;
                screen.LastReason = $"Connectivity restored: {reason}";
            }

            return ToSnapshot(screen);
        }
    }

    public ScreenHealthSnapshot RecordMemoryPressure(string screenId, long workingSetMb, long thresholdMb, DateTimeOffset utcNow)
    {
        return RecordFailure(
            screenId,
            ScreenFailureType.MemoryPressure,
            $"WorkingSetMB {workingSetMb} exceeded threshold {thresholdMb}",
            utcNow);
    }

    public ScreenHealthSnapshot EvaluateHeartbeatTimeout(string screenId, DateTimeOffset utcNow)
    {
        lock (_sync)
        {
            var screen = GetOrCreateUnsafe(screenId);
            if (screen.LastHeartbeatUtc is null)
            {
                return ToSnapshot(screen);
            }

            if ((utcNow - screen.LastHeartbeatUtc.Value).TotalSeconds <= _heartbeatTimeoutSec)
            {
                return ToSnapshot(screen);
            }

            if (screen.State is ScreenHealthState.Recovering or ScreenHealthState.Offline)
            {
                return ToSnapshot(screen);
            }

            return RecordFailure(
                screenId,
                ScreenFailureType.HeartbeatTimeout,
                $"No heartbeat for {(int)(utcNow - screen.LastHeartbeatUtc.Value).TotalSeconds}s",
                utcNow);
        }
    }

    public bool ShouldAttemptRecovery(string screenId, DateTimeOffset utcNow)
    {
        lock (_sync)
        {
            var screen = GetOrCreateUnsafe(screenId);
            if (screen.State is not (ScreenHealthState.Recovering or ScreenHealthState.Offline))
            {
                return false;
            }

            return screen.NextRecoveryAttemptUtc is null || screen.NextRecoveryAttemptUtc <= utcNow;
        }
    }

    private MutableScreenHealth GetOrCreateUnsafe(string screenId)
    {
        if (_screens.TryGetValue(screenId, out var existing))
        {
            return existing;
        }

        var created = new MutableScreenHealth(screenId);
        _screens[screenId] = created;
        return created;
    }

    private static ScreenHealthSnapshot ToSnapshot(MutableScreenHealth screen)
    {
        return new ScreenHealthSnapshot(
            screen.ScreenId,
            screen.State,
            screen.ConsecutiveFailures,
            screen.ProcessFailureCount,
            screen.LastSuccessUtc,
            screen.LastFailureUtc,
            screen.LastHeartbeatUtc,
            screen.NextRecoveryAttemptUtc,
            screen.NetworkAvailable,
            screen.LastReason);
    }

    private sealed class MutableScreenHealth
    {
        public MutableScreenHealth(string screenId)
        {
            ScreenId = screenId;
        }

        public string ScreenId { get; }
        public ScreenHealthState State { get; set; } = ScreenHealthState.Loading;
        public int ConsecutiveFailures { get; set; }
        public int ProcessFailureCount { get; set; }
        public DateTimeOffset? LastSuccessUtc { get; set; }
        public DateTimeOffset? LastFailureUtc { get; set; }
        public DateTimeOffset? LastHeartbeatUtc { get; set; }
        public DateTimeOffset? NextRecoveryAttemptUtc { get; set; }
        public bool NetworkAvailable { get; set; } = true;
        public string LastReason { get; set; } = "initial";
    }
}
