using Microsoft.Win32;
using Serilog;

namespace OrderBuddy.ScreenManager.App.Browser;

/// <summary>
/// Ensures WebView2/Edge autoplay policies are set in the registry so audio/video
/// plays without user gesture — survives WebView2 runtime updates and machine policy overrides.
/// </summary>
internal static class AutoplayPolicyEnforcer
{
    private const string EdgeWebView2PolicyPath = @"SOFTWARE\Policies\Microsoft\Edge\WebView2";
    private const string EdgePolicyPath = @"SOFTWARE\Policies\Microsoft\Edge";

    /// <summary>
    /// Writes registry keys that allow autoplay for all sites in WebView2.
    /// Falls back gracefully if the process lacks admin rights (non-HKLM write).
    /// </summary>
    public static void Apply(IReadOnlyList<string> allowedDomains)
    {
        try
        {
            // Policy 1: WebView2-specific — "AutoplayAllowed" = 1 (allow all autoplay)
            EnsureRegistryValue(Registry.LocalMachine, EdgeWebView2PolicyPath, "AutoplayAllowed", 1);

            // Policy 2: Edge-level AutoplayAllowed (covers any Edge policy inheritance)
            EnsureRegistryValue(Registry.LocalMachine, EdgePolicyPath, "AutoplayAllowed", 1);

            // Policy 3: Per-domain allowlist — belt-and-suspenders for specific domains
            if (allowedDomains.Count > 0)
            {
                var allowlistPath = EdgePolicyPath + @"\AutoplayAllowlist";
                using var key = Registry.LocalMachine.CreateSubKey(allowlistPath, writable: true);
                if (key is not null)
                {
                    for (var i = 0; i < allowedDomains.Count; i++)
                    {
                        key.SetValue((i + 1).ToString(), allowedDomains[i], RegistryValueKind.String);
                    }
                }

                var wv2AllowlistPath = EdgeWebView2PolicyPath + @"\AutoplayAllowlist";
                using var wv2Key = Registry.LocalMachine.CreateSubKey(wv2AllowlistPath, writable: true);
                if (wv2Key is not null)
                {
                    for (var i = 0; i < allowedDomains.Count; i++)
                    {
                        wv2Key.SetValue((i + 1).ToString(), allowedDomains[i], RegistryValueKind.String);
                    }
                }
            }

            Log.Information(
                "Autoplay registry policies applied. Domains: {DomainCount}",
                allowedDomains.Count);
        }
        catch (UnauthorizedAccessException)
        {
            // Running as standard user — HKLM write not possible.
            // The --autoplay-policy arg still works; this is just extra insurance.
            Log.Warning("Autoplay registry policy: skipped (no admin rights). The browser argument fallback is active.");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Autoplay registry policy enforcement failed. Browser argument fallback is active.");
        }
    }

    private static void EnsureRegistryValue(RegistryKey root, string subKeyPath, string valueName, object value)
    {
        using var key = root.CreateSubKey(subKeyPath, writable: true);
        key?.SetValue(valueName, value);
    }
}
