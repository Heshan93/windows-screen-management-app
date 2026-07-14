using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace OrderBuddy.ScreenManager.App.Diagnostics;

/// <summary>
/// Dependency-free, fail-safe startup logger. Writes plain-text diagnostics to the
/// first writable location so we can diagnose crashes that happen <b>before</b> Serilog
/// is configured, or when the app dies silently on a fresh PC. This class must never throw.
/// </summary>
internal static class StartupDiagnostics
{
    private static readonly object Sync = new();
    private static string? _resolvedPath;
    private static bool _headerWritten;

    /// <summary>The log file actually being written to (resolved on first use), or null if none is writable.</summary>
    public static string? LogPath => _resolvedPath;

    /// <summary>
    /// Writes a timestamped diagnostic line. Optionally records an exception with full detail.
    /// Guaranteed not to throw.
    /// </summary>
    public static void Write(string stage, Exception? exception = null)
    {
        try
        {
            lock (Sync)
            {
                var path = ResolvePath();
                if (path is null)
                {
                    return;
                }

                var sb = new StringBuilder();
                if (!_headerWritten)
                {
                    sb.AppendLine(BuildHeader());
                    _headerWritten = true;
                }

                sb.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
                sb.Append("  ");
                sb.Append(stage);

                if (exception is not null)
                {
                    sb.AppendLine();
                    sb.AppendLine("    EXCEPTION: " + exception.GetType().FullName + ": " + exception.Message);
                    sb.AppendLine("    " + exception.ToString().Replace("\n", "\n    "));
                }

                File.AppendAllText(path, sb.ToString() + Environment.NewLine);
            }
        }
        catch
        {
            // Diagnostics must never crash the app.
        }
    }

    /// <summary>Resolves the first writable log location. Tries ProgramData, then TEMP, then exe folder, then Desktop.</summary>
    private static string? ResolvePath()
    {
        if (_resolvedPath is not null)
        {
            return _resolvedPath;
        }

        var candidates = new List<string>();

        TryAdd(candidates, () => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "OrderBuddy", "ScreenManager", "startup-diagnostics.log"));

        TryAdd(candidates, () => Path.Combine(Path.GetTempPath(), "OrderBuddy-ScreenManager-startup.log"));

        TryAdd(candidates, () =>
        {
            var exeDir = Path.GetDirectoryName(Environment.ProcessPath ?? AppContext.BaseDirectory) ?? AppContext.BaseDirectory;
            return Path.Combine(exeDir, "startup-diagnostics.log");
        });

        TryAdd(candidates, () => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "OrderBuddy-ScreenManager-startup.log"));

        foreach (var candidate in candidates)
        {
            try
            {
                var dir = Path.GetDirectoryName(candidate);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Probe writability.
                File.AppendAllText(candidate, string.Empty);
                _resolvedPath = candidate;
                return _resolvedPath;
            }
            catch
            {
                // Try the next candidate.
            }
        }

        return null;
    }

    private static void TryAdd(List<string> list, Func<string> factory)
    {
        try
        {
            var value = factory();
            if (!string.IsNullOrWhiteSpace(value))
            {
                list.Add(value);
            }
        }
        catch
        {
            // Ignore candidate resolution failures.
        }
    }

    private static string BuildHeader()
    {
        var sb = new StringBuilder();
        sb.AppendLine("==================================================================");
        sb.AppendLine("  OrderBuddy Screen Manager — startup diagnostics");
        sb.AppendLine("==================================================================");
        Safe(sb, "  Time (local)   : " + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
        Safe(sb, "  Machine        : " + Environment.MachineName);
        Safe(sb, "  User           : " + Environment.UserName);
        Safe(sb, "  Elevated       : " + IsElevated());
        Safe(sb, "  OS             : " + RuntimeInformation.OSDescription);
        Safe(sb, "  Architecture   : proc=" + RuntimeInformation.ProcessArchitecture + " os=" + RuntimeInformation.OSArchitecture);
        Safe(sb, "  .NET runtime   : " + RuntimeInformation.FrameworkDescription);
        Safe(sb, "  64-bit process : " + Environment.Is64BitProcess);
        Safe(sb, "  Process path   : " + (Environment.ProcessPath ?? "(unknown)"));
        Safe(sb, "  Base directory : " + AppContext.BaseDirectory);
        Safe(sb, "  Command line   : " + Environment.CommandLine);
        Safe(sb, "  WebView2       : " + DetectWebView2());
        sb.Append("------------------------------------------------------------------");
        return sb.ToString();
    }

    private static void Safe(StringBuilder sb, string line)
    {
        try
        {
            sb.AppendLine(line);
        }
        catch
        {
            // Ignore.
        }
    }

    private static string IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator).ToString();
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Detects the installed WebView2 Evergreen runtime version. A missing runtime is one of the most
    /// common reasons the app fails on a fresh PC, so we capture it explicitly.
    /// </summary>
    private static string DetectWebView2()
    {
        try
        {
            var version = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();
            return string.IsNullOrWhiteSpace(version) ? "NOT INSTALLED (no version returned)" : version;
        }
        catch (Exception ex)
        {
            return "NOT INSTALLED / not usable — " + ex.GetType().Name + ": " + ex.Message;
        }
    }
}
