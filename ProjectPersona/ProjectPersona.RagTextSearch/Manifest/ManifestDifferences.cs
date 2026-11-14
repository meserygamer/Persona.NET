namespace ProjectPersona.RagTextSearch.Manifest;

/// <summary> Форматирование различий полей манифеста для сообщений пользователю. </summary>
internal static class ManifestDifferences
{
    /// <summary> Добавляет описание различия, если значения не равны. </summary>
    /// <param name="differences">Список различий.</param>
    /// <param name="name">Имя поля для сообщения.</param>
    /// <param name="expected">Значение из манифеста.</param>
    /// <param name="actual">Текущее значение.</param>
    public static void Add<T>(List<string> differences, string name, T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            differences.Add($"{name}: index has {Format(expected)}, current is {Format(actual)}");
    }

    private static string Format<T>(T value) => value is null ? "<none>" : $"'{value}'";
}
