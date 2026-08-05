using System.Text.Json.Serialization;

namespace OrderBuddy.ScreenManager.Core.Config;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(CacheMaintenanceSettings))]
[JsonSerializable(typeof(ScreenCredentials))]
internal partial class ConfigJsonContext : JsonSerializerContext
{
}
