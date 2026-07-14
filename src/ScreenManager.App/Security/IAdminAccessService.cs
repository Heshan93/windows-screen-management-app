using OrderBuddy.ScreenManager.Core.Config;

namespace OrderBuddy.ScreenManager.App.Security;

public interface IAdminAccessService
{
    bool TryUnlock(string pin, AppConfig config, DateTimeOffset now, out TimeSpan? retryAfter);
}

public sealed class AdminAccessService : IAdminAccessService
{
    private readonly object _sync = new();
    private int _failedAttempts;
    private DateTimeOffset? _lockedUntilUtc;

    public bool TryUnlock(string pin, AppConfig config, DateTimeOffset now, out TimeSpan? retryAfter)
    {
        lock (_sync)
        {
            if (_lockedUntilUtc is not null && now < _lockedUntilUtc.Value)
            {
                retryAfter = _lockedUntilUtc.Value - now;
                return false;
            }

            var valid = ConfigSecurity.VerifyPin(pin, config.General.AdminPinHash);
            if (valid)
            {
                _failedAttempts = 0;
                _lockedUntilUtc = null;
                retryAfter = null;
                return true;
            }

            _failedAttempts++;
            var seconds = Math.Min(300, (int)Math.Pow(2, Math.Min(7, _failedAttempts)));
            _lockedUntilUtc = now.AddSeconds(seconds);
            retryAfter = TimeSpan.FromSeconds(seconds);
            return false;
        }
    }
}
