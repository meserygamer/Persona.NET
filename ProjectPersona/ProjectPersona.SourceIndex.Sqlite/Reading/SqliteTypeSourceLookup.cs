using ProjectPersona.SourceIndex.Contracts;
using ProjectPersona.SourceIndex.Find;
using ProjectPersona.SourceIndex.Models;
using ProjectPersona.SourceIndex.Naming;
using ProjectPersona.SourceIndex.Projection;
using ProjectPersona.SourceIndex.Sqlite.Connections;
using ProjectPersona.SourceIndex.UsedBy;

namespace ProjectPersona.SourceIndex.Sqlite.Reading;

/// <summary>
/// Поиск исходников по индексу в SQLite: данные читаются по запросу, ответы собирает общий <see cref="TypeSourceProjection"/>.
/// </summary>
internal sealed class SqliteTypeSourceLookup : ITypeSourceLookup
{
    private static readonly IReadOnlyList<TypeSourceMatch> Empty = [];
    private static readonly IReadOnlyList<TypeSourceUsedByMatch> EmptyUsedBy = [];

    private readonly string _connectionString;

    /// <summary>
    /// Создаёт поиск по базе, открываемой на каждый вызов.
    /// </summary>
    /// <param name="connectionString">Строка подключения в режиме только чтения.</param>
    public SqliteTypeSourceLookup(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <inheritdoc />
    public IReadOnlyList<TypeSourceMatch> Find(
        string fqn,
        string? assemblyName = null,
        IReadOnlyList<string>? memberNames = null)
    {
        var normalizedFqn = TypeFqn.NormalizeQuery(fqn);
        if (normalizedFqn is null)
            return Empty;

        return Read(reader =>
        {
            var declarations = reader.LoadDeclarations(normalizedFqn, assemblyName);
            if (declarations.Count == 0)
                return Empty;

            return TypeSourceProjection.ProjectMatches(declarations, memberNames);
        });
    }

    /// <inheritdoc />
    public IReadOnlyList<TypeSourceUsedByMatch> FindUsedBy(
        string fqn,
        string? assemblyName = null,
        IReadOnlyList<string>? memberNames = null,
        int limit = TypeSourceUsedByLimit.DefaultLimit)
    {
        var normalizedFqn = TypeFqn.NormalizeQuery(fqn);
        if (normalizedFqn is null)
            return EmptyUsedBy;

        return Read(reader =>
        {
            var declarations = reader.LoadDeclarations(normalizedFqn, assemblyName);
            if (declarations.Count == 0)
                return EmptyUsedBy;

            var usedBy = reader.LoadUsedBy(normalizedFqn);
            return TypeSourceProjection.ProjectUsedBy(declarations, usedBy, memberNames, limit);
        });
    }

    /// <inheritdoc />
    public long GetDeclarationCount(IEnumerable<(string AssemblyName, string? TargetFramework)>? assemblies)
    {
        using var connection = SqliteIndexFile.OpenReadOnly(_connectionString);
        return new SqliteIndexReader(connection).GetDeclarationCount(assemblies);
    }

    /// <inheritdoc />
    public IEnumerable<TypeSourceDeclaration> EnumerateDeclarations()
    {
        using var connection = SqliteIndexFile.OpenReadOnly(_connectionString);
        foreach (var declaration in new SqliteIndexReader(connection).EnumerateDeclarations())
            yield return declaration;
    }

    /// <inheritdoc />
    public IEnumerable<TypeSourceDeclaration> EnumerateDeclarations(IEnumerable<(string AssemblyName, string? TargetFramework)> keys)
    {
        var filters = TypeSourceAssemblyFilter.Normalize(keys);

        using var connection = SqliteIndexFile.OpenReadOnly(_connectionString);
        foreach (var declaration in new SqliteIndexReader(connection).EnumerateDeclarations(filters))
            yield return declaration;
    }

    /// <summary>Выполняет чтение на соединении, которое открывается на время вызова.</summary>
    private T Read<T>(Func<SqliteIndexReader, T> read)
    {
        // SqliteConnection не потокобезопасен, а MCP вызывает tools параллельно: соединение открывается на каждый вызов.
        using var connection = SqliteIndexFile.OpenReadOnly(_connectionString);
        return read(new SqliteIndexReader(connection));
    }
}
