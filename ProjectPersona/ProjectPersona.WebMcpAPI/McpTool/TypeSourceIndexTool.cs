using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using ProjectPersona.Application.Services.TypeSourceIndexService;
using ProjectPersona.SourceIndex;
using ProjectPersona.SourceIndex.UsedBy;

namespace ProjectPersona.WebMcpAPI.McpTool;

/// <summary>
/// MCP tool для поиска исходного кода типов по FQN через <see cref="TypeSourceIndexService"/>.
/// </summary>
[McpServerToolType]
public class TypeSourceIndexTool
{
    private readonly TypeSourceIndexService _service;

    /// <summary>
    /// Создаёт tool с доступом к сервису индекса.
    /// </summary>
    /// <param name="service">Сервис поиска по построенному индексу.</param>
    public TypeSourceIndexTool(TypeSourceIndexService service)
    {
        _service = service
                   ?? throw new ArgumentNullException(nameof(service));
    }

    [McpServerTool, Description("Returns source code fragments for the specified FQN, or an Error field if the search failed.")]
    public async Task<string> Find(
        [Description("The full type name without `global::` and without the list of type parameters.")]
        string fqn,
        [Description("The assembly name to narrow the result, or null to return matches from all assemblies.")]
        string? assemblyName,
        [Description("member replacement in catalog format; null or an empty list — the entire type.")]
        string[]? memberNames,
        CancellationToken cancellationToken)
    {
        var response = await _service.FindAsync(fqn, assemblyName, memberNames, cancellationToken);
        return JsonSerializer.Serialize(response);
    }

    [McpServerTool(Name = "usedBy"), Description("Returns consumers that reference the specified FQN. Not a substitute for find. Default limit is 100; use limit <= 0 for the full list only when necessary.")]
    public async Task<string> UsedBy(
        [Description("The full type name without `global::` and without the list of type parameters.")]
        string fqn,
        [Description("The assembly name of the target declaration, or null to return matches from all assemblies.")]
        string? assemblyName,
        [Description("Target member names in catalog format; null or an empty list — consumers of the type.")]
        string[]? memberNames,
        [Description("Maximum number of consumers per list when > 0; <= 0 returns the full list. Default is 100.")]
        int limit = TypeSourceUsedByLimit.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        var response = await _service.FindUsedByAsync(fqn, assemblyName, memberNames, limit, cancellationToken);
        return JsonSerializer.Serialize(response);
    }
}
