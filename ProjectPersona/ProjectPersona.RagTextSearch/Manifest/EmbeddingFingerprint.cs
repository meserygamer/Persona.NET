namespace ProjectPersona.RagTextSearch.Manifest;

/// <summary>
/// Жёсткая часть отпечатка эмбеддера: всё, что меняет сам вектор. Векторы с разными отпечатками несравнимы.
/// </summary>
/// <param name="ModelName">Имя модели из GGUF (<c>general.name</c>); <see langword="null"/>, если ключа нет.</param>
/// <param name="Architecture">Архитектура из GGUF (<c>general.architecture</c>); <see langword="null"/>, если ключа нет.</param>
/// <param name="FileType">Тип квантования из GGUF (<c>general.file_type</c>); <see langword="null"/>, если ключа нет.</param>
/// <param name="EmbeddingDimension">Длина эмбеддинга модели.</param>
/// <param name="ModelFileSize">Размер GGUF-файла в байтах.</param>
/// <param name="PoolingType">Фактический тип pooling контекста llama.</param>
public sealed record EmbeddingFingerprint(
    string? ModelName,
    string? Architecture,
    string? FileType,
    int EmbeddingDimension,
    long ModelFileSize,
    int PoolingType)
{
    /// <summary> Перечисляет поля, которыми отпечаток отличается от ожидаемого. </summary>
    /// <param name="expected">Отпечаток, с которым сравнивается текущий (обычно из манифеста).</param>
    /// <returns>Описания различий вида «поле: ожидалось X, сейчас Y»; пустой список, если различий нет.</returns>
    public IReadOnlyList<string> DescribeDifferences(EmbeddingFingerprint expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        var differences = new List<string>();
        ManifestDifferences.Add(differences, "model name", expected.ModelName, ModelName);
        ManifestDifferences.Add(differences, "architecture", expected.Architecture, Architecture);
        ManifestDifferences.Add(differences, "file type (quantization)", expected.FileType, FileType);
        ManifestDifferences.Add(differences, "embedding dimension", expected.EmbeddingDimension, EmbeddingDimension);
        ManifestDifferences.Add(differences, "model file size", expected.ModelFileSize, ModelFileSize);
        ManifestDifferences.Add(differences, "pooling type", expected.PoolingType, PoolingType);
        return differences;
    }

    /// <inheritdoc />
    public override string ToString() =>
        $"{ModelName ?? "<unnamed>"} ({Architecture ?? "<unknown architecture>"}, file type {FileType ?? "<unknown>"}, " +
        $"dimension {EmbeddingDimension})";
}
