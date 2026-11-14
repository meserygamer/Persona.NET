namespace ProjectPersona.RagTextSearch;

/// <summary> Получение векторных представлений для набора текстовых запросов. </summary>
public interface ITextEmbedder
{
    /// <summary> Возвращает векторное представление для каждого запроса. </summary>
    /// <param name="queries"> Тексты промптов. </param>
    /// <param name="cancellationToken"> Токен отмены. </param>
    /// <returns> Массив эмбеддингов в порядке <paramref name="queries"/>. </returns>
    ValueTask<float[][]> ProcessTextsAsync(
        IEnumerable<string> queries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает эмбеддинги для сырых текстов поисковых запросов после подстановки в <see cref="Llama.LlamaSettings.SearchQueryTemplate"/>.
    /// </summary>
    /// <param name="queries">Тексты запросов без обёртки шаблона.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Массив эмбеддингов в порядке <paramref name="queries"/> после материализации.</returns>
    ValueTask<float[][]> ProcessSearchQueriesAsync(
        IEnumerable<string> queries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает эмбеддинги для уже токенизированных промптов. Токены должны быть получены с
    /// <c>addSpecial: true</c> и <c>parseSpecial: true</c>, как в <see cref="ProcessTextsAsync"/>.
    /// </summary>
    /// <param name="tokenIds"> Идентификаторы токенов каждого промпта. </param>
    /// <param name="cancellationToken"> Токен отмены. </param>
    /// <returns> Массив эмбеддингов в порядке <paramref name="tokenIds"/>. </returns>
    ValueTask<float[][]> ProcessTokenizedTextsAsync(
        IReadOnlyList<int[]> tokenIds,
        CancellationToken cancellationToken = default);
}
