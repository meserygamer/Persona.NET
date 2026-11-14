using Microsoft.Extensions.Logging;
using ProjectPersona.Application.Services.TextSearchService.Responses;
using ProjectPersona.SourceIndex.Contracts;
using ProjectPersona.SourceIndex.Models;

namespace ProjectPersona.Application.Services.TextSearchService;

/// <summary>
/// Полнотекстовый поиск по объявлениям из type-source-index для MCP tool.
/// </summary>
public sealed class TextSearchService
{
    /// <summary>Минимальная длина запроса (длина триграммы FTS5).</summary>
    public const int MinQueryLength = 3;

    /// <summary>Лимит совпадений по умолчанию.</summary>
    public const int DefaultLimit = 50;

    private const int SnippetMaxLength = 200;

    private readonly ITypeSourceTextSearch _textSearch;
    private readonly ILogger<TextSearchService> _logger;

    /// <summary>
    /// Создаёт сервис полнотекстового поиска.
    /// </summary>
    /// <param name="textSearch">Полнотекстовый поиск по хранилищу индекса.</param>
    /// <param name="logger">Логгер ошибок поиска.</param>
    public TextSearchService(ITypeSourceTextSearch textSearch, ILogger<TextSearchService> logger)
    {
        _textSearch = textSearch
            ?? throw new ArgumentNullException(nameof(textSearch));
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Ищет точную подстроку в объявлениях типов и членов.
    /// </summary>
    /// <param name="query">Искомая подстрока.</param>
    /// <param name="assemblyName">Имя сборки для сужения или <see langword="null"/> — все сборки.</param>
    /// <param name="limit">Потолок при <c>&gt; 0</c>; при <c>&lt;= 0</c> — без обрезки.</param>
    /// <param name="ignoreCase"><see langword="true"/> — сравнение без учёта регистра.</param>
    /// <param name="cancellationToken">Токен отмены поиска.</param>
    public async ValueTask<TextSearchResponse> SearchAsync(
        string query,
        string? assemblyName = null,
        int limit = DefaultLimit,
        bool ignoreCase = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogWarning(
                "textSearch failed: empty query. Operation=textSearch, AssemblyName={AssemblyName}, IgnoreCase={IgnoreCase}, Limit={Limit}",
                assemblyName, ignoreCase, limit);
            return TextSearchResponse.Fail("The query parameter is mandatory: specify a non-empty substring to search for.");
        }

        if (query.Length < MinQueryLength)
        {
            _logger.LogWarning(
                "textSearch failed: query too short. Operation=textSearch, QueryLength={QueryLength}, AssemblyName={AssemblyName}",
                query.Length, assemblyName);
            return TextSearchResponse.Fail(
                $"The query must be at least {MinQueryLength} characters long (trigram length of the search index).");
        }

        try
        {
            var raw = await Task
                .Run(() => _textSearch.Search(query, assemblyName, ignoreCase, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
            var filtered = Project(raw, query);
            var total = filtered.Count;

            if (limit > 0 && filtered.Count > limit)
                filtered = filtered.Take(limit).ToList();

            return TextSearchResponse.Ok(total, filtered);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "textSearch cancelled. Operation=textSearch, AssemblyName={AssemblyName}, IgnoreCase={IgnoreCase}, Limit={Limit}",
                assemblyName, ignoreCase, limit);
            return TextSearchResponse.Fail(
                "The request was cancelled or timed out. Retry the `textSearch` operation.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "textSearch failed. Operation=textSearch, AssemblyName={AssemblyName}, IgnoreCase={IgnoreCase}, Limit={Limit}",
                assemblyName, ignoreCase, limit);
            return TextSearchResponse.Fail(FormatSearchError(ex));
        }
    }

    private static List<TextSearchMatch> Project(IReadOnlyList<TypeSourceTextMatch> results, string query)
    {
        var projected = new List<TextSearchMatch>(results.Count);

        foreach (var result in results)
        {
            projected.Add(new TextSearchMatch(
                result.AssemblyName,
                result.TargetFramework,
                result.Type,
                result.MemberName is null ? "Type" : "Member",
                result.MemberName,
                result.Indexes.Count,
                BuildSnippet(result.Text, result.Indexes[0], query.Length)));
        }

        projected.Sort(static (left, right) =>
        {
            var cmp = StringComparer.Ordinal.Compare(left.AssemblyName, right.AssemblyName);
            if (cmp != 0)
                return cmp;

            cmp = StringComparer.Ordinal.Compare(left.TargetFramework, right.TargetFramework);
            if (cmp != 0)
                return cmp;

            cmp = StringComparer.Ordinal.Compare(left.Type, right.Type);
            if (cmp != 0)
                return cmp;

            cmp = LevelOrder(left.Level).CompareTo(LevelOrder(right.Level));
            if (cmp != 0)
                return cmp;

            return StringComparer.Ordinal.Compare(left.MemberName, right.MemberName);
        });

        return projected;
    }

    private static int LevelOrder(string level) =>
        string.Equals(level, "Type", StringComparison.Ordinal) ? 0 : 1;

    private static string BuildSnippet(string text, int matchIndex, int matchLength)
    {
        const int context = 60;
        var start = Math.Max(0, matchIndex - context);
        var end = Math.Min(text.Length, matchIndex + matchLength + context);
        var snippet = text[start..end];

        if (start > 0)
            snippet = "..." + snippet;

        if (end < text.Length)
            snippet += "...";

        if (snippet.Length <= SnippetMaxLength)
            return snippet;

        return snippet[..SnippetMaxLength] + "...";
    }

    private static string FormatSearchError(Exception exception)
    {
        var message = exception is AggregateException aggregate
            ? aggregate.GetBaseException().Message
            : exception.Message;

        if (exception is InvalidOperationException)
        {
            if (message.Contains("--generate-type-source-index", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("type source index", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("typeSourceIndex", StringComparison.OrdinalIgnoreCase))
            {
                return $"Failed to load the source index: {message}";
            }
        }

        return $"Search error: {message}";
    }
}
