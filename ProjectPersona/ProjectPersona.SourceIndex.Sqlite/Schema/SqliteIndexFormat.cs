namespace ProjectPersona.SourceIndex.Sqlite.Schema;

/// <summary>
/// Константы формата файла индекса: имя файла по умолчанию и версия формата.
/// </summary>
public static class SqliteIndexFormat
{
    /// <summary>
    /// Имя файла базы внутри каталога индекса.
    /// </summary>
    public const string DatabaseFileName = "type-source-index.db";

    /// <summary>
    /// Текущая версия формата; хранится в <c>PRAGMA user_version</c>.
    /// </summary>
    public const int FormatVersion = 2;
}
