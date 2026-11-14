namespace ProjectPersona.Application.Services.TextSearchService.Responses;

/// <summary>
/// Ответ MCP tool <c>textSearch</c>: совпадения или сообщение об ошибке.
/// </summary>
/// <param name="Error">Текст ошибки для агента; <see langword="null"/>, если поиск выполнен.</param>
/// <param name="Total">Число совпадений до обрезки <paramref name="Matches"/>; <c>0</c>, если поиск не выполнен.</param>
/// <param name="Matches">Совпадения; <see langword="null"/>, если поиск не выполнен из‑за ошибки.</param>
public sealed record TextSearchResponse(
    string? Error,
    int Total,
    IReadOnlyList<TextSearchMatch>? Matches)
{
    /// <summary>Успешный поиск (включая пустой результат).</summary>
    public static TextSearchResponse Ok(int total, IReadOnlyList<TextSearchMatch> matches) =>
        new(null, total, matches);

    /// <summary>Ошибка поиска или построения индекса.</summary>
    public static TextSearchResponse Fail(string message) =>
        new(message, 0, null);
}

/// <summary>Одно совпадение полнотекстового поиска.</summary>
/// <param name="AssemblyName">Имя сборки объявления.</param>
/// <param name="TargetFramework">Короткий TFM компиляции.</param>
/// <param name="Type">FQN типа.</param>
/// <param name="Level"><c>Type</c> или <c>Member</c>.</param>
/// <param name="MemberName">Имя члена; <see langword="null"/> для уровня типа.</param>
/// <param name="OccurrenceCount">Число вхождений подстроки в документе.</param>
/// <param name="Snippet">Фрагмент текста вокруг первого вхождения.</param>
public sealed record TextSearchMatch(
    string AssemblyName,
    string TargetFramework,
    string Type,
    string Level,
    string? MemberName,
    int OccurrenceCount,
    string Snippet);
