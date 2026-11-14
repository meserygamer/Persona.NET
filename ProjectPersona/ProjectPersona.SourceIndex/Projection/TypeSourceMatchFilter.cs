using ProjectPersona.SourceIndex.Find;
using ProjectPersona.SourceIndex.Naming;
using ProjectPersona.SourceIndex.Storage;

namespace ProjectPersona.SourceIndex.Projection;

/// <summary>
/// Собирает <see cref="TypeSourceMatch"/> по выбранным членам из индексированного совпадения.
/// </summary>
internal static class TypeSourceMatchFilter
{
    /// <summary>
    /// Нормализует список имён членов: trim, без пустых; <see langword="null"/>, если фильтр не задан.
    /// </summary>
    /// <param name="memberNames">Имена членов из запроса или <see langword="null"/>.</param>
    /// <returns>Множество имён для сопоставления или <see langword="null"/>, если вернуть полный тип.</returns>
    public static HashSet<string>? NormalizeMemberNames(IReadOnlyList<string>? memberNames)
    {
        if (memberNames is null || memberNames.Count == 0)
            return null;

        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in memberNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            set.Add(name.Trim());
        }

        return set.Count == 0 ? null : set;
    }

    /// <summary>
    /// Оставляет в совпадении только выбранные члены или возвращает полное совпадение без фильтра.
    /// </summary>
    /// <param name="entry">Индексированное совпадение.</param>
    /// <param name="memberNames">Нормализованное множество имён или <see langword="null"/>.</param>
    /// <returns>Срез по членам или <see langword="null"/>, если ни один член не совпал.</returns>
    public static TypeSourceMatch? Apply(TypeSourceIndexedMatch entry, HashSet<string>? memberNames)
    {
        if (memberNames is null)
            return entry.Full;

        var full = entry.Full;
        var fullSourceCode = full.SourceCode!;
        var matchedMembers = new List<TypeSourceMemberMatch>();

        foreach (var slice in entry.Members)
        {
            if (!memberNames.Any(name =>
                    TypeSourceMemberCatalogName.MatchesDeclaredMemberQuery(name, slice, entry.Full.Type)))
            {
                continue;
            }

            matchedMembers.Add(new TypeSourceMemberMatch(
                slice.Name,
                fullSourceCode.Substring(slice.Start, slice.Length),
                slice.References));
        }

        if (matchedMembers.Count == 0)
            return null;

        return new TypeSourceMatch(
            full.AssemblyName,
            full.Type,
            full.TargetFramework,
            SourceCode: null,
            References: null,
            Members: matchedMembers);
    }
}
