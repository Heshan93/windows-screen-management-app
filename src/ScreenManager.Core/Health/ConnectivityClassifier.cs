namespace OrderBuddy.ScreenManager.Core.Health;

public enum ConnectivityFailureCause
{
    None,
    InternetDown,
    OriginDown,
    CaptivePortal,
    Unknown
}

public sealed record ConnectivityStatus(
    bool InternetReachable,
    bool OriginReachable,
    bool CaptivePortalDetected,
    ConnectivityFailureCause FailureCause,
    string Message);

public static class ConnectivityClassifier
{
    public static ConnectivityStatus Evaluate(bool internetReachable, bool originReachable, bool captivePortalDetected)
    {
        if (captivePortalDetected)
        {
            return new ConnectivityStatus(
                InternetReachable: false,
                OriginReachable: false,
                CaptivePortalDetected: true,
                FailureCause: ConnectivityFailureCause.CaptivePortal,
                Message: "Captive portal detected while checking internet access.");
        }

        if (!internetReachable)
        {
            return new ConnectivityStatus(
                InternetReachable: false,
                OriginReachable: false,
                CaptivePortalDetected: false,
                FailureCause: ConnectivityFailureCause.InternetDown,
                Message: "Internet connectivity is unavailable.");
        }

        if (!originReachable)
        {
            return new ConnectivityStatus(
                InternetReachable: true,
                OriginReachable: false,
                CaptivePortalDetected: false,
                FailureCause: ConnectivityFailureCause.OriginDown,
                Message: "Internet is up, but the configured app endpoint is unavailable.");
        }

        return new ConnectivityStatus(
            InternetReachable: true,
            OriginReachable: true,
            CaptivePortalDetected: false,
            FailureCause: ConnectivityFailureCause.None,
            Message: "Connectivity healthy.");
    }
}
