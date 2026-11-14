using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using ProjectPersona.Application.Pipes;
using ProjectPersona.Application.Services.RagSearchService.Pipes;
using ProjectPersona.Application.Services.TypeSourceIndexService;
using ProjectPersona.RagTextSearch;
using ProjectPersona.RagTextSearch.Manifest;
using ProjectPersona.SourceIndex.Find;
using ProjectPersona.SourceIndex.Models;

namespace ProjectPersona.Application.Services.RagSearchService;

public class TypeSourceIndexVectorizerV2
{
    private readonly IVectorStore _vectorStore;
    private readonly TypeSourceIndexHolder _indexHolder;
    private readonly IEmbeddingProfileProvider _profileProvider;
    private readonly ILogger<TypeSourceIndexVectorizerV2> _logger;
    
    private readonly Func<TypeSourceProducerPipe> _typeSourceProducerPipeFactory;
    private readonly Func<IEnumerable<(string Assembly, string? TargetFramework)>, AssemblyTypeSourceProducerPipe> _assemblyTypeSourceProducerPipeFactory;
    private readonly Func<SplitDeclarationToEmbedPipe> _splitDeclarationToEmbedPipeFactory;
    private readonly Func<EmbeddingPipe> _embeddingPipeFactory;
    private readonly Func<InsertVectorDataInDbPipe> _insertVectorDataInDbPipeFactory;
    
    /// <summary> Создать векторизатор индекса исходников. </summary>
    public TypeSourceIndexVectorizerV2(
        IVectorStore vectorStore,
        TypeSourceIndexHolder indexHolder,
        IEmbeddingProfileProvider profileProvider,
        ILogger<TypeSourceIndexVectorizerV2> logger,
        Func<TypeSourceProducerPipe> typeSourceProducerPipeFactory, 
        Func<IEnumerable<(string Assembly, string? TargetFramework)>, AssemblyTypeSourceProducerPipe> assemblyTypeSourceProducerPipeFactory,
        Func<SplitDeclarationToEmbedPipe> splitDeclarationToEmbedPipeFactory,
        Func<EmbeddingPipe> embeddingPipeFactory,
        Func<InsertVectorDataInDbPipe> insertVectorDataInDbPipeFactory)
    {
        _vectorStore = vectorStore 
                       ?? throw new ArgumentNullException(nameof(vectorStore));
        _indexHolder = indexHolder 
                       ?? throw new ArgumentNullException(nameof(indexHolder));
        _profileProvider = profileProvider
                           ?? throw new ArgumentNullException(nameof(profileProvider));
        _logger = logger
                  ?? throw new ArgumentNullException(nameof(logger));
        
        _typeSourceProducerPipeFactory = typeSourceProducerPipeFactory 
                                         ?? throw new ArgumentNullException(nameof(typeSourceProducerPipeFactory));
        _assemblyTypeSourceProducerPipeFactory = assemblyTypeSourceProducerPipeFactory
                                                 ?? throw new ArgumentNullException(nameof(assemblyTypeSourceProducerPipeFactory));
        _splitDeclarationToEmbedPipeFactory = splitDeclarationToEmbedPipeFactory 
                                              ?? throw new ArgumentNullException(nameof(splitDeclarationToEmbedPipeFactory));
        _embeddingPipeFactory = embeddingPipeFactory 
                                ?? throw new ArgumentNullException(nameof(embeddingPipeFactory));
        _insertVectorDataInDbPipeFactory = insertVectorDataInDbPipeFactory 
                                           ?? throw new ArgumentNullException(nameof(insertVectorDataInDbPipeFactory));
    }
    
    /// <summary> Полностью пересобирает векторный индекс по-текущему type-source-index. </summary>
    /// <param name="ct">Токен отмены.</param>
    /// <param name="progress">
    /// Необязательный отчёт о доле объявлений, уже записанных в хранилище.
    /// 100% — после последней записи и оптимизации.
    /// </param>
    /// <remarks>
    /// Манифест удаляется до очистки и записывается только после записи всех векторов:
    /// прерванная векторизация оставляет хранилище без манифеста, то есть невалидным.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Число векторов от эмбеддера не совпало с числом промптов.</exception>
    /// <exception cref="OperationCanceledException">Операция отменена.</exception>
    public async ValueTask VectorizeTypeSourceIndexAsync(
        CancellationToken ct = default,
        IProgress<VectorizationProgress>? progress = null)
    {
        // Модель загружается до очистки: если она не загрузится, хранилище останется нетронутым.
        var profile = _profileProvider.GetProfile(ct);
        var index = await _indexHolder.GetAsync(ct).ConfigureAwait(false);
        var total = index.GetDeclarationCount(null);

        await _vectorStore.DeleteManifestAsync(ct).ConfigureAwait(false);
        await _vectorStore.ClearStoreAsync(ct).ConfigureAwait(false);
        await WriteProducedDeclarationsAsync(_typeSourceProducerPipeFactory.Invoke(), total, ct, progress).ConfigureAwait(false);

        await _vectorStore.WriteManifestAsync(CreateReadyManifest(profile), ct).ConfigureAwait(false);
        await OptimizeAndCompleteAsync(total, ct, progress).ConfigureAwait(false);
    }

    /// <summary>
    /// Перевекторизует только указанные сборки: удаляет их векторы из хранилища и заново записывает
    /// по-текущему type-source-index. Остальные сборки не затрагиваются.
    /// </summary>
    /// <param name="assemblies">
    /// Пары «код сборки – короткий TFM». Если TFM равен <see langword="null"/> или пустой, обрабатывается сборка для всех TFM.
    /// Пустая коллекция ничего не делает.
    /// </param>
    /// <param name="ct">Токен отмены.</param>
    /// <param name="progress">
    /// Необязательный отчёт о доле объявлений указанных сборок, уже записанных в хранилище.
    /// 100% — после последней записи и оптимизации.
    /// </param>
    /// <remarks>
    /// Старые векторы удаляются даже если сборки в индексе больше нет или в ней нет деклараций:
    /// так из хранилища уходят устаревшие данные. Удаление и запись не атомарны: до удаления сборки
    /// отмечаются в манифесте как незавершённые, при сбое отметка остаётся, повторный вызов приведёт состояние в порядок.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Коллекция сборок равна null.</exception>
    /// <exception cref="ArgumentException">Код сборки пустой либо состоит из пробелов.</exception>
    /// <exception cref="InvalidOperationException">
    /// Манифеста нет или он несовместим с текущим эмбеддером; число векторов от эмбеддера не совпало с числом промптов.
    /// </exception>
    /// <exception cref="OperationCanceledException">Операция отменена.</exception>
    public async ValueTask VectorizeAssembliesAsync(
        IEnumerable<(string Assembly, string? TargetFramework)> assemblies,
        CancellationToken ct = default,
        IProgress<VectorizationProgress>? progress = null)
    {
        // Нормализация одна на все шаги: индекс, поставщик и хранилище получают одинаковые ключи.
        var keys = TypeSourceAssemblyFilter.Normalize(assemblies);

        if (keys.Count == 0)
        {
            progress?.Report(new VectorizationProgress(0, 0, IsComplete: true));
            return;
        }

        await EnsureCompatibleManifestAsync(ct).ConfigureAwait(false);

        var index = await _indexHolder.GetAsync(ct).ConfigureAwait(false);
        var total = index.GetDeclarationCount(keys);

        await _vectorStore.MarkUpdatingAsync(keys, ct).ConfigureAwait(false);
        await _vectorStore.ClearAssembliesAsync(keys, ct).ConfigureAwait(false);
        await WriteProducedDeclarationsAsync(_assemblyTypeSourceProducerPipeFactory.Invoke(keys), total, ct, progress)
            .ConfigureAwait(false);

        await _vectorStore.MarkReadyAsync(keys, ct).ConfigureAwait(false);
        await OptimizeAndCompleteAsync(total, ct, progress).ConfigureAwait(false);
    }

    /// <summary> Проверяет, что манифест есть и совместим с текущим кодом и эмбеддером; мягкие расхождения пишет в лог. </summary>
    /// <param name="ct"> Токен отмены. </param>
    /// <exception cref="InvalidOperationException"> Манифеста нет или он несовместим. </exception>
    private async ValueTask EnsureCompatibleManifestAsync(CancellationToken ct)
    {
        var manifest = await _vectorStore.ReadRequiredManifestAsync(
                "The vector store has no manifest: it was never fully vectorized or the last full " +
                "vectorization was interrupted. Run `vectorize-type-source-index` first.",
                ct).ConfigureAwait(false);

        var compatibility = ManifestCompatibility.Check(manifest, VectorizationPipeline.Version, _profileProvider.GetProfile(ct));
        if (!compatibility.IsCompatible)
        {
            throw new InvalidOperationException(
                "The vector store was built with an incompatible embedder or pipeline (" +
                string.Join("; ", compatibility.Errors) + "). Run `vectorize-type-source-index` to rebuild it.");
        }

        foreach (var warning in compatibility.Warnings)
            _logger.LogWarning("Vector store build parameters differ, vectors stay comparable: {Difference}", warning);

        if (manifest.State == VectorStoreState.Updating)
        {
            _logger.LogWarning(
                "Vector store has unfinished assemblies from a previous run: {Assemblies}",
                string.Join(", ", manifest.PendingAssemblies));
        }
    }

    /// <summary> Собирает манифест полностью построенного хранилища. </summary>
    /// <param name="profile">Описание эмбеддера, которым построено хранилище.</param>
    private static VectorStoreManifest CreateReadyManifest(EmbeddingProfile profile) => new()
    {
        FormatVersion = VectorStoreManifest.CurrentFormatVersion,
        PipelineVersion = VectorizationPipeline.Version,
        VectorDimension = profile.Fingerprint.EmbeddingDimension,
        EmbeddingFingerprint = profile.Fingerprint,
        BuildParameters = profile.BuildParameters,
        DocumentQueryTemplate = profile.DocumentQueryTemplate,
        SearchQueryTemplate = profile.SearchQueryTemplate,
        State = VectorStoreState.Ready
    };

    /// <summary> Оптимизирует хранилище и сообщает о завершении. </summary>
    /// <param name="total">Число деклараций, которые должны быть записаны.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <param name="progress">Необязательный отчёт о прогрессе.</param>
    private async ValueTask OptimizeAndCompleteAsync(long total, CancellationToken ct, IProgress<VectorizationProgress>? progress)
    {
        await _vectorStore.OptimizeAsync(ct).ConfigureAwait(false);
        progress?.Report(new VectorizationProgress(total, total, IsComplete: true));
    }

    /// <summary> Прогоняет декларации от поставщика через разбиение, эмбеддинг и запись. </summary>
    /// <param name="producerPipe">Поставщик деклараций.</param>
    /// <param name="total">Число деклараций, которые поставит <paramref name="producerPipe"/>.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <param name="progress">Необязательный отчёт о прогрессе.</param>
    private async ValueTask WriteProducedDeclarationsAsync(
        IChannelPipe<Signal, IReadOnlyCollection<TypeSourceDeclaration>> producerPipe,
        long total,
        CancellationToken ct,
        IProgress<VectorizationProgress>? progress)
    {
        progress?.Report(new VectorizationProgress(0, total));

        if (total == 0)
            return;

        var splitDeclarationToEmbedPipe = _splitDeclarationToEmbedPipeFactory.Invoke();
        var embeddingPipe = _embeddingPipeFactory.Invoke();
        var insertVectorDataInDbPipe = _insertVectorDataInDbPipeFactory.Invoke();

        await using var pipeline = await producerPipe
                .Join(splitDeclarationToEmbedPipe)
                .Join(embeddingPipe)
                .Join(insertVectorDataInDbPipe)
            .Finalize(declarationEnd =>
            {
                progress?.Report(new VectorizationProgress(declarationEnd, total));
                return ValueTask.CompletedTask;
            })
            .RunOnceAsync(new Signal());
        using var cancellationRegistration = ct.Register(() => pipeline.Cancel());

        try
        {
            await pipeline.CompleteTask.ConfigureAwait(false);
        }
        catch (Exception) when (pipeline.CompleteTask.Exception is { } aggregate)
        {
            throw FindRootCause(aggregate);
        }
    }

    /// <summary>
    /// Выбирает первопричину сбоя. <see cref="ChannelClosedException"/> — лишь след закрытия канала соседней стадией.
    /// </summary>
    /// <param name="aggregate">Все исключения конвейера.</param>
    /// <returns>Первое исключение, не являющееся следом закрытия канала.</returns>
    private static Exception FindRootCause(AggregateException aggregate)
    {
        var causes = aggregate.Flatten().InnerExceptions;
        return causes.FirstOrDefault(cause => cause is not ChannelClosedException) ?? causes[0];
    }
}