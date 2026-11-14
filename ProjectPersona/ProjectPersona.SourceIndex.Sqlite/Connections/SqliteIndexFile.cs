using Microsoft.Data.Sqlite;
using ProjectPersona.SourceIndex.Sqlite.Configuration;
using ProjectPersona.SourceIndex.Sqlite.Schema;

namespace ProjectPersona.SourceIndex.Sqlite.Connections;

/// <summary>
/// Проверка файла индекса и открытие соединений для чтения; общая для всех читателей базы.
/// </summary>
internal static class SqliteIndexFile
{
    /// <summary>
    /// Проверяет наличие файла, версию формата и наличие обязательных таблиц; содержимое в память не загружается.
    /// </summary>
    /// <param name="options">Расположение файла индекса.</param>
    /// <returns>Строка подключения в режиме только чтения.</returns>
    /// <exception cref="InvalidOperationException">Файл отсутствует, не является базой SQLite, имеет другую версию формата или в нём нет обязательных таблиц.</exception>
    public static string GetReadOnlyConnectionString(SqliteTypeSourceIndexOptions options)
    {
        var path = options.ResolveDatabasePath();
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Type source index not found at '{path}'. Generate it first by running the generate-type-source-index command.");
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        var version = ReadFormatVersion(connectionString, path);
        if (version != SqliteIndexFormat.FormatVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported type source index format version {version} in '{path}'. " +
                $"Expected {SqliteIndexFormat.FormatVersion}. Re-run generate-type-source-index.");
        }

        EnsureRequiredTables(connectionString, path);

        return connectionString;
    }

    /// <summary>
    /// Открывает соединение по строке из <see cref="GetReadOnlyConnectionString"/>; закрывает его вызывающий.
    /// </summary>
    /// <param name="connectionString">Строка подключения в режиме только чтения.</param>
    /// <returns>Открытое соединение.</returns>
    public static SqliteConnection OpenReadOnly(string connectionString)
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>Проверяет, что в <c>sqlite_master</c> есть все таблицы из <see cref="SqliteSchema.RequiredTables"/>.</summary>
    private static void EnsureRequiredTables(string connectionString, string path)
    {
        try
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";

            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    existing.Add(reader.GetString(0));
            }

            var missing = SqliteSchema.RequiredTables.Where(table => !existing.Contains(table)).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Type source index '{path}' is missing required tables: {string.Join(", ", missing)}. " +
                    "Re-run generate-type-source-index.");
            }
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException(
                $"Type source index '{path}' cannot be read. Re-run generate-type-source-index.",
                ex);
        }
    }

    /// <summary>Читает версию формата из <c>PRAGMA user_version</c>.</summary>
    private static long ReadFormatVersion(string connectionString, string path)
    {
        try
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version";
            return Convert.ToInt64(command.ExecuteScalar());
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException(
                $"Type source index '{path}' cannot be read. Re-run generate-type-source-index.",
                ex);
        }
    }
}
