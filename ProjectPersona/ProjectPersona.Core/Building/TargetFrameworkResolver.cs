using Microsoft.CodeAnalysis;

namespace ProjectPersona.Core.Building;

/// <summary>
/// Идентичность компиляции сборки: имя и целевой фреймворк, если он известен.
/// </summary>
/// <param name="Name">Имя сборки.</param>
/// <param name="TargetFramework">Короткий TFM (например <c>net472</c>) или пустая строка, если таргет неизвестен.</param>
internal readonly record struct AssemblyIdentity(string Name, string TargetFramework)
{
    /// <summary>
    /// Создаёт идентичность, нормализуя отсутствующий TFM к пустой строке.
    /// </summary>
    /// <param name="name">Имя сборки.</param>
    /// <param name="targetFramework">Короткий TFM или <see langword="null"/>, если таргет неизвестен.</param>
    /// <returns>Идентичность компиляции сборки.</returns>
    public static AssemblyIdentity Create(string name, string? targetFramework) =>
        new(name, string.IsNullOrWhiteSpace(targetFramework) ? string.Empty : targetFramework.Trim());

    /// <summary>
    /// Целевой фреймворк или <see langword="null"/>, если он неизвестен.
    /// </summary>
    public string? TargetFrameworkOrNull =>
        string.IsNullOrEmpty(TargetFramework) ? null : TargetFramework;
}

/// <summary>
/// Извлекает короткий TFM из проекта Roslyn, загруженного через MSBuild Workspace.
/// </summary>
internal static class TargetFrameworkResolver
{
    /// <summary>
    /// Возвращает короткий TFM проекта или <see langword="null"/>, если определить его не удалось.
    /// </summary>
    /// <param name="project">Проект, для которого определяется целевой фреймворк.</param>
    /// <returns>Короткий TFM вроде <c>net472</c> или <see langword="null"/>.</returns>
    public static string? Get(Project project)
    {
        if (TryFromProjectName(project.Name, out var tfm))
            return tfm;

        if (TryFromOutputPath(project.OutputFilePath, out tfm) ||
            TryFromOutputPath(project.OutputRefFilePath, out tfm))
            return tfm;

        if (TryFromAnalyzerOptions(project, out tfm))
            return tfm;

        return null;
    }

    private static bool TryFromProjectName(string name, out string? tfm)
    {
        tfm = null;
        var start = name.LastIndexOf(" (", StringComparison.Ordinal);
        if (start < 0 || !name.EndsWith(')'))
            return false;

        var candidate = name.AsSpan(start + 2, name.Length - start - 3).Trim();
        if (!IsTargetFrameworkMoniker(candidate))
            return false;

        tfm = candidate.ToString();
        return true;
    }

    private static bool TryFromOutputPath(string? path, out string? tfm)
    {
        tfm = null;
        if (string.IsNullOrEmpty(path))
            return false;

        var folder = Path.GetFileName(Path.GetDirectoryName(path));
        if (string.IsNullOrEmpty(folder) || !IsTargetFrameworkMoniker(folder))
            return false;

        tfm = folder;
        return true;
    }

    private static bool TryFromAnalyzerOptions(Project project, out string? tfm)
    {
        tfm = null;
        var options = project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GlobalOptions;
        if (!options.TryGetValue("build_property.TargetFramework", out var value) ||
            string.IsNullOrWhiteSpace(value))
            return false;

        var candidate = value.Trim();
        if (!IsTargetFrameworkMoniker(candidate))
            return false;

        tfm = candidate;
        return true;
    }

    private static bool IsTargetFrameworkMoniker(ReadOnlySpan<char> value)
    {
        if (!value.StartsWith("net", StringComparison.OrdinalIgnoreCase) || value.Length < 4)
            return false;

        foreach (var ch in value)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch is '.' or '-' or '+')
                continue;

            return false;
        }

        return true;
    }
}
