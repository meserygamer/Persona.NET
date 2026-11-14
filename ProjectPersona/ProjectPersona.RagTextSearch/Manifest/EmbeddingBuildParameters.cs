namespace ProjectPersona.RagTextSearch.Manifest;

/// <summary>
/// Мягкие параметры построения базы: меняют только нарезку на чанки, но не пространство векторов.
/// Векторы с разной нарезкой остаются сравнимыми.
/// </summary>
/// <param name="BatchSize">Лимит токенов окна нарезки документа.</param>
/// <param name="FreeSpaceClearance">Запас свободного места окна; <see langword="null"/> — значение по умолчанию.</param>
/// <param name="OverlapTokens">Перекрытие окон в токенах; <see langword="null"/> — значение по умолчанию.</param>
public sealed record EmbeddingBuildParameters(
    long BatchSize,
    long? FreeSpaceClearance,
    long? OverlapTokens)
{
    /// <summary> Перечисляет параметры, которыми текущие отличаются от ожидаемых. </summary>
    /// <param name="expected">Параметры, с которыми сравниваются текущие (обычно из манифеста).</param>
    /// <returns>Описания различий; пустой список, если различий нет.</returns>
    public IReadOnlyList<string> DescribeDifferences(EmbeddingBuildParameters expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        var differences = new List<string>();
        ManifestDifferences.Add(differences, "batch size", expected.BatchSize, BatchSize);
        ManifestDifferences.Add(differences, "free space clearance", expected.FreeSpaceClearance, FreeSpaceClearance);
        ManifestDifferences.Add(differences, "overlap tokens", expected.OverlapTokens, OverlapTokens);
        return differences;
    }
}
