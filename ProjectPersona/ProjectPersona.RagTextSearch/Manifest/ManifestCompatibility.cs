namespace ProjectPersona.RagTextSearch.Manifest;

/// <summary> Результат сверки манифеста с текущим кодом и эмбеддером. </summary>
/// <param name="Errors">Несовместимости: векторы базы нельзя использовать с текущим эмбеддером.</param>
/// <param name="Warnings">Расхождения, не делающие векторы несравнимыми.</param>
public sealed record ManifestCompatibility(
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    /// <summary> Несовместимостей нет. </summary>
    public bool IsCompatible => Errors.Count == 0;

    /// <summary> Сверяет манифест с текущим кодом и эмбеддером. </summary>
    /// <param name="manifest">Манифест базы.</param>
    /// <param name="pipelineVersion">Версия конвейера векторизации в текущем коде.</param>
    /// <param name="profile">Описание текущего эмбеддера.</param>
    /// <returns>
    /// Ошибки: другие <see cref="VectorStoreManifest.FormatVersion"/>, <see cref="VectorStoreManifest.PipelineVersion"/>,
    /// размерность или жёсткая часть отпечатка. Предупреждения: другие мягкие параметры, шаблон документа и шаблон поиска.
    /// </returns>
    public static ManifestCompatibility Check(VectorStoreManifest manifest, int pipelineVersion, EmbeddingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(profile);

        // При другой версии формата остальные поля могут значить другое, сверять их бессмысленно.
        if (manifest.FormatVersion != VectorStoreManifest.CurrentFormatVersion)
        {
            return new ManifestCompatibility(
                [$"manifest format version: index has {manifest.FormatVersion}, " +
                 $"current is {VectorStoreManifest.CurrentFormatVersion}"],
                []);
        }

        var errors = new List<string>();
        ManifestDifferences.Add(errors, "vectorization pipeline version", manifest.PipelineVersion, pipelineVersion);
        ManifestDifferences.Add(errors, "vector dimension", manifest.VectorDimension, profile.Fingerprint.EmbeddingDimension);
        errors.AddRange(profile.Fingerprint.DescribeDifferences(manifest.EmbeddingFingerprint));

        var warnings = new List<string>(profile.BuildParameters.DescribeDifferences(manifest.BuildParameters));
        ManifestDifferences.Add(warnings, "document query template", manifest.DocumentQueryTemplate, profile.DocumentQueryTemplate);
        ManifestDifferences.Add(warnings, "search query template", manifest.SearchQueryTemplate, profile.SearchQueryTemplate);

        return new ManifestCompatibility(errors, warnings);
    }
}
