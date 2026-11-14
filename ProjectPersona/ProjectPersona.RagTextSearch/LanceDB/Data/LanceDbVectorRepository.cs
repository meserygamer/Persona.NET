using System.ComponentModel.DataAnnotations;
using ProjectPersona.RagTextSearch.LanceDB.Structure;
using ProjectPersona.RagTextSearch.Utils;

namespace ProjectPersona.RagTextSearch.LanceDB.Data;

/// <summary> Слой данных: запись и поиск векторов в уже подготовленной таблице. </summary>
/// <remarks>
/// Добавление и поиск идут под <see cref="AsyncReaderWriterLock.ReadAsync"/> (LanceDB допускает параллельные
/// операции с данными), поэтому не мешают друг другу, но исключаются структурными операциями.
/// </remarks>
public class LanceDbVectorRepository
{
    /// <summary> Максимум строк в одном батче при записи. </summary>
    private const int WriteBatchSize = 1024;
    
    /// <summary> Настройки LanceDB. </summary>
    private readonly LanceDbSettings _settings;
    /// <summary> Структура таблицы. </summary>
    private readonly LanceDbTableManager _tableManager;
    /// <summary> Подключение к БД. </summary>
    private readonly LanceDbConnectionProvider _connections;
    /// <summary> Преобразователь записей. </summary>
    private readonly VectorDataArrowMapper _mapper;
    /// <summary> Общая блокировка доступа к БД. </summary>
    private readonly AsyncReaderWriterLock _lock;

    /// <summary> Создать репозиторий векторов. </summary>
    /// <exception cref="ArgumentNullException"> Один из параметров равен null. </exception>
    public LanceDbVectorRepository(
        LanceDbSettings settings,
        LanceDbTableManager tableManager,
        LanceDbConnectionProvider connections,
        VectorDataArrowMapper mapper,
        AsyncReaderWriterLock accessLock)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _tableManager = tableManager ?? throw new ArgumentNullException(nameof(tableManager));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _lock = accessLock ?? throw new ArgumentNullException(nameof(accessLock));
        
        Validator.ValidateObject(settings, new ValidationContext(settings), true);
    }

    /// <summary> Добавить векторы в таблицу. </summary>
    /// <param name="vectors"> Векторы. </param>
    /// <param name="ct"> Токен отмены. </param>
    /// <exception cref="ArgumentException"> Некорректные метаданные или длина вектора. </exception>
    public async ValueTask AddAsync(IReadOnlyList<VectorData> vectors, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        if (vectors.Count == 0)
            return;

        _mapper.EnsureValidRecords(vectors);
        await _tableManager.EnsureReadyAsync(ct).ConfigureAwait(false);

        using var _ = await _lock.ReadAsync(ct).ConfigureAwait(false);
        var connection = await _connections.GetConnectionAsync(ct).ConfigureAwait(false);
        using var table = await connection.OpenTable(_settings.TableName).ConfigureAwait(false);

        for (var offset = 0; offset < vectors.Count; offset += WriteBatchSize)
        {
            ct.ThrowIfCancellationRequested();

            var batchLength = Math.Min(WriteBatchSize, vectors.Count - offset);
            using var batch = _mapper.Pack(vectors, offset, batchLength);
            await table.Add(batch).ConfigureAwait(false);
        }
    }

    /// <summary> Удалить векторы сборок из таблицы одним запросом. </summary>
    /// <param name="assemblies">
    /// Пары «код сборки – короткий TFM компиляции». Если TFM равен null или пустой, удаляются векторы сборки для всех TFM.
    /// </param>
    /// <param name="ct"> Токен отмены. </param>
    /// <returns> Задача удаления. Если таблицы нет или коллекция пуста, метод завершается без изменений. </returns>
    /// <exception cref="ArgumentNullException"> Коллекция равна null. </exception>
    /// <exception cref="ArgumentException"> Код сборки пустой. </exception>
    public async ValueTask DeleteByAssembliesAsync(IEnumerable<(string Assembly, string? TargetFramework)> assemblies,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var predicates = new List<string>();
        foreach (var (assembly, targetFramework) in assemblies)
        {
            if (string.IsNullOrEmpty(assembly))
                throw new ArgumentException("Код сборки не может быть пустым.", nameof(assemblies));

            var escapedAssembly = assembly.Replace("'", "''", StringComparison.Ordinal);
            var escapedTargetFramework = targetFramework?.Replace("'", "''", StringComparison.Ordinal);
            
            var predicate = string.IsNullOrEmpty(targetFramework)
                ? $"assembly = '{escapedAssembly}'"
                : $"(assembly = '{escapedAssembly}' AND target_framework = '{escapedTargetFramework}')";
            
            predicates.Add(predicate);
        }

        if (predicates.Count == 0)
            return;

        if (!await _tableManager.ExistsAsync(ct).ConfigureAwait(false))
            return;

        using var _ = await _lock.ReadAsync(ct).ConfigureAwait(false);
        var connection = await _connections.GetConnectionAsync(ct).ConfigureAwait(false);
        using var table = await connection.OpenTable(_settings.TableName).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        await table.Delete(string.Join(" OR ", predicates)).ConfigureAwait(false);
    }

    /// <summary> Найти ближайшие векторы. </summary>
    /// <param name="vector"> Поисковый вектор. </param>
    /// <param name="maximumVectors"> Максимальное число результатов. </param>
    /// <param name="ct"> Токен отмены. </param>
    /// <exception cref="ArgumentOutOfRangeException"> maximumVectors меньше 1 или неверная размерность вектора. </exception>
    public async ValueTask<VectorData[]> SearchNearestAsync(float[] vector, int maximumVectors,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vector);

        if (maximumVectors < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumVectors),
                "maximumVectors must be greater than or equal to 1!");

        if (vector.Length != _settings.VectorDimension)
            throw new ArgumentOutOfRangeException(nameof(vector),
                $"Incorrect vector length! Vector length: {vector.Length}");

        await _tableManager.EnsureReadyAsync(ct).ConfigureAwait(false);

        using var _ = await _lock.ReadAsync(ct).ConfigureAwait(false);
        var connection = await _connections.GetConnectionAsync(ct).ConfigureAwait(false);
        using var table = await connection.OpenTable(_settings.TableName).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        var result = await table.Query()
            .NearestTo(vector)
            .Limit(maximumVectors)
            .ToArrow()
            .ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        return _mapper.Unpack(result);
    }
}
