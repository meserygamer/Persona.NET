using ProjectPersona.RagTextSearch.Manifest;

namespace ProjectPersona.RagTextSearch.Llama;

/// <summary>
/// Описание эмбеддера по метаданным GGUF и <see cref="LlamaSettings"/>, без хеша файла модели.
/// </summary>
public sealed class LlamaEmbeddingProfileProvider : IEmbeddingProfileProvider
{
    /// <summary> Настройки Llama. </summary>
    private readonly LlamaSettings _settings;
    /// <summary> Сессия модели: метаданные GGUF, размерность и pooling. </summary>
    private readonly LlamaModelSessionHolder _sessionHolder;

    /// <summary> Создаёт источник описания эмбеддера. </summary>
    /// <param name="settings">Настройки Llama.</param>
    /// <param name="sessionHolder">Сессия модели.</param>
    /// <exception cref="ArgumentNullException">Любой из параметров равен <see langword="null"/>.</exception>
    public LlamaEmbeddingProfileProvider(LlamaSettings settings, LlamaModelSessionHolder sessionHolder)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _sessionHolder = sessionHolder ?? throw new ArgumentNullException(nameof(sessionHolder));
    }

    /// <inheritdoc />
    public EmbeddingProfile GetProfile(CancellationToken ct = default)
    {
        var session = _sessionHolder.GetSession(ct);

        // Дата изменения файла не учитывается: копия модели не должна делать базу «чужой».
        var fingerprint = new EmbeddingFingerprint(
            session.GetMetadataValue("general.name"),
            session.GetMetadataValue("general.architecture"),
            session.GetMetadataValue("general.file_type"),
            session.EmbeddingDimension,
            new FileInfo(_settings.ModelPath.Trim()).Length,
            session.PoolingType);

        var buildParameters = new EmbeddingBuildParameters(
            _settings.BatchSize,
            _settings.FreeSpaceClearance,
            _settings.OverlapTokens);

        return new EmbeddingProfile(
            fingerprint,
            buildParameters,
            _settings.DocumentQueryTemplate,
            _settings.SearchQueryTemplate);
    }
}
