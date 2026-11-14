# MCP `libraries-info`: contract details

Read on demand from [CLAUDE.md](CLAUDE.md). Responses are JSON with PascalCase fields. Every response has `Error` (null on success) and `Matches` (null on failure).

## Response shapes

`find` → `Matches[]`: `AssemblyName`, `Type` (FQN), `TargetFramework`, then either `SourceCode` + `References` (whole type; `memberNames` null/empty) or `Members[]` of `Name`, `SourceCode`, `References`. A partial type yields several `Matches` elements. Without `assemblyName`, matches come from all assemblies.

`References[]`: `Kind`, `Display`, `Fqn`, `Name`, `AssemblyName`, `Signature`. `Kind` is a number: `0` Type, `1` Method (operators too), `2` Constructor, `3` Property, `4` Indexer, `5` Field, `6` Event, `7` Unresolved, `8` EnumMember. For a member reference `Fqn` is the FQN of the type that declares the member. `Unresolved` entries have an empty `Fqn`: don't fill it in yourself and don't pass them to `find`. Don't read an enum member as a field.

`usedBy` → `Matches[]`: `AssemblyName`, `Type`, `TargetFramework`, `UsedBy[]`, `UsedByTotal`, `Members[]`. Each member row: `Name`, `Signature`, `UsedBy[]`, `UsedByTotal`. Each consumer: `AssemblyName`, `TargetFramework`, `Type`, `MemberName`, `Signature`. Overloads sharing one catalog name come back as separate member rows; tell them apart by `Signature`.

`textSearch` → `Total`, `Matches[]`: `AssemblyName`, `TargetFramework`, `Type`, `Level` (`Type` or `Member`), `MemberName`, `OccurrenceCount`, `Snippet`. One substring inside a member produces two rows: `Level: Type` and `Level: Member`. For the body, call `find` with the `Member` row's `MemberName`. `Total` counts rows before `limit`. Rows are sorted by `AssemblyName`, `TargetFramework`, `Type`, then the `Type` row before its `Member` rows; this is not a relevance order, so a truncated list covers the alphabetically first assemblies. `Snippet` is about 200 characters around the first occurrence.

`ragSearch` → `Matches[]` ordered by closeness: `AssemblyName`, `TargetFramework`, `Type`, `Level`, `MemberName`, `ChunkIndex` (window of a split declaration, 0 if single), `Source`. With `includeFragmentSource: false`, `Source` is null and `ChunkIndex` is always 0, so read `ChunkIndex` only with fragments on. `Warnings` is null or a list of notes about result completeness: when the vector index is only partially updated it names the assemblies whose results may be missing. Pass a warning on when your answer depends on those assemblies.

## Errors

`Error` is English text for the agent. Common cases:
- Empty `fqn` / `query`, `textSearch` query shorter than 3 characters, `ragSearch` `limit` below 1, or a `ragSearch` query longer than the model context ("Shorten the query"): fix the arguments and call again.
- "cancelled or timed out": retry the same call once.
- `ragSearch` reports that the vector index does not exist, has no manifest, or is incompatible with the server's model: the server checks the index once at startup and keeps that state until restart. Semantic search is unavailable; tell the user and fall back to `textSearch` and the catalog. Don't invent a rebuild procedure.

## Empty results

`find`:
- `Matches: []` after you verified the `FQN:` and the member line (and `Base`/`Implements`): the pair isn't in the index. Before that check, don't claim absence.

`usedBy` has three distinct empty results:
1. `Matches: []` — the target FQN isn't in the index.
2. `Matches` present, `Members: []` — no name in `memberNames` matched a declared member (wrong owner or name).
3. A member row with `UsedBy: []`, `UsedByTotal: 0` — member is indexed but has no static edges.

`usedBy` with empty `memberNames` answers a different question: where the type appears in signatures, fields, bases and attributes. It is not a creator list or call list, and its `UsedByTotal` differs from member-level totals; don't mix them.

`textSearch`: `Error` null and `Matches: []` means the substring isn't in the index (it respects `ignoreCase`; retry with `ignoreCase: true` before concluding a differently-cased name doesn't exist). `Base`/`Implements` traversal doesn't apply.

`ragSearch`: `Error` null and `Matches: []` means no close fragments, unless `Warnings` names the assembly you expected. `Source` is an index window, not the full declaration.

## Operators

`usedBy` and `find` accept the catalog string (`operator -`) or the metadata name (`op_*`).

| Token | Metadata name |
|---|---|
| `+` | binary `op_Addition`, unary `op_UnaryPlus` |
| `-` | binary `op_Subtraction`, unary `op_UnaryNegation` |
| `*` `/` `%` | `op_Multiply` `op_Division` `op_Modulus` |
| `&` `\|` `^` | `op_BitwiseAnd` `op_BitwiseOr` `op_ExclusiveOr` |
| `<<` `>>` `>>>` | `op_LeftShift` `op_RightShift` `op_UnsignedRightShift` |
| `==` `!=` | `op_Equality` `op_Inequality` |
| `<` `>` `<=` `>=` | `op_LessThan` `op_GreaterThan` `op_LessThanOrEqual` `op_GreaterThanOrEqual` |
| `!` `~` | `op_LogicalNot` `op_OnesComplement` |
| `++` `--` | `op_Increment` `op_Decrement` |
| `true` `false` | `op_True` `op_False` |
| implicit / explicit | `op_Implicit` / `op_Explicit` |

- `operator -` and `operator +` return the unary and binary forms together; distinguish them by `Signature`. To pick one arity, use `op_UnaryNegation` / `op_Subtraction` or `op_UnaryPlus` / `op_Addition`.
- In `find`, copy `implicit operator` / `explicit operator` with the target type exactly as in the catalog signature (`implicit operator BPMSoft.Core.Display`); don't shorten it.
- Never pass `op_Checked*`: the catalog writes checked operators as plain `operator …`.
- Don't invent other `op_*` names.

## Who creates instances

No single call answers it; combine:
1. `usedBy` with the constructor name from the catalog, e.g. `memberNames: ["CompositeObject"]`. Not `null`.
2. Factories, deserialization, `Activator.CreateInstance` and casts separately: `find` on suspicious consumers from step 1, or `usedBy` on a known factory method.
3. Tell constructor overloads apart by `Signature` on member rows that share one `Name`.

## Limits of the index

- `usedBy` is a static graph of the indexed assemblies, not an IDE find-all-references. BCL and `Unresolved` references aren't in it.
- `textSearch` searches declaration text in the index, not the catalog markdown. `ragSearch` doesn't replace `textSearch` for known literals.
- `ragSearch` exists only when the server enables semantic search. Without it, search by likely identifiers with `textSearch`.
- Snapshots of different TFMs aren't interchangeable; check the `FQN:` line and the member line for each type you plan to call, in the file whose `targetFramework` matches the question.
