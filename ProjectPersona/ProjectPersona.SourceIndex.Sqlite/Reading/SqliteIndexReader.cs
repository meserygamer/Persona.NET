using Microsoft.Data.Sqlite;
using ProjectPersona.SourceIndex.Find;
using ProjectPersona.SourceIndex.Models;
using ProjectPersona.SourceIndex.Storage;
using ProjectPersona.SourceIndex.UsedBy;

namespace ProjectPersona.SourceIndex.Sqlite.Reading;

/// <summary>
/// SQL-чтение таблиц индекса на открытом соединении: строки превращаются в модели, ответы не собираются.
/// </summary>
internal sealed class SqliteIndexReader
{
    private readonly SqliteConnection _connection;

    /// <summary>
    /// Создаёт читатель поверх открытого соединения.
    /// </summary>
    /// <param name="connection">Открытое соединение с файлом индекса.</param>
    public SqliteIndexReader(SqliteConnection connection)
    {
        _connection = connection;
    }

    /// <summary>
    /// Загружает объявления FQN вместе со ссылками и срезами членов.
    /// </summary>
    /// <param name="fqn">Нормализованный FQN типа.</param>
    /// <param name="assemblyName">Имя сборки для сужения или <see langword="null"/> — все сборки.</param>
    /// <returns>Объявления в порядке сборок и вставки; пустой список, если FQN не найден.</returns>
    public List<TypeSourceIndexedMatch> LoadDeclarations(string fqn, string? assemblyName)
    {
        var assembly = string.IsNullOrWhiteSpace(assemblyName) ? null : assemblyName.Trim();

        using var command = _connection.CreateCommand();
        command.CommandText = assembly is null
            ? """
              SELECT d.id, a.name, a.target_framework, d.source_code
              FROM declaration d
              JOIN assembly a ON a.id = d.assembly_id
              WHERE d.fqn = $fqn
              ORDER BY a.id, d.id
              """
            : """
              SELECT d.id, a.name, a.target_framework, d.source_code
              FROM declaration d
              JOIN assembly a ON a.id = d.assembly_id
              WHERE d.fqn = $fqn AND a.name = $assembly
              ORDER BY a.id, d.id
              """;
        command.Parameters.AddWithValue("$fqn", fqn);
        if (assembly is not null)
            command.Parameters.AddWithValue("$assembly", assembly);

        var rows = new List<(long Id, string Assembly, string Tfm, string Source)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                rows.Add((
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3)));
        }

        var result = new List<TypeSourceIndexedMatch>(rows.Count);
        foreach (var row in rows)
        {
            var (declarationReferences, sliceReferences) = LoadReferences(row.Id);
            var slices = LoadSlices(row.Id, sliceReferences);
            var full = new TypeSourceMatch(
                row.Assembly,
                fqn,
                row.Tfm,
                row.Source,
                declarationReferences,
                Members: null);

            result.Add(new TypeSourceIndexedMatch(full, slices));
        }

        return result;
    }

    /// <summary>
    /// Загружает потребителей цели: ссылки на тип и ссылки на члены по именам и сигнатурам.
    /// </summary>
    /// <param name="fqn">Нормализованный FQN цели.</param>
    /// <returns>Рёбра потребителей или <see langword="null"/>, если рёбер нет.</returns>
    public UsedByFqnEntry? LoadUsedBy(string fqn)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT target_member, target_signature, consumer_assembly, consumer_tfm,
                   consumer_type, consumer_member, consumer_signature
            FROM used_by
            WHERE target_fqn = $fqn
            ORDER BY id
            """;
        command.Parameters.AddWithValue("$fqn", fqn);

        var typeUsedBy = new List<TypeSourceUsedBy>();
        var byMember = new Dictionary<string, Dictionary<string, List<TypeSourceUsedBy>>>(StringComparer.Ordinal);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var consumer = new TypeSourceUsedBy(
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6));

            if (reader.IsDBNull(0))
            {
                typeUsedBy.Add(consumer);
                continue;
            }

            var memberName = reader.GetString(0);
            var signature = reader.GetString(1);
            if (!byMember.TryGetValue(memberName, out var bySignature))
            {
                bySignature = new Dictionary<string, List<TypeSourceUsedBy>>(StringComparer.Ordinal);
                byMember[memberName] = bySignature;
            }

            if (!bySignature.TryGetValue(signature, out var consumers))
            {
                consumers = [];
                bySignature[signature] = consumers;
            }

            consumers.Add(consumer);
        }

        if (typeUsedBy.Count == 0 && byMember.Count == 0)
            return null;

        var memberUsedBy = byMember.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<MemberUsedBySignatureGroup>)pair.Value
                .OrderBy(static group => group.Key, StringComparer.Ordinal)
                .Select(static group => new MemberUsedBySignatureGroup(group.Key, group.Value))
                .ToList(),
            StringComparer.Ordinal);

        return new UsedByFqnEntry(typeUsedBy, memberUsedBy);
    }

    /// <summary> Получить число деклараций. </summary>
    /// <param name="assemblies">Пары сборки и TFM; <see langword="null"/> — все сборки, пустой TFM — любой TFM.</param>
    /// <returns>Число деклараций, подходящих хотя бы под одну пару.</returns>
    public long GetDeclarationCount(IEnumerable<(string AssemblyName, string? TargetFramework)>? assemblies)
    {
        using var command = _connection.CreateCommand();
        var parameters = new List<(string Name, string Value)>();

        if (assemblies is null)
        {
            command.CommandText = "SELECT COUNT(*) FROM declaration";
        }
        else
        {
            var filters = TypeSourceAssemblyFilter.Normalize(assemblies);
            if (filters.Count == 0)
                return 0;

            var declarationFilter = BuildKeyFilter(filters, parameters);
            command.CommandText = $"""
                                   SELECT COUNT(*)
                                   FROM declaration d
                                   JOIN assembly a ON a.id = d.assembly_id
                                   {declarationFilter}
                                   """;
        }

        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);

        var result = command.ExecuteScalar();
        return result is null or DBNull ? 0 : Convert.ToInt64(result);
    }

    /// <summary>
    /// Перечисляет все объявления в порядке id без ссылок.
    /// </summary>
    /// <returns>Ленивая последовательность; соединение должно оставаться открытым до её окончания.</returns>
    public IEnumerable<TypeSourceDeclaration> EnumerateDeclarations() =>
        EnumerateDeclarationsCore(keys: null);

    /// <summary>
    /// Перечисляет объявления указанных сборок в порядке id без ссылок.
    /// </summary>
    /// <param name="keys">Нормализованные пары сборки и TFM (<see cref="TypeSourceAssemblyFilter.Normalize"/>); TFM <see langword="null"/> — все TFM сборки.</param>
    /// <returns>Ленивая последовательность; соединение должно оставаться открытым до её окончания.</returns>
    public IEnumerable<TypeSourceDeclaration> EnumerateDeclarations(IReadOnlyList<(string AssemblyName, string? TargetFramework)> keys) =>
        EnumerateDeclarationsCore(keys);

    /// <summary>
    /// Общая реализация: срезы членов подтягиваются слиянием с курсором срезов.
    /// </summary>
    private IEnumerable<TypeSourceDeclaration> EnumerateDeclarationsCore(
        IReadOnlyList<(string AssemblyName, string? TargetFramework)>? keys)
    {
        if (keys is { Count: 0 })
            yield break;

        var filtered = keys is not null;
        var parameters = new List<(string Name, string Value)>();
        var declarationFilter = filtered ? BuildKeyFilter(keys!, parameters) : string.Empty;

        using var declarationCommand = _connection.CreateCommand();
        declarationCommand.CommandText = filtered
            ? $"""
              SELECT d.id, a.name, a.target_framework, d.fqn, d.source_code
              FROM declaration d
              JOIN assembly a ON a.id = d.assembly_id
              {declarationFilter}
              ORDER BY d.id
              """
            : """
              SELECT d.id, a.name, a.target_framework, d.fqn, d.source_code
              FROM declaration d
              JOIN assembly a ON a.id = d.assembly_id
              ORDER BY d.id
              """;

        using var sliceCommand = _connection.CreateCommand();
        sliceCommand.CommandText = filtered
            ? $"""
              SELECT declaration_id, name, start, length
              FROM member_slice
              WHERE declaration_id IN (
                  SELECT d.id
                  FROM declaration d
                  JOIN assembly a ON a.id = d.assembly_id
                  {declarationFilter})
              ORDER BY declaration_id, id
              """
            : """
              SELECT declaration_id, name, start, length
              FROM member_slice
              ORDER BY declaration_id, id
              """;

        foreach (var (name, value) in parameters)
        {
            declarationCommand.Parameters.AddWithValue(name, value);
            sliceCommand.Parameters.AddWithValue(name, value);
        }

        using var declarations = declarationCommand.ExecuteReader();
        using var slices = sliceCommand.ExecuteReader();

        // Оба курсора отсортированы по declaration_id, поэтому срезы подтягиваются слиянием без запроса на каждое объявление.
        var hasSlice = slices.Read();
        while (declarations.Read())
        {
            var declarationId = declarations.GetInt64(0);
            var members = new List<TypeSourceMemberSpan>();

            while (hasSlice && slices.GetInt64(0) < declarationId)
                hasSlice = slices.Read();

            while (hasSlice && slices.GetInt64(0) == declarationId)
            {
                members.Add(new TypeSourceMemberSpan(
                    slices.GetString(1),
                    (int)slices.GetInt64(2),
                    (int)slices.GetInt64(3)));
                hasSlice = slices.Read();
            }

            yield return new TypeSourceDeclaration(
                declarations.GetString(1),
                declarations.GetString(2),
                declarations.GetString(3),
                declarations.GetString(4),
                members);
        }
    }

    /// <summary>Строит условие WHERE по парам сборки и TFM и добавляет их параметры.</summary>
    /// <param name="keys">Пары сборки и TFM; TFM <see langword="null"/> или пустой — без фильтра по TFM.</param>
    /// <param name="parameters">Параметры запроса, которые заполняются по ходу построения.</param>
    /// <returns>Условие WHERE, объединяющее пары через OR; пустая строка, если пар нет.</returns>
    private static string BuildKeyFilter(
        IReadOnlyList<(string AssemblyName, string? TargetFramework)> keys,
        List<(string Name, string Value)> parameters)
    {
        if (keys.Count == 0)
            return string.Empty;

        var clauses = new List<string>(keys.Count);
        foreach (var (assemblyName, targetFramework) in keys)
        {
            var assemblyParameter = $"$assembly{parameters.Count}";
            parameters.Add((assemblyParameter, assemblyName));

            if (string.IsNullOrEmpty(targetFramework))
            {
                clauses.Add($"a.name = {assemblyParameter}");
                continue;
            }

            var tfmParameter = $"$tfm{parameters.Count}";
            parameters.Add((tfmParameter, targetFramework));
            clauses.Add($"(a.name = {assemblyParameter} AND a.target_framework = {tfmParameter})");
        }

        return "WHERE " + string.Join(" OR ", clauses);
    }

    /// <summary>Читает ссылки объявления: отдельно уровня типа и отдельно по id среза члена.</summary>
    private (List<TypeSourceReference> Declaration, Dictionary<long, List<TypeSourceReference>> BySlice) LoadReferences(
        long declarationId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT member_slice_id, kind, display, fqn, name, assembly_name, signature
            FROM reference
            WHERE declaration_id = $declaration
            ORDER BY id
            """;
        command.Parameters.AddWithValue("$declaration", declarationId);

        var declarationLevel = new List<TypeSourceReference>();
        var bySlice = new Dictionary<long, List<TypeSourceReference>>();

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var reference = new TypeSourceReference(
                (TypeSourceReferenceKind)(byte)reader.GetInt64(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6));

            if (reader.IsDBNull(0))
            {
                declarationLevel.Add(reference);
                continue;
            }

            var sliceId = reader.GetInt64(0);
            if (!bySlice.TryGetValue(sliceId, out var list))
            {
                list = [];
                bySlice[sliceId] = list;
            }

            list.Add(reference);
        }

        return (declarationLevel, bySlice);
    }

    /// <summary>Читает срезы членов объявления и подставляет к каждому его ссылки.</summary>
    private List<TypeSourceMemberSlice> LoadSlices(
        long declarationId,
        Dictionary<long, List<TypeSourceReference>> referencesBySlice)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, start, length, signature
            FROM member_slice
            WHERE declaration_id = $declaration
            ORDER BY id
            """;
        command.Parameters.AddWithValue("$declaration", declarationId);

        var result = new List<TypeSourceMemberSlice>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            IReadOnlyList<TypeSourceReference> references =
                referencesBySlice.TryGetValue(reader.GetInt64(0), out var list) ? list : [];

            result.Add(new TypeSourceMemberSlice(
                reader.GetString(1),
                (int)reader.GetInt64(2),
                (int)reader.GetInt64(3),
                references,
                reader.GetString(4)));
        }

        return result;
    }
}
