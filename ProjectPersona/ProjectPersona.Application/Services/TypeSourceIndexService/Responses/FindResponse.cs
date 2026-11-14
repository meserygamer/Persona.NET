using ProjectPersona.SourceIndex;
using ProjectPersona.SourceIndex.Find;

namespace ProjectPersona.Application.Services.TypeSourceIndexService.Responses;

/// <summary>
/// Ответ MCP tool <c>Find</c>: совпадения или сообщение об ошибке поиска.
/// </summary>
/// <param name="Error">Текст ошибки для агента; <see langword="null"/>, если поиск выполнен.</param>
/// <param name="Matches">Совпадения по FQN; <see langword="null"/>, если поиск не выполнен из‑за ошибки.</param>
public sealed record FindResponse(
    string? Error,
    IReadOnlyList<TypeSourceMatch>? Matches)
{
    /// <summary>
    /// Успешный поиск (включая пустой результат — тип не найден).
    /// </summary>
    /// <param name="matches">Совпадения; пустой список — FQN или члены не найдены в индексе.</param>
    public static FindResponse Ok(IReadOnlyList<TypeSourceMatch> matches) => new(null, matches);

    /// <summary>
    /// Ошибка поиска или построения индекса.
    /// </summary>
    /// <param name="message">Сообщение для агента.</param>
    public static FindResponse Fail(string message) => new(message, null);
}
