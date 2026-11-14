using Microsoft.Data.Sqlite;
using ProjectPersona.SourceIndex.Sqlite.Schema;
using ProjectPersona.SourceIndex.Storage;

namespace ProjectPersona.SourceIndex.Sqlite.Writing;

/// <summary>
/// Создаёт файл индекса с нуля: схема, массовая вставка, индексы, версия формата и самопроверка.
/// </summary>
internal static class SqliteIndexDatabaseBuilder
{
    /// <summary>
    /// Строит базу по пути <paramref name="path"/> из снимка и сверяет число записанных строк со снимком.
    /// </summary>
    /// <param name="path">Путь к создаваемому файлу базы.</param>
    /// <param name="snapshot">Индекс исходников для записи.</param>
    /// <param name="cancellationToken">Токен отмены вставки.</param>
    /// <returns>Фактические счётчики записанной базы.</returns>
    /// <exception cref="InvalidOperationException">Число строк в базе не совпало со снимком.</exception>
    public static TypeSourceIndexWriteResult Build(
        string path,
        TypeSourceIndexSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var expected = SqliteIndexVerifier.CountExpected(snapshot);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        // Артефакт воспроизводимый: при сбое файл генерируется заново, поэтому журнал и fsync не нужны.
        Execute(connection, "PRAGMA journal_mode = OFF; PRAGMA synchronous = OFF; PRAGMA temp_store = MEMORY;");
        Execute(connection, SqliteSchema.Tables);

        using (var transaction = connection.BeginTransaction())
        {
            using var rows = new SqliteIndexRowWriter(connection, transaction);
            rows.WriteAll(snapshot, cancellationToken);
            transaction.Commit();
        }

        Execute(connection, SqliteSchema.Indexes);
        Execute(connection, SqliteSchema.FullTextIndex);
        Execute(connection, $"PRAGMA user_version = {SqliteIndexFormat.FormatVersion}");
        Execute(connection, "PRAGMA optimize");

        var actual = SqliteIndexVerifier.CountActual(connection);
        SqliteIndexVerifier.EnsureMatches(expected, actual);
        return actual;
    }

    /// <summary>Выполняет SQL без результата.</summary>
    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
