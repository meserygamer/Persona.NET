# ProjectPersona.Core

Parent: [project index](../../docs/project-structure.md)

Class library, `net10.0`. Builds the public catalog model and the in-memory source index from Roslyn and writes the markdown catalog. It knows nothing about index storage.

Namespaces: `ProjectPersona.Core`, `.Building`, `.Entities`, `.Dto`, `.SourceLookup`.

Dependencies: `ProjectPersona.SourceIndex` (shared names and models), `Microsoft.CodeAnalysis.CSharp` / `.Workspaces` 5.9.0.

Public API: `TypeCatalog`, `TypeCatalogMarkdownWriter`, `TypeSourceIndex` (Roslyn build and `ToSnapshot`), and the records/enums in `Entities` and `Dto`. The index contracts (`ITypeSourceLookup`, `TypeSourceMatch`, …) live in `ProjectPersona.SourceIndex`. Everything else in `Building` and `SourceLookup` is `internal`.

## `Entities/` and `Dto/` — immutable snapshot model

Hierarchy: `AssemblyGroup` (`Assembly`: name + TFM) → `NamespaceGroup` → `DeclaredType` (nested types recursively) → `DeclaredMember`. Hierarchy nodes live in `Entities/`, the rest in `Dto/`.

| File | Role |
|---|---|
| `Entities/Assembly.cs` | Assembly name and target framework (TFM) or `null` |
| `Entities/AssemblyGroup.cs` | `Assembly` plus the namespaces of one compilation |
| `Entities/NamespaceGroup.cs` | Namespace name + types (`(global)` for the empty name) |
| `Entities/DeclaredType.cs` | Type: kind, accessibility, XML docs, delegate signature, attributes, bases, type params, modifiers, members, nested types |
| `Dto/DeclaredMember.cs` | Member: kind, accessibility, XML docs, signature, attributes, modifiers, compact accessors, explicit interface implementation |
| `Dto/FunctionSignature.cs` | Method / operator / delegate / constructor signature |
| `Dto/ParameterInfo.cs` | Signature parameter |
| `Dto/TypeParameterInfo.cs` | Type parameter and constraints |
| `Dto/TypeRef.cs` | Type reference: FQN when resolved, source text otherwise; factories `Unresolved` / `Keyword` |
| `Dto/TypeRefKind.cs` | Named, Array, Pointer, NullableValue, TypeParameter, Dynamic, Tuple, Keyword, Unresolved |
| `Dto/BaseTypeRef.cs`, `BaseTypeKind.cs` | Direct base class or interface |
| `Dto/AttributeInfo.cs` | Attribute (resolved type + arguments as written) |
| `Dto/CodeTypeKind.cs` | Class, Struct, Interface, Record, RecordStruct, Enum, Delegate |
| `Dto/MemberKind.cs` | Method, Constructor, Operator, Property, Indexer, Field, Event, EnumMember |
| `Dto/AccessibilityLevel.cs` | C# accessibility levels |

## `Building/` — catalog reading and assembly

| File | Role |
|---|---|
| `TypeCatalog.cs` | Public entry point: `CreateAsync(Solution \| IEnumerable<Project>)` |
| `TypeDeclarationReader.cs` | Walks compilation unit / namespace, produces type and member snapshots |
| `CatalogBuilder.cs` | Accumulates snapshots, merges partials, sorts by assembly and TFM; contains `TypeSnapshot`, `AssemblyBuilder`, `NamespaceBuilder`, `TypeNodeBuilder` |
| `TargetFrameworkResolver.cs` | Project TFM (`AssemblyIdentity`) so multi-target assemblies are grouped separately |
| `TypeRefResolver.cs` | `TypeRef` from Roslyn syntax/symbol |
| `TypeRefCompatibility.cs` | Type compatibility when merging: FQN and an unresolved short name count as one type |
| `AttributeListReader.cs` | Attributes and base types via `TypeRefResolver` |
| `AccessorListReader.cs` | Compact property/indexer accessors without bodies (`get;`, `get...;`, explicit set access) |
| `MemberModifierReader.cs` | Member modifiers other than accessibility |
| `AccessibilityResolver.cs` | Effective accessibility from tokens and syntactic context |
| `XmlDocumentationContext.cs` | One `inheritdoc` resolver per project compilation |
| `XmlDocumentationReader.cs` | Normalizes XML docs from a Roslyn symbol or trivia; expands `inheritdoc` (override / interface / BCL XML) |
| `XmlDocumentationInheritdocResolver.cs` | Expands `inheritdoc` by documentation ID, override and interface chain; a `cref` to a type copies that type's docs |
| `XmlDocumentationAssemblyLookup.cs` | XML of referenced assemblies from a `.xml` next to the dll when metadata has no DocumentationProvider |
| `XmlDocumentationFormatter.cs` | Flat string from XML: `see` / `cref` / `param` / `returns` / `exception` |

## `SourceLookup/` — source index built from Roslyn

A pipeline separate from the signature catalog: syntax plus `SemanticModel` at `CreateAsync` to compute `References`. The result stays in memory; persisting it is the job of an `ITypeSourceIndexWriter` (SQLite) outside Core.

| File | Role |
|---|---|
| `TypeSourceIndex.cs` | Public entry point: `CreateAsync` (Roslyn), `ToSnapshot`, `Find`, `FindUsedBy`, `EnumerateDeclarations` over in-memory data |
| `TypeSourceIndexBuilder.cs` | Accumulates matches, groups by FQN and assembly |
| `TypeSourceLocator.cs` | Walks compilation unit / namespace / type / nested / enum / delegate, cuts `FullSpan`, collects references, indexes member slices |
| `TypeSourceMemberNameResolver.cs` | Member names in catalog format for matching `memberNames` |
| `TypeSourceReferenceCollector.cs` | Semantic walk of a declaration: types and members, dedup, `Unresolved` |
| `NamedTypeFqn.cs` | FQN from `INamedTypeSymbol` (string building is `TypeFqn.Build` in `SourceIndex`) |
| `TypeSourceUsedByInverter.cs` | Inverts `References` into `UsedBy` on `Build` |

## Core root files

- `TypeCatalogMarkdownWriter.cs` — serializes `TypeCatalog` to markdown (YAML frontmatter `assembly`, `targetFramework`, `namespaces`, `types`; namespace/type anchors; FQNs in signatures).
- `AssemblyArtifactFileName.cs` — artifact file names `{AssemblyName}[.{tfm}].ext` for the markdown catalog.

## Related

- Storage contracts and models: [SourceIndex](../ProjectPersona.SourceIndex/STRUCTURE.md)
- Who calls Core: [CliTools](../ProjectPersona.CliTools/STRUCTURE.md)
