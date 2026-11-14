# ProjectPersona.Tests

Parent: [project index](../../docs/project-structure.md)

xUnit test project, `net10.0`. Covers library contracts before integration. It can see internals of `RagTextSearch` (`InternalsVisibleTo`).

```
ProjectPersona.Tests/
├── Application/
│   ├── RagIndexStateHolderTests.cs        RagIndexStateHolder state checks
│   └── RagSearchRegistrationTests.cs      AddRagSearch registration
├── Pipes/
│   └── ChannelPipeBaseDisposeTests.cs     ChannelPipeBase.DisposeAsync
├── SourceIndexSqlite/
│   ├── GetDeclarationCountTests.cs        declaration counting
│   └── SqliteTypeSourceTextSearchTests.cs SqliteTypeSourceTextSearch on a real SQLite file
└── RagTextSearch/
    ├── LanceDB/    LanceDbSettingsTests, LanceDbManifestRepositoryTests
    ├── Llama/
    │   ├── Embedder/    EmbeddingParserTests
    │   ├── Tokenizer/   TokenizerParserTests, ModelTokenParserTests, TokenizerResponseStreams
    │   └── TextSplitter/ LlamaTextSplitterTests, TokenWindowPlannerTests, TokenTextAlignerTests, FakeTextTokenizer
    └── Manifest/   VectorStoreManifestTests, ManifestCompatibilityTests, ManifestTestData
```

Where the code under test lives: [SourceIndex.Sqlite](../ProjectPersona.SourceIndex.Sqlite/STRUCTURE.md), [RagTextSearch](../ProjectPersona.RagTextSearch/STRUCTURE.md), [Application](../ProjectPersona.Application/STRUCTURE.md).
