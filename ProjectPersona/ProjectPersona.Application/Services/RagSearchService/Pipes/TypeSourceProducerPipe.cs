using ProjectPersona.Application.Pipes;
using ProjectPersona.Application.Services.TypeSourceIndexService;
using ProjectPersona.SourceIndex;
using ProjectPersona.SourceIndex.Models;

namespace ProjectPersona.Application.Services.RagSearchService.Pipes;

public class TypeSourceProducerPipe : ChannelPipeBase<Signal, IReadOnlyCollection<TypeSourceDeclaration>>
{
    /// <summary> Держатель индекса исходников. </summary>
    private readonly TypeSourceIndexHolder _indexHolder;
    /// <summary> Размер пачки деклараций. </summary>
    private readonly int _batchSize;

    /// <summary> Создать пайп поставки деклараций из исходников. </summary>
    public TypeSourceProducerPipe(TypeSourceIndexHolder indexHolder, int batchSize = 256)
        : base(capacity: 1)
    {
        _indexHolder = indexHolder
                       ?? throw new ArgumentNullException(nameof(indexHolder));
        _batchSize = batchSize < 1
            ? throw new ArgumentOutOfRangeException("BatchSize must be bigger or equal than 1!", nameof(batchSize))
            : batchSize;
    }

    /// <inheritdoc />
    protected override async ValueTask ProcessAsync(
        Signal item,
        Func<IReadOnlyCollection<TypeSourceDeclaration>, ValueTask> emit,
        CancellationToken ct)
    {
        var index = await _indexHolder.GetAsync(ct).ConfigureAwait(false);

        foreach (var chunk in index.EnumerateDeclarations().Chunk(_batchSize))
            await emit(chunk).ConfigureAwait(false);
    }
}
