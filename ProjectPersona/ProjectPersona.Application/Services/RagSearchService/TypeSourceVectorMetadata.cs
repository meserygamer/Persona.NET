using System.Text.Json.Serialization;

namespace ProjectPersona.Application.Services.RagSearchService;

/// <summary> Метаданные векторизованного фрагмента объявления из type-source-index. </summary>
public record TypeSourceVectorMetadata
{
    /// <summary> Имя сборки объявления. </summary>
    [JsonPropertyName("assemblyName")]
    public string AssemblyName { get; init; }
    
    /// <summary> Короткий TFM компиляции или пустая строка. </summary>
    [JsonPropertyName("targetFramework")]
    public string TargetFramework { get; init; }
    
    /// <summary> Полное имя типа. </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; }
    
    /// <summary> Имя члена в формате каталога или <see langword="null"/> для шапки типа. </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}