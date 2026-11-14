using ProjectPersona.Application.Pipes;
using ProjectPersona.Application.Services.RagSearchService.Pipes.Dto;
using ProjectPersona.RagTextSearch;

namespace ProjectPersona.Application.Services.RagSearchService.Pipes;

public class EmbeddingPipe :
    ChannelPipeBase<DeclarationBatch<EmbeddingWorkItem>, DeclarationBatch<VectorData>>
{
    /// <summary> Ёмкость входного канала в пачках рабочих item эмбедингов. </summary>
    private const int ChannelCapacity = 8;

    /// <summary> Эмбеддер текстов. </summary>
    private readonly ITextEmbedder _textEmbedder;

    /// <summary> Создать пайп эмбеддинга рабочих элементов. </summary>
    public EmbeddingPipe(ITextEmbedder textEmbedder)
        : base(ChannelCapacity)
    {
        _textEmbedder = textEmbedder
                        ?? throw new ArgumentNullException(nameof(textEmbedder));
    }

    /// <inheritdoc />
    protected override async ValueTask ProcessAsync(
        DeclarationBatch<EmbeddingWorkItem> item,
        Func<DeclarationBatch<VectorData>, ValueTask> emit,
        CancellationToken ct)
    {
        await emit(await Process(item, ct).ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <summary> Обработать пачку: эмбеддить токены и собрать векторы с готовыми метаданными. </summary>
    /// <param name="batch"> Пачка рабочих item пайпы. </param>
    /// <param name="ct"> Токен отмены. </param>
    /// <returns> Пачка векторов с той же границей объявлений. </returns>
    private async ValueTask<DeclarationBatch<VectorData>> Process(
        DeclarationBatch<EmbeddingWorkItem> batch, CancellationToken ct)
    {
        var items = batch.Items;
        var tokenIds = items.Select(static item => item.TokenIds).ToArray();
        var embeddings = await _textEmbedder.ProcessTokenizedTextsAsync(tokenIds, ct).ConfigureAwait(false);

        var vectors = new VectorData[items.Count];
        var index = 0;
        foreach (var item in items)
        {
            vectors[index] = new VectorData
            {
                Assembly = item.Assembly,
                TargetFramework = item.TargetFramework,
                Metadata = item.Metadata,
                Vector = embeddings[index]
            };
            index++;
        }

        return new DeclarationBatch<VectorData>(batch.DeclarationEnd, vectors);
    }
}
