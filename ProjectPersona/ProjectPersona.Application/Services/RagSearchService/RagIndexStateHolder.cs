using Microsoft.Extensions.Logging;
using ProjectPersona.RagTextSearch;
using ProjectPersona.RagTextSearch.Manifest;

namespace ProjectPersona.Application.Services.RagSearchService;

/// <summary> Проверяет векторную базу и её манифест один раз при старте сервера и хранит результат на время жизни процесса. </summary>
/// <remarks>
/// Сервер запускается по готовой базе или без неё; изменение базы во время работы сервера — невалидное состояние,
/// которое не отслеживается. Поэтому поиск не обращается к манифесту на каждый запрос.
/// </remarks>
public sealed class RagIndexStateHolder
{
    /// <summary> Сообщение для агента, когда базы нет. </summary>
    private const string MissingMessage =
        "The RAG vector index does not exist. Run vectorization first with the CliTools command " +
        "`vectorize-type-source-index <IndexPath> <LanceDbPath> --llama-options … --vector-dimension …`, " +
        "then restart the server.";

    /// <summary> Хранилище векторов. </summary>
    private readonly IVectorStore _vectorStore;
    /// <summary> Описание текущего эмбеддера. </summary>
    private readonly IEmbeddingProfileProvider _profileProvider;
    /// <summary> Логгер результата проверки. </summary>
    private readonly ILogger<RagIndexStateHolder> _logger;
    /// <summary> Единственная проверка на процесс. </summary>
    private readonly Lazy<Task<RagIndexState>> _state;

    /// <summary> Создаёт holder состояния векторной базы. </summary>
    /// <param name="vectorStore"> Хранилище векторов. </param>
    /// <param name="profileProvider"> Описание текущего эмбеддера. </param>
    /// <param name="logger"> Логгер. </param>
    /// <exception cref="ArgumentNullException"> Любой из параметров равен <see langword="null"/>. </exception>
    public RagIndexStateHolder(
        IVectorStore vectorStore,
        IEmbeddingProfileProvider profileProvider,
        ILogger<RagIndexStateHolder> logger)
    {
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _profileProvider = profileProvider ?? throw new ArgumentNullException(nameof(profileProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Проверка загружает модель синхронно, поэтому уходит в пул потоков и не держит вызывающего.
        _state = new Lazy<Task<RagIndexState>>(() => Task.Run(CheckAsync));
    }

    /// <summary> Возвращает состояние базы, дожидаясь завершения проверки. </summary>
    /// <param name="ct"> Токен отмены ожидания; саму проверку не отменяет. </param>
    /// <returns> Состояние базы. </returns>
    public Task<RagIndexState> GetAsync(CancellationToken ct = default) =>
        _state.Value.WaitAsync(ct);

    /// <summary> Проверить состояние индекса RAG. </summary>
    /// <returns> Состояние индекса RAG. </returns>
    private async Task<RagIndexState> CheckAsync()
    {
        try
        {
            var state = await CheckCoreAsync().ConfigureAwait(false);
            LogState(state);
            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG vector index check failed");
            return InvalidState($"The RAG vector index could not be checked: {ex.GetBaseException().Message}");
        }
    }

    /// <summary> Проверить состояние индекса RAG. </summary>
    /// <returns> Состояние индекса RAG. </returns>
    private async Task<RagIndexState> CheckCoreAsync()
    {
        if (!await _vectorStore.StoreExistsAsync().ConfigureAwait(false))
            return new RagIndexState(RagIndexStatus.Missing, MissingMessage, []);

        var manifest = await _vectorStore.ReadManifestAsync().ConfigureAwait(false);
        if (manifest is null)
        {
            return InvalidState(
                "The RAG vector index has no manifest: full vectorization was interrupted or not finished. " +
                "Run `vectorize-type-source-index` again, then restart the server.");
        }

        var compatibility = ManifestCompatibility.Check(manifest, VectorizationPipeline.Version, _profileProvider.GetProfile());
        if (!compatibility.IsCompatible)
        {
            return InvalidState(
                "The RAG vector index is incompatible with the server's embedding model or pipeline (" +
                string.Join("; ", compatibility.Errors) + "). " +
                "Rebuild it with `vectorize-type-source-index` or start the server with the model the index was built with.");
        }

        // Мягкие расхождения на поиск не влияют, поэтому остаются в логе.
        foreach (var warning in compatibility.Warnings)
            _logger.LogWarning("RAG vector index settings differ from the server's, search is unaffected: {Difference}", warning);

        if (manifest.State == VectorStoreState.Updating)
        {
            var warning =
                "The RAG vector index is partially updated: results for these assemblies may be missing or incomplete: " +
                string.Join(", ", manifest.PendingAssemblies) + ". Re-run `revectorize-assemblies` for them.";
            return new RagIndexState(RagIndexStatus.Updating, null, [warning]);
        }

        return new RagIndexState(RagIndexStatus.Ready, null, []);
    }

    /// <summary> Произвести логгирование состояния. </summary>
    /// <param name="state"> Состояние. </param>
    private void LogState(RagIndexState state)
    {
        switch (state.Status)
        {
            case RagIndexStatus.Missing:
                _logger.LogWarning("RAG vector index does not exist, ragSearch is unavailable");
                break;
            case RagIndexStatus.Invalid:
                _logger.LogError("RAG vector index is invalid, ragSearch is unavailable: {Error}", state.Error);
                break;
            case RagIndexStatus.Updating:
                _logger.LogWarning("RAG vector index is not fully updated: {Warnings}", string.Join(" ", state.Warnings));
                break;
            default:
                _logger.LogInformation("RAG vector index is ready");
                break;
        }
    }

    /// <summary> Получить инвалидное состояние индекса RAG. </summary>
    /// <param name="error"> Ошибка. </param>
    /// <returns> Инвалидное состояние индекса RAG. </returns>
    private static RagIndexState InvalidState(string error) => new(RagIndexStatus.Invalid, error, []);
}
