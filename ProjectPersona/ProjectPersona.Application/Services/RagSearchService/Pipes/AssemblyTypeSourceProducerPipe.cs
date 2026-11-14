using ProjectPersona.Application.Pipes;
using ProjectPersona.Application.Services.TypeSourceIndexService;
using ProjectPersona.SourceIndex;
using ProjectPersona.SourceIndex.Models;

namespace ProjectPersona.Application.Services.RagSearchService.Pipes;

/// <summary> Пайп поставки деклараций только указанных сборок. Остальные сборки индекса не перечисляются. </summary>
public class AssemblyTypeSourceProducerPipe : ChannelPipeBase<Signal, IReadOnlyCollection<TypeSourceDeclaration>>
{
    /// <summary> Держатель индекса исходников. </summary>
    private readonly TypeSourceIndexHolder _indexHolder;
    /// <summary> Пары «код сборки – короткий TFM», декларации которых нужно поставить. </summary>
    private readonly IReadOnlyList<(string Assembly, string? TargetFramework)> _assemblies;
    /// <summary> Размер пачки деклараций. </summary>
    private readonly int _batchSize;

    /// <summary> Создать пайп поставки деклараций указанных сборок из исходников. </summary>
    /// <param name="indexHolder"> Держатель индекса исходников. </param>
    /// <param name="assemblies">
    /// Пары «код сборки – короткий TFM». Если TFM равен <see langword="null"/> или пустой, поставляются декларации сборки для всех TFM.
    /// </param>
    /// <param name="batchSize"> Размер пачки деклараций. </param>
    /// <exception cref="ArgumentNullException"> Держатель или коллекция сборок равны null. </exception>
    /// <exception cref="ArgumentException"> Код сборки пустой либо состоит из пробелов. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Размер пачки меньше 1. </exception>
    public AssemblyTypeSourceProducerPipe(
        TypeSourceIndexHolder indexHolder,
        IEnumerable<(string Assembly, string? TargetFramework)> assemblies,
        int batchSize = 256)
        : base(capacity: 1)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        _indexHolder = indexHolder
                       ?? throw new ArgumentNullException(nameof(indexHolder));
        _assemblies = assemblies
            .Select(assembly =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(assembly.Assembly, nameof(assemblies));
                return assembly;
            })
            .ToList();
        _batchSize = batchSize < 1
            ? throw new ArgumentOutOfRangeException(nameof(batchSize), "BatchSize must be bigger or equal than 1!")
            : batchSize;
    }

    /// <inheritdoc />
    protected override async ValueTask ProcessAsync(
        Signal item,
        Func<IReadOnlyCollection<TypeSourceDeclaration>, ValueTask> emit,
        CancellationToken ct)
    {
        var index = await _indexHolder.GetAsync(ct).ConfigureAwait(false);

        foreach (var chunk in index.EnumerateDeclarations(_assemblies).Chunk(_batchSize))
            await emit(chunk).ConfigureAwait(false);
    }
}
