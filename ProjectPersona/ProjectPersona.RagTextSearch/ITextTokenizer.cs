using ProjectPersona.RagTextSearch.Llama.Tokenizer;

namespace ProjectPersona.RagTextSearch;

/// <summary> Токенизатор текста. </summary>
public interface ITextTokenizer
{
    /// <summary> Получить количество токенов в тексте. </summary>
    /// <param name="text"> Текст. </param>
    /// <param name="addSpecialTokens"> Добавлять ли специальные токены. </param>
    /// <param name="cancellationToken"> Токен отмены. </param>
    /// <returns> Число токенов. </returns>
    ValueTask<long> GetTokenCountAsync(
        string text,
        bool addSpecialTokens = true,
        CancellationToken cancellationToken = default);

    /// <summary> Токенизировать текст. </summary>
    /// <param name="text"> Текст. </param>
    /// <param name="addSpecialTokens"> Добавлять ли специальные токены. </param>
    /// <param name="cancellationToken"> Токен отмены. </param>
    /// <returns> Массив токенов. </returns>
    ValueTask<LlamaModelToken[]> TokenizeAsync(
        string text,
        bool addSpecialTokens = true,
        CancellationToken cancellationToken = default);

    /// <summary> Получить только идентификаторы токенов, без декодирования piece. </summary>
    /// <param name="text"> Текст. </param>
    /// <param name="addSpecialTokens"> Добавлять ли специальные токены. </param>
    /// <param name="cancellationToken"> Токен отмены. </param>
    /// <returns> Идентификаторы токенов в порядке токенизации. </returns>
    ValueTask<int[]> GetTokenIdsAsync(
        string text,
        bool addSpecialTokens = true,
        CancellationToken cancellationToken = default);
}
