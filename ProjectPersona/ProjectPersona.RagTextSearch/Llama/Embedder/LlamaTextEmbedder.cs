using ProjectPersona.LlamaWrapper;

namespace ProjectPersona.RagTextSearch.Llama.Embedder;

/// <summary>
/// Эмбеддинги через локальную <c>llama.dll</c> и общую <see cref="LlamaModelSession"/>.
/// </summary>
public class LlamaTextEmbedder : ITextEmbedder
{
    private readonly LlamaModelSessionHolder _sessionHolder;
    private readonly LlamaSettings _settings;

    /// <summary>
    /// Создаёт эмбеддер на базе shared-сессии модели.
    /// </summary>
    /// <param name="sessionHolder">Holder с ленивой загрузкой GGUF.</param>
    /// <param name="settings">Настройки шаблонов и лимитов.</param>
    public LlamaTextEmbedder(LlamaModelSessionHolder sessionHolder, LlamaSettings settings)
    {
        _sessionHolder = sessionHolder
                         ?? throw new ArgumentNullException(nameof(sessionHolder));
        _settings = settings
                    ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc />
    public ValueTask<float[][]> ProcessTextsAsync(
        IEnumerable<string>? queries,
        CancellationToken cancellationToken = default)
    {
        if (queries is null)
            return ValueTask.FromResult(Array.Empty<float[]>());

        var items = queries as IReadOnlyList<string> ?? queries.ToArray();
        if (items.Count == 0)
            return ValueTask.FromResult(Array.Empty<float[]>());

        var session = _sessionHolder.GetSession(cancellationToken);
        var vectors = session.Embed(items, addSpecial: true, parseSpecial: true, cancellationToken);
        return ValueTask.FromResult(vectors);
    }

    /// <inheritdoc />
    public ValueTask<float[][]> ProcessTokenizedTextsAsync(
        IReadOnlyList<int[]> tokenIds,
        CancellationToken cancellationToken = default)
    {
        if (tokenIds.Count == 0)
            return ValueTask.FromResult(Array.Empty<float[]>());

        var session = _sessionHolder.GetSession(cancellationToken);
        var vectors = session.EmbedTokens(tokenIds, cancellationToken);
        return ValueTask.FromResult(vectors);
    }

    /// <inheritdoc />
    public async ValueTask<float[][]> ProcessSearchQueriesAsync(
        IEnumerable<string>? queries,
        CancellationToken cancellationToken = default)
    {
        if (queries is null)
            return Array.Empty<float[]>();

        var rawItems = queries.ToArray();
        if (rawItems.Length == 0)
            return Array.Empty<float[]>();

        var prompts = rawItems.Select(_settings.FormatSearchQuery).ToArray();
        return await ProcessTextsAsync(prompts, cancellationToken).ConfigureAwait(false);
    }
}
