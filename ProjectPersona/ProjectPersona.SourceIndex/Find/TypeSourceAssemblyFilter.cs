namespace ProjectPersona.SourceIndex.Find;

/// <summary>
/// Нормализация фильтров по сборкам и TFM, общая для всех реализаций <see cref="Contracts.ITypeSourceLookup"/>.
/// </summary>
public static class TypeSourceAssemblyFilter
{
    /// <summary>Приводит пары сборки и TFM к виду для сравнения: обрезает пробелы, пустой TFM заменяет на <see langword="null"/>, убирает дубликаты.</summary>
    /// <param name="assemblies">Пары сборки и TFM; TFM <see langword="null"/> или пустой — любой TFM сборки.</param>
    /// <returns>Уникальные пары в порядке первого появления.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assemblies"/> равен <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Имя сборки пустое.</exception>
    public static List<(string AssemblyName, string? TargetFramework)> Normalize(
        IEnumerable<(string AssemblyName, string? TargetFramework)> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var seen = new HashSet<(string AssemblyName, string? TargetFramework)>();
        var filters = new List<(string AssemblyName, string? TargetFramework)>();
        foreach (var (assemblyName, targetFramework) in assemblies)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);

            var tfm = targetFramework?.Trim();
            var filter = (assemblyName.Trim(), string.IsNullOrEmpty(tfm) ? null : tfm);
            if (seen.Add(filter))
                filters.Add(filter);
        }

        return filters;
    }
}
