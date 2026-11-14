using ProjectPersona.SourceIndex.Contracts;
using ProjectPersona.SourceIndex.Find;
using ProjectPersona.SourceIndex.Storage;
using ProjectPersona.SourceIndex.UsedBy;

namespace ProjectPersona.SourceIndex.Projection;

/// <summary>
/// Превращает индексированные объявления и рёбра потребителей в ответы <see cref="ITypeSourceLookup"/>.
/// Общая часть для всех реализаций хранилища: им остаётся только прочитать данные.
/// </summary>
public static class TypeSourceProjection
{
    /// <summary>
    /// Формирует ответ <see cref="ITypeSourceLookup.Find"/> по найденным объявлениям FQN.
    /// </summary>
    /// <param name="source">Объявления одного FQN (partial, TFM, сборки).</param>
    /// <param name="memberNames">Имена членов в формате каталога; <see langword="null"/> или пустой список — весь тип.</param>
    /// <returns>Совпадения; пустой список, если ни один член не совпал.</returns>
    public static IReadOnlyList<TypeSourceMatch> ProjectMatches(
        IReadOnlyList<TypeSourceIndexedMatch> source,
        IReadOnlyList<string>? memberNames)
    {
        var filter = TypeSourceMatchFilter.NormalizeMemberNames(memberNames);
        if (filter is null)
            return source.Select(static entry => entry.Full).ToList();

        var result = new List<TypeSourceMatch>(source.Count);
        foreach (var entry in source)
        {
            var match = TypeSourceMatchFilter.Apply(entry, filter);
            if (match is not null)
                result.Add(match);
        }

        return result;
    }

    /// <summary>
    /// Формирует ответ <see cref="ITypeSourceLookup.FindUsedBy"/> для найденных объявлений цели.
    /// </summary>
    /// <param name="targets">Объявления целевого FQN (partial, TFM).</param>
    /// <param name="usedByEntry">Потребители цели или <see langword="null"/>, если рёбра не индексировались.</param>
    /// <param name="memberNames">Имена членов цели или <see langword="null"/> — тип целиком.</param>
    /// <param name="limit">Потолок элементов в каждом списке <c>UsedBy</c>; при <c>&lt;= 0</c> без обрезки.</param>
    /// <returns>Совпадения с потребителями.</returns>
    public static IReadOnlyList<TypeSourceUsedByMatch> ProjectUsedBy(
        IReadOnlyList<TypeSourceIndexedMatch> targets,
        UsedByFqnEntry? usedByEntry,
        IReadOnlyList<string>? memberNames,
        int limit) =>
        TypeSourceUsedByProjector.Project(targets, usedByEntry, memberNames, limit);
}
