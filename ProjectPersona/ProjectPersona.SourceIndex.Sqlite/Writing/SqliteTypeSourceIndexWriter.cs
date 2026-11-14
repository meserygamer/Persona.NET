using ProjectPersona.SourceIndex.Contracts;
using ProjectPersona.SourceIndex.Sqlite.Configuration;
using ProjectPersona.SourceIndex.Storage;

namespace ProjectPersona.SourceIndex.Sqlite.Writing;

/// <summary>
/// Записывает <see cref="TypeSourceIndexSnapshot"/> в один файл SQLite.
/// </summary>
public sealed class SqliteTypeSourceIndexWriter : ITypeSourceIndexWriter
{
    private readonly SqliteTypeSourceIndexOptions _options;

    /// <summary>
    /// Создаёт писатель для файла, заданного <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Расположение файла индекса.</param>
    public SqliteTypeSourceIndexWriter(SqliteTypeSourceIndexOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Пишет индекс во временный файл рядом с целевым и заменяет им целевой файл после проверки числа строк.
    /// </summary>
    /// <param name="snapshot">Индекс исходников для сохранения.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Число записанных элементов.</returns>
    /// <exception cref="InvalidOperationException">Число строк в базе не совпало со снимком или целевой файл занят другим процессом.</exception>
    public Task<TypeSourceIndexWriteResult> WriteAsync(
        TypeSourceIndexSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Task.Run(() => Write(snapshot, cancellationToken), cancellationToken);
    }

    /// <summary>Строит базу во временном файле и заменяет ею целевой; временный файл удаляется в любом случае.</summary>
    private TypeSourceIndexWriteResult Write(TypeSourceIndexSnapshot snapshot, CancellationToken cancellationToken)
    {
        var databasePath = _options.ResolveDatabasePath();
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        var tempPath = databasePath + ".tmp";
        File.Delete(tempPath);

        try
        {
            var result = SqliteIndexDatabaseBuilder.Build(tempPath, snapshot, cancellationToken);
            SqliteIndexFileReplacer.Replace(tempPath, databasePath);
            return result;
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
