# ProjectPersona.RagTextSearch

Parent: [project index](../../docs/project-structure.md)

Class library, `net10.0`. Tokenization, splitting declarations under the token limit, embeddings, vector writing and the vector-store manifest. The current path is `llama.dll`, no HTTP.

Dependencies: `LlamaWrapper`; packages LanceDB 2.5.0, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`. `InternalsVisibleTo`: `ProjectPersona.Tests`.

```
ProjectPersona.RagTextSearch/
├── ITextTokenizer.cs              token count and text parsing into LlamaModelToken
├── ITextEmbedder.cs               embeddings of ready prompts and search queries
├── ITextSplitter.cs               text windows under the document template
├── IVectorStore.cs                write, search, clear the whole table or assemblies, Optimize; manifest: Read / Write / Delete, MarkUpdating / MarkReady
├── IEmbeddingProfileProvider.cs   description of the current embedder for manifest comparison
├── VectorData.cs                  vector, assembly, TFM and JSON metadata of one row
├── ServiceCollectionExtensions.cs AddRagTextSearch: Llama session, tokenizer, embedder, splitter, profile, LanceDB layers, IVectorStore
├── Llama/LlamaSettings.cs         GGUF, llama parameters, document and search templates, batchSize, overlap, freeSpaceClearance
├── Llama/LlamaModelSessionHolder.cs  lazy single LlamaModelSession per process; actual n_ctx
├── Llama/LlamaEmbeddingProfileProvider.cs  EmbeddingProfile from GGUF metadata and LlamaSettings (no file hash)
├── Llama/Tokenizer/LlamaTextTokenizer.cs  ITextTokenizer via the llama.dll session
├── Llama/Tokenizer/LlamaModelToken.cs     token id and piece
├── Llama/Tokenizer/LlamaTokenizerQuery.cs DTO for HTTP /tokenize; not called by the llama.dll pipeline
├── Llama/Tokenizer/TokenizerParser.cs     token count from /tokenize JSON; not called by the llama.dll pipeline
├── Llama/Tokenizer/ModelTokenParser.cs    id and piece from /tokenize JSON; not called by the llama.dll pipeline
├── Llama/Embedder/LlamaTextEmbedder.cs    ITextEmbedder via the llama.dll session
├── Llama/Embedder/EmbeddingParser.cs      parses OpenAI-compatible JSON; not called by the llama.dll pipeline
├── Llama/TextSplitter/LlamaTextSplitter.cs  windows under LlamaSettings.BatchSize
├── Llama/TextSplitter/TokenWindowPlanner.cs internal: sliding token windows
├── Llama/TextSplitter/TokenTextAligner.cs   internal: piece offsets mapped to characters of the source string
├── LanceDB/LanceDbSettings.cs            directory (empty → BaseDirectory/lance-db), table (type_source_index; the name _meta is reserved), index name, dimension
├── LanceDB/LanceDbVectorStore.cs          IVectorStore: delegates to the structure, data and manifest layers
├── LanceDB/LanceDbConnectionProvider.cs   lazy shared connection
├── LanceDB/Structure/LanceDbTableManager.cs  existence, creation, schema check, clearing, index, optimization
├── LanceDB/Data/LanceDbVectorRepository.cs   batch writes, nearest search, delete by assembly / target_framework
├── LanceDB/Data/VectorDataArrowMapper.cs     VectorData ↔ Arrow (columns vector, metadata, assembly, target_framework)
├── LanceDB/Data/LanceDbManifestRepository.cs manifest in the service table _meta, one row per main table
├── Manifest/VectorStoreManifest.cs     manifest: versions, dimension, fingerprint, parameters, templates, State (Ready / Updating), PendingAssemblies
├── Manifest/VectorStoreManifestJson.cs  manifest serialization (enums as strings)
├── Manifest/EmbeddingFingerprint.cs    hard fingerprint: model, architecture, file type, dimension, file size, pooling
├── Manifest/EmbeddingBuildParameters.cs soft splitting parameters: batchSize, freeSpaceClearance, overlapTokens
├── Manifest/EmbeddingProfile.cs        fingerprint + parameters + templates of the current embedder
├── Manifest/ManifestCompatibility.cs   Check: errors (format, pipeline version, dimension, fingerprint) and warnings (parameters, templates)
├── Manifest/ManifestDifferences.cs     internal: text of field differences
├── Utils/AsyncReaderWriterLock.cs      async many-readers / one-writer lock for LanceDB
└── Utils/StringUtils.cs               number of arguments of a format string
```

## Notes

- `AddVectorsAsync` writes vectors with assembly, TFM and JSON metadata. `ClearStoreAsync` leaves the table empty; `ClearAssembliesAsync` deletes rows of the chosen assemblies. `StoreExistsAsync` checks that the table exists, not the number of vectors.
- MCP and CLI bind the `llama` config section to `LlamaSettings` and the `lanceDb` section to `LanceDbSettings`; `AppSettings` has none of these fields.
- Templates: `DocumentQueryTemplate` (default `title: {0} | text: {1}`) and `SearchQueryTemplate` (default `task: code retrieval | query: {0}`), both in `LlamaSettings`.
- The manifest (`VectorStoreManifest`) lives in the LanceDB service table `_meta`. A fingerprint mismatch (model metadata, file size, pooling) makes vectors incomparable → error; a mismatch in build parameters or templates is only a warning.

## Related

- Native layer: [LlamaWrapper](../ProjectPersona.LlamaWrapper/STRUCTURE.md)
- Pipeline that fills the store and the service that queries it: [Application](../ProjectPersona.Application/STRUCTURE.md)
- JSON file for `llama` settings used by the CLI: [CliTools](../ProjectPersona.CliTools/STRUCTURE.md)
