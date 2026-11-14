# Contributing

English | [Русский](CONTRIBUTING.ru.md)

## Setup

- .NET 10 SDK
- Windows x64 is required to run the RAG features (the native wrapper targets `llama.dll`); building, the catalog/index generation and the tests do not need it.

```
dotnet build ProjectPersona/ProjectPersona.sln
dotnet test ProjectPersona/ProjectPersona.sln
```

## Before you start

Read [docs/project-structure.md](docs/project-structure.md) and then only the `STRUCTURE.md` of the projects you touch.

## Rules

- Keep the layering: `SourceIndex` has no dependencies; only `Core` uses Roslyn; MCP servers never call Roslyn or vectorize.
- MCP tool classes are duplicated in `StdioMcpAPI` and `WebMcpAPI`. Change both together.
- If you change the catalog markdown format or the contract of `find` / `usedBy` / `textSearch` / `ragSearch`, update `AgentInteraction.Claude` and `AgentInteraction.Cursor` in the same pull request.
- If you change the vector or metadata format, bump `VectorizationPipeline.Version`.
- Keep `STRUCTURE.md` files accurate when you add, move or remove files.
- Do not commit generated data: `type-source-index/`, `type-catalog/`, LanceDB directories, `.gguf` models, `llama.dll` builds.

## Pull requests

Open a PR against the default branch and fill in the template.
