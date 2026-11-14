# ProjectPersona.SourceIndex.Sqlite

Parent: [project index](../../docs/project-structure.md)

Class library, `net10.0`. SQLite storage for the source index: schema, writer, reader, text search, DI registration.

Dependencies: `ProjectPersona.SourceIndex`, `Microsoft.Data.Sqlite`, `Microsoft.Extensions.DependencyInjection.Abstractions`. Namespaces mirror folders (`ProjectPersona.SourceIndex.Sqlite.Reading`, …).

| Folder / file | Role |
|---|---|
| `Configuration/SqliteTypeSourceIndexOptions.cs` | Path to the index file (empty → `BaseDirectory/type-source-index/type-source-index.db`) |
| `Schema/SqliteIndexFormat.cs` | Default file name `type-source-index.db` and format version (`PRAGMA user_version`, currently 2) |
| `Schema/SqliteSchema.cs` | `internal` DDL: `assembly`, `declaration`, `member_slice`, `reference`, `used_by` (`STRICT`). Indexes and the FTS5 table `declaration_fts` (external content over `declaration.source_code`, `trigram` tokenizer) are created after the inserts |
| `Connections/SqliteIndexFile.cs` | `internal`: file and format-version check, `ReadOnly` connection string; shared by reading and search |
| `Reading/SqliteTypeSourceLookupFactory.cs` | `ITypeSourceLookupFactory`: validates the file and format version |
| `Reading/SqliteTypeSourceLookup.cs` | `internal` `ITypeSourceLookup`: on-demand reads (a `Mode=ReadOnly` connection per call), streaming `EnumerateDeclarations`; answers are built by `TypeSourceProjection` |
| `Reading/SqliteIndexReader.cs` | `internal`: SQL reads of tables into models on an open connection, no answer building |
| `Searching/SqliteTypeSourceTextSearch.cs` | `ITypeSourceTextSearch`: `MATCH` on `declaration_fts` selects declarations; occurrence positions and member slices are computed from the candidate text; case sensitivity is an exact comparison |
| `Searching/SqliteTextSearchQueries.cs` | `internal`: SQL for candidate selection and member-slice reads |
| `Searching/SubstringOccurrences.cs` | `internal`: positions of substring occurrences (empty string is rejected) |
| `Writing/SqliteTypeSourceIndexWriter.cs` | `ITypeSourceIndexWriter`: writes the snapshot to a temp file and swaps it in |
| `Writing/SqliteIndexDatabaseBuilder.cs` | `internal`: schema → bulk insert in one transaction → indexes → format version → self-check |
| `Writing/SqliteIndexRowWriter.cs` | `internal`: inserts snapshot rows, assigns identifiers |
| `Writing/SqliteIndexVerifier.cs` | `internal`: compares expected and actual `COUNT(*)` |
| `Writing/SqliteIndexFileReplacer.cs` | `internal`: replaces the index file with the finished temp file, retrying if the target is busy |
| `Writing/SqliteAssemblyKey.cs` | `internal`: key of an `assembly` row (name + TFM, a missing TFM is the empty string) |
| `ServiceCollectionExtensions.cs` | `AddSqliteTypeSourceIndex(databasePathFactory)` — registers the factory, the writer and `ITypeSourceTextSearch` |

## Notes

- Member slice offsets (`member_slice.start` / `length`) are UTF-16 code units and are cut in .NET, not with SQL `substr`.
- Results are sorted by assembly number (name, then TFM) and by insertion `id`.
- In the database an assembly compilation is a row of the `assembly` table (name + TFM); declarations and references are attached to it.

## Related

- Contracts implemented here: [SourceIndex](../ProjectPersona.SourceIndex/STRUCTURE.md)
- Writer is called by: [CliTools](../ProjectPersona.CliTools/STRUCTURE.md); reader is used by: [Application](../ProjectPersona.Application/STRUCTURE.md) and the MCP hosts
