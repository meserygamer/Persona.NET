using ProjectPersona.Application.Pipes;
using ProjectPersona.Application.Services.RagSearchService.Pipes.Dto;
using ProjectPersona.RagTextSearch;
using ProjectPersona.RagTextSearch.LanceDB;

namespace ProjectPersona.Application.Services.RagSearchService.Pipes;

public class InsertVectorDataInDbPipe : ChannelPipeBase<DeclarationBatch<VectorData>, int>
{
    /// <summary> Ёмкость входного канала в пачках векторов. </summary>
    private const int ChannelCapacity = 8;

    /// <summary> Векторное хранилище. </summary>
    private readonly IVectorStore _vectorStore;

    /// <summary> Создать пайп записи векторов в хранилище. </summary>
    public InsertVectorDataInDbPipe(IVectorStore vectorStore)
        : base(ChannelCapacity)
    {
        _vectorStore = vectorStore
                       ?? throw new ArgumentNullException(nameof(vectorStore));
    }

    /// <inheritdoc />
    /// <remarks> Отправляет дальше границу объявлений, которая уже записана в хранилище. </remarks>
    protected override async ValueTask ProcessAsync(
        DeclarationBatch<VectorData> item,
        Func<int, ValueTask> emit,
        CancellationToken ct)
    {
        if (item.Items.Count > 0)
            await _vectorStore.AddVectorsAsync(item.Items, ct).ConfigureAwait(false);

        await emit(item.DeclarationEnd).ConfigureAwait(false);
    }
}
