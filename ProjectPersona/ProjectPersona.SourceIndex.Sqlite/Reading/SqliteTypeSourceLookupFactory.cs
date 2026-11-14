using ProjectPersona.SourceIndex.Contracts;
using ProjectPersona.SourceIndex.Sqlite.Configuration;
using ProjectPersona.SourceIndex.Sqlite.Connections;

namespace ProjectPersona.SourceIndex.Sqlite.Reading;

/// <summary>
/// Открывает индекс исходников из файла SQLite.
/// </summary>
public sealed class SqliteTypeSourceLookupFactory : ITypeSourceLookupFactory
{
    private readonly SqliteTypeSourceIndexOptions _options;

    /// <summary>
    /// Создаёт фабрику для файла, заданного <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Расположение файла индекса.</param>
    public SqliteTypeSourceLookupFactory(SqliteTypeSourceIndexOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string Location => _options.ResolveDatabasePath();

    /// <summary>
    /// Проверяет наличие файла и версию формата; содержимое в память не загружается.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Поиск, который читает данные из файла по запросу.</returns>
    /// <exception cref="InvalidOperationException">Файл отсутствует, не является базой SQLite или имеет другую версию формата.</exception>
    public Task<ITypeSourceLookup> OpenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var connectionString = SqliteIndexFile.GetReadOnlyConnectionString(_options);
        return Task.FromResult<ITypeSourceLookup>(new SqliteTypeSourceLookup(connectionString));
    }
}
