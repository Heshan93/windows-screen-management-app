using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32;
using OrderBuddy.ScreenManager.Core.Config;
using OrderBuddy.ScreenManager.Core.Lifecycle;

namespace OrderBuddy.ScreenManager.App.Lifecycle;

public interface IAutoStartService
{
    Task<AutoStartStatus> ApplyAsync(AppConfig config, CancellationToken cancellationToken = default);
    Task<AutoStartStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

public sealed record AutoStartStatus(bool EnabledByConfig, bool IsRegistered, bool RestartPolicyEnabled, string Detail);

public sealed class WindowsAutoStartService : IAutoStartService
{
    private const string TaskName = "OrderBuddy.ScreenManager.v2";
    private const string RunValueName = "OrderBuddy.ScreenManager.v2";
    private readonly Serilog.ILogger _logger;

    public WindowsAutoStartService(Serilog.ILogger logger)
    {
        _logger = logger;
    }

    public async Task<AutoStartStatus> ApplyAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        if (!config.General.AutoStartOnLogin)
        {
            await DeleteTaskIfExistsAsync(cancellationToken);
            DeleteRunKeyValue();
            return new AutoStartStatus(false, false, false, "Auto-start disabled in config.");
        }

        var action = ResolveTaskAction();
        if (string.IsNullOrWhiteSpace(action.Command))
        {
            return new AutoStartStatus(true, false, false, "Unable to resolve application launch command.");
        }

        var currentIdentity = WindowsIdentity.GetCurrent();
        var userSid = currentIdentity.User?.Value ?? string.Empty;
        var isElevated = IsCurrentProcessElevated(currentIdentity);

        if (string.IsNullOrWhiteSpace(userSid))
        {
            return new AutoStartStatus(true, false, false, "Unable to resolve current user SID for scheduled task registration.");
        }

        var xml = StartupTaskXmlBuilder.Build(
            TaskName,
            "OrderBuddy Screen Manager startup task",
            "OrderBuddy",
            userSid,
            action.Command,
            action.Arguments,
            useHighestPrivileges: isElevated);

        var tempXml = Path.Combine(Path.GetTempPath(), $"orderbuddy-startup-{Guid.NewGuid():N}.xml");
        try
        {
            await File.WriteAllTextAsync(tempXml, xml, cancellationToken);

            var create = await RunSchtasksAsync($"/Create /TN \"{TaskName}\" /XML \"{tempXml}\" /F", cancellationToken);
            if (create.ExitCode != 0)
            {
                _logger.Warning("Failed to register startup task. ExitCode: {ExitCode}, Output: {Output}", create.ExitCode, create.Output);

                var runFallback = TrySetRunKey(action.Command, action.Arguments, out var runDetail);
                return runFallback
                    ? new AutoStartStatus(true, true, false, $"Startup task unavailable; using HKCU Run fallback. {runDetail}")
                    : new AutoStartStatus(true, false, false, $"Scheduled task registration failed and HKCU Run fallback failed. {runDetail}");
            }

            var status = await GetStatusAsync(cancellationToken);
            _logger.Information(
                "Auto-start registration applied. Registered: {Registered}, RestartPolicyEnabled: {RestartPolicyEnabled}, Detail: {Detail}",
                status.IsRegistered,
                status.RestartPolicyEnabled,
                status.Detail);
            return status with { EnabledByConfig = true };
        }
        finally
        {
            try
            {
                if (File.Exists(tempXml))
                {
                    File.Delete(tempXml);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    public async Task<AutoStartStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var query = await RunSchtasksAsync($"/Query /TN \"{TaskName}\" /XML", cancellationToken);
        if (query.ExitCode == 0)
        {
            var hasRestartPolicy =
                query.Output.Contains("<RestartOnFailure>", StringComparison.OrdinalIgnoreCase) &&
                query.Output.Contains("<Interval>PT1M</Interval>", StringComparison.OrdinalIgnoreCase);

            return new AutoStartStatus(false, true, hasRestartPolicy, hasRestartPolicy
                ? "Startup task is registered with restart-on-failure policy."
                : "Startup task is registered but restart policy is missing.");
        }

        if (TryGetRunKeyValue(out var runValue) && !string.IsNullOrWhiteSpace(runValue))
        {
            return new AutoStartStatus(false, true, false, "Auto-start configured via HKCU Run fallback.");
        }

        return new AutoStartStatus(false, false, false, "Startup task not found.");
    }

    private async Task DeleteTaskIfExistsAsync(CancellationToken cancellationToken)
    {
        var result = await RunSchtasksAsync($"/Delete /TN \"{TaskName}\" /F", cancellationToken);
        if (result.ExitCode != 0)
        {
            _logger.Debug("Startup task delete attempt finished with code {ExitCode}: {Output}", result.ExitCode, result.Output);
        }
    }

    private static (string Command, string Arguments) ResolveTaskAction()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            processPath = Process.GetCurrentProcess().MainModule?.FileName;
        }

        if (string.IsNullOrWhiteSpace(processPath))
        {
            return (string.Empty, string.Empty);
        }

        if (processPath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            var assemblyName = typeof(App).Assembly.GetName().Name;
            var assemblyPath = string.IsNullOrWhiteSpace(assemblyName)
                ? string.Empty
                : Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.dll");
            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                return (string.Empty, string.Empty);
            }

            return (processPath, $"\"{assemblyPath}\"");
        }

        return (processPath, string.Empty);
    }

    private static async Task<(int ExitCode, string Output)> RunSchtasksAsync(string args, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo("schtasks.exe", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var output = (await stdoutTask) + Environment.NewLine + (await stderrTask);

        return (process.ExitCode, output.Trim());
    }

    private static bool TrySetRunKey(string command, string arguments, out string detail)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true)
                           ?? Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);

            if (key is null)
            {
                detail = "Could not open HKCU Run key.";
                return false;
            }

            var value = string.IsNullOrWhiteSpace(arguments)
                ? Quote(command)
                : $"{Quote(command)} {arguments}";
            key.SetValue(RunValueName, value, RegistryValueKind.String);

            detail = "HKCU Run value was updated.";
            return true;
        }
        catch (Exception ex)
        {
            detail = ex.Message;
            return false;
        }
    }

    private static bool TryGetRunKeyValue(out string value)
    {
        value = string.Empty;
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: false);
        var raw = key?.GetValue(RunValueName) as string;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        value = raw;
        return true;
    }

    private static void DeleteRunKeyValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        key?.DeleteValue(RunValueName, throwOnMissingValue: false);
    }

    private static string Quote(string value)
    {
        return value.Contains(' ') ? $"\"{value}\"" : value;
    }

    private static bool IsCurrentProcessElevated(WindowsIdentity identity)
    {
        try
        {
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
