using System.Runtime.InteropServices;

namespace OrderBuddy.ScreenManager.Core;

/// <summary>
/// Detects system-wide user inactivity using the Win32 GetLastInputInfo API.
/// On a kiosk device this is a reliable proxy for "screen is idle" — if no
/// mouse, keyboard, or touch input has occurred for the threshold period, no
/// active order or interaction is in progress.
/// </summary>
public static class KioskIdleDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo plii);

    /// <summary>
    /// Returns the elapsed time since the last user input event (mouse, keyboard, touch).
    /// </summary>
    public static TimeSpan GetIdleDuration()
    {
        var info = new LastInputInfo { cbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info))
        {
            // If the call fails, treat as idle=0 so we don't block clears indefinitely.
            return TimeSpan.Zero;
        }

        var idleTicks = (uint)Environment.TickCount - info.dwTime;
        return TimeSpan.FromMilliseconds(idleTicks);
    }

    /// <summary>
    /// Returns true when the system has been idle (no user input) for at least
    /// <paramref name="threshold"/>.  Always returns true when threshold is zero.
    /// </summary>
    public static bool IsIdle(TimeSpan threshold)
    {
        if (threshold <= TimeSpan.Zero)
        {
            return true;
        }

        return GetIdleDuration() >= threshold;
    }
}
