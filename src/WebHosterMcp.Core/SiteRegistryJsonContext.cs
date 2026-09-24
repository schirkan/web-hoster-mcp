using System.Text.Json.Serialization;

namespace WebHosterMcp.Core;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Registry))]
[JsonSerializable(typeof(SiteEntry))]
internal partial class SiteRegistryJsonContext : JsonSerializerContext
{
}
