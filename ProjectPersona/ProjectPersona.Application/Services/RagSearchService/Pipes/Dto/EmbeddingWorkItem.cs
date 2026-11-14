namespace ProjectPersona.Application.Services.RagSearchService.Pipes.Dto;

/// <summary> Рабочая единица для Embedding. </summary>
public readonly record struct EmbeddingWorkItem
{
    /// <summary> Создать рабочую единицу. </summary>
    /// <param name="assembly"> Идентификатор сборки. </param>
    /// <param name="targetFramework"> Короткий TFM компиляции. </param>
    /// <param name="metadata"> Метаданные сборки. </param>
    /// <param name="tokenIds"> Id токенов. </param>
    public EmbeddingWorkItem(string assembly, string targetFramework, string metadata, int[] tokenIds)
    {
        Assembly = assembly;
        TargetFramework = targetFramework;
        Metadata = metadata;
        TokenIds = tokenIds;
    }

    /// <summary> Код сборки декларации. </summary>
    public string Assembly { get; init; }

    /// <summary> Короткий TFM компиляции декларации. </summary>
    public string TargetFramework { get; init; }

    /// <summary> Метаданные. </summary>
    public string Metadata { get; init; }

    /// <summary> Id токенов. </summary>
    public int[] TokenIds { get; init; }
}
