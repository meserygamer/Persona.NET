namespace ProjectPersona.SourceIndex.Storage;

/// <summary>
/// Содержимое индекса исходников в виде, который понимает любое хранилище: объявления со срезами членов и рёбра потребителей.
/// </summary>
/// <param name="Declarations">Все объявления типов по всем сборкам и TFM.</param>
/// <param name="UsedBy">Потребители по FQN цели; общие для всех сборок, где объявлен FQN.</param>
public sealed record TypeSourceIndexSnapshot(
    IReadOnlyCollection<TypeSourceIndexedMatch> Declarations,
    IReadOnlyDictionary<string, UsedByFqnEntry> UsedBy);
