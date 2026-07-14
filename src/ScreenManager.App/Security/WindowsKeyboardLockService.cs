using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;

namespace OrderBuddy.ScreenManager.App.Security;

public sealed class WindowsKeyboardLockService : IKeyboardLockService, IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int VkLwin = 0x5B;
    private const int VkRwin = 0x5C;
    private const int VkEscape = 0x1B;
    private const int VkControl = 0x11;

    private readonly Serilog.ILogger _logger;
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookHandle = IntPtr.Zero;

    public WindowsKeyboardLockService(Serilog.ILogger logger)
    {
        _logger = logger;
        _proc = HookCallback;
    }

    public bool IsActive => _hookHandle != IntPtr.Zero;

    public void Start()
    {
        if (IsActive)
        {
            return;
        }

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        var moduleHandle = GetModuleHandle(module?.ModuleName);

        _hookHandle = SetWindowsHookEx(WhKeyboardLl, _proc, moduleHandle, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            _logger.Warning("Failed to start global keyboard lock hook.");
            return;
        }

        _logger.Information("Global keyboard lock hook started (Win key / Ctrl+Esc blocked).");
    }

    public void Stop()
    {
        if (!IsActive)
        {
            return;
        }

        if (UnhookWindowsHookEx(_hookHandle))
        {
            _logger.Information("Global keyboard lock hook stopped.");
        }

        _hookHandle = IntPtr.Zero;
    }

    public void Dispose()
    {
        Stop();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            if (message == WmKeyDown || message == WmSysKeyDown)
            {
                var keyInfo = Marshal.PtrToStructure<Kbdllhookstruct>(lParam);

                var isWindowsKey = keyInfo.VkCode is VkLwin or VkRwin;
                var isCtrlEsc = keyInfo.VkCode == VkEscape && (GetAsyncKeyState(VkControl) & 0x8000) != 0;

                if (isWindowsKey || isCtrlEsc)
                {
                    return (IntPtr)1;
                }
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Kbdllhookstruct
    {
        public int VkCode;
        public int ScanCode;
        public int Flags;
        public int Time;
        public IntPtr DwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hmod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
