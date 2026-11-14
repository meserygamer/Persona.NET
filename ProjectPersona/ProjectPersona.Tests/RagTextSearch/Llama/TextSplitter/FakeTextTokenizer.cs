using ProjectPersona.RagTextSearch;
using ProjectPersona.RagTextSearch.Llama.Tokenizer;

namespace ProjectPersona.Tests.RagTextSearch.Llama.TextSplitter;

/// <summary> Токенизатор без HTTP: один токен на символ, настраиваемый подсчёт длины. </summary>
internal sealed class FakeTextTokenizer : ITextTokenizer
{
    /// <summary> Добавка к длине при <c>addSpecialTokens: true</c>. </summary>
    public int SpecialTokenCount { get; init; }

    /// <summary> Дополнительные «токены» на каждый символ обёрнутого текста (модель неаддитивности). </summary>
    public int NonAdditivePerChar { get; init; }

    /// <summary> Префикс обёрнутого запроса; при совпадении начала строки включается <see cref="NonAdditivePerChar"/>. </summary>
    public string WrappedPrefix { get; init; } = string.Empty;

    /// <summary> Сколько раз бросить исключение при первом обращении к <see cref="GetTokenCountAsync"/> после сброса. </summary>
    public int FailGetTokenCountTimes { get; set; }

    public int TokenizeCallCount { get; private set; }
    public int GetTokenCountCallCount { get; private set; }
    public List<string> GetTokenCountInputs { get; } = [];

    /// <summary> Возвращает длину строки с опциональными special и штрафом неаддитивности. </summary>
    public ValueTask<long> GetTokenCountAsync(
        string text,
        bool addSpecialTokens = true,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GetTokenCountCallCount++;
        GetTokenCountInputs.Add(text);

        if (FailGetTokenCountTimes > 0)
        {
            FailGetTokenCountTimes--;
            throw new InvalidOperationException("Имитация сбоя токенизатора.");
        }

        text ??= string.Empty;
        var count = text.Length;
        if (addSpecialTokens)
            count += SpecialTokenCount;

        if (NonAdditivePerChar > 0
            && WrappedPrefix.Length > 0
            && text.StartsWith(WrappedPrefix, StringComparison.Ordinal)
            && text.Length > WrappedPrefix.Length)
        {
            count += (text.Length - WrappedPrefix.Length) * NonAdditivePerChar;
        }

        return ValueTask.FromResult((long)count);
    }

    /// <summary> Делит текст на токены по одному символу с <c>piece</c> = символ. </summary>
    public ValueTask<LlamaModelToken[]> TokenizeAsync(
        string text,
        bool addSpecialTokens = true,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TokenizeCallCount++;
        text ??= string.Empty;
        var tokens = new LlamaModelToken[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            tokens[i] = new LlamaModelToken
            {
                Id = i,
                Piece = text[i].ToString()
            };
        }

        return ValueTask.FromResult(tokens);
    }

    /// <summary> Возвращает индексы символов как идентификаторы токенов. </summary>
    public ValueTask<int[]> GetTokenIdsAsync(
        string text,
        bool addSpecialTokens = true,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        text ??= string.Empty;
        return ValueTask.FromResult(Enumerable.Range(0, text.Length).ToArray());
    }
}
