# Persona.NET project structure (index)

Entry point for agents and humans. This file is a map only: it tells you which project does what and which child `STRUCTURE.md` to open. **Read only the child documents relevant to your task** — do not read them all.

Target framework: **.NET 10**. C# analysis: **Roslyn** (`Microsoft.CodeAnalysis.CSharp` 5.9.0). Source index storage: **SQLite** (`Microsoft.Data.Sqlite`, single file `type-source-index.db`). Vector storage: **LanceDB**. Embeddings: local `llama.dll` (llama.cpp b12359 ABI).

## What the repository does

Persona.NET turns a C# solution into documentation an AI agent can query without reading the sources:

1. A **type catalog** — markdown files with signatures, FQNs and XML docs (one file per assembly compilation).
2. A **source index** — a SQLite file with full declaration sources, outgoing references, consumers (`UsedBy`) and an FTS5 text index.
3. An optional **vector index** (RAG) in LanceDB built from the source index.
4. An **MCP server** (stdio or HTTP) exposing the index to agents through the tools `find`, `usedBy`, `textSearch` and `ragSearch`.

Generation (Roslyn, MSBuild) and serving (MCP) are separate processes. The MCP servers never call Roslyn and never vectorize.

## Repository layout

```
Persona.NET/
├── docs/project-structure.md        this file (root of the documentation tree)
├── AgentInteraction.Claude/         sample Claude Code consumer workspace (CLAUDE.md, mcp-contract.md, .mcp.json)
├── AgentInteraction.Cursor/         sample Cursor consumer workspace (AGENTS.md, .cursor rules and skill, mcp.json)
└── ProjectPersona/                  solution and sources
    ├── ProjectPersona.sln
    ├── ProjectPersona.Core/
    ├── ProjectPersona.SourceIndex/
    ├── ProjectPersona.SourceIndex.Sqlite/
    ├── ProjectPersona.Application/
    ├── ProjectPersona.LlamaWrapper/
    ├── ProjectPersona.RagTextSearch/
    ├── ProjectPersona.CliTools/
    ├── ProjectPersona.StdioMcpAPI/
    ├── ProjectPersona.WebMcpAPI/
    └── ProjectPersona.Tests/
```

Not stored in git: `type-source-index/` (the generated `.db`), generated `type-catalog/` markdown, LanceDB directories, the GGUF model file and the `llama.dll` binaries. Everything is produced or supplied locally.

## Projects and where to read more

| Project | Kind | Role | Details |
|---|---|---|---|
| `ProjectPersona.Core` | library | Catalog model, Roslyn-based catalog and source-index building, markdown writer | [STRUCTURE.md](../ProjectPersona/ProjectPersona.Core/STRUCTURE.md) |
| `ProjectPersona.SourceIndex` | library | Storage-agnostic source-index contracts, DTOs, `find` / `usedBy` projection logic | [STRUCTURE.md](../ProjectPersona/ProjectPersona.SourceIndex/STRUCTURE.md) |
| `ProjectPersona.SourceIndex.Sqlite` | library | SQLite implementation: schema, writer, reader, FTS5 text search | [STRUCTURE.md](../ProjectPersona/ProjectPersona.SourceIndex.Sqlite/STRUCTURE.md) |
| `ProjectPersona.Application` | library | MCP-facing services, streaming vectorization pipes, `AppSettings` | [STRUCTURE.md](../ProjectPersona/ProjectPersona.Application/STRUCTURE.md) |
| `ProjectPersona.LlamaWrapper` | library | P/Invoke wrapper over `llama.dll`: tokenizer and embeddings | [STRUCTURE.md](../ProjectPersona/ProjectPersona.LlamaWrapper/STRUCTURE.md) |
| `ProjectPersona.RagTextSearch` | library | Text splitting, embeddings, LanceDB store, vector-store manifest | [STRUCTURE.md](../ProjectPersona/ProjectPersona.RagTextSearch/STRUCTURE.md) |
| `ProjectPersona.CliTools` | console exe | Generator: catalog, source index, vectorization | [STRUCTURE.md](../ProjectPersona/ProjectPersona.CliTools/STRUCTURE.md) |
| `ProjectPersona.StdioMcpAPI` | console exe | stdio MCP server | [STRUCTURE.md](../ProjectPersona/ProjectPersona.StdioMcpAPI/STRUCTURE.md) |
| `ProjectPersona.WebMcpAPI` | web exe | HTTP MCP server (`/mcp`) | [STRUCTURE.md](../ProjectPersona/ProjectPersona.WebMcpAPI/STRUCTURE.md) |
| `ProjectPersona.Tests` | xUnit | Unit tests | [STRUCTURE.md](../ProjectPersona/ProjectPersona.Tests/STRUCTURE.md) |
| `AgentInteraction.Claude` | workspace | Consumer workspace for Claude Code | [STRUCTURE.md](../AgentInteraction.Claude/STRUCTURE.md) |
| `AgentInteraction.Cursor` | workspace | Consumer workspace for Cursor | [STRUCTURE.md](../AgentInteraction.Cursor/STRUCTURE.md) |

## Dependency graph

```
SourceIndex  <──  SourceIndex.Sqlite
    ▲   ▲
    │   └────────  Core (Roslyn)
    │
LlamaWrapper  <──  RagTextSearch  <──  Application  <──  StdioMcpAPI / WebMcpAPI
                                           ▲
                      Core, SourceIndex.Sqlite, RagTextSearch ──┘ (all three also feed CliTools)
```

- `SourceIndex` has no dependencies (no Roslyn, no SQLite).
- `Core` is the only library that uses Roslyn; MCP servers do not reference it.
- `Application` depends on `SourceIndex` and `RagTextSearch`.
- `CliTools` depends on `Application`, `Core`, `SourceIndex.Sqlite`, `RagTextSearch`.
- `StdioMcpAPI` / `WebMcpAPI` depend on `Application` and `SourceIndex.Sqlite`.

## Data flow in one minute

- **Generation** (`CliTools` only): open `.sln` with MSBuild → `Core` builds the catalog and the source index → markdown files and `type-source-index.db` are written. Details: [CliTools](../ProjectPersona/ProjectPersona.CliTools/STRUCTURE.md), [Core](../ProjectPersona/ProjectPersona.Core/STRUCTURE.md).
- **Vectorization** (`CliTools` only): read the SQLite index → split declarations into token windows → embed with `llama.dll` → write LanceDB plus a manifest. Details: [Application](../ProjectPersona/ProjectPersona.Application/STRUCTURE.md) (pipes), [RagTextSearch](../ProjectPersona/ProjectPersona.RagTextSearch/STRUCTURE.md) (storage, manifest).
- **Serving** (`StdioMcpAPI` / `WebMcpAPI`): open the `.db` read-only at startup, answer `find` / `usedBy` / `textSearch` from SQLite and `ragSearch` from LanceDB. Details: [StdioMcpAPI](../ProjectPersona/ProjectPersona.StdioMcpAPI/STRUCTURE.md), [WebMcpAPI](../ProjectPersona/ProjectPersona.WebMcpAPI/STRUCTURE.md), [Application](../ProjectPersona/ProjectPersona.Application/STRUCTURE.md).

## Which document to open

| Task | Open |
|---|---|
| Change catalog format or Roslyn reading of types/members | Core |
| Change how `find` / `usedBy` answers are shaped | SourceIndex (projection), then Application (responses) |
| Change SQLite schema, FTS5 search, file writing | SourceIndex.Sqlite |
| Add or change an MCP tool | StdioMcpAPI **and** WebMcpAPI (tool classes are duplicated), Application |
| Add a CLI subcommand or option | CliTools |
| Change chunking, embeddings, LanceDB, manifest compatibility | RagTextSearch, then Application (pipes) |
| Change the vectorization pipeline or progress | Application |
| Change how an agent in a consumer workspace is instructed | AgentInteraction.Claude / AgentInteraction.Cursor |

## Cross-cutting rules

- Generated `type-catalog/*.md` files are overwritten by the writer — never edit them by hand.
- The tool contract (`find`, `usedBy`, `textSearch`, `ragSearch`) is mirrored in the consumer workspaces. When the catalog format or the contract changes, update both [AgentInteraction.Claude](../AgentInteraction.Claude/STRUCTURE.md) and [AgentInteraction.Cursor](../AgentInteraction.Cursor/STRUCTURE.md) in the same change.
- When you add, move or remove files, update the `STRUCTURE.md` of the affected project, and this index if a project is added or removed.
