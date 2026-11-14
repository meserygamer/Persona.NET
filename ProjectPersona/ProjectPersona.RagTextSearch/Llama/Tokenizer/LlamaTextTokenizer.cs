using ProjectPersona.LlamaWrapper;

namespace ProjectPersona.RagTextSearch.Llama.Tokenizer;

/// <summary>
/// Токенизация через общую <see cref="LlamaModelSession"/> и <c>llama.dll</c>.
/// </summary>
public class LlamaTextTokenizer : ITextTokenizer
{
    private readonly LlamaModelSessionHolder _sessionHolder;

    /// <summary>
    /// Создаёт токенизатор поверх shared-сессии модели.
    /// </summary>
    /// <param name="sessionHolder">Holder с ленивой загрузкой GGUF.</param>
    public LlamaTextTokenizer(LlamaModelSessionHolder sessionHolder)
    {
        _sessionHolder = sessionHolder
                         ?? throw new ArgumentNullException(nameof(sessionHolder));
    }

    /// <inheritdoc />
    public ValueTask<long> GetTokenCountAsync(
        string? text,
        bool addSpecialTokens = true,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tokenizer = _sessionHolder.GetSession(cancellationToken).Tokenizer;
        var count = tokenizer.CountTokens(text ?? string.Empty, addSpecialTokens);
        return ValueTask.FromResult((long)count);
    }

    /// <inheritdoc />
    public ValueTask<int[]> GetTokenIdsAsync(
        string? text,
        bool addSpecialTokens = true,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ids = _sessionHolder.GetSession(cancellationToken).Tokenizer
            .GetTokenIds(text ?? string.Empty, addSpecialTokens);
        return ValueTask.FromResult(ids);
    }

    /// <inheritdoc />
    public ValueTask<LlamaModelToken[]> TokenizeAsync(
        string? text,
        bool addSpecialTokens = true,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pieces = _sessionHolder.GetSession(cancellationToken).Tokenizer
            .Tokenize(text ?? string.Empty, addSpecialTokens);
        var tokens = new LlamaModelToken[pieces.Length];
        for (var i = 0; i < pieces.Length; i++)
        {
            tokens[i] = new LlamaModelToken
            {
                Id = pieces[i].Id,
                Piece = pieces[i].Piece
            };
        }

        return ValueTask.FromResult(tokens);
    }
}
