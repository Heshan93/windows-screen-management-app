using System.Text.Json.Serialization;

namespace OrderBuddy.ScreenManager.Core.Config;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppConfig))]
internal partial class ConfigJsonContext : JsonSerializerContext
{
}
