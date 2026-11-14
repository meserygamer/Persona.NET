namespace ProjectPersona.SourceIndex.Sqlite.Searching;

/// <summary>
/// Поиск позиций вхождений подстроки в тексте документа.
/// </summary>
internal static class SubstringOccurrences
{
    /// <summary>
    /// Находит позиции всех неперекрывающихся вхождений подстроки.
    /// </summary>
    /// <param name="text">Текст, в котором ищем.</param>
    /// <param name="query">Искомая подстрока.</param>
    /// <param name="comparison">Правило сравнения.</param>
    /// <returns>Позиции по возрастанию; пустой список, если вхождений нет или подстрока пустая.</returns>
    public static List<int> FindAll(string text, string query, StringComparison comparison)
    {
        var indexes = new List<int>();
        // Пустая подстрока находится на каждой позиции и зациклила бы поиск: вхождений не считаем.
        if (query.Length == 0)
            return indexes;

        for (var start = 0; ;)
        {
            var index = text.IndexOf(query, start, comparison);
            if (index < 0)
                break;

            indexes.Add(index);
            start = index + query.Length;
        }

        return indexes;
    }
}
