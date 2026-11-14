using System.Text.Json.Serialization;

namespace ProjectPersona.Application.Services.RagSearchService;

/// <summary> Метаданные векторизованного фрагмента объявления из type-source-index с кодом. </summary>
public record TypeSourceVectorMetadataWithChunk : TypeSourceVectorMetadata
{
    /// <summary> Индекс окна нарезки (0 для единственного фрагмента). </summary>
    [JsonPropertyName("chunkIndex")]
    public int ChunkIndex { get; init; }
    
    /// <summary> Исходники. </summary>
    [JsonPropertyName("source")]
    public string Source { get; init; }
}
