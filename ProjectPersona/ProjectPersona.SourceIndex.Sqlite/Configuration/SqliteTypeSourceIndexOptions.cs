using ProjectPersona.SourceIndex.Sqlite.Schema;

namespace ProjectPersona.SourceIndex.Sqlite.Configuration;

/// <summary>
/// Расположение SQLite-файла индекса исходников.
/// </summary>
public sealed class SqliteTypeSourceIndexOptions
{
    /// <summary>
    /// Полный путь к файлу индекса (например, <c>C:\indexes\type-source-index.db</c>). Если пусто — <c>BaseDirectory/type-source-index/type-source-index.db</c>.
    /// </summary>
    public string? DatabasePath { get; init; }

    /// <summary>
    /// Возвращает полный путь к файлу базы.
    /// </summary>
    public string ResolveDatabasePath()
    {
        return string.IsNullOrWhiteSpace(DatabasePath)
            ? Path.Combine(AppContext.BaseDirectory, "type-source-index", SqliteIndexFormat.DatabaseFileName)
            : DatabasePath;
    }
}
