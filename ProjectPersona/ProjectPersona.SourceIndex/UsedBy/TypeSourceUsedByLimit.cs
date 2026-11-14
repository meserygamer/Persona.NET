using ProjectPersona.SourceIndex.Contracts;

namespace ProjectPersona.SourceIndex.UsedBy;

/// <summary>
/// Нормализация и применение параметра <c>limit</c> к спискам потребителей.
/// </summary>
public static class TypeSourceUsedByLimit
{
    /// <summary>
    /// Лимит по умолчанию для <see cref="ITypeSourceLookup.FindUsedBy"/> и MCP tool <c>usedBy</c>.
    /// </summary>
    public const int DefaultLimit = 100;

    /// <summary>
    /// Возвращает обрезанный список и полный размер до обрезки.
    /// </summary>
    /// <param name="source">Полный отсортированный список потребителей.</param>
    /// <param name="limit">Строгий потолок при <c>&gt; 0</c>; при <c>&lt;= 0</c> — полный список без обрезки.</param>
    /// <returns>Пара: элементы после <paramref name="limit"/> и полный размер до обрезки.</returns>
    public static (IReadOnlyList<TypeSourceUsedBy> Items, int Total) Apply(
        IReadOnlyList<TypeSourceUsedBy> source,
        int limit)
    {
        var total = source.Count;
        if (limit <= 0)
            return (source, total);

        if (source.Count <= limit)
            return (source, total);

        var slice = new TypeSourceUsedBy[limit];
        for (var index = 0; index < limit; index++)
            slice[index] = source[index];

        return (slice, total);
    }
}
