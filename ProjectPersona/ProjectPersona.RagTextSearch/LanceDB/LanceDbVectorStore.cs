using ProjectPersona.RagTextSearch.LanceDB.Data;
using ProjectPersona.RagTextSearch.LanceDB.Structure;
using ProjectPersona.RagTextSearch.Manifest;

namespace ProjectPersona.RagTextSearch.LanceDB;

/// <summary>
/// Хранилище векторов в LanceDB: реализация <see cref="IVectorStore"/>, делегирующая слою структуры
/// (<see cref="LanceDbTableManager"/>), слою данных (<see cref="LanceDbVectorRepository"/>)
/// и манифесту (<see cref="LanceDbManifestRepository"/>).
/// Блокировками управляют сами слои.
/// </summary>
public class LanceDbVectorStore : IVectorStore
{
    /// <summary> Структура таблицы. </summary>
    private readonly LanceDbTableManager _tableManager;
    /// <summary> Данные таблицы. </summary>
    private readonly LanceDbVectorRepository _repository;
    /// <summary> Манифест таблицы. </summary>
    private readonly LanceDbManifestRepository _manifest;
    /// <summary> Подключение к БД. </summary>
    private readonly LanceDbConnectionProvider _connections;

    /// <summary> Создать хранилище векторов на основе LanceDB. </summary>
    /// <param name="tableManager"> Структура таблицы. </param>
    /// <param name="repository"> Данные таблицы. </param>
    /// <param name="manifest"> Манифест таблицы. </param>
    /// <param name="connections"> Подключение к БД. </param>
    /// <exception cref="ArgumentNullException"> Один из параметров равен null. </exception>
    public LanceDbVectorStore(LanceDbTableManager tableManager,
        LanceDbVectorRepository repository,
        LanceDbManifestRepository manifest,
        LanceDbConnectionProvider connections)
    {
        _tableManager = tableManager ?? throw new ArgumentNullException(nameof(tableManager));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
    }

    /// <summary> Освобождает соединение с LanceDB. </summary>
    public void Dispose()
    {
        _connections.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public ValueTask AddVectorsAsync(IEnumerable<VectorData>? vectors, CancellationToken ct = default)
    {
        if (vectors is null)
            return ValueTask.CompletedTask;

        var safeVectors = vectors as IReadOnlyList<VectorData> ?? vectors.ToList();
        return _repository.AddAsync(safeVectors, ct);
    }

    /// <inheritdoc />
    public ValueTask<bool> StoreExistsAsync(CancellationToken ct = default)
        => _tableManager.ExistsAsync(ct);

    /// <inheritdoc />
    public ValueTask ClearStoreAsync(CancellationToken ct = default)
        => _tableManager.ClearAsync(ct);

    /// <inheritdoc />
    public ValueTask ClearAssembliesAsync(IEnumerable<(string Assembly, string? TargetFramework)> assemblies,
        CancellationToken ct = default)
        => _repository.DeleteByAssembliesAsync(assemblies, ct);

    /// <inheritdoc />
    public ValueTask<VectorData[]> SearchNearestVectorsAsync(float[] vector, int maximumVectors,
        CancellationToken ct = default)
        => _repository.SearchNearestAsync(vector, maximumVectors, ct);

    /// <inheritdoc />
    public ValueTask OptimizeAsync(CancellationToken ct = default)
        => _tableManager.OptimizeAsync(ct);

    /// <inheritdoc />
    public ValueTask<VectorStoreManifest?> ReadManifestAsync(CancellationToken ct = default)
        => _manifest.ReadAsync(ct);

    /// <inheritdoc />
    public ValueTask WriteManifestAsync(VectorStoreManifest manifest, CancellationToken ct = default)
        => _manifest.WriteAsync(manifest, ct);

    /// <inheritdoc />
    public ValueTask DeleteManifestAsync(CancellationToken ct = default)
        => _manifest.DeleteAsync(ct);

    /// <inheritdoc />
    public ValueTask MarkUpdatingAsync(IEnumerable<(string Assembly, string? TargetFramework)> assemblies,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var keys = assemblies.ToList();
        return _manifest.UpdateAsync(manifest => manifest.WithUpdating(keys), ct);
    }

    /// <inheritdoc />
    public ValueTask MarkReadyAsync(IEnumerable<(string Assembly, string? TargetFramework)> assemblies,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var keys = assemblies.ToList();
        return _manifest.UpdateAsync(manifest => manifest.WithCompleted(keys), ct);
    }
}
