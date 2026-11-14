# ProjectPersona.StdioMcpAPI

Parent: [project index](../../docs/project-structure.md)

Console exe, `net10.0`. stdio MCP server. Dependencies: Application, SourceIndex.Sqlite (no Roslyn, no Core); `ModelContextProtocol` 2.2.0, Generic Host, Serilog.

```
ProjectPersona.StdioMcpAPI/
├── Startup.cs                     Generic Host, DI, AddMcpServer; RAG registrations and RagSearchTool only when ragSearchEnabled
├── AppLogging.cs                  Serilog: file in BaseDirectory/logs, stderr only Error+
├── appsettings.json               typeSourceIndexPath, ragSearchEnabled, llama, lanceDb
└── McpTool/
    ├── TypeSourceIndexTool.cs     find and usedBy → JSON via TypeSourceIndexService
    ├── TextSearchTool.cs          textSearch → JSON via TextSearchService
    └── RagSearchTool.cs           ragSearch → JSON via RagSearchService
```

## Behaviour

- Same tool set and service registration as [WebMcpAPI](../ProjectPersona.WebMcpAPI/STRUCTURE.md). The tool classes are duplicated in both projects and differ only by namespace — **change both when you touch tool attributes, descriptions or parameters**.
- Logs: Serilog writes `logs/log-<date>.txt` next to the exe; stderr gets only `Error+` (stdout is reserved for JSON-RPC).
- Index file: `typeSourceIndexPath` (path to the `.db`). `TypeSourceIndexStartupValidator` opens it before startup; a missing, corrupt or wrong-version file stops the process.
- Application errors are logged by the service that produced them: `TypeSourceIndexService` (find/usedBy), `TextSearchService`, `RagSearchService`, and `RagIndexStateHolder` for the vector-store state.
- `textSearch` uses the FTS5 table inside the same `.db`; there is no separate index to build.
- The LanceDB vector store is built only by [CliTools](../ProjectPersona.CliTools/STRUCTURE.md); directory `lanceDb.pathToLanceDb` (empty → `BaseDirectory/lance-db`). Roslyn and MSBuild Locator are never called at startup.
- The `type-source-index/` directory (the `.db` with full sources) is not stored in git; CliTools creates it.

## MCP tools

| Tool | Parameters | Behaviour |
|---|---|---|
| `find` | `fqn`, `assemblyName?`, `memberNames?` | Returns `TypeSourceMatch` (partial types give several elements; without `assemblyName`, all assemblies). `memberNames` null/empty → whole-type `SourceCode` / `References`, otherwise `Members` with `Name` / `SourceCode` / `References` per found member |
| `usedBy` | `fqn`, `assemblyName?`, `memberNames?`, `limit?` | Consumers of the target (`TypeSourceUsedByMatch`); `limit` default 100, `<= 0` = full list |
| `textSearch` | `query`, `assemblyName?`, `ignoreCase?`, `limit?` | Exact substring in type and member text (`TextSearchMatch` with a snippet); `query` at least 3 characters; `limit` default 50, `<= 0` = full list. Full source is still `find` |
| `ragSearch` | `query`, `limit?`, `includeFragmentSource?` | Semantic nearest-fragment search (`RagSearchMatch`: `Level`, `MemberName`, `ChunkIndex`, optional `Source`, plus `Warnings`); `limit` default 10, minimum 1. Published only when `ragSearchEnabled: true` (default `true`); when `false` the model, LanceDB and warmup are not registered |

## Related

- Services behind the tools and `ragSearch` states: [Application](../ProjectPersona.Application/STRUCTURE.md)
- Consumer-side rules for these tools: [AgentInteraction.Claude](../../AgentInteraction.Claude/STRUCTURE.md), [AgentInteraction.Cursor](../../AgentInteraction.Cursor/STRUCTURE.md)
