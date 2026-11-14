# Persona.NET

English | [Русский](README.ru.md)

Give AI coding agents accurate knowledge of a C# codebase **without letting them read all of its sources**.

Persona.NET analyzes a .NET solution with Roslyn and produces:

- a **type catalog** — compact markdown with signatures, FQNs and XML docs, one file per assembly;
- a **source index** — a single SQLite file with full declaration sources, outgoing references, consumers (`usedBy`) and full-text search;
- an optional **vector index** (RAG) in LanceDB for semantic search, embedded locally with llama.cpp.

An **MCP server** (stdio or HTTP) then exposes the index to agents through four tools:

| Tool | Purpose |
|---|---|
| `find` | Source code and outgoing references of a type or specific members |
| `usedBy` | Consumers of a type or member |
| `textSearch` | Exact substring search in type and member sources (SQLite FTS5) |
| `ragSearch` | Semantic search over declaration fragments (optional) |

Agents read signatures from the catalog and pull method bodies or consumers on demand, so the context stays small.

## Requirements

- .NET 10 SDK (or newer) — to build Persona.NET. Running the MCP servers only needs the .NET 10 Runtime.
- **A .NET SDK that can build the solution you want to analyze** — needed only for generation (`CliTools`). The MCP servers do not use Roslyn or MSBuild and just read the generated index, so they do not need this SDK. The generator opens your `.sln` with Roslyn's `MSBuildWorkspace`, and `MSBuildLocator` loads MSBuild from an SDK installed on the machine. That SDK must support your solution's target frameworks and language version (check its `global.json`, if any). If it is too old, projects fail to load or types are reported as unresolved.
- The analyzed solution must be **restored** (`dotnet restore`) so package references resolve; for .NET Framework projects, the targeting packs or reference assemblies must be available.
- For RAG only: Windows x64, a GGUF embedding model (for example `embeddinggemma-2-Q8_0.gguf`) and a [llama.cpp](https://github.com/ggml-org/llama.cpp) **b12359** `llama.dll` build. Other builds may be ABI-incompatible.

### Embedding model

RAG was tested with `embeddinggemma-2-Q8_0.gguf` from [Unsloth](https://huggingface.co/unsloth/embeddinggemma-2-GGUF). Other GGUF embedding models can be used too, but keep in mind:

- set `--vector-dimension` (CLI) and `lanceDb.vectorDimension` (server) to the model's embedding size;
- adjust `documentQueryTemplate` and `searchQueryTemplate` to the prompt format your model expects (the defaults are tuned for EmbeddingGemma);
- after changing the model, rebuild the vector index with `vectorize-type-source-index` — the manifest fingerprint of the old index no longer matches, and `ragSearch` will report an error until you do;
- check the model's own license (EmbeddingGemma is under the Gemma Terms of Use, see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)).

## Quick start

```
git clone https://github.com/meserygamer/Persona.NET.git
cd Persona.NET
dotnet build ProjectPersona/ProjectPersona.sln
```

### 1. Generate the catalog and the source index

```
dotnet run --project ProjectPersona/ProjectPersona.CliTools generate-type-artifacts <path/to/YourSolution.sln> <catalog-output-dir> <index-output-dir>
```

This writes `{Assembly}[.{tfm}].md` files into the catalog directory and `type-source-index.db` into the index directory. Other subcommands: `generate-type-catalog`, `generate-type-source-index`.

### 2. (Optional) Build the vector index

Copy `ProjectPersona/ProjectPersona.CliTools/llama-options.example.json`, set `modelPath` and `nativeLibraryDirectory`, then:

```
dotnet run --project ProjectPersona/ProjectPersona.CliTools vectorize-type-source-index <index-dir> <lancedb-dir> --vector-dimension 768 --llama-options <your-options.json>
```

Use `revectorize-assemblies` to refresh only selected assemblies afterwards.

### 3. Run an MCP server

Set `typeSourceIndexPath` (and the `llama` / `lanceDb` sections if RAG is on, or `ragSearchEnabled: false`) in the server's `appsettings.json`, then start one of:

- `ProjectPersona.StdioMcpAPI` — stdio, launched by the client;
- `ProjectPersona.WebMcpAPI` — HTTP at `/mcp` (default port 5095, optional HTTPS).

### 4. Connect an agent

Ready-to-copy consumer workspaces:

- [`AgentInteraction.Claude`](AgentInteraction.Claude) — Claude Code (`CLAUDE.md`, `.mcp.json`)
- [`AgentInteraction.Cursor`](AgentInteraction.Cursor) — Cursor (`AGENTS.md`, rules, skill, `mcp.json`)

Put the generated `type-catalog/` folder next to the instructions and point `.mcp.json` / `mcp.json` at your server.

## Repository layout

```
docs/project-structure.md     documentation index (start here for the code)
AgentInteraction.Claude/      consumer workspace for Claude Code
AgentInteraction.Cursor/      consumer workspace for Cursor
ProjectPersona/               solution: Core, SourceIndex, SourceIndex.Sqlite, Application,
                              LlamaWrapper, RagTextSearch, CliTools, StdioMcpAPI, WebMcpAPI, Tests
```

The documentation is a tree: [docs/project-structure.md](docs/project-structure.md) links to a `STRUCTURE.md` in each project, so you (or an agent) only read what the task needs.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) ([на русском](CONTRIBUTING.ru.md)).

## License

[BSD Zero Clause License (0BSD)](LICENSE). Third-party components keep their own licenses, see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
