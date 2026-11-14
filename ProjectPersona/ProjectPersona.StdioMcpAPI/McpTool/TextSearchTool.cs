using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using ProjectPersona.Application.Services.TextSearchService;

namespace ProjectPersona.StdioMcpAPI.McpTool;

/// <summary>
/// MCP tool для полнотекстового поиска по объявлениям типов и членов.
/// </summary>
[McpServerToolType]
public class TextSearchTool
{
    /// <summary> Сервис поиска точной подстроки. </summary>
    private readonly TextSearchService _textSearchService;

    /// <summary>
    /// Создаёт tool с доступом к сервису поиска.
    /// </summary>
    /// <param name="textSearchService">Сервис поиска точной подстроки.</param>
    public TextSearchTool(TextSearchService textSearchService)
    {
        _textSearchService = textSearchService
            ?? throw new ArgumentNullException(nameof(textSearchService));
    }

    /// <summary>
    /// Точный поиск подстроки; сериализует ответ полнотекстового поиска в JSON.
    /// </summary>
    /// <param name="query">Искомая подстрока (не короче 3 символов).</param>
    /// <param name="assemblyName">Имя сборки или <see langword="null"/>.</param>
    /// <param name="ignoreCase">Сравнение без учёта регистра.</param>
    /// <param name="limit">Максимум совпадений.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>JSON с полями <c>Error</c>, <c>Total</c> и <c>Matches</c> (PascalCase).</returns>
    [McpServerTool(Name = "textSearch"), Description("Finds exact substring matches in indexed type and member source text. Returns snippets and metadata; use `find` for full source code. Query must be at least 3 characters. Type-level and member-level hits are separate rows.")]
    public async Task<string> TextSearch(
        [Description("Substring to search for (exact match, at least 3 characters).")]
        string query,
        [Description("Assembly name to narrow results, or null for all assemblies.")]
        string? assemblyName,
        [Description("When true, matches without regard to character case. Default is false (case-sensitive).")]
        bool ignoreCase = false,
        [Description("Maximum matches returned when > 0; <= 0 returns the full list. Default is 50.")]
        int limit = TextSearchService.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        var response = await _textSearchService.SearchAsync(query, assemblyName, limit, ignoreCase, cancellationToken);
        return JsonSerializer.Serialize(response);
    }
}
