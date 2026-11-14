using System.ComponentModel.DataAnnotations;
using Apache.Arrow;
using Apache.Arrow.Types;
using lancedb;
using Microsoft.Extensions.Logging;
using ProjectPersona.RagTextSearch.Utils;

namespace ProjectPersona.RagTextSearch.LanceDB.Structure;

/// <summary>
/// Слой структуры БД: существование, создание, проверка схемы, очистка, индекс и оптимизация таблицы.
/// </summary>
/// <remarks>
/// Операции, меняющие структуру, берут <see cref="AsyncReaderWriterLock.WriteAsync"/>;
/// чтение структуры — <see cref="AsyncReaderWriterLock.ReadAsync"/>. Методы не вызывают друг друга под блокировкой.
/// </remarks>
public class LanceDbTableManager
{
    /// <summary> Настройки LanceDB. </summary>
    private readonly LanceDbSettings _settings;
    /// <summary> Подключение к БД. </summary>
    private readonly LanceDbConnectionProvider _connections;
    /// <summary> Общая блокировка доступа к БД. </summary>
    private readonly AsyncReaderWriterLock _lock;
    /// <summary> Логгер. </summary>
    private readonly ILogger<LanceDbTableManager> _logger;

    /// <summary> Таблица создана/проверена. </summary>
    private volatile bool _initialized;

    /// <summary> Схема таблицы. </summary>
    public Schema Schema => field ??= BuildSchema();

    /// <summary> Создать менеджер структуры таблицы. </summary>
    /// <exception cref="ArgumentNullException"> Один из параметров равен null. </exception>
    public LanceDbTableManager(
        LanceDbSettings settings,
        LanceDbConnectionProvider connections,
        AsyncReaderWriterLock accessLock,
        ILogger<LanceDbTableManager> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _lock = accessLock ?? throw new ArgumentNullException(nameof(accessLock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        Validator.ValidateObject(settings, new ValidationContext(settings), true);
    }

    /// <summary> Гарантировать, что таблица существует и её схема совместима с настройками. </summary>
    /// <param name="ct"> Токен отмены. </param>
    /// <remarks> Выполняется один раз; дальше — быстрый выход без блокировки. </remarks>
    /// <exception cref="InvalidOperationException"> Схема существующей таблицы несовместима с настройками. </exception>
    public async ValueTask EnsureReadyAsync(CancellationToken ct = default)
    {
        if (_initialized)
            return;

        using var _ = await _lock.WriteAsync(ct).ConfigureAwait(false);
        if (_initialized)
            return;

        var connection = await _connections.GetConnectionAsync(ct).ConfigureAwait(false);

        if (await TableExistsCoreAsync(connection).ConfigureAwait(false))
        {
            using var existingTable = await connection.OpenTable(_settings.TableName).ConfigureAwait(false);
            await EnsureValidTableSchemaAsync(existingTable, ct).ConfigureAwait(false);
        }
        else
        {
            _logger.LogInformation("Таблица {TableName} не найдена, создаётся пустая таблица.", _settings.TableName);
            await OverwriteTableAsync(connection, ct).ConfigureAwait(false);
        }

        _initialized = true;
    }

    /// <summary> Проверить, создана ли таблица хранилища. </summary>
    /// <param name="ct"> Токен отмены. </param>
    /// <returns> Существование таблицы (без проверки схемы и наличия векторов). </returns>
    public async ValueTask<bool> ExistsAsync(CancellationToken ct = default)
    {
        using var _ = await _lock.ReadAsync(ct).ConfigureAwait(false);
        var connection = await _connections.GetConnectionAsync(ct).ConfigureAwait(false);
        return await TableExistsCoreAsync(connection).ConfigureAwait(false);
    }

    /// <summary> Опустошить таблицу, перезаписав её пустой схемой. Если таблицы нет — ничего не делает. </summary>
    /// <param name="ct"> Токен отмены. </param>
    public async ValueTask ClearAsync(CancellationToken ct = default)
    {
        using var _ = await _lock.WriteAsync(ct).ConfigureAwait(false);
        var connection = await _connections.GetConnectionAsync(ct).ConfigureAwait(false);

        if (await TableExistsCoreAsync(connection).ConfigureAwait(false))
            await OverwriteTableAsync(connection, ct).ConfigureAwait(false);
    }

    /// <summary> Создать векторный индекс, если его нет, и оптимизировать таблицу. </summary>
    /// <param name="ct"> Токен отмены. </param>
    public async ValueTask OptimizeAsync(CancellationToken ct = default)
    {
        await EnsureReadyAsync(ct).ConfigureAwait(false);

        using var _ = await _lock.WriteAsync(ct).ConfigureAwait(false);
        var connection = await _connections.GetConnectionAsync(ct).ConfigureAwait(false);
        using var table = await connection.OpenTable(_settings.TableName).ConfigureAwait(false);

        var indices = await table.ListIndices().ConfigureAwait(false);
        var hasVectorIndex = indices.Any(index =>
            string.Equals(index.Name, _settings.IndexVectorName, StringComparison.Ordinal));

        if (!hasVectorIndex)
        {
            await table.CreateIndex(
                ["vector"],
                new HnswSqIndex { DistanceType = DistanceType.Cosine },
                name: _settings.IndexVectorName).ConfigureAwait(false);
        }

        await table.Optimize().ConfigureAwait(false);
    }
    
    /// <summary> Создать Arrow схему таблицы. </summary>
    /// <returns> Arrow схема таблицы. </returns>
    private Schema BuildSchema()
    {
        var itemField = new Field("item", FloatType.Default, nullable: false);
        var vectorType = new FixedSizeListType(itemField, _settings.VectorDimension);
        return new Schema.Builder()
            .Field(new Field("assembly", StringType.Default, nullable: false))
            .Field(new Field("target_framework", StringType.Default, nullable: false))
            .Field(new Field("metadata", StringType.Default, nullable: false))
            .Field(new Field("vector", vectorType, nullable: false))
            .Build();
    }

    private async ValueTask<bool> TableExistsCoreAsync(Connection connection)
    {
        var tableNames = await connection.TableNames().ConfigureAwait(false);
        return tableNames.Contains(_settings.TableName, StringComparer.Ordinal);
    }

    private async ValueTask EnsureValidTableSchemaAsync(lancedb.Table table, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var schema = await table.Schema().ConfigureAwait(false);

        if (schema.FieldsList.All(static field => field.Name != "assembly"))
            throw new InvalidOperationException(
                "Существующая таблица LanceDB не содержит колонку assembly. " +
                "Очистите хранилище и выполните векторизацию заново.");

        if (schema.FieldsList.All(static field => field.Name != "target_framework"))
            throw new InvalidOperationException(
                "Существующая таблица LanceDB не содержит колонку target_framework. " +
                "Очистите хранилище и выполните векторизацию заново.");

        var vectorField = schema.FieldsList.FirstOrDefault(static field => field.Name == "vector");

        if (vectorField?.DataType is not FixedSizeListType listType)
            throw new InvalidOperationException(
                "Существующая таблица LanceDB не содержит колонку vector типа fixed_size_list.");

        if (listType.ListSize != _settings.VectorDimension)
            throw new InvalidOperationException(
                $"Длина вектора в таблице ({listType.ListSize}) не совпадает " +
                $"с настройкой VectorDimension ({_settings.VectorDimension}).");
    }

    private async ValueTask OverwriteTableAsync(Connection connection, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        using var _ = await connection
            .CreateTable(
                _settings.TableName,
                new CreateTableOptions { Schema = Schema, Mode = "overwrite" })
            .ConfigureAwait(false);
    }
}
