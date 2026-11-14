using Microsoft.Data.Sqlite;
using ProjectPersona.SourceIndex.Storage;

namespace ProjectPersona.SourceIndex.Sqlite.Writing;

/// <summary>
/// Сверка счётчиков записи: сколько строк должно быть по снимку и сколько фактически лежит в базе.
/// </summary>
internal static class SqliteIndexVerifier
{
    /// <summary>
    /// Считает строки, которые должны появиться в базе по снимку.
    /// </summary>
    /// <param name="snapshot">Индекс исходников.</param>
    /// <returns>Ожидаемые счётчики по всем таблицам.</returns>
    public static TypeSourceIndexWriteResult CountExpected(TypeSourceIndexSnapshot snapshot)
    {
        var assemblies = snapshot.Declarations
            .Select(static match => SqliteAssemblyKey.Of(match))
            .Distinct()
            .Count();

        var slices = 0;
        var references = 0;
        foreach (var match in snapshot.Declarations)
        {
            references += match.Full.References?.Count ?? 0;
            slices += match.Members.Count;
            foreach (var slice in match.Members)
                references += slice.References.Count;
        }

        var usedBy = 0;
        foreach (var entry in snapshot.UsedBy.Values)
        {
            usedBy += entry.TypeUsedBy.Count;
            foreach (var groups in entry.MemberUsedBy.Values)
            {
                foreach (var group in groups)
                    usedBy += group.UsedBy.Count;
            }
        }

        return new TypeSourceIndexWriteResult(assemblies, snapshot.Declarations.Count, slices, references, usedBy);
    }

    /// <summary>
    /// Считает строки в таблицах записанной базы.
    /// </summary>
    /// <param name="connection">Открытое соединение с базой.</param>
    /// <returns>Фактические счётчики по всем таблицам.</returns>
    public static TypeSourceIndexWriteResult CountActual(SqliteConnection connection) =>
        new(
            Count(connection, "assembly"),
            Count(connection, "declaration"),
            Count(connection, "member_slice"),
            Count(connection, "reference"),
            Count(connection, "used_by"));

    /// <summary>
    /// Проверяет, что фактические счётчики совпадают с ожидаемыми.
    /// </summary>
    /// <param name="expected">Счётчики по снимку.</param>
    /// <param name="actual">Счётчики записанной базы.</param>
    /// <exception cref="InvalidOperationException">Счётчики не совпали.</exception>
    public static void EnsureMatches(TypeSourceIndexWriteResult expected, TypeSourceIndexWriteResult actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"Type source index self-check failed: expected {expected}, but the database contains {actual}.");
        }
    }

    /// <summary>Считает строки таблицы.</summary>
    private static int Count(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return Convert.ToInt32(command.ExecuteScalar());
    }
}
