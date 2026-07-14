namespace OrderBuddy.ScreenManager.Core.Health;

public enum ScreenHealthState
{
    Loading,
    Online,
    Recovering,
    Offline,
    Failed
}

public enum ScreenFailureType
{
    Navigation,
    ProcessCrash,
    HeartbeatTimeout,
    Connectivity,
    MemoryPressure,
    Manual
}

public sealed record ScreenHealthSnapshot(
    string ScreenId,
    ScreenHealthState State,
    int ConsecutiveFailures,
    int ProcessFailureCount,
    DateTimeOffset? LastSuccessUtc,
    DateTimeOffset? LastFailureUtc,
    DateTimeOffset? LastHeartbeatUtc,
    DateTimeOffset? NextRecoveryAttemptUtc,
    bool NetworkAvailable,
    string LastReason);

public interface IHealthMonitor
{
    void ApplySettings(IReadOnlyList<int> retryBackoffSec, int offlineProbeSec, int heartbeatTimeoutSec);
    void EnsureScreen(string screenId);
    ScreenHealthSnapshot GetSnapshot(string screenId);
    IReadOnlyList<ScreenHealthSnapshot> GetSnapshots();
    bool IsSystemHealthy();

    ScreenHealthSnapshot RecordNavigationSuccess(string screenId, string source, DateTimeOffset utcNow);
    ScreenHealthSnapshot RecordFailure(string screenId, ScreenFailureType failureType, string reason, DateTimeOffset utcNow);
    ScreenHealthSnapshot RecordHeartbeat(string screenId, DateTimeOffset utcNow);
    ScreenHealthSnapshot RecordConnectivity(string screenId, bool networkAvailable, DateTimeOffset utcNow, string reason);
    ScreenHealthSnapshot RecordMemoryPressure(string screenId, long workingSetMb, long thresholdMb, DateTimeOffset utcNow);
    ScreenHealthSnapshot EvaluateHeartbeatTimeout(string screenId, DateTimeOffset utcNow);
    bool ShouldAttemptRecovery(string screenId, DateTimeOffset utcNow);
}
