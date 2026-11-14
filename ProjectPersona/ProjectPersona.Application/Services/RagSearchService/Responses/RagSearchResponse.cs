namespace ProjectPersona.Application.Services.RagSearchService.Responses;

/// <summary>
/// Ответ MCP tool <c>ragSearch</c>: ближайшие фрагменты или сообщение об ошибке.
/// </summary>
/// <param name="Error">Текст ошибки для агента; <see langword="null"/>, если поиск выполнен.</param>
/// <param name="Matches">Совпадения в порядке близости; <see langword="null"/>, если поиск не выполнен из‑за ошибки.</param>
/// <param name="Warnings">
/// Предупреждения о неполноте результата (например, база обновлена не полностью); <see langword="null"/>, если их нет.
/// </param>
public sealed record RagSearchResponse(
    string? Error,
    IReadOnlyList<RagSearchMatch>? Matches,
    IReadOnlyList<string>? Warnings = null)
{
    /// <summary>Успешный поиск (включая пустой результат).</summary>
    /// <param name="matches">Совпадения.</param>
    /// <param name="warnings">Предупреждения; пустой список превращается в <see langword="null"/>.</param>
    public static RagSearchResponse Ok(IReadOnlyList<RagSearchMatch> matches, IReadOnlyList<string>? warnings = null) =>
        new(null, matches, warnings is { Count: > 0 } ? warnings : null);

    /// <summary>Ошибка поиска, эмбеддинга или сборки индекса.</summary>
    public static RagSearchResponse Fail(string message) =>
        new(message, null);
}

/// <summary>Одно семантическое совпадение RAG-поиска.</summary>
/// <param name="AssemblyName">Имя сборки объявления.</param>
/// <param name="TargetFramework">Короткий TFM компиляции.</param>
/// <param name="Type">FQN типа.</param>
/// <param name="Level"><c>Type</c> или <c>Member</c>.</param>
/// <param name="MemberName">Имя члена; <see langword="null"/> для уровня типа.</param>
/// <param name="ChunkIndex">Индекс окна нарезки (0 для единственного фрагмента).</param>
/// <param name="Source">Текст фрагмента; <see langword="null"/>, если фрагмент не запрашивался.</param>
public sealed record RagSearchMatch(
    string AssemblyName,
    string TargetFramework,
    string Type,
    string Level,
    string? MemberName,
    int ChunkIndex,
    string? Source);
