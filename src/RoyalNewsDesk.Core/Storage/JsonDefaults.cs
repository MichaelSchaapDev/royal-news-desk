using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoyalNewsDesk.Core.Storage;

/// <summary>Shared JSON settings so every stored file looks the same.</summary>
public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
