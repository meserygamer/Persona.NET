# ProjectPersona.SourceIndex

Parent: [project index](../../docs/project-structure.md)

Class library, `net10.0`. Source-index contracts, shared DTOs and the common logic that shapes `find` / `usedBy` answers. No Roslyn, no storage dependency, no package dependencies.

`InternalsVisibleTo`: `ProjectPersona.Core`. A single namespace `ProjectPersona.SourceIndex` covers all folders (subfolders do not change the namespace).

| Folder | Files | Role |
|---|---|---|
| `Contracts/` | `ITypeSourceLookup.cs`, `ITypeSourceTextSearch.cs`, `ITypeSourceIndexWriter.cs`, `ITypeSourceLookupFactory.cs` | Contracts: lookup (`Find`, `FindUsedBy`, `EnumerateDeclarations` — all, or a set of assembly + TFM pairs), substring search (`Search` → `TypeSourceTextMatch`), snapshot writing, opening a stored index (`Location` for logs) |
| `Models/` | `TypeSourceReference.cs`, `TypeSourceReferenceKind.cs`, `TypeSourceDeclaration.cs`, `TypeSourceMemberSpan.cs`, `TypeSourceTextMatch.cs` | Shared models: reference to a type or member, reference kind, declaration without `References`, member slice, document with substring occurrence positions |
| `Find/` | `TypeSourceAssemblyFilter.cs`, `TypeSourceMatch.cs`, `TypeSourceMemberMatch.cs` | `Find` answer: assembly, `Type` (FQN), TFM; either `SourceCode` / `References` of the whole type, or `Members` |
| `UsedBy/` | `TypeSourceUsedBy.cs`, `TypeSourceUsedByMatch.cs`, `TypeSourceUsedByMemberMatch.cs`, `TypeSourceUsedByLimit.cs` | `FindUsedBy` answer: consumer, target match, members; `DefaultLimit` (100) and list truncation |
| `Storage/` | `TypeSourceIndexSnapshot.cs`, `TypeSourceIndexWriteResult.cs`, `TypeSourceIndexedMatch.cs`, `UsedByFqnEntry.cs`, `MemberUsedBySignatureGroup.cs` | Data model for stores: snapshot to write, write result, indexed matches, consumer edges |
| `Projection/` | `TypeSourceProjection.cs`, `TypeSourceMatchFilter.cs`, `TypeSourceUsedByProjector.cs` | Builds `Find` / `FindUsedBy` answers from rows already read. Store implementations only read rows and call `TypeSourceProjection`. The member filter and consumer projector are `internal` |
| `Naming/` | `TypeFqn.cs`, `TypeSourceMemberCatalogName.cs` | Naming: FQN building and normalization (`TypeFqn`); catalog member names, overloads, operator aliases and `member_used_by` keys (`internal`) |

## Related

- SQLite implementation of these contracts: [SourceIndex.Sqlite](../ProjectPersona.SourceIndex.Sqlite/STRUCTURE.md)
- Producer of snapshots (Roslyn): [Core](../ProjectPersona.Core/STRUCTURE.md)
- Consumer of the contracts in services: [Application](../ProjectPersona.Application/STRUCTURE.md)
