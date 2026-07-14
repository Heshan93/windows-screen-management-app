using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;

namespace OrderBuddy.ScreenManager.Core.Config;

internal static class ConfigFileAcl
{
    public static bool ShouldApplyAcl(string filePath)
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(programData))
        {
            return false;
        }

        var normalizedTarget = Path.GetFullPath(filePath);
        var normalizedProgramData = Path.GetFullPath(programData)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return normalizedTarget.StartsWith(normalizedProgramData, StringComparison.OrdinalIgnoreCase);
    }

    [SupportedOSPlatform("windows")]
    public static bool TryApplyAdminSystemWriteAcl(string filePath, out string diagnostic)
    {
        diagnostic = "ACL applied";

        try
        {
            if (!File.Exists(filePath))
            {
                diagnostic = "File does not exist";
                return false;
            }

            var fileSecurity = new FileSecurity();
            fileSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var usersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

            fileSecurity.AddAccessRule(new FileSystemAccessRule(
                adminSid,
                FileSystemRights.FullControl,
                AccessControlType.Allow));
            fileSecurity.AddAccessRule(new FileSystemAccessRule(
                systemSid,
                FileSystemRights.FullControl,
                AccessControlType.Allow));
            fileSecurity.AddAccessRule(new FileSystemAccessRule(
                usersSid,
                FileSystemRights.ReadAndExecute,
                AccessControlType.Allow));

            var fileInfo = new FileInfo(filePath);
            fileInfo.SetAccessControl(fileSecurity);
            return true;
        }
        catch (Exception ex)
        {
            diagnostic = $"ACL update failed: {ex.GetType().Name}";
            return false;
        }
    }
}
