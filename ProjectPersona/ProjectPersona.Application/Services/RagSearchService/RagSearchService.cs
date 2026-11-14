using System.Runtime.Serialization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProjectPersona.Application.Services.RagSearchService.Responses;
using ProjectPersona.RagTextSearch;
using ProjectPersona.RagTextSearch.Llama;
using ProjectPersona.RagTextSearch.Utils;

namespace ProjectPersona.Application.Services.RagSearchService;

/// <summary> Векторизация объявлений type-source-index и RAG-поиск для MCP tool. </summary>
public class RagSearchService
{
    /// <summary> Лимит совпадений по умолчанию для MCP. </summary>
    public const int DefaultLimit = 10;

    /// <summary> Эмбеддер текста. </summary>
    private readonly ITextEmbedder _textEmbedder;
    /// <summary> Токенизатор модели эмбеддинга. </summary>
    private readonly ITextTokenizer _textTokenizer;
    /// <summary> Настройки Llama (шаблон запроса и лимит батча). </summary>
    private readonly LlamaSettings _llamaSettings;
    /// <summary> Сессия модели: фактический <c>n_ctx</c> для лимита поискового промпта. </summary>
    private readonly LlamaModelSessionHolder _sessionHolder;
    /// <summary> Хранилище векторов. </summary>
    private readonly IVectorStore _vectorStore;
    /// <summary> Состояние базы, проверенное при старте. </summary>
    private readonly RagIndexStateHolder _ragIndexStateHolder;
    /// <summary> Логгер ошибок поиска. </summary>
    private readonly ILogger<RagSearchService> _logger;
    
    /// <summary> Создаёт сервис RAG-поиска. </summary>
    /// <param name="textEmbedder"> Эмбеддер текста. </param>
    /// <param name="textTokenizer"> Токенизатор для проверки длины поискового промпта. </param>
    /// <param name="llamaSettings"> Настройки шаблона запроса. </param>
    /// <param name="sessionHolder"> Сессия модели с фактическим <c>n_ctx</c>. </param>
    /// <param name="vectorStore"> Хранилище векторов. </param>
    /// <param name="ragIndexStateHolder"> Состояние базы, проверенное при старте. </param>
    /// <param name="logger"> Логгер ошибок поиска. </param>
    /// <exception cref="ArgumentNullException"> Любой из параметров равен <see langword="null"/>. </exception>
    public RagSearchService(
        ITextEmbedder textEmbedder,
        ITextTokenizer textTokenizer,
        LlamaSettings llamaSettings,
        LlamaModelSessionHolder sessionHolder,
        IVectorStore vectorStore,
        RagIndexStateHolder ragIndexStateHolder,
        ILogger<RagSearchService> logger)
    {
        _textEmbedder = textEmbedder 
            ?? throw new ArgumentNullException(nameof(textEmbedder));
        _textTokenizer = textTokenizer
            ?? throw new ArgumentNullException(nameof(textTokenizer));
        _llamaSettings = llamaSettings
            ?? throw new ArgumentNullException(nameof(llamaSettings));
        _sessionHolder = sessionHolder
            ?? throw new ArgumentNullException(nameof(sessionHolder));
        _vectorStore = vectorStore 
            ?? throw new ArgumentNullException(nameof(vectorStore));
        _ragIndexStateHolder = ragIndexStateHolder
                               ?? throw new ArgumentNullException(nameof(ragIndexStateHolder));
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Выполняет RAG-поиск для MCP tool: проверяет состояние индекса на момент старта, валидирует параметры,
    /// возвращает ответ с ошибкой или совпадениями и предупреждениями.
    /// </summary>
    /// <param name="query">Текст поискового запроса.</param>
    /// <param name="limit">Максимум ближайших фрагментов при <c>&gt; 0</c>.</param>
    /// <param name="includeFragmentSource">Включать текст фрагмента в совпадения.</param>
    /// <param name="cancellationToken">Токен отмены поиска.</param>
    /// <returns>Успешный ответ с совпадениями или сообщение об ошибке для агента.</returns>
    public async ValueTask<RagSearchResponse> SearchForToolAsync(
        string query,
        int limit = DefaultLimit,
        bool includeFragmentSource = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogWarning(
                "ragSearch failed: empty query. Operation=ragSearch, Limit={Limit}, IncludeFragmentSource={IncludeFragmentSource}",
                limit, includeFragmentSource);
            return RagSearchResponse.Fail(
                "The query parameter is mandatory: specify a non-empty search text.");
        }

        if (limit < 1)
        {
            _logger.LogWarning(
                "ragSearch failed: invalid limit. Operation=ragSearch, Limit={Limit}",
                limit);
            return RagSearchResponse.Fail(
                "The limit must be greater than or equal to 1.");
        }

        try
        {
            var indexState = await _ragIndexStateHolder.GetAsync(cancellationToken).ConfigureAwait(false);
            if (!indexState.IsSearchable)
            {
                _logger.LogWarning(
                    "ragSearch failed: vector index is {Status}. Operation=ragSearch, Limit={Limit}",
                    indexState.Status, limit);
                return RagSearchResponse.Fail(indexState.Error ?? $"The RAG vector index is {indexState.Status}.");
            }

            var metadata = await SearchAsync(query, limit, includeFragmentSource, cancellationToken)
                .ConfigureAwait(false);
            var matches = ProjectMatches(metadata, includeFragmentSource);
            return RagSearchResponse.Ok(matches, indexState.Warnings);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "ragSearch cancelled. Operation=ragSearch, Limit={Limit}, IncludeFragmentSource={IncludeFragmentSource}",
                limit, includeFragmentSource);
            return RagSearchResponse.Fail(
                "The request was cancelled or timed out. Retry the operation.");
        }
        catch (ArgumentException ex) when (ex.Message.Contains("batch limit", StringComparison.OrdinalIgnoreCase)
                                            || ex.Message.Contains("Shorten the query", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                ex,
                "ragSearch failed: query too long. Operation=ragSearch, Limit={Limit}",
                limit);
            return RagSearchResponse.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "ragSearch failed. Operation=ragSearch, Limit={Limit}, IncludeFragmentSource={IncludeFragmentSource}",
                limit, includeFragmentSource);
            return RagSearchResponse.Fail(FormatSearchError(ex));
        }
    }

    /// <summary> Выполняет семантический поиск по векторному индексу. </summary>
    /// <param name="searchQuery"> Текст поискового запроса. </param>
    /// <param name="limit"> Лимит результатов; не меньше 1. </param>
    /// <param name="needFragmentSource"> Необходимость возврата исходников фрагмента в метаданных. </param>
    /// <param name="ct"> Токен отмены. </param>
    /// <returns> Метаданные ближайших векторов. </returns>
    /// <exception cref="ArgumentException"> Некорректный <paramref name="limit"/> или слишком длинный запрос в токенах. </exception>
    public async ValueTask<TypeSourceVectorMetadata[]> SearchAsync(string searchQuery, int limit = 10,
        bool needFragmentSource = false, CancellationToken ct = default)
    {
        if (limit < 1)
            throw new ArgumentException("Limit must be greater than or equal to 1.", nameof(limit));
        
        if (string.IsNullOrEmpty(searchQuery))
            return [];

        await ValidateSearchQueryTokenLengthAsync(searchQuery, ct).ConfigureAwait(false);

        var embedding = await _textEmbedder.ProcessSearchQueriesAsync([searchQuery], ct).ConfigureAwait(false);
        if (embedding.Length != 1)
            throw new Exception("An unknown error occurred while creating an embedding!");

        var vectorDatas = await _vectorStore.SearchNearestVectorsAsync(embedding[0], limit, ct)
            .ConfigureAwait(false);

        return vectorDatas
            .Select(vectorData => 
                (needFragmentSource
                    ? JsonSerializer.Deserialize<TypeSourceVectorMetadataWithChunk>(vectorData.Metadata) as TypeSourceVectorMetadata
                    : JsonSerializer.Deserialize<TypeSourceVectorMetadata>(vectorData.Metadata)) 
                ?? throw new SerializationException("Could not deserialize the embedding metadata string!"))
            .ToArray();
    }

    /// <summary>
    /// Проверяет, что промпт поискового запроса после шаблона не превышает фактический <c>n_ctx</c> контекста.
    /// </summary>
    /// <param name="searchQuery">Сырой текст запроса.</param>
    /// <param name="cancellationToken">Токен отмены обращения к токенизатору.</param>
    /// <exception cref="ArgumentException">Некорректный шаблон или превышение лимита токенов.</exception>
    private async ValueTask ValidateSearchQueryTokenLengthAsync(
        string searchQuery,
        CancellationToken cancellationToken)
    {
        if (StringUtils.GetFormatArgumentsCount(_llamaSettings.SearchQueryTemplate) != 1)
        {
            throw new ArgumentException(
                $"Шаблон {nameof(LlamaSettings.SearchQueryTemplate)} должен содержать ровно один аргумент формата.");
        }

        var prompt = _llamaSettings.FormatSearchQuery(searchQuery);
        var tokenCount = await _textTokenizer
            .GetTokenCountAsync(prompt, addSpecialTokens: true, cancellationToken)
            .ConfigureAwait(false);
        var contextLimit = _sessionHolder.GetContextTokenLimit(cancellationToken);

        if (tokenCount > contextLimit)
        {
            throw new ArgumentException(
                $"Search query exceeds the context limit: {tokenCount} tokens, maximum is {contextLimit}. Shorten the query.");
        }
    }

    /// <summary> Проецирует метаданные векторов в DTO ответа MCP. </summary>
    /// <param name="metadata">Метаданные из LanceDB.</param>
    /// <param name="includeFragmentSource">Включать поле <see cref="RagSearchMatch.Source"/>.</param>
    /// <returns>Список совпадений в порядке близости.</returns>
    private static List<RagSearchMatch> ProjectMatches(
        IReadOnlyList<TypeSourceVectorMetadata> metadata,
        bool includeFragmentSource)
    {
        var matches = new List<RagSearchMatch>(metadata.Count);

        foreach (var item in metadata)
        {
            var level = string.IsNullOrEmpty(item.Name) ? "Type" : "Member";
            var chunkIndex = 0;
            string? source = null;

            if (item is TypeSourceVectorMetadataWithChunk withChunk)
            {
                chunkIndex = withChunk.ChunkIndex;
                if (includeFragmentSource)
                    source = withChunk.Source;
            }

            matches.Add(new RagSearchMatch(
                item.AssemblyName,
                item.TargetFramework,
                item.Type,
                level,
                item.Name,
                chunkIndex,
                source));
        }

        return matches;
    }

    /// <summary> Формирует текст ошибки RAG-поиска для агента. </summary>
    /// <param name="exception">Исключение при эмбеддинге, LanceDB, индексе или векторизации.</param>
    /// <returns>Сообщение на английском для поля <see cref="RagSearchResponse.Error"/>.</returns>
    private static string FormatSearchError(Exception exception)
    {
        var message = exception is AggregateException aggregate
            ? aggregate.GetBaseException().Message
            : exception.Message;

        if (exception is InvalidOperationException &&
            (message.Contains("--generate-type-source-index", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("type source index", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("typeSourceIndex", StringComparison.OrdinalIgnoreCase)))
        {
            return $"Failed to load the source index: {message}";
        }

        if (exception is InvalidOperationException &&
            (message.Contains("llama_decode", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("эмбеддинг", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("embedding", StringComparison.OrdinalIgnoreCase)))
        {
            return $"RAG embedding failed: {message}";
        }

        if (message.Contains("LanceDB", StringComparison.OrdinalIgnoreCase))
        {
            return $"RAG vector store error: {message}";
        }

        return $"RAG search error: {message}";
    }
}
