using System.Text.Json;

namespace OrderBuddy.ScreenManager.Core.Config;

internal static class ConfigMigrator
{
    public static bool TryMigrate(string json, out AppConfig migratedConfig, out string diagnostic)
    {
        migratedConfig = AppConfig.CreateDefault();
        diagnostic = string.Empty;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Check both PascalCase ("SchemaVersion") and camelCase ("schemaVersion") to handle
        // any JSON written by older builds or external tools.
        if ((root.TryGetProperty("SchemaVersion", out var versionEl)
             || root.TryGetProperty("schemaVersion", out versionEl))
            && versionEl.TryGetInt32(out var schemaVersion))
        {
            // v2 → v3: add CacheMaintenance defaults (new in P13).
            if (schemaVersion == 2)
            {
                // Deserialize what we have, then patch the new section in.
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var v2Config = System.Text.Json.JsonSerializer.Deserialize<AppConfig>(json, options);
                if (v2Config is not null)
                {
                    migratedConfig = v2Config with
                    {
                        SchemaVersion = AppConfig.CurrentSchemaVersion,
                        General = v2Config.General with
                        {
                            CacheMaintenance = new CacheMaintenanceSettings()
                        }
                    };
                    diagnostic = "Migrated schemaVersion 2 → 3: added CacheMaintenance defaults.";
                    return true;
                }
            }

            // Already current — no migration needed.
            return false;
        }

        // Legacy/v1 marker used by previous app shape.
        if (!root.TryGetProperty("version", out _))
        {
            return false;
        }

        var autoStart = root.TryGetProperty("autostart", out var autostartEl) && autostartEl.ValueKind is JsonValueKind.True;

        var screens = new List<ScreenConfig>();
        if (root.TryGetProperty("screens", out var screensEl) && screensEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in screensEl.EnumerateArray())
            {
                var id = s.TryGetProperty("screen_id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                    ? idEl.GetString() ?? Guid.NewGuid().ToString("N")
                    : Guid.NewGuid().ToString("N");

                var url = s.TryGetProperty("url", out var urlEl) && urlEl.ValueKind == JsonValueKind.String
                    ? urlEl.GetString() ?? "https://example.org"
                    : "https://example.org";

                var rotation = s.TryGetProperty("rotation_degrees", out var rotationEl) && rotationEl.TryGetInt32(out var rotationValue)
                    ? rotationValue
                    : 0;

                var zoom = s.TryGetProperty("zoom_percent", out var zoomEl) && zoomEl.TryGetInt32(out var zoomValue)
                    ? zoomValue
                    : 100;

                var refresh = s.TryGetProperty("refresh_interval_sec", out var refreshEl) && refreshEl.TryGetInt32(out var refreshValue)
                    ? refreshValue
                    : 0;

                screens.Add(new ScreenConfig
                {
                    Id = id,
                    Name = "Migrated Screen",
                    Url = url,
                    RotationDegrees = rotation,
                    ZoomPercent = zoom,
                    ReloadIntervalSec = refresh,
                    AppType = AppType.Custom,
                    FullScreen = true,
                    Enabled = true
                });
            }
        }

        migratedConfig = AppConfig.CreateDefault() with
        {
            General = AppConfig.CreateDefault().General with
            {
                AutoStartOnLogin = autoStart
            },
            Screens = screens
        };

        diagnostic = "Migrated legacy v1 config format to schemaVersion 3.";
        return true;
    }
}
