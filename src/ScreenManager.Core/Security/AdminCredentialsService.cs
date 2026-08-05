using System.Security.Cryptography;

namespace OrderBuddy.ScreenManager.Core.Security;

/// <summary>
/// Encrypts and decrypts kiosk login passwords using DPAPI (LocalMachine scope).
/// LocalMachine scope is used so the encrypted blob is valid regardless of which
/// Windows account the kiosk process runs under (standard user, SYSTEM, scheduled-task).
/// The config file itself is ACL-restricted to Administrators/SYSTEM to limit who can
/// read the encrypted blob.
/// </summary>
public static class AdminCredentialsService
{
    private static readonly byte[] Entropy = [];

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> with DPAPI (LocalMachine) and returns
    /// a base64-encoded blob suitable for storage in config. Returns an empty string
    /// when <paramref name="plaintext"/> is null or empty.
    /// </summary>
    public static string Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return string.Empty;
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var encrypted = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.LocalMachine);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// Decrypts a DPAPI blob produced by <see cref="Protect"/>. Returns an empty string
    /// when the blob is null, empty, or cannot be decrypted.
    /// Never throws — a failure is returned as an empty string and logged by the caller.
    /// </summary>
    public static string Unprotect(string? encryptedBase64)
    {
        if (string.IsNullOrEmpty(encryptedBase64))
        {
            return string.Empty;
        }

        try
        {
            var bytes = Convert.FromBase64String(encryptedBase64);
            var decrypted = ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.LocalMachine);
            return System.Text.Encoding.UTF8.GetString(decrypted);
        }
        catch (CryptographicException)
        {
            // Blob from a different machine / corrupted — return empty rather than crashing.
            return string.Empty;
        }
        catch (FormatException)
        {
            // Not a valid base64 string.
            return string.Empty;
        }
    }

    /// <summary>
    /// Returns true when <paramref name="encryptedBase64"/> is non-empty and can be
    /// successfully decrypted on this machine.
    /// </summary>
    public static bool HasCredential(string? encryptedBase64)
        => !string.IsNullOrEmpty(Unprotect(encryptedBase64));
}
