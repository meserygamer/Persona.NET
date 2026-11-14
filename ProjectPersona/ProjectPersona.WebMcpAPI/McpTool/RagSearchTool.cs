using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using ProjectPersona.Application.Services.RagSearchService;

namespace ProjectPersona.WebMcpAPI.McpTool;

/// <summary>
/// MCP tool для семантического поиска по объявлениям типов и членов; публикуется только при <c>ragSearchEnabled</c>.
/// </summary>
[McpServerToolType]
public class RagSearchTool
{
    /// <summary> Сервис семантического RAG-поиска. </summary>
    private readonly RagSearchService _ragSearchService;

    /// <summary>
    /// Создаёт tool с доступом к сервису RAG-поиска.
    /// </summary>
    /// <param name="ragSearchService">Сервис семантического RAG-поиска.</param>
    public RagSearchTool(RagSearchService ragSearchService)
    {
        _ragSearchService = ragSearchService
            ?? throw new ArgumentNullException(nameof(ragSearchService));
    }

    /// <summary>
    /// Семантический поиск ближайших фрагментов объявлений; сериализует ответ RAG-поиска в JSON.
    /// </summary>
    /// <param name="query">Текст запроса (не точная подстрока).</param>
    /// <param name="limit">Максимум совпадений; по умолчанию <see cref="RagSearchService.DefaultLimit"/>.</param>
    /// <param name="includeFragmentSource">Включать текст фрагмента в ответ.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>JSON с полями <c>Error</c> и <c>Matches</c> (PascalCase).</returns>
    [McpServerTool(Name = "ragSearch"), Description("Semantic nearest-neighbor search over indexed type and member source fragments. Returns metadata, optional fragment text and Warnings about assemblies with incomplete vectors; use `find` for full declarations. Returns an Error when the vector index is missing or incompatible with the server's model. Not a substring search — use `textSearch` for literals.")]
    public async Task<string> RagSearch(
        [Description("Natural-language or conceptual search text (not exact substring matching).")]
        string query,
        [Description("Maximum nearest fragments when > 0. Default is 10.")]
        int limit = RagSearchService.DefaultLimit,
        [Description("When true, include the indexed source fragment in each match. Default is true.")]
        bool includeFragmentSource = true,
        CancellationToken cancellationToken = default)
    {
        var response = await _ragSearchService.SearchForToolAsync(
            query, limit, includeFragmentSource, cancellationToken);
        return JsonSerializer.Serialize(response);
    }
}
