namespace OrderBuddy.ScreenManager.App.Security;

public interface IKeyboardLockService
{
    bool IsActive { get; }
    void Start();
    void Stop();
}
