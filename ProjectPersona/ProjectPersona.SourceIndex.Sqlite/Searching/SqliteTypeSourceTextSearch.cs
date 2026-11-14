using Microsoft.Data.Sqlite;
using ProjectPersona.SourceIndex.Contracts;
using ProjectPersona.SourceIndex.Models;
using ProjectPersona.SourceIndex.Sqlite.Configuration;
using ProjectPersona.SourceIndex.Sqlite.Connections;

namespace ProjectPersona.SourceIndex.Sqlite.Searching;

/// <summary>
/// Поиск подстроки по исходникам через FTS5 (токенайзер <c>trigram</c>): индекс отбирает объявления-кандидаты,
/// точные позиции вхождений и срезы членов определяются по тексту кандидата.
/// </summary>
public sealed class SqliteTypeSourceTextSearch : ITypeSourceTextSearch
{
    private readonly SqliteTypeSourceIndexOptions _options;

    /// <summary>
    /// Создаёт поиск для файла, заданного <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Расположение файла индекса.</param>
    public SqliteTypeSourceTextSearch(SqliteTypeSourceIndexOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Файл индекса отсутствует, не читается или имеет другую версию формата.</exception>
    public IReadOnlyList<TypeSourceTextMatch> Search(
        string query,
        string? assemblyName = null,
        bool ignoreCase = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var connectionString = SqliteIndexFile.GetReadOnlyConnectionString(_options);
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var assembly = string.IsNullOrWhiteSpace(assemblyName) ? null : assemblyName.Trim();

        // SqliteConnection не потокобезопасен, а MCP вызывает tools параллельно: соединение открывается на каждый вызов.
        using var connection = SqliteIndexFile.OpenReadOnly(connectionString);
        using var candidates = SqliteTextSearchQueries.CreateCandidatesCommand(connection, query, assembly);
        using var slices = SqliteTextSearchQueries.CreateSlicesCommand(connection);

        var matches = new List<TypeSourceTextMatch>();
        using var reader = candidates.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var declaration = new CandidateDeclaration(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4));

            AddIfFound(
                matches,
                declaration.AssemblyName,
                declaration.TargetFramework,
                declaration.Type,
                memberName: null,
                declaration.SourceCode,
                query,
                comparison);

            AddSliceMatches(matches, slices, declaration, query, comparison);
        }

        return matches;
    }

    /// <summary>Проверяет срезы членов кандидата; срезы нулевой длины пропускаются.</summary>
    private static void AddSliceMatches(
        List<TypeSourceTextMatch> matches,
        SqliteCommand slices,
        CandidateDeclaration declaration,
        string query,
        StringComparison comparison)
    {
        slices.Parameters["$declaration"].Value = declaration.Id;

        using var sliceReader = slices.ExecuteReader();
        while (sliceReader.Read())
        {
            var start = (int)sliceReader.GetInt64(1);
            var length = (int)sliceReader.GetInt64(2);
            if (length <= 0)
                continue;

            AddIfFound(
                matches,
                declaration.AssemblyName,
                declaration.TargetFramework,
                declaration.Type,
                sliceReader.GetString(0),
                declaration.SourceCode.Substring(start, length),
                query,
                comparison);
        }
    }

    /// <summary>FTS5 без учёта регистра даёт надмножество совпадений; точное сравнение и позиции определяются здесь.</summary>
    private static void AddIfFound(
        List<TypeSourceTextMatch> matches,
        string assemblyName,
        string targetFramework,
        string type,
        string? memberName,
        string text,
        string query,
        StringComparison comparison)
    {
        var indexes = SubstringOccurrences.FindAll(text, query, comparison);
        if (indexes.Count > 0)
            matches.Add(new TypeSourceTextMatch(assemblyName, targetFramework, type, memberName, text, indexes));
    }

    /// <summary>Объявление-кандидат, отобранное FTS5: строка таблицы <c>declaration</c> с именами сборки и типа.</summary>
    private sealed record CandidateDeclaration(
        long Id,
        string AssemblyName,
        string TargetFramework,
        string Type,
        string SourceCode);
}
