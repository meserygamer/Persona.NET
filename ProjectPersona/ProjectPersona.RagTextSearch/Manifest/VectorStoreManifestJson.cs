using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectPersona.RagTextSearch.Manifest;

/// <summary> Сериализация <see cref="VectorStoreManifest"/> для хранения в служебной таблице. </summary>
public static class VectorStoreManifestJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary> Сериализует манифест в JSON. </summary>
    /// <param name="manifest">Манифест.</param>
    /// <returns>JSON-строка.</returns>
    public static string Serialize(VectorStoreManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.Serialize(manifest, SerializerOptions);
    }

    /// <summary> Разбирает манифест из JSON. </summary>
    /// <param name="json">JSON-строка.</param>
    /// <returns>Манифест.</returns>
    /// <exception cref="InvalidOperationException">JSON повреждён или не содержит обязательных полей.</exception>
    public static VectorStoreManifest Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            return JsonSerializer.Deserialize<VectorStoreManifest>(json, SerializerOptions)
                   ?? throw new InvalidOperationException("The vector store manifest is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"The vector store manifest is corrupted: {ex.Message}", ex);
        }
    }
}
