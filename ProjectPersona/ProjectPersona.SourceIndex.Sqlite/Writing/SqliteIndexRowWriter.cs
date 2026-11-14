using Microsoft.Data.Sqlite;
using ProjectPersona.SourceIndex.Models;
using ProjectPersona.SourceIndex.Storage;
using ProjectPersona.SourceIndex.UsedBy;

namespace ProjectPersona.SourceIndex.Sqlite.Writing;

/// <summary>
/// Вставляет строки снимка в таблицы индекса внутри одной транзакции; идентификаторы назначаются здесь.
/// </summary>
internal sealed class SqliteIndexRowWriter : IDisposable
{
    private readonly PreparedInsert _assemblyInsert;
    private readonly PreparedInsert _declarationInsert;
    private readonly PreparedInsert _sliceInsert;
    private readonly PreparedInsert _referenceInsert;
    private readonly PreparedInsert _usedByInsert;

    /// <summary>
    /// Подготавливает команды вставки в рамках переданной транзакции.
    /// </summary>
    /// <param name="connection">Открытое соединение с создаваемой базой.</param>
    /// <param name="transaction">Транзакция, в которой выполняются вставки.</param>
    public SqliteIndexRowWriter(SqliteConnection connection, SqliteTransaction transaction)
    {
        _assemblyInsert = new PreparedInsert(
            connection,
            transaction,
            "INSERT INTO assembly (id, name, target_framework) VALUES ($id, $name, $tfm)",
            "$id", "$name", "$tfm");
        _declarationInsert = new PreparedInsert(
            connection,
            transaction,
            "INSERT INTO declaration (id, assembly_id, fqn, source_code) VALUES ($id, $assembly, $fqn, $source)",
            "$id", "$assembly", "$fqn", "$source");
        _sliceInsert = new PreparedInsert(
            connection,
            transaction,
            "INSERT INTO member_slice (id, declaration_id, name, start, length, signature) " +
            "VALUES ($id, $declaration, $name, $start, $length, $signature)",
            "$id", "$declaration", "$name", "$start", "$length", "$signature");
        _referenceInsert = new PreparedInsert(
            connection,
            transaction,
            "INSERT INTO reference (declaration_id, member_slice_id, kind, display, fqn, name, assembly_name, signature) " +
            "VALUES ($declaration, $slice, $kind, $display, $fqn, $name, $assembly, $signature)",
            "$declaration", "$slice", "$kind", "$display", "$fqn", "$name", "$assembly", "$signature");
        _usedByInsert = new PreparedInsert(
            connection,
            transaction,
            "INSERT INTO used_by (target_fqn, target_member, target_signature, consumer_assembly, consumer_tfm, " +
            "consumer_type, consumer_member, consumer_signature) " +
            "VALUES ($fqn, $member, $signature, $assembly, $tfm, $type, $consumerMember, $consumerSignature)",
            "$fqn", "$member", "$signature", "$assembly", "$tfm", "$type", "$consumerMember", "$consumerSignature");
    }

    /// <summary>
    /// Записывает сборки, объявления со срезами и ссылками, затем рёбра потребителей.
    /// </summary>
    /// <param name="snapshot">Индекс исходников.</param>
    /// <param name="cancellationToken">Токен отмены вставки.</param>
    public void WriteAll(TypeSourceIndexSnapshot snapshot, CancellationToken cancellationToken)
    {
        var assemblyIds = WriteAssemblies(snapshot);
        WriteDeclarations(snapshot, assemblyIds, cancellationToken);
        WriteUsedBy(snapshot, cancellationToken);
    }

    /// <summary>Освобождает подготовленные команды.</summary>
    public void Dispose()
    {
        _assemblyInsert.Dispose();
        _declarationInsert.Dispose();
        _sliceInsert.Dispose();
        _referenceInsert.Dispose();
        _usedByInsert.Dispose();
    }

    /// <summary>
    /// Нумерует сборки по имени и TFM (ordinal) и записывает их; читатель сортирует результаты по этому номеру.
    /// </summary>
    private Dictionary<SqliteAssemblyKey, long> WriteAssemblies(TypeSourceIndexSnapshot snapshot)
    {
        var assemblyIds = new Dictionary<SqliteAssemblyKey, long>();
        var assemblies = snapshot.Declarations
            .Select(static match => SqliteAssemblyKey.Of(match))
            .Distinct()
            .OrderBy(static assembly => assembly.Name, StringComparer.Ordinal)
            .ThenBy(static assembly => assembly.TargetFramework, StringComparer.Ordinal);

        foreach (var assembly in assemblies)
        {
            var id = assemblyIds.Count + 1L;
            assemblyIds[assembly] = id;
            _assemblyInsert.Execute(id, assembly.Name, assembly.TargetFramework);
        }

        return assemblyIds;
    }

    /// <summary>Записывает объявления в порядке сборок, затем срезы членов и ссылки каждого объявления.</summary>
    private void WriteDeclarations(
        TypeSourceIndexSnapshot snapshot,
        Dictionary<SqliteAssemblyKey, long> assemblyIds,
        CancellationToken cancellationToken)
    {
        // OrderBy стабилен: порядок объявлений одного FQN внутри сборки сохраняется.
        var ordered = snapshot.Declarations.OrderBy(match => assemblyIds[SqliteAssemblyKey.Of(match)]);

        long declarationId = 0;
        long sliceId = 0;
        foreach (var match in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var full = match.Full;
            declarationId++;
            _declarationInsert.Execute(
                declarationId,
                assemblyIds[SqliteAssemblyKey.Of(match)],
                full.Type,
                full.SourceCode ?? string.Empty);

            foreach (var reference in full.References ?? [])
                InsertReference(declarationId, null, reference);

            foreach (var slice in match.Members)
            {
                sliceId++;
                _sliceInsert.Execute(
                    sliceId,
                    declarationId,
                    slice.Name,
                    (long)slice.Start,
                    (long)slice.Length,
                    slice.Signature ?? string.Empty);

                foreach (var reference in slice.References)
                    InsertReference(declarationId, sliceId, reference);
            }
        }
    }

    /// <summary>Записывает рёбра «потребитель → цель» для типа и для каждой перегрузки члена.</summary>
    private void WriteUsedBy(TypeSourceIndexSnapshot snapshot, CancellationToken cancellationToken)
    {
        foreach (var (targetFqn, entry) in snapshot.UsedBy)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var consumer in entry.TypeUsedBy)
                InsertUsedBy(targetFqn, null, string.Empty, consumer);

            foreach (var (memberName, groups) in entry.MemberUsedBy)
            {
                foreach (var group in groups)
                {
                    foreach (var consumer in group.UsedBy)
                        InsertUsedBy(targetFqn, memberName, group.Signature, consumer);
                }
            }
        }
    }

    /// <summary>Вставляет ссылку объявления или среза.</summary>
    private void InsertReference(long declarationId, long? sliceId, TypeSourceReference reference) =>
        _referenceInsert.Execute(
            declarationId,
            (object?)sliceId ?? DBNull.Value,
            (long)(byte)reference.Kind,
            reference.Display ?? string.Empty,
            reference.Fqn ?? string.Empty,
            reference.Name ?? string.Empty,
            reference.AssemblyName ?? string.Empty,
            reference.Signature ?? string.Empty);

    /// <summary>Вставляет одного потребителя цели.</summary>
    private void InsertUsedBy(
        string targetFqn,
        string? targetMember,
        string targetSignature,
        TypeSourceUsedBy consumer) =>
        _usedByInsert.Execute(
            targetFqn,
            (object?)targetMember ?? DBNull.Value,
            targetSignature ?? string.Empty,
            consumer.AssemblyName ?? string.Empty,
            consumer.TargetFramework ?? string.Empty,
            consumer.Type ?? string.Empty,
            consumer.MemberName ?? string.Empty,
            consumer.Signature ?? string.Empty);

    /// <summary>Подготовленная команда вставки: параметры создаются один раз, значения подставляются на каждую строку.</summary>
    private sealed class PreparedInsert : IDisposable
    {
        private readonly SqliteCommand _command;
        private readonly SqliteParameter[] _parameters;

        /// <summary>Создаёт команду в транзакции и подготавливает её.</summary>
        public PreparedInsert(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string sql,
            params string[] parameterNames)
        {
            _command = connection.CreateCommand();
            _command.Transaction = transaction;
            _command.CommandText = sql;
            _parameters = parameterNames.Select(name => _command.Parameters.Add(new SqliteParameter { ParameterName = name }))
                .ToArray();
            _command.Prepare();
        }

        /// <summary>Подставляет значения в параметры и выполняет вставку строки.</summary>
        public void Execute(params ReadOnlySpan<object> values)
        {
            for (var index = 0; index < values.Length; index++)
                _parameters[index].Value = values[index];

            _command.ExecuteNonQuery();
        }

        /// <summary>Освобождает команду.</summary>
        public void Dispose() => _command.Dispose();
    }
}
