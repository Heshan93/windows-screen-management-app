using System.Text.Json;
using FluentAssertions;
using OrderBuddy.ScreenManager.Core.Config;
using OrderBuddy.ScreenManager.Core.Security;

namespace ScreenManager.Tests;

/// <summary>
/// P13: Cache maintenance + kiosk auto-login unit tests.
/// Covers: DPAPI round-trip, scope validation, migration v2→v3, validator range guards,
/// and IsOriginAllowed logic (via the static helper extracted for testability).
/// </summary>
public sealed class P13CacheAndCredentialsTests
{
    // -----------------------------------------------------------------------
    // P13-8  AdminCredentialsService — DPAPI protect/unprotect
    // -----------------------------------------------------------------------

    [Fact]
    public void AdminCredentialsService_Protect_ProducesNonEmptyBlob()
    {
        var blob = AdminCredentialsService.Protect("secret123");
        blob.Should().NotBeNullOrEmpty();
        blob.Should().NotBe("secret123");
    }

    [Fact]
    public void AdminCredentialsService_Protect_Unprotect_RoundTrips()
    {
        const string plain = "Pa$$w0rd!";
        var blob = AdminCredentialsService.Protect(plain);
        var recovered = AdminCredentialsService.Unprotect(blob);
        recovered.Should().Be(plain);
    }

    [Fact]
    public void AdminCredentialsService_Protect_EmptyInput_ReturnsEmpty()
    {
        AdminCredentialsService.Protect(string.Empty).Should().BeEmpty();
        AdminCredentialsService.Protect(null).Should().BeEmpty();
    }

    [Fact]
    public void AdminCredentialsService_Unprotect_EmptyInput_ReturnsEmpty()
    {
        AdminCredentialsService.Unprotect(string.Empty).Should().BeEmpty();
        AdminCredentialsService.Unprotect(null).Should().BeEmpty();
    }

    [Fact]
    public void AdminCredentialsService_Unprotect_GarbageInput_ReturnsEmpty()
    {
        // Invalid base64 or corrupted blob should never throw — just return empty.
        AdminCredentialsService.Unprotect("not-a-valid-blob!!").Should().BeEmpty();
    }

    [Fact]
    public void AdminCredentialsService_HasCredential_TrueAfterProtect()
    {
        var blob = AdminCredentialsService.Protect("password");
        AdminCredentialsService.HasCredential(blob).Should().BeTrue();
    }

    [Fact]
    public void AdminCredentialsService_HasCredential_FalseForEmpty()
    {
        AdminCredentialsService.HasCredential(string.Empty).Should().BeFalse();
        AdminCredentialsService.HasCredential(null).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // P13-1  CacheMaintenanceSettings — defaults + serialization
    // -----------------------------------------------------------------------

    [Fact]
    public void CacheMaintenanceSettings_DefaultValues_AreCorrect()
    {
        var s = new CacheMaintenanceSettings();
        s.Enabled.Should().BeTrue();
        s.IntervalMinutes.Should().Be(60);
        s.Scope.Should().Be("cacheAndSession");
        s.ReloadAfterClear.Should().BeTrue();
    }

    [Fact]
    public void AppConfig_CacheMaintenanceSettings_RoundTripsJson()
    {
        var config = AppConfig.CreateDefault();
        var json = JsonSerializer.Serialize(config);
        var rt = JsonSerializer.Deserialize<AppConfig>(json)!;
        rt.General.CacheMaintenance.Scope.Should().Be("cacheAndSession");
        rt.General.CacheMaintenance.IntervalMinutes.Should().Be(60);
    }

    // -----------------------------------------------------------------------
    // P13-9  ScreenCredentials — defaults + serialization
    // -----------------------------------------------------------------------

    [Fact]
    public void ScreenCredentials_DefaultValues_AreCorrect()
    {
        var c = new ScreenCredentials();
        c.AutoLoginEnabled.Should().BeFalse();
        c.Username.Should().BeEmpty();
        c.EncryptedPassword.Should().BeEmpty();
    }

    [Fact]
    public void ScreenConfig_Credentials_SerializesWithoutPlaintext()
    {
        var screen = new ScreenConfig
        {
            Credentials = new ScreenCredentials
            {
                AutoLoginEnabled = true,
                Username = "admin",
                EncryptedPassword = AdminCredentialsService.Protect("secret")
            }
        };
        var json = JsonSerializer.Serialize(screen);
        json.Should().NotContain("secret");
        json.Should().Contain("admin");
        json.Should().Contain("EncryptedPassword");
    }

    // -----------------------------------------------------------------------
    // P13-2  Migration v2 → v3
    // -----------------------------------------------------------------------

    [Fact]
    public void ConfigMigrator_V2Config_MigratesTo_V3WithCacheMaintenanceDefaults()
    {
        // Build a v3 config (current), serialise it normally, then patch SchemaVersion
        // in the raw JSON string to simulate a v2 file on disk.
        var currentConfig = AppConfig.CreateDefault();
        var currentJson = JsonSerializer.Serialize(currentConfig);

        // Patch schemaVersion to simulate a file written by an older v2 build.
        var v2Json = currentJson.Replace(
            $"\"SchemaVersion\":{AppConfig.CurrentSchemaVersion}",
            "\"SchemaVersion\":2");

        var migrated = ConfigMigrator.TryMigrate(v2Json, out var config, out var diagnostic);

        migrated.Should().BeTrue();
        config.SchemaVersion.Should().Be(AppConfig.CurrentSchemaVersion);
        config.General.CacheMaintenance.Should().NotBeNull();
        config.General.CacheMaintenance.Enabled.Should().BeTrue();
        config.General.CacheMaintenance.IntervalMinutes.Should().Be(60);
        config.General.CacheMaintenance.Scope.Should().Be("cacheAndSession");
        diagnostic.Should().Contain("3");
    }

    [Fact]
    public void ConfigMigrator_CurrentVersionConfig_ReturnsNoMigration()
    {
        var currentJson = JsonSerializer.Serialize(AppConfig.CreateDefault());
        var migrated = ConfigMigrator.TryMigrate(currentJson, out _, out _);
        migrated.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // P13-2  Validator range guards for CacheMaintenance
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void ConfigValidator_CacheIntervalBelowMin_ClampsTo5(int tooLow)
    {
        var config = AppConfig.CreateDefault() with
        {
            General = AppConfig.CreateDefault().General with
            {
                CacheMaintenance = new CacheMaintenanceSettings { IntervalMinutes = tooLow }
            }
        };
        var result = ConfigValidator.Normalize(config);
        result.NormalizedConfig.General.CacheMaintenance.IntervalMinutes.Should().Be(5);
        result.Diagnostics.Should().Contain(d => d.Contains("intervalMinutes") && d.Contains("5"));
    }

    [Fact]
    public void ConfigValidator_ValidCacheInterval_PassesThrough()
    {
        var config = AppConfig.CreateDefault() with
        {
            General = AppConfig.CreateDefault().General with
            {
                CacheMaintenance = new CacheMaintenanceSettings { IntervalMinutes = 30 }
            }
        };
        var result = ConfigValidator.Normalize(config);
        result.NormalizedConfig.General.CacheMaintenance.IntervalMinutes.Should().Be(30);
    }

    [Fact]
    public void ConfigValidator_InvalidScope_ResetsToDefault()
    {
        var config = AppConfig.CreateDefault() with
        {
            General = AppConfig.CreateDefault().General with
            {
                CacheMaintenance = new CacheMaintenanceSettings { Scope = "everything" }
            }
        };
        var result = ConfigValidator.Normalize(config);
        result.NormalizedConfig.General.CacheMaintenance.Scope.Should().Be("cacheAndSession");
        result.Diagnostics.Should().Contain(d => d.Contains("scope"));
    }

    [Theory]
    [InlineData("cacheOnly")]
    [InlineData("cacheAndSession")]
    public void ConfigValidator_ValidScope_PassesThrough(string scope)
    {
        var config = AppConfig.CreateDefault() with
        {
            General = AppConfig.CreateDefault().General with
            {
                CacheMaintenance = new CacheMaintenanceSettings { Scope = scope }
            }
        };
        var result = ConfigValidator.Normalize(config);
        result.NormalizedConfig.General.CacheMaintenance.Scope.Should().Be(scope);
    }

    // -----------------------------------------------------------------------
    // P13-12  IsOriginAllowed — origin allow-list gate (tested via static logic)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("https://till.example.com/login", "https://till.example.com/dashboard", true)]
    [InlineData("https://till.example.com/", "https://till.example.com", true)]
    [InlineData("https://evil.example.com/", "https://till.example.com", false)]
    [InlineData("http://till.example.com/", "https://till.example.com", false)]   // scheme mismatch
    [InlineData("https://till.example.com:8443/", "https://till.example.com:8443/app", true)]
    [InlineData("https://till.example.com:8443/", "https://till.example.com/app", false)]  // port mismatch
    [InlineData(null, "https://till.example.com", false)]
    [InlineData("not-a-uri", "https://till.example.com", false)]
    public void IsOriginAllowed_MatchesExpected(string? pageSource, string configuredUrl, bool expected)
    {
        var result = OriginHelper.IsAllowed(pageSource, configuredUrl);
        result.Should().Be(expected);
    }
}

/// <summary>
/// Mirrors the origin-check logic from KioskWindow.IsOriginAllowed for testability
/// without pulling in the WPF assembly.
/// </summary>
internal static class OriginHelper
{
    public static bool IsAllowed(string? pageSource, string configuredUrl)
    {
        if (string.IsNullOrEmpty(pageSource) || string.IsNullOrEmpty(configuredUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(pageSource, UriKind.Absolute, out var pageUri))
        {
            return false;
        }

        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var configUri))
        {
            return false;
        }

        return string.Equals(pageUri.Host, configUri.Host, StringComparison.OrdinalIgnoreCase)
            && pageUri.Port == configUri.Port
            && string.Equals(pageUri.Scheme, configUri.Scheme, StringComparison.OrdinalIgnoreCase);
    }
}
