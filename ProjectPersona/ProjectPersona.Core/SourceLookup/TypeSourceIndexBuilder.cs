using ProjectPersona.SourceIndex;
using ProjectPersona.SourceIndex.Storage;

namespace ProjectPersona.Core.SourceLookup;

/// <summary>
/// Накапливает совпадения FQN при построении индекса исходников.
/// </summary>
internal sealed class TypeSourceIndexBuilder
{
    private readonly Dictionary<string, FqnBucket> _buckets = new(StringComparer.Ordinal);

    /// <summary>
    /// Регистрирует индексированное совпадение FQN.
    /// </summary>
    /// <param name="match">Найденное объявление типа с срезами членов.</param>
    public void Add(TypeSourceIndexedMatch match)
    {
        if (!_buckets.TryGetValue(match.Full.Type, out var bucket))
        {
            bucket = new FqnBucket();
            _buckets[match.Full.Type] = bucket;
        }

        bucket.Add(match);
    }

    /// <summary>
    /// Формирует неизменяемый индекс из накопленных совпадений.
    /// </summary>
    /// <returns>Готовый к поиску индекс исходников.</returns>
    public TypeSourceIndex Build()
    {
        var usedByEntries = TypeSourceUsedByInverter.Build(_buckets);
        return new TypeSourceIndex(_buckets, usedByEntries);
    }
}

/// <summary>
/// Группа совпадений одного FQN, разложенная по сборкам для быстрого поиска.
/// </summary>
internal sealed class FqnBucket
{
    private readonly List<TypeSourceIndexedMatch> _all = [];
    private readonly Dictionary<string, List<TypeSourceIndexedMatch>> _byAssembly = new(StringComparer.Ordinal);

    /// <summary>
    /// Добавляет совпадение в общий список и в группу по имени сборки.
    /// </summary>
    /// <param name="match">Индексированное совпадение для добавления.</param>
    public void Add(TypeSourceIndexedMatch match)
    {
        _all.Add(match);

        if (!_byAssembly.TryGetValue(match.Full.AssemblyName, out var assemblyMatches))
        {
            assemblyMatches = [];
            _byAssembly[match.Full.AssemblyName] = assemblyMatches;
        }

        assemblyMatches.Add(match);
    }

    /// <summary>
    /// Перечисляет все накопленные совпадения FQN.
    /// </summary>
    internal IEnumerable<TypeSourceIndexedMatch> EnumerateMatches() => _all;

    /// <summary>
    /// Формирует неизменяемую запись индекса для одного FQN.
    /// </summary>
    /// <returns>Запись с полным списком и словарём по сборкам.</returns>
    public FqnIndexEntry ToEntry() =>
        new(
            _all,
            _byAssembly.ToDictionary(
                static pair => pair.Key,
                static pair => (IReadOnlyList<TypeSourceIndexedMatch>)pair.Value,
                StringComparer.Ordinal));
}

/// <summary>
/// Неизменяемая запись индекса для одного FQN.
/// </summary>
/// <param name="All">Все совпадения FQN по всем сборкам.</param>
/// <param name="ByAssembly">Совпадения FQN, сгруппированные по имени сборки.</param>
internal sealed record FqnIndexEntry(
    IReadOnlyList<TypeSourceIndexedMatch> All,
    IReadOnlyDictionary<string, IReadOnlyList<TypeSourceIndexedMatch>> ByAssembly);
