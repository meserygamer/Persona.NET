using System.Text.Json;
using ProjectPersona.Application.Pipes;
using ProjectPersona.Application.Services.RagSearchService.Pipes.Dto;
using ProjectPersona.SourceIndex;
using ProjectPersona.RagTextSearch;
using ProjectPersona.RagTextSearch.Llama;
using ProjectPersona.SourceIndex.Models;

namespace ProjectPersona.Application.Services.RagSearchService.Pipes;

public class SplitDeclarationToEmbedPipe :
    ChannelPipeBase<IReadOnlyCollection<TypeSourceDeclaration>, DeclarationBatch<EmbeddingWorkItem>>
{
    /// <summary> Ёмкость входного канала в пачках деклараций. </summary>
    private const int ChannelCapacity = 8;
    /// <summary> Максимум потоков, которые участвуют в разбиении деклараций на чанки. </summary>
    private const int MaxDegreeOfParallelism = 4;
    /// <summary> Максимум элементов в одной пачке, которая уходит в этап эмбеддинга. </summary>
    private const int MaxItemsPerBatch = 2048;

    /// <summary> Настройки Llama. </summary>
    private readonly LlamaSettings _llamaSettings;
    /// <summary> Разделитель текста. </summary>
    private readonly ITextSplitter _textSplitter;
    /// <summary> Токенизатор модели эмбеддинга. </summary>
    private readonly ITextTokenizer _textTokenizer;
    /// <summary> Число объявлений, уже переданных дальше. Меняется только в фоновом цикле пайпа. </summary>
    private int _declarationEnd;

    /// <summary> Создать пайп подготовки деклараций к этапу чанкинга. </summary>
    public SplitDeclarationToEmbedPipe(
        LlamaSettings llamaSettings,
        ITextSplitter textSplitter,
        ITextTokenizer textTokenizer)
        : base(ChannelCapacity)
    {
        _llamaSettings = llamaSettings
                         ?? throw new ArgumentNullException(nameof(llamaSettings));
        _textSplitter = textSplitter
                        ?? throw new ArgumentNullException(nameof(textSplitter));
        _textTokenizer = textTokenizer
                         ?? throw new ArgumentNullException(nameof(textTokenizer));
    }

    /// <inheritdoc />
    protected override async ValueTask ProcessAsync(
        IReadOnlyCollection<TypeSourceDeclaration> item,
        Func<DeclarationBatch<EmbeddingWorkItem>, ValueTask> emit,
        CancellationToken ct)
    {
        var start = _declarationEnd;
        _declarationEnd += item.Count;
        var items = await PrepareEmbeddingWorkBatchAsync(item, ct).ConfigureAwait(false);

        if (items.Count == 0)
        {
            await emit(new DeclarationBatch<EmbeddingWorkItem>(_declarationEnd, [])).ConfigureAwait(false);
            return;
        }

        // Режем пачку на куски, чтобы очередь ограничивалась числом элементов, а не числом деклараций.
        // Граница объявлений растёт только на последнем куске, иначе прогресс опередит записанные векторы.
        for (var offset = 0; offset < items.Count; offset += MaxItemsPerBatch)
        {
            var slice = items.GetRange(offset, Math.Min(MaxItemsPerBatch, items.Count - offset));
            var declarationEnd = offset + slice.Count == items.Count ? _declarationEnd : start;

            await emit(new DeclarationBatch<EmbeddingWorkItem>(declarationEnd, slice)).ConfigureAwait(false);
        }
    }

    /// <summary> Подготовить пачку деклараций к этапу чанкинга. </summary>
    /// <param name="declarationsBatch"> Пачка деклараций. </param>
    /// <param name="ct"> Токен отмены. </param>
    /// <returns> Части деклараций на чанкинг. </returns>
    private async Task<List<EmbeddingWorkItem>> PrepareEmbeddingWorkBatchAsync(
        IReadOnlyCollection<TypeSourceDeclaration> declarationsBatch, CancellationToken ct)
    {
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = ct,
            MaxDegreeOfParallelism = MaxDegreeOfParallelism
        };

        // Каждый поток пишет только в свой индекс, поэтому гонки нет.
        var workByDeclaration = new IReadOnlyCollection<EmbeddingWorkItem>[declarationsBatch.Count];
        await Parallel.ForEachAsync(
            declarationsBatch.Select((declaration, index) => (declaration, index)),
            parallelOptions,
            async (item, innerCt) =>
            {
                workByDeclaration[item.index] = await PrepareEmbeddingWorkAsync(item.declaration, innerCt);
            });

        var declarationsToChunking = new List<EmbeddingWorkItem>(workByDeclaration.Sum(work => work.Count));
        foreach (var work in workByDeclaration)
            declarationsToChunking.AddRange(work);

        return declarationsToChunking;
    }

    /// <summary> Подготовить декларации. </summary>
    /// <param name="declaration"> Декларация. </param>
    /// <returns> Части декларации на чанкинг. </returns>
    private async Task<IReadOnlyCollection<EmbeddingWorkItem>> PrepareEmbeddingWorkAsync(
        TypeSourceDeclaration declaration, CancellationToken ct)
    {
        if (declaration.Members.Count == 0)
        {
            return [
                ..await EnqueueChunksAsync(
                    declaration.AssemblyName,
                    declaration.TargetFramework,
                    declaration.Type,
                    null,
                    declaration.Type,
                    declaration.SourceCode,
                    ct)
            ];
        }

        var minMemberStart = int.MaxValue;
        var members = declaration.Members
            .Where(member => member.Length > 0)
            .ToArray();

        foreach (var member in members)
        {
            if (member.Start < minMemberStart)
                minMemberStart = member.Start;
        }

        var addHeader = minMemberStart > 0 && minMemberStart != int.MaxValue;
        var parts = new List<EmbeddingWorkItem>(addHeader
            ? declaration.Members.Count + 1
            : declaration.Members.Count);

        if (addHeader)
        {
            var header = declaration.SourceCode[..minMemberStart];
            if (!string.IsNullOrWhiteSpace(header))
            {
                parts.AddRange(
                    await EnqueueChunksAsync(
                        declaration.AssemblyName,
                        declaration.TargetFramework,
                        declaration.Type,
                        null,
                        declaration.Type,
                        header,
                        ct));
            }
        }

        foreach (var member in members)
        {
            var memberText = declaration.SourceCode.Substring(member.Start, member.Length);
            var title = declaration.Type + "." + member.Name;

            parts.AddRange(
                await EnqueueChunksAsync(
                    declaration.AssemblyName,
                    declaration.TargetFramework,
                    declaration.Type,
                    member.Name,
                    title,
                    memberText,
                    ct));
        }

        return parts;
    }

    /// <summary> Нарезает текст, формирует промпты и отдаёт их в проход векторизации. </summary>
    private async ValueTask<IReadOnlyCollection<EmbeddingWorkItem>> EnqueueChunksAsync(
        string assemblyName,
        string targetFramework,
        string type,
        string? memberName,
        string title,
        string text,
        CancellationToken ct)
    {
        var chunks = await _textSplitter
            .SplitTextAsync(text, title, ct)
            .ConfigureAwait(false);

        var embeddingWorkItems = new EmbeddingWorkItem[chunks.Length];
        for (var chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
        {
            var chunk = chunks[chunkIndex];
            var metadata = new TypeSourceVectorMetadataWithChunk
            {
                AssemblyName = assemblyName,
                TargetFramework = targetFramework,
                Type = type,
                Name = memberName,
                ChunkIndex = chunkIndex,
                Source = chunk
            };

            var prompt = _llamaSettings.FormatDocument(title, chunk);
            var tokenIds = await _textTokenizer
                .GetTokenIdsAsync(prompt, addSpecialTokens: true, ct)
                .ConfigureAwait(false);
            embeddingWorkItems[chunkIndex] = new EmbeddingWorkItem(
                assemblyName,
                targetFramework,
                JsonSerializer.Serialize(metadata),
                tokenIds);
        }

        return embeddingWorkItems;
    }
}
