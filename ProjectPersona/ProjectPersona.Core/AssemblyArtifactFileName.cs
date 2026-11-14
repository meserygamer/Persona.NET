using System.Text;

namespace ProjectPersona.Core;

/// <summary>
/// Имена файлов артефактов одной компиляции сборки: <c>{AssemblyName}[.{tfm}].ext</c>.
/// </summary>
internal static class AssemblyArtifactFileName
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    /// <summary>
    /// Возвращает имена сборок, у которых в выборке больше одной компиляции (несколько TFM).
    /// </summary>
    /// <param name="assemblyNames">Имена сборок в порядке обхода компиляций.</param>
    /// <returns>Множество имён multi-target сборок.</returns>
    public static HashSet<string> GetMultiTargetAssemblyNames(IEnumerable<string> assemblyNames)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in assemblyNames)
        {
            if (!seen.Add(name))
                names.Add(name);
        }

        return names;
    }

    /// <summary>
    /// Выделяет уникальное имя файла для компиляции сборки.
    /// </summary>
    /// <param name="assemblyName">Имя сборки.</param>
    /// <param name="targetFramework">Короткий TFM или пустая строка.</param>
    /// <param name="includeTargetFramework">Добавлять TFM в имя, если он известен.</param>
    /// <param name="extension">Расширение с точкой или без (<c>.md</c>, <c>fb</c>).</param>
    /// <param name="usedFileNames">Уже занятые имена файлов (сравнение без учёта регистра).</param>
    /// <returns>Имя файла без пути.</returns>
    public static string Allocate(
        string assemblyName,
        string? targetFramework,
        bool includeTargetFramework,
        string extension,
        HashSet<string> usedFileNames)
    {
        var baseName = Sanitize(assemblyName);
        if (includeTargetFramework && !string.IsNullOrEmpty(targetFramework))
            baseName = $"{baseName}.{Sanitize(targetFramework)}";

        var ext = NormalizeExtension(extension);
        var fileName = baseName + ext;
        if (usedFileNames.Add(fileName))
            return fileName;

        for (var index = 2; ; index++)
        {
            fileName = $"{baseName}_{index}{ext}";
            if (usedFileNames.Add(fileName))
                return fileName;
        }
    }

    /// <summary>
    /// Убирает недопустимые для имени файла символы.
    /// </summary>
    /// <param name="value">Имя сборки или TFM.</param>
    /// <returns>Безопасный фрагмент имени файла.</returns>
    public static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "assembly";

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
            builder.Append(Array.IndexOf(InvalidFileNameChars, ch) >= 0 ? '_' : ch);

        var sanitized = builder.ToString().Trim('.', ' ');
        return string.IsNullOrEmpty(sanitized) ? "assembly" : sanitized;
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        return extension[0] == '.' ? extension : "." + extension;
    }
}
