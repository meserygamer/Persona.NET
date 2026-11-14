using ProjectPersona.SourceIndex;
using ProjectPersona.SourceIndex.UsedBy;

namespace ProjectPersona.Application.Services.TypeSourceIndexService.Responses;

/// <summary>
/// Ответ MCP tool <c>usedBy</c>: потребители цели или сообщение об ошибке поиска.
/// </summary>
/// <param name="Error">Текст ошибки для агента; <see langword="null"/>, если поиск выполнен.</param>
/// <param name="Matches">Совпадения по FQN; <see langword="null"/>, если поиск не выполнен из‑за ошибки.</param>
public sealed record UsedByResponse(
    string? Error,
    IReadOnlyList<TypeSourceUsedByMatch>? Matches)
{
    /// <summary>
    /// Успешный поиск (включая пустой результат — цель не найдена).
    /// </summary>
    /// <param name="matches">Совпадения; пустой список — FQN или члены не найдены в индексе.</param>
    public static UsedByResponse Ok(IReadOnlyList<TypeSourceUsedByMatch> matches) => new(null, matches);

    /// <summary>
    /// Ошибка поиска или загрузки индекса.
    /// </summary>
    /// <param name="message">Сообщение для агента.</param>
    public static UsedByResponse Fail(string message) => new(message, null);
}
