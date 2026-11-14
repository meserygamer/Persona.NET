# ProjectPersona.Application

Parent: [project index](../../docs/project-structure.md)

Class library, `net10.0`. MCP-facing services, the vectorization pipeline and shared settings.

Dependencies: `SourceIndex`, `RagTextSearch`, `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`. It does not generate the catalog or the index and knows nothing about the storage format.

```
ProjectPersona.Application/
├── AppSettings.cs                 typeSourceIndexPath (path to the .db; empty → BaseDirectory/type-source-index/type-source-index.db), ragSearchEnabled (default true)
├── Pipes/
│   ├── IChannelPipe.cs            streaming stage contract: Join / RunOnceAsync / Finalize / Cancel, Writer, CompleteTask
│   ├── ChannelPipeBase.cs         skeleton: bounded channel, background loop, failure and cancellation propagation along the chain; descendants implement ProcessAsync
│   ├── JoinedChannelPipe.cs       joins two pipes, CompleteTask = WhenAll
│   └── Signal.cs                  empty start signal for the first pipe
├── Services/TypeSourceIndexService/
│   ├── TypeSourceIndexHolder.cs   lazy one-time index open via ITypeSourceLookupFactory
│   ├── TypeSourceIndexStartupValidator.cs  IHostedService: opens the index at startup, an error aborts startup
│   ├── TypeSourceIndexService.cs  FindAsync / FindUsedByAsync → responses with an Error for the agent
│   └── Responses/
│       ├── FindResponse.cs        search error or list of TypeSourceMatch
│       └── UsedByResponse.cs      search error or list of TypeSourceUsedByMatch
├── Services/RagSearchService/
│   ├── ServiceCollectionExtensions.cs  AddRagSearch (RagTextSearch + holder + service + warmup) and AddTypeSourceIndexVectorizer (vectorizer and pipe factories)
│   ├── RagSearchService.cs        SearchForToolAsync / SearchAsync: checks vector-store state, limit and query length in tokens, projects to RagSearchMatch
│   ├── RagIndexState.cs           RagIndexStatus (Missing / Invalid / Updating / Ready) and the check result with Error / Warnings
│   ├── RagIndexStateHolder.cs     one check per process of the table and manifest (ManifestCompatibility)
│   ├── RagIndexWarmupService.cs   IHostedService: starts the check at startup without waiting for it
│   ├── TypeSourceIndexVectorizerV2.cs  VectorizeTypeSourceIndexAsync (full rebuild) and VectorizeAssembliesAsync (selected assemblies); manifest and Optimize
│   ├── VectorizationPipeline.cs   Version — pipeline version stored in the manifest; bump it when the vector or metadata format changes
│   ├── VectorStoreExtensions.cs   ReadRequiredManifestAsync
│   ├── VectorizationProgress.cs   snapshot of the processed-declaration share for the CLI
│   ├── TypeSourceVectorMetadata.cs  JSON fragment metadata for the metadata column
│   ├── TypeSourceVectorMetadataWithChunk.cs  same metadata plus ChunkIndex and window text
│   ├── Pipes/
│   │   ├── TypeSourceProducerPipe.cs          Signal → batches of TypeSourceDeclaration for the whole index (default 256)
│   │   ├── AssemblyTypeSourceProducerPipe.cs  same, only for given assembly + TFM pairs
│   │   ├── SplitDeclarationToEmbedPipe.cs     tokenization and window splitting (Parallel.ForEachAsync) → DeclarationBatch<EmbeddingWorkItem>
│   │   ├── EmbeddingPipe.cs                   embeds a batch and serializes metadata → DeclarationBatch<VectorData>
│   │   ├── InsertVectorDataInDbPipe.cs        writes to IVectorStore; the DeclarationEnd boundary moves on
│   │   └── Dto/
│   │       ├── DeclarationBatch.cs            batch of data + number of declarations since the start of the index (for progress)
│   │       └── EmbeddingWorkItem.cs           assembly, TFM, metadata, window token ids
│   └── Responses/
│       └── RagSearchResponse.cs   Error / Matches / Warnings for the MCP ragSearch tool
└── Services/TextSearchService/
    ├── TextSearchService.cs       SearchAsync via ITypeSourceTextSearch → sort (assembly, TFM, type, level, member), ~200-char snippets, Total before truncation
    └── Responses/
        └── TextSearchResponse.cs  Error / Total / Matches
```

Public contract for MCP hosts and CLI vectorization: `AppSettings`, `TypeSourceIndexHolder`, `TypeSourceIndexStartupValidator`, `TypeSourceIndexService`, `TextSearchService`, `RagSearchService`, `RagIndexStateHolder`, `RagIndexWarmupService`, `TypeSourceIndexVectorizerV2`, the `AddRagSearch` / `AddTypeSourceIndexVectorizer` extensions, the pipes in `Pipes/` and `Services/RagSearchService/Pipes/`, and the DTOs in `Responses/`.

## Vectorization pipeline (V2)

A chain of streaming pipes on bounded channels (`ChannelPipeBase`):

`TypeSourceProducerPipe` (or `AssemblyTypeSourceProducerPipe`) → `SplitDeclarationToEmbedPipe` → `EmbeddingPipe` → `InsertVectorDataInDbPipe`

- Each batch carries `DeclarationEnd` for progress percentage.
- A failure in any stage closes the neighbouring channels; `Cancel` stops the whole chain.
- Tokens and embeddings come from a single `LlamaModelSession` (see [LlamaWrapper](../ProjectPersona.LlamaWrapper/STRUCTURE.md)).
- Two modes: `VectorizeTypeSourceIndexAsync` — full rebuild (delete manifest → clear table → write all → write `Ready` manifest → `Optimize`; an interrupted run leaves the store without a manifest, i.e. invalid); `VectorizeAssembliesAsync` — partial (the manifest must exist and match the current embedder; assemblies are marked `Updating`, their rows deleted by `assembly` / `target_framework`, rewritten, then unmarked; on failure the mark stays and a rerun repairs the store).

## `ragSearch` state handling

`RagIndexStateHolder` checks the vector store once per process; `RagIndexWarmupService` runs the check at startup without blocking the host.

- `Missing` / `Invalid` (no manifest, or incompatible with the server's model) → every `ragSearch` returns `Error`.
- `Updating` → search works, and `Warnings` lists the unfinished assemblies.
- `Ready` → normal.
- Changes to the store while the server runs are not tracked; restart is required.

## Related

- Embedding, LanceDB and manifest: [RagTextSearch](../ProjectPersona.RagTextSearch/STRUCTURE.md)
- Hosts that expose these services: [StdioMcpAPI](../ProjectPersona.StdioMcpAPI/STRUCTURE.md), [WebMcpAPI](../ProjectPersona.WebMcpAPI/STRUCTURE.md)
- CLI that runs the vectorizer: [CliTools](../ProjectPersona.CliTools/STRUCTURE.md)
