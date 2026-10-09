using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OrderBuddy.ScreenManager.Core.Security;

/// <summary>
/// Masks credential-like values (passwords, tokens, pairing codes, PINs) in JSON, query strings,
/// form bodies and auth headers before they are written to diagnostic logs.
/// </summary>
public static class SensitiveDataRedactor
{
    private const int FingerprintMinLength = 12;

    private static readonly Regex JsonPair = new(
        "\"(?<key>[^\"\\\\]{1,100})\"\\s*:\\s*(?<val>\"(?:[^\"\\\\]|\\\\.)*\"|-?\\d+(?:\\.\\d+)?)",
        RegexOptions.Compiled);

    private static readonly Regex FormPair = new(
        "(?<pre>^|[?&])(?<key>[^=&#\\s]{1,100})=(?<val>[^&#\\s]*)",
        RegexOptions.Compiled);

    private static readonly Regex AuthScheme = new(
        "\\b(?<scheme>Bearer|Basic)\\s+[A-Za-z0-9._~+/=-]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] SensitiveKeyFragments =
    [
        "password", "passwd", "pwd", "token", "secret", "authorization", "apikey", "api_key",
        "pairing_code", "pairingcode", "cvv", "cardnumber", "card_number", "private_key"
    ];

    public static bool IsSensitiveKey(string key)
    {
        var k = key.Trim().ToLowerInvariant();
        if (k is "pin" or "pan" or "auth" || k.EndsWith("_pin", StringComparison.Ordinal) || k.StartsWith("pin_", StringComparison.Ordinal))
        {
            return true;
        }

        return SensitiveKeyFragments.Any(f => k.Contains(f, StringComparison.Ordinal));
    }

    /// <param name="fingerprint">When true, long secrets are replaced with a short hash so changes between log lines are visible.</param>
    public static string Redact(string? text, int maxLength = 2000, bool fingerprint = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var result = JsonPair.Replace(text, m =>
        {
            if (!IsSensitiveKey(m.Groups["key"].Value))
            {
                return m.Value;
            }

            var raw = m.Groups["val"].Value.Trim('"');
            return $"\"{m.Groups["key"].Value}\":\"{Mask(raw, fingerprint)}\"";
        });

        result = FormPair.Replace(result, m => IsSensitiveKey(m.Groups["key"].Value)
            ? $"{m.Groups["pre"].Value}{m.Groups["key"].Value}={Mask(m.Groups["val"].Value, fingerprint)}"
            : m.Value);

        result = AuthScheme.Replace(result, m => $"{m.Groups["scheme"].Value} <redacted>");

        return Truncate(result, maxLength);
    }

    public static string Truncate(string text, int maxLength)
    {
        if (maxLength <= 0 || text.Length <= maxLength)
        {
            return text;
        }

        return text[..maxLength] + $"...(+{text.Length - maxLength} chars)";
    }

    private static string Mask(string value, bool fingerprint)
    {
        if (fingerprint && value.Length >= FingerprintMinLength)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return $"<redacted fp={Convert.ToHexString(hash, 0, 4).ToLowerInvariant()}>";
        }

        return "<redacted>";
    }
}
