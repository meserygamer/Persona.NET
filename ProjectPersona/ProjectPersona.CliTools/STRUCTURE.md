# ProjectPersona.CliTools

Parent: [project index](../../docs/project-structure.md)

Console exe, `net10.0`. The official entry point for generating artifacts and for RAG vectorization. It is separate from the MCP servers.

Dependencies: Application, Core, SourceIndex.Sqlite, RagTextSearch; MSBuild Locator and `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.9.0; `System.CommandLine` 2.0.12; `Microsoft.Extensions.Hosting` / `Logging.Console` (DI and console logs for vectorization).

```
ProjectPersona.CliTools/
├── Startup.cs                     RootCommand: subcommand = builder + validator + handler
├── MsBuildSolutionSession.cs      MSBuild Locator + OpenSolutionAsync
├── LlamaSettingsFile.cs           reads LlamaSettings from JSON (comments allowed, an unknown field is an error)
├── llama-options.example.json     example of the --llama-options file
├── CommandBuilders/               per subcommand: *CommandBuilder (System.CommandLine arguments and options) and *Options (record with validation attributes):
│                                  GenerateTypeCatalog, GenerateTypeSourceIndex, GenerateTypeArtifacts, VectorizeTypeSourceIndex, RevectorizeAssemblies
├── CommandHandlers/
│   ├── GenerationCli.cs           generate-*: one .sln open, calls Core writers and SqliteTypeSourceIndexWriter
│   ├── VectorizeTypeSourceIndexCli.cs  vectorize-type-source-index; RunWithVectorizerAsync — shared DI container and console progress
│   └── RevectorizeAssembliesCli.cs    revectorize-assemblies: parses --assembly, dimension from the manifest, VectorizeAssembliesAsync
└── Validation/
    ├── ICommandInputValidator.cs      contract for input checks before the handler
    ├── DataAnnotationsInputValidator.cs  checks ValidationAttribute on *Options
    ├── LlamaSettingsInputValidator.cs    LlamaSettings attributes and a trial model load before LanceDB is cleared
    └── Attributes/                    ExistingFile, ExistingDirectory, AssemblySelectors (Name or Name:tfm)
```

## Flow

1. `Startup.cs` parses the subcommand with `System.CommandLine`. Each `CommandBuilders/*CommandBuilder` builds an options record and runs it through `DataAnnotationsInputValidator` (`Required`, `ExistingFile`, `ExistingDirectory`, `AssemblySelectors`, `Range`) before the handler in `CommandHandlers/` runs.
2. `GenerationCli` opens the `.sln` once via `MsBuildSolutionSession` (`RunWithSolutionAsync`); `generate-type-artifacts` uses one open for both writers.
3. Catalog: `TypeCatalog.CreateAsync` → `TypeCatalogMarkdownWriter` (one `.md` per assembly compilation; multi-target: `{AssemblyName}.{tfm}.md`).
4. Index: `TypeSourceIndex.CreateAsync` → `ToSnapshot()` → `SqliteTypeSourceIndexWriter` (one file `type-source-index.db`; written to a temporary `.tmp` that replaces the target after the row counts are verified). The process then exits.
5. Vectorization: `VectorizeTypeSourceIndexCli` (and `RevectorizeAssembliesCli` on top of it) builds `TypeSourceIndexVectorizerV2`, reads the finished SQLite index, writes LanceDB and prints the declaration progress percentage.

## Subcommands

Paths are passed as arguments (not `appsettings.json`). `SolutionPath` and the `--llama-options` file must exist; `IndexPath` must be an existing directory containing `type-source-index.db`.

- `generate-type-catalog <SolutionPath> <OutputPath>`
- `generate-type-source-index <SolutionPath> <OutputPath>` — `OutputPath` is a directory; the file inside is `type-source-index.db`
- `generate-type-artifacts <SolutionPath> <CatalogOutputPath> <IndexOutputPath>`
- `vectorize-type-source-index <IndexPath> <LanceDbPath> --vector-dimension … --llama-options <JSON path>` — optional `--table-name` (default `type_source_index`). Full rebuild.
- `revectorize-assemblies <IndexPath> <LanceDbPath> --assembly Name[:tfm] … --llama-options <JSON path>` — `--assembly` is repeatable (without `:tfm` = all TFMs of the assembly); optional `--vector-dimension` (must match the manifest; taken from it by default) and `--table-name`. Requires an existing store with a manifest from a full vectorization.

The `--llama-options` JSON holds `LlamaSettings` fields in camelCase: `modelPath`, `nativeLibraryDirectory`, `batchSize`, `documentQueryTemplate` (default `title: {0} | text: {1}`), `freeSpaceClearance`, `searchQueryTemplate` (default `task: code retrieval | query: {0}`), `overlapTokens`, `nCtx`, `nBatch`, `nUbatch`, `nSeqMax`, `nThreads`, `nThreadsBatch`, `nGpuLayers`, `devices`, `flashAttn`. A missing field keeps the `llama_*_default_params` value; an unknown field is an error. The file is validated (`LlamaSettingsInputValidator`: attributes and a trial model load) before LanceDB is cleared. See `llama-options.example.json`. There is no `appsettings.json` and no Roslyn in the vectorization path.

An empty `OutputPath` is rejected by validation (`Required`); the `BaseDirectory/type-catalog` and `BaseDirectory/type-source-index` fallbacks remain in `ResolveDirectory` only for an empty string.

## Related

- Catalog and Roslyn index building: [Core](../ProjectPersona.Core/STRUCTURE.md)
- SQLite writer: [SourceIndex.Sqlite](../ProjectPersona.SourceIndex.Sqlite/STRUCTURE.md)
- Vectorizer and pipes: [Application](../ProjectPersona.Application/STRUCTURE.md); store and manifest: [RagTextSearch](../ProjectPersona.RagTextSearch/STRUCTURE.md)
