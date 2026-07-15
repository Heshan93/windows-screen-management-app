using System.Text.Json;
using FluentAssertions;
using OrderBuddy.ScreenManager.Core.Config;

namespace ScreenManager.Tests;

public sealed class P2ConfigServiceTests
{
    private static (string RootDir, ConfigServiceOptions Options) CreateOptions()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), "ScreenManagerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootDir);
        return (rootDir, ConfigServiceOptions.FromRootDirectory(rootDir));
    }

    [Fact]
    public void AppConfig_SystemTextJson_RoundTrips()
    {
        var config = AppConfig.CreateDefault() with
        {
            Device = new DeviceInfo { DeviceName = "TEST-DEVICE" },
            Screens =
            [
                new ScreenConfig { Name = "Till", Url = "https://example.org", AppType = AppType.Till }
            ]
        };

        var json = JsonSerializer.Serialize(config);
        var roundTrip = JsonSerializer.Deserialize<AppConfig>(json);

        roundTrip.Should().NotBeNull();
        roundTrip!.Device.DeviceName.Should().Be("TEST-DEVICE");
        roundTrip.Screens.Should().HaveCount(1);
    }

    [Fact]
    public void Load_ConfigMissing_CreatesDefaultConfigFile()
    {
        var (rootDir, options) = CreateOptions();
        using var service = new ConfigService(options);

        service.CurrentConfig.SchemaVersion.Should().Be(AppConfig.CurrentSchemaVersion);
        File.Exists(options.ConfigFilePath).Should().BeTrue();
        File.Exists(options.BackupFilePath).Should().BeTrue();

        Directory.Delete(rootDir, true);
    }

    [Fact]
    public void Load_InvalidValues_AreNormalizedAndDiagnosticsRecorded()
    {
        var (rootDir, options) = CreateOptions();
        var badConfig = AppConfig.CreateDefault() with
        {
            General = AppConfig.CreateDefault().General with
            {
                OfflineTimeoutSec = -42,
                AdminPinHash = "1234"
            },
            Screens =
            [
                new ScreenConfig
                {
                    Id = "screen-1",
                    Url = "not-a-url",
                    ZoomPercent = 9999,
                    RotationDegrees = 13
                }
            ]
        };

        File.WriteAllText(options.ConfigFilePath, JsonSerializer.Serialize(badConfig));

        using var service = new ConfigService(options);

        service.CurrentConfig.General.OfflineTimeoutSec.Should().Be(20);
        service.CurrentConfig.Screens[0].Url.Should().Be("https://example.org");
        service.CurrentConfig.Screens[0].ZoomPercent.Should().Be(500);
        service.CurrentConfig.Screens[0].RotationDegrees.Should().Be(0);
        service.CurrentConfig.General.AdminPinHash.Should().StartWith("PBKDF2$");
        service.LastDiagnostics.Should().NotBeEmpty();

        Directory.Delete(rootDir, true);
    }

    [Fact]
    public async Task SaveAsync_ExistingConfig_UsesAtomicReplaceAndCreatesBackup()
    {
        var (rootDir, options) = CreateOptions();
        var first = AppConfig.CreateDefault() with
        {
            Device = new DeviceInfo { DeviceName = "FIRST" }
        };

        await File.WriteAllTextAsync(options.ConfigFilePath, JsonSerializer.Serialize(first));

        using var service = new ConfigService(options);
        var updated = service.CurrentConfig with
        {
            Device = service.CurrentConfig.Device with { DeviceName = "SECOND" }
        };

        await service.SaveAsync(updated);

        var currentJson = await File.ReadAllTextAsync(options.ConfigFilePath);
        var backupJson = await File.ReadAllTextAsync(options.BackupFilePath);

        currentJson.Should().Contain("SECOND");
        backupJson.Should().Contain("FIRST");

        Directory.Delete(rootDir, true);
    }

    [Fact]
    public void Load_PrimaryCorrupt_RecoversFromBackup()
    {
        var (rootDir, options) = CreateOptions();

        File.WriteAllText(options.ConfigFilePath, "{ this-is-corrupt-json");
        var backup = AppConfig.CreateDefault() with
        {
            Device = new DeviceInfo { DeviceName = "FROM-BACKUP" }
        };
        File.WriteAllText(options.BackupFilePath, JsonSerializer.Serialize(backup));

        using var service = new ConfigService(options);

        service.CurrentConfig.Device.DeviceName.Should().Be("FROM-BACKUP");
        service.LastDiagnostics.Should().Contain(d => d.Contains("Recovered config from backup"));

        Directory.Delete(rootDir, true);
    }

    [Fact]
    public void Load_LegacyV1Shape_MigratesToSchemaV2()
    {
        var (rootDir, options) = CreateOptions();

        var legacyJson = """
        {
          "version": 1,
          "autostart": false,
          "screens": [
            {
              "screen_id": "legacy-screen-1",
              "url": "https://legacy.example.org",
              "rotation_degrees": 90,
              "zoom_percent": 120,
              "refresh_interval_sec": 60
            }
          ]
        }
        """;

        File.WriteAllText(options.ConfigFilePath, legacyJson);

        using var service = new ConfigService(options);

        service.CurrentConfig.SchemaVersion.Should().Be(AppConfig.CurrentSchemaVersion);
        service.CurrentConfig.General.AutoStartOnLogin.Should().BeFalse();
        service.CurrentConfig.Screens.Should().ContainSingle();
        service.CurrentConfig.Screens[0].Id.Should().Be("legacy-screen-1");
        service.CurrentConfig.Screens[0].Url.Should().Be("https://legacy.example.org");
        service.LastDiagnostics.Should().Contain(d => d.Contains("Migrated legacy v1 config format"));

        Directory.Delete(rootDir, true);
    }

    [Fact]
    public async Task HotReload_ExternalFileChange_RaisesConfigChangedEvent()
    {
        var (rootDir, options) = CreateOptions();
        using var service = new ConfigService(options);

        var tcs = new TaskCompletionSource<AppConfig>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.ConfigChanged += (_, args) => tcs.TrySetResult(args.NewConfig);

        var updated = service.CurrentConfig with
        {
            Device = service.CurrentConfig.Device with
            {
                DeviceName = "HOT-RELOAD-DEVICE"
            }
        };

        await File.WriteAllTextAsync(options.ConfigFilePath, JsonSerializer.Serialize(updated));

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(tcs.Task);
        var changedConfig = await tcs.Task;
        changedConfig.Device.DeviceName.Should().Be("HOT-RELOAD-DEVICE");

        Directory.Delete(rootDir, true);
    }

    [Fact]
    public async Task SaveAsync_AdminPinIsPersistedAsProtectedHash_NotPlaintext()
    {
        var (rootDir, options) = CreateOptions();
        using var service = new ConfigService(options);

        var plaintext = service.CurrentConfig with
        {
            General = service.CurrentConfig.General with
            {
                AdminPinHash = "9999"
            }
        };

        await service.SaveAsync(plaintext);

        var json = await File.ReadAllTextAsync(options.ConfigFilePath);
        json.Should().NotContain("\"9999\"");
        service.CurrentConfig.General.AdminPinHash.Should().StartWith("PBKDF2$");

        Directory.Delete(rootDir, true);
    }

    [Fact]
    public async Task SaveAsync_RaisesConfigChangedEvent()
    {
        // Regression: admin saves must notify listeners so kiosk windows re-apply
        // the new URL/zoom immediately (the file watcher is suppressed during internal saves).
        var (rootDir, options) = CreateOptions();
        using var service = new ConfigService(options);

        var tcs = new TaskCompletionSource<AppConfig>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.ConfigChanged += (_, args) => tcs.TrySetResult(args.NewConfig);

        var updated = service.CurrentConfig with
        {
            Screens =
            [
                new ScreenConfig { Id = "s1", Name = "Till", Enabled = true, Url = "https://example.org", ZoomPercent = 70 }
            ]
        };

        await service.SaveAsync(updated);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(tcs.Task, "SaveAsync must raise ConfigChanged");
        var changed = await tcs.Task;
        changed.Screens.Should().ContainSingle();
        changed.Screens[0].ZoomPercent.Should().Be(70);

        Directory.Delete(rootDir, true);
    }

    [Fact]
    public async Task SaveAsync_NoChanges_DoesNotRaiseConfigChangedEvent()
    {
        var (rootDir, options) = CreateOptions();
        using var service = new ConfigService(options);

        var raised = false;
        service.ConfigChanged += (_, _) => raised = true;

        await service.SaveAsync(service.CurrentConfig);

        raised.Should().BeFalse("no-op saves should not trigger config change fan-out");
        service.LastDiagnostics.Should().Contain(d => d.Contains("save skipped", StringComparison.OrdinalIgnoreCase));

        Directory.Delete(rootDir, true);
    }

    [Fact]
    public void Load_ZoomBelowMinimum_ClampsTo25()
    {
        var (rootDir, options) = CreateOptions();
        var config = AppConfig.CreateDefault() with
        {
            Screens =
            [
                new ScreenConfig
                {
                    Id = "screen-1",
                    Url = "https://example.org",
                    ZoomPercent = 20
                }
            ]
        };

        File.WriteAllText(options.ConfigFilePath, JsonSerializer.Serialize(config));

        using var service = new ConfigService(options);

        service.CurrentConfig.Screens[0].ZoomPercent.Should().Be(25);
        service.LastDiagnostics.Should().Contain(d => d.Contains("clamped to 25", StringComparison.OrdinalIgnoreCase));

        Directory.Delete(rootDir, true);
    }

    [Theory]
    [InlineData("mypos.local/app", "https://mypos.local/app")]
    [InlineData("localhost:8080", "https://localhost:8080")]
    [InlineData("http://plain.example.com", "http://plain.example.com")]
    [InlineData("https://secure.example.com/x?y=1", "https://secure.example.com/x?y=1")]
    [InlineData("not-a-url", "https://example.org")]
    public void Normalize_Url_AppliesFriendlyNormalizationOrFallback(string input, string expected)
    {
        var (rootDir, options) = CreateOptions();

        var config = AppConfig.CreateDefault() with
        {
            Screens = [new ScreenConfig { Id = "s1", Url = input }]
        };
        File.WriteAllText(options.ConfigFilePath, JsonSerializer.Serialize(config));

        using var service = new ConfigService(options);

        service.CurrentConfig.Screens[0].Url.Should().Be(expected);

        Directory.Delete(rootDir, true);
    }
}

