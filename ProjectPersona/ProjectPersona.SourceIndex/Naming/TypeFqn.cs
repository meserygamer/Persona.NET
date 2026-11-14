namespace ProjectPersona.SourceIndex.Naming;

/// <summary>
/// Собирает и нормализует FQN типа в формате type-catalog: <c>Namespace.Type</c> и вложенные <c>Namespace.Outer.Inner</c>.
/// </summary>
public static class TypeFqn
{
    /// <summary>
    /// Собирает FQN из пространства имён и пути имён типа (включая вложенные сегменты).
    /// </summary>
    /// <param name="namespaceName">Пространство имён или пустая строка для глобального пространства имён.</param>
    /// <param name="typePath">Путь имён типа, например <c>Outer.Inner</c>.</param>
    /// <returns>FQN без <c>global::</c> и без списка type parameters.</returns>
    public static string Build(string namespaceName, string typePath) =>
        string.IsNullOrEmpty(namespaceName) ? typePath : $"{namespaceName}.{typePath}";

    /// <summary>
    /// Нормализует FQN запроса: обрезка пробелов и срез префикса <c>global::</c>.
    /// </summary>
    /// <param name="fqn">FQN из запроса.</param>
    /// <returns>Нормализованный FQN или <see langword="null"/>, если строка пустая.</returns>
    public static string? NormalizeQuery(string? fqn)
    {
        if (string.IsNullOrWhiteSpace(fqn))
            return null;

        var trimmed = fqn.Trim();
        const string globalPrefix = "global::";
        if (trimmed.StartsWith(globalPrefix, StringComparison.Ordinal))
            trimmed = trimmed[globalPrefix.Length..];

        return trimmed.Length == 0 ? null : trimmed;
    }
}
