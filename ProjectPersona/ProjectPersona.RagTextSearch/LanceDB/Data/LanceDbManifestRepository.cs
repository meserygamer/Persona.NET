using System.ComponentModel.DataAnnotations;
using System.Text;
using Apache.Arrow;
using Apache.Arrow.Types;
using lancedb;
using ProjectPersona.RagTextSearch.Manifest;
using ProjectPersona.RagTextSearch.Utils;

namespace ProjectPersona.RagTextSearch.LanceDB.Data;

/// <summary>
/// Манифест основной таблицы в служебной таблице <see cref="MetaTableName"/>: по строке на основную таблицу
/// с ключом <c>table_name</c>. Операции затрагивают только строку своей таблицы.
/// </summary>
/// <remarks>
/// Чтение идёт под <see cref="AsyncReaderWriterLock.ReadAsync"/>, запись — под <see cref="AsyncReaderWriterLock.WriteAsync"/>.
/// Блокировка внутрипроцессная: одновременная запись из нескольких процессов не поддерживается.
/// </remarks>
public class LanceDbManifestRepository
{
    /// <summary> Имя служебной таблицы манифестов. </summary>
    public const string MetaTableName = "_meta";

    private const string TableNameColumn = "table_name";
    private const string ManifestColumn = "manifest";

    /// <summary> Настройки LanceDB. </summary>
    private readonly LanceDbSettings _settings;
    /// <summary> Подключение к БД. </summary>
    private readonly LanceDbConnectionProvider _connections;
    /// <summary> Общая блокировка доступа к БД. </summary>
    private readonly AsyncReaderWriterLock _lock;

    /// <summary> Схема служебной таблицы. </summary>
    private static readonly Schema MetaSchema = new Schema.Builder()
        .Field(new Field(TableNameColumn, StringType.Default, nullable: false))
        .Field(new Field(ManifestColumn, StringType.Default, nullable: false))
        .Build();

    /// <summary> Создать репозиторий манифеста. </summary>
    /// <exception cref="ArgumentNullException"> Один из параметров равен null. </exception>
    public LanceDbManifestRepository(
        LanceDbSettings settings,
        LanceDbConnectionProvider connections,
        AsyncReaderWriterLock accessLock)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _lock = accessLock ?? throw new ArgumentNullException(nameof(accessLock));

        Validator.ValidateObject(settings, new ValidationContext(settings), true);
    }

    /// <summary> Прочитать манифест основной таблицы. </summary>
    /// <param name="ct"> Токен отмены. </param>
    /// <returns> Манифест; <see langword="null"/>, если его нет. </returns>
    /// <exception cref="InvalidOperationException"> Манифест повреждён. </exception>
    public async ValueTask<VectorStoreManifest?> ReadAsync(CancellationToken ct = default)
    {
        using var _ = await _lock.ReadAsync(ct).ConfigureAwait(false);
        return await ReadCoreAsync(ct).ConfigureAwait(false);
    }

    /// <summary> Записать манифест основной таблицы, заменив прежний. </summary>
    /// <param name="manifest"> Манифест. </param>
    /// <param name="ct"> Токен отмены. </param>
    public async ValueTask WriteAsync(VectorStoreManifest manifest, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        using var _ = await _lock.WriteAsync(ct).ConfigureAwait(false);
        await WriteCoreAsync(manifest, ct).ConfigureAwait(false);
    }

    /// <summary> Удалить манифест основной таблицы. Если его нет — ничего не делает. </summary>
    /// <param name="ct"> Токен отмены. </param>
    public async ValueTask DeleteAsync(CancellationToken ct = default)
    {
        using var _ = await _lock.WriteAsync(ct).ConfigureAwait(false);
        var connection = await _connections.GetConnectionAsync(ct).ConfigureAwait(false);

        if (!await MetaTableExistsAsync(connection).ConfigureAwait(false))
            return;

        using var table = await connection.OpenTable(MetaTableName).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        await table.Delete(RowPredicate).ConfigureAwait(false);
    }

    /// <summary> Прочитать манифест, изменить и записать обратно под одной блокировкой. </summary>
    /// <param name="update"> Изменение манифеста. </param>
    /// <param name="ct"> Токен отмены. </param>
    /// <exception cref="InvalidOperationException"> Манифеста нет или он повреждён. </exception>
    public async ValueTask UpdateAsync(Func<VectorStoreManifest, VectorStoreManifest> update, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        using var _ = await _lock.WriteAsync(ct).ConfigureAwait(false);
        var manifest = await ReadCoreAsync(ct).ConfigureAwait(false)
                       ?? throw new InvalidOperationException(
                           $"Манифест таблицы {_settings.TableName} не найден: выполните полную векторизацию.");

        await WriteCoreAsync(update(manifest), ct).ConfigureAwait(false);
    }

    /// <summary> Предикат строки основной таблицы. </summary>
    private string RowPredicate =>
        $"{TableNameColumn} = '{_settings.TableName.Replace("'", "''", StringComparison.Ordinal)}'";

    /// <summary> Прочитать метаданные. </summary>
    /// <param name="ct"> Токен отмены. </param>
    /// <returns> Метаданные или NULL в случае их отсутствия. </returns>
    /// <exception cref="InvalidOperationException"> Если схема метаданных не валидна. </exception>
    private async ValueTask<VectorStoreManifest?> ReadCoreAsync(CancellationToken ct)
    {
        var connection = await _connections.GetConnectionAsync(ct).ConfigureAwait(false);
        if (!await MetaTableExistsAsync(connection).ConfigureAwait(false))
            return null;

        using var table = await connection.OpenTable(MetaTableName).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        var batch = await table.Query()
            .Where(RowPredicate)
            .Limit(1)
            .ToArrow()
            .ConfigureAwait(false);

        if (batch.Length == 0)
            return null;

        var column = batch.Column(ManifestColumn) as StringArray
                     ?? throw new InvalidOperationException(
                         $"Служебная таблица {MetaTableName} не содержит колонку {ManifestColumn}.");

        return VectorStoreManifestJson.Deserialize(column.GetString(0, Encoding.UTF8));
    }
    
    /// <summary> Записать манифест. </summary>
    /// <param name="manifest"> Манифест. </param>
    /// <param name="ct"> Токен отмены. </param>
    private async ValueTask WriteCoreAsync(VectorStoreManifest manifest, CancellationToken ct)
    {
        var connection = await _connections.GetConnectionAsync(ct).ConfigureAwait(false);

        if (!await MetaTableExistsAsync(connection).ConfigureAwait(false))
        {
            using var _ = await connection
                .CreateTable(MetaTableName, new CreateTableOptions { Schema = MetaSchema, Mode = "create" })
                .ConfigureAwait(false);
        }

        using var table = await connection.OpenTable(MetaTableName).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        var tableNameBuilder = new StringArray.Builder().Append(_settings.TableName);
        var manifestBuilder = new StringArray.Builder().Append(VectorStoreManifestJson.Serialize(manifest));
        using var batch = new RecordBatch(MetaSchema, [tableNameBuilder.Build(), manifestBuilder.Build()], 1);

        // Upsert одной операцией: строка не пропадает между удалением и вставкой.
        await table.MergeInsert(TableNameColumn)
            .WhenMatchedUpdateAll()
            .WhenNotMatchedInsertAll()
            .Execute(batch)
            .ConfigureAwait(false);
    }

    /// <summary> Проверить существование таблицы с метаданными. </summary>
    /// <param name="connection"> Подключение. </param>
    /// <returns> Существование таблицы с метаданными. </returns>
    private static async ValueTask<bool> MetaTableExistsAsync(Connection connection)
    {
        var tableNames = await connection.TableNames().ConfigureAwait(false);
        return tableNames.Contains(MetaTableName, StringComparer.Ordinal);
    }
}
