using Microsoft.Data.Sqlite;
using ProjectPersona.SourceIndex.Sqlite.Schema;

namespace ProjectPersona.SourceIndex.Sqlite.Searching;

/// <summary>
/// SQL-команды поиска подстроки: отбор кандидатов через FTS5 и чтение срезов членов.
/// </summary>
internal static class SqliteTextSearchQueries
{
    /// <summary>
    /// Создаёт команду, которая отбирает объявления через FTS5 по фразе и необязательной сборке.
    /// </summary>
    /// <param name="connection">Открытое соединение.</param>
    /// <param name="query">Искомая подстрока; экранируется в фразу FTS5.</param>
    /// <param name="assembly">Имя сборки или <see langword="null"/> — все сборки.</param>
    /// <returns>Команда, готовая к <see cref="SqliteCommand.ExecuteReader()"/>.</returns>
    public static SqliteCommand CreateCandidatesCommand(SqliteConnection connection, string query, string? assembly)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT d.id, a.name, a.target_framework, d.fqn, d.source_code
            FROM {SqliteSchema.FullTextTable}
                JOIN declaration d ON d.id = {SqliteSchema.FullTextTable}.rowid
                JOIN assembly a ON a.id = d.assembly_id
            WHERE {SqliteSchema.FullTextTable} MATCH $match
                AND ($assembly IS NULL OR a.name = $assembly)
            ORDER BY d.id
            """;

        command.Parameters.AddWithValue("$match", ToPhraseQuery(query));
        command.Parameters.AddWithValue("$assembly", (object?)assembly ?? DBNull.Value);

        return command;
    }

    /// <summary>
    /// Создаёт команду чтения срезов членов; значение <c>$declaration</c> задаётся перед каждым выполнением.
    /// </summary>
    /// <param name="connection">Открытое соединение.</param>
    /// <returns>Команда с параметром <c>$declaration</c>.</returns>
    public static SqliteCommand CreateSlicesCommand(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT name, start, length FROM member_slice WHERE declaration_id = $declaration ORDER BY id";
        command.Parameters.Add(new SqliteParameter { ParameterName = "$declaration" });
        return command;
    }

    /// <summary>
    /// Экранирует запрос в фразу FTS5: иначе пробелы, <c>-</c>, <c>:</c> и скобки читаются как операторы синтаксиса.
    /// </summary>
    /// <param name="query">Искомая подстрока.</param>
    /// <returns>Фраза в двойных кавычках с удвоенными внутренними кавычками.</returns>
    public static string ToPhraseQuery(string query) => $"\"{query.Replace("\"", "\"\"")}\"";
}
